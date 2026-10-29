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

    [Header("Inventário (Sem Stacking — 1 Item por Slot)")]
    [Tooltip("Lista de slots de itens da run (cada slot = 1 item individual)")]
    [SerializeField] private List<string> itemSlots = new List<string>();

    public const int MAX_INVENTORY_CAPACITY = 5;

    void Awake()
    {
        maxSlots = MAX_INVENTORY_CAPACITY;
        if (itemSlots.Count > maxSlots)
        {
            itemSlots.RemoveRange(maxSlots, itemSlots.Count - maxSlots);
        }
    }

    void Start()
    {
        maxSlots = MAX_INVENTORY_CAPACITY;
        if (itemSlots.Count > maxSlots)
        {
            itemSlots.RemoveRange(maxSlots, itemSlots.Count - maxSlots);
        }

        // Garante que sistemas de inventário existam em qualquer cena.
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
    [Tooltip("Número máximo de slots (itens individuais)")]
    [SerializeField] private int maxSlots = 5;

    /// <summary>Número máximo de slots disponíveis</summary>
    public int MaxSlots => maxSlots;

    /// <summary>Número de slots atualmente ocupados</summary>
    public int OccupiedSlots => itemSlots.Count;

    /// <summary>Verifica se o inventário está cheio</summary>
    public bool IsFull => itemSlots.Count >= maxSlots;

    [Header("Eventos")]
    public UnityEngine.Events.UnityEvent<string, int> onItemAdded;
    public UnityEngine.Events.UnityEvent<string, int> onItemRemoved;

    [Tooltip("Disparado quando qualquer mudança ocorre no inventário (add, remove, resize)")]
    public UnityEngine.Events.UnityEvent onInventoryChanged;

    /// <summary>
    /// Adiciona um item ao inventário.
    /// Cada item ocupa seu próprio slot dedicado (SEM STACKING).
    /// Retorna true se adicionado com sucesso, false se o inventário estiver cheio.
    /// </summary>
    public bool AddItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId)) return false;

        int addedCount = 0;
        for (int i = 0; i < amount; i++)
        {
            if (itemSlots.Count < maxSlots)
            {
                itemSlots.Add(itemId);
                addedCount++;
            }
            else
            {
                Debug.Log($"[INVENTORY] Inventário cheio! ({itemSlots.Count}/{maxSlots} slots). Não foi possível adicionar mais {itemId}.");
                break;
            }
        }

        if (addedCount > 0)
        {
            Debug.Log($"[INVENTORY] +{addedCount} {itemId} (Slot separado) | Total do item: {GetItemCount(itemId)} | Slots: {itemSlots.Count}/{maxSlots}");
            PlayItemPickupSound();
            onItemAdded?.Invoke(itemId, GetItemCount(itemId));
            onInventoryChanged?.Invoke();

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AdvanceQuest(QuestType.CollectItem, itemId, addedCount);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Remove determinada quantidade de um item do inventário
    /// </summary>
    public bool RemoveItem(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId)) return false;

        int count = GetItemCount(itemId);
        if (count < amount)
        {
            Debug.Log($"[INVENTORY] Não possui {amount}x {itemId} (tem apenas {count})");
            return false;
        }

        int removed = 0;
        for (int i = itemSlots.Count - 1; i >= 0 && removed < amount; i--)
        {
            if (itemSlots[i] == itemId)
            {
                itemSlots.RemoveAt(i);
                removed++;
            }
        }

        Debug.Log($"[INVENTORY] -{amount} {itemId} | Restantes: {GetItemCount(itemId)} | Slots ocupados: {itemSlots.Count}/{maxSlots}");
        onItemRemoved?.Invoke(itemId, GetItemCount(itemId));
        onInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Retorna a quantidade total de determinado item no inventário
    /// </summary>
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        int count = 0;
        for (int i = 0; i < itemSlots.Count; i++)
        {
            if (itemSlots[i] == itemId) count++;
        }
        return count;
    }

    /// <summary>
    /// Verifica se possui determinada quantidade de um item
    /// </summary>
    public bool HasItem(string itemId, int amount = 1)
    {
        return GetItemCount(itemId) >= amount;
    }

    /// <summary>
    /// Retorna todos os itens agregados em um dicionário (para compatibilidade com SaveManager e Crafting)
    /// </summary>
    public Dictionary<string, int> GetAllItems()
    {
        Dictionary<string, int> dict = new Dictionary<string, int>();
        for (int i = 0; i < itemSlots.Count; i++)
        {
            string id = itemSlots[i];
            if (!dict.ContainsKey(id)) dict[id] = 0;
            dict[id]++;
        }
        return dict;
    }

    /// <summary>
    /// Retorna a lista ordenada dos slots individuais (cada entrada = 1 item)
    /// </summary>
    public List<string> GetItemSlots()
    {
        return new List<string>(itemSlots);
    }

    /// <summary>
    /// Limpa todos os itens do inventário de run. Útil para reset de testes rápidos sem reiniciar a run.
    /// </summary>
    public void ClearInventory()
    {
        itemSlots.Clear();
        onInventoryChanged?.Invoke();
        Debug.Log("[INVENTORY] Todos os itens da mochila foram limpos!");
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
        // Mantém travado em 5 slots conforme a regra de inventário
        maxSlots = MAX_INVENTORY_CAPACITY;
        if (itemSlots.Count > maxSlots)
        {
            itemSlots.RemoveRange(maxSlots, itemSlots.Count - maxSlots);
        }
        onInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Define diretamente o número máximo de slots.
    /// Use com cuidado — IncreaseMaxSlots() é preferível.
    /// </summary>
    public void SetMaxSlots(int newMax)
    {
        // Limita estritamente ao teto de 5 slots
        maxSlots = Mathf.Clamp(newMax, 1, MAX_INVENTORY_CAPACITY);
        if (itemSlots.Count > maxSlots)
        {
            itemSlots.RemoveRange(maxSlots, itemSlots.Count - maxSlots);
        }
        Debug.Log("[INVENTORY] Capacidade definida para " + maxSlots + " slots");
        onInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Debug: Lista todos os itens no console
    /// </summary>
    public void DebugListItems()
    {
        Debug.Log("=== INVENTÁRIO (" + itemSlots.Count + "/" + maxSlots + " slots) ===");
        for (int i = 0; i < itemSlots.Count; i++)
        {
            Debug.Log($"Slot #{i}: {itemSlots[i]}");
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
