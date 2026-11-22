using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Componente universal de visual Elite (usado em qualquer inimigo Elite sem alterar a cor/modelo 3D).
/// Gera um anel luminoso anti-clipping no chão + partículas sutis subindo em volta do inimigo + luz ambiente discreta.
/// </summary>
public class EliteVariantConfig : MonoBehaviour
{
    [Header("Identificacao")]
    [Tooltip("Nome de exibicao desta variante (usado em logs).")]
    public string variantName = "Variante Elite";

    [Header("Aura VFX (opcional)")]
    [Tooltip("Se preenchido, instancia este prefab como filho. Deixe vazio para usar a aura procedural.")]
    public GameObject auraPrefab;

    [Header("Aura Procedural")]
    [Tooltip("Se auraPrefab for null, gera a aura luminosa em volta do inimigo.")]
    public bool useProceduralAura = true;
    public Color auraColor = new Color(1f, 0.52f, 0.12f, 1f);
    [Range(0.3f, 4f)]
    public float auraRadius = 0.85f;
    [Range(0.05f, 0.5f)]
    public float auraWidth = 0.09f;
    [Tooltip("Se verdadeiro, ajusta o raio automaticamente ao tamanho físico do Collider do inimigo.")]
    public bool autoFitToBounds = true;

    private GameObject auraInstance;
    private Material ringMat;
    private Material particleMat;
    private Mesh ringQuadMesh;
    private Light auraLight;

    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
    private static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
    private static readonly int PropIntensity = Shader.PropertyToID("_Intensity");

    void Start()
    {
        SpawnAura();
    }

    void Update()
    {
        if (auraInstance == null) return;

        float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 2.6f);

        if (ringMat != null)
        {
            if (ringMat.HasProperty(PropAlpha)) ringMat.SetFloat(PropAlpha, 0.52f * pulse);
            if (ringMat.HasProperty(PropIntensity)) ringMat.SetFloat(PropIntensity, 0.95f + 0.35f * pulse);
        }

        if (auraLight != null)
        {
            auraLight.intensity = 1.45f * pulse;
        }
    }

    void OnDestroy()
    {
        if (auraInstance != null) Destroy(auraInstance);
        if (ringMat != null) Destroy(ringMat);
        if (particleMat != null) Destroy(particleMat);
        if (ringQuadMesh != null) Destroy(ringQuadMesh);
    }

    public void SetAuraVisible(bool visible)
    {
        if (auraInstance != null)
            auraInstance.SetActive(visible);
    }

    void SpawnAura()
    {
        if (auraInstance != null) return;

        if (auraPrefab != null)
        {
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
            return;
        }

        if (!useProceduralAura) return;

        // Calcula o raio e altura ideais em espaço local para ficar proporcional em Goblins, Spiders e Golens
        float worldRadius = auraRadius;
        float worldHeight = 1.6f;
        if (autoFitToBounds)
        {
            Collider col = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds;
                float horizExtent = Mathf.Max(b.extents.x, b.extents.z);
                worldRadius = Mathf.Clamp(horizExtent * 1.45f, 0.85f, 2.8f);
                worldHeight = Mathf.Clamp(b.size.y, 1.0f, 4.5f);
            }
        }

        // Compensa a escala do pai para que o raio em mundo fique exato
        float invScale = 1f / Mathf.Max(0.1f, Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        float localRadius = worldRadius * invScale;
        float localHeight = worldHeight * invScale;

        auraInstance = new GameObject("EliteAura");
        auraInstance.transform.SetParent(transform, false);
        auraInstance.transform.localPosition = Vector3.zero;

        // 1. Anel luminoso limpo no chão (anti-clipping com depth bias)
        GameObject ringObj = new GameObject("EliteGroundRing");
        ringObj.transform.SetParent(auraInstance.transform, false);
        ringObj.transform.localPosition = new Vector3(0f, 0.06f * invScale, 0f);
        ringObj.transform.localScale = new Vector3(localRadius * 2f, 1f, localRadius * 2f);

        ringQuadMesh = BuildUnitQuadMesh();
        MeshFilter mf = ringObj.AddComponent<MeshFilter>();
        mf.sharedMesh = ringQuadMesh;

        MeshRenderer mr = ringObj.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Shader ringShader = Shader.Find("Custom/GolemGroundRune_URP")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Sprites/Default");
        ringMat = new Material(ringShader);
        if (ringMat.HasProperty("_UseTexture")) ringMat.SetFloat("_UseTexture", 0f);
        if (ringMat.HasProperty("_ShowProceduralRing")) ringMat.SetFloat("_ShowProceduralRing", 1f);
        if (ringMat.HasProperty("_FillProgress")) ringMat.SetFloat("_FillProgress", 0f);
        if (ringMat.HasProperty("_OuterRingWidth")) ringMat.SetFloat("_OuterRingWidth", 0.085f);
        if (ringMat.HasProperty(PropColor)) ringMat.SetColor(PropColor, auraColor);
        if (ringMat.HasProperty(PropCoreColor)) ringMat.SetColor(PropCoreColor, Color.Lerp(auraColor, Color.white, 0.45f) * 1.25f);
        if (ringMat.HasProperty(PropAlpha)) ringMat.SetFloat(PropAlpha, 0.55f);
        if (ringMat.HasProperty(PropIntensity)) ringMat.SetFloat(PropIntensity, 1.1f);
        mr.sharedMaterial = ringMat;

        // 2. Partículas sutis subindo em volta do inimigo (sem mexer na cor do modelo 3D)
        GameObject embersObj = new GameObject("EliteAuraMotes");
        embersObj.transform.SetParent(auraInstance.transform, false);
        embersObj.transform.localPosition = new Vector3(0f, 0.15f * invScale, 0f);
        embersObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = embersObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        Color c1 = new Color(auraColor.r, auraColor.g, auraColor.b, 0.55f);
        Color c2 = Color.Lerp(c1, new Color(1f, 0.92f, 0.72f, 0.40f), 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 28;

        var em = ps.emission;
        em.rateOverTime = 11f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = worldRadius * 0.85f;
        shape.radiusThickness = 0.28f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0.9f);

        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 0f));

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        particleMat = new Material(pShader);
        if (particleMat.HasProperty("_Surface")) particleMat.SetFloat("_Surface", 1f);
        if (particleMat.HasProperty("_ZWrite")) particleMat.SetFloat("_ZWrite", 0f);
        if (particleMat.HasProperty("_Blend")) particleMat.SetFloat("_Blend", 2f);
        particleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMat.renderQueue = 3010;
        if (particleMat.HasProperty("_BaseColor")) particleMat.SetColor("_BaseColor", c1);
        if (particleMat.HasProperty("_Color")) particleMat.SetColor("_Color", c1);
        if (particleMat.HasProperty("_SrcBlend") && particleMat.HasProperty("_DstBlend"))
        {
            particleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            particleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        rend.material = particleMat;
        ps.Play();

        // 3. Luz suave acompanhando o centro do inimigo
        GameObject lightObj = new GameObject("EliteAuraLight");
        lightObj.transform.SetParent(auraInstance.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, localHeight * 0.45f, 0f);
        auraLight = lightObj.AddComponent<Light>();
        auraLight.type = LightType.Point;
        auraLight.color = auraColor;
        auraLight.intensity = 1.45f;
        auraLight.range = Mathf.Clamp(worldRadius * 2.6f, 3.2f, 6.5f);
        auraLight.shadows = LightShadows.None;
    }

    private static Mesh BuildUnitQuadMesh()
    {
        Mesh mesh = new Mesh { name = "EliteAuraRingQuad" };
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
}
