using UnityEngine;

/// <summary>
/// Status de Envenenado aplicado em INIMIGOS (efeito Tier 4 "Poison" do Peixe).
///  • Causa dano por tick (1 tick = tickInterval segundos) via DummyHealth.TakeDamage.
///  • Se o inimigo for atingido novamente enquanto envenenado, o timer é RESTAURADO (sem stack).
///  • Remove-se sozinho ao expirar ou quando o inimigo morre.
///
/// Padrão baseado em PoisonedStatus.cs (player):
///  • Componente adicionado dinamicamente via ApplyPoison() estático.
///
/// Uso: PoisonedEnemyStatus.ApplyPoison(enemyGameObject, damagePerTick, duration);
/// </summary>
[DisallowMultipleComponent]
public class PoisonedEnemyStatus : MonoBehaviour
{
    private int damagePerTick = 5;
    private float duration = 5f;
    private float tickInterval = 1f;

    private float remainingTime = 0f;
    private float tickTimer = 0f;

    private DummyHealth enemyHealth;

    // ── API Estática ────────────────────────────────────────────────
    /// <summary>
    /// Aplica (ou restaura) o veneno em um inimigo.
    /// </summary>
    /// <param name="enemyObj">GameObject do inimigo (ou filho/pai dele que contenha DummyHealth).</param>
    /// <param name="damage">Dano por tick.</param>
    /// <param name="statusDuration">Duração total em segundos.</param>
    /// <param name="interval">Intervalo entre ticks em segundos (padrão 1s).</param>
    public static void ApplyPoison(GameObject enemyObj, int damage, float statusDuration, float interval = 1f)
    {
        if (enemyObj == null) return;

        DummyHealth health = enemyObj.GetComponent<DummyHealth>()
                          ?? enemyObj.GetComponentInParent<DummyHealth>();
        if (health == null || health.CurrentHealth <= 0) return;

        // O status vive no mesmo GameObject do DummyHealth
        PoisonedEnemyStatus status = health.GetComponent<PoisonedEnemyStatus>();
        if (status == null)
        {
            status = health.gameObject.AddComponent<PoisonedEnemyStatus>();
        }

        status.enemyHealth = health;
        status.Refresh(damage, statusDuration, interval);
    }

    // ── Refresh (reaplica/restaura o timer) ─────────────────────────
    private void Refresh(int damage, float statusDuration, float interval)
    {
        damagePerTick = Mathf.Max(1, damage);
        duration = Mathf.Max(0.1f, statusDuration);
        tickInterval = Mathf.Max(0.1f, interval);
        remainingTime = duration;
    }

    // ── Loop de Dano por Tempo ──────────────────────────────────────
    private void Update()
    {
        if (enemyHealth == null || enemyHealth.CurrentHealth <= 0)
        {
            Destroy(this);
            return;
        }

        remainingTime -= Time.deltaTime;
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            enemyHealth.TakeDamage(damagePerTick, false);
        }

        if (remainingTime <= 0f)
        {
            Destroy(this);
        }
    }
}
