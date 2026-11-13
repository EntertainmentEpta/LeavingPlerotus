using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the spider's legs procedurally using relative joint rotations.
/// Decoupled from Spider_AI, automatically detects speed and leaping states.
/// </summary>
public enum GaitType
{
    Tripod, // Tripé alternado (natural para 6 patas: 3 apoiam, 3 movem)
    Wave    // Ondulado (ripple sequencial de trás para frente ou vice-versa)
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
    [Tooltip("Tripod = Tripé alternado (natural para 6 patas, 3 pernas apoiam e 3 movem). Wave = Ondulado (sequencial).")]
    public GaitType gaitType = GaitType.Tripod;
    [Tooltip("Espaçamento de fase entre pernas consecutivas. (Usado apenas no modo Wave).")]
    public float legPhaseSpread = 0.5f;
    [Tooltip("Defasagem de fase entre o lado esquerdo e o direito (Ex: 3.14 para alternar lados, 1.57 para onda contínua).")]
    public float leftRightPhaseOffset = 1.57f;
    [Tooltip("Inverte a direção da onda do lado esquerdo (frente <-> trás).")]
    public bool invertLeftWave = false;
    [Tooltip("Inverte a direção da onda do lado direito (frente <-> trás).")]
    public bool invertRightWave = false;

    [Header("Eixos de Rotação (Ajuste no Inspector se girar errado)")]
    [Tooltip("Eixo de rotação para o quadril (Ex: 0, 0, 1 para Z, 1, 0, 0 para X)")]
    public Vector3 hipRotationAxis = new Vector3(0, 0, 1);
    [Tooltip("Eixo de rotação para o joelho (Ex: 1, 0, 0 para X, 0, 0, 1 para Z)")]
    public Vector3 kneeRotationAxis = new Vector3(1, 0, 0);
    
    [Header("Ajustes de Espelhamento (Rigging)")]
    [Tooltip("Inverte o sentido de balanço (frente/trás) do quadril esquerdo.")]
    public bool invertLeftSwingSign = false;
    [Tooltip("Inverte o sentido de balanço (frente/trás) do quadril direito.")]
    public bool invertRightSwingSign = false;
    [Tooltip("Inverte o sentido de dobra (cima/baixo) do joelho esquerdo.")]
    public bool invertLeftKneeSign = false;
    [Tooltip("Inverte o sentido de dobra (cima/baixo) do joelho direito.")]
    public bool invertRightKneeSign = false;

    [Header("Ossos das Pernas (Deixe vazio para auto-detectar)")]
    public List<Transform> leftLegRoots = new List<Transform>();
    public List<Transform> rightLegRoots = new List<Transform>();

    [Header("Animação de Ataque (Leap/Dash)")]
    [Tooltip("Osso esquerdo que vai se mover durante o ataque (Ex: garra/mandíbula esquerda).")]
    public Transform attackBoneLeft;
    [Tooltip("Osso direito que vai se mover durante o ataque (Ex: garra/mandíbula direita).")]
    public Transform attackBoneRight;
    [Tooltip("Eixo local de rotação para o movimento de ataque.")]
    public Vector3 attackRotationAxis = new Vector3(1, 0, 0);
    [Tooltip("Ângulo de rotação alvo para a animação de ataque.")]
    public float attackRotationAngle = 45f;
    [Tooltip("Duração do golpe de ataque (tempo que as garras levam para abrir e fechar durante o pulo).")]
    public float attackStrikeDuration = 0.35f;
    [Tooltip("Inverte a rotação do osso de ataque direito. Desmarque se o modelo já estiver espelhado no Blender.")]
    public bool invertRightAttackRotation = false;
    [Tooltip("Velocidade da transição da animação do osso de ataque.")]
    public float attackSpeed = 15f;

    private Quaternion initialAttackLeftRotation;
    private Quaternion initialAttackRightRotation;
    private float leapAttackTimer = 0f;

    // Estrutura para armazenar as articulações de cada perna
    private struct LegJoints
    {
        public Transform hip;
        public Transform knee;
        public Quaternion initialHipRotation;
        public Quaternion initialKneeRotation;
        public float phaseOffset; // Para alternar os passos
    }

    private List<LegJoints> allLegs = new List<LegJoints>();
    private Rigidbody rb;
    private Spider_AI spiderAI;
    private float animationTime = 0f;
    private float currentMovementFactor = 0f;
    private Vector3 lastPosition;
    private float debugTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        spiderAI = GetComponent<Spider_AI>();
        lastPosition = transform.position;

        // Se o usuário não definiu os ossos, tenta encontrar automaticamente
        if (leftLegRoots.Count == 0 && rightLegRoots.Count == 0)
        {
            AutoDetectLegs();
        }

        InitializeLegJoints();
        Debug.Log($"[SPIDER_ANIMATION] Inicializado com {allLegs.Count} pernas na lista interna.");

        if (attackBoneLeft != null)
        {
            initialAttackLeftRotation = attackBoneLeft.localRotation;
        }
        if (attackBoneRight != null)
        {
            initialAttackRightRotation = attackBoneRight.localRotation;
        }

        // Verifica se há algum Animator ativo que possa estar travando a rotação
        Animator[] parentAnimators = GetComponentsInParent<Animator>();
        foreach (var anim in parentAnimators)
        {
            if (anim.enabled)
            {
                Debug.LogWarning($"[SPIDER_ANIMATION] ATENÇÃO: Existe um Animator ativo no objeto PAI '{anim.gameObject.name}'. Ele vai travar a rotação das pernas! Desative-o no Inspector.");
            }
        }
        
        Animator[] childAnimators = GetComponentsInChildren<Animator>();
        foreach (var anim in childAnimators)
        {
            if (anim.enabled && anim.gameObject != gameObject)
            {
                Debug.LogWarning($"[SPIDER_ANIMATION] ATENÇÃO: Existe um Animator ativo no objeto FILHO '{anim.gameObject.name}'. Ele vai travar a rotação das pernas! Desative-o no Inspector.");
            }
        }
    }

    void AutoDetectLegs()
    {
        // Procura todos os filhos recursivamente
        Transform[] allChildren = GetComponentsInChildren<Transform>();
        List<Transform> leftFound = new List<Transform>();
        List<Transform> rightFound = new List<Transform>();

        foreach (Transform child in allChildren)
        {
            string nameLower = child.name.ToLower();
            
            // Ignora garras/mãos terminadas em 013 ou que contenham 'claw'/'hand'
            if (nameLower.Contains("013") || nameLower.Contains("claw") || nameLower.Contains("hand"))
            {
                continue;
            }

            // Ignora pontas de ossos (ends)
            if (nameLower.Contains("end"))
            {
                continue;
            }

            // Verifica se o pai deste osso também é um osso de perna (se for, este osso é um joelho/pata, não o quadril)
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

            if (isChildOfLeg)
            {
                continue; // Pula joelhos, patas e juntas intermediárias
            }

            // Procura ossos de pernas (como l.leg, l.bone, etc.)
            if (nameLower.StartsWith("l.leg") || nameLower.StartsWith("l.bone"))
            {
                leftFound.Add(child);
            }
            else if (nameLower.StartsWith("r.leg") || nameLower.StartsWith("r.bone"))
            {
                rightFound.Add(child);
            }
            else if (nameLower.StartsWith("bone03") || nameLower.StartsWith("bone.003"))
            {
                // Adiciona o bone03 (geralmente vai para o lado correspondente à sua posição)
                if (child.position.x < transform.position.x)
                {
                    leftFound.Add(child);
                }
                else
                {
                    rightFound.Add(child);
                }
            }
        }

        // Ordena pela posição Z local (frente para trás) para garantir que a onda siga a ordem física real das pernas!
        // Isso resolve o problema de nomes misturados (L.Leg e L.Bone) bagunçarem a sequência da onda de passos.
        leftFound.Sort((a, b) => {
            float az = transform.InverseTransformPoint(a.position).z;
            float bz = transform.InverseTransformPoint(b.position).z;
            return bz.CompareTo(az); // Decrescente (Z maior = frente)
        });
        rightFound.Sort((a, b) => {
            float az = transform.InverseTransformPoint(a.position).z;
            float bz = transform.InverseTransformPoint(b.position).z;
            return bz.CompareTo(az); // Decrescente (Z maior = frente)
        });

        leftLegRoots = leftFound;
        rightLegRoots = rightFound;

        Debug.Log($"[SPIDER_ANIMATION] Auto-detectado: {leftLegRoots.Count} pernas esquerdas, {rightLegRoots.Count} pernas direitas.");
    }

    void InitializeLegJoints()
    {
        allLegs.Clear();

        // Inicializa pernas esquerdas
        for (int i = 0; i < leftLegRoots.Count; i++)
        {
            AddLegToSystem(leftLegRoots[i], i, true);
        }

        // Inicializa pernas direitas
        for (int i = 0; i < rightLegRoots.Count; i++)
        {
            AddLegToSystem(rightLegRoots[i], i, false);
        }
    }

    void AddLegToSystem(Transform root, int index, bool isLeft)
    {
        if (root == null) return;

        LegJoints leg = new LegJoints();
        leg.hip = root;
        leg.initialHipRotation = root.localRotation;

        // O joelho geralmente é o primeiro filho direto do quadril
        if (root.childCount > 0)
        {
            leg.knee = root.GetChild(0);
            leg.initialKneeRotation = leg.knee.localRotation;
        }

        // Define a fase do passo de acordo com o GaitType
        if (gaitType == GaitType.Tripod)
        {
            // Padrão Tripé Alternado (Tripod Gait) - Altamente natural para insetos/aranhas de 6 patas
            // L0 (frente) e L2 (trás) movem no mesmo tempo, L1 (meio) move em oposição (defasado em PI)
            if (isLeft)
            {
                leg.phaseOffset = (index % 2 == 0) ? 0f : Mathf.PI;
            }
            else
            {
                // Lado direito em oposição para manter a simetria de tripé alternado
                leg.phaseOffset = (index % 2 == 0) ? Mathf.PI : 0f;
            }
        }
        else
        {
            // Padrão Ondulado (Wave Gait) - Movimento sequencial em onda
            int totalLegs = isLeft ? leftLegRoots.Count : rightLegRoots.Count;
            bool invertThisSide = isLeft ? invertLeftWave : invertRightWave;
            float factor = invertThisSide ? (totalLegs - 1 - index) : index;

            if (isLeft)
            {
                leg.phaseOffset = factor * legPhaseSpread;
            }
            else
            {
                leg.phaseOffset = factor * legPhaseSpread + leftRightPhaseOffset;
            }
        }

        allLegs.Add(leg);
    }

    void Update()
    {
        // Calcula a velocidade real baseada na movimentação física
        Vector3 displacement = transform.position - lastPosition;
        displacement.y = 0; // Ignora altura
        float speed = displacement.magnitude / Time.deltaTime;
        lastPosition = transform.position;

        // Se tiver Rigidbody ativo, pega a velocidade dele
        if (rb != null)
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.y = 0;
            speed = Mathf.Max(speed, localVel.magnitude);
        }

        // Verifica se a aranha está pulando (leap)
        bool isLeaping = false;
        if (spiderAI != null)
        {
            isLeaping = spiderAI.IsLeaping;
        }
        else if (rb != null && Mathf.Abs(rb.linearVelocity.y) > 1.5f)
        {
            isLeaping = true;
        }

        // Executa a animação do osso de ataque (mandíbula/garra/cauda)
        AnimateAttackBone(isLeaping);

        if (isLeaping)
        {
            ApplyLeapPose();
            return;
        }

        // Controla o fator de animação (0 = parado, 1 = correndo)
        float targetFactor = (speed > 0.1f) ? 1f : 0f;
        currentMovementFactor = Mathf.Lerp(currentMovementFactor, targetFactor, transitionSmoothness * Time.deltaTime);

        if (speed > 0.1f)
        {
            // Avança o tempo da animação proporcionalmente à velocidade
            animationTime += Time.deltaTime * legCycleSpeed * Mathf.Clamp(speed / 4f, 0.5f, 2f);
        }

        AnimateLegs();

        // debugTimer += Time.deltaTime;
        // if (debugTimer >= 1f)
        // {
        //     debugTimer = 0f;
        //     Debug.Log($"[SPIDER_ANIMATION] Update Rodando | Speed: {speed:F2} | MovFactor: {currentMovementFactor:F2} | Pernas Ativas: {allLegs.Count}");
        // }
    }

    void AnimateLegs()
    {
        foreach (LegJoints leg in allLegs)
        {
            if (leg.hip == null) continue;

            // Calcula a onda do passo para essa perna
            float wave = Mathf.Sin(animationTime + leg.phaseOffset);

            // Identifica se a perna é esquerda ou direita
            bool isLeft = leftLegRoots.Contains(leg.hip);

            // 1. Rotação do Quadril (Balanço frente/trás)
            float hipSwing = wave * swingAngle * currentMovementFactor;
            
            // Corrige espelhamento do quadril se necessário
            if (isLeft && invertLeftSwingSign) hipSwing = -hipSwing;
            if (!isLeft && invertRightSwingSign) hipSwing = -hipSwing;

            Quaternion hipTargetRot = leg.initialHipRotation * Quaternion.Euler(hipRotationAxis * hipSwing);

            // 2. Rotação do Joelho (Levantamento)
            float kneeLift = 0f;
            if (wave > 0f)
            {
                kneeLift = wave * liftAngle * currentMovementFactor;
            }

            // Corrige espelhamento do joelho se necessário
            if (isLeft && invertLeftKneeSign) kneeLift = -kneeLift;
            if (!isLeft && invertRightKneeSign) kneeLift = -kneeLift;

            Quaternion kneeTargetRot = leg.initialKneeRotation * Quaternion.Euler(kneeRotationAxis * kneeLift);

            // Suaviza as rotações locais
            leg.hip.localRotation = Quaternion.Slerp(leg.hip.localRotation, hipTargetRot, transitionSmoothness * 2f * Time.deltaTime);
            if (leg.knee != null)
            {
                leg.knee.localRotation = Quaternion.Slerp(leg.knee.localRotation, kneeTargetRot, transitionSmoothness * 2f * Time.deltaTime);
            }
        }
    }

    void ApplyLeapPose()
    {
        // No pulo, as pernas frontais esticam para frente e as traseiras empurram para trás
        for (int i = 0; i < allLegs.Count; i++)
        {
            LegJoints leg = allLegs[i];
            if (leg.hip == null) continue;

            float hipTargetAngle = 0f;
            float kneeTargetAngle = -15f; // Dobra levemente para pose de pulo

            // Determina se a perna é frontal ou traseira com base no index
            if (i % 2 == 0) // Pernas da frente
            {
                hipTargetAngle = -15f; // Estica para frente
            }
            else // Pernas de trás
            {
                hipTargetAngle = 20f; // Estica para trás
            }

            bool isLeft = leftLegRoots.Contains(leg.hip);

            // Corrige espelhamento do quadril no pulo se necessário
            if (isLeft && invertLeftSwingSign) hipTargetAngle = -hipTargetAngle;
            if (!isLeft && invertRightSwingSign) hipTargetAngle = -hipTargetAngle;

            // Corrige espelhamento do joelho no pulo se necessário
            if (isLeft && invertLeftKneeSign) kneeTargetAngle = -kneeTargetAngle;
            if (!isLeft && invertRightKneeSign) kneeTargetAngle = -kneeTargetAngle;

            Quaternion hipTargetRot = leg.initialHipRotation * Quaternion.Euler(hipRotationAxis * hipTargetAngle);
            Quaternion kneeTargetRot = leg.initialKneeRotation * Quaternion.Euler(kneeRotationAxis * kneeTargetAngle);

            leg.hip.localRotation = Quaternion.Slerp(leg.hip.localRotation, hipTargetRot, transitionSmoothness * Time.deltaTime);
            if (leg.knee != null)
            {
                leg.knee.localRotation = Quaternion.Slerp(leg.knee.localRotation, kneeTargetRot, transitionSmoothness * Time.deltaTime);
            }
        }
    }

    void AnimateAttackBone(bool isAttacking)
    {
        float attackFactor = 0f;
        if (isAttacking)
        {
            leapAttackTimer += Time.deltaTime;
            float tNorm = Mathf.Clamp01(leapAttackTimer / attackStrikeDuration);
            // Função seno: sobe de 0 a 1 e desce de 1 a 0 (golpe rápido e fechamento automático)
            attackFactor = Mathf.Sin(tNorm * Mathf.PI); 
        }
        else
        {
            leapAttackTimer = 0f;
            attackFactor = 0f;
        }

        // Animação do osso esquerdo (rotação normal)
        if (attackBoneLeft != null)
        {
            Quaternion targetRotLeft = initialAttackLeftRotation * Quaternion.Euler(attackRotationAxis * attackRotationAngle * attackFactor);
            attackBoneLeft.localRotation = Quaternion.Slerp(attackBoneLeft.localRotation, targetRotLeft, attackSpeed * Time.deltaTime);
        }

        // Animação do osso direito (rotação com espelhamento configurável)
        if (attackBoneRight != null)
        {
            float angleMultiplier = invertRightAttackRotation ? -1f : 1f;
            Quaternion targetRotRight = initialAttackRightRotation * Quaternion.Euler(attackRotationAxis * attackRotationAngle * attackFactor * angleMultiplier);
            attackBoneRight.localRotation = Quaternion.Slerp(attackBoneRight.localRotation, targetRotRight, attackSpeed * Time.deltaTime);
        }
    }
}
