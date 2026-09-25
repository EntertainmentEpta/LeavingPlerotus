using UnityEngine;
using System.Collections;

/// <summary>
/// Status de Envenenado (Debuff do Peixe).
///  • Dura 4 segundos por padrão.
///  • Causa dano por segundo (3 HP/tick) e aplica redução de velocidade (20% slow).
///  • Se o jogador for atingido novamente enquanto envenenado, o timer é RESTAURADO.
///
/// Padrão baseado em ElectrocutedStatus.cs:
///  • Componente adicionado dinamicamente via ApplyPoison() estático.
///  • Coroutine interna faz o DoT tick a tick.
///  • Restaura velocidade normal ao expirar ou ser destruído.
/// </summary>
public class PoisonedStatus : MonoBehaviour
{
    [Header("Configuração do Veneno")]
    [Tooltip("Dano por tick (1 tick = 1 segundo).")]
    private int damagePerTick = 3;

    [Tooltip("Redução de velocidade durante o veneno (0.20 = 20%).")]
    private float slowPercent = 0.20f;

    [Tooltip("Duração total do envenenamento em segundos.")]
    private float duration = 4.0f;

    private float timer = 0f;

    private PlayerHealth playerHealth;
    private PlayerDebuffs playerDebuffs;
    private PlayerM playerM;
    private Coroutine poisonCoroutine;

    // ── API Estática ────────────────────────────────────────────────
    /// <summary>
    /// Aplica (ou restaura) o status de envenenamento no jogador.
    /// Chamado pelo Espinho.cs e PeixeAI.cs ao atingir o jogador.
    /// </summary>
    /// <param name="playerObj">GameObject raiz do jogador (que contém PlayerHealth).</param>
    /// <param name="damage">Dano por tick (padrão: 3).</param>
    /// <param name="slow">Percentual de slow (0.20 = 20%).</param>
    /// <param name="statusDuration">Duração total do envenenamento em segundos.</param>
    public static void ApplyPoison(GameObject playerObj, int damage, float slow, float statusDuration)
    {
        if (playerObj == null) return;

        PoisonedStatus status = playerObj.GetComponent<PoisonedStatus>();
        if (status == null)
        {
            status = playerObj.AddComponent<PoisonedStatus>();
        }

        status.RefreshStatus(damage, slow, statusDuration);
    }

    // ── Refresh (reaplica/restaura o timer) ─────────────────────────
    public void RefreshStatus(int damage, float slow, float statusDuration)
    {
        damagePerTick = (damage > 0) ? damage : 3;
        slowPercent = (slow > 0f) ? slow : 0.20f;
        duration = statusDuration;

        // Restaura o timer toda vez que o jogador é atingido novamente
        timer = duration;

        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? GetComponentInChildren<PlayerHealth>();
        if (playerDebuffs == null) playerDebuffs = GetComponent<PlayerDebuffs>() ?? GetComponentInParent<PlayerDebuffs>() ?? GetComponentInChildren<PlayerDebuffs>();
        if (playerM == null) playerM = GetComponent<PlayerM>() ?? GetComponentInParent<PlayerM>() ?? GetComponentInChildren<PlayerM>();

        // Aplica redução de velocidade
        if (playerM != null)
        {
            playerM.debuffSpeedMultiplier = (1f - slowPercent);
        }
        if (playerDebuffs != null)
        {
            playerDebuffs.ApplySlow(slowPercent);
        }

        Debug.LogWarning($"🐡☠️ [STATUS ENVENENADO ATIVO] Velocidade do Player reduzida em {slowPercent * 100}%! | Dano: {damagePerTick}/s | Duração: {duration}s");

        // Inicia a coroutine de DoT (apenas se não estiver já rodando)
        if (poisonCoroutine == null)
        {
            poisonCoroutine = StartCoroutine(PoisonRoutine());
        }
    }

    // ── Coroutine de Dano por Tempo ─────────────────────────────────
    private IEnumerator PoisonRoutine()
    {
        float tickTimer = 0f;

        // Primeiro tick imediato ao ser envenenado
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damagePerTick, gameObject);
        }

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            tickTimer += Time.deltaTime;

            // Dano por segundo durante o status de envenenamento
            if (tickTimer >= 1.0f)
            {
                tickTimer = 0f;
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damagePerTick, gameObject);
                }
            }

            yield return null;
        }

        // Fim do envenenamento: restaura velocidade normal
        RestoreSpeed();

        poisonCoroutine = null;
        Debug.Log("🐡 [STATUS ENVENENADO] Expirou. Velocidade restaurada.");
        Destroy(this);
    }

    // ── Restauração de Velocidade ───────────────────────────────────
    private void RestoreSpeed()
    {
        if (playerM != null)
        {
            playerM.debuffSpeedMultiplier = 1.0f;
        }
        if (playerDebuffs != null)
        {
            playerDebuffs.RemoveSlow();
        }
    }

    void OnDestroy()
    {
        RestoreSpeed();
    }
}
