using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Habilidade Ultimate da Pistola (Ressonadora / Plasma Gun).
/// Nome Canônico Recomendado: "Sobrecarga Ressonante" (Resonant Overdrive).
/// 
/// Mecânica de Combate:
/// 1. EMP Shockwave (360°): Emite um pulso eletromagnético radial que empurra (Knockback) e aplica lentidão aos inimigos ao redor.
/// 2. Salva Concentrada de Plasma (Plasma Barrage): Dispara uma rajada em leque de projéteis de plasma teleguiados de altíssimo dano com 100% crítico garantido e múltiplos ricochetes.
/// 3. Overdrive de Bateria: Recarrega instantaneamente a bateria de plasma ao máximo e concede disparos ilimitados por alguns segundos.
/// </summary>
public class Ultimate_Gun : MonoBehaviour
{
    [Header("Identidade da Habilidade")]
    [Tooltip("Nome canônico da Ultimate para exibição na UI e logs.")]
    public string ultimateName = "Sobrecarga Ressonante";

    [Header("💥 Pulso Eletromagnético (EMP Shockwave)")]
    [Tooltip("Raio do pulso de choque EMP que afasta inimigos ao redor.")]
    public float empRadius = 6.5f;

    [Tooltip("Dano instantâneo causado pelo pulso EMP.")]
    public int empDamage = 95;

    [Tooltip("Força com que os inimigos são empurrados para longe do jogador.")]
    public float empKnockbackForce = 12.0f;

    [Tooltip("Percentual de lentidão aplicado aos inimigos pegos no pulso (0..1).")]
    public float empSlowStrength = 0.60f;

    [Tooltip("Duração da lentidão aplicada pelo EMP.")]
    public float empSlowDuration = 3.0f;

    [Header("⚡ Salva Concentrada de Plasma (Plasma Barrage)")]
    [Tooltip("Quantidade de projéteis disparados na salva da Ultimate.")]
    public int barrageProjectileCount = 14;

    [Tooltip("Abertura do cone de disparo em graus.")]
    public float barrageConeAngle = 55f;

    [Tooltip("Multiplicador de dano aplicado aos projéteis da salva.")]
    public float projectileDamageMultiplier = 2.5f;

    [Tooltip("Velocidade com que os projéteis de plasma da salva viajam.")]
    public float barrageProjectileSpeed = 24.0f;

    [Tooltip("Distância máxima que os projéteis viajam.")]
    public float barrageRange = 22.0f;

    [Tooltip("Quantidade de perfurações (Piercing) dos tiros da salva.")]
    public int barragePiercing = 3;

    [Tooltip("Quantidade de ricochetes garantidos por tiro.")]
    public int barrageBounceCount = 3;

    [Header("🔋 Overdrive de Bateria")]
    [Tooltip("Duração em segundos onde os tiros convencionais não consomem bateria após a Ultimate.")]
    public float infiniteBatteryDuration = 3.5f;

    [Header("✨ Efeitos Visuais e Sonoros")]
    [Tooltip("Prefab opcional de VFX da onda de choque / explosão de plasma.")]
    public GameObject empVFXPrefab;

    [Tooltip("Som da ativação do pulso eletromagnético / sobrecarga.")]
    public AudioClip empSoundClip;

    [Tooltip("Volume do efeito sonoro (0..1).")]
    [Range(0f, 1f)] public float soundVolume = 0.85f;

    private bool isUltActive = false;
    private PlayerUltimate playerUltimateRef;
    private GunAttack gunAttackRef;

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        playerUltimateRef = GetComponentInParent<PlayerUltimate>() ?? GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        gunAttackRef = GetComponentInParent<GunAttack>() ?? GetComponentInChildren<GunAttack>() ?? FindFirstObjectByType<GunAttack>();
    }

    /// <summary>
    /// Chamado pelo script PlayerUltimate.cs quando o jogador aperta 'U' com a Pistola equipada.
    /// </summary>
    public void ExecuteUltimate()
    {
        if (isUltActive) return;
        isUltActive = true;
        CacheReferences();

        Debug.Log($"[Ultimate_Gun] ★ Ativando '{ultimateName}'!");
        StartCoroutine(ExecuteUltimateSequence());
    }

    // Suporte para Animation Events
    public void OnGunUlt() => TriggerImpact();
    public void ongunult() => TriggerImpact();
    public void OnShoot() => TriggerImpact();

    private IEnumerator ExecuteUltimateSequence()
    {
        // Breve windup cinematográfico de 0.12s para sincronizar com a animação
        yield return new WaitForSeconds(0.12f);
        TriggerImpact();
    }

    /// <summary>
    /// Dispara o impacto: pulso EMP radial, salva em leque de plasma e overdrive de bateria.
    /// </summary>
    public void TriggerImpact()
    {
        if (!isUltActive) return;
        CacheReferences();

        Vector3 centerPos = transform.position + Vector3.up * 0.9f;
        Vector3 forwardDir = GetAimDirection();

        // 1. Pulso Eletromagnético (EMP Shockwave)
        ExecuteEmpShockwave(centerPos);

        // 2. Salva Concentrada de Plasma (Plasma Barrage)
        ExecutePlasmaBarrage(centerPos, forwardDir);

        // 3. Recarga Completa e Overdrive de Bateria Infinita
        if (gunAttackRef != null)
        {
            gunAttackRef.ActivateInfiniteBattery(infiniteBatteryDuration);
        }

        // Toca som caso atribuído
        if (empSoundClip != null)
        {
            AudioSource.PlayClipAtPoint(empSoundClip, centerPos, soundVolume);
        }

        isUltActive = false;
    }

    private void ExecuteEmpShockwave(Vector3 center)
    {
        if (empVFXPrefab != null)
        {
            Instantiate(empVFXPrefab, center, Quaternion.identity);
        }

        Collider[] colliders = Physics.OverlapSphere(center, empRadius);
        HashSet<DummyHealth> hitEnemies = new HashSet<DummyHealth>();

        foreach (var c in colliders)
        {
            if (c == null) continue;
            DummyHealth enemy = c.GetComponentInParent<DummyHealth>();
            if (enemy != null && !hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);

                // Aplica dano elétrico da sobrecarga
                enemy.TakeDamage(empDamage, isCritical: true, isHeavyStun: true, bypassArmored: false);

                // Aplica Lentidão
                if (empSlowStrength > 0f)
                {
                    enemy.ApplySlow(empSlowStrength, empSlowDuration);
                }

                // Aplica Knockback Radial
                Rigidbody enemyRb = enemy.GetComponent<Rigidbody>() ?? enemy.GetComponentInParent<Rigidbody>();
                if (enemyRb != null && !enemyRb.isKinematic)
                {
                    Vector3 pushDir = Vector3.ProjectOnPlane(enemy.transform.position - center, Vector3.up).normalized;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = transform.forward;
                    float effectiveKb = empKnockbackForce * Mathf.Clamp01(1f - enemy.knockbackResistance);
                    enemyRb.AddForce(pushDir * effectiveKb, ForceMode.Impulse);
                }
            }
        }
    }

    private void ExecutePlasmaBarrage(Vector3 origin, Vector3 forwardDir)
    {
        if (gunAttackRef == null || gunAttackRef.projectilePrefab == null) return;

        PlayerAttributesOffensive stats = PlayerAttributesOffensive.Instance;
        float baseDmg = gunAttackRef.baseDamage * (stats != null ? stats.baseDamageMultiplier : 1f);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDmg * projectileDamageMultiplier));

        float critMult = stats != null ? Mathf.Max(1.5f, stats.critMultiplier) : 1.75f;
        float kb = stats != null ? Mathf.Max(1f, stats.knockback) : 1.5f;

        Transform muzzleTr = gunAttackRef.muzzle != null ? gunAttackRef.muzzle : transform;
        Vector3 spawnPos = muzzleTr.position + forwardDir * 0.4f;

        float startAngle = -barrageConeAngle * 0.5f;
        float angleStep = barrageProjectileCount > 1 ? barrageConeAngle / (barrageProjectileCount - 1) : 0f;

        for (int i = 0; i < barrageProjectileCount; i++)
        {
            float curAngle = startAngle + (angleStep * i);
            Vector3 shotDir = Quaternion.Euler(0f, curAngle, 0f) * forwardDir;

            GunProjectile proj = Instantiate(gunAttackRef.projectilePrefab, spawnPos, Quaternion.LookRotation(shotDir, Vector3.up));
            if (proj != null)
            {
                proj.speed = barrageProjectileSpeed;
                proj.Initialize(
                    projectileDamage: finalDamage,
                    range: barrageRange,
                    piercing: barragePiercing,
                    owner: transform.root,
                    critChance: 100f, // Crítico garantido em todos os tiros da Ultimate
                    critMultiplier: critMult,
                    projectileKnockback: kb,
                    projectileBounceChance: 100f,
                    projectileBounceCount: barrageBounceCount,
                    projectileSlowOnHit: empSlowStrength,
                    enableHoming: true, // Projéteis da Ultimate são teleguiados
                    detectionRadius: 16f,
                    turnSpeed: 180f,
                    targetRefreshInterval: 0.08f
                );
            }
        }

        if (gunAttackRef.muzzleFlashVFX != null)
        {
            gunAttackRef.muzzleFlashVFX.Play(true);
        }
    }

    private Vector3 GetAimDirection()
    {
        if (AimSettings.IsMouseAim && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 hitPt = ray.GetPoint(enter);
                Vector3 dir = Vector3.ProjectOnPlane(hitPt - transform.position, Vector3.up).normalized;
                if (dir.sqrMagnitude > 0.001f) return dir;
            }
        }

        return Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, empRadius);

        Gizmos.color = Color.cyan;
        Vector3 fwd = transform.forward;
        Vector3 left = Quaternion.Euler(0f, -barrageConeAngle * 0.5f, 0f) * fwd;
        Vector3 right = Quaternion.Euler(0f, barrageConeAngle * 0.5f, 0f) * fwd;
        Gizmos.DrawRay(transform.position, left * barrageRange);
        Gizmos.DrawRay(transform.position, right * barrageRange);
    }
}
