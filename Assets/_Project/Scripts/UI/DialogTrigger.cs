using UnityEngine;

public class DialogTrigger : MonoBehaviour
{
    [TextArea(3, 10)]
    public string[] dialogLines;
    
    public bool triggerOnStart = false; // Se for as caixas da base, marca isso
    public bool triggerOnCollision = false; // Se for o Mercador/Sintonizador
    public string playerTag = "Player";

    [Header("Integração com Quests (Opcional)")]
    [Tooltip("Se preenchido, esse diálogo SÓ vai tocar se essa for a quest atual do jogador.")]
    public QuestData requiredQuest;
    [Tooltip("Se marcado, quando o diálogo acabar, ele avisa o QuestManager para concluir o objetivo.")]
    public bool completesQuestOnEnd = false;
    
    private bool alreadyTriggered = false;

    void Start()
    {
        // Precisamos dar um pequeno delay (0.6s) se for no Start, 
        // para dar tempo do QuestManager carregar o save
        if (triggerOnStart && !alreadyTriggered)
        {
            Invoke(nameof(TryTriggerDialog), 0.6f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnCollision && !alreadyTriggered && other.CompareTag(playerTag))
        {
            TryTriggerDialog();
        }
    }

    public void TryTriggerDialog()
    {
        if (alreadyTriggered) return;

        // Se esse diálogo exige uma quest específica, verifica se ela é a quest atual
        if (requiredQuest != null)
        {
            if (QuestManager.Instance == null || QuestManager.Instance.currentQuest != requiredQuest)
            {
                return; // A quest atual não é a que o diálogo precisa, então ele fica mudo.
            }
        }

        alreadyTriggered = true; 
        
        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.StartDialog(dialogLines, OnDialogFinished);
        }
        else
        {
            Debug.LogError("DialogManager não encontrado na cena!");
        }
    }

    private void OnDialogFinished()
    {
        if (completesQuestOnEnd && requiredQuest != null && QuestManager.Instance != null)
        {
            // O diálogo era uma missão de interação. Agora que ele acabou, manda o jogo avançar a quest!
            QuestManager.Instance.AdvanceQuest(QuestType.Interact, requiredQuest.questId, 1);
        }
    }
}
