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
    public float crystalYOffset = 0.5f;

    [Header("Reposicionamento")]
    public float playerMovementTolerance = 2.0f;
    public float minRepositionAngle = 90f;

    [Header("Áudio")]
    [Tooltip("Sons reproduzidos aleatoriamente quando o Cristalus recebe um hit")]
    public AudioClip[] hitSounds;
    [Tooltip("Volume dos sons de hit do Cristalus")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Tooltip("Sons reproduzidos ao criar/dropar cristais")]
    public AudioClip[] crystalSpawnSounds;
    [Tooltip("Volume do som de spawn de cristais")]
    [Range(0f, 1f)]
    public float crystalSpawnVolume = 0.75f;

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

        // Registra callback de som de hit
        if (health != null && hitSounds != null && hitSounds.Length > 0)
        {
            health.onDamageTaken += (damage, attacker) => PlayHitSound();
        }
    }

    void Update()
    {
        if (playerTransform == null || (health != null && health.CurrentHealth <= 0)) return;

        switch (currentState)
        {
            case State.Idle: HandleIdle(); break;
            case State.Approaching: HandleApproaching(); break;
        }
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
            if (animator != null) animator.SetBool("IsMoving", true);
            transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, moveSpeed * Time.deltaTime);
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
                float timer = 0f;
                Vector3 startPos = transform.position;
                Vector3 startRelDir = startPos - pivot;

                while (timer < crystalSpawnInterval)
                {
                    timer += Time.deltaTime;
                    float t = timer / crystalSpawnInterval;
                    
                    Vector3 newPos = Vector3.Slerp(startRelDir, nextRelDir, t) + pivot;
                    

                    Vector3 moveDir = (newPos - transform.position).normalized;
                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                      
                        LookAtTarget(transform.position + moveDir, 20f);
                    }

                    if (animator != null) animator.SetBool("IsMoving", true);
                    transform.position = newPos;
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
        Vector3 dirFromPlayerToEnemy = (transform.position - playerTransform.position).normalized;
        dirFromPlayerToEnemy.y = 0;

        float sign = Random.value > 0.5f ? 1f : -1f;
        Vector3 newDir = Quaternion.Euler(0, minRepositionAngle * sign, 0) * dirFromPlayerToEnemy;
        Vector3 targetPos = playerTransform.position + newDir * targetDistance;

        if (animator != null) animator.SetBool("IsMoving", true);

        while (Vector3.Distance(transform.position, targetPos) > 0.5f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * 1.5f * Time.deltaTime);
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
        spawnPos.y = 0;
        return SpawnCrystal(spawnPos);
    }

    GameObject SpawnCrystal(Vector3 position)
    {
        if (crystalPrefabs == null || crystalPrefabs.Length == 0) return null;

        int randomIndex = Random.Range(0, crystalPrefabs.Length);
        GameObject selectedPrefab = crystalPrefabs[randomIndex];
        if (selectedPrefab == null) return null;

        position.y = crystalYOffset;

        PlayCrystalSpawnSound(position);

        return Instantiate(selectedPrefab, position, Quaternion.identity);
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

    private void PlayCrystalSpawnSound(Vector3 position)
    {
        if (crystalSpawnSounds == null || crystalSpawnSounds.Length == 0) return;
        AudioClip clip = crystalSpawnSounds[Random.Range(0, crystalSpawnSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, position, Random.Range(0.9f, 1.1f), crystalSpawnVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        GameObject audioObj = new GameObject("TempCristalusAudio");
        audioObj.transform.position = position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.pitch = pitch;
        aSource.volume = volume;
        aSource.spatialBlend = 0.5f; // Semi-3D
        aSource.minDistance = 3f;
        aSource.maxDistance = 50f;
        aSource.rolloffMode = AudioRolloffMode.Linear;
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