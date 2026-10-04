using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Inventário do Player para itens característicos
/// Armazena materiais dropados por inimigos para crafting/buffs futuros
/// 
/// SISTEMA DE SLOTS:
/// - Cada tipo de item ocupa 1 slot (quantidade ilimitada por slot)
/// - maxSlots define o limite total de tipos diferentes de itens
/// - Use IncreaseMaxSlots() para expandir a capacidade (ex: upgrade na base)
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Áudio — Inventário e Coleta")]
    [Tooltip("Som reproduzido ao abrir o inventário")]
    public AudioClip openInventorySound;
    [Tooltip("Volume do som de abrir o inventário")]
    [Range(0f, 1f)]
    public float openInventorySoundVolume = 0.8f;

    [Tooltip("Som reproduzido ao fechar o inventário")]
    public AudioClip closeInventorySound;
    [Tooltip("Volume do som de fechar o inventário")]
    [Range(0f, 1f)]
    public float closeInventorySoundVolume = 0.8f;

    [Tooltip("Vetor de sons reproduzidos ao coletar/pegar um item do chão (seleciona aleatoriamente)")]
    public AudioClip[] itemPickupSounds;
    [Tooltip("Volume dos sons de coleta de item")]
    [Range(0f, 1f)]
    public float itemPickupSoundVolume = 0.8f;

    [Header("Inventário")]
    [Tooltip("Dicionário de itens coletados (ID -> Quantidade)")]
    private Dictionary<string, int> items = new Dictionary<string, int>();

    void Start()
    {
        // Garante que sistemas de inventário existam em qualquer cena.
        // O PlayerInventory vive no Player.prefab (DontDestroyOnLoad),
        // mas ItemDatabase e InventoryUI podem não estar na cena atual.
        EnsureItemDatabaseExists();
        EnsureInventoryUIExists();
    }

    /// <summary>
    /// Cria o ItemDatabase automaticamente se não existir.
    /// Sem ele, os itens aparecem sem sprite, nome ou cor de tier.
    /// </summary>
    void EnsureItemDatabaseExists()
    {
        ItemDatabase existing = FindFirstObjectByType<ItemDatabase>();
        if (existing != null) return;

        // Tenta carregar o prefab completo do Resources (que já tem a lista preenchida no Inspector)
        GameObject prefab = Resources.Load<GameObject>("ItemDatabase");
        if (prefab != null)
        {
            GameObject dbObj = Instantiate(prefab);
            dbObj.name = "ItemDatabase_Auto";
            Debug.Log("[INVENTORY] ItemDatabase instanciado via prefab do Resources.");
        }
        else
        {
            // Fallback extremo
            GameObject dbObj = new GameObject("ItemDatabase_Auto");
            dbObj.AddComponent<ItemDatabase>();
            Debug.Log("[INVENTORY] ItemDatabase criado vazio como fallback.");
        }
    }

    void EnsureInventoryUIExists()
    {
        InventoryUI existing = FindFirstObjectByType<InventoryUI>();
        if (existing != null) return;

        // Cria um novo InventoryUI (ele faz DontDestroyOnLoad no próprio Awake)
        GameObject inventoryUIObj = new GameObject("InventoryUI_Auto");
        inventoryUIObj.AddComponent<InventoryUI>();
        Debug.Log("[INVENTORY] InventoryUI criado automaticamente pelo PlayerInventory.");
    }

    [Header("Capacidade")]
    [Tooltip("Número máximo de slots (tipos diferentes de itens)")]
    [SerializeField] private int maxSlots = 10;

    /// <summary>Número máximo de slots disponíveis</summary>
    public int MaxSlots => maxSlots;

    /// <summary>Número de slots atualmente ocupados</summary>
    public int OccupiedSlots => items.Count;

    /// <summary>Verifica se o inventário está cheio</summary>
    public bool IsFull => items.Count >= maxSlots;

    [Header("Eventos")]
    public UnityEngine.Events.UnityEvent<string, int> onItemAdded;
    public UnityEngine.Events.UnityEvent<string, int> onItemRemoved;

    [Tooltip("Disparado quando qualquer mudança ocorre no inventário (add, remove, resize)")]
    public UnityEngine.Events.UnityEvent onInventoryChanged;

    /// <summary>
    /// Adiciona um item ao inventário.
    /// Retorna true se adicionado com sucesso, false se o inventário estiver cheio.
    /// </summary>
    public bool AddItem(string itemId, int amount = 1)
    {
        // Se o item já existe no inventário, apenas incrementa a quantidade (não gasta slot)
        if (items.ContainsKey(itemId))
        {
            items[itemId] += amount;
        }
        // Se é um item novo, verifica se há slot disponível
        else if (items.Count < maxSlots)
        {
            items[itemId] = amount;
        }
        else
        {
            Debug.Log("[INVENTORY] Inventário cheio! (" + items.Count + "/" + maxSlots + " slots) - Não foi possível adicionar: " + itemId);
            return false;
        }

        Debug.Log("[INVENTORY] +" + amount + " " + itemId + " | Total: " + items[itemId] + " | Slots: " + items.Count + "/" + maxSlots);
        PlayItemPickupSound();
        onItemAdded?.Invoke(itemId, items[itemId]);
        onInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Remove um item do inventário
    /// </summary>
    public bool RemoveItem(string itemId, int amount = 1)
    {
        if (!items.ContainsKey(itemId) || items[itemId] < amount)
        {
            Debug.Log("[INVENTORY] Não possui " + amount + "x " + itemId);
            return false;
        }

        items[itemId] -= amount;
        
        if (items[itemId] <= 0)
        {
            items.Remove(itemId);
        }

        Debug.Log("[INVENTORY] -" + amount + " " + itemId);
        onItemRemoved?.Invoke(itemId, GetItemCount(itemId));
        onInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Retorna a quantidade de um item
    /// </summary>
    public int GetItemCount(string itemId)
    {
        return items.ContainsKey(itemId) ? items[itemId] : 0;
    }

    /// <summary>
    /// Verifica se possui determinada quantidade de um item
    /// </summary>
    public bool HasItem(string itemId, int amount = 1)
    {
        return GetItemCount(itemId) >= amount;
    }

    /// <summary>
    /// Retorna todos os itens do inventário
    /// </summary>
    public Dictionary<string, int> GetAllItems()
    {
        return new Dictionary<string, int>(items);
    }

    /// <summary>
    /// Aumenta a capacidade máxima de slots do inventário.
    /// Use para upgrades na base, compras de expansão, etc.
    /// 
    /// Exemplo de uso:
    ///   playerInventory.IncreaseMaxSlots(5); // Agora tem 15 slots
    /// </summary>
    public void IncreaseMaxSlots(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[INVENTORY] IncreaseMaxSlots recebeu valor inválido: " + amount);
            return;
        }

        int oldMax = maxSlots;
        maxSlots += amount;
        Debug.Log("[INVENTORY] Capacidade aumentada! " + oldMax + " -> " + maxSlots + " slots");
        onInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Define diretamente o número máximo de slots.
    /// Use com cuidado — IncreaseMaxSlots() é preferível.
    /// </summary>
    public void SetMaxSlots(int newMax)
    {
        if (newMax < 1)
        {
            Debug.LogWarning("[INVENTORY] SetMaxSlots recebeu valor inválido: " + newMax);
            return;
        }

        maxSlots = newMax;
        Debug.Log("[INVENTORY] Capacidade definida para " + maxSlots + " slots");
        onInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Debug: Lista todos os itens no console
    /// </summary>
    public void DebugListItems()
    {
        Debug.Log("=== INVENTÁRIO (" + items.Count + "/" + maxSlots + " slots) ===");
        foreach (var item in items)
        {
            Debug.Log(item.Key + ": " + item.Value);
        }
        Debug.Log("==================");
    }

    // ── Áudio ───────────────────────────────────────────────────────
    public void PlayItemPickupSound()
    {
        if (itemPickupSounds == null || itemPickupSounds.Length == 0) return;
        AudioClip clip = itemPickupSounds[Random.Range(0, itemPickupSounds.Length)];
        if (clip != null)
            Play2DAudio(clip, itemPickupSoundVolume);
    }

    public void PlayOpenInventorySound()
    {
        if (openInventorySound == null) return;
        Play2DAudio(openInventorySound, openInventorySoundVolume);
    }

    public void PlayCloseInventorySound()
    {
        if (closeInventorySound == null) return;
        Play2DAudio(closeInventorySound, closeInventorySoundVolume);
    }

    private void Play2DAudio(AudioClip clip, float volume)
    {
        if (clip == null) return;
        GameObject audioObj = new GameObject("TempInventoryAudio");
        audioObj.transform.position = transform.position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume;
        aSource.spatialBlend = 0f; // 2D som de UI/Player
        aSource.Play();
        Destroy(audioObj, clip.length + 0.1f);
    }
}
