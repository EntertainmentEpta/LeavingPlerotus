using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gerador de nível procedural baseado em ConnectionPoints.
///
/// COMO FUNCIONA:
///   1. Instancia a Safe Room na origem.
///   2. Coleta todos os ConnectionPoints do tipo "Saida" da sala inicial.
///   3. Para cada saída livre, tenta parear com a "Entrada" de um novo prefab compatível.
///   4. Usa AlignRooms para encaixar os dois transforms perfeitamente.
///   5. Repete até atingir maxMainRooms ou esgotar as saídas.
///   6. Saídas restantes viram: ExitRoom, MerchantRoom ou DeadEnd.
///
/// SETUP NO INSPECTOR:
///   • startRoomPrefab    → Sala inicial (Safe Room) com ConnectionPoints configurados.
///   • mainRoomPrefabs    → Lista de salas de combate, orgânicas, Y-shape, etc.
///   • transitionRoomPrefab → Corredor de transição com 1 Entrada + 1 Saida.
///   • merchantRoomPrefab, merchantPrefab, exitRoomPrefab, deadEndPrefab → especiais.
///
/// REGRA DE SETUP DOS PREFABS:
///   Cada prefab DEVE ter exatamente 1 ConnectionPoint(Entrada) e N ConnectionPoints(Saida).
///   O forward de cada ConnectionPoint deve apontar para FORA da sala.
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    // =========================================================
    // INSPECTOR
    // =========================================================

    [Header("Prefabs das Salas")]
    [Tooltip("A sala inicial (Safe Room). Deve ter 1 Entrada (usada como âncora) e N Saídas.")]
    public GameObject startRoomPrefab;

    [Tooltip("Pool de salas de combate, orgânicas, Y-shape, etc. Misture quantas quiser.")]
    public List<GameObject> mainRoomPrefabs;

    [Tooltip("Pool de corredores de ligação entre salas. Devem ter 1 Entrada e 1 Saída.")]
    public List<GameObject> transitionRoomPrefabs = new List<GameObject>();

    [Header("Prefabs Especiais")]
    public GameObject merchantRoomPrefab;
    public GameObject merchantPrefab;

    [Tooltip("Sala final com portal para o próximo nível. Deve ter 1 Entrada.")]
    public GameObject exitRoomPrefab;

    [Tooltip("Lista de prefabs de becos sem saída (raízes, tocos, rochas). Sorteia aleatoriamente entre eles para variar a dungeon.")]
    public List<GameObject> deadEndPrefabs = new List<GameObject>();

    [Tooltip("Prefab legado único (usado como fallback se a lista acima estiver vazia). Deve ter 1 Entrada.")]
    public GameObject deadEndPrefab;

    public GameObject GetRandomDeadEndPrefab()
    {
        if (deadEndPrefabs != null && deadEndPrefabs.Count > 0)
        {
            var valid = deadEndPrefabs.Where(p => p != null).ToList();
            if (valid.Count > 0) return valid[Random.Range(0, valid.Count)];
        }
        return deadEndPrefab;
    }

    [Header("Boss Fight")]
    [Tooltip("Prefab da sala de Boss Fight (Round 4). Deve ter 1 ConnectionPoint(Entrada).\nQuando o round boss for detectado, apenas Safe Room + Boss Room são gerados.")]
    public GameObject bossRoomPrefab;

    [Header("Regras de Geração")]
    [Tooltip("Número alvo de salas principais (sem contar especiais).")]
    public int maxMainRooms = 10;

    [Tooltip("Quantas vezes o mesmo prefab pode aparecer por run. Evita repetição excessiva.")]
    public int roomLimitPerType = 5;

    [Range(0f, 1f)]
    [Tooltip("Chance de cada beco sem saída virar sala do Mercador. O último garante o spawn caso nenhum sorteie.")]
    public float merchantRoomChance = 0.25f;

    [Header("Configurações")]
    [Tooltip("Delay extra após a geração antes de notificar o GameManager (para assets carregarem).")]
    public float extraLoadDelay = 1.0f;

    [Header("Alinhamento")]
    [Tooltip("Posição Y do chão de todas as salas. Ajuste se as salas flutuarem ou afundarem.")]
    public float roomFloorY = 0f;

    [Header("Anti-Sobreposição")]
    [Tooltip("Margem (metros) aplicada ao bounds check. Aumente se salas legítimas forem descartadas (falso positivo). Diminua se ainda houver colisões.")]
    public float overlapTolerance = 0.5f;

    // =========================================================
    // ESTADO INTERNO
    // =========================================================

    /// <summary>Contagem de usos de cada prefab (por nome) na run atual.</summary>
    private Dictionary<string, int> roomCounts = new Dictionary<string, int>();

    /// <summary>Fila de ConnectionPoints do tipo Saida ainda não conectados.</summary>
    private List<ConnectionPoint> openOutputs = new List<ConnectionPoint>();

    private Transform playerSpawnPoint;
    private bool merchantRoomSpawned = false;
    private bool exitRoomSpawned = false;
    private int roomSequenceCounter = 0;
    private NavMeshDataInstance activeNavMeshInstance;
    private NavMeshData activeNavData;

    /// <summary>Bounds de todas as salas já confirmadas no mapa — (referência + bounds) para exclusão de vizinhos diretos.</summary>
    private List<(GameObject room, Bounds bounds)> placedRoomBounds = new List<(GameObject, Bounds)>();

    // =========================================================
    // API PÚBLICA
    // =========================================================

    /// <summary>Chamado pelo GameManager para iniciar a geração.</summary>
    public void GenerateLevel()
    {
        // Destroi salas antigas da geração anterior se existirem na cena
        RoomController[] oldRooms = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        foreach (var r in oldRooms)
        {
            if (r != null && r.gameObject != null)
            {
                Destroy(r.gameObject);
            }
        }

        roomCounts.Clear();
        openOutputs.Clear();
        placedRoomBounds.Clear();
        merchantRoomSpawned = false;
        exitRoomSpawned = false;
        roomSequenceCounter = 0;

        // --- Progressão de Rounds: ajusta maxMainRooms dinamicamente ---
        bool isBossRound = false;
        if (RunManager.instance != null)
        {
            isBossRound = RunManager.instance.isBossRound;
            if (!isBossRound)
            {
                maxMainRooms = RunManager.instance.GetMaxRoomsForCurrentLevel();
                Debug.Log($"[LevelGenerator] 🗺️ Round {RunManager.instance.currentLevel}/{RunManager.instance.totalLevels} — gerando {maxMainRooms} salas principais.");
            }
            else
            {
                Debug.Log($"[LevelGenerator] 🏆 Round {RunManager.instance.currentLevel}/{RunManager.instance.totalLevels} — BOSS FIGHT!");
            }
        }

        // --- Instancia a Safe Room na origem ---
        // Usa a rotação salva no prefab (não força identity) para respeitar correções de eixo do FBX
        GameObject startRoom = Instantiate(startRoomPrefab, Vector3.zero, startRoomPrefab.transform.rotation);

        playerSpawnPoint = FindNamedChild(startRoom.transform, "Player_StartPoint");
        if (playerSpawnPoint == null)
        {
            Debug.LogError("[LevelGenerator] Safe Room não tem um filho chamado 'Player_StartPoint'!");
            return;
        }

        // Registra bounds da Safe Room como âncora para o sistema anti-sobreposição
        RegisterRoomBounds(startRoom);

        // Registra as saídas da Safe Room (pula a Entrada, que fica no West/Nave)
        RegisterOutputPoints(startRoom, isStartRoom: true);

        if (isBossRound)
            StartCoroutine(BossGenerationCoroutine());
        else
            StartCoroutine(GenerationLoop(roomCount: 1));
    }

    // =========================================================
    // BOSS GENERATION
    // =========================================================

    /// <summary>
    /// Corrotina usada no round de Boss Fight.
    /// Pula a geração procedural normal e conecta o bossRoomPrefab
    /// diretamente à primeira saída disponível da Safe Room.
    /// </summary>
    IEnumerator BossGenerationCoroutine()
    {
        if (bossRoomPrefab == null)
        {
            Debug.LogError("[LevelGenerator] ❌ bossRoomPrefab não definido no Inspector! " +
                           "Arraste o prefab de Boss Fight no campo 'Boss Room Prefab' do LevelGenerator.");
            yield break;
        }

        if (openOutputs.Count == 0)
        {
            Debug.LogError("[LevelGenerator] ❌ Safe Room não tem saídas disponíveis para conectar o Boss!");
            yield break;
        }

        // Pega a primeira saída da Safe Room
        ConnectionPoint bossOutput = openOutputs[0];
        openOutputs.RemoveAt(0);
        bossOutput.isOccupied = true;

        GameObject bossRoom = Instantiate(bossRoomPrefab);
        ConnectionPoint bossEntrada = GetInputPoint(bossRoom, bossOutput.connectionTag, bossOutput.transform);

        if (bossEntrada != null)
        {
            AlignRooms(bossOutput.transform, bossEntrada.transform);
            bossEntrada.isOccupied = true;
            Debug.Log("[LevelGenerator] ✅ Sala de Boss conectada à Safe Room!");
        }
        else
        {
            // Fallback: posiciona na frente da saída sem alinhar sockets
            bossRoom.transform.position = bossOutput.transform.position + bossOutput.transform.forward * 20f;
            Debug.LogWarning("[LevelGenerator] ⚠️ Boss Room não tem ConnectionPoint(Entrada) compatível. Usando posição fallback.");
        }

        Debug.Log("[LevelGenerator] 🏆 BOSS FIGHT gerado!");
        yield return new WaitForSeconds(extraLoadDelay);

        TriggerItemSpawning();

        BakeGlobalNavMesh();

        if (GameManager.instance != null)
            GameManager.instance.OnLevelReady(playerSpawnPoint);
    }

    // =========================================================
    // LOOP DE GERAÇÃO
    // =========================================================

    IEnumerator GenerationLoop(int roomCount)
    {
        while (roomCount < maxMainRooms && openOutputs.Count > 0)
        {
            // Pega uma saída aleatória da fila
            int idx = Random.Range(0, openOutputs.Count);
            ConnectionPoint currentOutput = openOutputs[idx];
            openOutputs.RemoveAt(idx);

            if (currentOutput.isOccupied) continue;
            currentOutput.isOccupied = true;

            // --- Corredor de Transição (retry com todas as opções embaralhadas) ---
            if (transitionRoomPrefabs == null || transitionRoomPrefabs.Count == 0)
            {
                Debug.LogError("[LevelGenerator] transitionRoomPrefabs vazio no Inspector!");
                continue;
            }

            GameObject sourceRoom = currentOutput.transform.root.gameObject;
            List<GameObject> shuffledTransitions = ShuffledCopy(transitionRoomPrefabs);
            bool outputPlaced = false;

            foreach (GameObject selectedTransitionPrefab in shuffledTransitions)
            {
                if (selectedTransitionPrefab == null) continue;

                GameObject transitionRoom = Instantiate(selectedTransitionPrefab);
                ConnectionPoint[] transCPs = transitionRoom.GetComponentsInChildren<ConnectionPoint>();

                ConnectionPoint transEntrada = null;
                ConnectionPoint transSaida   = null;

                foreach (var cp in transCPs)
                {
                    if (transEntrada == null && cp.pointType == ConnectionPoint.PointType.Entrada && !cp.isOccupied)
                        transEntrada = cp;
                    if (transSaida == null && cp.pointType == ConnectionPoint.PointType.Saida && !cp.isOccupied)
                        transSaida = cp;
                    if (transEntrada != null && transSaida != null) break;
                }

                if (transEntrada == null || transSaida == null)
                {
                    Debug.LogWarning($"[LevelGenerator] ⚠️ '{selectedTransitionPrefab.name}' sem Entrada/Saida válida. Pulando.");
                    Destroy(transitionRoom);
                    continue;
                }

                transSaida.connectionTag = currentOutput.connectionTag;
                AlignRooms(currentOutput.transform, transEntrada.transform);

                // Checa sobreposição da transição ANTES de tentar a sala principal
                if (HasOverlapWithExistingRooms(transitionRoom, excludeRoom: sourceRoom))
                {
                    Debug.LogWarning($"[LevelGenerator] ⚠️ Transição '{selectedTransitionPrefab.name}' sobrepõe. Tentando próxima...");
                    Destroy(transitionRoom);
                    continue;
                }

                // --- Sala Principal ---
                GameObject roomPrefab = GetCompatibleRoomPrefab(transSaida.connectionTag);

                if (roomPrefab == null)
                {
                    // Sem sala compatível — mantém transição e registra saída como beco
                    transEntrada.isOccupied = true;
                    RegisterRoomBounds(transitionRoom);
                    openOutputs.Add(transSaida);
                    outputPlaced = true;
                    Debug.LogWarning($"[LevelGenerator] ⚠️ Nenhum mainRoom compatível com tag='{transSaida.connectionTag}'. Transição virou beco.");
                    break;
                }

                GameObject newRoom = Instantiate(roomPrefab);
                ConnectionPoint roomEntrada = GetInputPoint(newRoom, transSaida.connectionTag, transSaida.transform);

                if (roomEntrada == null)
                {
                    Debug.LogError($"[LevelGenerator] ❌ '{roomPrefab.name}' sem Entrada compatível. Descartando.");
                    Destroy(newRoom);
                    Destroy(transitionRoom);
                    continue;
                }

                AlignRooms(transSaida.transform, roomEntrada.transform);

                if (HasOverlapWithExistingRooms(newRoom, excludeRoom: transitionRoom))
                {
                    Debug.LogWarning($"[LevelGenerator] ⚠️ Sala '{roomPrefab.name}' sobrepõe. Tentando próxima transição...");
                    Destroy(newRoom);
                    Destroy(transitionRoom);
                    continue;
                }

                // ✅ Par transição + sala aceito sem sobreposição
                transEntrada.isOccupied = true;
                transSaida.isOccupied = true;
                roomEntrada.isOccupied = true;

                RegisterRoomBounds(transitionRoom);
                RegisterRoomBounds(newRoom);

                roomCount++;
                roomSequenceCounter++;
                string prefabName = roomPrefab.name;
                if (!roomCounts.ContainsKey(prefabName)) roomCounts[prefabName] = 0;
                roomCounts[prefabName]++;

                RoomController roomCtrl = newRoom.GetComponentInChildren<RoomController>();
                if (roomCtrl != null)
                    roomCtrl.Initialize(roomSequenceCounter);

                RegisterOutputPoints(newRoom, isStartRoom: false);
                Debug.Log($"[LevelGenerator] ✅ Sala '{prefabName}' adicionada (#{roomCount}). Saídas abertas: {openOutputs.Count}");
                outputPlaced = true;
                break;
            }

            if (!outputPlaced)
            {
                // Fallback: Tenta conectar uma mainRoom DIRETA na saída (sem corredor de transição)
                GameObject roomPrefab = GetCompatibleRoomPrefab(currentOutput.connectionTag);
                if (roomPrefab != null)
                {
                    GameObject directRoom = Instantiate(roomPrefab);
                    ConnectionPoint directEntrada = GetInputPoint(directRoom, currentOutput.connectionTag, currentOutput.transform);
                    if (directEntrada != null)
                    {
                        AlignRooms(currentOutput.transform, directEntrada.transform);
                        if (!HasOverlapWithExistingRooms(directRoom, excludeRoom: sourceRoom))
                        {
                            currentOutput.isOccupied = true;
                            directEntrada.isOccupied = true;
                            RegisterRoomBounds(directRoom);

                            roomCount++;
                            roomSequenceCounter++;
                            string prefabName = roomPrefab.name;
                            if (!roomCounts.ContainsKey(prefabName)) roomCounts[prefabName] = 0;
                            roomCounts[prefabName]++;

                            RoomController roomCtrl = directRoom.GetComponentInChildren<RoomController>();
                            if (roomCtrl != null)
                                roomCtrl.Initialize(roomSequenceCounter);

                            RegisterOutputPoints(directRoom, isStartRoom: false);
                            Debug.Log($"[LevelGenerator] ✅ Sala '{prefabName}' adicionada via conexão direta (#{roomCount}). Saídas abertas: {openOutputs.Count}");
                            outputPlaced = true;
                        }
                        else
                        {
                            Destroy(directRoom);
                        }
                    }
                    else
                    {
                        Destroy(directRoom);
                    }
                }
            }

            if (!outputPlaced)
                Debug.LogWarning($"[LevelGenerator] ⚠️ Saída '{currentOutput.gameObject.name}' descartada: nenhuma transição sem sobreposição.");

            yield return null;
        }

        Debug.Log("[LevelGenerator] Caminho principal concluído. Processando saídas restantes...");
        yield return StartCoroutine(ProcessRemainingOutputs());
    }

    // =========================================================
    // PROCESSAMENTO DE SAÍDAS RESTANTES
    // =========================================================

    IEnumerator ProcessRemainingOutputs()
    {
        // 1. Escolhe a saída MAIS DISTANTE das salas geradas (não-SafeRoom) para ser a ExitRoom
        if (openOutputs.Count > 0 && !exitRoomSpawned)
        {
            List<ConnectionPoint> mainOutputs = openOutputs.Where(op => {
                if (op == null || op.isOccupied || op.transform.root == null) return false;
                string rootName = op.transform.root.gameObject.name;
                if (startRoomPrefab != null && rootName.Contains(startRoomPrefab.name)) return false;
                if (rootName.Contains("Safe")) return false;
                return true;
            }).OrderByDescending(op => Vector3.Distance(Vector3.zero, op.transform.position)).ToList();

            // Tenta primeiro com transição
            for (int i = 0; i < mainOutputs.Count; i++)
            {
                if (TrySpawnSpecialRoom(exitRoomPrefab, mainOutputs[i], "Sala de Saída", useTransition: true))
                {
                    openOutputs.Remove(mainOutputs[i]);
                    exitRoomSpawned = true;
                    break;
                }
            }

            // Se falhar com transição, tenta conexão direta sem transição nas mesmas saídas principais
            if (!exitRoomSpawned)
            {
                for (int i = 0; i < mainOutputs.Count; i++)
                {
                    if (TrySpawnSpecialRoom(exitRoomPrefab, mainOutputs[i], "Sala de Saída (direta)", useTransition: false))
                    {
                        openOutputs.Remove(mainOutputs[i]);
                        exitRoomSpawned = true;
                        break;
                    }
                }
            }
        }

        // Fallback de fase 2 em saídas livres (excluindo estritamente a SafeRoom)
        if (openOutputs.Count > 0 && !exitRoomSpawned)
        {
            List<ConnectionPoint> sortedOutputs = openOutputs.Where(op => {
                if (op == null || op.isOccupied || op.transform.root == null) return false;
                string rootName = op.transform.root.gameObject.name;
                if (startRoomPrefab != null && rootName.Contains(startRoomPrefab.name)) return false;
                if (rootName.Contains("Safe")) return false;
                return true;
            }).OrderByDescending(op => Vector3.Distance(Vector3.zero, op.transform.position)).ToList();

            for (int i = 0; i < sortedOutputs.Count; i++)
            {
                if (TrySpawnSpecialRoom(exitRoomPrefab, sortedOutputs[i], "Sala de Saída (fallback)", useTransition: false))
                {
                    openOutputs.Remove(sortedOutputs[i]);
                    exitRoomSpawned = true;
                    break;
                }
            }
        }

        // 2. Demais saídas → Mercador ou DeadEnd
        List<ConnectionPoint> deadEnds = new List<ConnectionPoint>(openOutputs);
        openOutputs.Clear();

        for (int i = 0; i < deadEnds.Count; i++)
        {
            bool isLast = (i == deadEnds.Count - 1);

            if (!merchantRoomSpawned && (Random.value < merchantRoomChance || isLast))
            {
                if (TrySpawnMerchantRoom(deadEnds[i]))
                    merchantRoomSpawned = true;
                else
                    TrySpawnSpecialRoom(GetRandomDeadEndPrefab(), deadEnds[i], "Dead End", useTransition: false);
            }
            else
            {
                TrySpawnSpecialRoom(GetRandomDeadEndPrefab(), deadEnds[i], "Dead End", useTransition: false);
            }
        }

        // 3. Garantia Absoluta: se ExitRoom ainda não foi conectada, força conexão à saída mais distante disponível
        if (!exitRoomSpawned && exitRoomPrefab != null)
        {
            Debug.LogWarning("[LevelGenerator] ⚠️ Buscando qualquer porta livre para conectar a ExitRoom...");

            ConnectionPoint[] allCPs = FindObjectsByType<ConnectionPoint>(FindObjectsSortMode.None);
            var sortedCPs = allCPs.Where(cp => cp != null && cp.pointType == ConnectionPoint.PointType.Saida && !cp.isOccupied)
                                  .OrderByDescending(cp => Vector3.Distance(Vector3.zero, cp.transform.position));

            foreach (var cp in sortedCPs)
            {
                if (startRoomPrefab != null && cp.transform.root.gameObject.name.Contains(startRoomPrefab.name)) continue;
                if (cp.transform.root.gameObject.name.Contains("Safe")) continue;

                if (TrySpawnSpecialRoom(exitRoomPrefab, cp, "Sala de Saída (recuperação)", useTransition: false))
                {
                    exitRoomSpawned = true;
                    Debug.Log("[LevelGenerator] ✅ ExitRoom conectada com sucesso na saída de recuperação!");
                    break;
                }
            }

            // Se mesmo assim não conseguiu por causa de verificação de sobreposição estrita, força acoplamento direto no conector da porta
            if (!exitRoomSpawned)
            {
                ConnectionPoint bestCP = sortedCPs.FirstOrDefault();
                if (bestCP == null)
                {
                    bestCP = FindObjectsByType<ConnectionPoint>(FindObjectsSortMode.None)
                        .Where(cp => cp != null && cp.pointType == ConnectionPoint.PointType.Saida)
                        .OrderByDescending(cp => Vector3.Distance(Vector3.zero, cp.transform.position))
                        .FirstOrDefault();
                }

                if (bestCP != null)
                {
                    Debug.LogWarning($"[LevelGenerator] 🚨 Conectando ExitRoom diretamente à porta '{bestCP.gameObject.name}'!");
                    GameObject forcedExit = Instantiate(exitRoomPrefab);
                    ConnectionPoint entradaExit = GetInputPoint(forcedExit, bestCP.connectionTag, bestCP.transform);
                    if (entradaExit != null)
                    {
                        AlignRooms(bestCP.transform, entradaExit.transform);
                        bestCP.isOccupied = true;
                        entradaExit.isOccupied = true;
                        RegisterRoomBounds(forcedExit);
                        exitRoomSpawned = true;
                    }
                    else
                    {
                        Destroy(forcedExit);
                    }
                }
            }
        }

        Debug.Log($"[LevelGenerator] Geração Completa! ExitRoom: {(exitRoomSpawned ? "✅" : "❌")}");
        yield return new WaitForSeconds(extraLoadDelay);

        TriggerItemSpawning();

        BakeGlobalNavMesh();

        if (BarrierCounterUI.Instance != null)
            BarrierCounterUI.Instance.UpdateDisplay();

        if (GameManager.instance != null)
            GameManager.instance.OnLevelReady(playerSpawnPoint);
    }

    // =========================================================
    // SPAWN DE SALAS ESPECIAIS (SUPORTE A TRANSIÇÃO E CHECAGEM DE OVERLAP)
    // =========================================================

    bool TrySpawnSpecialRoom(GameObject prefab, ConnectionPoint targetOutput, string label, bool useTransition = true)
    {
        if (prefab == null || targetOutput == null) return false;

        GameObject sourceRoom = targetOutput.transform.root.gameObject;

        // 1. Tenta posicionar a sala especial COM um corredor de transição para evitar colisão
        if (useTransition && transitionRoomPrefabs != null && transitionRoomPrefabs.Count > 0)
        {
            List<GameObject> shuffledTransitions = ShuffledCopy(transitionRoomPrefabs);
            foreach (GameObject transPrefab in shuffledTransitions)
            {
                if (transPrefab == null) continue;

                GameObject transitionRoom = Instantiate(transPrefab);
                ConnectionPoint[] transCPs = transitionRoom.GetComponentsInChildren<ConnectionPoint>();
                ConnectionPoint transEntrada = transCPs.FirstOrDefault(cp => cp.pointType == ConnectionPoint.PointType.Entrada && !cp.isOccupied);
                ConnectionPoint transSaida   = transCPs.FirstOrDefault(cp => cp.pointType == ConnectionPoint.PointType.Saida && !cp.isOccupied);

                if (transEntrada == null || transSaida == null)
                {
                    Destroy(transitionRoom);
                    continue;
                }

                transSaida.connectionTag = targetOutput.connectionTag;
                AlignRooms(targetOutput.transform, transEntrada.transform);

                if (HasOverlapWithExistingRooms(transitionRoom, excludeRoom: sourceRoom))
                {
                    Destroy(transitionRoom);
                    continue;
                }

                // Tenta conectar a sala especial na saída da transição
                GameObject roomWithTrans = Instantiate(prefab);
                ConnectionPoint entradaTrans = GetInputPoint(roomWithTrans, transSaida.connectionTag, transSaida.transform);

                if (entradaTrans == null)
                {
                    Destroy(roomWithTrans);
                    Destroy(transitionRoom);
                    continue;
                }

                AlignRooms(transSaida.transform, entradaTrans.transform);

                if (HasOverlapWithExistingRooms(roomWithTrans, excludeRoom: transitionRoom))
                {
                    Destroy(roomWithTrans);
                    Destroy(transitionRoom);
                    continue;
                }

                // SUCESSO com transição!
                targetOutput.isOccupied = true;
                transEntrada.isOccupied = true;
                transSaida.isOccupied = true;
                entradaTrans.isOccupied = true;

                RegisterRoomBounds(transitionRoom);
                RegisterRoomBounds(roomWithTrans);

                Debug.Log($"[LevelGenerator] ✅ {label} criado (com corredor de transição).");
                return true;
            }
        }

        // 2. Conexão direta sem transição (fallback)
        GameObject directRoom = Instantiate(prefab);
        ConnectionPoint entradaDirect = GetInputPoint(directRoom, targetOutput.connectionTag, targetOutput.transform);

        if (entradaDirect == null)
        {
            Destroy(directRoom);
            Debug.LogWarning($"[LevelGenerator] '{label}' não tem Entrada. Descartando.");
            return false;
        }

        AlignRooms(targetOutput.transform, entradaDirect.transform);

        if (HasOverlapWithExistingRooms(directRoom, excludeRoom: sourceRoom))
        {
            Destroy(directRoom);
            Debug.Log($"[LevelGenerator] ⚠️ '{label}' sobrepôs. Tentando próxima saída...");
            return false;
        }

        targetOutput.isOccupied = true;
        entradaDirect.isOccupied = true;
        RegisterRoomBounds(directRoom);
        Debug.Log($"[LevelGenerator] ✅ {label} criado (conexão direta).");
        return true;
    }

    bool TrySpawnMerchantRoom(ConnectionPoint targetOutput)
    {
        if (merchantRoomPrefab == null || merchantPrefab == null) return false;

        // Se o pacto já foi realizado nesta run, impede a criação da Sala do Mercador
        if (MerchantUIController.HasInstance && MerchantUIController.Instance.HasMadePactInRun)
        {
            Debug.Log("[LevelGenerator] 🚫 Pacto já realizado nesta run. Sala do Mercador cancelada.");
            return false;
        }

        GameObject room = Instantiate(merchantRoomPrefab);
        ConnectionPoint entrada = GetInputPoint(room, targetOutput.connectionTag);

        if (entrada == null)
        {
            Destroy(room);
            return false;
        }

        AlignRooms(targetOutput.transform, entrada.transform);

        if (HasOverlapWithExistingRooms(room, excludeRoom: targetOutput.transform.root.gameObject))
        {
            Destroy(room);
            return false;
        }

        targetOutput.isOccupied = true;
        entrada.isOccupied = true;
        RegisterRoomBounds(room);

        Transform spawnPoint = FindNamedChild(room.transform, "Merchant_SpawnPoint");
        if (spawnPoint != null)
            Instantiate(merchantPrefab, spawnPoint.position, spawnPoint.rotation);

        Debug.Log("[LevelGenerator] ✅ Sala do Mercador criada.");
        return true;
    }
    // =========================================================
    // SELEÇÃO DE PREFAB
    // =========================================================

    /// <summary>
    /// Busca um prefab da mainRoomPrefabs que:
    /// 1. Tenha um ConnectionPoint(Entrada) com a tag compatível.
    /// 2. Não tenha atingido o roomLimitPerType.
    /// Retorna null se nenhum for encontrado.
    /// </summary>
    GameObject GetCompatibleRoomPrefab(string requiredTag)
    {
        List<GameObject> valid = new List<GameObject>();

        foreach (GameObject prefab in mainRoomPrefabs)
        {
            string prefabName = prefab.name;
            int count = roomCounts.ContainsKey(prefabName) ? roomCounts[prefabName] : 0;
            if (count >= roomLimitPerType) continue;

            // Verifica se o prefab tem pelo menos 1 ConnectionPoint(Entrada) com a tag certa
            ConnectionPoint[] cps = prefab.GetComponentsInChildren<ConnectionPoint>();
            bool hasCompatibleInput = cps.Any(cp =>
                cp.pointType == ConnectionPoint.PointType.Entrada &&
                cp.connectionTag == requiredTag &&
                !cp.isOccupied);

            if (hasCompatibleInput)
                valid.Add(prefab);
        }

        if (valid.Count == 0) return null;
        return valid[Random.Range(0, valid.Count)];
    }

    // =========================================================
    // REGISTRO DE SAÍDAS
    // =========================================================

    /// <summary>
    /// Coleta todos os ConnectionPoints(Saida) de uma sala instanciada e os
    /// adiciona à fila openOutputs.
    /// Se isStartRoom == true, pula a saída marcada como West (Nave/Crash Site).
    /// </summary>
    void RegisterOutputPoints(GameObject room, bool isStartRoom)
    {
        ConnectionPoint[] allCPs = room.GetComponentsInChildren<ConnectionPoint>();
        List<ConnectionPoint> outputs = allCPs
            .Where(cp => cp.pointType == ConnectionPoint.PointType.Saida && !cp.isOccupied)
            .ToList();

        if (isStartRoom && outputs.Count > 0)
        {
            // Na Safe Room, apenas UMA saída aleatória é aberta inicialmente
            // (a saída West fica reservada para a Nave/Crash Site — não deve gerar salas)
            List<ConnectionPoint> validStart = outputs
                .Where(cp => !cp.gameObject.name.Contains("West") && !cp.gameObject.name.Contains("Nave"))
                .ToList();

            if (validStart.Count > 0)
                openOutputs.Add(validStart[Random.Range(0, validStart.Count)]);
            else if (outputs.Count > 0)
                openOutputs.Add(outputs[Random.Range(0, outputs.Count)]);
        }
        else
        {
            openOutputs.AddRange(outputs);
        }
    }

    // =========================================================
    // ALINHAMENTO
    // =========================================================

    /// <summary>
    /// Alinha a sala que contém socketB de modo que socketB coincida com socketA
    /// no plano XZ, com os forwards opostos (as salas se "encaixam" pela boca dos sockets).
    ///
    /// O eixo Y é IGNORADO no delta de posição: todas as salas ficam ancoradas em
    /// roomFloorY independentemente da altura local dos sockets no prefab.
    /// Isso evita que diferenças de pivot entre modelos façam uma sala flutuar ou afundar.
    /// </summary>
    void AlignRooms(Transform socketA, Transform socketB)
    {
        if (socketA == null || socketB == null)
        {
            Debug.LogError("[LevelGenerator] AlignRooms: um dos sockets é null!");
            return;
        }

        Transform roomA = socketA.root;
        Transform roomB = socketB.root;

        // 1. Rotação: alinha no plano horizontal (XZ), garantindo 0° de Pitch e Roll (sala 100% nivelada)
        Vector3 fwdA = socketA.forward;
        fwdA.y = 0f;
        if (fwdA.sqrMagnitude < 0.0001f) fwdA = Vector3.forward;
        fwdA.Normalize();

        Vector3 fwdB = socketB.forward;
        fwdB.y = 0f;
        if (fwdB.sqrMagnitude < 0.0001f) fwdB = Vector3.forward;
        fwdB.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(-fwdA, Vector3.up);
        Quaternion currentSocketBRot = Quaternion.LookRotation(fwdB, Vector3.up);
        Quaternion correctionRotation = targetRotation * Quaternion.Inverse(currentSocketBRot);
        roomB.rotation = correctionRotation * roomB.rotation;

        // Força rotação perfeitamente horizontal (apenas Yaw no eixo Y)
        Vector3 euler = roomB.eulerAngles;
        roomB.rotation = Quaternion.Euler(0f, euler.y, 0f);

        // 2. Posição XZ: alinha os sockets horizontalmente
        Vector3 delta = socketA.position - socketB.position;
        roomB.position += new Vector3(delta.x, 0f, delta.z);

        // 3. Posição Y: alinha o nível exato do chão dos dois sockets
        ConnectionPoint cpA = socketA.GetComponent<ConnectionPoint>();
        ConnectionPoint cpB = socketB.GetComponent<ConnectionPoint>();

        float floorYA = (cpA != null) ? cpA.GetFloorWorldY() : socketA.position.y;
        float floorYB = (cpB != null) ? cpB.GetFloorWorldY() : socketB.position.y;

        float deltaY = floorYA - floorYB;
        roomB.position += new Vector3(0f, deltaY, 0f);
    }

    // =========================================================
    // HELPERS DE BUSCA
    // =========================================================

    /// <summary>
    /// Retorna o melhor ConnectionPoint(Entrada) disponível para conectar com o socket de origem.
    ///
    /// Se o prefab tiver apenas 1 Entrada, retorna ela diretamente.
    /// Se tiver múltiplas Entradas (candidatas), escolhe a que exige MENOS rotação para se
    /// alinhar com o socket de origem — ou seja, a cujo forward é mais oposto ao forward do socket.
    ///
    /// Isso permite prefabs com 2+ posições de entrada possíveis, onde o gerador escolhe
    /// automaticamente a mais natural para cada conexão.
    /// </summary>
    ConnectionPoint GetInputPoint(GameObject room, string tag, Transform incomingSocket = null)
    {
        var candidates = room.GetComponentsInChildren<ConnectionPoint>()
            .Where(cp =>
                cp.pointType == ConnectionPoint.PointType.Entrada &&
                cp.connectionTag == tag &&
                !cp.isOccupied)
            .ToList();

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1 || incomingSocket == null) return candidates[0];

        // Escolhe a Entrada cujo forward é mais oposto ao forward do socket de saída.
        // O AlignRooms vai rotacionar a sala para que os dois se oponham — quanto mais
        // alinhados já estiverem, menos rotação é necessária e mais natural fica o layout.
        ConnectionPoint best = null;
        float bestDot = float.MinValue;

        foreach (var cp in candidates)
        {
            // Queremos: cp.forward ≈ -incomingSocket.forward (opostos = encaixe perfeito)
            float dot = Vector3.Dot(cp.transform.forward, -incomingSocket.forward);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = cp;
            }
        }

        return best;
    }

    /// <summary>Retorna o primeiro ConnectionPoint(Saida) não ocupado com a tag especificada.</summary>
    ConnectionPoint GetFirstOutputPoint(GameObject room, string tag)
    {
        return room.GetComponentsInChildren<ConnectionPoint>()
            .FirstOrDefault(cp =>
                cp.pointType == ConnectionPoint.PointType.Saida &&
                cp.connectionTag == tag &&
                !cp.isOccupied);
    }

    /// <summary>Encontra um filho pelo nome exato (para Player_StartPoint e Merchant_SpawnPoint).</summary>
    Transform FindNamedChild(Transform parent, string childName)
    {
        return parent.GetComponentsInChildren<Transform>()
            .FirstOrDefault(t => t.name == childName);
    }

    // =========================================================
    // ANTI-SOBREPOSIÇÃO
    // =========================================================

    /// <summary>
    /// Calcula o Bounds combinado de todos os Renderers da sala.
    /// Retorna Bounds zerado se não houver renderers (sala invisível/vazia).
    /// </summary>
    Bounds GetRoomBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds();

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);
        return combined;
    }

    /// <summary>Registra os bounds da sala como área ocupada para checagens futuras.</summary>
    void RegisterRoomBounds(GameObject room)
    {
        Bounds b = GetRoomBounds(room);
        // Só registra se tem volume real (evita entrar com bounds nulo)
        if (b.size.sqrMagnitude > 0f)
            placedRoomBounds.Add((room, b));
    }

    /// <summary>
    /// Retorna true se os bounds da sala se sobrepõem com alguma sala já confirmada.
    /// O overlapTolerance encolhe o bounds antes de testar, permitindo que paredes
    /// encostem sem disparar um falso positivo.
    ///
    /// excludeRoom: ignora este GameObject na checagem (usado para não rejeitar
    /// salas que encostam em seu vizinho direto por design).
    /// </summary>
    bool HasOverlapWithExistingRooms(GameObject room, GameObject excludeRoom = null)
    {
        Bounds b = GetRoomBounds(room);
        if (b.size.sqrMagnitude == 0f) return false;

        // Encolhe para aceitar paredes tocando, mas rejeitar penetração real
        b.Expand(-overlapTolerance);

        foreach (var entry in placedRoomBounds)
        {
            // Pula o vizinho direto — ele encosta por design, não é colisão real
            if (excludeRoom != null && entry.room == excludeRoom) continue;
            if (b.Intersects(entry.bounds)) return true;
        }
        return false;
    }

    /// <summary>
    /// Retorna uma cópia embaralhada da lista sem modificar a original.
    /// Usada para tentar todas as transições em ordem aleatória antes de desistir.
    /// </summary>
    List<T> ShuffledCopy<T>(List<T> source)
    {
        List<T> copy = new List<T>(source);
        for (int i = copy.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = copy[i]; copy[i] = copy[j]; copy[j] = tmp;
        }
        return copy;
    }

    // =========================================================
    // RUNTIME NAVMESH BAKING
    // =========================================================

    [Header("NavMesh Debug Options")]
    [Tooltip("Se desabilitado, não gera o NavMesh global em runtime.")]
    public bool useNavMesh = true;

    [Tooltip("Se desabilitado, desativa a contenção nos limites do NavMesh para todos os inimigos.")]
    public bool useNavMeshConstraint = true;

    [Header("NavMesh Runtime Baking")]
    [Tooltip("Ângulo máximo de rampa walkable (graus). " +
             "50° cobre terreno orgânico sem deixar escalar paredes reais.")]
    [Range(30f, 60f)]
    public float navMeshSlope = 50f;

    [Tooltip("Altura máxima de degrau que o agente transpõe (metros). " +
             "0.5 preenche juntas entre peças do terreno orgânico sem escalar obstáculos.")]
    [Range(0.1f, 1f)]
    public float navMeshStepHeight = 0.5f;

    [Tooltip("Raio do agente para o bake (metros). " +
             "0.2 permite que a malha chegue perto das paredes — ideal para agentes de corpo pequeno.")]
    [Range(0.05f, 0.5f)]
    public float navMeshAgentRadius = 0.2f;

    [Tooltip("Tamanho do voxel de amostragem (metros). " +
             "Regra: AgentRadius / 3 = resolução ideal. Menor = mais preciso.")]
    [Range(0.03f, 0.2f)]
    public float navMeshVoxelSize = 0.0667f;

    [Tooltip("Área mínima de regiões isoladas do NavMesh (m²). " +
             "Regiões menores são removidas. Valor baixo preserva passagens estreitas.")]
    [Range(0f, 2f)]
    public float navMeshMinRegionArea = 0.05f;

    /// <summary>
    /// Assa a malha de navegação global usando NavMeshBuilder diretamente.
    /// Isso dá controle total sobre Slope, StepHeight, VoxelSize e AgentRadius
    /// — parâmetros que o NavMeshSurface não expõe via script.
    ///
    /// Usa o agent type Humanoid (ID 0) para manter compatibilidade com o
    /// NavMeshAgent do player sem precisar trocar o agent type.
    /// </summary>
    private void TriggerItemSpawning()
    {
        ItemSpawner itemSpawner = FindFirstObjectByType<ItemSpawner>();
        if (itemSpawner == null)
        {
            itemSpawner = gameObject.AddComponent<ItemSpawner>();
            Debug.Log("[LevelGenerator] ItemSpawner nao encontrado na cena. Adicionado automaticamente.");
        }
        itemSpawner.SpawnItems();
    }

    void BakeGlobalNavMesh()
    {
        if (!useNavMesh)
        {
            Debug.Log("[LevelGenerator] 🚫 Geração de NavMesh desabilitada via Inspector.");
            if (activeNavMeshInstance.valid)
            {
                activeNavMeshInstance.Remove();
            }
            if (activeNavData != null)
            {
                Destroy(activeNavData);
                activeNavData = null;
            }
            return;
        }

        // 1. Busca as configurações base do Humanoid e aplica parâmetros relaxados
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0); // 0 = Humanoid
        if (settings.agentTypeID == -1)
        {
            Debug.LogWarning("[LevelGenerator] ⚠️ Agent type 'Humanoid' não encontrado. Usando settings padrão.");
            settings.agentTypeID = 0;
        }

        settings.agentSlope          = navMeshSlope;
        settings.agentClimb          = navMeshStepHeight;
        settings.agentRadius         = navMeshAgentRadius;
        settings.agentHeight         = 2.0f;
        settings.overrideVoxelSize   = true;
        settings.voxelSize           = navMeshVoxelSize;
        settings.minRegionArea       = navMeshMinRegionArea;
        // 2. Coleta TODA a geometria de colisores da cena (todas as salas instanciadas)
        var sources = new List<NavMeshBuildSource>();
        var markups = new List<NavMeshBuildMarkup>();
        NavMeshBuilder.CollectSources(
            null, ~0, NavMeshCollectGeometry.PhysicsColliders,
            0, false, markups, false, sources
        );

        // Remove geometria de objetos com NavMeshAgent (ex: o player)
        sources.RemoveAll(s =>
            s.component != null &&
            s.component.gameObject.GetComponent<NavMeshAgent>() != null
        );

        if (sources.Count == 0)
        {
            Debug.LogError("[LevelGenerator] ❌ Nenhuma geometria encontrada para assar NavMesh!");
            return;
        }

        // 3. Calcula bounds que cobrem toda a geometria coletada
        Bounds worldBounds = CalculateNavMeshBounds(sources);

        // 4. Assa a malha
        NavMeshData navData = NavMeshBuilder.BuildNavMeshData(
            settings, sources, worldBounds, Vector3.zero, Quaternion.identity
        );

        if (navData != null)
        {
            // Limpa a malha anterior da memória e do registro global do Unity para evitar acúmulo de NavMeshes fantasmas
            if (activeNavMeshInstance.valid)
            {
                activeNavMeshInstance.Remove();
            }
            if (activeNavData != null)
            {
                Destroy(activeNavData);
            }

            navData.name = "RuntimeGlobalNavMesh";
            activeNavData = navData;
            activeNavMeshInstance = NavMesh.AddNavMeshData(navData);
            Debug.Log($"[LevelGenerator] ✅ NavMesh global assado! " +
                      $"Slope={navMeshSlope}° StepHeight={navMeshStepHeight}m " +
                      $"Radius={navMeshAgentRadius}m VoxelSize={navMeshVoxelSize}m " +
                      $"({sources.Count} fontes de geometria)");
        }
        else
        {
            Debug.LogError("[LevelGenerator] ❌ NavMeshBuilder.BuildNavMeshData retornou null!");
        }
    }

    void OnDestroy()
    {
        if (activeNavMeshInstance.valid)
        {
            activeNavMeshInstance.Remove();
        }
        if (activeNavData != null)
        {
            Destroy(activeNavData);
        }
    }

    /// <summary>
    /// Calcula os bounds combinados de todas as fontes de geometria do NavMesh.
    /// </summary>
    Bounds CalculateNavMeshBounds(List<NavMeshBuildSource> sources)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;

        foreach (var src in sources)
        {
            Bounds srcBounds;

            if (src.shape == NavMeshBuildSourceShape.Mesh && src.sourceObject is Mesh mesh)
            {
                // Transforma os bounds locais do mesh para world space via Matrix4x4
                srcBounds = TransformBounds(src.transform, mesh.bounds);
            }
            else
            {
                // Box, Sphere, Capsule — posição extraída da coluna 3 da matrix
                Vector3 worldCenter = new Vector3(
                    src.transform.m03, src.transform.m13, src.transform.m23
                );
                srcBounds = new Bounds(worldCenter, src.size);
            }

            if (!initialized)
            {
                bounds = srcBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(srcBounds);
            }
        }

        // Margem extra para evitar cortar bordas
        bounds.Expand(2f);
        return bounds;
    }

    /// <summary>
    /// Transforma Bounds locais para world space usando uma Matrix4x4.
    /// Lida corretamente com rotação e escala não-uniforme.
    /// </summary>
    static Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);

        // Extrai os eixos escalados da matrix para calcular o tamanho correto
        Vector3 extents = localBounds.extents;
        Vector3 axisX = new Vector3(matrix.m00, matrix.m10, matrix.m20) * extents.x;
        Vector3 axisY = new Vector3(matrix.m01, matrix.m11, matrix.m21) * extents.y;
        Vector3 axisZ = new Vector3(matrix.m02, matrix.m12, matrix.m22) * extents.z;

        // O tamanho no world space é a soma dos valores absolutos dos eixos transformados
        float worldExtentX = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        float worldExtentY = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        float worldExtentZ = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds(center, new Vector3(worldExtentX, worldExtentY, worldExtentZ) * 2f);
    }
}
