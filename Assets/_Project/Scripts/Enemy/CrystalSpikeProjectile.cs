using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CrystalSpikeProjectile : ShieldProjectile
{
    protected override int ReflectedDamage => damage;
    protected override void Redirect(Vector3 aim)
    {
        direction = aim;
        transform.forward = aim;
        if (rb != null && !rb.isKinematic) rb.linearVelocity = aim * speed;
    }
    [HideInInspector] public int damage = 8;

    private Vector3 direction = Vector3.forward;
    private float speed = 20f;
    private float lifetime = 4f;
    private Rigidbody rb;

    public void Launch(Vector3 direction, float speed, float lifetime)
    {
        this.direction = direction.normalized;
        this.speed = speed;
        this.lifetime = lifetime;

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = this.direction * this.speed;
        }

        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (rb == null || rb.isKinematic)
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    private void HandleHit(GameObject hitObject)
    {
        if (hitObject == null || HandleShieldContact(hitObject)) return;
        if (owner != null && (hitObject == owner || hitObject.transform.IsChildOf(owner.transform))) return;

        PlayerHealth playerHealth = hitObject.GetComponent<PlayerHealth>() ?? hitObject.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, owner != null ? owner : gameObject);
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject);
    }
}
