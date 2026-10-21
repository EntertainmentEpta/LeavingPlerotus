using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton que registra todos os ItemData do jogo para busca rápida por itemId.
/// Persiste entre cenas via DontDestroyOnLoad.
/// 
/// SETUP:
///   1. Adicione a um GameObject na cena inicial (ou será criado automaticamente).
///   2. Arraste todos os ScriptableObjects de ItemData para a lista "allItems" no Inspector.
///   3. Se a lista estiver vazia, tenta carregar de Resources/ItemData/ automaticamente.
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    private static ItemDatabase _instance;
    public static ItemDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ItemDatabase>();
                if (_instance == null)
                {
                    GameObject prefab = Resources.Load<GameObject>("ItemDatabase");
                    if (prefab != null)
                    {
                        GameObject dbObj = Instantiate(prefab);
                        dbObj.name = "ItemDatabase_Auto";
                        _instance = dbObj.GetComponent<ItemDatabase>();
                        Debug.Log("[ITEM DATABASE] Recriado automaticamente via prefab do Resources.");
                    }
                    else
                    {
                        GameObject dbObj = new GameObject("ItemDatabase_Auto");
                        _instance = dbObj.AddComponent<ItemDatabase>();
                        Debug.Log("[ITEM DATABASE] Recriado automaticamente sob demanda (Vazio).");
                    }
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }
    [Header("Todos os Itens do Jogo")]
    [Tooltip("Arraste todos os ScriptableObjects de ItemData aqui")]
    public List<ItemData> allItems = new List<ItemData>();

    private Dictionary<string, ItemData> itemLookup = new Dictionary<string, ItemData>(System.StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            List<ItemData> loadedList = new List<ItemData>();

            ItemData[] itemDataFolder = Resources.LoadAll<ItemData>("ItemData");
            if (itemDataFolder != null && itemDataFolder.Length > 0) loadedList.AddRange(itemDataFolder);

            ItemData[] spawnItemsFolder = Resources.LoadAll<ItemData>("SpawnItems");
            if (spawnItemsFolder != null && spawnItemsFolder.Length > 0) loadedList.AddRange(spawnItemsFolder);

            if (loadedList.Count == 0)
            {
                ItemData[] fallbackItems = Resources.LoadAll<ItemData>("");
                if (fallbackItems != null && fallbackItems.Length > 0) loadedList.AddRange(fallbackItems);
            }

            if (loadedList.Count > 0)
            {
                foreach (var item in loadedList)
                {
                    if (item != null && !allItems.Contains(item))
                    {
                        allItems.Add(item);
                    }
                }
                Debug.Log($"[ITEM DATABASE] Mesclou {loadedList.Count} itens do Resources. Total agora: {allItems.Count}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ITEM DATABASE] Aviso ao carregar itens do Resources: {ex.Message}");
        }

        // Constrói dicionário para busca rápida por itemId
        RebuildLookup();
    }

    /// <summary>
    /// Reconstrói o dicionário de busca. Chamado no Awake e pode ser chamado
    /// se novos itens forem adicionados em runtime.
    /// </summary>
    public void RebuildLookup()
    {
        itemLookup.Clear();
        foreach (var item in allItems)
        {
            if (item == null) continue;

            if (!string.IsNullOrEmpty(item.itemId))
            {
                itemLookup[item.itemId] = item;
                // Registra versão sem acento (ex: goblin_braço_t2 -> goblin_braco_t2)
                string unaccented = item.itemId.Replace("ç", "c").Replace("ã", "a").Replace("õ", "o").Replace("é", "e");
                if (!itemLookup.ContainsKey(unaccented)) itemLookup[unaccented] = item;
            }

            // Registra pelo nome do asset do ScriptableObject
            if (!string.IsNullOrEmpty(item.name) && !itemLookup.ContainsKey(item.name))
            {
                itemLookup[item.name] = item;
            }
        }

        // Mapeamentos manuais para resiliência de drops e nomes legados
        if (itemLookup.TryGetValue("goblin_trinket_t2", out var gTrinket))
        {
            itemLookup["goblin_braço_t2"] = gTrinket;
            itemLookup["goblin_braco_t2"] = gTrinket;
        }
        else if (itemLookup.TryGetValue("goblin_braço_t2", out var gBraco))
        {
            itemLookup["goblin_trinket_t2"] = gBraco;
            itemLookup["goblin_braco_t2"] = gBraco;
        }

        Debug.Log("[ITEM DATABASE] " + itemLookup.Count + " chaves/itens registrados.");
    }

    /// <summary>
    /// Busca os dados de um item pelo seu ID.
    /// Retorna null se o item não estiver registrado.
    /// </summary>
    public ItemData GetItemData(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (itemLookup.TryGetValue(itemId, out var data)) return data;

        string unaccented = itemId.Replace("ç", "c").Replace("ã", "a").Replace("õ", "o").Replace("é", "e");
        if (itemLookup.TryGetValue(unaccented, out data)) return data;

        return null;
    }

    /// <summary>
    /// Verifica se um item está registrado no banco de dados.
    /// </summary>
    public bool HasItem(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && itemLookup.ContainsKey(itemId);
    }

    /// <summary>
    /// Retorna todos os itens registrados.
    /// </summary>
    public List<ItemData> GetAllItems()
    {
        return new List<ItemData>(allItems);
    }
}
