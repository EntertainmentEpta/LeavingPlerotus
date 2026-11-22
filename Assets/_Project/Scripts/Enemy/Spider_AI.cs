using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// IA da Aranha — Predadora ágil de "bate e recua" (Hit-and-Run):
/// • Persegue usando NavMesh + checagem de bordas/paredes.
/// • Ataca com um dash rápido e rente ao chão (sem pulo alto travando no ar).
/// • Característica principal (Retreat): logo após investir, recua rapidamente rente ao chão
///   mantendo o olhar no player (desviando na diagonal se houver parede atrás).
/// • Elite: 3 dashes rápidos em sequência finalizando com recuo.
/// • Crystal Tuner: mais rápida, menor cooldown e rastro/after-image ciano.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class Spider_AI : MonoBehaviour
{
    private Transform playerTransform;
    private DummyHealth health;
    private Rigidbody rb;
    private SpiderDashVFX dashVFX;
    private AudioSource audioSource;

    [Header("Ativação")]
    public float activationDistance = 20f;
    private bool isActivated = false;

    [Header("Movimento")]
    [Tooltip("Velocidade de movimentação em direção ao player.")]
    public float moveSpeed = 6.2f;
    [Tooltip("Velocidade de rotação para olhar o player.")]
    public float rotationSpeed = 12f;

    [Header("Ataque (Dash Rente ao Chão)")]
    [Tooltip("Velocidade horizontal do dash de ataque.")]
    public float leapForce = 16f;
    [Tooltip("Elevação vertical (0 = totalmente rente ao chão).")]
    public float leapUpForce = 0f;
    [Tooltip("Distância para iniciar o dash de ataque.")]
    public float leapRange = 6f;
    [Tooltip("Cooldown entre ataques.")]
    public float leapCooldown = 1.8f;
    [Tooltip("Dano causado pelo dash.")]
    public int leapDamage = 12;
    [Tooltip("Raio da hitbox durante o dash.")]
    public float leapHitRadius = 1.45f;
    [Tooltip("Duração do dash para frente em segundos.")]
    public float leapDuration = 0.24f;
    [Tooltip("Pequena pausa de recuperação ao concluir o dash.")]
    public float leapLandingDuration = 0.10f;

    [Header("Variação Elite — Dash Triplo")]
    [Tooltip("Quantidade de dashes consecutivos quando a Aranha é Elite.")]
    public int eliteLeapCount = 3;
    [Tooltip("Intervalo curto entre cada dash do Elite.")]
    public float elitePauseBetweenLeaps = 0.16f;

    [Header("Recuo Característico (Retreat)")]
    [Tooltip("Chance de recuar após o dash (ao acertar o player, sempre recua!).")]
    [Range(0f, 1f)]
    public float retreatChance = 0.9f;
    [Tooltip("Velocidade do dash de recuo rente ao chão.")]
    public float retreatForce = 12f;
    [Tooltip("Duração do recuo rápido.")]
    public float retreatDuration = 0.26f;
    [Tooltip("Cooldown mínimo entre recuos.")]
    public float retreatCooldown = 1.0f;

    [Header("Áudio — Movimentação (Passos)")]
    public AudioClip[] walkingSounds;
    [Range(0f, 1f)]
    public float walkingSoundVolume = 0.5f;
    public float stepInterval = 0.16f;
    private float stepTimer = 0f;

    [Header("Áudio — Ataque (Dash / Recuo)")]
    public AudioClip[] dashSounds;
    [Range(0f, 1f)]
    public float dashSoundVolume = 0.8f;

    [Header("Áudio — Dano Recebido (Hit)")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    public AudioClip[] dieSounds;
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    // ── Estados ─────────────────────────────────────────────────────
    private bool isLeaping = false;
    private bool isRetreating = false;
    private float leapTimer = 0f;
    private float retreatTimer = 0f;
    private bool hasHitThisLeap = false;
    private bool isBuffed = false;

    private float originalMoveSpeed;
    private float originalLeapCooldown;

    private NavMeshPath navPath;
    private float navPathTimer = 0f;
    private int navCornerIndex = 0;

    private Coroutine activeAttackRoutine;

    public bool IsLeaping => isLeaping;
    public bool IsRetreating => isRetreating;
    private bool IsElite => health != null && health.isElite;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        health = GetComponent<DummyHealth>();
        navPath = new NavMeshPath();

        rb.freezeRotation = true;

        if (GetComponent<NavMeshBoundaryConstraint>() == null)
            gameObject.AddComponent<NavMeshBoundaryConstraint>();

        originalMoveSpeed = moveSpeed;
        originalLeapCooldown = leapCooldown;

        dashVFX = GetComponent<SpiderDashVFX>();
        if (dashVFX == null)
            dashVFX = gameObject.AddComponent<SpiderDashVFX>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        if (health != null)
        {
            health.onDamageTaken += (damage, attacker) =>
            {
                ActivateSpider();
                PlayHitSound();
            };

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;

            health.onDeath += CleanupOnDeath;
        }
    }

    private void OnDisable() => CleanupActiveAttack();
    private void CleanupOnDeath() => CleanupActiveAttack();

    private void CleanupActiveAttack()
    {
        if (activeAttackRoutine != null)
        {
            StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = null;
        }
        isLeaping = false;
        isRetreating = false;
        if (dashVFX != null) dashVFX.ForceClearTrail();
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    private bool IsDead() => health != null && health.CurrentHealth <= 0;

    public void ActivateSpider()
    {
        if (isActivated) return;
        isActivated = true;

        EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
        if (id != null && BestiarioManager.instancia != null)
            BestiarioManager.instancia.Registrar(id);
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

    // ─────────────────────────────────────────────────────────────────
    void Update()
    {
        if (IsDead()) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else return;
        }

        if (leapTimer > 0f) leapTimer -= Time.deltaTime;
        if (retreatTimer > 0f) retreatTimer -= Time.deltaTime;

        if (!isActivated)
        {
            float distToPlayer = GetHorizontalDistance(transform.position, playerTransform.position);
            if (distToPlayer <= activationDistance)
                ActivateSpider();
            return;
        }

        if (isLeaping || isRetreating) return;

        UpdateNavPath();
        HandleRotation();
        HandleAttackTrigger();
    }

    void FixedUpdate()
    {
        if (IsDead())
        {
            if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (!isActivated || isLeaping || isRetreating) return;

        HandleMovement();
        HandleWalkingSound();
    }

    void HandleRotation()
    {
        Vector3 dir = ComputeChaseDirection();
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateNavPath()
    {
        navPathTimer -= Time.deltaTime;
        if (navPathTimer > 0f) return;
        navPathTimer = 0.22f;

        if (navPath == null) navPath = new NavMeshPath();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 3.5f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(playerTransform.position, out NavMeshHit targetHit, 3.5f, NavMesh.AllAreas))
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

        return GetDirectionToPlayerFlat();
    }

    void HandleMovement()
    {
        float distToPlayer = GetHorizontalDistance(transform.position, playerTransform.position);

        if (distToPlayer > leapRange * 0.90f)
        {
            Vector3 direction = ComputeChaseDirection();
            Vector3 targetVelocity = direction * moveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    void HandleAttackTrigger()
    {
        float distToPlayer = GetHorizontalDistance(transform.position, playerTransform.position);
        if (distToPlayer <= leapRange && leapTimer <= 0f)
        {
            activeAttackRoutine = StartCoroutine(PerformDashAttackSequence());
        }
    }

    // ── Ataque: Dash rápido rente ao chão (1x normal, 3x Elite) → Recuo característico ─
    IEnumerator PerformDashAttackSequence()
    {
        int totalDashes = IsElite ? Mathf.Max(2, eliteLeapCount) : 1;
        leapTimer = leapCooldown;
        bool anyHit = false;

        for (int dashIdx = 0; dashIdx < totalDashes; dashIdx++)
        {
            if (IsDead() || playerTransform == null) { CleanupActiveAttack(); yield break; }

            Vector3 dashDir = GetDirectionToPlayerFlat();
            transform.rotation = Quaternion.LookRotation(dashDir);

            isLeaping = true;
            hasHitThisLeap = false;

            if (dashVFX != null)
            {
                dashVFX.StartDashEffect();
                dashVFX.SpawnAfterImage();
            }

            PlayDashSound();

            float dur = Mathf.Max(0.14f, leapDuration);
            float dashSpeed = leapForce;
            int ghostCount = (dashVFX != null) ? Mathf.Clamp(dashVFX.afterImageCount, 2, 4) : 3;
            float ghostInterval = dur / (ghostCount + 1);
            float nextGhostTime = ghostInterval;
            int obstacleMask = (1 << 0) | (1 << 3);
            bool hasNav = NavMesh.SamplePosition(transform.position, out _, 3f, NavMesh.AllAreas);

            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (IsDead()) { CleanupActiveAttack(); yield break; }

                // Para o dash antes de atravessar parede ou cair de borda
                Vector3 ahead = transform.position + dashDir * 0.95f;
                bool wallAhead = Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.45f, dashDir, out _, 0.85f, obstacleMask, QueryTriggerInteraction.Ignore);
                bool floorAhead = Physics.Raycast(ahead + Vector3.up * 2.0f, Vector3.down, 6f, obstacleMask, QueryTriggerInteraction.Ignore);
                bool navAhead = !hasNav || NavMesh.SamplePosition(ahead, out _, 1.0f, NavMesh.AllAreas);

                if (wallAhead || !floorAhead || !navAhead)
                    break;

                // Dash veloz e rente ao chão (sem pulo alto no ar!)
                float speedCurve = Mathf.Lerp(1.15f, 0.65f, elapsed / dur);
                rb.linearVelocity = new Vector3(dashDir.x * dashSpeed * speedCurve, Mathf.Min(rb.linearVelocity.y, 0f), dashDir.z * dashSpeed * speedCurve);

                // Verifica mordida no player — ao acertar, encerra o avanço para já engatar o Retreat sem travar!
                if (!hasHitThisLeap)
                {
                    Collider[] hits = Physics.OverlapSphere(transform.position, leapHitRadius);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        Collider hit = hits[i];
                        if (!hit.CompareTag("Player")) continue;

                        PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>() ?? hit.GetComponentInParent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            int dmg = IsElite ? Mathf.RoundToInt(leapDamage * 1.15f) : leapDamage;
                            playerHealth.TakeDamage(dmg, gameObject);
                            hasHitThisLeap = true;
                            anyHit = true;
                            break;
                        }
                    }

                    // No ataque normal (ou último dash do Elite), assim que morde já corta pra engatar o recuo ágil!
                    if (hasHitThisLeap && dashIdx == totalDashes - 1)
                        break;
                }

                if (dashVFX != null && elapsed >= nextGhostTime)
                {
                    dashVFX.SpawnAfterImage();
                    nextGhostTime += ghostInterval;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (dashVFX != null) dashVFX.StopDashEffect();
            rb.linearVelocity = new Vector3(0f, Mathf.Min(rb.linearVelocity.y, 0f), 0f);
            isLeaping = false;

            if (dashIdx < totalDashes - 1)
            {
                // Pequeno giro pro player antes do próximo dash do Elite
                float pElapsed = 0f;
                float pause = Mathf.Max(0.10f, elitePauseBetweenLeaps);
                while (pElapsed < pause)
                {
                    if (IsDead() || playerTransform == null) { CleanupActiveAttack(); yield break; }
                    Vector3 aim = GetDirectionToPlayerFlat();
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim), Time.deltaTime * 18f);
                    pElapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else if (!anyHit && leapLandingDuration > 0.01f)
            {
                yield return new WaitForSeconds(leapLandingDuration);
            }
        }

        // ── RECUO CARACTERÍSTICO (Retreat): marca registrada da Aranha! ─
        bool shouldRetreat = !IsDead() && retreatTimer <= 0f && (anyHit || Random.value < retreatChance);
        if (shouldRetreat)
        {
            yield return StartCoroutine(PerformRetreatRoutine());
        }

        activeAttackRoutine = null;
    }

    IEnumerator PerformRetreatRoutine()
    {
        isRetreating = true;
        retreatTimer = retreatCooldown;

        // Tenta recuar direto para trás; se houver parede/borda, desvia na diagonal mantendo distância do player
        Vector3 awayDir = -GetDirectionToPlayerFlat();
        Vector3 chosenDir = PickSafeRetreatDirection(awayDir);

        if (chosenDir.sqrMagnitude < 0.01f)
        {
            isRetreating = false;
            yield break;
        }

        if (dashVFX != null)
        {
            dashVFX.StartDashEffect();
            dashVFX.SpawnAfterImage();
        }

        PlayDashSound(pitchBoost: 1.18f);

        float dur = Mathf.Max(0.16f, retreatDuration);
        float elapsed = 0f;
        int obstacleMask = (1 << 0) | (1 << 3);
        bool hasNav = NavMesh.SamplePosition(transform.position, out _, 3f, NavMesh.AllAreas);
        float nextGhost = dur * 0.45f;

        while (elapsed < dur)
        {
            if (IsDead()) { CleanupActiveAttack(); yield break; }

            Vector3 ahead = transform.position + chosenDir * 0.9f;
            bool wallAhead = Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.45f, chosenDir, out _, 0.8f, obstacleMask, QueryTriggerInteraction.Ignore);
            bool floorAhead = Physics.Raycast(ahead + Vector3.up * 2.0f, Vector3.down, 6f, obstacleMask, QueryTriggerInteraction.Ignore);
            bool navAhead = !hasNav || NavMesh.SamplePosition(ahead, out _, 1.0f, NavMesh.AllAreas);

            if (wallAhead || !floorAhead || !navAhead)
                break;

            // Deslize rápido de recuo rente ao chão com desaceleração natural no final
            float speedCurve = Mathf.Lerp(1.2f, 0.45f, elapsed / dur);
            rb.linearVelocity = new Vector3(chosenDir.x * retreatForce * speedCurve, Mathf.Min(rb.linearVelocity.y, 0f), chosenDir.z * retreatForce * speedCurve);

            // Mantém o olhar/presas travados no player enquanto recua de costas!
            Vector3 facePlayer = GetDirectionToPlayerFlat();
            if (facePlayer.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facePlayer), Time.deltaTime * 16f);

            if (dashVFX != null && elapsed >= nextGhost)
            {
                dashVFX.SpawnAfterImage();
                nextGhost = dur + 1f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (dashVFX != null) dashVFX.StopDashEffect();
        rb.linearVelocity = new Vector3(0f, Mathf.Min(rb.linearVelocity.y, 0f), 0f);
        yield return new WaitForSeconds(0.12f);

        isRetreating = false;
    }

    private Vector3 PickSafeRetreatDirection(Vector3 straightBack)
    {
        int obstacleMask = (1 << 0) | (1 << 3);
        bool hasNav = NavMesh.SamplePosition(transform.position, out _, 3f, NavMesh.AllAreas);
        float sideSign = Random.value > 0.5f ? 1f : -1f;

        float[] testAngles = { 0f, 38f * sideSign, -38f * sideSign, 72f * sideSign, -72f * sideSign };
        foreach (float angle in testAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * straightBack;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue;
            dir.Normalize();

            bool wallBlocked = Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.45f, dir, out _, 2.4f, obstacleMask, QueryTriggerInteraction.Ignore);
            Vector3 probe = transform.position + dir * 2.2f;
            bool floorOk = Physics.Raycast(probe + Vector3.up * 2.0f, Vector3.down, 6f, obstacleMask, QueryTriggerInteraction.Ignore);
            bool navOk = !hasNav || NavMesh.SamplePosition(probe, out _, 1.0f, NavMesh.AllAreas);

            if (!wallBlocked && floorOk && navOk)
                return dir;
        }

        return Vector3.zero;
    }

    // ── Crystal Tuner Buff ──────────────────────────────────────────
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            moveSpeed = originalMoveSpeed * 1.20f;
            leapCooldown = originalLeapCooldown * 0.75f;
            if (dashVFX != null) dashVFX.SetBuffTint(true);
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            moveSpeed = originalMoveSpeed;
            leapCooldown = originalLeapCooldown;
            if (dashVFX != null) dashVFX.SetBuffTint(false);
        }
    }

    // ── Auxiliares ──────────────────────────────────────────────────
    private Vector3 GetDirectionToPlayerFlat()
    {
        if (playerTransform == null) return transform.forward;
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void HandleWalkingSound()
    {
        if (rb == null) return;
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.magnitude > 0.2f)
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
        if (walkingSounds == null || walkingSounds.Length == 0) return;
        AudioClip clip = walkingSounds[Random.Range(0, walkingSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.9f, 1.1f), walkingSoundVolume);
    }

    private void PlayDashSound(float pitchBoost = 1f)
    {
        if (dashSounds == null || dashSounds.Length == 0) return;
        AudioClip clip = dashSounds[Random.Range(0, dashSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.92f, 1.08f) * pitchBoost, dashSoundVolume);
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, leapRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, leapHitRadius);
    }
}
