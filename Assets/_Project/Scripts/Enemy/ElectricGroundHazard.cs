using UnityEngine;

/// <summary>
/// Área Eletrocutada no Solo gerada pelo impacto do raio da Magic Stone.
///  • Fica ativa no chão exatamente por 2 segundos.
///  • Se o jogador passar por cima ou pisar na área, recebe o status Eletrocutado (dano + 50% slow da Star).
///  • Apresenta visual de descarga elétrica no solo com anel pulsante, centelhas e faíscas.
/// </summary>
public class ElectricGroundHazard : MonoBehaviour
{
    [Header("Configurações da Área")]
    public float radius = 1.8f;
    public float duration = 2.0f;
    public int damagePerTick = 5;
    public float slowPercent = 0.50f;
    public float statusDuration = 2.0f;

    [Header("Visual")]
    public Color electricColor = new Color(0.25f, 0.90f, 1.0f, 0.95f); // Ciano Elétrico Neon
    public Color coreColor = new Color(1.0f, 1.0f, 0.90f, 1.0f);

    private float elapsed = 0f;
    private LineRenderer ringLR;
    private LineRenderer innerRingLR;
    private ParticleSystem sparksPS;
    private Light groundLight;
    private Material hazardMat;
    private bool hasTriggeredSound = false;

    public static ElectricGroundHazard Spawn(Vector3 position, float radius = 1.8f, float duration = 2.0f, int damage = 5)
    {
        // Garante que a poça fique rente ao chão físico
        Vector3 spawnPos = position;
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point;
        }
        spawnPos.y += 0.04f; // Leve offset para evitar Z-fighting com o chão

        GameObject go = new GameObject("ElectricGroundField");
        go.transform.position = spawnPos;

        ElectricGroundHazard hazard = go.AddComponent<ElectricGroundHazard>();
        hazard.radius = radius;
        hazard.duration = duration;
        hazard.damagePerTick = damage;

        return hazard;
    }

    void Start()
    {
        // 1. Collider Trigger para detecção do jogador
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;
        col.center = new Vector3(0f, 0.5f, 0f);

        // Rigidbody cinemático para garantir eventos de trigger na Unity
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 2. Material aditivo com glow
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        hazardMat = new Material(shader);
        if (hazardMat.HasProperty("_Surface")) hazardMat.SetFloat("_Surface", 1); // Transparent
        if (hazardMat.HasProperty("_Blend")) hazardMat.SetFloat("_Blend", 0);     // Additive
        hazardMat.color = electricColor;

        // 3. Constrói os anéis de energia e partículas
        BuildVisuals();

        // 4. Se o jogador já estiver dentro no momento do spawn, aplica o efeito imediatamente
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var h in hits)
        {
            CheckAndShock(h);
        }

        Destroy(gameObject, duration);
    }

    void BuildVisuals()
    {
        // Anel Externo
        ringLR = CreateRingObject("OuterRing", radius, 0.10f, electricColor);

        // Anel Interno (Núcleo incandescente)
        innerRingLR = CreateRingObject("InnerRing", radius * 0.55f, 0.08f, coreColor);

        // Partículas de centelhas elétricas no solo
        BuildSparks();

        // Luz estática suave no chão
        GameObject lightObj = new GameObject("HazardLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        groundLight = lightObj.AddComponent<Light>();
        groundLight.type = LightType.Point;
        groundLight.range = radius * 2.2f;
        groundLight.intensity = 4f;
        groundLight.color = electricColor;
    }

    LineRenderer CreateRingObject(string name, float r, float width, Color col)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = Vector3.zero;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = hazardMat;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = col;
        lr.endColor = col;

        int segments = 28;
        lr.positionCount = segments;
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = (i * step) * Mathf.Deg2Rad;
            lr.SetPosition(i, new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r));
        }

        return lr;
    }

    void BuildSparks()
    {
        GameObject psObj = new GameObject("GroundSparks");
        psObj.transform.SetParent(transform, false);
        psObj.transform.localPosition = Vector3.zero;

        sparksPS = psObj.AddComponent<ParticleSystem>();

        var main = sparksPS.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = sparksPS.emission;
        emission.enabled = true;
        emission.rateOverTime = 25f;

        var shape = sparksPS.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.9f;

        var colorOverLifetime = sparksPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(electricColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = psObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = hazardMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        sparksPS.Play();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);

        // Fade-out suave no final dos 2 segundos
        float alpha = 1f;
        if (progress > 0.65f)
        {
            alpha = (1f - progress) / 0.35f;
        }

        // Tremor elétrico orgânico no anel do chão (jitter de voltagem)
        float jitter = (Mathf.Sin(Time.time * 30f) + Mathf.Cos(Time.time * 45f)) * 0.04f;
        float currentRadius = radius + jitter;

        if (ringLR != null)
        {
            UpdateRingPositions(ringLR, currentRadius);
            Color c = electricColor;
            c.a = alpha * 0.9f;
            ringLR.startColor = c;
            ringLR.endColor = c;
        }

        if (innerRingLR != null)
        {
            UpdateRingPositions(innerRingLR, (radius * 0.55f) - jitter);
            Color c = coreColor;
            c.a = alpha * 0.8f;
            innerRingLR.startColor = c;
            innerRingLR.endColor = c;
        }

        if (groundLight != null)
        {
            groundLight.intensity = Mathf.Lerp(4f, 0f, progress);
        }
    }

    void UpdateRingPositions(LineRenderer lr, float r)
    {
        int count = lr.positionCount;
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float rad = (i * step) * Mathf.Deg2Rad;
            // Ruído sutil no contorno para efeito de choque
            float noise = Mathf.PerlinNoise(i * 0.4f, elapsed * 10f) * 0.08f;
            float actualR = r + noise;
            lr.SetPosition(i, new Vector3(Mathf.Cos(rad) * actualR, 0f, Mathf.Sin(rad) * actualR));
        }
    }

    void OnTriggerEnter(Collider other)
    {
        CheckAndShock(other);
    }

    void OnTriggerStay(Collider other)
    {
        CheckAndShock(other);
    }

    void CheckAndShock(Collider other)
    {
        if (other == null) return;

        if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
        {
            GameObject playerObj = other.CompareTag("Player") ? other.gameObject : other.transform.root.gameObject;

            // Aplica o status de Eletrocutado idêntico ao da Star (dano + slow 50% por 2s)
            ElectrocutedStatus.ApplyElectrocuted(playerObj, damagePerTick, slowPercent, statusDuration);
            ElectricShockVFX.AttachToPlayer(playerObj, 0.5f);

            if (!hasTriggeredSound)
            {
                hasTriggeredSound = true;
                // Toca som de choque se disponível
                AudioClip zap = Resources.Load<AudioClip>("SoundFX/MagicStone/magicStoneZap");
                if (zap != null)
                {
                    AudioSource.PlayClipAtPoint(zap, transform.position, 0.7f);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (hazardMat != null)
        {
            Destroy(hazardMat);
        }
    }
}
