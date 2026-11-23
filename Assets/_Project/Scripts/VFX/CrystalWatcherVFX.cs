using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VFX aprimorado do Crystal Watcher — combina prefabs URP reais (CFXR LightGlow / Electric / Impact)
/// com partículas de convergência, aura cristalina, luz dinâmica no núcleo e faíscas/descargas no impacto.
/// </summary>
public class CrystalWatcherVFX : MonoBehaviour
{
    [Header("Prefabs URP (CFXR)")]
    [Tooltip("Brilho contínuo no cristal central durante carregamento e disparo (ex.: CFXR3 LightGlow A Loop).")]
    public GameObject coreGlowLoopPrefab;

    [Tooltip("Descarga elétrica na ignição do raio e impactos (ex.: CFXR3 Hit Electric C Air).")]
    public GameObject electricBurstPrefab;

    [Tooltip("Flash de impacto ao disparar ou atingir o jogador (ex.: CFXR Impact Glowing HDR Blue).")]
    public GameObject impactFlashPrefab;

    [Header("Cores")]
    public Color normalCoreColor = new Color(0.74f, 0.34f, 1.0f, 0.90f);
    public Color normalGlowColor = new Color(0.52f, 0.16f, 0.88f, 0.70f);
    public Color buffedCoreColor = new Color(0.28f, 0.92f, 1.0f, 0.92f);
    public Color buffedGlowColor = new Color(0.12f, 0.70f, 1.0f, 0.72f);

    // Sistemas de partículas procedurais leves de suporte
    private ParticleSystem chargeParticles;
    private ParticleSystem ambientParticles;
    private ParticleSystem laserParticles;
    private ParticleSystem impactParticles;

    // Instâncias vivas de VFX URP e luzes dinâmicas
    private GameObject activeCoreGlowObj;
    private Light coreLight;
    private Light impactLight;

    private Renderer modelRenderer;
    private Transform coreAnchor;
    private bool isBuffed = false;
    private float nextImpactBurstTime = 0f;
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private static readonly Dictionary<(Material, Color32), Material> TintedMatCache = new Dictionary<(Material, Color32), Material>();
    private static Texture2D _softTex;

    void Start()
    {
        modelRenderer = GetComponentInChildren<Renderer>();
        if (coreAnchor == null)
            coreAnchor = transform.Find("CrystalWatcher_Model/CoreCrystal");

        Vector3 center = GetCenter();
        CreateAmbientParticles(center);
        CreateChargeParticles(center);
        CreateLaserParticles(center);
        CreateImpactParticles(center);
        CreateDynamicLights(center);
        CreateCoreGlowLoop(center);
    }

    void LateUpdate()
    {
        Vector3 center = GetCenter();
        if (ambientParticles != null)
            ambientParticles.transform.position = center;
        if (chargeParticles != null && chargeParticles.isPlaying)
            chargeParticles.transform.position = center;
        if (coreLight != null)
            coreLight.transform.position = center;
        if (activeCoreGlowObj != null && activeCoreGlowObj.activeSelf)
            activeCoreGlowObj.transform.position = center;
    }

    void OnDestroy()
    {
        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
                Destroy(runtimeMaterials[i]);
        }
        runtimeMaterials.Clear();
    }

    public void SetCoreAnchor(Transform anchor)
    {
        coreAnchor = anchor;
    }

    public Vector3 GetCenter()
    {
        if (coreAnchor != null)
            return coreAnchor.position;
        if (modelRenderer != null)
            return modelRenderer.bounds.center;
        return transform.position + Vector3.up * 1.5f;
    }

    private Color ActiveCoreColor => isBuffed ? buffedCoreColor : normalCoreColor;
    private Color ActiveGlowColor => isBuffed ? buffedGlowColor : normalGlowColor;

    // ── Setup de luzes e núcleo CFXR ────────────────────────────────
    private void CreateDynamicLights(Vector3 center)
    {
        GameObject cObj = new GameObject("WatcherCoreLight");
        cObj.transform.SetParent(transform, false);
        cObj.transform.position = center;
        coreLight = cObj.AddComponent<Light>();
        coreLight.type = LightType.Point;
        coreLight.color = ActiveCoreColor;
        coreLight.range = 6.5f;
        coreLight.intensity = 0.6f;

        GameObject iObj = new GameObject("WatcherImpactLight");
        iObj.transform.SetParent(transform, false);
        iObj.transform.position = center;
        impactLight = iObj.AddComponent<Light>();
        impactLight.type = LightType.Point;
        impactLight.color = ActiveCoreColor;
        impactLight.range = 5.5f;
        impactLight.intensity = 0f;
        impactLight.enabled = false;
    }

    private void CreateCoreGlowLoop(Vector3 center)
    {
        if (coreGlowLoopPrefab == null) return;
        activeCoreGlowObj = Instantiate(coreGlowLoopPrefab, center, Quaternion.identity, transform);
        activeCoreGlowObj.name = "WatcherCoreGlowVFX";
        DisableCfxrCameraShake(activeCoreGlowObj);
        ApplyTintToPrefab(activeCoreGlowObj, ActiveCoreColor);
        activeCoreGlowObj.transform.localScale = Vector3.one * 0.25f;
        activeCoreGlowObj.SetActive(false);
    }

    // ── Partículas de suporte ───────────────────────────────────────
    private void CreateAmbientParticles(Vector3 center)
    {
        GameObject obj = new GameObject("AmbientVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        ambientParticles = obj.AddComponent<ParticleSystem>();
        var main = ambientParticles.main;
        main.startLifetime = 1.8f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.68f, 0.28f, 1f, 0.55f),
            new Color(0.92f, 0.72f, 1f, 0.75f)
        );
        main.maxParticles = 28;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ambientParticles.emission;
        emission.rateOverTime = 7f;

        var shape = ambientParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.75f;

        var sizeOverLifetime = ambientParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        SetupParticleMaterial(ambientParticles);
    }

    private void CreateChargeParticles(Vector3 center)
    {
        GameObject obj = new GameObject("ChargeVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        chargeParticles = obj.AddComponent<ParticleSystem>();
        var main = chargeParticles.main;
        main.startLifetime = 0.45f;
        main.startSpeed = -3.4f; // Convergência rápida pro centro do cristal
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.75f, 0.35f, 1f, 0.85f),
            new Color(1f, 0.85f, 1f, 0.95f)
        );
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = chargeParticles.emission;
        emission.rateOverTime = 50f;

        var shape = chargeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.9f;

        var sizeOverLifetime = chargeParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

        SetupParticleMaterial(chargeParticles);
        chargeParticles.Stop();
    }

    private void CreateLaserParticles(Vector3 center)
    {
        GameObject obj = new GameObject("LaserTrailVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        laserParticles = obj.AddComponent<ParticleSystem>();
        var main = laserParticles.main;
        main.startLifetime = 0.32f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.55f, 1f, 0.8f),
            new Color(1f, 0.95f, 1f, 0.95f)
        );
        main.maxParticles = 75;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = laserParticles.emission;
        emission.rateOverTime = 45f;

        var shape = laserParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 3.5f;
        shape.radius = 0.12f;

        var sizeOverLifetime = laserParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        SetupParticleMaterial(laserParticles);
        laserParticles.Stop();
    }

    private void CreateImpactParticles(Vector3 center)
    {
        GameObject obj = new GameObject("LaserImpactVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        impactParticles = obj.AddComponent<ParticleSystem>();
        var main = impactParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.40f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.88f, 0.52f, 1f, 0.95f),
            new Color(1f, 0.88f, 1f, 0.95f)
        );
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = impactParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 48f;

        var shape = impactParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 42f;
        shape.radius = 0.08f;

        var sizeOverLifetime = impactParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        SetupParticleMaterial(impactParticles);
        impactParticles.Stop();
    }

    private void SetupParticleMaterial(ParticleSystem ps)
    {
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        Shader addShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");

        Material mat = new Material(addShader);
        if (_softTex == null)
        {
            const int s = 32;
            _softTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            _softTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[s * s];
            float h = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Clamp01(Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h)) / h);
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2.2f));
                }
            _softTex.SetPixels(px);
            _softTex.Apply(false, true);
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _softTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _softTex);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        runtimeMaterials.Add(mat);
        psr.sharedMaterial = mat;
    }

    // ── API chamada pelo CrystalWatcher_AI ──────────────────────────
    public void StartChargeEffect()
    {
        Vector3 center = GetCenter();
        if (chargeParticles != null)
        {
            chargeParticles.transform.position = center;
            chargeParticles.Play();
        }
        if (activeCoreGlowObj != null)
        {
            activeCoreGlowObj.transform.position = center;
            activeCoreGlowObj.transform.localScale = Vector3.one * 0.20f;
            activeCoreGlowObj.SetActive(true);
            foreach (ParticleSystem ps in activeCoreGlowObj.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play(true);
        }
    }

    public void UpdateChargeProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        if (activeCoreGlowObj != null && activeCoreGlowObj.activeSelf)
        {
            float scale = Mathf.Lerp(0.20f, 0.85f, p * p);
            activeCoreGlowObj.transform.localScale = Vector3.one * scale;
        }
        if (coreLight != null)
        {
            coreLight.color = ActiveCoreColor;
            coreLight.intensity = Mathf.Lerp(0.8f, 4.5f, p);
            coreLight.range = Mathf.Lerp(6f, 9f, p);
        }
    }

    public void StopChargeEffect()
    {
        if (chargeParticles != null)
            chargeParticles.Stop();
    }

    public void StartLaserEffect(Vector3 direction)
    {
        Vector3 center = GetCenter();

        // Flash elétrico de ignição no cristal central
        SpawnOneShotBurst(electricBurstPrefab, center, 0.70f, 0.9f);
        SpawnOneShotBurst(impactFlashPrefab, center, 0.55f, 0.8f);

        if (activeCoreGlowObj != null)
        {
            activeCoreGlowObj.transform.position = center;
            activeCoreGlowObj.transform.localScale = Vector3.one * 0.90f;
            activeCoreGlowObj.SetActive(true);
        }
        if (coreLight != null)
        {
            coreLight.color = ActiveCoreColor;
            coreLight.intensity = 5.2f;
            coreLight.range = 10f;
        }
        if (impactLight != null)
        {
            impactLight.color = ActiveCoreColor;
            impactLight.intensity = 3.8f;
            impactLight.enabled = true;
        }

        if (laserParticles != null)
        {
            laserParticles.transform.position = center;
            UpdateLaserDirection(direction);
            laserParticles.Play();
        }
        if (impactParticles != null)
            impactParticles.Play();

        nextImpactBurstTime = Time.time + 0.12f;
    }

    public void UpdateLaserDirection(Vector3 direction)
    {
        if (laserParticles != null && direction.sqrMagnitude > 0.001f)
        {
            laserParticles.transform.position = GetCenter();
            laserParticles.transform.rotation = Quaternion.LookRotation(direction);
        }
        if (coreLight != null)
        {
            coreLight.intensity = 4.8f + Mathf.Sin(Time.time * 18f) * 0.6f;
        }
    }

    public void UpdateLaserImpact(Vector3 impactPoint, Vector3 normal)
    {
        if (impactParticles != null)
        {
            impactParticles.transform.position = impactPoint;
            if (normal.sqrMagnitude > 0.001f)
                impactParticles.transform.rotation = Quaternion.LookRotation(normal);
        }
        if (impactLight != null)
        {
            impactLight.transform.position = impactPoint + (normal.sqrMagnitude > 0.001f ? normal.normalized * 0.25f : Vector3.zero);
            impactLight.intensity = 3.4f + Mathf.Sin(Time.time * 24f) * 0.7f;
        }

        // Pequenas descargas elétricas periódicas no ponto de impacto do laser
        if (electricBurstPrefab != null && Time.time >= nextImpactBurstTime)
        {
            nextImpactBurstTime = Time.time + 0.22f;
            SpawnOneShotBurst(electricBurstPrefab, impactPoint, 0.50f, 0.65f);
        }
    }

    public void SpawnPlayerHitBurst(Vector3 hitPos)
    {
        SpawnOneShotBurst(impactFlashPrefab, hitPos, 0.55f, 0.7f);
    }

    public void StopLaserEffect()
    {
        if (laserParticles != null)
            laserParticles.Stop();
        if (impactParticles != null)
            impactParticles.Stop();
        if (activeCoreGlowObj != null)
            activeCoreGlowObj.SetActive(false);
        if (coreLight != null)
        {
            coreLight.intensity = 0.6f;
            coreLight.range = 6.5f;
        }
        if (impactLight != null)
        {
            impactLight.intensity = 0f;
            impactLight.enabled = false;
        }
    }

    public void SetAmbientIntensity(float multiplier)
    {
        if (ambientParticles != null)
        {
            var emission = ambientParticles.emission;
            emission.rateOverTime = 7f * multiplier;
        }
    }

    public void SetBuffTint(bool buffed)
    {
        isBuffed = buffed;
        Color a = buffed ? new Color(0.20f, 0.88f, 1.0f, 0.85f) : new Color(0.75f, 0.35f, 1.0f, 0.8f);
        Color b = buffed ? new Color(0.75f, 0.98f, 1.0f, 1.0f) : new Color(0.95f, 0.72f, 1.0f, 1.0f);

        ApplyStartColor(ambientParticles, a, b);
        ApplyStartColor(chargeParticles, a, b);
        ApplyStartColor(laserParticles, a, b);
        ApplyStartColor(impactParticles, a, b);

        if (coreLight != null) coreLight.color = ActiveCoreColor;
        if (impactLight != null) impactLight.color = ActiveCoreColor;
        if (activeCoreGlowObj != null) ApplyTintToPrefab(activeCoreGlowObj, ActiveCoreColor);
    }

    public void StopAllEffects()
    {
        StopChargeEffect();
        StopLaserEffect();
        if (ambientParticles != null)
            ambientParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (coreLight != null)
            coreLight.enabled = false;
    }

    private void SpawnOneShotBurst(GameObject prefab, Vector3 pos, float scale, float lifetime)
    {
        if (prefab == null) return;
        GameObject inst = Instantiate(prefab, pos, Quaternion.identity);
        DisableCfxrCameraShake(inst);
        inst.transform.localScale = Vector3.one * scale;
        ApplyTintToPrefab(inst, ActiveCoreColor);
        Destroy(inst, lifetime);
    }

    private static void DisableCfxrCameraShake(GameObject root)
    {
        if (root == null) return;
        foreach (CartoonFX.CFXR_Effect fx in root.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true))
        {
            if (fx.cameraShake != null)
            {
                fx.cameraShake.enabled = false;
                fx.cameraShake.StopShake();
            }
        }
    }

    private static void ApplyStartColor(ParticleSystem ps, Color c1, Color c2)
    {
        if (ps == null) return;
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
    }

    private static void ApplyTintToPrefab(GameObject root, Color tint)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = tint;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null)
            {
                psr.sharedMaterial = GetOrCreateTintedMaterial(psr.sharedMaterial, tint);
            }
        }
    }

    private static Material GetOrCreateTintedMaterial(Material template, Color tint)
    {
        if (template == null) return null;
        var key = (template, (Color32)tint);
        if (TintedMatCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material mat = new Material(template);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
        TintedMatCache[key] = mat;
        return mat;
    }
}
