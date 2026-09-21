using UnityEngine;

/// <summary>
/// ScriptableObject que define os dados de um inimigo para o Bestiário do Eptinho.
/// Crie assets via: Assets > Create > Eptinho > Enemy Data
///
/// SETUP:
///   1. Crie um asset EnemyData para cada tipo de inimigo.
///   2. Arraste-o no campo "enemyData" do componente EnemyIdentity no prefab do inimigo.
///   3. O BestiarioManager registra automaticamente quando o player avista o inimigo.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Eptinho/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Nome de exibição do inimigo no Bestiário.")]
    public string enemyName;

    [Tooltip("Tipo/classe do inimigo (ex: Mob Menor, Atirador, Tanque, Elite).")]
    public string enemyClass;

    [Header("Visual")]
    [Tooltip("Ícone exibido no Bestiário.")]
    public Sprite icon;

    [Header("Lore")]
    [TextArea(3, 6)]
    [Tooltip("Descrição do inimigo exibida no Bestiário.")]
    public string descricao;

    [Header("Sinergia")]
    [Tooltip("ID do nó correspondente no catálogo de sinergias (SynergyNodeData.nodeID). " +
             "Deixe vazio se o inimigo não participa do sistema de sinergias.")]
    public string synergyNodeID;

    [Header("Dados de Combate (opcional — exibição no Bestiário)")]
    [Tooltip("Vida base do inimigo.")]
    public int vidaBase;
    [Tooltip("Dano base do inimigo.")]
    public float danoBase;
    [Tooltip("Pontos de spawn budget que este inimigo custa (GDD §1.2).")]
    public int custoBudget = 1;
}
