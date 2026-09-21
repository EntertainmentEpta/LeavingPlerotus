using System.Collections.Generic;
using UnityEngine;

// ItemTier já definido em ItemData.cs (Common, Uncommon, Rare, Legendary)

/// <summary>
/// Tipo funcional de cada nó no catálogo de infusões.
/// </summary>
public enum NodeType
{
    /// <summary>Nó raiz — identifica o monstro catalogado.</summary>
    EnemyOrigin,

    /// <summary>Peça dropada (T1–T4) que fornece status base ao ser infundida.</summary>
    InfusionPart,

    /// <summary>Receita de combo que exige múltiplas peças para ser ativada.</summary>
    SynergyRecipe
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
/// ScriptableObject que representa uma receita de sinergia no catálogo de infusão.
/// Crie novas receitas pelo menu: Assets → Create → Synergy System → Node Data.
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

    [Header("Infusão")]
    [Tooltip("Porcentagem de desconto na essência ao aplicar esta sinergia (0 = sem desconto, 1 = 100% de desconto).")]
    [Range(0f, 1f)]
    [SerializeField] private float essenceDiscountPercentage;

    [Header("Requisitos de Sinergia")]
    [Tooltip("Lista de nós que precisam estar desbloqueados para habilitar esta receita.")]
    [SerializeField] private List<SynergyNodeData> synergyRequirements;

    [Header("Modificadores")]
    [Tooltip("Lista de modificadores de atributo concedidos ao desbloquear este nó.")]
    [SerializeField] private List<StatModifier> statModifiers;

    // ──────────────────────────────────────────────
    // Propriedades públicas de leitura (read-only)
    // ──────────────────────────────────────────────

    public string NodeID                    => nodeID;
    public string DisplayName               => displayName;
    public NodeType NodeType                => nodeType;
    public float EssenceDiscountPercentage  => essenceDiscountPercentage;

    public IReadOnlyList<SynergyNodeData> SynergyRequirements => synergyRequirements;
    public IReadOnlyList<StatModifier> StatModifiers          => statModifiers;
}
