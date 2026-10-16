using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class SharpBlur : MonoBehaviour
{
    private Transform playerTransform;
    private Rigidbody playerRb; 
    private DummyHealth health;
    private Rigidbody rb;
    private Animator anim;

    [Header("Ativação & Perseguição")]
    public float activationDistance = 25f;
    public float chaseSpeed = 6f;
    
    [Header("Gatilhos de Ataque (Distâncias)")]
    [Tooltip("Distância média para iniciar a sequência de Dash")]
    public float dashTriggerDistance = 8f; 
    [Tooltip("Distância curta para dar um soco/ataque normal")]
    public float meleeTriggerDistance = 3.0f;
    [Tooltip("Tempo de recarga entre sequências de teleporte/dash para permitir aproximação e ataque corpo a corpo")]
    public float dashCooldown = 3.5f;

    [Header("Configurações do Dash")]
    public float dashDistance = 9f;
    public float dashSpeed = 30f;
    public float pauseBetweenDashes = 0.25f;
    public float endSequenceRest = 1.0f;

    [Header("Configurações do Ataque Corpo a Corpo")]
    public float meleeDuration = 1f; // Tempo que dura a animação do soco
    public int meleeDamage = 20;

    [Header("Refinamentos Inteligencia")]
    public float anticipationTime = 0.2f; 
    [Range(0f, 1f)] public float leadPrediction = 0.5f;
    public float retreatDistance = 3.5f;

    [Header("Dano")]
    public int dashDamage = 15;

    [Header("Efeitos Visuais")]
    [Tooltip("Coloque o Prefab vazio com o script DashHologram aqui")]
    public GameObject hologramPrefab; 
    [Tooltip("Coloque o Material transparente/fantasma aqui")]
    public Material hologramMaterial;
    [Range(0.1f, 1f)] public float hologramAlpha = 0.6f;

    [Header("Áudio — Movimentação (Teleporte / Dash)")]
    [Tooltip("Som da distorção de teleporte / dash do SharpBlur")]
    public AudioClip teleportSound;
    [Tooltip("Volume do som de teleporte")]
    [Range(0f, 1f)]
    public float teleportSoundVolume = 0.8f;

    [Header("Áudio — Ataque (Golpe)")]
    [Tooltip("Som do ataque (swoosh / corte de cristal)")]
    public AudioClip meleeAttackSound;
    [Tooltip("Volume do som de ataque")]
    [Range(0f, 1f)]
    public float meleeAttackSoundVolume = 0.9f;

    [Header("Áudio — Dano Recebido (Hit)")]
    [Tooltip("Sons reproduzidos quando o SharpBlur leva dano (golpe do player). NÃO colocar sons de ataque aqui.")]
    public AudioClip[] hitSounds;
    [Tooltip("Volume dos sons de dano recebido (hit)")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    [Tooltip("Sons reproduzidos quando o SharpBlur morre.")]
    public AudioClip[] dieSounds;
    [Tooltip("Volume dos sons de morte")]
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    // Adicionado o estado MeleeAttacking
    private enum State { Idle, Chasing, Dashing, MeleeAttacking, Resting }
    private State currentState = State.Idle;
    private float nextDashAllowedTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<DummyHealth>();
        anim = GetComponentInChildren<Animator>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerRb = player.GetComponent<Rigidbody>();
        }

        // Snap de segurança no chão para evitar clipagem no piso da sala
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Vector3 pos = transform.position;
            pos.y = navHit.position.y;
            transform.position = pos;
        }

        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.constraints |= RigidbodyConstraints.FreezePositionY;

        // Registra callbacks de áudio de hit e morte
        if (health != null)
        {
            if (hitSounds != null && hitSounds.Length > 0)
                health.onDamageTaken += (damage, attacker) => PlayHitSound();

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;
        }
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
        if (playerTransform == null || (health != null && health.CurrentHealth <= 0)) return;

        switch (currentState)
        {
            case State.Idle: HandleIdle(); break;
            case State.Chasing: HandleChasing(); break;
        }
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void HandleIdle()
    {
        if (Vector3.Distance(transform.position, playerTransform.position) <= activationDistance)
        {
            currentState = State.Chasing;
            if (anim != null) anim.SetBool("isChasing", true);
        }
    }

    void HandleChasing()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        LookAtPosition(playerTransform.position, 10f);

        bool canDash = Time.time >= nextDashAllowedTime;

        // Se chegou perto do jogador, prioriza o ataque corpo a corpo (com som de ataque!)
        if (distToPlayer <= Mathf.Max(2.8f, meleeTriggerDistance))
        {
            if (!canDash || Random.Range(0, 100) < 75)
            {
                StartCoroutine(MeleeAttackRoutine());
            }
            else
            {
                StartCoroutine(DashRoutine());
            }
        }
        else if (distToPlayer <= dashTriggerDistance && canDash)
        {
            // Na distância média e com o Dash fora de cooldown, usa os dashes
            StartCoroutine(DashRoutine());
        }
        else
        {
            // Se estiver longe (ou com Dash em recarga), corre atrás do player até alcançar o Melee!
            Vector3 targetPos = Vector3.MoveTowards(transform.position, playerTransform.position, chaseSpeed * Time.deltaTime);
            rb.MovePosition(targetPos);
        }
    }

    // ROTINA: Ataque de perto (Combo de Garras)
    IEnumerator MeleeAttackRoutine()
    {
        currentState = State.MeleeAttacking;

        LookAtPosition(playerTransform.position, 30f);

        if (anim != null)
        {
            anim.SetBool("isChasing", false);
            anim.Play("Melee", 0, 0f);
        }

        // Toca o som do ataque (swoosh / corte de cristal) imediatamente no início do golpe
        PlayMeleeAttackSound();

        // Aguarda a primeira metade da animação (momento do impacto)
        yield return new WaitForSeconds(meleeDuration * 0.45f);

        // Verifica se o jogador ainda está perto no momento do impacto para aplicar o dano
        if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= Mathf.Max(2.8f, meleeTriggerDistance) + 1.2f)
        {
            PlayerHealth ph = playerTransform.GetComponent<PlayerHealth>() ?? playerTransform.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(meleeDamage, gameObject);
            }
            Debug.Log($"[SharpBlur] acertou o jogador de perto e causou {meleeDamage} de dano.");
        }

        // Termina a segunda metade da animação
        yield return new WaitForSeconds(meleeDuration * 0.55f);

        currentState = State.Resting;
        yield return new WaitForSeconds(endSequenceRest * 0.6f);
        
        currentState = State.Chasing;
        if (anim != null) anim.SetBool("isChasing", true);
    }

    // Sequência de Teleporte / Dash seguida de Golpe
    IEnumerator DashRoutine()
    {
        currentState = State.Dashing;

        if (anim != null) anim.SetBool("isChasing", false);

        // Escolhe um número aleatório entre 1, 2 e 3 para a quantidade de dashes
        int numberOfDashes = Random.Range(1, 4);

        for (int i = 0; i < numberOfDashes; i++)
        {
            Vector3 targetPredictPos = GetPredictedPlayerPosition();
            LookAtPosition(targetPredictPos, 20f);
            
            Vector3 dashDirection = (targetPredictPos - transform.position).normalized;
            dashDirection.y = 0;
            if (dashDirection == Vector3.zero) dashDirection = transform.forward;

            Vector3 targetBasePos = targetPredictPos;
            targetBasePos.y = transform.position.y;

            // No último dash da sequência, para próximo ao jogador para encaixar o golpe!
            bool isLastDash = (i == numberOfDashes - 1);
            Vector3 targetDashPos = isLastDash
                ? targetBasePos - (dashDirection * 1.2f)
                : targetBasePos + (dashDirection * (dashDistance / 2f));
            Quaternion dashRot = Quaternion.LookRotation(dashDirection);

            if (hologramPrefab != null && hologramMaterial != null)
            {
                GameObject holo = Instantiate(hologramPrefab);
                DashHologram holoScript = holo.GetComponent<DashHologram>();
                if (holoScript != null)
                {
                    holoScript.Init(transform, targetDashPos, dashRot, anticipationTime, hologramMaterial, hologramAlpha);
                }
            }

            if (anim != null) anim.Play("Dash", 0, 0f);
            
            yield return new WaitForSeconds(anticipationTime);
            yield return StartCoroutine(ExecuteSingleDash(dashDirection, targetDashPos));
            yield return new WaitForSeconds(pauseBetweenDashes);
        }

        // Ao finalizar o teleporte perto do jogador, desfere o golpe com som de ataque antes de recuar!
        if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= Mathf.Max(3.5f, meleeTriggerDistance + 1f))
        {
            LookAtPosition(playerTransform.position, 40f);
            if (anim != null) anim.Play("Melee", 0, 0f);
            PlayMeleeAttackSound();

            yield return new WaitForSeconds(meleeDuration * 0.45f);

            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= Mathf.Max(3.5f, meleeTriggerDistance + 1.2f))
            {
                PlayerHealth ph = playerTransform.GetComponent<PlayerHealth>() ?? playerTransform.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(meleeDamage, gameObject);
                }
            }

            yield return new WaitForSeconds(meleeDuration * 0.35f);
        }

        yield return StartCoroutine(RetreatRoutine());

        nextDashAllowedTime = Time.time + dashCooldown;

        currentState = State.Resting;
        yield return new WaitForSeconds(endSequenceRest);
        
        currentState = State.Chasing;
        if (anim != null) anim.SetBool("isChasing", true);
    }

    IEnumerator ExecuteSingleDash(Vector3 direction, Vector3 targetDashPos)
    {
        if (direction == Vector3.zero) direction = transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        // Toca o som de distorção de teleporte / dash
        PlayTeleportSound();

        Vector3 startPos = transform.position;
        targetDashPos.y = startPos.y; 
        
        float startTime = Time.time;
        float distance = Vector3.Distance(startPos, targetDashPos);
        float duration = Mathf.Max(0.05f, distance / dashSpeed);

        while (Time.time - startTime < duration)
        {
            float t = (Time.time - startTime) / duration;
            rb.MovePosition(Vector3.Lerp(startPos, targetDashPos, t));
            yield return null;
        }

        rb.MovePosition(targetDashPos);
    }

    IEnumerator RetreatRoutine()
    {
        Vector3 retreatDir = -transform.forward; 
        Vector3 startPos = transform.position;
        Vector3 targetRetreatPos = startPos + (retreatDir * retreatDistance);

        float startTime = Time.time;
        float duration = 0.25f; 

        while (Time.time - startTime < duration)
        {
            float t = (Time.time - startTime) / duration;
            rb.MovePosition(Vector3.Lerp(startPos, targetRetreatPos, Mathf.Sin(t * Mathf.PI / 2f)));
            yield return null;
        }
        rb.MovePosition(targetRetreatPos);
    }

    Vector3 GetPredictedPlayerPosition()
    {
        Vector3 playerPos = playerTransform.position;
        if (playerRb != null && leadPrediction > 0)
        {
            float distance = Vector3.Distance(transform.position, playerPos);
            float timeToTarget = distance / dashSpeed;
            playerPos += playerRb.linearVelocity * timeToTarget * leadPrediction; 
        }
        return playerPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == State.Dashing)
        {
            PlayerHealth ph = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                PlayMeleeAttackSound();
                ph.TakeDamage(dashDamage, gameObject);
                Debug.Log($"[SharpBlur] causou {dashDamage} de dano com o Dash.");
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState == State.Dashing)
        {
            PlayerHealth ph = collision.gameObject.GetComponent<PlayerHealth>() ?? collision.gameObject.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                PlayMeleeAttackSound();
                ph.TakeDamage(dashDamage, gameObject);
                Debug.Log($"[SharpBlur] causou {dashDamage} de dano com o Dash.");
            }
        }
    }

    void LookAtPosition(Vector3 target, float rotationSpeed)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.1f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotationSpeed * Time.deltaTime);
        }
    }

    // =============================================
    // SISTEMA DE ÁUDIO (Teleporte/Dash & Ataques)
    // =============================================

    private void PlayTeleportSound()
    {
        if (teleportSound == null) return;
        float pitch = Random.Range(0.9f, 1.1f);
        PlayClipAtPointWithPitch(teleportSound, transform.position, pitch, teleportSoundVolume);
    }

    private void PlayMeleeAttackSound()
    {
        if (meleeAttackSound == null) return;
        float pitch = Random.Range(0.95f, 1.05f);
        PlayClipAtPointWithPitch(meleeAttackSound, transform.position, pitch, meleeAttackSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        GameObject audioObj = new GameObject("TempSharpBlurAudio");
        audioObj.transform.position = position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.pitch = pitch;
        aSource.volume = volume;
        aSource.spatialBlend = 0.2f; // Mais presente na câmera top-down/isométrica
        aSource.minDistance = 12f;
        aSource.maxDistance = 60f;
        aSource.rolloffMode = AudioRolloffMode.Linear;
        aSource.Play();
        float safePitch = Mathf.Abs(pitch) > 0.01f ? Mathf.Abs(pitch) : 1f;
        Destroy(audioObj, (clip.length / safePitch) + 0.1f);
    }
}