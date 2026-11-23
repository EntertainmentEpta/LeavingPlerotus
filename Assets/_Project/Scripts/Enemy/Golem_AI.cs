using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// IA do Golem - Inimigo tanque pesado com golpe corpo a corpo telegrafado e ataque orbital de stun.
/// - Normal: Stun com rastreamento suave + lock-on antes do feixe cair do céu (exige desvio ativo/dash).
/// - Crystal Tuner: Armadura reforçada, velocidade um pouco maior e raio de stun ampliado em tom ciano.
/// - Elite: Barragem orbital de 3 feixes de stun em sequência + golpe melee mais pesado e rápido.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class Golem_AI : MonoBehaviour
{
    [Header("Referências")]
    private Transform playerTransform;
    private Rigidbody playerRb;
    private DummyHealth health;
    private Rigidbody rb;
    private AudioSource audioSource;

    [Header("VFX Prefabs (Placeholders ok)")]
    [Tooltip("Prefab opcional do círculo de aviso no chão antes do stun. Se vazio, usa o indicador limpo do StunBeam.")]
    public GameObject stunMarkerPrefab;
    [Tooltip("Prefab de configuração do feixe do céu e partículas de impacto (StunBeam.prefab).")]
    public GameObject stunBeamPrefab;

    [Header("Ativação")]
    public float activationDistance = 25f;
    private bool isActivated = false;

    [Header("Movimento & Navegação")]
    [Tooltip("Velocidade de movimento pesado")]
    public float moveSpeed = 3.4f;
    [Tooltip("Velocidade de rotação")]
    public float rotationSpeed = 5f;
    [Tooltip("Intervalo (s) para recalcular o caminho no NavMesh ao contornar obstáculos.")]
    public float navPathUpdateInterval = 0.2f;

    [Header("Ataque Melee (Golpe Pesado)")]
    [Tooltip("Distância para iniciar o ataque corpo a corpo")]
    public float meleeRange = 3.2f;
    [Tooltip("Cooldown entre ataques melee após o término do golpe")]
    public float meleeCooldown = 1.15f;
    [Tooltip("Dano do ataque melee")]
    public int meleeDamage = 35;
    [Tooltip("Raio da hitbox e do VFX de impacto do melee")]
    public float meleeHitRadius = 2.4f;
    [Tooltip("Distância à frente do Golem onde o golpe atinge o chão")]
    public float meleeHitForwardOffset = 1.65f;
    [Tooltip("Tempo de preparação/wind-up antes do impacto do melee")]
    public float meleeWindupTime = 0.50f;
    [Tooltip("Tempo de recuperação após o impacto do melee")]
    public float meleeRecoveryTime = 0.40f;
    [Tooltip("Cor do aviso no chão do golpe melee")]
    public Color meleeImpactColor = new Color(0.95f, 0.72f, 0.35f, 1f);

    [Header("Habilidade de Stun (Feixe do Céu)")]
    [Tooltip("Distância MÁXIMA para usar stun (não funciona se player estiver mais longe)")]
    public float stunMaxRange = 10f;
    [Tooltip("Cooldown do stun")]
    public float stunCooldown = 8.5f;
    [Tooltip("Duração do stun no jogador")]
    public float stunDuration = 1.5f;
    [Tooltip("Tempo de antecipação antes do feixe cair (rastreia o player no início e trava antes do impacto)")]
    public float stunTelegraphTime = 0.80f;
    [Tooltip("Tempo de recuperação após o impacto do stun")]
    public float stunRecoveryTime = 0.40f;
    [Tooltip("Raio base da área de stun (casa 1:1 com o círculo no chão)")]
    public float stunRadius = 4.3f;
    [Tooltip("Fração inicial da antecipação (0..1) em que o círculo acompanha suavemente o player antes de travar.")]
    [Range(0f, 0.65f)]
    public float stunTrackingPortion = 0.45f;
    [Tooltip("Velocidade de acompanhamento do círculo no chão durante a fase de rastreamento.")]
    public float stunTrackingSpeed = 6.5f;
    [Tooltip("Previsão leve (segundos) da direção do player durante o rastreamento do stun.")]
    [Range(0f, 0.5f)]
    public float stunLeadTime = 0.22f;
    [Tooltip("Quantidade de melee hits para ativar stun no combo")]
    public int comboHitsForStun = 2;

    [Header("Buff do Crystal Tuner")]
    [Tooltip("Multiplicador de velocidade ao estar conectado ao Crystal Tuner.")]
    public float tunerSpeedMultiplier = 1.18f;
    [Tooltip("Multiplicador de cooldown do stun ao estar conectado ao Crystal Tuner.")]
    public float tunerStunCooldownMultiplier = 0.82f;
    [Tooltip("Multiplicador do raio do stun ao estar conectado ao Crystal Tuner.")]
    public float tunerStunRadiusMultiplier = 1.30f;
    [Tooltip("Resistência mínima contra projéteis (armadura extra além dos 50% do Tuner no DummyHealth).")]
    [Range(0f, 0.9f)]
    public float tunerProjectileResistance = 0.78f;
    [Tooltip("Cor ciano cristalina do Stun quando fortalecido pelo Crystal Tuner.")]
    public Color tunerStunColor = new Color(0.22f, 0.90f, 1.0f, 1f);

    [Header("Melhorias do Golem Elite")]
    [Tooltip("Quantidade de feixes de stun em sequência disparados pelo Golem Elite.")]
    [Range(1, 4)]
    public int eliteStunBeamCount = 2;
    [Tooltip("Pausa (s) entre o impacto do 1º stun e o início do 2º stun no Golem Elite.")]
    public float eliteBarrageDelayAfterImpact = 0.35f;
    [Tooltip("Tempo de antecipação de cada feixe da sequência do Elite.")]
    public float eliteBarrageTelegraphTime = 0.78f;
    [Tooltip("Multiplicador do wind-up melee no Elite (< 1 = mais ágil).")]
    public float eliteMeleeWindupMultiplier = 0.88f;
    [Tooltip("Multiplicador do raio de impacto melee no Elite.")]
    public float eliteMeleeRadiusMultiplier = 1.15f;
    [Tooltip("Multiplicador de dano melee no Elite.")]
    public float eliteMeleeDamageMultiplier = 1.20f;

    [Header("Estados")]
    private bool isAttacking = false;
    private bool isCastingStun = false;
    private float meleeTimer = 0f;
    private float stunTimer = 0f;
    private int meleeCombo = 0;
    private bool isBuffed = false;
    private float savedProjectileResistance = -1f;

    // Progresso sincronizado com GolemProceduralAnimation
    private float attackNormalizedTime = 0f;
    private float stunNormalizedTime = 0f;

    // Navegação NavMesh + Steering
    private NavMeshPath navPath;
    private float navPathTimer = 0f;
    private int navCornerIndex = 0;
    private Vector3 currentMoveDirection = Vector3.zero;

    // Referências aos telegraphs/feixes ativos para limpar se morrer ou desativar no meio do cast
    private GameObject activeTelegraphObject;
    private readonly List<GolemSkyStunVFX> activeSkyStunVFXList = new List<GolemSkyStunVFX>();

    [Header("Áudio — Movimentação (Passos)")]
    public AudioClip[] walkingSounds;
    [Range(0f, 1f)]
    public float walkingSoundVolume = 0.6f;
    public float stepInterval = 0.52f;
    private float stepTimer = 0f;

    [Header("Áudio — Ataque Normal (Melee)")]
    public AudioClip meleeAttackSound;
    [Range(0f, 1f)]
    public float meleeAttackSoundVolume = 0.8f;

    [Header("Áudio — Habilidade (Stomp / Stun)")]
    public AudioClip stompSound;
    [Range(0f, 1f)]
    public float stompSoundVolume = 0.50f;

    [Header("Áudio — Dano Recebido (Hit)")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    public AudioClip[] dieSounds;
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    private bool IsElite => health != null && health.isElite;

    public bool IsAttacking => isAttacking;
    public bool IsCastingStun => isCastingStun;
    public bool IsActivated => isActivated;
    public float AttackNormalizedTime => attackNormalizedTime;
    public float StunNormalizedTime => stunNormalizedTime;
    public float MeleeTotalDuration => Mathf.Max(0.15f, GetEffectiveMeleeWindup() + meleeRecoveryTime);
    public float StunTotalDuration => Mathf.Max(0.25f, stunTelegraphTime + stunRecoveryTime);
    public float MeleeHitNormalizedTime => Mathf.Clamp(GetEffectiveMeleeWindup() / MeleeTotalDuration, 0.15f, 0.85f);
    public float StunImpactNormalizedTime => Mathf.Clamp(stunTelegraphTime / StunTotalDuration, 0.25f, 0.88f);

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        health = GetComponent<DummyHealth>();
        navPath = new NavMeshPath();

        // Golem é pesado como uma rocha: massa alta e imune a empurrões leves
        if (rb != null)
        {
            rb.mass = 800f;
            rb.linearDamping = 10f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // +30% HP no Golem (Pilar 4)
        if (health != null)
        {
            health.maxHealth = Mathf.RoundToInt(health.maxHealth * 1.30f);
            health.ResetHealth();
        }

        // Snap de segurança no chão para evitar clipagem no mapa
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            Vector3 pos = transform.position;
            pos.y = navHit.position.y;
            transform.position = pos;
        }

        // Encontra o player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerRb = player.GetComponent<Rigidbody>();
        }
        else
        {
            Debug.LogError("Golem_AI: Player não encontrado! Verifique a tag 'Player'.");
        }

        // Registra callbacks de áudio de hit e morte
        if (health != null)
        {
            if (hitSounds != null && hitSounds.Length > 0)
                health.onDamageTaken += (damage, attacker) => PlayHitSound();

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;

            health.onDeath += CleanupActiveTelegraphs;
        }
    }

    private void OnDisable()
    {
        CleanupActiveTelegraphs();
        isAttacking = false;
        isCastingStun = false;
        attackNormalizedTime = 0f;
        stunNormalizedTime = 0f;
    }

    private void OnDestroy()
    {
        CleanupActiveTelegraphs();
    }

    private void CleanupActiveTelegraphs()
    {
        if (activeTelegraphObject != null)
        {
            Destroy(activeTelegraphObject);
            activeTelegraphObject = null;
        }

        for (int i = 0; i < activeSkyStunVFXList.Count; i++)
        {
            if (activeSkyStunVFXList[i] != null)
                activeSkyStunVFXList[i].CancelAnticipation();
        }
        activeSkyStunVFXList.Clear();
    }

    private float GetEffectiveMeleeWindup()
    {
        float w = Mathf.Max(0.15f, meleeWindupTime);
        if (isBuffed) w *= 0.85f;
        if (IsElite) w *= Mathf.Clamp(eliteMeleeWindupMultiplier, 0.5f, 1.2f);
        return w;
    }

    private float GetEffectiveMeleeRange()
    {
        return IsElite ? meleeRange * 1.10f : meleeRange;
    }

    private float GetEffectiveMeleeForwardOffset()
    {
        return IsElite ? meleeHitForwardOffset * 1.15f : meleeHitForwardOffset;
    }

    private float GetEffectiveStunRadius()
    {
        return isBuffed ? stunRadius * tunerStunRadiusMultiplier : stunRadius;
    }

    private void PlayHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0) return;
        AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.9f, 1.1f), hitSoundVolume);
    }

    private void PlayDieSound()
    {
        if (dieSounds == null || dieSounds.Length == 0) return;
        AudioClip clip = dieSounds[Random.Range(0, dieSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.9f, 1.1f), dieSoundVolume);
    }

    void Update()
    {
        if (playerTransform == null) return;
        if (health != null && health.CurrentHealth <= 0)
        {
            CleanupActiveTelegraphs();
            return;
        }

        if (meleeTimer > 0f) meleeTimer -= Time.deltaTime;
        if (stunTimer > 0f) stunTimer -= Time.deltaTime;

        if (!isActivated)
        {
            float dist = GetHorizontalDistance(transform.position, playerTransform.position);
            if (dist < activationDistance)
            {
                isActivated = true;
                EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                if (id != null && BestiarioManager.instancia != null)
                    BestiarioManager.instancia.Registrar(id);
            }
            return;
        }

        if (isAttacking || isCastingStun) return;

        UpdateNavigationPath();
        HandleRotation();
        HandleCombat();
    }

    void FixedUpdate()
    {
        if (!isActivated || isAttacking || isCastingStun) return;
        if (health != null && health.CurrentHealth <= 0) return;

        HandleMovement();
        HandleWalkingSound();
    }

    private void UpdateNavigationPath()
    {
        navPathTimer -= Time.deltaTime;
        if (navPathTimer > 0f) return;
        navPathTimer = Mathf.Max(0.1f, navPathUpdateInterval);

        if (navPath == null) navPath = new NavMeshPath();

        Vector3 start = transform.position;
        Vector3 target = playerTransform.position;

        if (NavMesh.SamplePosition(start, out NavMeshHit startHit, 3f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(target, out NavMeshHit targetHit, 3f, NavMesh.AllAreas))
        {
            if (NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, navPath) &&
                navPath.corners != null && navPath.corners.Length > 1)
            {
                navCornerIndex = 1;
                return;
            }
        }

        navPath.ClearCorners();
        navCornerIndex = 0;
    }

    private Vector3 ComputeDesiredMoveDirection()
    {
        if (navPath != null && navPath.corners != null && navPath.corners.Length > 1)
        {
            while (navCornerIndex < navPath.corners.Length - 1)
            {
                Vector3 toCorner = navPath.corners[navCornerIndex] - transform.position;
                toCorner.y = 0f;
                if (toCorner.magnitude < 0.75f)
                    navCornerIndex++;
                else
                    break;
            }

            if (navCornerIndex < navPath.corners.Length)
            {
                Vector3 dir = navPath.corners[navCornerIndex] - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }
        }

        Vector3 direct = playerTransform.position - transform.position;
        direct.y = 0f;
        return direct.sqrMagnitude > 0.001f ? direct.normalized : transform.forward;
    }

    private void HandleWalkingSound()
    {
        if (rb == null) return;

        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.magnitude > 0.25f)
        {
            stepTimer -= Time.fixedDeltaTime;
            if (stepTimer <= 0f)
            {
                PlayWalkingSound();
                stepTimer = stepInterval;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }

    private void PlayWalkingSound()
    {
        if (walkingSounds == null || walkingSounds.Length == 0)
            return;

        AudioClip clipToPlay = walkingSounds[Random.Range(0, walkingSounds.Length)];
        if (clipToPlay != null)
        {
            float pitch = Random.Range(0.85f, 1.05f);
            PlayClipAtPointWithPitch(clipToPlay, transform.position, pitch, walkingSoundVolume);
        }
    }

    private void PlayStompSound(Vector3 position)
    {
        if (stompSound == null) return;

        float pitch = Random.Range(0.9f, 1.1f);
        PlayClipAtPointWithPitch(stompSound, position, pitch, stompSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;
        float effectiveVolume = volume * GlobalAudioManager.SFXVolumeMultiplier;
        if (effectiveVolume <= 0.001f) return;

        if (audioSource != null)
        {
            audioSource.pitch = pitch;
            if (audioSource.outputAudioMixerGroup == null && GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                audioSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
            audioSource.PlayOneShot(clip, effectiveVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, position, effectiveVolume);
        }
    }

    void HandleRotation()
    {
        float distToPlayer = GetHorizontalDistance(transform.position, playerTransform.position);
        Vector3 lookDir;

        if (distToPlayer > meleeRange + 1.0f && currentMoveDirection.sqrMagnitude > 0.01f)
        {
            lookDir = currentMoveDirection;
        }
        else
        {
            lookDir = playerTransform.position - transform.position;
            lookDir.y = 0f;
        }

        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void FacePlayerSmooth(float speedMultiplier = 1f)
    {
        if (playerTransform == null) return;
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * speedMultiplier * Time.deltaTime);
        }
    }

    void HandleMovement()
    {
        float distToPlayer = GetHorizontalDistance(transform.position, playerTransform.position);
        float effMeleeRange = GetEffectiveMeleeRange();

        if (distToPlayer > effMeleeRange * 0.92f)
        {
            currentMoveDirection = ComputeDesiredMoveDirection();
            Vector3 targetVelocity = currentMoveDirection * moveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            currentMoveDirection = Vector3.zero;
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    void HandleCombat()
    {
        float distToPlayer = GetHorizontalDistance(transform.position, playerTransform.position);
        float effMeleeRange = GetEffectiveMeleeRange();

        // Prioridade 1: Após X melee hits seguidos, usa stun como combo breaker
        if (meleeCombo >= comboHitsForStun && stunTimer <= 0f && distToPlayer <= stunMaxRange)
        {
            StartCoroutine(CastStun());
            meleeCombo = 0;
            return;
        }

        // Prioridade 2: Se perto, executa ataque melee pesado
        if (distToPlayer <= effMeleeRange && meleeTimer <= 0f)
        {
            StartCoroutine(PerformMeleeAttack());
            return;
        }

        // Prioridade 3: Se player está na faixa média do stun (ou melee em cooldown) e stun pronto, usa stun
        if (distToPlayer <= stunMaxRange && stunTimer <= 0f && (distToPlayer > effMeleeRange + 0.6f || meleeTimer > 0f))
        {
            StartCoroutine(CastStun());
            return;
        }
    }

    IEnumerator PerformMeleeAttack() =>
        DummyHealth.PauseAttackWhileStunned(this, PerformMeleeAttackSteps());

    private IEnumerator PerformMeleeAttackSteps()
    {
        isAttacking = true;
        attackNormalizedTime = 0f;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        float windup = GetEffectiveMeleeWindup();
        float recovery = Mathf.Max(0.10f, meleeRecoveryTime);
        float totalDuration = windup + recovery;
        float hitRadius = IsElite ? meleeHitRadius * eliteMeleeRadiusMultiplier : meleeHitRadius;
        int damageToDeal = IsElite ? Mathf.RoundToInt(meleeDamage * eliteMeleeDamageMultiplier) : meleeDamage;

        float trackingCutoff = windup * 0.30f;
        float elapsed = 0f;
        bool spawnedTelegraph = false;
        Vector3 lockedHitCenter = Vector3.zero;

        while (elapsed < windup)
        {
            if (health != null && health.CurrentHealth <= 0)
            {
                CleanupActiveTelegraphs();
                isAttacking = false;
                attackNormalizedTime = 0f;
                yield break;
            }

            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

            if (elapsed < trackingCutoff)
            {
                FacePlayerSmooth(1.25f);
            }
            else if (!spawnedTelegraph)
            {
                spawnedTelegraph = true;
                lockedHitCenter = GetGroundPosition(transform.position + transform.forward * GetEffectiveMeleeForwardOffset(), transform.position.y);
                float remainingTelegraph = Mathf.Max(0.12f, windup - elapsed);
                Material telegraphTemplate = stunBeamPrefab != null ? stunBeamPrefab.GetComponent<StunBeam>()?.cleanTelegraphMaterial : null;
                GolemGroundTelegraph telegraph = GolemGroundTelegraph.Spawn(
                    lockedHitCenter,
                    hitRadius,
                    remainingTelegraph,
                    meleeImpactColor,
                    telegraphTemplate
                );
                activeTelegraphObject = telegraph.gameObject;
            }

            elapsed += Time.deltaTime;
            attackNormalizedTime = Mathf.Clamp01(elapsed / totalDuration);
            yield return null;
        }

        if (activeTelegraphObject != null)
        {
            Destroy(activeTelegraphObject);
            activeTelegraphObject = null;
        }

        if (!spawnedTelegraph)
            lockedHitCenter = GetGroundPosition(transform.position + transform.forward * GetEffectiveMeleeForwardOffset(), transform.position.y);

        // Momento exato do impacto: som + VFX reais de impacto no chão + hitbox sincronizada
        if (meleeAttackSound != null)
        {
            float pitch = Random.Range(0.88f, 1.05f);
            PlayClipAtPointWithPitch(meleeAttackSound, lockedHitCenter, pitch, meleeAttackSoundVolume);
        }

        StunBeam beamCfg = stunBeamPrefab != null ? stunBeamPrefab.GetComponent<StunBeam>() : null;
        GolemMeleeSlamVFX.Spawn(
            lockedHitCenter,
            hitRadius,
            beamCfg != null ? beamCfg.groundHitRingPrefab : null,
            beamCfg != null ? beamCfg.groundImpactPrefab : null
        );

        Vector3 flatHitCenter = new Vector3(lockedHitCenter.x, 0f, lockedHitCenter.z);
        Collider[] hits = Physics.OverlapSphere(lockedHitCenter + Vector3.up * 0.8f, hitRadius + 1.0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (!hit.CompareTag("Player")) continue;

            Vector3 flatTarget = new Vector3(hit.transform.position.x, 0f, hit.transform.position.z);
            if (Vector3.Distance(flatHitCenter, flatTarget) > hitRadius) continue;

            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>() ?? hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damageToDeal, gameObject);
                meleeCombo++;
                break;
            }
        }

        float recElapsed = 0f;
        while (recElapsed < recovery)
        {
            if (health != null && health.CurrentHealth <= 0)
            {
                isAttacking = false;
                attackNormalizedTime = 0f;
                yield break;
            }

            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            recElapsed += Time.deltaTime;
            attackNormalizedTime = Mathf.Clamp01((windup + recElapsed) / totalDuration);
            yield return null;
        }

        meleeTimer = meleeCooldown;
        attackNormalizedTime = 0f;
        isAttacking = false;
    }

    IEnumerator CastStun() =>
        DummyHealth.PauseAttackWhileStunned(this, CastStunSteps());

    private IEnumerator CastStunSteps()
    {
        isCastingStun = true;
        stunNormalizedTime = 0f;
        meleeCombo = 0;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        float effectiveRadius = GetEffectiveStunRadius();
        Color? overrideColor = isBuffed ? tunerStunColor : (Color?)null;

        if (IsElite && eliteStunBeamCount > 1)
        {
            // ELITE: Barragem orbital de múltiplos feixes em sequência
            yield return StartCoroutine(CastEliteStunBarrage(effectiveRadius, overrideColor));
        }
        else
        {
            // NORMAL (ou buffado pelo Tuner com raio maior): 1 feixe com rastreamento suave + lock-on
            float telegraphTime = Mathf.Max(0.30f, stunTelegraphTime);
            float recovery = Mathf.Max(0.15f, stunRecoveryTime);
            float totalDuration = telegraphTime + recovery;

            yield return StartCoroutine(RunTrackedSkyBeam(
                effectiveRadius,
                telegraphTime,
                stunTrackingPortion,
                overrideColor,
                driveStunAnim: true,
                animTotalDuration: totalDuration
            ));

            if (health == null || health.CurrentHealth > 0)
            {
                float recElapsed = 0f;
                while (recElapsed < recovery)
                {
                    if (health != null && health.CurrentHealth <= 0) break;
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                    recElapsed += Time.deltaTime;
                    stunNormalizedTime = Mathf.Clamp01((telegraphTime + recElapsed) / totalDuration);
                    yield return null;
                }
            }
        }

        stunTimer = stunCooldown;
        stunNormalizedTime = 0f;
        isCastingStun = false;
    }

    private IEnumerator CastEliteStunBarrage(float effectiveRadius, Color? overrideColor)
    {
        int beamCount = Mathf.Max(2, eliteStunBeamCount);
        float beamTelegraph = Mathf.Max(0.35f, eliteBarrageTelegraphTime);
        float delayBetweenBeams = Mathf.Max(0.15f, eliteBarrageDelayAfterImpact);
        float recovery = Mathf.Max(0.20f, stunRecoveryTime);
        float animTotal = beamTelegraph + recovery;

        for (int b = 0; b < beamCount; b++)
        {
            if (health != null && health.CurrentHealth <= 0)
            {
                CleanupActiveTelegraphs();
                yield break;
            }

            FacePlayerSmooth(3f);

            // Executa cada feixe completamente antes da pausa para o próximo (evita sobreposição apressada)
            stunNormalizedTime = 0f;
            yield return StartCoroutine(RunTrackedSkyBeam(
                effectiveRadius,
                beamTelegraph,
                stunTrackingPortion,
                overrideColor,
                driveStunAnim: true,
                animTotalDuration: animTotal
            ));

            if (b < beamCount - 1)
            {
                // Pequeno delay entre o impacto do 1º stun e o início do 2º
                float wait = 0f;
                while (wait < delayBetweenBeams)
                {
                    if (health != null && health.CurrentHealth <= 0)
                    {
                        CleanupActiveTelegraphs();
                        yield break;
                    }
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                    FacePlayerSmooth(1.8f);
                    wait += Time.deltaTime;
                    stunNormalizedTime = Mathf.Clamp01(Mathf.Lerp(StunImpactNormalizedTime, 0.92f, wait / delayBetweenBeams));
                    yield return null;
                }
            }
        }

        // Recovery final após o último feixe
        float rec = 0f;
        while (rec < recovery)
        {
            if (health != null && health.CurrentHealth <= 0) yield break;
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rec += Time.deltaTime;
            stunNormalizedTime = Mathf.Clamp01(Mathf.Lerp(StunImpactNormalizedTime, 1f, rec / recovery));
            yield return null;
        }
    }

    private IEnumerator RunTrackedSkyBeam(
        float effectiveRadius,
        float telegraphTime,
        float trackingPortion,
        Color? overrideColor,
        bool driveStunAnim,
        float animTotalDuration)
    {
        if (playerTransform == null) yield break;

        Vector3 currentTarget = GetPredictedPlayerGroundPos();

        if (driveStunAnim)
        {
            Vector3 lookDir = currentTarget - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        }

        GameObject customMarker = null;
        if (stunMarkerPrefab != null)
        {
            customMarker = Instantiate(stunMarkerPrefab, currentTarget + Vector3.up * 0.08f, Quaternion.Euler(90f, 0f, 0f));
            customMarker.transform.localScale = new Vector3(effectiveRadius * 2f, effectiveRadius * 2f, 1f);
        }

        GolemSkyStunVFX skyVfx;
        StunBeam beamTemplate = stunBeamPrefab != null ? stunBeamPrefab.GetComponent<StunBeam>() : null;
        if (beamTemplate != null)
        {
            skyVfx = beamTemplate.SpawnSkyStunAnticipation(currentTarget, effectiveRadius, telegraphTime, overrideColor);
        }
        else
        {
            Color col = overrideColor ?? GetStunTelegraphColor();
            skyVfx = GolemSkyStunVFX.SpawnAnticipation(
                currentTarget,
                effectiveRadius,
                telegraphTime,
                38f,
                col,
                new Color(0.70f, 0.56f, 1f, 1f),
                new Color(0.95f, 0.97f, 1f, 1f),
                null, null, null, null, null, null, null, null
            );
        }

        activeSkyStunVFXList.Add(skyVfx);

        float lockTime = telegraphTime * Mathf.Clamp01(trackingPortion);
        bool locked = false;
        float elapsed = 0f;

        while (elapsed < telegraphTime)
        {
            if (health != null && health.CurrentHealth <= 0)
            {
                if (customMarker != null) Destroy(customMarker);
                if (skyVfx != null)
                {
                    skyVfx.CancelAnticipation();
                    activeSkyStunVFXList.Remove(skyVfx);
                }
                yield break;
            }

            if (driveStunAnim)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

            // Rastreamento suave nos primeiros ~45%, depois trava no chão antes do feixe cair!
            if (elapsed < lockTime && playerTransform != null)
            {
                Vector3 desired = GetPredictedPlayerGroundPos();
                Vector3 flatCur = new Vector3(currentTarget.x, 0f, currentTarget.z);
                Vector3 flatDes = new Vector3(desired.x, 0f, desired.z);
                Vector3 movedFlat = Vector3.MoveTowards(flatCur, flatDes, stunTrackingSpeed * Time.deltaTime);
                currentTarget = GetGroundPosition(new Vector3(movedFlat.x, desired.y, movedFlat.z), desired.y);

                if (skyVfx != null) skyVfx.UpdateTargetPosition(currentTarget);
                if (customMarker != null) customMarker.transform.position = currentTarget + Vector3.up * 0.08f;

                if (driveStunAnim)
                    FacePlayerSmooth(1.4f);
            }
            else if (!locked)
            {
                locked = true;
                if (skyVfx != null) skyVfx.MarkLockedOn();
            }

            elapsed += Time.deltaTime;
            if (driveStunAnim)
                stunNormalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, animTotalDuration));

            yield return null;
        }

        if (customMarker != null) Destroy(customMarker);

        // Impacto sincronizado: som + explosão do feixe + stun na área exata
        PlayStompSound(currentTarget);

        if (skyVfx != null)
        {
            skyVfx.TriggerImpact();
            activeSkyStunVFXList.Remove(skyVfx);
        }

        StunBeam.ApplyStunInArea(currentTarget, effectiveRadius, stunDuration);
    }

    private Vector3 GetPredictedPlayerGroundPos()
    {
        if (playerTransform == null) return transform.position;
        Vector3 basePos = playerTransform.position;

        if (playerRb != null && stunLeadTime > 0f)
        {
            Vector3 flatVel = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            // Limita a previsão para não jogar o círculo longe demais se o player der dash
            Vector3 leadOffset = Vector3.ClampMagnitude(flatVel * stunLeadTime, 2.2f);
            basePos += leadOffset;
        }

        return GetGroundPosition(basePos, playerTransform.position.y);
    }

    private Color GetStunTelegraphColor()
    {
        if (stunBeamPrefab != null)
        {
            StunBeam sb = stunBeamPrefab.GetComponent<StunBeam>();
            if (sb != null)
                return sb.beamColor;
        }
        return new Color(0.52f, 0.75f, 1.0f, 1f);
    }

    private Vector3 GetGroundPosition(Vector3 worldPos, float referenceY)
    {
        int groundMask = (1 << 0) | (1 << 3); // Default | Ground
        Vector3 rayOrigin = new Vector3(worldPos.x, referenceY + 2.5f, worldPos.z);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, groundMask, QueryTriggerInteraction.Ignore);

        bool foundSurface = false;
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.normal.y < 0.55f) continue;
            if (h.point.y > referenceY + 1.0f) continue;
            if (h.point.y > bestY)
            {
                bestY = h.point.y;
                foundSurface = true;
            }
        }

        if (foundSurface)
            return new Vector3(worldPos.x, bestY, worldPos.z);

        if (NavMesh.SamplePosition(worldPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            return new Vector3(worldPos.x, Mathf.Max(navHit.position.y, referenceY), worldPos.z);

        return new Vector3(worldPos.x, referenceY, worldPos.z);
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// Buff aplicado pelo CrystalTuner: armadura extra, velocidade moderadamente maior e raio do stun ampliado.
    /// </summary>
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            moveSpeed *= tunerSpeedMultiplier;
            stunCooldown *= tunerStunCooldownMultiplier;

            if (health != null)
            {
                savedProjectileResistance = health.projectileResistance;
                health.projectileResistance = Mathf.Max(health.projectileResistance, tunerProjectileResistance);
            }
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            moveSpeed /= tunerSpeedMultiplier;
            stunCooldown /= tunerStunCooldownMultiplier;

            if (health != null && savedProjectileResistance >= 0f)
            {
                health.projectileResistance = savedProjectileResistance;
                savedProjectileResistance = -1f;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position + transform.forward * meleeHitForwardOffset, meleeHitRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stunMaxRange);

        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawSphere(transform.position + transform.forward * 5f, GetEffectiveStunRadius());
    }
}
