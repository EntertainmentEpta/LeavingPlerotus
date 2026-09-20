using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador da Invisibilidade do Boss Cromatico - Fase 2
///
/// COMPORTAMENTO:
///   1. Ativa APENAS durante a Fase 2, nos limiares de HP configurados no Inspector
///   2. Quando invisivel: o boss foge do player, spawna mobs e invoca pilares/prisoes
///   3. Cancelada automaticamente apos o tempo configurado OU se o player acertar o boss
///   4. O boss fica VULNERAVEL durante a invisibilidade (pode tomar dano)
///
/// INSPECTOR:
///   - refractionThresholds: porcentagens de HP que ativam a invisibilidade
///   - maxRefractionUses: quantas vezes o boss pode ficar invisivel
/// </summary>
[RequireComponent(typeof(BossController))]
[AddComponentMenu("Boss/BossPhase2_Refraction")]
public class BossPhase2_Refraction : MonoBehaviour
{
    // -- Referencias
    private BossController bossController;
    private DummyHealth health;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private Renderer[] renderers;
    private List<MaterialData> originalMaterialData = new List<MaterialData>();

    // -- Configuracao (Inspector)
    [Header("Invisibilidade -- Configuracao")]
    [Tooltip("Numero maximo de vezes que o boss pode ficar invisivel durante a Fase 2")]
    public int maxRefractionUses = 1;

    [Tooltip("Duração máxima em segundos da invisibilidade antes de reaparecer automaticamente")]
    public float refractionDuration = 6.0f;

    [Tooltip("Tempo para o boss sumir (fade out)")]
    public float fadeOutTime = 0.4f;

    [Tooltip("Tempo para o boss reaparecer (fade in)")]
    public float fadeInTime = 0.7f;

    [Header("Limiares de HP para Ativar Invisibilidade")]
    [Tooltip("Porcentagens de HP (0.0 a 1.0) nas quais a invisibilidade e ativada. Ex: 0.55 = 55% de HP total.")]
    public float[] refractionThresholds = { 0.55f };

    [Tooltip("Distancia que o boss tenta fugir do player durante a invisibilidade")]
    public float fleeDistance = 12f;

    [Tooltip("Com qual frequencia (segundos) o boss tenta se reposicionar enquanto invisivel")]
    public float repositionInterval = 2.5f;

    [Header("Ataques durante Invisibilidade")]
    [Tooltip("Com qual frequencia (segundos) o boss spawna mobs aceleradamente enquanto invisivel")]
    public float mobSpawnInterval = 2.5f;

    [Tooltip("Com qual frequencia (segundos) o boss invoca pilares enquanto invisivel")]
    public float pilarSpawnInterval = 5f;

    [Header("Efeito Visual")]
    [Tooltip("Cor do brilho na borda durante invisibilidade")]
    public Color refractionGlowColor = new Color(0.5f, 0.8f, 1f, 0.3f);

    [Header("Debug")]
    public bool showDebugLog = true;

    // -- Estado Interno
    private int refractionUsesRemaining;
    private int nextThresholdIndex = 0;
    private bool isRefracting = false;
    public bool IsRefracting => isRefracting;
    private bool isPhase2Active = false;
    private Coroutine refractionCoroutine;
    private float originalAgentSpeed = -1f;

    // -- Cache para restaurar materiais
    private struct MaterialData
    {
        public Material material;
        public Color originalColor;
        public Color originalBaseColor;
        public bool hasColor;
        public bool hasBaseColor;
        public float originalAlpha;
        public int originalRenderQueue;
        public Shader originalShader;
    }

    void Awake()
    {
        bossController = GetComponent<BossController>();
        health = GetComponent<DummyHealth>();
        agent = GetComponent<NavMeshAgent>();
    }

    void OnEnable()
    {
        BossEvents.OnPhaseChanged += OnPhaseChanged;
        BossEvents.OnBossFightStarted += OnFightStarted;
        BossEvents.OnBossDefeated += OnBossDefeated;
        if (bossController != null) bossController.OnTookDamage += OnTookDamage;
    }

    void OnDisable()
    {
        BossEvents.OnPhaseChanged -= OnPhaseChanged;
        BossEvents.OnBossFightStarted -= OnFightStarted;
        BossEvents.OnBossDefeated -= OnBossDefeated;
        if (bossController != null) bossController.OnTookDamage -= OnTookDamage;
    }

    void Start()
    {
        refractionUsesRemaining = maxRefractionUses;
        renderers = GetComponentsInChildren<Renderer>();
        CacheOriginalMaterials();

        if (bossController != null)
        {
            bossController.OnTookDamage -= OnTookDamage;
            bossController.OnTookDamage += OnTookDamage;
        }
    }

    private float visiblePilarTimer = 2.0f;

    void Update()
    {
        if (bossController == null) bossController = GetComponent<BossController>();
        if (bossController == null || bossController.IsDead || bossController.IsStunned) return;

        if (isPhase2Active)
        {
            if (!isRefracting)
            {
                CheckRefractionThreshold();

                // Enquanto visível na Fase 2, invoca magias e combos apenas quando estiver LIVRE (sem sobreposição) e sem IA congelada
                if (bossController != null && bossController.CanInitiateAction && !bossController.OverrideMovement)
                {
                    visiblePilarTimer -= Time.deltaTime;
                    if (visiblePilarTimer <= 0f)
                    {
                        visiblePilarTimer = 5.0f;
                        float rnd = UnityEngine.Random.value;
                        if (rnd < 0.20f && bossController != null)
                        {
                            bossController.PerformBossSpellWide(); // Super Ataque Raro 360° de Espinhos!
                        }
                        else if (rnd < 0.50f && bossController != null)
                        {
                            bossController.ExecuteTrapAndStompCombo(); // Combo Inteligente: Prende e Pisa!
                        }
                        else if (rnd < 0.75f && bossController != null)
                        {
                            bossController.PerformGolemStunCast(); // Stun do Céu no Player!
                        }
                        else
                        {
                            BossPhase1_MestreDoSolo mestre = GetComponent<BossPhase1_MestreDoSolo>();
                            if (mestre != null && !mestre.ExistemPilaresNaCena()) mestre.InvocarPrisaoForado();
                        }
                    }
                }
            }
        }
    }

    void OnFightStarted()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void OnPhaseChanged(int newPhase)
    {
        if (newPhase == 2)
        {
            isPhase2Active = true;
            nextThresholdIndex = 0;
            refractionUsesRemaining = maxRefractionUses;
            if (isRefracting) CancelRefraction();
            if (showDebugLog) Debug.Log("[BossPhase2] Fase 2 ATIVA -- Boss visivel, lutando normalmente ate o threshold de refração!");
        }
        else
        {
            isPhase2Active = false;
            if (isRefracting) CancelRefraction();
            if (showDebugLog) Debug.Log("[BossPhase2] Fase 2 DESATIVADA.");
        }
    }

    void OnBossDefeated()
    {
        if (isRefracting) CancelRefraction();
        isPhase2Active = false;
    }

    void CheckRefractionThreshold()
    {
        if (refractionUsesRemaining <= 0) return;
        if (nextThresholdIndex >= refractionThresholds.Length) return;
        if (health == null) return;

        float healthPercent = bossController.HealthPercent;
        float currentThreshold = refractionThresholds[nextThresholdIndex];

        if (healthPercent <= currentThreshold)
        {
            nextThresholdIndex++;
            refractionUsesRemaining--;
            ActivateRefraction();
        }
    }

    void ActivateRefraction()
    {
        if (isRefracting) return;
        refractionCoroutine = StartCoroutine(RefractionRoutine());

        int useNumber = maxRefractionUses - refractionUsesRemaining;
        if (showDebugLog)
            Debug.Log(string.Format("[BossPhase2] Invisibilidade ATIVADA! (uso {0}/{1}, HP: {2}%)", useNumber, maxRefractionUses, (int)(bossController.HealthPercent * 100)));
    }

    private void OnTookDamage()
    {
        if (!isRefracting) return;
        if (showDebugLog) Debug.Log("[BossPhase2] Boss atingido durante invisibilidade! REVELADO!");
        CancelRefraction();
    }

    void CancelRefraction()
    {
        if (refractionCoroutine != null)
        {
            StopCoroutine(refractionCoroutine);
            refractionCoroutine = null;
        }

        isRefracting = false;
        bossController.SetRefraction(false);

        RestoreOriginalMaterials();
        SetMaterialsTransparent(false);
        SetRenderersVisibility(true);

        if (agent != null && agent.enabled)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            if (agent.isOnNavMesh && bossController != null && bossController.CanInitiateAction) agent.isStopped = false;
            if (originalAgentSpeed > 0f) agent.speed = originalAgentSpeed;
        }
    }

    IEnumerator RefractionRoutine()
    {
        isRefracting = true;
        bossController.SetRefraction(true);

        SetRenderersVisibility(false);

        BossPhase1_MobSpawner mobSpawner = GetComponent<BossPhase1_MobSpawner>();
        BossPhase1_MestreDoSolo mestre = GetComponent<BossPhase1_MestreDoSolo>();

        if (agent != null && agent.enabled)
        {
            originalAgentSpeed = agent.speed;
            agent.speed = originalAgentSpeed * 1.35f;
        }

        float nextMobSpawn = mobSpawnInterval;
        float nextPilarSpawn = pilarSpawnInterval;
        float nextReposition = 0.5f;
        float remainingDuration = refractionDuration;

        // Duração máxima de refração (ex: 6 segundos). Ao acabar OU tomar dano, o boss é revelado!
        while (isRefracting && remainingDuration > 0f)
        {
            remainingDuration -= Time.deltaTime;
            nextMobSpawn -= Time.deltaTime;
            nextPilarSpawn -= Time.deltaTime;
            nextReposition -= Time.deltaTime;

            if (nextReposition <= 0f)
            {
                nextReposition = repositionInterval;
                if (bossController != null && bossController.CanInitiateAction)
                    StartCoroutine(FleeRoutine());
            }

            if (nextMobSpawn <= 0f)
            {
                nextMobSpawn = mobSpawnInterval;
                if (mobSpawner != null)
                    mobSpawner.SpawnWave(BossPhase1_MobSpawner.WaveType.CounterAttack);
            }

            if (nextPilarSpawn <= 0f && (bossController == null || bossController.CanInitiateAction))
            {
                nextPilarSpawn = pilarSpawnInterval;
                float rnd = UnityEngine.Random.value;
                if (rnd < 0.20f && bossController != null)
                {
                    bossController.PerformBossSpellWide();
                }
                else if (rnd < 0.60f && bossController != null)
                {
                    bossController.PerformGolemStunCast();
                }
                else if (mestre != null && !mestre.Atacando && !mestre.ExistemPilaresNaCena())
                {
                    mestre.InvocarPrisaoForado();
                }
            }

            yield return null;
        }

        if (isRefracting)
        {
            if (showDebugLog) Debug.Log("[BossPhase2] Tempo de refração esgotado! Boss reaparece para combate!");
            CancelRefraction();
        }
    }

    IEnumerator FleeRoutine()
    {
        // Se o Boss estiver ocupado conjurando feitiço ou executando golpe, NÃO interrompe nem desliza!
        if (bossController != null && !bossController.CanInitiateAction) yield break;
        if (playerTransform == null) yield break;

        Vector3 dirAway = (transform.position - playerTransform.position).normalized;
        Vector3 targetPos = transform.position + dirAway * fleeDistance;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 4.0f, NavMesh.AllAreas))
            targetPos = navHit.position;

        if (agent != null && agent.enabled && agent.isOnNavMesh && bossController != null && bossController.CanInitiateAction)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);
        }

        yield return null;
    }

    public void SetTemporaryVisibility(bool tempVisible)
    {
        SetRenderersVisibility(tempVisible);
    }

    private void SetRenderersVisibility(bool visible)
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            if (rend is ParticleSystemRenderer) continue;
            if (rend.gameObject == gameObject) continue; // Ignora o MeshRenderer esférico legado na raiz do Boss!
            if (rend is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            rend.enabled = visible;
        }
    }

    void SetMaterialAlpha(MaterialData data, float alpha)
    {
        if (data.material == null) return;
        if (data.hasColor) { Color c = data.material.color; c.a = alpha; data.material.color = c; }
        if (data.hasBaseColor) { Color bc = data.material.GetColor("_BaseColor"); bc.a = alpha; data.material.SetColor("_BaseColor", bc); }
    }

    void CacheOriginalMaterials()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        originalMaterialData.Clear();
        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            foreach (Material mat in rend.materials)
            {
                if (mat == null) continue;
                MaterialData data = new MaterialData
                {
                    material = mat,
                    hasColor = mat.HasProperty("_Color"),
                    hasBaseColor = mat.HasProperty("_BaseColor"),
                    originalRenderQueue = mat.renderQueue,
                    originalShader = mat.shader,
                };
                if (data.hasColor) { data.originalColor = mat.color; data.originalAlpha = mat.color.a; }
                if (data.hasBaseColor) { data.originalBaseColor = mat.GetColor("_BaseColor"); if (!data.hasColor) data.originalAlpha = data.originalBaseColor.a; }
                originalMaterialData.Add(data);
            }
        }
    }

    void SetMaterialsTransparent(bool transparent)
    {
        foreach (MaterialData data in originalMaterialData)
        {
            if (data.material == null) continue;
            if (transparent)
            {
                data.material.SetFloat("_Surface", 1); data.material.SetFloat("_Blend", 0);
                data.material.SetOverrideTag("RenderType", "Transparent"); data.material.renderQueue = 3000;
                data.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                data.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                data.material.SetInt("_ZWrite", 1); data.material.EnableKeyword("_ALPHABLEND_ON");
                data.material.DisableKeyword("_ALPHATEST_ON"); data.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                data.material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                data.material.shader = data.originalShader; data.material.renderQueue = data.originalRenderQueue;
                data.material.SetOverrideTag("RenderType", "Opaque");
                data.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                data.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                data.material.SetInt("_ZWrite", 1); data.material.DisableKeyword("_ALPHABLEND_ON");
                data.material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); data.material.SetFloat("_Surface", 0);
                if (data.material.HasProperty("_EmissionColor")) { data.material.SetColor("_EmissionColor", Color.black); data.material.DisableKeyword("_EMISSION"); }
            }
        }
    }

    void RestoreOriginalMaterials()
    {
        foreach (MaterialData data in originalMaterialData)
        {
            if (data.material == null) continue;
            if (data.hasColor) data.material.color = data.originalColor;
            data.material.renderQueue = data.originalRenderQueue;
            data.material.shader = data.originalShader;
            if (data.hasBaseColor) data.material.SetColor("_BaseColor", data.originalBaseColor);
            if (data.material.HasProperty("_EmissionColor")) { data.material.SetColor("_EmissionColor", Color.black); data.material.DisableKeyword("_EMISSION"); }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
    }
#endif
}
