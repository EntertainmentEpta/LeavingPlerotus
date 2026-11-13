using UnityEngine;

/// <summary>
/// Efeito visual de estilhaçamento de cristal (Shatter).
/// Instancia uma explosão cristalina de partículas douradas/âmbar com gravidade,
/// rotação tridimensional, clarão central e anel de onda de choque.
/// </summary>
public class TunerMarkShatterVFX : MonoBehaviour
{
    [Header("Paleta de Cores")]
    [Tooltip("Cor cristalina primária para estilhaços e detritos.")]
    public Color crystalCoreColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);

    [Tooltip("Cor secundária em tom de âmbar para partículas de energia.")]
    public Color crystalAmberColor = new Color(1.0f, 0.55f, 0.1f, 1.0f);

    [Tooltip("Cor do clarão inicial no ponto central da explosão.")]
    public Color flashColor = new Color(1.0f, 1.0f, 0.9f, 1.0f);

    [Header("Configurações da Explosão")]
    [Tooltip("Tempo de vida do objeto antes de se auto-destruir.")]
    [Range(0.5f, 3.0f)]
    public float lifetime = 1.8f;

    [Tooltip("Raio da dispersão dos estilhaços e onda de choque.")]
    [Range(1.0f, 5.0f)]
    public float explosionRadius = 2.0f;

    [Tooltip("Quantidade de estilhaços de cristal gerados.")]
    [Range(10, 60)]
    public int shardCount = 30;

    [Header("Áudio")]
    [Tooltip("Clip de som reproduzido ao estilhaçar o cristal.")]
    public AudioClip shatterAudioClip;

    [Tooltip("Volume da reprodução de áudio.")]
    [Range(0f, 1f)]
    public float audioVolume = 0.85f;

    private void Start()
    {
        ParticleSystem[] existingParticles = GetComponentsInChildren<ParticleSystem>();
        if (existingParticles == null || existingParticles.Length == 0)
        {
            BuildProceduralShatter();
        }

        PlayShatterAudio();
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Constrói os sistemas de partículas em camadas proceduralmente com texturas limpas.
    /// </summary>
    private void BuildProceduralShatter()
    {
        Material shardMat = CreateParticleMaterial(GenerateDiamondShardTexture(64));
        Material glowMat = CreateParticleMaterial(GenerateSoftCircleTexture(64));
        Material ringMat = CreateParticleMaterial(GenerateRingTexture(128));

        CreateCrystalShards(shardMat);
        CreateCentralFlash(glowMat);
        CreateEnergyShockwave(ringMat);
        CreateGlitterEmbers(glowMat);
    }

    /// <summary>
    /// Estilhaços de cristal em losango com velocidade radial, rotação e gravidade.
    /// </summary>
    private void CreateCrystalShards(Material sharedMaterial)
    {
        GameObject shardsObj = new GameObject("CrystalShards");
        shardsObj.transform.SetParent(transform, false);

        ParticleSystem ps = shardsObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(crystalCoreColor, crystalAmberColor);
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = shardCount + 10;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, (short)(shardCount * 0.8f), (short)shardCount)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.6f, 0.85f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(crystalCoreColor, 0f),
                new GradientColorKey(crystalAmberColor, 0.7f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = shardsObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = sharedMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
    }

    /// <summary>
    /// Clarão circular suave e brilhante no ponto de impacto.
    /// </summary>
    private void CreateCentralFlash(Material sharedMaterial)
    {
        GameObject flashObj = new GameObject("CentralFlash");
        flashObj.transform.SetParent(transform, false);

        ParticleSystem ps = flashObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = 0.16f;
        main.startSpeed = 0f;
        main.startSize = 0.6f;
        main.startColor = flashColor;
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 1)
        });

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(1f, explosionRadius * 1.1f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(flashColor, 0f), new GradientColorKey(crystalCoreColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = flashObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = sharedMaterial;

        ps.Play();
    }

    /// <summary>
    /// Onda de choque em formato de anel fino expandindo.
    /// </summary>
    private void CreateEnergyShockwave(Material sharedMaterial)
    {
        GameObject waveObj = new GameObject("Shockwave");
        waveObj.transform.SetParent(transform, false);

        ParticleSystem ps = waveObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 0.3f;
        main.startColor = new Color(crystalAmberColor.r, crystalAmberColor.g, crystalAmberColor.b, 0.85f);
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 1)
        });

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.7f, explosionRadius * 1.5f),
            new Keyframe(1f, explosionRadius * 1.8f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(crystalCoreColor, 0f), new GradientColorKey(crystalAmberColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = waveObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = sharedMaterial;

        ps.Play();
    }

    /// <summary>
    /// Fagulhas circulares que sobem suavemente simulando a dissipação da energia.
    /// </summary>
    private void CreateGlitterEmbers(Material sharedMaterial)
    {
        GameObject embersObj = new GameObject("GlitterEmbers");
        embersObj.transform.SetParent(transform, false);

        ParticleSystem ps = embersObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = crystalCoreColor;
        main.gravityModifier = -0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 15;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0.05f, 10, 15)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(crystalCoreColor, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = embersObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = sharedMaterial;

        ps.Play();
    }

    private void PlayShatterAudio()
    {
        if (shatterAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(shatterAudioClip, transform.position, audioVolume);
        }
    }

    /// <summary>
    /// Cria material compatível com partículas usando shader Sprites/Default ou URP Particles com alfa transparente.
    /// </summary>
    private Material CreateParticleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        mat.mainTexture = texture;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);     // Additive
        mat.color = Color.white;
        return mat;
    }

    /// <summary>
    /// Gera textura em formato de estilhaço de cristal alongado (losango afiado).
    /// </summary>
    private Texture2D GenerateDiamondShardTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Estreita no eixo X para parecer uma lasca afiada de cristal
                float nx = Mathf.Abs(x - half) / (half * 0.55f);
                float ny = Mathf.Abs(y - half) / (half * 0.95f);
                float dist = nx + ny;

                float alpha = Mathf.Clamp01((1f - dist) * 6f);
                float brightness = Mathf.Lerp(0.75f, 1f, 1f - dist);
                tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Gera textura circular suave para brilho e fagulhas.
    /// </summary>
    private Texture2D GenerateSoftCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Gera textura de anel fino para a onda de choque.
    /// </summary>
    private Texture2D GenerateRingTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Anel centrado em 0.78 do raio com espessura de 0.12
                float ringDelta = Mathf.Abs(dist - 0.78f);
                float alpha = Mathf.Clamp01(1f - (ringDelta / 0.12f));
                alpha = alpha * alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }
}