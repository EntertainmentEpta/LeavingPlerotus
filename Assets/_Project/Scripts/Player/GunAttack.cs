using UnityEngine;

public class GunAttack : MonoBehaviour
{
    [Header("Disparo")]
    public GunProjectile projectilePrefab;
    public Transform muzzle;
    public float fireCooldown = 0.35f;
    public int baseDamage = 25;
    public int maxExtraProjectiles = 5;

    [Header("Mira")]
    public bool useMuzzleForward = true;
    public Transform aimTransform;

    private float nextFireTime;
    private Transform ownerRoot;
    private PlayerAttributesOffensive offensiveAttributes;

    private void Awake()
    {
        ownerRoot = transform.root;
        offensiveAttributes = ownerRoot.GetComponentInChildren<PlayerAttributesOffensive>();
    }

    private void Update()
    {
        if (projectilePrefab == null || Time.time < nextFireTime) return;
        if (!Input.GetMouseButton(0)) return;

        Fire();
        nextFireTime = Time.time + Mathf.Max(0.01f, fireCooldown);
    }

    public void Fire()
    {
        if (projectilePrefab == null) return;

        PlayerAttributesOffensive attributes = offensiveAttributes != null
            ? offensiveAttributes
            : PlayerAttributesOffensive.Instance;

        float damageMultiplier = attributes != null ? attributes.baseDamageMultiplier : 1f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        float spread = attributes != null ? attributes.spread : 0f;
        int extraProjectiles = GetExtraProjectileCount(attributes);

        for (int index = 0; index <= extraProjectiles; index++)
        {
            float angle = GetSpreadAngle(index, extraProjectiles, spread);
            SpawnProjectile(damage, angle, attributes);
        }
    }

    private int GetExtraProjectileCount(PlayerAttributesOffensive attributes)
    {
        if (attributes == null) return 0;

        int extraProjectiles = 0;
        while (extraProjectiles < Mathf.Max(0, maxExtraProjectiles)
               && Random.value <= Mathf.Clamp01(attributes.multiShotChance / 100f))
        {
            extraProjectiles++;
        }

        return extraProjectiles;
    }

    private float GetSpreadAngle(int index, int totalExtraProjectiles, float spread)
    {
        if (index == 0 || totalExtraProjectiles == 0) return 0f;

        float position = (index - (totalExtraProjectiles + 1) * 0.5f) * 2f;
        return position * spread;
    }

    private void SpawnProjectile(int damage, float spreadAngle, PlayerAttributesOffensive attributes)
    {
        Transform spawnPoint = muzzle != null ? muzzle : transform;
        Vector3 direction = GetFireDirection(spawnPoint);
        direction = Quaternion.AngleAxis(spreadAngle, Vector3.up) * direction;

        GunProjectile projectile = Instantiate(
            projectilePrefab,
            spawnPoint.position,
            Quaternion.LookRotation(direction, Vector3.up));

        float range = attributes != null ? attributes.weaponRangeProjectile : 10f;
        int piercing = attributes != null ? attributes.piercing : 0;
        projectile.Initialize(damage, range, piercing, ownerRoot);
    }

    private Vector3 GetFireDirection(Transform spawnPoint)
    {
        if (useMuzzleForward) return spawnPoint.forward.normalized;

        Transform target = aimTransform != null ? aimTransform : ownerRoot;
        return target != null ? target.forward.normalized : spawnPoint.forward.normalized;
    }
}