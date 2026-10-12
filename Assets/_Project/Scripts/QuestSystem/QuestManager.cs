using UnityEngine;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Atual (Carregada do Save)")]
    public QuestData currentQuest;
    
    [Header("Configuração de Início")]
    [Tooltip("A missão que o jogador ganha na primeira vez que abre o jogo.")]
    public QuestData firstQuest;

    // Eventos para o HUD e a UI do Eptinho saberem que algo mudou
    public delegate void OnQuestUpdated(QuestData quest, int progress);
    public event OnQuestUpdated QuestUpdatedEvent;

    public delegate void OnQuestCompleted(QuestData quest);
    public event OnQuestCompleted QuestCompletedEvent;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Garante que o SaveManager já carregou os dados
        Invoke(nameof(InitializeQuestSystem), 0.5f);
    }

    private void InitializeQuestSystem()
    {
        PersistentSaveData saveData = SaveManager.instance?.CachedData;
        if (saveData == null) return;

        // Se nunca pegou uma quest na vida, dá a primeira (Consertar a Porta)
        if (string.IsNullOrEmpty(saveData.activeQuestId) && saveData.completedQuestIds.Count == 0)
        {
            StartQuest(firstQuest);
        }
        else if (!string.IsNullOrEmpty(saveData.activeQuestId))
        {
            // O jogador fechou o jogo no meio de uma quest. Vamos carregar ela!
            // Para isso funcionar, as Quests devem estar na pasta Resources/Quests/
            currentQuest = Resources.Load<QuestData>("Quests/" + saveData.activeQuestId);
            
            // Atualiza o HUD com o progresso atual
            QuestUpdatedEvent?.Invoke(currentQuest, saveData.activeQuestProgress);
        }
    }

    public void StartQuest(QuestData newQuest)
    {
        if (newQuest == null) return;

        currentQuest = newQuest;
        PersistentSaveData saveData = SaveManager.instance.CachedData;
        
        saveData.activeQuestId = newQuest.questId;
        saveData.activeQuestProgress = 0;
        
        // Aqui nós poderíamos chamar SaveManager.instance.SavePersistentData();
        // Mas para não forçar disco a toda hora, deixamos para salvar no final da partida.

        QuestUpdatedEvent?.Invoke(currentQuest, 0);
        Debug.Log($"[QuestManager] Nova Missão Iniciada: {newQuest.questTitle}");
    }

    /// <summary>
    /// Sempre que o jogador interagir com uma porta, coletar um item ou craftar,
    /// os outros scripts do jogo devem chamar AvançarQuest(...)
    /// </summary>
    public void AdvanceQuest(QuestType actionType, string targetId = "", int amount = 1)
    {
        if (currentQuest == null) return;

        // Verifica se a ação que o jogador fez é a mesma que a missão pede
        if (currentQuest.questType != actionType) return;

        // Verifica se é o item específico que a missão pediu (se ela tiver pedido um)
        if (!string.IsNullOrEmpty(currentQuest.targetId) && currentQuest.targetId != targetId) return;

        PersistentSaveData saveData = SaveManager.instance.CachedData;
        saveData.activeQuestProgress += amount;

        Debug.Log($"[QuestManager] Progresso da Missão: {saveData.activeQuestProgress} / {currentQuest.requiredAmount}");

        if (saveData.activeQuestProgress >= currentQuest.requiredAmount)
        {
            CompleteCurrentQuest();
        }
        else
        {
            // Atualiza o HUD
            QuestUpdatedEvent?.Invoke(currentQuest, saveData.activeQuestProgress);
        }
    }

    private void CompleteCurrentQuest()
    {
        PersistentSaveData saveData = SaveManager.instance.CachedData;
        
        saveData.completedQuestIds.Add(currentQuest.questId);
        saveData.activeQuestId = "";
        saveData.activeQuestProgress = 0;

        Debug.Log($"[QuestManager] Missão Concluída: {currentQuest.questTitle}");
        QuestCompletedEvent?.Invoke(currentQuest);

        // Inicia a próxima missão automaticamente, se existir
        if (currentQuest.nextQuest != null)
        {
            StartQuest(currentQuest.nextQuest);
        }
        else
        {
            currentQuest = null;
        }
    }
}
