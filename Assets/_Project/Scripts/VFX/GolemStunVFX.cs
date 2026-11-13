using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// VFX para o Stun do Golem.
/// Cria um efeito visual de onda de choque com partículas, luz, pilar e fissuras no solo.
/// </summary>
public class GolemStunVFX : MonoBehaviour
{
    [Header("Cores")]
    public Color stunColor = new Color(0.2f, 0.8f, 1f, 1f); // Cyan
    public Color flashColor = new Color(1f, 1f, 1f, 1f); // Branco

    [Header("Ring Shockwave")]
    public float expandSpeed = 15f;
    public float maxRadius = 5f;
    public float ringWidth = 0.5f;

    [Header("Pillar Beam")]
    public float pillarHeight = 3.5f;
    public float pillarDuration = 0.18f;

    [Header("Partículas")]
    public int particleCount = 30;

    // --- Internos para LineRenderers ---
    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private LineRenderer pillarBeam;
    private List<LineRenderer> crackLines = new List<LineRenderer>();
    private Vector3[][] crackPaths;

    // --- Materiais ---
    private Material outerRingMat;
    private Material innerRingMat;
    private Material pillarMat;
    private Material crackMat;
    private List<Material> materialsToDestroy = new List<Material>();

    // --- Partículas ---
    private ParticleSystem sparksSystem;
    private ParticleSystem debrisSystem;
    private ParticleSystem dustSystem;

    private Light flashLight;
    private float lifetime = 0f;
    private float totalDuration = 0.8f;

    private const int circleSegments = 64;

    void Start()
    {
        CreateFlashLight();
        CreateRings();
        CreatePillar();
        CreateCracks();
        CreateParticles();
    }

    void Update()
    {
        lifetime += Time.deltaTime;

        // Animação do anel externo (fase rápida inicial)
        float outerT = Mathf.Clamp01(lifetime / 0.5f);
        float outerRadius = maxRadius * (1f - Mathf.Pow(1f - outerT, 3f)); // easeOutCubic
        DrawCircle(outerRing, outerRadius, outerRingMat, outerT);

        // Animação do anel interno (levemente atrasado e menor)
        float innerT = Mathf.Clamp01((lifetime - 0.05f) / 0.45f);
        if (innerT > 0f)
        {
            float innerRadius = (maxRadius * 0.7f) * (1f - Mathf.Pow(1f - innerT, 3f));
            DrawCircle(innerRing, innerRadius, innerRingMat, innerT);
        }
        else if (innerRing != null)
        {
            innerRing.startWidth = 0f;
            innerRing.endWidth = 0f;
        }

        // Animação das rachaduras no solo
        float crackDuration = 0.25f;
        float crackProgress = Mathf.Clamp01(lifetime / crackDuration);
        float crackFadeT = Mathf.Clamp01((lifetime - crackDuration) / (totalDuration - crackDuration));
        
        if (crackMat != null)
        {
            Color crackCol = stunColor;
            crackCol.a = 1f - crackFadeT;
            crackMat.SetColor("_BaseColor", crackCol);
            if (crackMat.HasProperty("_EmissionColor"))
            {
                crackMat.SetColor("_EmissionColor", crackCol * (3f * (1f - crackFadeT)));
            }
        }

        for (int c = 0; c < crackLines.Count; c++)
        {
            LineRenderer lr = crackLines[c];
            if (lr == null) continue;

            float currentWidth = Mathf.Lerp(0.08f, 0f, crackFadeT);
            lr.startWidth = currentWidth;
            lr.endWidth = currentWidth * 0.5f;

            int segments = crackPaths[c].Length;
            for (int i = 0; i < segments; i++)
            {
                float segT = (float)i / (segments - 1);
                float pointT = Mathf.Min(segT, crackProgress);
                Vector3 pos = GetPositionOnPath(crackPaths[c], pointT);
                lr.SetPosition(i, pos + Vector3.up * 0.03f); // 3cm acima do chão
            }
        }

        // Animação do pilar vertical (feixe fino e rápido)
        if (pillarBeam != null)
        {
            float pillarT = Mathf.Clamp01(lifetime / pillarDuration);
            float currentPillarWidth = Mathf.Lerp(0.35f, 0f, pillarT);
            pillarBeam.startWidth = currentPillarWidth;
            pillarBeam.endWidth = currentPillarWidth * 0.4f;

            if (pillarMat != null)
            {
                Color pCol = stunColor;
                pCol.a = 1f - pillarT;
                pillarMat.SetColor("_BaseColor", pCol);
                if (pillarMat.HasProperty("_EmissionColor"))
                {
                    pillarMat.SetColor("_EmissionColor", pCol * (3f * (1f - pillarT)));
                }
            }
        }

        // Fade da luz
        if (flashLight != null)
        {
            flashLight.intensity = Mathf.Lerp(4f, 0f, lifetime / 0.25f);
        }

        // Autodestruição quando o tempo total passar
        if (lifetime >= totalDuration)
        {
            Destroy(gameObject);
        }
    }

    void CreateFlashLight()
    {
        GameObject lightObj = new GameObject("StunFlash");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.up * 1f;

        flashLight = lightObj.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = stunColor;
        flashLight.intensity = 4f;
        flashLight.range = maxRadius * 1.2f;
        flashLight.shadows = LightShadows.None;
    }

    void CreateRings()
    {
        // Anel Externo
        GameObject outerObj = new GameObject("OuterRing");
        outerObj.transform.SetParent(transform, false);
        outerObj.transform.localPosition = Vector3.zero;
        outerRing = outerObj.AddComponent<LineRenderer>();
        SetupLineRenderer(outerRing, out outerRingMat, stunColor, 3f);

        // Anel Interno
        GameObject innerObj = new GameObject("InnerRing");
        innerObj.transform.SetParent(transform, false);
        innerObj.transform.localPosition = Vector3.zero;
        innerRing = innerObj.AddComponent<LineRenderer>();
        SetupLineRenderer(innerRing, out innerRingMat, Color.Lerp(stunColor, Color.white, 0.4f), 2.5f);
    }

    void SetupLineRenderer(LineRenderer lr, out Material outMat, Color baseColor, float emissionMultiplier)
    {
        lr.loop = true;
        lr.positionCount = circleSegments + 1;
        lr.useWorldSpace = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        outMat = CreateURPMaterial(baseColor, emissionMultiplier);
        lr.material = outMat;
        lr.startWidth = 0f;
        lr.endWidth = 0f;
    }

    void CreatePillar()
    {
        GameObject pillarObj = new GameObject("PillarBeam");
        pillarObj.transform.SetParent(transform, false);
        pillarObj.transform.localPosition = Vector3.zero;

        pillarBeam = pillarObj.AddComponent<LineRenderer>();
        pillarBeam.positionCount = 2;
        pillarBeam.useWorldSpace = false;
        pillarBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pillarBeam.receiveShadows = false;

        pillarBeam.SetPosition(0, Vector3.zero);
        pillarBeam.SetPosition(1, Vector3.up * pillarHeight);

        pillarMat = CreateURPMaterial(stunColor, 3f);
        pillarBeam.material = pillarMat;
        pillarBeam.startWidth = 0.35f;
        pillarBeam.endWidth = 0.15f;
    }

    void CreateCracks()
    {
        int crackCount = 4;
        crackPaths = new Vector3[crackCount][];
        crackMat = CreateURPMaterial(stunColor, 3f);

        for (int c = 0; c < crackCount; c++)
        {
            GameObject crackObj = new GameObject("Crack_" + c);
            crackObj.transform.SetParent(transform, false);
            crackObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = crackObj.AddComponent<LineRenderer>();
            lr.loop = false;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = crackMat;
            crackLines.Add(lr);

            // Pre-gera o caminho tortuoso (jagged path)
            int segments = 6;
            crackPaths[c] = new Vector3[segments];
            float angle = (c * 90f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            Vector3 right = new Vector3(-dir.z, 0, dir.x);

            crackPaths[c][0] = Vector3.zero;
            for (int i = 1; i < segments; i++)
            {
                float segmentDist = (float)i / (segments - 1);
                float jitter = Random.Range(-0.08f, 0.08f) * maxRadius;
                float factor = Mathf.Sin(segmentDist * Mathf.PI);

                crackPaths[c][i] = dir * (segmentDist * maxRadius) + right * (jitter * factor);
            }
        }
    }

    void CreateParticles()
    {
        // 1. Sparks System (Faíscas rápidas esticadas)
        GameObject sparksObj = new GameObject("SparksVFX");
        sparksObj.transform.SetParent(transform, false);
        sparksObj.transform.localPosition = Vector3.up * 0.1f;
        sparksSystem = sparksObj.AddComponent<ParticleSystem>();
        SetupSparks();

        // 2. Debris System (Estilhaços pesados pulando)
        GameObject debrisObj = new GameObject("DebrisVFX");
        debrisObj.transform.SetParent(transform, false);
        debrisObj.transform.localPosition = Vector3.up * 0.1f;
        debrisSystem = debrisObj.AddComponent<ParticleSystem>();
        SetupDebris();

        // 3. Dust System (Onda de poeira soft no chão)
        GameObject dustObj = new GameObject("DustVFX");
        dustObj.transform.SetParent(transform, false);
        dustObj.transform.localPosition = Vector3.up * 0.05f;
        dustSystem = dustObj.AddComponent<ParticleSystem>();
        SetupDust();

        // Toca todos os sistemas de partículas
        sparksSystem.Play();
        debrisSystem.Play();
        dustSystem.Play();
    }

    void SetupSparks()
    {
        var main = sparksSystem.main;
        main.duration = totalDuration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(stunColor, flashColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = particleCount;

        var emission = sparksSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, (short)particleCount)
        });

        var shape = sparksSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var sizeOverLifetime = sparksSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rend = sparksSystem.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.12f;
        rend.lengthScale = 1.5f;
        
        SetupRendererMaterial(rend, stunColor, 4f, true);
    }

    void SetupDebris()
    {
        var main = debrisSystem.main;
        main.duration = totalDuration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(stunColor, Color.white);
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 15;

        var emission = debrisSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 15)
        });

        var shape = debrisSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.3f;
        debrisSystem.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Aponta para cima

        var rotationOverLifetime = debrisSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

        var sizeOverLifetime = debrisSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.8f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rend = debrisSystem.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        SetupRendererMaterial(rend, stunColor * 0.8f, 2f, true);
    }

    void SetupDust()
    {
        var main = dustSystem.main;
        main.duration = totalDuration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        Color dColor = stunColor;
        dColor.a = 0.2f;
        main.startColor = dColor;
        main.gravityModifier = -0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 20;

        var emission = dustSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 20)
        });

        var shape = dustSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        shape.radiusThickness = 0.1f;
        dustSystem.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Plano no chão

        var colorOverLifetime = dustSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(stunColor, 0f),
                new GradientColorKey(stunColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.2f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = dustSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(1f, 1.5f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rend = dustSystem.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        SetupRendererMaterial(rend, stunColor, 0f, false);
    }

    void DrawCircle(LineRenderer lr, float r, Material mat, float progressT)
    {
        if (lr == null || mat == null) return;

        float width = Mathf.Lerp(ringWidth, 0f, progressT);
        lr.startWidth = width;
        lr.endWidth   = width;
        
        Color col = mat.GetColor("_BaseColor");
        col.a = 1f - progressT;
        mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", col * (3f * (1f - progressT)));
        }

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;
            lr.SetPosition(i, new Vector3(x, 0.05f, z));
        }
    }

    Vector3 GetPositionOnPath(Vector3[] path, float t)
    {
        if (path == null || path.Length == 0) return Vector3.zero;
        float exactIndex = t * (path.Length - 1);
        int index = Mathf.FloorToInt(exactIndex);
        if (index >= path.Length - 1) return path[path.Length - 1];
        float frac = exactIndex - index;
        return Vector3.Lerp(path[index], path[index + 1], frac);
    }

    Material CreateURPMaterial(Color baseColor, float emissionMultiplier, bool additive = true)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        
        Material mat = new Material(shader);
        materialsToDestroy.Add(mat);
        mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", baseColor * emissionMultiplier);
        }
        
        if (mat.HasProperty("_SrcBlend") && mat.HasProperty("_DstBlend"))
        {
            if (additive)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
        }
        return mat;
    }

    void SetupRendererMaterial(ParticleSystemRenderer rend, Color color, float emissionMultiplier, bool additive = true)
    {
        Material mat = rend.material; // Clona o material padrão (com textura redonda)
        materialsToDestroy.Add(mat);

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emissionMultiplier);
        }

        if (mat.HasProperty("_SrcBlend") && mat.HasProperty("_DstBlend"))
        {
            if (additive)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
        }
    }

    public void SetRadius(float radius)
    {
        maxRadius = radius;
    }

    void OnDestroy()
    {
        foreach (Material mat in materialsToDestroy)
        {
            if (mat != null) Destroy(mat);
        }
        materialsToDestroy.Clear();
    }
}

