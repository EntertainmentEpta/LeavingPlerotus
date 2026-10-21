using UnityEngine;

/// <summary>
/// Efeito de Raio de Ataque (Skybeam/Lightning) da MagicStone.
/// Gera proceduralmente um feixe vertical instável (eletricidade), um anel de impacto (shockwave)
/// que se expande no chão, uma explosão de faíscas físicas e um flash de luz dinâmica.
/// Todas as propriedades visuais podem ser ajustadas em tempo real no Inspector.
/// </summary>
public class AttackBeam : MonoBehaviour
{
    [Header("Configurações do Combate")]
    public int damage = 40;
    public float radius = 2f;
    [Tooltip("Tempo em segundos que o efeito visual do raio fica na tela antes de desaparecer.")]
    public float lifetime = 0.85f;

    [Header("Aparência do Raio")]
    [Tooltip("Cor do núcleo interno do raio (normalmente um branco-amarelado brilhante).")]
    public Color coreColor = new Color(1f, 1f, 0.85f, 1f);
    [Tooltip("Cor da aura brilhante do raio.")]
    public Color glowColor = new Color(1f, 0.72f, 0f, 1f);
    [Range(0.05f, 2f)]
    [Tooltip("Intensidade do zigue-zague (desvio horizontal).")]
    public float jaggedness = 0.35f;
    [Tooltip("Altura de origem do raio no céu.")]
    public float skyHeight = 35f;
    [Range(4, 30)]
    [Tooltip("Quantidade de dobras/segmentos que formam o raio (menos = mais limpo).")]
    public int segmentsCount = 8;
    [Range(0f, 0.9f)]
    [Tooltip("Progresso (0 a 1) até o qual o raio fica 100% visível antes de começar a sumir. Maior = some mais tarde na saída.")]
    public float sustainProgress = 0.5f;

    [Header("Magia / Partículas")]
    [Tooltip("Quantidade de partículas de energia que sobem do chão.")]
    public int particleCount = 15;
    [Tooltip("Velocidade inicial de subida das partículas.")]
    public float particleSpeed = 3.5f;
    [Tooltip("Tempo que as partículas duram (elas continuam flutuando mesmo após o raio sumir!).")]
    public float particleLifetime = 1.2f;

    [Header("Iluminação de Impacto")]
    [Tooltip("Intensidade do clarão de luz gerado no chão.")]
    public float lightIntensity = 8f;

    [HideInInspector]
    public GameObject owner; // O inimigo que criou este raio (para thorns)

    [Header("Elite — MagicStone Mark System")]
    [Tooltip("Ative SOMENTE no AttackBeam do Prefab Variant da MagicStone Elite. " +
             "Substitui o dano direto pelo sistema de marca+stun e muda as cores para laranja.")]
    public bool isEliteBeam = false;

    // --- Componentes Internos Procedurais ---
    private LineRenderer coreBeam;     // Linha central fina e intensa
    private LineRenderer auraBeam;     // Linha externa grossa e brilhante
    private LineRenderer shockwaveRing; // Círculo de impacto em expansão
    private ParticleSystem sparks;
    private Light flashLight;
    
    private float elapsed = 0f;
    private Material beamMat;
    private Vector3[] pathPositions;

    void Start()
    {
        // Reseta a escala do transform raiz para evitar que a escala distorcida do prefab original (0.1, 30, 0.1)
        // encolha as linhas e a onda de choque horizontalmente no mundo.
        transform.localScale = Vector3.one;

        pathPositions = new Vector3[segmentsCount];

        // 1. Limpa componentes visuais obsoletos do prefab antigo
        MeshRenderer oldMesh = GetComponent<MeshRenderer>();
        if (oldMesh != null) oldMesh.enabled = false;

        MeshFilter oldFilter = GetComponent<MeshFilter>();
        if (oldFilter != null) Destroy(oldFilter);

        // Desativa filhos legados (como VFX Graphs quebrados ou cilindros antigos)
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }

        // 2. Cria o material aditivo luminoso para o raio
        Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (additiveShader == null)
        {
            additiveShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
        beamMat = new Material(additiveShader);

        // 3. Constrói os emissores e linhas procedurais
        // Se for raio elite, sobrepoe as cores para laranja antes de BuildVisuals
        if (isEliteBeam)
        {
            coreColor = new Color(1f, 0.7f, 0.1f, 1f);  // amarelo-laranja intenso
            glowColor = new Color(1f, 0.35f, 0f, 1f);   // laranja puro
        }

        BuildVisuals();

        // 4. Aplica dano imediato em área
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerHealth pHealth = hit.GetComponent<PlayerHealth>();
                if (pHealth != null)
                {
                    if (isEliteBeam && MagicStoneMarkSystem.Instance != null)
                    {
                        // Logica elite: marca + stun ao inves de dano direto
                        MagicStoneMarkSystem.Instance.OnLaserHit(hit.gameObject);
                    }
                    else
                    {
                        pHealth.TakeDamage(damage, owner);
                    }
                }
            }
        }


        // 5. Autodestruição do objeto após o tempo de vida
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / lifetime);

        // Curva de opacidade customizável na saída (sustain)
        float alpha = 1f;
        if (progress > sustainProgress)
        {
            alpha = 1f - (progress - sustainProgress) / (1f - sustainProgress);
        }

        Color currentCore = coreColor;
        currentCore.a = alpha;
        Color currentGlow = glowColor;
        currentGlow.a = alpha;

        // Gera o caminho elétrico (zig-zag com base em ruído) uma vez por frame
        // O raio fica mais "calmo" no final enquanto se desfaz, crescendo a partir do chão
        float currentJaggedness = Mathf.Lerp(jaggedness, jaggedness * 0.15f, progress);
        GenerateLightningPath(currentJaggedness, progress);

        // --- Animação do Raio Vertical (Flicker & Jitter de eletricidade) ---
        float coreWidth = Mathf.Lerp(0.12f, 0.0f, progress); 
        float auraWidth = Mathf.Lerp(0.48f, 0.0f, progress); 

        UpdateVerticalBeam(coreBeam, coreWidth, currentCore);
        UpdateVerticalBeam(auraBeam, auraWidth, currentGlow);

        // --- Animação do Anel de Impacto (Expande e some) ---
        float waveRadius = Mathf.Lerp(0f, radius * 1.05f, progress); 
        float waveWidth = Mathf.Lerp(0.1f, 0f, progress); 
        UpdateShockwave(shockwaveRing, waveRadius, waveWidth, currentGlow);

        // --- Animação da Luz ---
        if (flashLight != null)
        {
            flashLight.intensity = Mathf.Lerp(lightIntensity, 0f, progress);
        }
    }

    // =========================================================
    // CONSTRUÇÃO E DESENHO PROCEDURAL
    // =========================================================

    void BuildVisuals()
    {
        // Linha central
        coreBeam = CreateLineObject("CoreBeam");
        coreBeam.loop = false;

        // Aura externa
        auraBeam = CreateLineObject("AuraBeam");
        auraBeam.loop = false;

        // Anel de impacto no chão
        shockwaveRing = CreateLineObject("ShockwaveRing");
        shockwaveRing.loop = true;

        // Partículas (Faíscas/Sparks)
        BuildParticles(glowColor, coreColor);

        // Luz dinâmica de impacto
        BuildLight(glowColor);
    }

    LineRenderer CreateLineObject(string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = beamMat;
        return lr;
    }

    void GenerateLightningPath(float currentJaggedness, float progress)
    {
        int segments = pathPositions.Length;
        Vector3 bottomPoint = new Vector3(0f, -0.2f, 0f); // Rente ao chão
        
        // A altura máxima cresce nos primeiros 35% do efeito (subida rápida e legível)
        float maxHeight = Mathf.Lerp(0f, skyHeight, Mathf.Clamp01(progress / 0.35f));
        
        // O topo do raio fica mais próximo do centro (menos inclinado/caótico)
        Vector3 topPoint = new Vector3(Random.Range(-1.2f, 1.2f), maxHeight, Random.Range(-1.2f, 1.2f));

        pathPositions[0] = bottomPoint;
        for (int i = 1; i < segments - 1; i++)
        {
            float t = (float)i / (segments - 1);
            Vector3 straightPos = Vector3.Lerp(bottomPoint, topPoint, t);

            // Desvios horizontais mais contidos
            float xOffset = Random.Range(-currentJaggedness, currentJaggedness);
            float zOffset = Random.Range(-currentJaggedness, currentJaggedness);

            float curveFactor = Mathf.Sin(t * Mathf.PI);
            Vector3 offset = new Vector3(xOffset, 0f, zOffset) * (1f + curveFactor * 1.5f);

            // Ruído de energia sutil subindo pela descarga (scrolling macio)
            offset.x += Mathf.Sin(t * 8f - elapsed * 12f) * 0.07f;
            offset.z += Mathf.Cos(t * 8f - elapsed * 12f) * 0.07f;

            pathPositions[i] = straightPos + offset;
        }
        pathPositions[segments - 1] = topPoint;
    }

    void UpdateVerticalBeam(LineRenderer lr, float width, Color color)
    {
        if (lr == null) return;

        lr.startWidth = width;
        lr.endWidth   = width * 0.4f;
        lr.startColor = color;
        lr.endColor   = color;

        lr.positionCount = pathPositions.Length;
        lr.SetPositions(pathPositions);
    }

    void UpdateShockwave(LineRenderer lr, float r, float width, Color color)
    {
        if (lr == null) return;

        lr.startWidth = width;
        lr.endWidth   = width;
        lr.startColor = color;
        lr.endColor   = color;

        int segments = 32;
        lr.positionCount = segments + 1;
        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angleRad = (i * angleStep) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angleRad) * r;
            float z = Mathf.Sin(angleRad) * r;

            // Rente ao chão
            lr.SetPosition(i, new Vector3(x, 0.04f, z));
        }
    }

    void BuildParticles(Color color, Color peak)
    {
        GameObject psObj = new GameObject("Sparks");
        psObj.transform.SetParent(transform, false);
        psObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        sparks = psObj.AddComponent<ParticleSystem>();

        var main = sparks.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(particleLifetime * 0.5f, particleLifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(particleSpeed, particleSpeed * 1.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.gravityModifier = -0.2f; // Sobe flutuando suavemente como poeira mágica
        main.maxParticles    = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = sparks.emission;
        emission.enabled = true;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, particleCount) });

        var shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.05f;

        var sizeOverLifetime = sparks.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.8f);
        sizeCurve.AddKey(0.2f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = sparks.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0f), 
                new GradientColorKey(color, 0.4f), 
                new GradientColorKey(peak, 1f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1f, 0f), 
                new GradientAlphaKey(0.8f, 0.6f), 
                new GradientAlphaKey(0f, 1f) 
            }
        );
        colorOverLifetime.color = grad;

        var noise = sparks.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.8f;

        var renderer = psObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = beamMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        sparks.Play();
    }

    void BuildLight(Color color)
    {
        GameObject lightObj = new GameObject("LightFlash");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);

        flashLight = lightObj.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.range = radius * 3f;
        flashLight.intensity = lightIntensity;
        flashLight.color = color;
    }

    private void OnDestroy()
    {
        // Desacopla o sistema de partículas para que continue flutuando e sumindo
        // suavemente no mundo físico mesmo depois que o raio do inimigo foi destruído
        if (sparks != null)
        {
            sparks.transform.SetParent(null);
            var main = sparks.main;
            main.stopAction = ParticleSystemStopAction.Destroy; // Se destrói sozinho ao acabar as partículas
            sparks.Stop();
        }

        if (beamMat != null)
        {
            Destroy(beamMat);
        }
    }
}