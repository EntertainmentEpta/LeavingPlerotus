using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class PlayerM : MonoBehaviour
{
    [Header("Referências")]
    public DashM dashScript;
    public PrimaryAttackKnife attackScript;

    [Header("Controles de Movimento")]
    public KeyCode keyUp = KeyCode.W;
    public KeyCode keyDown = KeyCode.S;
    public KeyCode keyLeft = KeyCode.A;
    public KeyCode keyRight = KeyCode.D;

    [Header("Movement Settings")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 11f;
    public float rotationSpeed = 12f;
    public float acceleration = 25f; 

    [Header("Hitbox Window Settings (O Ajuste Fino)")]
    [Tooltip("Velocidade FÍSICA durante o impacto. 0 = O boneco para no lugar. 5 = O boneco desliza.")]
    public float hitboxMoveSpeed = 4.5f; 

    [Tooltip("Rotação durante o impacto. 0 = Não vira. 2 = Vira devagar.")]
    public float hitboxRotationSpeed = 8f;

    [Tooltip("Velocidade da ANIMAÇÃO durante o impacto. 1 = Normal. 0.1 = Câmera Lenta (Matrix).")]
    public float hitboxAnimSpeed = 1f;

    [Header("Subida de Degraus e Raízes (Step Offset)")]
    [Tooltip("Altura máxima de degraus, raízes e pequenas elevações de piso que o player sobe suavemente.")]
    public float maxStepHeight = 0.35f;
    [Tooltip("Distância à frente do pé para checagem de degraus.")]
    public float stepCheckDistance = 0.45f;
    [Tooltip("Velocidade de subida suave sobre o degrau.")]
    public float stepSmoothSpeed = 8f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public LayerMask whatIsGround;
    public float groundRadius = 0.2f;
    private bool grounded;

    [Header("Animation")]
    public Animator animator;

    private Rigidbody rb;
    private PlayerAttributesDefensive playerAttributes;
    private Vector3 moveDirection;
    private float targetSpeed;
    private float currentRotationSpeed;
    private bool isPlasmaActionLocked;
    private float plasmaMoveSpeedMultiplier = 1f;
    private Vector3 plasmaAimDirection;
    private Vector3 lastSafeGroundedPos;
    private bool hasLastSafeGroundedPos;
    private bool hasSeenNavMesh;

    public bool IsPlasmaActionLocked => isPlasmaActionLocked;
    public Vector3 MoveDirection => moveDirection;

    [HideInInspector]
    public float debuffSpeedMultiplier = 1.0f;

    private float temporarySpeedBoostMultiplier = 1.0f;
    private float temporarySpeedBoostDuration = 0f;

    public void ApplyTemporarySpeedBoost(float multiplier, float duration)
    {
        temporarySpeedBoostMultiplier = Mathf.Max(temporarySpeedBoostMultiplier, multiplier);
        temporarySpeedBoostDuration = Mathf.Max(temporarySpeedBoostDuration, duration);
    }

    [Header("Running Footstep SFX")]
    [Tooltip("Som de passos ao correr. Arraste o AudioClip Running aqui.")]
    public AudioClip runningFootstepClip;

    [Tooltip("Volume dos passos (0.0 a 1.0)")]
    [Range(0f, 1f)]
    public float footstepVolume = 0.60f;

    private AudioSource footstepSource;
    private bool isFootstepPlaying = false;
    private void OnEnable()
    {
        isPlasmaActionLocked = false;
        plasmaMoveSpeedMultiplier = 1f;
        UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>() ?? GetComponentInParent<UnityEngine.AI.NavMeshAgent>() ?? GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        // SEGURANÇA NAVMESH: O jogador usa Rigidbody (linearVelocity).
        // Desativa updatePosition e updateRotation no NavMeshAgent para nunca criar paredes invisíveis em portas.
        UnityEngine.AI.NavMeshAgent navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>() ?? GetComponentInParent<UnityEngine.AI.NavMeshAgent>() ?? GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }
        if (attackScript == null) attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>();
        if (dashScript == null) dashScript = GetComponent<DashM>() ?? GetComponentInChildren<DashM>() ?? GetComponentInParent<DashM>();
        
        // Buscar PlayerAttributesDefensive
        playerAttributes = GetComponent<PlayerAttributesDefensive>() ?? GetComponentInParent<PlayerAttributesDefensive>() ?? GetComponentInChildren<PlayerAttributesDefensive>();

        // Buscar Animator
        if (animator == null)
        {
            Player_WeaponManager wm = GetComponent<Player_WeaponManager>() ?? GetComponentInParent<Player_WeaponManager>();
            if (wm != null && wm.playerAnimator != null && wm.playerAnimator.isActiveAndEnabled)
            {
                animator = wm.playerAnimator;
            }
            else
            {
                animator = GetComponentInChildren<Animator>(false) ?? GetComponentInParent<Animator>();
            }
        }
        // Setup do AudioSource para passos (loop contínuo)
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.clip = runningFootstepClip;
        footstepSource.loop = true;
        footstepSource.playOnAwake = false;
        footstepSource.volume = footstepVolume;
        footstepSource.spatialBlend = 0f; // Som 2D

        // Garante que o KeybindApplier esteja presente para aplicar as teclas configuradas
        if (GetComponent<KeybindApplier>() == null)
        {
            gameObject.AddComponent<KeybindApplier>();
        }
        ReloadKeybinds();
    }

    public void ReloadKeybinds()
    {
        keyUp = KeybindApplier.ParseKey("KeyUp", "W");
        keyDown = KeybindApplier.ParseKey("KeyDown", "S");
        keyLeft = KeybindApplier.ParseKey("KeyLeft", "A");
        keyRight = KeybindApplier.ParseKey("KeyRight", "D");
    }

    private void Update()
    {
        if (temporarySpeedBoostDuration > 0f)
        {
            temporarySpeedBoostDuration -= Time.deltaTime;
            if (temporarySpeedBoostDuration <= 0f)
            {
                temporarySpeedBoostMultiplier = 1.0f;
            }
        }

        bool isUIBlocking = DialogManager.BlocksGameplayInput || 
                            GamePause.IsPaused || 
                            (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) || 
                            (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen);

        if (isUIBlocking)
        {
            if (footstepSource != null && footstepSource.isPlaying)
            {
                footstepSource.Stop();
                isFootstepPlaying = false;
            }
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetFloat("Speed", 0f);
            }
            return;
        }

        // Pressione K para imprimir diagnósticos de movimentação no console do Unity
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.LogWarning($"=== DIAGNÓSTICO DE MOVIMENTAÇÃO ===");
            Debug.LogWarning($"[PlayerM] Script Ativo: {enabled}, Objeto: {gameObject.name}, Ativo na Hierarquia: {gameObject.activeInHierarchy}");
            Debug.LogWarning($"[PlayerM] targetSpeed: {targetSpeed}, debuffSpeedMultiplier: {debuffSpeedMultiplier}, speedMultiplier: {(playerAttributes != null ? playerAttributes.speedMultiplier : 1f)}");
        }

        // Bloquear rotação e movimentação manual se o Ultimate estiver ativo
        PlayerUltimate ult = GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        if (ult != null && ult.IsUltimateActive()) return;

        if (groundCheck != null) grounded = Physics.CheckSphere(groundCheck.position, groundRadius, whatIsGround);

        MyInput();
        LookAtMoveDirection();
        UpdateAnimations();
        UpdateFootstepSound();
    }

    private void FixedUpdate()
    {
        if ((CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) || 
            (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen))
        {
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            EnforceBoundsAndVoidRescue();
            return;
        }

        PlayerUltimate ult = GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        if (ult != null && ult.IsUltimateActive())
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            EnforceBoundsAndVoidRescue();
            return;
        }

        if (dashScript != null && dashScript.isDashing)
        {
            EnforceBoundsAndVoidRescue();
            return;
        }

        MovePlayer();
        HandleStepClimbing();
        EnforceBoundsAndVoidRescue();
    }

    public void SetPlasmaFiring(bool isFiring, float speedMultiplier = 0.85f)
    {
        isPlasmaActionLocked = isFiring;
        plasmaMoveSpeedMultiplier = isFiring ? Mathf.Clamp(speedMultiplier, 0f, 1.5f) : 1f;
    }

    public void SetPlasmaAimDirection(Vector3 direction)
    {
        plasmaAimDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
    }

    private void MyInput()
    {
        // Permite movimentação fluida durante o disparo da pistola (strafing)

        // === LÓGICA MANUAL DE MOVIMENTO (Substitui o GetAxisRaw) ===
        float horizontal = 0f;
        if (Input.GetKey(keyRight)) horizontal += 1f;
        if (Input.GetKey(keyLeft)) horizontal -= 1f;

        float vertical = 0f;
        if (Input.GetKey(keyUp)) vertical += 1f;
        if (Input.GetKey(keyDown)) vertical -= 1f;

        moveDirection = new Vector3(horizontal, 0, vertical).normalized;

        bool inDamageWindow = attackScript != null && attackScript.isHitboxActive;

        float attrMult = (playerAttributes != null) ? playerAttributes.speedMultiplier : 1f;
        float boostMult = (temporarySpeedBoostDuration > 0f) ? temporarySpeedBoostMultiplier : 1.0f;
        float totalSpeedMult = attrMult * debuffSpeedMultiplier * boostMult;

        if (inDamageWindow)
        {
            targetSpeed = hitboxMoveSpeed * totalSpeedMult; 
            currentRotationSpeed = hitboxRotationSpeed;
        }
        else
        {
            // Ao atirar com arma à distância ou carregar Glaive, aplica o multiplicador de velocidade da ação (0.85 Pistola, 0.10 Glaive)
            float firingPenalty = isPlasmaActionLocked ? plasmaMoveSpeedMultiplier : 1.0f;
            targetSpeed = sprintSpeed * totalSpeedMult * firingPenalty;
            currentRotationSpeed = rotationSpeed;
        }
    }

    private void MovePlayer()
    {
        bool isAttacking = attackScript != null && attackScript.isAttacking;
        bool inDamageWindow = attackScript != null && attackScript.isHitboxActive;
        
        Vector3 currentVelocity = rb.linearVelocity; 
        if (isAttacking)
        {
            rb.linearVelocity = Vector3.Lerp(
                currentVelocity,
                new Vector3(0f, currentVelocity.y, 0f),
                5f * Time.fixedDeltaTime);
            return;
        }

        Vector3 targetVelocity = moveDirection * targetSpeed;

        if (inDamageWindow)
        {
            if (moveDirection != Vector3.zero)
            {
                // Aplicação direta (sem inércia) para controle preciso durante o golpe
                rb.linearVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);
            }
            else
            {
                // Se não há input, deixa a inércia (do lunge) desacelerar suavemente
                rb.linearVelocity = Vector3.Lerp(currentVelocity, new Vector3(0f, currentVelocity.y, 0f), 5f * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Movimento suave normal (com frenagem rápida quando fortemente desacelerado pelo charge)
            float blendRate = (isPlasmaActionLocked && plasmaMoveSpeedMultiplier <= 0.15f)
                ? Mathf.Max(acceleration, 40f)
                : acceleration;
            Vector3 newVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);
            rb.linearVelocity = Vector3.Lerp(currentVelocity, newVelocity, blendRate * Time.fixedDeltaTime);
        }
    }

    private void LookAtMoveDirection()
    {
        if (attackScript != null && attackScript.isAttacking)
        {
            Vector3 aimAssistDirection = attackScript.GetAimAssistDirection();
            if (aimAssistDirection.sqrMagnitude > 0.0001f)
            {
                float blend = 1f - Mathf.Exp(-attackScript.aimAssistSmoothing * Time.deltaTime);
                Quaternion targetRotation = Quaternion.LookRotation(aimAssistDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            }
            return;
        }

        if (isPlasmaActionLocked && plasmaAimDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion aimRotation = Quaternion.LookRotation(plasmaAimDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, aimRotation, rotationSpeed * Time.deltaTime);
            return;
        }

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimations()
    {
        Player_WeaponManager wm = GetComponent<Player_WeaponManager>() ?? GetComponentInParent<Player_WeaponManager>();
        if (wm != null && wm.playerAnimator != null && wm.playerAnimator.isActiveAndEnabled)
        {
            animator = wm.playerAnimator;
        }
        else if (animator == null || !animator.isActiveAndEnabled)
        {
            animator = GetComponentInChildren<Animator>(false) ?? GetComponentInParent<Animator>();
        }

        if (animator == null || !animator.isActiveAndEnabled) return;
        
        try
        {
            bool inDamageWindow = attackScript != null && attackScript.isHitboxActive;

            if (inDamageWindow)
            {
                animator.speed = hitboxAnimSpeed; 

                if (hitboxMoveSpeed < 0.1f)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }
            else
            {
                animator.speed = 1f; 

                // Lógica de pernas correndo/paradas (baseada no input de movimento, zerando quando imobilizado pelo carregamento)
                bool isImmobilizedByCharge = isPlasmaActionLocked && plasmaMoveSpeedMultiplier <= 0.01f;
                float moveMagnitude = moveDirection.sqrMagnitude;
                float speedParam = (!isImmobilizedByCharge && moveMagnitude > 0.01f) ? 1f : 0f;

                animator.SetFloat("Speed", speedParam);
            }
        }
        catch (System.Exception)
        {
            // Evita crashar
        }
    }

    private void UpdateFootstepSound()
    {
        if (footstepSource == null || runningFootstepClip == null) return;

        bool isImmobilizedByCharge = isPlasmaActionLocked && plasmaMoveSpeedMultiplier <= 0.01f;
        bool isMoving = !isImmobilizedByCharge && moveDirection.sqrMagnitude > 0.01f;
        bool isDashing = dashScript != null && dashScript.isDashing;
        bool isAttacking = attackScript != null && attackScript.isHitboxActive;

        // Verifica se o player está morto
        PlayerHealth health = GetComponent<PlayerHealth>();
        bool isDead = health != null && health.isDead;

        bool shouldPlay = isMoving && !isDashing && !isAttacking && !isDead;

        if (shouldPlay && !isFootstepPlaying)
        {
            footstepSource.clip = runningFootstepClip;
            footstepSource.volume = (footstepVolume * 1.25f) * GlobalAudioManager.SFXVolumeMultiplier;
            if (footstepSource.volume > 0.001f)
            {
                footstepSource.Play();
                isFootstepPlaying = true;
            }
        }
        else if (shouldPlay && isFootstepPlaying)
        {
            footstepSource.volume = (footstepVolume * 1.25f) * GlobalAudioManager.SFXVolumeMultiplier;
        }
        else if (!shouldPlay && isFootstepPlaying)
        {
            footstepSource.Stop();
            isFootstepPlaying = false;
        }
    }

    /// <summary>
    /// Detecta e sobe pequenos degraus, raízes e desníveis de malha 3D sem prender o jogador.
    /// </summary>
    private void HandleStepClimbing()
    {
        if (moveDirection.sqrMagnitude < 0.01f || rb == null) return;

        Vector3 moveDirNorm = moveDirection.normalized;
        Vector3 footOrigin = transform.position + Vector3.up * 0.05f;
        Vector3 stepOrigin = transform.position + Vector3.up * maxStepHeight;

        int mask = (whatIsGround.value != 0) ? (whatIsGround.value | (1 << 0)) : ~0;

        // Raio inferior: detecta se os pés bateram em algum obstáculo/degrau
        if (Physics.Raycast(footOrigin, moveDirNorm, out RaycastHit footHit, stepCheckDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (footHit.collider != null && !footHit.collider.isTrigger && footHit.collider.gameObject != gameObject && !footHit.collider.CompareTag("Player"))
            {
                // Raio superior: verifica se na altura do degrau o espaço está LIVRE (não é parede alta)
                if (!Physics.Raycast(stepOrigin, moveDirNorm, stepCheckDistance + 0.15f, mask, QueryTriggerInteraction.Ignore))
                {
                    // Garante que existe superfície caminhável adiante (evita escalar bordas de parede/vazio)
                    Vector3 landProbeOrigin = transform.position + moveDirNorm * (stepCheckDistance + 0.25f) + Vector3.up * (maxStepHeight + 0.5f);
                    if (Physics.Raycast(landProbeOrigin, Vector3.down, out RaycastHit landHit, maxStepHeight + 0.8f, mask, QueryTriggerInteraction.Ignore) &&
                        landHit.collider != null && !landHit.collider.isTrigger && landHit.normal.y >= 0.6f)
                    {
                        bool navValid = !(LevelGenerator.Instance != null && LevelGenerator.Instance.useNavMesh && LevelGenerator.Instance.useNavMeshConstraint && hasSeenNavMesh) ||
                                        NavMesh.SamplePosition(landHit.point, out _, 1.0f, NavMesh.AllAreas);
                        if (navValid)
                        {
                            // Espaço livre! Aplica elevação suave para deslizar sobre o degrau
                            float climbAmount = (footHit.point.y + maxStepHeight) - transform.position.y;
                            if (climbAmount > 0.01f && climbAmount <= maxStepHeight + 0.15f)
                            {
                                rb.position += Vector3.up * (stepSmoothSpeed * Time.fixedDeltaTime);
                                rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, 0.4f), rb.linearVelocity.z);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Mantém o jogador dentro dos limites do NavMesh/piso em salas procedurais (ex.: salas de transição sem paredes)
    /// e resgata para a última posição segura caso caia no vazio.
    /// </summary>
    private void EnforceBoundsAndVoidRescue()
    {
        if (rb == null) return;

        // Salas procedurais usam Default (layer 0) além de Ground (layer 3); usa ~0 filtrando entidades/triggers no RaycastAll.
        int floorMask = ~((1 << 2) | (1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10));
        bool hasFloorBelow = TryGetWalkableFloorBelow(transform.position, 1.8f, floorMask, out _);
        bool navConstraintActive = LevelGenerator.Instance != null && LevelGenerator.Instance.useNavMesh && LevelGenerator.Instance.useNavMeshConstraint;
        bool hasNavMeshNear = NavMesh.SamplePosition(transform.position, out _, 1.25f, NavMesh.AllAreas);
        if (hasNavMeshNear) hasSeenNavMesh = true;

        if (hasFloorBelow && (!navConstraintActive || !hasSeenNavMesh || hasNavMeshNear))
        {
            lastSafeGroundedPos = transform.position;
            hasLastSafeGroundedPos = true;
        }

        if (navConstraintActive && hasSeenNavMesh)
        {
            bool insideTightNav = NavMesh.SamplePosition(transform.position, out _, 0.75f, NavMesh.AllAreas);
            if ((!insideTightNav || !hasFloorBelow) && NavMesh.SamplePosition(transform.position, out NavMeshHit closestNav, 6.0f, NavMesh.AllAreas))
            {
                Vector2 curXZ = new Vector2(transform.position.x, transform.position.z);
                Vector2 navXZ = new Vector2(closestNav.position.x, closestNav.position.z);
                float xzDist = Vector2.Distance(curXZ, navXZ);

                if (!hasFloorBelow || xzDist > 0.75f)
                {
                    // Se ainda há piso válido, mantém rb.position.y intacto para não interferir na colisão física com a malha.
                    bool shouldRescueY = !hasFloorBelow && transform.position.y < closestNav.position.y - 0.5f;
                    float targetY = shouldRescueY ? closestNav.position.y + 0.1f : rb.position.y;
                    Vector3 clampedPos = new Vector3(closestNav.position.x, targetY, closestNav.position.z);
                    Vector3 outward = transform.position - clampedPos;
                    outward.y = 0f;

                    if (xzDist > 0.02f || shouldRescueY)
                    {
                        rb.position = clampedPos;
                    }

                    Vector3 vel = rb.linearVelocity;
                    if (outward.sqrMagnitude > 0.0001f)
                    {
                        Vector3 outwardNorm = outward.normalized;
                        float outwardSpeed = Vector3.Dot(new Vector3(vel.x, 0f, vel.z), outwardNorm);
                        if (outwardSpeed > 0f)
                        {
                            vel -= outwardNorm * outwardSpeed;
                        }
                    }
                    if (shouldRescueY && vel.y < 0f) vel.y = 0f;
                    rb.linearVelocity = vel;
                    return;
                }
            }
        }

        if (hasLastSafeGroundedPos && !TryGetWalkableFloorBelow(transform.position, 8.0f, floorMask, out _) && transform.position.y < lastSafeGroundedPos.y - 2.5f)
        {
            rb.position = lastSafeGroundedPos + Vector3.up * 0.1f;
            rb.linearVelocity = Vector3.zero;
        }
    }

    private bool TryGetWalkableFloorBelow(Vector3 pos, float maxDistance, int mask, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 0.5f, Vector3.down, maxDistance + 0.5f, mask, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.isTrigger) continue;
            if (col.gameObject.layer == 2) continue; // Ignore Raycast
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
            if (col.CompareTag("Player") || col.CompareTag("Enemy")) continue;
            if (hits[i].normal.y < 0.45f) continue;

            if (hits[i].distance < closestDist)
            {
                closestDist = hits[i].distance;
                bestHit = hits[i];
                found = true;
            }
        }

        return found;
    }
}