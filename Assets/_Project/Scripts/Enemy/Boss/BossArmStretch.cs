using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema Procedural de Estiramento Elástico de Braço (Boss Arm Stretch).
/// Exclusivo para o modelo da Fase 3 do Boss (Neutral Idle / Fase3).
/// Permite calibrar em tempo real no Inspector o Delay de início, a Duração total e a Força do estiramento.
/// </summary>
public class BossArmStretch : MonoBehaviour
{
    [Header("⚙️ Configurações Gerais")]
    [Tooltip("Ativa ou desativa o efeito de braço esticando.")]
    public bool enableStretch = true;

    [Header("Calibracao: Ataque Basico Baixo (bossLowAttack)")]
    [Tooltip("Tempo de espera (em segundos) antes do braço começar a esticar (espera o recuo da pose).")]
    [Range(0.0f, 1.5f)] public float lowAttackStartDelay = 0.5f;

    [Tooltip("Duração total do ciclo elástico do Ataque Baixo.")]
    [Range(0.2f, 2.0f)] public float lowAttackDuration = 0.65f;

    [Tooltip("Comprimento máximo do antebraço no ápice do Ataque Baixo.")]
    [Range(1.2f, 5.0f)] public float lowAttackMultiplier = 1.5f;

    [Header("Calibracao: Ataque Baixo Uppercut (bossUpAttack)")]
    [Tooltip("Tempo de espera (em segundos) antes do braço começar a esticar (espera o agachamento e preparação do soco).")]
    [Range(0.0f, 1.5f)] public float upAttackStartDelay = 0.9f;

    [Tooltip("Duração total do ciclo elástico do Uppercut.")]
    [Range(0.2f, 2.0f)] public float upAttackDuration = 0.45f;

    [Tooltip("Comprimento máximo do antebraço no ápice do Uppercut.")]
    [Range(1.2f, 5.0f)] public float upAttackMultiplier = 2.5f;

    [Header("Curva de Interpolacao Elastica")]
    [Tooltip("Se ativado, usa a AnimationCurve abaixo para controlar a aceleracao/desaceleracao do elastico.")]
    public bool useCustomCurve = false;
    public AnimationCurve customStretchCurve;

    [Header("🦴 Ossos do Braço Direito - Fase 3 (Auto-detectados)")]
    public Transform rightUpperArm;
    public Transform rightForeArm;
    public Transform rightHand;

    [Header("🦴 Ossos do Braço Esquerdo - Fase 3 (Auto-detectados)")]
    public Transform leftUpperArm;
    public Transform leftForeArm;
    public Transform leftHand;

    // Estados de interpolação em tempo real
    private float currentRightStretch = 1.0f;
    private float currentLeftStretch = 1.0f;

    private Coroutine rightStretchRoutine;
    private Coroutine leftStretchRoutine;

    // Cache de posições locais padrão
    private Vector3 defaultRightForeArmLocalPos = Vector3.zero;
    private Vector3 defaultRightHandLocalPos = Vector3.zero;
    private Vector3 defaultLeftForeArmLocalPos = Vector3.zero;
    private Vector3 defaultLeftHandLocalPos = Vector3.zero;

    private void Awake()
    {
        if (customStretchCurve == null || customStretchCurve.length == 0)
        {
            customStretchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
        }
        FindBones();
    }

    private void Start()
    {
        FindBones();
    }

    private void OnEnable()
    {
        FindBones();
    }

    /// <summary>
    /// Auto-detecta os ossos dos braços focando estritamente no modelo da Fase 3 (Neutral Idle / Fase3).
    /// </summary>
    public void FindBones(GameObject targetRoot = null)
    {
        Transform searchRoot = null;

        if (targetRoot != null)
        {
            searchRoot = targetRoot.transform;
        }
        else
        {
            var boss = GetComponent<BossController>() ?? GetComponentInParent<BossController>();
            if (boss != null && boss.visualPhase3 != null)
            {
                searchRoot = boss.visualPhase3.transform;
            }
            else
            {
                searchRoot = transform.Find("Neutral Idle") ?? transform.Find("Fase3") ?? transform.Find("Visual_Phase3") ?? transform.Find("Boss_Fase3");
            }
        }

        if (searchRoot == null) searchRoot = transform;

        // Limpa para não manter ossos antigos da Fase 2
        rightUpperArm = null;
        rightForeArm = null;
        rightHand = null;
        leftUpperArm = null;
        leftForeArm = null;
        leftHand = null;

        Transform[] allChildren = searchRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allChildren)
        {
            string n = t.name.ToLower();

            // Braço Direito (Fase 3)
            if (rightUpperArm == null && (n.Contains("rightarm") || n.Contains("arm_r") || n.Contains("upperarm.r")) && !n.Contains("fore"))
                rightUpperArm = t;
            else if (rightForeArm == null && (n.Contains("rightforearm") || n.Contains("forearm_r") || n.Contains("forearm.r") || n.Contains("lowerarm_r")))
                rightForeArm = t;
            else if (rightHand == null && (n.Contains("righthand") || n.Contains("hand_r") || n.Contains("hand.r")) && !n.Contains("thumb") && !n.Contains("index"))
                rightHand = t;

            // Braço Esquerdo (Fase 3)
            if (leftUpperArm == null && (n.Contains("leftarm") || n.Contains("arm_l") || n.Contains("upperarm.l")) && !n.Contains("fore"))
                leftUpperArm = t;
            else if (leftForeArm == null && (n.Contains("leftforearm") || n.Contains("forearm_l") || n.Contains("forearm.l") || n.Contains("lowerarm_l")))
                leftForeArm = t;
            else if (leftHand == null && (n.Contains("lefthand") || n.Contains("hand_l") || n.Contains("hand.l")) && !n.Contains("thumb") && !n.Contains("index"))
                leftHand = t;
        }

        CacheDefaultLocalPositions();
    }

    private void CacheDefaultLocalPositions()
    {
        if (rightForeArm != null) defaultRightForeArmLocalPos = rightForeArm.localPosition;
        if (rightHand != null) defaultRightHandLocalPos = rightHand.localPosition;

        if (leftForeArm != null) defaultLeftForeArmLocalPos = leftForeArm.localPosition;
        if (leftHand != null) defaultLeftHandLocalPos = leftHand.localPosition;
    }

    /// <summary>
    /// Dispara o estiramento configurado para um ataque específico (ex: "bossLowAttack" ou "bossUpAttack").
    /// </summary>
    public void TriggerAttackStretch(string attackName, bool isRightArm = true)
    {
        if (!enableStretch) return;
        FindBones();

        float delay = 0f;
        float duration = 0.70f;
        float multiplier = 3.0f;

        if (attackName == "bossLowAttack" || attackName == "Attack_Low")
        {
            delay = lowAttackStartDelay;
            duration = lowAttackDuration;
            multiplier = lowAttackMultiplier;
        }
        else if (attackName == "bossUpAttack" || attackName == "Attack_Uppercut")
        {
            delay = upAttackStartDelay;
            duration = upAttackDuration;
            multiplier = upAttackMultiplier;
        }

        if (isRightArm)
        {
            if (rightStretchRoutine != null) StopCoroutine(rightStretchRoutine);
            rightStretchRoutine = StartCoroutine(AnimateElasticCycleWithDelay(true, multiplier, duration, delay));
        }
        else
        {
            if (leftStretchRoutine != null) StopCoroutine(leftStretchRoutine);
            leftStretchRoutine = StartCoroutine(AnimateElasticCycleWithDelay(false, multiplier, duration, delay));
        }
    }

    public void StretchRightArm(float multiplier = 3.0f, float totalDuration = 0.75f, float delay = 0f)
    {
        if (!enableStretch) return;
        FindBones();
        if (rightStretchRoutine != null) StopCoroutine(rightStretchRoutine);
        rightStretchRoutine = StartCoroutine(AnimateElasticCycleWithDelay(true, multiplier, totalDuration, delay));
    }

    public void StretchLeftArm(float multiplier = 3.0f, float totalDuration = 0.75f, float delay = 0f)
    {
        if (!enableStretch) return;
        FindBones();
        if (leftStretchRoutine != null) StopCoroutine(leftStretchRoutine);
        leftStretchRoutine = StartCoroutine(AnimateElasticCycleWithDelay(false, multiplier, totalDuration, delay));
    }

    public void StretchBothArms(float multiplier = 3.0f, float totalDuration = 0.75f, float delay = 0f)
    {
        StretchRightArm(multiplier, totalDuration, delay);
        StretchLeftArm(multiplier, totalDuration, delay);
    }

    private IEnumerator AnimateElasticCycleWithDelay(bool isRight, float targetMultiplier, float duration, float delay)
    {
        // 1. Aguarda o tempo de antecipação/windup da animação
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        // 2. Executa a curva elástica perfeita (Início -> Ápice -> Retorno)
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float factor = 0f;
            if (useCustomCurve && customStretchCurve != null && customStretchCurve.length > 0)
            {
                factor = customStretchCurve.Evaluate(t);
            }
            else
            {
                // Curva senoidal elástica pura (t=0 -> 0; t=0.5 -> 1.0; t=1.0 -> 0)
                float sine = Mathf.Sin(t * Mathf.PI);
                factor = Mathf.Pow(sine, 1.25f);
            }

            float stretch = Mathf.Lerp(1.0f, targetMultiplier, factor);

            if (isRight) currentRightStretch = stretch;
            else currentLeftStretch = stretch;

            yield return null;
        }

        if (isRight)
        {
            currentRightStretch = 1.0f;
            rightStretchRoutine = null;
        }
        else
        {
            currentLeftStretch = 1.0f;
            leftStretchRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (!enableStretch) return;

        // Aplica modificações exclusivamente nos ossos do Braço Direito da Fase 3
        if (currentRightStretch > 1.001f)
        {
            ApplyStretchToArm(rightUpperArm, rightForeArm, rightHand, currentRightStretch, defaultRightForeArmLocalPos, defaultRightHandLocalPos);
        }

        // Aplica modificações exclusivamente nos ossos do Braço Esquerdo da Fase 3
        if (currentLeftStretch > 1.001f)
        {
            ApplyStretchToArm(leftUpperArm, leftForeArm, leftHand, currentLeftStretch, defaultLeftForeArmLocalPos, defaultLeftHandLocalPos);
        }
    }

    private void ApplyStretchToArm(Transform upperArm, Transform foreArm, Transform hand, float stretchFactor, Vector3 defaultForeArmPos, Vector3 defaultHandPos)
    {
        // 1. ANTEBRAÇO: Detecta o eixo primário do osso e estica o cilindro da malha do antebraço
        if (foreArm != null)
        {
            Vector3 boneDir = defaultHandPos != Vector3.zero ? defaultHandPos.normalized : Vector3.forward;
            float absX = Mathf.Abs(boneDir.x);
            float absY = Mathf.Abs(boneDir.y);
            float absZ = Mathf.Abs(boneDir.z);

            Vector3 foreArmScale = Vector3.one;
            if (absX >= absY && absX >= absZ)
                foreArmScale = new Vector3(stretchFactor, 1f, 1f);
            else if (absY >= absX && absY >= absZ)
                foreArmScale = new Vector3(1f, stretchFactor, 1f);
            else
                foreArmScale = new Vector3(1f, 1f, stretchFactor);

            foreArm.localScale = foreArmScale;

            // Desloca o antebraço levemente para frente acompanhando a expansão do braço
            if (defaultForeArmPos != Vector3.zero)
            {
                foreArm.localPosition = defaultForeArmPos * (1.0f + (stretchFactor - 1.0f) * 0.30f);
            }
        }

        // 2. BRAÇO SUPERIOR (Ombro ao cotovelo): Alonga suavemente para dar continuidade ao membro
        if (upperArm != null && defaultForeArmPos != Vector3.zero)
        {
            Vector3 upperDir = defaultForeArmPos.normalized;
            float uAbsX = Mathf.Abs(upperDir.x);
            float uAbsY = Mathf.Abs(upperDir.y);
            float uAbsZ = Mathf.Abs(upperDir.z);

            float upperStretch = 1.0f + (stretchFactor - 1.0f) * 0.35f;
            Vector3 upperArmScale = Vector3.one;

            if (uAbsX >= uAbsY && uAbsX >= uAbsZ)
                upperArmScale = new Vector3(upperStretch, 1f, 1f);
            else if (uAbsY >= uAbsX && uAbsY >= uAbsZ)
                upperArmScale = new Vector3(1f, upperStretch, 1f);
            else
                upperArmScale = new Vector3(1f, 1f, upperStretch);

            upperArm.localScale = upperArmScale;
        }

        // 3. MÃO: Empurrada para a frente no ápice do estiramento, mantendo o tamanho proporcional da mão (sem inflar)
        if (hand != null)
        {
            if (defaultHandPos != Vector3.zero)
            {
                hand.localPosition = defaultHandPos * stretchFactor;
            }

            // Garante que a mão permaneça com escala normal e elegante
            hand.localScale = Vector3.one;
        }
    }

    [ContextMenu("🌿 Test Low Attack Stretch")]
    public void TestLowAttack()
    {
        FindBones();
        TriggerAttackStretch("bossLowAttack");
    }

    [ContextMenu("🌿 Test Uppercut Stretch")]
    public void TestUpAttack()
    {
        FindBones();
        TriggerAttackStretch("bossUpAttack");
    }
}
