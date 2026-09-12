using UnityEngine;
using System.Collections;

/// <summary>
/// IA do Golem - Inimigo lento com ataques fortes e habilidade de stun
/// Usa DummyHealth para sistema de vida
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class Golem_AI : MonoBehaviour
{
    [Header("Referências")]
    private Transform playerTransform;
    private DummyHealth health;
    private Rigidbody rb;

    private AudioSource audioSource;

    [Header("VFX Prefabs (Placeholders ok)")]
    [Tooltip("Prefab do círculo de aviso no chão antes do stun. Se null, usa um placeholder.")]
    public GameObject stunMarkerPrefab;
    [Tooltip("Prefab do raio/efeito de stun. Se null, usa lógica interna.")]
    public GameObject stunBeamPrefab;

    [Header("Ativação")]
    public float activationDistance = 25f;
    private bool isActivated = false;

    [Header("Movimento")]
    [Tooltip("Velocidade de movimento (lento)")]
    public float moveSpeed = 2f;
    [Tooltip("Velocidade de rotação")]
    public float rotationSpeed = 5f;

    [Header("Ataque Melee")]
    [Tooltip("Distância para atacar corpo a corpo")]
    public float meleeRange = 3f;
    [Tooltip("Cooldown entre ataques melee")]
    public float meleeCooldown = 3f;
    [Tooltip("Dano do ataque melee")]
    public int meleeDamage = 30;
    [Tooltip("Raio da hitbox do melee")]
    public float meleeHitRadius = 2f;

    [Header("Habilidade de Stun")]
    [Tooltip("Distância MÁXIMA para usar stun (não funciona se player estiver mais longe)")]
    public float stunMaxRange = 10f;
    [Tooltip("Cooldown do stun")]
    public float stunCooldown = 12f;
    [Tooltip("Duração do stun no jogador")]
    public float stunDuration = 1.5f;
    [Tooltip("Tempo de telegrafagem antes do stun (quase instantâneo)")]
    public float stunTelegraphTime = 0.3f;
    [Tooltip("Raio da área de stun")]
    public float stunRadius = 4f;
    [Tooltip("Quantidade de melee hits para ativar stun no combo")]
    public int comboHitsForStun = 2;

    [Header("Estados")]
    private bool isAttacking = false;
    private bool isCastingStun = false;
    private float meleeTimer = 0f;
    private float stunTimer = 0f;
    private int meleeCombo = 0; // Para usar stun em combos
    private bool isBuffed = false;

    [Header("Áudio")]
    [Tooltip("Vetor de áudios de passos do Golem selecionados aleatoriamente")]
    public AudioClip[] walkingSounds;
    [Tooltip("Volume dos sons de passos do Golem")]
    [Range(0f, 1f)]
    public float walkingSoundVolume = 0.6f;
    [Tooltip("Intervalo entre cada passo enquanto caminha")]
    public float stepInterval = 0.45f;
    private float stepTimer = 0f;

    [Tooltip("Som do impacto da pisada/estouro de Stun (Stomp)")]
    public AudioClip stompSound;
    [Tooltip("Volume do som de Stomp")]
    [Range(0f, 1f)]
    public float stompSoundVolume = 0.9f;

    public bool IsAttacking => isAttacking;
    public bool IsCastingStun => isCastingStun;
    public bool IsActivated => isActivated;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        health = GetComponent<DummyHealth>();

        // Configura Rigidbody
        rb.freezeRotation = true;

        // Snap de segurança no chão para evitar clipagem no mapa
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
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
        }
        else
        {
            Debug.LogError("Golem_AI: Player não encontrado! Verifique a tag 'Player'.");
        }
    }

    void Update()
    {
        if (playerTransform == null) return;
        if (health != null && health.CurrentHealth <= 0) return;

        // Timers
        if (meleeTimer > 0) meleeTimer -= Time.deltaTime;
        if (stunTimer > 0) stunTimer -= Time.deltaTime;

        // Ativação por proximidade
        if (!isActivated)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist < activationDistance)
            {
                isActivated = true;
                Debug.Log("[GOLEM] Ativado! Player detectado a " + dist.ToString("F1") + "m");

                EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                Debug.Log("[GOLEM] EnemyIdentity: " + (id != null ? id.nomeInimigo : "NULL") + " | BestiarioManager: " + (BestiarioManager.instancia != null));
                if (id != null && BestiarioManager.instancia != null)
                    BestiarioManager.instancia.Registrar(id);
            }
            return;
        }

        // Não faz nada enquanto ataca ou usa stun
        if (isAttacking || isCastingStun) return;

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

    private void HandleWalkingSound()
    {
        if (rb == null) return;

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
            stepTimer = 0f;
        }
    }

    private void PlayWalkingSound()
    {
        if (walkingSounds == null || walkingSounds.Length == 0)
            return;

        int randIndex = Random.Range(0, walkingSounds.Length);
        AudioClip clipToPlay = walkingSounds[randIndex];

        if (clipToPlay != null)
        {
            float pitch = Random.Range(0.85f, 1.15f);
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
            if (audioSource != null)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, volume);
            }
        }

    void HandleRotation()
    {
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

        // Move em direção ao player se não está no range de melee
        if (distToPlayer > meleeRange)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0;

            Vector3 targetVelocity = direction * moveSpeed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            // Para perto do player
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    void HandleCombat()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Prioridade 1: Após X melee hits seguidos, usa stun INSTANTÂNEO (combo breaker)
        if (meleeCombo >= comboHitsForStun && stunTimer <= 0 && distToPlayer <= stunMaxRange)
        {
            Debug.Log("[GOLEM] COMBO STUN! Ativando stun após " + meleeCombo + " hits!");
            StartCoroutine(CastStun());
            meleeCombo = 0;
            return;
        }

        // Prioridade 2: Se perto, ataca melee
        if (distToPlayer <= meleeRange && meleeTimer <= 0)
        {
            StartCoroutine(PerformMeleeAttack());
            return;
        }

        // Prioridade 3: Se player está no range do stun e cooldown pronto, usa stun
        if (distToPlayer <= stunMaxRange && stunTimer <= 0 && meleeTimer > 0)
        {
            StartCoroutine(CastStun());
            return;
        }
    }

    IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;
        meleeTimer = meleeCooldown;

        // Animação de vento (wind-up)
        Debug.Log("[GOLEM] MELEE ATTACK! Preparando golpe...");
        yield return new WaitForSeconds(0.5f);

        // Verifica hit
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.5f, meleeHitRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(meleeDamage, gameObject);
                    Debug.Log("[GOLEM] HIT! Melee acertou o player! Dano: " + meleeDamage + " | Combo: " + (meleeCombo + 1));
                    meleeCombo++;
                }
            }
        }

        // Recovery
        yield return new WaitForSeconds(0.5f);

        isAttacking = false;
    }

    IEnumerator CastStun()
    {
        isCastingStun = true;
        stunTimer = stunCooldown;
        meleeCombo = 0;

        // Para de se mover
        rb.linearVelocity = Vector3.zero;

        // Posição alvo = posição atual do player
        Vector3 targetPosition = new Vector3(playerTransform.position.x, 0.05f, playerTransform.position.z);


        // Spawn do marcador de aviso (telegrafagem)
        GameObject marker = null;
        if (stunMarkerPrefab != null)
        {
            marker = Instantiate(stunMarkerPrefab, targetPosition, Quaternion.Euler(90, 0, 0));
            // Escala para mostrar o raio
            marker.transform.localScale = new Vector3(stunRadius * 2, stunRadius * 2, 1);
        }
        else
        {
            // Placeholder: Debug visual
            Debug.Log("Golem marcando área de stun em: " + targetPosition);
            Debug.DrawLine(targetPosition, targetPosition + Vector3.up * 5f, Color.cyan, stunTelegraphTime);
        }

        Debug.Log("[GOLEM] STUN CAST! Carregando ataque de área em " + targetPosition + " (raio: " + stunRadius + "m)");

        // Espera o tempo de telegrafagem
        yield return new WaitForSeconds(stunTelegraphTime);

        // Destrói o marcador
        if (marker != null) Destroy(marker);

        // Toca o som do impacto do Stomp
        PlayStompSound(targetPosition);

        // Dispara o stun
        if (stunBeamPrefab != null)
        {
            GameObject beam = Instantiate(stunBeamPrefab, targetPosition, Quaternion.identity);
            StunBeam stunScript = beam.GetComponent<StunBeam>();
            if (stunScript != null)
            {
                stunScript.Initialize(stunRadius, stunDuration);
            }
            Destroy(beam, 1f);
        }
        else
        {
            // Fallback: verifica diretamente
            Collider[] hits = Physics.OverlapSphere(targetPosition, stunRadius);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.ApplyStun(stunDuration);
                        Debug.Log("[GOLEM] STUN HIT! Player atordoado por " + stunDuration + " segundos!");
                    }
                }
            }
        }

        // Recovery do cast
        yield return new WaitForSeconds(0.5f);

        isCastingStun = false;
    }

    /// <summary>
    /// Pode ser buffado pelo CrystalTuner
    /// </summary>
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            moveSpeed *= 1.3f;
            stunCooldown *= 0.7f;
            Debug.Log("[GOLEM] BUFFED! Velocidade +30%, Cooldown do Stun -30%");
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            moveSpeed /= 1.3f;
            stunCooldown /= 0.7f;
            Debug.Log("[GOLEM] UNBUFFED! Velocidade e Cooldown restaurados");
        }
    }

    // Visualização no Editor
    void OnDrawGizmosSelected()
    {
        // Ativação
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        // Range melee
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        // Hitbox melee
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawSphere(transform.position + transform.forward * 1.5f, meleeHitRadius);

        // Range máximo do stun
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stunMaxRange);

        // Raio do stun
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawSphere(transform.position + transform.forward * 5f, stunRadius);
    }
}
