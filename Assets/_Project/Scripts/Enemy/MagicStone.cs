using UnityEngine;
using System.Collections;

public class MagicStone_AI : MonoBehaviour
{
    [Header("Referências")]
    public Transform playerTransform;
    public GameObject attackMarkerPrefab;
    public GameObject attackBeamPrefab;

    // --- NOVA VARIÁVEL ---
    [HideInInspector]
    public BoxCollider roomBounds; // A sala vai preencher isso automaticamente

    [Header("Ativação")]
    public float activationDistance = 25f;
    private bool isAwake = false;

    [Header("Comportamento de Movimento")]
    public float moveSpeed = 4f;
    public float minOrbitDistance = 12f;
    public float maxOrbitDistance = 15f;
    public int orbitDirection = 1;

    [Header("Comportamento de Flutuação")]
    public float floatHeight = 0.2f;
    public float floatSpeed = 1f;
    private float startY;

    [Header("Teleporte")]
    public float teleportRange = 6f;
    public float minTeleportDistance = 8f;
    public float maxTeleportDistance = 14f;
    public float teleportCooldown = 8f;

    [Header("Ataque")]
    public float attackInterval = 5f;
    public float attackTelegraphTime = 2.5f;

    [Header("Áudio — Movimentação / Flutuação")]
    [Tooltip("Som do hover/flutuação (loop constante da pedra flutuando)")]
    public AudioClip hoverSound;
    [Tooltip("Volume do som de hover")]
    [Range(0f, 1f)]
    public float hoverSoundVolume = 0.4f;

    [Tooltip("Som do teleporte/deslocamento instantâneo")]
    public AudioClip teleportSound;
    [Tooltip("Volume do som de teleporte")]
    [Range(0f, 1f)]
    public float teleportSoundVolume = 0.8f;

    [Header("Áudio — Ataque (Raio e Marcação)")]
    [Tooltip("Som do aviso/marcação de ataque no chão (AttackMarker)")]
    public AudioClip telegraphSound;
    [Tooltip("Volume do som do aviso de marcação")]
    [Range(0f, 1f)]
    public float telegraphSoundVolume = 0.8f;

    [Tooltip("Som do raio caindo do céu (Skybeam/Trovão)")]
    public AudioClip skybeamSound;
    [Tooltip("Volume do som do raio caindo")]
    [Range(0f, 1f)]
    public float skybeamSoundVolume = 0.9f;

    [Header("Áudio — Dano Recebido (Hit)")]
    [Tooltip("Sons reproduzidos quando o MagicStone leva dano (golpe do player). NÃO colocar sons de ataque aqui.")]
    public AudioClip[] hitSounds;
    [Tooltip("Volume dos sons de dano recebido (hit)")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    [Tooltip("Sons reproduzidos quando o MagicStone morre.")]
    public AudioClip[] dieSounds;
    [Tooltip("Volume dos sons de morte")]
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    private AudioSource hoverAudioSource;
    private float originalAttackInterval;
    private float originalMoveSpeed;
    private bool isBuffed = false;
    private float teleportTimer;
    private float attackTimer;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Tenta achar o player se não foi atribuído
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // Snap de segurança no chão para evitar clipagem no piso da sala
        Vector3 rayStart = transform.position + Vector3.up * 4f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit startGround, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 pos = transform.position;
            pos.y = startGround.point.y;
            transform.position = pos;
        }
        else if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Vector3 pos = transform.position;
            pos.y = navHit.position.y;
            transform.position = pos;
        }

        startY = transform.position.y;

        if (Random.value > 0.5f) orbitDirection = -1;

        // Configura o som de flutuação/hover constante
        SetupHoverAudio();

        // Registra callbacks de áudio de hit e morte
        DummyHealth health = GetComponent<DummyHealth>();
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
        if (playerTransform == null) return;

        // Trava de rotação visual (olha sempre para o player, mas em pé)
        Vector3 lookPos = playerTransform.position;
        lookPos.y = transform.position.y;
        transform.LookAt(lookPos);

        HandleFloating();

        if (!isAwake)
        {
            if (Vector3.Distance(transform.position, playerTransform.position) < activationDistance)
            {
                WakeUp();
            }
        }
        else
        {
            teleportTimer -= Time.deltaTime;
            attackTimer -= Time.deltaTime;

            HandleMovement();
            HandleTeleport();
            HandleAttack();
        }
    }

    // Função chamada pelo RoomController para definir os limites
    public void SetRoomBounds(BoxCollider bounds)
    {
        roomBounds = bounds;
    }


    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            originalAttackInterval = attackInterval;
            originalMoveSpeed = moveSpeed;

            attackInterval /= 2f; // Ataca 2x mais rápido
            moveSpeed *= 1.5f;    // Move 50% mais rápido

            // Se quiser aumentar o dano do raio, precisaria passar isso para o prefab do raio,
            // mas a velocidade de ataque já aumenta o DPS drasticamente.
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            attackInterval = originalAttackInterval;
            moveSpeed = originalMoveSpeed;
        }
    }
    void WakeUp()
    {
        isAwake = true;
        teleportTimer = 0;
        attackTimer = attackInterval / 2;

        EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
        Debug.Log("[MAGICSTONE] EnemyIdentity: " + (id != null ? id.nomeInimigo : "NULL") + " | BestiarioManager: " + (BestiarioManager.instancia != null));
        if (id != null && BestiarioManager.instancia != null)
            BestiarioManager.instancia.Registrar(id);
    }

    void HandleMovement()
    {
        // (Lógica de movimento orbital continua igual...)
        Vector3 playerPositionOnPlane = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        Vector3 directionToPlayer = (playerPositionOnPlane - transform.position).normalized;
        float distance = Vector3.Distance(playerPositionOnPlane, transform.position);

        Vector3 orbitDirectionVector = Vector3.Cross(directionToPlayer, Vector3.up) * orbitDirection;
        Vector3 finalMoveDirection = Vector3.zero;

        if (distance < minOrbitDistance)
            finalMoveDirection = (-directionToPlayer + orbitDirectionVector).normalized;
        else if (distance > maxOrbitDistance)
            finalMoveDirection = (directionToPlayer + orbitDirectionVector).normalized;
        else
            finalMoveDirection = orbitDirectionVector;

        rb.linearVelocity = new Vector3(finalMoveDirection.x * moveSpeed, rb.linearVelocity.y, finalMoveDirection.z * moveSpeed);
    }

    void HandleFloating()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            startY = hit.point.y;
        }

        float newY = startY + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void HandleTeleport()
    {
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance < teleportRange && teleportTimer <= 0)
        {
            Teleport();
        }
    }

    void HandleAttack()
    {
        if (attackTimer <= 0)
        {
            StartCoroutine(SkybeamAttack());
            attackTimer = attackInterval;
        }
    }

    void Teleport()
    {
        Vector3 bestPosition = transform.position;
        bool foundValidSpot = false;

        // Se roomBounds for nulo, tenta localizar o RoomController da sala atual
        if (roomBounds == null)
        {
            RoomController rc = GetComponentInParent<RoomController>() ?? FindClosestRoomController();
            if (rc != null && rc.spawnAreas != null && rc.spawnAreas.Count > 0 && rc.spawnAreas[0] != null)
            {
                roomBounds = rc.spawnAreas[0];
            }
        }

        // Tenta até 12 direções diferentes ao redor do jogador procurando um ponto estritamente válido dentro do mapa
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(minTeleportDistance, Mathf.Min(maxTeleportDistance, 10f));
            Vector3 candidate = playerTransform.position + new Vector3(randomDir.x, 0, randomDir.y) * randomDist;

            // 1. Confinamento em roomBounds (com margem de 1.5m para longe das paredes)
            if (roomBounds != null)
            {
                candidate = roomBounds.ClosestPoint(candidate);
                Vector3 toCenter = (roomBounds.bounds.center - candidate).normalized;
                candidate += toCenter * 1.5f;
            }

            // 2. Amostragem rigorosa no NavMesh da sala
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit navHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // 3. Raycast vertical para confirmar que existe chão físico e não é abismo/fora da caverna
                if (Physics.Raycast(navHit.position + Vector3.up * 4f, Vector3.down, out RaycastHit groundHit, 8f))
                {
                    bestPosition = navHit.position;
                    bestPosition.y = groundHit.point.y;
                    foundValidSpot = true;
                    break;
                }
            }
        }

        // Fallback de segurança máxima: se não achou spot válido, teleporta para o centro da sala ou próximo ao jogador no NavMesh
        if (!foundValidSpot)
        {
            if (roomBounds != null)
            {
                bestPosition = roomBounds.bounds.center;
                if (UnityEngine.AI.NavMesh.SamplePosition(bestPosition, out UnityEngine.AI.NavMeshHit centerHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    bestPosition = centerHit.position;
                }
            }
            else if (UnityEngine.AI.NavMesh.SamplePosition(playerTransform.position + transform.forward * -4f, out UnityEngine.AI.NavMeshHit safeHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                bestPosition = safeHit.position;
            }
            bestPosition.y = transform.position.y;
        }

        PlayTeleportSound(transform.position); // Som na posição de origem
        transform.position = bestPosition;
        startY = bestPosition.y;
        PlayTeleportSound(bestPosition);   // Som na posição de chegada
        teleportTimer = teleportCooldown;

        Debug.Log($"[MagicStone] Teleportou com validação de NavMesh para {bestPosition}!");
    }

    private RoomController FindClosestRoomController()
    {
        RoomController[] allRooms = FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        RoomController closest = null;
        float minDist = float.MaxValue;
        foreach (var r in allRooms)
        {
            if (r == null) continue;
            float d = Vector3.Distance(transform.position, r.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = r;
            }
        }
        return closest;
    }

    IEnumerator SkybeamAttack()
    {
        Vector3 targetPosition = new Vector3(playerTransform.position.x, 0.01f, playerTransform.position.z);
        GameObject marker = Instantiate(attackMarkerPrefab, targetPosition, Quaternion.Euler(90, 0, 0));

        // Toca o som do aviso de marcação no chão
        PlayTelegraphSound(targetPosition);

        // Sincroniza a duração do efeito visual com o tempo de telegrafagem
        AttackMarkerEffect effect = marker.GetComponent<AttackMarkerEffect>();
        if (effect != null)
            effect.duration = attackTelegraphTime;

        yield return new WaitForSeconds(attackTelegraphTime);
        Destroy(marker); // Garante destruição mesmo se o efeito falhar

        // Toca o som do raio caindo na posição de impacto
        PlaySkybeamSound(targetPosition);

        // Criar o raio e definir o owner para thorns
        GameObject beam = Instantiate(attackBeamPrefab, targetPosition, Quaternion.identity);
        AttackBeam beamScript = beam.GetComponent<AttackBeam>();
        if (beamScript != null)
        {
            beamScript.owner = gameObject;
        }
    }

    // =============================================
    // SISTEMA DE ÁUDIO (Hover, Telegraph & Skybeam)
    // =============================================

    private void SetupHoverAudio()
    {
        if (hoverSound != null)
        {
            GameObject hoverObj = new GameObject("Audio_Hover");
            hoverObj.transform.SetParent(transform, false);
            hoverAudioSource = hoverObj.AddComponent<AudioSource>();
            hoverAudioSource.clip = hoverSound;
            hoverAudioSource.volume = hoverSoundVolume;
            hoverAudioSource.loop = true;
            hoverAudioSource.spatialBlend = 0.5f; // Semi-3D
            hoverAudioSource.minDistance = 3f;
            hoverAudioSource.maxDistance = 50f;
            hoverAudioSource.Play();
        }
    }

    private void PlayTeleportSound(Vector3 position)
    {
        if (teleportSound == null) return;

        float pitch = Random.Range(0.95f, 1.05f);
        PlayClipAtPointWithPitch(teleportSound, position, pitch, teleportSoundVolume);
    }

    private void PlayTelegraphSound(Vector3 position)
    {
        if (telegraphSound == null) return;

        float pitch = Random.Range(0.95f, 1.05f);
        PlayClipAtPointWithPitch(telegraphSound, position, pitch, telegraphSoundVolume);
    }

    private void PlaySkybeamSound(Vector3 position)
    {
        if (skybeamSound == null) return;

        float pitch = Random.Range(0.95f, 1.05f);
        PlayClipAtPointWithPitch(skybeamSound, position, pitch, skybeamSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;
        float effectiveVolume = volume * GlobalAudioManager.SFXVolumeMultiplier;
        if (effectiveVolume <= 0.001f) return;

        GameObject audioObj = new GameObject("TempSkybeamAudio");
        audioObj.transform.position = position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.pitch = pitch;
        aSource.volume = effectiveVolume;
        aSource.spatialBlend = 0.5f; // Semi-3D
        aSource.minDistance = 5f;
        aSource.maxDistance = 50f;
        aSource.rolloffMode = AudioRolloffMode.Linear;
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
            aSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
        aSource.Play();
        float safePitch = Mathf.Abs(pitch) > 0.01f ? Mathf.Abs(pitch) : 1f;
        Destroy(audioObj, clip.length / safePitch);
    }
}