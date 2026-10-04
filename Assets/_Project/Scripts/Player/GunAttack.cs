using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

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

    [Header("HUD de Carga")]
    [Tooltip("Sprites ordenados de 0 a 4 cargas por esfera.")]
    public Sprite[] batteryOrbSprites = new Sprite[5];
    public Color batteryOrbTint = Color.white;
    [Min(16f)] public float batteryOrbSize = 96f;
    [Min(0f)] public float batteryOrbSpacing = 4f;
    public Vector2 batteryHUDOffset = new Vector2(24f, -24f);

    [Header("Mira")]
    public bool useMuzzleForward = true;
    public Transform aimTransform;

    [Header("Indicador de Alcance")]
    public Color aimConeColor = new Color(0.05f, 0.75f, 1f, 0.2f);
    [Min(3)] public int aimConeSegments = 32;
    public float aimConeXOffset;
    [Min(0f)] public float aimConeGroundOffset = 0.04f;
    [Min(0f)] public float minimumAimConeHalfAngle = 5f;

    private float nextFireTime;
    private float batteryRechargeElapsed;
    private int batteryCharge;
    private Transform ownerRoot;
    private Transform cachedLookupRoot;
    private PlayerAttributesOffensive offensiveAttributes;
    private PlayerM playerMovement;
    private DashM dashController;
    private Animator playerAnimator;
    private Camera aimCamera;
    private Vector3 currentAimPoint;
    private Vector3 currentAimDirection;
    private bool hasCurrentAimPoint;
    private bool isAimSessionActive;
    private GameObject aimConeObject;
    private Transform aimConeTransform;
    private Mesh aimConeMesh;
    private Material aimConeMaterial;
    private float cachedConeRange = -1f;
    private float cachedConeHalfAngle = -1f;
    private Canvas batteryHUDCanvas;
    private RectTransform batteryHUDRoot;
    private readonly List<Image> batteryOrbImages = new List<Image>();
    private int lastHUDCharge = -1;
    private int lastHUDCapacity = -1;
    private Color lastHUDTint = new Color(-1f, -1f, -1f, -1f);

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
        currentAimDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        CreateAimCone();
    }

    private void Update()
    {
        RefreshOwnerReferences();
        EnsureBatteryHUD();
        UpdateBattery();
        RefreshBatteryHUD();
        SetAnimatorBool("IsReloading", IsRecharging);

        bool firingInput = projectilePrefab != null && Input.GetMouseButton(0);
        bool dashing = dashController != null && dashController.isDashing;
        if (firingInput && !IsRecharging && !isAimSessionActive) BeginAimSession();

        hasCurrentAimPoint = firingInput && !IsRecharging && TryGetAimPoint(out currentAimPoint);
        if (firingInput && !IsRecharging && !dashing) UpdateCurrentAimDirection();
        if (!firingInput && !IsRecharging) isAimSessionActive = false;
        Vector3 aimDirection = currentAimDirection.sqrMagnitude > 0.0001f
            ? currentAimDirection
            : GetFallbackAimDirection();
        bool plasmaActionLocked = IsRecharging || (firingInput && !dashing);
        if (playerMovement != null)
        {
            playerMovement.SetPlasmaFiring(plasmaActionLocked);
            if (plasmaActionLocked) playerMovement.SetPlasmaAimDirection(aimDirection);
        }

        UpdateAimCone(firingInput && !IsRecharging && !dashing && hasCurrentAimPoint, aimDirection);

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
        if (aimConeObject != null) aimConeObject.SetActive(false);
        if (batteryHUDCanvas != null) batteryHUDCanvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (aimConeMesh != null) Destroy(aimConeMesh);
        if (aimConeMaterial != null) Destroy(aimConeMaterial);
        if (batteryHUDCanvas != null) Destroy(batteryHUDCanvas.gameObject);
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

    private void EnsureBatteryHUD()
    {
        bool hasAllSprites = batteryOrbSprites != null && batteryOrbSprites.Length >= 5;
        if (hasAllSprites)
        {
            for (int index = 0; index < 5; index++)
            {
                if (batteryOrbSprites[index] == null)
                {
                    hasAllSprites = false;
                    break;
                }
            }
        }

        if (playerMovement == null || !hasAllSprites)
        {
            if (batteryHUDCanvas != null) batteryHUDCanvas.gameObject.SetActive(false);
            return;
        }

        if (batteryHUDCanvas == null)
        {
            GameObject canvasObject = new GameObject("PlasmaBatteryHUD");
            batteryHUDCanvas = canvasObject.AddComponent<Canvas>();
            batteryHUDCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            batteryHUDCanvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            GameObject rootObject = new GameObject("BatteryOrbs", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            batteryHUDRoot = rootObject.GetComponent<RectTransform>();
            batteryHUDRoot.anchorMin = new Vector2(0f, 1f);
            batteryHUDRoot.anchorMax = new Vector2(0f, 1f);
            batteryHUDRoot.pivot = new Vector2(0f, 1f);
        }

        batteryHUDCanvas.gameObject.SetActive(true);
    }

    private void RefreshBatteryHUD()
    {
        if (batteryHUDCanvas == null || !batteryHUDCanvas.gameObject.activeInHierarchy) return;

        int capacity = Mathf.Max(1, batteryCapacity);
        batteryCharge = Mathf.Clamp(batteryCharge, 0, capacity);
        if (capacity == lastHUDCapacity && batteryCharge == lastHUDCharge && batteryOrbTint == lastHUDTint) return;

        int orbCount = Mathf.CeilToInt(capacity / 4f);
        while (batteryOrbImages.Count < orbCount)
        {
            GameObject orbObject = new GameObject("ChargeOrb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            orbObject.transform.SetParent(batteryHUDRoot, false);
            Image orbImage = orbObject.GetComponent<Image>();
            orbImage.preserveAspect = true;
            orbImage.raycastTarget = false;
            batteryOrbImages.Add(orbImage);
        }

        while (batteryOrbImages.Count > orbCount)
        {
            Image lastImage = batteryOrbImages[batteryOrbImages.Count - 1];
            batteryOrbImages.RemoveAt(batteryOrbImages.Count - 1);
            Destroy(lastImage.gameObject);
        }

        float iconSize = Mathf.Max(16f, batteryOrbSize);
        float spacing = Mathf.Max(0f, batteryOrbSpacing);
        float stride = iconSize + spacing;
        float availableWidth = batteryHUDCanvas.pixelRect.width / Mathf.Max(0.01f, batteryHUDCanvas.scaleFactor);
        int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth - Mathf.Abs(batteryHUDOffset.x) * 2f) / stride));
        columns = Mathf.Min(columns, Mathf.Max(1, orbCount));
        int rows = Mathf.CeilToInt(orbCount / (float)columns);

        for (int index = 0; index < orbCount; index++)
        {
            int column = index % columns;
            int row = index / columns;
            RectTransform orbRect = batteryOrbImages[index].rectTransform;
            orbRect.anchorMin = new Vector2(0f, 1f);
            orbRect.anchorMax = new Vector2(0f, 1f);
            orbRect.pivot = new Vector2(0f, 1f);
            orbRect.sizeDelta = new Vector2(iconSize, iconSize);
            orbRect.anchoredPosition = new Vector2(column * stride, -row * stride);

            int orbCapacity = Mathf.Min(4, capacity - index * 4);
            int orbCharge = Mathf.Clamp(batteryCharge - index * 4, 0, orbCapacity);
            batteryOrbImages[index].sprite = batteryOrbSprites[orbCharge];
            batteryOrbImages[index].color = batteryOrbTint;
        }

        batteryHUDRoot.anchoredPosition = batteryHUDOffset;
        batteryHUDRoot.sizeDelta = new Vector2(
            columns * stride - spacing,
            rows * stride - spacing);
        lastHUDCapacity = capacity;
        lastHUDCharge = batteryCharge;
        lastHUDTint = batteryOrbTint;
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        if (aimCamera == null) aimCamera = Camera.main;
        if (aimCamera == null) return false;

        Transform aimOrigin = playerMovement != null ? playerMovement.transform : ownerRoot;
        if (aimOrigin == null) return false;

        Ray mouseRay = aimCamera.ScreenPointToRay(Input.mousePosition);
        Plane aimPlane = new Plane(Vector3.up, aimOrigin.position);
        if (!aimPlane.Raycast(mouseRay, out float distance) || distance < 0f) return false;

        aimPoint = mouseRay.GetPoint(distance);
        return true;
    }

    private void BeginAimSession()
    {
        currentAimDirection = GetFallbackAimDirection();
        isAimSessionActive = true;
    }

    private void UpdateCurrentAimDirection()
    {
        if (!isAimSessionActive || !hasCurrentAimPoint) return;

        Transform aimSource = muzzle != null ? muzzle : playerMovement != null ? playerMovement.transform : ownerRoot;
        Vector3 origin = aimSource.position;
        Vector3 desiredDirection = Vector3.ProjectOnPlane(currentAimPoint - origin, Vector3.up);
        if (desiredDirection.sqrMagnitude <= 0.0001f) return;

        currentAimDirection = desiredDirection.normalized;
    }

    private float GetAimConeHalfAngle()
    {
        PlayerAttributesOffensive attributes = offensiveAttributes != null
            ? offensiveAttributes
            : PlayerAttributesOffensive.Instance;
        float spread = attributes != null ? Mathf.Max(0f, attributes.spread) : 0f;
        int extraProjectiles = Mathf.Max(0, maxExtraProjectiles);
        float maximumShotAngle = extraProjectiles > 0
            ? Mathf.Abs(GetSpreadAngle(extraProjectiles, extraProjectiles, spread))
            : 0f;
        return Mathf.Min(180f, Mathf.Max(minimumAimConeHalfAngle, maximumShotAngle));
    }

    private Vector3 GetFallbackAimDirection()
    {
        Transform fallback = muzzle != null ? muzzle : transform;
        Vector3 direction = Vector3.ProjectOnPlane(fallback.forward, Vector3.up);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private void CreateAimCone()
    {
        Shader coneShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (coneShader == null) return;

        aimConeObject = new GameObject("PlasmaAimRange");
        aimConeTransform = aimConeObject.transform;
        MeshFilter meshFilter = aimConeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = aimConeObject.AddComponent<MeshRenderer>();
        aimConeMesh = new Mesh { name = "PlasmaAimRangeMesh" };
        meshFilter.sharedMesh = aimConeMesh;

        aimConeMaterial = new Material(coneShader) { name = "PlasmaAimRangeMaterial" };
        aimConeMaterial.color = aimConeColor;
        if (aimConeMaterial.HasProperty("_Surface")) aimConeMaterial.SetFloat("_Surface", 1f);
        if (aimConeMaterial.HasProperty("_Blend")) aimConeMaterial.SetFloat("_Blend", 0f);
        if (aimConeMaterial.HasProperty("_SrcBlend")) aimConeMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (aimConeMaterial.HasProperty("_DstBlend")) aimConeMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (aimConeMaterial.HasProperty("_ZWrite")) aimConeMaterial.SetInt("_ZWrite", 0);
        if (aimConeMaterial.HasProperty("_Cull")) aimConeMaterial.SetInt("_Cull", (int)CullMode.Off);
        if (aimConeMaterial.HasProperty("_BaseColor")) aimConeMaterial.SetColor("_BaseColor", aimConeColor);
        if (aimConeMaterial.HasProperty("_Color")) aimConeMaterial.SetColor("_Color", aimConeColor);
        if (coneShader.name == "Universal Render Pipeline/Unlit")
            aimConeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        aimConeMaterial.renderQueue = (int)RenderQueue.Transparent;

        meshRenderer.sharedMaterial = aimConeMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sortingOrder = 10;
        aimConeObject.SetActive(false);
    }

    private void UpdateAimCone(bool visible, Vector3 aimDirection)
    {
        if (aimConeObject == null) return;
        if (aimConeObject.activeSelf != visible) aimConeObject.SetActive(visible);
        if (!visible) return;

        PlayerAttributesOffensive attributes = offensiveAttributes != null
            ? offensiveAttributes
            : PlayerAttributesOffensive.Instance;
        float range = attributes != null ? Mathf.Max(0.1f, attributes.weaponRangeProjectile) : 10f;
        float halfAngle = GetAimConeHalfAngle();

        Vector3 playerPosition = playerMovement != null ? playerMovement.transform.position : ownerRoot.position;
        Vector3 coneOrigin = muzzle != null ? muzzle.position : playerPosition;
        coneOrigin.y = playerPosition.y + aimConeGroundOffset;
        coneOrigin.x += aimConeXOffset;
        aimConeTransform.position = coneOrigin;
        aimConeTransform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);

        if (!Mathf.Approximately(range, cachedConeRange) || !Mathf.Approximately(halfAngle, cachedConeHalfAngle))
        {
            BuildAimConeMesh(range, halfAngle);
            cachedConeRange = range;
            cachedConeHalfAngle = halfAngle;
        }
    }

    private void BuildAimConeMesh(float range, float halfAngle)
    {
        int segments = Mathf.Max(3, aimConeSegments);
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        for (int index = 0; index <= segments; index++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, index / (float)segments) * Mathf.Deg2Rad;
            vertices[index + 1] = new Vector3(Mathf.Sin(angle) * range, 0f, Mathf.Cos(angle) * range);
            if (index == segments) continue;

            int triangleIndex = index * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = index + 1;
            triangles[triangleIndex + 2] = index + 2;
        }

        aimConeMesh.Clear();
        aimConeMesh.vertices = vertices;
        aimConeMesh.triangles = triangles;
        aimConeMesh.RecalculateNormals();
        aimConeMesh.RecalculateBounds();
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
        if (!isAimSessionActive)
        {
            BeginAimSession();
            hasCurrentAimPoint = TryGetAimPoint(out currentAimPoint);
            UpdateCurrentAimDirection();
        }

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
        if (isAimSessionActive && currentAimDirection.sqrMagnitude > 0.0001f)
            return currentAimDirection.normalized;

        if (!useMuzzleForward && aimTransform != null)
            return Vector3.ProjectOnPlane(aimTransform.forward, Vector3.up).normalized;

        Vector3 fallbackDirection = Vector3.ProjectOnPlane(spawnPoint.forward, Vector3.up);
        return fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector3.forward;
    }
}