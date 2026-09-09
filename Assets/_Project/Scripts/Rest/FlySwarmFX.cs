using UnityEngine;

/// <summary>
/// Efeito Visual e Animação Orgânica de Enxame de Moscas (Fauna Recursos de Bolsa Sintética)
/// Aplica turbulência 3D com Noise, flutuação, rastro (trail) e brilho pulsante de item coletável.
/// </summary>
public class FlySwarmFX : MonoBehaviour
{
    [Header("Órbita e Flutuação")]
    public float orbitSpeed = 110f;
    public float bobHeight = 0.28f;
    public float bobSpeed = 2.2f;

    [Header("Tamanho e Escala do Enxame")]
    [Tooltip("Escala geral do enxame de moscas (padrão: 2.5 para boa visibilidade 3D)")]
    public float swarmScale = 2.5f;

    [Header("Turbulência e Vibração (Noise)")]
    public float noiseIntensity = 0.12f;
    public float noiseFrequency = 9.0f;

    [Header("Brilho Pulsante (Indicador de Item Coletável)")]
    public bool enablePulsingGlow = true;
    public Color glowColor = new Color(0.2f, 0.95f, 0.85f, 1f); // Glow Cyan Bioluminescente
    public float glowPulseSpeed = 3.5f;
    public float minGlowIntensity = 0.2f;
    public float maxGlowIntensity = 1.0f;

    [Header("Rastro de Voo (Trail)")]
    public bool enableTrail = true;
    [Tooltip("Cor do rastro das moscas (editável no Inspector)")]
    public Color trailColor = new Color(0.2f, 0.95f, 0.85f, 0.8f); // Cyan translúcido por padrão
    public float trailDuration = 0.30f;
    public float trailStartWidth = 0.06f;

    private Vector3 initialLocalPos;
    private float seedX;
    private float seedY;
    private float seedZ;
    private Transform[] flyNodes;
    private Renderer[] flyRenderers;
    private Vector3[] nodeInitialPos;
    private Light cachedLight;
    private Material[] flyMaterials;
    private TrailRenderer[] flyTrails;

    void Start()
    {
        initialLocalPos = transform.localPosition;
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(100f, 200f);
        seedZ = Random.Range(200f, 300f);

        // Coleta APENAS as 3 moscas reais com Renderer
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var flyList = new System.Collections.Generic.List<Transform>();
        var matList = new System.Collections.Generic.List<Material>();

        if (renderers != null && renderers.Length > 0)
        {
            foreach (var r in renderers)
            {
                if (r != null && !r.gameObject.name.Contains("Canvas") && !r.gameObject.name.Contains("Text"))
                {
                    flyList.Add(r.transform);

                    foreach (var mat in r.materials)
                    {
                        if (mat != null)
                        {
                            mat.EnableKeyword("_EMISSION");
                            matList.Add(mat);
                        }
                    }
                }
            }
        }

        flyNodes = flyList.ToArray();
        flyRenderers = renderers;
        flyMaterials = matList.ToArray();
        nodeInitialPos = new Vector3[flyNodes.Length];
        flyTrails = new TrailRenderer[flyNodes.Length];

        // Configura a PointLight pulsante no centro do enxame de moscas
        cachedLight = GetComponentInChildren<Light>(true);
        if (cachedLight == null)
        {
            GameObject lightGo = new GameObject("FlySwarmLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.zero;
            cachedLight = lightGo.AddComponent<Light>();
            cachedLight.type = LightType.Point;
        }

        if (cachedLight != null)
        {
            cachedLight.color = glowColor;
            cachedLight.range = 2.5f;
            cachedLight.intensity = minGlowIntensity;
            cachedLight.enabled = enablePulsingGlow;
        }

        Shader trailShader = Shader.Find("Sprites/Default") ??
                             Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                             Shader.Find("Universal Render Pipeline/Unlit") ??
                             Shader.Find("Particles/Standard Unlit");

        for (int i = 0; i < flyNodes.Length; i++)
        {
            nodeInitialPos[i] = flyNodes[i].localPosition;

            // Adiciona RASTRO (TrailRenderer) estritamente nas moscas reais
            if (enableTrail)
            {
                TrailRenderer tr = flyNodes[i].GetComponent<TrailRenderer>();
                if (tr == null)
                {
                    tr = flyNodes[i].gameObject.AddComponent<TrailRenderer>();
                }

                tr.time = trailDuration > 0.05f ? trailDuration : 0.30f;
                tr.startWidth = trailStartWidth > 0.01f ? trailStartWidth : 0.06f;
                tr.endWidth = 0.00f;
                tr.minVertexDistance = 0.01f;
                tr.alignment = LineAlignment.View;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.emitting = true;

                if (trailShader != null)
                {
                    Material trailMat = new Material(trailShader);
                    trailMat.color = Color.white;
                    tr.material = trailMat;
                }

                ApplyTrailColor(tr, trailColor);
                flyTrails[i] = tr;
            }
        }

        // Tenta rodar a animação nativa do FBX caso exista Animation ou Animator
        Animation animComponent = GetComponentInChildren<Animation>();
        if (animComponent != null)
        {
            animComponent.wrapMode = WrapMode.Loop;
            animComponent.Play();
        }
    }

    void Update()
    {
        float t = Time.time;

        // 1. Flutuação Vertical Sobe e Desce
        float newY = initialLocalPos.y + Mathf.Sin(t * bobSpeed) * bobHeight;
        transform.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);

        // 2. Rotação de Órbita Elíptica em 3D
        transform.Rotate(Vector3.up, orbitSpeed * Time.deltaTime, Space.Self);

        // 3. Turbulência e Vibração Orgânica de Insetos (Perlin Noise em X, Y, Z)
        if (flyNodes != null)
        {
            for (int i = 0; i < flyNodes.Length; i++)
            {
                if (flyNodes[i] == null || !flyNodes[i].gameObject.activeInHierarchy) continue;

                float offsetX = (Mathf.PerlinNoise(t * noiseFrequency + seedX + i * 2.5f, 0f) - 0.5f) * noiseIntensity * 2f;
                float offsetY = (Mathf.PerlinNoise(t * noiseFrequency + seedY + i * 2.5f, 1f) - 0.5f) * noiseIntensity * 2f;
                float offsetZ = (Mathf.PerlinNoise(t * noiseFrequency + seedZ + i * 2.5f, 2f) - 0.5f) * noiseIntensity * 2f;

                flyNodes[i].localPosition = nodeInitialPos[i] + new Vector3(offsetX, offsetY, offsetZ);
            }
        }

        // 4. Brilho Pulsante de Item Coletável (Glow Oscilante no Material e na Luz)
        if (enablePulsingGlow)
        {
            float pulseT = (Mathf.Sin(t * glowPulseSpeed) + 1.0f) * 0.5f;
            float currentIntensity = Mathf.Lerp(minGlowIntensity, maxGlowIntensity, pulseT);

            if (cachedLight != null)
            {
                cachedLight.color = glowColor;
                cachedLight.intensity = currentIntensity;
            }

            if (flyMaterials != null)
            {
                Color emission = glowColor * currentIntensity;
                foreach (var mat in flyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetColor("_EmissionColor", emission);
                    }
                }
            }
        }
    }

    private void ApplyTrailColor(TrailRenderer tr, Color color)
    {
        if (tr == null) return;
        Gradient gradient = new Gradient();
        Color endColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0f);
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(color, 0.0f), 
                new GradientColorKey(endColor, 1.0f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(color.a, 0.0f), 
                new GradientAlphaKey(0.0f, 1.0f) 
            }
        );
        tr.colorGradient = gradient;
    }

    void OnValidate()
    {
        if (flyTrails != null)
        {
            foreach (var tr in flyTrails)
            {
                if (tr != null)
                {
                    tr.time = trailDuration;
                    tr.startWidth = trailStartWidth;
                    ApplyTrailColor(tr, trailColor);
                }
            }
        }
    }
}
