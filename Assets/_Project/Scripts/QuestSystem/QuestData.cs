using UnityEngine;

public enum QuestType
{
    Interact,           // Falar com um NPC ou clicar num painel (Porta)
    CollectItem,        // Coletar algo (Seja RNG ou Base)
    CraftItem,          // Craftar um equipamento (Arma ou Melhoria)
    CatalogEnemies      // Descobrir Inimigos
}

[CreateAssetMenu(fileName = "New Quest", menuName = "Plerotus/Quests/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Identificação")]
    public string questId;      // ID único (ex: "quest_fix_door")
    public string questTitle;   // Título curto para o HUD (ex: "Conserte a Porta")
    
    [TextArea(3, 5)]
    public string questDescription; // Explicação detalhada para a UI do Eptinho

    [Header("Lógica (O que o jogador deve fazer)")]
    public QuestType questType;
    
    [Tooltip("Ex: Se for CollectItem, qual o ID do item? Se for vazia, serve qualquer um.")]
    public string targetId; 
    
    [Tooltip("Ex: Se for CatalogEnemies, coloque 5. Se for Consertar Porta, coloque 1.")]
    public int requiredAmount = 1;

    [Header("Progressão Automática")]
    [Tooltip("A próxima Quest que iniciará automaticamente quando esta for concluída.")]
    public QuestData nextQuest;
}
