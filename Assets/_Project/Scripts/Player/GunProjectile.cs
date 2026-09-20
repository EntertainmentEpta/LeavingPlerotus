using System.Collections.Generic;
using UnityEngine;

public class GunProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 18f;
    public float lifetime = 5f;

    private int damage;
    private int maxTargets;
    private float elapsedTime;
    private Transform ownerRoot;
    private readonly HashSet<DummyHealth> hitTargets = new HashSet<DummyHealth>();

    public void Initialize(int projectileDamage, float range, int piercing, Transform owner)
    {
        damage = Mathf.Max(1, projectileDamage);
        lifetime = speed > 0f ? Mathf.Max(0.1f, range / speed) : lifetime;
        maxTargets = Mathf.Max(1, piercing + 1);
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
        target.TakeDamage(damage, false);

        if (hitTargets.Count >= maxTargets)
        {
            Destroy(gameObject);
        }
    }
}