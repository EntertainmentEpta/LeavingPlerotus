using UnityEngine;

public sealed class GuidedCrystalProjectile : MonoBehaviour
{
    private const float Speed = 16f;
    private const float Lifetime = 4f;
    private const float DetectionRadius = 30f;

    private int damage;
    private Transform sourceRoot;
    private DummyHealth dummyTarget;
    private ShardSwarmHealth swarmTarget;
    private float nextTargetScanTime;
    private float elapsedTime;

    public static void Spawn(Vector3 position, Transform sourceRoot, int damage)
    {
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "GuidedCrystalProjectile";
        crystal.transform.position = position;
        crystal.transform.localScale = Vector3.one * 0.18f;
        Renderer renderer = crystal.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.2f, 0.85f, 1f, 1f);
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", new Color(0.05f, 0.4f, 1f, 1f));
        }

        SphereCollider collider = crystal.GetComponent<SphereCollider>();
        collider.isTrigger = true;

        Rigidbody rigidbody = crystal.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        GuidedCrystalProjectile projectile = crystal.AddComponent<GuidedCrystalProjectile>();
        projectile.sourceRoot = sourceRoot;
        projectile.damage = Mathf.Max(1, damage);
    }

    private void Update()
    {
        if (Time.time >= nextTargetScanTime)
        {
            FindTarget();
            nextTargetScanTime = Time.time + 0.1f;
        }

        Vector3 targetPosition = GetTargetPosition();
        if (targetPosition != Vector3.zero)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * Speed * Time.deltaTime;
            transform.forward = direction;
        }
        else
        {
            transform.position += transform.forward * Speed * Time.deltaTime;
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= Lifetime)
            Destroy(gameObject);
    }

    private void FindTarget()
    {
        dummyTarget = null;
        swarmTarget = null;
        float nearestDistanceSquared = DetectionRadius * DetectionRadius;

        foreach (DummyHealth candidate in FindObjectsByType<DummyHealth>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.CurrentHealth <= 0 || IsSource(candidate.transform)) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                dummyTarget = candidate;
            }
        }

        foreach (ShardSwarmHealth candidate in FindObjectsByType<ShardSwarmHealth>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.CurrentHealth <= 0 || IsSource(candidate.transform)) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                dummyTarget = null;
                swarmTarget = candidate;
            }
        }
    }

    private bool IsSource(Transform candidate)
    {
        return sourceRoot != null && candidate.root == sourceRoot;
    }

    private Vector3 GetTargetPosition()
    {
        if (dummyTarget != null) return dummyTarget.transform.position + Vector3.up * 0.8f;
        if (swarmTarget != null) return swarmTarget.transform.position + Vector3.up * 0.8f;
        return Vector3.zero;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || IsSource(other.transform)) return;

        DummyHealth target = other.GetComponent<DummyHealth>() ?? other.GetComponentInParent<DummyHealth>();
        if (target != null && target.CurrentHealth > 0)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        ShardSwarmHealth swarm = other.GetComponent<ShardSwarmHealth>() ?? other.GetComponentInParent<ShardSwarmHealth>();
        if (swarm != null && swarm.CurrentHealth > 0)
        {
            swarm.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
