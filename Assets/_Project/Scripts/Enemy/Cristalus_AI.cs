using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(DummyHealth))]
public class Cristalus_AI : MonoBehaviour
{
    private Transform playerTransform;
    private DummyHealth health;

    [Header("Ativação & Movimentação")]
    public float activationDistance = 25f;
    public float targetDistance = 8f;
    public float moveSpeed = 4f;
    public float trailDropInterval = 1.5f;
    public float trailOffset = 1.5f; 
    private float trailTimer = 0f;

    [Header("Arco Cristalino")]
    public GameObject[] crystalPrefabs; // Os colchetes [] indicam que agora é uma lista!
    public float arcAngle = 120f;
    public int crystalsPerArc = 5;
    public float crystalSpawnInterval = 0.5f;
    public float arcOverlapThreshold = 3f;

    [Header("Ajuste de Altura dos Cristais")]
    [Tooltip("Ajuste de altura Y para o spawn dos cristais (caso o pivô esteja no centro).")]
    public float crystalYOffset = 0.15f;

    [Header("Reposicionamento")]
    public float playerMovementTolerance = 2.0f;
    public float minRepositionAngle = 90f;

    [Header("Áudio — Movimentação (Slide)")]
    [Tooltip("Sons reproduzidos quando o Cristalus dá o slide/desliza")]
    public AudioClip[] slideSounds;
    [Tooltip("Volume dos sons de slide")]
    [Range(0f, 1f)]
    public float slideSoundVolume = 0.8f;

    [Header("Áudio — Habilidade / Ataque (Cristais)")]
    [Tooltip("Sons reproduzidos ao criar/dropar cristais (arco e rastro)")]
    public AudioClip[] crystalSpawnSounds;
    [Tooltip("Volume do som de spawn de cristais")]
    [Range(0f, 1f)]
    public float crystalSpawnVolume = 0.75f;
    [Tooltip("Sons reproduzidos quando os cristais do Cristalus explodem")]
    public AudioClip[] crystalExplodeSounds;
    [Tooltip("Volume do som de explosão dos cristais")]
    [Range(0f, 1f)]
    public float crystalExplodeVolume = 0.8f;

    [Header("Áudio — Dano Recebido (Hit)")]
    [Tooltip("Sons reproduzidos quando o Cristalus leva dano (golpe do player). NÃO colocar sons de ataque aqui.")]
    public AudioClip[] hitSounds;
    [Tooltip("Volume dos sons de dano recebido (hit)")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    [Tooltip("Sons reproduzidos quando o Cristalus morre.")]
    public AudioClip[] dieSounds;
    [Tooltip("Volume dos sons de morte")]
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    // Variaveis Crystal Tunner
    private bool isBuffed = false;
    private float originalMoveSpeed;
    private float originalTrailDropInterval;
    private float originalCrystalSpawnInterval;

    private Animator animator;

    private enum State { Idle, Approaching, CastingArc, Repositioning }
    private State currentState = State.Idle;
    private List<CrystalArcGroup> activeArcs = new List<CrystalArcGroup>();

    void Start()
    {
        health = GetComponent<DummyHealth>();
        animator = GetComponentInChildren<Animator>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        // Registra callbacks de áudio de hit e morte
        if (health != null)
        {
            if (hitSounds != null && hitSounds.Length > 0)
                health.onDamageTaken += (damage, attacker) => PlayHitSound();

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;
        }
    }

    void Update()
    {
        if (playerTransform == null || (health != null && health.CurrentHealth <= 0)) return;

        KeepOnGround();

        switch (currentState)
        {
            case State.Idle: HandleIdle(); break;
            case State.Approaching: HandleApproaching(); break;
        }
    }

    private void KeepOnGround()
    {
        Vector3 rayStart = transform.position + Vector3.up * 2.5f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 8.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestGroundY = float.MinValue;
        bool found = false;

        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.transform.IsChildOf(transform)) continue;
            if (h.collider.CompareTag("Player") || h.collider.CompareTag("Enemy")) continue;

            string n = h.collider.gameObject.name.ToLower();
            if (n.Contains("crystal") || n.Contains("prop") || h.collider.CompareTag("Prop")) continue;

            if (h.point.y <= transform.position.y + 0.8f && h.point.y > bestGroundY)
            {
                bestGroundY = h.point.y;
                found = true;
            }
        }

        if (found)
        {
            float targetY = bestGroundY + 0.05f;
            transform.position = new Vector3(transform.position.x, Mathf.MoveTowards(transform.position.y, targetY, 15f * Time.deltaTime), transform.position.z);
        }
    }

    private bool CheckWallCollision(Vector3 moveDir, out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;
        if (moveDir.sqrMagnitude < 0.001f) return false;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, moveDir.normalized, 1.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.transform.IsChildOf(transform)) continue;
            if (h.collider.CompareTag("Player") || h.collider.CompareTag("Enemy")) continue;

            string n = h.collider.gameObject.name.ToLower();
            if (n.Contains("crystal") || n.Contains("prop") || h.collider.CompareTag("Prop")) continue;

            if (h.normal.y < 0.6f)
            {
                wallNormal = h.normal;
                return true;
            }
        }

        return false;
    }

    void HandleIdle()
    {
        if (animator != null) animator.SetBool("IsMoving", false);

        if (Vector3.Distance(transform.position, playerTransform.position) <= activationDistance)
            currentState = State.Approaching;
    }

    void HandleApproaching()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        LookAtTarget(playerTransform.position, 8f); 

        if (distToPlayer <= targetDistance)
        {
            if (animator != null) animator.SetBool("IsMoving", false);
            StartCoroutine(CastArcRoutine());
        }
        else
        {
            Vector3 target = playerTransform.position;
            target.y = transform.position.y;
            Vector3 moveDir = (target - transform.position).normalized;

            // Se bater numa superfície/parede, realiza movimento circular para contornar e voltar
            if (CheckWallCollision(moveDir, out Vector3 wallNormal))
            {
                Vector3 circleReturnDir = Vector3.Cross(wallNormal, Vector3.up).normalized;
                if (Vector3.Dot(circleReturnDir, moveDir) < 0f) circleReturnDir = -circleReturnDir;
                transform.position += circleReturnDir * (moveSpeed * Time.deltaTime);
                LookAtTarget(transform.position + circleReturnDir, 12f);
            }
            else
            {
                if (animator != null) animator.SetBool("IsMoving", true);
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            }

            KeepOnGround();

            trailTimer += Time.deltaTime;
            if (trailTimer >= trailDropInterval)
            {
                DropTrailCrystal();
                trailTimer = 0f;
            }
        }
    }

    IEnumerator CastArcRoutine()
    {
        currentState = State.CastingArc;
        
        Vector3 pivot = playerTransform.position;
        pivot.y = transform.position.y; 
        Vector3 playerPosAtStart = pivot;

        CrystalArcGroup newArcGroup = new CrystalArcGroup(pivot);
        CheckArcOverlap(newArcGroup);
        activeArcs.Add(newArcGroup);

        float sweepDirection = Random.value > 0.5f ? 1f : -1f;
        float angleStep = (arcAngle / (crystalsPerArc - 1)) * sweepDirection;

        for (int i = 0; i < crystalsPerArc; i++)
        {
            
            Vector3 currentRelDir = transform.position - pivot;
            Vector3 nextRelDir = Quaternion.Euler(0, angleStep, 0) * currentRelDir;
            Vector3 tangentDir = (nextRelDir - currentRelDir).normalized;

            if (i < crystalsPerArc - 1) 
                transform.rotation = Quaternion.LookRotation(tangentDir); 

         
            GameObject crystal = DropTrailCrystal();
            if (crystal != null) newArcGroup.crystals.Add(crystal);

            if (i < crystalsPerArc - 1)
            {
                PlaySlideSound();
                float timer = 0f;
                Vector3 startPos = transform.position;
                Vector3 startRelDir = startPos - pivot;

                while (timer < crystalSpawnInterval)
                {
                    timer += Time.deltaTime;
                    float t = timer / crystalSpawnInterval;
                    
                    Vector3 newPos = Vector3.Slerp(startRelDir, nextRelDir, t) + pivot;
                    newPos.y = transform.position.y;

                    Vector3 moveDir = (newPos - transform.position).normalized;

                    // Se detectar parede durante o arco, quebra para contorno seguro
                    if (CheckWallCollision(moveDir, out Vector3 wallNormal))
                    {
                        break;
                    }

                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                        LookAtTarget(transform.position + moveDir, 20f);
                    }

                    if (animator != null) animator.SetBool("IsMoving", true);
                    transform.position = newPos;
                    KeepOnGround();
                    yield return null;
                }
                if (animator != null) animator.SetBool("IsMoving", false);
            }
        }

        float distanceMovedByPlayer = Vector3.Distance(playerPosAtStart, playerTransform.position);
        if (distanceMovedByPlayer < playerMovementTolerance)
            StartCoroutine(RepositionRoutine());
        else
            currentState = State.Approaching;
    }

    IEnumerator RepositionRoutine()
    {
        currentState = State.Repositioning;
        PlaySlideSound();
        Vector3 dirFromPlayerToEnemy = (transform.position - playerTransform.position).normalized;
        dirFromPlayerToEnemy.y = 0;

        float sign = Random.value > 0.5f ? 1f : -1f;
        Vector3 newDir = Quaternion.Euler(0, minRepositionAngle * sign, 0) * dirFromPlayerToEnemy;
        Vector3 targetPos = playerTransform.position + newDir * targetDistance;

        if (animator != null) animator.SetBool("IsMoving", true);

        while (Vector3.Distance(transform.position, targetPos) > 0.5f)
        {
            Vector3 moveDir = (targetPos - transform.position).normalized;
            if (CheckWallCollision(moveDir, out Vector3 wallNormal))
            {
                Vector3 circleReturnDir = Vector3.Cross(wallNormal, Vector3.up).normalized;
                targetPos = transform.position + circleReturnDir * 3.0f;
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * 1.5f * Time.deltaTime);
            KeepOnGround();
            LookAtTarget(targetPos, 10f);

            trailTimer += Time.deltaTime;
            if (trailTimer >= trailDropInterval)
            {
                DropTrailCrystal();
                trailTimer = 0f;
            }
            yield return null;
        }

        if (animator != null) animator.SetBool("IsMoving", false);
        currentState = State.Approaching;
    }

    void CheckArcOverlap(CrystalArcGroup newArc)
    {
        activeArcs.RemoveAll(a => a.IsDestroyed());
        foreach (var oldArc in activeArcs)
        {
            if (Vector3.Distance(newArc.center, oldArc.center) < arcOverlapThreshold)
                oldArc.DestroyAllCrystals();
        }
    }

    GameObject DropTrailCrystal()
    {
    
        Vector3 spawnPos = transform.position - (transform.forward * trailOffset);
        // spawnPos.y mantem a altura do Cristalus
        return SpawnCrystal(spawnPos);
    }

    GameObject SpawnCrystal(Vector3 position)
    {
        if (crystalPrefabs == null || crystalPrefabs.Length == 0) return null;

        int randomIndex = Random.Range(0, crystalPrefabs.Length);
        GameObject selectedPrefab = crystalPrefabs[randomIndex];
        if (selectedPrefab == null) return null;

        // Garante que o cristal seja posicionado sobre a superfície real do chão via Raycast
        RaycastHit groundHit;
        if (Physics.Raycast(position + Vector3.up * 4f, Vector3.down, out groundHit, 15f))
        {
            position.y = groundHit.point.y + crystalYOffset;
        }
        else
        {
            position.y = transform.position.y + crystalYOffset;
        }

        PlayCrystalSpawnSound(position);

        GameObject crystalObj = Instantiate(selectedPrefab, position, Quaternion.identity);
        if (crystalObj != null)
        {
            SonicCrystal sonic = crystalObj.GetComponent<SonicCrystal>();
            if (sonic != null)
            {
                if ((sonic.explodeSounds == null || sonic.explodeSounds.Length == 0) && crystalExplodeSounds != null && crystalExplodeSounds.Length > 0)
                {
                    sonic.explodeSounds = crystalExplodeSounds;
                    sonic.explodeVolume = crystalExplodeVolume;
                }
            }
        }
        return crystalObj;
    }

    void LookAtTarget(Vector3 target, float rotationSpeed)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.1f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotationSpeed * Time.deltaTime);
        }
    }

    // Buff do Crystal Tuner
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            originalMoveSpeed = moveSpeed;
            originalTrailDropInterval = trailDropInterval;
            originalCrystalSpawnInterval = crystalSpawnInterval;

            moveSpeed *= 1.5f;             
            trailDropInterval *= 0.6f;    
            crystalSpawnInterval *= 0.6f; 
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            moveSpeed = originalMoveSpeed;
            trailDropInterval = originalTrailDropInterval;
            crystalSpawnInterval = originalCrystalSpawnInterval;
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

    public void PlayCrystalExplodeSound(Vector3 position)
    {
        if (crystalExplodeSounds == null || crystalExplodeSounds.Length == 0) return;
        AudioClip clip = crystalExplodeSounds[Random.Range(0, crystalExplodeSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, position, Random.Range(0.9f, 1.1f), crystalExplodeVolume);
    }

    private void PlayCrystalSpawnSound(Vector3 position)
    {
        if (crystalSpawnSounds == null || crystalSpawnSounds.Length == 0) return;
        AudioClip clip = crystalSpawnSounds[Random.Range(0, crystalSpawnSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, position, Random.Range(0.9f, 1.1f), crystalSpawnVolume);
    }

    public void AnimationEvent_Slide() => PlaySlideSound();
    public void Slide() => PlaySlideSound();
    public void OnSlide() => PlaySlideSound();

    public void PlaySlideSound()
    {
        if (slideSounds == null || slideSounds.Length == 0) return;
        AudioClip clip = slideSounds[Random.Range(0, slideSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.9f, 1.1f), slideSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;
        float effectiveVolume = volume * GlobalAudioManager.SFXVolumeMultiplier;
        if (effectiveVolume <= 0.001f) return;

        GameObject audioObj = new GameObject("TempCristalusAudio");
        audioObj.transform.position = position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.pitch = pitch;
        aSource.volume = effectiveVolume;
        aSource.spatialBlend = 0.5f; // Semi-3D
        aSource.minDistance = 3f;
        aSource.maxDistance = 50f;
        aSource.rolloffMode = AudioRolloffMode.Linear;
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
            aSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
        aSource.Play();
        float safePitch = Mathf.Abs(pitch) > 0.01f ? Mathf.Abs(pitch) : 1f;
        Destroy(audioObj, clip.length / safePitch);
    }


        private class CrystalArcGroup
    {
        public Vector3 center;
        public List<GameObject> crystals = new List<GameObject>();
        public CrystalArcGroup(Vector3 centerPoint) { center = centerPoint; }
        
        public bool IsDestroyed() { crystals.RemoveAll(c => c == null); return crystals.Count == 0; }

        public void DestroyAllCrystals() 
        { 
            foreach (var c in crystals) 
            {
                if (c != null) 
                {
                  
                    SonicCrystal crystalScript = c.GetComponent<SonicCrystal>();
                    
                    if (crystalScript != null)
                    {
                        crystalScript.SelfDestruct();
                    }
                    else
                    {
                        Destroy(c);
                    }
                }
            }
            crystals.Clear(); 
        }
    }
}