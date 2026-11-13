using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animação procedural completa e orgânica para o Robozinho de Síntese da Nave.
/// Controla os ossos mapeados diretamente:
/// - Corpo: Bone_001 (Flutuação vertical suave, respiração e inclinação para o player)
/// - Aletas: Bone_010 e Bone_012 (Batimento ritmado de estabilização e propulsão)
/// - Perna Direita: Bone_015, Bone_016, Bone_017, Bone_018 (Inércia pendular articulada)
/// - Perna Esquerda: Bone_022, Bone_023, Bone_024, Bone_025 (Inércia pendular articulada)
/// 
/// Inclui o modo de celebração especial (Opção 1 + Bônus Extra):
/// Salto no ar, Giro 360° gracioso, abertura de aletas em pose de vitória,
/// pernas recolhidas com pouso elástico e flash de luz neon brilhante!
/// </summary>
public class RobozinhoProceduralAnimator : MonoBehaviour
{
    [Header("1. Ossos Mapeados (Auto-Detectados ou Arrastáveis)")]
    [Tooltip("Osso principal do corpo (Bone_001)")]
    public Transform bodyBone;

    [Tooltip("Aleta esquerda de estabilização (Bone_010)")]
    public Transform finLeftBone;

    [Tooltip("Aleta direita de estabilização (Bone_012)")]
    public Transform finRightBone;

    [Tooltip("Articulações da perna direita em cadeia (Bone_015, Bone_016, Bone_017, Bone_018)")]
    public List<Transform> rightLegBones = new List<Transform>();

    [Tooltip("Articulações da perna esquerda em cadeia (Bone_022, Bone_023, Bone_024, Bone_025)")]
    public List<Transform> leftLegBones = new List<Transform>();

    [Header("2. Configuração de Flutuação (Corpo)")]
    public float hoverSpeed = 2.4f;
    public float hoverAmplitude = 0.07f;
    public float bodyTiltAngle = 3.5f;

    [Header("3. Configuração das Aletas (Asas/Hélices)")]
    public float finIdleSpeed = 4.5f;
    public float finIdleAngle = 18f;
    public float finCraftingSpeed = 12f;
    public float finCraftingAngle = 30f;
    public Vector3 finRotationAxis = new Vector3(0f, 0f, 1f);

    [Header("4. Configuração das Pernas (Inércia / Pêndulo)")]
    public float legWaveSpeed = 2.4f;
    public float legWaveAngle = 8f;
    public float jointPhaseDelay = 0.45f;
    public Vector3 legCurlAxis = new Vector3(1f, 0f, 0f);

    [Header("5. Interação com Jogador")]
    public bool lookAtPlayer = true;
    public float playerDetectDistance = 3.5f;

    [Header("6. Bônus Extra: Luz de Comemoração no Craft")]
    [Tooltip("Luz do robô que pisca no momento do craft (auto-encontrada se nula).")]
    public Light robotLight;
    public Color craftLightColor = new Color(0.0f, 1.0f, 0.75f, 1.0f); // Verde Esmeralda / Ciano Neon
    public float craftLightIntensityMultiplier = 3.2f;
    private Color originalLightColor = Color.white;
    private float originalLightIntensity = 1.0f;

    [Header("7. Especial de Craft: Salto e Giro 360° (Opção 1)")]
    public float celebrationDuration = 1.35f;
    public float celebrationJumpHeight = 0.38f;
    public float celebrationSpinDegrees = 360f;
    public float celebrationFinFlareAngle = 42f;
    public float celebrationLegTuckAngle = 26f;

    // Estados internos de descanso
    private Vector3 bodyInitialLocalPos;
    private Quaternion bodyInitialLocalRot;
    private Quaternion finLeftInitialRot;
    private Quaternion finRightInitialRot;
    private List<Quaternion> rightLegInitialRots = new List<Quaternion>();
    private List<Quaternion> leftLegInitialRots = new List<Quaternion>();

    private Transform playerTransform;
    private bool isInitialized = false;
    private Coroutine celebrationCoroutine = null;
    private bool isCelebrating = false;

    private void Awake()
    {
        InitializeBones();
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        if (robotLight == null)
        {
            robotLight = GetComponentInChildren<Light>();
        }

        if (robotLight != null)
        {
            originalLightColor = robotLight.color;
            originalLightIntensity = robotLight.intensity;
        }
    }

    [ContextMenu("Auto-Mapear Ossos Pelos Nomes")]
    public void AutoMapBones()
    {
        // 1. Corpo (Bone_001)
        if (bodyBone == null) bodyBone = FindBoneRecursive(transform, "Bone_001", "Bone_1");

        // 2. Aletas (Bone_010 e Bone_012)
        if (finLeftBone == null) finLeftBone = FindBoneRecursive(transform, "Bone_010", "Bone_10");
        if (finRightBone == null) finRightBone = FindBoneRecursive(transform, "Bone_012", "Bone_12");

        // 3. Perna Direita (Bone_015, 016, 017, 018)
        if (rightLegBones.Count == 0)
        {
            string[] rBones = { "Bone_015", "Bone_016", "Bone_017", "Bone_018" };
            foreach (var bName in rBones)
            {
                var t = FindBoneRecursive(transform, bName);
                if (t != null) rightLegBones.Add(t);
            }
        }

        // 4. Perna Esquerda (Bone_022, 023, 024, 025)
        if (leftLegBones.Count == 0)
        {
            string[] lBones = { "Bone_022", "Bone_023", "Bone_024", "Bone_025" };
            foreach (var bName in lBones)
            {
                var t = FindBoneRecursive(transform, bName);
                if (t != null) leftLegBones.Add(t);
            }
        }

        if (robotLight == null)
        {
            robotLight = GetComponentInChildren<Light>();
        }
    }

    public void InitializeBones()
    {
        AutoMapBones();

        if (bodyBone != null)
        {
            bodyInitialLocalPos = bodyBone.localPosition;
            bodyInitialLocalRot = bodyBone.localRotation;
        }

        if (finLeftBone != null) finLeftInitialRot = finLeftBone.localRotation;
        if (finRightBone != null) finRightInitialRot = finRightBone.localRotation;

        rightLegInitialRots.Clear();
        for (int i = 0; i < rightLegBones.Count; i++)
        {
            if (rightLegBones[i] != null) rightLegInitialRots.Add(rightLegBones[i].localRotation);
        }

        leftLegInitialRots.Clear();
        for (int i = 0; i < leftLegBones.Count; i++)
        {
            if (leftLegBones[i] != null) leftLegInitialRots.Add(leftLegBones[i].localRotation);
        }

        isInitialized = true;
    }

    private Transform FindBoneRecursive(Transform parent, params string[] boneNames)
    {
        if (parent == null) return null;

        foreach (var bName in boneNames)
        {
            if (parent.name.Equals(bName, StringComparison.OrdinalIgnoreCase) ||
                parent.name.EndsWith(bName, StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindBoneRecursive(parent.GetChild(i), boneNames);
            if (found != null) return found;
        }

        return null;
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Se a celebração de craft estiver ativa, ela assume o controle do movimento
        if (isCelebrating) return;

        bool isCraftingOpen = CraftingUI.Instance != null && CraftingUI.Instance.IsOpen();
        float time = Time.time;

        // === 1. ANIMAÇÃO DO CORPO (FLUTUAÇÃO E RESPIRAÇÃO) ===
        if (bodyBone != null)
        {
            float speedMult = isCraftingOpen ? 1.6f : 1.0f;
            float hoverY = Mathf.Sin(time * hoverSpeed * speedMult) * hoverAmplitude;

            bodyBone.localPosition = bodyInitialLocalPos + new Vector3(0f, hoverY, 0f);

            // Leve balanço oscilatório (sway)
            float tiltZ = Mathf.Sin(time * (hoverSpeed * 0.7f)) * bodyTiltAngle;
            float tiltX = Mathf.Cos(time * hoverSpeed) * (bodyTiltAngle * 0.8f);

            Quaternion swayRot = Quaternion.Euler(tiltX, 0f, tiltZ);
            bodyBone.localRotation = bodyInitialLocalRot * swayRot;
        }

        // === 2. ANIMAÇÃO DAS ALETAS (BATIMENTO DE ESTABILIZAÇÃO) ===
        float curFinSpeed = isCraftingOpen ? finCraftingSpeed : finIdleSpeed;
        float curFinAngle = isCraftingOpen ? finCraftingAngle : finIdleAngle;
        float finFlap = Mathf.Sin(time * curFinSpeed) * curFinAngle;

        if (finLeftBone != null)
        {
            finLeftBone.localRotation = finLeftInitialRot * Quaternion.AngleAxis(finFlap, finRotationAxis);
        }

        if (finRightBone != null)
        {
            // Aleta oposta em espelho
            finRightBone.localRotation = finRightInitialRot * Quaternion.AngleAxis(-finFlap, finRotationAxis);
        }

        // === 3. ANIMAÇÃO DAS PERNAS ARTICULADAS (INÉRCIA PENDULAR) ===
        AnimateLegChain(rightLegBones, rightLegInitialRots, time, 0f, isCraftingOpen);
        AnimateLegChain(leftLegBones, leftLegInitialRots, time, 0.4f, isCraftingOpen);
    }

    private void AnimateLegChain(List<Transform> legBones, List<Quaternion> initialRots, float time, float phaseOffset, bool excited)
    {
        float speed = (hoverSpeed * (excited ? 1.5f : 1.0f));
        float baseAngle = excited ? (legWaveAngle * 1.5f) : legWaveAngle;

        for (int i = 0; i < legBones.Count; i++)
        {
            if (i >= initialRots.Count || legBones[i] == null) continue;

            float jointDelay = i * jointPhaseDelay;
            float legCurl = Mathf.Sin(time * speed - jointDelay + phaseOffset) * baseAngle;

            Quaternion curlRot = Quaternion.AngleAxis(legCurl, legCurlAxis);
            legBones[i].localRotation = initialRots[i] * curlRot;
        }
    }

    /// <summary>
    /// Dispara a comemoração especial de síntese: Salto no ar + Giro 360° + Flash de Luz!
    /// Chamado automaticamente pelo CraftingTableInteraction ao forjar qualquer item.
    /// </summary>
    [ContextMenu("★ Testar Comemoração (Giro 360 + Pulo + Luz)")]
    public void TriggerCraftReaction()
    {
        if (celebrationCoroutine != null)
        {
            StopCoroutine(celebrationCoroutine);
        }
        celebrationCoroutine = StartCoroutine(CelebrationSpinRoutine());
    }

    private IEnumerator CelebrationSpinRoutine()
    {
        isCelebrating = true;

        float elapsed = 0f;
        float duration = Mathf.Max(0.5f, celebrationDuration);

        // Flash de Luz Neon no Bônus Extra
        if (robotLight != null)
        {
            robotLight.color = craftLightColor;
            robotLight.intensity = originalLightIntensity * craftLightIntensityMultiplier;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 1. Curva do Salto com Pouso Amortecido
            float jumpY = 0f;
            if (t < 0.72f)
            {
                // Voo ascendente e descendente suave (parábola senoidal)
                float flightT = t / 0.72f;
                jumpY = Mathf.Sin(flightT * Mathf.PI) * celebrationJumpHeight;
            }
            else
            {
                // Pouso elástico: afunda levemente (-0.035m) e volta ao repouso
                float landT = (t - 0.72f) / 0.28f;
                jumpY = -Mathf.Sin(landT * Mathf.PI) * 0.035f;
            }

            // 2. Giro Suave de 360 Graus no Eixo Y
            float spinProgress = Mathf.Clamp01(t / 0.75f);
            float currentSpin = Mathf.SmoothStep(0f, celebrationSpinDegrees, spinProgress);

            if (bodyBone != null)
            {
                bodyBone.localPosition = bodyInitialLocalPos + new Vector3(0f, jumpY, 0f);
                bodyBone.localRotation = bodyInitialLocalRot * Quaternion.Euler(0f, currentSpin, 0f);
            }

            // 3. Aletas: Abrem amplamente no salto e batem felizes
            float finTuck = Mathf.Sin(t * Mathf.PI);
            float currentFinAngle = Mathf.Lerp(finIdleAngle, celebrationFinFlareAngle, finTuck);
            // Adiciona tremor de vibração energética durante o giro
            float flutter = Mathf.Sin(elapsed * 24f) * 8f;

            if (finLeftBone != null)
            {
                finLeftBone.localRotation = finLeftInitialRot * Quaternion.AngleAxis(currentFinAngle + flutter, finRotationAxis);
            }
            if (finRightBone != null)
            {
                finRightBone.localRotation = finRightInitialRot * Quaternion.AngleAxis(-(currentFinAngle + flutter), finRotationAxis);
            }

            // 4. Pernas Articuladas: Recolhem no ar (tuck) e esticam suavemente no pouso
            float legTuck = Mathf.Sin(t * Mathf.PI) * celebrationLegTuckAngle;
            for (int i = 0; i < rightLegBones.Count; i++)
            {
                if (i < rightLegInitialRots.Count && rightLegBones[i] != null)
                {
                    rightLegBones[i].localRotation = rightLegInitialRots[i] * Quaternion.AngleAxis(legTuck * (1f + i * 0.2f), legCurlAxis);
                }
            }
            for (int i = 0; i < leftLegBones.Count; i++)
            {
                if (i < leftLegInitialRots.Count && leftLegBones[i] != null)
                {
                    leftLegBones[i].localRotation = leftLegInitialRots[i] * Quaternion.AngleAxis(legTuck * (1f + i * 0.2f), legCurlAxis);
                }
            }

            // 5. Suavização da Luz Neon de volta ao normal
            if (robotLight != null)
            {
                float lightFadeT = Mathf.Clamp01(elapsed / (duration * 0.9f));
                robotLight.color = Color.Lerp(craftLightColor, originalLightColor, lightFadeT);
                robotLight.intensity = Mathf.Lerp(originalLightIntensity * craftLightIntensityMultiplier, originalLightIntensity, lightFadeT);
            }

            yield return null;
        }

        // Restauração Segura
        if (robotLight != null)
        {
            robotLight.color = originalLightColor;
            robotLight.intensity = originalLightIntensity;
        }

        if (bodyBone != null)
        {
            bodyBone.localPosition = bodyInitialLocalPos;
            bodyBone.localRotation = bodyInitialLocalRot;
        }

        isCelebrating = false;
        celebrationCoroutine = null;
    }
}
