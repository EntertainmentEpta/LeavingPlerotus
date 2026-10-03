using UnityEngine;
using System.Collections;

/// <summary>
/// Peixe AI. States: Chase → Shoot → Defend → Stunned → Chase
/// Aquatic enemy: chases the player, shoots "Espinho" projectiles from range,
/// inflates defensively at melee range (invulnerable + contact damage),
/// then becomes stunned and vulnerable.
/// Smooth movement via linearVelocity on the Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PeixeAI : MonoBehaviour
{
    // ── References ──────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Referência ao transform do jogador. Se não atribuída, busca pela tag 'Player' no Start.")]
    public Transform player;

    [Tooltip("Prefab do projétil 'Espinho' disparado no estado Shoot.")]
    public GameObject espinhoPrefab;

    [Tooltip("Transform de onde o projétil é instanciado (ponto de disparo).")]
    public Transform firePoint;

    // ── Distances ───────────────────────────────────────────────────
    [Header("Distances")]
    [Tooltip("Distância em que o Peixe para de perseguir e começa a atirar.")]
    public float shootDistance = 10f;

    [Tooltip("Distância de corpo-a-corpo que ativa o estado Defend.")]
    public float defendDistance = 3f;

    [Tooltip("Distância máxima para começar a perseguir o jogador.")]
    public float chaseDistance = 25f;

    // ── Speeds ──────────────────────────────────────────────────────
    [Header("Speeds")]
    [Tooltip("Velocidade de perseguição do Peixe.")]
    public float chaseSpeed = 4.5f;

    [Tooltip("Aceleração do movimento (maior = mais responsivo, sem 'patinar').")]
    public float acceleration = 10.0f;

    // ── Shoot ───────────────────────────────────────────────────────
    [Header("Ranged - Espinho")]
    [Tooltip("Velocidade do projétil Espinho ao ser disparado.")]
    public float projectileSpeed = 14f;

    [Tooltip("Tempo de recarga entre disparos de Espinho (segundos).")]
    public float shootCooldown = 3.0f;

    [Tooltip("Offset vertical adicionado à posição do jogador para mirar no torso em vez dos pés.")]
    public float playerHeightOffset = 1.5f;

    // ── Defend ──────────────────────────────────────────────────────
    [Header("Defend")]
    [Tooltip("Duração do estado Defend em segundos.")]
    public float defendDuration = 3.0f;

    [Tooltip("Multiplicador de escala durante o Defend (2 = dobro do tamanho).")]
    public float defendScaleMultiplier = 2.0f;

    [Tooltip("Velocidade do Lerp de escala (maior = mais rápido).")]
    public float scaleLerpSpeed = 4.0f;

    [Tooltip("Cor do material durante o estado Defend.")]
    public Color defendColor = Color.red;

    [Tooltip("Dano de contato aplicado ao jogador durante o estado Defend.")]
    public int contactDamage = 20;

    [Header("Envenenamento (Poison)")]
    [Tooltip("Dano por tick do veneno (1 tick = 1 segundo).")]
    public int poisonDamagePerTick = 3;

    [Tooltip("Redução de velocidade durante o veneno (0.20 = 20%).")]
    public float poisonSlow = 0.20f;

    [Tooltip("Duração do envenenamento em segundos.")]
    public float poisonDuration = 4f;

    // ── Stunned ─────────────────────────────────────────────────────
    [Header("Stunned")]
    [Tooltip("Duração do estado Stunned em segundos.")]
    public float stunnedDuration = 2.0f;

    [Tooltip("Cor do material durante o estado Stunned (feedback de vulnerabilidade).")]
    public Color stunnedColor = Color.blue;

    // ── Public State ────────────────────────────────────────────────
    [Header("Estado")]
    [Tooltip("Quando true, o Peixe não recebe dano (ativo durante Defend).")]
    public bool isInvulnerable = false;

    // ── Private ─────────────────────────────────────────────────────
    private Rigidbody rb;
    private Renderer meshRenderer;
    private Color originalColor;
    private Vector3 originalScale;
    private Vector3 currentVelocity = Vector3.zero;

    private float lastShootTime = -999f;
    private Vector3 spawnPosition; // posição inicial para safety net

    /// <summary>Velocidade máxima permitida para evitar que o physics solver arremesse o Peixe.</summary>
    private const float MAX_VELOCITY = 20f;
    /// <summary>Distância máxima do spawn antes de acionar o safety net.</summary>
    private const float MAX_DISTANCE_FROM_SPAWN = 60f;

    private bool registeredInBestiary = false;

    // Simple State Machine
    private enum State { Idle, Chase, Shoot, Defend, Stunned }
    private State currentState = State.Idle;

    // Flags para estados temporizados
    private bool isDefending = false;
    private bool isStunned = false;

    // ── Cached scale targets ────────────────────────────────────────
    private Vector3 defendScale;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }

        originalScale = transform.localScale;
        defendScale = originalScale * defendScaleMultiplier;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        spawnPosition = transform.position;
    }

    // ─────────────────────────────────────────────────────────────────
    void Update()
    {
        if (player == null) return;

        // Durante Defend ou Stunned, o Lerp de escala continua no Update
        if (isDefending)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                defendScale,
                scaleLerpSpeed * Time.deltaTime
            );
        }
        else if (isStunned)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                originalScale,
                scaleLerpSpeed * Time.deltaTime
            );
        }
    }

    void FixedUpdate()
    {
        if (player == null) return;

        // ── Clamp de velocidade: previne que o physics solver lance o Peixe ──
        if (rb.linearVelocity.sqrMagnitude > MAX_VELOCITY * MAX_VELOCITY)
        {
            Debug.LogWarning($"[PeixeAI] Velocidade excessiva detectada ({rb.linearVelocity.magnitude:F1} m/s)! Clamping para {MAX_VELOCITY}.");
            rb.linearVelocity = rb.linearVelocity.normalized * MAX_VELOCITY;
        }

        // ── Safety net: reposiciona se saiu dos limites (qualquer eixo) ──
        float distFromSpawn = Vector3.Distance(transform.position, spawnPosition);
        if (transform.position.y < spawnPosition.y - 10f || distFromSpawn > MAX_DISTANCE_FROM_SPAWN)
        {
            Debug.LogWarning($"[PeixeAI] Safety net! Peixe fora dos limites (pos={transform.position}, dist={distFromSpawn:F1}). Reposicionando.");
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.position = spawnPosition;
        }

        float dist = Vector3.Distance(transform.position, player.position);
        UpdateState(dist);
        ExecuteMovement();

        // Disparo contínuo enquanto estiver no estado Shoot
        if (currentState == State.Shoot)
        {
            TryShoot();
        }
    }

    // ── State Machine ───────────────────────────────────────────────
    void UpdateState(float dist)
    {
        // Defend e Stunned são temporizados — não podem ser interrompidos por distância
        if (isDefending || isStunned) return;

        // Priority: Defend > Shoot > Chase > Idle
        if (dist <= defendDistance)       ChangeState(State.Defend);
        else if (dist <= shootDistance)    ChangeState(State.Shoot);
        else if (dist <= chaseDistance)    ChangeState(State.Chase);
        else                              ChangeState(State.Idle);
    }

    void ChangeState(State newState)
    {
        if (currentState == newState) return;

        // Register in Bestiary the first time it leaves Idle
        if (!registeredInBestiary && currentState == State.Idle && newState != State.Idle)
        {
            registeredInBestiary = true;
            EnemyIdentity id = GetComponent<EnemyIdentity>()
                            ?? GetComponentInChildren<EnemyIdentity>()
                            ?? GetComponentInParent<EnemyIdentity>();
            if (id != null && BestiarioManager.instancia != null)
                BestiarioManager.instancia.Registrar(id);
        }

        State previousState = currentState;
        currentState = newState;

        // Iniciar lógica de estados temporizados
        switch (newState)
        {
            case State.Shoot:
                // Disparo imediato ao entrar no estado (reseta cooldown para atirar já)
                lastShootTime = -999f;
                break;
            case State.Defend:
                StartCoroutine(DefendRoutine());
                break;
        }
    }

    // ── Movement ────────────────────────────────────────────────────
    void ExecuteMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        switch (currentState)
        {
            case State.Chase:
                targetVelocity = DirectionTo(player.position) * chaseSpeed;
                break;

            case State.Shoot:
            case State.Defend:
            case State.Stunned:
            case State.Idle:
            default:
                targetVelocity = Vector3.zero;
                break;
        }

        // Suaviza a aceleração de forma responsiva no chão
        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            new Vector3(targetVelocity.x, 0, targetVelocity.z),
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);

        // Rotaciona em direção ao jogador quando parado; na direção do movimento quando se movendo
        if (currentVelocity.sqrMagnitude > 0.15f)
        {
            LookAtDirection(currentVelocity.normalized);
        }
        else if (player != null)
        {
            LookAtPlayer();
        }
    }

    // ── Shoot ───────────────────────────────────────────────────────
    void TryShoot()
    {
        if (Time.time >= lastShootTime + shootCooldown)
        {
            ShootEspinho();
            lastShootTime = Time.time;
        }
    }

    void ShootEspinho()
    {
        if (espinhoPrefab == null || firePoint == null) return;

        // Posição ajustada para mirar no torso do jogador, não nos pés
        Vector3 targetPosition = player.position + new Vector3(0, playerHeightOffset, 0);

        // Direção para o jogador a partir do ponto de disparo
        Vector3 direction = (targetPosition - firePoint.position).normalized;

        GameObject espinho = Instantiate(espinhoPrefab, firePoint.position, Quaternion.LookRotation(direction));

        // Configura o componente Espinho (owner + velocidade)
        Espinho espinhoScript = espinho.GetComponent<Espinho>();
        if (espinhoScript != null)
        {
            espinhoScript.owner = gameObject;
            espinhoScript.speed = projectileSpeed;
        }
    }

    // ── Defend Coroutine ────────────────────────────────────────────
    IEnumerator DefendRoutine()
    {
        isDefending = true;
        isInvulnerable = true;

        // Torna kinematic para evitar que o physics solver arremesse o Peixe
        // quando o collider infla e interpenetra chão/jogador/paredes
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Feedback visual: cor vermelha
        if (meshRenderer != null)
        {
            meshRenderer.material.color = defendColor;
        }

        // Aguarda a duração do Defend (o Lerp de escala roda no Update)
        yield return new WaitForSeconds(defendDuration);

        isDefending = false;

        // Transição para Stunned (continua kinematic até o fim do Stunned)
        StartCoroutine(StunnedRoutine());
    }

    // ── Stunned Coroutine ───────────────────────────────────────────
    IEnumerator StunnedRoutine()
    {
        isStunned = true;
        isInvulnerable = false;
        currentState = State.Stunned;

        // Feedback visual: cor azul (vulnerabilidade)
        if (meshRenderer != null)
        {
            meshRenderer.material.color = stunnedColor;
        }

        // Aguarda a duração do Stunned (o Lerp de escala de volta roda no Update)
        yield return new WaitForSeconds(stunnedDuration);

        // Restaura a cor original e o scale (segurança: força o scale exato)
        if (meshRenderer != null)
        {
            meshRenderer.material.color = originalColor;
        }
        transform.localScale = originalScale;

        // Restaura a física agora que o scale voltou ao normal
        rb.isKinematic = false;

        isStunned = false;
        currentState = State.Idle; // Permite que o UpdateState re-avalie no próximo FixedUpdate
    }

    // ── Contact Damage (Defend) ─────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        if (!isDefending) return;

        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(contactDamage, gameObject);

            // Aplica envenenamento (DoT + slow) no jogador ao encostar durante Defend
            PoisonedStatus.ApplyPoison(playerHealth.gameObject, poisonDamagePerTick, poisonSlow, poisonDuration);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────
    Vector3 DirectionTo(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        return dir.normalized;
    }

    void LookAtPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 8f * Time.fixedDeltaTime);
        }
    }

    void LookAtDirection(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 8f * Time.fixedDeltaTime);
        }
    }

    // ── Diagnóstico: detecta quem está destruindo o Peixe ────────────
    void OnDestroy()
    {
        Debug.LogError($"[PeixeAI] ⚠️ Peixe '{gameObject.name}' foi DESTRUÍDO! Estado: {currentState}, isDefending={isDefending}, isStunned={isStunned}, isInvulnerable={isInvulnerable}, Pos={transform.position}");
        Debug.LogError("[PeixeAI] Stack trace da destruição:\n" + System.Environment.StackTrace);
    }
}
