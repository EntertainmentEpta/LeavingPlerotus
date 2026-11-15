using UnityEngine;

/// <summary>
/// Variante 1 (Longa Distância — PDF Página 5): Cristal Desacoplante Elétrico.
/// Inspirado diretamente na Istar (ShardSwarm_AI.cs, ElectricTrailVFX.cs e ElectricShockVFX.cs).
/// Quando o bumerangue é arremessado:
/// 1. As pontas de cristal (piramide_superior / piramide_inferior) se desacoplam radialmente
///    da base da lâmina conforme o multiplicador de área.
/// 2. Feixes de eletricidade (LineRenderer com jitter de 7 pontos) conectam a base às pontas
///    desacopladas e formam arcos elétricos circulares ao girar.
/// 3. Deixa trilhas elétricas no ar (ElectricTrailVFX) e aplica paralisia/choque (ElectricShockVFX + ElectrocutedStatus).
/// </summary>
public class BoomerangElectricCrystalVFX : MonoBehaviour
{
    [Header("Configuração do Cristal Desacoplante (Estilo Istar)")]
    [Tooltip("Multiplicador base de expansão radial das pontas de cristal quando desacopladas.")]
    public float decoupleExpansionMultiplier = 2.15f;

    [Tooltip("Velocidade de transição do desacoplamento/reacoplamento.")]
    public float decoupleLerpSpeed = 12f;

    [Tooltip("Amplitude de oscilação (pulsação magnética) dos cristais enquanto desacoplados.")]
    public float magneticPulseAmplitude = 0.08f;

    [Header("Feixes de Eletricidade (LineRenderer)")]
    [Tooltip("Largura dos feixes elétricos que prendem o cristal desacoplado à base.")]
    public float tetherBeamWidth = 0.065f;

    [Tooltip("Largura dos arcos elétricos externos entre as pontas giratórias.")]
    public float outerArcWidth = 0.045f;

    [Tooltip("Amplitude do jitter elétrico (zigue-zague dos raios).")]
    public float electricJitterAmount = 0.07f;

    [Tooltip("Cor principal ciano elétrico (mesma paleta de ElectricTrailVFX).")]
    public Color electricCyanColor = new Color(0.2f, 0.92f, 1.0f, 0.95f);

    [Tooltip("Cor secundária amarelo elétrico (mesma paleta de ElectricShockVFX).")]
    public Color electricYellowColor = new Color(1.0f, 0.88f, 0.32f, 0.95f);

    [Header("Trilha Elétrica no Ar & Paralisia")]
    [Tooltip("Spawna segmentos de ElectricTrailVFX no ar conforme as pontas giram?")]
    public bool spawnAirElectricTrailSegments = true;

    [Tooltip("Intervalo em segundos entre segmentos de ElectricTrailVFX.")]
    public float trailSegmentInterval = 0.035f;

    [Tooltip("Duração da paralisia/choque nos inimigos atingidos pelo feixe elétrico.")]
    public float paralysisDuration = 1.6f;

    private Transform upperCrystalPivot;
    private Transform lowerCrystalPivot;
    private Transform upperBladeBase;
    private Transform lowerBladeBase;

    private Vector3 upperInitialLocalPos;
    private Vector3 lowerInitialLocalPos;
    private bool initialPositionsCached;

    private float currentDecoupleBlend; // 0 = acoplado, 1 = desacoplado
    private float targetDecoupleBlend;
    private float areaStatMultiplier = 1f;

    // LineRenderers dos feixes de eletricidade
    private GameObject beamsContainer;
    private LineRenderer upperTetherLR;
    private LineRenderer lowerTetherLR;
    private LineRenderer outerArc1LR;
    private LineRenderer outerArc2LR;
    private Light electricCoreLight;
    private Material sharedElectricMat;

    private Vector3 prevUpperWorldPos;
    private Vector3 prevLowerWorldPos;
    private bool hasPrevWorldPos;
    private float trailTimer;

    private const int TETHER_POINTS = 7;
    private const int OUTER_ARC_POINTS = 15;

    public bool IsDecoupled => currentDecoupleBlend > 0.05f;

    public void Initialize(
        Transform upperCrystal,
        Transform lowerCrystal,
        Transform upperBase,
        Transform lowerBase)
    {
        upperCrystalPivot = (upperCrystal != null && upperCrystal.IsChildOf(transform)) ? upperCrystal : null;
        lowerCrystalPivot = (lowerCrystal != null && lowerCrystal.IsChildOf(transform)) ? lowerCrystal : null;
        upperBladeBase = (upperBase != null && upperBase.IsChildOf(transform)) ? upperBase : null;
        lowerBladeBase = (lowerBase != null && lowerBase.IsChildOf(transform)) ? lowerBase : null;

        if (upperCrystalPivot != null && !initialPositionsCached)
        {
            upperInitialLocalPos = upperCrystalPivot.localPosition;
        }
        if (lowerCrystalPivot != null && !initialPositionsCached)
        {
            lowerInitialLocalPos = lowerCrystalPivot.localPosition;
        }
        initialPositionsCached = (upperCrystalPivot != null && lowerCrystalPivot != null);

        EnsureElectricBeams();
        SetBeamsVisible(false);
    }

    private void EnsureElectricBeams()
    {
        if (beamsContainer != null) return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            sharedElectricMat = new Material(shader);

        beamsContainer = new GameObject("ElectricCrystal_BeamsVFX");
        beamsContainer.transform.SetParent(transform, false);
        beamsContainer.transform.localPosition = Vector3.zero;

        upperTetherLR = CreateElectricLineRenderer("UpperCrystal_TetherBeam", TETHER_POINTS, tetherBeamWidth, electricCyanColor, electricYellowColor);
        lowerTetherLR = CreateElectricLineRenderer("LowerCrystal_TetherBeam", TETHER_POINTS, tetherBeamWidth, electricCyanColor, electricYellowColor);
        outerArc1LR = CreateElectricLineRenderer("OuterElectricArc_1", OUTER_ARC_POINTS, outerArcWidth, electricYellowColor, electricCyanColor);
        outerArc2LR = CreateElectricLineRenderer("OuterElectricArc_2", OUTER_ARC_POINTS, outerArcWidth, electricCyanColor, electricYellowColor);

        GameObject lightGO = new GameObject("ElectricCrystal_Light");
        lightGO.transform.SetParent(beamsContainer.transform, false);
        electricCoreLight = lightGO.AddComponent<Light>();
        electricCoreLight.type = LightType.Point;
        electricCoreLight.color = electricCyanColor;
        electricCoreLight.intensity = 3.2f;
        electricCoreLight.range = 4.0f;
        electricCoreLight.shadows = LightShadows.None;
    }

    private LineRenderer CreateElectricLineRenderer(string name, int pointCount, float width, Color startColor, Color endColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(beamsContainer.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = pointCount;
        lr.startWidth = width;
        lr.endWidth = width * 0.8f;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (sharedElectricMat != null)
            lr.material = sharedElectricMat;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(endColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(1.0f, 0.5f),
                new GradientAlphaKey(0.95f, 1f)
            }
        );
        lr.colorGradient = grad;

        return lr;
    }

    /// <summary>
    /// Ativa ou desativa o desacoplamento elétrico das pontas de cristal.
    /// </summary>
    public void SetDecoupledState(bool decoupled, float currentAreaMultiplier = 1f)
    {
        EnsureElectricBeams();
        targetDecoupleBlend = decoupled ? 1f : 0f;
        areaStatMultiplier = Mathf.Max(0.8f, currentAreaMultiplier);

        if (decoupled)
        {
            hasPrevWorldPos = false;
            SetBeamsVisible(true);
        }
    }

    /// <summary>
    /// Força o reacoplamento imediato (ex: quando guardado nas costas ou na mão).
    /// </summary>
    public void SnapCoupledImmediate()
    {
        targetDecoupleBlend = 0f;
        currentDecoupleBlend = 0f;
        hasPrevWorldPos = false;

        if (initialPositionsCached)
        {
            if (upperCrystalPivot != null) upperCrystalPivot.localPosition = upperInitialLocalPos;
            if (lowerCrystalPivot != null) lowerCrystalPivot.localPosition = lowerInitialLocalPos;
        }

        SetBeamsVisible(false);
    }

    public void UpdateElectricCrystals(float deltaTime)
    {
        if (!initialPositionsCached) return;

        currentDecoupleBlend = Mathf.MoveTowards(currentDecoupleBlend, targetDecoupleBlend, decoupleLerpSpeed * deltaTime);

        float pulse = Mathf.Sin(Time.time * 28f) * magneticPulseAmplitude * currentDecoupleBlend;
        float effectiveExpansion = Mathf.Lerp(1f, (decoupleExpansionMultiplier * areaStatMultiplier) + pulse, currentDecoupleBlend);

        if (upperCrystalPivot != null)
            upperCrystalPivot.localPosition = upperInitialLocalPos * effectiveExpansion;

        if (lowerCrystalPivot != null)
            lowerCrystalPivot.localPosition = lowerInitialLocalPos * effectiveExpansion;

        bool showBeams = currentDecoupleBlend > 0.03f;
        SetBeamsVisible(showBeams);

        if (!showBeams) return;

        UpdateElectricLineRenderers();
        UpdateAirElectricTrails(deltaTime);
    }

    private void SetBeamsVisible(bool visible)
    {
        if (beamsContainer != null && beamsContainer.activeSelf != visible)
            beamsContainer.SetActive(visible);
    }

    private void UpdateElectricLineRenderers()
    {
        if (upperCrystalPivot == null || lowerCrystalPivot == null) return;

        Vector3 centerPos = transform.position;
        Vector3 upperTipPos = upperCrystalPivot.position;
        Vector3 lowerTipPos = lowerCrystalPivot.position;

        // Pontos de ancoragem na base da lâmina (ou centro caso não haja sub-mesh separada)
        Vector3 upperBasePos = Vector3.Lerp(centerPos, upperTipPos, 0.32f);
        Vector3 lowerBasePos = Vector3.Lerp(centerPos, lowerTipPos, 0.32f);

        // 1. Feixes retos em zigue-zague conectando a base até o cristal desacoplado (estilo ElectricShockVFX)
        DrawJitterTether(upperTetherLR, upperBasePos, upperTipPos, electricJitterAmount);
        DrawJitterTether(lowerTetherLR, lowerBasePos, lowerTipPos, electricJitterAmount);

        // 2. Arcos elétricos curvos externos ligando uma ponta de cristal à outra (estilo Istar)
        float radius = Vector3.Distance(centerPos, upperTipPos);
        Vector3 dirUpper = (upperTipPos - centerPos).normalized;
        if (dirUpper.sqrMagnitude < 0.001f) dirUpper = transform.right;
        Vector3 perp = Vector3.Cross(Vector3.up, dirUpper).normalized;
        if (perp.sqrMagnitude < 0.001f) perp = transform.forward;

        DrawCurvedElectricArc(outerArc1LR, centerPos, dirUpper, perp, radius, electricJitterAmount * 1.25f);
        DrawCurvedElectricArc(outerArc2LR, centerPos, dirUpper, -perp, radius, electricJitterAmount * 1.25f);

        if (electricCoreLight != null)
        {
            electricCoreLight.intensity = Random.Range(2.4f, 4.5f) * currentDecoupleBlend;
            electricCoreLight.color = (Random.value > 0.3f) ? electricCyanColor : electricYellowColor;
        }
    }

    private void DrawJitterTether(LineRenderer lr, Vector3 start, Vector3 end, float jitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        Vector3 dir = end - start;
        Vector3 perp1 = Vector3.Cross(dir.normalized, Vector3.up);
        if (perp1.sqrMagnitude < 0.001f) perp1 = Vector3.right;
        perp1.Normalize();

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            if (i > 0 && i < count - 1)
            {
                p += perp1 * Random.Range(-jitter, jitter);
                p.y += Random.Range(-jitter * 0.6f, jitter * 0.6f);
            }
            lr.SetPosition(i, p);
        }
    }

    private void DrawCurvedElectricArc(LineRenderer lr, Vector3 center, Vector3 dirUpper, Vector3 perp, float radius, float jitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = t * Mathf.PI; // 0 até 180 graus (de uma ponta de cristal até a outra)
            Vector3 radialDir = (dirUpper * Mathf.Cos(angle)) + (perp * Mathf.Sin(angle));
            Vector3 p = center + radialDir * radius;

            if (i > 0 && i < count - 1)
            {
                p += radialDir * Random.Range(-jitter, jitter);
                p.y += Random.Range(-jitter * 0.5f, jitter * 0.5f);
            }
            lr.SetPosition(i, p);
        }
    }

    private void UpdateAirElectricTrails(float deltaTime)
    {
        if (!spawnAirElectricTrailSegments || upperCrystalPivot == null || lowerCrystalPivot == null)
            return;

        Vector3 curUpper = upperCrystalPivot.position;
        Vector3 curLower = lowerCrystalPivot.position;

        if (!hasPrevWorldPos)
        {
            prevUpperWorldPos = curUpper;
            prevLowerWorldPos = curLower;
            hasPrevWorldPos = true;
            return;
        }

        trailTimer += deltaTime;
        if (trailTimer >= trailSegmentInterval)
        {
            trailTimer = 0f;
            if (Vector3.Distance(prevUpperWorldPos, curUpper) > 0.05f)
            {
                ElectricTrailVFX.CreateTrailSegment(prevUpperWorldPos, curUpper, 3, 0.35f, hostileToPlayer: false);
            }
            if (Vector3.Distance(prevLowerWorldPos, curLower) > 0.05f)
            {
                ElectricTrailVFX.CreateTrailSegment(prevLowerWorldPos, curLower, 3, 0.35f, hostileToPlayer: false);
            }
            prevUpperWorldPos = curUpper;
            prevLowerWorldPos = curLower;
        }
    }

    /// <summary>
    /// Aplica o efeito visual de choque elétrico (ElectricShockVFX) e paralisia (ElectrocutedStatus / StunVFX)
    /// em um inimigo atingido pelo Cristal Desacoplante Elétrico.
    /// </summary>
    public void TriggerElectricCrystalHit(GameObject targetEnemy, Vector3 hitPoint)
    {
        if (targetEnemy == null) return;

        // Dispara arco elétrico visual entre o centro do bumerangue e o inimigo (estilo Istar)
        ElectricTrailVFX.CreateTrailSegment(transform.position, targetEnemy.transform.position, 3, 0.4f, hostileToPlayer: false);
        ElectricShockVFX.AttachToPlayer(targetEnemy, paralysisDuration);

        // Aplica status de choque/paralisia visual no inimigo
        ElectrocutedStatus.ApplyElectrocuted(targetEnemy, 3, 0.50f, paralysisDuration);

        DummyHealth dh = targetEnemy.GetComponentInParent<DummyHealth>();
        if (dh != null)
        {
            dh.ApplySlow(0.85f, paralysisDuration);
        }

        StunVFX stun = targetEnemy.GetComponent<StunVFX>() ?? targetEnemy.AddComponent<StunVFX>();
        if (stun != null)
        {
            stun.ShowStun(paralysisDuration);
        }
    }

    /// <summary>
    /// Retorna o raio de colisão expandido atual considerando o desacoplamento dos cristais.
    /// </summary>
    public float GetExpandedHitRadius(float baseRadius = 0.65f)
    {
        if (upperCrystalPivot != null)
        {
            float dist = Vector3.Distance(transform.position, upperCrystalPivot.position);
            if (dist > 0.1f) return dist;
        }
        return baseRadius * Mathf.Lerp(1f, decoupleExpansionMultiplier * areaStatMultiplier, currentDecoupleBlend);
    }

    private void OnDestroy()
    {
        if (sharedElectricMat != null)
            Destroy(sharedElectricMat);
    }
}
