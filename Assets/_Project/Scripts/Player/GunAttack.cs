using UnityEngine;

public class GunAttack : MonoBehaviour
{
    [Header("Disparo")]
    public GunProjectile projectilePrefab;
    public Transform muzzle;
    public float fireCooldown = 0.35f;
    public int baseDamage = 25;
    public int maxExtraProjectiles = 5;

    [Header("Bateria de Plasma")]
    [Min(1)] public int batteryCapacity = 6;
    [Min(0.1f)] public float batteryRechargeDuration = 1.5f;
    [Min(1)] public int chargePerTrigger = 1;

    [Header("Mira")]
    public bool useMuzzleForward = true;
    public Transform aimTransform;

    private float nextFireTime;
    private float batteryRechargeElapsed;
    private int batteryCharge;
    private Transform ownerRoot;
    private Transform cachedLookupRoot;
    private PlayerAttributesOffensive offensiveAttributes;
    private PlayerM playerMovement;
    private DashM dashController;
    private Animator playerAnimator;

    public int BatteryCharge => batteryCharge;
    public bool IsRecharging { get; private set; }
    public event System.Action<int, int> BatteryChanged;

    private void Awake()
    {
        ownerRoot = transform.root;
        offensiveAttributes = ownerRoot.GetComponentInChildren<PlayerAttributesOffensive>();
        playerMovement = ownerRoot.GetComponent<PlayerM>();
        dashController = ownerRoot.GetComponent<DashM>();
        batteryCharge = Mathf.Max(1, batteryCapacity);
    }

    private void Update()
    {
        RefreshOwnerReferences();
        UpdateBattery();
        SetAnimatorBool("IsReloading", IsRecharging);

        bool firingInput = projectilePrefab != null && Input.GetMouseButton(0);
        bool dashing = dashController != null && dashController.isDashing;
        if (playerMovement != null) playerMovement.SetPlasmaFiring(firingInput && !dashing);

        if (!firingInput || dashing || Time.time < nextFireTime) return;

        if (Fire())
        {
            float attackSpeed = offensiveAttributes != null
                ? Mathf.Max(0.2f, offensiveAttributes.attackSpeedMelee)
                : 1f;
            nextFireTime = Time.time + Mathf.Max(0.01f, fireCooldown / attackSpeed);
        }
    }

    private void OnDisable()
    {
        if (playerMovement != null) playerMovement.SetPlasmaFiring(false);
    }

    private void RefreshOwnerReferences()
    {
        Transform currentRoot = transform.root;
        if (currentRoot == cachedLookupRoot) return;

        cachedLookupRoot = currentRoot;
        ownerRoot = currentRoot;
        offensiveAttributes = currentRoot.GetComponentInChildren<PlayerAttributesOffensive>();
        playerMovement = currentRoot.GetComponentInChildren<PlayerM>();
        dashController = currentRoot.GetComponentInChildren<DashM>();
        playerAnimator = currentRoot.GetComponentInChildren<Animator>();
    }

    public bool TryConsumeCharge(int amount = 1)
    {
        if (amount <= 0) return true;
        if (IsRecharging || batteryCharge < amount)
        {
            if (!IsRecharging) BeginRecharge();
            return false;
        }

        batteryCharge -= amount;
        if (batteryCharge == 0) BeginRecharge();
        BatteryChanged?.Invoke(batteryCharge, Mathf.Max(1, batteryCapacity));
        return true;
    }

    public void RechargeBattery(int amount)
    {
        if (amount <= 0) return;

        int previousCharge = batteryCharge;
        batteryCharge = Mathf.Min(Mathf.Max(1, batteryCapacity), batteryCharge + amount);
        if (batteryCharge > 0)
        {
            IsRecharging = false;
            batteryRechargeElapsed = 0f;
        }

        if (batteryCharge != previousCharge)
            BatteryChanged?.Invoke(batteryCharge, Mathf.Max(1, batteryCapacity));
    }

    public bool Fire()
    {
        if (projectilePrefab == null || !TryConsumeCharge(Mathf.Max(1, chargePerTrigger))) return false;

        PlayerAttributesOffensive attributes = offensiveAttributes != null
            ? offensiveAttributes
            : PlayerAttributesOffensive.Instance;

        float damageMultiplier = attributes != null ? attributes.baseDamageMultiplier : 1f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        float spread = attributes != null ? attributes.spread : 0f;
        int extraProjectiles = GetExtraProjectileCount(attributes);
        float critChance = attributes != null ? attributes.critChance : 0f;
        float critMultiplier = attributes != null ? attributes.critMultiplier : 1f;
        float knockback = attributes != null ? attributes.knockback : 1f;
        int piercing = attributes != null ? attributes.piercing : 0;
        float bounceChance = attributes != null ? attributes.bounceChance : 0f;
        int bounceCount = attributes != null ? attributes.bounceCount : 0;
        float slowOnHit = attributes != null ? attributes.slowOnHit : 0f;

        for (int index = 0; index <= extraProjectiles; index++)
        {
            float angle = GetSpreadAngle(index, extraProjectiles, spread);
            SpawnProjectile(damage, angle, attributes, critChance, critMultiplier, knockback,
                piercing, bounceChance, bounceCount, slowOnHit);
        }

        SetAnimatorTrigger("Shoot");
        SetAnimatorBool("IsReloading", IsRecharging);
        return true;
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

    private void SpawnProjectile(int damage, float spreadAngle, PlayerAttributesOffensive attributes,
        float critChance, float critMultiplier, float knockback, int piercing,
        float bounceChance, int bounceCount, float slowOnHit)
    {
        Transform spawnPoint = muzzle != null ? muzzle : transform;
        Vector3 direction = GetFireDirection(spawnPoint);
        direction = Quaternion.AngleAxis(spreadAngle, Vector3.up) * direction;

        GunProjectile projectile = Instantiate(
            projectilePrefab,
            spawnPoint.position,
            Quaternion.LookRotation(direction, Vector3.up));

        float range = attributes != null ? attributes.weaponRangeProjectile : 10f;
        projectile.Initialize(damage, range, piercing, ownerRoot, critChance, critMultiplier,
            knockback, bounceChance, bounceCount, slowOnHit);
    }

    private void BeginRecharge()
    {
        IsRecharging = true;
        batteryRechargeElapsed = 0f;
    }

    private void UpdateBattery()
    {
        if (!IsRecharging) return;

        batteryRechargeElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.1f, batteryRechargeDuration);
        int rechargedAmount = Mathf.FloorToInt(batteryRechargeElapsed / duration * Mathf.Max(1, batteryCapacity));
        int nextCharge = Mathf.Min(Mathf.Max(1, batteryCapacity), rechargedAmount);
        if (nextCharge != batteryCharge)
        {
            batteryCharge = nextCharge;
            BatteryChanged?.Invoke(batteryCharge, Mathf.Max(1, batteryCapacity));
        }

        if (batteryCharge >= Mathf.Max(1, batteryCapacity))
        {
            IsRecharging = false;
            batteryRechargeElapsed = 0f;
        }
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
            playerAnimator.SetTrigger(parameterName);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            playerAnimator.SetBool(parameterName, value);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return false;

        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType) return true;
        }

        return false;
    }

    private Vector3 GetFireDirection(Transform spawnPoint)
    {
        if (useMuzzleForward) return spawnPoint.forward.normalized;

        Transform target = aimTransform != null ? aimTransform : ownerRoot;
        return target != null ? target.forward.normalized : spawnPoint.forward.normalized;
    }
}