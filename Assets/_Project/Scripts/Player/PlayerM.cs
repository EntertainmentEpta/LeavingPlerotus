using UnityEngine;

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
    private Vector3 plasmaAimDirection;

    public bool IsPlasmaActionLocked => isPlasmaActionLocked;

    [HideInInspector]
    public float debuffSpeedMultiplier = 1.0f;

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
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
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
    }

    private void Update()
    {
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
            return;
        }

        PlayerUltimate ult = GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        if (ult != null && ult.IsUltimateActive())
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        if (dashScript != null && dashScript.isDashing) return;
        MovePlayer();
        HandleStepClimbing();
    }

    public void SetPlasmaFiring(bool isFiring)
    {
        isPlasmaActionLocked = isFiring;
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
        float totalSpeedMult = attrMult * debuffSpeedMultiplier;

        if (inDamageWindow)
        {
            targetSpeed = hitboxMoveSpeed * totalSpeedMult; 
            currentRotationSpeed = hitboxRotationSpeed;
        }
        else
        {
            // Ao atirar com arma de plasma, mantém 85% da velocidade para sensação tática de recuo sem paralisar o jogador
            float firingPenalty = isPlasmaActionLocked ? 0.85f : 1.0f;
            targetSpeed = sprintSpeed * totalSpeedMult * firingPenalty;
            currentRotationSpeed = rotationSpeed;
        }
    }

    private void MovePlayer()
    {
        bool inDamageWindow = attackScript != null && attackScript.isHitboxActive;
        
        Vector3 targetVelocity = moveDirection * targetSpeed;
        Vector3 currentVelocity = rb.linearVelocity; 

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
            // Movimento suave normal
            Vector3 newVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);
            rb.linearVelocity = Vector3.Lerp(currentVelocity, newVelocity, acceleration * Time.fixedDeltaTime);
        }
    }

    private void LookAtMoveDirection()
    {
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

                // Lógica de pernas correndo/paradas (baseada no input de movimento para parada instantânea)
                float moveMagnitude = moveDirection.sqrMagnitude;
                float speedParam = (moveMagnitude > 0.01f) ? 1f : 0f;

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

        bool isMoving = moveDirection.sqrMagnitude > 0.01f;
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

        int mask = (whatIsGround.value != 0) ? whatIsGround.value : ~0;

        // Raio inferior: detecta se os pés bateram em algum obstáculo/degrau
        if (Physics.Raycast(footOrigin, moveDirNorm, out RaycastHit footHit, stepCheckDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (footHit.collider != null && !footHit.collider.isTrigger && footHit.collider.gameObject != gameObject && !footHit.collider.CompareTag("Player"))
            {
                // Raio superior: verifica se na altura do degrau o espaço está LIVRE (não é parede alta)
                if (!Physics.Raycast(stepOrigin, moveDirNorm, stepCheckDistance + 0.15f, mask, QueryTriggerInteraction.Ignore))
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