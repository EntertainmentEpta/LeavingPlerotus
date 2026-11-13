using UnityEngine;

/// <summary>
/// Efeito de Raio de Ataque (Skybeam/Lightning) da MagicStone.
/// Gera proceduralmente um poderoso feixe vertical de plasma cósmico/eletricidade descendo do céu,
/// arcos elétricos secundários bifurcados, onda de choque expansiva no solo, faíscas físicas e flash luminoso.
/// </summary>
public class AttackBeam : MonoBehaviour
{
    [Header("Configurações do Combate")]
    public int damage = 40;
    public float radius = 2f;
    [Tooltip("Tempo em segundos que o efeito visual do raio fica na tela antes de desaparecer.")]
    public float lifetime = 0.85f;

    [Header("Aparência do Raio")]
    [Tooltip("Cor do núcleo interno do raio (normalmente um branco-amarelado radiante).")]
    public Color coreColor = new Color(1f, 1f, 0.95f, 1f);
    [Tooltip("Cor da aura brilhante do raio.")]
    public Color glowColor = new Color(1f, 0.72f, 0f, 1f);
    [Range(0.05f, 2f)]
    [Tooltip("Intensidade do zigue-zague elétrico.")]
    public float jaggedness = 0.40f;
    [Tooltip("Altura de origem do raio no céu.")]
    public float skyHeight = 35f;
    [Range(4, 30)]
    [Tooltip("Quantidade de dobras/segmentos que formam o raio principal.")]
    public int segmentsCount = 14;
    [Range(0f, 0.9f)]
    [Tooltip("Progresso até o qual o raio mantém impacto visual alto antes do fade-out.")]
    public float sustainProgress = 0.45f;

    [Header("Magia / Partículas")]
    [Tooltip("Quantidade de partículas de energia que sobem do chão.")]
    public int particleCount = 28;
    [Tooltip("Velocidade inicial de subida das partículas.")]
    public float particleSpeed = 4.5f;
    [Tooltip("Tempo que as partículas duram.")]
    public float particleLifetime = 1.3f;

    [Header("Iluminação de Impacto")]
    [Tooltip("Intensidade do clarão de luz gerado no chão.")]
    public float lightIntensity = 14f;

    [HideInInspector]
    public GameObject owner; // O inimigo que criou este raio (para thorns)

    [Header("Elite — MagicStone Mark System")]
    [Tooltip("Ative SOMENTE no AttackBeam do Prefab Variant da MagicStone Elite.")]
    public bool isEliteBeam = false;

    // --- Componentes Internos Procedurais ---
    private LineRenderer coreBeam;       // Feixe central denso e incandescente
    private LineRenderer auraBeam;       // Aura externa brilhante de energia
    private LineRenderer forkBeam;       // Arco elétrico secundário crepitando nas laterais
    private LineRenderer groundShockwave;// Onda de choque circular no solo
    private LineRenderer groundGlowDisc; // Disco emissivo rente ao chão no ponto de impacto
    private ParticleSystem sparks;
    private Light flashLight;

    private float elapsed = 0f;
    private Material beamMat;
    private Vector3[] pathPositions;
    private Vector3[] forkPositions;

    void Start()
    {
        // Reseta escala do transform raiz para evitar distorções
        transform.localScale = Vector3.one;

        pathPositions = new Vector3[segmentsCount];
        forkPositions = new Vector3[segmentsCount];

        // 1. Limpa componentes visuais obsoletos do prefab legado
        MeshRenderer oldMesh = GetComponent<MeshRenderer>();
        if (oldMesh != null) oldMesh.enabled = false;

        MeshFilter oldFilter = GetComponent<MeshFilter>();
        if (oldFilter != null) Destroy(oldFilter);

        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }

        // 2. Material aditivo com emissão luminosa compatível com URP e Built-in
        Shader additiveShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (additiveShader == null)
            additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (additiveShader == null)
            additiveShader = Shader.Find("Sprites/Default");

        beamMat = new Material(additiveShader);
        if (beamMat.HasProperty("_Surface")) beamMat.SetFloat("_Surface", 1); // Transparent
        if (beamMat.HasProperty("_Blend")) beamMat.SetFloat("_Blend", 0);     // Additive

        // 3. Configura cores se for elite
        if (isEliteBeam)
        {
            coreColor = new Color(1f, 0.85f, 0.4f, 1f); // Amarelo-dourado intenso
            glowColor = new Color(1f, 0.38f, 0.05f, 1f); // Laranja avermelhado cósmico
        }

        BuildVisuals();

        // 4. Aplica dano imediato em área com validação rigorosa
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
                        MagicStoneMarkSystem.Instance.OnLaserHit(hit.gameObject);
                    }
                    else
                    {
                        pHealth.TakeDamage(damage, owner);
                    }
                }
            }
        }

        // 5. Destruição após o lifetime
        // Gera a área eletrocutada no solo por 2 segundos (efeito de choque idêntico ao da Star)
        ElectricGroundHazard.Spawn(transform.position, radius: radius * 0.95f, duration: 2.0f, damage: 5);

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / lifetime);

        // Curva de opacidade com pico de impacto inicial e dissipação suave
        float alpha = 1f;
        if (progress > sustainProgress)
        {
            float fadeProgress = (progress - sustainProgress) / (1f - sustainProgress);
            alpha = Mathf.Pow(1f - fadeProgress, 1.8f);
        }

        Color currentCore = coreColor;
        currentCore.a = alpha;
        Color currentGlow = glowColor;
        currentGlow.a = alpha;

        // O raio atinge o solo no instante zero e mantém eletricidade fluida com harmônicos
        float currentJaggedness = Mathf.Lerp(jaggedness, jaggedness * 0.25f, progress);
        GenerateSkyStrikePath(currentJaggedness, progress);

        // --- Espessuras do Raio (Feixe robusto, não mais linha de pesca) ---
        // Decaimento quadrático dá a sensação de choque estrondoso inicial
        float widthFade = Mathf.Pow(1f - progress * 0.75f, 2f);
        float coreWidth = Mathf.Lerp(0.55f, 0.10f, progress) * widthFade;
        float auraWidth = Mathf.Lerp(2.20f, 0.40f, progress) * widthFade;
        float forkWidth = Mathf.Lerp(0.25f, 0.04f, progress) * widthFade;

        UpdateVerticalBeam(coreBeam, coreWidth, currentCore);
        UpdateVerticalBeam(auraBeam, auraWidth, currentGlow);
        UpdateVerticalBeam(forkBeam, forkWidth, new Color(currentGlow.r, currentGlow.g, currentGlow.b, alpha * 0.75f), forkPositions);

        // --- Onda de Choque no Solo (Impact Shockwave) ---
        // Expande muito rápido nos primeiros 30% do tempo e se desfaz
        float waveProgress = Mathf.Clamp01(progress / 0.7f);
        float waveRadius = Mathf.Lerp(0.1f, radius * 1.15f, Mathf.Sqrt(waveProgress));
        float waveWidth = Mathf.Lerp(0.18f, 0f, waveProgress);
        UpdateShockwave(groundShockwave, waveRadius, waveWidth, currentGlow);

        // --- Disco Central de Impacto no Solo ---
        float discRadius = Mathf.Lerp(radius * 0.45f, 0f, progress);
        UpdateShockwave(groundGlowDisc, discRadius, 0.12f * alpha, currentCore);

        // --- Luz Dinâmica com decaimento exponencial realista ---
        if (flashLight != null)
        {
            flashLight.intensity = lightIntensity * Mathf.Pow(1f - progress, 2.5f);
        }
    }

    void BuildVisuals()
    {
        // Linha central (Núcleo denso incandescente)
        coreBeam = CreateLineObject("CoreBeam");
        coreBeam.loop = false;

        // Aura externa luminosa
        auraBeam = CreateLineObject("AuraBeam");
        auraBeam.loop = false;

        // Arco secundário de faíscas laterais
        forkBeam = CreateLineObject("ForkBeam");
        forkBeam.loop = false;

        // Anel de choque no solo
        groundShockwave = CreateLineObject("ShockwaveRing");
        groundShockwave.loop = true;

        // Disco de impacto central no solo
        groundGlowDisc = CreateLineObject("GroundGlowDisc");
        groundGlowDisc.loop = true;

        // Partículas de faíscas
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
        lr.alignment = LineAlignment.View; // Sempre voltado de frente para a câmera
        return lr;
    }

    void GenerateSkyStrikePath(float currentJaggedness, float progress)
    {
        int segments = pathPositions.Length;
        Vector3 bottomPoint = new Vector3(0f, -0.05f, 0f); // Rente ao solo
        Vector3 topPoint = new Vector3(0f, skyHeight, 0f);   // Origem nas alturas celestiais

        pathPositions[0] = bottomPoint;
        forkPositions[0] = bottomPoint;

        // Ruído contínuo que corre ao longo do tempo (evita flicker quebrado de random puro)
        float timeOffset = elapsed * 14f;

        for (int i = 1; i < segments - 1; i++)
        {
            float t = (float)i / (segments - 1);
            Vector3 straightPos = Vector3.Lerp(bottomPoint, topPoint, t);

            // Perlin Noise harmônico + senoide contínua
            float pX = (Mathf.PerlinNoise(t * 3.5f, timeOffset) - 0.5f) * 2f;
            float pZ = (Mathf.PerlinNoise(t * 3.5f + 19.3f, timeOffset) - 0.5f) * 2f;

            // Arco elétrico mais acentuado na faixa intermediária e mais firme perto do chão
            float arcFactor = Mathf.Sin(t * Mathf.PI);
            float xOffset = pX * currentJaggedness * (0.4f + arcFactor * 1.8f);
            float zOffset = pZ * currentJaggedness * (0.4f + arcFactor * 1.8f);

            // Micro-vibração de alta frequência de eletricidade
            float jitter = Mathf.Sin(t * 28f + timeOffset * 2f) * 0.05f;

            pathPositions[i] = straightPos + new Vector3(xOffset + jitter, 0f, zOffset + jitter);

            // Arco secundário (Fork): desvia mais lateralmente criando faíscas ramificadas
            float forkX = xOffset * 2.2f + Mathf.Cos(t * 12f + timeOffset) * 0.45f;
            float forkZ = zOffset * 2.2f + Mathf.Sin(t * 12f + timeOffset) * 0.45f;
            forkPositions[i] = straightPos + new Vector3(forkX, 0f, forkZ);
        }

        pathPositions[segments - 1] = topPoint;
        forkPositions[segments - 1] = topPoint + new Vector3(0.5f, 0f, -0.5f);
    }

    void UpdateVerticalBeam(LineRenderer lr, float width, Color color, Vector3[] customPositions = null)
    {
        if (lr == null) return;

        lr.startWidth = width;
        lr.endWidth   = width * 0.7f; // Mantém o topo volumoso em vez de afinar num cone
        lr.startColor = color;
        lr.endColor   = color;

        Vector3[] pos = customPositions ?? pathPositions;
        lr.positionCount = pos.Length;
        lr.SetPositions(pos);
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
        main.startLifetime   = new ParticleSystem.MinMaxCurve(particleLifetime * 0.6f, particleLifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(particleSpeed * 0.8f, particleSpeed * 2.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.gravityModifier = -0.1f; // Sobe suavemente como faíscas incandescentes
        main.maxParticles    = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = sparks.emission;
        emission.enabled = true;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, particleCount) });

        var shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere; // Espalha em 360 graus na horizontal e para cima
        shape.radius = 0.35f;

        var sizeOverLifetime = sparks.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1.2f);
        sizeCurve.AddKey(0.2f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = sparks.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color, 0.35f),
                new GradientColorKey(peak, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var noise = sparks.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 1.2f;

        var renderer = psObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = beamMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        sparks.Play();
    }

    void BuildLight(Color color)
    {
        GameObject lightObj = new GameObject("LightFlash");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        flashLight = lightObj.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.range = radius * 3.5f;
        flashLight.intensity = lightIntensity;
        flashLight.color = color;
    }

    private void OnDestroy()
    {
        if (sparks != null)
        {
            sparks.transform.SetParent(null);
            var main = sparks.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            sparks.Stop();
        }

        if (beamMat != null)
        {
            Destroy(beamMat);
        }
    }
}
