using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    private static QuestManager _instance;
    public static QuestManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<QuestManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("QuestManager");
                    _instance = go.AddComponent<QuestManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Quest Atual (Carregada do Save)")]
    [Tooltip("Somente leitura em runtime. Valores colocados no Inspector são ignorados.")]
    public QuestData currentQuest;

    [Header("Configuração de Início")]
    [Tooltip("A missão que o jogador ganha na primeira vez que abre o jogo.")]
    public QuestData firstQuest;

    // Eventos para o HUD e a UI do Eptinho saberem que algo mudou
    public delegate void OnQuestUpdated(QuestData quest, int progress);
    public event OnQuestUpdated QuestUpdatedEvent;

    public delegate void OnQuestCompleted(QuestData quest);
    public event OnQuestCompleted QuestCompletedEvent;

    /// <summary>Disparado após QUALQUER transição confirmada de estado (inicialização, início, progresso, conclusão).</summary>
    public event Action StateChanged;

    public bool IsInitialized { get; private set; }

    private struct PendingAction { public QuestType type; public string targetId; public int amount; }
    private readonly Queue<PendingAction> deferred = new Queue<PendingAction>();
    private bool isTransitioning;
    private const int MaxChainSteps = 64;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        currentQuest = null;
    }

    private void OnEnable()
    {
        CraftingManager.OnCraftCompleted += HandleCraftCompleted;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        BossEvents.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        CraftingManager.OnCraftCompleted -= HandleCraftCompleted;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        BossEvents.OnBossDefeated -= HandleBossDefeated;
    }

    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        BossEvents.OnBossDefeated -= HandleBossDefeated;
        BossEvents.OnBossDefeated += HandleBossDefeated;

        if (IsInitialized)
        {
            Reinitialize();
        }
    }

    private void HandleBossDefeated()
    {
        Debug.Log("[QuestManager] Boss Derrotado! Recuperando Núcleo da Nave e avançando objetivos.");
        if (SaveManager.instance != null)
        {
            SaveManager.instance.AddShipPart();
        }
        AdvanceQuest(QuestType.Interact, "boss_defeated", 1);
    }

    public void Reinitialize()
    {
        currentQuest = null;
        InitializeQuestSystem();
    }

    private IEnumerator Start()
    {
        yield return null;
        InitializeQuestSystem();
    }

    private void InitializeQuestSystem()
    {
        PersistentSaveData save = SaveManager.instance.CachedData;
        bool dirty = false;
        currentQuest = null;

        if (!string.IsNullOrEmpty(save.activeQuestId))
        {
            currentQuest = FindQuestById(save.activeQuestId);
            if (currentQuest != null)
            {
                SeedStateBasedProgress(save);
            }
            else
            {
                Debug.LogError($"[QuestManager] Quest salva '{save.activeQuestId}' não encontrada. Recuperando pela cadeia a partir de firstQuest.");
                save.activeQuestId = "";
                save.activeQuestProgress = 0;
                dirty = true;
            }
        }

        if (firstQuest == null)
        {
            firstQuest = Resources.Load<QuestData>("Quests/quest_0_intro_eptinho");
            if (firstQuest == null)
                firstQuest = Resources.Load<QuestData>("Quests/quest_consertar_porta");
        }

        if (currentQuest == null)
        {
            QuestData resume = FirstUncompletedFrom(firstQuest, save);
            if (resume != null)
            {
                BeginQuest(resume, save);
                dirty = true;
            }
        }

        if (dirty) SaveManager.instance.SavePersistentData();

        IsInitialized = true;
        NotifyUpdated(save);

        if (IsSatisfied(save)) CompleteCurrentQuest();
        DrainDeferred();
    }

    public void StartQuest(QuestData newQuest)
    {
        if (newQuest == null) return;
        if (isTransitioning)
        {
            Debug.LogWarning($"[QuestManager] StartQuest({newQuest.questId}) ignorado: transição em andamento.");
            return;
        }

        PersistentSaveData save = SaveManager.instance.CachedData;
        if (save.completedQuestIds.Contains(newQuest.questId))
        {
            Debug.LogWarning($"[QuestManager] '{newQuest.questId}' já foi concluída — não será reiniciada.");
            return;
        }

        BeginQuest(newQuest, save);
        SaveManager.instance.SavePersistentData();
        Debug.Log($"[QuestManager] Nova Missão Iniciada: {newQuest.questTitle}");
        NotifyUpdated(save);

        if (IsSatisfied(save)) CompleteCurrentQuest();
    }

    /// <summary>
    /// Sempre que o jogador interagir com uma porta, coletar um item ou craftar,
    /// os outros scripts do jogo devem chamar AdvanceQuest(...)
    /// </summary>
    public void AdvanceQuest(QuestType actionType, string targetId = "", int amount = 1)
    {
        if (amount <= 0) return;

        if (!IsInitialized || isTransitioning)
        {
            deferred.Enqueue(new PendingAction { type = actionType, targetId = targetId, amount = amount });
            return;
        }

        ApplyAction(actionType, targetId, amount);
    }

    private void ApplyAction(QuestType actionType, string targetId, int amount)
    {
        if (currentQuest == null) return;
        bool typeMatches = currentQuest.questType == actionType;
        bool targetMatches = string.IsNullOrEmpty(currentQuest.targetId) || currentQuest.targetId == targetId;

        // Tolerância especial para a quest de consertar porta: aceita tanto CraftItem (eq_consertar_porta) quanto Interact (porta_run)
        if (currentQuest.questId == "quest_consertar_porta")
        {
            if (targetId == "eq_consertar_porta" || targetId == "porta_run")
            {
                typeMatches = true;
                targetMatches = true;
            }
        }

        if (!typeMatches || !targetMatches) return;

        PersistentSaveData save = SaveManager.instance.CachedData;
        save.activeQuestProgress = Mathf.Min(Required(currentQuest), save.activeQuestProgress + amount);
        Debug.Log($"[QuestManager] Progresso da Missão: {save.activeQuestProgress} / {Required(currentQuest)}");

        if (IsSatisfied(save)) CompleteCurrentQuest();
        else NotifyUpdated(save);
    }

    private void CompleteCurrentQuest()
    {
        PersistentSaveData save = SaveManager.instance.CachedData;
        isTransitioning = true;
        try
        {
            for (int step = 0; step < MaxChainSteps && IsSatisfied(save); step++)
            {
                QuestData finished = currentQuest;

                if (!save.completedQuestIds.Contains(finished.questId))
                    save.completedQuestIds.Add(finished.questId);
                save.activeQuestId = "";
                save.activeQuestProgress = 0;
                currentQuest = null;

                QuestData nextCandidate = finished.nextQuest != null ? finished.nextQuest : GetNextQuestInChain(finished);
                QuestData next = FirstUncompletedFrom(nextCandidate, save);
                if (next != null) BeginQuest(next, save);

                SaveManager.instance.SavePersistentData();

                Debug.Log($"[QuestManager] Missão Concluída: {finished.questTitle}");
                QuestCompletedEvent?.Invoke(finished);
                NotifyUpdated(save);
            }
        }
        finally
        {
            isTransitioning = false;
        }

        DrainDeferred();
    }

    private void BeginQuest(QuestData q, PersistentSaveData save)
    {
        currentQuest = q;
        save.activeQuestId = q.questId;
        save.activeQuestProgress = 0;
        SeedStateBasedProgress(save);
    }

    private void SeedStateBasedProgress(PersistentSaveData save)
    {
        if (currentQuest == null) return;
        int existing = 0;
        switch (currentQuest.questType)
        {
            case QuestType.CatalogEnemies:
                if (save.inimigosDescobertos == null) return;
                existing = string.IsNullOrEmpty(currentQuest.targetId)
                    ? save.inimigosDescobertos.Count
                    : (save.inimigosDescobertos.Contains(currentQuest.targetId) ? 1 : 0);
                break;
            case QuestType.CraftItem:
                if (string.IsNullOrEmpty(currentQuest.targetId)) return;
                existing = SaveManager.instance.GetCraftedEquipmentCount(currentQuest.targetId);
                break;
            default:
                if (currentQuest.questId == "quest_consertar_porta" && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_consertar_porta"))
                {
                    existing = 1;
                }
                break;
        }
        save.activeQuestProgress = Mathf.Min(Required(currentQuest), Mathf.Max(save.activeQuestProgress, existing));
    }

    private void HandleCraftCompleted(CraftingRecipe recipe)
    {
        if (recipe == null) return;
        string craftedId = recipe.resultType == CraftingResultType.Equipment
            ? (recipe.resultEquipment != null ? recipe.resultEquipment.equipmentId : recipe.recipeId.Replace("recipe_", "equip_"))
            : recipe.resultItemId;
        AdvanceQuest(QuestType.CraftItem, craftedId, 1);
    }

    private void DrainDeferred()
    {
        int budget = 32;
        while (deferred.Count > 0 && !isTransitioning && IsInitialized && budget-- > 0)
        {
            PendingAction a = deferred.Dequeue();
            ApplyAction(a.type, a.targetId, a.amount);
        }
    }

    private void NotifyUpdated(PersistentSaveData save)
    {
        if (currentQuest != null) QuestUpdatedEvent?.Invoke(currentQuest, save.activeQuestProgress);
        StateChanged?.Invoke();
        if (QuestHUDTracker.Instance != null) QuestHUDTracker.Instance.RefreshDisplay();
    }

    private bool IsSatisfied(PersistentSaveData save) =>
        currentQuest != null && save.activeQuestProgress >= Required(currentQuest);

    private static int Required(QuestData q) => Mathf.Max(1, q.requiredAmount);

    private QuestData FirstUncompletedFrom(QuestData start, PersistentSaveData save)
    {
        var visited = new HashSet<QuestData>();
        for (QuestData q = start; q != null && visited.Add(q); q = GetNextQuestInChain(q))
            if (!save.completedQuestIds.Contains(q.questId)) return q;
        return null;
    }

    public QuestData GetNextQuestInChain(QuestData q)
    {
        if (q == null) return null;
        if (q.nextQuest != null) return q.nextQuest;
        string fallbackId = GetFallbackNextQuestId(q.questId);
        if (!string.IsNullOrEmpty(fallbackId))
        {
            return FindQuestById(fallbackId);
        }
        return null;
    }

    public string GetFallbackNextQuestId(string questId)
    {
        switch (questId)
        {
            case "quest_0_intro_eptinho": return "quest_consertar_porta";
            case "quest_consertar_porta": return "quest_2_amostragem";
            case "quest_2_amostragem": return "quest_3_reconhecimento";
            case "quest_3_reconhecimento": return "quest_4_infusao";
            case "quest_4_infusao": return "quest_5_mercador";
            case "quest_5_mercador": return "quest_6_minerios";
            case "quest_6_minerios": return "quest_7_engenharia";
            case "quest_7_engenharia": return "quest_8_climax_boss";
            default: return null;
        }
    }

    public QuestData FindQuestById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        QuestData q = Resources.Load<QuestData>("Quests/" + id);
        if (q != null && (q.questId == id || q.name == id)) return q;

        foreach (QuestData c in Resources.LoadAll<QuestData>("Quests"))
            if (c != null && (c.questId == id || c.name == id)) return c;

        var visited = new HashSet<QuestData>();
        for (QuestData c = firstQuest; c != null && visited.Add(c); c = c.nextQuest)
            if (c != null && (c.questId == id || c.name == id)) return c;

        return null;
    }
}
