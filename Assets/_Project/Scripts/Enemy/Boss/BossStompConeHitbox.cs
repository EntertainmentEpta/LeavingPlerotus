using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hitbox Progressivo e Área de Perigo dos Cristais para o VFX do Stomp (Pisada do Boss).
/// 1. Onda Expansiva (0.0s a 0.6s): O dano cresce e viaja pelo chão acompanhando a erupção visual de cristais (até 11 metros).
/// 2. Zona de Perigo Persistente (0.6s a 2.6s): Os cristais permanecem no chão causando dano a quem pisar neles.
/// </summary>
public class BossStompConeHitbox : MonoBehaviour
{
    [Header("⚔️ Dano do Impacto Inicial (Onda Expansiva)")]
    [Tooltip("Dano causado ao ser atingido pela onda de cristais em expansão.")]
    public int damage = 40;

    [Tooltip("Força de empurrão (knockback) do impacto inicial.")]
    public float knockbackForce = 10.0f;

    [Tooltip("Elevação vertical do knockback.")]
    public float knockbackUpwardRatio = 0.35f;

    [Header("💎 Dano Contínuo dos Cristais no Chão (Lingering Hazard)")]
    [Tooltip("Duração que os cristais permanecem ativos no solo causando dano a quem pisar.")]
    public float hazardDuration = 2.0f;

    [Tooltip("Dano por tick ao permanecer em cima dos cristais.")]
    public int hazardTickDamage = 12;

    [Tooltip("Intervalo entre ticks de dano dos cristais (em segundos).")]
    public float hazardTickInterval = 0.45f;

    [Header("📐 Geometria do Cone")]
    [Tooltip("Alcance máximo frontal da onda de cristais no chão (em metros).")]
    public float maxDistance = 11.0f;

    [Tooltip("Ângulo total de abertura do cone (em graus, ex: 80°).")]
    [Range(20f, 180f)]
    public float coneAngle = 80f;

    [Tooltip("Tempo total de expansão da onda no solo (em segundos, sincronizado com o VFX).")]
    public float waveDuration = 0.60f;

    [Tooltip("Largura da faixa da frente de onda ativa (em metros).")]
    public float waveFrontThickness = 2.0f;

    [Header("🎯 Detecção")]
    [Tooltip("LayerMask do Jogador para otimização de colisão.")]
    public LayerMask targetLayers = ~0;

    [Header("Debug")]
    public bool showDebugLogs = true;
    public bool drawGizmos = true;

    private HashSet<GameObject> burstHitTargets = new HashSet<GameObject>();
    private Dictionary<GameObject, float> hazardDamageCooldowns = new Dictionary<GameObject, float>();
    private Coroutine activeWaveRoutine;
    private GameObject bossOwner;

    private void Awake()
    {
        FindBossOwner();
    }

    private void OnEnable()
    {
        FindBossOwner();
        StartWave();
    }

    private void OnDisable()
    {
        if (activeWaveRoutine != null)
        {
            StopCoroutine(activeWaveRoutine);
            activeWaveRoutine = null;
        }
        burstHitTargets.Clear();
        hazardDamageCooldowns.Clear();
    }

    private void FindBossOwner()
    {
        if (bossOwner == null)
        {
            BossController boss = GetComponentInParent<BossController>();
            if (boss != null) bossOwner = boss.gameObject;
            else if (transform.root != null) bossOwner = transform.root.gameObject;
            else bossOwner = gameObject;
        }
    }

    /// <summary>
    /// Inicia a expansão progressiva e a zona de cristais persistente
    /// </summary>
    public void StartWave(GameObject customOwner = null)
    {
        if (customOwner != null) bossOwner = customOwner;
        else FindBossOwner();

        if (activeWaveRoutine != null) StopCoroutine(activeWaveRoutine);
        activeWaveRoutine = StartCoroutine(ConeWaveRoutine());
    }

    private Vector3 GetEffectiveForwardDirection()
    {
        // Sempre utiliza o forward do Boss (Owner) para ignorar qualquer rotação local do prefab do VFX!
        Vector3 forwardDir = (bossOwner != null ? bossOwner.transform.forward : transform.forward);
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;
        return forwardDir.normalized;
    }

    private Vector3 GetEffectiveOrigin()
    {
        Vector3 origin = (bossOwner != null ? bossOwner.transform.position : transform.position);
        origin.y = transform.position.y;
        return origin;
    }

    private IEnumerator ConeWaveRoutine()
    {
        burstHitTargets.Clear();
        hazardDamageCooldowns.Clear();
        float timer = 0f;
        float totalRoutineDuration = waveDuration + hazardDuration;

        Vector3 origin = GetEffectiveOrigin();
        Vector3 forwardDir = GetEffectiveForwardDirection();

        if (showDebugLogs)
            Debug.Log($"🦶 [STOMP VFX HITBOX] Iniciando onda de cristais em cone: Alcance: {maxDistance}m, Ângulo: {coneAngle}°, Duração: {totalRoutineDuration:F1}s");

        // Origem e Direção congelados no World Space no momento exato do impacto (imune a rotação/movimento do Boss)
        origin = GetEffectiveOrigin();
        forwardDir = GetEffectiveForwardDirection();

        while (timer < totalRoutineDuration)
        {
            timer += Time.deltaTime;

            bool isExpandingWave = (timer <= waveDuration);
            float currentProgress = isExpandingWave ? Mathf.Clamp01(timer / waveDuration) : 1.0f;
            float currentWaveRadius = currentProgress * maxDistance;

            // Busca colisores na área coberta
            Collider[] colliders = Physics.OverlapSphere(origin, isExpandingWave ? (currentWaveRadius + 1.0f) : (maxDistance + 1.0f), targetLayers);

            foreach (Collider col in colliders)
            {
                if (col == null) continue;
                if (!col.CompareTag("Player") && !col.name.ToLower().Contains("player")) continue;

                GameObject targetObj = col.gameObject;

                // Calcula distância e ângulo em relação à frente do Boss
                Vector3 toTarget = col.transform.position - origin;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist > maxDistance + 0.5f) continue;

                Vector3 dirToTarget = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : forwardDir;
                float angle = Vector3.Angle(forwardDir, dirToTarget);

                if (angle > (coneAngle * 0.5f)) continue;

                // --- 1. FASE DE IMPACTO DA ONDA EXPANSIVA ---
                if (isExpandingWave && !burstHitTargets.Contains(targetObj))
                {
                    // Qualquer alvo dentro da área varrida da frente de onda recebe o impacto direto
                    if (dist <= currentWaveRadius + 1.0f)
                    {
                        burstHitTargets.Add(targetObj);
                        ApplyBurstDamageAndKnockback(col, origin);
                    }
                }
                // --- 2. FASE DOS CRISTAIS PERSISTENTES NO CHÃO (LINGERING HAZARD) ---
                else
                {
                    // Dano por tick para quem pisar ou ficar em cima dos cristais
                    float nextAllowedTime = 0f;
                    hazardDamageCooldowns.TryGetValue(targetObj, out nextAllowedTime);

                    if (Time.time >= nextAllowedTime)
                    {
                        hazardDamageCooldowns[targetObj] = Time.time + hazardTickInterval;
                        ApplyHazardTickDamage(col);
                    }
                }
            }

            yield return null;
        }

        activeWaveRoutine = null;
    }

    private void ApplyBurstDamageAndKnockback(Collider col, Vector3 origin)
    {
        // 1. Dano Burst Inicial
        PlayerHealth ph = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(damage, bossOwner);
            if (showDebugLogs) Debug.Log($"💥 [STOMP CONE] Jogador atingido pela ERUPÇÃO DE CRISTAIS! Dano: {damage}");
        }

        // 2. Knockback
        Rigidbody rb = col.GetComponent<Rigidbody>() ?? col.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            Vector3 pushDir = (col.transform.position - origin);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.001f) pushDir = GetEffectiveForwardDirection();
            pushDir = (pushDir.normalized + Vector3.up * knockbackUpwardRatio).normalized;

            rb.linearVelocity = Vector3.zero;
            rb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
        }

        // Camera Shake
        BossController.TriggerCameraShake(0.40f, 0.25f);
    }

    private void ApplyHazardTickDamage(Collider col)
    {
        PlayerHealth ph = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(hazardTickDamage, bossOwner);
            if (showDebugLogs) Debug.Log($"💎 [CRISTAIS NO CHÃO] Jogador pisando nos cristais ativos! Dano contínuo: {hazardTickDamage}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0f, 0.9f, 1f, 0.45f);
        Vector3 origin = GetEffectiveOrigin();
        Vector3 forward = GetEffectiveForwardDirection();

        Quaternion leftRot = Quaternion.Euler(0, -coneAngle * 0.5f, 0);
        Quaternion rightRot = Quaternion.Euler(0, coneAngle * 0.5f, 0);

        Vector3 leftDir = leftRot * forward;
        Vector3 rightDir = rightRot * forward;

        Gizmos.DrawLine(origin, origin + leftDir * maxDistance);
        Gizmos.DrawLine(origin, origin + rightDir * maxDistance);
        Gizmos.DrawWireSphere(origin + forward * maxDistance, 1.0f);
    }
}
