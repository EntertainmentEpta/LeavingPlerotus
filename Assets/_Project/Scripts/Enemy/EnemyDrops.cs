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

    [Header("Item Característico (Roleta)")]
    [Tooltip("Chance do inimigo dropar ALGUM item da lista (0-1)")]
    [Range(0f, 1f)]
    public float globalDropChance = 0.3f;
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
        Debug.Log("[ENEMY DROPS] SpawnDrops chamado para " + gameObject.name);
        
        Vector3 basePosition = transform.position + Vector3.up * spawnHeight;

        // --- SISTEMA DE PACTOS DO JOGADOR ---
        bool doubleLoot = false;
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null && playerHealth.hasDoubleLoot)
        {
            doubleLoot = true;
            Debug.Log("[ENEMY DROPS] A Ganância ativada! Drop em dobro!");
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

            if (doubleLoot) finalEssence *= 2; // Dobra a essência!

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

        // 2. Chance de dropar ALGUM item característico calibrada por Classe (Matriz de Balanceamento GDD)
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
            effectiveDropChance = Mathf.Min(effectiveDropChance, 0.35f); // 35% Tanques e Estruturas
        }
        else if (isSupport)
        {
            effectiveDropChance = Mathf.Min(effectiveDropChance, 0.25f); // 25% Suportes
        }
        else if (isRanged)
        {
            effectiveDropChance = Mathf.Min(effectiveDropChance, 0.18f); // 18% Atiradores
        }
        else
        {
            effectiveDropChance = Mathf.Min(effectiveDropChance, 0.10f); // 10% Mobs Menores (Aranha, SharpBlur)
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
                    int finalItemAmount = doubleLoot ? itemAmount * 2 : itemAmount; // Dobra itens!

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

    GameObject SpawnDrop(GameObject prefab, Vector3 position)
    {
        GameObject drop = Instantiate(prefab, position, Quaternion.identity);

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
}
