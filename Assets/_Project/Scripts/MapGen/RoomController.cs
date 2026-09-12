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
    public bool isCleared { get; private set; } = false;

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
    public float timeBetweenWaves = 2f;
    [Tooltip("Número mínimo de inimigos garantidos por onda (Mob Menor preenchem o restante).")]
    [Range(1, 20)] public int minEnemiesPerWave = 6;
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
    public float spawnDelay = 1.5f;
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
    private int   enemiesPendingSpawn = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool  hasTriggered  = false;
    private bool  combatActive  = false;

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
        EnsureDefaultCollectibleEntries();

        if (enemyPoolConfig == null)
        {
            enemyPoolConfig = Resources.Load<EnemyPoolConfig>("DefaultEnemyPool");
        }
    }

    void Start()
    {
        if (gameObject.name.ToLower().Contains("3way") || gameObject.name.ToLower().Contains("treeway"))
        {
            roomCategory = RoomCategory.Contemplation;
        }

        if (isSafeRoom || roomCategory != RoomCategory.Combat) return;
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
        // Sem este delay, a barreira fecha enquanto o player ainda está no vão da entrada.
        yield return new WaitForSeconds(0.8f);

        LockDoors();
        if (showSpawnLog)
            Debug.Log($"[ROOM {roomIndex}] Portas trancadas. {totalWavesThisRoom} ondas programadas.");
        yield return new WaitForSeconds(1f);

        SpawnNextWave();
        combatActive = true;
    }

    void SpawnNextWave()
    {
        currentWave++;

        // Nas salas iniciais (1 e 2), limita a 1 onda leve para introdução suave
        if (roomIndex <= 2)
        {
            totalWavesThisRoom = 1;
        }
        else if (roomIndex <= 4)
        {
            totalWavesThisRoom = Mathf.Min(totalWavesThisRoom, 2);
        }

        // O budget total da sala é dividido igualmente entre as ondas.
        int totalBudget = RunManager.instance != null
            ? RunManager.instance.GetSpawnBudget(roomIndex)
            : Mathf.RoundToInt(3 + 0.9f * roomIndex);
        int waveBudget = Mathf.Max(3, Mathf.RoundToInt(totalBudget / (float)totalWavesThisRoom));

        int effectiveMinEnemies = Mathf.Clamp(Mathf.RoundToInt(2 + (roomIndex * 0.25f)), 2, minEnemiesPerWave);

        if (showSpawnLog)
            Debug.Log($"[ROOM {roomIndex}] ONDA {currentWave}/{totalWavesThisRoom} | Budget: {waveBudget} pts | Mín. inimigos: {effectiveMinEnemies}");

        // Gera a lista de inimigos desta onda pelo algoritmo de pontos
        List<GameObject> waveEnemies = BuildWaveFromPoints(waveBudget, effectiveMinEnemies);

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
                if (magicStone != null && targetRoom.spawnAreas.Count > 0)
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
    List<GameObject> BuildWaveFromPoints(int waveBudget, int minEnemies)
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

        int maxMobPoints = Mathf.FloorToInt(waveBudget * mobCapFraction);
        int remainingPts = waveBudget;
        int mobPointsUsed = 0;

        List<GameObject> curMobMenor = GetMobMenorPool();
        List<GameObject> curAtirador = GetAtiradorPool();
        List<GameObject> curTanque   = GetTanquePool();
        List<GameObject> curElite    = GetElitePool();
        List<GameObject> curSuporte  = GetSuportePool();

        List<GameObject> result = new List<GameObject>();

        // Suporte / Base: Apenas da Sala 2 em diante (NUNCA na Sala 1!), no máximo 1 por sala
        if (roomIndex >= 2 && currentWave == 1 && curSuporte.Count > 0 && suporteSpawnedTotal < 1)
        {
            result.Add(GetRandom(curSuporte));
            suporteSpawnedTotal++;
            remainingPts -= 3;
        }

        bool addedSomething = true;
        while (remainingPts > 0 && addedSomething)
        {
            addedSomething = false;
            List<EnemyClass> validOptions = new List<EnemyClass>();

            PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
            bool chaosMode = player != null && player.hasChaosSymphony;
            int eliteMax = chaosMode ? 3 : 1;
            int tanqueMax = chaosMode ? 8 : 4;
            int minRoomElite = chaosMode ? 2 : 5;
            int minRoomTanque = chaosMode ? 1 : 3;

            // Elite: máx. 1 POR SALA, apenas da Sala 5 em diante (No chaos mode, mais elites)
            if (roomIndex >= minRoomElite && curElite.Count > 0 && remainingPts >= 10 && eliteSpawnedTotal < eliteMax)
                validOptions.Add(EnemyClass.Elite);

            // Tanque: máx. 4 POR SALA, apenas da Sala 3 em diante (No chaos mode, mais tanques)
            if (roomIndex >= minRoomTanque && curTanque.Count > 0 && remainingPts >= 4 && tanqueSpawnedTotal < tanqueMax)
                validOptions.Add(EnemyClass.Tanque);

            // Atirador: par (4 pts), apenas da Sala 2 em diante
            if (roomIndex >= 2 && curAtirador.Count > 0 && remainingPts >= 4)
                validOptions.Add(EnemyClass.Atirador);

            // Mob Menor: sempre permitido para preencher a onda
            if (curMobMenor.Count > 0 && remainingPts >= 1)
                validOptions.Add(EnemyClass.MobMenor);

            if (validOptions.Count == 0) break;

            // Prioriza Mobs Menores nas escolhas (ponderado) para não entulhar inimigos pesados nas salas iniciais
            EnemyClass chosen = EnemyClass.MobMenor;
            if (validOptions.Count > 1 && Random.value < 0.35f)
            {
                chosen = validOptions[Random.Range(0, validOptions.Count)];
            }
            else if (validOptions.Contains(EnemyClass.MobMenor))
            {
                chosen = EnemyClass.MobMenor;
            }
            else
            {
                chosen = validOptions[Random.Range(0, validOptions.Count)];
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
                    // Par de atiradores (podem ser inimigos diferentes)
                    result.Add(GetRandom(curAtirador));
                    result.Add(GetRandom(curAtirador));
                    remainingPts -= 4;
                    break;

                case EnemyClass.MobMenor:
                    result.Add(GetRandom(curMobMenor));
                    mobPointsUsed++;
                    remainingPts -= 1;
                    break;

                case EnemyClass.Suporte:
                    result.Add(GetRandom(curSuporte));
                    remainingPts -= 3;
                    break;
            }
        }

        // Garante mínimo de inimigos por onda preenchendo com Mob Menor
        if (curMobMenor.Count > 0)
        {
            while (result.Count < minEnemies)
                result.Add(GetRandom(curMobMenor));
        }

        if (showSpawnLog)
            Debug.Log($"[ROOM {roomIndex}] Onda {currentWave}: {result.Count} inimigos gerados (Elite:{eliteSpawnedTotal} Tanque:{tanqueSpawnedTotal} Suporte:{suporteSpawnedTotal})");

        return result;
    }

    IEnumerator SpawnEnemyWithDelay(GameObject prefab)
    {
        if (prefab == null || spawnAreas == null || spawnAreas.Count == 0)
        {
            enemiesPendingSpawn--;
            yield break;
        }

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

        GameObject newEnemy = Instantiate(prefab, spawnPos, prefab.transform.rotation);

        // Garante que o inimigo não caia do mapa (contenção pelo NavMesh global)
        if (newEnemy.GetComponent<NavMeshBoundaryConstraint>() == null)
            newEnemy.AddComponent<NavMeshBoundaryConstraint>();

        // Integração especial: passa os limites de área para a MagicStone (usa a primeira área como referência)
        MagicStone_AI magicStone = newEnemy.GetComponent<MagicStone_AI>();
        if (magicStone != null)
            magicStone.SetRoomBounds(spawnAreas[0]);

        activeEnemies.Add(newEnemy);
        enemiesPendingSpawn--;
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

    Vector3 GetRandomPositionInArea(BoxCollider area)
    {
        if (area == null) return transform.position;
        Bounds bounds = area.bounds;
        Vector3 size = bounds.size;

        // Inset das margens (0.70f = 30% de recuo das bordas) para não spawnar em cristais e paredes do perímetro
        float halfX = (size.x < 2f ? 6f : size.x * 0.5f) * 0.70f;
        float halfZ = (size.z < 2f ? 6f : size.z * 0.5f) * 0.70f;

        // Tenta até 15 vezes encontrar uma posição no centro caminhável do NavMesh
        for (int i = 0; i < 15; i++)
        {
            float x = Random.Range(bounds.center.x - halfX, bounds.center.x + halfX);
            float z = Random.Range(bounds.center.z - halfZ, bounds.center.z + halfZ);
            Vector3 candidatePos = new Vector3(x, bounds.center.y, z);

            // 1. Tenta NavMesh e garante distância mínima das paredes/bordas
            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 4f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Verifica se não está colado na borda do NavMesh (mínimo 1.2m da parede)
                if (UnityEngine.AI.NavMesh.FindClosestEdge(navHit.position, out UnityEngine.AI.NavMeshHit edgeHit, UnityEngine.AI.NavMesh.AllAreas))
                {
                    if (edgeHit.distance < 1.2f) continue;
                }

                // Raycast de confirmação para garantir que não caiu no topo de um cristal decorativo
                Vector3 checkRayStart = navHit.position + Vector3.up * 5f;
                if (Physics.Raycast(checkRayStart, Vector3.down, out RaycastHit surfaceHit, 8f))
                {
                    string nameLower = surfaceHit.collider.gameObject.name.ToLower();
                    if (nameLower.Contains("crystal") || nameLower.Contains("prop") || surfaceHit.collider.CompareTag("Prop"))
                    {
                        continue; // Rejeita e sorteia outro ponto se for no topo de um cristal
                    }
                }

                return navHit.position;
            }

            // 2. Raycast de cima para baixo como fallback secundário
            Vector3 rayStart = new Vector3(x, bounds.center.y + 15f, z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 35f))
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

        // Fallback final seguro: amostragem no NavMesh no centro da área ou na posição da sala
        if (UnityEngine.AI.NavMesh.SamplePosition(bounds.center, out UnityEngine.AI.NavMeshHit centerHit, 20f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return centerHit.position;
        }

        return transform.position + Vector3.up * 0.1f;
    }

    private Vector3 GetRandomPositionForCollectible(BoxCollider box, bool isHigh, bool isNearWall)
    {
        if (box == null) return transform.position;
        
        Bounds bounds = box.bounds;
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
        float y = bounds.center.y;
        Vector3 candidatePos = new Vector3(x, y, z);
        bool foundGround = false;

        // 1. Tenta NavMesh primeiro
        if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out UnityEngine.AI.NavMeshHit navHit, 15f, UnityEngine.AI.NavMesh.AllAreas))
        {
            y = navHit.position.y;
            foundGround = true;
        }

        // 2. Se NavMesh falhar, tenta Raycast de cima para baixo
        if (!foundGround)
        {
            Vector3 rayStart = new Vector3(x, bounds.center.y + 8f, z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f))
            {
                if (!hit.collider.isTrigger)
                {
                    y = hit.point.y;
                    foundGround = true;
                }
            }
        }

        // 3. Fallback final seguro
        if (!foundGround)
        {
            y = bounds.min.y + 0.1f;
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

        // 3. MINÉRIOS
        if (mineralSpawnEntries == null || mineralSpawnEntries.Count == 0)
        {
            mineralSpawnEntries = new List<CollectibleSpawnEntry>()
            {
                new CollectibleSpawnEntry() { itemName = "Cristal (Pó de Cristal)", prefab = Resources.Load<GameObject>("SpawnItems/Cristal"), itemData = Resources.Load<ItemData>("ItemData/po_de_cristal"), weight = 1.0f }
            };
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

    private void SpawnRoomCollectibles()
    {
        if (spawnAreas == null || spawnAreas.Count == 0) return;

        BoxCollider area = spawnAreas[Random.Range(0, spawnAreas.Count)];
        if (area == null) return;

        EnsureDefaultCollectibleEntries();

        // Detecta se a sala é uma LakeRoom (ou contém corpo d'água)
        bool isLakeRoom = gameObject.name.ToLower().Contains("lake") || 
                          transform.Find("Water") != null || 
                          GetComponentInChildren<Collider>()?.gameObject.name.ToLower().Contains("water") == true;

        // 1. Spawn Minerals (No chão)
        CollectibleSpawnEntry mineralEntry = GetWeightedRandomEntry(mineralSpawnEntries);
        if (mineralEntry != null && mineralEntry.prefab != null)
        {
            Vector3 pos = GetRandomPositionForCollectible(area, false, false);
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
                Vector3 pos = GetRandomPositionForCollectible(area, false, false);
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
            Vector3 pos = GetRandomPositionForCollectible(area, false, true);
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