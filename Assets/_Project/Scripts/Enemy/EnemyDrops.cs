using UnityEngine;
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
    [Tooltip("Chance do inimigo dropar ALGUM item da lista (0-1)")]
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
            Debug.Log("[ENEMY DROPS] SpawnDrops chamado para " + gameObject.name);
            
            Vector3 basePosition = transform.position;
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
            // Aplica o multiplicador de sala (d é o essenceAmount base)
            float roomMultiplier = RunManager.instance != null
                ? RunManager.instance.GetEssenceMultiplier()
                : 1f;

            int scaledBase  = Mathf.RoundToInt(essenceAmount * roomMultiplier);
            int finalEssence = scaledBase + Random.Range(-essenceVariation, essenceVariation + 1);
            finalEssence = Mathf.Max(1, finalEssence);

            if (doubleEssence || hasGreed) finalEssence *= 2; // Dobra a essência com Ganância!

            GameObject essence = SpawnDrop(essencePrefab, basePosition);
            
            // Configura a quantidade de essência
            EssencePickup essenceScript = essence.GetComponent<EssencePickup>();
            if (essenceScript != null)
            {
                essenceScript.essenceValue = finalEssence;
            }

            Debug.Log($"[ENEMY DROPS] Dropou {finalEssence} de essência! (base:{essenceAmount} × mult:{roomMultiplier:F2} | sala {RunManager.instance?.currentRoomNumber})");
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
        else if (isTankOrStructure)
        {
            // Totem e Tanques: chance saudável e calibrada (~28%)
            effectiveDropChance = Mathf.Clamp(effectiveDropChance, 0.32f, 0.45f);
        }
        else if (isSupport)
        {
            effectiveDropChance = Mathf.Clamp(effectiveDropChance, 0.30f, 0.40f);
        }
        else if (isRanged)
        {
            effectiveDropChance = Mathf.Clamp(effectiveDropChance, 0.30f, 0.40f);
        }
        else
        {
            // Mobs menores (SharpBlur, Aranha): ajustado de 10% para 25% para não sumir
            effectiveDropChance = Mathf.Clamp(effectiveDropChance, 0.32f, 0.45f);
        }

        // --- PACTO DE GANÂNCIA: Sorte aumentada significativamente em vez de duplicar drop! ---
        if (hasGreed)
        {
            effectiveDropChance = Mathf.Clamp01(effectiveDropChance * 2.5f);
            Debug.Log($"[ENEMY DROPS] A Ganância ativa! Chance de drop amplificada para {effectiveDropChance * 100:F0}%");
        }

        // --- PACTO A SINFONIA DO CAOS: +20% na chance geral de drop! ---
        if (hasChaos)
        {
            effectiveDropChance = Mathf.Clamp01(effectiveDropChance + 0.20f);
            Debug.Log($"[ENEMY DROPS] A Sinfonia do Caos ativa: +20% na chance geral de drop! (Total: {effectiveDropChance * 100:F0}%)");
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

            foreach (var loot in filteredPool)
            {
                if (loot.itemPrefab == null || loot.weight <= 0f)
                {
                    adjustedWeights.Add(0f);
                    continue;
                }

                float weightMult = 1f;
                string pName = loot.itemPrefab.name.ToLower();

                if (isBossOrElites)
                {
                    // Elites priorizam T2, T3 e T4 (sem T1)
                    if (pName.Contains("t1") || pName.Contains("1")) weightMult = 0f;
                    else if (pName.Contains("t2") || pName.Contains("2")) weightMult = 0.5f;
                    else if (pName.Contains("t3") || pName.Contains("3")) weightMult = 1.5f;
                    else if (pName.Contains("t4") || pName.Contains("4")) weightMult = 2.0f;
                }
                else
                {
                    if (currentRoom <= 4)
                    {
                        if (pName.Contains("t1") || pName.Contains("1")) weightMult = 1.2f;
                        else if (pName.Contains("t2") || pName.Contains("2")) weightMult = 0.5f;
                        else if (pName.Contains("t3") || pName.Contains("3")) weightMult = 0.15f;
                        else if (pName.Contains("t4") || pName.Contains("4")) weightMult = 0f;
                    }
                    else if (currentRoom <= 8)
                    {
                        if (pName.Contains("t1") || pName.Contains("1")) weightMult = 0.8f;
                        else if (pName.Contains("t2") || pName.Contains("2")) weightMult = 1.0f;
                        else if (pName.Contains("t3") || pName.Contains("3")) weightMult = 0.5f;
                        else if (pName.Contains("t4") || pName.Contains("4")) weightMult = 0.15f;
                    }
                    else
                    {
                        if (pName.Contains("t1") || pName.Contains("1")) weightMult = 0.5f;
                        else if (pName.Contains("t2") || pName.Contains("2")) weightMult = 1.1f;
                        else if (pName.Contains("t3") || pName.Contains("3")) weightMult = 1.0f;
                        else if (pName.Contains("t4") || pName.Contains("4")) weightMult = 0.5f;
                    }
                }

                if (hasGreed)
                {
                    // Ganância: Melhora as chances de sortear Tiers superiores (T2, T3, T4)!
                    if (pName.Contains("t1") || pName.Contains("1")) weightMult *= 1.2f;
                    else if (pName.Contains("t2") || pName.Contains("2")) weightMult *= 2.5f;
                    else if (pName.Contains("t3") || pName.Contains("3")) weightMult *= 3.5f;
                    else if (pName.Contains("t4") || pName.Contains("4")) weightMult *= 5.0f;
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

                    Debug.Log("[ENEMY DROPS] Dropou item da roleta: " + selectedPrefab.name);
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

        // Se o prefab atribuido for um objeto filho (ex: Visual), recupera a raiz com o pickup
        GameObject prefabToSpawn = prefab;
        if (prefabToSpawn.transform.parent != null)
        {
            prefabToSpawn = prefabToSpawn.transform.root.gameObject;
        }

        GameObject drop = Instantiate(prefabToSpawn, position, Quaternion.identity);

        // Se o drop nao tiver CharacteristicItemPickup nem ItemPickup, recupera ou anexa dinamicamente
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

    private int GetItemTier(GameObject prefab)
    {
        if (prefab == null) return 1;
        string name = prefab.name.ToLower();
        if (name.Contains("_t4") || name.Contains("t4") || name.Contains("tier4") || name.Contains("heart") || name.Contains("rotor") || name.Contains("catalyst") || name.Contains("bomb") || name.Contains("matrix") || name.Contains("beacon") || name.Contains("monolith") || name.Contains("osso")) return 4;
        if (name.Contains("_t3") || name.Contains("t3") || name.Contains("tier3") || name.Contains("core") || name.Contains("amulet") || name.Contains("essence") || name.Contains("fang") || name.Contains("tail") || name.Contains("prism") || name.Contains("ancient") || name.Contains("cerebro") || name.Contains("sight")) return 3;
        if (name.Contains("_t2") || name.Contains("t2") || name.Contains("tier2") || name.Contains("plate") || name.Contains("silk") || name.Contains("rune") || name.Contains("claw") || name.Contains("lens") || name.Contains("carved") || name.Contains("cora") || name.Contains("espinhos") || name.Contains("bra")) return 2;
        if (name.Contains("_t1") || name.Contains("t1") || name.Contains("tier1") || name.Contains("chip") || name.Contains("dust") || name.Contains("leg") || name.Contains("scale") || name.Contains("coin") || name.Contains("garras") || name.Contains("stone") || name.Contains("splinter")) return 1;
        return 1;
    }

    private void SpawnEliteDrops(Vector3 basePosition, bool hasGreed)
    {
        if (lootPool == null || lootPool.Count == 0) return;

        List<GameObject> t2Items = new List<GameObject>();
        List<GameObject> t3Items = new List<GameObject>();
        List<GameObject> t4Items = new List<GameObject>();

        foreach (var loot in lootPool)
        {
            if (loot == null || loot.itemPrefab == null) continue;
            int tier = GetItemTier(loot.itemPrefab);
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
            Debug.Log($"[ELITE DROPS] ★ ROLAGEM TIER 4 SUCESSO! ({t4Chance * 100:F0}%): {selectedT4.name}");
        }

        // 2. Rolagem Tier 3 (40%)
        if (t3Items.Count > 0 && Random.value <= t3Chance)
        {
            GameObject selectedT3 = t3Items[Random.Range(0, t3Items.Count)];
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            SpawnDrop(selectedT3, basePosition + offset);
            droppedAny = true;
            Debug.Log($"[ELITE DROPS] ★ ROLAGEM TIER 3 SUCESSO! ({t3Chance * 100:F0}%): {selectedT3.name}");
        }

        // 3. Rolagem Tier 2 (80%)
        if (t2Items.Count > 0 && Random.value <= t2Chance)
        {
            GameObject selectedT2 = t2Items[Random.Range(0, t2Items.Count)];
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            SpawnDrop(selectedT2, basePosition + offset);
            droppedAny = true;
            Debug.Log($"[ELITE DROPS] ★ ROLAGEM TIER 2 SUCESSO! ({t2Chance * 100:F0}%): {selectedT2.name}");
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
                Debug.Log($"[ELITE DROPS] Proteção de azar ativada: Drop Tier 2 garantido ({fallback.name})");
            }
        }
    }
}
