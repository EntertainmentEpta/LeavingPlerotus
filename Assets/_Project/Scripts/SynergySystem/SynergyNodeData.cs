using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tier de item usado como moeda para desbloquear nós na Árvore de Sinergias.
/// </summary>
public enum ItemTier
{
    None,
    T1,
    T2,
    T3,
    T4
}

/// <summary>
/// Tipo funcional de cada nó dentro da Árvore de Sinergias.
/// </summary>
public enum NodeType
{
    /// <summary>Raiz — catálogo de inimigos derrotados.</summary>
    EnemyCatalog,

    /// <summary>Nós menores pagos — melhorias de atributo.</summary>
    StatUpgrade,

    /// <summary>Combos automáticos — desbloqueiam-se ao cumprir pré-requisitos.</summary>
    ComboSynergy,

    /// <summary>Poderes supremos — ascensões de alto custo.</summary>
    Ascension
}

/// <summary>
/// Custo necessário para desbloquear um nó na árvore.
/// </summary>
[System.Serializable]
public class UnlockCost
{
    [Tooltip("Tier do item exigido como pagamento.")]
    public ItemTier tierRequired;

    [Tooltip("Quantidade de itens desse tier necessária.")]
    public int amountRequired;
}

/// <summary>
/// Modificador de atributo concedido ao desbloquear um nó.
/// </summary>
[System.Serializable]
public class StatModifier
{
    [Tooltip("Nome do atributo afetado (ex.: 'ATK', 'DEF', 'HP').")]
    public string statName;

    [Tooltip("Valor de adição fixa ao atributo.")]
    public float baseValue;

    [Tooltip("Ganho percentual aplicado ao atributo (ex.: 0.15 = +15%).")]
    public float multiplierValue;
}

/// <summary>
/// ScriptableObject que representa um único nó na Árvore de Sinergias.
/// Crie novos nós pelo menu: Assets → Create → Synergy System → Node Data.
/// </summary>
[CreateAssetMenu(fileName = "New Synergy Node", menuName = "Synergy System/Node Data")]
public class SynergyNodeData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("ID único do nó (usado em lookups e save/load).")]
    [SerializeField] private string nodeID;

    [Tooltip("Nome exibido na UI para o jogador.")]
    [SerializeField] private string displayName;

    [Tooltip("Tipo funcional deste nó na árvore.")]
    [SerializeField] private NodeType nodeType;

    [Header("Custo e Desbloqueio")]
    [Tooltip("Custo para comprar/desbloquear este nó.")]
    [SerializeField] private UnlockCost cost;

    [Tooltip("Se verdadeiro, ignora o custo e desbloqueia automaticamente quando os pré-requisitos forem atingidos (ideal para ComboSynergy).")]
    [SerializeField] private bool isAutoUnlock;

    [Header("Conexões")]
    [Tooltip("Lista de nós que precisam estar desbloqueados antes deste.")]
    [SerializeField] private List<SynergyNodeData> prerequisites;

    [Header("Modificadores")]
    [Tooltip("Lista de modificadores de atributo concedidos ao desbloquear este nó.")]
    [SerializeField] private List<StatModifier> statModifiers;

    // ──────────────────────────────────────────────
    // Propriedades públicas de leitura (read-only)
    // ──────────────────────────────────────────────

    public string NodeID          => nodeID;
    public string DisplayName     => displayName;
    public NodeType NodeType      => nodeType;
    public UnlockCost Cost        => cost;
    public bool IsAutoUnlock      => isAutoUnlock;

    public IReadOnlyList<SynergyNodeData> Prerequisites => prerequisites;
    public IReadOnlyList<StatModifier> StatModifiers     => statModifiers;
}
