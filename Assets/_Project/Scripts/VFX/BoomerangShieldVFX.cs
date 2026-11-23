using UnityEngine;

/// <summary>
/// Efeito visual do Escudo Giratório Defensivo ao redor do corpo do player (Estado SpinningShield — PDF Páginas 7 e 8).
/// Inclui:
/// 1. Giro de altíssima rotação da lâmina formando uma barreira circular de energia.
/// 2. Anéis concêntricos de LineRenderer (vórtice ciano/branco) ao redor do escudo.
/// 3. Destaque visual para a Janela de Parry nos primeiros 0.2s (brilho dourado/ciano + pulso de reflexão).
/// 4. Arco elétrico de Paralisia para o upgrade Melee (PDF Página 8).
/// </summary>
public class BoomerangShieldVFX : MonoBehaviour
{
    [Header("Posicionamento e Giro do Escudo")]
    [Tooltip("Offset local do escudo em relação ao peito/centro do astronauta.")]
    public Vector3 shieldCenterOffset = new Vector3(0f, 1.08f, 0.62f);

    [Tooltip("Inclinação do disco do escudo giratório (em frente ao corpo do player, conforme PDF pág. 7).")]
    public Vector3 shieldPlaneEuler = new Vector3(15f, 0f, 0f);

    [Tooltip("Se verdadeiro, além de girar em alta velocidade, orbita ao redor do corpo do player.")]
    public bool orbitAroundPlayerBody = false;

    [Tooltip("Raio da órbita ao redor do corpo caso orbitAroundPlayerBody esteja ativo.")]
    public float bodyOrbitRadius = 0.75f;

    [Tooltip("Velocidade de órbita ao redor do corpo (graus/seg).")]
    public float bodyOrbitSpeed = 540f;

    [Tooltip("Velocidade de rotação própria do bumerangue no modo escudo (graus/seg).")]
    public float shieldBladeSpinSpeed = 2500f;

    [Header("Anéis do Escudo (LineRenderer)")]
    [Tooltip("Raio visual da barreira circular do escudo.")]
    public float shieldRadius = 0.85f;

    [Tooltip("Cor principal ciano da barreira defensiva.")]
    public Color shieldPrimaryColor = new Color(0.15f, 0.92f, 1.0f, 0.9f);

    [Tooltip("Cor interna branca de alta rotação.")]
    public Color shieldCoreColor = new Color(0.92f, 0.98f, 1.0f, 0.95f);

    [Tooltip("Cor do flash durante a janela de Parry (primeiros 0.2s).")]
    public Color parryWindowColor = new Color(1.0f, 0.92f, 0.35f, 1.0f);

    [Header("Upgrade Melee — Paralisia Elétrica (PDF Página 8)")]
    [Tooltip("Ativa os raios elétricos de paralisia ao redor do escudo giratório.")]
    public bool enableMeleeParalysisUpgrade = false;

    [Tooltip("Duração do stun/paralisia nos monstros que encostam no escudo.")]
    public float shieldParalysisDuration = 1.8f;

    private Transform visualSpinTarget;
    private Transform shieldAnchorTransform;

    private GameObject shieldVFXRoot;
    private LineRenderer outerBarrierLR;
    private LineRenderer innerVortexLR1;
    private LineRenderer innerVortexLR2;
    private LineRenderer bodyAuraRingLR;
    private LineRenderer parryFlashRingLR;
    private LineRenderer paralysisBoltLR;
    private Light shieldLight;
    private Material sharedShieldMat;

    private bool isShieldActive;
    private float bladeSpinAngle;
    private float orbitAngle;
    private float parryFlashTimer;

    private const int CIRCLE_SEGMENTS = 40;
    private const int VORTEX_ARC_SEGMENTS = 20;
    private const int BOLT_SEGMENTS = 12;

    public void Initialize(Transform visualRoot)
    {
        visualSpinTarget = (visualRoot != null && visualRoot.IsChildOf(transform)) ? visualRoot : transform;
        EnsureShieldVFXObjects();
        SetShieldActive(false);
    }

    private void EnsureShieldVFXObjects()
    {
        if (shieldVFXRoot != null) return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            sharedShieldMat = new Material(shader);

        shieldVFXRoot = new GameObject("BoomerangShield_VFX");
        shieldVFXRoot.transform.SetParent(transform, false);
        shieldVFXRoot.transform.localPosition = Vector3.zero;
        shieldVFXRoot.transform.localRotation = Quaternion.identity;

        outerBarrierLR = CreateShieldLine("OuterBarrier_LR", CIRCLE_SEGMENTS, true, 0.065f, shieldPrimaryColor);
        innerVortexLR1 = CreateShieldLine("InnerVortex1_LR", VORTEX_ARC_SEGMENTS, false, 0.05f, shieldCoreColor);
        innerVortexLR2 = CreateShieldLine("InnerVortex2_LR", VORTEX_ARC_SEGMENTS, false, 0.05f, shieldPrimaryColor);
        bodyAuraRingLR = CreateShieldLine("BodyDefenseAura_LR", CIRCLE_SEGMENTS, true, 0.045f, new Color(0.2f, 0.85f, 1f, 0.55f));
        parryFlashRingLR = CreateShieldLine("ParryWindowFlash_LR", CIRCLE_SEGMENTS, true, 0.09f, parryWindowColor);
        paralysisBoltLR = CreateShieldLine("MeleeParalysisBolt_LR", BOLT_SEGMENTS, true, 0.04f, new Color(1f, 0.9f, 0.3f, 0.95f));

        GameObject lightGO = new GameObject("Shield_Light");
        lightGO.transform.SetParent(shieldVFXRoot.transform, false);
        shieldLight = lightGO.AddComponent<Light>();
        shieldLight.type = LightType.Point;
        shieldLight.color = shieldPrimaryColor;
        shieldLight.intensity = 3.0f;
        shieldLight.range = 3.8f;
        shieldLight.shadows = LightShadows.None;
    }

    private LineRenderer CreateShieldLine(string name, int segments, bool loop, float width, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(shieldVFXRoot.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = segments;
        lr.startWidth = width;
        lr.endWidth = width * (loop ? 1f : 0.25f);
        lr.numCapVertices = 3;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (sharedShieldMat != null)
            lr.material = sharedShieldMat;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(color, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(loop ? color.a : 0f, 0f),
                new GradientAlphaKey(color.a, 0.5f),
                new GradientAlphaKey(loop ? color.a : 0f, 1f)
            }
        );
        lr.colorGradient = grad;
        return lr;
    }

    public void SetShieldActive(bool active, bool triggerParryFlash = true)
    {
        EnsureShieldVFXObjects();
        isShieldActive = active;

        if (shieldVFXRoot != null)
            shieldVFXRoot.SetActive(active);

        if (active && triggerParryFlash)
        {
            parryFlashTimer = 0.2f; // Janela de Parry de 0.2s
        }
    }

    /// <summary>
    /// Atualiza o giro do escudo e os anéis de energia a cada frame durante o estado SpinningShield.
    /// </summary>
    public void UpdateShield(Transform playerTransform, float deltaTime, bool isWithinParryWindow)
    {
        if (!isShieldActive) return;

        if (parryFlashTimer > 0f)
            parryFlashTimer -= deltaTime;

        bool showParryHighlight = isWithinParryWindow || parryFlashTimer > 0f;

        // 1. Posiciona o bumerangue na frente ou orbitando ao redor do corpo do player
        if (playerTransform != null)
        {
            if (orbitAroundPlayerBody)
            {
                orbitAngle = (orbitAngle + bodyOrbitSpeed * deltaTime) % 360f;
                float rad = orbitAngle * Mathf.Deg2Rad;
                Vector3 orbitOffset = new Vector3(Mathf.Cos(rad) * bodyOrbitRadius, shieldCenterOffset.y, Mathf.Sin(rad) * bodyOrbitRadius);
                transform.position = playerTransform.position + orbitOffset;
                Vector3 outward = new Vector3(orbitOffset.x, 0f, orbitOffset.z).normalized;
                if (outward.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            }
            else
            {
                Vector3 worldPos = playerTransform.TransformPoint(shieldCenterOffset);
                transform.position = worldPos;
                transform.rotation = playerTransform.rotation * Quaternion.Euler(shieldPlaneEuler);
            }
        }

        // 2. Rotação em altíssima velocidade da lâmina formando o escudo
        bladeSpinAngle = (bladeSpinAngle + shieldBladeSpinSpeed * deltaTime) % 360f;
        if (visualSpinTarget != null)
        {
            visualSpinTarget.localRotation = Quaternion.Euler(0f, 0f, bladeSpinAngle);
        }

        // 3. Atualiza os anéis concêntricos de LineRenderer no plano XY local do escudo
        float pulse = 1f + Mathf.Sin(Time.time * 45f) * 0.035f;
        float r = shieldRadius * pulse;

        DrawCircleXY(outerBarrierLR, r, 0.015f);

        float vortexRad = bladeSpinAngle * Mathf.Deg2Rad;
        DrawArcXY(innerVortexLR1, r * 0.72f, vortexRad, vortexRad + Mathf.PI * 0.8f, 0.02f);
        DrawArcXY(innerVortexLR2, r * 0.52f, -vortexRad, -vortexRad + Mathf.PI * 0.8f, 0.02f);

        // Anel de proteção no chão/cintura ao redor do corpo do player
        if (bodyAuraRingLR != null && playerTransform != null)
        {
            bodyAuraRingLR.gameObject.SetActive(true);
            DrawBodyAuraRing(playerTransform);
        }

        // 4. Janela de Parry (primeiros 0.2s)
        if (parryFlashRingLR != null)
        {
            parryFlashRingLR.gameObject.SetActive(showParryHighlight);
            if (showParryHighlight)
            {
                float parryRadius = r * (1.08f + Mathf.Sin(Time.time * 70f) * 0.06f);
                DrawCircleXY(parryFlashRingLR, parryRadius, 0.03f);
            }
        }

        // 5. Raios Elétricos do Upgrade Melee (Paralisia — PDF Página 8)
        if (paralysisBoltLR != null)
        {
            paralysisBoltLR.gameObject.SetActive(enableMeleeParalysisUpgrade);
            if (enableMeleeParalysisUpgrade)
            {
                DrawElectricCircleXY(paralysisBoltLR, r * 1.04f, 0.08f);
            }
        }

        if (shieldLight != null)
        {
            shieldLight.color = showParryHighlight ? parryWindowColor : shieldPrimaryColor;
            shieldLight.intensity = showParryHighlight ? 4.5f : 2.8f;
        }
    }

    private void DrawCircleXY(LineRenderer lr, float radius, float jitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)i / count * Mathf.PI * 2f;
            float r = radius + Random.Range(-jitter, jitter);
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
        }
    }

    private void DrawArcXY(LineRenderer lr, float radius, float startAngle, float endAngle, float jitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            float r = radius + ((i > 0 && i < count - 1) ? Random.Range(-jitter, jitter) : 0f);
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f));
        }
    }

    private void DrawElectricCircleXY(LineRenderer lr, float radius, float electricJitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)i / count * Mathf.PI * 2f;
            float r = radius + Random.Range(-electricJitter, electricJitter);
            float z = Random.Range(-electricJitter * 0.5f, electricJitter * 0.5f);
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z));
        }
    }

    private void DrawBodyAuraRing(Transform playerTransform)
    {
        int count = bodyAuraRingLR.positionCount;
        float auraRadius = 0.72f;
        Vector3 playerWorldBase = playerTransform.position + Vector3.up * 0.15f;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)i / count * Mathf.PI * 2f + Time.time * 4f;
            Vector3 worldPt = playerWorldBase + new Vector3(Mathf.Cos(angle) * auraRadius, 0f, Mathf.Sin(angle) * auraRadius);
            bodyAuraRingLR.SetPosition(i, shieldVFXRoot.transform.InverseTransformPoint(worldPt));
        }
    }

    /// <summary>
    /// Disparado quando o Parry de 0.2s reflete um projétil ou atordoa um ataque melee.
    /// </summary>
    public void TriggerParrySuccessVFX(Vector3 contactWorldPoint, GameObject attackerOrProjectile = null)
    {
        parryFlashTimer = 0.25f;
        ElectricTrailVFX.CreateTrailSegment(transform.position, contactWorldPoint, 2, 0.35f, hostileToPlayer: false);

        if (attackerOrProjectile != null) StunEnemy(attackerOrProjectile);
    }

    /// <summary>
    /// Disparado quando um inimigo entra em contato com o Escudo Giratório com o upgrade de Paralisia (PDF Página 8).
    /// </summary>
    public void TriggerShieldMeleeParalysis(GameObject targetEnemy)
    {
        if (targetEnemy == null) return;
        ElectricTrailVFX.CreateTrailSegment(transform.position, targetEnemy.transform.position, 2, 0.35f, hostileToPlayer: false);
        StunEnemy(targetEnemy);
    }

    private void StunEnemy(GameObject target)
    {
        var enemy = target.GetComponentInParent<DummyHealth>();
        if (enemy != null) enemy.ApplyStun(shieldParalysisDuration);
        else
        {
            var swarm = target.GetComponentInParent<ShardSwarmHealth>();
            if (swarm == null) return;
            swarm.ApplyStun(shieldParalysisDuration);
        }
        ElectricShockVFX.AttachToPlayer(target, shieldParalysisDuration);
        var icon = target.GetComponent<StunVFX>() ?? target.AddComponent<StunVFX>();
        icon.ShowStun(shieldParalysisDuration);
    }

    private void OnDestroy()
    {
        if (sharedShieldMat != null)
            Destroy(sharedShieldMat);
    }
}
