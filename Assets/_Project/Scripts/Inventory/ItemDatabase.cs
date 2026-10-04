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

    private Dictionary<string, ItemData> itemLookup = new Dictionary<string, ItemData>();

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
        HashSet<string> seenIds = new HashSet<string>();

        foreach (var item in allItems)
        {
            if (item == null) continue;

            if (!ItemIdentityValidator.IsValid(item.itemId))
            {
                Debug.LogError($"[ITEM DATABASE] Item inválido descartado do registro: {item.name} | itemId='{item.itemId}'");
                continue;
            }

            if (seenIds.Contains(item.itemId))
            {
                Debug.LogError("[ITEM DATABASE] ID duplicado detectado e bloqueado: " + item.itemId);
                continue;
            }

            seenIds.Add(item.itemId);
            itemLookup[item.itemId] = item;
        }

        Debug.Log("[ITEM DATABASE] " + itemLookup.Count + " itens registrados.");
    }

    public bool RegisterItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogError("[ITEM DATABASE] Tentativa de registrar ItemData nulo.");
            return false;
        }

        if (!ItemIdentityValidator.IsValid(item.itemId))
        {
            Debug.LogError($"[ITEM DATABASE] ID inválido: '{item.itemId}' em {item.name}. Bloqueado.");
            return false;
        }

        if (itemLookup.ContainsKey(item.itemId) || allItems.Exists(x => x != null && x.itemId == item.itemId))
        {
            Debug.LogError($"[ITEM DATABASE] Conflito de ID detectado: '{item.itemId}'. O item não foi registrado.");
            return false;
        }

        allItems.Add(item);
        itemLookup[item.itemId] = item;
        return true;
    }

    /// <summary>
    /// Busca os dados de um item pelo seu ID.
    /// Retorna null se o item não estiver registrado.
    /// </summary>
    public ItemData GetItemData(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        return itemLookup.TryGetValue(itemId, out var data) ? data : null;
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
