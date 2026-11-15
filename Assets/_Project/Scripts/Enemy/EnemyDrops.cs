using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Componente de Drop para Inimigos
/// Adicione este script a qualquer inimigo com DummyHealth
/// Configura os drops de essência e itens característicos
/// </summary>
public class EnemyDrops : MonoBehaviour
{
    [Header("Essência (Sempre Dropa)")]
    [Tooltip("Prefab da essência que spawna")]
    public GameObject essencePrefab;
    [Tooltip("Quantidade de essência dropada")]
    public int essenceAmount = 10;
    [Tooltip("Variação aleatória (+/-)")]
    public int essenceVariation = 3;

    [System.Serializable]
    public class LootPoolItem
    {
        public GameObject itemPrefab;
        [Tooltip("Peso de chance para dropar. Maior = mais comum. Menor = mais raro.")]
        public float weight = 10f;
    }

    [Header("Elite Drop Settings")]
    [Tooltip("Se ativado, usa a tabela de drops calibrada de Elite (Tier 2: 80%, Tier 3: 40%, Tier 4: 20%).")]
    public bool isEliteDrops = false;

    [Header("Item Característico (Roleta)")]
    [Tooltip("Chance de o inimigo dropar ALGUM item da lista (0-1)")]
    [Range(0f, 1f)]
    public float globalDropChance = 0.38f;
    [Tooltip("Quantidade de itens dropados se der drop (da opção sorteada)")]
    public int itemAmount = 1;
    [Tooltip("Lista de possíveis drops (T1, T2, T3...). Apenas 1 será sorteado.")]
    public List<LootPoolItem> lootPool = new List<LootPoolItem>();

    [Header("Spawn Settings")]
    [Tooltip("Altura acima do inimigo para spawnar drops")]
    public float spawnHeight = 0.5f;
    [Tooltip("Raio de dispersão dos drops")]
    public float spawnRadius = 0.5f;
    [Tooltip("Força de impulso para cima")]
    public float launchForce = 3f;

    [Header("Posicionamento seguro dos drops")]
    [Tooltip("Ativado: valida com NavMesh. Desativado: usa o posicionamento anterior por raycast.")]
    public bool validateDropPositionWithNavMesh = true;
    [Min(0.1f), Tooltip("Distância horizontal máxima da morte, incluindo a dispersão.")]
    public float navMeshSearchRadius = 2f;
    [Min(0.05f), Tooltip("Distância de cada consulta local à NavMesh.")]
    public float navMeshSampleRadius = 0.75f;
    [Min(0f), Tooltip("Diferença vertical máxima em relação ao pivô do inimigo; evita outros andares.")]
    public float maxDropVerticalDistance = 1f;
    [Range(1, 16)] public int dropPositionAttempts = 10;
    [Tooltip("Máscara de áreas NavMesh permitidas (1 = Walkable).")]
    public int dropNavMeshAreaMask = 1;
    [Tooltip("Tipo de agente usado pelo bake global do projeto (0 = Humanoid).")]
    public int dropAgentTypeId = 0;
    [Min(0.05f), Tooltip("Folga física ao redor do drop; ajustar para os maiores visuais do prefab.")]
    public float dropClearanceRadius = 0.25f;
    [Tooltip("Camadas sólidas usadas para chão e obstáculos. Triggers são ignorados.")]
    public LayerMask dropCollisionMask = ~0;

    private readonly Collider[] dropOverlapBuffer = new Collider[32];
    private readonly RaycastHit[] dropRayBuffer = new RaycastHit[32];
    private DummyHealth health;

    void Start()
    {
        health = GetComponent<DummyHealth>();
        
        if (health == null)
        {
            Debug.LogWarning("[ENEMY DROPS] DummyHealth não encontrado! Este script requer DummyHealth.");
        }
    }

    /// <summary>
    /// Chamado pelo DummyHealth quando o inimigo morre
    /// </summary>
    public void OnDeath()
    {
        SpawnDrops();
    }

    void SpawnDrops()
    {
        try
        {
            Debug.Log("[ENEMY DROPS] SpawnDrops chamado para " + gameObject.name +
                " | modo: " + (validateDropPositionWithNavMesh ? "NavMesh" : "Anterior (raycast)"));
            
            Vector3 basePosition = transform.position;
            if (!validateDropPositionWithNavMesh)
            {
                RaycastHit[] groundHits = Physics.RaycastAll(transform.position + Vector3.up * 1.5f, Vector3.down, 15f);
                float bestGroundY = float.MinValue;
                bool foundGround = false;
                foreach (var h in groundHits)
                {
                    if (h.collider == null || h.collider.isTrigger) continue;
                    if (h.collider.transform.root == transform.root) continue;
                    if (h.collider.CompareTag("Enemy") || h.collider.CompareTag("Player")) continue;
                    if (h.collider.GetComponentInParent<DummyHealth>() != null) continue;
                    if (h.collider.GetComponentInParent<ShardSwarmHealth>() != null) continue;

                    if (h.point.y <= transform.position.y + 0.8f && h.point.y > bestGroundY)
                    {
                        bestGroundY = h.point.y;
                        foundGround = true;
                    }
                }

                if (foundGround)
                {
                    basePosition = new Vector3(transform.position.x, bestGroundY + 0.35f, transform.position.z);
                }
                else
                {
                    basePosition = transform.position + Vector3.up * Mathf.Min(spawnHeight, 0.4f);
                }
            }

            // --- SISTEMA DE PACTOS DO JOGADOR ---
            bool hasGreed = false;
            bool doubleEssence = false;
            bool hasChaos = false;
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                if (playerHealth.hasGreedPact) hasGreed = true;
                if (playerHealth.hasDoubleLoot) doubleEssence = true;
                if (playerHealth.hasChaosSymphony) hasChaos = true;
            }
            // ------------------------------------

        // 1. Sempre spawna essência com inflação por sala
        // Fórmula GDD §1.1: E(n) = d × (1 + α × n)  onde α = 0,05
        if (essencePrefab == null)
        {
            essencePrefab = Resources.Load<GameObject>("Essência")
                         ?? Resources.Load<GameObject>("Drops Prefabs/Essência");
#if UNITY_EDITOR
            if (essencePrefab == null)
            {
                essencePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Items_and_Crafting/Drops Prefabs/Essência.prefab");
            }
#endif
        }

        if (essencePrefab != null)
        {
            // Aplica o multiplicador de sala e bônus de +10% de essência em todos os inimigos
            float roomMultiplier = RunManager.instance != null
                ? RunManager.instance.GetEssenceMultiplier()
                : 1f;

            int scaledBase  = Mathf.RoundToInt(essenceAmount * 1.10f * roomMultiplier);
            int finalEssence = Mathf.RoundToInt((scaledBase + Random.Range(-essenceVariation, essenceVariation + 1)) * 1.10f);
            finalEssence = Mathf.Max(1, finalEssence);

            if (doubleEssence || hasGreed) finalEssence *= 2; // Dobra a essência com Ganância!

            GameObject essence = SpawnDrop(essencePrefab, basePosition);
            
            // Configura a quantidade de essência
            EssencePickup essenceScript = essence != null ? essence.GetComponent<EssencePickup>() : null;
            if (essenceScript != null)
            {
                essenceScript.essenceValue = finalEssence;
            }

            if (essence != null) Debug.Log($"[ENEMY DROPS] Dropou {finalEssence} de essência! (base:{essenceAmount} × mult:{roomMultiplier:F2} | sala {RunManager.instance?.currentRoomNumber})");
        }
        else
        {
            Debug.LogWarning("[ENEMY DROPS] essencePrefab está NULL! Configure no Inspector.");
        }

        // 2. SISTEMA DE DROPS DE ELITE (GDD: Tier 2: 80%, Tier 3: 40%, Tier 4: 20%)
        if (IsEliteMob())
        {
            SpawnEliteDrops(basePosition, hasGreed || hasChaos);
            return;
        }

        // 3. Chance de dropar ALGUM item característico calibrada por Classe
        float effectiveDropChance = globalDropChance;
        string objName = gameObject.name.ToLower();

        bool isBossOrElites = GetComponent<Geobionte_AI>() != null || 
                              objName.Contains("sentinel") || 
                              objName.Contains("bismutado") || 
                              objName.Contains("boss");

        bool isTankOrStructure = GetComponent<Golem_AI>() != null || 
                                 GetComponent<MagicStone_AI>() != null || 
                                 GetComponent<ShardSwarm_AI>() != null || 
                                 GetComponent<TotemSpawner>() != null || 
                                 objName.Contains("golem") || 
                                 objName.Contains("magicstone") || 
                                 objName.Contains("star") || 
                                 objName.Contains("totem");

        bool isSupport = GetComponent<Cristalus_AI>() != null || 
                         GetComponent<CrystalTuner>() != null || 
                         objName.Contains("cristalus") || 
                         objName.Contains("tuner");

        bool isRanged = GetComponent<PeixeAI>() != null || 
                        GetComponent<GoblinAI_Transform>() != null || 
                        GetComponent<CrystalWatcher_AI>() != null || 
                        objName.Contains("peixe") || 
                        objName.Contains("goblin") || 
                        objName.Contains("watcher");

        if (isBossOrElites)
        {
            effectiveDropChance = 1.0f; // Drop garantido para Elites / Chefes
        }
        else if (effectiveDropChance <= 0.01f)
        {
            // Fallback seguro caso o prefab esteja com 0 no Inspector
            if (isTankOrStructure) effectiveDropChance = 0.35f;
            else if (isSupport) effectiveDropChance = 0.35f;
            else if (isRanged) effectiveDropChance = 0.30f;
            else effectiveDropChance = 0.32f;
        }
        else
        {
            // Respeita o globalDropChance configurado no prefab (sem sobrescrever com clamp estreito)
            effectiveDropChance = Mathf.Clamp01(effectiveDropChance);
        }

        // --- PACTO DE GANÂNCIA: Sorte aumentada significativamente em vez de duplicar drop! ---
        if (hasGreed)
        {
            effectiveDropChance = Mathf.Clamp01(effectiveDropChance * 2.5f);
            Debug.Log($"[ENEMY DROPS] A Ganância está ativa! Chance de drop amplificada para {effectiveDropChance * 100:F0}%");
        }

        // --- PACTO A SINFONIA DO CAOS: +20% na chance geral de drop! ---
        if (hasChaos)
        {
            effectiveDropChance = Mathf.Clamp01(effectiveDropChance + 0.20f);
            Debug.Log($"[ENEMY DROPS] A Sinfonia do Caos está ativa: +20% na chance geral de drop! (Total: {effectiveDropChance * 100:F0}%)");
        }

        if (lootPool != null && lootPool.Count > 0 && Random.value <= effectiveDropChance)
        {
            List<LootPoolItem> filteredPool = new List<LootPoolItem>();
            Geobionte_AI geobionteAI = GetComponent<Geobionte_AI>();
            if (geobionteAI != null)
            {
                bool isSentinel = geobionteAI.IsSentinel;
                foreach (var loot in lootPool)
                {
                    if (loot.itemPrefab == null) continue;
                    string nameLower = loot.itemPrefab.name.ToLower();
                    if (isSentinel && nameLower.Contains("sentinel"))
                    {
                        filteredPool.Add(loot);
                    }
                    else if (!isSentinel && !nameLower.Contains("sentinel"))
                    {
                        filteredPool.Add(loot);
                    }
                }
            }
            else
            {
                filteredPool = lootPool;
            }

            // Ponderação dinâmica de Tier de acordo com a sala atual (GDD §4)
            int currentRoom = RunManager.instance != null ? RunManager.instance.currentRoomNumber : 1;

            float totalWeight = 0f;
            List<float> adjustedWeights = new List<float>();

            for (int idx = 0; idx < filteredPool.Count; idx++)
            {
                var loot = filteredPool[idx];
                if (loot.itemPrefab == null || loot.weight <= 0f)
                {
                    adjustedWeights.Add(0f);
                    continue;
                }

                float weightMult = 1f;
                int itemTier = ResolveLootTier(loot.itemPrefab, idx);

                if (isBossOrElites)
                {
                    // Elites priorizam T2, T3 e T4 (sem T1)
                    if (itemTier == 1) weightMult = 0f;
                    else if (itemTier == 2) weightMult = 0.5f;
                    else if (itemTier == 3) weightMult = 1.5f;
                    else if (itemTier == 4) weightMult = 2.0f;
                }
                else
                {
                    if (currentRoom <= 4)
                    {
                        if (itemTier == 1) weightMult = 1.2f;
                        else if (itemTier == 2) weightMult = 0.5f;
                        else if (itemTier == 3) weightMult = 0.15f;
                        else if (itemTier == 4) weightMult = 0f;
                    }
                    else if (currentRoom <= 8)
                    {
                        if (itemTier == 1) weightMult = 0.8f;
                        else if (itemTier == 2) weightMult = 1.0f;
                        else if (itemTier == 3) weightMult = 0.5f;
                        else if (itemTier == 4) weightMult = 0.15f;
                    }
                    else
                    {
                        if (itemTier == 1) weightMult = 0.5f;
                        else if (itemTier == 2) weightMult = 1.1f;
                        else if (itemTier == 3) weightMult = 1.0f;
                        else if (itemTier == 4) weightMult = 0.5f;
                    }
                }

                if (hasGreed)
                {
                    // Ganância: Melhora as chances de sortear Tiers superiores (T2, T3, T4)!
                    if (itemTier == 1) weightMult *= 1.2f;
                    else if (itemTier == 2) weightMult *= 2.5f;
                    else if (itemTier == 3) weightMult *= 3.5f;
                    else if (itemTier == 4) weightMult *= 5.0f;
                }

                float finalW = loot.weight * weightMult;
                adjustedWeights.Add(finalW);
                totalWeight += finalW;
            }

            if (totalWeight > 0f)
            {
                float randomVal = Random.Range(0f, totalWeight);
                float currentSum = 0f;
                GameObject selectedPrefab = null;

                for (int i = 0; i < filteredPool.Count; i++)
                {
                    float w = adjustedWeights[i];
                    if (w <= 0f) continue;
                    
                    currentSum += w;
                    if (randomVal <= currentSum)
                    {
                        selectedPrefab = filteredPool[i].itemPrefab;
                        break;
                    }
                }

                if (selectedPrefab != null)
                {
                    int finalItemAmount = itemAmount; // Não duplica itens no chão; a Ganância aumenta a chance de drop e tiers!

                    for (int i = 0; i < finalItemAmount; i++)
                    {
                        Vector3 offset = Random.insideUnitSphere * spawnRadius;
                        offset.y = 0;
                        
                        SpawnDrop(selectedPrefab, basePosition + offset);
                    }

                    Debug.Log("[ENEMY DROPS] Item sorteado na roleta (spawn sujeito à validação): " + selectedPrefab.name);
                }
            }
        }
        else if (lootPool == null || lootPool.Count == 0)
        {
            Debug.LogWarning("[ENEMY DROPS] lootPool está vazio! Adicione opções no Inspector.");
        }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ENEMY DROPS] Falha ao processar SpawnDrops em {gameObject.name}: {ex}");
        }
    }

    GameObject SpawnDrop(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return null;

        // Se o prefab atribuído for um objeto filho (ex.: Visual), recupera a raiz com o pickup
        GameObject prefabToSpawn = prefab;
        if (prefabToSpawn.transform.parent != null)
        {
            prefabToSpawn = prefabToSpawn.transform.root.gameObject;
        }

        Vector3 safePosition = position;
        if (validateDropPositionWithNavMesh && !TryFindDropPosition(position, out safePosition, out string rejection))
        {
            Debug.LogWarning($"[ENEMY DROPS] [NavMesh] Drop '{prefabToSpawn.name}' cancelado em '{name}' " +
                $"(cena={gameObject.scene.name}, origem={transform.position}, agente={dropAgentTypeId}, " +
                $"areas={dropNavMeshAreaMask}, mascaraFisica={dropCollisionMask.value}, raio={navMeshSearchRadius}m): " +
                rejection, this);
            return null;
        }

        GameObject drop = Instantiate(prefabToSpawn, safePosition, Quaternion.identity);

        // Se o drop não tiver CharacteristicItemPickup nem ItemPickup, recupera ou anexa dinamicamente
        // Ignora objetos com EssencePickup para não interferir na física e coleta de essência
        if (drop.GetComponent<EssencePickup>() == null &&
            drop.GetComponent<CharacteristicItemPickup>() == null && 
            drop.GetComponent<ItemPickup>() == null)
        {
            var childPickup = drop.GetComponentInChildren<CharacteristicItemPickup>(true);
            if (childPickup == null)
            {
                var pickup = drop.AddComponent<CharacteristicItemPickup>();
                string resolvedId = prefabToSpawn.name.Replace("(Clone)", "").Trim();
                if (resolvedId.Equals("Visual", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (gameObject.name.ToLower().Contains("goblin"))
                        resolvedId = "goblin_coin_t1";
                }
                pickup.itemId = resolvedId;
                pickup.itemName = resolvedId;
                SphereCollider col = drop.GetComponent<SphereCollider>();
                if (col == null) col = drop.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 1.25f;
                Debug.LogWarning($"[ENEMY DROPS] Drop '{drop.name}' sem pickup! Componente adicionado: {pickup.itemId}");
            }
        }

        // Configura rigidbody para não ter drift
        Rigidbody rb = drop.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Alto drag para parar rápido quando atingir o chão
            rb.linearDamping = 5f;
            rb.angularDamping = 5f;
            
            // Apenas impulso vertical (sem movimento lateral)
            Vector3 upForce = Vector3.up * launchForce;
            rb.AddForce(upForce, ForceMode.Impulse);
        }

        return drop;
    }

    // Tentativas determinísticas: não consomem Random e não alteram as rolagens de loot.
    private bool TryFindDropPosition(Vector3 requestedPosition, out Vector3 position, out string rejection)
    {
        position = default;
        rejection = null;
        Vector3 origin = transform.position;
        float radius = Mathf.Max(0.1f, navMeshSearchRadius);
        float sampleRadius = Mathf.Clamp(navMeshSampleRadius, 0.05f, radius);
        int attempts = Mathf.Clamp(dropPositionAttempts, 1, 16);
        var filter = new NavMeshQueryFilter { agentTypeID = dropAgentTypeId, areaMask = dropNavMeshAreaMask };

        // Âncora local: candidatos não podem atravessar bordas ou ilhas da NavMesh.
        if (!NavMesh.SamplePosition(origin, out NavMeshHit anchor, radius, filter))
        {
            rejection = $"0 candidatos testados; {DiagnoseMissingDropAnchor(origin, radius, filter)}";
            return false;
        }
        if (Mathf.Abs(anchor.position.y - origin.y) > Mathf.Max(0f, maxDropVerticalDistance))
        {
            rejection = $"0 candidatos testados; ANCORA_ALTURA: origemY={origin.y:F3}, " +
                $"navY={anchor.position.y:F3}, limite={maxDropVerticalDistance:F3}m.";
            return false;
        }

        int sampleMisses = 0, outsideRadius = 0, wrongHeight = 0, invalidRoute = 0;
        int rejectedGround = 0, obstacles = 0, blockedRoutes = 0;
        string groundDetail = "-", obstacleDetail = "-", routeDetail = "-";
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = requestedPosition;
            if (i == 1) candidate = origin;
            else if (i > 1)
            {
                float angle = (i - 2) * 2.39996323f; // Espiral de ângulo áureo, limitada ao raio.
                float distance = radius * Mathf.Sqrt((i - 1f) / (attempts - 1f));
                candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            }
            // O raio local não deve ser consumido pela altura do pivô acima do piso.
            // O limite vertical em relação ao inimigo continua sendo validado abaixo.
            candidate.y = anchor.position.y;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, filter))
            {
                sampleMisses++;
                continue;
            }
            Vector3 delta = hit.position - origin;
            if (new Vector2(delta.x, delta.z).sqrMagnitude > radius * radius)
            {
                outsideRadius++;
                continue;
            }
            if (Mathf.Abs(delta.y) > Mathf.Max(0f, maxDropVerticalDistance))
            {
                wrongHeight++;
                continue;
            }
            if (NavMesh.Raycast(anchor.position, hit.position, out NavMeshHit boundary, filter))
            {
                invalidRoute++;
                continue;
            }
            if (!HasDropGround(hit.position, out string groundReason))
            {
                rejectedGround++;
                groundDetail = groundReason;
                continue;
            }

            float clearance = Mathf.Max(0.05f, dropClearanceRadius);
            Vector3 bottom = hit.position + Vector3.up * (clearance + 0.05f);
            Vector3 spawn = hit.position + Vector3.up * Mathf.Max(spawnHeight, clearance + 0.05f);
            // Espaço do solo até acima do spawn: rejeita pedras, troncos e tetos baixos.
            Vector3 top = spawn + Vector3.up * 1.2f;
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, clearance, dropOverlapBuffer,
                dropCollisionMask, QueryTriggerInteraction.Ignore);
            bool blocked = count == dropOverlapBuffer.Length; // Saturação: resultado incompleto, rejeitar.
            if (blocked) obstacleDetail = "buffer de overlap saturado (32)";
            for (int j = 0; j < count && !blocked; j++)
            {
                if (IsDyingEnemyCollider(dropOverlapBuffer[j])) continue;
                blocked = true;
                obstacleDetail = DescribeDropCollider(dropOverlapBuffer[j]);
            }
            if (blocked)
            {
                obstacles++;
                continue;
            }
            if (!HasClearDropRoute(origin + Vector3.up * (spawn.y - origin.y), spawn, out string routeReason))
            {
                blockedRoutes++;
                routeDetail = routeReason;
                continue;
            }

            position = spawn;
            return true;
        }

        rejection = $"{attempts} candidatos testados; SEM_AMOSTRA_LOCAL={sampleMisses} (raio={sampleRadius}m), " +
            $"FORA_DO_RAIO={outsideRadius}, ALTURA_CANDIDATO={wrongHeight}, CAMINHO_NAVMESH_INVALIDO={invalidRoute}, " +
            $"CHAO_REJEITADO={rejectedGround} (ultimo: {groundDetail}), " +
            $"OBSTACULO={obstacles} (ultimo: {obstacleDetail}), ROTA_FISICA_BLOQUEADA={blockedRoutes} (ultimo: {routeDetail}).";
        return false; // Nunca instancia sem validar no modo NavMesh.
    }

    // Consultas adicionais apenas para diagnosticar uma âncora ausente; nunca viram fallback.
    private string DiagnoseMissingDropAnchor(Vector3 origin, float radius, NavMeshQueryFilter filter)
    {
        if (NavMesh.GetSettingsByID(filter.agentTypeID).agentTypeID == -1)
            return "AGENTE_INCOMPATIVEL: tipo de agente não cadastrado no projeto.";
        if (filter.areaMask == 0)
            return "AREAS_INCOMPATIVEIS: máscara de áreas vazia.";
        if (NavMesh.CalculateTriangulation().indices.Length == 0)
            return "NAVMESH_AUSENTE: nenhuma triangulação carregada; verificar bake e ativação da Surface.";

        filter.areaMask = NavMesh.AllAreas;
        if (NavMesh.SamplePosition(origin, out NavMeshHit sameAgent, radius, filter))
            return $"AREAS_INCOMPATIVEIS: há NavMesh local para este agente, mas fora da máscara permitida (areaHit={sameAgent.mask}).";

        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            int otherAgent = NavMesh.GetSettingsByIndex(i).agentTypeID;
            if (otherAgent == dropAgentTypeId) continue;
            filter.agentTypeID = otherAgent;
            if (NavMesh.SamplePosition(origin, out NavMeshHit otherHit, radius, filter))
                return $"AGENTE_INCOMPATIVEL_LOCAL: encontrada NavMesh apenas com outro agente (id={otherAgent}, pos={otherHit.position}).";
        }
        return "NAVMESH_SEM_COBERTURA_LOCAL: há triangulação carregada, mas nenhuma amostra no raio 3D da origem; verificar posição, altura e cobertura do bake.";
    }

    private bool IsDyingEnemyCollider(Collider collider)
    {
        return collider != null && (collider.transform == transform || collider.transform.IsChildOf(transform));
    }

    private static string DescribeDropCollider(Collider collider)
    {
        return collider == null ? "collider ausente" :
            $"'{collider.name}' (id={collider.GetInstanceID()}, layer={collider.gameObject.layer}, tipo={collider.GetType().Name})";
    }

    private bool HasDropGround(Vector3 point, out string rejection)
    {
        rejection = null;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.2f, Vector3.down, dropRayBuffer,
            0.4f, dropCollisionMask, QueryTriggerInteraction.Ignore);
        if (count == dropRayBuffer.Length)
        {
            rejection = "buffer de chão saturado (32)";
            return false;
        }
        float closest = float.PositiveInfinity;
        RaycastHit ground = default;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = dropRayBuffer[i];
            if (IsDyingEnemyCollider(hit.collider) || hit.distance >= closest) continue;
            closest = hit.distance;
            ground = hit;
        }
        if (float.IsPositiveInfinity(closest))
        {
            rejection = $"sem collider de chão a +/-0.2m do NavMesh em {point}; conferir máscara física e alinhamento do bake";
            return false;
        }
        if (ground.normal.y < 0.7f ||
            ground.collider.GetComponentInParent<DummyHealth>() != null ||
            ground.collider.GetComponentInParent<ShardSwarmHealth>() != null ||
            ground.collider.GetComponentInParent<PlayerHealth>() != null)
        {
            rejection = $"normalY={ground.normal.y:F3} (min=0.7) ou collider de personagem: {DescribeDropCollider(ground.collider)}";
            return false;
        }
        return true;
    }

    private bool HasClearDropRoute(Vector3 from, Vector3 to, out string rejection)
    {
        rejection = null;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < 0.01f) return true;
        int count = Physics.RaycastNonAlloc(from, direction / distance, dropRayBuffer, distance,
            dropCollisionMask, QueryTriggerInteraction.Ignore);
        if (count == dropRayBuffer.Length)
        {
            rejection = "buffer de rota saturado (32)";
            return false;
        }
        for (int i = 0; i < count; i++)
        {
            if (IsDyingEnemyCollider(dropRayBuffer[i].collider)) continue;
            rejection = DescribeDropCollider(dropRayBuffer[i].collider);
            return false;
        }
        return true;
    }

    public bool IsEliteMob()
    {
        if (isEliteDrops) return true;
        if (GetComponent<EliteVariantConfig>() != null) return true;
        if (health != null && health.isElite) return true;
        ShardSwarmHealth ssh = GetComponent<ShardSwarmHealth>();
        if (ssh != null && ssh.isElite) return true;
        string n = gameObject.name.ToLower();
        if (n.Contains("elite") || n.Contains("rebornelite")) return true;
        return false;
    }

    /// <summary>
    /// Resolve o Tier (1 a 4) de um item do lootPool lendo diretamente o ItemData.tier via CharacteristicItemPickup,
    /// com fallback seguro do maior sufixo (_t4) para o menor (_t1) e para o índice no lootPool.
    /// Corrige o bug em que Contains("t1") capturava todos os prefabs do Peixe ("peixe_escama_t1 1/2/3") e a Placa T2 do Geobionte ("bismutado_placa_t1").
    /// </summary>
    public int ResolveLootTier(GameObject prefab, int poolIndex = -1)
    {
        if (prefab == null) return Mathf.Clamp(poolIndex + 1, 1, 4);

        // 1. Fonte da verdade: CharacteristicItemPickup -> ItemDatabase / Resources ItemData.tier
        var pickup = prefab.GetComponent<CharacteristicItemPickup>() ?? prefab.GetComponentInChildren<CharacteristicItemPickup>(true);
        if (pickup != null && !string.IsNullOrEmpty(pickup.itemId))
        {
            string id = pickup.itemId.Trim();
            ItemData data = null;
            if (ItemDatabase.Instance != null)
            {
                data = ItemDatabase.Instance.GetItemData(id);
            }
            if (data == null)
            {
                data = Resources.Load<ItemData>("ItemData/" + id);
            }
            if (data != null)
            {
                return Mathf.Clamp((int)data.tier + 1, 1, 4);
            }

            // Fallback pelo próprio itemId do pickup (ex.: "04px", "bismutado_placa_t2")
            string idLower = id.ToLower();
            if (idLower.Contains("_t4") || idLower.EndsWith("t4") || idLower.StartsWith("04")) return 4;
            if (idLower.Contains("_t3") || idLower.EndsWith("t3") || idLower.StartsWith("03")) return 3;
            if (idLower.Contains("_t2") || idLower.EndsWith("t2") || idLower.StartsWith("02")) return 2;
            if (idLower.Contains("_t1") || idLower.EndsWith("t1") || idLower.StartsWith("01")) return 1;
        }

        // 2. Fallback seguro pelo nome do prefab (avaliando do maior sufixo _t4 para o menor _t1)
        string name = prefab.name.ToLower();
        if (name.Contains("peixe_escama_t1 3") || name.EndsWith(" 3")) return 4;
        if (name.Contains("peixe_escama_t1 2") || name.EndsWith(" 2")) return 3;
        if (name.Contains("peixe_escama_t1 1") || name.EndsWith(" 1")) return 2;

        if (name.Contains("_t4") || name.Contains("tier4") || name.EndsWith("t4") || name.EndsWith("item4") || name.Contains("heart") || name.Contains("rotor") || name.Contains("catalyst") || name.Contains("bomb") || name.Contains("matrix") || name.Contains("beacon") || name.Contains("monolith") || name.Contains("osso")) return 4;
        if (name.Contains("_t3") || name.Contains("tier3") || name.EndsWith("t3") || name.EndsWith("item3") || name.Contains("core") || name.Contains("amulet") || name.Contains("essence") || name.Contains("fang") || name.Contains("tail") || name.Contains("prism") || name.Contains("ancient") || name.Contains("cerebro") || name.Contains("sight")) return 3;
        if (name.Contains("_t2") || name.Contains("tier2") || name.EndsWith("t2") || name.EndsWith("item2") || name.Contains("placa") || name.Contains("plate") || name.Contains("silk") || name.Contains("rune") || name.Contains("claw") || name.Contains("lens") || name.Contains("carved") || name.Contains("cora") || name.Contains("espinhos") || name.Contains("bra")) return 2;
        if (name.Contains("_t1") || name.Contains("tier1") || name.EndsWith("t1") || name.EndsWith("item1") || name.Contains("chip") || name.Contains("dust") || name.Contains("leg") || name.Contains("scale") || name.Contains("coin") || name.Contains("garras") || name.Contains("stone") || name.Contains("splinter")) return 1;

        if (poolIndex >= 0) return Mathf.Clamp(poolIndex + 1, 1, 4);
        return 1;
    }

    private int GetItemTier(GameObject prefab)
    {
        return ResolveLootTier(prefab, -1);
    }

    private void SpawnEliteDrops(Vector3 basePosition, bool hasGreed)
    {
        if (lootPool == null || lootPool.Count == 0) return;

        List<GameObject> t2Items = new List<GameObject>();
        List<GameObject> t3Items = new List<GameObject>();
        List<GameObject> t4Items = new List<GameObject>();

        for (int idx = 0; idx < lootPool.Count; idx++)
        {
            var loot = lootPool[idx];
            if (loot == null || loot.itemPrefab == null) continue;
            int tier = ResolveLootTier(loot.itemPrefab, idx);
            if (tier == 2) t2Items.Add(loot.itemPrefab);
            else if (tier == 3) t3Items.Add(loot.itemPrefab);
            else if (tier == 4) t4Items.Add(loot.itemPrefab);
        }

        // Fallbacks se o lootPool tiver organização não padrão
        if (t2Items.Count == 0 && lootPool.Count > 1 && lootPool[1].itemPrefab != null) 
            t2Items.Add(lootPool[1].itemPrefab);
        if (t3Items.Count == 0 && lootPool.Count > 2 && lootPool[2].itemPrefab != null) 
            t3Items.Add(lootPool[2].itemPrefab);
        if (t4Items.Count == 0 && lootPool.Count > 3 && lootPool[3].itemPrefab != null) 
            t4Items.Add(lootPool[3].itemPrefab);

        // Chances solicitadas pelo usuário: Tier 2: 80%, Tier 3: 40%, Tier 4: 20%
        float t2Chance = hasGreed ? 0.95f : 0.80f;
        float t3Chance = hasGreed ? 0.60f : 0.40f;
        float t4Chance = hasGreed ? 0.35f : 0.20f;

        bool droppedAny = false;

        // 1. Rolagem Tier 4 (20%)
        if (t4Items.Count > 0 && Random.value <= t4Chance)
        {
            GameObject selectedT4 = t4Items[Random.Range(0, t4Items.Count)];
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            SpawnDrop(selectedT4, basePosition + offset);
            droppedAny = true;
            Debug.Log($"[ELITE DROPS] ★ ROLAGEM DO TIER 4 COM SUCESSO (spawn sujeito à validação)! ({t4Chance * 100:F0}%): {selectedT4.name}");
        }

        // 2. Rolagem Tier 3 (40%)
        if (t3Items.Count > 0 && Random.value <= t3Chance)
        {
            GameObject selectedT3 = t3Items[Random.Range(0, t3Items.Count)];
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            SpawnDrop(selectedT3, basePosition + offset);
            droppedAny = true;
            Debug.Log($"[ELITE DROPS] ★ ROLAGEM DO TIER 3 COM SUCESSO (spawn sujeito à validação)! ({t3Chance * 100:F0}%): {selectedT3.name}");
        }

        // 3. Rolagem Tier 2 (80%)
        if (t2Items.Count > 0 && Random.value <= t2Chance)
        {
            GameObject selectedT2 = t2Items[Random.Range(0, t2Items.Count)];
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            SpawnDrop(selectedT2, basePosition + offset);
            droppedAny = true;
            Debug.Log($"[ELITE DROPS] ★ ROLAGEM DO TIER 2 COM SUCESSO (spawn sujeito à validação)! ({t2Chance * 100:F0}%): {selectedT2.name}");
        }

        // 4. Proteção contra azar (garante ao menos 1 item T2+ se todas as rolagens falharem)
        if (!droppedAny)
        {
            GameObject fallback = t2Items.Count > 0 ? t2Items[Random.Range(0, t2Items.Count)] :
                                 (t3Items.Count > 0 ? t3Items[0] : (t4Items.Count > 0 ? t4Items[0] : null));
            if (fallback != null)
            {
                Vector3 offset = Random.insideUnitSphere * spawnRadius;
                offset.y = 0;
                SpawnDrop(fallback, basePosition + offset);
                Debug.Log($"[ELITE DROPS] Proteção contra azar ativada: item sorteado (spawn sujeito à validação) ({fallback.name})");
            }
        }
    }
}
