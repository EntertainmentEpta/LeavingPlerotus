using UnityEngine;

/// <summary>
/// VFX do Crystal Watcher — Efeitos visuais de partículas para o laser
/// Adiciona partículas de carregamento, aura de energia, e faíscas de impacto.
/// 
/// COMO USAR:
/// 1. Adicione este componente no mesmo GameObject do CrystalWatcher
/// 2. O CrystalWatcher_AI vai chamar os métodos automaticamente
/// </summary>
public class CrystalWatcherVFX : MonoBehaviour
{
    [Header("Cores")]
    public Color coreColor = new Color(0.8f, 0.4f, 1.0f);       // Roxo claro
    public Color glowColor = new Color(0.5f, 0.1f, 0.8f, 0.5f); // Roxo escuro

    // Sistemas de partículas
    private ParticleSystem chargeParticles;   // Partículas durante carregamento
    private ParticleSystem ambientParticles;  // Aura constante ao redor do cristal
    private ParticleSystem laserParticles;    // Partículas ao longo do laser
    private ParticleSystem impactParticles;   // Faíscas no ponto de impacto (parede)

    private Renderer modelRenderer;

    void Start()
    {
        modelRenderer = GetComponentInChildren<Renderer>();
        Vector3 center = GetCenter();

        CreateAmbientParticles(center);
        CreateChargeParticles(center);
        CreateLaserParticles(center);
        CreateImpactParticles(center);
    }

    Vector3 GetCenter()
    {
        if (modelRenderer != null)
            return modelRenderer.bounds.center;
        return transform.position;
    }

    // =============================================
    // AURA AMBIENTE — partículas flutuando ao redor do cristal sempre
    // =============================================
    void CreateAmbientParticles(Vector3 center)
    {
        GameObject obj = new GameObject("AmbientVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        ambientParticles = obj.AddComponent<ParticleSystem>();

        var main = ambientParticles.main;
        main.startLifetime = 2f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.3f, 1f, 0.6f),
            new Color(0.9f, 0.7f, 1f, 0.8f)
        );
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ambientParticles.emission;
        emission.rateOverTime = 8f;

        var shape = ambientParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        // Tamanho diminui ao longo da vida
        var sizeOverLifetime = ambientParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Cor com fade
        var colorOverLifetime = ambientParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGrad = new Gradient();
        colorGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.7f, 0.3f, 1f), 0f),
                new GradientColorKey(new Color(0.9f, 0.6f, 1f), 0.5f),
                new GradientColorKey(new Color(0.5f, 0.1f, 0.8f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.7f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = colorGrad;

        // Material
        SetupParticleMaterial(ambientParticles);
    }

    // =============================================
    // CARREGAMENTO — partículas convergindo para o centro
    // =============================================
    void CreateChargeParticles(Vector3 center)
    {
        GameObject obj = new GameObject("ChargeVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        chargeParticles = obj.AddComponent<ParticleSystem>();

        var main = chargeParticles.main;
        main.startLifetime = 0.5f;
        main.startSpeed = -2f; // Negativa = partículas vão PARA o centro (convergem)
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.4f, 1f, 0.9f),
            new Color(1f, 0.8f, 1f, 1f)
        );
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = chargeParticles.emission;
        emission.rateOverTime = 40f;

        var shape = chargeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2f; // Partículas nascem espalhadas e convergem

        // Tamanho diminui
        var sizeOverLifetime = chargeParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        SetupParticleMaterial(chargeParticles);

        // Começa desligado
        chargeParticles.Stop();
    }

    // =============================================
    // LASER — partículas ao longo do feixe
    // =============================================
    void CreateLaserParticles(Vector3 center)
    {
        GameObject obj = new GameObject("LaserTrailVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        laserParticles = obj.AddComponent<ParticleSystem>();

        var main = laserParticles.main;
        main.startLifetime = 0.3f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.6f, 1f, 0.8f),
            new Color(1f, 1f, 1f, 1f)
        );
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = laserParticles.emission;
        emission.rateOverTime = 30f;

        // Forma = cone fino na direção do laser
        var shape = laserParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f; // Cone bem estreito
        shape.radius = 0.1f;

        // Tamanho diminui
        var sizeOverLifetime = laserParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Cor com fade
        var colorOverLifetime = laserParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGrad = new Gradient();
        colorGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.8f, 1f), 0f),
                new GradientColorKey(new Color(0.6f, 0.1f, 0.9f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = colorGrad;

        SetupParticleMaterial(laserParticles);

        // Começa desligado
        laserParticles.Stop();
    }

    void CreateImpactParticles(Vector3 center)
    {
        GameObject obj = new GameObject("LaserImpactVFX");
        obj.transform.SetParent(transform);
        obj.transform.position = center;

        impactParticles = obj.AddComponent<ParticleSystem>();

        var main = impactParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.6f, 1f, 1f),
            new Color(1f, 0.8f, 1f, 0.9f)
        );
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = impactParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 40f;

        var shape = impactParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f; // cone de faíscas se espalhando
        shape.radius = 0.05f;

        var sizeOverLifetime = impactParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        SetupParticleMaterial(impactParticles);

        // Começa desligado
        impactParticles.Stop();
    }

    // =============================================
    // MATERIAL — configura material aditivo para todas as partículas
    // =============================================
    private static Texture2D _softTex;

    void SetupParticleMaterial(ParticleSystem ps)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Shader addShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Particles/Standard Unlit");

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
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f));
                }
            _softTex.SetPixels(px);
            _softTex.Apply(false, true);
        }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _softTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _softTex);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        renderer.material = mat;
    }

    // =============================================
    // MÉTODOS PÚBLICOS — chamados pelo CrystalWatcher_AI
    // =============================================

    /// <summary>
    /// Ativa efeito de carregamento (partículas convergindo para o cristal)
    /// </summary>
    public void StartChargeEffect()
    {
        if (chargeParticles != null)
        {
            chargeParticles.transform.position = GetCenter();
            chargeParticles.Play();
        }
    }

    /// <summary>
    /// Para efeito de carregamento
    /// </summary>
    public void StopChargeEffect()
    {
        if (chargeParticles != null)
            chargeParticles.Stop();
    }

    /// <summary>
    /// Ativa partículas ao longo do laser
    /// </summary>
    public void StartLaserEffect(Vector3 direction)
    {
        if (laserParticles != null)
        {
            laserParticles.transform.position = GetCenter();
            UpdateLaserDirection(direction);
            laserParticles.Play();
        }
        if (impactParticles != null)
        {
            impactParticles.Play();
        }
    }

    /// <summary>
    /// Atualiza a direção das partículas do laser (chamado todo frame durante firing)
    /// </summary>
    public void UpdateLaserDirection(Vector3 direction)
    {
        if (laserParticles != null)
        {
            laserParticles.transform.position = GetCenter();
            laserParticles.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    /// <summary>
    /// Atualiza o ponto de colisão e a normal do impacto do laser
    /// </summary>
    public void UpdateLaserImpact(Vector3 impactPoint, Vector3 normal)
    {
        if (impactParticles != null)
        {
            impactParticles.transform.position = impactPoint;
            if (normal != Vector3.zero)
            {
                // Faz as faíscas ricochetearem para longe da parede
                impactParticles.transform.rotation = Quaternion.LookRotation(normal);
            }
        }
    }

    /// <summary>
    /// Para partículas do laser
    /// </summary>
    public void StopLaserEffect()
    {
        if (laserParticles != null)
            laserParticles.Stop();
        if (impactParticles != null)
            impactParticles.Stop();
    }

    /// <summary>
    /// Aumenta a intensidade da aura ambiente (durante ataque)
    /// </summary>
    public void SetAmbientIntensity(float multiplier)
    {
        if (ambientParticles != null)
        {
            var emission = ambientParticles.emission;
            emission.rateOverTime = 8f * multiplier;
        }
    }
}
