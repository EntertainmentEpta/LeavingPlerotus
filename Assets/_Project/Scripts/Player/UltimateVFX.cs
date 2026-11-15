using UnityEngine;

/// <summary>
/// Sistema de VFX para a Ultimate do jogador.
/// Cria efeitos visuais procedurais sem necessidade de assets externos.
/// </summary>
public class UltimateVFX : MonoBehaviour
{
    [Header("VFX Settings")]
    [Tooltip("Cor principal do efeito")]
    public Color primaryColor = new Color(1f, 0.8f, 0f, 1f); // Dourado
    
    [Tooltip("Cor secundária do efeito")]
    public Color secondaryColor = new Color(1f, 0.3f, 0f, 1f); // Laranja
    
    [Tooltip("Intensidade do brilho")]
    [Range(0f, 5f)]
    public float glowIntensity = 2.0f;
    
    [Tooltip("Duração do efeito")]
    public float duration = 5f;
    
    private ParticleSystem auraParticles;
    private ParticleSystem burstParticles;
    private Light pointLight;
    private float timer = 0f;
    private bool isActive = false;
    
    void Start()
    {
        CreateAuraEffect();
        CreateBurstEffect();
        CreateLight();
        
        // Desativar no início
        if (auraParticles != null) auraParticles.Stop();
        if (burstParticles != null) burstParticles.Stop();
        if (pointLight != null) pointLight.enabled = false;
    }
    
    void Update()
    {
        if (isActive)
        {
            timer += Time.deltaTime;
            
            // Pulsar a luz
            if (pointLight != null)
            {
                float pulse = Mathf.Sin(Time.time * 5f) * 0.3f + 0.7f;
                pointLight.intensity = glowIntensity * pulse;
            }
            
            if (timer >= duration)
            {
                StopEffect();
            }
        }
    }
    
    /// <summary>
    /// Ativa o efeito visual da Ultimate.
    /// </summary>
    public void PlayEffect()
    {
        isActive = true;
        timer = 0f;
        
        if (auraParticles != null)
        {
            auraParticles.Play();
        }
        
        if (burstParticles != null)
        {
            burstParticles.Play();
        }
        
        if (pointLight != null)
        {
            pointLight.enabled = true;
        }
        
        Debug.Log("💥 Ultimate VFX ativado!");
    }
    
    /// <summary>
    /// Para o efeito visual.
    /// </summary>
    public void StopEffect()
    {
        isActive = false;
        
        if (auraParticles != null)
        {
            auraParticles.Stop();
        }
        
        if (burstParticles != null)
        {
            burstParticles.Stop();
        }
        
        if (pointLight != null)
        {
            pointLight.enabled = false;
        }
    }
    
    void CreateAuraEffect()
    {
        GameObject auraObj = new GameObject("UltimateAura");
        auraObj.transform.SetParent(transform);
        auraObj.transform.localPosition = Vector3.zero;
        
        auraParticles = auraObj.AddComponent<ParticleSystem>();
        var main = auraParticles.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = 1.5f;
        main.startSpeed = 2f;
        main.startSize = 0.5f;
        main.startColor = new ParticleSystem.MinMaxGradient(primaryColor, secondaryColor);
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        // Emissão
        var emission = auraParticles.emission;
        emission.rateOverTime = 30f;
        
        // Shape - Esfera ao redor do player
        var shape = auraParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;
        shape.radiusThickness = 0.5f;
        
        // Color over lifetime
        var colorOverLifetime = auraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(primaryColor, 0f),
                new GradientColorKey(secondaryColor, 0.5f),
                new GradientColorKey(primaryColor, 1f)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;
        
        // Size over lifetime
        var sizeOverLifetime = auraParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(0.5f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // Renderer
        var renderer = auraObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateUrpParticleMaterial(primaryColor, glowIntensity);
    }
    
    void CreateBurstEffect()
    {
        GameObject burstObj = new GameObject("UltimateBurst");
        burstObj.transform.SetParent(transform);
        burstObj.transform.localPosition = Vector3.zero;
        
        burstParticles = burstObj.AddComponent<ParticleSystem>();
        var main = burstParticles.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 8f;
        main.startSize = 0.8f;
        main.startColor = primaryColor;
        main.maxParticles = 100;
        
        // Emissão em burst
        var emission = burstParticles.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, 50);
        emission.SetBurst(0, burst);
        
        // Shape - Explosão esférica
        var shape = burstParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        
        // Color over lifetime
        var colorOverLifetime = burstParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(primaryColor, 0f),
                new GradientColorKey(secondaryColor, 1f)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;
        
        // Size over lifetime
        var sizeOverLifetime = burstParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // Renderer
        var renderer = burstObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateUrpParticleMaterial(primaryColor, glowIntensity * 2f);
    }

    private static Texture2D _softParticleTex;

    private static Texture2D GetSoftParticleTexture()
    {
        if (_softParticleTex != null) return _softParticleTex;

        const int size = 32;
        _softParticleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _softParticleTex.wrapMode = TextureWrapMode.Clamp;
        _softParticleTex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float alpha = Mathf.Pow(1f - dist, 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        _softParticleTex.SetPixels(pixels);
        _softParticleTex.Apply(false, true);
        return _softParticleTex;
    }

    private static Material CreateUrpParticleMaterial(Color color, float emissionScale)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        Texture2D tex = GetSoftParticleTexture();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f); // Additive
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * emissionScale);
        return mat;
    }
    
    void CreateLight()
    {
        GameObject lightObj = new GameObject("UltimateLight");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = Vector3.up;
        
        pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = primaryColor;
        pointLight.intensity = glowIntensity;
        pointLight.range = 10f;
        pointLight.shadows = LightShadows.None;
        pointLight.enabled = false;
    }
}
