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
    public float meleeTriggerDistance = 2.5f;

    [Header("Configurações do Dash")]
    public float dashDistance = 9f;
    public float dashSpeed = 30f;
    public float pauseBetweenDashes = 0.25f;
    public float endSequenceRest = 1.2f;

    [Header("Configurações do Ataque Corpo a Corpo")]
    public float meleeDuration = 1f; // Tempo que dura a animação do soco
    public int meleeDamage = 20;

    [Header("Refinamentos Inteligencia")]
    public float anticipationTime = 0.2f; 
    [Range(0f, 1f)] public float leadPrediction = 0.5f;
    public float retreatDistance = 4f;

    [Header("Dano")]
    public int dashDamage = 15;

    [Header("Efeitos Visuais")]
    [Tooltip("Coloque o Prefab vazio com o script DashHologram aqui")]
    public GameObject hologramPrefab; 
    [Tooltip("Coloque o Material transparente/fantasma aqui")]
    public Material hologramMaterial;
    [Range(0.1f, 1f)] public float hologramAlpha = 0.6f;

    [Header("Áudio")]
    [Tooltip("Som da distorção de teleporte / dash do SharpBlur")]
    public AudioClip teleportSound;
    [Tooltip("Volume do som de teleporte")]
    [Range(0f, 1f)]
    public float teleportSoundVolume = 0.8f;

    [Tooltip("Som do ataque (swoosh / swish de cristal)")]
    public AudioClip meleeAttackSound;
    [Tooltip("Volume do som de ataque")]
    [Range(0f, 1f)]
    public float meleeAttackSoundVolume = 0.8f;

    // Adicionado o estado MeleeAttacking
    private enum State { Idle, Chasing, Dashing, MeleeAttacking, Resting }
    private State currentState = State.Idle;

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

        // NOVA LÓGICA DE DECISÃO COM ALEATORIEDADE DE PERTO
        if (distToPlayer <= meleeTriggerDistance)
        {
            // Sorteia um número de 0 a 100. 
            // Se cair abaixo de 50 (50% de chance), ele dá o ataque Melee.
            // Se cair 50 ou mais, ele surpreende e dá o Dash!
            if (Random.Range(0, 100) < 50)
            {
                StartCoroutine(MeleeAttackRoutine());
            }
            else
            {
                StartCoroutine(DashRoutine());
            }
        }
        else if (distToPlayer <= dashTriggerDistance)
        {
            // Se estiver na distância média, ele só usa os dashes
            StartCoroutine(DashRoutine());
        }
        else
        {
            // Se estiver longe, continua correndo atrás do player
            Vector3 targetPos = Vector3.MoveTowards(transform.position, playerTransform.position, chaseSpeed * Time.deltaTime);
            rb.MovePosition(targetPos);
        }
    }

    // NOVA ROTINA: Ataque de perto
    IEnumerator MeleeAttackRoutine()
    {
        currentState = State.MeleeAttacking;

        if (anim != null) anim.SetBool("isChasing", false);
        
        // Substitua "Melee" pelo nome exato da sua animação de soco/mordida no Animator
        if (anim != null) anim.Play("Melee", 0, 0f); 

        // Toca o som do ataque (swoosh / swish de cristal)
        PlayMeleeAttackSound();

        // Aguarda o tempo do ataque acontecer
        yield return new WaitForSeconds(meleeDuration / 2f);

        // Verifica se o jogador ainda está perto no momento do impacto para dar o dano
        if (Vector3.Distance(transform.position, playerTransform.position) <= meleeTriggerDistance + 1f)
        {
            PlayerHealth ph = playerTransform.GetComponent<PlayerHealth>() ?? playerTransform.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(meleeDamage, gameObject);
            }
            Debug.Log($"[SharpBlur] acertou o jogador de perto e causou {meleeDamage} de dano.");
        }

        // Termina a segunda metade da animação
        yield return new WaitForSeconds(meleeDuration / 2f);

        currentState = State.Resting;
        yield return new WaitForSeconds(endSequenceRest);
        
        currentState = State.Chasing;
        if (anim != null) anim.SetBool("isChasing", true);
    }

    // ANTIGO TripleDashRoutine AGORA É DashRoutine (Quantidade Aleatória)
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
            Vector3 targetDashPos = targetBasePos + (dashDirection * (dashDistance / 2f));
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
            yield return StartCoroutine(ExecuteSingleDash(dashDirection, targetPredictPos));
            yield return new WaitForSeconds(pauseBetweenDashes);
        }

        yield return StartCoroutine(RetreatRoutine());

        currentState = State.Resting;
        yield return new WaitForSeconds(endSequenceRest);
        
        currentState = State.Chasing;
        if (anim != null) anim.SetBool("isChasing", true);
    }

    IEnumerator ExecuteSingleDash(Vector3 direction, Vector3 targetBasePos)
    {
        if (direction == Vector3.zero) direction = transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        // Toca o som de distorção de teleporte / dash
        PlayTeleportSound();

        Vector3 startPos = transform.position;
        targetBasePos.y = startPos.y; 

        Vector3 targetDashPos = targetBasePos + (direction * (dashDistance / 2f));
        
        float startTime = Time.time;
        float distance = Vector3.Distance(startPos, targetDashPos);
        float duration = distance / dashSpeed;

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
        aSource.spatialBlend = 1f; // Som 3D
        aSource.minDistance = 3f;
        aSource.maxDistance = 25f;
        aSource.rolloffMode = AudioRolloffMode.Linear;
        aSource.Play();
        float safePitch = Mathf.Abs(pitch) > 0.01f ? Mathf.Abs(pitch) : 1f;
        Destroy(audioObj, clip.length / safePitch);
    }
}