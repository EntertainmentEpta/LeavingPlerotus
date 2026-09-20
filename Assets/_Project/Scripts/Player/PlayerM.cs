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
    public float walkSpeed = 5f;
    public float sprintSpeed = 10f;
    public float rotationSpeed = 10f;
    public float acceleration = 15f; 

    [Header("Hitbox Window Settings (O Ajuste Fino)")]
    [Tooltip("Velocidade FÍSICA durante o impacto. 0 = O boneco para no lugar. 5 = O boneco desliza.")]
    public float hitboxMoveSpeed = 0f; 

    [Tooltip("Rotação durante o impacto. 0 = Não vira. 2 = Vira devagar.")]
    public float hitboxRotationSpeed = 0f;

    [Tooltip("Velocidade da ANIMAÇÃO durante o impacto. 1 = Normal. 0.1 = Câmera Lenta (Matrix).")]
    public float hitboxAnimSpeed = 1f;

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

    [HideInInspector]
    public float debuffSpeedMultiplier = 1.0f;

    [Header("Running Footstep SFX")]
    [Tooltip("Som de passos ao correr. Arraste o AudioClip Running aqui.")]
    public AudioClip runningFootstepClip;

    [Tooltip("Volume dos passos (0.0 a 1.0)")]
    [Range(0f, 1f)]
    public float footstepVolume = 0.5f;

    private AudioSource footstepSource;
    private bool isFootstepPlaying = false;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        if (attackScript == null) attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>();
        
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
        PlayerUltimate ult = GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        if (ult != null && ult.IsUltimateActive())
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        if (dashScript != null && dashScript.isDashing) return;
        MovePlayer();
    }

    private void MyInput()
    {
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
            targetSpeed = sprintSpeed * totalSpeedMult;
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
            footstepSource.volume = footstepVolume;
            footstepSource.Play();
            isFootstepPlaying = true;
        }
        else if (!shouldPlay && isFootstepPlaying)
        {
            footstepSource.Stop();
            isFootstepPlaying = false;
        }
    }
}