using UnityEngine;

/// <summary>
/// Animação Procedural do Bismutado (Fantoche de Pedra Possuído por Gosma).
/// Inclui Poses do Ataque de Cruzado Totalmente Editáveis no Inspector, Rastro de Energia (Trail VFX)
/// e Auto-Ataque de Proximidade (garante que ele ataque qualquer Player que chegar perto).
/// </summary>
public class BismutadoProceduralAnimation : MonoBehaviour
{
    [Header("1. Osso do Torso / Corpo")]
    public Transform rootBody;

    [Header("2. Ossos das 3 Pernas do Tripé")]
    public Transform legHipL;
    public Transform legFrontL;
    public Transform footFrontL;

    public Transform legHipR;
    public Transform legFrontR;
    public Transform footFrontR;

    public Transform legHipBack;
    public Transform legBack;
    public Transform footBack;

    [Header("3. Ossos do Braço com Lança de Cristal")]
    public Transform armShoulder;
    public Transform armUpper;
    public Transform armForearm; // Inclui a pedra/lança pontiaguda na mão

    public enum LegSwingAxis { LocalZ_Forward, LocalY_Forward, LocalX_Sideways }

    [Header("Parâmetros de Animação - Caminhada em Tripé")]
    public bool isWalking = false;
    public float walkGaitSpeed = 6.0f;
    public float legStepAngleMax = 22.0f;
    public LegSwingAxis swingDirection = LegSwingAxis.LocalY_Forward;
    public Vector3 swingAxis = new Vector3(0, 1, 1);

    [Header("Parâmetros de Idle Caótico (Pernas Fincadas, Tronco Instável)")]
    public float idleTwitchSpeed = 2.0f;
    public float bodyJitterIntensity = 2.0f;
    public float armJitterIntensity = 3.0f;

    [Header("Parâmetros de Ataque")]
    public bool isAttacking = false;
    public float attackSpeed = 1.0f;

    [Header("🤖 GATILHO AUTOMÁTICO DE PROXIMIDADE (AUTO-ATAQUE DA IA)")]
    [Tooltip("Se verdadeiro, o Bismutado atacará automaticamente qualquer Player que chegar perto, mesmo sem IA externa!")]
    public bool autoAttackNearPlayer = true;
    [Tooltip("Distância mínima do Player para disparar o soco de Cruzado")]
    public float autoAttackDistance = 5.0f;
    [Tooltip("Intervalo entre auto-ataques em segundos")]
    public float autoAttackCooldown = 1.5f;

    [Header("✨ VFX RASTRO DE ENERGIA DO PUNHO (Trail VFX)")]
    public TrailRenderer fistTrailRenderer;
    [Tooltip("Cor reluzente de Bismuto para o rastro do soco")]
    public Color trailColor = new Color(1f, 0.15f, 0.75f, 0.9f); // Rosa Bismuto Magenta
    public float trailTime = 0.4f;
    public float trailStartWidth = 0.8f;

    [Header("🛠️ CONTROLE DE POSES DO ATAQUE (Ajuste fino no Inspector)")]
    [Tooltip("Marque esta caixa para congelar o ataque e pré-visualizar a pose ajustando a barra abaixo!")]
    public bool previewAttackPose = false;
    [Range(0f, 1f)]
    public float attackPreviewProgress = 0.5f;

    [Header("Pose 1: Wind-Up (Puxada do Corpo/Braço para a Direita)")]
    public Vector3 windupBodyEuler = new Vector3(10f, 55f, -10f);
    public Vector3 windupShoulderEuler = new Vector3(-20f, 70f, -20f);
    public Vector3 windupUpperArmEuler = new Vector3(-25f, -67f, 30f);
    public Vector3 windupForearmEuler = new Vector3(0f, 0f, 0f);

    [Header("Pose 2: Strike (Varredura do Cruzado na Altura do Player)")]
    public Vector3 strikeBodyEuler = new Vector3(18f, -70f, -1.1f);
    public Vector3 strikeShoulderEuler = new Vector3(-10f, -100f, 4f);
    public Vector3 strikeUpperArmEuler = new Vector3(0f, -5f, 55f);
    public Vector3 strikeForearmEuler = new Vector3(-70f, -10f, 0f);

    // Guardar posições e rotações iniciais
    private Vector3 rootInitialPos;
    private Quaternion rootInitialRot;
    private Quaternion shoulderInitialRot;
    private Quaternion upperInitialRot;
    private Quaternion forearmInitialRot;

    private Quaternion hipLInitialRot;
    private Quaternion hipRInitialRot;
    private Quaternion hipBackInitialRot;

    private Quaternion legLInitialRot;
    private Quaternion legRInitialRot;
    private Quaternion legBackInitialRot;

    private BismutadoHitbox bismutadoHitbox;
    private float attackTimer = 0f;
    private float autoAttackTimer = 0f;
    private Transform playerTarget;

    void Awake()
    {
        AutoFindBones();
        SaveInitialRotations();
        SetupFistTrail();
    }

    public void AutoFindBones()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            string n = t.name.ToLower().Replace("_", "").Replace(".", "").Trim();

            if (n.Contains("rootbody") || n == "root") rootBody = t;
            else if (n.Contains("leghipl") || n.Contains("hipl")) legHipL = t;
            else if (n.Contains("legfrontl")) legFrontL = t;
            else if (n.Contains("footfrontl")) footFrontL = t;
            else if (n.Contains("leghipr") || n.Contains("hipr")) legHipR = t;
            else if (n.Contains("legfrontr")) legFrontR = t;
            else if (n.Contains("footfrontr")) footFrontR = t;
            else if (n.Contains("leghipback") || n.Contains("hipback")) legHipBack = t;
            else if (n.Contains("legback")) legBack = t;
            else if (n.Contains("footback")) footBack = t;
            else if (n.Contains("armshoulder") || n.Contains("shoulder")) armShoulder = t;
            else if (n.Contains("armupper") || n.Contains("upper")) armUpper = t;
            else if (n.Contains("armforearm") || n.Contains("forearm")) armForearm = t;
        }

        if (armForearm != null)
        {
            bismutadoHitbox = armForearm.GetComponent<BismutadoHitbox>();
            if (bismutadoHitbox == null) bismutadoHitbox = armForearm.gameObject.AddComponent<BismutadoHitbox>();
        }
    }

    private void SetupFistTrail()
    {
        if (armForearm == null) return;

        if (fistTrailRenderer == null)
        {
            fistTrailRenderer = armForearm.GetComponent<TrailRenderer>();
            if (fistTrailRenderer == null)
            {
                fistTrailRenderer = armForearm.gameObject.AddComponent<TrailRenderer>();
            }
        }

        fistTrailRenderer.time = trailTime;
        fistTrailRenderer.startWidth = trailStartWidth;
        fistTrailRenderer.endWidth = 0.0f;
        fistTrailRenderer.autodestruct = false;
        fistTrailRenderer.emitting = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trailColor, 0.0f), new GradientColorKey(new Color(0.5f, 0f, 0.8f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        fistTrailRenderer.colorGradient = gradient;

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
        {
            Material trailMat = new Material(particleShader);
            if (trailMat.HasProperty("_Surface")) trailMat.SetFloat("_Surface", 1f);
            if (trailMat.HasProperty("_Blend")) trailMat.SetFloat("_Blend", 2f);
            if (trailMat.HasProperty("_SrcBlend")) trailMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (trailMat.HasProperty("_DstBlend")) trailMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (trailMat.HasProperty("_ZWrite")) trailMat.SetInt("_ZWrite", 0);
            if (trailMat.HasProperty("_BaseColor")) trailMat.SetColor("_BaseColor", trailColor);
            if (trailMat.HasProperty("_Color")) trailMat.SetColor("_Color", trailColor);
            if (trailMat.HasProperty("_EmissionColor"))
            {
                trailMat.EnableKeyword("_EMISSION");
                trailMat.SetColor("_EmissionColor", trailColor * 2.5f);
            }
            trailMat.renderQueue = 3000;
            fistTrailRenderer.material = trailMat;
        }
    }

    private void SaveInitialRotations()
    {
        if (rootBody != null)
        {
            rootInitialPos = rootBody.localPosition;
            rootInitialRot = rootBody.localRotation;
        }
        if (armShoulder != null) shoulderInitialRot = armShoulder.localRotation;
        if (armUpper != null) upperInitialRot = armUpper.localRotation;
        if (armForearm != null) forearmInitialRot = armForearm.localRotation;

        if (legHipL != null) hipLInitialRot = legHipL.localRotation;
        if (legHipR != null) hipRInitialRot = legHipR.localRotation;
        if (legHipBack != null) hipBackInitialRot = legHipBack.localRotation;

        if (legFrontL != null) legLInitialRot = legFrontL.localRotation;
        if (legFrontR != null) legRInitialRot = legFrontR.localRotation;
        if (legBack != null) legBackInitialRot = legBack.localRotation;
    }

    void Update()
    {
        float time = Time.time;

        if (previewAttackPose)
        {
            ApplyPreviewPose(attackPreviewProgress);
            return;
        }

        // AUTO-ATAQUE DE SEGURANÇA: Se qualquer Player se aproximar, o Bismutado dispara o soco automaticamente!
        if (autoAttackNearPlayer && !isAttacking)
        {
            autoAttackTimer -= Time.deltaTime;
            if (autoAttackTimer <= 0f)
            {
                if (playerTarget == null)
                {
                    GameObject p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) playerTarget = p.transform;
                }

                if (playerTarget != null)
                {
                    float dist = Vector3.Distance(transform.position, playerTarget.position);
                    if (dist <= autoAttackDistance)
                    {
                        Debug.Log($"⚔️ [BISMUTADO AUTO-ATTACK] Player detectado a {dist:F1}m! DISPARANDO CRUZADO!");
                        TriggerCrystalSlam();
                        autoAttackTimer = autoAttackCooldown;
                    }
                }
            }
        }

        if (isAttacking)
        {
            AnimateRightHookAttack();
        }
        else
        {
            AnimateChaoticIdleAndWalk(time);
        }
    }

    private void ApplyPreviewPose(float progress)
    {
        if (progress <= 0.5f)
        {
            float t = progress / 0.5f;
            if (rootBody != null) rootBody.localRotation = Quaternion.Slerp(rootInitialRot, rootInitialRot * Quaternion.Euler(windupBodyEuler), t);
            if (armShoulder != null) armShoulder.localRotation = Quaternion.Slerp(shoulderInitialRot, shoulderInitialRot * Quaternion.Euler(windupShoulderEuler), t);
            if (armUpper != null) armUpper.localRotation = Quaternion.Slerp(upperInitialRot, upperInitialRot * Quaternion.Euler(windupUpperArmEuler), t);
            if (armForearm != null) armForearm.localRotation = Quaternion.Slerp(forearmInitialRot, forearmInitialRot * Quaternion.Euler(windupForearmEuler), t);
        }
        else
        {
            float t = (progress - 0.5f) / 0.5f;
            if (rootBody != null) rootBody.localRotation = Quaternion.Slerp(rootInitialRot * Quaternion.Euler(windupBodyEuler), rootInitialRot * Quaternion.Euler(strikeBodyEuler), t);
            if (armShoulder != null) armShoulder.localRotation = Quaternion.Slerp(shoulderInitialRot * Quaternion.Euler(windupShoulderEuler), shoulderInitialRot * Quaternion.Euler(strikeShoulderEuler), t);
            if (armUpper != null) armUpper.localRotation = Quaternion.Slerp(upperInitialRot * Quaternion.Euler(windupUpperArmEuler), upperInitialRot * Quaternion.Euler(strikeUpperArmEuler), t);
            if (armForearm != null) armForearm.localRotation = Quaternion.Slerp(forearmInitialRot * Quaternion.Euler(windupForearmEuler), forearmInitialRot * Quaternion.Euler(strikeForearmEuler), t);
        }
    }

    private void AnimateChaoticIdleAndWalk(float time)
    {
        if (rootBody != null)
        {
            float noiseX = (Mathf.PerlinNoise(time * idleTwitchSpeed, 0f) - 0.5f) * bodyJitterIntensity;
            float noiseY = (Mathf.PerlinNoise(0f, time * idleTwitchSpeed) - 0.5f) * (bodyJitterIntensity * 1.5f);
            float noiseZ = Mathf.Sin(time * idleTwitchSpeed * 1.3f) * (bodyJitterIntensity * 0.8f);

            Quaternion chaoticRot = Quaternion.Euler(noiseX, noiseY, noiseZ);
            rootBody.localRotation = Quaternion.Slerp(rootBody.localRotation, rootInitialRot * chaoticRot, Time.deltaTime * 6f);
        }

        if (armShoulder != null)
        {
            float armNoiseZ = Mathf.Sin(time * 3.0f) * armJitterIntensity;
            float armNoiseX = (Mathf.PerlinNoise(time * 5.0f, 5.0f) - 0.5f) * (armJitterIntensity * 1.2f);
            armShoulder.localRotation = Quaternion.Slerp(armShoulder.localRotation, shoulderInitialRot * Quaternion.Euler(armNoiseX, 0, armNoiseZ), Time.deltaTime * 6f);
        }

        if (armUpper != null)
        {
            float upperTwitch = Mathf.Sin(time * 4.2f) * (armJitterIntensity * 0.7f);
            armUpper.localRotation = Quaternion.Slerp(armUpper.localRotation, upperInitialRot * Quaternion.Euler(0, upperTwitch, 0), Time.deltaTime * 6f);
        }

        if (isWalking)
        {
            Vector3 activeAxis = swingAxis;
            if (swingDirection == LegSwingAxis.LocalZ_Forward) activeAxis = new Vector3(0, 0, 1);
            else if (swingDirection == LegSwingAxis.LocalY_Forward) activeAxis = swingAxis;
            else if (swingDirection == LegSwingAxis.LocalX_Sideways) activeAxis = new Vector3(1, 0, 0);

            float stepL = Mathf.Sin(time * walkGaitSpeed) * legStepAngleMax;
            float stepR = Mathf.Sin((time * walkGaitSpeed) + Mathf.PI * 0.66f) * legStepAngleMax;
            float stepBack = Mathf.Sin((time * walkGaitSpeed) + Mathf.PI * 1.33f) * legStepAngleMax;

            float flexL = Mathf.Max(0, Mathf.Sin(time * walkGaitSpeed)) * (legStepAngleMax * 0.5f);
            float flexR = Mathf.Max(0, Mathf.Sin((time * walkGaitSpeed) + Mathf.PI * 0.66f)) * (legStepAngleMax * 0.5f);
            float flexBack = Mathf.Max(0, Mathf.Sin((time * walkGaitSpeed) + Mathf.PI * 1.33f)) * (legStepAngleMax * 0.5f);

            if (legHipL != null) legHipL.localRotation = hipLInitialRot * Quaternion.Euler(activeAxis * stepL);
            if (legHipR != null) legHipR.localRotation = hipRInitialRot * Quaternion.Euler(activeAxis * stepR);
            if (legHipBack != null) legHipBack.localRotation = hipBackInitialRot * Quaternion.Euler(activeAxis * stepBack);

            if (legFrontL != null) legFrontL.localRotation = legLInitialRot * Quaternion.Euler(activeAxis * -flexL);
            if (legFrontR != null) legFrontR.localRotation = legRInitialRot * Quaternion.Euler(activeAxis * -flexR);
            if (legBack != null) legBack.localRotation = legBackInitialRot * Quaternion.Euler(activeAxis * -flexBack);
        }
        else
        {
            if (legHipL != null) legHipL.localRotation = Quaternion.Slerp(legHipL.localRotation, hipLInitialRot, Time.deltaTime * 10f);
            if (legHipR != null) legHipR.localRotation = Quaternion.Slerp(legHipR.localRotation, hipRInitialRot, Time.deltaTime * 10f);
            if (legHipBack != null) legHipBack.localRotation = Quaternion.Slerp(legHipBack.localRotation, hipBackInitialRot, Time.deltaTime * 10f);

            if (legFrontL != null) legFrontL.localRotation = Quaternion.Slerp(legFrontL.localRotation, legLInitialRot, Time.deltaTime * 10f);
            if (legFrontR != null) legFrontR.localRotation = Quaternion.Slerp(legFrontR.localRotation, legRInitialRot, Time.deltaTime * 10f);
            if (legBack != null) legBack.localRotation = Quaternion.Slerp(legBack.localRotation, legBackInitialRot, Time.deltaTime * 10f);
        }
    }

    private void AnimateRightHookAttack()
    {
        attackTimer += Time.deltaTime * attackSpeed;
        float normTime = attackTimer / 1.3f; // Animação total em 1.3s

        if (normTime <= 0.35f)
        {
            if (bismutadoHitbox != null) bismutadoHitbox.DisableHitbox();
            if (fistTrailRenderer != null) fistTrailRenderer.emitting = false;

            float t = normTime / 0.35f;
            float smoothT = t * t;

            if (rootBody != null) rootBody.localRotation = Quaternion.Slerp(rootBody.localRotation, rootInitialRot * Quaternion.Euler(windupBodyEuler), smoothT);
            if (armShoulder != null) armShoulder.localRotation = Quaternion.Slerp(armShoulder.localRotation, shoulderInitialRot * Quaternion.Euler(windupShoulderEuler), smoothT);
            if (armUpper != null) armUpper.localRotation = Quaternion.Slerp(armUpper.localRotation, upperInitialRot * Quaternion.Euler(windupUpperArmEuler), smoothT);
            if (armForearm != null) armForearm.localRotation = Quaternion.Slerp(armForearm.localRotation, forearmInitialRot * Quaternion.Euler(windupForearmEuler), smoothT);
        }
        else if (normTime <= 0.65f)
        {
            if (bismutadoHitbox != null) bismutadoHitbox.EnableHitbox();
            if (fistTrailRenderer != null) fistTrailRenderer.emitting = true;

            float t = (normTime - 0.35f) / 0.30f;
            float punchT = Mathf.Sin(t * Mathf.PI * 0.5f);

            if (rootBody != null) rootBody.localRotation = Quaternion.Slerp(rootBody.localRotation, rootInitialRot * Quaternion.Euler(strikeBodyEuler), punchT);
            if (armShoulder != null) armShoulder.localRotation = Quaternion.Slerp(armShoulder.localRotation, shoulderInitialRot * Quaternion.Euler(strikeShoulderEuler), punchT);
            if (armUpper != null) armUpper.localRotation = Quaternion.Slerp(armUpper.localRotation, upperInitialRot * Quaternion.Euler(strikeUpperArmEuler), punchT);
            if (armForearm != null) armForearm.localRotation = Quaternion.Slerp(armForearm.localRotation, forearmInitialRot * Quaternion.Euler(strikeForearmEuler), punchT);
        }
        else if (normTime <= 1.0f)
        {
            if (bismutadoHitbox != null) bismutadoHitbox.DisableHitbox();
            if (fistTrailRenderer != null) fistTrailRenderer.emitting = false;

            float t = (normTime - 0.65f) / 0.35f;

            if (rootBody != null) rootBody.localRotation = Quaternion.Slerp(rootBody.localRotation, rootInitialRot, t * Time.deltaTime * 6f);
            if (armShoulder != null) armShoulder.localRotation = Quaternion.Slerp(armShoulder.localRotation, shoulderInitialRot, t * Time.deltaTime * 6f);
            if (armUpper != null) armUpper.localRotation = Quaternion.Slerp(armUpper.localRotation, upperInitialRot, t * Time.deltaTime * 6f);
            if (armForearm != null) armForearm.localRotation = Quaternion.Slerp(armForearm.localRotation, forearmInitialRot * Quaternion.Euler(strikeForearmEuler), t * Time.deltaTime * 6f);
        }
        else
        {
            if (bismutadoHitbox != null) bismutadoHitbox.DisableHitbox();
            if (fistTrailRenderer != null) fistTrailRenderer.emitting = false;
            isAttacking = false;
            attackTimer = 0f;
        }
    }

    public void TriggerCrystalSlam()
    {
        if (!isAttacking)
        {
            isAttacking = true;
            attackTimer = 0f;

            // Busca o Geobionte_AI no pai ou no próprio GameObject para disparar o som de ataque
            Geobionte_AI ai = GetComponentInParent<Geobionte_AI>() ?? GetComponent<Geobionte_AI>();
            if (ai != null)
            {
                ai.PlayBismutadoAttackSound();
            }
        }
        else
        {
            // Se já estava atacando, apenas reseta o timer
            attackTimer = 0f;
        }
    }
}
