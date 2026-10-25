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
    private ShardSwarmHealth swarmHealth;

    // ── API Estática ────────────────────────────────────────────────
    /// <summary>
    /// Aplica (ou restaura) o veneno em um inimigo.
    /// </summary>
    public static void ApplyPoison(GameObject enemyObj, int damage, float statusDuration, float interval = 1f)
    {
        if (enemyObj == null) return;

        DummyHealth health = enemyObj.GetComponent<DummyHealth>()
                          ?? enemyObj.GetComponentInParent<DummyHealth>();
        ShardSwarmHealth swarm = enemyObj.GetComponent<ShardSwarmHealth>()
                              ?? enemyObj.GetComponentInParent<ShardSwarmHealth>();

        if ((health == null || health.CurrentHealth <= 0) && (swarm == null || swarm.CurrentHealth <= 0)) return;

        GameObject targetObj = health != null ? health.gameObject : swarm.gameObject;

        PoisonedEnemyStatus status = targetObj.GetComponent<PoisonedEnemyStatus>();
        if (status == null)
        {
            status = targetObj.AddComponent<PoisonedEnemyStatus>();
        }

        status.enemyHealth = health;
        status.swarmHealth = swarm;
        status.Refresh(damage, statusDuration, interval);
    }

    // ── Refresh (reaplica/restaura o timer) ─────────────────────────
    private void Refresh(int damage, float statusDuration, float interval)
    {
        damagePerTick = Mathf.Max(1, damage);
        duration = Mathf.Max(0.1f, statusDuration);
        tickInterval = Mathf.Max(0.1f, interval);
        remainingTime = duration;
        
        // Pequeno adiantamento para que o primeiro tick de veneno apareça rápido
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval * 0.4f;
        }
    }

    // ── Loop de Dano por Tempo ──────────────────────────────────────
    private void Update()
    {
        bool isAlive = (enemyHealth != null && enemyHealth.CurrentHealth > 0)
                    || (swarmHealth != null && swarmHealth.CurrentHealth > 0);

        if (!isAlive)
        {
            Destroy(this);
            return;
        }

        remainingTime -= Time.deltaTime;
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            
            if (enemyHealth != null && enemyHealth.CurrentHealth > 0)
            {
                enemyHealth.TakeDamage(damagePerTick, isCritical: false, isPoison: true);
            }
            else if (swarmHealth != null && swarmHealth.CurrentHealth > 0)
            {
                swarmHealth.TakeDamage(damagePerTick, isCritical: false, isPoison: true);
            }
        }

        if (remainingTime <= 0f)
        {
            Destroy(this);
        }
    }
}
