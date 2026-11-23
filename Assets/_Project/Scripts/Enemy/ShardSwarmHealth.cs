using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Sistema de vida exclusivo do Shard Swarm.
/// Cópia do DummyHealth com método SetHealth() para permitir divisão de HP no Split.
/// </summary>
public class ShardSwarmHealth : MonoBehaviour
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
    private Image healthBarFill;

    [Header("Cores da Barra")]
    public Color normalColor = Color.red;
    [Tooltip("Cor da barra quando está sendo protegido pelo Sintonizador.")]
    public Color buffedColor = Color.cyan;

    [Header("Feedback de Dano")]
    public GameObject floatingDamageTextPrefab;
    public Vector3 textOffset = new Vector3(0, 2f, 0);
    public Color hitColor = Color.red;
    public float hitFlashTime = 0.2f;

    [HideInInspector] public bool isInvulnerable = false;
    [HideInInspector] public bool isBuffed = false;
    private bool isDead = false;

    /// <summary>
    /// Se definido, substitui a lógica padrão de morte (drops + Destroy).
    /// Usado pelo ShardSwarm_AI para controlar o split de gerações.
    /// </summary>
    [HideInInspector] public System.Action onDeathOverride = null;
    [HideInInspector] public System.Action<int, GameObject> onDamageTaken = null;
    [HideInInspector] public System.Action onDeath = null;

    private Renderer dummyRenderer;
    private Animator[] allAnimators;
    private float[] savedAnimatorSpeeds;
    private NavMeshAgent navAgent;
    private Rigidbody rb;
    private readonly List<Behaviour> enemyAIBehaviours = new List<Behaviour>();
    private readonly List<Behaviour> pausedAIBehaviours = new List<Behaviour>();
    private Coroutine hitStopCo;
    private bool isHitStopActive;
    private bool savedNavWasStopped;
    private bool biomassKillRewarded = false;

    [Header("Biomassa do Traje")]
    public bool grantsBiomass = true;
    private const float BiomassPerHit = 2f;
    private const float BiomassPerKill = 8f;
    private const float BiomassPerEliteKill = 40f;

    private void Start()
    {
        navAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        allAnimators = GetComponentsInChildren<Animator>(true);
        DummyHealth.CollectEnemyAIBehaviours(gameObject, enemyAIBehaviours);
        // Aplica escalonamento de HP por sala (+3% por sala avançada conforme GDD)
        if (RunManager.instance != null && RunManager.instance.currentRoomNumber > 1)
        {
            int roomIndex = RunManager.instance.currentRoomNumber;
            float hpScalingMultiplier = 1f + (0.03f * (roomIndex - 1));
            maxHealth = Mathf.RoundToInt(maxHealth * hpScalingMultiplier);
        }

        // Escalonamento de HP no Modo Endless (+15% por level acima do 3)
        if (RunManager.instance != null && RunManager.instance.isEndlessMode && RunManager.instance.currentLevel > 3)
        {
            maxHealth = Mathf.RoundToInt(maxHealth * RunManager.instance.GetEndlessHpMultiplier());
        }

        CurrentHealth = maxHealth;

        if (GetComponent<EliteVariantConfig>() != null || gameObject.name.ToLower().Contains("elite"))
        {
            isElite = true;
        }

        dummyRenderer = GetComponentInChildren<Renderer>();

        if (healthBarSlider == null)
        {
            healthBarSlider = GetComponentInChildren<Slider>(true);

            if (healthBarSlider == null)
                Debug.LogWarning("[" + gameObject.name + "] Slider de vida não encontrado!");
            else
                Debug.Log("[" + gameObject.name + "] Slider encontrado: " + healthBarSlider.gameObject.name);
        }

        if (healthBarSlider != null)
        {
            if (healthBarSlider.fillRect != null)
            {
                healthBarFill = healthBarSlider.fillRect.GetComponent<Image>();
            }
            if (healthBarFill != null) healthBarFill.color = normalColor;
            healthBarSlider.gameObject.SetActive(false);

            // Garante que o Canvas da barra de vida encare a câmera fixo sem girar
            Canvas parentCanvas = healthBarSlider.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.GetComponent<FaceCamera>() == null)
            {
                parentCanvas.gameObject.AddComponent<FaceCamera>();
            }
        }

        UpdateHealthBar();
        DetectAIScripts();

        if (RunManager.instance != null && RunManager.instance.isEndlessMode)
        {
            float moveMult = RunManager.instance.GetEndlessEnemyMoveSpeedMultiplier();
            if (moveMult > 1.001f)
            {
                if (shardSwarmAI != null) shardSwarmAI.moveSpeed *= moveMult;
                if (navAgent != null && navAgent.speed > 0.01f) navAgent.speed *= moveMult;
            }
        }
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
    }

    public void SetBuffedStatus(bool buffed)
    {
        isBuffed = buffed;
        if (healthBarFill != null)
        {
            healthBarFill.color = buffed ? buffedColor : normalColor;
        }
    }

    /// <summary>
    /// Define o HP diretamente (usado pelo Split do ShardSwarm).
    /// </summary>
    public void SetHealth(int hp)
    {
        CurrentHealth = Mathf.Clamp(hp, 0, maxHealth);
        UpdateHealthBar();

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(int damage, bool isCritical = false, bool isPoison = false)
    {
        if (isInvulnerable) return;
        if (CurrentHealth <= 0) return;

        if (shardSwarmAI == null)
        {
            shardSwarmAI = GetComponent<ShardSwarm_AI>() ?? GetComponentInChildren<ShardSwarm_AI>() ?? GetComponentInParent<ShardSwarm_AI>();
        }

        if (shardSwarmAI != null)
        {
            float mult = shardSwarmAI.GetCurrentDamageMultiplier();
            damage = Mathf.RoundToInt(damage * mult);
            if (mult > 1.0f)
            {
                isCritical = true; // Exibe Dano Crítico ao acertar o Núcleo EXPOSTO!
            }
            else
            {
                // Dispara a ativação do holograma 'Escudo' ao receber dano no modo protegido!
                shardSwarmAI.FlashShieldVisual();
            }
        }
        else if (isBuffed)
        {
            damage = Mathf.RoundToInt(damage * 0.5f);
        }

        Tier4EffectManager tunerManager = FindFirstObjectByType<Tier4EffectManager>();
        damage = tunerManager != null ? tunerManager.ModifyIncomingDamage(gameObject, damage) : damage;
        CurrentHealth -= damage;
        onDamageTaken?.Invoke(damage, null);

        if (grantsBiomass && !isPoison && damage > 0 && SuitHealingSystem.Instance != null)
        {
            SuitHealingSystem.Instance.AddBiomass(BiomassPerHit);
        }

        UpdateHealthBar();
        ShowHealthBar();

        if (!isPoison)
        {
            ApplyHitStop(isCritical ? 0.12f : 0.08f);
        }

        if (floatingDamageTextPrefab == null)
        {
            DummyHealth[] others = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            foreach (var other in others)
            {
                if (other.floatingDamageTextPrefab != null)
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
            StartCoroutine(isPoison ? FlashRed() : FlashWhite());
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

    void ShowHealthBar()
    {
        if (healthBarSlider == null) return;
        healthBarSlider.gameObject.SetActive(true);
    }

    void UpdateHealthBar()
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
            flashRends = DummyHealth.CollectFlashMeshRenderers(gameObject);
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

    private float stunUntil;
    public bool IsStunned => Time.time < stunUntil;

    public void ApplyStun(float duration)
    {
        if (duration <= 0f || isDead || CurrentHealth <= 0) return;
        stunUntil = Mathf.Max(stunUntil, Time.time + duration);
        if (!isHitStopActive) hitStopCo = StartCoroutine(HitStopRoutine(0f));
    }

    public void ApplyHitStop(float duration = 0.08f)
    {
        if (!gameObject.activeInHierarchy || CurrentHealth <= 0 || isDead) return;
        if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 1.5f) return;

        if (IsStunned) return;
        if (hitStopCo != null)
        {
            StopCoroutine(hitStopCo);
            RestoreHitStopState();
        }
        hitStopCo = StartCoroutine(HitStopRoutine(duration));
    }

    private void OnDisable()
    {
        stunUntil = 0f;
        if (hitStopCo != null) StopCoroutine(hitStopCo);
        hitStopCo = null;
        RestoreHitStopState();
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopActive = true;
        if (enemyAIBehaviours.Count == 0) DummyHealth.CollectEnemyAIBehaviours(gameObject, enemyAIBehaviours);
        if (allAnimators == null || allAnimators.Length == 0) allAnimators = GetComponentsInChildren<Animator>(true);

        savedAnimatorSpeeds = new float[allAnimators.Length];
        for (int i = 0; i < allAnimators.Length; i++)
        {
            if (allAnimators[i] != null)
            {
                savedAnimatorSpeeds[i] = allAnimators[i].speed;
                allAnimators[i].speed = 0f;
            }
        }

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

        DummyHealth.PauseActiveAIBehaviours(enemyAIBehaviours, pausedAIBehaviours);
        yield return new WaitForSecondsRealtime(duration);
        while (IsStunned && !isDead) yield return null;
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

        DummyHealth.ResumePausedAIBehaviours(pausedAIBehaviours);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        RestoreHitStopState();

        if (!biomassKillRewarded)
        {
            biomassKillRewarded = true;
            if (grantsBiomass && SuitHealingSystem.Instance != null)
            {
                SuitHealingSystem.Instance.AddBiomass(isElite ? BiomassPerEliteKill : BiomassPerKill);
            }
        }

        try { onDeath?.Invoke(); } catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro em onDeath: {ex}"); }

        try
        {
            if (shardSwarmAI != null)
            {
                shardSwarmAI.enabled = false;
                shardSwarmAI.DestroyAllSpikes();
            }
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;
        }
        catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro ao desativar espinhos/colisores: {ex}"); }

        // Notifica a sala imediatamente para não travar portas
        try
        {
            RoomController room = FindRoomController();
            if (room != null) room.NotifyEnemyDefeated(gameObject);
        }
        catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro ao notificar sala: {ex}"); }

        // Se um override foi definido (ex: ShardSwarm_AI controla o split),
        // chama o callback e retorna sem executar a lógica padrão.
        if (onDeathOverride != null)
        {
            try { onDeathOverride.Invoke(); } catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro em onDeathOverride: {ex}"); }
            return;
        }

        Debug.Log(gameObject.name + " foi destruído.");

        try
        {
            EnemyDrops drops = GetComponent<EnemyDrops>()
                            ?? GetComponentInChildren<EnemyDrops>()
                            ?? GetComponentInParent<EnemyDrops>();
            if (drops != null)
            {
                drops.OnDeath();
            }
        }
        catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro em EnemyDrops: {ex}"); }

        // --- RECOMPENSA DE COMBATE DA SINFONIA DO CAOS (Pacto + Mutação do Abismo) ---
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
                }
                else if (hasMerchantChaos)
                {
                    playerHealth.Heal(8);
                    playerHealth.TriggerEliteKillAdrenaline(5f);
                }
                else if (hasAbyssChaos && Random.value <= chaosTriggerChance)
                {
                    if (isElite) playerHealth.Heal(6);
                    playerHealth.TriggerEliteKillAdrenaline(chaosDuration, chaosMoveBonus, chaosAttackBonus);
                }
            }
        }
        catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro em recompensa caos: {ex}"); }

        // --- SISTEMA DE RENASCIMENTO ELITE ---
        try
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            bool canReborn = (!isElite && !hasRebornAsElite &&
                              GetComponent<EliteVariantConfig>() == null &&
                              !gameObject.name.ToLower().Contains("elite") &&
                              !gameObject.name.ToLower().Contains("boss") &&
                              !gameObject.name.ToLower().Contains("geobionte") &&
                              !gameObject.name.ToLower().Contains("sentinel"));

            float effectiveRebirthChance = eliteRebirthChance;
            if (playerHealth != null && playerHealth.hasChaosSymphony)
            {
                effectiveRebirthChance = Mathf.Clamp01(effectiveRebirthChance + 0.25f);
            }
            if (RunManager.instance != null && RunManager.instance.isEndlessMode)
            {
                effectiveRebirthChance = Mathf.Clamp(effectiveRebirthChance + RunManager.instance.GetEndlessEliteRebirthBonus(), 0f, 0.70f);
            }

            bool shouldReborn = canReborn && (forceEliteRebirth || DummyHealth.forceEliteRebirth || Random.value <= effectiveRebirthChance);

            if (shouldReborn)
            {
                TriggerEliteRebirth();
                return;
            }
        }
        catch (System.Exception ex) { Debug.LogError($"[ShardSwarmHealth] Erro em Elite Rebirth: {ex}"); }

        Destroy(gameObject);
    }

    private void TriggerEliteRebirth()
    {
        try
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
                RunManager.instance.StartCoroutine(DummyHealth.RebirthFallbackRoutine(transform.position, transform.rotation, originalPrefab != null ? originalPrefab : gameObject, gameObject.name));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ShardSwarmHealth] Erro em TriggerEliteRebirth: {ex}");
        }
        finally
        {
            Destroy(gameObject);
        }
    }

    public void ShowRebirthFloatingText(Vector3 position)
    {
        if (floatingDamageTextPrefab == null)
        {
            DummyHealth[] others = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            foreach (var other in others)
            {
                if (other != null && other.floatingDamageTextPrefab != null)
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
