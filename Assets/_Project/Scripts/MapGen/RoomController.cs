using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controlador de Sala — fusão do sistema de ondas/portas com o sistema de
/// pontos econômicos do GDD (Economy.pdf §1.2).
///
/// SISTEMA DE PONTOS:
///   Budget total da sala: P(n) = 10 + 0,9 × n   (via RunManager)
///   O budget é dividido igualmente entre as ondas sorteadas.
///   Cada onda usa sua cota de pontos para compor um grupo de inimigos,
///   respeitando as regras de classe definidas no GDD.
///
/// Regras de spawn por classe:
///   Mob Menor  → 1 ponto  | máx. 50% dos pontos totais
///   Atirador   → 2 pontos | sempre em pares (4 pts)
///   Tanque     → 4 pontos | máx. 4 por sala (total, não por onda)
///   Elite      → 10 pontos| máx. 1 por sala (total, não por onda)
///
/// FLUXO:
///   1. LevelGenerator chama Initialize(roomIndex)
///   2. Player entra → portas trancam → ondas começam
///   3. Cada onda spawna inimigos pelo sistema de pontos
///   4. Ao limpar todas as ondas, portas destrancam
///
/// SETUP NO UNITY:
///   • Adicione os prefabs nas listas de cada classe
///   • Configure spawnArea (BoxCollider)
///   • Arraste as portas em doors[]
///   • O LevelGenerator preenche roomIndex automaticamente
/// </summary>
public class RoomController : MonoBehaviour
{
    // =====================================================
    // CONFIGURAÇÃO DA SALA
    // =====================================================

    public enum RoomCategory
    {
        Combat,
        SafeRoom,
        Contemplation,
        Merchant
    }

    [Header("Sala")]
    public bool isSafeRoom = false;
    [Tooltip("Categoria da sala (Combat = combate normal, Contemplation = sala pacífica sem combate, SafeRoom = sala inicial, Merchant = mercador).")]
    public RoomCategory roomCategory = RoomCategory.Combat;
    [HideInInspector] public bool doorsAreLocked = false;
    public bool isCleared { get; set; } = false;

    /// <summary>Disparado quando uma sala de combate é limpa e as portas destravam.</summary>
    public static event System.Action<RoomController> OnRoomCleared;

    /// <summary>Modo Showcase: Força o spawn de 1 de cada tipo de mob na mesma sala toda vez.</summary>
    public static bool forceAllMobsMode = false;

    [Header("Índice (definido pelo LevelGenerator)")]
    [Tooltip("Número desta sala na sequência da Run (1–32). Não editar manualmente.")]
    public int roomIndex = 1;

    // =====================================================
    // POOLS DE INIMIGOS POR CLASSE (GDD §1.2)
    // =====================================================

    [Header("Configuração Global de Inimigos (EnemyPoolConfig)")]
    [Tooltip("Se preenchido, a sala usa este asset central para todas as categorias. Se nulo, carrega o DefaultEnemyPool de Resources.")]
    public EnemyPoolConfig enemyPoolConfig;

    [Header("Mob Menor — 1 ponto | máx. 50% do budget da onda (Fallback Local)")]
    [Tooltip("Todos os prefabs que podem aparecer como Mob Menor neste bioma.")]
    public List<GameObject> mobMenorPrefabs = new List<GameObject>();

    [Header("Atirador — 2 pontos | sempre em pares (4 pts/par)")]
    [Tooltip("Todos os prefabs que podem aparecer como Atirador neste bioma.")]
    public List<GameObject> atiradorPrefabs = new List<GameObject>();

    [Header("Tanque — 4 pontos | máx. 4 por sala (total)")]
    [Tooltip("Todos os prefabs que podem aparecer como Tanque neste bioma.")]
    public List<GameObject> tanquePrefabs = new List<GameObject>();

    [Header("Elite — 10 pontos | máx. 1 por sala (total)")]
    [Tooltip("Todos os prefabs que podem aparecer como Elite neste bioma.")]
    public List<GameObject> elitePrefabs = new List<GameObject>();

    [Header("Suporte — 3 pontos | máx. 2 por onda")]
    [Tooltip("Todos os prefabs que podem aparecer como Suporte neste bioma.")]
    public List<GameObject> suportePrefabs = new List<GameObject>();

    public List<GameObject> GetMobMenorPool() => (enemyPoolConfig != null && enemyPoolConfig.mobMenorPrefabs.Count > 0) ? enemyPoolConfig.mobMenorPrefabs : mobMenorPrefabs;
    public List<GameObject> GetAtiradorPool() => (enemyPoolConfig != null && enemyPoolConfig.atiradorPrefabs.Count > 0) ? enemyPoolConfig.atiradorPrefabs : atiradorPrefabs;
    public List<GameObject> GetTanquePool()   => (enemyPoolConfig != null && enemyPoolConfig.tanquePrefabs.Count > 0)   ? enemyPoolConfig.tanquePrefabs   : tanquePrefabs;
    public List<GameObject> GetElitePool()    => (enemyPoolConfig != null && enemyPoolConfig.elitePrefabs.Count > 0)    ? enemyPoolConfig.elitePrefabs    : elitePrefabs;
    public List<GameObject> GetSuportePool()  => (enemyPoolConfig != null && enemyPoolConfig.suportePrefabs.Count > 0)  ? enemyPoolConfig.suportePrefabs  : suportePrefabs;

    // =====================================================
    // CONFIGURAÇÃO DE ONDAS
    // =====================================================

    [Header("Ondas")]
    [Range(1, 5)] public int minWaves = 1;
    [Range(1, 5)] public int maxWaves = 3;
    [Tooltip("Intervalo em segundos entre o fim de uma onda e o início da próxima.")]
    public float timeBetweenWaves = 0.8f;
    [Tooltip("Número mínimo de inimigos garantidos por onda (Mob Menor preenchem o restante).")]
    [Range(1, 20)] public int minEnemiesPerWave = 3;
    [Tooltip("Fracão máxima do budget que pode ser gasta em Mob Menor. 0.7 = 70%.")]
    [Range(0.1f, 1f)] public float mobCapFraction = 0.7f;

    // =====================================================
    // CONFIGURAÇÃO DE SPAWN
    // =====================================================

    [Header("Spawn")]
    [Tooltip("Uma ou mais áreas onde inimigos podem aparecer.\nUse múltiplas para salas irregulares (Y-Shape, Ampulheta, etc.).")]
    public List<BoxCollider> spawnAreas = new List<BoxCollider>();
    [Tooltip("Prefab do indicador visual (círculo/marcador) antes do spawn.")]
    public GameObject spawnIndicatorPrefab;
    [Tooltip("Tempo em segundos entre o indicador aparecer e o inimigo spawnar.")]
    public float spawnDelay = 0.75f;
    [Tooltip("Altura acima do CHÃO da SpawnArea onde inimigos aparecem. 0.1 = rente ao chão.")]
    public float spawnHeightOffset = 0.1f;

    [Header("Bolsa Sintética - Probabilidades Ponderadas no Inspector")]
    [Tooltip("Lista de opções de Fauna terrestre/aérea com seus pesos de probabilidade no Inspector")]
    public List<CollectibleSpawnEntry> faunaSpawnEntries = new List<CollectibleSpawnEntry>();
    [Tooltip("Lista de opções de Flora com seus pesos de probabilidade no Inspector")]
    public List<CollectibleSpawnEntry> floraSpawnEntries = new List<CollectibleSpawnEntry>();
    [Tooltip("Lista de opções de Minérios com seus pesos de probabilidade no Inspector")]
    public List<CollectibleSpawnEntry> mineralSpawnEntries = new List<CollectibleSpawnEntry>();
    [Tooltip("Fauna aquática exclusiva para salas de lagoa (Peixe Lampião)")]
    public CollectibleSpawnEntry waterFaunaEntry;

    // =====================================================
    // PORTAS E REFERÊNCIAS
    // =====================================================

    [Header("Portas")]
    [Tooltip("GameObjects das portas que trancam durante o combate.")]
    public GameObject[] doors;

    [Header("Debug")]
    public bool showSpawnLog = true;

    // =====================================================
    // ESTADO INTERNO
    // =====================================================

    // Restrições globais (não por onda) — reiniciam apenas ao entrar na sala
    private int eliteSpawnedTotal  = 0;
    private int tanqueSpawnedTotal = 0;
    private int suporteSpawnedTotal = 0;

    private int   totalWavesThisRoom = 1;
    private int   currentWave        = 0;
    [HideInInspector] public int   enemiesPendingSpawn = 0;
    [HideInInspector] public List<GameObject> activeEnemies = new List<GameObject>();
    private bool  hasTriggered  = false;
    [HideInInspector] public bool  combatActive  = false;

    public bool ContainsActiveEnemy(GameObject enemy) => activeEnemies != null && activeEnemies.Contains(enemy);

    public void RegisterRebornPending(GameObject dyingEnemy)
    {
        enemiesPendingSpawn++;
        if (dyingEnemy != null && activeEnemies != null)
        {
            activeEnemies.Remove(dyingEnemy);
        }
    }

    private enum EnemyClass { Elite, Tanque, Atirador, MobMenor, Suporte }

    // =====================================================
    // API PÚBLICA
    // =====================================================

    public int CurrentWaveNumber => currentWave;
    public int TotalWaves => totalWavesThisRoom;
    public bool IsCombatActive => combatActive;
    public bool HasTriggered => hasTriggered;

    /// <summary>
    /// Chamado pelo LevelGenerator ao instanciar a sala.
    /// </summary>
    public void Initialize(int index)
    {
        roomIndex = Mathf.Max(1, index);
    }

    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================

    void Awake()
    {
        EnsureValidSpawnAreas();
        EnsureDefaultCollectibleEntries();

        if (enemyPoolConfig == null)
        {
            enemyPoolConfig = Resources.Load<EnemyPoolConfig>("DefaultEnemyPool");
        }

        if (!isSafeRoom && enemyPoolConfig != null)
        {
            SyncListsFromPoolConfig();
        }
    }

    public void SyncListsFromPoolConfig()
    {
        if (enemyPoolConfig == null || isSafeRoom) return;

        if (enemyPoolConfig.mobMenorPrefabs != null && enemyPoolConfig.mobMenorPrefabs.Count > 0)
            mobMenorPrefabs = new List<GameObject>(enemyPoolConfig.mobMenorPrefabs);
        if (enemyPoolConfig.atiradorPrefabs != null && enemyPoolConfig.atiradorPrefabs.Count > 0)
            atiradorPrefabs = new List<GameObject>(enemyPoolConfig.atiradorPrefabs);
        if (enemyPoolConfig.tanquePrefabs != null && enemyPoolConfig.tanquePrefabs.Count > 0)
            tanquePrefabs = new List<GameObject>(enemyPoolConfig.tanquePrefabs);
        if (enemyPoolConfig.elitePrefabs != null && enemyPoolConfig.elitePrefabs.Count > 0)
            elitePrefabs = new List<GameObject>(enemyPoolConfig.elitePrefabs);
        if (enemyPoolConfig.suportePrefabs != null && enemyPoolConfig.suportePrefabs.Count > 0)
            suportePrefabs = new List<GameObject>(enemyPoolConfig.suportePrefabs);
    }

    void Start()
    {
        string lowerName = gameObject.name.ToLower();
        if (lowerName.Contains("3way") || lowerName.Contains("treeway") ||
            lowerName.Contains("anomaly") || lowerName.Contains("anomalia") ||
            lowerName.Contains("deadend") || lowerName.Contains("beco") ||
            lowerName.Contains("semsaida") || GetComponent<AnomalyRoomController>() != null)
        {
            isSafeRoom = true;
            roomCategory = RoomCategory.Contemplation;
            doorsAreLocked = false;
            isCleared = true;
            if (doors != null)
            {
                foreach (GameObject d in doors) if (d) d.SetActive(false);
            }
        }

        if (isSafeRoom || roomCategory != RoomCategory.Combat || GetComponent<AnomalyRoomController>() != null)
        {
            doorsAreLocked = false;
            isCleared = true;
            if (doors != null)
            {
                foreach (GameObject d in doors) if (d) d.SetActive(false);
            }
            return;
        }

        EnsureValidSpawnAreas();
        totalWavesThisRoom = Random.Range(minWaves, maxWaves + 1);

        doorsAreLocked = false;
        isCleared = false;
        foreach (GameObject d in doors) if (d) d.SetActive(false);
    }

    void Update()
    {
        if (hasTriggered && combatActive)
            CheckWaveStatus();
    }

    // Caso o BoxCollider (trigger) esteja no MESMO GameObject que o RoomController
    private void OnTriggerEnter(Collider other)
    {
        OnPlayerEnteredRoom(other);
    }

    /// <summary>
    /// Chamado diretamente pelo OnTriggerEnter (quando o collider está no mesmo GO)
    /// OU pelo RoomTriggerProxy (quando o collider está num filho).
    /// </summary>
    public void OnPlayerEnteredRoom(Collider other)
    {
        if (other.CompareTag("Player") && !isSafeRoom && roomCategory == RoomCategory.Combat && !hasTriggered)
        {
            hasTriggered = true;

            // Atualiza o RunManager para esta sala
            if (RunManager.instance != null)
                RunManager.instance.SetCurrentRoom(roomIndex);

            StartCoroutine(StartCombatEncounter());
        }
    }

    // =====================================================
    // COMBATE E ONDAS
    // =====================================================

    IEnumerator StartCombatEncounter()
    {
        // Spawna os 3 tipos de coletáveis (Minérios, Fauna, Flora) antes do combate iniciar
        SpawnRoomCollectibles();

        // Aguarda o player entrar completamente na sala antes de fechar as portas.
        yield return new WaitForSeconds(0.35f);

        LockDoors();
        if (showSpawnLog)
            Debug.Log($"[ROOM {roomIndex}] Portas trancadas. {totalWavesThisRoom} ondas programadas.");
        yield return new WaitForSeconds(0.40f);

        SpawnNextWave();
        combatActive = true;
    }

    void SpawnNextWave()
    {
        currentWave++;

        int currentLvl = RunManager.instance != null ? RunManager.instance.currentLevel : 1;
        int minEnemies;
        int maxEnemies;

        if (currentLvl == 1)
        {
            // Fase 1: Escalonamento suave e gradual para o início da run
            if (roomIndex <= 2)
            {
                totalWavesThisRoom = 1;
                minEnemies = 2;
                maxEnemies = 3;
            }
            else if (roomIndex <= 4)
            {
                totalWavesThisRoom = Mathf.Min(totalWavesThisRoom, 2);
                minEnemies = 2;
                maxEnemies = 4;
            }
            else
            {
                totalWavesThisRoom = Mathf.Min(totalWavesThisRoom, 2);
                minEnemies = 3;
                maxEnemies = 4;
            }
        }
        else if (currentLvl == 2)
        {
            // Fase 2: Complexidade moderada
            minEnemies = 3;
            maxEnemies = 5;
        }
        else
        {
            // Fase 3+: Combate pleno com alta variedade
            minEnemies = 4;
            maxEnemies = 7;
        }

        // O budget total da sala é dividido igualmente entre as ondas.
        int totalBudget = RunManager.instance != null
            ? RunManager.instance.GetSpawnBudget(roomIndex)
            : Mathf.RoundToInt(3 + 0.9f * roomIndex);
        int waveBudget = Mathf.Max(3, Mathf.RoundToInt(totalBudget / (float)totalWavesThisRoom));

        if (showSpawnLog)
            Debug.Log($"[ROOM {roomIndex} | FASE {currentLvl}] ONDA {currentWave}/{totalWavesThisRoom} | Budget: {waveBudget} pts | Inimigos: {minEnemies}-{maxEnemies}");

        // Gera a lista de inimigos desta onda pelo algoritmo de pontos
        List<GameObject> waveEnemies = BuildWaveFromPoints(waveBudget, minEnemies, maxEnemies);

        // Flag de pendentes para o CheckWaveStatus
        enemiesPendingSpawn += waveEnemies.Count;

        // Spawna cada inimigo com delay e indicador visual
        foreach (GameObject prefab in waveEnemies)
        {
            StartCoroutine(SpawnEnemyWithDelay(prefab));
        }
    }

    /// <summary>
    /// Retorna todos os prefabs únicos configurados nos pools desta sala.
    /// </summary>
    public List<GameObject> GetAllUniqueEnemyPrefabs()
    {
        if (enemyPoolConfig != null)
        {
            var unique = enemyPoolConfig.GetAllUniquePrefabs();
            if (unique != null && unique.Count > 0) return unique;
        }

        List<GameObject> list = new List<GameObject>();
        void AddList(List<GameObject> source)
        {
            if (source == null) return;
            foreach (var p in source)
            {
                if (p != null && !list.Contains(p)) list.Add(p);
            }
        }

        AddList(mobMenorPrefabs);
        AddList(atiradorPrefabs);
        AddList(tanquePrefabs);
        AddList(elitePrefabs);
        AddList(suportePrefabs);

        return list;
    }

    /// <summary>
    /// Spawna instantaneamente 1 de cada tipo de inimigo cadastrado ao redor do jogador.
    /// </summary>
    public static int SpawnAllMobsNow()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 centerPos = player != null ? player.transform.position : Vector3.zero;

        RoomController targetRoom = null;
        RoomController[] allRooms = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);

        float closestDist = float.MaxValue;
        foreach (var room in allRooms)
        {
            if (room == null) continue;
            float dist = Vector3.Distance(room.transform.position, centerPos);
            if (dist < closestDist)
            {
                closestDist = dist;
                targetRoom = room;
            }
        }

        List<GameObject> prefabsToSpawn = new List<GameObject>();
        if (targetRoom != null)
        {
            prefabsToSpawn.AddRange(targetRoom.GetAllUniqueEnemyPrefabs());
        }

        // Se a sala atual não tiver todos os tipos, busca em todas as salas da cena
        foreach (var room in allRooms)
        {
            foreach (var p in room.GetAllUniqueEnemyPrefabs())
            {
                if (p != null && !prefabsToSpawn.Contains(p))
                    prefabsToSpawn.Add(p);
            }
        }

        if (prefabsToSpawn.Count == 0)
        {
            Debug.LogWarning("[ALL MOBS] Nenhum prefab de inimigo encontrado!");
            return 0;
        }

        int count = 0;
        float angleStep = 360f / prefabsToSpawn.Count;
        float radius = Mathf.Clamp(3f + (prefabsToSpawn.Count * 0.4f), 4f, 10f);

        for (int i = 0; i < prefabsToSpawn.Count; i++)
        {
            GameObject prefab = prefabsToSpawn[i];
            if (prefab == null) continue;

            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 spawnPos = centerPos + offset;

            // Alinha com NavMesh ou chão
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }
            else if (Physics.Raycast(spawnPos + Vector3.up * 3f, Vector3.down, out RaycastHit groundHit, 10f))
            {
                spawnPos = groundHit.point;
            }

            GameObject enemy = Object.Instantiate(prefab, spawnPos, Quaternion.LookRotation((centerPos - spawnPos).normalized));

            if (enemy.GetComponent<NavMeshBoundaryConstraint>() == null)
                enemy.AddComponent<NavMeshBoundaryConstraint>();

            if (targetRoom != null)
            {
                targetRoom.activeEnemies.Add(enemy);
                MagicStone_AI magicStone = enemy.GetComponent<MagicStone_AI>();
                if (magicStone != null && targetRoom.spawnAreas.Count > 0 && targetRoom.spawnAreas[0] != null)
                    magicStone.SetRoomBounds(targetRoom.spawnAreas[0]);
            }

            count++;
        }

        Debug.Log($"[ALL MOBS] Spawnados {count} inimigos únicos com sucesso ao redor do jogador!");
        return count;
    }

    /// <summary>
    /// Monta a lista de inimigos de uma onda usando o sistema de pontos do GDD.
    /// Respeita a progressão por salas e limites GLOBAIS.
    /// </summary>
    List<GameObject> BuildWaveFromPoints(int waveBudget, int minEnemies, int maxEnemies = 10)
    {
        if (forceAllMobsMode)
        {
            List<GameObject> allUnique = GetAllUniqueEnemyPrefabs();
            if (allUnique != null && allUnique.Count > 0)
            {
                Debug.Log($"[ALL MOBS MODE] Spawnando todos os {allUnique.Count} tipos únicos de mobs nesta sala!");
                return allUnique;
            }
        }

        int remainingPts = waveBudget;
        List<GameObject> curMobMenor = GetMobMenorPool();
        List<GameObject> curAtirador = GetAtiradorPool();
        List<GameObject> curTanque   = GetTanquePool();
        List<GameObject> curElite    = GetElitePool();
        List<GameObject> curSuporte  = GetSuportePool();

        List<GameObject> result = new List<GameObject>();

        int currentLvl = RunManager.instance != null ? RunManager.instance.currentLevel : 1;
        int effectiveProgression = ((currentLvl - 1) * 4) + roomIndex;

        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        bool chaosMode = player != null && player.hasChaosSymphony;
        int eliteMax = chaosMode ? 3 : (currentLvl >= 3 ? 2 : 1);
        int tanqueMax = chaosMode ? 8 : (currentLvl >= 3 ? 6 : (currentLvl >= 2 ? 4 : 2));

        // 1. Suporte: da Sala 2 ou Round 2 em diante (máx 1 suporte por sala)
        if (effectiveProgression >= 2 && currentWave == 1 && curSuporte.Count > 0 && suporteSpawnedTotal < 1 && remainingPts >= 3 && result.Count < maxEnemies)
        {
            result.Add(GetRandom(curSuporte));
            suporteSpawnedTotal++;
            remainingPts -= 3;
        }

        // 2. ÂNCORA DE VARIEDADE: Garante que inimigos pesados (Elite ou Tanque) e Atiradores apareçam com frequência
        // No Round 3 (Fase 3), prioriza spawnar Geobionte (Elite) e Tanques pesados (Golem, Magic Stone, Star, Totem)!
        if (currentLvl >= 3 && curElite.Count > 0 && remainingPts >= 10 && eliteSpawnedTotal < eliteMax && Random.value < 0.75f && result.Count < maxEnemies)
        {
            result.Add(GetRandom(curElite));
            eliteSpawnedTotal++;
            remainingPts -= 10;
        }

        // Garante presença de Tanques no Round 2 e Round 3
        if (effectiveProgression >= 2 && curTanque.Count > 0 && remainingPts >= 4 && tanqueSpawnedTotal < tanqueMax && Random.value < 0.75f && result.Count < maxEnemies)
        {
            result.Add(GetRandom(curTanque));
            tanqueSpawnedTotal++;
            remainingPts -= 4;
        }

        // Garante presença de Atiradores (Peixe, Goblin, Crystal Watcher)
        if (effectiveProgression >= 2 && curAtirador.Count > 0 && remainingPts >= 2 && Random.value < 0.70f && result.Count < maxEnemies)
        {
            result.Add(GetRandom(curAtirador));
            remainingPts -= 2;
        }

        // 3. Distribuição ponderada e equilibrada para os pontos restantes
        int loopGuard = 0;
        while (remainingPts > 0 && result.Count < maxEnemies && loopGuard < 30)
        {
            loopGuard++;
            List<EnemyClass> validOptions = new List<EnemyClass>();

            // Elite (10 pts)
            if (effectiveProgression >= 4 && curElite.Count > 0 && remainingPts >= 10 && eliteSpawnedTotal < eliteMax)
                validOptions.Add(EnemyClass.Elite);

            // Tanque (4 pts)
            if (effectiveProgression >= 2 && curTanque.Count > 0 && remainingPts >= 4 && tanqueSpawnedTotal < tanqueMax)
                validOptions.Add(EnemyClass.Tanque);

            // Atirador (2 pts individual ou 4 pts par)
            if (effectiveProgression >= 2 && curAtirador.Count > 0 && remainingPts >= 2)
                validOptions.Add(EnemyClass.Atirador);

            // Mob Menor (1 pt)
            if (curMobMenor.Count > 0 && remainingPts >= 1)
                validOptions.Add(EnemyClass.MobMenor);

            if (validOptions.Count == 0) break;

            // Sorteio justo entre as opções viáveis
            EnemyClass chosen = validOptions[Random.Range(0, validOptions.Count)];

            // Em fases avançadas (Round 2 e 3), favorece variedade de Tanques e Atiradores sobre MobMenor
            if (currentLvl >= 2 && validOptions.Count > 1 && chosen == EnemyClass.MobMenor && Random.value < 0.65f)
            {
                List<EnemyClass> advanced = validOptions.FindAll(c => c != EnemyClass.MobMenor);
                if (advanced.Count > 0) chosen = advanced[Random.Range(0, advanced.Count)];
            }

            switch (chosen)
            {
                case EnemyClass.Elite:
                    result.Add(GetRandom(curElite));
                    eliteSpawnedTotal++;
                    remainingPts -= 10;
                    break;

                case EnemyClass.Tanque:
                    result.Add(GetRandom(curTanque));
                    tanqueSpawnedTotal++;
                    remainingPts -= 4;
                    break;

                case EnemyClass.Atirador:
                    result.Add(GetRandom(curAtirador));
                    remainingPts -= 2;
                    if (remainingPts >= 2 && result.Count < maxEnemies && Random.value < 0.5f)
                    {
                        result.Add(GetRandom(curAtirador));
                        remainingPts -= 2;
                    }
                    break;

                case EnemyClass.MobMenor:
                    result.Add(GetRandom(curMobMenor));
                    remainingPts -= 1;
                    break;

                case EnemyClass.Suporte:
                    result.Add(GetRandom(curSuporte));
                    remainingPts -= 3;
                    break;
            }
        }

        // Garante mínimo de inimigos por onda sem ultrapassar o maxEnemies nem inflar artificialmente caso já existam elites ou tanques
        int targetMin = Mathf.Min(minEnemies, maxEnemies);
        if (eliteSpawnedTotal > 0) targetMin = Mathf.Min(targetMin, 3);
        else if (tanqueSpawnedTotal >= 2) targetMin = Mathf.Min(targetMin, 4);

        while (result.Count < targetMin && result.Count < maxEnemies)
        {
            // No Round 2 e 3, varia o preenchimento entre Atiradores e MobMenor
            if (currentLvl >= 2 && curAtirador.Count > 0 && Random.value < 0.4f)
            {
                result.Add(GetRandom(curAtirador));
            }
            else if (curMobMenor.Count > 0)
            {
                result.Add(GetRandom(curMobMenor));
            }
            else
            {
                break;
            }
        }

        // Trava final rígida para que nunca exceda maxEnemies
        if (result.Count > maxEnemies)
        {
            result = result.GetRange(0, maxEnemies);
        }

        if (showSpawnLog)
            Debug.Log($"[ROOM {roomIndex} | FASE {currentLvl}] Onda {currentWave}: {result.Count} inimigos gerados (Elite:{eliteSpawnedTotal} Tanque:{tanqueSpawnedTotal} Suporte:{suporteSpawnedTotal})");

        return result;
    }

    IEnumerator SpawnEnemyWithDelay(GameObject prefab)
    {
        EnsureValidSpawnAreas();
        if (prefab == null || spawnAreas == null || spawnAreas.Count == 0)
        {
            enemiesPendingSpawn--;
            yield break;
        }

        // ── INTERCEPTAÇÃO DE SPAWN DUPLO (Variante Elite / Shiny) ───────────
        // Após o sistema normal decidir qual prefab spawnar (pagando os pontos),
        // verificamos se existe uma variante configurada e rolamos a mutationChance.
        // O custo em pontos NAO muda — a variante é gratuita em termos de budget.
        GameObject prefabToSpawn = prefab;
        PlayerHealth playerRef = Object.FindFirstObjectByType<PlayerHealth>();
        bool hasChaos = (playerRef != null && playerRef.hasChaosSymphony);

        if (enemyPoolConfig != null)
        {
            EnemySpawnSetup setup = enemyPoolConfig.GetMutationSetup(prefab);
            if (setup != null && setup.variantPrefab != null)
            {
                float mutChance = setup.mutationChance;
                if (hasChaos)
                {
                    mutChance = Mathf.Min(0.85f, mutChance + 0.35f); // +35% de chance de virar variante elite
                }

                if (UnityEngine.Random.value <= mutChance)
                {
                    prefabToSpawn = setup.variantPrefab;
                    if (showSpawnLog)
                        Debug.Log($"[ROOM {roomIndex}] MUTACAO! {prefab.name} → {setup.variantPrefab.name} ({mutChance * 100:F0}% de chance)");
                }
            }
        }

        // Se o mob não possui variante configurada, com a Sinfonia do Caos ele tem 30% de chance de spawnar como Elite Procedural
        bool forceProceduralElite = false;
        if (hasChaos && prefabToSpawn == prefab && !prefab.name.ToLower().Contains("boss") && !prefab.name.ToLower().Contains("geobionte"))
        {
            if (UnityEngine.Random.value <= 0.30f)
            {
                forceProceduralElite = true;
            }
        }
        // ────────────────────────────────────────────────────────────────────

        // Escolhe uma área aleatória da lista para este inimigo
        BoxCollider chosenArea = spawnAreas[Random.Range(0, spawnAreas.Count)];
        Vector3 spawnPos = GetRandomPositionInArea(chosenArea);

        // Spawn indicator visual
        GameObject indicator = null;
        if (spawnIndicatorPrefab != null)
        {
            Vector3 indicatorPos = new Vector3(spawnPos.x, spawnPos.y + 0.05f, spawnPos.z);
            indicator = Instantiate(spawnIndicatorPrefab, indicatorPos, Quaternion.identity);
        }

        yield return new WaitForSeconds(spawnDelay);

        if (indicator != null) Destroy(indicator);

        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPos, prefabToSpawn.transform.rotation);

        // Garante que o inimigo não caia do mapa (contenção pelo NavMesh global)
        if (newEnemy.GetComponent<NavMeshBoundaryConstraint>() == null)
            newEnemy.AddComponent<NavMeshBoundaryConstraint>();

        // Integração especial: passa os limites de área para a MagicStone (usa a primeira área como referência)
        MagicStone_AI magicStone = newEnemy.GetComponent<MagicStone_AI>();
        if (magicStone != null && spawnAreas != null && spawnAreas.Count > 0 && spawnAreas[0] != null)
            magicStone.SetRoomBounds(spawnAreas[0]);

        bool isInitialElite = (prefabToSpawn != prefab) || 
                              (newEnemy.GetComponent<EliteVariantConfig>() != null) || 
                              newEnemy.name.ToLower().Contains("elite") ||
                              forceProceduralElite;

        if (forceProceduralElite)
        {
            if (newEnemy.GetComponent<EliteVariantConfig>() == null)
            {
                EliteVariantConfig aura = newEnemy.AddComponent<EliteVariantConfig>();
                aura.variantName = newEnemy.name.Replace("(Clone)", "").Trim() + " Elite";
                aura.useProceduralAura = true;
                aura.auraColor = new Color(1f, 0.45f, 0f, 1f);
                aura.auraRadius = 0.85f;
                aura.auraWidth = 0.09f;
            }
            newEnemy.transform.localScale *= 1.25f;
        }

        DummyHealth dh = newEnemy.GetComponent<DummyHealth>() ?? newEnemy.GetComponentInChildren<DummyHealth>();
        if (dh != null)
        {
            dh.originalPrefab = prefab;
            dh.owningRoom = this;
            if (isInitialElite)
            {
                dh.isElite = true;
                if (forceProceduralElite)
                {
                    dh.maxHealth = Mathf.RoundToInt(dh.maxHealth * 1.6f);
                    dh.SetHealth(dh.maxHealth);
                }
            }
        }

        ShardSwarmHealth ssh = newEnemy.GetComponent<ShardSwarmHealth>() ?? newEnemy.GetComponentInChildren<ShardSwarmHealth>();
        if (ssh != null)
        {
            ssh.originalPrefab = prefab;
            ssh.owningRoom = this;
            if (isInitialElite)
            {
                ssh.isElite = true;
                if (forceProceduralElite)
                {
                    ssh.maxHealth = Mathf.RoundToInt(ssh.maxHealth * 1.6f);
                    ssh.SetHealth(ssh.maxHealth);
                }
            }
        }

        if (isInitialElite)
        {
            EnemyDrops drops = newEnemy.GetComponent<EnemyDrops>() ?? newEnemy.GetComponentInChildren<EnemyDrops>();
            if (drops != null) drops.isEliteDrops = true;
        }

        activeEnemies.Add(newEnemy);
        enemiesPendingSpawn--;
    }

    /// <summary>
    /// Corrotina disparada quando um inimigo morre e rola a chance de Renascer como Elite.
    /// Mantém enemiesPendingSpawn ativo para evitar que as portas destranquem prematuramente.
    /// </summary>
    public IEnumerator RebirthAsEliteRoutine(Vector3 spawnPos, Quaternion spawnRot, GameObject sourcePrefabOrInstance, string originalName)
    {
        // 1. Spawna indicador visual no chão durante o breve delay
        GameObject indicator = null;
        if (spawnIndicatorPrefab != null)
        {
            Vector3 indicatorPos = new Vector3(spawnPos.x, spawnPos.y + 0.05f, spawnPos.z);
            indicator = Instantiate(spawnIndicatorPrefab, indicatorPos, Quaternion.identity);
        }

        yield return new WaitForSeconds(0.6f);

        if (indicator != null) Destroy(indicator);

        // 2. Determina o prefab ou cria a variante Elite
        GameObject prefabToSpawn = null;
        EnemyPoolConfig pool = enemyPoolConfig != null ? enemyPoolConfig : Resources.Load<EnemyPoolConfig>("DefaultEnemyPool");
        string cleanName = originalName.Replace("(Clone)", "").Trim();

        if (pool != null)
        {
            EnemySpawnSetup setup = pool.GetMutationSetup(sourcePrefabOrInstance);
            if (setup != null && setup.variantPrefab != null)
            {
                prefabToSpawn = setup.variantPrefab;
            }
            else
            {
                foreach (var s in pool.mutationSetups)
                {
                    if (s != null && s.basePrefab != null && s.variantPrefab != null)
                    {
                        if (s.basePrefab.name.Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            prefabToSpawn = s.variantPrefab;
                            break;
                        }
                    }
                }
            }

            if (prefabToSpawn == null && sourcePrefabOrInstance != null)
            {
                foreach (var p in pool.GetAllUniquePrefabs())
                {
                    if (p != null && p.name.Equals(cleanName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        prefabToSpawn = p;
                        break;
                    }
                }
            }
        }

        if (prefabToSpawn == null)
        {
            if (cleanName.ToLower().Contains("spider"))
                prefabToSpawn = Resources.Load<GameObject>("Spider_Elite") ?? (pool != null ? pool.mutationSetups.Find(m => m.variantPrefab != null && m.variantPrefab.name.Contains("Spider"))?.variantPrefab : null);
            else if (cleanName.ToLower().Contains("goblin"))
                prefabToSpawn = Resources.Load<GameObject>("Goblin_Elite") ?? (pool != null ? pool.mutationSetups.Find(m => m.variantPrefab != null && m.variantPrefab.name.Contains("Goblin"))?.variantPrefab : null);
            else if (cleanName.ToLower().Contains("magicstone"))
                prefabToSpawn = Resources.Load<GameObject>("MagicStone_Elite") ?? (pool != null ? pool.mutationSetups.Find(m => m.variantPrefab != null && m.variantPrefab.name.Contains("MagicStone"))?.variantPrefab : null);
        }

        if (prefabToSpawn == null)
        {
            prefabToSpawn = sourcePrefabOrInstance;
        }

        if (prefabToSpawn == null)
        {
            enemiesPendingSpawn = Mathf.Max(0, enemiesPendingSpawn - 1);
            yield break;
        }

        // 3. Instancia o Reborn Elite
        GameObject rebornEnemy = Instantiate(prefabToSpawn, spawnPos, spawnRot);
        rebornEnemy.name = cleanName + "_RebornElite";

        if (rebornEnemy.GetComponent<NavMeshBoundaryConstraint>() == null)
            rebornEnemy.AddComponent<NavMeshBoundaryConstraint>();

        bool hasDedicatedVariant = (sourcePrefabOrInstance != null &&
                                    sourcePrefabOrInstance.GetComponent<EliteVariantConfig>() != null &&
                                    !sourcePrefabOrInstance.name.Contains("_RebornElite"));

        // 4. Configura propriedades de Elite no DummyHealth
        DummyHealth dh = rebornEnemy.GetComponent<DummyHealth>() ?? rebornEnemy.GetComponentInChildren<DummyHealth>();
        if (dh != null)
        {
            dh.isElite = true;
            dh.hasRebornAsElite = true;
            dh.owningRoom = this;

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
            ssh.owningRoom = this;

            if (!hasDedicatedVariant)
            {
                ssh.maxHealth = Mathf.RoundToInt(ssh.maxHealth * 1.6f);
            }
            ssh.SetHealth(ssh.maxHealth);
        }

        // Aura procedural se não tiver
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

        MagicStone_AI magicStone = rebornEnemy.GetComponent<MagicStone_AI>();
        if (magicStone != null && spawnAreas != null && spawnAreas.Count > 0 && spawnAreas[0] != null)
            magicStone.SetRoomBounds(spawnAreas[0]);

        activeEnemies.Add(rebornEnemy);
        enemiesPendingSpawn = Mathf.Max(0, enemiesPendingSpawn - 1);

        Debug.Log($"[ELITE REBIRTH] ★ {rebornEnemy.name} renasceu como Elite em {spawnPos}!");
    }


    public void NotifyEnemyDefeated(GameObject enemy)
    {
        if (enemy != null && activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            CheckWaveStatus();
        }
    }

    void CheckWaveStatus()
    {
        // Remove referências de inimigos destruídos, inativos, fora do mapa ou desativados/fugindo (como Geobionte)
        activeEnemies.RemoveAll(e => {
            if (e == null || !e.activeInHierarchy || e.transform.position.y < -15f) return true;
            DummyHealth dh = e.GetComponent<DummyHealth>() ?? e.GetComponentInChildren<DummyHealth>();
            if (dh != null && dh.CurrentHealth <= 0) return true;
            ShardSwarmHealth ssh = e.GetComponent<ShardSwarmHealth>() ?? e.GetComponentInChildren<ShardSwarmHealth>();
            if (ssh != null && ssh.CurrentHealth <= 0) return true;
            Geobionte_AI geo = e.GetComponent<Geobionte_AI>() ?? e.GetComponentInChildren<Geobionte_AI>() ?? e.GetComponentInParent<Geobionte_AI>();
            if (geo != null && geo.IsDefeatedOrFleeing) return true;
            return false;
        });

        if (activeEnemies.Count == 0 && enemiesPendingSpawn == 0)
        {
            if (currentWave < totalWavesThisRoom)
            {
                combatActive = false;
                if (showSpawnLog) Debug.Log($"[ROOM {roomIndex}] Onda {currentWave} vencida! Próxima em {timeBetweenWaves}s...");
                Invoke(nameof(StartNextWaveDelayed), timeBetweenWaves);
            }
            else
            {
                if (showSpawnLog) Debug.Log($"[ROOM {roomIndex}] SALA LIMPA! Destrancando portas.");
                UnlockDoors();
                this.enabled = false;
            }
        }
    }

    void StartNextWaveDelayed()
    {
        SpawnNextWave();
        combatActive = true;
    }

    // =====================================================
    // PORTAS
    // =====================================================

    void LockDoors()
    {
        doorsAreLocked = true;
        foreach (GameObject d in doors) if (d) d.SetActive(true);
    }

    void UnlockDoors()
    {
        isCleared = true;
        doorsAreLocked = false;
        foreach (GameObject d in doors) if (d) d.SetActive(false);
        OnRoomCleared?.Invoke(this);
    }

    // =====================================================
    // HELPERS
    // =====================================================

    /// <summary>
    /// Retorna a altura Y do chão da sala no espaço de mundo.
    /// Dá prioridade aos ConnectionPoints (Entrada/Saída), que estão sempre no nível do piso.
    /// Evita depender do transform.position do root, que pode ter offset elevado no modelo.
    /// </summary>
    public float GetRoomFloorWorldY()
    {
        ConnectionPoint[] cps = GetComponentsInChildren<ConnectionPoint>();
        if (cps != null)
        {
            foreach (var cp in cps)
            {
                if (cp != null && cp.pointType == ConnectionPoint.PointType.Entrada)
                {
                    return cp.GetFloorWorldY();
                }
            }
            if (cps.Length > 0 && cps[0] != null)
            {
                return cps[0].GetFloorWorldY();
            }
        }

        if (LevelGenerator.Instance != null)
        {
            return LevelGenerator.Instance.roomFloorY;
        }

        return transform.position.y;
    }

    /// <summary>
    /// Calcula os limites tridimensionais (Bounds) da sala para uso de spawn.
    /// Usa SpawnAreas configuradas, ou renderers combinados, ou ConnectionPoints como fallback.
    /// </summary>
    public Bounds GetRoomBounds()
    {
        // 1. Tenta usar as SpawnAreas configuradas
        if (spawnAreas != null && spawnAreas.Count > 0)
        {
            Bounds b = new Bounds();
            bool hasFirst = false;
            foreach (var area in spawnAreas)
            {
                if (area == null) continue;
                if (!hasFirst) { b = area.bounds; hasFirst = true; }
                else { b.Encapsulate(area.bounds); }
            }
            if (hasFirst && b.size.sqrMagnitude > 1f) return b;
        }

        // 2. Se não houver spawnAreas, calcula bounds combinados de todos os renderers da sala
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = new Bounds();
            bool hasFirst = false;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!hasFirst) { b = r.bounds; hasFirst = true; }
                else { b.Encapsulate(r.bounds); }
            }
            if (hasFirst && b.size.sqrMagnitude > 1f) return b;
        }

        // 3. Fallback usando ConnectionPoints
        ConnectionPoint[] cps = GetComponentsInChildren<ConnectionPoint>();
        if (cps != null && cps.Length > 0)
        {
            Bounds b = new Bounds(cps[0].transform.position, Vector3.one * 10f);
            for (int i = 1; i < cps.Length; i++)
            {
                if (cps[i] != null)
                    b.Encapsulate(cps[i].transform.position);
            }
            return b;
        }

        return new Bounds(new Vector3(transform.position.x, GetRoomFloorWorldY(), transform.position.z), Vector3.one * 10f);
    }

    /// <summary>
    /// Garante que a lista de spawnAreas não tenha referências nulas e, caso esteja vazia,
    /// detecta colliders filhos ou cria dinamicamente uma AutoSpawnArea no nível correto do chão.
    /// </summary>
    public void EnsureValidSpawnAreas()
    {
        if (isSafeRoom || roomCategory != RoomCategory.Combat || GetComponent<AnomalyRoomController>() != null)
        {
            return;
        }

        if (spawnAreas != null)
        {
            spawnAreas.RemoveAll(a => a == null);
        }
        else
        {
            spawnAreas = new List<BoxCollider>();
        }

        if (spawnAreas.Count == 0)
        {
            // 1. Tenta encontrar BoxCollider existente nos filhos que se chame SpawnArea ou contenha Spawn
            BoxCollider[] allBoxes = GetComponentsInChildren<BoxCollider>(true);
            foreach (var box in allBoxes)
            {
                if (box == null) continue;
                string n = box.gameObject.name.ToLower();
                if (n.Contains("spawnarea") || n.Contains("spawn_area") || n.Contains("spawn area") || n == "spawn")
                {
                    spawnAreas.Add(box);
                    break;
                }
            }

            // 2. Se ainda não encontrou, cria dinamicamente uma AutoSpawnArea baseada nos limites reais da sala
            if (spawnAreas.Count == 0)
            {
                Bounds roomBounds = GetRoomBounds();
                float floorY = GetRoomFloorWorldY();

                GameObject autoSpawnGO = new GameObject("AutoSpawnArea");
                autoSpawnGO.transform.SetParent(transform, true);
                autoSpawnGO.transform.position = new Vector3(roomBounds.center.x, floorY + 1.5f, roomBounds.center.z);

                BoxCollider autoBox = autoSpawnGO.AddComponent<BoxCollider>();
                autoBox.isTrigger = true;

                float sx = Mathf.Clamp(roomBounds.size.x * 0.65f, 6f, 30f);
                float sz = Mathf.Clamp(roomBounds.size.z * 0.65f, 6f, 30f);
                autoBox.size = new Vector3(sx, 3f, sz);

                spawnAreas.Add(autoBox);
                Debug.Log($"[RoomController] AutoSpawnArea gerada dinamicamente para a sala '{gameObject.name}' em Y={floorY:F2}");
            }
        }
    }

    Vector3 GetRandomPositionInArea(BoxCollider area)
    {
        if (area == null)
        {
            EnsureValidSpawnAreas();
            if (spawnAreas != null && spawnAreas.Count > 0 && spawnAreas[0] != null)
                area = spawnAreas[0];
        }

        float floorY = GetRoomFloorWorldY();
        Bounds bounds = (area != null) ? area.bounds : GetRoomBounds();
        Vector3 size = bounds.size;

        // Inset das margens (0.70f = 30% de recuo das bordas) para não spawnar em cristais e paredes do perímetro
        float halfX = (size.x < 2f ? 6f : size.x * 0.5f) * 0.70f;
        float halfZ = (size.z < 2f ? 6f : size.z * 0.5f) * 0.70f;

        // Tenta até 15 vezes encontrar uma posição no centro caminhável do NavMesh
        for (int i = 0; i < 15; i++)
        {
            float x = Random.Range(bounds.center.x - halfX, bounds.center.x + halfX);
            float z = Random.Range(bounds.center.z - halfZ, bounds.center.z + halfZ);
            Vector3 candidatePos = new Vector3(x, floorY + 0.5f, z);

            // 1. Tenta NavMesh e garante distância mínima das paredes/bordas
            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 6f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (Mathf.Abs(navHit.position.y - floorY) <= 4.0f)
                {
                    // Verifica se não está colado na borda do NavMesh (mínimo 1.2m da parede)
                    if (UnityEngine.AI.NavMesh.FindClosestEdge(navHit.position, out UnityEngine.AI.NavMeshHit edgeHit, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        if (edgeHit.distance < 1.2f) continue;
                    }

                    // Raycast de confirmação para garantir que não caiu no topo de um cristal decorativo
                    Vector3 checkRayStart = navHit.position + Vector3.up * 4f;
                    if (Physics.Raycast(checkRayStart, Vector3.down, out RaycastHit surfaceHit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        string nameLower = surfaceHit.collider.gameObject.name.ToLower();
                        if (nameLower.Contains("crystal") || nameLower.Contains("prop") || surfaceHit.collider.CompareTag("Prop"))
                        {
                            continue; // Rejeita e sorteia outro ponto se for no topo de um cristal
                        }
                    }

                    return navHit.position;
                }
            }

            // 2. Raycast de cima para baixo como fallback secundário
            Vector3 rayStart = new Vector3(x, floorY + 3.5f, z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger && !hit.collider.CompareTag("Player"))
                {
                    string hitName = hit.collider.gameObject.name.ToLower();
                    if (!hitName.Contains("crystal") && !hitName.Contains("prop") && !hit.collider.CompareTag("Prop"))
                    {
                        return hit.point;
                    }
                }
            }
        }

        // Fallback final seguro: amostragem no NavMesh no centro da área ou na posição do chão da sala
        Vector3 centerCandidate = new Vector3(bounds.center.x, floorY + 0.5f, bounds.center.z);
        if (UnityEngine.AI.NavMesh.SamplePosition(centerCandidate, out UnityEngine.AI.NavMeshHit centerHit, 15f, UnityEngine.AI.NavMesh.AllAreas))
        {
            if (Mathf.Abs(centerHit.position.y - floorY) <= 4.0f)
                return centerHit.position;
        }

        return new Vector3(bounds.center.x, floorY + spawnHeightOffset, bounds.center.z);
    }

    private Vector3 GetRandomPositionForCollectible(BoxCollider box, bool isNearWall, bool isHigh)
    {
        if (box == null)
        {
            EnsureValidSpawnAreas();
            if (spawnAreas != null && spawnAreas.Count > 0 && spawnAreas[0] != null)
                box = spawnAreas[0];
        }

        float floorY = GetRoomFloorWorldY();
        Bounds bounds = (box != null) ? box.bounds : GetRoomBounds();
        Vector3 size = bounds.size;

        // Se a área for muito pequena (erro de escala no prefab), expande para um raio útil de 7 metros
        float halfX = size.x < 2f ? 7f : size.x * 0.5f;
        float halfZ = size.z < 2f ? 7f : size.z * 0.5f;

        float x = Random.Range(bounds.center.x - halfX, bounds.center.x + halfX);
        float z = Random.Range(bounds.center.z - halfZ, bounds.center.z + halfZ);

        if (isNearWall)
        {
            float distToMinX = Mathf.Abs(x - (bounds.center.x - halfX));
            float distToMaxX = Mathf.Abs((bounds.center.x + halfX) - x);
            float distToMinZ = Mathf.Abs(z - (bounds.center.z - halfZ));
            float distToMaxZ = Mathf.Abs((bounds.center.z + halfZ) - z);

            float minDist = Mathf.Min(Mathf.Min(distToMinX, distToMaxX), Mathf.Min(distToMinZ, distToMaxZ));

            float margin = 0.05f;
            float sizeX = halfX * 2f;
            float sizeZ = halfZ * 2f;

            if (minDist == distToMinX) x = (bounds.center.x - halfX) + (sizeX * margin);
            else if (minDist == distToMaxX) x = (bounds.center.x + halfX) - (sizeX * margin);
            else if (minDist == distToMinZ) z = (bounds.center.z - halfZ) + (sizeZ * margin);
            else if (minDist == distToMaxZ) z = (bounds.center.z + halfZ) - (sizeZ * margin);
        }

        // Snap to ground using NavMesh or Raycast downwards
        float y = floorY;
        Vector3 candidatePos = new Vector3(x, floorY + 0.5f, z);
        bool foundGround = false;

        // 1. Tenta NavMesh primeiro (próximo do nível do chão da sala)
        if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 6f, UnityEngine.AI.NavMesh.AllAreas))
        {
            if (Mathf.Abs(navHit.position.y - floorY) <= 4.0f)
            {
                y = navHit.position.y;
                foundGround = true;
            }
        }

        // 2. Se NavMesh falhar (ex: sobre a água do LakeRoom), tenta Raycast de cima para baixo
        if (!foundGround)
        {
            Vector3 rayStart = new Vector3(x, floorY + 3.5f, z);
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 8.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float bestDist = float.MaxValue;
            RaycastHit bestHit = default;
            bool hitFound = false;

            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.CompareTag("Player") || h.collider.CompareTag("Enemy")) continue;

                string hitName = h.collider.gameObject.name.ToLower();
                if (hitName.Contains("crystal") || hitName.Contains("prop") || h.collider.CompareTag("Prop"))
                    continue;

                // Aceita colisor se não for trigger OU se for a superfície da água (Water)
                if (h.collider.isTrigger && !hitName.Contains("water"))
                    continue;

                float dist = Mathf.Abs(h.point.y - floorY);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestHit = h;
                    hitFound = true;
                }
            }

            if (hitFound)
            {
                y = bestHit.point.y;
                foundGround = true;
            }
        }

        // 3. Fallback final seguro: se nada encontrou colisão, usa o chão de referência da sala
        if (!foundGround)
        {
            y = floorY;
        }

        if (isHigh)
        {
            y += 1.8f; // Floating above the ground
        }
        else
        {
            y += 0.05f; // Slightly above ground to prevent clipping
        }

        return new Vector3(x, y, z);
    }

    private void EnsureDefaultCollectibleEntries()
    {
        // 1. FAUNA
        if (faunaSpawnEntries == null || faunaSpawnEntries.Count == 0)
        {
            faunaSpawnEntries = new List<CollectibleSpawnEntry>()
            {
                new CollectibleSpawnEntry() { itemName = "Vagalume Cristalizado", prefab = Resources.Load<GameObject>("SpawnItems/Fly") ?? Resources.Load<GameObject>("Fly"), itemData = Resources.Load<ItemData>("ItemData/vagalume_cristalizado"), weight = 1.0f },
                new CollectibleSpawnEntry() { itemName = "Caracol Geodo (Quebradiço)", prefab = Resources.Load<GameObject>("SpawnItems/Quebradiço") ?? Resources.Load<GameObject>("SpawnItems/CarangueijoQuebradisso"), itemData = Resources.Load<ItemData>("ItemData/caracol_geodo"), weight = 1.0f },
                new CollectibleSpawnEntry() { itemName = "Ovos de Cristal Crawler", prefab = Resources.Load<GameObject>("SpawnItems/Ovocristal") ?? Resources.Load<GameObject>("SpawnItems/Ovocristal.prefab"), itemData = Resources.Load<ItemData>("ItemData/ovo_cristal_crawler"), weight = 1.0f }
            };
        }
        foreach (var entry in faunaSpawnEntries)
        {
            if (entry != null && entry.itemData == null && !string.IsNullOrEmpty(entry.itemName))
            {
                if (entry.itemName.Contains("Vagalume")) entry.itemData = Resources.Load<ItemData>("ItemData/vagalume_cristalizado");
                else if (entry.itemName.Contains("Caracol") || entry.itemName.Contains("Quebradiço")) entry.itemData = Resources.Load<ItemData>("ItemData/caracol_geodo");
                else if (entry.itemName.Contains("Ovos") || entry.itemName.Contains("Crawler")) entry.itemData = Resources.Load<ItemData>("ItemData/ovo_cristal_crawler");
            }
        }

        // 2. FLORA
        if (floraSpawnEntries == null || floraSpawnEntries.Count == 0)
        {
            floraSpawnEntries = new List<CollectibleSpawnEntry>()
            {
                new CollectibleSpawnEntry() { itemName = "Melocactus", prefab = Resources.Load<GameObject>("SpawnItems/Melacotus"), itemData = Resources.Load<ItemData>("ItemData/melocactus"), weight = 1.0f },
                new CollectibleSpawnEntry() { itemName = "Lithos (Lotus)", prefab = Resources.Load<GameObject>("SpawnItems/Lotus"), itemData = Resources.Load<ItemData>("ItemData/lithos"), weight = 1.0f },
                new CollectibleSpawnEntry() { itemName = "Árvore de Vagens (Lanternas)", prefab = Resources.Load<GameObject>("SpawnItems/Lanternas"), itemData = Resources.Load<ItemData>("ItemData/arvore_vagens"), weight = 1.0f }
            };
        }
        foreach (var entry in floraSpawnEntries)
        {
            if (entry != null && entry.itemData == null && !string.IsNullOrEmpty(entry.itemName))
            {
                if (entry.itemName.Contains("Melocactus")) entry.itemData = Resources.Load<ItemData>("ItemData/melocactus");
                else if (entry.itemName.Contains("Lotus") || entry.itemName.Contains("Lithos")) entry.itemData = Resources.Load<ItemData>("ItemData/lithos");
                else if (entry.itemName.Contains("Vagens") || entry.itemName.Contains("Lanternas")) entry.itemData = Resources.Load<ItemData>("ItemData/arvore_vagens");
            }
        }

        // 3. MINÉRIOS
        if (mineralSpawnEntries == null || mineralSpawnEntries.Count == 0)
        {
            mineralSpawnEntries = new List<CollectibleSpawnEntry>()
            {
                new CollectibleSpawnEntry() { itemName = "Cristal (Pó de Cristal)", prefab = Resources.Load<GameObject>("SpawnItems/Cristal"), itemData = Resources.Load<ItemData>("ItemData/po_de_cristal"), weight = 1.0f }
            };
        }
        foreach (var entry in mineralSpawnEntries)
        {
            if (entry != null && entry.itemData == null && !string.IsNullOrEmpty(entry.itemName))
            {
                if (entry.itemName.Contains("Cristal")) entry.itemData = Resources.Load<ItemData>("ItemData/po_de_cristal");
            }
        }

        // 4. FAUNA AQUÁTICA EXCLUSIVA (PEIXE LAMPIÃO / LAKE ROOM)
        if (waterFaunaEntry == null || waterFaunaEntry.itemData == null)
        {
            waterFaunaEntry = new CollectibleSpawnEntry()
            {
                itemName = "Peixe Lampião",
                prefab = Resources.Load<GameObject>("SpawnItems/Fly") ?? Resources.Load<GameObject>("Fly"),
                itemData = Resources.Load<ItemData>("ItemData/peixe_lagoa"),
                weight = 1.0f
            };
        }
    }

    private CollectibleSpawnEntry GetWeightedRandomEntry(List<CollectibleSpawnEntry> entries)
    {
        if (entries == null || entries.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var entry in entries)
        {
            if (entry != null && entry.weight > 0f)
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0f) return entries[0];

        float randValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var entry in entries)
        {
            if (entry == null || entry.weight <= 0f) continue;
            currentSum += entry.weight;
            if (randValue <= currentSum)
            {
                return entry;
            }
        }

        return entries[0];
    }

    private Vector3 GetSeparatedRandomPositionForCollectible(List<Vector3> existingPositions, bool isNearWall, bool isHigh, float minDistance = 7f)
    {
        EnsureValidSpawnAreas();
        if (spawnAreas == null || spawnAreas.Count == 0)
        {
            float floorY = GetRoomFloorWorldY();
            return new Vector3(transform.position.x, floorY + (isHigh ? 1.8f : 0.05f), transform.position.z);
        }

        Vector3 bestCandidate = Vector3.zero;
        float maxMinDist = -1f;

        for (int i = 0; i < 20; i++)
        {
            BoxCollider area = spawnAreas[Random.Range(0, spawnAreas.Count)];
            if (area == null) continue;

            Vector3 candidate = GetRandomPositionForCollectible(area, isNearWall, isHigh);

            if (existingPositions == null || existingPositions.Count == 0)
            {
                return candidate;
            }

            float closest = float.MaxValue;
            foreach (var existing in existingPositions)
            {
                float d = Vector3.Distance(candidate, existing);
                if (d < closest) closest = d;
            }

            if (closest >= minDistance)
            {
                return candidate;
            }

            if (closest > maxMinDist)
            {
                maxMinDist = closest;
                bestCandidate = candidate;
            }
        }

        return bestCandidate != Vector3.zero ? bestCandidate : GetRandomPositionForCollectible(spawnAreas[0], isNearWall, isHigh);
    }

    private void SpawnRoomCollectibles()
    {
        EnsureValidSpawnAreas();
        if (spawnAreas == null || spawnAreas.Count == 0) return;

        EnsureDefaultCollectibleEntries();

        // Detecta se a sala é uma LakeRoom (ou contém corpo d'água)
        bool isLakeRoom = gameObject.name.ToLower().Contains("lake") || 
                          transform.Find("Water") != null || 
                          GetComponentInChildren<Collider>()?.gameObject.name.ToLower().Contains("water") == true;

        List<Vector3> spawnedPositions = new List<Vector3>();

        // 1. Spawn Minerals (No chão)
        CollectibleSpawnEntry mineralEntry = GetWeightedRandomEntry(mineralSpawnEntries);
        if (mineralEntry != null && mineralEntry.prefab != null)
        {
            Vector3 pos = GetSeparatedRandomPositionForCollectible(spawnedPositions, false, false, 7f);
            spawnedPositions.Add(pos);
            GameObject obj = Instantiate(mineralEntry.prefab, pos, Quaternion.identity);
            
            ItemPickup pickup = obj.GetComponent<ItemPickup>();
            if (pickup == null) pickup = obj.AddComponent<ItemPickup>();

            Interactable interactable = obj.GetComponent<Interactable>();
            if (interactable == null) interactable = obj.AddComponent<Interactable>();

            if (mineralEntry.itemData != null)
            {
                interactable.itemData = mineralEntry.itemData;
                interactable.objetoNome = mineralEntry.itemData.itemName;
            }
            
            pickup.InitializeItem("Minerals");

            if (obj.GetComponent<OreNode>() == null)
            {
                obj.AddComponent<OreNode>();
            }
        }

        // 2. Spawn Fauna (Peixe Lampião na água se LakeRoom; Fauna terrestre/aérea sorteada se sala comum)
        CollectibleSpawnEntry faunaEntry = isLakeRoom && waterFaunaEntry != null && waterFaunaEntry.itemData != null ? 
                                             waterFaunaEntry : GetWeightedRandomEntry(faunaSpawnEntries);

        if (faunaEntry != null)
        {
            GameObject faunaPrefabToInstantiate = faunaEntry.prefab;
            if (faunaPrefabToInstantiate == null)
            {
                faunaPrefabToInstantiate = Resources.Load<GameObject>("SpawnItems/Fly") ?? Resources.Load<GameObject>("Fly");
            }

            if (faunaPrefabToInstantiate != null)
            {
                bool isFly = (faunaEntry.itemName.ToLower().Contains("vagalume") || faunaPrefabToInstantiate.name.ToLower().Contains("fly")) && !faunaEntry.itemName.ToLower().Contains("peixe");
                Vector3 pos = GetSeparatedRandomPositionForCollectible(spawnedPositions, false, isFly, 7f);
                spawnedPositions.Add(pos);
                GameObject obj = Instantiate(faunaPrefabToInstantiate, pos, Quaternion.identity);
                
                ItemPickup pickup = obj.GetComponent<ItemPickup>();
                if (pickup == null) pickup = obj.AddComponent<ItemPickup>();

                Interactable interactable = obj.GetComponent<Interactable>();
                if (interactable == null) interactable = obj.AddComponent<Interactable>();

                if (faunaEntry.itemData != null)
                {
                    interactable.itemData = faunaEntry.itemData;
                    interactable.objetoNome = faunaEntry.itemData.itemName;
                }

                pickup.InitializeItem("Fauna");
            }
        }

        // 3. Spawn Flora (No chão)
        CollectibleSpawnEntry floraEntry = GetWeightedRandomEntry(floraSpawnEntries);
        if (floraEntry != null && floraEntry.prefab != null)
        {
            Vector3 pos = GetSeparatedRandomPositionForCollectible(spawnedPositions, false, false, 7f);
            spawnedPositions.Add(pos);
            GameObject obj = Instantiate(floraEntry.prefab, pos, Quaternion.identity);
            
            ItemPickup pickup = obj.GetComponent<ItemPickup>();
            if (pickup == null) pickup = obj.AddComponent<ItemPickup>();

            Interactable interactable = obj.GetComponent<Interactable>();
            if (interactable == null) interactable = obj.AddComponent<Interactable>();

            if (floraEntry.itemData != null)
            {
                interactable.itemData = floraEntry.itemData;
                interactable.objetoNome = floraEntry.itemData.itemName;
            }

            pickup.InitializeItem("Flora");
        }
    }

    GameObject GetRandom(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }
}

/// <summary>
/// Estrutura para configurar probabilidades ponderadas de spawn de coletáveis da Bolsa Sintética.
/// </summary>
[System.Serializable]
public class CollectibleSpawnEntry
{
    public string itemName;
    public GameObject prefab;
    public ItemData itemData;
    [Range(0.1f, 100f)] public float weight = 1.0f;
}