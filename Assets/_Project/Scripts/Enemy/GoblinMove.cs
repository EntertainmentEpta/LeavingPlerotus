using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

/// <summary>
/// Goblin AI. States: Idle → Pursue → Strafe/Attack → Flee → MeleePickaxe
/// Underground miner: throws bombs from a distance and attacks with a pickaxe in melee.
/// Smooth movement via MoveTowards on the Rigidbody.
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

    // ── Distances ───────────────────────────────────────────────────
    [Header("Distances")]
    [Tooltip("Minimum distance to keep from the player (triggers fleeing).")]
    [FormerlySerializedAs("distanciaFuga")]
    public float fleeDistance = 6f;
    
    [Tooltip("Ideal distance to throw bombs. The Goblin tries to stay here.")]
    [FormerlySerializedAs("distanciaAtaque")]
    public float throwDistance = 12f;
    
    [Tooltip("Maximum distance to start pursuing the player.")]
    [FormerlySerializedAs("distanciaMaxBusca")]
    public float chaseDistance = 28f;

    // ── Speeds ──────────────────────────────────────────────────
    [Header("Speeds")]
    [FormerlySerializedAs("velocidadePerseguicao")]
    public float chaseSpeed = 4.2f;
    
    [FormerlySerializedAs("velocidadeFuga")]
    public float fleeSpeed = 5.2f;
    
    [FormerlySerializedAs("velocidadeStrafe")]
    public float strafeSpeed = 2.5f;
    
    [Tooltip("Movement acceleration (higher = responsive ground control without ice-skating).")]
    [FormerlySerializedAs("aceleracao")]
    public float acceleration = 10.0f;

    // ── Ranged Attack (Bomb) ────────────────────────────────────
    [Header("Ranged - Bomb Throw")]
    [FormerlySerializedAs("forcaArremesso")]
    public float throwForce = 12f;
    
    [FormerlySerializedAs("forcaArco")]
    public float throwArcForce = 6f;
    
    [FormerlySerializedAs("intervaloAtaque")]
    public float throwCooldown = 2.8f;
    
    [Tooltip("If the player enters this distance while the Goblin is winding up a throw, the throw is cancelled.")]
    public float cancelThrowDistance = 4f;

    // ── Melee Attack (Pickaxe) ───────────────────────────────────────
    [Header("Melee - Pickaxe")]
    [Tooltip("Max distance for melee attack (overrides fleeing).")]
    [FormerlySerializedAs("distanciaMelee")]
    public float meleeDistance = 2.5f;
    
    [Tooltip("Damage dealt by the pickaxe hit.")]
    [FormerlySerializedAs("danoMelee")]
    public int meleeDamage = 15;
    
    [Tooltip("Cooldown between pickaxe hits.")]
    [FormerlySerializedAs("cooldownMelee")]
    public float meleeCooldown = 1.5f;
    
    [Tooltip("Radius of the pickaxe hitbox.")]
    [FormerlySerializedAs("raioHitMelee")]
    public float meleeHitRadius = 1.8f;

    // ── Strafe ───────────────────────────────────────────────────────
    [Header("Strafe (Lateral movement when attacking)")]
    [Tooltip("Duration of each strafe cycle before changing direction.")]
    [FormerlySerializedAs("strafeChangeDuration")]
    public float strafeChangeDuration = 1.2f;

    // ── Audio ────────────────────────────────────────────────────────
    [Header("Áudio")]
    [Tooltip("Som único de passo do Goblin (tocado a cada stepInterval)")]
    public AudioClip walkSound;
    [Tooltip("Volume do som de passo")]
    [Range(0f, 1f)]
    public float walkSoundVolume = 0.5f;
    [Tooltip("Intervalo entre cada passo enquanto caminha em segundos (ex: 0.22s)")]
    public float stepInterval = 0.22f;
    private float stepTimer = 0f;

    [Tooltip("Som do arremesso da bomba pelo Goblin")]
    public AudioClip throwSound;
    [Tooltip("Volume do som de arremesso")]
    [Range(0f, 1f)]
    public float throwSoundVolume = 0.8f;

    [Tooltip("Som de tick da bomba acesa voando no ar")]
    public AudioClip bombTickSound;
    [Tooltip("Volume do som de tick da bomba")]
    [Range(0f, 1f)]
    public float bombTickSoundVolume = 0.6f;

    [Tooltip("Som da explosão da bomba ao impactar")]
    public AudioClip bombExplosionSound;
    [Tooltip("Volume do som da explosão da bomba")]
    [Range(0f, 1f)]
    public float bombExplosionSoundVolume = 1.0f;

    // ── Private ─────────────────────────────────────────────────────    
    private Rigidbody rb;
    private Animator anim;
    private AudioSource audioSource;
    private float lastThrowTime;
    private float strafeTimer;
    private int strafeDir = 1;
    private Vector3 currentVelocity = Vector3.zero;

    // Buff (Crystal Tuner)
    private bool isBuffed = false;
    private float originalChaseSpeed;
    private float originalFleeSpeed;
    private float originalThrowCooldown;

    // Simple State Machine
    private enum State { Idle, Chase, RangedAttack, Flee, MeleeAttack }
    private State currentState = State.Idle;
    private bool registeredInBestiary = false;

    // Attack Flags
    private float meleeTimer = 0f;
    private bool isDoingMelee = false;
    private bool isThrowing = false; // Tracks if a bomb throw animation is currently active

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        rb.freezeRotation = true;

        originalChaseSpeed = chaseSpeed;
        originalFleeSpeed = fleeSpeed;
        originalThrowCooldown = throwCooldown;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        strafeTimer = strafeChangeDuration;
    }

    // ── Crystal Tuner Buff ─────────────────────────────────────────
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            chaseSpeed *= 1.15f;
            fleeSpeed *= 1.15f;
            throwCooldown /= 2f;
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
        if (player == null) return;

        if (meleeTimer > 0f) meleeTimer -= Time.deltaTime;

        float dist = Vector3.Distance(transform.position, player.position);
        UpdateState(dist);

        // Durante o arremesso: trava a rotação na direção do player (snap imediato)
        // Impede que o Goblin jogue de costas durante a transição de strafe
        if (isThrowing)
        {
            Vector3 dirToPlayer = player.position - transform.position;
            dirToPlayer.y = 0;
            if (dirToPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }
        else if (currentState != State.Flee)
        {
            LookAtPlayer();
        }
    }

    void FixedUpdate()
    {
        if (player == null) return;
        ExecuteMovement();
        HandleWalkSound();
    }

    private void HandleWalkSound()
    {
        if (walkSound == null) return;
        if (rb == null) return;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVel.magnitude > 0.1f)
        {
            stepTimer -= Time.fixedDeltaTime;
            if (stepTimer <= 0f)
            {
                PlayWalkSound();
                stepTimer = stepInterval;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }

    private void PlayWalkSound()
    {
        if (walkSound == null) return;

        float pitch = Random.Range(0.9f, 1.1f);
        PlayClipAtPointWithPitch(walkSound, transform.position, pitch, walkSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
        {
            if (audioSource != null)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, volume);
            }
        }
    // ─────────────────────────────────────────────────────────────────
    void UpdateState(float dist)
    {
        // Cancel Throw Logic
        if (isThrowing && dist <= cancelThrowDistance)
        {
            CancelThrow();
        }

        // Priority: Melee > Flee > Ranged Attack > Chase > Idle
        if (dist <= meleeDistance)         ChangeState(State.MeleeAttack);
        else if (dist < fleeDistance)      ChangeState(State.Flee);
        else if (dist <= throwDistance)    ChangeState(State.RangedAttack);
        else if (dist <= chaseDistance)    ChangeState(State.Chase);
        else                               ChangeState(State.Idle);

        // State Logic execution
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

        // Register in Bestiary the first time it leaves Idle
        if (!registeredInBestiary && currentState == State.Idle && newState != State.Idle)
        {
            registeredInBestiary = true;
            EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
            if (id != null && BestiarioManager.instancia != null)
                BestiarioManager.instancia.Registrar(id);
        }

        currentState = newState;
        // Não seta Running aqui — isso é feito por velocidade real em ExecuteMovement
    }

    // ─────────────────────────────────────────────────────────────────
    void ExecuteMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        switch (currentState)
        {
            case State.Chase:
                targetVelocity = DirectionTo(player.position) * chaseSpeed;
                break;

            case State.Flee:
                targetVelocity = FleeDirection() * fleeSpeed;
                break;

            case State.MeleeAttack:
                // Para completamente para atacar
                targetVelocity = Vector3.zero;
                break;

            case State.RangedAttack:
                // Para completamente durante o arremesso; strafe suave quando livre
                if (isThrowing)
                    targetVelocity = Vector3.zero;
                else
                {
                    Vector3 lateral = transform.right * strafeDir * strafeSpeed * 0.3f;
                    targetVelocity = new Vector3(lateral.x, 0, lateral.z);
                }
                break;

            case State.Idle:
            default:
                targetVelocity = Vector3.zero;
                break;
        }

        // Suaviza a aceleração de forma responsiva no chão
        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            new Vector3(targetVelocity.x, 0, targetVelocity.z),
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);

        // Alinha a rotação do corpo com a velocidade de deslocamento real (evita rotações abruptas no ar)
        if (currentVelocity.sqrMagnitude > 0.15f)
        {
            LookAtDirection(currentVelocity.normalized);
        }
        else if (player != null && !isThrowing && !isDoingMelee)
        {
            LookAtPlayer();
        }

        // 🏃 SINCRONIZAÇÃO DE ANIMAÇÃO DINÂMICA (Elimina a sensação de "patinar no chão")
        float currentSpeedMagnitude = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
        bool isRunning = currentSpeedMagnitude > 0.8f && !isThrowing && !isDoingMelee;

        anim.SetBool("Running", isRunning);

        if (isRunning && chaseSpeed > 0.1f)
        {
            // Ajusta o speed do Animator proporcionalmente à velocidade física real
            float speedRatio = currentSpeedMagnitude / chaseSpeed;
            anim.speed = Mathf.Clamp(speedRatio * 1.15f, 0.7f, 1.4f);
        }
        else
        {
            anim.speed = 1.0f;
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
            anim.SetTrigger("Attacking");
            lastThrowTime = Time.time;
        }
    }

    void CancelThrow()
    {
        if (!isThrowing) return;
        
        isThrowing = false;
        anim.ResetTrigger("Attacking");
        
        // Force the animator to abort the Throw animation immediately
        // CrossFade smoothly transitions to Idle, interrupting the attack.
        anim.CrossFade("Idle", 0.1f);
        Debug.Log("[GOBLIN] Throw cancelled due to player proximity!");
    }

    // ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Melee Pickaxe Attack.
    /// </summary>
    void TryMeleeAttack()
    {
        if (isDoingMelee || meleeTimer > 0f) return;
        
        // If we were throwing, definitely cancel it to prioritize melee
        if (isThrowing) CancelThrow();

        StartCoroutine(ExecuteMeleeRoutine());
    }

    IEnumerator ExecuteMeleeRoutine()
    {
        isDoingMelee = true;
        meleeTimer = meleeCooldown;

        // Wind-up
        anim.SetTrigger("MeleeAttack");

        yield return new WaitForSeconds(0.35f); // Swing timing

        // Hitbox check
        Vector3 hitCenter = transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(hitCenter, meleeHitRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerHealth ph = hit.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(meleeDamage, gameObject);
                    Debug.Log("[GOBLIN] Melee hit player! Damage: " + meleeDamage);
                }
                break;
            }
        }

        yield return new WaitForSeconds(0.4f); // Recovery
        isDoingMelee = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Called by Unity Animation Event exactly when the hand goes forward
    public void AnimationEvent_ThrowBomb()
    {
        isThrowing = false; // Throw completed successfully

        if (bombPrefab == null || throwPoint == null) return;

        // Toca o som do arremesso da bomba
        if (throwSound != null)
        {
            PlayClipAtPointWithPitch(throwSound, throwPoint.position, 1f, throwSoundVolume);
        }

        GameObject bomb = Instantiate(bombPrefab, throwPoint.position, Quaternion.identity);

        // Ignore collision with the Goblin itself
        Collider cBomb = bomb.GetComponent<Collider>();
        Collider cGoblin = GetComponent<Collider>();
        if (cBomb != null && cGoblin != null)
            Physics.IgnoreCollision(cBomb, cGoblin);

        // Pass references, audio settings, and buff stats
        BombaExplosiva script = bomb.GetComponent<BombaExplosiva>();
        if (script != null)
        {
            script.owner = gameObject;
            script.raioExplosao = isBuffed ? 4f : 2f;

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
        }

        // Apply parabolic force towards player
        Rigidbody rbBomb = bomb.GetComponent<Rigidbody>();
        if (rbBomb != null)
        {
            rbBomb.WakeUp();
            Vector3 direction = (player != null)
                ? (player.position - throwPoint.position).normalized
                : transform.forward;

            Vector3 force = direction * throwForce + Vector3.up * throwArcForce;
            rbBomb.AddForce(force, ForceMode.Impulse);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    Vector3 DirectionTo(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        return dir.normalized;
    }

    Vector3 FleeDirection()
    {
        Vector3 dir = transform.position - player.position;
        dir.y = 0;
        return dir.normalized;
    }

    void LookAtPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            LookAtDirection(dir);
    }

    void LookAtDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
    }
}
