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
    public const int MaxSlots = 3;
    private const string LegacySaveFileName = "player_progress.json";
    private static int _activeSlot = -1;

    public static int ActiveSlot
    {
        get
        {
            if (_activeSlot <= 0)
            {
                _activeSlot = PlayerPrefs.GetInt("SelectedSaveSlot", 1);
                if (_activeSlot < 1 || _activeSlot > MaxSlots) _activeSlot = 1;
            }
            return _activeSlot;
        }
        set
        {
            _activeSlot = Mathf.Clamp(value, 1, MaxSlots);
            PlayerPrefs.SetInt("SelectedSaveSlot", _activeSlot);
            PlayerPrefs.Save();
        }
    }

    public static string GetSlotFileName(int slot) => $"player_progress_slot_{slot}.json";
    public static string GetSlotFilePath(int slot) => Path.Combine(Application.persistentDataPath, GetSlotFileName(slot));
    public string SaveFilePath => GetSlotFilePath(ActiveSlot);

    public static void CheckLegacyMigration()
    {
        try
        {
            string legacyPath = Path.Combine(Application.persistentDataPath, LegacySaveFileName);
            string slot1Path = GetSlotFilePath(1);
            if (File.Exists(legacyPath) && !File.Exists(slot1Path))
            {
                File.Copy(legacyPath, slot1Path);
                Debug.Log("[SAVE] Save legado migrado com sucesso para o Slot 1!");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SAVE] Falha ao migrar save legado: {e.Message}");
        }
    }

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
                if (!_cachedData.craftedEquipmentIds.Contains("eq_crystal_axe")) _cachedData.craftedEquipmentIds.Add("eq_crystal_axe");
                if (_cachedData.equippedEquipmentIds == null) _cachedData.equippedEquipmentIds = new List<string>();
                if (_cachedData.inimigosDescobertos == null) _cachedData.inimigosDescobertos = new List<string>();
                if (_cachedData.itensCatalogados == null) _cachedData.itensCatalogados = new List<string>();
                if (_cachedData.itensInfundidos == null) _cachedData.itensInfundidos = new List<string>();
                if (_cachedData.discoveredSynergies == null) _cachedData.discoveredSynergies = new List<string>();
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
        CheckLegacyMigration();
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

    private float _sessionStartTime;

    void Start()
    {
        _sessionStartTime = Time.realtimeSinceStartup;
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

        // Atualiza metadados do perfil antes de serializar
        CachedData.profileName = $"Slot {ActiveSlot}";
        CachedData.lastPlayedTimestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        if (_sessionStartTime > 0f)
        {
            float elapsed = Time.realtimeSinceStartup - _sessionStartTime;
            if (elapsed > 0f)
            {
                CachedData.playTimeSeconds += Mathf.RoundToInt(elapsed);
                _sessionStartTime = Time.realtimeSinceStartup;
            }
        }
        else
        {
            _sessionStartTime = Time.realtimeSinceStartup;
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

        // Reseta essência volátil acumulada durante a run
        PlayerEssence essence = player.GetComponent<PlayerEssence>() ?? player.GetComponentInChildren<PlayerEssence>();
        if (essence != null)
        {
            essence.currentEssence = 0;
            essence.onEssenceChanged?.Invoke(0);
        }

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
        string path = GetSlotFilePath(ActiveSlot);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"[SAVE] Arquivo de save do Slot {ActiveSlot} deletado do disco.");
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
        _cachedData.profileName = $"Slot {ActiveSlot}";
        if (!_cachedData.craftedEquipmentIds.Contains("eq_crystal_axe"))
        {
            _cachedData.craftedEquipmentIds.Add("eq_crystal_axe");
        }

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

        // Reseta o TutorialManager de volta ao início (PegarAdaga)
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.ResetTutorial();
        }

        // Reseta o estado do Robozinho na sessão
        CraftingTableInteraction.hasInteractedWithRobot = false;

        // Notifica as UIs ativas para atualizar imediatamente
        OnBaseResourcesChanged?.Invoke();
        OnEquipmentChanged?.Invoke();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.Reinitialize();
        }

        if (FloatingBookDisplay.Instance != null)
        {
            FloatingBookDisplay.Instance.CheckBookVisibility();
        }

        PlayerHealth.shouldPlayWakeUpAnimation = true;

        // Grava imediatamente o save limpo em disco
        SavePersistentData();
        Debug.Log($"[SAVE] Progresso limpo da memória e persistido no Slot {ActiveSlot}.");
    }

    /// <summary>
    /// Verifica se existe algum save em qualquer um dos slots.
    /// </summary>
    public static bool HasSaveFile()
    {
        CheckLegacyMigration();
        for (int i = 1; i <= MaxSlots; i++)
        {
            if (File.Exists(GetSlotFilePath(i))) return true;
        }
        return false;
    }

    /// <summary>
    /// Verifica se um slot específico possui save gravado.
    /// </summary>
    public static bool SlotExists(int slot)
    {
        CheckLegacyMigration();
        return File.Exists(GetSlotFilePath(slot));
    }

    /// <summary>
    /// Lê e retorna os dados de um slot sem alterar o slot ativo na sessão.
    /// </summary>
    public static PersistentSaveData GetSlotData(int slot)
    {
        CheckLegacyMigration();
        string path = GetSlotFilePath(slot);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            PersistentSaveData data = JsonUtility.FromJson<PersistentSaveData>(json);
            if (data != null && string.IsNullOrEmpty(data.profileName))
            {
                data.profileName = $"Slot {slot}";
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SAVE] Erro ao ler dados do Slot {slot}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converte os segundos totais de jogo em formato legível (ex: "1h 24m" ou "18m 42s").
    /// </summary>
    public static string FormatPlayTime(int totalSeconds)
    {
        if (totalSeconds <= 0) return "Início da jornada";
        TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
        if (t.TotalHours >= 1)
        {
            return $"{(int)t.TotalHours}h {t.Minutes:D2}m";
        }
        return $"{t.Minutes}m {t.Seconds:D2}s";
    }

    /// <summary>
    /// Retorna o número do slot que foi jogado mais recentemente.
    /// </summary>
    public static int GetMostRecentSlot()
    {
        CheckLegacyMigration();
        int bestSlot = ActiveSlot;
        DateTime bestTime = DateTime.MinValue;

        for (int i = 1; i <= MaxSlots; i++)
        {
            string path = GetSlotFilePath(i);
            if (File.Exists(path))
            {
                DateTime t = File.GetLastWriteTimeUtc(path);
                if (t > bestTime)
                {
                    bestTime = t;
                    bestSlot = i;
                }
            }
        }

        return bestSlot;
    }

    /// <summary>
    /// Define o slot ativo e recarrega o cache correspondente.
    /// </summary>
    public static void SelectSlot(int slot)
    {
        ActiveSlot = slot;
        if (instance != null)
        {
            instance._cachedData = null; // Força recarregar os dados do novo slot
        }
    }

    /// <summary>
    /// Apaga o arquivo de save de um slot específico.
    /// </summary>
    public static void DeleteSlot(int slot)
    {
        string path = GetSlotFilePath(slot);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"[SAVE] Slot {slot} deletado do disco.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SAVE] Erro ao deletar Slot {slot}: {e.Message}");
            }
        }

        if (slot == ActiveSlot && instance != null)
        {
            instance.ResetAllProgress();
        }
    }

    /// <summary>
    /// Inicia uma jornada nova do zero em um slot específico.
    /// </summary>
    public static void StartNewGameInSlot(int slot)
    {
        SelectSlot(slot);
        RestartGameFromZero();
    }

    /// <summary>
    /// Carrega e continua a jornada de um slot específico.
    /// </summary>
    public static void LoadGameFromSlot(int slot)
    {
        SelectSlot(slot);

        // 1. Fecha qualquer diálogo ativo
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ForceClose();
        }

        // 2. Limpa qualquer efeito de olhos/visão rodando
        PlayerWakeUpEffect.CleanUpAllActiveEffects();

        // 3. Destrói o Player persistente antigo para respawnar no dormitório
        if (PlayerPersistence.instance != null)
        {
            Destroy(PlayerPersistence.instance.gameObject);
            PlayerPersistence.instance = null;
        }
        if (GameManager.instance != null)
        {
            GameManager.instance.currentPlayer = null;
        }

        // 4. Reseta RunManager para round 1
        if (RunManager.instance != null)
        {
            RunManager.instance.StartNewRun();
        }

        // 5. Destrava pausas e timescale
        GamePause.ResetAll();
        Time.timeScale = 1f;

        // 6. Garante que a animação de acordar execute na Base
        PlayerHealth.shouldPlayWakeUpAnimation = true;

        // 7. Carrega Base com os dados do slot selecionado usando a tela de Loading
        GameManager.EnsureInstance().ReturnToBase();
    }

    /// <summary>
    /// Reinicia toda a progressão do jogo do zero no slot ativo e carrega a Base com animação de acordar.
    /// Limpa diálogos, áudio de menu, pausas, save em disco e cache de memória.
    /// </summary>
    public static void RestartGameFromZero()
    {
        Debug.Log($"[SAVE] 🔄 Reiniciando jogo 100% do zero no Slot {ActiveSlot}...");

        // 1. Fecha qualquer diálogo ativo e cancela rotinas de digitação
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ForceClose();
        }

        // 2. Limpa qualquer efeito de olhos/visão rodando
        PlayerWakeUpEffect.CleanUpAllActiveEffects();

        // 3. Destrói o Player persistente antigo para instanciar o Player limpo na cama do dormitório da Base
        if (PlayerPersistence.instance != null)
        {
            Destroy(PlayerPersistence.instance.gameObject);
            PlayerPersistence.instance = null;
        }
        if (GameManager.instance != null)
        {
            GameManager.instance.currentPlayer = null;
        }

        // 4. Reseta estado de run
        if (RunManager.instance != null)
        {
            RunManager.instance.StartNewRun();
        }

        // 5. Destrava pausas e timescale
        GamePause.ResetAll();
        Time.timeScale = 1f;

        // 6. Deleta arquivo de save do slot ativo e limpa cache em memória
        ResetProfile();

        // 7. Garante que a animação de acordar execute na Base
        PlayerHealth.shouldPlayWakeUpAnimation = true;

        // 8. Carrega cena Base limpa usando a tela de Loading
        GameManager.EnsureInstance().ReturnToBase();
    }

    /// <summary>
    /// Atalhos de reinício rápido (Shift+R ou F5) para reiniciar o slot atual do zero a qualquer momento.
    /// </summary>
    void Update()
    {
        bool shiftR = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.R);
        bool f5 = Input.GetKeyDown(KeyCode.F5);

        if (shiftR || f5)
        {
            Debug.Log($"[DEBUG] Atalho de reinício acionado (Shift+R / F5) no Slot {ActiveSlot}!");
            RestartGameFromZero();
        }
    }
}
