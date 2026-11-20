using UnityEngine;
using System.Text;

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

    // ==================== SEED DA RUN ====================

    [Header("Seed da Run")]

    [Tooltip("Versão textual e legível da Seed utilizada na run atual. Ex: EPTA-4921.")]
    [SerializeField] private string activeSeedText = "";

    [Tooltip("Valor numérico determinístico derivado da Seed textual.")]
    [SerializeField] private int masterSeed = 0;

    /// <summary>
    /// Seed textual atualmente utilizada pela run.
    /// Pode ser exibida no HUD, menu de pausa ou tela de fim de run.
    /// </summary>
    public string ActiveSeedText => activeSeedText;

    /// <summary>
    /// Seed numérica principal da run.
    /// Serve como origem para os geradores aleatórios determinísticos
    /// utilizados pelo mapa e pelas salas.
    /// </summary>
    public int MasterSeed => masterSeed;

    // Seed digitada no menu, esperando a run começar. Vazia = jogador não digitou nada.
    private string pendingSeedText = "";

    // O menu vai chamar isso para guardar a seed até a run começar.
    public void SetPendingSeed(string seedText)
    {
        pendingSeedText = seedText;
    }

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
        }
        else
        {
            Destroy(gameObject);
        }
    }


    /// <summary>
    /// Configura a Seed que será utilizada durante toda a run.
    /// Se nenhum texto for informado, uma nova Seed textual é gerada automaticamente.
    ///
    /// A conversão da Seed textual para inteiro utiliza FNV-1a,
    /// garantindo que o mesmo texto sempre produza o mesmo valor numérico.
    /// </summary>
    /// <param name="seedText">
    /// Seed textual desejada, por exemplo "EPTA-4921".
    /// Pode ser vazia ou nula para gerar uma Seed automaticamente.
    /// </param>
    public void ConfigureRunSeed(string seedText)
    {
        if (string.IsNullOrWhiteSpace(seedText))
        {
            seedText = GenerateRandomSeedText();
        }

        activeSeedText = seedText.Trim().ToUpperInvariant();
        masterSeed = StableHash(activeSeedText);

        Debug.Log(
            $"[SEED] Seed ativa: {activeSeedText} | MasterSeed: {masterSeed}"
        );
    }

    /// <summary>
    /// Converte uma string em um inteiro determinístico utilizando
    /// o algoritmo FNV-1a de 32 bits.
    ///
    /// Diferentemente de string.GetHashCode(), este resultado permanece
    /// igual entre execuções diferentes do jogo.
    /// </summary>
    /// <param name="text">Texto que será convertido em Seed numérica.</param>
    /// <returns>Hash determinístico de 32 bits.</returns>
    private static int StableHash(string text)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            uint hash = offsetBasis;

            byte[] bytes = Encoding.UTF8.GetBytes(text);

            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= prime;
            }

            return (int)hash;
        }
    }

    /// <summary>
    /// Gera uma nova Seed textual legível para uma run que não recebeu
    /// uma Seed manualmente.
    ///
    /// Exemplo de resultado: "EPTA-A12F84C9".
    /// </summary>
    /// <returns>Nova Seed textual no formato EPTA-XXXXXXXX.</returns>
    private static string GenerateRandomSeedText()
    {
        string code = System.Guid.NewGuid()
            .ToString("N")
            .Substring(0, 8)
            .ToUpperInvariant();

        return $"EPTA-{code}";
    }

    /// <summary>
    /// Cria um gerador de números aleatórios exclusivo para a geração do mapa.
    ///
    /// O gerador utiliza diretamente a masterSeed da run. Portanto,
    /// duas runs iniciadas com a mesma Seed produzirão a mesma sequência
    /// de números aleatórios durante a geração do mapa, desde que as
    /// chamadas ao RNG ocorram na mesma ordem.
    /// </summary>
    /// <returns>
    /// Nova instância determinística de System.Random destinada
    /// exclusivamente à geração do mapa.
    /// </returns>
    public System.Random CreateMapRng()
    {
        unchecked
        {
            const int levelPrime = 83492791;
            return new System.Random(masterSeed ^ (currentLevel * levelPrime));
        }
    }

    /// <summary>
    /// Cria um gerador de números aleatórios independente para uma sala.
    ///
    /// A Seed da sala é derivada da masterSeed, do round atual e do índice
    /// da sala. Isso impede que o consumo de números aleatórios em uma sala
    /// altere os resultados das salas seguintes.
    ///
    /// Exemplo:
    /// gastar mais números aleatórios na Sala 1 não modifica a sequência
    /// aleatória utilizada pela Sala 2.
    /// </summary>
    /// <param name="roomIndex">
    /// Índice da sala dentro do round atual.
    /// </param>
    /// <returns>
    /// Nova instância determinística de System.Random exclusiva da sala.
    /// </returns>
    public System.Random CreateRoomRng(int roomIndex)
    {
        unchecked
        {
            const int roomPrime = 73856093;
            const int levelPrime = 83492791;

            int roomSeed =
                masterSeed ^
                (roomIndex * roomPrime) ^
                (currentLevel * levelPrime);

            return new System.Random(roomSeed);
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
        // Define a seed da run (se a caixinha estiver vazia, gera uma aleatória).
        ConfigureRunSeed(pendingSeedText);
        // Esvazia a caixinha: a próxima run sem seed digitada ganha uma seed nova.
        pendingSeedText = "";

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

        // Reset do Mercador (permite 1 encontro e 1 pacto por run)
        hasSpawnedMerchantThisRun = false;
        hasCompletedPactThisRun = false;

        Debug.Log($"[RunManager] 🆕 Nova run iniciada. Sala 1 | Round {currentLevel}. Mercador resetado para novo encontro único.");
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

        return budget;
    }
}
