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
    [Tooltip("Distância máxima que o player precisa se aproximar para acionar fuga")]
    public float teleportRange = 3.6f;
    [Tooltip("Distância mínima de teleporte (mantém dentro da tela: 5.5m)")]
    public float minTeleportDistance = 5.5f;
    [Tooltip("Distância máxima de teleporte (mantém dentro da tela: 8.5m)")]
    public float maxTeleportDistance = 8.5f;
    [Tooltip("Tempo de recarga longo entre teleportes para o jogador poder combá-la")]
    public float teleportCooldown = 7.5f;
    [Tooltip("Prefab do efeito visual de teleporte (ex: Hovl Studio Teleport). Se nulo, procura automaticamente um filho chamado 'Teleport'.")]
    public GameObject teleportVfxPrefab;
    [Tooltip("Duração do efeito de teleporte antes de ser destruído.")]
    public float teleportVfxDuration = 3f;
    [Tooltip("Gera o efeito visual no ponto de chegada também (Ponto B)?")]
    public bool spawnVfxOnArrival = true;

    private bool hasTeleportedOnFirstHit = false;

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

        // Configura VFX de teleporte (se não foi passado via inspector, procura filho 'Teleport' e desativa para não mover junto)
        if (teleportVfxPrefab == null)
        {
            Transform childTeleport = transform.Find("Teleport");
            if (childTeleport != null)
            {
                teleportVfxPrefab = childTeleport.gameObject;
                childTeleport.gameObject.SetActive(false);
            }
        }

        if (Random.value > 0.5f) orbitDirection = -1;

        // Configura o som de flutuação/hover constante
        SetupHoverAudio();

        // Registra callbacks de áudio de hit e morte
        DummyHealth health = GetComponent<DummyHealth>();
        if (health != null)
        {
            health.onDamageTaken += OnDamageTaken;

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;
        }
    }

    private void OnDamageTaken(int damage, GameObject attacker)
    {
        PlayHitSound();

        if (!isAwake)
        {
            WakeUp();
        }

        // Esquiva reativa no PRIMEIRO golpe da luta:
        if (!hasTeleportedOnFirstHit)
        {
            hasTeleportedOnFirstHit = true;
            teleportTimer = teleportCooldown; // Trava por 7.5s
            Teleport(awayFromPlayer: true);
            return;
        }

        // Golpes subsequentes: só teleporta se o cooldown de 7.5s já expirou!
        // Isso permite ao jogador combá-la livremente sem que ela fique pulando sem parar.
        if (teleportTimer <= 0f)
        {
            teleportTimer = teleportCooldown;
            Teleport(awayFromPlayer: true);
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
        teleportTimer = 3.0f; // Dá 3 segundos livres ao player antes de poder teleportar
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

    void HandleAttack()
    {
        if (attackTimer <= 0)
        {
            StartCoroutine(SkybeamAttack());
            attackTimer = attackInterval;
        }
    }

    void HandleTeleport()
    {
        if (playerTransform == null) return;

        // Só tenta fuga por aproximação se o cooldown estiver 100% pronto (<= 0)
        // e o jogador estiver colado em distância de machado (<= 3.6m)
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance <= teleportRange && teleportTimer <= 0f)
        {
            teleportTimer = teleportCooldown;
            Teleport(awayFromPlayer: true);
        }
    }

    void Teleport(bool awayFromPlayer = true)
    {
        Vector3 bestPosition = Vector3.zero;
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

        // Vetor de fuga para longe do player
        Vector3 baseFleeDir = Vector3.back;
        if (playerTransform != null)
        {
            Vector3 diff = transform.position - playerTransform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude > 0.05f)
            {
                baseFleeDir = diff.normalized;
            }
            else
            {
                baseFleeDir = -playerTransform.forward;
                baseFleeDir.y = 0f;
                baseFleeDir.Normalize();
            }
        }

        float curPlayerDist = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : 0f;
        float bestDistanceToPlayer = 0f;

        // 1ª Tentativa: Leque de Fuga (16 ângulos de -85° a +85° na direção oposta ao player)
        // Usa Raycast físico de chão (funciona tanto com NavMesh quanto sem NavMesh na EnemyTestScene!)
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angleOffset = (attempt == 0) ? 0f : Random.Range(-85f, 85f);
            Vector3 fleeDir = Quaternion.Euler(0f, angleOffset, 0f) * baseFleeDir;
            // Distância equilibrada na tela da câmera: 5.5m a 8.5m (não mais 15-20m longe do player)
            float randomDist = Random.Range(Mathf.Clamp(minTeleportDistance, 5.0f, 6.5f), Mathf.Clamp(maxTeleportDistance, 7.5f, 8.8f));

            Vector3 candidate = transform.position + fleeDir * randomDist;

            // Confinamento em roomBounds (se existir na cena)
            if (roomBounds != null)
            {
                candidate = roomBounds.ClosestPoint(candidate);
                Vector3 toCenter = (roomBounds.bounds.center - candidate).normalized;
                candidate += toCenter * 1.5f;
            }

            // Tenta NavMesh se existir
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit navHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                candidate = navHit.position;
            }

            // Validação por Raycast físico de chão (garante colisão com chão em qualquer cena!)
            if (Physics.Raycast(candidate + Vector3.up * 6f, Vector3.down, out RaycastHit groundHit, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                candidate.y = groundHit.point.y;
                float pDist = playerTransform != null ? Vector3.Distance(candidate, playerTransform.position) : 10f;
                float moveDist = Vector3.Distance(candidate, transform.position);

                // Garante que a pedra realmente se moveu (pelo menos 5m) e se afastou do player
                if (moveDist >= 5f && pDist >= curPlayerDist * 0.95f && pDist > bestDistanceToPlayer)
                {
                    bestPosition = candidate;
                    bestDistanceToPlayer = pDist;
                    foundValidSpot = true;
                }
            }
        }

        // 2ª Tentativa: 360 Graus com distâncias variadas caso esteja encurralada contra a parede
        if (!foundValidSpot)
        {
            for (int a = 0; a < 12; a++)
            {
                Vector2 r = Random.insideUnitCircle.normalized;
                Vector3 candidate = transform.position + new Vector3(r.x, 0f, r.y) * Random.Range(5.5f, 7.5f);

                if (roomBounds != null)
                {
                    candidate = roomBounds.ClosestPoint(candidate);
                }

                if (Physics.Raycast(candidate + Vector3.up * 6f, Vector3.down, out RaycastHit groundHit, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    candidate.y = groundHit.point.y;
                    float moveDist = Vector3.Distance(candidate, transform.position);
                    if (moveDist >= 4.5f)
                    {
                        bestPosition = candidate;
                        foundValidSpot = true;
                        break;
                    }
                }
            }
        }

        // Fallback Absoluto: Desloca diretamente no plano físico para longe do jogador
        if (!foundValidSpot)
        {
            Vector3 fallbackPos = transform.position + baseFleeDir * 6.5f;
            if (Physics.Raycast(fallbackPos + Vector3.up * 6f, Vector3.down, out RaycastHit fHit, 15f))
            {
                fallbackPos.y = fHit.point.y;
            }
            bestPosition = fallbackPos;
        }

        Vector3 departurePos = transform.position;

        // 1. Efeito visual no Ponto A (onde ela estava / saiu)
        SpawnTeleportVfx(departurePos);
        PlayTeleportSound(departurePos);

        // 2. Teleporta o monstro fisicamente para o Ponto B e zera inércias físicas do Rigidbody
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = bestPosition;
        }
        transform.position = bestPosition;
        startY = bestPosition.y;

        // 3. Efeito visual no Ponto B (onde ela reapareceu)
        if (spawnVfxOnArrival)
        {
            SpawnTeleportVfx(bestPosition);
        }
        PlayTeleportSound(bestPosition);

        teleportTimer = teleportCooldown;
        Debug.Log($"[MagicStone] Teleportou com sucesso para {bestPosition}! (Deslocamento: {Vector3.Distance(departurePos, bestPosition):F1}m | Dist Player: {Vector3.Distance(bestPosition, playerTransform != null ? playerTransform.position : Vector3.zero):F1}m)");
    }

    private void SpawnTeleportVfx(Vector3 position)
    {
        if (teleportVfxPrefab == null) return;

        GameObject vfxInstance = Instantiate(teleportVfxPrefab, position, Quaternion.identity);
        vfxInstance.transform.SetParent(null); // Desanexa do monstro para ficar estático no mundo!
        vfxInstance.SetActive(true);

        // Reinicia sistemas de partículas se existirem
        ParticleSystem[] systems = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            ps.Clear();
            ps.Play();
        }

        // Reinicia Animator se existir
        Animator anim = vfxInstance.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }

        Destroy(vfxInstance, teleportVfxDuration);
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

        // Cria a área eletrocutada no solo por 2 segundos (mesmo efeito de eletrocutado da Star!)
        ElectricGroundHazard.Spawn(targetPosition, radius: 1.8f, duration: 2.0f, damage: 5);
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