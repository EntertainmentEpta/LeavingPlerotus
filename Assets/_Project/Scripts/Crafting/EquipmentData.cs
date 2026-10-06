using UnityEngine;

/// <summary>
/// Tipos de efeito que uma melhoria pode aplicar.
/// Extensível para novos tipos de melhoria no futuro.
/// </summary>
public enum EquipmentEffectType
{
    InventorySlotExpansion,  // Aumenta maxSlots do inventário de run
    MaxHealthBoost,          // Aumenta vida máxima
    MaxArmorBoost,           // Aumenta armadura máxima
    SpeedBoost,              // Aumenta velocidade
    DamageBoost,             // Aumenta dano base
    CritChanceBoost,         // Aumenta chance de crítico
    ArmorRegenBoost,         // Aumenta regeneração de armadura
    RangeBoost,              // Aumenta o alcance da arma/ataques
    DodgeChanceBoost,        // Aumenta a chance de esquivar de ataques
    CritMultiplierBoost,     // Aumenta o multiplicador de dano crítico
    KnockbackBoost,          // Aumenta a força de empurrão
    AttackSpeedBoost,        // Aumenta a velocidade de ataque melee
    HealthRegenBoost,        // Aumenta a regeneração de vida por segundo
    DashCooldownBoost,       // Diminui o cooldown do dash (multiplicador)
    DashCountsBoost,         // Aumenta a quantidade de cargas de dash
    DashInvulnerabilityBoost, // Aumenta o tempo de invulnerabilidade do dash
    MagnetRangeBoost,        // Aumenta o raio de atração de essências
    BarrierCounterDisplay,   // Exibe o contador de salas/barreiras na UI (Detector de Barreiras)
    UnlockSynergyMap,        // Desbloqueia o Mapa de Sinergias no Oráculo Eptinho
    UnlockProjectileWeapon,  // Desbloqueia a Arma de Projétil para a run
    RepairLabDoor            // Conserta a porta do Laboratório na Base
}

/// <summary>
/// ScriptableObject que define uma melhoria equipável.
/// Crie assets via: Assets > Create > Crafting > Equipment Data
///
/// Melhorias são produzidas pelo sistema de Crafting e podem ser
/// equipadas/desequipadas pelo jogador na Mesa de Trabalho.
///
/// EXEMPLO:
///   "Expansão de Inventário Nv.1"
///   Efeito: InventorySlotExpansion, valor: 5
///   → Ao equipar, inventário ganha +5 slots
///   → Ao desequipar, perde os 5 slots extras
/// </summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "Crafting/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("ID único da melhoria (usado no save)")]
    public string equipmentId;

    [Tooltip("Nome de exibição")]
    public string equipmentName;

    [TextArea(2, 4)]
    [Tooltip("Descrição da melhoria")]
    public string description;

    [Tooltip("Ícone exibido na UI")]
    public Sprite icon;

    [Header("Efeito")]
    [Tooltip("Tipo de efeito aplicado ao equipar (ou imediatamente se for de base)")]
    public EquipmentEffectType effectType = EquipmentEffectType.InventorySlotExpansion;

    [Tooltip("Valor do efeito (ex: 5 para +5 slots, 20 para +20 HP)")]
    public float effectValue = 5f;

    [Header("Tipo de Melhoria")]
    [Tooltip("Se verdadeiro, esta melhoria pertence à Base (não precisa ser equipada/desequipada e tem aba própria no Crafting).")]
    public bool isBaseUpgrade = false;

    [Header("Limites")]
    [Tooltip("Quantas cópias desta melhoria o jogador pode possuir (0 = ilimitado)")]
    public int maxStack = 0;
}
