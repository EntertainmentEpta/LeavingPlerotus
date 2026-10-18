using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class GunAttack : MonoBehaviour
{
    [Header("Disparo")]
    public GunProjectile projectilePrefab;
    public Transform muzzle;
    public ParticleSystem muzzleFlashVFX;
    public float fireCooldown = 0.40f;
    public int baseDamage = 22;
    public int maxExtraProjectiles = 5;

    [Header("Alcance do Projétil")]
    [Min(1f)] public float minimumProjectileRange = 18f;
    [Min(0f)] public float mapRangePadding = 5f;

    [Header("Bateria de Plasma")]
    [Min(1)] public int batteryCapacity = 6;
    [Tooltip("Tempo sem atirar para começar a recuperar cargas passivamente.")]
    [Min(0.1f)] public float passiveChargeRegenDelay = 2.0f;
    [Tooltip("Tempo para recuperar uma carga enquanto a bateria ainda não está vazia após o delay.")]
    [Min(0.05f)] public float passiveChargeRegenInterval = 0.8f;
    [Tooltip("Tempo da recarga manual punitiva após a bateria zerar.")]
    [Min(0.1f)] public float batteryRechargeDuration = 2.0f;
    public KeyCode reloadKey = KeyCode.R;
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
    [Min(0.1f)] public float minimumAimDistance = 1.25f;

    [Header("Indicador de Alcance")]
    public Color aimConeColor = new Color(0.05f, 0.75f, 1f, 0.2f);
    [Min(3)] public int aimConeSegments = 32;
    public float aimConeXOffset;
    [Min(0f)] public float aimConeGroundOffset = 0.04f;
    [Min(0f)] public float minimumAimConeHalfAngle = 5f;

    private float nextFireTime;
    private float lastFireTime = -999f;
    private float batteryRechargeElapsed;
    private float passiveChargeRegenElapsed;
    private int batteryCharge;
    private Transform ownerRoot;
    private Transform cachedLookupRoot;
    private PlayerAttributesOffensive offensiveAttributes;
    private PlayerM playerMovement;
    private DashM dashController;
    private Player_WeaponManager weaponManager;
    private Tier4EffectManager tier4EffectManager;
    private Animator playerAnimator;
    private Camera aimCamera;
    private Vector3 currentAimPoint;
    private Vector3 currentAimDirection;
    private bool hasCurrentAimPoint;
    private bool isAimSessionActive;
    private bool aimCursorOverrideActive;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
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
        weaponManager = ownerRoot.GetComponent<Player_WeaponManager>();
        tier4EffectManager = ownerRoot.GetComponent<Tier4EffectManager>();
        playerAnimator = ownerRoot.GetComponentInChildren<Animator>();
        batteryCharge = Mathf.Max(1, batteryCapacity);
        currentAimDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        CreateAimCone();

        if (muzzleFlashVFX != null)
        {
            var main = muzzleFlashVFX.main;
            main.playOnAwake = false;
        }

        SyncRechargeWithAnimation();
    }

    private void SyncRechargeWithAnimation()
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return;

        foreach (AnimationClip clip in playerAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.Contains("Reload") || clip.name.Contains("Reloading"))
            {
                batteryRechargeDuration = clip.length;
                Debug.Log($"[GunAttack] Tempo de recarga ajustado automaticamente para o tempo da animação: {clip.length}s");
                break;
            }
        }
    }

    private void OnEnable()
    {
        AimSettings.OnAimModeChanged += HandleAimModeChanged;
    }

    private void Update()
    {
        RefreshOwnerReferences();
        if (!IsEquippedInPlayerHand())
        {
            RestoreAimCursor();
            if (playerMovement != null) playerMovement.SetPlasmaFiring(false);
            if (aimConeObject != null) aimConeObject.SetActive(false);
            if (batteryHUDCanvas != null) batteryHUDCanvas.gameObject.SetActive(false);
            isAimSessionActive = false;
            return;
        }

        EnsureBatteryHUD();
        if (batteryCharge <= 0 && !IsRecharging && Input.GetKeyDown(reloadKey))
            BeginManualReload();
        UpdateBattery();
        RefreshBatteryHUD();
        SetAnimatorBool("IsReloading", IsRecharging);

        UpdateAimCursor();

        bool isUiOpen = IsAnyUIOpen();
        bool attackKeyPressed = false;
        if (weaponManager != null && weaponManager.attackScript != null)
        {
            attackKeyPressed = Input.GetKey(weaponManager.attackScript.attackKey);
        }

        bool firingInput = projectilePrefab != null && (Input.GetMouseButton(0) || attackKeyPressed) && !isUiOpen;
        bool dashing = dashController != null && dashController.isDashing;

        if (firingInput && !IsRecharging && !isAimSessionActive) BeginAimSession();

        if (AimSettings.IsMouseAim)
        {
            hasCurrentAimPoint = TryGetAimPoint(out currentAimPoint);
            if (firingInput && !IsRecharging && !dashing) UpdateCurrentAimDirection();
        }
        else
        {
            hasCurrentAimPoint = true;
            if (firingInput && !IsRecharging && !dashing)
            {
                currentAimDirection = GetPlayerDirectionAim();
            }
        }

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

        bool showCone = firingInput && !IsRecharging && !dashing && hasCurrentAimPoint;
        UpdateAimCone(showCone, aimDirection);

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
        AimSettings.OnAimModeChanged -= HandleAimModeChanged;
        RestoreAimCursor();
        if (playerMovement != null) playerMovement.SetPlasmaFiring(false);
        if (aimConeObject != null) aimConeObject.SetActive(false);
        if (batteryHUDCanvas != null) batteryHUDCanvas.gameObject.SetActive(false);
        isAimSessionActive = false;
    }

    private void HandleAimModeChanged(AimMode newMode)
    {
        if (IsEquippedInPlayerHand())
        {
            UpdateAimCursor();
        }
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
        weaponManager = currentRoot.GetComponentInChildren<Player_WeaponManager>();
        tier4EffectManager = currentRoot.GetComponentInChildren<Tier4EffectManager>();
        playerAnimator = currentRoot.GetComponentInChildren<Animator>();
    }

    private bool IsEquippedInPlayerHand()
    {
        if (!gameObject.activeInHierarchy || playerMovement == null || weaponManager == null)
            return false;

        if (!weaponManager.isWeaponDrawn || weaponManager.currentWeapon != gameObject)
            return false;

        return weaponManager.rightHand != null && transform.IsChildOf(weaponManager.rightHand);
    }

    private void UpdateAimCursor()
    {
        if (IsAnyUIOpen())
        {
            return;
        }

        if (AimSettings.IsMouseAim)
        {
            if (!aimCursorOverrideActive)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockState = Cursor.lockState;
                aimCursorOverrideActive = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreAimCursor();
        }
    }

    private void RestoreAimCursor()
    {
        if (!aimCursorOverrideActive) return;

        if (!IsAnyUIOpen())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        aimCursorOverrideActive = false;
    }

    private bool IsAnyUIOpen()
    {
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused) return true;
        if (CheatConsole.IsOpen) return true;
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen()) return true;
        if (SyntheticBagUI.Instance != null && SyntheticBagUI.Instance.IsOpen()) return true;
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) return true;
        if (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen) return true;
        if (EptinhoMenuController.instancia != null && EptinhoMenuController.instancia.IsOpen()) return true;
        if (MerchantUIController.HasInstance && MerchantUIController.Instance.IsUiOpen()) return true;
        if (DeathScreenUI.Instance != null && DeathScreenUI.Instance.deathPanel != null && DeathScreenUI.Instance.deathPanel.activeSelf) return true;
        if (VictoryScreenUI.Instance != null && VictoryScreenUI.Instance.victoryPanel != null && VictoryScreenUI.Instance.victoryPanel.activeSelf) return true;
        return false;
    }

    private Vector3 GetPlayerDirectionAim()
    {
        float h = 0f;
        float v = 0f;
        if (playerMovement != null)
        {
            if (Input.GetKey(playerMovement.keyRight)) h += 1f;
            if (Input.GetKey(playerMovement.keyLeft)) h -= 1f;
            if (Input.GetKey(playerMovement.keyUp)) v += 1f;
            if (Input.GetKey(playerMovement.keyDown)) v -= 1f;
        }

        float joyH = Input.GetAxisRaw("Horizontal");
        float joyV = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(joyH) > 0.2f) h = joyH;
        if (Mathf.Abs(joyV) > 0.2f) v = joyV;

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.sqrMagnitude > 0.05f)
        {
            return inputDir.normalized;
        }

        Transform origin = playerMovement != null ? playerMovement.transform : ownerRoot;
        if (origin != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
                return forward.normalized;
        }

        return GetFallbackAimDirection();
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
        Color displayTint = tier4EffectManager != null
            && tier4EffectManager.HasEffect(Tier4EffectType.HomingProjectiles)
                ? tier4EffectManager.homingBatteryOrbTint
                : batteryOrbTint;
        if (capacity == lastHUDCapacity && batteryCharge == lastHUDCharge && displayTint == lastHUDTint) return;

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
            batteryOrbImages[index].color = displayTint;
        }

        batteryHUDRoot.anchoredPosition = batteryHUDOffset;
        batteryHUDRoot.sizeDelta = new Vector2(
            columns * stride - spacing,
            rows * stride - spacing);
        lastHUDCapacity = capacity;
        lastHUDCharge = batteryCharge;
        lastHUDTint = displayTint;
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
        if (AimSettings.IsPlayerDirectionAim)
        {
            currentAimDirection = GetPlayerDirectionAim();
        }
        else if (TryGetAimPoint(out Vector3 aimPoint))
        {
            Transform aimOrigin = playerMovement != null ? playerMovement.transform : ownerRoot;
            if (aimOrigin != null)
            {
                Vector3 desired = Vector3.ProjectOnPlane(aimPoint - aimOrigin.position, Vector3.up);
                currentAimDirection = desired.sqrMagnitude > 0.01f ? desired.normalized : GetFallbackAimDirection();
            }
            else
            {
                currentAimDirection = GetFallbackAimDirection();
            }
        }
        else
        {
            currentAimDirection = GetFallbackAimDirection();
        }
        isAimSessionActive = true;
    }

    private void UpdateCurrentAimDirection()
    {
        if (!isAimSessionActive) return;

        if (AimSettings.IsPlayerDirectionAim)
        {
            currentAimDirection = GetPlayerDirectionAim();
            return;
        }

        if (!hasCurrentAimPoint) return;

        Transform aimOrigin = playerMovement != null ? playerMovement.transform : ownerRoot;
        if (aimOrigin == null) return;

        Vector3 desiredDirection = Vector3.ProjectOnPlane(currentAimPoint - aimOrigin.position, Vector3.up);
        float minimumDistance = Mathf.Max(0.1f, minimumAimDistance);
        if (desiredDirection.sqrMagnitude < minimumDistance * minimumDistance) return;

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

    private float GetEffectiveProjectileRange(PlayerAttributesOffensive attributes)
    {
        float attributeRange = attributes != null
            ? Mathf.Max(0.1f, attributes.weaponRangeProjectile)
            : 10f;
        float configuredRange = Mathf.Max(1f, minimumProjectileRange);

        if (playerMovement != null && LevelGenerator.Instance != null)
        {
            float mapRange = LevelGenerator.Instance.GetFarthestRoomDistance(playerMovement.transform.position);
            configuredRange = Mathf.Max(configuredRange, mapRange + Mathf.Max(0f, mapRangePadding));
        }

        return Mathf.Max(attributeRange, configuredRange);
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
        float range = GetEffectiveProjectileRange(attributes);
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
        if (IsRecharging || batteryCharge <= 0 || batteryCharge < amount) return false;

        batteryCharge -= amount;
        if (batteryCharge == 0) passiveChargeRegenElapsed = 0f;
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
            passiveChargeRegenElapsed = 0f;
        }

        if (batteryCharge != previousCharge)
            BatteryChanged?.Invoke(batteryCharge, Mathf.Max(1, batteryCapacity));
    }

    public bool Fire()
    {
        if (!isAimSessionActive)
        {
            BeginAimSession();
            if (AimSettings.IsMouseAim)
            {
                hasCurrentAimPoint = TryGetAimPoint(out currentAimPoint);
                UpdateCurrentAimDirection();
            }
            else
            {
                currentAimDirection = GetPlayerDirectionAim();
                hasCurrentAimPoint = true;
            }
        }

        if (projectilePrefab == null || !TryConsumeCharge(Mathf.Max(1, chargePerTrigger))) return false;
        lastFireTime = Time.time;

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

        if (muzzleFlashVFX != null)
        {
            muzzleFlashVFX.gameObject.SetActive(true);
            muzzleFlashVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlashVFX.Play(true);
        }

        SetAnimatorTrigger("Shoot");
        SetAnimatorBool("IsReloading", IsRecharging);
        return true;
    }

    /// <summary>
    /// Chamado via Animation Event na Unity para sincronizar com a animação de tiro (se houver).
    /// </summary>
    public void SpawnProjectileEvent()
    {
        if (muzzleFlashVFX != null)
        {
            muzzleFlashVFX.gameObject.SetActive(true);
            muzzleFlashVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlashVFX.Play(true);
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

        float range = GetEffectiveProjectileRange(attributes);
        bool homingEnabled = tier4EffectManager != null
            && tier4EffectManager.HasEffect(Tier4EffectType.HomingProjectiles);
        projectile.Initialize(damage, range, piercing, ownerRoot, critChance, critMultiplier,
            knockback, bounceChance, bounceCount, slowOnHit,
            homingEnabled,
            homingEnabled ? tier4EffectManager.homingDetectionRadius : 0f,
            homingEnabled ? tier4EffectManager.homingTurnSpeed : 0f,
            homingEnabled ? tier4EffectManager.homingTargetRefreshInterval : 0f);
    }

    private void BeginManualReload()
    {
        if (batteryCharge > 0 || IsRecharging) return;

        IsRecharging = true;
        batteryRechargeElapsed = 0f;
        passiveChargeRegenElapsed = 0f;
    }

    private void UpdateBattery()
    {
        int capacity = Mathf.Max(1, batteryCapacity);
        if (IsRecharging)
        {
            batteryRechargeElapsed += Time.deltaTime;
            if (batteryRechargeElapsed < Mathf.Max(0.1f, batteryRechargeDuration)) return;

            int previousCharge = batteryCharge;
            batteryCharge = capacity;
            IsRecharging = false;
            batteryRechargeElapsed = 0f;
            passiveChargeRegenElapsed = 0f;
            BatteryChanged?.Invoke(batteryCharge, Mathf.Max(1, batteryCapacity));
            return;
        }

        batteryCharge = Mathf.Clamp(batteryCharge, 0, capacity);
        if (batteryCharge <= 0) return;
        if (batteryCharge >= capacity)
        {
            passiveChargeRegenElapsed = 0f;
            return;
        }

        // Delay sem disparar para a bateria esfriar e iniciar a regeneração passiva
        if (Time.time - lastFireTime < passiveChargeRegenDelay)
        {
            passiveChargeRegenElapsed = 0f;
            return;
        }

        passiveChargeRegenElapsed += Time.deltaTime;
        float interval = Mathf.Max(0.05f, passiveChargeRegenInterval);
        int chargesToRestore = Mathf.FloorToInt(passiveChargeRegenElapsed / interval);
        if (chargesToRestore <= 0) return;

        int oldCharge = batteryCharge;
        batteryCharge = Mathf.Min(capacity, batteryCharge + chargesToRestore);
        passiveChargeRegenElapsed -= chargesToRestore * interval;
        if (batteryCharge != oldCharge)
            BatteryChanged?.Invoke(batteryCharge, capacity);
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