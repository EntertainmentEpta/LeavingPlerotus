using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// VFX limpo e cinematográfico em 3 fases para o Stun do Golem (sem runas medievais, sem poluição visual):
/// 1) Antecipação limpa no chão (círculo minimalista anti-clipping + brilho CFXR central + fio guia vindo do céu)
/// 2) Feixe de luz alto (38m) descendo rasgando do céu até o ponto marcado
/// 3) Impacto real usando prefabs de partículas feitos à mão (CFXR2 Ground Hit + CFXR Impact Glowing HDR + CFXR3 Hit Electric + Golem_StunGroundImpact com cratera/pedras/fumaça)
/// </summary>
public class GolemSkyStunVFX : MonoBehaviour
{
    private const int CylinderSegments = 32;

    private float radius = 4f;
    private float telegraphDuration = 1.05f;
    private float beamHeight = 38f;
    private Color primaryColor = new Color(0.62f, 0.30f, 0.95f, 1f);
    private Color accentColor = new Color(0.44f, 0.18f, 0.85f, 1f);
    private Color flashColor = new Color(0.82f, 0.62f, 1.0f, 1f);

    // Prefabs reais de VFX feitos à mão
    private GameObject anticipationGlowPrefab;
    private GameObject groundImpactPrefab;
    private GameObject groundHitRingPrefab;
    private GameObject impactBurstPrefab;
    private GameObject electricStunPrefab;

    private enum Phase { Anticipation, Impact, Cancelled }
    private Phase phase = Phase.Anticipation;
    private float phaseElapsed;
    private float cancelElapsed;
    private bool isLockedOn;
    private float lockFlashTimer;
    private const float ImpactTotalDuration = 2.0f;
    private const float CancelFadeDuration = 0.20f;

    private Transform telegraphTransform;
    private Transform guideBeamTransform;
    private Transform skyBeamCoreTransform;
    private Transform skyBeamOuterTransform;
    private GameObject activeAnticipationGlow;

    private Material telegraphMat;
    private Material guideBeamMat;
    private Material skyBeamCoreMat;
    private Material skyBeamOuterMat;

    private Light centerLight;

    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();

    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
    private static readonly int PropOuterColor = Shader.PropertyToID("_OuterColor");
    private static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
    private static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
    private static readonly int PropFillProgress = Shader.PropertyToID("_FillProgress");
    private static readonly int PropRevealBottom = Shader.PropertyToID("_RevealBottom");
    private static readonly int PropRevealTop = Shader.PropertyToID("_RevealTop");

    public static GolemSkyStunVFX SpawnAnticipation(
        Vector3 groundPos,
        float stunRadius,
        float duration,
        float skyHeight,
        Color mainColor,
        Color secondaryColor,
        Color impactFlashColor,
        Material coreBeamTemplate,
        Material outerBeamTemplate,
        Material cleanTelegraphTemplate,
        GameObject anticipationGlow,
        GameObject groundImpact,
        GameObject groundHitRing,
        GameObject impactBurst,
        GameObject electricStun)
    {
        GameObject go = new GameObject("Golem_SkyStunVFX");
        go.transform.position = groundPos;
        GolemSkyStunVFX vfx = go.AddComponent<GolemSkyStunVFX>();
        vfx.Initialize(
            stunRadius, duration, skyHeight,
            mainColor, secondaryColor, impactFlashColor,
            coreBeamTemplate, outerBeamTemplate, cleanTelegraphTemplate,
            anticipationGlow, groundImpact, groundHitRing, impactBurst, electricStun
        );
        return vfx;
    }

    private void Initialize(
        float stunRadius,
        float duration,
        float skyHeight,
        Color mainColor,
        Color secondaryColor,
        Color impactFlashColor,
        Material coreBeamTemplate,
        Material outerBeamTemplate,
        Material cleanTelegraphTemplate,
        GameObject anticipationGlow,
        GameObject groundImpact,
        GameObject groundHitRing,
        GameObject impactBurst,
        GameObject electricStun)
    {
        radius = Mathf.Max(0.8f, stunRadius);
        telegraphDuration = Mathf.Max(0.25f, duration);
        beamHeight = Mathf.Max(12f, skyHeight);
        primaryColor = mainColor;
        accentColor = secondaryColor;
        flashColor = impactFlashColor;

        anticipationGlowPrefab = anticipationGlow;
        groundImpactPrefab = groundImpact;
        groundHitRingPrefab = groundHitRing;
        impactBurstPrefab = impactBurst;
        electricStunPrefab = electricStun;

        Mesh quadMesh = BuildGroundQuadMesh();
        Mesh cylinderMesh = BuildOpenCylinderMesh(CylinderSegments);
        ownedMeshes.Add(quadMesh);
        ownedMeshes.Add(cylinderMesh);

        // 1. Notificação limpa e minimalista no chão (ZERO runas, anti-clipping, arroxeado translúcido)
        telegraphMat = CloneOrCreateTelegraphMat(cleanTelegraphTemplate, primaryColor, flashColor);
        telegraphTransform = CreateMeshChild("CleanGroundTelegraph", quadMesh, telegraphMat, new Vector3(0f, 0.08f, 0f), new Vector3(radius * 2f, 1f, radius * 2f));

        // 2. Brilho central sutil feito à mão (CFXR3 LightGlow A)
        if (anticipationGlowPrefab != null)
        {
            activeAnticipationGlow = Instantiate(anticipationGlowPrefab, transform);
            activeAnticipationGlow.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            activeAnticipationGlow.transform.localScale = Vector3.one * 0.42f;
            TintParticlesSoft(activeAnticipationGlow, primaryColor, 0.65f);
        }

        // 3. Fio de luz sutil vindo lá do céu (arroxeado translúcido)
        guideBeamMat = CloneOrCreateBeamMat(coreBeamTemplate, flashColor * 1.1f, primaryColor * 0.95f, 0.95f);
        guideBeamTransform = CreateMeshChild("SkyGuideThread", cylinderMesh, guideBeamMat, new Vector3(0f, 0.08f, 0f), new Vector3(0.20f, beamHeight, 0.20f));

        // 4. Feixe de Luz (núcleo lavanda + aura roxa translúcida, proporção mais focada)
        skyBeamCoreMat = CloneOrCreateBeamMat(coreBeamTemplate, flashColor * 1.25f, primaryColor * 1.05f, 1.15f);
        skyBeamCoreTransform = CreateMeshChild("SkyBeamCore", cylinderMesh, skyBeamCoreMat, new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, beamHeight, 0.36f));
        SetBeamReveal(skyBeamCoreMat, 1.05f, 1.05f, 0f);

        skyBeamOuterMat = CloneOrCreateBeamMat(outerBeamTemplate, primaryColor * 1.05f, accentColor * 0.95f, 0.95f);
        skyBeamOuterTransform = CreateMeshChild("SkyBeamOuter", cylinderMesh, skyBeamOuterMat, new Vector3(0f, 0.05f, 0f), new Vector3(0.82f, beamHeight, 0.82f));
        SetBeamReveal(skyBeamOuterMat, 1.05f, 1.05f, 0f);

        // 5. Luz dinâmica roxa suave no centro
        GameObject lightObj = new GameObject("StunLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * 1.5f;
        centerLight = lightObj.AddComponent<Light>();
        centerLight.type = LightType.Point;
        centerLight.color = primaryColor;
        centerLight.intensity = 0.5f;
        centerLight.range = radius * 2.2f;
        centerLight.shadows = LightShadows.None;

        UpdateAnticipation(0f);
    }

    public void UpdateTargetPosition(Vector3 groundPos)
    {
        if (phase != Phase.Anticipation) return;
        transform.position = groundPos;
    }

    public void MarkLockedOn()
    {
        if (isLockedOn) return;
        isLockedOn = true;
        lockFlashTimer = 0.16f;
    }

    public void TriggerImpact()
    {
        if (phase != Phase.Anticipation) return;
        phase = Phase.Impact;
        phaseElapsed = 0f;

        if (activeAnticipationGlow != null)
        {
            Destroy(activeAnticipationGlow);
            activeAnticipationGlow = null;
        }
        if (guideBeamTransform != null)
            guideBeamTransform.gameObject.SetActive(false);

        float radiusFactor = radius / 4f;
        Vector3 impactPos = transform.position + Vector3.up * 0.12f;

        // 1. Cratera pintada + pedras voando + fumaça estilizada
        if (groundImpactPrefab != null)
        {
            GameObject groundVfx = Instantiate(groundImpactPrefab, impactPos, Quaternion.identity);
            groundVfx.transform.localScale = Vector3.one * (0.95f * radiusFactor);
            TintParticlesSoft(groundVfx, primaryColor, 0.78f, tintStrength: 0.45f);
            Destroy(groundVfx, 2.4f);
        }

        // 2. Onda de impacto estilizada no chão (CFXR2 Ground Hit)
        if (groundHitRingPrefab != null)
        {
            GameObject ringVfx = Instantiate(groundHitRingPrefab, impactPos + Vector3.up * 0.04f, Quaternion.identity);
            ringVfx.transform.localScale = Vector3.one * (2.6f * radiusFactor);
            TintParticlesSoft(ringVfx, primaryColor, 0.72f, tintStrength: 0.85f);
            Destroy(ringVfx, 1.6f);
        }

        // 3. Clarão / Flare arroxeado translúcido (CFXR Impact Glowing HDR)
        if (impactBurstPrefab != null)
        {
            GameObject burstVfx = Instantiate(impactBurstPrefab, impactPos + Vector3.up * 0.45f, Quaternion.identity);
            burstVfx.transform.localScale = Vector3.one * (0.38f * radiusFactor);
            TintParticlesSoft(burstVfx, primaryColor, 0.62f, tintStrength: 0.90f);
            Destroy(burstVfx, 1.6f);
        }

        // 4. Arcos elétricos roxos/lavanda de STUN (CFXR3 Hit Electric C)
        if (electricStunPrefab != null)
        {
            GameObject elecVfx = Instantiate(electricStunPrefab, impactPos + Vector3.up * 0.85f, Quaternion.identity);
            elecVfx.transform.localScale = Vector3.one * (1.35f * radiusFactor);
            TintParticlesSoft(elecVfx, flashColor, 0.75f, tintStrength: 0.88f);
            Destroy(elecVfx, 1.6f);
        }

        if (centerLight != null)
        {
            centerLight.color = primaryColor;
            centerLight.intensity = 5.2f;
            centerLight.range = radius * 2.5f;
        }
    }

    public void CancelAnticipation()
    {
        if (phase == Phase.Impact) return;
        phase = Phase.Cancelled;
        cancelElapsed = 0f;
        if (activeAnticipationGlow != null)
        {
            Destroy(activeAnticipationGlow);
            activeAnticipationGlow = null;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (phase == Phase.Anticipation)
        {
            phaseElapsed += dt;
            float t = Mathf.Clamp01(phaseElapsed / telegraphDuration);
            UpdateAnticipation(t);
        }
        else if (phase == Phase.Impact)
        {
            phaseElapsed += dt;
            UpdateImpact();
            if (phaseElapsed >= ImpactTotalDuration)
                Destroy(gameObject);
        }
        else if (phase == Phase.Cancelled)
        {
            cancelElapsed += dt;
            float fade = 1f - Mathf.Clamp01(cancelElapsed / CancelFadeDuration);
            SetShaderFloat(telegraphMat, PropAlpha, fade * 0.65f);
            SetBeamReveal(guideBeamMat, 0f, 1f, fade * 0.4f);
            SetBeamReveal(skyBeamCoreMat, 0f, 1f, fade * 0.4f);
            SetBeamReveal(skyBeamOuterMat, 0f, 1f, fade * 0.3f);
            if (centerLight != null) centerLight.intensity *= fade;
            if (cancelElapsed >= CancelFadeDuration)
                Destroy(gameObject);
        }
    }

    private void UpdateAnticipation(float t)
    {
        if (lockFlashTimer > 0f)
            lockFlashTimer = Mathf.Max(0f, lockFlashTimer - Time.deltaTime);

        float lockBoost = lockFlashTimer > 0f ? (lockFlashTimer / 0.16f) * 0.35f : 0f;

        float intro = Mathf.Clamp01(t / 0.14f);
        float introEase = 1f - Mathf.Pow(1f - intro, 3f);

        // 1. Círculo limpo arroxeado e translúcido no chão
        if (telegraphMat != null)
        {
            SetShaderFloat(telegraphMat, PropFillProgress, t);
            SetShaderFloat(telegraphMat, PropAlpha, Mathf.Clamp01(introEase * Mathf.Lerp(0.42f, 0.68f, t) + lockBoost * 0.16f));
            SetShaderFloat(telegraphMat, PropIntensity, Mathf.Lerp(0.80f, 1.20f, t * t) + lockBoost);
        }

        // 2. Brilho central cresce sutilmente conforme carrega
        if (activeAnticipationGlow != null)
        {
            float s = Mathf.Lerp(0.32f, 0.92f, t) * (radius / 4f);
            activeAnticipationGlow.transform.localScale = Vector3.one * s;
        }

        // 3. Fio guia vertical sutil e translúcido descendo do céu
        if (guideBeamTransform != null && guideBeamMat != null)
        {
            float dropT = Mathf.Clamp01(t / 0.22f);
            float revBot = Mathf.Lerp(1f, 0f, 1f - Mathf.Pow(1f - dropT, 2f));
            float guideR = Mathf.Lerp(0.26f, 0.06f, t);
            guideBeamTransform.localScale = new Vector3(guideR, beamHeight, guideR);
            SetBeamReveal(guideBeamMat, revBot, 1.05f, Mathf.Lerp(0.14f, 0.45f, t));
        }

        // 4. Fase 2 (65% -> 100%): Feixe de luz roxo translúcido rasga lá do céu até o chão
        const float beamStartT = 0.65f;
        if (t >= beamStartT)
        {
            float beamT = Mathf.Clamp01((t - beamStartT) / (1f - beamStartT));
            float descentT = Mathf.Clamp01(beamT / 0.58f);
            float revBot = Mathf.Lerp(1.0f, 0.0f, descentT * descentT);

            float surgeT = Mathf.Clamp01((beamT - 0.58f) / 0.42f);
            float coreR = Mathf.Lerp(0.20f, radius * 0.15f, surgeT);
            float outerR = Mathf.Lerp(0.50f, radius * 0.32f, surgeT);

            if (skyBeamCoreTransform != null)
                skyBeamCoreTransform.localScale = new Vector3(coreR, beamHeight, coreR);
            if (skyBeamOuterTransform != null)
                skyBeamOuterTransform.localScale = new Vector3(outerR, beamHeight, outerR);

            SetBeamReveal(skyBeamCoreMat, revBot, 1.05f, Mathf.Lerp(0.45f, 0.68f, beamT));
            SetBeamReveal(skyBeamOuterMat, revBot, 1.05f, Mathf.Lerp(0.28f, 0.50f, beamT));
        }

        if (centerLight != null)
            centerLight.intensity = Mathf.Lerp(0.45f, 2.8f, t * t) + lockBoost;
    }

    private void UpdateImpact()
    {
        // Feixe do céu dá o pico roxo translúcido no impacto e dissipa limpo pra cima
        if (phaseElapsed < 0.65f)
        {
            float expand = Mathf.Clamp01(phaseElapsed / 0.07f);
            float fadeOut = Mathf.Clamp01((phaseElapsed - 0.09f) / 0.56f);
            float easeOut = fadeOut * fadeOut;

            float coreR = Mathf.Lerp(radius * 0.15f, radius * 0.27f, expand) * Mathf.Lerp(1f, 0.04f, easeOut);
            float outerR = Mathf.Lerp(radius * 0.32f, radius * 0.52f, expand) * Mathf.Lerp(1f, 0.10f, easeOut);

            if (skyBeamCoreTransform != null)
                skyBeamCoreTransform.localScale = new Vector3(Mathf.Max(0.02f, coreR), beamHeight, Mathf.Max(0.02f, coreR));
            if (skyBeamOuterTransform != null)
                skyBeamOuterTransform.localScale = new Vector3(Mathf.Max(0.02f, outerR), beamHeight, Mathf.Max(0.02f, outerR));

            float revBot = Mathf.Lerp(0f, 0.55f, easeOut);
            SetBeamReveal(skyBeamCoreMat, revBot, 1.05f, (1f - easeOut) * 0.72f);
            SetBeamReveal(skyBeamOuterMat, revBot * 0.8f, 1.05f, (1f - easeOut) * 0.52f);
            SetShaderFloat(skyBeamCoreMat, PropIntensity, Mathf.Lerp(1.55f, 0.80f, fadeOut));
            SetShaderFloat(skyBeamOuterMat, PropIntensity, Mathf.Lerp(1.15f, 0.55f, fadeOut));
        }
        else
        {
            if (skyBeamCoreTransform != null) skyBeamCoreTransform.gameObject.SetActive(false);
            if (skyBeamOuterTransform != null) skyBeamOuterTransform.gameObject.SetActive(false);
        }

        // Telegraph some suavemente após o impacto
        float telFade = 1f - Mathf.Clamp01(phaseElapsed / 0.24f);
        SetShaderFloat(telegraphMat, PropAlpha, telFade * 0.60f);

        // Luz decai limpo
        if (centerLight != null)
        {
            float lf = 1f - Mathf.Clamp01(phaseElapsed / 0.40f);
            centerLight.intensity = 5.2f * lf * lf;
        }
    }

    private static void TintParticlesSoft(GameObject root, Color tint, float alphaScale = 0.75f, float tintStrength = 0.85f)
    {
        if (root == null) return;
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            var sc = main.startColor;
            switch (sc.mode)
            {
                case ParticleSystemGradientMode.Color:
                    Color c = Color.Lerp(sc.color, tint, tintStrength);
                    c.a = sc.color.a * alphaScale;
                    main.startColor = new ParticleSystem.MinMaxGradient(c);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    Color c1 = Color.Lerp(sc.colorMin, tint, tintStrength);
                    Color c2 = Color.Lerp(sc.colorMax, Color.Lerp(tint, Color.white, 0.25f), tintStrength);
                    c1.a = sc.colorMin.a * alphaScale;
                    c2.a = sc.colorMax.a * alphaScale;
                    main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
                    break;
                default:
                    Color fallback = tint;
                    fallback.a = alphaScale;
                    main.startColor = new ParticleSystem.MinMaxGradient(fallback);
                    break;
            }
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].color = tint;
            lights[i].intensity *= 0.65f;
        }
    }

    private Transform CreateMeshChild(string childName, Mesh mesh, Material mat, Vector3 localPos, Vector3 localScale)
    {
        GameObject obj = new GameObject(childName);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = localScale;

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return obj.transform;
    }

    private Material CloneOrCreateBeamMat(Material template, Color coreCol, Color outerCol, float intensity)
    {
        Material mat;
        if (template != null)
            mat = new Material(template);
        else
        {
            Shader sh = Shader.Find("Custom/GolemSkyBeam_URP")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
        }
        ownedMaterials.Add(mat);

        if (mat.HasProperty(PropCoreColor)) mat.SetColor(PropCoreColor, coreCol);
        if (mat.HasProperty(PropOuterColor)) mat.SetColor(PropOuterColor, outerCol);
        if (mat.HasProperty(PropIntensity)) mat.SetFloat(PropIntensity, intensity);
        if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, outerCol);
        return mat;
    }

    private Material CloneOrCreateTelegraphMat(Material template, Color mainCol, Color hiCol)
    {
        Material mat;
        if (template != null)
            mat = new Material(template);
        else
        {
            Shader sh = Shader.Find("Custom/GolemGroundRune_URP")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
        }
        ownedMaterials.Add(mat);

        if (mat.HasProperty("_UseTexture")) mat.SetFloat("_UseTexture", 0f);
        if (mat.HasProperty("_ShowProceduralRing")) mat.SetFloat("_ShowProceduralRing", 1f);
        if (mat.HasProperty("_OuterRingWidth")) mat.SetFloat("_OuterRingWidth", 0.028f);
        if (mat.HasProperty(PropColor)) mat.SetColor(PropColor, mainCol);
        if (mat.HasProperty(PropCoreColor)) mat.SetColor(PropCoreColor, hiCol * 1.15f);
        return mat;
    }

    private static void SetBeamReveal(Material mat, float bottom, float top, float alpha)
    {
        if (mat == null) return;
        if (mat.HasProperty(PropRevealBottom)) mat.SetFloat(PropRevealBottom, bottom);
        if (mat.HasProperty(PropRevealTop)) mat.SetFloat(PropRevealTop, top);
        if (mat.HasProperty(PropAlpha)) mat.SetFloat(PropAlpha, alpha);
    }

    private static void SetShaderFloat(Material mat, int propId, float val)
    {
        if (mat != null && mat.HasProperty(propId))
            mat.SetFloat(propId, val);
    }

    private static Mesh BuildGroundQuadMesh()
    {
        Mesh mesh = new Mesh { name = "GolemCleanTelegraphQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f)
        };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        return mesh;
    }

    private static Mesh BuildOpenCylinderMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "GolemSkyBeamCylinder" };
        int ringVerts = segments + 1;
        Vector3[] verts = new Vector3[ringVerts * 2];
        Vector3[] normals = new Vector3[ringVerts * 2];
        Vector2[] uvs = new Vector2[ringVerts * 2];
        int[] tris = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            float a = u * Mathf.PI * 2f;
            float cx = Mathf.Cos(a);
            float cz = Mathf.Sin(a);

            verts[i] = new Vector3(cx, 0f, cz);
            normals[i] = new Vector3(cx, 0f, cz);
            uvs[i] = new Vector2(u, 0f);

            verts[ringVerts + i] = new Vector3(cx, 1f, cz);
            normals[ringVerts + i] = new Vector3(cx, 0f, cz);
            uvs[ringVerts + i] = new Vector2(u, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int b0 = i;
            int b1 = i + 1;
            int t0 = ringVerts + i;
            int t1 = ringVerts + i + 1;

            int idx = i * 6;
            tris[idx] = b0;
            tris[idx + 1] = t0;
            tris[idx + 2] = t1;
            tris[idx + 3] = b0;
            tris[idx + 4] = t1;
            tris[idx + 5] = b1;
        }

        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < ownedMaterials.Count; i++)
            if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
        ownedMaterials.Clear();

        for (int i = 0; i < ownedMeshes.Count; i++)
            if (ownedMeshes[i] != null) Destroy(ownedMeshes[i]);
        ownedMeshes.Clear();
    }
}

/// <summary>
/// Compatibilidade para scripts que adicionem GolemStunVFX diretamente.
/// </summary>
public class GolemStunVFX : MonoBehaviour
{
    public Color stunColor = new Color(0.52f, 0.75f, 1.0f, 1f);
    public Color flashColor = new Color(0.95f, 0.97f, 1.0f, 1f);
    public float maxRadius = 4f;
    public float pillarHeight = 38f;

    public void SetRadius(float radius) => maxRadius = radius;

    void Start()
    {
        GolemSkyStunVFX sky = GolemSkyStunVFX.SpawnAnticipation(
            transform.position, maxRadius, 0.25f, Mathf.Max(20f, pillarHeight),
            stunColor, new Color(0.70f, 0.56f, 1.0f, 1f), flashColor,
            null, null, null, null, null, null, null, null
        );
        sky.TriggerImpact();
        Destroy(gameObject);
    }
}

/// <summary>
/// Indicador limpo e minimalista no chão (sem runas, anti-clipping) para o golpe Melee do Golem.
/// </summary>
public class GolemGroundTelegraph : MonoBehaviour
{
    private float radius = 2.4f;
    private float duration = 0.35f;
    private Color baseColor = new Color(0.95f, 0.72f, 0.35f, 1f);
    private float elapsed;

    private Material mat;
    private Mesh quadMesh;

    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
    private static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
    private static readonly int PropFillProgress = Shader.PropertyToID("_FillProgress");
    private static readonly int PropIntensity = Shader.PropertyToID("_Intensity");

    public static GolemGroundTelegraph Spawn(Vector3 position, float radius, float duration, Color color, Material template = null)
    {
        GameObject go = new GameObject("Golem_GroundTelegraph");
        go.transform.position = new Vector3(position.x, position.y + 0.08f, position.z);
        GolemGroundTelegraph telegraph = go.AddComponent<GolemGroundTelegraph>();
        telegraph.Initialize(radius, duration, color, template);
        return telegraph;
    }

    public void Initialize(float targetRadius, float telegraphDuration, Color color, Material template)
    {
        radius = Mathf.Max(0.35f, targetRadius);
        duration = Mathf.Max(0.06f, telegraphDuration);
        baseColor = color;
        elapsed = 0f;

        quadMesh = new Mesh { name = "GolemMeleeTelegraphQuad" };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f)
        };
        quadMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };

        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = quadMesh;

        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (template != null)
            mat = new Material(template);
        else
        {
            Shader sh = Shader.Find("Custom/GolemGroundRune_URP")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
        }

        if (mat.HasProperty("_UseTexture")) mat.SetFloat("_UseTexture", 0f);
        if (mat.HasProperty("_ShowProceduralRing")) mat.SetFloat("_ShowProceduralRing", 1f);
        if (mat.HasProperty("_OuterRingWidth")) mat.SetFloat("_OuterRingWidth", 0.036f);
        if (mat.HasProperty(PropColor)) mat.SetColor(PropColor, baseColor);
        if (mat.HasProperty(PropCoreColor)) mat.SetColor(PropCoreColor, Color.Lerp(baseColor, Color.white, 0.65f) * 1.6f);
        mr.sharedMaterial = mat;

        transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
        UpdateVisuals(0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        UpdateVisuals(t);

        if (elapsed >= duration + 0.04f)
            Destroy(gameObject);
    }

    private void UpdateVisuals(float t)
    {
        if (mat == null) return;
        if (mat.HasProperty(PropFillProgress)) mat.SetFloat(PropFillProgress, t);
        if (mat.HasProperty(PropAlpha)) mat.SetFloat(PropAlpha, Mathf.Lerp(0.45f, 0.85f, t));
        if (mat.HasProperty(PropIntensity)) mat.SetFloat(PropIntensity, Mathf.Lerp(0.95f, 1.55f, t));
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (quadMesh != null) Destroy(quadMesh);
    }
}

/// <summary>
/// Helper de impacto para o Melee pesado do Golem usando os prefabs reais de impacto de chão (CFXR2 Ground Hit + Golem_StunGroundImpact).
/// </summary>
public static class GolemMeleeSlamVFX
{
    public static void Spawn(Vector3 position, float radius, GameObject groundHitRingPrefab, GameObject groundImpactPrefab)
    {
        Vector3 pos = new Vector3(position.x, position.y + 0.12f, position.z);
        float scaleFactor = Mathf.Max(0.4f, radius / 2.4f);

        if (groundHitRingPrefab != null)
        {
            GameObject ring = Object.Instantiate(groundHitRingPrefab, pos, Quaternion.identity);
            ring.transform.localScale = Vector3.one * (1.55f * scaleFactor);
            Object.Destroy(ring, 1.5f);
        }

        if (groundImpactPrefab != null)
        {
            GameObject impact = Object.Instantiate(groundImpactPrefab, pos, Quaternion.identity);
            impact.transform.localScale = Vector3.one * (0.52f * scaleFactor);
            Object.Destroy(impact, 2.0f);
        }
    }
}
