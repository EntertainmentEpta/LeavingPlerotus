using UnityEngine;

// Shared allegiance and collision handling for enemy shots that the glaive can intercept.
public abstract class ShieldProjectile : MonoBehaviour
{
    [HideInInspector] public GameObject owner;
    public bool IsFriendly { get; private set; }
    private bool spent;
    protected abstract void Redirect(Vector3 direction);
    protected abstract int ReflectedDamage { get; }

    public bool Reflect(GameObject defender)
    {
        if (spent || IsFriendly || defender == null) return false;
        Vector3 direction = -transform.forward;
        if (owner != null)
        {
            Collider source = owner.GetComponentInChildren<Collider>();
            Vector3 target = source != null ? source.bounds.center : owner.transform.position;
            direction = target - transform.position;
            foreach (Collider shot in GetComponentsInChildren<Collider>())
                foreach (Collider body in owner.GetComponentsInChildren<Collider>())
                    if (Physics.GetIgnoreCollision(shot, body)) Physics.IgnoreCollision(shot, body, false);
        }
        if (direction.sqrMagnitude < 0.0001f) direction = defender.transform.forward;
        foreach (Collider shot in GetComponentsInChildren<Collider>())
            foreach (Collider body in defender.GetComponentsInChildren<Collider>())
                if (shot != body) Physics.IgnoreCollision(shot, body, true);
        owner = defender;
        IsFriendly = true;
        Redirect(direction.normalized);
        return true;
    }

    protected bool HandleShieldContact(GameObject other)
    {
        if (spent || !gameObject.activeInHierarchy) return true;
        var weapon = other.GetComponentInParent<BoomerangWeapon>();
        if (weapon != null && weapon.CurrentState == BoomerangState.SpinningShield)
        {
            weapon.InterceptProjectile(this);
            return true;
        }
        var player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            if (IsFriendly) return true;
            var shield = BoomerangWeapon.ActiveShieldOn(player);
            if (shield != null)
            {
                shield.InterceptProjectile(this);
                return IsFriendly || !gameObject.activeInHierarchy;
            }
        }
        if (!IsFriendly) return false;
        var enemy = other.GetComponentInParent<DummyHealth>();
        var swarm = other.GetComponentInParent<ShardSwarmHealth>();
        if (enemy != null || swarm != null)
        {
            spent = true;
            if (enemy != null) enemy.TakeDamage(ReflectedDamage, isProjectile: true);
            else swarm.TakeDamage(ReflectedDamage);
            Destroy(gameObject);
            return true;
        }
        return false;
    }
}
