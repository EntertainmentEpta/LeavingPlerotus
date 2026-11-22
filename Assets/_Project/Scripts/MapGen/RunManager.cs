using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gerencia o estado global de uma Run (partida).
/// Rastreia o número da sala atual (n) para aplicar as fórmulas de
/// progressão econômica definidas no GDD (Economy.pdf).
///
/// Fórmula de Drops:  E(n) = d × (1 + α × n)   onde α = 0,05
/// Fórmula de Spawn:  P(n) = 10 + 0,9 × n
///
/// O LevelGenerator notifica este manager ao criar salas.
/// O RunManager persiste entre cenas (DontDestroyOnLoad).
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager instance;

    [Header("Estado da Run")]
    [Tooltip("Número da sala atual no contexto global da run (1–32).")]
    public int currentRoomNumber = 1;

    [Header("Parâmetros de Drops (GDD §1.1)")]
    [Tooltip("Coeficiente de inflação de drops por sala (α). GDD usa 0,05 → +5% por sala.")]
    public float dropInflationAlpha = 0.05f;

    [Header("Parâmetros de Spawn (GDD §1.2)")]
    [Tooltip("Pontos base de spawn na sala 1 (reduzido para início suave).")]
    public float spawnPointsBase = 5f;
    [Tooltip("Incremento de pontos de spawn por sala.")]
    public float spawnPointsGrowth = 0.65f;

    [Header("Progressão de Rounds")]
    [Tooltip("Total de rounds por run. O ÚLTIMO round é sempre Boss Fight.")]
    public int totalLevels = 4;

    [Tooltip("Número de salas principais geradas em cada round normal (índice 0 = Round 1, 1 = Round 2, etc.).\nO boss round não usa este array.")]
    public int[] roomsPerLevel = new int[] { 5, 7, 9 };

    /// <summary>Round atual da run (1 = primeiro nível, totalLevels = boss). Persiste entre cenas via DontDestroyOnLoad.</summary>
    [HideInInspector] public int currentLevel = 1;

    // ==================== GEOBIONTE — PROGRESSO MULTI-FASE ====================

    /// <summary>
    /// Quantos Geobiontes foram derrotados na run atual (0 a 3).
    /// </summary>
    [HideInInspector] public int geobionteDefeatCount = 0;

    /// <summary>
    /// Trava de segurança: impede absorver o mesmo Geobionte mais de uma vez no mesmo nível.
    /// </summary>
    [HideInInspector] public bool geobionteAbsorbedThisLevel = false;

    // ==================== CHEAT: FORÇAR BOSS ====================

    /// <summary>
    /// Flag de cheat: quando true, a próxima run inicia direto no Boss Round.
    /// Consumida automaticamente por StartNewRun() e resetada após uso.
    /// </summary>
    [HideInInspector] public bool forceBossNextRun = false;

    // ==================== MERCADOR — ENCONTRO ÚNICO POR RUN ====================

    /// <summary>
    /// Trava global: o Mercador e a Sala do Mercador só podem aparecer UMA vez por run inteira.
    /// </summary>
    [HideInInspector] public bool hasSpawnedMerchantThisRun = false;

    /// <summary>
    /// Trava global: impede realizar mais de 1 Pacto de Sangue na mesma run.
    /// </summary>
    [HideInInspector] public bool hasCompletedPactThisRun = false;

    // =====================================================

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureMutationCatalog();
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureMutationCatalog();
        maxEndlessRoomsPerFloor = Mathf.Max(5, maxEndlessRoomsPerFloor);
        bossCycleInterval = Mathf.Max(1, bossCycleInterval);
        dualMutationStartFloor = Mathf.Max(2, dualMutationStartFloor);
        maxEndlessEnemySpeedMultiplier = Mathf.Clamp(maxEndlessEnemySpeedMultiplier, 1f, 2f);
    }
#endif

    private void EnsureMutationCatalog()
    {
        if (mutationCatalog == null || mutationCatalog.Count == 0)
        {
            mutationCatalog = AbyssMutationCatalog.CreateDefaultDefinitions();
        }
    }

    // =====================================================
    // API PÚBLICA
    // =====================================================

    /// <summary>
    /// Inicia uma nova Run: reseta sala E round.
    /// Chamado pelo GameManager quando o jogador começa uma partida do zero (ex: saindo da BaseLab).
    /// NÃO chame isso ao avançar entre níveis da mesma run — use AdvanceLevel().
    /// </summary>
    public void StartNewRun()
    {
        EnsureMutationCatalog();
        currentRoomNumber = 1;
        skipNextAdvanceLevel = false;
        enteredEndlessFromBossRift = false;

        // Cheat: se forceBossNextRun está ativo, pula direto pro boss
        if (forceBossNextRun)
        {
            currentLevel = totalLevels;
            forceBossNextRun = false;
            Debug.Log($"[RunManager] 🎮 CHEAT BOSS: Run iniciada direto no Boss Round! (Level {currentLevel}/{totalLevels})");
        }
        else
        {
            currentLevel = 1;
        }

        // Reset do progresso do Geobionte
        geobionteDefeatCount = 0;
        geobionteAbsorbedThisLevel = false;

        // Reset do Mercador (permite 1 encontro e 1 pacto por run)
        hasSpawnedMerchantThisRun = false;
        hasCompletedPactThisRun = false;

        // Estado do Modo Endless ao iniciar da Base
        if (isEndlessMode)
        {
            endlessFloorCount = 1;
            lastPrimaryMutation = AbyssMutationType.None;
            RollAbyssMutationsForCurrentFloor();
        }
        else
        {
            endlessFloorCount = 0;
            ClearAbyssMutations();
        }

        Debug.Log($"[RunManager] 🆕 Nova run iniciada. Sala 1 | Round {currentLevel} | Endless: {isEndlessMode} (Andar {endlessFloorCount}).");
        UpdateEndlessUI();

        RunStatsManager.Instance?.StartRunTracking(isEndlessMode);
        if (isEndlessMode)
        {
            RunStatsManager.Instance?.CommitEndlessRecord(currentLevel);
        }
    }

    [Header("Modo Endless — Ciclo e Escalonamento")]
    public bool isEndlessMode = false;

    [Tooltip("Teto máximo de salas principais por andar no Modo Endless.")]
    [SerializeField] private int maxEndlessRoomsPerFloor = 12;

    [Tooltip("A cada quantos andares no Endless ocorre uma Boss Fight (ex: 3 = andares 3, 6, 9...).")]
    [SerializeField] private int bossCycleInterval = 3;

    [Tooltip("Andar Endless a partir do qual podem ocorrer 2 Mutações do Abismo simultâneas.")]
    [SerializeField] private int dualMutationStartFloor = 7;

    [Tooltip("Incremento base de velocidade de movimento inimiga por andar Endless além do 1º (ex: 0.04 = +4%/andar).")]
    [SerializeField] private float endlessSpeedPerFloor = 0.04f;

    [Tooltip("Teto seguro do multiplicador de velocidade dos inimigos no Endless (evita quebrar física/animações).")]
    [SerializeField] private float maxEndlessEnemySpeedMultiplier = 1.45f;

    [Tooltip("Bônus progressivo na chance de spawn de variantes/elites por andar Endless (ex: 0.04 = +4%/andar).")]
    [SerializeField] private float endlessEliteSpawnBonusPerFloor = 0.04f;

    [Tooltip("Teto seguro para o bônus total de chance de spawn Elite no Endless.")]
    [SerializeField] private float maxEndlessEliteSpawnBonus = 0.55f;

    [Tooltip("Bônus progressivo na chance de renascimento Elite por andar Endless (ex: 0.025 = +2.5%/andar).")]
    [SerializeField] private float endlessEliteRebirthBonusPerFloor = 0.025f;

    [Tooltip("Teto seguro para o bônus total de renascimento Elite no Endless.")]
    [SerializeField] private float maxEndlessEliteRebirthBonus = 0.45f;

    [Header("Mutações do Abismo (Endless)")]
    [Tooltip("Catálogo das Mutações do Abismo sorteadas por andar no Modo Endless.")]
    [SerializeField] private List<AbyssMutationDefinition> mutationCatalog = new List<AbyssMutationDefinition>();

    /// <summary>Contador de andares percorridos dentro do Modo Endless (inicia em 1 ao entrar no Endless).</summary>
    [HideInInspector] public int endlessFloorCount = 0;

    /// <summary>Mutações do Abismo ativas no andar atual do Modo Endless.</summary>
    [HideInInspector] public List<AbyssMutationType> activeMutations = new List<AbyssMutationType>();

    private AbyssMutationType lastPrimaryMutation = AbyssMutationType.None;
    private bool skipNextAdvanceLevel = false;
    private bool enteredEndlessFromBossRift = false;
    private GameObject endlessCanvasInstance;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateEndlessUI();
    }

    private static bool IsNonDungeonScene(string sceneName)
    {
        return string.IsNullOrEmpty(sceneName) ||
               sceneName == "Base" ||
               sceneName == "BaseLab" ||
               sceneName == "MainMenu" ||
               sceneName == "Menu" ||
               sceneName.Contains("Loading");
    }

    void Start()
    {
        EnsureMutationCatalog();
        if (isEndlessMode)
        {
            if (endlessFloorCount <= 0) endlessFloorCount = 1;
            if (activeMutations.Count == 0) RollAbyssMutationsForCurrentFloor();
        }
        UpdateEndlessUI();
    }

    /// <summary>
    /// Ativa ou desativa o Modo Endless de forma limpa (Base, Cheat ou Fluxo de Menu).
    /// </summary>
    public void SetEndlessMode(bool enabled)
    {
        EnsureMutationCatalog();
        isEndlessMode = enabled;
        skipNextAdvanceLevel = false;

        if (isEndlessMode)
        {
            enteredEndlessFromBossRift = false;
            if (endlessFloorCount <= 0) endlessFloorCount = 1;
            if (activeMutations.Count == 0) RollAbyssMutationsForCurrentFloor();
            if (RunStatsManager.Instance != null && RunStatsManager.Instance.isRunActive)
            {
                RunStatsManager.Instance.isEndlessMode = true;
            }
        }
        else
        {
            enteredEndlessFromBossRift = false;
            endlessFloorCount = 0;
            ClearAbyssMutations();
            if (RunStatsManager.Instance != null)
            {
                RunStatsManager.Instance.isEndlessMode = false;
            }
        }

        UpdateEndlessUI();
        Debug.Log($"[RunManager] ♾️ SetEndlessMode({isEndlessMode}) | Level: {currentLevel} | EndlessFloor: {endlessFloorCount}");
    }

    /// <summary>
    /// Ativa o loop do Modo Endless a partir da Fenda Pós-Boss:
    /// mantém a build do player, ativa isEndlessMode, inicia endlessFloorCount em 1,
    /// avança currentLevel para manter o escalonamento ativo, sorteia Mutação do Abismo e atualiza HUD/Stats.
    /// Marca skipNextAdvanceLevel para que o GameManager.LoadNextLevel() imediato não pule o 1º andar do Endless.
    /// </summary>
    public void EnterEndlessLoopFromBoss()
    {
        EnsureMutationCatalog();
        isEndlessMode = true;
        enteredEndlessFromBossRift = true;
        forceBossNextRun = false;
        endlessFloorCount = 1;
        currentLevel = Mathf.Max(currentLevel + 1, totalLevels);
        skipNextAdvanceLevel = true;
        geobionteAbsorbedThisLevel = false;

        lastPrimaryMutation = AbyssMutationType.None;
        RollAbyssMutationsForCurrentFloor();

        RunStatsManager.Instance?.PromoteToEndlessMode(currentLevel);
        UpdateEndlessUI();

        Debug.Log($"[RunManager] 🌀 Fenda Pós-Boss → Loop Endless iniciado! Level {currentLevel} | Andar Endless {endlessFloorCount} | Mutação: {GetActiveMutationsShortNames()}");
    }

    /// <summary>
    /// Chamado pelo GameManager.ReturnToBase() para garantir que o estado e o HUD do Modo Endless
    /// não vazem para a Base após morte, vitória ou extração pela Fenda.
    /// </summary>
    public void OnReturnedToBase()
    {
        if (isEndlessMode || enteredEndlessFromBossRift || endlessFloorCount > 0)
        {
            SetEndlessMode(false);
        }
        else
        {
            skipNextAdvanceLevel = false;
            enteredEndlessFromBossRift = false;
            UpdateEndlessUI();
        }
    }

    /// <summary>
    /// True se o round atual é Boss Fight:
    /// - Modo Normal: currentLevel >= totalLevels (preservado).
    /// - Modo Endless: a cada bossCycleInterval andares (endlessFloorCount > 0 && endlessFloorCount % bossCycleInterval == 0).
    /// </summary>
    public bool isBossRound => isEndlessMode
        ? (bossCycleInterval > 0 && endlessFloorCount > 0 && endlessFloorCount % bossCycleInterval == 0)
        : (currentLevel >= totalLevels);

    /// <summary>
    /// Avança para o próximo round da run.
    /// Chamado pelo GameManager quando o jogador usa a Exit Room ou a Fenda Pós-Boss.
    /// </summary>
    public void AdvanceLevel()
    {
        if (skipNextAdvanceLevel)
        {
            skipNextAdvanceLevel = false;
            geobionteAbsorbedThisLevel = false;
            UpdateEndlessUI();
            Debug.Log($"[RunManager] 🌀 Mantendo 1º Andar Endless preparado pela Fenda (Level {currentLevel} | Andar {endlessFloorCount}).");
            return;
        }

        if (isEndlessMode)
        {
            currentLevel++;
            endlessFloorCount = Mathf.Max(1, endlessFloorCount + 1);
            RollAbyssMutationsForCurrentFloor();
            RunStatsManager.Instance?.CommitEndlessRecord(currentLevel);
        }
        else
        {
            currentLevel = Mathf.Min(currentLevel + 1, totalLevels);
        }

        // Permite o Geobionte absorver um novo cristal na próxima fase
        geobionteAbsorbedThisLevel = false;

        UpdateEndlessUI();

        Debug.Log($"[RunManager] ▶️ Round {currentLevel}/{totalLevels} | Endless? {isEndlessMode} (Andar {endlessFloorCount}) | Boss? {isBossRound} | Geobionte derrotas: {geobionteDefeatCount}/3");
    }

    /// <summary>
    /// Retorna o maxMainRooms configurado para o round atual.
    /// Retorna 0 se for Boss Round (o LevelGenerator deve lidar com isso).
    /// </summary>
    public int GetMaxRoomsForCurrentLevel()
    {
        if (isBossRound) return 0;

        int safeCap = Mathf.Max(5, maxEndlessRoomsPerFloor);
        if (isEndlessMode && currentLevel > 3)
        {
            int baseRooms = (roomsPerLevel != null && roomsPerLevel.Length > 0)
                ? roomsPerLevel[roomsPerLevel.Length - 1]
                : 9;
            return Mathf.Min(baseRooms + (currentLevel - 3) * 2, safeCap);
        }

        int idx = Mathf.Clamp(currentLevel - 1, 0, roomsPerLevel.Length - 1);
        return isEndlessMode ? Mathf.Min(roomsPerLevel[idx], safeCap) : roomsPerLevel[idx];
    }

    // =====================================================
    // MUTAÇÕES DO ABISMO (ENDLESS)
    // =====================================================

    public void ClearAbyssMutations()
    {
        activeMutations.Clear();
        lastPrimaryMutation = AbyssMutationType.None;
    }

    public void RollAbyssMutationsForCurrentFloor()
    {
        EnsureMutationCatalog();
        activeMutations.Clear();

        if (!isEndlessMode) return;

        List<AbyssMutationType> pool = new List<AbyssMutationType>
        {
            AbyssMutationType.HordaDeElites,
            AbyssMutationType.FrenesiAbissal,
            AbyssMutationType.SinfoniaDoCaos,
            AbyssMutationType.FortunaDoAbismo
        };

        // Evita repetir a mutação primária do andar anterior
        List<AbyssMutationType> primaryCandidates = pool.FindAll(m => m != lastPrimaryMutation);
        if (primaryCandidates.Count == 0) primaryCandidates = pool;

        AbyssMutationType primary = primaryCandidates[Random.Range(0, primaryCandidates.Count)];
        activeMutations.Add(primary);
        lastPrimaryMutation = primary;

        // Em andares altos (>= dualMutationStartFloor), sorteia uma 2ª mutação distinta
        if (endlessFloorCount >= dualMutationStartFloor)
        {
            List<AbyssMutationType> secondaryCandidates = pool.FindAll(m => m != primary);
            if (secondaryCandidates.Count > 0)
            {
                AbyssMutationType secondary = secondaryCandidates[Random.Range(0, secondaryCandidates.Count)];
                activeMutations.Add(secondary);
            }
        }

        Debug.Log($"[RunManager] ⚡ Mutações do Abismo (Andar Endless {endlessFloorCount}): {GetActiveMutationsShortNames()}");
    }

    public bool HasActiveMutation(AbyssMutationType type)
    {
        return isEndlessMode && type != AbyssMutationType.None && activeMutations != null && activeMutations.Contains(type);
    }

    public AbyssMutationDefinition GetMutationDefinition(AbyssMutationType type)
    {
        EnsureMutationCatalog();
        for (int i = 0; i < mutationCatalog.Count; i++)
        {
            if (mutationCatalog[i] != null && mutationCatalog[i].type == type)
            {
                return mutationCatalog[i];
            }
        }
        return AbyssMutationCatalog.GetDefault(type);
    }

    public List<AbyssMutationDefinition> GetActiveMutationDefinitions()
    {
        List<AbyssMutationDefinition> list = new List<AbyssMutationDefinition>();
        if (!isEndlessMode || activeMutations == null) return list;

        for (int i = 0; i < activeMutations.Count; i++)
        {
            if (activeMutations[i] == AbyssMutationType.None) continue;
            list.Add(GetMutationDefinition(activeMutations[i]));
        }
        return list;
    }

    public string GetActiveMutationsShortNames()
    {
        var defs = GetActiveMutationDefinitions();
        if (defs.Count == 0) return "Nenhuma";

        string text = "";
        for (int i = 0; i < defs.Count; i++)
        {
            if (i > 0) text += " + ";
            text += defs[i].displayName;
        }
        return text;
    }

    // =====================================================
    // MULTIPLICADORES DE ESCALONAMENTO ENDLESS
    // (Sempre retornam valores neutros quando isEndlessMode == false)
    // =====================================================

    public float GetEndlessHpMultiplier()
    {
        if (!isEndlessMode || currentLevel <= 3) return 1f;
        return 1f + (currentLevel - 3) * 0.15f;
    }

    public float GetEndlessEnemyMoveSpeedMultiplier()
    {
        if (!isEndlessMode) return 1f;

        int floorSteps = Mathf.Max(0, endlessFloorCount - 1);
        float mult = 1f + floorSteps * endlessSpeedPerFloor;

        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            mult *= Mathf.Max(1f, defs[i].enemyMoveSpeedMultiplier);
        }

        return Mathf.Clamp(mult, 1f, Mathf.Max(1f, maxEndlessEnemySpeedMultiplier));
    }

    public float GetEndlessEnemyAttackSpeedMultiplier()
    {
        if (!isEndlessMode) return 1f;

        int floorSteps = Mathf.Max(0, endlessFloorCount - 1);
        float mult = 1f + floorSteps * (endlessSpeedPerFloor * 0.75f);

        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            mult *= Mathf.Max(1f, defs[i].enemyAttackSpeedMultiplier);
        }

        return Mathf.Clamp(mult, 1f, Mathf.Max(1f, maxEndlessEnemySpeedMultiplier));
    }

    public float GetEndlessEliteSpawnBonus()
    {
        if (!isEndlessMode) return 0f;

        int floorSteps = Mathf.Max(0, endlessFloorCount - 1);
        float bonus = floorSteps * endlessEliteSpawnBonusPerFloor;

        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            bonus += Mathf.Max(0f, defs[i].eliteSpawnChanceBonus);
        }

        return Mathf.Clamp(bonus, 0f, Mathf.Clamp01(maxEndlessEliteSpawnBonus));
    }

    public float GetEndlessEliteRebirthBonus()
    {
        if (!isEndlessMode) return 0f;

        int floorSteps = Mathf.Max(0, endlessFloorCount - 1);
        float bonus = floorSteps * endlessEliteRebirthBonusPerFloor;

        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            bonus += Mathf.Max(0f, defs[i].eliteRebirthBonus);
        }

        return Mathf.Clamp(bonus, 0f, Mathf.Clamp01(maxEndlessEliteRebirthBonus));
    }

    public int GetEndlessBonusMaxElitesPerRoom()
    {
        if (!isEndlessMode) return 0;

        int bonus = endlessFloorCount >= 8 ? 2 : (endlessFloorCount >= 4 ? 1 : 0);
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            bonus += Mathf.Max(0, defs[i].bonusMaxElitesPerRoom);
        }

        return Mathf.Clamp(bonus, 0, 3);
    }

    public int GetEndlessEliteWeightEntries()
    {
        if (!isEndlessMode) return 0;

        int entries = endlessFloorCount >= 6 ? 2 : (endlessFloorCount >= 3 ? 1 : 0);
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            entries += Mathf.Max(0, defs[i].bonusEliteWeightEntries);
        }

        return Mathf.Clamp(entries, 0, 4);
    }

    public float GetEndlessSpawnBudgetMultiplier()
    {
        if (!isEndlessMode) return 1f;

        float mult = 1f;
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            mult *= Mathf.Max(0.5f, defs[i].spawnBudgetMultiplier);
        }
        return Mathf.Clamp(mult, 1f, 1.75f);
    }

    public float GetEndlessEssenceDropMultiplier()
    {
        if (!isEndlessMode) return 1f;

        float mult = 1f;
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            mult *= Mathf.Max(1f, defs[i].essenceDropMultiplier);
        }
        return mult;
    }

    public float GetEndlessItemDropChanceBonus()
    {
        if (!isEndlessMode) return 0f;

        float bonus = 0f;
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            bonus += Mathf.Max(0f, defs[i].itemDropChanceBonus);
        }
        return Mathf.Clamp01(bonus);
    }

    public float GetEndlessExtraItemDropChance()
    {
        if (!isEndlessMode) return 0f;

        float chance = 0f;
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            chance = Mathf.Max(chance, defs[i].extraItemDropChance);
        }
        return Mathf.Clamp01(chance);
    }

    public float GetEndlessEliteTierChanceBonus()
    {
        if (!isEndlessMode) return 0f;

        float bonus = 0f;
        var defs = GetActiveMutationDefinitions();
        for (int i = 0; i < defs.Count; i++)
        {
            bonus += Mathf.Max(0f, defs[i].eliteTierChanceBonus);
        }
        return Mathf.Clamp(bonus, 0f, 0.5f);
    }

    public bool TryGetChaosMutationAdrenalineParams(bool isEliteKill, out float duration, out float moveBonus, out float attackBonus, out float triggerChance)
    {
        duration = 5f;
        moveBonus = 0.15f;
        attackBonus = 0.20f;
        triggerChance = 0f;

        if (!HasActiveMutation(AbyssMutationType.SinfoniaDoCaos)) return false;

        AbyssMutationDefinition def = GetMutationDefinition(AbyssMutationType.SinfoniaDoCaos);
        if (isEliteKill)
        {
            duration = Mathf.Max(5f, def.chaosEliteAdrenalineDuration);
            moveBonus = Mathf.Max(0.15f, def.chaosEliteMoveBonus);
            attackBonus = Mathf.Max(0.20f, def.chaosEliteAttackSpeedBonus);
            triggerChance = 1f;
        }
        else
        {
            duration = Mathf.Max(3f, def.chaosAdrenalineDuration);
            moveBonus = 0.15f;
            attackBonus = 0.20f;
            triggerChance = Mathf.Clamp01(def.chaosKillAdrenalineChance);
        }
        return true;
    }

    // =====================================================
    // UI TOPO-CENTRAL DO MODO ENDLESS
    // =====================================================

    private string BuildEndlessBannerText()
    {
        string bossTag = isBossRound ? "  <color=#FFD166>[BOSS DO CICLO]</color>" : "";
        string header = $"MODO ENDLESS — NÍVEL {currentLevel}  <size=20><color=#FFD5D5>(ANDAR {Mathf.Max(1, endlessFloorCount)})</color></size>{bossTag}";

        var defs = GetActiveMutationDefinitions();
        if (defs.Count == 0) return header;

        string mutLine = "";
        for (int i = 0; i < defs.Count; i++)
        {
            if (i > 0) mutLine += "  <color=#AAAAAA>|</color>  ";
            mutLine += $"<color={defs[i].accentHexColor}>» {defs[i].displayName.ToUpper()}</color>";
        }

        return $"{header}\n<size=17>{mutLine}</size>";
    }

    public void UpdateEndlessUI()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        bool shouldShowBanner = isEndlessMode && !IsNonDungeonScene(activeScene);

        if (shouldShowBanner)
        {
            if (endlessCanvasInstance == null)
            {
                CreateEndlessUI();
            }
            else
            {
                if (!endlessCanvasInstance.activeSelf)
                {
                    endlessCanvasInstance.SetActive(true);
                }

                var textComp = endlessCanvasInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (textComp != null)
                {
                    textComp.text = BuildEndlessBannerText();
                }
            }
        }
        else
        {
            if (endlessCanvasInstance != null)
            {
                if (!isEndlessMode)
                {
                    Destroy(endlessCanvasInstance);
                    endlessCanvasInstance = null;
                }
                else if (endlessCanvasInstance.activeSelf)
                {
                    endlessCanvasInstance.SetActive(false);
                }
            }
        }
    }

    private void CreateEndlessUI()
    {
        GameObject go = new GameObject("EndlessModeUI");
        DontDestroyOnLoad(go);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;

        UnityEngine.UI.CanvasScaler scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject textObj = new GameObject("EndlessText");
        textObj.transform.SetParent(go.transform, false);

        TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = BuildEndlessBannerText();
        text.fontSize = 25;
        text.color = new Color(1f, 0.35f, 0.35f, 0.90f);
        text.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.Italic;
        text.alignment = TMPro.TextAlignmentOptions.Top;
        text.raycastTarget = false;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(760f, 70f);

        endlessCanvasInstance = go;
    }

    /// <summary>
    /// Atualiza a sala atual (chamado pelo RoomEnemySpawner ao entrar na sala).
    /// </summary>
    public void SetCurrentRoom(int roomNumber)
    {
        currentRoomNumber = Mathf.Max(1, roomNumber);
        Debug.Log($"[RUN MANAGER] Sala atual: {currentRoomNumber}");
    }

    /// <summary>
    /// Multiplicador de essência para a sala n.
    /// E(n) = d × (1 + α × n) → multiplicador = (1 + α × n)
    /// 
    /// Se roomNumber = -1, usa o currentRoomNumber.
    /// </summary>
    public float GetEssenceMultiplier(int roomNumber = -1)
    {
        int n = roomNumber < 0 ? currentRoomNumber : roomNumber;
        float baseMult = 1f + dropInflationAlpha * n;

        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player != null && player.hasChaosSymphony)
        {
            baseMult *= 2f; // Dobro de essência
        }

        if (isEndlessMode)
        {
            baseMult *= GetEndlessEssenceDropMultiplier();
        }

        return baseMult;
    }

    /// <summary>
    /// Orçamento de pontos de spawn para a sala n no round/level atual.
    /// Escala de forma progressiva considerando tanto a sala quanto a fase (round) da run.
    /// Garante que no Round 2 e Round 3 o orçamento de cada onda seja suficiente para spawnar
    /// Tanques (4 pts), Atiradores (2 pts) e Elites (10 pts como Geobionte).
    /// </summary>
    public int GetSpawnBudget(int roomNumber)
    {
        int level = Mathf.Max(1, currentLevel);
        int room = Mathf.Max(1, roomNumber);

        float baseBudget;
        float perRoomBudget;

        if (level == 1)
        {
            // Level 1: Início calmo e escalonado para aprendizado do jogador
            // Sala 1 = 4 pts, Sala 2 = 5 pts, Sala 3 = 6 pts, Sala 4 = 7 pts, Sala 5 = 8 pts
            baseBudget = 3.5f;
            perRoomBudget = 0.9f;
        }
        else if (level == 2)
        {
            // Level 2: Intermediário
            // Sala 1 = 14 pts, Sala 4 = 19 pts, Sala 7 = 25 pts
            baseBudget = 12f;
            perRoomBudget = 1.8f;
        }
        else if (level == 3)
        {
            // Level 3: Avançado com grande variedade de mobs pesados
            // Sala 1 = 25 pts, Sala 5 = 35 pts, Sala 9 = 45 pts
            baseBudget = 22f;
            perRoomBudget = 2.5f;
        }
        else // Endless / Level 4+
        {
            baseBudget = 36f + (level - 3) * 15f;
            perRoomBudget = 4.0f;
        }

        int budget = Mathf.RoundToInt(baseBudget + perRoomBudget * room);

        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player != null && player.hasChaosSymphony)
        {
            budget = Mathf.RoundToInt(budget * 2.0f); // Dobra o budget de inimigos na sala
        }

        if (isEndlessMode)
        {
            budget = Mathf.RoundToInt(budget * GetEndlessSpawnBudgetMultiplier());
        }

        return budget;
    }
}
