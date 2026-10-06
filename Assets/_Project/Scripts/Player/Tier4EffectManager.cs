using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerenciador central de efeitos especiais de itens Tier 4 (Lendários).
/// Fica no GameObject do Player (junto com InfusionManager, PlayerInventory, etc.)
/// 
/// Responsabilidades:
///   - Ativar/desativar efeitos T4 quando itens lendários são infundidos/removidos
///   - Manter registro de quais efeitos estão ativos
///   - Delegar para os scripts especializados de cada efeito
/// 
/// Uso:
///   1. Adicione este componente ao Player
///   2. O InfusionManager chama ActivateEffect/DeactivateEffect automaticamente
///   3. Cada efeito T4 tem seu próprio script (Ex: ExplosiveDashEffect) que é
///      ativado/desativado por este gerenciador
/// </summary>
public class Tier4EffectManager : MonoBehaviour
{
    [Header("Efeitos T4 Registrados")]
    [Tooltip("Referência ao ExplosiveDashEffect (pode estar no mesmo GameObject ou em filho).")]
    public ExplosiveDashEffect explosiveDashEffect;

    // Registro de efeitos ativos para consulta rápida
    private HashSet<Tier4EffectType> activeEffects = new HashSet<Tier4EffectType>();

    private void Start()
    {
        // Auto-detectar referências se não foram atribuídas no Inspector
        if (explosiveDashEffect == null)
        {
            explosiveDashEffect = GetComponent<ExplosiveDashEffect>() 
                ?? GetComponentInChildren<ExplosiveDashEffect>();
        }
    }

    /// <summary>
    /// Ativa um efeito especial T4. Chamado pelo InfusionManager ao infundir um item lendário.
    /// </summary>
    public void ActivateEffect(Tier4EffectType effectType)
    {
        if (effectType == Tier4EffectType.None) return;
        if (activeEffects.Contains(effectType))
        {
            Debug.LogWarning($"[Tier4EffectManager] Efeito {effectType} já está ativo!");
            return;
        }

        switch (effectType)
        {
            case Tier4EffectType.ExplosiveDash:
                if (explosiveDashEffect != null)
                {
                    explosiveDashEffect.ActivateEffect();
                    activeEffects.Add(effectType);
                    Debug.Log($"💎 [T4 EFFECT] {effectType} ATIVADO!");
                }
                else
                {
                    Debug.LogError("[Tier4EffectManager] ExplosiveDashEffect não encontrado no Player! Adicione o componente.");
                }
                break;

            default:
                Debug.LogWarning($"[Tier4EffectManager] Efeito T4 '{effectType}' não possui handler implementado.");
                break;
        }
    }

    /// <summary>
    /// Desativa um efeito especial T4. Chamado pelo InfusionManager ao remover infusão de item lendário.
    /// </summary>
    public void DeactivateEffect(Tier4EffectType effectType)
    {
        if (effectType == Tier4EffectType.None) return;
        if (!activeEffects.Contains(effectType))
        {
            Debug.LogWarning($"[Tier4EffectManager] Tentativa de desativar efeito {effectType} que não está ativo.");
            return;
        }

        switch (effectType)
        {
            case Tier4EffectType.ExplosiveDash:
                if (explosiveDashEffect != null)
                {
                    explosiveDashEffect.DeactivateEffect();
                    activeEffects.Remove(effectType);
                    Debug.Log($"💎 [T4 EFFECT] {effectType} DESATIVADO!");
                }
                break;
        }
    }

    /// <summary>
    /// Consulta se um efeito T4 específico está ativo.
    /// </summary>
    public bool HasEffect(Tier4EffectType effectType)
    {
        return activeEffects.Contains(effectType);
    }

    /// <summary>
    /// Retorna todos os efeitos T4 ativos.
    /// </summary>
    public HashSet<Tier4EffectType> GetActiveEffects()
    {
        return activeEffects;
    }

    /// <summary>
    /// Desativa todos os efeitos T4 ativos. Útil para resetar no início de uma nova run.
    /// </summary>
    public void DeactivateAllEffects()
    {
        // Cria cópia para iterar sem modificar a coleção
        var effectsCopy = new List<Tier4EffectType>(activeEffects);
        foreach (var effect in effectsCopy)
        {
            DeactivateEffect(effect);
        }
        Debug.Log("[Tier4EffectManager] Todos os efeitos T4 desativados.");
    }
}
