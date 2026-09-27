using System.Collections.Generic;
using UnityEngine;

public class GunProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 18f;
    public float lifetime = 5f;

    private int damage;
    private int maxTargets;
    private int bouncesRemaining;
    private float criticalChance;
    private float criticalMultiplier;
    private float knockback;
    private float bounceChance;
    private float slowOnHit;
    private float elapsedTime;
    private Transform ownerRoot;
    private readonly HashSet<DummyHealth> hitTargets = new HashSet<DummyHealth>();

    public void Initialize(int projectileDamage, float range, int piercing, Transform owner,
        float critChance, float critMultiplier, float projectileKnockback,
        float projectileBounceChance, int projectileBounceCount, float projectileSlowOnHit)
    {
        damage = Mathf.Max(1, projectileDamage);
        lifetime = speed > 0f ? Mathf.Max(0.1f, range / speed) : lifetime;
        maxTargets = Mathf.Max(1, piercing + 1);
        criticalChance = Mathf.Clamp01(critChance / 100f);
        criticalMultiplier = Mathf.Max(1f, critMultiplier);
        knockback = Mathf.Max(0f, projectileKnockback);
        bounceChance = Mathf.Clamp01(projectileBounceChance / 100f);
        bouncesRemaining = Mathf.Max(0, projectileBounceCount);
        slowOnHit = Mathf.Clamp01(projectileSlowOnHit);
        ownerRoot = owner != null ? owner.root : null;
        elapsedTime = 0f;
    }

    private void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
        elapsedTime += Time.deltaTime;

        if (elapsedTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessHit(collision.collider);
    }

    private void ProcessHit(Collider other)
    {
        if (other == null) return;
        if (ownerRoot != null && other.transform.root == ownerRoot) return;

        DummyHealth target = other.GetComponent<DummyHealth>() ?? other.GetComponentInParent<DummyHealth>();
        if (target == null || hitTargets.Contains(target)) return;

        hitTargets.Add(target);
        bool isCritical = criticalChance >= 1f || Random.value < criticalChance;
        int hitDamage = isCritical ? Mathf.Max(1, Mathf.RoundToInt(damage * criticalMultiplier)) : damage;
        target.TakeDamage(hitDamage, isCritical);
        ApplyKnockback(target);
        if (slowOnHit > 0f) target.ApplySlow(slowOnHit, 2f);

        if (TryBounce()) return;

        if (hitTargets.Count >= maxTargets)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyKnockback(DummyHealth target)
    {
        if (knockback <= 0f) return;

        Rigidbody targetRigidbody = target.GetComponent<Rigidbody>() ?? target.GetComponentInParent<Rigidbody>();
        if (targetRigidbody == null || targetRigidbody.isKinematic) return;

        Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        targetRigidbody.AddForce(direction * (5f * knockback), ForceMode.Impulse);
    }

    private bool TryBounce()
    {
        if (bouncesRemaining <= 0 || Random.value >= bounceChance) return false;

        DummyHealth nearestTarget = null;
        float nearestDistance = float.PositiveInfinity;
        DummyHealth[] targets = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
        foreach (DummyHealth candidate in targets)
        {
            if (candidate == null || candidate.CurrentHealth <= 0 || hitTargets.Contains(candidate)) continue;
            if (ownerRoot != null && candidate.transform.root == ownerRoot) continue;

            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearestTarget = candidate;
        }

        if (nearestTarget == null) return false;

        Vector3 directionToTarget = nearestTarget.transform.position - transform.position;
        if (directionToTarget.sqrMagnitude <= 0f) return false;

        bouncesRemaining--;
        transform.forward = directionToTarget.normalized;
        return true;
    }
}