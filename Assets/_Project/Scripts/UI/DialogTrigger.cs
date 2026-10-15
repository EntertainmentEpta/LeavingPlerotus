using UnityEngine;

public class DialogTrigger : MonoBehaviour
{
    [TextArea(3, 10)]
    public string[] dialogLines;

    public bool triggerOnStart = false;     // Se for as caixas da base, marca isso
    public bool triggerOnCollision = false; // Se for o Mercador/Sintonizador
    public string playerTag = "Player";

    [Header("Integração com Quests (Opcional)")]
    [Tooltip("Se preenchido, esse diálogo SÓ vai tocar se essa for a quest atual do jogador.")]
    public QuestData requiredQuest;
    [Tooltip("Se marcado, quando o diálogo acabar, ele avisa o QuestManager para concluir o objetivo (quest do tipo Interact).")]
    public bool completesQuestOnEnd = false;

    [Header("Persistência (Opcional)")]
    [Tooltip("Se preenchido, o diálogo toca UMA vez por save (sobrevive a mortes, reloads e novas runs).")]
    public string oneShotId = "";

    [Header("Tutorial (Opcional)")]
    public bool advancesTutorialStep = false;
    public TutorialStep tutorialStepToComplete;

    private bool alreadyTriggered = false;
    private bool playerInside = false;
    private QuestManager boundQuestManager;

    private void Start()
    {
        // Auto-vincula a quest_0_intro_eptinho se for o diálogo inicial e não estiver configurado no Inspector
        if (triggerOnStart && requiredQuest == null)
        {
            requiredQuest = Resources.Load<QuestData>("Quests/quest_0_intro_eptinho");
            completesQuestOnEnd = true;
            if (string.IsNullOrEmpty(oneShotId)) oneShotId = "dialog_intro_eptinho";
        }

        if (IsSeenPersistently())
        {
            alreadyTriggered = true;
            return;
        }

        boundQuestManager = QuestManager.Instance;
        if (boundQuestManager != null)
        {
            boundQuestManager.StateChanged += OnQuestStateChanged;
            if (triggerOnStart && boundQuestManager.IsInitialized)
            {
                TryTriggerDialog();
            }
        }
        else if (triggerOnStart)
        {
            TryTriggerDialog();
        }
    }

    private void OnDestroy()
    {
        if (boundQuestManager != null)
        {
            boundQuestManager.StateChanged -= OnQuestStateChanged;
        }
    }

    private void OnQuestStateChanged()
    {
        if (alreadyTriggered) return;
        if (triggerOnStart || (triggerOnCollision && playerInside))
        {
            TryTriggerDialog();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = true;
        if (triggerOnCollision && !alreadyTriggered)
        {
            TryTriggerDialog();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;
        }
    }

    public void TryTriggerDialog()
    {
        if (alreadyTriggered) return;

        if (requiredQuest != null)
        {
            QuestManager qm = QuestManager.Instance;
            if (qm == null || !qm.IsInitialized) return;
            bool matches = qm.currentQuest != null && (qm.currentQuest == requiredQuest || qm.currentQuest.questId == requiredQuest.questId);
            if (!matches) return;
        }

        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager não encontrado na cena!", this);
            return;
        }

        alreadyTriggered = true;
        DialogManager.Instance.StartDialog(dialogLines, OnDialogFinished);
    }

    private void OnDialogFinished()
    {
        MarkSeenPersistently();

        if (completesQuestOnEnd)
        {
            QuestManager qm = QuestManager.Instance;
            if (qm != null && qm.currentQuest != null)
            {
                if (requiredQuest != null && (qm.currentQuest == requiredQuest || qm.currentQuest.questId == requiredQuest.questId))
                {
                    qm.AdvanceQuest(QuestType.Interact, requiredQuest.targetId, 1);
                }
                else if (requiredQuest == null && qm.currentQuest.questType == QuestType.Interact)
                {
                    qm.AdvanceQuest(QuestType.Interact, qm.currentQuest.targetId, 1);
                }
            }
        }

        if (advancesTutorialStep && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.CompleteStep(tutorialStepToComplete);
        }

        if (QuestHUDTracker.Instance != null)
        {
            QuestHUDTracker.Instance.RefreshDisplay();
        }
    }

    private bool IsSeenPersistently() =>
        !string.IsNullOrEmpty(oneShotId) && SaveManager.instance.CachedData.seenDialogIds.Contains(oneShotId);

    private void MarkSeenPersistently()
    {
        if (string.IsNullOrEmpty(oneShotId)) return;
        PersistentSaveData data = SaveManager.instance.CachedData;
        if (data.seenDialogIds.Contains(oneShotId)) return;
        data.seenDialogIds.Add(oneShotId);
        SaveManager.instance.SavePersistentData();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (completesQuestOnEnd && requiredQuest != null && requiredQuest.questType != QuestType.Interact)
            Debug.LogWarning($"[DialogTrigger] '{name}': completesQuestOnEnd só funciona com quests Interact " +
                             $"('{requiredQuest.questId}' é {requiredQuest.questType}).", this);
        if (completesQuestOnEnd && requiredQuest != null && requiredQuest.requiredAmount > 1)
            Debug.LogWarning($"[DialogTrigger] '{name}': um único trigger só dá 1 de progresso, mas a quest pede {requiredQuest.requiredAmount}.", this);
    }
#endif
}
