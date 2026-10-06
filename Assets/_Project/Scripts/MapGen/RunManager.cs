using UnityEngine;

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

    // =====================================================

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
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
        currentRoomNumber = 1;

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

        Debug.Log($"[RunManager] 🆕 Nova run iniciada. Sala 1 | Round {currentLevel}.");
        UpdateEndlessUI();

        RunStatsManager.Instance?.StartRunTracking(isEndlessMode);
    }

    [Header("Modo Endless")]
    public bool isEndlessMode = false;
    private GameObject endlessCanvasInstance;

    void Start()
    {
        UpdateEndlessUI();
    }

    /// <summary>
    /// True se o round atual é o Boss Fight (último round da run).
    /// </summary>
    public bool isBossRound => !isEndlessMode && currentLevel >= totalLevels;

    /// <summary>
    /// Avança para o próximo round da run.
    /// Chamado pelo GameManager quando o jogador usa a Exit Room.
    /// </summary>
    public void AdvanceLevel()
    {
        if (isEndlessMode)
        {
            currentLevel++;
        }
        else
        {
            currentLevel = Mathf.Min(currentLevel + 1, totalLevels);
        }

        // Permite o Geobionte absorver um novo cristal na próxima fase
        geobionteAbsorbedThisLevel = false;

        UpdateEndlessUI();

        Debug.Log($"[RunManager] ▶️ Round {currentLevel}/{totalLevels} | Boss? {isBossRound} | Geobionte derrotas: {geobionteDefeatCount}/3");
    }

    /// <summary>
    /// Retorna o maxMainRooms configurado para o round atual.
    /// Retorna 0 se for Boss Round (o LevelGenerator deve lidar com isso).
    /// </summary>
    public int GetMaxRoomsForCurrentLevel()
    {
        if (isBossRound) return 0;

        if (isEndlessMode && currentLevel > 3)
        {
            int baseRooms = roomsPerLevel[roomsPerLevel.Length - 1]; // 9
            return baseRooms + (currentLevel - 3) * 2; // Infla +2 salas por level a mais
        }

        int idx = Mathf.Clamp(currentLevel - 1, 0, roomsPerLevel.Length - 1);
        return roomsPerLevel[idx];
    }

    public void UpdateEndlessUI()
    {
        if (isEndlessMode)
        {
            if (endlessCanvasInstance == null)
            {
                CreateEndlessUI();
            }
            else
            {
                var textComp = endlessCanvasInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = $"♾️ MODO ENDLESS: NÍVEL {currentLevel}";
                }
            }
        }
        else
        {
            if (endlessCanvasInstance != null)
            {
                Destroy(endlessCanvasInstance);
                endlessCanvasInstance = null;
            }
        }
    }

    private void CreateEndlessUI()
    {
        GameObject go = new GameObject("EndlessModeUI");
        DontDestroyOnLoad(go);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        UnityEngine.UI.CanvasScaler scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject textObj = new GameObject("EndlessText");
        textObj.transform.SetParent(go.transform, false);
        
        TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = $"♾️ MODO ENDLESS: NÍVEL {currentLevel}";
        text.fontSize = 28;
        text.color = new Color(1f, 0.3f, 0.3f, 0.85f);
        text.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.Italic;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -25f);
        rect.sizeDelta = new Vector2(500f, 50f);

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

        return baseMult;
    }

    /// <summary>
    /// Orçamento de pontos de spawn para a sala n.
    /// P(1) = 4 pts (apenas mobs leves), crescendo gradualmente a cada sala.
    /// </summary>
    public int GetSpawnBudget(int roomNumber)
    {
        int n = Mathf.Max(1, roomNumber);
        int budget = Mathf.RoundToInt(3f + 1.1f * n);

        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player != null && player.hasChaosSymphony)
        {
            budget = Mathf.RoundToInt(budget * 2.0f); // Dobra o budget de inimigos na sala
        }

        return budget;
    }
}
