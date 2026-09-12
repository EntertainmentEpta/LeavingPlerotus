using UnityEngine;
using System.Collections;

/// <summary>
/// IA da Aranha - Inimigo rápido com ataques de pulo (leap attack)
/// Usa DummyHealth para sistema de vida
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class Spider_AI : MonoBehaviour
{
    [Header("Referências")]
    private Transform playerTransform;
    private DummyHealth health;
    private Rigidbody rb;
    private Collider spiderCollider;
    private SpiderDashVFX dashVFX;
    private AudioSource audioSource;

    [Header("Ativação")]
    public float activationDistance = 35f;
    private bool isActivated = false;

    [Header("Movimento")]
    [Tooltip("Velocidade de movimento em direção ao player")]
    public float moveSpeed = 6f;
    [Tooltip("Velocidade de rotação para olhar o player")]
    public float rotationSpeed = 10f;

    [Header("Ataque (Leap)")]
    [Tooltip("Força do pulo em direção ao player")]
    public float leapForce = 12f;
    [Tooltip("Força vertical do pulo")]
    public float leapUpForce = 5f;
    [Tooltip("Distância para iniciar o leap attack")]
    public float leapRange = 8f;
    [Tooltip("Cooldown entre leaps")]
    public float leapCooldown = 2f;
    [Tooltip("Dano causado pelo leap")]
    public int leapDamage = 25;
    [Tooltip("Raio da hitbox durante o leap")]
    public float leapHitRadius = 1.5f;

    [Header("Duração do Leap")]
    [Tooltip("Duração do pulo do leap attack em segundos.")]
    public float leapDuration = 0.4f;
    [Tooltip("Tempo de espera ao aterrissar antes de voltar ao estado normal.")]
    public float leapLandingDuration = 0.15f;

    [Header("Recuo (Retreat)")]
    [Tooltip("Chance de recuar após um ataque (0-1)")]
    [Range(0f, 1f)]
    public float retreatChance = 0.3f;
    [Tooltip("Força do pulo de recuo")]
    public float retreatForce = 8f;
    [Tooltip("Cooldown do recuo")]
    public float retreatCooldown = 5f;

    [Header("Áudio")]
    [Tooltip("Vetor de áudios que serão selecionados aleatoriamente")]
    public AudioClip[] walkingSounds;
    [Tooltip("Volume dos sons de passos")]
    [Range(0f, 1f)]
    public float walkingSoundVolume = 0.5f;
    [Tooltip("Intervalo entre cada passo enquanto caminha")]
    public float stepInterval = 0.18f;
    private float stepTimer = 0f;

    [Tooltip("Vetor de áudios de dash/ataque que serão selecionados aleatoriamente")]
    public AudioClip[] dashSounds;
    [Tooltip("Volume dos sons de dash/ataque")]
    [Range(0f, 1f)]
    public float dashSoundVolume = 0.8f;

    [Header("Estados")]
    private bool isLeaping = false;
    private bool isRetreating = false;
    private float leapTimer = 0f;
    private float retreatTimer = 0f;
    private bool hasHitThisLeap = false;
    private bool isBuffed = false;

    public bool IsLeaping => isLeaping;
    public bool IsRetreating => isRetreating;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        health = GetComponent<DummyHealth>();
        spiderCollider = GetComponent<Collider>();

        // Configura Rigidbody
        rb.freezeRotation = true;

        // Configura VFX de Dash (adiciona se não existir)
        dashVFX = GetComponent<SpiderDashVFX>();
        if (dashVFX == null)
        {
            dashVFX = gameObject.AddComponent<SpiderDashVFX>();
        }

        // Encontra o player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("Spider_AI: Player não encontrado! Verifique a tag 'Player'.");
        }
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else return;
        }
        if (health != null && health.CurrentHealth <= 0) return;

        // Timer de cooldowns
        if (leapTimer > 0) leapTimer -= Time.deltaTime;
        if (retreatTimer > 0) retreatTimer -= Time.deltaTime;

        // Ativação por proximidade
        if (!isActivated)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer < activationDistance)
            {
                isActivated = true;
                Debug.Log("[SPIDER] Ativada! Player detectado a " + distToPlayer.ToString("F1") + "m");

                EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                Debug.Log("[SPIDER] EnemyIdentity: " + (id != null ? id.nomeInimigo : "NULL") + " | BestiarioManager: " + (BestiarioManager.instancia != null));
                if (id != null && BestiarioManager.instancia != null)
                    BestiarioManager.instancia.Registrar(id);
            }
            return;
        }

        // Não faz nada enquanto está no ar (leap ou retreat)
        if (isLeaping || isRetreating) return;

        // Lógica principal
        HandleRotation();
        HandleMovementAndAttack();
    }

    void FixedUpdate()
    {
        if (!isActivated || isLeaping || isRetreating) return;
        if (health != null && health.CurrentHealth <= 0) return;

        HandleMovement();
        HandleWalkingSound();
    }

    void HandleRotation()
    {
        // Olha para o player
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0;

        if (directionToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void HandleMovement()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Se está longe demais do range de leap, se aproxima
        if (distToPlayer > leapRange)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0;

            Vector3 targetVelocity = direction * moveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            // Para perto do player, espera o cooldown do leap
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    void HandleMovementAndAttack()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Se está no range de leap e cooldown pronto
        if (distToPlayer <= leapRange && leapTimer <= 0)
        {
            StartCoroutine(PerformLeapAttack());
        }
    }

    IEnumerator PerformLeapAttack()
    {
        isLeaping = true;
        hasHitThisLeap = false;
        leapTimer = leapCooldown;

        // Calcula direção do pulo
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;

        // Ativa VFX de dash
        if (dashVFX != null)
        {
            dashVFX.StartDashEffect();
            dashVFX.SpawnAfterImage();
        }

        // Toca som de dash aleatório do vetor dashSounds
        PlayDashSound();

        // Aplica força do pulo
        rb.linearVelocity = Vector3.zero;
        Vector3 leapVelocity = directionToPlayer * leapForce + Vector3.up * leapUpForce;
        rb.AddForce(leapVelocity, ForceMode.VelocityChange);

        Debug.Log("[SPIDER] LEAP ATTACK! Pulando em direção ao player.");

        // Espera estar no ar
        yield return new WaitForSeconds(0.1f);

        // Durante o pulo, verifica colisão com player
        float elapsed = 0;

        while (elapsed < leapDuration)
        {
            elapsed += Time.deltaTime;

            // Verifica hit no player
            if (!hasHitThisLeap)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, leapHitRadius);
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Player"))
                    {
                        PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            playerHealth.TakeDamage(leapDamage, gameObject);
                            hasHitThisLeap = true;
                            Debug.Log("Spider acertou o player! Dano: " + leapDamage);
                        }
                    }
                }
            }

            // Spawn after-image durante o pulo
            if (dashVFX != null && ((int)(elapsed * 10) % 2 == 0))
            {
                dashVFX.SpawnAfterImage();
            }

            yield return null;
        }

        // Para VFX e espera tocar o chão
        if (dashVFX != null) dashVFX.StopDashEffect();
        yield return new WaitForSeconds(leapLandingDuration);

        isLeaping = false;

        // Chance de recuar após o ataque
        if (hasHitThisLeap && retreatTimer <= 0 && Random.value < retreatChance)
        {
            StartCoroutine(PerformRetreat());
        }
    }

    IEnumerator PerformRetreat()
    {
        isRetreating = true;
        retreatTimer = retreatCooldown;

        // Ativa VFX de retreat
        if (dashVFX != null)
        {
            dashVFX.StartDashEffect();
            dashVFX.SpawnAfterImage();
        }

        // Pula para trás
        Vector3 retreatDirection = (transform.position - playerTransform.position).normalized;
        retreatDirection.y = 0;

        rb.linearVelocity = Vector3.zero;
        Vector3 retreatVelocity = retreatDirection * retreatForce + Vector3.up * (leapUpForce * 0.7f);
        rb.AddForce(retreatVelocity, ForceMode.VelocityChange);

        Debug.Log("[SPIDER] RETREAT! Recuando do player após acertar o ataque.");

        yield return new WaitForSeconds(0.4f);

        // Para VFX
        if (dashVFX != null) dashVFX.StopDashEffect();

        yield return new WaitForSeconds(0.2f);

        isRetreating = false;
    }

    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            moveSpeed *= 1.3f;
            leapCooldown *= 0.7f;
            Debug.Log("[SPIDER] BUFFED! Velocidade +30%, Cooldown do Leap -30%");
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            moveSpeed /= 1.3f;
            leapCooldown /= 0.7f;
            Debug.Log("[SPIDER] UNBUFFED! Velocidade e Cooldown restaurados");
        }
    }

    private void HandleWalkingSound()
    {
        if (rb == null) return;

        // Se a aranha está se movendo horizontalmente com velocidade significativa
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.magnitude > 0.1f)
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
            // Se parar, reseta para tocar o som rapidamente ao dar o próximo passo
            stepTimer = 0f;
        }
    }

    private void PlayWalkingSound()
    {
        if (walkingSounds == null || walkingSounds.Length == 0)
            return;

        int randIndex = Random.Range(0, walkingSounds.Length);
        AudioClip clipToPlay = walkingSounds[randIndex];
        
        float pitch = Random.Range(0.9f, 1.1f);
        
        // Se contiver "3" no nome ou for o índice correspondente, pode dar uma variação (seguindo a mesma lógica do Mimic)
        if (clipToPlay != null)
        {
            if (randIndex == 2 || clipToPlay.name.Contains("3"))
            {
                pitch = Random.Range(1.4f, 1.6f);
            }
            PlayClipAtPointWithPitch(clipToPlay, transform.position, pitch, walkingSoundVolume);
        }
    }

    private void PlayDashSound()
    {
        if (dashSounds == null || dashSounds.Length == 0)
            return;

        int randIndex = Random.Range(0, dashSounds.Length);
        AudioClip clipToPlay = dashSounds[randIndex];

        if (clipToPlay != null)
        {
            float pitch = Random.Range(0.9f, 1.1f);
            PlayClipAtPointWithPitch(clipToPlay, transform.position, pitch, dashSoundVolume);
        }
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
        {
            if (audioSource != null)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, volume);
            }
        }
    // Visualização do range no Editor
    void OnDrawGizmosSelected()
    {
        // Range de ativação
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        // Range do leap
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, leapRange);

        // Hitbox do leap
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, leapHitRadius);
    }
}
