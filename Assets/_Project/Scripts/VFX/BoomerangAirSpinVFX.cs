using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla o visual do Bumerangue girando em alta rotação no ar (Estados Throw e Returning).
/// Utiliza LineRenderers procedurais (inspirado na Istar / ShardSwarm_AI e ElectricTrailVFX)
/// para criar os anéis circulares de velocidade e rastros de lâmina das páginas 1 a 3 do PDF.
/// </summary>
public class BoomerangAirSpinVFX : MonoBehaviour
{
    [Header("Configuração de Rotação no Ar")]
    [Tooltip("Velocidade de giro da lâmina no ar em graus por segundo.")]
    public float airSpinSpeed = 1800f;

    [Tooltip("Velocidade de giro ao carregar na mão (feedback visual de carga).")]
    public float chargeSpinSpeedMax = 720f;

    [Tooltip("Eixo local de rotação da lâmina no ar.")]
    public Vector3 localSpinAxis = new Vector3(0f, 0f, 1f);

    [Tooltip("Inclinação do plano de giro no ar (mantém o bumerangue girando horizontalmente em relação ao solo).")]
    public Vector3 airPlaneTiltEuler = new Vector3(90f, 0f, 0f);

    [Header("Anéis de Velocidade (LineRenderer estilo Istar)")]
    [Tooltip("Raio base do disco de vento/energia ao girar no ar.")]
    public float spinDiscRadius = 0.72f;

    [Tooltip("Largura das linhas circulares de alta rotação.")]
    public float ringLineWidth = 0.055f;

    [Tooltip("Cor principal ciano neon das linhas de giro.")]
    public Color primarySpinColor = new Color(0.2f, 0.92f, 1.0f, 0.85f);

    [Tooltip("Cor secundária branca brilhante para o anel interno de corte.")]
    public Color innerCoreSpinColor = new Color(0.9f, 0.98f, 1.0f, 0.9f);

    [Header("Rastros das Pontas (TrailRenderer)")]
    public float tipTrailTime = 0.14f;
    public float tipTrailWidth = 0.08f;

    private Transform visualSpinTarget;
    private Transform upperTipTransform;
    private Transform lowerTipTransform;

    private LineRenderer outerSpinRingLR;
    private LineRenderer innerSpinRingLR;
    private LineRenderer motionArcLR1;
    private LineRenderer motionArcLR2;

    private TrailRenderer upperTipTrail;
    private TrailRenderer lowerTipTrail;
    private Light airSpinLight;

    private GameObject vfxContainer;
    private Material sharedAdditiveMat;

    private bool isSpinningInAir;
    private float currentSpinAngle;
    private float currentRadiusMultiplier = 1f;

    private const int RING_SEGMENTS = 36;
    private const int ARC_SEGMENTS = 18;

    public void Initialize(Transform visualRoot, Transform upperTip, Transform lowerTip)
    {
        visualSpinTarget = (visualRoot != null && visualRoot.IsChildOf(transform)) ? visualRoot : transform;
        upperTipTransform = (upperTip != null && upperTip.IsChildOf(transform)) ? upperTip : transform;
        lowerTipTransform = (lowerTip != null && lowerTip.IsChildOf(transform)) ? lowerTip : transform;

        EnsureVFXObjects();
        SetAirSpinActive(false);
    }

    private void EnsureVFXObjects()
    {
        if (vfxContainer != null) return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            sharedAdditiveMat = new Material(shader);
        }

        vfxContainer = new GameObject("BoomerangAirSpin_VFX");
        vfxContainer.transform.SetParent(transform, false);
        vfxContainer.transform.localPosition = Vector3.zero;
        vfxContainer.transform.localRotation = Quaternion.identity;

        outerSpinRingLR = CreateRingLineRenderer("OuterSpinRing_LR", RING_SEGMENTS, true, ringLineWidth, primarySpinColor);
        innerSpinRingLR = CreateRingLineRenderer("InnerSpinRing_LR", RING_SEGMENTS, true, ringLineWidth * 0.65f, innerCoreSpinColor);
        motionArcLR1 = CreateRingLineRenderer("MotionArc1_LR", ARC_SEGMENTS, false, ringLineWidth * 0.8f, innerCoreSpinColor);
        motionArcLR2 = CreateRingLineRenderer("MotionArc2_LR", ARC_SEGMENTS, false, ringLineWidth * 0.8f, primarySpinColor);

        if (upperTipTransform != null)
        {
            upperTipTrail = CreateTipTrail(upperTipTransform, "UpperTip_AirTrail");
        }
        if (lowerTipTransform != null)
        {
            lowerTipTrail = CreateTipTrail(lowerTipTransform, "LowerTip_AirTrail");
        }

        GameObject lightGO = new GameObject("AirSpin_Light");
        lightGO.transform.SetParent(vfxContainer.transform, false);
        airSpinLight = lightGO.AddComponent<Light>();
        airSpinLight.type = LightType.Point;
        airSpinLight.color = new Color(0.2f, 0.9f, 1.0f);
        airSpinLight.intensity = 2.2f;
        airSpinLight.range = 3.2f;
        airSpinLight.shadows = LightShadows.None;
    }

    private LineRenderer CreateRingLineRenderer(string objName, int segments, bool loop, float width, Color color)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(vfxContainer.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = segments;
        lr.startWidth = width;
        lr.endWidth = width * (loop ? 1f : 0.2f);
        lr.numCapVertices = 3;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (sharedAdditiveMat != null)
            lr.material = sharedAdditiveMat;

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

    private TrailRenderer CreateTipTrail(Transform tipParent, string trailName)
    {
        GameObject go = new GameObject(trailName);
        go.transform.SetParent(tipParent, false);
        go.transform.localPosition = Vector3.zero;

        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.time = tipTrailTime;
        tr.startWidth = tipTrailWidth;
        tr.endWidth = 0f;
        tr.minVertexDistance = 0.02f;
        tr.autodestruct = false;
        tr.emitting = false;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        if (sharedAdditiveMat != null)
            tr.material = sharedAdditiveMat;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(primarySpinColor, 0.4f),
                new GradientColorKey(new Color(0.1f, 0.6f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.45f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        tr.colorGradient = grad;

        return tr;
    }

    public void SetAirSpinActive(bool active)
    {
        EnsureVFXObjects();
        isSpinningInAir = active;

        if (vfxContainer != null)
            vfxContainer.SetActive(active);

        if (upperTipTrail != null)
        {
            upperTipTrail.Clear();
            upperTipTrail.emitting = active;
        }

        if (lowerTipTrail != null)
        {
            lowerTipTrail.Clear();
            lowerTipTrail.emitting = active;
        }
    }

    public void SetRadiusMultiplier(float multiplier)
    {
        currentRadiusMultiplier = Mathf.Max(0.5f, multiplier);
    }

    /// <summary>
    /// Atualiza a rotação em alta velocidade e os anéis de LineRenderer no ar.
    /// </summary>
    public void UpdateAirSpin(float deltaTime, float speedMultiplier = 1f)
    {
        if (!isSpinningInAir) return;

        float effectiveSpeed = airSpinSpeed * Mathf.Max(0.2f, speedMultiplier);
        currentSpinAngle = (currentSpinAngle + effectiveSpeed * deltaTime) % 360f;

        if (visualSpinTarget != null)
        {
            Quaternion tilt = Quaternion.Euler(airPlaneTiltEuler);
            Quaternion spin = Quaternion.AngleAxis(currentSpinAngle, localSpinAxis.normalized);
            visualSpinTarget.localRotation = tilt * spin;
        }

        UpdateSpinRings();
    }

    /// <summary>
    /// Feedback sutil na mão do astronauta durante o estado Charging.
    /// </summary>
    public void UpdateChargeVisual(float charge01, float deltaTime)
    {
        if (airSpinLight != null)
        {
            airSpinLight.intensity = Mathf.Lerp(0.8f, 3.0f, charge01);
        }
    }

    private void UpdateSpinRings()
    {
        if (vfxContainer == null) return;

        float dynamicRadius = spinDiscRadius * currentRadiusMultiplier;
        if (upperTipTransform != null)
        {
            float dist = Vector3.Distance(transform.position, upperTipTransform.position);
            if (dist > 0.1f)
                dynamicRadius = Mathf.Max(dynamicRadius, dist * 1.02f);
        }

        float time = Time.time;
        float jitterOuter = 1f + Mathf.Sin(time * 38f) * 0.04f;
        DrawCircle(outerSpinRingLR, dynamicRadius * jitterOuter, 0.02f);
        DrawCircle(innerSpinRingLR, dynamicRadius * 0.68f, 0.015f);

        float arcBaseAngle = -currentSpinAngle * Mathf.Deg2Rad;
        DrawArc(motionArcLR1, dynamicRadius * 0.92f, arcBaseAngle, arcBaseAngle + Mathf.PI * 0.65f, 0.025f);
        DrawArc(motionArcLR2, dynamicRadius * 0.92f, arcBaseAngle + Mathf.PI, arcBaseAngle + Mathf.PI * 1.65f, 0.025f);
    }

    private void DrawCircle(LineRenderer lr, float radius, float verticalJitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count * Mathf.PI * 2f;
            float y = Random.Range(-verticalJitter, verticalJitter);
            lr.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, y, Mathf.Sin(t) * radius));
        }
    }

    private void DrawArc(LineRenderer lr, float radius, float startRad, float endRad, float verticalJitter)
    {
        if (lr == null) return;
        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float angle = Mathf.Lerp(startRad, endRad, t);
            float y = (i == 0 || i == count - 1) ? 0f : Random.Range(-verticalJitter, verticalJitter);
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
        }
    }

    private void OnDestroy()
    {
        if (sharedAdditiveMat != null)
            Destroy(sharedAdditiveMat);
    }
}
