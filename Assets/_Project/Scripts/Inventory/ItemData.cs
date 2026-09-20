using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Todos os atributos possíveis no jogo.
/// </summary>
public enum AttributeType
{
    // === OFENSIVOS ===
    BaseDamageMultiplier, 
    AttackSpeedMelee, 
    CritChance, 
    CritMultiplier, 
    Knockback, 
    WeaponRangeMelee, 
    WeaponRangeProjectile, 
    Piercing, 
    BounceChance, 
    BounceCount, 
    MultiShotChance, 
    Spread,
    SlowOnHit,
    
    // === DEFENSIVOS E VIDA ===
    MaxHealth, 
    MaxArmor, 
    ArmorRegen, 
    DodgeChance, 
    DamageNegation, 
    Thorns,
    
    // === MOBILIDADE ===
    SpeedMultiplier, 
    DashCooldownMultiplier, 
    DashCounts, 
    DashInvulnerability
}

[System.Serializable]
public class ItemAttributeParam
{
    public AttributeType attributeType;
    [Tooltip("O valor do buff (ex: 0.2 para +20% ou 50 para +50 vida)")]
    public float value;
    [Tooltip("Esse valor deve multiplicar o atributo atual ou ser somado?")]
    public bool isMultiplier = false;
}

/// <summary>
/// ScriptableObject que define os dados visuais e de classificação de um item.
/// Crie assets via: Assets > Create > Inventory > Item Data
/// 
/// EXTENSÃO FUTURA: Adicione campos como buffType, buffValue, craftingRecipe, etc.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("ID único do item (deve ser igual ao itemId do CharacteristicItemPickup)")]
    public string itemId;

    [Tooltip("Nome de exibição do item")]
    public string itemName;

    [TextArea(2, 4)]
    [Tooltip("Descrição do item para tooltip")]
    public string description;

    [Header("Visual")]
    [Tooltip("Ícone do item exibido no inventário")]
    public Sprite icon;

    [Header("Classificação")]
    public ItemTier tier = ItemTier.Common;

    [Tooltip("Nome do inimigo de origem (ex: Spider, Golem)")]
    public string enemySource;

    [Header("Upgrades e Infusão")]
    [Tooltip("Se true, este item retorna à base ao morrer e é salvo na progressão permanente.\n" +
             "Use para recursos usados em upgrades permanentes da base.\n" +
             "Se false, o item é descartado ao morrer (item de run/infusão comum).")]
    public bool returnsToBase = false;

    [Tooltip("Essência gerada se o item for reciclado (descartado).")]
    public int recycleEssenceValue = 10;
    
    [Tooltip("Custo BASE de essência para infundir este item (B na fórmula). " +
             "O custo REAL é inflacionado pelo InfusionManager: C = B × (1 + 0.1 × Ptotal). " +
             "Valores padrão por tier: T1=60, T2=180, T3=300, T4=420.")]
    public int infusionEssenceCost = 60;

    [Tooltip("Use o '+' para adicionar quantos buffs o item der!")]
    public List<ItemAttributeParam> itemAttributes = new List<ItemAttributeParam>();

    [Header("Tier 4 Special Effect")]
    [Tooltip("Efeito especial concedido ao infundir este item T4.\n" +
             "Só funciona em itens Legendary. Itens de outros tiers devem usar None.\n" +
             "O efeito é ativado/desativado pelo Tier4EffectManager no Player.")]
    public Tier4EffectType tier4Effect = Tier4EffectType.None;

    /// <summary>
    /// Retorna a cor associada ao Tier do item
    /// </summary>
    public Color GetTierColor()
    {
        switch (tier)
        {
            case ItemTier.Common:    return new Color(0.85f, 0.85f, 0.85f); // Branco/Cinza claro
            case ItemTier.Uncommon:  return new Color(0.30f, 0.85f, 0.30f); // Verde
            case ItemTier.Rare:      return new Color(0.30f, 0.50f, 1.00f); // Azul
            case ItemTier.Legendary: return new Color(1.00f, 0.84f, 0.00f); // Dourado
            default:                 return Color.white;
        }
    }

    /// <summary>
    /// Retorna o nome localizado do Tier
    /// </summary>
    public string GetTierName()
    {
        switch (tier)
        {
            case ItemTier.Common:    return "Comum";
            case ItemTier.Uncommon:  return "Incomum";
            case ItemTier.Rare:      return "Raro";
            case ItemTier.Legendary: return "Lendário";
            default:                 return "Desconhecido";
        }
    }

    /// <summary>
    /// Peso de inflação deste tier (P na fórmula GDD §1.3).
    /// Ptotal = soma dos pesos de todos os itens já infundidos na arma.
    /// T1=1.0 | T2=2.25 | T3=4.0 | T4=6.0
    /// </summary>
    public float GetTierWeight()
    {
        switch (tier)
        {
            case ItemTier.Common:    return 1.00f;
            case ItemTier.Uncommon:  return 2.25f;
            case ItemTier.Rare:      return 4.00f;
            case ItemTier.Legendary: return 6.00f; // Tier 4 definido internamente (GDD não especifica)
            default:                 return 1.00f;
        }
    }
}

/// <summary>
/// Tiers de raridade dos itens (GDD 3.7.2)
/// </summary>
public enum ItemTier
{
    Common,     // T1 - Branco
    Uncommon,   // T2 - Verde
    Rare,       // T3 - Azul
    Legendary   // T4 - Dourado
}
