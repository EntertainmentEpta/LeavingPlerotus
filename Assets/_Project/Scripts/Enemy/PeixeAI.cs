using UnityEngine;
using System.Collections;

/// <summary>
/// Peixe AI. Máquina de estados: Chase → Shoot → Defend → Stunned.
/// Inimigo aquático:
/// - A meia distância (shootDistance): para e atira espinhos no jogador.
/// - A curta distância (defendDistance): quando o jogador se aproxima para atacar,
///   infla em modo Baiacu (invulnerável + dano de contato).
/// - Ao esvaziar (Stunned): murcha de volta ao normal, fica parado e vulnerável para o jogador bater.
/// - Pós-Stun: delay antes de poder atirar ou inflar novamente.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PeixeAI : MonoBehaviour
{
    // ── Referências ──────────────────────────────────────────────────
    [Header("Referências")]
    [Tooltip("Transform do jogador. Se vazio, busca pela tag 'Player' no Start.")]
    public Transform player;

    [Tooltip("Prefab do projétil 'Espinho' disparado no estado Shoot.")]
    public GameObject espinhoPrefab;

    [Tooltip("Ponto de onde o projétil é instanciado.")]
    public Transform firePoint;

    // ── Distâncias ───────────────────────────────────────────────────
    [Header("Distâncias")]
    [Tooltip("Distância em que o Peixe para de perseguir e começa a atirar.")]
    public float shootDistance = 10f;

    [Tooltip("Distância corpo-a-corpo em que o Peixe infla em Baiacu ao ser encurralado.")]
    public float defendDistance = 2.5f;

    [Tooltip("Distância máxima para detectar e perseguir o jogador.")]
    public float chaseDistance = 25f;

    // ── Velocidades ──────────────────────────────────────────────────
    [Header("Velocidade")]
    [Tooltip("Velocidade de perseguição.")]
    public float chaseSpeed = 4.5f;

    [Tooltip("Aceleração do movimento.")]
    public float acceleration = 10.0f;

    // ── Ataque à Distância (Espinho) ─────────────────────────────────
    [Header("Disparo - Espinho")]
    [Tooltip("Velocidade do projétil.")]
    public float projectileSpeed = 14f;

    [Tooltip("Tempo de recarga entre disparos.")]
    public float shootCooldown = 2.5f;

    [Tooltip("Offset vertical na mira para mirar no torso do jogador.")]
    public float playerHeightOffset = 1.5f;

    [Tooltip("Pequeno atraso de mira antes do primeiro disparo ao entrar no range.")]
    public float aimDelay = 0.3f;

    [Tooltip("Ângulo do spread entre os 3 espinhos disparados.")]
    public float spreadAngle = 60f;

    // ── Defesa (Baiacu Inflado) ──────────────────────────────────────
    [Header("Defend (Baiacu)")]
    [Tooltip("Tempo em que permanece inflado segurando o ar.")]
    public float defendDuration = 2.5f;

    [Tooltip("Dano de contato aplicado ao jogador durante o estado Defend.")]
    public int contactDamage = 20;

    [Header("Envenenamento (Poison)")]
    [Tooltip("Dano por tick do veneno.")]
    public int poisonDamagePerTick = 3;

    [Tooltip("Redução de velocidade durante o veneno.")]
    public float poisonSlow = 0.20f;

    [Tooltip("Duração do envenenamento em segundos.")]
    public float poisonDuration = 4f;

    // ── Stunned & Vulnerabilidade ───────────────────────────────────
    [Header("Stunned & Vulnerabilidade")]
    [Tooltip("Tempo em que o peixe fica esvaziado, parado e vulnerável aos ataques do jogador.")]
    public float stunnedDuration = 2.0f;

    [Tooltip("Tempo de espera após o Stun antes de poder atirar novamente (evita tiro surpresa na cara).")]
    public float postStunShootDelay = 2.5f;

    [Tooltip("Tempo de recarga após o Stun antes de poder inflar em Baiacu novamente.")]
    public float postStunDefendCooldown = 3.5f;

    [Tooltip("Cor do material durante o Stun indicando vulnerabilidade.")]
    public Color stunnedColor = new Color(0.3f, 0.8f, 1.0f, 1.0f);

    // ── Modelos Visuais ─────────────────────────────────────────────
    [Header("Modelos Visuais")]
    [Tooltip("Modelo 3D normal (nadando e atirando).")]
    public GameObject normalMeshObject;

    [Tooltip("Modelo 3D inflado (Baiacu com espinhos).")]
    public GameObject baiacuMeshObject;

    // ── Escala Leve do Baiacu ───────────────────────────────────────
    [Header("Escala")]
    [Tooltip("Multiplicador de escala ao virar Baiacu (ex: 1.2 = cresce 20% levemente).")]
    public float baiacuScaleMultiplier = 1.2f;

    [Tooltip("Velocidade da transição de escala (inflar / desinflar).")]
    public float scaleLerpSpeed = 5f;

    // ── Flutuação e Respiração ──────────────────────────────────────
    [Header("Flutuação & Respiração")]
    [Tooltip("Ativa a flutuação aquática suave (subindo e descendo) e o efeito de respiração orgânica nas guelras/corpo.")]
    public bool enableFloatingAndBreathing = true;

    [Tooltip("Velocidade do ciclo de flutuação vertical.")]
    public float floatSpeed = 2.0f;

    [Tooltip("Altura máxima da flutuação (amplitude vertical).")]
    public float floatAmplitude = 0.08f;

    [Tooltip("Velocidade do ciclo de respiração.")]
    public float breathSpeed = 3.0f;

    [Tooltip("Intensidade da expansão lateral da respiração (ex: 0.04 = 4% de expansão).")]
    public float breathIntensity = 0.04f;

    // ── Estado Público ──────────────────────────────────────────────
    [Header("Estado")]
    public bool isInvulnerable = false;

    // ── Variáveis Privadas ──────────────────────────────────────────
    private Rigidbody rb;
    private SphereCollider sphereCollider;
    private Renderer meshRenderer;
    private Color originalColor;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private Vector3 currentVelocity = Vector3.zero;

    private Vector3 normalMeshOriginalPos;
    private Vector3 normalMeshOriginalScale;
    private Vector3 baiacuMeshOriginalPos;
    private Vector3 baiacuMeshOriginalScale;

    private float nextShootTime = 0f;
    private float nextDefendAllowedTime = 0f;
    private Vector3 spawnPosition;

    private const float MAX_VELOCITY = 15f;
    private const float MAX_DISTANCE_FROM_SPAWN = 60f;

    private bool registeredInBestiary = false;

    private enum State { Idle, Chase, Shoot, Defend, Stunned }
    private State currentState = State.Idle;

    private bool isDefending = false;
    private bool isStunned = false;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Congela rotações e eixo Y para o Peixe nunca sair voando ou tombar
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        sphereCollider = GetComponent<SphereCollider>();

        if (normalMeshObject == null)
        {
            Transform t = transform.Find("PeixeNormal");
            if (t != null) normalMeshObject = t.gameObject;
        }

        if (baiacuMeshObject == null)
        {
            Transform t = transform.Find("PeixeBaiacu");
            if (t != null) baiacuMeshObject = t.gameObject;
        }

        if (normalMeshObject != null)
        {
            normalMeshOriginalPos = normalMeshObject.transform.localPosition;
            normalMeshOriginalScale = normalMeshObject.transform.localScale;
        }

        if (baiacuMeshObject != null)
        {
            baiacuMeshOriginalPos = baiacuMeshObject.transform.localPosition;
            baiacuMeshOriginalScale = baiacuMeshObject.transform.localScale;
        }

        if (firePoint == null)
        {
            Transform fp = transform.Find("FirePoint");
            if (fp != null) firePoint = fp;
            else firePoint = transform;
        }

        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null)
        {
            originalColor = meshRenderer.material.color;
        }

        originalScale = transform.localScale;
        targetScale = originalScale;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        spawnPosition = transform.position;

        // Inicia no modo normal
        SetMeshMode(false);
    }

    /// <summary>
    /// Alterna entre o modelo normal e o modelo Baiacu.
    /// </summary>
    public void SetMeshMode(bool isBaiacu)
    {
        if (normalMeshObject != null) normalMeshObject.SetActive(!isBaiacu);
        if (baiacuMeshObject != null) baiacuMeshObject.SetActive(isBaiacu);

        // Atualiza a referência de renderer para o modelo atualmente ativo
        Renderer r = (isBaiacu ? baiacuMeshObject : normalMeshObject)?.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            meshRenderer = r;
            if (!isStunned && !isDefending)
            {
                originalColor = meshRenderer.material.color;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    void Update()
    {
        // Interpola suavemente a escala em direção ao alvo (inflar / desinflar)
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                targetScale,
                scaleLerpSpeed * Time.deltaTime
            );
        }

        // Flutuação aquática e respiração visual no modelo 3D ativo
        if (enableFloatingAndBreathing)
        {
            UpdateFloatingAndBreathing();
        }
    }

    void UpdateFloatingAndBreathing()
    {
        GameObject activeMesh = isDefending ? baiacuMeshObject : normalMeshObject;
        if (activeMesh == null) return;

        Vector3 basePos = isDefending ? baiacuMeshOriginalPos : normalMeshOriginalPos;
        Vector3 baseScale = isDefending ? baiacuMeshOriginalScale : normalMeshOriginalScale;

        // Durante Defend (segurando o ar) e Stunned (desmaiado), mantém a posição/escala base
        if (isDefending || isStunned)
        {
            activeMesh.transform.localPosition = basePos;
            activeMesh.transform.localScale = baseScale;
            return;
        }

        // 1. Subindo e descendo como se estivesse flutuando na água
        float floatY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        activeMesh.transform.localPosition = new Vector3(basePos.x, basePos.y + floatY, basePos.z);

        // 2. Distorção de respiração (expansão das guelras/corpo nos eixos X/Z e leve compressão em Y)
        float breathCycle = Mathf.Sin(Time.time * breathSpeed);
        Vector3 breathMultipliers = new Vector3(
            1f + breathCycle * breathIntensity,
            1f - breathCycle * (breathIntensity * 0.4f),
            1f + breathCycle * (breathIntensity * 0.6f)
        );
        activeMesh.transform.localScale = Vector3.Scale(baseScale, breathMultipliers);
    }

    void FixedUpdate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            if (player == null) return;
        }

        // Safety net: se foi jogado para fora dos limites, reposiciona
        float distFromSpawn = Vector3.Distance(transform.position, spawnPosition);
        if (distFromSpawn > MAX_DISTANCE_FROM_SPAWN)
        {
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
        // Durante Defend ou Stunned, o estado é temporizado e não interrompido
        if (isDefending || isStunned) return;

        bool canDefend = Time.time >= nextDefendAllowedTime;

        // 1. Curta distância (corpo-a-corpo): o jogador se aproximou para bater -> infla em Baiacu!
        if (canDefend && dist <= defendDistance)
        {
            ChangeState(State.Defend);
        }
        // 2. Média distância: para e atira
        else if (dist <= shootDistance)
        {
            ChangeState(State.Shoot);
        }
        // 3. Longa distância: persegue o jogador
        else if (dist <= chaseDistance)
        {
            ChangeState(State.Chase);
        }
        // 4. Fora de alcance: parado
        else
        {
            ChangeState(State.Idle);
        }
    }

    void ChangeState(State newState)
    {
        if (currentState == newState) return;

        // Registra no Bestiário ao sair de Idle
        if (!registeredInBestiary && currentState == State.Idle && newState != State.Idle)
        {
            registeredInBestiary = true;
            EnemyIdentity id = GetComponent<EnemyIdentity>()
                            ?? GetComponentInChildren<EnemyIdentity>()
                            ?? GetComponentInParent<EnemyIdentity>();
            if (id != null && BestiarioManager.instancia != null)
                BestiarioManager.instancia.Registrar(id);
        }

        currentState = newState;

        switch (newState)
        {
            case State.Shoot:
                // Se o cooldown já expirou, dá um pequeno tempo de mira antes do tiro
                if (Time.time >= nextShootTime)
                {
                    nextShootTime = Time.time + aimDelay;
                }
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

        // Suaviza a velocidade no plano horizontal (X/Z)
        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            new Vector3(targetVelocity.x, 0, targetVelocity.z),
            acceleration * Time.fixedDeltaTime
        );

        // Aplica velocidade garantindo Y = 0 (nunca voa)
        rb.linearVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        // Rotaciona para o jogador quando parado; para a direção do movimento quando nadando
        if (currentVelocity.sqrMagnitude > 0.15f)
        {
            LookAtDirection(currentVelocity.normalized);
        }
        else if (player != null && !isStunned)
        {
            LookAtPlayer();
        }
    }

    // ── Shoot ───────────────────────────────────────────────────────
    void TryShoot()
    {
        if (Time.time >= nextShootTime)
        {
            ShootEspinho();
            nextShootTime = Time.time + shootCooldown;
        }
    }

    void ShootEspinho()
    {
        if (espinhoPrefab == null)
        {
            espinhoPrefab = Resources.Load<GameObject>("projétil") ?? Resources.Load<GameObject>("projtil");
#if UNITY_EDITOR
            if (espinhoPrefab == null)
            {
                espinhoPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Peixe/projétil.prefab");
            }
#endif
        }
        if (espinhoPrefab == null) return;
        Transform fp = firePoint != null ? firePoint : transform;

        // Mira no torso do jogador
        Vector3 targetPosition = player.position + new Vector3(0, playerHeightOffset, 0);
        Vector3 direction = (targetPosition - fp.position).normalized;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        Quaternion centerRotation = Quaternion.LookRotation(direction);

        // Spread: tiro central + 2 tiros laterais
        float[] angles = { 0f, -spreadAngle, spreadAngle };
        Collider[] peixeCols = GetComponentsInChildren<Collider>();

        foreach (float angle in angles)
        {
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up) * centerRotation;
            Vector3 spawnPos = fp.position + (rot * Vector3.forward) * 0.85f;
            GameObject espinho = Instantiate(espinhoPrefab, spawnPos, rot);

            Espinho espinhoScript = espinho.GetComponent<Espinho>();
            if (espinhoScript != null)
            {
                espinhoScript.owner = gameObject;
                espinhoScript.speed = projectileSpeed;
            }

            Collider espCol = espinho.GetComponent<Collider>();
            if (espCol != null && peixeCols != null)
            {
                foreach (var pCol in peixeCols)
                {
                    if (pCol != null) Physics.IgnoreCollision(pCol, espCol, true);
                }
            }
        }
    }

    // ── Defend Coroutine (Segurando o ar) ───────────────────────────
    IEnumerator DefendRoutine()
    {
        isDefending = true;
        isInvulnerable = true;
        currentState = State.Defend;

        rb.linearVelocity = Vector3.zero;
        currentVelocity = Vector3.zero;

        // Vira Baiacu e infla levemente
        SetMeshMode(true);
        targetScale = originalScale * baiacuScaleMultiplier;

        // Aguarda a duração do Defend segurando o ar
        yield return new WaitForSeconds(defendDuration);

        isDefending = false;

        // Transição imediata para o Stun (o peixe esvazia e fica vulnerável!)
        StartCoroutine(StunnedRoutine());
    }

    // ── Stunned Coroutine (Esvaziando e Vulnerável) ─────────────────
    IEnumerator StunnedRoutine()
    {
        isStunned = true;
        isInvulnerable = false; // VULNERÁVEL: player pode bater!
        currentState = State.Stunned;

        rb.linearVelocity = Vector3.zero;
        currentVelocity = Vector3.zero;

        // Esvazia imediatamente: volta ao modelo normal e escala original
        SetMeshMode(false);
        targetScale = originalScale;

        // Feedback visual claro: cor azul/ciano de vulnerabilidade
        if (meshRenderer != null)
        {
            meshRenderer.material.color = stunnedColor;
        }

        // Fica totalmente PARADO durante o Stun para o jogador poder bater
        yield return new WaitForSeconds(stunnedDuration);

        // Restaura a cor original
        if (meshRenderer != null)
        {
            meshRenderer.material.color = originalColor;
        }

        rb.linearVelocity = Vector3.zero;
        currentVelocity = Vector3.zero;
        isStunned = false;

        // Delays cruciais para não atirar na cara do jogador nem inflar de novo imediatamente
        nextShootTime = Time.time + postStunShootDelay;
        nextDefendAllowedTime = Time.time + postStunDefendCooldown;

        currentState = State.Idle;
    }

    // ── Contact Damage (Defend) ─────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        if (!isDefending) return;

        PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(contactDamage, gameObject);
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
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void LookAtDirection(Vector3 dir)
    {
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
