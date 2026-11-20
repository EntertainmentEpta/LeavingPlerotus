using UnityEngine;

[DisallowMultipleComponent]
public class BleedingEnemyStatus : MonoBehaviour
{
    private int damagePerTick;
    private float remainingTime;
    private float tickTimer;
    private DummyHealth enemyHealth;
    private ShardSwarmHealth swarmHealth;

    public static void ApplyBleed(GameObject enemyObj, int damage, float duration)
    {
        if (enemyObj == null) return;

        DummyHealth health = enemyObj.GetComponent<DummyHealth>()
                          ?? enemyObj.GetComponentInParent<DummyHealth>();
        ShardSwarmHealth swarm = enemyObj.GetComponent<ShardSwarmHealth>()
                              ?? enemyObj.GetComponentInParent<ShardSwarmHealth>();
        if ((health == null || health.CurrentHealth <= 0) &&
            (swarm == null || swarm.CurrentHealth <= 0))
        {
            return;
        }

        GameObject targetObj = health != null ? health.gameObject : swarm.gameObject;
        BleedingEnemyStatus status = targetObj.GetComponent<BleedingEnemyStatus>();
        if (status == null) status = targetObj.AddComponent<BleedingEnemyStatus>();

        status.enemyHealth = health;
        status.swarmHealth = swarm;
        status.damagePerTick = Mathf.Max(1, damage);
        status.remainingTime = Mathf.Max(0.1f, duration);
        status.tickTimer = 0f;
    }

    private void Update()
    {
        bool alive = (enemyHealth != null && enemyHealth.CurrentHealth > 0) ||
                     (swarmHealth != null && swarmHealth.CurrentHealth > 0);
        if (!alive)
        {
            Destroy(this);
            return;
        }

        remainingTime -= Time.deltaTime;
        tickTimer += Time.deltaTime;
        if (tickTimer >= 1f)
        {
            tickTimer -= 1f;
            if (enemyHealth != null && enemyHealth.CurrentHealth > 0)
                enemyHealth.TakeDamage(damagePerTick);
            else if (swarmHealth != null && swarmHealth.CurrentHealth > 0)
                swarmHealth.TakeDamage(damagePerTick);
        }

        if (remainingTime <= 0f) Destroy(this);
    }
}
