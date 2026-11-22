using UnityEngine;

/// <summary>Contra-ataque unico, chamado pela transicao Defend -> Stunned.</summary>
[RequireComponent(typeof(PeixeAI), typeof(DummyHealth))]
public class PeixeEliteOverride : MonoBehaviour
{
    [Range(3, 36)] public int spikeCount = 12;
    private PeixeAI baseAI;
    private DummyHealth health;

    void Awake()
    {
        baseAI = GetComponent<PeixeAI>();
        health = GetComponent<DummyHealth>();
    }
    void OnEnable() { health.onDeath += CancelDefense; }
    void OnDisable() { health.onDeath -= CancelDefense; }
    void CancelDefense() { baseAI.StopAllCoroutines(); }

    public void FireDeflationBurst()
    {
        if (!isActiveAndEnabled || health.CurrentHealth <= 0 || baseAI.espinhoPrefab == null) return;
        int count = Mathf.Clamp(spikeCount, 3, 36);
        Vector3 origin = transform.position;
        origin.y = baseAI.firePoint != null ? baseAI.firePoint.position.y : transform.position.y + 1f;
        Collider[] ownerColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < count; i++)
        {
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y + i * 360f / count, 0f);
            GameObject spike = Instantiate(baseAI.espinhoPrefab, origin, rotation);
            Espinho projectile = spike.GetComponent<Espinho>();
            if (projectile != null)
            {
                projectile.owner = gameObject;
                projectile.speed = baseAI.projectileSpeed;
            }
            foreach (Collider projectileCollider in spike.GetComponentsInChildren<Collider>(true))
                foreach (Collider ownerCollider in ownerColliders)
                    Physics.IgnoreCollision(ownerCollider, projectileCollider, true);
        }
    }
}
