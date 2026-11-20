using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Preenche o espaço vazio (void) ao redor e abaixo das salas da GameScene com
/// opções escaláveis para desempenho (apenas Fog, Abismo Leve ou Abismo Completo com Bruma)
/// e Vignette URP em tempo real (Etapa 3).
/// </summary>
public class AbyssBackdropController : MonoBehaviour
{
    public enum BackdropQualityMode
    {
        [Tooltip("Zero malhas extras ou transparência: aplica apenas Fog + cor da câmera (ideal para PCs fracos).")]
        ApenasFogLeve = 0,
        [Tooltip("Fog + 1 plano opaco Unlit no fundo do abismo (ótimo equilíbrio visual e FPS).")]
        AbismoLeve = 1,
        [Tooltip("Fog + fundo + camadas animadas de bruma transparente + partículas.")]
        CompletoComBruma = 2
    }

    [Header("Controle Geral (Ao Vivo no Play Mode)")]
    [Tooltip("Liga/desliga o abismo + fog para comparar facilmente durante o teste.")]
    public bool enableAbyssAndFog = true;

    [Tooltip("Perfil de desempenho do fundo: ApenasFogLeve é praticamente custo zero de GPU.")]
    public BackdropQualityMode qualityMode = BackdropQualityMode.AbismoLeve;

    [Header("Plano de Abismo (Abaixo de Y = 0)")]
    public float abyssSize = 120f;
    public float floorDepthOffset = -14f;
    public float floorTextureTileWorldSize = 24f;
    public Color abyssFloorColor = new Color(0.055f, 0.05f, 0.095f, 1f);

    [Header("Camadas de Bruma (Apenas em CompletoComBruma)")]
    public bool enableMistLayers = true;
    public float mistLayer1DepthOffset = -3.8f;
    public float mistLayer2DepthOffset = -7.8f;
    public float mistTileWorldSize = 34f;
    public Color mistLayer1Color = new Color(0.11f, 0.095f, 0.19f, 0.32f);
    public Color mistLayer2Color = new Color(0.075f, 0.07f, 0.135f, 0.52f);
    public Vector2 mistLayer1ScrollSpeed = new Vector2(0.012f, 0.008f);
    public Vector2 mistLayer2ScrollSpeed = new Vector2(-0.009f, 0.005f);

    [Header("Partículas de Profundidade (Apenas em CompletoComBruma)")]
    public bool enableAbyssParticles = true;
    public float particlesDepthOffset = -3.0f;
    public Color particleColor = new Color(0.45f, 0.38f, 0.78f, 0.35f);

    [Header("Fog da Cena")]
    public bool enableFog = true;
    public FogMode fogMode = FogMode.Linear;
    public Color fogColor = new Color(0.082f, 0.076f, 0.132f, 1f);
    [Tooltip("Distância a partir da câmera onde a neblina começa (mantenha > 22 para não escurecer a sala atual).")]
    public float linearFogStart = 23.5f;
    [Tooltip("Distância onde o abismo desaparece totalmente na neblina.")]
    public float linearFogEnd = 48f;
    [Range(0.005f, 0.12f)]
    public float expFogDensity = 0.03f;
    public bool syncCameraBackgroundWithFog = true;

    [Header("Etapa 3: Vignette & Brilho Geral (URP Post-Processing)")]
    [Tooltip("Liga/desliga a vinheta suave nas bordas da tela em tempo real.")]
    public bool enableVignette = true;
    public Color vignetteColor = new Color(0.022f, 0.018f, 0.050f, 1f);
    [Range(0f, 1f)]
    public float vignetteIntensity = 0.54f;
    [Range(0.05f, 1f)]
    public float vignetteSmoothness = 0.34f;
    [Range(0f, 1f)]
    public float vignetteRoundness = 0f;
    [Tooltip("Compensação de exposição para manter o centro e o gameplay bem claros mesmo com bordas fortes.")]
    [Range(-0.5f, 1f)]
    public float postExposureLift = 0.30f;

    private static Texture2D sharedFloorNoiseTex;
    private static Texture2D sharedMistNoiseTex;

    private GameObject backdropRoot;
    private Transform floorTransform;
    private Transform mist1Transform;
    private Transform mist2Transform;
    private Transform particlesTransform;
    private ParticleSystem abyssParticles;

    private Material floorMat;
    private Material mist1Mat;
    private Material mist2Mat;
    private Material particleMat;

    private Volume vignetteVolume;
    private VolumeProfile vignetteProfile;
    private Vignette vignetteOverride;
    private ColorAdjustments colorAdjustmentsOverride;

    private Transform cachedPlayerTransform;
    private Camera cachedCamera;
    private UniversalAdditionalCameraData cachedCameraData;
    private Color originalCamBgColor;
    private bool capturedOriginalCamColor;
    private bool originalRenderPostProcessing;
    private bool capturedOriginalPostProcessing;

    private bool originalFogEnabled;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalFogDensity;
    private float originalFogStart;
    private float originalFogEnd;
    private bool capturedOriginalFog;

    private bool isDirty = true;
    private bool lastAbyssEnabled;
    private BackdropQualityMode lastQualityMode;
    private bool lastVignetteEnabled;

    private void Awake()
    {
        CaptureOriginalRenderSettings();
        backdropRoot = new GameObject("AbyssBackdrop_Runtime");
        backdropRoot.layer = 2; // Ignore Raycast
        SyncFromGraphicsSettings();
    }

    private void OnEnable()
    {
        GameGraphicsSettings.OnSettingsChanged += HandleGraphicsSettingsChanged;
        SyncFromGraphicsSettings();
        ApplyAllSettings();
    }

    private void OnDisable()
    {
        GameGraphicsSettings.OnSettingsChanged -= HandleGraphicsSettingsChanged;
        RestoreOriginalRenderSettings();
        RestoreCameraPostProcessing();
        if (backdropRoot != null) backdropRoot.SetActive(false);
        if (vignetteVolume != null) vignetteVolume.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        GameGraphicsSettings.OnSettingsChanged -= HandleGraphicsSettingsChanged;
        RestoreOriginalRenderSettings();
        RestoreCameraPostProcessing();

        if (vignetteVolume != null) Destroy(vignetteVolume.gameObject);
        if (vignetteProfile != null) Destroy(vignetteProfile);
        if (backdropRoot != null) Destroy(backdropRoot);
        if (floorMat != null) Destroy(floorMat);
        if (mist1Mat != null) Destroy(mist1Mat);
        if (mist2Mat != null) Destroy(mist2Mat);
        if (particleMat != null) Destroy(particleMat);
    }

    private void HandleGraphicsSettingsChanged()
    {
        SyncFromGraphicsSettings();
        ApplyAllSettings();
    }

    private void SyncFromGraphicsSettings()
    {
        int mode = GameGraphicsSettings.AbyssFogMode;
        enableAbyssAndFog = mode != 0;
        if (mode == 1) qualityMode = BackdropQualityMode.ApenasFogLeve;
        else if (mode == 2) qualityMode = BackdropQualityMode.AbismoLeve;
        else if (mode >= 3) qualityMode = BackdropQualityMode.CompletoComBruma;

        enableVignette = GameGraphicsSettings.PostProcessEnabled;
        isDirty = true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        isDirty = true;
    }

    private void LateUpdate()
    {
        if (isDirty
            || lastAbyssEnabled != enableAbyssAndFog
            || lastQualityMode != qualityMode
            || lastVignetteEnabled != enableVignette
            || cachedCamera == null)
        {
            ApplyAllSettings();
            isDirty = false;
        }

        if (enableAbyssAndFog && qualityMode != BackdropQualityMode.ApenasFogLeve)
        {
            UpdateFollowAndScrolling();
        }
    }

    private void CaptureOriginalRenderSettings()
    {
        if (capturedOriginalFog) return;
        capturedOriginalFog = true;
        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogStart = RenderSettings.fogStartDistance;
        originalFogEnd = RenderSettings.fogEndDistance;
    }

    private void RestoreOriginalRenderSettings()
    {
        if (!capturedOriginalFog) return;
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogStartDistance = originalFogStart;
        RenderSettings.fogEndDistance = originalFogEnd;

        if (capturedOriginalCamColor && cachedCamera != null)
        {
            cachedCamera.backgroundColor = originalCamBgColor;
        }
    }

    private void RestoreCameraPostProcessing()
    {
        if (capturedOriginalPostProcessing && cachedCameraData != null)
        {
            cachedCameraData.renderPostProcessing = originalRenderPostProcessing;
        }
    }

    private void EnsureFloorBuilt()
    {
        if (floorTransform != null || backdropRoot == null) return;

        if (sharedFloorNoiseTex == null)
        {
            sharedFloorNoiseTex = GenerateSeamlessNoiseTexture(96, 5, 0.35f, 1.0f, isAlphaMask: false, seed: 17);
        }

        // Opaque Unlit evita custo de iluminação PBR e overdraw em PCs fracos
        Shader unlitOpaque = FindFirstValidShader("Universal Render Pipeline/Unlit", "Unlit/Texture", "Standard");
        floorMat = new Material(unlitOpaque);
        SetMaterialTexture(floorMat, sharedFloorNoiseTex);
        floorTransform = CreateQuadLayer("Abyss_DeepFloor", floorMat);
    }

    private void EnsureMistAndParticlesBuilt()
    {
        if (backdropRoot == null) return;

        if (mist1Transform == null || mist2Transform == null)
        {
            if (sharedMistNoiseTex == null)
            {
                sharedMistNoiseTex = GenerateSeamlessNoiseTexture(96, 4, 0.0f, 1.0f, isAlphaMask: true, seed: 53);
            }

            Shader unlitTransparent = FindFirstValidShader("Universal Render Pipeline/Unlit", "Unlit/Transparent", "Standard");
            if (mist1Transform == null)
            {
                mist1Mat = CreateTransparentMistMaterial(unlitTransparent, sharedMistNoiseTex);
                mist1Transform = CreateQuadLayer("Abyss_MistLayer_Near", mist1Mat);
            }
            if (mist2Transform == null)
            {
                mist2Mat = CreateTransparentMistMaterial(unlitTransparent, sharedMistNoiseTex);
                mist2Transform = CreateQuadLayer("Abyss_MistLayer_Deep", mist2Mat);
            }
        }

        if (abyssParticles == null)
        {
            BuildAbyssParticles();
        }
    }

    private Transform CreateQuadLayer(string objName, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objName;
        quad.layer = 2; // Ignore Raycast
        quad.transform.SetParent(backdropRoot.transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Collider col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        return quad.transform;
    }

    private void BuildAbyssParticles()
    {
        GameObject psObj = new GameObject("Abyss_AmbientParticles");
        psObj.layer = 2;
        psObj.transform.SetParent(backdropRoot.transform, false);
        particlesTransform = psObj.transform;

        abyssParticles = psObj.AddComponent<ParticleSystem>();
        var main = abyssParticles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;

        var emission = abyssParticles.emission;
        emission.rateOverTime = 10f;

        var shape = abyssParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(44f, 4f, 44f);

        var colOverLife = abyssParticles.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0.8f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colOverLife.color = grad;

        var psRend = psObj.GetComponent<ParticleSystemRenderer>();
        Shader psShader = FindFirstValidShader("Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Unlit", "Sprites/Default");
        particleMat = new Material(psShader);
        if (particleMat.HasProperty("_Surface")) particleMat.SetFloat("_Surface", 1f);
        if (particleMat.HasProperty("_Blend")) particleMat.SetFloat("_Blend", 2f);
        if (particleMat.HasProperty("_SrcBlend")) particleMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (particleMat.HasProperty("_DstBlend")) particleMat.SetInt("_DstBlend", (int)BlendMode.One);
        if (particleMat.HasProperty("_ZWrite")) particleMat.SetInt("_ZWrite", 0);
        particleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMat.renderQueue = 3001;
        psRend.sharedMaterial = particleMat;
        psRend.shadowCastingMode = ShadowCastingMode.Off;
        psRend.receiveShadows = false;
    }

    private void ApplyAllSettings()
    {
        lastAbyssEnabled = enableAbyssAndFog;
        lastQualityMode = qualityMode;
        lastVignetteEnabled = enableVignette;

        ResolveCamera();

        if (backdropRoot != null)
        {
            backdropRoot.SetActive(enableAbyssAndFog && qualityMode != BackdropQualityMode.ApenasFogLeve);
        }

        if (!enableAbyssAndFog)
        {
            RestoreOriginalRenderSettings();
        }
        else
        {
            ApplyFogAndCameraSettings();

            bool showFloor = qualityMode == BackdropQualityMode.AbismoLeve || qualityMode == BackdropQualityMode.CompletoComBruma;
            bool showFullEffects = qualityMode == BackdropQualityMode.CompletoComBruma;

            if (showFloor)
            {
                EnsureFloorBuilt();
                if (floorTransform != null)
                {
                    floorTransform.gameObject.SetActive(true);
                    floorTransform.localScale = new Vector3(abyssSize, abyssSize, 1f);
                    SetMaterialColor(floorMat, abyssFloorColor);
                }
            }
            else if (floorTransform != null)
            {
                floorTransform.gameObject.SetActive(false);
            }

            if (showFullEffects)
            {
                EnsureMistAndParticlesBuilt();
            }

            if (mist1Transform != null)
            {
                mist1Transform.gameObject.SetActive(showFullEffects && enableMistLayers);
                mist1Transform.localScale = new Vector3(abyssSize, abyssSize, 1f);
                SetMaterialColor(mist1Mat, mistLayer1Color);
            }

            if (mist2Transform != null)
            {
                mist2Transform.gameObject.SetActive(showFullEffects && enableMistLayers);
                mist2Transform.localScale = new Vector3(abyssSize, abyssSize, 1f);
                SetMaterialColor(mist2Mat, mistLayer2Color);
            }

            if (abyssParticles != null)
            {
                particlesTransform.gameObject.SetActive(showFullEffects && enableAbyssParticles);
                var main = abyssParticles.main;
                main.startColor = particleColor;
            }

            if (qualityMode != BackdropQualityMode.ApenasFogLeve)
            {
                UpdateFollowAndScrolling();
            }
        }

        ApplyVignetteSettings();
    }

    private void ApplyVignetteSettings()
    {
        if (!enableVignette)
        {
            if (vignetteVolume != null) vignetteVolume.gameObject.SetActive(false);
            ResolveCamera();
            if (cachedCameraData != null) cachedCameraData.renderPostProcessing = false;
            return;
        }

        EnsureCameraPostProcessingEnabled();
        EnsureVignetteVolumeBuilt();

        if (vignetteVolume != null)
        {
            vignetteVolume.gameObject.SetActive(true);
        }

        if (vignetteOverride != null)
        {
            vignetteOverride.active = true;
            vignetteOverride.color.Override(vignetteColor);
            vignetteOverride.intensity.Override(vignetteIntensity);
            vignetteOverride.smoothness.Override(vignetteSmoothness);
            vignetteOverride.rounded.Override(vignetteRoundness > 0.5f);
        }

        if (colorAdjustmentsOverride != null)
        {
            bool applyLift = Mathf.Abs(postExposureLift) > 0.001f;
            colorAdjustmentsOverride.active = applyLift;
            colorAdjustmentsOverride.postExposure.Override(postExposureLift);
        }
    }

    private void EnsureVignetteVolumeBuilt()
    {
        if (vignetteVolume != null && vignetteOverride != null && colorAdjustmentsOverride != null) return;

        GameObject volObj = new GameObject("Abyss_PostProcessVignette");
        volObj.layer = 0;
        volObj.transform.SetParent(transform, false);

        vignetteVolume = volObj.AddComponent<Volume>();
        vignetteVolume.isGlobal = true;
        vignetteVolume.priority = 10f;
        vignetteVolume.weight = 1f;

        vignetteProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignetteOverride = vignetteProfile.Add<Vignette>(overrides: true);
        colorAdjustmentsOverride = vignetteProfile.Add<ColorAdjustments>(overrides: true);
        vignetteVolume.sharedProfile = vignetteProfile;
    }

    private void ResolveCamera()
    {
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera != null && cachedCameraData == null)
        {
            cachedCameraData = cachedCamera.GetComponent<UniversalAdditionalCameraData>();
        }
    }

    private void EnsureCameraPostProcessingEnabled()
    {
        ResolveCamera();
        if (cachedCamera == null) return;

        if (cachedCameraData == null)
        {
            cachedCameraData = cachedCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        if (!capturedOriginalPostProcessing)
        {
            capturedOriginalPostProcessing = true;
            originalRenderPostProcessing = cachedCameraData.renderPostProcessing;
        }

        cachedCameraData.renderPostProcessing = true;
    }

    private void ApplyFogAndCameraSettings()
    {
        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogStartDistance = linearFogStart;
            RenderSettings.fogEndDistance = Mathf.Max(linearFogStart + 1f, linearFogEnd);
            RenderSettings.fogDensity = expFogDensity;
        }
        else
        {
            RenderSettings.fog = false;
        }

        ResolveCamera();
        if (cachedCamera != null)
        {
            if (!capturedOriginalCamColor)
            {
                capturedOriginalCamColor = true;
                originalCamBgColor = cachedCamera.backgroundColor;
            }

            if (syncCameraBackgroundWithFog && enableFog)
            {
                cachedCamera.backgroundColor = fogColor;
            }
            else if (capturedOriginalCamColor)
            {
                cachedCamera.backgroundColor = originalCamBgColor;
            }
        }
    }

    private void UpdateFollowAndScrolling()
    {
        Vector3 anchorXZ = ResolveAnchorXZ();
        float baseY = (LevelGenerator.Instance != null) ? LevelGenerator.Instance.roomFloorY : 0f;

        if (floorTransform != null && floorTransform.gameObject.activeSelf)
        {
            floorTransform.position = new Vector3(anchorXZ.x, baseY + floorDepthOffset, anchorXZ.z);
            float floorTile = Mathf.Max(1f, floorTextureTileWorldSize);
            float floorTilesAcross = abyssSize / floorTile;
            SetTextureTilingAndOffset(
                floorMat,
                new Vector2(floorTilesAcross, floorTilesAcross),
                new Vector2(anchorXZ.x / floorTile, anchorXZ.z / floorTile));
        }

        if (qualityMode != BackdropQualityMode.CompletoComBruma) return;

        float mistTile = Mathf.Max(1f, mistTileWorldSize);
        float mistTilesAcross = abyssSize / mistTile;

        if (mist1Transform != null && enableMistLayers)
        {
            mist1Transform.position = new Vector3(anchorXZ.x, baseY + mistLayer1DepthOffset, anchorXZ.z);
            Vector2 worldUv1 = new Vector2(anchorXZ.x / mistTile, anchorXZ.z / mistTile);
            SetTextureTilingAndOffset(
                mist1Mat,
                new Vector2(mistTilesAcross, mistTilesAcross),
                worldUv1 + mistLayer1ScrollSpeed * Time.time);
        }

        if (mist2Transform != null && enableMistLayers)
        {
            mist2Transform.position = new Vector3(anchorXZ.x, baseY + mistLayer2DepthOffset, anchorXZ.z);
            Vector2 worldUv2 = new Vector2(anchorXZ.x / mistTile, anchorXZ.z / mistTile);
            SetTextureTilingAndOffset(
                mist2Mat,
                new Vector2(mistTilesAcross * 0.75f, mistTilesAcross * 0.75f),
                worldUv2 * 0.75f + mistLayer2ScrollSpeed * Time.time);
        }

        if (particlesTransform != null && enableAbyssParticles)
        {
            particlesTransform.position = new Vector3(anchorXZ.x, baseY + particlesDepthOffset, anchorXZ.z);
        }
    }

    private Vector3 ResolveAnchorXZ()
    {
        if (cachedPlayerTransform == null)
        {
            PlayerM pm = Object.FindFirstObjectByType<PlayerM>();
            if (pm != null)
            {
                cachedPlayerTransform = pm.transform;
            }
            else
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) cachedPlayerTransform = tagged.transform;
            }
        }

        if (cachedPlayerTransform != null) return cachedPlayerTransform.position;
        ResolveCamera();
        if (cachedCamera != null) return cachedCamera.transform.position;
        return Vector3.zero;
    }

    private static Shader FindFirstValidShader(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Shader s = Shader.Find(names[i]);
            if (s != null) return s;
        }
        return Shader.Find("Hidden/InternalErrorShader");
    }

    private static Material CreateTransparentMistMaterial(Shader shader, Texture2D tex)
    {
        Material mat = new Material(shader);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        SetMaterialTexture(mat, tex);
        return mat;
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    private static void SetMaterialTexture(Material mat, Texture2D tex)
    {
        if (mat == null || tex == null) return;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
    }

    private static void SetTextureTilingAndOffset(Material mat, Vector2 tiling, Vector2 offset)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureOffset("_BaseMap", offset);
        }
        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTextureScale("_MainTex", tiling);
            mat.SetTextureOffset("_MainTex", offset);
        }
    }

    private static Texture2D GenerateSeamlessNoiseTexture(int size, int baseFrequency, float minVal, float maxVal, bool isAlphaMask, int seed)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;

                float f1 = PeriodicValueNoise(u, v, baseFrequency, seed);
                float f2 = PeriodicValueNoise(u, v, baseFrequency * 2, seed + 31);
                float f3 = PeriodicValueNoise(u, v, baseFrequency * 4, seed + 79);
                float fbm = f1 * 0.55f + f2 * 0.30f + f3 * 0.15f;

                if (isAlphaMask)
                {
                    float alpha = Mathf.SmoothStep(0.25f, 0.85f, fbm);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Lerp(minVal, maxVal, alpha));
                }
                else
                {
                    float lum = Mathf.Lerp(minVal, maxVal, fbm);
                    pixels[y * size + x] = new Color(lum * 0.9f, lum * 0.88f, lum, 1f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(updateMipmaps: true);
        return tex;
    }

    private static float PeriodicValueNoise(float u, float v, int period, int seed)
    {
        float px = u * period;
        float py = v * period;

        int x0 = Mathf.FloorToInt(px) % period;
        int y0 = Mathf.FloorToInt(py) % period;
        int x1 = (x0 + 1) % period;
        int y1 = (y0 + 1) % period;

        float tx = px - Mathf.Floor(px);
        float ty = py - Mathf.Floor(py);
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        float c00 = Hash01(x0, y0, seed);
        float c10 = Hash01(x1, y0, seed);
        float c01 = Hash01(x0, y1, seed);
        float c11 = Hash01(x1, y1, seed);

        float nx0 = Mathf.Lerp(c00, c10, tx);
        float nx1 = Mathf.Lerp(c01, c11, tx);
        return Mathf.Lerp(nx0, nx1, ty);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= (h >> 16);
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}
