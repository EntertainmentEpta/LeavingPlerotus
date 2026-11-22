using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class DummyHealth : MonoBehaviour
{
    [Header("Debuffs (Slow)")]
    [HideInInspector] public bool isSlowed = false;
    private float slowPercent = 0f;
    private float slowTimer = 0f;

    private List<Behaviour> enemyAIBehaviours = new List<Behaviour>();
    private List<Behaviour> pausedAIBehaviours = new List<Behaviour>();
    private Animator[] allAnimators;
    private float[] savedAnimatorSpeeds;
    private BossController bossController;
    private bool isHitStopActive;
    private bool savedNavWasStopped;
    private bool endlessSpeedScalingApplied = false;
    public float AppliedEndlessMoveSpeedMultiplier { get; private set; } = 1f;
    public float AppliedEndlessAttackSpeedMultiplier { get; private set; } = 1f;

    [Header("Vida")]
    public int maxHealth = 100;
    [Tooltip("Ativa logs detalhados de dano/morte para depuração em runtime.")]
    public bool debugLogDamage = false;
    public int CurrentHealth { get; private set; }

    [Header("Elite Rebirth")]
    [Tooltip("Indica se este mob já é uma unidade elite.")]
    public bool isElite = false;
    [Tooltip("Indica se este mob já passou pelo ciclo de renascimento elite (evita renascer infinitamente).")]
    public bool hasRebornAsElite = false;
    [Tooltip("Chance deste mob renascer como Elite ao morrer (0.10 = 10%).")]
    [Range(0f, 1f)]
    public float eliteRebirthChance = 0.10f;
    [HideInInspector] public GameObject originalPrefab;
    [HideInInspector] public RoomController owningRoom;

    public static bool forceEliteRebirth = false;

    [Header("Referências UI")]
    public Slider healthBarSlider;
    // A imagem que realmente tem a cor (dentro do Slider)
    private Image healthBarFill;

    [Header("Cores da Barra")]
    public Color normalColor = Color.red;
    [Tooltip("Cor da barra quando está sendo protegido pelo Sintonizador.")]
    public Color buffedColor = Color.cyan;

    [Header("Resistências (Pilar 4)")]
    [Tooltip("Redução de dano percentual contra projéteis/tiros (ex: 0.60 = 60% de redução / recebe 40% do dano). Incentiva combate corpo a corpo.")]
    [Range(0f, 1f)]
    public float projectileResistance = 0f;

    [Tooltip("Resistência a empurrão / knockback (ex: 0.70 = 70% de redução do impulso recebido).")]
    [Range(0f, 1f)]
    public float knockbackResistance = 0f;

    [Header("Feedback de Dano")]
    public GameObject floatingDamageTextPrefab;
    public Vector3 textOffset = new Vector3(0, 2f, 0);
    public Color hitColor = Color.red;
    public float hitFlashTime = 0.2f;

    [HideInInspector] public bool isInvulnerable = false;
    [HideInInspector] public bool isBuffed = false;

    [Header("Biomassa do Traje")]
    [Tooltip("Se falso, este alvo não concede Biomassa ao jogador (ex: mortes forçadas por script/cheat).")]
    public bool grantsBiomass = true;
    private const float BiomassPerHit = 2f;
    private const float BiomassPerKill = 8f;
    private const float BiomassPerEliteKill = 40f;
    private bool biomassKillRewarded = false;

    /// <summary>
    /// Se > 0, cada hit causa exatamente este valor de dano, independente do dano da arma.
    /// Usado pelo Geobionte para que 7 hits = 7 HP (1 dano por hit).
    /// Valor padrão 0 = comportamento normal (usa o dano real da arma).
    /// </summary>
    [HideInInspector] public int fixedDamageOverride = 0;

    /// <summary>
    /// Se definido, chama este callback ao invés da lógica padrão de morte (drops + Destroy).
    /// Usado pelo Geobionte para substituir morte por fuga.
    /// Não afeta nenhum inimigo que não defina esse campo (null por padrão).
    /// </summary>
    [HideInInspector] public System.Action onDeathOverride = null;

    /// <summary>
    /// Evento disparado sempre que este objeto recebe dano de um ataque.
    /// </summary>
    [HideInInspector] public System.Action<int, GameObject> onDamageTaken = null;

    /// <summary>
    /// Evento disparado quando este inimigo morre (antes de ser destruído).
    /// </summary>
    [HideInInspector] public System.Action onDeath = null;

    private Color originalRenderColor;
    private Color originalBaseColor;
    private bool hasBaseColor = false;
    private Renderer dummyRenderer;
    private Animator animator;
    private NavMeshAgent navAgent;
    private Rigidbody rb;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (floatingDamageTextPrefab == null)
        {
#if UNITY_EDITOR
            floatingDamageTextPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/DamageVFX_CFXR.prefab");
#endif
        }
    }

    private void Start()
    {
        // Aplica escalonamento de HP por sala (+3% por sala avançada conforme Hp_Dano_Inimigos.pdf / GDD)
        if (RunManager.instance != null && RunManager.instance.currentRoomNumber > 1)
        {
            int roomIndex = RunManager.instance.currentRoomNumber;
            float hpScalingMultiplier = 1f + (0.03f * (roomIndex - 1));
            maxHealth = Mathf.RoundToInt(maxHealth * hpScalingMultiplier);
            CurrentHealth = maxHealth;
        }

        // Se estivermos no modo Endless e o level for maior que 3, aumenta a vida máxima do inimigo
        if (RunManager.instance != null && RunManager.instance.isEndlessMode && RunManager.instance.currentLevel > 3)
        {
            float hpMultiplier = RunManager.instance.GetEndlessHpMultiplier(); // +15% HP por level acima do 3
            maxHealth = Mathf.RoundToInt(maxHealth * hpMultiplier);
            CurrentHealth = maxHealth;
        }

        if (GetComponent<EliteVariantConfig>() != null || gameObject.name.ToLower().Contains("elite"))
        {
            isElite = true;
        }

        dummyRenderer = GetComponentInChildren<Renderer>();

        animator = GetComponentInChildren<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        if (dummyRenderer != null)
        {
            originalRenderColor = dummyRenderer.material.color;
            // Suporte a shaders URP/HDRP que usam _BaseColor em vez de _Color
            if (dummyRenderer.material.HasProperty("_BaseColor"))
            {
                hasBaseColor = true;
                originalBaseColor = dummyRenderer.material.GetColor("_BaseColor");
            }
        }

        // Auto-busca o Slider nos filhos se não foi atribuído no Inspector
        if (healthBarSlider == null)
        {
            healthBarSlider = GetComponentInChildren<Slider>(true); // true = inclui desativados

            if (healthBarSlider == null)
                Debug.LogWarning("[" + gameObject.name + "] Slider de vida não encontrado! Crie um Canvas (World Space) com um Slider filho.");
            else
                Debug.Log("[" + gameObject.name + "] Slider encontrado automaticamente: " + healthBarSlider.gameObject.name);
        }

        // Busca a imagem Fill dentro do Slider para controlar a cor
        if (healthBarSlider != null)
        {
            if (healthBarSlider.fillRect != null)
            {
                healthBarFill = healthBarSlider.fillRect.GetComponent<Image>();
            }
            // Garante a cor inicial
            if (healthBarFill != null) healthBarFill.color = normalColor;

            // Começa escondida — só aparece após o primeiro hit
            healthBarSlider.gameObject.SetActive(false);
        }

        UpdateHealthBar();
        DetectAIScripts();
        ApplyEndlessSpeedScalingIfNeeded();
    }

    /// <summary>
    /// Aplica escalonamento de velocidade de movimento e ataque no Modo Endless (andar + Mutação do Abismo).
    /// Aplicado uma única vez no Start, preservando 100% de compatibilidade com ApplySlow / ApplyStun / ScaleAISpeeds.
    /// No Modo Normal (isEndlessMode == false), não altera nada.
    /// </summary>
    private void ApplyEndlessSpeedScalingIfNeeded()
    {
        if (endlessSpeedScalingApplied) return;
        if (RunManager.instance == null || !RunManager.instance.isEndlessMode) return;

        float moveMult = RunManager.instance.GetEndlessEnemyMoveSpeedMultiplier();
        float attackMult = RunManager.instance.GetEndlessEnemyAttackSpeedMultiplier();

        AppliedEndlessMoveSpeedMultiplier = moveMult;
        AppliedEndlessAttackSpeedMultiplier = attackMult;
        endlessSpeedScalingApplied = true;

        if (moveMult > 1.001f)
        {
            ScaleAISpeeds(moveMult);
        }

        if (attackMult > 1.001f)
        {
            ApplyEndlessAttackSpeedScaling(attackMult);
        }
    }

    private void ApplyEndlessAttackSpeedScaling(float attackMult)
    {
        float inv = 1f / Mathf.Max(1f, attackMult);

        if (TryGetComponent<Golem_AI>(out var golem))
        {
            golem.meleeCooldown = Mathf.Max(0.45f, golem.meleeCooldown * inv);
            golem.stunCooldown = Mathf.Max(4.0f, golem.stunCooldown * inv);
        }
        if (TryGetComponent<Spider_AI>(out var spider))
        {
            spider.leapCooldown = Mathf.Max(0.9f, spider.leapCooldown * inv);
            spider.retreatCooldown = Mathf.Max(2.0f, spider.retreatCooldown * inv);
        }
        if (TryGetComponent<GoblinAI_Transform>(out var goblin))
        {
            goblin.throwCooldown = Mathf.Max(1.1f, goblin.throwCooldown * inv);
            goblin.meleeCooldown = Mathf.Max(0.65f, goblin.meleeCooldown * inv);
        }
        if (TryGetComponent<PeixeAI>(out var peixe))
        {
            peixe.shootCooldown = Mathf.Max(1.0f, peixe.shootCooldown * inv);
        }
        if (TryGetComponent<SharpBlur>(out var sharpBlur))
        {
            sharpBlur.dashCooldown = Mathf.Max(1.5f, sharpBlur.dashCooldown * inv);
        }
        if (TryGetComponent<Cristalus_AI>(out var cristalus))
        {
            cristalus.arcCooldown = Mathf.Max(1.2f, cristalus.arcCooldown * inv);
        }
        if (TryGetComponent<CrystalWatcher_AI>(out var watcher))
        {
            watcher.cooldownTime = Mathf.Max(0.9f, watcher.cooldownTime * inv);
        }
        if (TryGetComponent<CrystalDragon_AI>(out var dragon))
        {
            dragon.spikeCooldownMin = Mathf.Max(0.9f, dragon.spikeCooldownMin * inv);
            dragon.spikeCooldownMax = Mathf.Max(dragon.spikeCooldownMin + 0.5f, dragon.spikeCooldownMax * inv);
            dragon.tailCooldown = Mathf.Max(0.9f, dragon.tailCooldown * inv);
            dragon.flightCooldown = Mathf.Max(4.0f, dragon.flightCooldown * inv);
        }
        if (TryGetComponent<CrystalDragonCommon_AI>(out var commonDragon))
        {
            commonDragon.cooldownAtaque = Mathf.Max(1.2f, commonDragon.cooldownAtaque * inv);
            commonDragon.spinCooldown = Mathf.Max(2.5f, commonDragon.spinCooldown * inv);
        }
        if (TryGetComponent<Geobionte_AI>(out var geo))
        {
            geo.sweepCooldown = Mathf.Max(0.6f, geo.sweepCooldown * inv);
            geo.fieldCooldown = Mathf.Max(3.0f, geo.fieldCooldown * inv);
        }
        if (TryGetComponent<MagicStone_AI>(out var stone))
        {
            stone.teleportCooldown = Mathf.Max(3.5f, stone.teleportCooldown * inv);
        }

        if (allAnimators != null)
        {
            for (int i = 0; i < allAnimators.Length; i++)
            {
                if (allAnimators[i] != null && allAnimators[i].speed > 0.001f)
                {
                    allAnimators[i].speed = Mathf.Min(allAnimators[i].speed * attackMult, 1.45f);
                }
            }
        }
    }

    private void DetectAIScripts()
    {
        CollectEnemyAIBehaviours(gameObject, enemyAIBehaviours);
        allAnimators = GetComponentsInChildren<Animator>(true);
        bossController = GetComponent<BossController>() ?? GetComponentInParent<BossController>();

        bool hasGolem = GetComponent<Golem_AI>() != null;
        bool hasCristalus = GetComponent<Cristalus_AI>() != null;
        bool hasGeobionte = GetComponent<Geobionte_AI>() != null;
        bool hasPeixe = TryGetComponent<PeixeAI>(out _);
        bool hasMagicStone = GetComponent<MagicStone_AI>() != null;

        // Configuração de resistências padrão por arquétipo (Pilar 4) caso não customizado no Inspector
        string lowerName = gameObject.name.ToLower();
        if (hasGolem || lowerName.Contains("golem"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.60f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.95f;
        }
        else if (hasCristalus || lowerName.Contains("cristalus"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.40f;
        }
        else if (hasGeobionte || lowerName.Contains("geobionte"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.30f;
        }
        else if (hasPeixe || lowerName.Contains("peixe"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.35f;
        }
        else if (hasMagicStone || lowerName.Contains("magicstone") || lowerName.Contains("star"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.50f;
        }

        // Garante que o inimigo possui EnemyIdentity e DetectorDeInimigo para o Bestiario
        if (!BestiarioManager.IsInimigoInvalido(gameObject.name))
        {
            EnemyIdentity eid = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
            if (eid == null)
            {
                eid = gameObject.AddComponent<EnemyIdentity>();
            }
            if (GetComponent<DetectorDeInimigo>() == null)
            {
                DetectorDeInimigo det = gameObject.AddComponent<DetectorDeInimigo>();
                det.enemyIdentity = eid;
                det.usarDistancia = true;
                det.distanciaDeteccao = 25f;
            }
        }
    }

    // --- NOVA FUNÇÃO CHAMADA PELO SINTONIZADOR ---
    public void SetBuffedStatus(bool buffed)
    {
        isBuffed = buffed;

        // Muda a cor da barra
        if (healthBarFill != null)
        {
            healthBarFill.color = buffed ? buffedColor : normalColor;
        }
    }

    /// <summary>
    /// Reseta o HP para o valor máximo. Usado pelo Geobionte no respawn.
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void TakeDamage(int damage, bool isCritical = false, bool isPoison = false, bool isProjectile = false)
    {
        // Peixe: respeita invulnerabilidade do estado Defend
        if (TryGetComponent<PeixeAI>(out PeixeAI peixeAI) && peixeAI.isInvulnerable) return;

        if (isInvulnerable) return;
        if (CurrentHealth <= 0) return;

        // Ao ser atingido pelo jogador, garante registro imediato no Bestiario
        if (BestiarioManager.instancia != null && !BestiarioManager.IsInimigoInvalido(gameObject.name))
        {
            EnemyIdentity eid = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
            if (eid != null && !eid.foiEncontrado)
            {
                BestiarioManager.instancia.Registrar(eid);
            }
        }

        // Pilar 4: Inimigos blindados/com couraça reduzem dano de projéteis
        if (isProjectile && projectileResistance > 0f)
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - projectileResistance)));
        }

        Debug.Log($"💥 [DAMAGE LOG] {gameObject.name} recebeu {damage} de dano! (HP antes: {CurrentHealth}/{maxHealth} -> HP restante: {Mathf.Max(0, CurrentHealth - damage)})");

        // Se fixedDamageOverride está ativo, cada hit causa exatamente esse valor
        if (fixedDamageOverride > 0)
            damage = fixedDamageOverride;

        // Blueprint: Solenoide de Quebra-Casca (+20% de dano contra monstros pesados/couraça)
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.HasShellBreaker)
        {
            string n = gameObject.name.ToLower();
            if (n.Contains("golem") || n.Contains("peixe") || n.Contains("geobionte") || n.Contains("bismutado") || n.Contains("magicstone") || n.Contains("star"))
            {
                damage = Mathf.RoundToInt(damage * 1.20f);
            }
        }

        // Se estiver buffado pelo Sintonizador:
        if (isBuffed)
        {
            // Se o jogador equipou o Estabilizador de Frequência do Robozinho, inibe o escudo defensivo do Sintonizador
            float buffFactor = (EquipmentManager.Instance != null && EquipmentManager.Instance.HasTunerDisruptor) ? 0.80f : 0.50f;
            damage = Mathf.RoundToInt(damage * buffFactor);
        }

        Tier4EffectManager tunerManager = FindFirstObjectByType<Tier4EffectManager>();
        damage = tunerManager != null ? tunerManager.ModifyIncomingDamage(gameObject, damage) : damage;
        CurrentHealth -= damage;
        RunStatsManager.Instance?.RecordDamageDealt(damage);

        onDamageTaken?.Invoke(damage, null);

        // Biomassa do traje: +2% por golpe válido (DoT de veneno não conta como golpe)
        if (grantsBiomass && !isPoison && damage > 0 && SuitHealingSystem.Instance != null)
        {
            SuitHealingSystem.Instance.AddBiomass(BiomassPerHit);
        }

        UpdateHealthBar();
        ShowHealthBar();

        if (floatingDamageTextPrefab == null)
        {
            DummyHealth[] others = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            foreach (var other in others)
            {
                if (other != this && other.floatingDamageTextPrefab != null)
                {
                    floatingDamageTextPrefab = other.floatingDamageTextPrefab;
                    break;
                }
            }
        }

        if (floatingDamageTextPrefab != null)
        {
            GameObject textObject = Instantiate(floatingDamageTextPrefab, transform.position + textOffset, Quaternion.identity);
            FloatingDamageText dmgScript = textObject.GetComponent<FloatingDamageText>();
            if (dmgScript != null)
            {
                dmgScript.SetDamage(damage, isCritical, isPoison);
            }
        }

        // Hit-Stop só em golpe corpo a corpo (não dispara com tiro nem veneno)
        if (!isProjectile && !isPoison)
            ApplyHitStop(isCritical ? 0.12f : 0.08f);

        if (dummyRenderer != null)
        {
            if (isPoison)
                StartCoroutine(FlashPoisonGreen());
            else if (!isProjectile)
                StartCoroutine(FlashWhite());
            else
                StartCoroutine(FlashRed());
        }

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            tunerManager?.NotifyEnemyDamaged(gameObject, true);
            Die();
        }
        else
        {
            tunerManager?.NotifyEnemyDamaged(gameObject, false);
        }
    }

    public void SetHealth(int value)
    {
        CurrentHealth = Mathf.Clamp(value, 0, maxHealth);
        UpdateHealthBar();
    }

    public void ShowHealthBar()
    {
        if (healthBarSlider == null) return;
        healthBarSlider.gameObject.SetActive(true);
    }

    public void UpdateHealthBar()
    {
        if (healthBarSlider != null)
        {
            float healthPercent = (float)CurrentHealth / maxHealth;
            healthBarSlider.value = healthPercent;
            if (CurrentHealth <= 0) healthBarSlider.gameObject.SetActive(false);
        }
    }

    private bool isFlashing = false;
    private Renderer[] flashRends;
    private Color[] origBase, origTint, origEmis;
    private bool[] hasBase, hasTint, hasEmis;

    IEnumerator FlashRed()
    {
        yield return FlashColor(hitColor, hitFlashTime);
    }

    IEnumerator FlashWhite()
    {
        yield return FlashColor(Color.white, 0.08f);
    }

    IEnumerator FlashColor(Color flashColor, float duration)
    {
        if (!isFlashing)
        {
            flashRends = CollectFlashMeshRenderers(gameObject);
            int n = flashRends.Length;
            origBase = new Color[n]; origTint = new Color[n]; origEmis = new Color[n];
            hasBase = new bool[n]; hasTint = new bool[n]; hasEmis = new bool[n];

            for (int i = 0; i < n; i++)
            {
                if (flashRends[i] == null) continue;
                Material m = flashRends[i].material;
                hasBase[i] = m.HasProperty("_BaseColor");
                hasTint[i] = m.HasProperty("_Color");
                hasEmis[i] = m.HasProperty("_EmissionColor");
                if (hasBase[i]) origBase[i] = m.GetColor("_BaseColor");
                if (hasTint[i]) origTint[i] = m.color;
                if (hasEmis[i]) origEmis[i] = m.GetColor("_EmissionColor");
            }
            isFlashing = true;
        }

        for (int i = 0; i < flashRends.Length; i++)
        {
            if (flashRends[i] == null) continue;
            Material m = flashRends[i].material;
            if (hasBase[i]) m.SetColor("_BaseColor", flashColor);
            if (hasTint[i]) m.color = flashColor;
            if (hasEmis[i]) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", flashColor * 0.8f); }
        }

        yield return new WaitForSeconds(duration);

        for (int i = 0; i < flashRends.Length; i++)
        {
            if (flashRends[i] == null) continue;
            Material m = flashRends[i].material;
            if (hasBase[i]) m.SetColor("_BaseColor", origBase[i]);
            if (hasTint[i]) m.color = origTint[i];
            if (hasEmis[i]) m.SetColor("_EmissionColor", origEmis[i]);
        }
        isFlashing = false;
    }

    IEnumerator FlashPoisonGreen()
    {
        Color poisonFlash = new Color(0.18f, 0.95f, 0.22f, 1f);
        dummyRenderer.material.color = poisonFlash;
        if (hasBaseColor) dummyRenderer.material.SetColor("_BaseColor", poisonFlash);
        yield return new WaitForSeconds(hitFlashTime);
        dummyRenderer.material.color = originalRenderColor;
        if (hasBaseColor) dummyRenderer.material.SetColor("_BaseColor", originalBaseColor);
    }


    private Coroutine hitStopCo;

    public void ApplyHitStop(float duration = 0.08f)
    {
        if (!gameObject.activeInHierarchy || CurrentHealth <= 0) return;

        // Ignora apenas se estiver realmente no alto (salto/queda forte); desníveis de rampa não bloqueiam
        if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 1.5f) return;

        if (hitStopCo != null)
        {
            StopCoroutine(hitStopCo);
            RestoreHitStopState();
        }
        hitStopCo = StartCoroutine(HitStopRoutine(duration));
    }

    private void OnDisable()
    {
        RestoreHitStopState();
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopActive = true;

        if (enemyAIBehaviours.Count == 0 && bossController == null)
        {
            CollectEnemyAIBehaviours(gameObject, enemyAIBehaviours);
        }
        if (allAnimators == null || allAnimators.Length == 0)
        {
            allAnimators = GetComponentsInChildren<Animator>(true);
        }

        // Congela todos os Animators do inimigo preservando a velocidade original de cada um
        savedAnimatorSpeeds = new float[allAnimators.Length];
        for (int i = 0; i < allAnimators.Length; i++)
        {
            if (allAnimators[i] != null)
            {
                savedAnimatorSpeeds[i] = allAnimators[i].speed;
                allAnimators[i].speed = 0f;
            }
        }

        if (bossController != null)
        {
            bossController.TriggerHitstop(duration);
        }

        // Trava o movimento via NavMeshAgent
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            savedNavWasStopped = navAgent.isStopped;
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        // Desativa temporariamente apenas os scripts de IA que estavam ativos
        PauseActiveAIBehaviours(enemyAIBehaviours, pausedAIBehaviours);

        yield return new WaitForSecondsRealtime(duration);

        RestoreHitStopState();
        hitStopCo = null;
    }

    private void RestoreHitStopState()
    {
        if (!isHitStopActive) return;
        isHitStopActive = false;

        if (allAnimators != null && savedAnimatorSpeeds != null)
        {
            int count = Mathf.Min(allAnimators.Length, savedAnimatorSpeeds.Length);
            for (int i = 0; i < count; i++)
            {
                if (allAnimators[i] != null)
                {
                    allAnimators[i].speed = savedAnimatorSpeeds[i] > 0.001f ? savedAnimatorSpeeds[i] : 1f;
                }
            }
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = savedNavWasStopped;
        }

        ResumePausedAIBehaviours(pausedAIBehaviours);
    }

    /// <summary>
    /// Coleta apenas MeshRenderer e SkinnedMeshRenderer para evitar alterar materiais de partículas/trails no flash.
    /// </summary>
    internal static Renderer[] CollectFlashMeshRenderers(GameObject root)
    {
        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
        List<Renderer> filtered = new List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is MeshRenderer || all[i] is SkinnedMeshRenderer)
            {
                filtered.Add(all[i]);
            }
        }
        return filtered.ToArray();
    }

    /// <summary>
    /// Coleta todos os scripts de IA e animação procedural de inimigos presentes na hierarquia.
    /// </summary>
    internal static void CollectEnemyAIBehaviours(GameObject root, List<Behaviour> outList)
    {
        outList.Clear();
        TryAddBehaviours<Geobionte_AI>(root, outList);
        TryAddBehaviours<Golem_AI>(root, outList);
        TryAddBehaviours<Spider_AI>(root, outList);
        TryAddBehaviours<GoblinAI_Transform>(root, outList);
        TryAddBehaviours<ShardSwarm_AI>(root, outList);
        TryAddBehaviours<MagicStone_AI>(root, outList);
        TryAddBehaviours<CrystalTuner>(root, outList);
        TryAddBehaviours<Cristalus_AI>(root, outList);
        TryAddBehaviours<CrystalWatcher_AI>(root, outList);
        TryAddBehaviours<PeixeAI>(root, outList);
        TryAddBehaviours<CrystalDragon_AI>(root, outList);
        TryAddBehaviours<CrystalDragonCommon_AI>(root, outList);
        TryAddBehaviours<SharpBlur>(root, outList);
        TryAddBehaviours<TotemSpawner>(root, outList);
        TryAddBehaviours<BismutadoProceduralAnimation>(root, outList);
        TryAddBehaviours<GolemProceduralAnimation>(root, outList);
        TryAddBehaviours<SpiderProceduralAnimation>(root, outList);
    }

    private static void TryAddBehaviours<T>(GameObject root, List<Behaviour> outList) where T : Behaviour
    {
        T[] found = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && !outList.Contains(found[i]))
            {
                outList.Add(found[i]);
            }
        }
    }

    internal static void PauseActiveAIBehaviours(List<Behaviour> candidates, List<Behaviour> pausedOut)
    {
        pausedOut.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            Behaviour b = candidates[i];
            if (b != null && b.enabled)
            {
                b.enabled = false;
                pausedOut.Add(b);
            }
        }
    }

    internal static void ResumePausedAIBehaviours(List<Behaviour> paused)
    {
        for (int i = 0; i < paused.Count; i++)
        {
            if (paused[i] != null)
            {
                paused[i].enabled = true;
            }
        }
        paused.Clear();
    }

    private void Die()
    {
        try { onDeath?.Invoke(); } catch (System.Exception ex) { Debug.LogError($"[DummyHealth] Erro em onDeath: {ex}"); }

        try { RunStatsManager.Instance?.RecordEnemyKilled(); } catch (System.Exception ex) { Debug.LogError($"[DummyHealth] Erro em RecordEnemyKilled: {ex}"); }

        // Se um override foi definido (ex: Geobionte usa fuga ao invés de morte),
        // chama o callback e retorna sem executar a lógica padrão.
        if (onDeathOverride != null)
        {
            try { onDeathOverride.Invoke(); } catch (System.Exception ex) { Debug.LogError($"[DummyHealth] Erro em onDeathOverride: {ex}"); }
            return;
        }

        Debug.Log(gameObject.name + " foi destruído.");
        if (debugLogDamage)
        {
            Debug.Log("[DummyHealth] Death stack:\n" + System.Environment.StackTrace);
        }

        // --- BIOMASSA DO TRAJE: +8% (comum) / +40% (Elite), apenas 1x por inimigo ---
        if (!biomassKillRewarded)
        {
            biomassKillRewarded = true;
            try
            {
                if (grantsBiomass && SuitHealingSystem.Instance != null)
                {
                    SuitHealingSystem.Instance.AddBiomass(isElite ? BiomassPerEliteKill : BiomassPerKill);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DummyHealth] Erro ao conceder Biomassa por kill: {ex}");
            }
        }

        // --- SISTEMA DE PACTOS DO JOGADOR ---
        try
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.lastKillTime = Time.time;        // Checa Vampirismo (GDD §4.1 - Carta O Parasita)
                if (playerHealth != null && playerHealth.currentHealth > 0 && playerHealth.hasVampirism)
                {
                    Debug.Log("[DUMMY] Inimigo morreu! Curando Player (Vampirismo).");
                    playerHealth.Heal(5); // Cura fixa de 5 (ajustável se quiser balancear)
                    playerHealth.lastKillTime = Time.time; // Reseta a degeneração!
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DummyHealth] Erro em pactos de jogador: {ex}");
        }
        // ------------------------------------

        // Chama o sistema de drops se existir
        // Busca em filhos e pais também — cobre hierarquias mais complexas de prefab
        try
        {
            EnemyDrops drops = GetComponent<EnemyDrops>()
                            ?? GetComponentInChildren<EnemyDrops>()
                            ?? GetComponentInParent<EnemyDrops>();
            if (drops != null)
            {
                Debug.Log("[DROPS] EnemyDrops encontrado, chamando OnDeath()...");
                drops.OnDeath();
            }
            else
            {
                Debug.LogWarning("[DROPS] EnemyDrops NÃO encontrado em " + gameObject.name + "! Adicionando fallback em runtime.");
                drops = gameObject.AddComponent<EnemyDrops>();
                drops.OnDeath();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DummyHealth] Erro ao disparar EnemyDrops em {gameObject.name}: {ex}");
        }

        // --- RECOMPENSA DE COMBATE DA SINFONIA DO CAOS (Pacto do Mercador + Mutação do Abismo) ---
        try
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null && !playerHealth.isDead)
            {
                bool hasMerchantChaos = isElite && playerHealth.hasChaosSymphony;
                float chaosDuration = 5f;
                float chaosMoveBonus = 0.15f;
                float chaosAttackBonus = 0.20f;
                float chaosTriggerChance = 0f;
                bool hasAbyssChaos = RunManager.instance != null &&
                                     RunManager.instance.TryGetChaosMutationAdrenalineParams(
                                         isElite,
                                         out chaosDuration,
                                         out chaosMoveBonus,
                                         out chaosAttackBonus,
                                         out chaosTriggerChance);

                if (hasMerchantChaos && hasAbyssChaos)
                {
                    playerHealth.Heal(10);
                    playerHealth.TriggerEliteKillAdrenaline(chaosDuration + 1.5f, chaosMoveBonus + 0.05f, chaosAttackBonus + 0.05f);
                    Debug.Log("[CAOS+] Elite abatido com Pacto + Mutação Sinfonia do Caos! +10 Vida e Adrenalina amplificada!");
                }
                else if (hasMerchantChaos)
                {
                    playerHealth.Heal(8);
                    playerHealth.TriggerEliteKillAdrenaline(5f);
                    Debug.Log("[CAOS] Inimigo Elite abatido! +8 Vida e Adrenalina concedidas ao jogador!");
                }
                else if (hasAbyssChaos && Random.value <= chaosTriggerChance)
                {
                    if (isElite) playerHealth.Heal(6);
                    playerHealth.TriggerEliteKillAdrenaline(chaosDuration, chaosMoveBonus, chaosAttackBonus);
                    Debug.Log($"[MUTAÇÃO CAOS] Adrenalina ativada ({chaosDuration:F1}s | Elite:{isElite})!");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DummyHealth] Erro em recompensa da Sinfonia do Caos: {ex}");
        }

        // --- SISTEMA DE RENASCIMENTO ELITE (10% base / 100% com cheat / +25% Pacto Caos / +Endless & Horda de Elites) ---
        bool shouldReborn = false;
        try
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            bool canReborn = (!isElite && !hasRebornAsElite &&
                              GetComponent<EliteVariantConfig>() == null &&
                              !gameObject.name.ToLower().Contains("elite") &&
                              !gameObject.name.ToLower().Contains("boss") &&
                              !gameObject.name.ToLower().Contains("geobionte") &&
                              !gameObject.name.ToLower().Contains("sentinel") &&
                              GetComponent<Geobionte_AI>() == null);

            float effectiveRebirthChance = eliteRebirthChance;
            if (playerHealth != null && playerHealth.hasChaosSymphony)
            {
                effectiveRebirthChance = Mathf.Clamp01(effectiveRebirthChance + 0.25f);
            }
            if (RunManager.instance != null && RunManager.instance.isEndlessMode)
            {
                effectiveRebirthChance = Mathf.Clamp(effectiveRebirthChance + RunManager.instance.GetEndlessEliteRebirthBonus(), 0f, 0.70f);
            }

            shouldReborn = canReborn && (forceEliteRebirth || Random.value <= effectiveRebirthChance);
            if (shouldReborn)
            {
                TriggerEliteRebirth();
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DummyHealth] Erro ao calcular renascimento Elite em {gameObject.name}: {ex}");
        }

        Destroy(gameObject);
    }

    private void TriggerEliteRebirth()
    {
        Debug.Log($"[ELITE REBIRTH] ★ {gameObject.name} iniciou o ciclo de renascimento Elite!");
        ShowRebirthFloatingText(transform.position);

        RoomController rc = FindRoomController();
        GameObject prefabRef = originalPrefab;
        if (prefabRef == null && rc != null)
        {
            string clean = gameObject.name.Replace("(Clone)", "").Trim();
            var allPrefabs = rc.GetAllUniqueEnemyPrefabs();
            if (allPrefabs != null)
            {
                prefabRef = allPrefabs.Find(p => p != null && p.name.Equals(clean, System.StringComparison.OrdinalIgnoreCase));
            }
        }

        if (rc != null && rc.gameObject.activeInHierarchy)
        {
            rc.enemiesPendingSpawn++;
            rc.activeEnemies.Remove(gameObject);
            rc.StartCoroutine(rc.RebirthAsEliteRoutine(transform.position, transform.rotation, prefabRef != null ? prefabRef : gameObject, gameObject.name));
        }
        else if (RunManager.instance != null && RunManager.instance.gameObject.activeInHierarchy)
        {
            RunManager.instance.StartCoroutine(RebirthFallbackRoutine(transform.position, transform.rotation, prefabRef != null ? prefabRef : gameObject, gameObject.name));
        }

        Destroy(gameObject);
    }

    public void ShowRebirthFloatingText(Vector3 position)
    {
        if (floatingDamageTextPrefab == null)
        {
            DummyHealth[] others = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            foreach (var other in others)
            {
                if (other != this && other.floatingDamageTextPrefab != null)
                {
                    floatingDamageTextPrefab = other.floatingDamageTextPrefab;
                    break;
                }
            }
        }

        if (floatingDamageTextPrefab != null)
        {
            GameObject textObject = Instantiate(floatingDamageTextPrefab, position + textOffset, Quaternion.identity);
            FloatingDamageText dmgScript = textObject.GetComponent<FloatingDamageText>();
            if (dmgScript != null)
            {
                dmgScript.SetCustomMessage("★ ELITE RENASCIDO! ★", new Color(1f, 0.55f, 0f, 1f), 1.5f);
            }
        }
    }

    public RoomController FindRoomController()
    {
        if (owningRoom != null) return owningRoom;

        RoomController[] allRooms = FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        foreach (var r in allRooms)
        {
            if (r != null && r.combatActive && r.activeEnemies.Contains(gameObject))
            {
                owningRoom = r;
                return r;
            }
        }
        RoomController closest = null;
        float minDist = 50f;
        foreach (var r in allRooms)
        {
            if (r != null && r.combatActive)
            {
                float d = Vector3.Distance(r.transform.position, transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = r;
                }
            }
        }
        if (closest != null)
        {
            owningRoom = closest;
            return closest;
        }

        return null;
    }

    public static IEnumerator RebirthFallbackRoutine(Vector3 spawnPos, Quaternion spawnRot, GameObject sourcePrefabOrInstance, string originalName)
    {
        yield return new WaitForSeconds(0.6f);
        if (sourcePrefabOrInstance == null) yield break;

        string cleanName = originalName.Replace("(Clone)", "").Trim();
        GameObject prefabToSpawn = sourcePrefabOrInstance;

        EnemyPoolConfig pool = Resources.Load<EnemyPoolConfig>("DefaultEnemyPool");
        if (pool != null)
        {
            EnemySpawnSetup setup = pool.GetMutationSetup(sourcePrefabOrInstance);
            if (setup != null && setup.variantPrefab != null) prefabToSpawn = setup.variantPrefab;
            else
            {
                foreach (var s in pool.mutationSetups)
                {
                    if (s != null && s.basePrefab != null && s.variantPrefab != null && s.basePrefab.name.Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        prefabToSpawn = s.variantPrefab;
                        break;
                    }
                }
            }
        }

        GameObject rebornEnemy = Object.Instantiate(prefabToSpawn, spawnPos, spawnRot);
        rebornEnemy.name = cleanName + "_RebornElite";

        bool hasDedicatedVariant = (sourcePrefabOrInstance != null &&
                                    sourcePrefabOrInstance.GetComponent<EliteVariantConfig>() != null &&
                                    !sourcePrefabOrInstance.name.Contains("_RebornElite"));

        DummyHealth dh = rebornEnemy.GetComponent<DummyHealth>() ?? rebornEnemy.GetComponentInChildren<DummyHealth>();
        if (dh != null)
        {
            dh.isElite = true;
            dh.hasRebornAsElite = true;
            if (!hasDedicatedVariant)
            {
                dh.maxHealth = Mathf.RoundToInt(dh.maxHealth * 1.6f);
            }
            dh.SetHealth(dh.maxHealth);
        }

        ShardSwarmHealth ssh = rebornEnemy.GetComponent<ShardSwarmHealth>() ?? rebornEnemy.GetComponentInChildren<ShardSwarmHealth>();
        if (ssh != null)
        {
            ssh.isElite = true;
            ssh.hasRebornAsElite = true;
            if (!hasDedicatedVariant)
            {
                ssh.maxHealth = Mathf.RoundToInt(ssh.maxHealth * 1.6f);
            }
            ssh.SetHealth(ssh.maxHealth);
        }

        if (rebornEnemy.GetComponent<EliteVariantConfig>() == null)
        {
            EliteVariantConfig aura = rebornEnemy.AddComponent<EliteVariantConfig>();
            aura.variantName = cleanName + " Elite";
            aura.useProceduralAura = true;
            aura.auraColor = new Color(1f, 0.45f, 0f, 1f);
            aura.auraRadius = 0.85f;
            aura.auraWidth = 0.09f;
        }

        if (!hasDedicatedVariant)
        {
            rebornEnemy.transform.localScale *= 1.25f;
        }

        EnemyDrops drops = rebornEnemy.GetComponent<EnemyDrops>() ?? rebornEnemy.GetComponentInChildren<EnemyDrops>();
        if (drops != null)
        {
            drops.isEliteDrops = true;
        }
    }

    private void Update()
    {
        if (isSlowed)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                RemoveSlow();
            }
        }
    }

    private void LateUpdate()
    {
        // Orientação Billboard: Garante que o Canvas/Slider de vida fique sempre virado de frente para a Câmera do Jogador
        if (healthBarSlider != null && healthBarSlider.gameObject.activeInHierarchy)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Canvas canvas = healthBarSlider.GetComponentInParent<Canvas>();
                Transform targetTransform = (canvas != null) ? canvas.transform : healthBarSlider.transform;
                targetTransform.rotation = mainCam.transform.rotation;
            }
        }
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0f || CurrentHealth <= 0) return;

        BossController boss = GetComponent<BossController>() ?? GetComponentInParent<BossController>();
        if (boss != null)
        {
            boss.ApplyStun(duration);
            return;
        }

        ApplySlow(0.95f, duration);
        StartCoroutine(StunNavAgentRoutine(duration));
        Debug.Log($"[DEBUFF] Stun aplicado a {gameObject.name} por {duration}s.");
    }

    private IEnumerator StunNavAgentRoutine(float duration)
    {
        UnityEngine.AI.NavMeshAgent nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        bool hadStopped = false;
        if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh)
        {
            hadStopped = nav.isStopped;
            nav.isStopped = true;
            nav.velocity = Vector3.zero;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        yield return new WaitForSeconds(duration);

        if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh && CurrentHealth > 0)
        {
            nav.isStopped = hadStopped;
        }
    }

    public void ApplySlow(float percent, float duration)
    {
        if (percent <= 0f) return;

        if (!isSlowed)
        {
            isSlowed = true;
            slowPercent = percent;
            slowTimer = duration;
            ApplySlowToAI(percent);
            Debug.Log($"[DEBUFF] Slow de {percent * 100}% aplicado a {gameObject.name} por {duration}s.");
        }
        else
        {
            // Atualiza tempo de duração (pega o maior)
            slowTimer = Mathf.Max(slowTimer, duration);

            // Se o novo slow for mais forte, aplica a diferença
            if (percent > slowPercent)
            {
                RemoveSlowFromAI(); // Reverte o antigo
                slowPercent = percent;
                ApplySlowToAI(percent); // Aplica o novo mais forte
            }
        }
    }

    public void RemoveSlow()
    {
        if (!isSlowed) return;

        RemoveSlowFromAI();
        isSlowed = false;
        slowPercent = 0f;
        slowTimer = 0f;
        Debug.Log($"[DEBUFF] Slow removido de {gameObject.name}. Velocidade restaurada.");
    }

    private void ApplySlowToAI(float percent)
    {
        ScaleAISpeeds(1f - percent);
    }

    private void RemoveSlowFromAI()
    {
        if (slowPercent >= 1f) return; // Evita divisão por zero
        ScaleAISpeeds(1f / (1f - slowPercent));
    }

    private void ScaleAISpeeds(float factor)
    {
        if (TryGetComponent<Geobionte_AI>(out var geo)) { geo.chaseSpeed *= factor; geo.seekSpeed *= factor; geo.wanderSpeed *= factor; }
        if (TryGetComponent<Golem_AI>(out var golem)) { golem.moveSpeed *= factor; }
        if (TryGetComponent<Spider_AI>(out var spider)) { spider.moveSpeed *= factor; }
        if (TryGetComponent<GoblinAI_Transform>(out var goblin)) { goblin.chaseSpeed *= factor; goblin.fleeSpeed *= factor; goblin.strafeSpeed *= factor; }
        if (TryGetComponent<ShardSwarm_AI>(out var swarm)) { swarm.moveSpeed *= factor; }
        if (TryGetComponent<MagicStone_AI>(out var stone)) { stone.moveSpeed *= factor; }
        if (TryGetComponent<CrystalTuner>(out var tuner)) { tuner.moveSpeed *= factor; }
        if (TryGetComponent<Cristalus_AI>(out var cristalus)) { cristalus.moveSpeed *= factor; }
        if (TryGetComponent<PeixeAI>(out var peixe)) { peixe.chaseSpeed *= factor; }
        if (TryGetComponent<SharpBlur>(out var sharpBlur)) { sharpBlur.chaseSpeed *= factor; }
        if (TryGetComponent<CrystalDragon_AI>(out var dragon)) { dragon.groundSpeed *= factor; dragon.orbitLinearSpeed *= factor; }

        if (bossController == null && navAgent != null && navAgent.speed > 0.01f)
        {
            navAgent.speed *= factor;
        }
    }
}