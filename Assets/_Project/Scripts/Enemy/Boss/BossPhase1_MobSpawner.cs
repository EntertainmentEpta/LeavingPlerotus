using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador de spawn de mobs durante a Fase 1 do Boss Cromático
///
/// TRIGGERS DE SPAWN:
///   1. Pós-Prisão: Chamado pelo BossPhase1_MestreDoSolo após cada ataque de prisão
///   2. Threshold de HP: Waves fixas em 95% e 85% HP
///   3. Contra-Ataque: Quando o player acerta X hits seguidos no boss
///
/// REGRAS:
///   • Máximo de mobs simultâneos (configurável via BossPhaseConfig)
///   • Cooldown global entre waves
///   • Mobs spawnam nas bordas da arena via NavMesh
///   • Todos os mobs morrem automaticamente ao sair da Fase 1
///
/// SETUP NO UNITY:
///   1. Adicione este componente no mesmo GameObject do Boss (junto do BossController)
///   2. Arraste os prefabs de mobs nos campos do Inspector
///   3. Os parâmetros numéricos vêm do BossPhaseConfig (ScriptableObject)
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossPhase1_MobSpawner : MonoBehaviour
{
    // =====================================================
    // TIPOS DE WAVE
    // =====================================================

    public enum WaveType
    {
        PostPrison_Pillar,  // Após prisão de pilares → Goblins rápidos
        PostPrison_Spike,   // Após prisão de espinhos → Spiders + SharpBlur
        Threshold_First,    // HP ≤ 95% → Totem
        Threshold_Second,   // HP ≤ 85% → Spiders + Cristalus
        CounterAttack       // Punição por agressividade → ShardSwarm
    }

    // =====================================================
    // INSPECTOR — PREFABS
    // =====================================================

    [Header("Prefabs de Mobs")]
    [Tooltip("Prefab do Goblin (mob leve e rápido).")]
    public GameObject goblinPrefab;

    [Tooltip("Prefab da Spider (mob leve com animação procedural).")]
    public GameObject spiderPrefab;

    [Tooltip("Prefab do SharpBlur (mob rápido).")]
    public GameObject sharpBlurPrefab;

    [Tooltip("Prefab do Golem (mob tanque pesado).")]
    public GameObject golemPrefab;

    [Tooltip("Prefab da Pedra Mágica (MagicStone).")]
    public GameObject magicStonePrefab;

    [Tooltip("Prefab do CrystalWatcher (Sentinela).")]
    public GameObject crystalWatcherPrefab;

    [Tooltip("Prefab do Totem (spawna caveiras HomingHazard).")]
    public GameObject totemPrefab;

    [Tooltip("Prefab do Cristalus (mob cristalino).")]
    public GameObject cristalusPrefab;

    [Tooltip("Prefab do ShardSwarm (enxame de fragmentos).")]
    public GameObject shardSwarmPrefab;

    [Tooltip("Prefab do Geobionte / Bismutado (Mob raro de elite).")]
    public GameObject geobiontePrefab;

    [Header("🎯 Probabilidade de Invasão de Mob Raro (Inspector)")]
    [Tooltip("Porcentagem de chance (0.0% a 100.0%) do Geobionte/Bismutado aparecer no spawn. Padrão: 2.0%")]
    [Range(0f, 100f)]
    public float geobionteSpawnChance = 2.0f;

    [Header("Configurações de Spawn Visual")]
    [Tooltip("Prefab do indicador visual (opcional). Usado para telegrafar o nascimento, igual nas salas normais.")]
    public GameObject spawnIndicatorPrefab;

    [Tooltip("Tempo de espera do indicador antes do mob começar a emergir (segundos).")]
    public float spawnIndicatorDelay = 1f;

    [Tooltip("Quão fundo no chão os mobs começam antes de emergir.")]
    public float spawnDepth = 3f;

    [Tooltip("Tempo que os mobs levam para emergir do chão (segundos).")]
    public float emergeTime = 0.6f;

    [Header("Modo Teste Solo (Sem Mobs)")]
    [Tooltip("Quando ativado via Cheat Console (bosssolo), desativa totalmente o spawn de mobs na luta do boss para testes isolados.")]
    public static bool isBossSoloMode = false;

    [Tooltip("Habilita spawn de mobs após ataques de prisão.")]
    public bool enablePostPrisonSpawn = true;

    [Tooltip("Habilita spawn de mobs em thresholds de HP.")]
    public bool enableThresholdSpawn = true;

    [Tooltip("Habilita spawn de mobs como contra-ataque.")]
    public bool enableCounterAttackSpawn = true;

    public static void SetBossSoloMode(bool solo)
    {
        isBossSoloMode = solo;
        Debug.Log($"💻 [BOSS SOLO MODE] Modo Solo do Boss setado para: {isBossSoloMode}");

        if (isBossSoloMode)
        {
            BossPhase1_MobSpawner spawner = FindFirstObjectByType<BossPhase1_MobSpawner>();
            if (spawner != null)
            {
                spawner.KillAllMobs();
            }
        }
    }

    // =====================================================
    // ESTADO INTERNO
    // =====================================================

    private BossController bossController;
    private BossPhaseConfig config;
    private Transform playerTransform;

    // Mobs vivos spawnados por este script
    private List<GameObject> activeMobs = new List<GameObject>();

    // Cooldown global
    private float lastSpawnTime = -999f;

    // Contagem de hits para contra-ataque
    private int hitCounter = 0;

    // Controle de thresholds (para não disparar 2x)
    private bool firstThresholdTriggered = false;
    private bool secondThresholdTriggered = false;

    // Estado da fase
    private bool phase1Active = false;

    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================

    private void Awake()
    {
        bossController = GetComponent<BossController>();
        config = bossController.phaseConfig;
        isBossSoloMode = false; // Garante que o modo solo não fique travado permanentemente por sessões anteriores
    }

    private void OnEnable()
    {
        BossEvents.OnPhaseChanged += OnPhaseChanged;
        BossEvents.OnBossFightStarted += OnFightStarted;
        BossEvents.OnBossHealthChanged += OnHealthChanged;
        bossController.OnTookDamage += OnBossTookDamage;
    }

    private void OnDisable()
    {
        BossEvents.OnPhaseChanged -= OnPhaseChanged;
        BossEvents.OnBossFightStarted -= OnFightStarted;
        BossEvents.OnBossHealthChanged -= OnHealthChanged;
        bossController.OnTookDamage -= OnBossTookDamage;
    }

    private void Update()
    {
        // Limpa referências nulas (mobs que morreram pelo jogador)
        if (phase1Active)
        {
            activeMobs.RemoveAll(mob => mob == null);
        }
    }

    // =====================================================
    // EVENTOS
    // =====================================================

    private void OnFightStarted()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    private void OnPhaseChanged(int newPhase)
    {
        if (newPhase == 1)
        {
            phase1Active = true;
            hitCounter = 0;
            lastSpawnTime = -999f; // Permite spawn imediato
            firstThresholdTriggered = false;
            secondThresholdTriggered = false;
            Debug.Log("[MobSpawner] Fase 1 ativada — pronto para spawnar mobs.");
            StopCoroutine(nameof(AutoSpawnRoutineFase1));
            StartCoroutine(AutoSpawnRoutineFase1());
        }
        else
        {
            // Saiu da Fase 1 — limpar todos os mobs
            if (phase1Active)
            {
                phase1Active = false;
                KillAllMobs();
                Debug.Log("[MobSpawner] Fase 1 encerrada — todos os mobs eliminados.");
            }
        }
    }

    private void OnHealthChanged(float healthPercent)
    {
        if (!phase1Active || !enableThresholdSpawn) return;

        float firstThreshold = config != null ? config.phase1FirstWaveThreshold : 0.95f;
        float secondThreshold = config != null ? config.phase1SecondWaveThreshold : 0.85f;

        // Threshold 1: Totem
        if (!firstThresholdTriggered && healthPercent <= firstThreshold)
        {
            firstThresholdTriggered = true;
            Debug.Log($"[MobSpawner] HP ≤ {firstThreshold:P0} — Spawning Totem!");
            SpawnWave(WaveType.Threshold_First);
        }

        // Threshold 2: Spiders + Cristalus
        if (!secondThresholdTriggered && healthPercent <= secondThreshold)
        {
            secondThresholdTriggered = true;
            Debug.Log($"[MobSpawner] HP ≤ {secondThreshold:P0} — Spawning pressão extra!");
            SpawnWave(WaveType.Threshold_Second);
        }
    }

    private void OnBossTookDamage()
    {
        if (!phase1Active || !enableCounterAttackSpawn) return;

        hitCounter++;
        int threshold = config != null ? config.phase1HitCounterThreshold : 4;

        if (hitCounter >= threshold)
        {
            hitCounter = 0;
            Debug.Log("[MobSpawner] Contra-Ataque! Player acertou muitos hits seguidos.");
            SpawnWave(WaveType.CounterAttack);
        }
    }

    private IEnumerator AutoSpawnRoutineFase1()
    {
        yield return new WaitForSeconds(1.5f);
        // Spawna uma wave inicial de abertura para compor o campo de batalha
        if (phase1Active && !isBossSoloMode)
        {
            SpawnWave(WaveType.Threshold_First);
        }

        while (phase1Active)
        {
            yield return new WaitForSeconds(4.5f);
            activeMobs.RemoveAll(mob => mob == null);
            int maxMobs = config != null ? config.phase1MaxMobs : 6;
            if (activeMobs.Count < maxMobs && !isBossSoloMode)
            {
                SpawnWave(WaveType.CounterAttack);
            }
        }
    }

    // =====================================================
    // API PÚBLICA — Chamada pelo BossPhase1_MestreDoSolo
    // =====================================================

    /// <summary>
    /// Spawna uma wave de mobs baseada no tipo.
    /// Chamado pelo BossPhase1_MestreDoSolo após cada ataque de prisão,
    /// ou internamente pelos triggers de HP/contra-ataque.
    /// Respeita cooldown global e limite máximo de mobs.
    /// </summary>
    public void SpawnWave(WaveType waveType)
    {
        if (isBossSoloMode) return;

        // Permite spawnar mobs durante a Fase 1 OU durante a Refração/Invisibilidade da Fase 2
        BossPhase2_Refraction refraction = GetComponent<BossPhase2_Refraction>();
        bool isRefractingInPhase2 = (refraction != null && refraction.IsRefracting) || (bossController != null && bossController.IsInvisible);

        if (!phase1Active && !isRefractingInPhase2) return;

        // Verifica cooldown global
        float cooldown = config != null ? config.phase1SpawnCooldown : 3f;
        if (Time.time - lastSpawnTime < cooldown)
        {
            return;
        }

        // Limpa nulos antes de verificar limite
        activeMobs.RemoveAll(mob => mob == null);

        // Verifica limite de mobs
        int maxMobs = config != null ? config.phase1MaxMobs : 5;
        if (activeMobs.Count >= maxMobs)
        {
            Debug.Log($"[MobSpawner] Wave {waveType} bloqueada — limite de {maxMobs} mobs atingido ({activeMobs.Count} vivos).");
            return;
        }

        // Calcula quantos mobs ainda cabem
        int slotsAvailable = maxMobs - activeMobs.Count;

        // Monta a lista de prefabs para spawnar baseado no tipo de wave
        List<GameObject> prefabsToSpawn = GetWaveComposition(waveType, slotsAvailable);

        if (prefabsToSpawn.Count == 0)
        {
            Debug.LogWarning($"[MobSpawner] Wave {waveType} — nenhum prefab configurado ou slots insuficientes.");
            return;
        }

        // Marca o tempo de spawn
        lastSpawnTime = Time.time;

        // Spawna os mobs
        StartCoroutine(SpawnMobsCoroutine(prefabsToSpawn));

        Debug.Log($"[MobSpawner] Wave {waveType} — spawnando {prefabsToSpawn.Count} mobs!");
    }

    // =====================================================
    // COMPOSIÇÃO DAS WAVES
    // =====================================================

    private List<GameObject> GetWaveComposition(WaveType waveType, int maxSlots)
    {
        List<GameObject> result = new List<GameObject>();
        AutoLoadMissingPrefabs();

        int slotsToFill = Mathf.Min(Random.Range(2, 4), maxSlots);

        switch (waveType)
        {
            case WaveType.Threshold_First:
                GameObject heavyUnit = (Random.value < 0.5f) ? (totemPrefab ?? golemPrefab) : (golemPrefab ?? totemPrefab);
                if (heavyUnit != null) result.Add(heavyUnit);
                break;

            case WaveType.Threshold_Second:
                float rareProb = Mathf.Clamp01(geobionteSpawnChance / 100f);
                if (Random.value <= rareProb && geobiontePrefab != null)
                {
                    result.Add(geobiontePrefab);
                }
                else
                {
                    for (int i = 0; i < slotsToFill; i++)
                    {
                        GameObject mob = GetRandomCommonMob();
                        if (mob != null) result.Add(mob);
                    }
                }
                break;

            default: // PostPrison_Pillar, PostPrison_Spike, CounterAttack, etc.
                for (int i = 0; i < slotsToFill; i++)
                {
                    GameObject mob = GetRandomCommonMob();
                    if (mob != null) result.Add(mob);
                }
                break;
        }

        return result;
    }

    private GameObject GetRandomCommonMob()
    {
        AutoLoadMissingPrefabs();

        List<GameObject> pool = new List<GameObject>();
        if (goblinPrefab != null) pool.Add(goblinPrefab);
        if (spiderPrefab != null) pool.Add(spiderPrefab);
        if (sharpBlurPrefab != null) pool.Add(sharpBlurPrefab);
        if (golemPrefab != null) pool.Add(golemPrefab);
        if (magicStonePrefab != null) pool.Add(magicStonePrefab);
        if (crystalWatcherPrefab != null) pool.Add(crystalWatcherPrefab);
        if (totemPrefab != null) pool.Add(totemPrefab);
        if (cristalusPrefab != null) pool.Add(cristalusPrefab);
        if (shardSwarmPrefab != null) pool.Add(shardSwarmPrefab);

        if (pool.Count == 0) return goblinPrefab;
        return pool[Random.Range(0, pool.Count)];
    }

    private void AutoLoadMissingPrefabs()
    {
#if UNITY_EDITOR
        if (goblinPrefab == null) goblinPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Goblin/Goblin.prefab");
        if (spiderPrefab == null) spiderPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Spider/Spider.prefab");
        if (golemPrefab == null) golemPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Golem/Golem.prefab");
        if (cristalusPrefab == null) cristalusPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Cristalus/Cristalus.prefab");
        if (crystalWatcherPrefab == null) crystalWatcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab");
        if (magicStonePrefab == null) magicStonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/MagicStone/MagicStoneEnemy.prefab");
        if (totemPrefab == null) totemPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Totem/Totem 1.prefab");
        if (shardSwarmPrefab == null) shardSwarmPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/ShardSwarm/Shard Swarm.prefab");
        if (geobiontePrefab == null) geobiontePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Bismutado/bismutado.prefab");
        if (sharpBlurPrefab == null) sharpBlurPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/SharpBlur/Sh.prefab");
#endif

        if (goblinPrefab == null) goblinPrefab = Resources.Load<GameObject>("Goblin") ?? Resources.Load<GameObject>("Enemies/Goblin");
        if (spiderPrefab == null) spiderPrefab = Resources.Load<GameObject>("Spider") ?? Resources.Load<GameObject>("Enemies/Spider");
        if (sharpBlurPrefab == null) sharpBlurPrefab = Resources.Load<GameObject>("SharpBlur") ?? Resources.Load<GameObject>("Enemies/SharpBlur");
        if (golemPrefab == null) golemPrefab = Resources.Load<GameObject>("Golem") ?? Resources.Load<GameObject>("Enemies/Golem");
        if (magicStonePrefab == null) magicStonePrefab = Resources.Load<GameObject>("MagicStone") ?? Resources.Load<GameObject>("Enemies/MagicStone");
        if (crystalWatcherPrefab == null) crystalWatcherPrefab = Resources.Load<GameObject>("CrystalWatcher") ?? Resources.Load<GameObject>("Enemies/CrystalWatcher");
        if (totemPrefab == null) totemPrefab = Resources.Load<GameObject>("Totem") ?? Resources.Load<GameObject>("Enemies/Totem");
        if (cristalusPrefab == null) cristalusPrefab = Resources.Load<GameObject>("Cristalus") ?? Resources.Load<GameObject>("Enemies/Cristalus");
        if (shardSwarmPrefab == null) shardSwarmPrefab = Resources.Load<GameObject>("ShardSwarm") ?? Resources.Load<GameObject>("Enemies/ShardSwarm");
        if (geobiontePrefab == null) geobiontePrefab = Resources.Load<GameObject>("Geobionte") ?? Resources.Load<GameObject>("Enemies/Geobionte");
    }

    private void AddPrefabs(List<GameObject> list, GameObject prefab, int count)
    {
        if (prefab == null) return;
        for (int i = 0; i < count; i++)
        {
            list.Add(prefab);
        }
    }

    // =====================================================
    // SPAWN COM EFEITO VISUAL (EMERGE DO CHÃO)
    // =====================================================

    private IEnumerator SpawnMobsCoroutine(List<GameObject> prefabs)
    {
        float radius = config != null ? config.phase1SpawnRadius : 15f;
        Vector3 arenaCenter = transform.position; // Centro da arena = posição do boss (pode ser ajustado)

        List<SpawnedMobData> spawnedMobs = new List<SpawnedMobData>();

        for (int i = 0; i < prefabs.Count; i++)
        {
            // Calcula posição na borda da arena (distribuída igualmente)
            float angle = (i * Mathf.PI * 2f / prefabs.Count) + Random.Range(-0.3f, 0.3f);
            Vector3 targetPos = arenaCenter + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;

            // Garante que a posição esteja no NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 10f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }

            spawnedMobs.Add(new SpawnedMobData
            {
                mobObject = null, // Será preenchido após o delay
                startPos = targetPos + Vector3.down * spawnDepth,
                targetPos = targetPos,
                agent = null
            });
        }

        // 1. Cria os indicadores visuais se configurado
        List<GameObject> indicators = new List<GameObject>();
        if (spawnIndicatorPrefab != null)
        {
            foreach (var data in spawnedMobs)
            {
                Vector3 indicatorPos = new Vector3(data.targetPos.x, data.targetPos.y + 0.05f, data.targetPos.z);
                GameObject indicator = Instantiate(spawnIndicatorPrefab, indicatorPos, Quaternion.identity);
                indicators.Add(indicator);
            }

            // 2. Espera o tempo de telegrafagem
            yield return new WaitForSeconds(spawnIndicatorDelay);

            // Limpa os indicadores
            foreach (var ind in indicators)
            {
                if (ind != null) Destroy(ind);
            }
        }

        // 3. Instancia os mobs no subsolo e prepara para emergir
        for (int i = 0; i < prefabs.Count; i++)
        {
            var data = spawnedMobs[i];
            
            // Instancia o mob no subsolo
            GameObject mob = Instantiate(prefabs[i], data.startPos, Quaternion.identity);

            // Desativa o NavMeshAgent temporariamente para não interferir na animação
            NavMeshAgent mobAgent = mob.GetComponent<NavMeshAgent>();
            if (mobAgent != null) mobAgent.enabled = false;

            activeMobs.Add(mob);
            
            // Atualiza os dados estruturados
            data.mobObject = mob;
            data.agent = mobAgent;
            spawnedMobs[i] = data; // Struct requer reatribuição na lista
        }

        // 4. Animação de emergir do chão
        float elapsed = 0f;
        while (elapsed < emergeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / emergeTime);
            // Ease-out para efeito dramático (sobe rápido, freia no final)
            float curve = Mathf.Sin(t * Mathf.PI * 0.5f);

            foreach (var data in spawnedMobs)
            {
                if (data.mobObject != null)
                {
                    data.mobObject.transform.position = Vector3.Lerp(data.startPos, data.targetPos, curve);
                }
            }

            yield return null;
        }

        // Reativa o NavMeshAgent de cada mob após emergir
        foreach (var data in spawnedMobs)
        {
            if (data.mobObject != null && data.agent != null)
            {
                // Garante posição final exata antes de religar o agent
                data.mobObject.transform.position = data.targetPos;
                data.agent.enabled = true;

                // Faz o mob olhar pro player se possível
                if (playerTransform != null)
                {
                    Vector3 lookDir = (playerTransform.position - data.mobObject.transform.position).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                        data.mobObject.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }
    }

    // Struct auxiliar para guardar dados de cada mob durante o spawn
    private struct SpawnedMobData
    {
        public GameObject mobObject;
        public Vector3 startPos;
        public Vector3 targetPos;
        public NavMeshAgent agent;
    }

    // =====================================================
    // LIMPEZA (TRANSIÇÃO DE FASE)
    // =====================================================

    /// <summary>
    /// Mata todos os mobs spawnados por este script.
    /// Chamado automaticamente quando sai da Fase 1.
    /// </summary>
    private void KillAllMobs()
    {
        foreach (GameObject mob in activeMobs)
        {
            if (mob != null)
            {
                Vector3 mobPos = mob.transform.position;

                // Tenta matar via DummyHealth para respeitar drops e efeitos de morte
                DummyHealth mobHealth = mob.GetComponent<DummyHealth>();
                if (mobHealth != null)
                {
                    mobHealth.isInvulnerable = false;
                    mobHealth.TakeDamage(999999);
                }
                else
                {
                    ShardSwarmHealth swarmHealth = mob.GetComponent<ShardSwarmHealth>();
                    if (swarmHealth != null)
                    {
                        swarmHealth.isInvulnerable = false;
                        swarmHealth.SetHealth(0);
                    }
                    else
                    {
                        Destroy(mob);
                    }
                }

                // Atrair todas as essências dos mobs mortos diretamente para o peito do Boss!
                Collider[] hits = Physics.OverlapSphere(mobPos, 6.0f);
                foreach (Collider col in hits)
                {
                    EssencePickup pickup = col.GetComponent<EssencePickup>();
                    if (pickup != null)
                    {
                        pickup.FlyToBoss(transform);
                    }
                }
            }
        }

        // Busca global na arena por essências soltas e atrai para o Boss
        EssencePickup[] allPickups = FindObjectsOfType<EssencePickup>();
        foreach (var p in allPickups)
        {
            if (p != null && !p.isBossAbsorb)
            {
                p.FlyToBoss(transform);
            }
        }

        activeMobs.Clear();
        hitCounter = 0;
    }

    // =====================================================
    // UTILIDADES
    // =====================================================

    /// <summary>
    /// Retorna quantos mobs spawnados por este script estão vivos.
    /// Útil para debug ou UI.
    /// </summary>
    public int GetAliveMobCount()
    {
        activeMobs.RemoveAll(mob => mob == null);
        return activeMobs.Count;
    }
}
