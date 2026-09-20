using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// IA do Geobionte — Criatura simbiótica que se funde a minérios.
/// 
/// CICLO DE VIDA:
/// 1. IDLE: Vaga pelo cenário, não-hostil, não pode ser atacado
/// 2. SEEKING ORE: Ao detectar o player, busca o minério mais próximo (atacável — 7 hits para impedir)
/// 3. FUSING: Funde-se ao minério (visual muda, cresce, vira cubo)
/// 4. TRANSFORMED (Bismutado): Cubo que persegue player e usa golpe giratório horizontal
/// 5. DEFEATED → Reverte ao Geobionte padrão (esfera passiva)
/// 6. INTERRUPTED: Se impedido durante SeekingOre, foge para um canto e despawna
/// 
/// [SENTINELA] (Semi-boss futuro): Usa as pernas maiores + campo de cristais debuffer
/// 
/// COMPONENTES NECESSÁRIOS no Prefab:
/// - Rigidbody
/// - Collider (SphereCollider recomendado)
/// - DummyHealth (para a forma transformada)
/// - EnemyDrops (drops ao ser "derrotado")
/// - EnemyIdentity (para o bestiário)
/// 
/// SETUP NO INSPECTOR:
/// 1. Coloque o prefab na sala com pelo menos um OreNode por perto
/// 2. Configure o Crystal Tuner se quiser que ele seja buffável
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class Geobionte_AI : MonoBehaviour
{
    // ==================== ESTADOS ====================

    public enum GeobionteState
    {
        Idle,           // Vagando pacificamente
        SeekingOre,     // Buscando minério (atacável — pode ser impedido)
        Fusing,         // Fundindo-se ao minério
        Transformed,    // Bismutado — ataca com debuffs
        Fleeing,        // Fugindo após ser derrotado
        Interrupted     // Impedido de transformar — fugindo para canto e despawnando
    }

    [Header("Estado Atual")]
    [SerializeField] private GeobionteState currentState = GeobionteState.Idle;

    public bool IsFleeing => currentState == GeobionteState.Fleeing;
    public bool IsInterrupted => currentState == GeobionteState.Interrupted;
    public bool IsDefeatedOrFleeing => currentState == GeobionteState.Fleeing || currentState == GeobionteState.Interrupted;

    // ==================== REFERÊNCIAS ====================

    [Header("Referências")]
    private Transform playerTransform;
    private Rigidbody rb;
    private DummyHealth health;
    private BismutadoProceduralAnimation bismutadoAnim;

    // ==================== ATIVAÇÃO ====================

    [Header("Ativação")]
    [Tooltip("Distância para detectar o player e começar a buscar minério (valor alto = sala inteira)")]
    public float activationDistance = 200f;
    private bool isActivated = false;

    // ==================== MIMIC — HOVER ====================

    [Header("Mimic — Corpo Flutuante")]
    [Tooltip("Altura do corpo acima do chão (para as pernas do Mimic)")]
    public float bodyHoverHeight = 1.2f;
    [Tooltip("Velocidade de ajuste da altura")]
    public float hoverLerpSpeed = 8f;

    // ==================== IDLE (Wandering) ====================

    [Header("Idle — Movimento Passivo")]
    [Tooltip("Velocidade ao vagar aleatoriamente")]
    public float wanderSpeed = 2f;
    [Tooltip("Intervalo entre mudanças de direção")]
    public float wanderChangeInterval = 3f;
    private Vector3 wanderDirection;
    private float wanderTimer;

    // ==================== BUSCA DE MINÉRIO ====================

    [Header("Busca de Minério")]
    [Tooltip("Raio de busca por minérios (valor alto = sala inteira)")]
    public float oreSearchRadius = 500f;
    [Tooltip("Velocidade ao ir até o minério")]
    public float seekSpeed = 5f;
    [Tooltip("Distância para considerar que chegou ao minério")]
    public float oreReachDistance = 2.5f;

    private OreNode targetOre;
    private bool hasFused = false; // Flag por ciclo (reset após derrota)

    [Header("Debug / Teste")]
    [Tooltip("Quantas derrotas como Bismutado já ocorreram. Defina como 2 para virar Sentinela na próxima derrota (fusionsToSentinel=3). Valor é sobrescrito pelo RunManager no Start se existir.")]
    [SerializeField] private int bismutadoDefeatCount = 0; // Quantas vezes foi derrotado como Bismutado
    private float oreStallTimer = 0f; // Timer para forçar absorção se ficar perto do cristal

    // ==================== PREVENÇÃO (Impedimento de Fusão) ====================

    [Header("Prevenção (Impedimento)")]
    [Tooltip("Número de hits que o player precisa dar para impedir a fusão")]
    public int preventionMaxHP = 7;
    [Tooltip("Velocidade de fuga ao ser impedido (indo para o canto da fase)")]
    public float interruptedFleeSpeed = 8f;

    private int preventionCurrentHP;
    private bool isPrevented = false; // Já foi impedido?
    private Vector3 interruptedTargetCorner; // Canto da fase para onde vai fugir

    // ==================== FUSÃO ====================

    [Header("Fusão")]
    [Tooltip("Tempo da animação de fusão (segundos)")]
    public float fusionDuration = 1.5f;
    [Tooltip("Escala do Geobionte após a fusão")]
    public float transformedScale = 2.5f;

    private Vector3 originalScale;
    private int absorbedOreValue = 0; // Valor do cristal absorvido
    private MimicSpace.Mimic mimicComponent; // Referência ao sistema de pernas procedurais
    private int originalNumberOfLegs;
    private int originalPartsPerLeg;
    private float originalNewLegRadius;

    // ==================== BISMUTADO (Transformado — Cubo) ====================

    [Header("Bismutado — Combate (Cubo)")]
    [Tooltip("Velocidade de perseguição ao player")]
    public float chaseSpeed = 7.0f;
    [Tooltip("Velocidade de rotação")]
    public float rotationSpeed = 12f;
    [Tooltip("Distância ideal para atacar")]
    public float attackRange = 6f;

    [Header("Bismutado — Golpe Giratório")]
    [Tooltip("Dano do golpe horizontal giratório")]
    public int sweepDamage = 40;
    [Tooltip("Raio do golpe giratório (alcance ao redor do corpo)")]
    public float sweepRange = 5.5f;
    [Tooltip("Cooldown entre golpes giratórios (segundos)")]
    public float sweepCooldown = 1.2f;
    [Tooltip("Duração da animação do golpe (segundos)")]
    public float sweepDuration = 0.6f;
    [Tooltip("Cor do indicador visual do golpe")]
    public Color sweepIndicatorColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    private float sweepTimer = 0f;
    private bool isSweeping = false;

    // ==================== CONFIGURAÇÃO DO TIPO ====================

    [Header("Configuração de Tipo")]
    [Tooltip("Número de absorções/derrotas como Bismutado necessárias para virar Sentinela")]
    public int fusionsToSentinel = 3;
    [System.NonSerialized]
    public float bismutadoScale = 1.5f;

    /// <summary>
    /// Flag definida automaticamente em runtime quando o Geobionte evolui para Sentinela.
    /// NÃO configurar manualmente no Inspector.
    /// </summary>
    private bool isSentinel = false;
    public bool IsSentinel => isSentinel;

    // ==================== SENTINELA (Semi-boss) ====================

    [Header("Sentinela — Configuração (Semi-boss)")]
    [Tooltip("Número de pernas do Sentinela")]
    public int sentinelNumberOfLegs = 12;
    [Tooltip("Partes por perna do Sentinela")]
    public int sentinelPartsPerLeg = 5;
    [Tooltip("Raio das pernas do Sentinela")]
    public float sentinelLegRadius = 6f;
    [Tooltip("Escala do Sentinela (tamanho grande)")]
    public float sentinelScale = 2.5f;

    [Header("Sentinela — Esfera Alta/Baixa")]
    [Tooltip("Altura da esfera quando invulnerável (alta, fora de alcance)")]
    public float sentinelHighHeight = 5f;
    [Tooltip("Altura da esfera quando vulnerável (baixa, player pode hitar)")]
    public float sentinelLowHeight = 1.2f;
    [Tooltip("Tempo que a esfera fica alta/invulnerável (segundos)")]
    public float sentinelInvulnerableDuration = 3f;
    [Tooltip("Duração da janela de vulnerabilidade (segundos)")]
    public float sentinelVulnerableWindow = 1f;
    [Tooltip("Hits necessários para derrotar o Sentinela (fixedDamageOverride = 1)")]
    public int sentinelMaxHP = 10;

    [Header("Sentinela — Dano das Pernas")]
    [Tooltip("Dano das pernas do Sentinela ao player")]
    public int sentinelLegDamage = 10;
    [Tooltip("Cooldown entre danos de perna (segundos)")]
    public float sentinelLegDamageCooldown = 0.5f;
    [Tooltip("Raio de proximidade para dano de perna")]
    public float sentinelLegDamageRadius = 1.0f;

    [Header("Sentinela — Slam")]
    [Tooltip("Dano do slam da esfera ao atingir o chão")]
    public int sentinelSlamDamage = 50;
    [Tooltip("Raio do dano do slam (ao redor do ponto de impacto)")]
    public float sentinelSlamRadius = 3f;
    [Tooltip("Velocidade de descida do slam (rápida)")]
    public float sentinelSlamSpeed = 20f;

    [Header("Bismutado — Campo de Cristais de Debuff")]
    [Tooltip("Cooldown em segundos entre criações da área de debuff")]
    public float fieldCooldown = 6f;
    [Tooltip("Duração em segundos de cada área de debuff no chão")]
    public float fieldDuration = 3.5f;
    [Tooltip("Raio em metros de cada área de debuff")]
    public float fieldRadius = 4f;

    // Estado do Sentinela em runtime
    private float fieldTimer = 0f;
    private bool hasSpeedBuff = false;  // Ganhou speed ao roubar do player
    private bool sentinelVulnerable = false; // Esfera está baixa/vulnerável?
    private float sentinelPhaseTimer = 0f; // Timer do ciclo alta/baixa
    private float sentinelTargetHeight; // Altura alvo atual da esfera
    private bool sentinelIsSlaming = false; // Está executando o slam?
    private bool sentinelSlamHitPlayer = false; // O slam acertou o player?

    // ==================== VISUAL — MESH ====================

    private GameObject bodyMeshObject; // Referência ao mesh do corpo (esfera ou cubo)
    private Vector3 originalMeshLocalScale; // Escala original do mesh do corpo
    private Vector3 mimicBodyLocalScale; // Escala reduzida do mesh do corpo para quando tem pernas

    // ==================== FUGA ====================

    [Header("Fuga")]
    [Tooltip("Velocidade de fuga após ser derrotado")]
    public float fleeSpeed = 10f;
    [Tooltip("Distância do player para ser destruído durante a fuga")]
    public float fleeDestroyDistance = 50f;

    // ==================== RESPAWN ====================

    [Header("Respawn")]
    [Tooltip("Tempo para respawnar após fugir (segundos). 0 = não respawna.")]
    public float respawnDelay = 30f;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private static Geobionte_AI pendingRespawn;

    // ==================== BUFF (Crystal Tuner) ====================

    [Header("Buff")]
    private bool isBuffed = false;
    private float originalChaseSpeed;
    private float originalFieldCooldown;

    // ==================== ÁUDIO ====================

    [Header("Áudio — Bismutado")]
    [Tooltip("Vetor de áudios de passos do Bismutado selecionados aleatoriamente")]
    public AudioClip[] bismutadoWalkingSounds;
    [Tooltip("Volume dos sons de passos do Bismutado")]
    [Range(0f, 1f)]
    public float bismutadoWalkingSoundVolume = 0.6f;
    [Tooltip("Intervalo entre cada passo do Bismutado enquanto caminha (segundos)")]
    public float bismutadoStepInterval = 0.25f;
    private float bismutadoStepTimer = 0f;

    [Tooltip("Vetor de áudios do campo de cristais (Crystal Field) selecionados aleatoriamente")]
    public AudioClip[] crystalFieldSounds;
    [Tooltip("Volume do som do campo de cristais")]
    [Range(0f, 1f)]
    public float crystalFieldSoundVolume = 0.8f;

    [Tooltip("Vetor de áudios de ataque (Cruzado/Soco) do Bismutado selecionados aleatoriamente")]
    public AudioClip[] bismutadoAttackSounds;

    [Tooltip("Volume do som de ataque do Bismutado")]
    [Range(0f, 1f)]
    public float bismutadoAttackSoundVolume = 0.8f;
    private AudioSource audioSource;

    // ==================== VISUAL ====================

    private Renderer geobionteRenderer;
    private Material geoMaterial;
    
    [Header("Cores do Geobionte (Corpo)")]
    [Tooltip("Cor na forma neutra (andando sem atacar)")]
    public Color baseColor = new Color(0.03f, 0.01f, 0.05f, 1f); // Preto/Roxo muito escuro
    [Tooltip("Cor quando se funde ao cristal (modo de ataque)")]
    public Color transformedColor = new Color(0.7f, 0.3f, 0.6f, 1f); // Bismuto roxo/rosa
    [Tooltip("Cor da esfera do Geobionte Sentinela")]
    public Color sentinelColor = new Color(0.9f, 0.2f, 0.8f, 1f); // Roxo/rosa brilhante
    
    private int originalLayer;

    // ==================== BESTIÁRIO ====================
    private bool registradoNoBestiario = false;

    // ========================================================================
    // START
    // ========================================================================

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<DummyHealth>();
        audioSource = GetComponent<AudioSource>();

        // Configurar Rigidbody
        rb.freezeRotation = true;

        // Salvar estado inicial
        originalScale = transform.localScale;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        originalChaseSpeed = chaseSpeed;
        originalFieldCooldown = fieldCooldown;
        originalLayer = gameObject.layer;

        // Configurar Mimic (pernas procedurais)
        mimicComponent = GetComponent<MimicSpace.Mimic>();
        if (mimicComponent != null)
        {
            originalNumberOfLegs = mimicComponent.numberOfLegs;
            originalPartsPerLeg = mimicComponent.partsPerLeg;
            originalNewLegRadius = mimicComponent.newLegRadius;

            // Remove o Movement.cs do Mimic (IA do Geobionte controla o movimento)
            MimicSpace.Movement mimicMovement = GetComponent<MimicSpace.Movement>();
            if (mimicMovement != null) Destroy(mimicMovement);

            // Desativar gravidade — o hover controla a altura
            rb.useGravity = false;
        }

        // Encontrar player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("[GEOBIONTE] Player não encontrado! Verifique a tag 'Player'.");
        }

        // Guarda referências do collider original da esfera
        geobionteCollider = GetComponent<SphereCollider>();
        if (geobionteCollider != null)
        {
            originalColliderRadius = geobionteCollider.radius;
            originalColliderCenter = geobionteCollider.center;
        }

        // Configurar visual
        SetupVisual();

        // ====== PROGRESSÃO MULTI-FASE: Ler progresso do RunManager ======
        if (RunManager.instance != null)
        {
            bismutadoDefeatCount = RunManager.instance.geobionteDefeatCount;
            Debug.Log("[GEOBIONTE] Progresso carregado do RunManager: " + bismutadoDefeatCount + "/" + fusionsToSentinel + " derrotas");
        }

        // FORMA BASE: não atacável
        // DummyHealth fica invulnerável e escondemos a health bar
        if (health != null)
        {
            health.isInvulnerable = true;
            // O health bar já começa escondido por padrão no DummyHealth
        }

        // Mudar layer para Default (neutro) — WeaponHitbox não detecta
        gameObject.layer = LayerMask.NameToLayer("Default");

        // Configurar override de morte
        if (health != null)
        {
            health.onDeathOverride = OnDefeated;
        }

        // Inicializar prevenção HP
        preventionCurrentHP = preventionMaxHP;

        // Iniciar wandering
        PickNewWanderDirection();

        Debug.Log("[GEOBIONTE] Inicializado em modo passivo. Escala: " + originalScale);
    }

    // ========================================================================
    // UPDATE
    // ========================================================================

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        switch (currentState)
        {
            case GeobionteState.Idle:
                HandleIdle();
                break;
            case GeobionteState.SeekingOre:
                HandleSeekingOre();
                break;
            case GeobionteState.Fusing:
                // Controlado por coroutine
                break;
            case GeobionteState.Transformed:
                HandleTransformed();
                break;
            case GeobionteState.Fleeing:
                HandleFleeing();
                break;
            case GeobionteState.Interrupted:
                HandleInterrupted();
                break;
        }
    }

    void FixedUpdate()
    {
        switch (currentState)
        {
            case GeobionteState.Idle:
                MoveWander();
                break;
            case GeobionteState.SeekingOre:
                MoveToOre();
                break;
            case GeobionteState.Transformed:
                MoveChasePlayer();
                HandleBismutadoWalkingSound();
                break;
            case GeobionteState.Fleeing:
                MoveFlee();
                break;
            case GeobionteState.Interrupted:
                MoveInterrupted();
                break;
        }

        // Manter corpo flutuando para as pernas do Mimic
        HandleBodyHover();
    }

    // ========================================================================
    // ESTADO: IDLE (Passivo)
    // ========================================================================

    void HandleIdle()
    {
        // Timer de wandering
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            PickNewWanderDirection();
        }

        // Se já foi impedido, não busca mais minério
        if (isPrevented) return;

        // Se já fundiu nesta sala/fase, não busca mais minério (funciona mesmo sem RunManager)
        if (hasFused) return;

        // PROGRESSÃO MULTI-FASE: Guard adicional via RunManager (persistente entre cenas)
        if (RunManager.instance != null && RunManager.instance.geobionteAbsorbedThisLevel) return;

        // Verifica proximidade do player para ativar
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer < activationDistance)
        {
            if (!isActivated)
            {
                isActivated = true;
                Debug.Log("[GEOBIONTE] Player detectado a " + distToPlayer.ToString("F1") + "m! Buscando minério...");

                // Registrar no bestiário
                if (!registradoNoBestiario)
                {
                    registradoNoBestiario = true;
                    EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                    if (id != null && BestiarioManager.instancia != null)
                        BestiarioManager.instancia.Registrar(id);
                }
            }

            // Só busca minério se ainda não fundiu neste ciclo
            if (!hasFused)
            {
                FindNearestOre();
                if (targetOre != null)
                {
                    EnterSeekingOre();
                }
            }
            // Se já fundiu ou não encontrou minério, continua vagando
        }
    }

    void PickNewWanderDirection()
    {
        wanderDirection = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ).normalized;

        wanderTimer = wanderChangeInterval + Random.Range(-1f, 1f);
    }

    void MoveWander()
    {
        Vector3 targetVelocity = wanderDirection * wanderSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        UpdateMimicVelocity();

        // Rotação suave na direção do movimento
        if (wanderDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(wanderDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 3f * Time.fixedDeltaTime);
        }
    }

    // ========================================================================
    // ESTADO: SEEKING ORE (Buscando Minério)
    // ========================================================================

    /// <summary>
    /// Entra no estado SeekingOre: torna o Geobionte atacável com prevenção HP.
    /// O player pode impedir a fusão acertando 7 hits.
    /// </summary>
    void EnterSeekingOre()
    {
        ChangeState(GeobionteState.SeekingOre);

        // Torna atacável durante a busca — player pode impedir a fusão
        if (health != null)
        {
            // Configura HP de prevenção (7 hits para impedir)
            health.maxHealth = preventionMaxHP;
            preventionCurrentHP = preventionMaxHP;
            health.ResetHealth();
            health.isInvulnerable = false;

            // Cada hit causa exatamente 1 de dano (7 hits = 7 HP)
            health.fixedDamageOverride = 1;

            // Override de morte: impedimento ao invés de morte
            health.onDeathOverride = OnPrevented;
        }

        // Mudar layer para Enemy — agora pode ser atacado
        gameObject.layer = originalLayer;

        // Cria health bar de prevenção
        CreateHealthBarIfNeeded();

        // Força a exibição e cor amarela da barra de vida de prevenção imediatamente
        if (health != null)
        {
            health.UpdateHealthBar();
            if (health.healthBarSlider != null)
            {
                health.healthBarSlider.gameObject.SetActive(true);
                Image fillImage = health.healthBarSlider.fillRect?.GetComponent<Image>();
                if (fillImage != null)
                    fillImage.color = new Color(1f, 0.8f, 0.2f, 1f); // Amarelo/dourado
            }
        }

        Debug.Log("[GEOBIONTE] Buscando minério — ATACÁVEL! Barra de prevenção (Amarela) ativada. " + preventionMaxHP + " hits para impedir.");
    }

    void HandleSeekingOre()
    {
        // Verifica se o minério alvo ainda é válido
        if (targetOre == null || !targetOre.IsAvailable())
        {
            // Busca outro minério
            FindNearestOre();
            if (targetOre == null)
            {
                Debug.Log("[GEOBIONTE] Sem minério disponível! Voltando a vagar.");
                ExitSeekingOre();
                ChangeState(GeobionteState.Idle);
                oreStallTimer = 0f;
                return;
            }
        }

        // Verifica se chegou ao minério (ignora o Y para não dar problema se ele estiver flutuando alto)
        Vector2 geobiontePos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 orePos2D = new Vector2(targetOre.transform.position.x, targetOre.transform.position.z);
        float distToOre = Vector2.Distance(geobiontePos2D, orePos2D);
        
        // Absorção direta se chegou perto o suficiente
        if (distToOre <= oreReachDistance)
        {
            Debug.Log("[GEOBIONTE] Alcançou o minério! Iniciando fusão...");
            oreStallTimer = 0f;
            ExitSeekingOre();
            StartCoroutine(FusionSequence());
            return;
        }
        
        // Segurança: se está relativamente perto mas não conseguiu absorver,
        // conta um timer e força a absorção após 2 segundos
        if (distToOre <= oreReachDistance * 2.5f)
        {
            oreStallTimer += Time.deltaTime;
            if (oreStallTimer >= 2f)
            {
                Debug.Log("[GEOBIONTE] Perto do minério por muito tempo — forçando absorção! Dist: " + distToOre.ToString("F2"));
                oreStallTimer = 0f;
                ExitSeekingOre();
                StartCoroutine(FusionSequence());
            }
        }
        else
        {
            oreStallTimer = 0f;
        }
    }

    /// <summary>
    /// Sai do estado SeekingOre: torna invulnerável novamente e esconde a health bar.
    /// </summary>
    void ExitSeekingOre()
    {
        if (health != null)
        {
            health.isInvulnerable = true;
            health.fixedDamageOverride = 0; // Volta ao dano normal

            // Esconde health bar de prevenção
            if (health.healthBarSlider != null)
                health.healthBarSlider.gameObject.SetActive(false);
        }

        // Volta para layer Default (não atacável)
        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    // ========================================================================
    // PREVENÇÃO — Player impediu a fusão
    // ========================================================================

    /// <summary>
    /// Chamado quando o player dá 7 hits no Geobionte enquanto ele busca minério.
    /// O Geobionte é impedido de transformar, foge para um canto da fase e despawna.
    /// </summary>
    void OnPrevented()
    {
        Debug.Log("[GEOBIONTE] IMPEDIDO! Player conseguiu impedir a fusão!");

        // Notifica RoomControllers que o Geobionte foi impedido/derrotado para liberar tranca das portas
        foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
        {
            rc.NotifyEnemyDefeated(gameObject);
        }

        isPrevented = true;
        hasFused = true; // Marca como fundido para não tentar de novo

        // Torna invulnerável (não pode ser atacado durante a fuga)
        if (health != null)
        {
            health.isInvulnerable = true;
            if (health.healthBarSlider != null)
                health.healthBarSlider.gameObject.SetActive(false);
        }

        // Volta para layer Default (não atacável)
        gameObject.layer = LayerMask.NameToLayer("Default");

        // Encontra o canto mais distante da fase
        interruptedTargetCorner = FindFarthestCorner();

        // Entra no estado Interrupted
        ChangeState(GeobionteState.Interrupted);

        Debug.Log("[GEOBIONTE] Fugindo para o canto em " + interruptedTargetCorner + " e despawnando...");
    }

    /// <summary>
    /// Encontra o canto mais distante da fase usando os limites dos renderers da cena
    /// ou os colliders de chão.
    /// </summary>
    Vector3 FindFarthestCorner()
    {
        // Tenta encontrar os limites da sala usando o chão ou as paredes
        // Busca todos os colliders de chão (normalmente no layer Default ou com tag "Ground")
        Bounds roomBounds = new Bounds(transform.position, Vector3.one * 10f);
        bool foundBounds = false;

        // Busca pelo chão/paredes da sala
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider col in allColliders)
        {
            // Ignora triggers e o próprio Geobionte
            if (col.isTrigger) continue;
            if (col.gameObject == gameObject) continue;
            if (col.GetComponent<Rigidbody>() != null) continue; // Ignora objetos dinâmicos

            if (!foundBounds)
            {
                roomBounds = col.bounds;
                foundBounds = true;
            }
            else
            {
                roomBounds.Encapsulate(col.bounds);
            }
        }

        // Define os 4 cantos no nível do chão
        float y = transform.position.y;
        Vector3[] corners = new Vector3[4]
        {
            new Vector3(roomBounds.min.x, y, roomBounds.min.z),
            new Vector3(roomBounds.min.x, y, roomBounds.max.z),
            new Vector3(roomBounds.max.x, y, roomBounds.min.z),
            new Vector3(roomBounds.max.x, y, roomBounds.max.z)
        };

        // Encontra o canto mais distante do player
        Vector3 farthest = corners[0];
        float maxDist = 0f;
        foreach (Vector3 corner in corners)
        {
            float dist = Vector3.Distance(playerTransform.position, corner);
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = corner;
            }
        }

        return farthest;
    }

    void HandleInterrupted()
    {
        // Verifica se chegou ao canto (ignora Y)
        Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 target2D = new Vector2(interruptedTargetCorner.x, interruptedTargetCorner.z);
        float dist = Vector2.Distance(pos2D, target2D);

        if (dist <= 2f)
        {
            Debug.Log("[GEOBIONTE] Chegou ao canto da fase. Despawnando...");
            StartCoroutine(DespawnSequence());
        }
    }

    void MoveInterrupted()
    {
        // Move em direção ao canto alvo
        Vector3 direction = (interruptedTargetCorner - transform.position).normalized;
        direction.y = 0;

        Vector3 targetVelocity = direction * interruptedFleeSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        UpdateMimicVelocity();

        // Rotação na direção da fuga
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Sequência de despawn: encolhe gradualmente e se destrói.
    /// </summary>
    IEnumerator DespawnSequence()
    {
        // Para o movimento
        rb.linearVelocity = Vector3.zero;
        UpdateMimicVelocity();

        // Encolhe até sumir
        float shrinkDuration = 1f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;

            // Encolhe com easing
            float easedT = t * t;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, easedT);

            // Escurece gradualmente
            if (geoMaterial != null)
            {
                Color fadeColor = Color.Lerp(baseColor, Color.black, easedT);
                geoMaterial.color = fadeColor;
                geoMaterial.SetColor("_EmissionColor", fadeColor * 0.5f);
                if (geoMaterial.HasProperty("_BaseColor"))
                    geoMaterial.SetColor("_BaseColor", fadeColor);
            }

            yield return null;
        }

        Debug.Log("[GEOBIONTE] Despawnado após ser impedido.");
        Destroy(gameObject);
    }

    void MoveToOre()
    {
        if (targetOre == null) return;

        Vector3 direction = (targetOre.transform.position - transform.position).normalized;
        direction.y = 0;

        Vector3 targetVelocity = direction * seekSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        UpdateMimicVelocity();

        // Rotação na direção do minério
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void FindNearestOre()
    {
        OreNode[] allOres = FindObjectsByType<OreNode>(FindObjectsSortMode.None);
        float closestDist = float.MaxValue;
        targetOre = null;

        foreach (OreNode ore in allOres)
        {
            if (!ore.IsAvailable()) continue;

            // Busca qualquer OreNode na cena (sem filtro de distância)
            float dist = Vector3.Distance(transform.position, ore.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                targetOre = ore;
            }
        }

        if (targetOre != null)
            Debug.Log("[GEOBIONTE] Minério encontrado: " + targetOre.oreName + " a " + closestDist.ToString("F1") + "m");
    }

    // ========================================================================
    // ESTADO: FUSING (Fusão)
    // ========================================================================

    IEnumerator FusionSequence()
    {
        ChangeState(GeobionteState.Fusing);
        hasFused = true;

        // PROGRESSÃO MULTI-FASE: Marca que já absorveu nesta fase
        if (RunManager.instance != null)
        {
            RunManager.instance.geobionteAbsorbedThisLevel = true;
            Debug.Log("[GEOBIONTE] Absorção marcada no RunManager para esta fase (Round " + RunManager.instance.currentLevel + ")");
        }

        // Para o movimento
        rb.linearVelocity = Vector3.zero;
        UpdateMimicVelocity();

        // Grava o valor do cristal e consome
        if (targetOre != null)
        {
            absorbedOreValue = targetOre.oreValue;
            targetOre.Consume(); // Destrói o cristal (some da cena)
        }

        Debug.Log("[GEOBIONTE] FUSÃO INICIADA! Cristal absorvido (valor: " + absorbedOreValue + "). Transformando em Bismutado...");

        // Animação de fusão: cresce gradualmente
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        float targetScaleMultiplier = isSentinel ? transformedScale : bismutadoScale;
        Vector3 endScale = originalScale * targetScaleMultiplier;

        while (elapsed < fusionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fusionDuration;

            // Crescimento com easing
            float easedT = t * t * (3f - 2f * t); // smoothstep
            transform.localScale = Vector3.Lerp(startScale, endScale, easedT);

            // Mudança de cor gradual
            if (geoMaterial != null)
            {
                Color currentColor = Color.Lerp(baseColor, transformedColor, easedT);
                geoMaterial.color = currentColor;
                geoMaterial.SetColor("_EmissionColor", currentColor * 2f);
                if (geoMaterial.HasProperty("_BaseColor"))
                    geoMaterial.SetColor("_BaseColor", currentColor);
            }

            yield return null;
        }

        // TRANSFORMAÇÃO COMPLETA
        transform.localScale = endScale;

        if (!isSentinel)
        {
            // Troca o mesh do corpo: esfera → cubo (Bismutado)
            SwapBodyMeshToCube();
            // Desativa as pernas para o Bismutado
            if (mimicComponent != null)
            {
                mimicComponent.SetLegsActive(false);
            }
        }
        else
        {
            // [SENTINELA] Transforma as pernas do Mimic
            TransformMimicLegs();
        }

        TransformIntoBismutado();
    }

    void TransformIntoBismutado()
    {
        // Ativa combate — restaura HP de combate (diferente do HP de prevenção)
        if (health != null)
        {
            health.maxHealth = 100; // HP de combate (original do DummyHealth)
            health.isInvulnerable = false;
            health.fixedDamageOverride = 0; // Volta ao dano normal da arma
            health.ResetHealth();

            // Override de morte: Bismutado derrotado → volta ao Geobionte padrão
            health.onDeathOverride = OnDefeated;
        }

        // Cria health bar e damage canvas por código se não existir
        CreateHealthBarIfNeeded();

        // Restaura cor da barra para vermelho (combate)
        if (health != null && health.healthBarSlider != null)
        {
            Image fillImage = health.healthBarSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = Color.red;
        }

        // Para o Bismutado, a escala do objeto raiz DEVE ser (1, 1, 1) 
        // para respeitar a escala própria do Prefab (3.5) sem multiplicar a escala da fusão (5.25)
        transform.localScale = Vector3.one;

        // Reativa física para perseguição
        if (rb != null) rb.isKinematic = false;
        bismutadoAnim = GetComponentInChildren<BismutadoProceduralAnimation>(true);

        // Ajusta o SphereCollider para o tamanho real do Bismutado na escala (1, 1, 1) da raiz
        if (geobionteCollider == null) geobionteCollider = GetComponent<SphereCollider>();
        if (geobionteCollider != null)
        {
            geobionteCollider.radius = bismutadoColliderRadius;
            geobionteCollider.center = new Vector3(0, bismutadoColliderCenterY, 0);
        }

        // Reset de timers
        sweepTimer = 0f;
        isSweeping = false;

        ChangeState(GeobionteState.Transformed);
        Debug.Log("[BISMUTADO] TRANSFORMAÇÃO COMPLETA! Modelo 3D com Rig e IA de perseguição ativados!");
    }

    // ========================================================================
    // ESTADO: TRANSFORMED (Bismutado — Combate)
    // ========================================================================

    void HandleTransformed()
    {
        if (health != null && health.CurrentHealth <= 0) return;

        if (isSentinel)
        {
            // Sentinela: ciclo de esfera alta/baixa + campo de cristais
            HandleSentinelCombat();
        }
        else
        {
            // Bismutado: perseguição + golpe giratório
            if (sweepTimer > 0) sweepTimer -= Time.deltaTime;
            if (fieldTimer > 0) fieldTimer -= Time.deltaTime;

            HandleRotation();
            HandleCombat();
        }
    }

    void HandleRotation()
    {
        if (isSweeping) return; // Não rotaciona durante o golpe

        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        dirToPlayer.y = 0;

        if (dirToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void MoveChasePlayer()
    {
        if (health != null && health.CurrentHealth <= 0) return;
        if (isSweeping) return; // Não se move durante o golpe

        if (bismutadoAnim == null) bismutadoAnim = GetComponentInChildren<BismutadoProceduralAnimation>(true);

        // Sentinela: perseguição lenta + para durante slam/vulnerabilidade
        if (isSentinel)
        {
            // Não se move durante o slam ou quando está stunado (vulnerável)
            if (sentinelIsSlaming || sentinelVulnerable)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                UpdateMimicVelocity();
                return;
            }

            // Perseguição lenta em direção ao player
            float sentinelDist = Vector3.Distance(transform.position, playerTransform.position);
            if (sentinelDist > sentinelLegRadius * 0.5f)
            {
                Vector3 direction = (playerTransform.position - transform.position).normalized;
                direction.y = 0;
                float speed = hasSpeedBuff ? chaseSpeed * 0.65f : chaseSpeed * 0.5f;
                Vector3 targetVelocity = direction * speed;
                rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
                if (bismutadoAnim != null) bismutadoAnim.isWalking = true;
            }
            else
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                if (bismutadoAnim != null) bismutadoAnim.isWalking = false;
            }
            UpdateMimicVelocity();
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float effectiveAttackRange = sweepRange;

        // Persegue o player se está fora do range de ataque de perto
        if (distToPlayer > effectiveAttackRange * 0.85f)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0;

            float speed = hasSpeedBuff ? chaseSpeed * 1.35f : chaseSpeed;
            Vector3 targetVelocity = direction * speed;

            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
            }
            transform.position += direction * speed * Time.fixedDeltaTime;

            if (bismutadoAnim != null)
            {
                bismutadoAnim.isWalking = true;
                bismutadoAnim.walkGaitSpeed = 8.5f; // Animação de corrida mais rápida e ágil
            }
            UpdateMimicVelocity();
        }
        else
        {
            // Para perto do player para desferir o ataque de perto
            if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            if (bismutadoAnim != null) bismutadoAnim.isWalking = false;
            UpdateMimicVelocity();
        }
    }

    private void HandleBismutadoWalkingSound()
    {
        if (currentState != GeobionteState.Transformed) return;
        if (health != null && health.CurrentHealth <= 0) return;
        if (isSweeping) return;

        bool isWalking = (bismutadoAnim != null && bismutadoAnim.isWalking);
        if (!isWalking && rb != null)
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.magnitude > 0.1f) isWalking = true;
        }

        if (isWalking)
        {
            bismutadoStepTimer -= Time.fixedDeltaTime;
            if (bismutadoStepTimer <= 0f)
            {
                PlayBismutadoWalkingSound();
                bismutadoStepTimer = bismutadoStepInterval;
            }
        }
        else
        {
            bismutadoStepTimer = 0f;
        }
    }

    private void PlayBismutadoWalkingSound()
    {
        if (bismutadoWalkingSounds == null || bismutadoWalkingSounds.Length == 0)
            return;

        int randIndex = Random.Range(0, bismutadoWalkingSounds.Length);
        AudioClip clipToPlay = bismutadoWalkingSounds[randIndex];

        if (clipToPlay != null)
        {
            float pitch = Random.Range(0.85f, 1.15f);
            PlayClipAtPointWithPitch(clipToPlay, transform.position, pitch, bismutadoWalkingSoundVolume);
        }
    }

    private void PlayCrystalFieldSound(Vector3 position)
    {
        if (crystalFieldSounds == null || crystalFieldSounds.Length == 0)
            return;

        int randIndex = Random.Range(0, crystalFieldSounds.Length);
        AudioClip clipToPlay = crystalFieldSounds[randIndex];

        if (clipToPlay != null)
        {
            float pitch = Random.Range(0.9f, 1.1f);
            PlayClipAtPointWithPitch(clipToPlay, position, pitch, crystalFieldSoundVolume);
        }
    }

    public void PlayBismutadoAttackSound()
    {
        StartCoroutine(PlayBismutadoAttackSoundDelayed(0.5f));
    }

    private IEnumerator PlayBismutadoAttackSoundDelayed(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (bismutadoAttackSounds == null || bismutadoAttackSounds.Length == 0)
        {
            Debug.LogWarning("[GEOBIONTE] Tentou tocar som de ataque do Bismutado, mas o vetor bismutadoAttackSounds está VAZIO no Inspector!");
            yield break;
        }

        int randIndex = Random.Range(0, bismutadoAttackSounds.Length);
        AudioClip clipToPlay = bismutadoAttackSounds[randIndex];

        if (clipToPlay != null)
        {
            float pitch = Random.Range(0.9f, 1.1f);
            PlayClipAtPointWithPitch(clipToPlay, transform.position, pitch, bismutadoAttackSoundVolume);
            Debug.Log($"[GEOBIONTE] Som de ataque do Bismutado tocado após delay de {delay}s! ({clipToPlay.name})");
        }
        else
        {
            Debug.LogWarning("[GEOBIONTE] O elemento selecionado no vetor bismutadoAttackSounds é NULL! Verifique o Inspector.");
        }
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
        {
            if (audioSource != null)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, volume);
            }
        }
    void HandleCombat()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else return;
        }

        if (bismutadoAnim == null) bismutadoAnim = GetComponentInChildren<BismutadoProceduralAnimation>(true);

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 1. PRIORIDADE MÁXIMA: Ataque de Cruzado se o player estiver perto/colado no Bismutado (<= 5.5m)
        if (distToPlayer <= sweepRange && sweepTimer <= 0 && !isSweeping)
        {
            Debug.Log($"⚔️ [BISMUTADO IA] PLAYER DENTRO DO ALCANCE DE PERTO ({distToPlayer:F1}m <= {sweepRange}m)! DISPARANDO CRUZADO!");
            TriggerBismutadoAttack();
            return;
        }

        // 2. Campo de debuff se o player estiver à distância (até 6.0m)
        if (distToPlayer <= attackRange && fieldTimer <= 0 && !isSweeping)
        {
            CreateCrystalField();
        }
    }

    /// <summary>
    /// Chamado pelo BismuthCrystalField quando o player entra na área do debuff.
    /// O Bismutado percebe o player no debuff e ataca se estiver no alcance físico de perto (4.0m)!
    /// </summary>
    public void OnPlayerTrappedInCrystalField()
    {
        if (playerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Mira instantaneamente na direção do player
        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        dirToPlayer.y = 0;
        if (dirToPlayer != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }

        // SÓ acerta o ataque se estiver no alcance físico real do braço (<= 4.0m) para não socar o vento!
        if (distToPlayer <= sweepRange && !isSweeping)
        {
            sweepTimer = 0f;
            TriggerBismutadoAttack();
        }
    }

    /// <summary>
    /// Dispara o ataque procedural de Cruzado no modelo 3D do Bismutado + Coroutine de Sweep.
    /// </summary>
    public void TriggerBismutadoAttack()
    {
        if (isSweeping) return;

        if (bismutadoAnim == null) bismutadoAnim = GetComponentInChildren<BismutadoProceduralAnimation>(true);
        if (bismutadoAnim != null)
        {
            bismutadoAnim.TriggerCrystalSlam();
        }

        StartCoroutine(SweepAttack());
    }

    // ========================================================================
    // GOLPE GIRATÓRIO (Bismutado)
    // ========================================================================

    /// <summary>
    /// Executa o Cruzado de Direita do Bismutado 3D com Rig.
    /// Zera a movimentação durante o golpe e sincroniza com a animação procedural, Hitbox e Rastro de Energia (Trail VFX).
    /// </summary>
    IEnumerator SweepAttack()
    {
        isSweeping = true;
        sweepTimer = sweepCooldown;

        // Para a movimentação durante o golpe
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        UpdateMimicVelocity();

        // Alinha o corpo para olhar diretamente para o jogador no início do golpe
        if (playerTransform != null)
        {
            Vector3 lookDir = (playerTransform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // Garante que a animação procedural do Bismutado 3D seja disparada com Hitbox e Trail VFX
        if (bismutadoAnim == null) bismutadoAnim = GetComponentInChildren<BismutadoProceduralAnimation>(true);
        if (bismutadoAnim != null)
        {
            bismutadoAnim.TriggerCrystalSlam();
        }

        Debug.Log("🥊 [BISMUTADO 3D] Cruzado de Direita executado! (Giro antigo de 360° e linhas de aviso removidos).");

        // Aguarda a duração exata do Cruzado de Direita (1.3s)
        yield return new WaitForSeconds(1.3f);

        isSweeping = false;
    }

    /// <summary>
    /// Cria um material transparente para os indicadores visuais do golpe giratório.
    /// </summary>
    Material CreateSweepMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    // ========================================================================
    // CAMPO DE CRISTAIS — [SENTINELA] (Semi-boss, futuro)
    // ========================================================================

    /// <summary>
    /// [BISMUTADO] Cria campo de cristais debuffer. Usado apenas pelo Bismutado (Sentinela NÃO usa).
    /// </summary>
    void CreateCrystalField()
    {
        fieldTimer = fieldCooldown;

        // Posiciona o campo na posição horizontal atual do player, mas no chão
        Vector3 fieldPos = playerTransform.position;
        RaycastHit groundHit;
        // Faz um raycast para baixo a partir de 2.0 unidades acima da posição do player para achar o chão
        if (Physics.Raycast(fieldPos + Vector3.up * 2f, Vector3.down, out groundHit, 10f))
        {
            fieldPos.y = groundHit.point.y + 0.05f;
        }
        else
        {
            fieldPos.y = 0.05f; // Fallback caso não ache o chão
        }

        GameObject fieldObj = new GameObject("BismuthCrystalField");
        fieldObj.transform.position = fieldPos;

        BismuthCrystalField field = fieldObj.AddComponent<BismuthCrystalField>();
        field.fieldDuration = fieldDuration;
        field.fieldRadius = fieldRadius;
        field.ownerBismutado = this;
        field.stealBuffTime = 3f; // 3 segundos

        // Toca o som de ativação do campo de cristais
        PlayCrystalFieldSound(fieldPos);

        Debug.Log($"[{(isSentinel ? "SENTINELA" : "BISMUTADO")}] Campo de cristais criado na posição do player!");
    }

    /// <summary>
    /// Chamado pelo BismuthCrystalField quando rouba um buff de speed do player.
    /// O Bismutado/Sentinela ganha um buff de velocidade para si.
    /// </summary>
    public void OnStoleSpeedBuff()
    {
        hasSpeedBuff = true;
        Debug.Log($"[{(isSentinel ? "SENTINELA" : "BISMUTADO")}] Ganhou buff de velocidade roubado do player!");
    }

    // ========================================================================
    // DERROTA → REVERSÃO (Bismutado volta ao Geobionte padrão)
    // ========================================================================

    /// <summary>
    /// Chamado pelo DummyHealth.onDeathOverride quando HP do Bismutado chega a 0.
    /// Incrementa contador de derrotas. Após 3 derrotas, evolui para Sentinela.
    /// </summary>
    void OnDefeated()
    {
        // Cancela qualquer sweep em andamento
        isSweeping = false;

        // Notifica RoomControllers que o Geobionte foi derrotado para liberar tranca das portas
        foreach (RoomController rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
        {
            rc.NotifyEnemyDefeated(gameObject);
        }

        // Incrementa contador de derrotas como Bismutado
        bismutadoDefeatCount++;
        Debug.Log("[BISMUTADO] Derrotado! Derrota #" + bismutadoDefeatCount + "/" + fusionsToSentinel);

        // PROGRESSÃO MULTI-FASE: Salva progresso no RunManager
        if (RunManager.instance != null)
        {
            RunManager.instance.geobionteDefeatCount = bismutadoDefeatCount;
            Debug.Log("[GEOBIONTE] Progresso salvo no RunManager: " + bismutadoDefeatCount + "/" + fusionsToSentinel);
        }

        // 1. Dropar loot
        EnemyDrops drops = GetComponent<EnemyDrops>()
                        ?? GetComponentInChildren<EnemyDrops>()
                        ?? GetComponentInParent<EnemyDrops>();
        if (drops != null)
        {
            drops.OnDeath();
        }

        // 2. Dropar 60% do valor do cristal absorvido como essência extra
        if (absorbedOreValue > 0 && drops != null && drops.essencePrefab != null)
        {
            int refundAmount = Mathf.RoundToInt(absorbedOreValue * 0.6f);
            if (refundAmount > 0)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
                GameObject essenceObj = Instantiate(drops.essencePrefab, spawnPos, Quaternion.identity);
                EssencePickup essencePickup = essenceObj.GetComponent<EssencePickup>();
                if (essencePickup != null)
                {
                    essencePickup.essenceValue = refundAmount;
                }
                Rigidbody essenceRb = essenceObj.GetComponent<Rigidbody>();
                if (essenceRb != null)
                {
                    essenceRb.linearDamping = 5f;
                    essenceRb.AddForce(Vector3.up * 3f, ForceMode.Impulse);
                }
                Debug.Log("[BISMUTADO] Devolveu 60% do cristal: " + refundAmount + " essência (cristal valia " + absorbedOreValue + ")");
            }
        }

        // 3. Restaurar buffs roubados do player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlayerDebuffs playerDebuffs = player.GetComponent<PlayerDebuffs>();
            if (playerDebuffs != null)
            {
                playerDebuffs.RestoreStolenBuffs(gameObject);
                playerDebuffs.RemoveSlow();
            }
        }

        // 4. Verificar se deve evoluir para Sentinela
        if (bismutadoDefeatCount >= fusionsToSentinel)
        {
            Debug.Log("[GEOBIONTE] " + fusionsToSentinel + " derrotas como Bismutado! EVOLUINDO PARA SENTINELA!");
            StartCoroutine(TransformIntoSentinel());
        }
        else
        {
            // PROGRESSÃO MULTI-FASE: Foge da sala após ser derrotado (só reaparece na próxima sala)
            StartCoroutine(FleeAfterDefeatSequence());
        }
    }

    /// <summary>
    /// Sequência pós-derrota do Bismutado: reverte o visual (cubo → esfera),
    /// depois foge da sala e despawna. O Geobionte só reaparece na próxima sala.
    /// </summary>
    IEnumerator FleeAfterDefeatSequence()
    {
        // Torna invulnerável durante a reversão
        if (health != null)
        {
            health.isInvulnerable = true;
        }

        // Esconde health bar
        if (health != null && health.healthBarSlider != null)
        {
            health.healthBarSlider.gameObject.SetActive(false);
        }

        // Volta para layer Default (não atacável)
        gameObject.layer = LayerMask.NameToLayer("Default");

        // Encolhe de volta ao tamanho original + muda cor
        float shrinkDuration = 0.8f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            float easedT = t * t * (3f - 2f * t); // smoothstep
            transform.localScale = Vector3.Lerp(startScale, originalScale, easedT);

            if (geoMaterial != null)
            {
                Color currentColor = Color.Lerp(transformedColor, baseColor, easedT);
                geoMaterial.color = currentColor;
                geoMaterial.SetColor("_EmissionColor", currentColor * 0.5f);
                if (geoMaterial.HasProperty("_BaseColor"))
                    geoMaterial.SetColor("_BaseColor", currentColor);
            }

            yield return null;
        }

        transform.localScale = originalScale;

        // Troca o mesh de volta: cubo → esfera
        SwapBodyMeshToSphere();

        // Restaura pernas do Mimic
        if (mimicComponent != null)
        {
            mimicComponent.numberOfLegs = originalNumberOfLegs;
            mimicComponent.partsPerLeg = originalPartsPerLeg;
            mimicComponent.newLegRadius = originalNewLegRadius;
            mimicComponent.legsDealDamage = false;
            mimicComponent.RecalculateParameters();
            mimicComponent.SetLegsActive(true);
        }

        // Marca que já fundiu nesta sala (impede nova transformação)
        hasFused = true;
        absorbedOreValue = 0;
        hasSpeedBuff = false;
        targetOre = null;
        sweepTimer = 0f;
        isSweeping = false;

        // Entra no estado Fleeing — foge para longe do player e despawna
        ChangeState(GeobionteState.Fleeing);

        Debug.Log("[GEOBIONTE] Derrotado como Bismutado (" + bismutadoDefeatCount + "/" + fusionsToSentinel + ")! Fugindo da sala...");
    }

    // ========================================================================
    // SENTINELA — EVOLUÇÃO E COMBATE (Semi-boss)
    // ========================================================================

    /// <summary>
    /// Transforma o Geobionte no Sentinela após 3 derrotas como Bismutado.
    /// Cresce, ativa pernas com dano, inicia ciclo de esfera alta/baixa.
    /// </summary>
    IEnumerator TransformIntoSentinel()
    {
        isSentinel = true;

        // Torna invulnerável durante a transformação
        if (health != null)
        {
            health.isInvulnerable = true;
        }

        // Esconde health bar temporariamente
        if (health != null && health.healthBarSlider != null)
        {
            health.healthBarSlider.gameObject.SetActive(false);
        }

        // Volta para layer Default durante a transformação
        gameObject.layer = LayerMask.NameToLayer("Default");

        Debug.Log("[SENTINELA] EVOLUÇÃO INICIADA! Crescendo...");

        // Se ainda é cubo, volta para esfera primeiro
        SwapBodyMeshToSphere();

        // Animação: cresce ao tamanho do Sentinela
        float growDuration = 2f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = originalScale * sentinelScale;

        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / growDuration;
            float easedT = t * t * (3f - 2f * t);
            transform.localScale = Vector3.Lerp(startScale, endScale, easedT);

            if (geoMaterial != null)
            {
                Color currentColor = Color.Lerp(transformedColor, sentinelColor, easedT);
                geoMaterial.color = currentColor;
                geoMaterial.SetColor("_EmissionColor", currentColor * 3f);
                if (geoMaterial.HasProperty("_BaseColor"))
                    geoMaterial.SetColor("_BaseColor", currentColor);
            }

            yield return null;
        }

        transform.localScale = endScale;

        // Configura pernas do Sentinela (com dano!)
        if (mimicComponent != null)
        {
            mimicComponent.numberOfLegs = sentinelNumberOfLegs;
            mimicComponent.partsPerLeg = sentinelPartsPerLeg;
            mimicComponent.newLegRadius = sentinelLegRadius;
            mimicComponent.legsDealDamage = true;
            mimicComponent.legDamageAmount = sentinelLegDamage;
            mimicComponent.legDamageCooldown = sentinelLegDamageCooldown;
            mimicComponent.legDamageRadius = sentinelLegDamageRadius;
            mimicComponent.RecalculateParameters();
            mimicComponent.SetLegsActive(true);
        }

        // Configura HP do Sentinela: 10 hits com fixedDamageOverride = 1
        if (health != null)
        {
            health.maxHealth = sentinelMaxHP;
            health.fixedDamageOverride = 1;
            health.ResetHealth();
            health.onDeathOverride = OnSentinelDefeated;
            // Começa invulnerável (esfera alta)
            health.isInvulnerable = true;
        }

        // Cria health bar
        CreateHealthBarIfNeeded();

        // Cor da barra: roxo para indicar Sentinela
        if (health != null && health.healthBarSlider != null)
        {
            Image fillImage = health.healthBarSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = new Color(0.8f, 0.2f, 0.9f, 1f); // Roxo
        }

        // Mudar layer para Enemy
        gameObject.layer = originalLayer;

        // Inicia o ciclo de esfera alta/baixa
        sentinelVulnerable = false;
        sentinelPhaseTimer = 0f;
        sentinelTargetHeight = sentinelHighHeight;

        // Reset timers
        sweepTimer = 0f;
        isSweeping = false;
        fieldTimer = 0f;

        ChangeState(GeobionteState.Transformed);
        Debug.Log("[SENTINELA] TRANSFORMAÇÃO COMPLETA! Esfera alta, pernas com dano, " + sentinelMaxHP + " hits para derrotar!");
    }

    /// <summary>
    /// Lógica de combate do Sentinela:
    /// - Esfera fica alta (invulnerável) por sentinelInvulnerableDuration segundos
    /// - Depois faz SLAM: desce rápido e tenta acertar o player
    /// - Se acertar: dano + knockback, volta a subir sem ficar stunado
    /// - Se errar: fica stunado (vulnerável) por sentinelVulnerableWindow segundos
    /// - Pernas dão dano automaticamente (gerenciado pelo Leg.cs)
    /// - NÃO cria campo de cristais (mecânica exclusiva do Bismutado)
    /// </summary>
    void HandleSentinelCombat()
    {
        if (health != null && health.CurrentHealth <= 0) return;

        // Rotação lenta para olhar o player (não rotaciona durante slam/stun)
        if (!sentinelIsSlaming && !sentinelVulnerable)
            HandleRotation();

        // Se está executando o slam, a coroutine SentinelSlam controla tudo
        if (sentinelIsSlaming) return;

        // Ciclo de esfera alta/baixa
        sentinelPhaseTimer += Time.deltaTime;

        if (!sentinelVulnerable)
        {
            // FASE: Esfera ALTA (invulnerável) — anda, pernas dão dano
            sentinelTargetHeight = sentinelHighHeight;

            if (health != null) health.isInvulnerable = true;

            // Após sentinelInvulnerableDuration, inicia o SLAM
            if (sentinelPhaseTimer >= sentinelInvulnerableDuration)
            {
                sentinelPhaseTimer = 0f;
                Debug.Log("[SENTINELA] Preparando SLAM!");
                StartCoroutine(SentinelSlam());
            }
        }
        else
        {
            // FASE: Esfera BAIXA (stunado/vulnerável — slam errou o player)
            sentinelTargetHeight = sentinelLowHeight;

            if (health != null) health.isInvulnerable = false;

            // Após sentinelVulnerableWindow, sobe a esfera
            if (sentinelPhaseTimer >= sentinelVulnerableWindow)
            {
                sentinelVulnerable = false;
                sentinelPhaseTimer = 0f;
                if (health != null) health.isInvulnerable = true;
                Debug.Log("[SENTINELA] Saiu do stun! Esfera SUBINDO! Invulnerável novamente.");
            }
        }
    }

    /// <summary>
    /// Coroutine de slam do Sentinela:
    /// 1. Breve pausa de preparação (0.3s)
    /// 2. Esfera desce rapidamente até sentinelLowHeight
    /// 3. Ao chegar, verifica se o player está no raio de impacto
    /// 4. Se acertou: dano + knockback, esfera sobe imediatamente
    /// 5. Se errou: Sentinela fica stunado (vulnerável)
    /// </summary>
    IEnumerator SentinelSlam()
    {
        sentinelIsSlaming = true;
        sentinelSlamHitPlayer = false;

        // Torna invulnerável durante o slam (transição)
        if (health != null) health.isInvulnerable = true;

        // Para o movimento durante o slam
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        UpdateMimicVelocity();

        // === FASE 1: Preparação (breve pausa + indicador visual) ===
        Debug.Log("[SENTINELA] SLAM — Preparando descida!");

        // Cria indicador de área no chão (onde o slam vai cair)
        GameObject slamIndicator = null;
        Material indicatorMat = null;
        {
            slamIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slamIndicator.name = "SlamIndicator";
            Collider indicatorCol = slamIndicator.GetComponent<Collider>();
            if (indicatorCol != null) Destroy(indicatorCol);

            // Posiciona no chão abaixo do Sentinela
            Vector3 indicatorPos = transform.position;
            RaycastHit groundHit;
            if (Physics.Raycast(transform.position, Vector3.down, out groundHit, 20f))
                indicatorPos.y = groundHit.point.y + 0.05f;
            else
                indicatorPos.y = 0.05f;

            slamIndicator.transform.position = indicatorPos;
            slamIndicator.transform.localScale = new Vector3(sentinelSlamRadius * 2f, 0.05f, sentinelSlamRadius * 2f);

            // Material vermelho pulsante
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            indicatorMat = new Material(shader);
            Color warnColor = new Color(1f, 0.2f, 0.1f, 0.3f);
            indicatorMat.color = warnColor;

            // Transparência
            if (shader != null && shader.name.Contains("Universal"))
            {
                indicatorMat.SetFloat("_Surface", 1);
                indicatorMat.SetFloat("_Blend", 0);
                indicatorMat.SetFloat("_ZWrite", 0);
                indicatorMat.SetFloat("_AlphaClip", 0);
                indicatorMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                indicatorMat.DisableKeyword("_ALPHATEST_ON");
                indicatorMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                indicatorMat.SetColor("_BaseColor", warnColor);
            }
            else
            {
                indicatorMat.SetFloat("_Mode", 3);
                indicatorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                indicatorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                indicatorMat.SetInt("_ZWrite", 0);
                indicatorMat.EnableKeyword("_ALPHABLEND_ON");
                indicatorMat.renderQueue = 3000;
            }

            indicatorMat.EnableKeyword("_EMISSION");
            indicatorMat.SetColor("_EmissionColor", warnColor * 2f);
            slamIndicator.GetComponent<Renderer>().material = indicatorMat;
        }

        // Pausa de preparação com pulsação do indicador
        float prepDuration = 0.4f;
        float prepElapsed = 0f;
        while (prepElapsed < prepDuration)
        {
            prepElapsed += Time.deltaTime;
            float pulse = Mathf.Sin(prepElapsed * Mathf.PI * 6f) * 0.15f + 0.35f;
            if (indicatorMat != null)
            {
                Color pulseColor = new Color(1f, 0.2f, 0.1f, pulse);
                indicatorMat.color = pulseColor;
                if (indicatorMat.HasProperty("_BaseColor"))
                    indicatorMat.SetColor("_BaseColor", pulseColor);
            }
            yield return null;
        }

        // === FASE 2: Descida rápida (slam) ===
        Debug.Log("[SENTINELA] SLAM — DESCENDO!");

        // Muda o target height para baixo — o HandleBodyHover vai mover a esfera
        // Mas queremos uma descida RÁPIDA, então vamos forçar a velocidade Y
        sentinelTargetHeight = sentinelLowHeight;
        float slamDuration = 0.3f; // Tempo máximo de descida
        float slamElapsed = 0f;

        while (slamElapsed < slamDuration)
        {
            slamElapsed += Time.deltaTime;
            // Força velocidade de descida rápida
            rb.linearVelocity = new Vector3(0, -sentinelSlamSpeed, 0);
            yield return null;
        }

        // Para a velocidade vertical
        rb.linearVelocity = new Vector3(0, 0, 0);

        // === FASE 3: Impacto — verifica se acertou o player ===
        if (playerTransform != null)
        {
            // Usa distância horizontal (ignora Y) para verificar se o player está na zona de impacto
            Vector2 slamPos2D = new Vector2(transform.position.x, transform.position.z);
            Vector2 playerPos2D = new Vector2(playerTransform.position.x, playerTransform.position.z);
            float horizontalDist = Vector2.Distance(slamPos2D, playerPos2D);

            if (horizontalDist <= sentinelSlamRadius)
            {
                // ACERTOU o player!
                sentinelSlamHitPlayer = true;
                PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(sentinelSlamDamage, gameObject);
                    Debug.Log("[SENTINELA] SLAM ACERTOU o player! Dano: " + sentinelSlamDamage);
                }

                // Knockback: empurra o player para longe
                Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    Vector3 knockDir = (playerTransform.position - transform.position).normalized;
                    knockDir.y = 0.3f; // Leve elevação
                    playerRb.AddForce(knockDir * 12f, ForceMode.Impulse);
                }
            }
            else
            {
                Debug.Log("[SENTINELA] SLAM ERROU! Player estava a " + horizontalDist.ToString("F1") + "m (raio: " + sentinelSlamRadius + "m)");
            }
        }

        // === FASE 4: Efeito visual de impacto (onda de choque) ===
        StartCoroutine(SlamImpactVFX());

        // Limpa o indicador
        if (slamIndicator != null) Destroy(slamIndicator);
        if (indicatorMat != null) Destroy(indicatorMat);

        // === FASE 5: Resultado do slam ===
        sentinelIsSlaming = false;

        if (sentinelSlamHitPlayer)
        {
            // Acertou → Sentinela NÃO fica stunado, esfera sobe imediatamente
            sentinelVulnerable = false;
            sentinelPhaseTimer = 0f;
            sentinelTargetHeight = sentinelHighHeight;
            if (health != null) health.isInvulnerable = true;
            Debug.Log("[SENTINELA] Slam acertou! Voltando para fase alta sem stun.");
        }
        else
        {
            // Errou → Sentinela fica STUNADO (vulnerável)
            sentinelVulnerable = true;
            sentinelPhaseTimer = 0f;
            sentinelTargetHeight = sentinelLowHeight;
            if (health != null) health.isInvulnerable = false;
            Debug.Log("[SENTINELA] Slam errou! STUNADO por " + sentinelVulnerableWindow + " segundos!");
        }
    }

    /// <summary>
    /// Efeito visual de onda de choque ao redor do ponto de impacto do slam.
    /// Um anel que se expande rapidamente e desaparece.
    /// </summary>
    IEnumerator SlamImpactVFX()
    {
        // Cria anel de impacto no chão
        GameObject ringObj = new GameObject("SlamImpactRing");
        LineRenderer ringLR = ringObj.AddComponent<LineRenderer>();

        // Material do anel
        Shader ringShader = Shader.Find("Sprites/Default");
        if (ringShader == null) ringShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        if (ringShader == null) ringShader = Shader.Find("Standard");
        Material ringMat = new Material(ringShader);
        ringLR.material = ringMat;
        ringLR.startWidth = 0.5f;
        ringLR.endWidth = 0.5f;
        ringLR.useWorldSpace = true;
        ringLR.loop = true;

        // Posição do impacto (no chão)
        Vector3 impactPos = transform.position;
        RaycastHit gHit;
        if (Physics.Raycast(transform.position, Vector3.down, out gHit, 20f))
            impactPos.y = gHit.point.y + 0.1f;
        else
            impactPos.y = 0.1f;

        int segments = 32;
        ringLR.positionCount = segments;

        // Animação: anel expande de 0 até sentinelSlamRadius * 1.5 em 0.4s
        float vfxDuration = 0.4f;
        float vfxElapsed = 0f;

        while (vfxElapsed < vfxDuration)
        {
            vfxElapsed += Time.deltaTime;
            float t = vfxElapsed / vfxDuration;

            float currentRadius = Mathf.Lerp(0.5f, sentinelSlamRadius * 1.5f, t);
            float alpha = Mathf.Lerp(0.8f, 0f, t);

            Color ringColor = new Color(1f, 0.3f, 0.1f, alpha);
            ringMat.color = ringColor;

            ringLR.startWidth = Mathf.Lerp(0.5f, 0.1f, t);
            ringLR.endWidth = ringLR.startWidth;

            Vector3[] ringPositions = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                ringPositions[i] = impactPos + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * currentRadius;
            }
            ringLR.SetPositions(ringPositions);

            yield return null;
        }

        // Limpa
        if (ringObj != null) Destroy(ringObj);
        if (ringMat != null) Destroy(ringMat);
    }

    /// <summary>
    /// Chamado quando o Sentinela é derrotado (10 hits).
    /// Morte permanente: dropa loot e é destruído.
    /// </summary>
    void OnSentinelDefeated()
    {
        Debug.Log("[SENTINELA] DERROTADO! Morte permanente!");

        isSweeping = false;

        // Desativa dano das pernas imediatamente
        if (mimicComponent != null)
        {
            mimicComponent.legsDealDamage = false;
        }

        // Dropar loot
        EnemyDrops drops = GetComponent<EnemyDrops>()
                        ?? GetComponentInChildren<EnemyDrops>()
                        ?? GetComponentInParent<EnemyDrops>();
        if (drops != null)
        {
            drops.OnDeath();
        }

        // Dropar essência extra (100% do valor acumulado)
        if (absorbedOreValue > 0 && drops != null && drops.essencePrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject essenceObj = Instantiate(drops.essencePrefab, spawnPos, Quaternion.identity);
            EssencePickup essencePickup = essenceObj.GetComponent<EssencePickup>();
            if (essencePickup != null)
            {
                essencePickup.essenceValue = absorbedOreValue;
            }
            Rigidbody essenceRb = essenceObj.GetComponent<Rigidbody>();
            if (essenceRb != null)
            {
                essenceRb.linearDamping = 5f;
                essenceRb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
            }
            Debug.Log("[SENTINELA] Devolveu 100% dos cristais: " + absorbedOreValue + " essência");
        }

        // Restaurar buffs roubados do player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlayerDebuffs playerDebuffs = player.GetComponent<PlayerDebuffs>();
            if (playerDebuffs != null)
            {
                playerDebuffs.RestoreStolenBuffs(gameObject);
                playerDebuffs.RemoveSlow();
            }
        }

        // Morte permanente — destruir após animação
        StartCoroutine(SentinelDeathSequence());
    }

    /// <summary>
    /// Animação de morte do Sentinela: encolhe e desaparece.
    /// </summary>
    IEnumerator SentinelDeathSequence()
    {
        // Torna invulnerável e esconde health bar
        if (health != null)
        {
            health.isInvulnerable = true;
            if (health.healthBarSlider != null)
                health.healthBarSlider.gameObject.SetActive(false);
        }

        // Para o movimento
        rb.linearVelocity = Vector3.zero;
        UpdateMimicVelocity();

        // Desativa pernas gradualmente
        if (mimicComponent != null)
        {
            mimicComponent.SetLegsActive(false);
        }

        // Animação de encolhimento + flash
        float deathDuration = 1.5f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < deathDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / deathDuration;
            float easedT = t * t;

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, easedT);

            if (geoMaterial != null)
            {
                float flash = Mathf.Sin(t * Mathf.PI * 8f) * 0.5f + 0.5f;
                Color deathColor = Color.Lerp(new Color(0.9f, 0.2f, 0.8f, 1f), Color.white, flash * (1f - t));
                geoMaterial.color = deathColor;
                geoMaterial.SetColor("_EmissionColor", deathColor * 5f * (1f - t));
                if (geoMaterial.HasProperty("_BaseColor"))
                    geoMaterial.SetColor("_BaseColor", deathColor);
            }

            yield return null;
        }

        Debug.Log("[SENTINELA] Destruído permanentemente!");
        Destroy(gameObject);
    }

    // ========================================================================
    // FUGA — [SENTINELA] (mantido para o semi-boss)
    // ========================================================================

    IEnumerator FleeSequence()
    {
        ChangeState(GeobionteState.Fleeing);

        // Torna invulnerável durante a fuga
        if (health != null)
        {
            health.isInvulnerable = true;
        }

        // Esconde health bar
        if (health != null && health.healthBarSlider != null)
        {
            health.healthBarSlider.gameObject.SetActive(false);
        }

        // Volta para layer Default (não atacável)
        gameObject.layer = LayerMask.NameToLayer("Default");

        // Restaura pernas do Mimic para forma base
        RestoreMimicLegs();

        // Encolhe de volta ao tamanho original rapidamente
        float shrinkDuration = 0.5f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);

            // Volta à cor base
            if (geoMaterial != null)
            {
                Color currentColor = Color.Lerp(transformedColor, baseColor, t);
                geoMaterial.color = currentColor;
                geoMaterial.SetColor("_EmissionColor", currentColor * 2f);
                if (geoMaterial.HasProperty("_BaseColor"))
                    geoMaterial.SetColor("_BaseColor", currentColor);
            }

            yield return null;
        }

        transform.localScale = originalScale;

        Debug.Log("[GEOBIONTE] Fuga iniciada! Correndo para longe...");

        // A fuga é gerenciada pelo FixedUpdate/MoveFlee
    }

    void HandleFleeing()
    {
        // Verifica distância do player — se longe o bastante, despawna
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer > fleeDestroyDistance)
        {
            Debug.Log("[GEOBIONTE] Fugiu com sucesso! Distância: " + distToPlayer.ToString("F1") + "m. Reaparecerá na próxima sala.");
            Destroy(gameObject);
        }
    }

    void MoveFlee()
    {
        // Corre na direção oposta ao player
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        fleeDirection.y = 0;

        Vector3 targetVelocity = fleeDirection * fleeSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        UpdateMimicVelocity();

        // Rotação na direção da fuga
        if (fleeDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(fleeDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
        }
    }

    // ========================================================================
    // RESPAWN
    // ========================================================================

    IEnumerator ScheduleRespawn()
    {
        // Não podemos usar SetActive(false) porque coroutines param em objetos desativados.
        // Em vez disso, "escondemos" o objeto: desativa renderer, collider e rigidbody.
        if (geobionteRenderer != null) geobionteRenderer.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        rb.linearVelocity = Vector3.zero;
        UpdateMimicVelocity();
        rb.isKinematic = true;

        Debug.Log("[GEOBIONTE] Respawn agendado em " + respawnDelay + " segundos...");

        yield return new WaitForSeconds(respawnDelay);

        // Verifica se o GameObject ainda existe (pode ter sido destruído pela troca de cena)
        if (this == null || gameObject == null) yield break;

        // Respawna na posição original
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        transform.localScale = originalScale;

        // Reset de estado
        currentState = GeobionteState.Idle;
        isActivated = false;
        hasSpeedBuff = false;
        hasFused = false;
        isPrevented = false;
        preventionCurrentHP = preventionMaxHP;
        absorbedOreValue = 0;
        targetOre = null;
        fieldTimer = 0f;

        // Restaura pernas do Mimic para forma base
        RestoreMimicLegs();

        // Reset de HP
        if (health != null)
        {
            health.isInvulnerable = true;
            health.ResetHealth();
        }

        // Volta à layer Default
        gameObject.layer = LayerMask.NameToLayer("Default");

        // Restaura visual base
        if (geoMaterial != null)
        {
            geoMaterial.color = baseColor;
            geoMaterial.SetColor("_EmissionColor", baseColor * 2f);
            if (geoMaterial.HasProperty("_BaseColor"))
                geoMaterial.SetColor("_BaseColor", baseColor);
        }

        // Reativa componentes
        if (geobionteRenderer != null) geobionteRenderer.enabled = true;
        if (col != null) col.enabled = true;
        rb.isKinematic = false;

        // Reinicia wandering
        PickNewWanderDirection();

        Debug.Log("[GEOBIONTE] Respawnado na posição original!");
    }

    // ========================================================================
    // BUFF (Crystal Tuner)
    // ========================================================================

    /// <summary>
    /// Chamado pelo Crystal Tuner quando buffa/desbuffa.
    /// Quando buffado: persegue mais rápido e campo de cristais tem cooldown menor.
    /// </summary>
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            chaseSpeed *= 1.3f;
            fieldCooldown *= 0.6f;
            Debug.Log("[GEOBIONTE] BUFFADO pelo Crystal Tuner! Speed +30%, Cooldown -40%");
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            chaseSpeed = originalChaseSpeed;
            fieldCooldown = originalFieldCooldown;
            Debug.Log("[GEOBIONTE] Buff do Crystal Tuner removido.");
        }
    }

    // ========================================================================
    // VISUAL
    // ========================================================================

    void SetupVisual()
    {
        geobionteRenderer = GetComponentInChildren<Renderer>();
        
        if (geobionteRenderer != null)
        {
            // Guardar referência ao mesh do corpo
            bodyMeshObject = geobionteRenderer.gameObject;
            
            // Salva escala original do mesh (completa, antes de reduzir)
            originalMeshLocalScale = geobionteRenderer.transform.localScale;

            // Se usar o Mimic, reduz a esfera para 1/3 do tamanho para servir de corpo central
            if (mimicComponent != null)
            {
                geobionteRenderer.transform.localScale *= 0.33f;
            }
            
            // Salva a escala do corpo com pernas (reduzida)
            mimicBodyLocalScale = geobionteRenderer.transform.localScale;

            // URP usa "Universal Render Pipeline/Lit", fallback para "Standard"
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            geoMaterial = new Material(shader);
            geoMaterial.color = baseColor;

            // Emissão (funciona em ambos URP e Built-in)
            geoMaterial.EnableKeyword("_EMISSION");
            // Brilho mais sutil na forma base para combinar com a cor escura
            geoMaterial.SetColor("_EmissionColor", baseColor * 0.5f);

            // Suporte URP (_BaseColor) e Built-in (_Color)
            if (geoMaterial.HasProperty("_BaseColor"))
                geoMaterial.SetColor("_BaseColor", baseColor);

            geobionteRenderer.material = geoMaterial;
        }
    }

    // ========================================================================
    // TROCA DE MESH (Esfera ↔ Cubo)
    // ========================================================================

    [Header("FBX do Bismutado (Modelo 3D com Rig)")]
    [Tooltip("Arraste o prefab/modelo FBX do Bismutado aqui (ex: bismutado.fbx). Se nulo, buscará automaticamente em Resources.")]
    public GameObject bismutadoModelPrefab;
    [Tooltip("Se verdadeiro, respeita a escala exata configurada no Prefab/FBX do Bismutado")]
    public bool usePrefabOriginalScale = true;
    [Tooltip("Escala própria do modelo 3D do Bismutado se usePrefabOriginalScale for falso")]
    public Vector3 bismutado3DModelScale = Vector3.one;
    [Tooltip("Ajuste de altura Y local do modelo do Bismutado para as pernas tocarem o chão (padrão 0.0)")]
    public float bismutadoModelOffsetY = 0f;

    [Header("Hitbox/Collider do Bismutado (Transformado)")]
    [Tooltip("Raio do SphereCollider quando transformado em Bismutado (padrão 1.8m)")]
    public float bismutadoColliderRadius = 1.8f;
    [Tooltip("Altura Y do centro do SphereCollider quando transformado em Bismutado (padrão 1.5m)")]
    public float bismutadoColliderCenterY = 1.5f;

    private SphereCollider geobionteCollider;
    private float originalColliderRadius = 0.5f;
    private Vector3 originalColliderCenter = Vector3.zero;

    /// <summary>
    /// Troca o mesh do corpo de esfera para o modelo 3D do Bismutado.
    /// Preserva a escala própria do Bismutado 3D e ajusta a altura para tocar o chão.
    /// </summary>
    void SwapBodyMeshToCube()
    {
        if (bodyMeshObject == null) return;

        // Guarda referências
        Transform parent = bodyMeshObject.transform.parent;
        Quaternion localRot = bodyMeshObject.transform.localRotation;

        // Destrói o mesh antigo (esfera)
        Destroy(bodyMeshObject);

        GameObject bismutadoModel = null;
        if (bismutadoModelPrefab != null)
        {
            bismutadoModel = Instantiate(bismutadoModelPrefab, parent);
        }
        else
        {
            GameObject loadedModel = Resources.Load<GameObject>("Bismutado") ?? Resources.Load<GameObject>("bismutado");
            if (loadedModel != null)
            {
                bismutadoModel = Instantiate(loadedModel, parent);
            }
            else
            {
                bismutadoModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }
        }

        // Determina a escala correta sem herdar o encolhimento da esfera do Geobionte
        Vector3 targetScale = bismutado3DModelScale;
        if (usePrefabOriginalScale && bismutadoModelPrefab != null)
        {
            targetScale = bismutadoModelPrefab.transform.localScale;
        }

        bismutadoModel.name = "BismutadoBody";
        bismutadoModel.transform.localPosition = new Vector3(0, bismutadoModelOffsetY, 0); // Fincado no chão!
        bismutadoModel.transform.localScale = targetScale;
        bismutadoModel.transform.localRotation = Quaternion.identity;

        // Remove qualquer script de teste manual para não interceptar os cliques do mouse ou teclas do player
        BismutadoAnimationTester tester = bismutadoModel.GetComponentInChildren<BismutadoAnimationTester>(true);
        if (tester != null)
        {
            Destroy(tester);
            Debug.Log("[BISMUTADO] Script de teste manual BismutadoAnimationTester removido para liberar controle 100% à IA!");
        }

        // Adiciona e configura a Animação Procedural do Bismutado
        BismutadoProceduralAnimation animScript = bismutadoModel.GetComponent<BismutadoProceduralAnimation>();
        if (animScript == null)
        {
            animScript = bismutadoModel.AddComponent<BismutadoProceduralAnimation>();
        }

        bodyMeshObject = bismutadoModel;
        geobionteRenderer = bismutadoModel.GetComponentInChildren<Renderer>();
        Debug.Log("[BISMUTADO] Modelo 3D com Rig, Escala do Prefab (" + targetScale + ") e alinhamento de chão ativados!");
    }

    /// <summary>
    /// Troca o mesh do corpo de cubo para esfera (reversão ao Geobionte padrão).
    /// Preserva a hierarquia, escala e material.
    /// </summary>
    void SwapBodyMeshToSphere()
    {
        if (bodyMeshObject == null) return;

        // Guarda referências
        Transform parent = bodyMeshObject.transform.parent;
        Vector3 localPos = bodyMeshObject.transform.localPosition;
        Quaternion localRot = bodyMeshObject.transform.localRotation;

        // Destrói o mesh antigo (cubo)
        Destroy(bodyMeshObject);

        // Cria esfera
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "GeobionteBody";
        sphere.transform.SetParent(parent);
        sphere.transform.localPosition = localPos;
        sphere.transform.localScale = mimicBodyLocalScale; // Usa escala reduzida para as pernas do Mimic
        sphere.transform.localRotation = localRot;

        // Remove o collider da esfera (o Geobionte já tem seu próprio collider)
        Collider sphereCol = sphere.GetComponent<Collider>();
        if (sphereCol != null) Destroy(sphereCol);

        // Aplica o material existente (com cor base)
        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer != null && geoMaterial != null)
        {
            geoMaterial.color = baseColor;
            geoMaterial.SetColor("_EmissionColor", baseColor * 0.5f);
            if (geoMaterial.HasProperty("_BaseColor"))
                geoMaterial.SetColor("_BaseColor", baseColor);
            sphereRenderer.material = geoMaterial;
        }

        // Restaura o collider original da esfera
        if (geobionteCollider == null) geobionteCollider = GetComponent<SphereCollider>();
        if (geobionteCollider != null)
        {
            geobionteCollider.radius = originalColliderRadius;
            geobionteCollider.center = originalColliderCenter;
        }

        Debug.Log("[GEOBIONTE] Mesh do corpo restaurado para ESFERA (tamanho reduzido).");
    }

    // ========================================================================
    // UTILS
    // ========================================================================

    void ChangeState(GeobionteState newState)
    {
        if (currentState == newState) return;
        Debug.Log("[GEOBIONTE] Estado: " + currentState + " → " + newState);
        currentState = newState;
    }

    // ========================================================================
    // MIMIC — HOVER (Corpo Flutuante)
    // ========================================================================

    /// <summary>
    /// Mantém o corpo do Geobionte flutuando acima do chão para que
    /// as pernas do Mimic tenham espaço para se estender.
    /// Substitui a lógica do Movement.cs original do Mimic.
    /// </summary>
    void HandleBodyHover()
    {
        if (mimicComponent == null) return;

        // Durante o slam, a coroutine SentinelSlam controla a velocity Y diretamente
        if (sentinelIsSlaming) return;

        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach(var c in cols) c.enabled = false;

        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 5f, Vector3.down, 15f);
        RaycastHit groundHit = new RaycastHit();
        bool hitGround = false;
        float highestGroundY = -float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.collider.isTrigger) continue;
            if (hit.collider.CompareTag("Player")) continue;
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.CompareTag("Enemy")) continue;

            // Encontra o chão válido mais alto abaixo de nós
            if (hit.point.y > highestGroundY)
            {
                highestGroundY = hit.point.y;
                groundHit = hit;
                hitGround = true;
            }
        }

        foreach(var c in cols) c.enabled = true;

        if (hitGround)
        {
            float currentHoverHeight;
            if (currentState == GeobionteState.Transformed && isSentinel)
            {
                // Sentinela: usa a altura alvo do ciclo alta/baixa
                currentHoverHeight = sentinelTargetHeight;
            }
            else if (currentState == GeobionteState.Transformed && !isSentinel)
            {
                // Bismutado: hover reduzido
                currentHoverHeight = bodyHoverHeight * 0.5f;
            }
            else
            {
                // Forma base: hover normal
                currentHoverHeight = bodyHoverHeight;
            }

            float targetY = groundHit.point.y + currentHoverHeight;
            float yError = targetY - transform.position.y;
            // Sentinela usa lerp mais suave para a transição alta/baixa
            float lerpSpeed = isSentinel ? hoverLerpSpeed * 0.5f : hoverLerpSpeed;
            float yVelocity = Mathf.Clamp(yError * lerpSpeed, -10f, 10f);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, yVelocity, rb.linearVelocity.z);
        }
        else
        {
            // Fallback: sem chão detectado, simula gravidade
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y - 9.81f * Time.fixedDeltaTime, rb.linearVelocity.z);
        }
    }

    // ========================================================================
    // TRANSFORMAÇÃO VISUAL: MIMIC LEGS
    // ========================================================================

    /// <summary>
    /// Aumenta pernas do Mimic para forma Bismutado.
    /// </summary>
    void TransformMimicLegs()
    {
        if (mimicComponent == null) return;

        // Aumenta número de pernas e alcance para forma Bismutado
        mimicComponent.numberOfLegs = originalNumberOfLegs * 2;
        mimicComponent.partsPerLeg = originalPartsPerLeg + 1;
        mimicComponent.newLegRadius = originalNewLegRadius * transformedScale;
        mimicComponent.RecalculateParameters();

        Debug.Log("[GEOBIONTE] Pernas Mimic transformadas para forma Bismutado!");
    }

    /// <summary>
    /// Restaura parâmetros originais do Mimic.
    /// </summary>
    void RestoreMimicLegs()
    {
        if (mimicComponent == null) return;

        mimicComponent.numberOfLegs = originalNumberOfLegs;
        mimicComponent.partsPerLeg = originalPartsPerLeg;
        mimicComponent.newLegRadius = originalNewLegRadius;
        mimicComponent.RecalculateParameters();

        Debug.Log("[GEOBIONTE] Pernas Mimic restauradas para forma base.");
    }

    /// <summary>
    /// Atualiza o velocity do Mimic para posicionamento correto das pernas.
    /// </summary>
    void UpdateMimicVelocity()
    {
        if (mimicComponent != null)
        {
            mimicComponent.velocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }
    }

    // ========================================================================
    // HEALTH BAR (criada por código)
    // ========================================================================

    /// <summary>
    /// Cria o Canvas World Space + Slider de vida + configura o DamageCanva por código.
    /// Só é chamado na transformação, pois a forma base é invulnerável.
    /// </summary>
    void CreateHealthBarIfNeeded()
    {
        if (health == null) return;

        // Se já tem slider configurado, apenas mostra
        if (health.healthBarSlider != null)
        {
            health.healthBarSlider.gameObject.SetActive(true);
            return;
        }

        // Criar Canvas World Space
        GameObject canvasObj = new GameObject("HealthBarCanvas");
        canvasObj.transform.SetParent(transform);
        
        float parentScale = transform.lossyScale.y > 0.001f ? transform.lossyScale.y : 1f;
        canvasObj.transform.localScale = (Vector3.one * 0.015f) / parentScale;
        canvasObj.transform.localPosition = new Vector3(0, 3.2f / parentScale, 0);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100; // Garante que a barra apareça à frente de todos os modelos 3D
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Billboard: faz o canvas sempre olhar para a câmera
        BillboardUI billboard = canvasObj.AddComponent<BillboardUI>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 30);

        // Background do slider
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Slider
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(canvasObj.transform, false);
        Slider slider = sliderObj.AddComponent<Slider>();
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = new Vector2(4, 4);
        sliderRect.offsetMax = new Vector2(-4, -4);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.red;
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Configurar slider
        slider.fillRect = fillRect;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;
        slider.interactable = false;
        // Remove os handles do slider (não é interativo)
        slider.transition = Selectable.Transition.None;

        // Registrar no DummyHealth
        health.healthBarSlider = slider;

        // Tentar buscar o DamageCanva_Text prefab na cena
        if (health.floatingDamageTextPrefab == null)
        {
            // Procurar em Resources ou usar referência de outro inimigo
            DummyHealth[] allHealth = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            foreach (var h in allHealth)
            {
                if (h != health && h.floatingDamageTextPrefab != null)
                {
                    health.floatingDamageTextPrefab = h.floatingDamageTextPrefab;
                    Debug.Log("[GEOBIONTE] DamageCanva_Text copiado de: " + h.gameObject.name);
                    break;
                }
            }
        }

        Debug.Log("[GEOBIONTE] Health bar criada por código!");
    }

    void OnDestroy()
    {
        // Limpa referências para evitar erros
        if (geoMaterial != null) Destroy(geoMaterial);

        // Remove qualquer slow residual no player
        if (playerTransform != null)
        {
            PlayerDebuffs debuffs = playerTransform.GetComponent<PlayerDebuffs>();
            if (debuffs != null)
            {
                debuffs.RestoreStolenBuffs(gameObject);
            }
        }
    }

    // ========================================================================
    // GIZMOS
    // ========================================================================

    void OnDrawGizmosSelected()
    {
        // Range de ativação
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        // Range de busca de minério
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, oreSearchRadius);

        // Range de ataque (campo de cristais)
        Gizmos.color = new Color(0.7f, 0.3f, 0.6f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Range de alcance do minério
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, oreReachDistance);
    }
}
