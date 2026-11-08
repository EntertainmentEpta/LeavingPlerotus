using UnityEngine;

/// <summary>
/// Campo de Debuff de Velocidade de Bismuto (Neon Speed Drain Zone).
/// Parâmetros de Slow, Duração, Raio e Cores 100% Editáveis no Inspector da Unity.
/// </summary>
public class BismuthCrystalField : MonoBehaviour
{
    [Header("1. Parâmetros de Redução de Velocidade (Slow Debuff)")]
    [Tooltip("Redução de velocidade quando o player tem buff de speed ativo (ex: 0.20 = 20% de slow)")]
    [Range(0.05f, 0.90f)]
    public float baseSlowPercent = 0.20f;

    [Tooltip("Redução de velocidade quando o player NÃO tem buff de speed ativo (ex: 0.40 = 40% de slow)")]
    [Range(0.05f, 0.90f)]
    public float enhancedSlowPercent = 0.40f;

    [Tooltip("Tempo em segundos que o player precisa permanecer na área para ter seu buff de velocidade roubado")]
    public float stealBuffTime = 3.0f;

    [Header("2. Parâmetros do Campo no Chão")]
    [Tooltip("Duração em segundos antes de a área de debuff desaparecer do chão")]
    public float fieldDuration = 3.5f;

    [Tooltip("Raio da área de debuff no chão (em metros)")]
    public float fieldRadius = 4.0f;

    [Header("3. Aparência e Cores (VFX no Inspector)")]
    [Tooltip("Cor do anel rúnico externo e da onda concentrica de drenagem")]
    public Color ringColor = new Color(0.70f, 0.40f, 0.95f, 0.35f);

    [Tooltip("Cor do disco holográfico translúcido no solo")]
    public Color floorDiscColor = new Color(0.55f, 0.25f, 0.85f, 0.10f);

    [Tooltip("Espessura da linha do anel rúnico néon no solo")]
    public float ringLineWidth = 0.05f;

    [Header("Referências")]
    [HideInInspector] public Geobionte_AI ownerBismutado;

    // Estado interno
    private bool playerInside = false;
    private float playerTimeInside = 0f;
    private bool hasStolen = false;
    private PlayerDebuffs playerDebuffs;
    private float lifeTimer = 0f;

    // Visual VFX
    private LineRenderer outerRingLine;
    private LineRenderer innerWaveLine;
    private MeshRenderer floorDiscRenderer;
    private Mesh floorDiscMesh;
    private Material ringMaterial;
    private Material discMaterial;

    void Start()
    {
        SetupCollider();
        CreateFloorDisc();
        CreateNeonRings();
    }

    void SetupCollider()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = fieldRadius;
    }

    /// <summary>
    /// Cria o disco holográfico moderno e sutil no solo com gradiente radial suave (sem blocos ou cilindros).
    /// </summary>
    void CreateFloorDisc()
    {
        GameObject discObj = new GameObject("SoftHolographicFloorDisc");
        discObj.transform.SetParent(transform, false);
        discObj.transform.localPosition = new Vector3(0, 0.02f, 0);

        MeshFilter mf = discObj.AddComponent<MeshFilter>();
        floorDiscRenderer = discObj.AddComponent<MeshRenderer>();

        // Gera mesh circular suave com vertex colors (centro suave, borda 100% transparente)
        int segments = 48;
        floorDiscMesh = new Mesh();
        floorDiscMesh.name = "BismuthDiscMesh";

        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        colors[0] = new Color(floorDiscColor.r, floorDiscColor.g, floorDiscColor.b, floorDiscColor.a);

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            vertices[i + 1] = new Vector3(Mathf.Cos(rad) * fieldRadius, 0f, Mathf.Sin(rad) * fieldRadius);
            colors[i + 1] = new Color(floorDiscColor.r, floorDiscColor.g, floorDiscColor.b, 0f); // Borda suave

            int next = (i + 1) % segments;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        floorDiscMesh.vertices = vertices;
        floorDiscMesh.colors = colors;
        floorDiscMesh.triangles = triangles;
        floorDiscMesh.RecalculateNormals();
        mf.mesh = floorDiscMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            discMaterial = new Material(shader);
            discMaterial.SetColor("_Color", Color.white);
            if (discMaterial.HasProperty("_BaseColor")) discMaterial.SetColor("_BaseColor", Color.white);
            discMaterial.SetFloat("_Surface", 1);
            discMaterial.SetFloat("_Blend", 0);
            discMaterial.renderQueue = 3000;
            floorDiscRenderer.material = discMaterial;
        }
    }

    /// <summary>
    /// Cria o anel néon externo e a onda de energia concêntrica fina e moderna.
    /// </summary>
    void CreateNeonRings()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            ringMaterial = new Material(shader);
            ringMaterial.SetColor("_Color", ringColor);
            if (ringMaterial.HasProperty("_BaseColor")) ringMaterial.SetColor("_BaseColor", ringColor);
            ringMaterial.SetFloat("_Surface", 1);
            ringMaterial.SetFloat("_Blend", 0);
            ringMaterial.renderQueue = 3001;
        }

        // 1. Anel Externo Fino e Sutil
        GameObject outerRingObj = new GameObject("OuterNeonRing");
        outerRingObj.transform.SetParent(transform, false);
        outerRingObj.transform.localPosition = new Vector3(0, 0.03f, 0);

        outerRingLine = outerRingObj.AddComponent<LineRenderer>();
        outerRingLine.startWidth = ringLineWidth;
        outerRingLine.endWidth = ringLineWidth;
        outerRingLine.useWorldSpace = false;
        outerRingLine.loop = true;
        SetupCirclePositions(outerRingLine, fieldRadius, 48);
        if (ringMaterial != null) outerRingLine.material = ringMaterial;

        // 2. Onda Interna Efémera de Drenagem
        GameObject innerWaveObj = new GameObject("InnerDrainWave");
        innerWaveObj.transform.SetParent(transform, false);
        innerWaveObj.transform.localPosition = new Vector3(0, 0.04f, 0);

        innerWaveLine = innerWaveObj.AddComponent<LineRenderer>();
        innerWaveLine.startWidth = ringLineWidth * 0.75f;
        innerWaveLine.endWidth = ringLineWidth * 0.75f;
        innerWaveLine.useWorldSpace = false;
        innerWaveLine.loop = true;
        SetupCirclePositions(innerWaveLine, fieldRadius * 0.5f, 36);
        if (ringMaterial != null) innerWaveLine.material = ringMaterial;
    }

    private void SetupCirclePositions(LineRenderer lr, float radius, int segments)
    {
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, 0, z));
        }
    }

    void Update()
    {
        // Timer de vida do campo
        if (fieldDuration > 0f)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= fieldDuration)
            {
                DestroyField();
                return;
            }
        }

        // Conta tempo que o player está dentro
        if (playerInside && playerDebuffs != null && !hasStolen)
        {
            playerTimeInside += Time.deltaTime;
            if (playerTimeInside >= stealBuffTime)
            {
                AttemptStealBuff();
            }
        }

        // Animação de Drenagem e Pulsação Holográfica
        UpdateHolographicPulse();
    }

    void UpdateHolographicPulse()
    {
        float time = Time.time;
        float pulse = Mathf.Sin(time * 2.5f) * 0.20f + 0.95f;

        // Anima a onda interna encolhendo suavemente em direção ao centro para simular drenagem
        float waveRadiusPct = (time * 0.35f) % 1.0f;
        float currentWaveRadius = Mathf.Lerp(fieldRadius * 0.92f, 0.25f, waveRadiusPct);
        if (innerWaveLine != null)
        {
            SetupCirclePositions(innerWaveLine, currentWaveRadius, 36);
        }

        // Brilho pulsante sutil no anel e disco
        if (ringMaterial != null)
        {
            Color neonPulse = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * pulse);
            ringMaterial.SetColor("_Color", neonPulse);
            if (ringMaterial.HasProperty("_BaseColor")) ringMaterial.SetColor("_BaseColor", neonPulse);
        }

        if (discMaterial != null)
        {
            Color discPulse = new Color(1f, 1f, 1f, pulse * 0.9f);
            discMaterial.SetColor("_Color", discPulse);
            if (discMaterial.HasProperty("_BaseColor")) discMaterial.SetColor("_BaseColor", discPulse);
        }
    }

    private void AttemptStealBuff()
    {
        if (playerDebuffs == null || hasStolen) return;

        bool stole = playerDebuffs.TryStealSpeedBuff(ownerBismutado != null ? ownerBismutado.gameObject : gameObject);
        if (stole)
        {
            hasStolen = true;
            if (ownerBismutado != null)
            {
                ownerBismutado.OnStoleSpeedBuff();
            }
            Debug.Log("💎 [BISMUTH FIELD] Roubou um buff de velocidade do player!");
        }
    }

    private void DestroyField()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
        if (discMaterial != null) Destroy(discMaterial);
        if (floorDiscMesh != null) Destroy(floorDiscMesh);
        Destroy(gameObject);
    }

    // ==================== TRIGGER ====================

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (ownerBismutado != null && other.gameObject == ownerBismutado.gameObject) return;

        playerDebuffs = other.GetComponent<PlayerDebuffs>() ?? other.gameObject.AddComponent<PlayerDebuffs>();

        playerInside = true;
        playerTimeInside = 0f;

        bool playerHasSpeedBuff = playerDebuffs.HasSpeedBuff();
        float slowAmount = playerHasSpeedBuff ? baseSlowPercent : enhancedSlowPercent;
        playerDebuffs.ApplySlow(slowAmount);

        if (ownerBismutado != null)
        {
            ownerBismutado.OnPlayerTrappedInCrystalField();
        }

        Debug.Log($"💎 [BISMUTH FIELD] Player entrou na zona de velocidade! Slow: {slowAmount * 100}%. Bismutado notificado!");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        playerTimeInside = 0f;

        if (playerDebuffs != null)
        {
            playerDebuffs.RemoveSlow();
        }

        Debug.Log("💎 [BISMUTH FIELD] Player saiu da zona! Slow removido.");
    }
}
