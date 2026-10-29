using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DummyHealth : MonoBehaviour
{
    [Header("Debuffs (Slow)")]
    [HideInInspector] public bool isSlowed = false;
    private float slowPercent = 0f;
    private float slowTimer = 0f;

    private Geobionte_AI geobionteAI;
    private Golem_AI golemAI;
    private Spider_AI spiderAI;
    private GoblinAI_Transform goblinAI;
    private ShardSwarm_AI shardSwarmAI;
    private MagicStone_AI magicStoneAI;
    private CrystalTuner crystalTunerAI;
    private Cristalus_AI cristalusAI;

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

    private void Awake()
    {
        CurrentHealth = maxHealth;
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
            float hpMultiplier = 1f + (RunManager.instance.currentLevel - 3) * 0.15f; // +15% HP por level acima do 3
            maxHealth = Mathf.RoundToInt(maxHealth * hpMultiplier);
            CurrentHealth = maxHealth;
        }

        if (GetComponent<EliteVariantConfig>() != null || gameObject.name.ToLower().Contains("elite"))
        {
            isElite = true;
        }

        dummyRenderer = GetComponentInChildren<Renderer>();
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
    }

    private void DetectAIScripts()
    {
        geobionteAI = GetComponent<Geobionte_AI>();
        golemAI = GetComponent<Golem_AI>();
        spiderAI = GetComponent<Spider_AI>();
        goblinAI = GetComponent<GoblinAI_Transform>();
        shardSwarmAI = GetComponent<ShardSwarm_AI>();
        magicStoneAI = GetComponent<MagicStone_AI>();
        crystalTunerAI = GetComponent<CrystalTuner>();
        cristalusAI = GetComponent<Cristalus_AI>();

        // Configuração de resistências padrão por arquétipo (Pilar 4) caso não customizado no Inspector
        string lowerName = gameObject.name.ToLower();
        if (golemAI != null || lowerName.Contains("golem"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.60f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.95f;
        }
        else if (cristalusAI != null || lowerName.Contains("cristalus"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.40f;
        }
        else if (geobionteAI != null || lowerName.Contains("geobionte"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.30f;
        }
        else if (TryGetComponent<PeixeAI>(out _) || lowerName.Contains("peixe"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.35f;
        }
        else if (magicStoneAI != null || lowerName.Contains("magicstone") || lowerName.Contains("star"))
        {
            if (projectileResistance <= 0f) projectileResistance = 0.50f;
            if (knockbackResistance <= 0f) knockbackResistance = 0.50f;
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

        CurrentHealth -= damage;
        RunStatsManager.Instance?.RecordDamageDealt(damage);

        onDamageTaken?.Invoke(damage, null);

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

        if (dummyRenderer != null)
        {
            StartCoroutine(isPoison ? FlashPoisonGreen() : FlashRed());
        }

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            Die();
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

    IEnumerator FlashRed()
    {
        dummyRenderer.material.color = hitColor;
        if (hasBaseColor) dummyRenderer.material.SetColor("_BaseColor", hitColor);
        yield return new WaitForSeconds(hitFlashTime);
        dummyRenderer.material.color = originalRenderColor;
        if (hasBaseColor) dummyRenderer.material.SetColor("_BaseColor", originalBaseColor);
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

        // --- RECOMPENSA DE COMBATE DA SINFONIA DO CAOS ---
        try
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (isElite && playerHealth != null && playerHealth.hasChaosSymphony)
            {
                playerHealth.Heal(8);
                playerHealth.TriggerEliteKillAdrenaline(5f);
                Debug.Log("[CAOS] Inimigo Elite abatido! +8 Vida e Adrenalina concedidas ao jogador!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DummyHealth] Erro em recompensa da Sinfonia do Caos: {ex}");
        }

        // --- SISTEMA DE RENASCIMENTO ELITE (10% base / 100% com cheat / +25% com Sinfonia do Caos) ---
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
        if (rc != null && rc.gameObject.activeInHierarchy)
        {
            rc.enemiesPendingSpawn++;
            rc.activeEnemies.Remove(gameObject);
            rc.StartCoroutine(rc.RebirthAsEliteRoutine(transform.position, transform.rotation, originalPrefab != null ? originalPrefab : gameObject, gameObject.name));
        }
        else if (RunManager.instance != null && RunManager.instance.gameObject.activeInHierarchy)
        {
            RunManager.instance.StartCoroutine(RebirthFallbackRoutine(transform.position, transform.rotation, originalPrefab != null ? originalPrefab : gameObject, gameObject.name));
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
        float factor = 1f - percent;
        if (geobionteAI != null) { geobionteAI.chaseSpeed *= factor; geobionteAI.seekSpeed *= factor; geobionteAI.wanderSpeed *= factor; }
        if (golemAI != null) { golemAI.moveSpeed *= factor; }
        if (spiderAI != null) { spiderAI.moveSpeed *= factor; }
        if (goblinAI != null) { goblinAI.chaseSpeed *= factor; goblinAI.fleeSpeed *= factor; goblinAI.strafeSpeed *= factor; }
        if (shardSwarmAI != null) { shardSwarmAI.moveSpeed *= factor; }
        if (magicStoneAI != null) { magicStoneAI.moveSpeed *= factor; }
        if (crystalTunerAI != null) { crystalTunerAI.moveSpeed *= factor; }
        if (cristalusAI != null) { cristalusAI.moveSpeed *= factor; }
    }

    private void RemoveSlowFromAI()
    {
        if (slowPercent >= 1f) return; // Evita divisão por zero
        float factor = 1f / (1f - slowPercent);
        if (geobionteAI != null) { geobionteAI.chaseSpeed *= factor; geobionteAI.seekSpeed *= factor; geobionteAI.wanderSpeed *= factor; }
        if (golemAI != null) { golemAI.moveSpeed *= factor; }
        if (spiderAI != null) { spiderAI.moveSpeed *= factor; }
        if (goblinAI != null) { goblinAI.chaseSpeed *= factor; goblinAI.fleeSpeed *= factor; goblinAI.strafeSpeed *= factor; }
        if (shardSwarmAI != null) { shardSwarmAI.moveSpeed *= factor; }
        if (magicStoneAI != null) { magicStoneAI.moveSpeed *= factor; }
        if (crystalTunerAI != null) { crystalTunerAI.moveSpeed *= factor; }
        if (cristalusAI != null) { cristalusAI.moveSpeed *= factor; }
    }
}