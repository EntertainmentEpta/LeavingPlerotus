using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema de debuffs do jogador.
/// Gerencia slow, roubo de buffs de velocidade e restauração.
/// Adicionado ao GameObject do Player.
/// </summary>
public class PlayerDebuffs : MonoBehaviour
{
    private PlayerAttributesDefensive playerAttributes;
    private PlayerM playerMovement;

    // Rastreamento de slow ativo
    private bool isSlowed = false;
    private float currentSlowPercent = 0f;

    // Rastreamento de buffs roubados (para restauração ao derrotar o Bismutado)
    private List<StolenBuff> stolenBuffs = new List<StolenBuff>();

    private struct StolenBuff
    {
        public float speedValue;        // Quanto de speed foi roubado
        public GameObject thief;         // Quem roubou (Bismutado)
    }

    void Start()
    {
        playerAttributes = GetComponent<PlayerAttributesDefensive>();
        playerMovement = GetComponent<PlayerM>();

        if (playerAttributes == null)
            Debug.LogWarning("[PLAYER DEBUFFS] PlayerAttributesDefensive não encontrado!");
    }

    private void OnDisable()
    {
        ResetAllDebuffs();
    }

    // ==================== SLOW ====================

    /// <summary>
    /// Aplica slow ao jogador. Se já está com slow, atualiza para o maior.
    /// </summary>
    /// <param name="percent">Percentual de redução (0.2 = 20%, 0.4 = 40%)</param>
    public void ApplySlow(float percent)
    {
        if (playerAttributes == null) return;

        // Se tiver o Agente de Diluição de Hifas do Robozinho equipado, reduz a intensidade do slow em 30%
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.HasSlowResistance)
        {
            percent *= 0.70f;
        }

        // Se já está com slow, remove o anterior antes de aplicar o novo
        if (isSlowed)
        {
            RemoveSlowInternal();
        }

        currentSlowPercent = percent;
        isSlowed = true;

        // Aplica redução no speedMultiplier
        // Ex: speedMultiplier era 1.5, slow de 20% → 1.5 * 0.8 = 1.2
        playerAttributes.speedMultiplier *= (1f - percent);

        Debug.Log($"[DEBUFF] Slow aplicado: -{percent * 100}% velocidade | speedMultiplier: {playerAttributes.speedMultiplier:F2}");
    }

    /// <summary>
    /// Remove o slow atual do jogador.
    /// </summary>
    public void RemoveSlow()
    {
        if (!isSlowed) return;
        RemoveSlowInternal();
        Debug.Log($"[DEBUFF] Slow removido | speedMultiplier restaurado: {playerAttributes.speedMultiplier:F2}");
    }

    private void RemoveSlowInternal()
    {
        if (playerAttributes == null || !isSlowed) return;

        // Reverte: se aplicamos * (1 - percent), agora dividimos por (1 - percent)
        float restoreFactor = 1f / (1f - currentSlowPercent);
        playerAttributes.speedMultiplier *= restoreFactor;

        isSlowed = false;
        currentSlowPercent = 0f;
    }

    // ==================== ROUBO DE BUFF ====================

    /// <summary>
    /// Tenta roubar um buff de velocidade do player.
    /// Retorna true se havia um buff para roubar, false se não tinha.
    /// </summary>
    /// <param name="thief">O inimigo que está roubando (Bismutado)</param>
    /// <returns>True se roubou buff, false se player não tinha buff de speed</returns>
    public bool TryStealSpeedBuff(GameObject thief)
    {
        if (playerAttributes == null) return false;

        // Verifica se o player tem algum buff de speed (speedMultiplier > 1.0)
        if (playerAttributes.speedMultiplier > 1.01f) // margem para floating point
        {
            // Calcula quanto de buff tem acima de 1.0
            float buffAmount = playerAttributes.speedMultiplier - 1.0f;

            // Registra o roubo
            stolenBuffs.Add(new StolenBuff
            {
                speedValue = buffAmount,
                thief = thief
            });

            // Remove o buff do player (volta para 1.0)
            playerAttributes.speedMultiplier = 1.0f;

            Debug.Log($"[DEBUFF] Buff de velocidade ROUBADO! -{buffAmount:F2} | Ladrão: {thief.name}");
            return true;
        }

        Debug.Log("[DEBUFF] Player não tem buff de velocidade para roubar.");
        return false;
    }

    /// <summary>
    /// Restaura todos os buffs roubados por um inimigo específico.
    /// Chamado quando o Bismutado é derrotado.
    /// </summary>
    /// <param name="thief">O inimigo que havia roubado os buffs</param>
    public void RestoreStolenBuffs(GameObject thief)
    {
        if (playerAttributes == null) return;

        float totalRestored = 0f;

        for (int i = stolenBuffs.Count - 1; i >= 0; i--)
        {
            if (stolenBuffs[i].thief == thief || stolenBuffs[i].thief == null)
            {
                totalRestored += stolenBuffs[i].speedValue;
                stolenBuffs.RemoveAt(i);
            }
        }

        if (totalRestored > 0f)
        {
            playerAttributes.speedMultiplier += totalRestored;
            Debug.Log($"[DEBUFF] Buffs restaurados! +{totalRestored:F2} velocidade | speedMultiplier: {playerAttributes.speedMultiplier:F2}");
        }
    }

    /// <summary>
    /// Restaura TODOS os buffs roubados, independente do ladrão.
    /// </summary>
    public void RestoreAllStolenBuffs()
    {
        if (playerAttributes == null) return;

        float totalRestored = 0f;
        foreach (var buff in stolenBuffs)
        {
            totalRestored += buff.speedValue;
        }
        stolenBuffs.Clear();

        if (totalRestored > 0f)
        {
            playerAttributes.speedMultiplier += totalRestored;
            Debug.Log($"[DEBUFF] TODOS os buffs restaurados! +{totalRestored:F2} velocidade");
        }
    }

    /// <summary>
    /// Limpa completamente quaisquer debuffs, lentidões e referências acumuladas ao morrer ou voltar para a Base.
    /// </summary>
    public void ResetAllDebuffs()
    {
        if (isSlowed && playerAttributes != null && currentSlowPercent > 0f && currentSlowPercent < 1f)
        {
            float restoreFactor = 1f / (1f - currentSlowPercent);
            playerAttributes.speedMultiplier *= restoreFactor;
        }
        isSlowed = false;
        currentSlowPercent = 0f;
        stolenBuffs.Clear();
        Debug.Log("[PLAYER DEBUFFS] Todos os debuffs e lentidões foram resetados.");
    }

    // ==================== QUERIES ====================

    /// <summary>
    /// Verifica se o player está atualmente com slow.
    /// </summary>
    public bool IsSlowed() { return isSlowed; }

    /// <summary>
    /// Verifica se o player tem algum buff de velocidade (speedMultiplier > 1.0).
    /// </summary>
    public bool HasSpeedBuff()
    {
        if (playerAttributes == null) return false;
        return playerAttributes.speedMultiplier > 1.01f;
    }

    /// <summary>
    /// Retorna o multiplicador de velocidade atual do player.
    /// </summary>
    public float GetCurrentSpeedMultiplier()
    {
        if (playerAttributes == null) return 1f;
        return playerAttributes.speedMultiplier;
    }
}
