using UnityEngine;

/// <summary>
/// VFX de Morte do ShardSwarm — Explosão violenta de cristal se estilhaçando
/// Uso: Crie um GameObject vazio, adicione este script, salve como Prefab.
/// Arraste o prefab no campo "Death Explosion VFX" do ShardSwarm_AI.
/// Auto-destrói após executar.
/// </summary>
public class ShardDeathVFX : MonoBehaviour
{
    [Header("Configuração")]
    public Color coreColor = new Color(1f, 0.4f, 0.2f, 1f);      // Laranja quente
    public Color outerColor = new Color(0.4f, 0.8f, 1f, 1f);      // Ciano cristal
    public Color flashColor = new Color(1f, 1f, 0.8f, 1f);        // Branco quente
    public float duration = 1.5f;
    public float explosionRadius = 3f;

    void Start()
    {
        CreateCoreExplosion();
        CreateShardDebris();
        CreateFlashBurst();
        CreateSmokeTrail();
        CreateGroundRing();

        Destroy(gameObject, duration + 1f);
    }

    /// <summary>
    /// Explosão central com partículas grandes expandindo
    /// </summary>
    void CreateCoreExplosion()
    {
        GameObject obj = new GameObject("CoreExplosion");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(coreColor, outerColor);
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 30, 40)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0, 1f),
            new Keyframe(0.3f, 1.2f),
            new Keyframe(1, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(flashColor, 0f),
                new GradientColorKey(coreColor, 0.3f),
                new GradientColorKey(outerColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial();
    }

    /// <summary>
    /// Debris de cristal — partículas que caem com gravidade simulando estilhaços
    /// </summary>
    void CreateShardDebris()
    {
        GameObject obj = new GameObject("ShardDebris");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = outerColor;
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
        main.maxParticles = 25;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 15, 25)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(outerColor, 0f), new GradientColorKey(outerColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.3f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial();
    }

    /// <summary>
    /// Flash inicial — clarão branco rápido
    /// </summary>
    void CreateFlashBurst()
    {
        GameObject obj = new GameObject("Flash");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = 0.2f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 1)
        });

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0, 1f),
            new Keyframe(1, explosionRadius * 2f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(flashColor, 0f), new GradientColorKey(flashColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    /// <summary>
    /// Fumaça/energia que se dissipa lentamente
    /// </summary>
    void CreateSmokeTrail()
    {
        GameObject obj = new GameObject("EnergySmoke");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor = new Color(outerColor.r, outerColor.g, outerColor.b, 0.3f);
        main.gravityModifier = -0.2f; // Sobe levemente
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 10;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0.1f, 8, 10)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0, 0.5f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1, 1.5f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(outerColor, 0f), new GradientColorKey(Color.gray, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial();
    }

    /// <summary>
    /// Anel de energia no chão que se expande
    /// </summary>
    void CreateGroundRing()
    {
        GameObject obj = new GameObject("GroundRing");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.startSize = 0.5f;
        main.startColor = new Color(coreColor.r, coreColor.g, coreColor.b, 0.4f);
        main.startRotation3D = true;
        main.maxParticles = 1;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0.05f, 1)
        });

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0, 0.5f),
            new Keyframe(0.5f, explosionRadius * 2f),
            new Keyframe(1, explosionRadius * 2.5f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(coreColor, 0f), new GradientColorKey(outerColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
    }

    private static Texture2D _softParticleTex;

    Material GetParticleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Particles/Standard Unlit");

        Material mat = new Material(shader);
        if (_softParticleTex == null)
        {
            const int s = 32;
            _softParticleTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            _softParticleTex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[s * s];
            float h = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Clamp01(Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h)) / h);
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f));
                }
            _softParticleTex.SetPixels(px);
            _softParticleTex.Apply(false, true);
        }
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _softParticleTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _softParticleTex);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        mat.color = Color.white;
        return mat;
    }
}
