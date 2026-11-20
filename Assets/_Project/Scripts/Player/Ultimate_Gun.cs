using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Habilidade Ultimate da Pistola (Ressonadora / Gun).
/// Estilo "O Expurgo" / R do Lucian (The Culling):
/// 
/// 🔹 Mecânica:
/// - Ao pressionar a tecla [U], a direção do disparo é travada exatamente para onde o jogador está mirando.
/// - O jogador pode se mover livremente em qualquer direção (360° / WASD / Strafe) enquanto a rajada dispara.
/// - Rajada contínua e rápida de múltiplos tiros de plasma luminosos e incandescentes.
/// - Os disparos NÃO atravessam (Piercing = 0) e NÃO ricocheteiam: excelente para focar dano massivo em um alvo único
///   (bosses, semi-bosses e tanques), e o primeiro inimigo atingido absorve o impacto.
/// - Se o jogador estiver com muito dano acumulado no final do jogo, permite limpar grupos andando de lado varrendo a sala.
/// - Cadência de tiro escala proporcionalmente com a velocidade de ataque (Attack Speed) do jogador.
/// </summary>
public class Ultimate_Gun : MonoBehaviour
{
    [Header("Identidade da Habilidade")]
    [Tooltip("Nome da Ultimate")]
    public string ultimateName = "O Expurgo Ressonante";

    [Header("⏱️ Duração e Cadência da Rajada (The Culling)")]
    [Tooltip("Duração total em segundos em que a rajada permanece atirando.")]
    public float barrageDuration = 2.8f;

    [Tooltip("Disparos base por segundo durante a rajada.")]
    public float baseShotsPerSecond = 14f;

    [Tooltip("A cadência da rajada aumenta se o jogador possuir buffs de Attack Speed?")]
    public bool scaleWithAttackSpeed = true;

    [Tooltip("Multiplicador de velocidade de movimento do jogador durante a rajada (1.0 = velocidade normal completa).")]
    [Range(0.5f, 1.5f)] public float moveSpeedMultiplier = 1.0f;

    [Header("💥 Dano e Projétil")]
    [Tooltip("Dano base de CADA disparo individual da rajada (individualmente baixo, mas devastador acumulado).")]
    public int baseDamagePerShot = 14;

    [Tooltip("Velocidade com que cada disparo viaja.")]
    public float projectileSpeed = 30f;

    [Tooltip("Alcance máximo que cada disparo percorre.")]
    public float projectileRange = 26f;

    [Tooltip("Dispersão angular mínima dos tiros da rajada (em graus).")]
    public float spreadAngle = 1.8f;

    [Header("✨ Visual Brilhante e Luminoso (Neon / Glow VFX)")]
    [Tooltip("Cor do brilho incandescente dos tiros e do rastro de energia.")]
    public Color glowColor = new Color(0.15f, 0.95f, 1.0f, 1.0f); // Ciano Elétrico

    [Tooltip("Intensidade da luz pontual dinâmica anexada a cada tiro.")]
    public float projectileLightIntensity = 2.8f;

    [Tooltip("Raio de alcance da luz dinâmica do projétil.")]
    public float projectileLightRange = 4.0f;

    [Tooltip("Largura do rastro luminoso (TrailRenderer) do tiro.")]
    public float trailWidth = 0.28f;

    [Tooltip("Deslocamento lateral alternado do cano para simular disparos contínuos de cano duplo.")]
    public float barrelAlternateOffset = 0.14f;

    [Header("🔊 Áudio")]
    [Tooltip("Som do disparo rápido da rajada. Se nulo, busca o áudio padrão do GunAttack.")]
    public AudioClip customShotClip;

    [Tooltip("Volume dos disparos da rajada.")]
    [Range(0f, 1f)] public float shotVolume = 0.65f;

    // Runtime refs
    private bool isFiringBarrage = false;
    public bool IsFiringBarrage => isFiringBarrage;

    private PlayerUltimate playerUltimate;
    private GunAttack gunAttack;
    private PlayerM playerMovement;
    private Player_WeaponManager weaponManager;
    private PlayerAttributesOffensive offensiveAttributes;
    private Vector3 lockedAimDirection = Vector3.forward;

    private static Material cachedTrailMaterial;

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        playerUltimate = GetComponentInParent<PlayerUltimate>() ?? GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        gunAttack = GetComponentInParent<GunAttack>() ?? GetComponentInChildren<GunAttack>() ?? FindFirstObjectByType<GunAttack>();
        playerMovement = GetComponentInParent<PlayerM>() ?? GetComponent<PlayerM>() ?? GetComponentInChildren<PlayerM>();
        weaponManager = GetComponentInParent<Player_WeaponManager>() ?? GetComponent<Player_WeaponManager>() ?? GetComponentInChildren<Player_WeaponManager>();
        offensiveAttributes = GetComponentInParent<PlayerAttributesOffensive>() ?? GetComponentInChildren<PlayerAttributesOffensive>() ?? PlayerAttributesOffensive.Instance;
    }

    /// <summary>
    /// Chamado pelo script PlayerUltimate.cs ao pressionar a tecla [U] com a Pistola empunhada.
    /// </summary>
    public void ExecuteUltimate()
    {
        if (isFiringBarrage) return;
        CacheReferences();

        // 1. Trava a direção de disparo exatamente para onde o jogador está apontando neste momento
        lockedAimDirection = CalculateAimDirection();

        Debug.Log($"[Ultimate_Gun] ★ Ativando '{ultimateName}'! Direção travada: {lockedAimDirection}. Iniciando rajada estilo R do Lucian...");

        StartCoroutine(TheCullingBarrageCoroutine());
    }

    private IEnumerator TheCullingBarrageCoroutine()
    {
        isFiringBarrage = true;

        // Calcula a cadência de disparo com base nos atributos do jogador
        float atkSpeedMult = (scaleWithAttackSpeed && offensiveAttributes != null && offensiveAttributes.attackSpeedMelee > 0.05f)
            ? Mathf.Clamp(offensiveAttributes.attackSpeedMelee, 0.5f, 2.5f)
            : 1.0f;

        float shotsPerSecond = Mathf.Max(4f, baseShotsPerSecond * atkSpeedMult);
        float shotInterval = 1f / shotsPerSecond;

        float elapsed = 0f;
        float nextShotTime = 0f;
        int shotCounter = 0;

        Transform spawnOrigin = (gunAttack != null && gunAttack.muzzle != null) ? gunAttack.muzzle : transform;

        while (elapsed < barrageDuration)
        {
            // A cada frame, mantém o corpo do astronauta virado para a direção travada,
            // permitindo que o jogador se mova livremente em qualquer direção com WASD (strafe completo)
            if (playerMovement != null)
            {
                playerMovement.SetPlasmaFiring(true, moveSpeedMultiplier);
                playerMovement.SetPlasmaAimDirection(lockedAimDirection);
            }

            // Hora de disparar o próximo tiro da rajada
            if (elapsed >= nextShotTime)
            {
                nextShotTime = elapsed + shotInterval;
                shotCounter++;

                SpawnBarrageProjectile(spawnOrigin, lockedAimDirection, shotCounter);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Finalização da rajada
        isFiringBarrage = false;

        if (playerMovement != null)
        {
            playerMovement.SetPlasmaFiring(false);
        }

        if (playerUltimate != null)
        {
            playerUltimate.EndUltimateSequence();
        }

        Debug.Log($"[Ultimate_Gun] ★ '{ultimateName}' concluída! Total de {shotCounter} disparos de plasma desferidos.");
    }

    private void SpawnBarrageProjectile(Transform spawnTransform, Vector3 baseDirection, int shotIndex)
    {
        if (gunAttack == null || gunAttack.projectilePrefab == null) return;

        // Alterna o cano ligeiramente entre a esquerda e a direita perpendicular à direção travada
        Vector3 rightAxis = Vector3.Cross(Vector3.up, baseDirection).normalized;
        float sideSign = (shotIndex % 2 == 0) ? 1f : -1f;
        Vector3 lateralOffset = rightAxis * (barrelAlternateOffset * sideSign);

        Vector3 spawnPos = (spawnTransform != null ? spawnTransform.position : transform.position + Vector3.up * 0.9f) + lateralOffset;

        // Leve dispersão angular orgânica para efeito de metralhadora de alta cadência
        float angleJitter = Random.Range(-spreadAngle, spreadAngle);
        Vector3 fireDir = Quaternion.Euler(0f, angleJitter, 0f) * baseDirection;

        // Instancia o projétil
        GunProjectile projectile = Instantiate(
            gunAttack.projectilePrefab,
            spawnPos,
            Quaternion.LookRotation(fireDir, Vector3.up)
        );

        if (projectile == null) return;

        // Configura atributos de dano (sozinhos dão pouco dano, mas acumulados derretem alvos únicos)
        float damageMult = offensiveAttributes != null ? offensiveAttributes.baseDamageMultiplier : 1.0f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamagePerShot * damageMult));

        float critChance = offensiveAttributes != null ? offensiveAttributes.critChance : 5f;
        float critMult = offensiveAttributes != null ? Mathf.Max(1.5f, offensiveAttributes.critMultiplier) : 1.5f;

        // PROJÉTIL DE ALVO ÚNICO: Piercing = 0 (não atravessa) e Ricochete = 0
        projectile.speed = projectileSpeed;
        projectile.Initialize(
            projectileDamage: finalDamage,
            range: projectileRange,
            piercing: 0, // NÃO ATRAVESSA! O primeiro alvo atingido toma o tiro e absorve
            owner: transform.root,
            critChance: critChance,
            critMultiplier: critMult,
            projectileKnockback: 0.35f,
            projectileBounceChance: 0f, // SEM RICOCHETE
            projectileBounceCount: 0,
            projectileSlowOnHit: 0f
        );

        // Aplica o efeito visual luminoso e brilhante
        ApplyGlowAndTrailEffect(projectile.gameObject);

        // Feedback de Muzzle Flash
        if (gunAttack.muzzleFlashVFX != null)
        {
            gunAttack.muzzleFlashVFX.Play(true);
        }

        // Feedback Sonoro
        AudioClip clipToPlay = customShotClip != null ? customShotClip : (gunAttack != null ? null : null);
        if (clipToPlay != null)
        {
            AudioSource.PlayClipAtPoint(clipToPlay, spawnPos, shotVolume);
        }
    }

    /// <summary>
    /// Adiciona luz dinâmica, TrailRenderer incandescente e cores HDR emissivas aos disparos da Ultimate.
    /// </summary>
    private void ApplyGlowAndTrailEffect(GameObject projectileObj)
    {
        if (projectileObj == null) return;

        // 1. Luz dinâmica pontual anexada ao projétil
        Light projLight = projectileObj.GetComponentInChildren<Light>();
        if (projLight == null)
        {
            GameObject lightObj = new GameObject("Ultimate_ProjectileLight");
            lightObj.transform.SetParent(projectileObj.transform, false);
            projLight = lightObj.AddComponent<Light>();
        }

        projLight.type = LightType.Point;
        projLight.color = glowColor;
        projLight.range = projectileLightRange;
        projLight.intensity = projectileLightIntensity;
        projLight.shadows = LightShadows.None;

        // 2. Rastro luminoso de energia cortando o ar (TrailRenderer)
        TrailRenderer trail = projectileObj.GetComponentInChildren<TrailRenderer>();
        if (trail == null)
        {
            trail = projectileObj.AddComponent<TrailRenderer>();
        }

        if (cachedTrailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null) cachedTrailMaterial = new Material(shader);
        }

        if (cachedTrailMaterial != null)
        {
            trail.material = cachedTrailMaterial;
        }

        trail.time = 0.12f;
        trail.startWidth = trailWidth;
        trail.endWidth = 0.04f;
        trail.startColor = glowColor;
        trail.endColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

        // 3. Materiais emissivos no modelo visual do tiro
        Renderer[] renderers = projectileObj.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (r.material != null)
            {
                r.material.color = glowColor;
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", glowColor * 3.0f);
            }
        }
    }

    private Vector3 CalculateAimDirection()
    {
        // Se estiver com mira no mouse ativada, aponta exatamente para a coordenada do cursor no piso
        if (AimSettings.IsMouseAim && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 dir = Vector3.ProjectOnPlane(hitPoint - transform.position, Vector3.up).normalized;
                if (dir.sqrMagnitude > 0.001f) return dir;
            }
        }

        // Fallback: direção para onde o personagem está virado
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        return forward.sqrMagnitude > 0.001f ? forward : Vector3.forward;
    }

    private void OnDisable()
    {
        if (isFiringBarrage)
        {
            isFiringBarrage = false;
            StopAllCoroutines();
            if (playerMovement != null) playerMovement.SetPlasmaFiring(false);
            if (playerUltimate != null) playerUltimate.EndUltimateSequence();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = glowColor;
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = Application.isPlaying && isFiringBarrage ? lockedAimDirection : transform.forward;
        Gizmos.DrawRay(origin, dir * projectileRange);
    }
}
