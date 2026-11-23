using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

/// <summary>
/// IA do Goblin — Minerador ágil que arremessa bombas em parábola balística exata e golpeia com picareta de perto.
/// Estados: Idle → Chase (NavMesh) → RangedAttack (Strafe + Bomba com telegraph no chão) → Flee → MeleeAttack.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class GoblinAI_Transform : MonoBehaviour
{
    // ── References ──────────────────────────────────────────────────
    [Header("References")]
    [FormerlySerializedAs("jogador")]
    public Transform player;

    [FormerlySerializedAs("prefabBomba")]
    public GameObject bombPrefab;

    [FormerlySerializedAs("pontoDeArremesso")]
    public Transform throwPoint;

    [Tooltip("VFX de slash horizontal da picareta (ex.: SharpBlur_Slash_VFX) que indica a área do golpe.")]
    public GameObject meleeSlashVfxPrefab;

    [Tooltip("VFX opcional de faíscas/impacto ao acertar o jogador com a picareta (ex.: CFXR Hit D 3D Yellow).")]
    public GameObject meleeImpactVfxPrefab;

    // ── Distances ───────────────────────────────────────────────────
    [Header("Distances")]
    [Tooltip("Minimum distance to keep from the player (triggers fleeing).")]
    [FormerlySerializedAs("distanciaFuga")]
    public float fleeDistance = 5.5f;

    [Tooltip("Ideal distance to throw bombs. The Goblin tries to stay here.")]
    [FormerlySerializedAs("distanciaAtaque")]
    public float throwDistance = 12.5f;

    [Tooltip("Maximum distance to start pursuing the player.")]
    [FormerlySerializedAs("distanciaMaxBusca")]
    public float chaseDistance = 28f;

    // ── Speeds ──────────────────────────────────────────────────────
    [Header("Speeds")]
    [FormerlySerializedAs("velocidadePerseguicao")]
    public float chaseSpeed = 4.2f;

    [FormerlySerializedAs("velocidadeFuga")]
    public float fleeSpeed = 5.0f;

    [FormerlySerializedAs("velocidadeStrafe")]
    public float strafeSpeed = 2.6f;

    [Tooltip("Movement acceleration (higher = responsive ground control without ice-skating).")]
    [FormerlySerializedAs("aceleracao")]
    public float acceleration = 18.0f;

    [Tooltip("Braking deceleration when stopping or changing states (prevents foot sliding).")]
    public float deceleration = 34.0f;

    [Tooltip("Velocidade em m/s correspondente à passada natural de RunningGoblin em 1x.")]
    public float strideReferenceSpeed = 2.35f;

    // ── Ranged Attack (Bomb) ────────────────────────────────────────
    [Header("Ranged - Bomb Throw")]
    [FormerlySerializedAs("forcaArremesso")]
    public float throwForce = 12f;

    [FormerlySerializedAs("forcaArco")]
    public float throwArcForce = 6f;

    [FormerlySerializedAs("intervaloAtaque")]
    public float throwCooldown = 2.6f;

    [Tooltip("Raio base da explosão da bomba arremessada.")]
    public float bombBaseRadius = 2.5f;

    [Tooltip("Multiplicador do raio da bomba quando conectado ao Crystal Tuner.")]
    public float tunerBombRadiusMultiplier = 1.35f;

    [Tooltip("Fator de previsão da velocidade do jogador ao calcular a parábola da bomba.")]
    [Range(0f, 0.8f)]
    public float bombLeadFactor = 0.35f;

    [Tooltip("If the player enters this distance while the Goblin is winding up a throw, the throw is cancelled.")]
    public float cancelThrowDistance = 3.5f;

    // ── Melee Attack (Pickaxe) ──────────────────────────────────────
    [Header("Melee - Pickaxe")]
    [Tooltip("Max distance for melee attack (overrides fleeing).")]
    [FormerlySerializedAs("distanciaMelee")]
    public float meleeDistance = 2.5f;

    [Tooltip("Damage dealt by the pickaxe hit.")]
    [FormerlySerializedAs("danoMelee")]
    public int meleeDamage = 18;

    [Tooltip("Cooldown between pickaxe hits.")]
    [FormerlySerializedAs("cooldownMelee")]
    public float meleeCooldown = 1.4f;

    [Tooltip("Radius of the pickaxe hitbox (matches the horizontal slash VFX).")]
    [FormerlySerializedAs("raioHitMelee")]
    public float meleeHitRadius = 2.0f;

    [Tooltip("Distância à frente do Goblin onde o golpe de picareta e o slash acontecem.")]
    public float meleeHitForwardOffset = 1.25f;

    [Header("Melee - Slash VFX (Indicação do Golpe)")]
    [Tooltip("Posição local do slash (X=lado, Y=altura do braço esquerdo, Z=frente).")]
    public Vector3 meleeSlashOffset = new Vector3(0.0f, 1.05f, 0.95f);
    [Tooltip("Rotação local em graus (Z=180 inverte o sentido para varrer da esquerda para a direita na horizontal).")]
    public Vector3 meleeSlashEulerAngles = new Vector3(0f, 42f, 180f);
    [Tooltip("Escala do slash horizontal da picareta.")]
    public float meleeSlashScale = 1.50f;
    [Tooltip("Espelhamento opcional de escala X (deixe desmarcado usando Z=180 para inversão limpa em ParticleSystem).")]
    public bool meleeSlashFlipX = false;
    [Tooltip("Velocidade de reprodução do VFX de slash.")]
    public float meleeSlashSimSpeed = 0.60f;
    [Tooltip("Cor do slash normal da picareta.")]
    public Color meleeSlashTint = new Color(1.0f, 0.70f, 0.24f, 0.88f);
    [Tooltip("Cor do slash quando conectado ao Crystal Tuner.")]
    public Color tunerMeleeSlashTint = new Color(0.28f, 0.92f, 1.0f, 0.90f);
    [Tooltip("Duração do GameObject do VFX de slash antes de destruir.")]
    public float meleeSlashLifetime = 1.3f;

    [Header("Telegraph (Pilar 4)")]
    [Tooltip("Multiplicador geral da velocidade da animação de ataque melee (menor = mais lento, maior = mais rápido).")]
    [Range(0.2f, 2.5f)]
    public float meleeAnimSpeedMultiplier = 1.0f;
    [Tooltip("Momento exato (em segundos após iniciar o ataque) em que o VFX de slash é disparado.")]
    [Range(0f, 2.0f)]
    public float meleeSlashTriggerTime = 0.1f;
    [Tooltip("Tempo entre disparar o VFX de slash e iniciar a janela ativa de dano do arco.")]
    [Range(0f, 0.8f)]
    public float meleeSwingHitDelay = 0.1f;
    [Tooltip("Fator que converte a escala do VFX de slash no raio da área de dano.")]
    public float meleeSlashHitRadiusFactor = 1.30f;
    [Tooltip("Tempo puxando a picareta para trás antes de segurar a antecipação.")]
    public float meleePullbackTime = 0.45f;
    [Tooltip("Velocidade da animação na fase de puxada.")]
    public float meleePullbackAnimSpeed = 1.15f;
    [Tooltip("Pausa de antecipação com o braço armado para o jogador ler o golpe claramente.")]
    public float meleeAnticipationHoldTime = 0.38f;
    [Tooltip("Velocidade da animação durante a pausa de antecipação (menor = mais congelado).")]
    public float meleeHoldAnimSpeed = 0.22f;
    [Tooltip("Velocidade da animação durante a varrida do golpe (menor = golpe mais lento).")]
    public float meleeSwingAnimSpeed = 0.85f;
    [Tooltip("Tempo de recuperação após o impacto da picareta.")]
    public float meleeRecoveryTime = 0.60f;
    [Tooltip("Som de aviso/preparação antes do golpe de picareta.")]
    public AudioClip meleeTelegraphSound;

    // ── Strafe ──────────────────────────────────────────────────────
    [Header("Strafe (Lateral movement when attacking)")]
    [FormerlySerializedAs("strafeChangeDuration")]
    public float strafeChangeDuration = 1.2f;

    // ── Audio ───────────────────────────────────────────────────────
    [Header("Áudio — Movimentação (Passos)")]
    public AudioClip walkSound;
    [Range(0f, 1f)]
    public float walkSoundVolume = 0.5f;

    [Header("Áudio — Ataque (Bomba)")]
    public AudioClip throwSound;
    [Range(0f, 1f)]
    public float throwSoundVolume = 0.8f;

    public AudioClip bombTickSound;
    [Range(0f, 1f)]
    public float bombTickSoundVolume = 0.6f;

    public AudioClip bombExplosionSound;
    [Range(0f, 1f)]
    public float bombExplosionSoundVolume = 1.0f;

    [Header("Áudio — Dano Recebido (Hit)")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    public AudioClip[] dieSounds;
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    // ── Private ─────────────────────────────────────────────────────
    private Rigidbody rb;
    private Rigidbody playerRb;
    private Animator anim;
    private AudioSource audioSource;
    private DummyHealth health;

    private float lastThrowTime = -10f;
    private float strafeTimer;
    private int strafeDir = 1;
    private Vector3 currentVelocity = Vector3.zero;

    // NavMesh pathing para contornar paredes/pilares
    private NavMeshPath navPath;
    private float navPathTimer = 0f;
    private int navCornerIndex = 0;

    // Buff (Crystal Tuner)
    private bool isBuffed = false;
    private float originalChaseSpeed;
    private float originalFleeSpeed;
    private float originalThrowCooldown;

    private enum State { Idle, Chase, RangedAttack, Flee, MeleeAttack }
    private State currentState = State.Idle;
    private bool registeredInBestiary = false;

    private float meleeTimer = 0f;
    private bool isDoingMelee = false;
    private bool isThrowing = false;
    private float throwWatchdogTimer = 0f;
    private Coroutine activeMeleeCoroutine;

    private static readonly Dictionary<(Material, Color32), Material> TintedSlashMaterialCache = new Dictionary<(Material, Color32), Material>();

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        health = GetComponent<DummyHealth>();
        navPath = new NavMeshPath();

        rb.freezeRotation = true;

        if (GetComponent<NavMeshBoundaryConstraint>() == null)
        {
            gameObject.AddComponent<NavMeshBoundaryConstraint>();
        }

        originalChaseSpeed = chaseSpeed;
        originalFleeSpeed = fleeSpeed;
        originalThrowCooldown = throwCooldown;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null)
            playerRb = player.GetComponent<Rigidbody>();

        strafeTimer = strafeChangeDuration;

        if (health != null)
        {
            if (hitSounds != null && hitSounds.Length > 0)
                health.onDamageTaken += (damage, attacker) => PlayHitSound();

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;

            health.onDeath += CleanupOnDeath;
        }
    }

    private void OnDisable()
    {
        isDoingMelee = false;
        isThrowing = false;
        if (anim != null) anim.speed = 1f;
    }

    private void CleanupOnDeath()
    {
        isDoingMelee = false;
        isThrowing = false;
        if (anim != null) anim.speed = 1f;
    }

    private bool IsDead()
    {
        return health != null && health.CurrentHealth <= 0;
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

    // ── Crystal Tuner Buff ──────────────────────────────────────────
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            chaseSpeed = originalChaseSpeed * 1.15f;
            fleeSpeed = originalFleeSpeed * 1.12f;
            throwCooldown = originalThrowCooldown * 0.72f;
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            chaseSpeed = originalChaseSpeed;
            fleeSpeed = originalFleeSpeed;
            throwCooldown = originalThrowCooldown;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    void Update()
    {
        if (player == null || IsDead()) return;

        if (meleeTimer > 0f) meleeTimer -= Time.deltaTime;

        // Segurança: se o evento de animação for interrompido por transição, libera isThrowing
        if (isThrowing)
        {
            throwWatchdogTimer += Time.deltaTime;
            if (throwWatchdogTimer > 1.4f)
            {
                isThrowing = false;
                throwWatchdogTimer = 0f;
            }
        }

        float dist = GetHorizontalDistance(transform.position, player.position);
        UpdateState(dist);

        if (currentState == State.Chase)
            UpdateNavPath();

        if (isThrowing)
        {
            Vector3 dirToPlayer = player.position - transform.position;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToPlayer), Time.deltaTime * 18f);
        }
    }

    void FixedUpdate()
    {
        if (player == null || IsDead())
        {
            currentVelocity = Vector3.zero;
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        ExecuteMovement();
    }

    // ── Footstep Events ─────────────────────────────────────────────
    public void AnimationEvent_Step() => PlayStepSound();
    public void OnStep() => PlayStepSound();
    public void Passo() => PlayStepSound();
    public void Footstep() => PlayStepSound();
    public void PlayWalkSound() => PlayStepSound();

    public void PlayStepSound()
    {
        if (walkSound == null || IsDead()) return;
        float pitch = Random.Range(0.9f, 1.1f);
        PlayClipAtPointWithPitch(walkSound, transform.position, pitch, walkSoundVolume);
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

    // ─────────────────────────────────────────────────────────────────
    void UpdateState(float dist)
    {
        if (isDoingMelee) return;

        if (isThrowing && dist <= cancelThrowDistance)
            CancelThrow();

        // Priority: Melee > Flee > Ranged Attack > Chase > Idle
        if (dist <= meleeDistance)         ChangeState(State.MeleeAttack);
        else if (dist < fleeDistance)      ChangeState(State.Flee);
        else if (dist <= throwDistance)    ChangeState(State.RangedAttack);
        else if (dist <= chaseDistance)    ChangeState(State.Chase);
        else                               ChangeState(State.Idle);

        switch (currentState)
        {
            case State.RangedAttack:
                TryThrowBomb();
                UpdateStrafe();
                break;
            case State.MeleeAttack:
                TryMeleeAttack();
                break;
        }
    }

    void ChangeState(State newState)
    {
        if (currentState == newState) return;

        if (!registeredInBestiary && currentState == State.Idle && newState != State.Idle)
        {
            registeredInBestiary = true;
            EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
            if (id != null && BestiarioManager.instancia != null)
                BestiarioManager.instancia.Registrar(id);
        }

        currentState = newState;
    }

    private void UpdateNavPath()
    {
        navPathTimer -= Time.deltaTime;
        if (navPathTimer > 0f) return;
        navPathTimer = 0.2f;

        if (navPath == null) navPath = new NavMeshPath();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 3f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(player.position, out NavMeshHit targetHit, 3f, NavMesh.AllAreas))
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

    private Vector3 ComputeChaseDirection()
    {
        if (navPath != null && navPath.corners != null && navPath.corners.Length > 1)
        {
            while (navCornerIndex < navPath.corners.Length - 1)
            {
                Vector3 toCorner = navPath.corners[navCornerIndex] - transform.position;
                toCorner.y = 0f;
                if (toCorner.magnitude < 0.7f) navCornerIndex++;
                else break;
            }

            if (navCornerIndex < navPath.corners.Length)
            {
                Vector3 dir = navPath.corners[navCornerIndex] - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }
        }

        return DirectionTo(player.position);
    }

    // ─────────────────────────────────────────────────────────────────
    void ExecuteMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        switch (currentState)
        {
            case State.Chase:
                targetVelocity = ComputeChaseDirection() * chaseSpeed;
                break;

            case State.Flee:
                targetVelocity = FleeDirection() * fleeSpeed;
                break;

            case State.MeleeAttack:
                targetVelocity = Vector3.zero;
                break;

            case State.RangedAttack:
                if (isThrowing)
                {
                    targetVelocity = Vector3.zero;
                }
                else
                {
                    Vector3 toPlayer = DirectionTo(player.position);
                    Vector3 lateral = Vector3.Cross(Vector3.up, toPlayer) * strafeDir * strafeSpeed;
                    targetVelocity = new Vector3(lateral.x, 0f, lateral.z);
                }
                break;

            case State.Idle:
            default:
                targetVelocity = Vector3.zero;
                break;
        }

        // Previne sair do chão / NavMesh ou colidir de frente em paredes durante fuga/strafe
        if (targetVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 moveDir = new Vector3(targetVelocity.x, 0f, targetVelocity.z).normalized;
            Vector3 lookAheadPos = transform.position + moveDir * 1.15f;

            bool hasGroundAhead = Physics.Raycast(lookAheadPos + Vector3.up * 1.2f, Vector3.down, 3.5f, (1 << 0) | (1 << 3), QueryTriggerInteraction.Ignore);
            bool hasNavMeshAhead = true;
            if (NavMesh.SamplePosition(transform.position, out _, 2.0f, NavMesh.AllAreas))
            {
                hasNavMeshAhead = NavMesh.SamplePosition(lookAheadPos, out _, 0.65f, NavMesh.AllAreas);
            }

            if (!hasGroundAhead || !hasNavMeshAhead)
            {
                if (currentState == State.RangedAttack)
                {
                    strafeDir = -strafeDir;
                    targetVelocity = Vector3.zero;
                }
                else if (currentState == State.Flee)
                {
                    // Escapa lateralmente ao longo da parede/borda em vez de travar
                    Vector3 sideDir = Vector3.Cross(Vector3.up, moveDir) * strafeDir;
                    Vector3 sideLookAhead = transform.position + sideDir * 1.15f;
                    bool sideValid = Physics.Raycast(sideLookAhead + Vector3.up * 1.2f, Vector3.down, 3.5f, (1 << 0) | (1 << 3), QueryTriggerInteraction.Ignore) &&
                                     NavMesh.SamplePosition(sideLookAhead, out _, 0.65f, NavMesh.AllAreas);
                    if (sideValid)
                    {
                        targetVelocity = sideDir * fleeSpeed;
                    }
                    else
                    {
                        strafeDir = -strafeDir;
                        targetVelocity = Vector3.zero;
                    }
                }
                else
                {
                    targetVelocity = Vector3.zero;
                }
            }
        }

        Vector3 desiredFlat = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        bool isBraking = desiredFlat.sqrMagnitude < 0.01f ||
                         (currentVelocity.sqrMagnitude > 0.25f && Vector3.Dot(currentVelocity, desiredFlat) < 0f);
        float moveRate = isBraking ? deceleration : acceleration;

        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            desiredFlat,
            moveRate * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);

        // Sempre olha para a direção em que está correndo (elimina deslize lateral / moonwalk);
        // ao parar ou arremessar, vira suavemente para o jogador.
        if (currentVelocity.sqrMagnitude > 0.12f && !isThrowing && !isDoingMelee)
        {
            LookAtDirection(currentVelocity.normalized, 14f);
        }
        else if (player != null && !isThrowing && !isDoingMelee)
        {
            LookAtPlayer(12f);
        }

        // Sincronização dinâmica da passada de RunningGoblin com a velocidade real no chão
        float currentSpeedMagnitude = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
        bool isRunning = currentSpeedMagnitude > 0.25f && !isThrowing && !isDoingMelee;

        anim.SetBool("Running", isRunning);
        if (!isDoingMelee)
            anim.speed = 1.0f;

        if (isRunning)
        {
            float refSpeed = Mathf.Max(0.5f, strideReferenceSpeed);
            float runSpeedParam = Mathf.Clamp(currentSpeedMagnitude / refSpeed, 0.75f, 2.4f);
            anim.SetFloat("RunSpeed", runSpeedParam);
        }
        else
        {
            anim.SetFloat("RunSpeed", 1.0f);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    void UpdateStrafe()
    {
        strafeTimer -= Time.deltaTime;
        if (strafeTimer <= 0f)
        {
            strafeDir = (Random.value > 0.5f) ? 1 : -1;
            strafeTimer = strafeChangeDuration + Random.Range(-0.3f, 0.5f);
        }
    }

    void TryThrowBomb()
    {
        if (!isThrowing && Time.time >= lastThrowTime + throwCooldown)
        {
            isThrowing = true;
            throwWatchdogTimer = 0f;
            anim.SetTrigger("Attacking");
            lastThrowTime = Time.time;
        }
    }

    void CancelThrow()
    {
        if (!isThrowing) return;

        isThrowing = false;
        throwWatchdogTimer = 0f;
        anim.ResetTrigger("Attacking");
        anim.CrossFade("IdleGoblin", 0.1f);
    }

    // ─────────────────────────────────────────────────────────────────
    void TryMeleeAttack()
    {
        if (isDoingMelee || meleeTimer > 0f) return;
        if (isThrowing) CancelThrow();

        activeMeleeCoroutine = StartCoroutine(ExecuteMeleeRoutine());
    }

    IEnumerator ExecuteMeleeRoutine() =>
        DummyHealth.PauseAttackWhileStunned(this, ExecuteMeleeRoutineSteps());

    private IEnumerator ExecuteMeleeRoutineSteps()
    {
        isDoingMelee = true;
        meleeTimer = meleeCooldown;

        float speedMult = Mathf.Clamp(meleeAnimSpeedMultiplier, 0.2f, 2.5f);

        anim.speed = Mathf.Clamp(meleePullbackAnimSpeed * speedMult, 0.05f, 4.0f);
        anim.ResetTrigger("MeleeAttack");
        anim.SetTrigger("MeleeAttack");

        if (meleeTelegraphSound != null)
            PlayClipAtPointWithPitch(meleeTelegraphSound, transform.position, 1.05f, 0.7f);
        else if (throwSound != null)
            PlayClipAtPointWithPitch(throwSound, transform.position, 1.25f, 0.4f);

        float scaleFactor = Mathf.Max(0.75f, transform.localScale.y / 1.25f);
        float pullbackEnd = Mathf.Max(0.05f, meleePullbackTime) / speedMult;
        float holdDuration = Mathf.Max(0.0f, meleeAnticipationHoldTime) / speedMult;
        float holdEnd = pullbackEnd + holdDuration;
        float trackingEnd = Mathf.Min(Mathf.Max(0.05f, meleeSlashTriggerTime), pullbackEnd + holdDuration * 0.40f);

        float slashTime = Mathf.Max(0.0f, meleeSlashTriggerTime);
        float hitWindowStart = slashTime + Mathf.Max(0.02f, meleeSwingHitDelay);
        float simSpeed = Mathf.Clamp(meleeSlashSimSpeed, 0.25f, 2.5f);
        float hitWindowEnd = hitWindowStart + (0.42f / simSpeed);
        float endTime = Mathf.Max(holdEnd, hitWindowEnd) + Mathf.Max(0.15f, meleeRecoveryTime);

        Vector3 lockedForward = transform.forward;
        lockedForward.y = 0f;
        if (lockedForward.sqrMagnitude < 0.001f) lockedForward = Vector3.forward;
        lockedForward.Normalize();

        bool spawnedSlash = false;
        bool hasHitPlayer = false;
        Vector3 vfxCenter = transform.position + lockedForward * (meleeHitForwardOffset * scaleFactor) + Vector3.up * (meleeSlashOffset.y * scaleFactor);
        float vfxRadius = Mathf.Max(0.9f, meleeSlashScale * scaleFactor * Mathf.Max(0.8f, meleeSlashHitRadiusFactor));
        float totalElapsed = 0f;

        while (totalElapsed < endTime)
        {
            if (IsDead())
            {
                anim.speed = 1f;
                isDoingMelee = false;
                activeMeleeCoroutine = null;
                yield break;
            }

            // Controla a velocidade da animação em cada fase respeitando o multiplicador geral
            if (totalElapsed < pullbackEnd)
                anim.speed = Mathf.Clamp(meleePullbackAnimSpeed * speedMult, 0.05f, 4.0f);
            else if (totalElapsed < holdEnd)
                anim.speed = Mathf.Clamp(meleeHoldAnimSpeed * speedMult, 0.01f, 4.0f);
            else
                anim.speed = Mathf.Clamp(meleeSwingAnimSpeed * speedMult, 0.05f, 4.0f);

            // Acompanha o jogador apenas antes do VFX travar a direção do corte
            if (!spawnedSlash && totalElapsed < trackingEnd)
            {
                LookAtPlayer(14f);
                lockedForward = transform.forward;
                lockedForward.y = 0f;
                if (lockedForward.sqrMagnitude > 0.001f) lockedForward.Normalize();
            }

            // Dispara o VFX de slash e fixa área + duração de dano exatamente pelo VFX na tela
            if (!spawnedSlash && totalElapsed >= slashTime)
            {
                spawnedSlash = true;
                Quaternion lockedRot = Quaternion.LookRotation(lockedForward);
                float visibleDuration = SpawnMeleeSlashVfx(transform.position, lockedRot, scaleFactor, out vfxCenter, out vfxRadius);
                hitWindowEnd = Mathf.Max(hitWindowStart + 0.08f, slashTime + visibleDuration);
                endTime = Mathf.Max(holdEnd, hitWindowEnd) + Mathf.Max(0.15f, meleeRecoveryTime);
            }

            // Janela ativa de dano: enquanto o VFX de slash está visível na tela, quem estiver na área dele recebe o golpe (1x)
            if (spawnedSlash && !hasHitPlayer && totalElapsed >= hitWindowStart && totalElapsed <= hitWindowEnd)
            {
                Vector3 flatCenter = new Vector3(vfxCenter.x, 0f, vfxCenter.z);
                Collider[] hits = Physics.OverlapSphere(vfxCenter, vfxRadius + 0.5f);

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider hit = hits[i];
                    if (!hit.CompareTag("Player")) continue;

                    // Checa distância no plano XZ e tolerância vertical para casar com o arco horizontal do VFX
                    Vector3 targetPos = hit.transform.position;
                    if (Mathf.Abs(targetPos.y - vfxCenter.y) > 2.2f) continue;

                    Vector3 flatTarget = new Vector3(targetPos.x, 0f, targetPos.z);
                    if (Vector3.Distance(flatCenter, flatTarget) > vfxRadius) continue;

                    PlayerHealth ph = hit.GetComponent<PlayerHealth>() ?? hit.GetComponentInParent<PlayerHealth>();
                    if (ph != null)
                    {
                        hasHitPlayer = true;
                        ph.TakeDamage(meleeDamage, gameObject);

                        if (meleeImpactVfxPrefab != null)
                        {
                            Vector3 sparkPos = Vector3.Lerp(vfxCenter, targetPos + Vector3.up * 0.9f, 0.55f);
                            GameObject spark = Instantiate(meleeImpactVfxPrefab, sparkPos, Quaternion.identity);
                            spark.transform.localScale = Vector3.one * (0.8f * scaleFactor);
                            Destroy(spark, 1.2f);
                        }
                        break;
                    }
                }
            }

            totalElapsed += Time.deltaTime;
            yield return null;
        }

        anim.speed = 1f;
        isDoingMelee = false;
        activeMeleeCoroutine = null;
    }

    private float SpawnMeleeSlashVfx(Vector3 originPos, Quaternion originRot, float scaleFactor, out Vector3 worldCenter, out float worldRadius)
    {
        worldCenter = originPos + (originRot * (meleeSlashOffset * scaleFactor));
        worldRadius = Mathf.Max(0.9f, meleeSlashScale * scaleFactor * Mathf.Max(0.8f, meleeSlashHitRadiusFactor));
        float simSpeed = Mathf.Clamp(meleeSlashSimSpeed, 0.25f, 2.5f);
        float visibleDuration = 0.45f / simSpeed;

        if (meleeSlashVfxPrefab == null) return visibleDuration;

        Quaternion spawnRot = originRot * Quaternion.Euler(meleeSlashEulerAngles);
        GameObject vfx = Instantiate(meleeSlashVfxPrefab, worldCenter, spawnRot);
        float s = Mathf.Max(0.1f, meleeSlashScale * scaleFactor);
        vfx.transform.localScale = new Vector3(meleeSlashFlipX ? -s : s, s, s);

        Color tint = isBuffed ? tunerMeleeSlashTint : meleeSlashTint;
        ParticleSystem rootPs = vfx.GetComponent<ParticleSystem>();
        if (rootPs != null)
        {
            // Usa 85% do tempo de vida real da partícula principal (arco brilhante na tela, excluindo o fade final)
            visibleDuration = Mathf.Clamp((rootPs.main.startLifetime.constantMax / simSpeed) * 0.85f, 0.12f, 1.4f);
        }

        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.simulationSpeed = simSpeed;
            Color currentStart = main.startColor.color;
            main.startColor = currentStart * tint;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null)
                psr.sharedMaterial = GetOrCreateTintedMaterial(psr.sharedMaterial, tint);

            ps.Play(true);
        }

        Destroy(vfx, Mathf.Max(visibleDuration + 0.45f, meleeSlashLifetime));
        return visibleDuration;
    }

    private static Material GetOrCreateTintedMaterial(Material template, Color tint)
    {
        if (template == null) return null;
        var key = (template, (Color32)tint);
        if (TintedSlashMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material mat = new Material(template);
        Color subtleHdr = new Color(tint.r * 1.15f, tint.g * 1.15f, tint.b * 1.15f, tint.a);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", subtleHdr);
        if (mat.HasProperty("_MainColor")) mat.SetColor("_MainColor", tint);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        TintedSlashMaterialCache[key] = mat;
        return mat;
    }

    // ─────────────────────────────────────────────────────────────────
    // Chamado pelo Animation Event em ThrowGoblin.anim exatamente quando a mão lança a bomba
    public void AnimationEvent_ThrowBomb()
    {
        isThrowing = false;
        throwWatchdogTimer = 0f;

        if (IsDead() || bombPrefab == null || throwPoint == null) return;

        if (throwSound != null)
            PlayClipAtPointWithPitch(throwSound, throwPoint.position, Random.Range(0.95f, 1.08f), throwSoundVolume);

        // Calcula o alvo no chão com leve previsão de movimento do jogador
        Vector3 targetWorld = (player != null) ? player.position : (transform.position + transform.forward * 7f);
        float rawDist = GetHorizontalDistance(throwPoint.position, targetWorld);
        float flightTime = Mathf.Clamp(rawDist / 10.5f, 0.55f, 1.05f);

        if (player != null && playerRb != null && bombLeadFactor > 0f)
        {
            Vector3 flatVel = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            Vector3 leadOffset = Vector3.ClampMagnitude(flatVel * flightTime * bombLeadFactor, 2.2f);
            targetWorld += leadOffset;
        }

        Vector3 landingPos = GetGroundPosition(targetWorld, player != null ? player.position.y : transform.position.y);

        GameObject bomb = Instantiate(bombPrefab, throwPoint.position, Quaternion.identity);

        // Ignora colisão com o próprio Goblin
        Collider cBomb = bomb.GetComponent<Collider>();
        Collider cGoblin = GetComponent<Collider>();
        if (cBomb != null && cGoblin != null)
            Physics.IgnoreCollision(cBomb, cGoblin);

        BombaExplosiva script = bomb.GetComponent<BombaExplosiva>();
        if (script != null)
        {
            script.owner = gameObject;
            float baseRad = bombBaseRadius > 0.1f ? bombBaseRadius : script.raioExplosao;
            script.raioExplosao = isBuffed ? baseRad * tunerBombRadiusMultiplier : baseRad;

            if (bombTickSound != null)
            {
                script.tickSound = bombTickSound;
                script.tickSoundVolume = bombTickSoundVolume;
            }
            if (bombExplosionSound != null)
            {
                script.explosionSound = bombExplosionSound;
                script.explosionSoundVolume = bombExplosionSoundVolume;
            }

            // Indicador limpo no chão no ponto exato onde a parábola vai aterrissar
            Color? telegraphCol = isBuffed ? new Color(0.25f, 0.88f, 1f, 1f) : (Color?)null;
            script.SetLandingTelegraph(landingPos, flightTime, telegraphCol);
        }

        // Aplica velocidade balística exata para atingir landingPos no tempo flightTime
        Rigidbody rbBomb = bomb.GetComponent<Rigidbody>();
        if (rbBomb != null)
        {
            rbBomb.WakeUp();
            Vector3 launchVel = ComputeBallisticVelocity(throwPoint.position, landingPos + Vector3.up * 0.25f, flightTime);
            rbBomb.linearVelocity = launchVel;
            rbBomb.angularVelocity = Random.insideUnitSphere * 6f;
        }
    }

    private static Vector3 ComputeBallisticVelocity(Vector3 start, Vector3 target, float time)
    {
        float t = Mathf.Max(0.25f, time);
        Vector3 delta = target - start;
        float vx = delta.x / t;
        float vz = delta.z / t;
        float vy = (delta.y - 0.5f * Physics.gravity.y * t * t) / t;
        return new Vector3(vx, vy, vz);
    }

    private Vector3 GetGroundPosition(Vector3 worldPos, float referenceY)
    {
        int groundMask = (1 << 0) | (1 << 3);
        Vector3 rayOrigin = new Vector3(worldPos.x, referenceY + 2.5f, worldPos.z);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, groundMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.normal.y < 0.55f) continue;
            if (h.point.y > referenceY + 1.0f) continue;
            if (h.point.y > bestY)
            {
                bestY = h.point.y;
                found = true;
            }
        }

        if (found)
            return new Vector3(worldPos.x, bestY, worldPos.z);

        if (NavMesh.SamplePosition(worldPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            return new Vector3(worldPos.x, Mathf.Max(navHit.position.y, referenceY), worldPos.z);

        return new Vector3(worldPos.x, referenceY, worldPos.z);
    }

    // ─────────────────────────────────────────────────────────────────
    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    Vector3 DirectionTo(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
    }

    Vector3 FleeDirection()
    {
        Vector3 dir = transform.position - player.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
    }

    void LookAtPlayer(float turnSpeed = 10f)
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            LookAtDirection(dir, turnSpeed);
    }

    void LookAtDirection(Vector3 dir, float turnSpeed = 10f)
    {
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * turnSpeed);
    }
}
