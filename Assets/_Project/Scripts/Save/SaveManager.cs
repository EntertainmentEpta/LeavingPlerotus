using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Gerencia os dois setores de persistência do jogo.
///
/// ─── SETOR 1 — PROGRESSÃO PERMANENTE ─────────────────────────────────────────
///   SavePersistentData()   Serializa PersistentSaveData em JSON e grava em disco.
///                          Chamado automaticamente pelo OnApplicationQuit.
///
///   LoadPersistentData()   Lê o JSON do disco e aplica ao player atual.
///                          Chamado pelo GameManager logo após RegisterPlayer().
///
/// ─── SETOR 2 — LIMPEZA DE RUN (morte) ────────────────────────────────────────
///   OnPlayerDied()         Remove do PlayerInventory todos os itens de run
///                          (ItemData.returnsToBase == false). Itens de base
///                          (returnsToBase == true) são mantidos intactos.
///                          Chamado por PlayerHealth.Die().
///
/// DEPENDÊNCIAS:
///   - GameManager.instance.currentPlayer  (player persistente)
///   - ItemDatabase.Instance               (lookup de ItemData)
///   - PlayerInventory, PlayerUpgrades     (no GameObject do player)
/// </summary>
public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    public static SaveManager instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SaveManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SaveManager_Auto");
                    _instance = go.AddComponent<SaveManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }
    private const string SaveFileName = "player_progress.json";
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    // ─── CACHE DE DADOS PERSISTENTES (acessível pelo CraftingManager) ────────
    private PersistentSaveData _cachedData;

    /// <summary>
    /// Dados persistentes em memória. Carregados do disco no LoadPersistentData.
    /// O CraftingManager e EquipmentManager leem/escrevem aqui.
    /// </summary>
    public PersistentSaveData CachedData
    {
        get
        {
            if (_cachedData == null)
            {
                if (File.Exists(SaveFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(SaveFilePath);
                        _cachedData = JsonUtility.FromJson<PersistentSaveData>(json);
                        Debug.Log("[SAVE] CachedData carregado automaticamente do disco.");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SAVE] Erro ao carregar CachedData automático: {e.Message}");
                    }
                }

                if (_cachedData == null)
                    _cachedData = new PersistentSaveData();

                // Garantia contra listas nulas pós-desserialização de versões antigas do JSON
                if (_cachedData.baseResources == null) _cachedData.baseResources = new List<ItemSaveEntry>();
                if (_cachedData.purchasedUpgradeIndices == null) _cachedData.purchasedUpgradeIndices = new List<int>();
                if (_cachedData.craftedEquipmentIds == null) _cachedData.craftedEquipmentIds = new List<string>();
                if (_cachedData.equippedEquipmentIds == null) _cachedData.equippedEquipmentIds = new List<string>();
                if (_cachedData.inimigosDescobertos == null) _cachedData.inimigosDescobertos = new List<string>();
                if (_cachedData.completedQuestIds == null) _cachedData.completedQuestIds = new List<string>();
                if (_cachedData.seenDialogIds == null) _cachedData.seenDialogIds = new List<string>();
                if (_cachedData.activeQuestId == null) _cachedData.activeQuestId = "";
            }
            return _cachedData;
        }
    }

    // ─── EVENTOS ─────────────────────────────────────────────────────────────

    /// <summary>Disparado quando baseResources muda (adicionar/remover recurso).</summary>
    public static event Action OnBaseResourcesChanged;

    /// <summary>Disparado quando equipamentos craftados/equipados mudam.</summary>
    public static event Action OnEquipmentChanged;

    // ─── CICLO DE VIDA ────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Salva automaticamente ao fechar o aplicativo.
    /// </summary>
    void OnApplicationQuit()
    {
        SavePersistentData();
    }

    // ─── SETOR 1 — PROGRESSÃO PERMANENTE ─────────────────────────────────────

    /// <summary>
    /// Serializa a progressão permanente do jogador (itens de base, slots e upgrades
    /// comprados) em um arquivo JSON em Application.persistentDataPath.
    ///
    /// O QUE SALVA:
    ///   • inventoryMaxSlots           — total de slots de inventário expandidos
    ///   • baseResources               — itens com ItemData.returnsToBase == true
    ///   • purchasedUpgradeIndices     — índices dos upgrades comprados (botão desativado)
    /// </summary>
    public void SavePersistentData()
    {
        GameObject player = GameManager.instance?.currentPlayer;
        
        // Se houver um player ativo, sincroniza suas propriedades no CachedData antes de salvar
        if (player != null)
        {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
                CachedData.inventoryMaxSlots = inventory.MaxSlots;

            PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
            if (upgrades != null)
            {
                CachedData.purchasedUpgradeIndices.Clear();
                for (int i = 0; i < upgrades.upgrades.Count; i++)
                {
                    var upg = upgrades.upgrades[i];
                    if (upg.button != null && !upg.button.interactable)
                        CachedData.purchasedUpgradeIndices.Add(i);
                }
            }

            PlayerSkinManager skinManager = player.GetComponent<PlayerSkinManager>();
            if (skinManager != null)
            {
                CachedData.selectedSkinID = skinManager.ActiveSkinID;
            }
        }

        // Serializa e grava o CachedData
        string json = JsonUtility.ToJson(CachedData, prettyPrint: true);
        File.WriteAllText(SaveFilePath, json);
        
        Debug.Log($"[SAVE] Progressão salva → {SaveFilePath} | " +
                  $"{CachedData.baseResources.Count} recursos | " +
                  $"{CachedData.purchasedUpgradeIndices.Count} upgrades | " +
                  $"{CachedData.craftedEquipmentIds.Count} equipamentos | " +
                  $"{CachedData.inimigosDescobertos.Count} inimigos descobertos");
    }

    /// <summary>
    /// Lê o arquivo JSON do disco e aplica os dados ao player informado.
    /// Chamado pelo GameManager após registrar o player (RegisterPlayer).
    ///
    /// O QUE RESTAURA:
    ///   • Capacidade máxima de slots do inventário
    ///   • Itens de base (returnsToBase == true) de volta ao inventário
    ///   • Re-aplica upgrades comprados (repassa para PlayerUpgrades.BuyUpgrade)
    /// </summary>
    public void LoadPersistentData(GameObject player)
    {
        PersistentSaveData data;
        if (_cachedData != null)
        {
            // O cache já foi carregado e pode conter alterações feitas em memória antes do registro do player
            data = _cachedData;
        }
        else
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.Log("[SAVE] Arquivo de save não encontrado. Jogador inicia do zero.");
                return;
            }
            data = CachedData;
        }

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null)
            inventory.SetMaxSlots(data.inventoryMaxSlots);
        // baseResources vivem no CachedData — não vão para o PlayerInventory

        PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
        if (upgrades != null)
        {
            foreach (int idx in data.purchasedUpgradeIndices)
                upgrades.BuyUpgrade(idx);
        }
        PlayerSkinManager skinManager = player.GetComponent<PlayerSkinManager>();
        if (skinManager != null)
        {
            skinManager.SetSkin(data.selectedSkinID);
        }

        // Notifica a Bolsa Sintética para atualizar com os dados carregados do disco
        OnBaseResourcesChanged?.Invoke();
        Debug.Log($"[SAVE] Progressão carregada | " +
                  $"{data.baseResources.Count} recursos de base | " +
                  $"{data.purchasedUpgradeIndices.Count} upgrades | " +
                  $"{data.craftedEquipmentIds.Count} equipamentos");
    }

    // ─── SETOR 2 — LIMPEZA DE RUN (morte) ────────────────────────────────────

    /// <summary>
    /// Limpa o inventário de infusão do jogador após a morte.
    ///
    /// COMPORTAMENTO:
    ///   • Remove todos os itens onde ItemData.returnsToBase == false
    ///     (itens comuns de run — coletados durante a dungeon para infusão).
    ///   • Mantém todos os itens onde ItemData.returnsToBase == true
    ///     (recursos permanentes usados para upgrades na base).
    ///   • Itens sem registro no ItemDatabase são descartados por segurança.
    ///
    /// Chamado por PlayerHealth.Die() antes da transição de cena.
    /// </summary>
    public void OnPlayerDied(GameObject player)
    {
        if (player == null) return;

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        List<string> toDiscard = new List<string>();

        foreach (var kvp in inventory.GetAllItems())
        {
            ItemData itemData = ItemDatabase.Instance?.GetItemData(kvp.Key);
            bool isBaseResource = itemData != null && itemData.returnsToBase;

            if (!isBaseResource)
                toDiscard.Add(kvp.Key);
        }

        foreach (string id in toDiscard)
            inventory.RemoveItem(id, inventory.GetItemCount(id));

        SavePersistentData(); // Persiste a Bolsa Sintética imediatamente
        Debug.Log($"[SAVE] Morte: {toDiscard.Count} item(s) de run descartado(s). Recursos de base salvos.");
    }

    // ─── SETOR 3 — HELPERS PARA BASE RESOURCES (usado pelo CraftingManager) ──

    /// <summary>
    /// Adiciona um recurso diretamente à Bolsa Sintética (baseResources).
    /// Usado pelo sistema de coleta quando ItemData.returnsToBase == true.
    /// Salva automaticamente em disco após adicionar.
    /// </summary>
    public void AddResourceToBase(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return;

        ItemSaveEntry existing = CachedData.baseResources.Find(e => e.itemId == itemId);
        if (existing != null)
        {
            existing.quantity += amount;
        }
        else
        {
            CachedData.baseResources.Add(new ItemSaveEntry(itemId, amount));
        }

        Debug.Log($"[SAVE] +{amount} {itemId} adicionado à Bolsa Sintética. " +
                  $"Total: {GetBaseResourceCount(itemId)}");

        OnBaseResourcesChanged?.Invoke();
    }

    /// <summary>
    /// Remove um recurso da Bolsa Sintética (baseResources).
    /// Usado pelo CraftingManager ao craftar receitas.
    /// Retorna true se removido com sucesso.
    /// </summary>
    public bool RemoveResourceFromBase(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;

        ItemSaveEntry existing = CachedData.baseResources.Find(e => e.itemId == itemId);
        if (existing == null || existing.quantity < amount)
        {
            Debug.LogWarning($"[SAVE] Não há {amount}x {itemId} na Bolsa Sintética.");
            return false;
        }

        existing.quantity -= amount;
        if (existing.quantity <= 0)
            CachedData.baseResources.Remove(existing);

        Debug.Log($"[SAVE] -{amount} {itemId} removido da Bolsa Sintética.");
        OnBaseResourcesChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Retorna a quantidade de um item na Bolsa Sintética.
    /// </summary>
    public int GetBaseResourceCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        ItemSaveEntry entry = CachedData.baseResources.Find(e => e.itemId == itemId);
        return entry != null ? entry.quantity : 0;
    }

    /// <summary>
    /// Retorna todos os recursos da Bolsa Sintética.
    /// </summary>
    public List<ItemSaveEntry> GetAllBaseResources()
    {
        return new List<ItemSaveEntry>(CachedData.baseResources);
    }

    // ─── SETOR 4 — HELPERS PARA EQUIPMENT (usado pelo EquipmentManager) ──────

    /// <summary>
    /// Adiciona um equipamento craftado à lista de persistência.
    /// </summary>
    public void AddCraftedEquipment(string equipmentId)
    {
        if (string.IsNullOrEmpty(equipmentId)) return;
        CachedData.craftedEquipmentIds.Add(equipmentId);
        Debug.Log($"[SAVE] Equipment craftado adicionado: {equipmentId}");
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>
    /// Marca um equipamento como equipado.
    /// </summary>
    public void SetEquipmentEquipped(string equipmentId, bool equipped)
    {
        if (string.IsNullOrEmpty(equipmentId)) return;

        if (equipped && !CachedData.equippedEquipmentIds.Contains(equipmentId))
        {
            CachedData.equippedEquipmentIds.Add(equipmentId);
        }
        else if (!equipped)
        {
            CachedData.equippedEquipmentIds.Remove(equipmentId);
        }

        Debug.Log($"[SAVE] Equipment {equipmentId} equipado={equipped}");
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>
    /// Retorna quantas cópias de um equipamento o jogador possui (craftou).
    /// </summary>
    public int GetCraftedEquipmentCount(string equipmentId)
    {
        int count = 0;
        foreach (string id in CachedData.craftedEquipmentIds)
            if (id == equipmentId) count++;
        return count;
    }

    /// <summary>
    /// Verifica se um equipamento está equipado.
    /// </summary>
    public bool IsEquipmentEquipped(string equipmentId)
    {
        return CachedData.equippedEquipmentIds.Contains(equipmentId);
    }

    /// <summary>
    /// Retorna a lista de IDs de equipamentos craftados.
    /// </summary>
    public List<string> GetAllCraftedEquipmentIds()
    {
        return new List<string>(CachedData.craftedEquipmentIds);
    }

    /// <summary>
    /// Retorna a lista de IDs de equipamentos equipados.
    /// </summary>
    public List<string> GetAllEquippedEquipmentIds()
    {
        return new List<string>(CachedData.equippedEquipmentIds);
    }

    /// <summary>
    /// Retorna quantos Núcleos Vitais da Nave foram recuperados pelo jogador (0 a 4).
    /// </summary>
    public int GetShipPartsRecovered()
    {
        return CachedData != null ? CachedData.shipPartsRecovered : 0;
    }

    /// <summary>
    /// Adiciona 1 Núcleo Vital recuperado da nave e salva o progresso permanentemente.
    /// </summary>
    public void AddShipPart()
    {
        if (CachedData != null)
        {
            CachedData.shipPartsRecovered = Mathf.Clamp(CachedData.shipPartsRecovered + 1, 0, 4);
            SavePersistentData();
            Debug.Log($"[SAVE] Núcleo da Nave recuperado! Total: {CachedData.shipPartsRecovered}/4");
        }
    }

    /// <summary>
    /// Reseta toda a progressão em disco e memória (deleta o arquivo JSON e re-inicializa cache).
    /// </summary>
    public static void ResetProfile()
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log("[SAVE] Arquivo de save deletado do disco.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SAVE] Erro ao deletar save: {e.Message}");
            }
        }
        
        // Se a instância estiver rodando (reset em tempo real)
        if (instance != null)
        {
            instance.ResetAllProgress();
        }
    }

    /// <summary>
    /// Reseta toda a progressão do jogo ativa em memória.
    /// </summary>
    public void ResetAllProgress()
    {
        _cachedData = new PersistentSaveData();

        // Aplica o reset ao player atual, se ativo na cena
        GameObject player = GameManager.instance != null ? GameManager.instance.currentPlayer : null;
        if (player != null)
        {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.SetMaxSlots(10);
                List<string> itemsToClear = new List<string>();
                foreach (var kvp in inventory.GetAllItems())
                {
                    itemsToClear.Add(kvp.Key);
                }
                foreach (string id in itemsToClear)
                {
                    inventory.RemoveItem(id, inventory.GetItemCount(id));
                }
            }

            PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.upgrades.Count; i++)
                {
                    var upg = upgrades.upgrades[i];
                    if (upg.button != null)
                    {
                        upg.button.interactable = true;
                    }
                }
            }

            PlayerSkinManager skinManager = player.GetComponent<PlayerSkinManager>();
            if (skinManager != null)
            {
                skinManager.SetSkin("astronaut");
            }
            
            EquipmentManager equipManager = player.GetComponent<EquipmentManager>();
            if (equipManager != null)
            {
                equipManager.ResetAllEquippedEffects();
            }
        }

        // Notifica as UIs ativas para atualizar imediatamente
        OnBaseResourcesChanged?.Invoke();
        OnEquipmentChanged?.Invoke();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.Reinitialize();
        }

        PlayerHealth.shouldPlayWakeUpAnimation = true;
        Debug.Log("[SAVE] Progresso limpo da memória.");
    }

    /// <summary>
    /// Atalho de depuração para testes de jogabilidade rápidos.
    /// </summary>
    void Update()
    {
        // Atalho: Shift + R para resetar progresso e reiniciar a cena na hora
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[DEBUG] Shift+R pressionado! Resetando progresso e recarregando cena...");
            ResetProfile();
            PlayerHealth.shouldPlayWakeUpAnimation = true;
            GamePause.ResetAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
