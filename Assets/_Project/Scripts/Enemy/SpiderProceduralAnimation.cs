using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the spider's legs procedurally using relative joint rotations.
/// Supports walking gait, leap windup crouch/coil anticipation, leap pose, and fangs/claws bite animation.
/// </summary>
public enum GaitType
{
    Tripod,
    Wave
}

public class SpiderProceduralAnimation : MonoBehaviour
{
    [Header("Configurações da Animação")]
    [Tooltip("Velocidade do ciclo de passos baseado no movimento")]
    public float legCycleSpeed = 10f;
    [Tooltip("Ângulo de balanço para frente e para trás das pernas (coxa/quadril)")]
    public float swingAngle = 18f;
    [Tooltip("Altura que a pata levanta durante o passo (joelho)")]
    public float liftAngle = 20f;
    [Tooltip("Suavidade na transição de parada/movimento")]
    public float transitionSmoothness = 10f;

    [Header("Gait / Padrão de Caminhada")]
    public GaitType gaitType = GaitType.Tripod;
    public float legPhaseSpread = 0.5f;
    public float leftRightPhaseOffset = 1.57f;
    public bool invertLeftWave = false;
    public bool invertRightWave = false;

    [Header("Eixos de Rotação (Ajuste no Inspector se girar errado)")]
    public Vector3 hipRotationAxis = new Vector3(0, 0, 1);
    public Vector3 kneeRotationAxis = new Vector3(1, 0, 0);

    [Header("Ajustes de Espelhamento (Rigging)")]
    public bool invertLeftSwingSign = false;
    public bool invertRightSwingSign = false;
    public bool invertLeftKneeSign = false;
    public bool invertRightKneeSign = false;

    [Header("Ossos das Pernas (Deixe vazio para auto-detectar)")]
    public List<Transform> leftLegRoots = new List<Transform>();
    public List<Transform> rightLegRoots = new List<Transform>();

    [Header("Animação de Ataque (Leap/Dash)")]
    public Transform attackBoneLeft;
    public Transform attackBoneRight;
    public Vector3 attackRotationAxis = new Vector3(1, 0, 0);
    public float attackRotationAngle = 45f;
    public float attackStrikeDuration = 0.35f;
    public bool invertRightAttackRotation = false;
    public float attackSpeed = 15f;

    private Quaternion initialAttackLeftRotation;
    private Quaternion initialAttackRightRotation;
    private float leapAttackTimer = 0f;

    private struct LegJoints
    {
        public Transform hip;
        public Transform knee;
        public Quaternion initialHipRotation;
        public Quaternion initialKneeRotation;
        public float phaseOffset;
    }

    private readonly List<LegJoints> allLegs = new List<LegJoints>();
    private Rigidbody rb;
    private Spider_AI spiderAI;
    private Transform modelRoot;
    private Vector3 initialModelLocalPos;
    private float animationTime = 0f;
    private float currentMovementFactor = 0f;
    private Vector3 lastPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        spiderAI = GetComponent<Spider_AI>();
        lastPosition = transform.position;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.ToLower().Contains("spider"))
            {
                modelRoot = child;
                initialModelLocalPos = child.localPosition;
                break;
            }
        }

        if (leftLegRoots.Count == 0 && rightLegRoots.Count == 0)
            AutoDetectLegs();

        InitializeLegJoints();

        if (attackBoneLeft != null)
            initialAttackLeftRotation = attackBoneLeft.localRotation;
        if (attackBoneRight != null)
            initialAttackRightRotation = attackBoneRight.localRotation;
    }

    void AutoDetectLegs()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        List<Transform> leftFound = new List<Transform>();
        List<Transform> rightFound = new List<Transform>();

        foreach (Transform child in allChildren)
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("013") || nameLower.Contains("claw") || nameLower.Contains("hand")) continue;
            if (nameLower.Contains("end")) continue;

            bool isChildOfLeg = false;
            if (child.parent != null)
            {
                string parentNameLower = child.parent.name.ToLower();
                if (parentNameLower.StartsWith("l.leg") || parentNameLower.StartsWith("l.bone") ||
                    parentNameLower.StartsWith("r.leg") || parentNameLower.StartsWith("r.bone"))
                {
                    isChildOfLeg = true;
                }
            }
            if (isChildOfLeg) continue;

            if (nameLower.StartsWith("l.leg") || nameLower.StartsWith("l.bone"))
                leftFound.Add(child);
            else if (nameLower.StartsWith("r.leg") || nameLower.StartsWith("r.bone"))
                rightFound.Add(child);
            else if (nameLower.StartsWith("bone03") || nameLower.StartsWith("bone.003"))
            {
                if (child.position.x < transform.position.x) leftFound.Add(child);
                else rightFound.Add(child);
            }
        }

        leftFound.Sort((a, b) =>
        {
            float az = transform.InverseTransformPoint(a.position).z;
            float bz = transform.InverseTransformPoint(b.position).z;
            return bz.CompareTo(az);
        });
        rightFound.Sort((a, b) =>
        {
            float az = transform.InverseTransformPoint(a.position).z;
            float bz = transform.InverseTransformPoint(b.position).z;
            return bz.CompareTo(az);
        });

        leftLegRoots = leftFound;
        rightLegRoots = rightFound;
    }

    void InitializeLegJoints()
    {
        allLegs.Clear();

        for (int i = 0; i < leftLegRoots.Count; i++)
            AddLegToSystem(leftLegRoots[i], i, true);

        for (int i = 0; i < rightLegRoots.Count; i++)
            AddLegToSystem(rightLegRoots[i], i, false);
    }

    void AddLegToSystem(Transform root, int index, bool isLeft)
    {
        if (root == null) return;

        LegJoints leg = new LegJoints
        {
            hip = root,
            initialHipRotation = root.localRotation
        };

        if (root.childCount > 0)
        {
            leg.knee = root.GetChild(0);
            leg.initialKneeRotation = leg.knee.localRotation;
        }

        if (gaitType == GaitType.Tripod)
        {
            if (isLeft)
                leg.phaseOffset = (index % 2 == 0) ? 0f : Mathf.PI;
            else
                leg.phaseOffset = (index % 2 == 0) ? Mathf.PI : 0f;
        }
        else
        {
            int totalLegs = isLeft ? leftLegRoots.Count : rightLegRoots.Count;
            bool invertThisSide = isLeft ? invertLeftWave : invertRightWave;
            float factor = invertThisSide ? (totalLegs - 1 - index) : index;
            leg.phaseOffset = isLeft ? (factor * legPhaseSpread) : (factor * legPhaseSpread + leftRightPhaseOffset);
        }

        allLegs.Add(leg);
    }

    void Update()
    {
        // Proteção contra divisão por zero / NaN quando o jogo está pausado
        if (Time.deltaTime <= 0.0001f) return;

        Vector3 displacement = transform.position - lastPosition;
        displacement.y = 0f;
        float speed = displacement.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        if (rb != null)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.y = 0f;
            speed = Mathf.Max(speed, localVel.magnitude);
        }

        bool isLeaping = spiderAI != null && spiderAI.IsLeaping;
        bool isRetreating = spiderAI != null && spiderAI.IsRetreating;

        // Mantém o corpo bem baixo e aerodinâmico no dash pra frente e leve inclinação no recuo
        if (modelRoot != null)
        {
            Vector3 targetModelOffset = Vector3.zero;
            if (isLeaping)
                targetModelOffset = new Vector3(0f, -0.06f, 0.14f);
            else if (isRetreating)
                targetModelOffset = new Vector3(0f, -0.05f, -0.10f);

            modelRoot.localPosition = Vector3.Lerp(
                modelRoot.localPosition,
                initialModelLocalPos + targetModelOffset,
                Time.deltaTime * 18f
            );
        }

        AnimateAttackBone(isLeaping);

        if (isLeaping)
        {
            ApplyLeapPose();
            return;
        }

        // Durante o Retreat, as patas patinam rápido para trás dando a cara ágil de aranha recuando!
        float targetFactor = (speed > 0.1f || isRetreating) ? 1f : 0f;
        currentMovementFactor = Mathf.Lerp(currentMovementFactor, targetFactor, transitionSmoothness * Time.deltaTime);

        if (isRetreating)
            animationTime -= Time.deltaTime * legCycleSpeed * 2.4f;
        else if (speed > 0.1f)
            animationTime += Time.deltaTime * legCycleSpeed * Mathf.Clamp(speed / 4f, 0.5f, 2.2f);

        AnimateLegs();
    }

    void AnimateLegs()
    {
        foreach (LegJoints leg in allLegs)
        {
            if (leg.hip == null) continue;

            float wave = Mathf.Sin(animationTime + leg.phaseOffset);
            bool isLeft = leftLegRoots.Contains(leg.hip);

            float hipSwing = wave * swingAngle * currentMovementFactor;
            if (isLeft && invertLeftSwingSign) hipSwing = -hipSwing;
            if (!isLeft && invertRightSwingSign) hipSwing = -hipSwing;

            Quaternion hipTargetRot = leg.initialHipRotation * Quaternion.Euler(hipRotationAxis * hipSwing);

            float kneeLift = wave > 0f ? (wave * liftAngle * currentMovementFactor) : 0f;
            if (isLeft && invertLeftKneeSign) kneeLift = -kneeLift;
            if (!isLeft && invertRightKneeSign) kneeLift = -kneeLift;

            Quaternion kneeTargetRot = leg.initialKneeRotation * Quaternion.Euler(kneeRotationAxis * kneeLift);

            leg.hip.localRotation = Quaternion.Slerp(leg.hip.localRotation, hipTargetRot, transitionSmoothness * 2f * Time.deltaTime);
            if (leg.knee != null)
                leg.knee.localRotation = Quaternion.Slerp(leg.knee.localRotation, kneeTargetRot, transitionSmoothness * 2f * Time.deltaTime);
        }
    }

    void ApplyLeapPose()
    {
        for (int i = 0; i < allLegs.Count; i++)
        {
            LegJoints leg = allLegs[i];
            if (leg.hip == null) continue;

            float hipTargetAngle = (i % 2 == 0) ? -15f : 20f;
            float kneeTargetAngle = -15f;

            bool isLeft = leftLegRoots.Contains(leg.hip);
            if (isLeft && invertLeftSwingSign) hipTargetAngle = -hipTargetAngle;
            if (!isLeft && invertRightSwingSign) hipTargetAngle = -hipTargetAngle;
            if (isLeft && invertLeftKneeSign) kneeTargetAngle = -kneeTargetAngle;
            if (!isLeft && invertRightKneeSign) kneeTargetAngle = -kneeTargetAngle;

            Quaternion hipTargetRot = leg.initialHipRotation * Quaternion.Euler(hipRotationAxis * hipTargetAngle);
            Quaternion kneeTargetRot = leg.initialKneeRotation * Quaternion.Euler(kneeRotationAxis * kneeTargetAngle);

            leg.hip.localRotation = Quaternion.Slerp(leg.hip.localRotation, hipTargetRot, transitionSmoothness * Time.deltaTime);
            if (leg.knee != null)
                leg.knee.localRotation = Quaternion.Slerp(leg.knee.localRotation, kneeTargetRot, transitionSmoothness * Time.deltaTime);
        }
    }

    void AnimateAttackBone(bool isLeaping)
    {
        float attackFactor = 0f;
        if (isLeaping)
        {
            leapAttackTimer += Time.deltaTime;
            float tNorm = Mathf.Clamp01(leapAttackTimer / Mathf.Max(0.1f, attackStrikeDuration));
            attackFactor = Mathf.Sin(tNorm * Mathf.PI);
        }
        else
        {
            leapAttackTimer = 0f;
            attackFactor = 0f;
        }

        if (attackBoneLeft != null)
        {
            Quaternion targetRotLeft = initialAttackLeftRotation * Quaternion.Euler(attackRotationAxis * attackRotationAngle * attackFactor);
            attackBoneLeft.localRotation = Quaternion.Slerp(attackBoneLeft.localRotation, targetRotLeft, attackSpeed * Time.deltaTime);
        }

        if (attackBoneRight != null)
        {
            float angleMultiplier = invertRightAttackRotation ? -1f : 1f;
            Quaternion targetRotRight = initialAttackRightRotation * Quaternion.Euler(attackRotationAxis * attackRotationAngle * attackFactor * angleMultiplier);
            attackBoneRight.localRotation = Quaternion.Slerp(attackBoneRight.localRotation, targetRotRight, attackSpeed * Time.deltaTime);
        }
    }
}
