using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// IA do Crystal Watcher (Vigia de Cristal):
/// 1. Ataca parado com laser contínuo (ex.: 5s) enquanto o cristal central permanece estável no centro.
/// 2. Ao finalizar o ataque, decide entre permanecer no local ou se reposicionar para uma área melhor
///    calculada em relação ao jogador (boa distância, área aberta e linha de visão livre).
/// 3. Ao se reposicionar, executa a animação característica: primeiro o CoreCrystal sai na frente,
///    depois o casco externo (Body) vem seguindo até se encaixar de volta no novo ponto.
/// </summary>
[RequireComponent(typeof(DummyHealth))]
public class CrystalWatcher_AI : MonoBehaviour
{
    private Transform playerTransform;
    private DummyHealth health;
    private CrystalWatcherVFX vfx;

    // ── Ativação ────────────────────────────────────────────────────
    [Header("Ativação")]
    [Tooltip("Distância em que o inimigo detecta o player e começa a atacar.")]
    public float activationDistance = 20f;

    // ── Ciclo de Ataque ─────────────────────────────────────────────
    [Header("Ciclo de Ataque")]
    [Tooltip("Tempo de carregamento antes do laser disparar (em segundos).")]
    public float chargeTime = 1.35f;

    [Tooltip("Duração do laser ativo (em segundos).")]
    public float fireDuration = 5.0f;

    [Tooltip("Pausa quando decide permanecer no mesmo local entre ataques (em segundos).")]
    public float cooldownTime = 1.8f;

    // ── Reposicionamento Tático ─────────────────────────────────────
    [Header("Reposicionamento Tático")]
    [Tooltip("Ativa a decisão de se reposicionar pelo mapa entre ataques.")]
    public bool enableRepositioning = true;

    [Tooltip("Chance de se reposicionar mesmo quando a posição atual já é aceitável (0 a 1).")]
    [Range(0f, 1f)]
    public float repositionChance = 0.70f;

    [Tooltip("Velocidade média mais lenta e flutuante do deslocamento (m/s).")]
    public float repositionSpeed = 3.2f;

    [Tooltip("Tempo máximo permitido para um reposicionamento (em segundos).")]
    public float maxRepositionDuration = 2.8f;

    [Tooltip("Distância máxima percorrida em um reposicionamento (permite deslocar ~2x mais devagar).")]
    public float maxRepositionDistance = 9.5f;

    [Tooltip("Distância mínima confortável em relação ao player (abaixo disso força reposicionamento).")]
    public float idealMinDistance = 7.5f;

    [Tooltip("Distância ideal preferida em relação ao player ao escolher um novo ponto.")]
    public float preferredDistance = 11.5f;

    [Tooltip("Distância máxima confortável em relação ao player (acima disso força reposicionamento).")]
    public float idealMaxDistance = 16.0f;

    // ── Animação de Reposicionamento: Core sai primeiro (~10%), Casco segue com easing ─
    [Header("Animação Core → Casco (Reposicionamento)")]
    [Tooltip("Filho do cristal central (auto-detectado em CrystalWatcher_Model/CoreCrystal).")]
    public Transform coreCrystalTransform;

    [Tooltip("Filho do casco externo (auto-detectado em CrystalWatcher_Model/Body).")]
    public Transform outerBodyTransform;

    [Tooltip("Fração de avanço do Core em relação ao Corpo (0.08 = Core ~8-10% adiantado com easing suave).")]
    [Range(0.03f, 0.20f)]
    public float coreLeadFraction = 0.08f;

    [Tooltip("Leve elevação flutuante durante o deslocamento (metros).")]
    public float repositionArcLift = 0.16f;

    [Tooltip("Inverte 180° ou gira 90° se o lado vazado do modelo estiver em outro eixo.")]
    public float modelOpenFaceYawOffset = 0f;

    [Header("Animação do Core durante Ataques")]
    [Tooltip("Velocidade máxima de giro do CoreCrystal no carregamento (graus/s).")]
    public float coreChargeMaxSpinSpeed = 420f;

    [Tooltip("Velocidade de giro do CoreCrystal enquanto o laser está disparando (graus/s).")]
    public float coreFireSpinSpeed = 580f;

    // ── Variação Elite — Feixe em V fechando no Player ──────────────
    [Header("Variação Elite — Feixe em V (Tesoura)")]
    [Tooltip("Meia-abertura inicial do V no carregamento e início do disparo (34° = V aberto de 68°).")]
    public float eliteVBeamStartHalfAngle = 34f;

    [Tooltip("Meia-abertura final quando o V se fecha no player (2.5° = quase convergindo num ponto).")]
    public float eliteVBeamEndHalfAngle = 2.5f;

    [Tooltip("Tempo (em segundos) que os dois raios levam para ir se fechando até o player.")]
    public float eliteVBeamCloseDuration = 3.4f;

    // ── Laser ───────────────────────────────────────────────────────
    [Header("Laser")]
    [Tooltip("Dano causado por cada tick do laser.")]
    public int laserDamage = 10;

    [Tooltip("Intervalo entre ticks de dano (em segundos).")]
    public float damageTickRate = 0.3f;

    [Tooltip("Velocidade de rotação do laser em graus por segundo.")]
    public float rotationSpeed = 30f;

    [Tooltip("Alcance máximo do laser.")]
    public float laserRange = 120f;

    [Tooltip("Largura do laser para detecção de colisão.")]
    public float laserWidth = 0.5f;

    // ── Áudio ───────────────────────────────────────────────────────
    [Header("Áudio — Movimentação / Flutuação")]
    public AudioClip hoverSound;
    [Range(0f, 1f)]
    public float hoverSoundVolume = 0.4f;

    [Header("Áudio — Ataque (Laser)")]
    public AudioClip chargeSound;
    [Range(0f, 1f)]
    public float chargeSoundVolume = 0.8f;

    public AudioClip firingSound;
    [Range(0f, 1f)]
    public float firingSoundVolume = 0.8f;

    public AudioClip impactSound;
    [Range(0f, 1f)]
    public float impactSoundVolume = 0.8f;

    [Header("Áudio — Dano Recebido (Hit)")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    public AudioClip[] dieSounds;
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    private AudioSource hoverAudioSource;
    private AudioSource firingAudioSource;
    private AudioSource impactAudioSource;
    private AudioSource baseAudioSource;

    // ── Buff (Crystal Tuner) ────────────────────────────────────────
    private bool isBuffed = false;
    private float originalChargeTime;
    private float originalRotationSpeed;
    private float originalRepositionSpeed;
    private bool IsElite => health != null && health.isElite;

    // ── Máquina de Estados ──────────────────────────────────────────
    private enum State
    {
        Idle,
        Charging,
        Firing,
        Cooldown,
        Repositioning
    }

    private State currentState = State.Idle;
    private float currentChargeProgress = 0f;
    private float fireStartTime = 0f;

    // ── Animação Core → Casco (ativa somente durante Repositioning) ─
    private Vector3 coreBaseLocalPos;
    private Vector3 bodyBaseLocalPos;
    private bool hasSplitParts = false;
    private bool isAnimatingReposition = false;
    private Vector3 animCoreWorldPos;
    private Quaternion animCoreWorldRot = Quaternion.identity;
    private Vector3 animBodyWorldPos;
    private Quaternion animBodyWorldRot = Quaternion.identity;
    private float coreAttackSpinAngle = 0f;
    private float currentCoreSpinSpeed = 0f;

    // ── Laser Visual (3 camadas URP multi-segmento, suporta Feixe em V no Elite) ─
    private const int BeamSegments = 24;
    private struct BeamVisualSet
    {
        public LineRenderer core;
        public LineRenderer glow;
        public LineRenderer arc;
    }

    private BeamVisualSet primaryBeam;
    private BeamVisualSet secondaryBeam; // Usado somente no Elite para o 2º braço do Feixe em V
    private Material runtimeGlowMat;
    private Material runtimeCoreMat;
    private Material runtimeArcMat;
    private float currentLaserAngle = 0f;
    private float damageTimer = 0f;
    private Renderer modelRenderer;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        if (chargeTime < 1.2f) chargeTime = 1.2f;
        if (laserRange <= 20f) laserRange = 120f;

        originalChargeTime = chargeTime;
        originalRotationSpeed = rotationSpeed;
        originalRepositionSpeed = repositionSpeed;

        health = GetComponent<DummyHealth>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        else
            Debug.LogError("[WATCHER] Player não encontrado! Verifique a tag 'Player'.");

        SetupSplitParts();
        SetupLaserVisual();

        modelRenderer = outerBodyTransform != null
            ? outerBodyTransform.GetComponent<Renderer>()
            : GetComponentInChildren<Renderer>();

        vfx = GetComponent<CrystalWatcherVFX>();
        if (vfx == null)
            vfx = gameObject.AddComponent<CrystalWatcherVFX>();

        if (vfx != null && coreCrystalTransform != null)
            vfx.SetCoreAnchor(coreCrystalTransform);

        baseAudioSource = GetComponent<AudioSource>();
        SetupAudioSources();

        if (health != null)
        {
            if (hitSounds != null && hitSounds.Length > 0)
                health.onDamageTaken += (damage, attacker) => PlayHitSound();

            if (dieSounds != null && dieSounds.Length > 0)
                health.onDeath += PlayDieSound;

            health.onDeath += CleanupOnDeath;
        }
    }

    private void SetupSplitParts()
    {
        if (coreCrystalTransform == null)
            coreCrystalTransform = transform.Find("CrystalWatcher_Model/CoreCrystal") ?? FindChildRecursive(transform, "CoreCrystal");

        if (outerBodyTransform == null)
            outerBodyTransform = transform.Find("CrystalWatcher_Model/Body") ?? FindChildRecursive(transform, "Body");

        if (coreCrystalTransform != null && outerBodyTransform != null)
        {
            hasSplitParts = true;
            coreBaseLocalPos = coreCrystalTransform.localPosition;
            bodyBaseLocalPos = outerBodyTransform.localPosition;
            animCoreWorldPos = coreCrystalTransform.position;
            animCoreWorldRot = coreCrystalTransform.rotation;
            animBodyWorldPos = outerBodyTransform.position;
            animBodyWorldRot = outerBodyTransform.rotation;
        }
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName) return child;
            Transform found = FindChildRecursive(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDisable()
    {
        DisableVisualsAndAudio();
    }

    private void OnDestroy()
    {
        if (runtimeGlowMat != null) Destroy(runtimeGlowMat);
        if (runtimeCoreMat != null) Destroy(runtimeCoreMat);
        if (runtimeArcMat != null) Destroy(runtimeArcMat);
    }

    private void CleanupOnDeath()
    {
        DisableVisualsAndAudio();
    }

    private void DisableVisualsAndAudio()
    {
        SetBeamEnabled(primaryBeam, false, false, false);
        SetBeamEnabled(secondaryBeam, false, false, false);
        if (vfx != null) vfx.StopAllEffects();
        StopAllAudioSources();
    }

    private static void SetBeamEnabled(BeamVisualSet beam, bool core, bool glow, bool arc)
    {
        if (beam.core != null) beam.core.enabled = core;
        if (beam.glow != null) beam.glow.enabled = glow;
        if (beam.arc != null) beam.arc.enabled = arc;
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
        if (playerTransform == null) return;

        if (health != null && health.CurrentHealth <= 0)
        {
            DisableVisualsAndAudio();
            return;
        }

        switch (currentState)
        {
            case State.Idle:
                HandleIdle();
                break;

            case State.Charging:
                UpdateLaserVisual();
                break;

            case State.Firing:
                HandleFiring();
                break;

            case State.Cooldown:
            case State.Repositioning:
                // Controlados pela Coroutine AttackCycle
                break;
        }
    }

    // LATEUPDATE — Animação do Core e Casco:
    // • Em Repositioning: aplica posição/rotação com easing (~8-10% Core à frente, Casco seguindo na mesma orientação).
    // • Em Charging / Firing: CoreCrystal gira acelerando como um reator e pulsa levemente no centro do casco.
    // • Parado (Idle / Cooldown): desacelera o giro suavemente até parar no centro.
    void LateUpdate()
    {
        if (!hasSplitParts || coreCrystalTransform == null || outerBodyTransform == null) return;
        if (health != null && health.CurrentHealth <= 0) return;

        if (isAnimatingReposition)
        {
            currentCoreSpinSpeed = 0f;
            coreCrystalTransform.position = animCoreWorldPos;
            coreCrystalTransform.rotation = animCoreWorldRot;
            coreCrystalTransform.localScale = Vector3.one;

            outerBodyTransform.position = animBodyWorldPos;
            outerBodyTransform.rotation = animBodyWorldRot;
            outerBodyTransform.localScale = Vector3.one;
        }
        else
        {
            float targetSpin = 0f;
            float pulseScale = 1f;
            float verticalJitter = 0f;

            if (currentState == State.Charging)
            {
                targetSpin = Mathf.Lerp(40f, coreChargeMaxSpinSpeed, currentChargeProgress * currentChargeProgress);
                pulseScale = Mathf.Lerp(1f, 1.12f, currentChargeProgress) + Mathf.Sin(Time.time * 18f) * 0.03f;
                verticalJitter = Mathf.Sin(Time.time * 12f) * 0.02f * currentChargeProgress;
            }
            else if (currentState == State.Firing)
            {
                float eliteBoost = IsElite ? 1.25f : 1f;
                targetSpin = coreFireSpinSpeed * eliteBoost;
                pulseScale = 1.08f + Mathf.Sin(Time.time * 24f) * 0.05f;
                verticalJitter = Mathf.Sin(Time.time * 15f) * 0.025f;
            }

            currentCoreSpinSpeed = Mathf.Lerp(currentCoreSpinSpeed, targetSpin, Time.deltaTime * 6f);
            coreAttackSpinAngle = (coreAttackSpinAngle + currentCoreSpinSpeed * Time.deltaTime) % 360f;

            coreCrystalTransform.localPosition = coreBaseLocalPos + Vector3.up * verticalJitter;
            coreCrystalTransform.localRotation = Quaternion.Euler(0f, coreAttackSpinAngle, 0f);
            coreCrystalTransform.localScale = Vector3.one * pulseScale;

            outerBodyTransform.localPosition = bodyBaseLocalPos;
            outerBodyTransform.localRotation = Quaternion.identity;
            outerBodyTransform.localScale = Vector3.one;
        }
    }

    // ── Estado Idle ─────────────────────────────────────────────────
    void HandleIdle()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer < activationDistance)
        {
            EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
            if (id != null && BestiarioManager.instancia != null)
                BestiarioManager.instancia.Registrar(id);

            StartCoroutine(AttackCycle());
        }
    }

    // ── Ciclo Principal: Carrega → Dispara (5s) → Decide Ficar ou Reposicionar ─
    IEnumerator AttackCycle() =>
        DummyHealth.PauseAttackWhileStunned(this, AttackCycleSteps());

    private IEnumerator AttackCycleSteps()
    {
        while (health != null && health.CurrentHealth > 0)
        {
            // FASE 1: CARREGAMENTO (parado no lugar, Core quieto no centro)
            currentState = State.Charging;
            isAnimatingReposition = false;
            currentChargeProgress = 0f;

            StopFiringAudioLoop();
            StopImpactAudioLoop();
            PlayChargeSound();

            Vector3 dirToPlayer = playerTransform.position - transform.position;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude > 0.001f)
            {
                currentLaserAngle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, currentLaserAngle, 0f);
            }

            ShowLaserCharging(true);
            if (vfx != null)
            {
                vfx.StartChargeEffect();
                vfx.SetAmbientIntensity(3f);
            }

            float chargeElapsed = 0f;
            float totalCharge = Mathf.Max(0.4f, chargeTime);
            while (chargeElapsed < totalCharge)
            {
                if (health != null && health.CurrentHealth <= 0) yield break;
                currentChargeProgress = Mathf.Clamp01(chargeElapsed / totalCharge);

                // No Elite (tesoura em V), acompanha o player até 70% do carregamento e depois TRAVA o ponto exato
                if (IsElite && currentChargeProgress < 0.70f && playerTransform != null)
                {
                    Vector3 aimDir = playerTransform.position - transform.position;
                    aimDir.y = 0f;
                    if (aimDir.sqrMagnitude > 0.001f)
                    {
                        float aimAngle = Mathf.Atan2(aimDir.x, aimDir.z) * Mathf.Rad2Deg;
                        currentLaserAngle = Mathf.MoveTowardsAngle(currentLaserAngle, aimAngle, 140f * Time.deltaTime);
                        transform.rotation = Quaternion.Euler(0f, currentLaserAngle, 0f);
                    }
                }

                if (vfx != null) vfx.UpdateChargeProgress(currentChargeProgress);
                chargeElapsed += Time.deltaTime;
                yield return null;
            }
            currentChargeProgress = 1f;
            if (vfx != null) vfx.UpdateChargeProgress(1f);

            // FASE 2: DISPARANDO LASER (parado por fireDuration = 5s, Core quieto no centro)
            currentState = State.Firing;
            fireStartTime = Time.time;
            damageTimer = 0f;
            StartFiringAudioLoop();

            if (vfx != null)
            {
                vfx.StopChargeEffect();
                vfx.StartLaserEffect(AngleToDirection(currentLaserAngle));
                vfx.SetAmbientIntensity(5f);
            }

            ShowLaserFiring(true);

            CrystalWatcherEliteOverride elite = GetComponent<CrystalWatcherEliteOverride>();
            float activeFireDuration = (elite != null && elite.isActiveAndEnabled)
                ? elite.trackingDuration
                : fireDuration;
            float fireElapsed = 0f;
            while (fireElapsed < activeFireDuration)
            {
                if (health != null && health.CurrentHealth <= 0) yield break;
                fireElapsed += Time.deltaTime;
                yield return null;
            }

            if (elite != null && elite.isActiveAndEnabled)
            {
                // Retira HandleFiring do Update: só a sequência Elite controla o feixe.
                currentState = State.Charging;
                yield return elite.SweepSequence(playerTransform);
            }

            // FASE 3: DESLIGA O LASER
            ShowLaserFiring(false);
            StopFiringAudioLoop();
            StopImpactAudioLoop();

            if (vfx != null)
            {
                vfx.StopLaserEffect();
                vfx.SetAmbientIntensity(1f);
            }

            if (health != null && health.CurrentHealth <= 0) yield break;

            // FASE 4: DECISÃO TÁTICA — Ficar parado ou se reposicionar para área melhor em relação ao player
            Vector3 bestSpot = transform.position;
            bool shouldReposition = enableRepositioning && ShouldRepositionNow(out bestSpot);
            if (shouldReposition)
            {
                yield return StartCoroutine(RepositionRoutine(bestSpot));
            }
            else
            {
                currentState = State.Cooldown;
                yield return new WaitForSeconds(cooldownTime);
            }

            if (playerTransform == null) yield break;
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist > activationDistance * 1.6f)
            {
                currentState = State.Idle;
                yield break;
            }
        }
    }

    // Pontos de integração opcionais do CrystalWatcherEliteOverride (desabilitado por padrão no prefab para priorizar o Feixe em V).
    internal void StopEliteLaser()
    {
        currentState = State.Cooldown;
        ShowLaserFiring(false);
        StopFiringAudioLoop();
        StopImpactAudioLoop();
        if (vfx != null)
        {
            vfx.StopChargeEffect();
            vfx.StopLaserEffect();
            vfx.SetAmbientIntensity(1f);
        }
    }

    internal void CancelEliteAttack()
    {
        StopAllCoroutines();
        StopEliteLaser();
        currentState = State.Idle;
    }

    internal void BeginEliteLaser(float angle, bool damaging)
    {
        currentState = State.Charging;
        currentLaserAngle = angle;
        transform.rotation = Quaternion.Euler(0f, angle, 0f);
        StopFiringAudioLoop();
        StopImpactAudioLoop();
        if (vfx != null)
        {
            vfx.StopChargeEffect();
            vfx.StopLaserEffect();
        }
        if (damaging)
        {
            damageTimer = damageTickRate;
            ShowLaserFiring(true);
            StartFiringAudioLoop();
            if (vfx != null) vfx.StartLaserEffect(AngleToDirection(angle));
        }
        else
        {
            ShowLaserCharging(true);
            PlayChargeSound();
            if (vfx != null) vfx.StartChargeEffect();
        }
        UpdateLaserVisual();
    }

    internal void TickEliteLaser(float angle, bool damaging)
    {
        currentLaserAngle = angle;
        transform.rotation = Quaternion.Euler(0f, angle, 0f);
        if (damaging && vfx != null) vfx.UpdateLaserDirection(AngleToDirection(angle));
        UpdateLaserVisual();
        if (damaging) CheckLaserHit();
    }

    // ── Decisão & Cálculo do Melhor Ponto Tático em Relação ao Player ─
    private bool ShouldRepositionNow(out Vector3 chosenTarget)
    {
        chosenTarget = transform.position;
        if (playerTransform == null) return false;

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        float currentDist = toPlayer.magnitude;

        bool hasCurrentLOS = HasClearLineOfSight(transform.position + Vector3.up * 1.5f, playerTransform.position + Vector3.up * 1.0f);
        bool mustMove = !hasCurrentLOS || currentDist < idealMinDistance || currentDist > idealMaxDistance;

        if (!mustMove && Random.value > repositionChance)
            return false;

        return TryFindBestTacticalPosition(out chosenTarget);
    }

    private bool TryFindBestTacticalPosition(out Vector3 bestPos)
    {
        bestPos = transform.position;
        if (playerTransform == null) return false;

        int obstacleMask = (1 << 0) | (1 << 3);
        Vector3 playerPos = playerTransform.position;
        float baseY = transform.position.y;
        float floorY = playerPos.y;
        float bestScore = float.NegativeInfinity;
        bool foundAny = false;

        float[] testRadii = { preferredDistance, Mathf.Lerp(idealMinDistance, preferredDistance, 0.5f) };
        const int angleSamples = 12;
        float randomOffset = Random.Range(0f, 360f);

        // Só exige NavMesh se a cena realmente tiver NavMesh assado (funciona tanto na dungeon quanto na EnemyTestScene!)
        NavMeshPath testPath = new NavMeshPath();
        Vector3 startFloorProbe = new Vector3(transform.position.x, floorY, transform.position.z);
        bool sceneHasNavMesh = NavMesh.SamplePosition(startFloorProbe, out NavMeshHit startNavHit, 6.0f, NavMesh.AllAreas);

        foreach (float radius in testRadii)
        {
            for (int i = 0; i < angleSamples; i++)
            {
                float angleRad = (randomOffset + (360f / angleSamples) * i) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
                Vector3 rawFloorCandidate = new Vector3(playerPos.x + dir.x * radius, floorY, playerPos.z + dir.z * radius);
                Vector3 candidate = new Vector3(rawFloorCandidate.x, baseY, rawFloorCandidate.z);

                if (sceneHasNavMesh)
                {
                    if (!NavMesh.SamplePosition(rawFloorCandidate, out NavMeshHit navHit, 3.0f, NavMesh.AllAreas))
                        continue;

                    candidate = new Vector3(navHit.position.x, baseY, navHit.position.z);

                    if (!NavMesh.CalculatePath(startNavHit.position, navHit.position, NavMesh.AllAreas, testPath) ||
                        testPath.status != NavMeshPathStatus.PathComplete)
                        continue;
                }

                // Evita escolher um ponto colado na posição atual (busca deslocamento real de ~5-10m)
                Vector3 deltaFlat = new Vector3(candidate.x - transform.position.x, 0f, candidate.z - transform.position.z);
                float travelDist = deltaFlat.magnitude;
                if (travelDist < 4.8f || travelDist > 18f)
                    continue;

                // Se não houver NavMesh na cena, garante linha de viagem livre de paredes
                if (!sceneHasNavMesh)
                {
                    Vector3 startEye = transform.position + Vector3.up * 1.4f;
                    if (Physics.SphereCast(startEye, 0.55f, deltaFlat / travelDist, out _, travelDist - 0.4f, obstacleMask, QueryTriggerInteraction.Ignore))
                        continue;
                }

                // Checa chão abaixo e espaço livre contra paredes (área boa de atuação)
                Vector3 floorCheckOrigin = new Vector3(candidate.x, Mathf.Max(baseY, floorY) + 2.5f, candidate.z);
                if (!Physics.Raycast(floorCheckOrigin, Vector3.down, 14f, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;

                float clearanceScore = EvaluateOpenClearance(candidate + Vector3.up * 1.4f, obstacleMask);
                if (clearanceScore < 1.2f) continue;

                bool clearLOS = HasClearLineOfSight(candidate + Vector3.up * 1.5f, playerPos + Vector3.up * 1.0f);

                float distToPlayer = Vector3.Distance(new Vector3(candidate.x, 0f, candidate.z), new Vector3(playerPos.x, 0f, playerPos.z));
                float distPenalty = Mathf.Abs(distToPlayer - preferredDistance) * 1.4f;
                float travelPenalty = Mathf.Abs(travelDist - 8.5f) * 0.40f;

                float score = (clearLOS ? 30f : -15f) + clearanceScore * 3.5f - distPenalty - travelPenalty + Random.Range(-1.5f, 1.5f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = candidate;
                    foundAny = true;
                }
            }
        }

        return foundAny;
    }

    private static float EvaluateOpenClearance(Vector3 eyePos, int obstacleMask)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        float minClear = 4.5f;
        float sumClear = 0f;
        for (int i = 0; i < dirs.Length; i++)
        {
            if (Physics.Raycast(eyePos, dirs[i], out RaycastHit hit, 4.5f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < minClear) minClear = hit.distance;
                sumClear += hit.distance;
            }
            else
            {
                sumClear += 4.5f;
            }
        }
        return Mathf.Min(minClear, sumClear * 0.25f);
    }

    private static bool HasClearLineOfSight(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float dist = delta.magnitude;
        if (dist < 0.1f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, delta / dist, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c.CompareTag("Player") || c.CompareTag("Enemy") || c.GetComponent<DummyHealth>() != null)
                continue;
            return false;
        }
        return true;
    }

    // ── Coroutine de Reposicionamento (1.0s total com Easing):
    //    Core orienta o lado vazado e sai ~10% na frente, Casco segue suavemente na mesma orientação ─
    IEnumerator RepositionRoutine(Vector3 destination)
    {
        currentState = State.Repositioning;

        Vector3 startPos = transform.position;
        float fixedY = startPos.y;
        int obstacleMask = (1 << 0) | (1 << 3);

        // Calcula destino intermediário respeitando NavMesh (se existir) e distância máxima de 1s
        Vector3 endPos = new Vector3(destination.x, fixedY, destination.z);
        float floorY = playerTransform != null ? playerTransform.position.y : fixedY;
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.SamplePosition(new Vector3(startPos.x, floorY, startPos.z), out NavMeshHit sHit, 6f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(new Vector3(destination.x, floorY, destination.z), out NavMeshHit dHit, 6f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(sHit.position, dHit.position, NavMesh.AllAreas, path) &&
            path.corners != null && path.corners.Length > 1)
        {
            // Usa o primeiro segmento útil do caminho para um deslize limpo de 1s
            Vector3 firstCorner = new Vector3(path.corners[1].x, fixedY, path.corners[1].z);
            if (Vector3.Distance(startPos, firstCorner) >= 1.5f)
                endPos = firstCorner;
        }

        Vector3 rawDelta = endPos - startPos;
        rawDelta.y = 0f;
        float dist = rawDelta.magnitude;
        if (dist < 1.0f)
        {
            currentState = State.Cooldown;
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        Vector3 travelDir = rawDelta / dist;
        float clampedDist = Mathf.Min(dist, Mathf.Max(2.0f, maxRepositionDistance));

        // Evita atravessar parede durante o deslize de 1s
        if (Physics.SphereCast(startPos + Vector3.up * 1.4f, 0.55f, travelDir, out RaycastHit wallHit, clampedDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            clampedDist = Mathf.Max(0f, wallHit.distance - 0.85f);
        }

        if (clampedDist < 1.2f)
        {
            currentState = State.Cooldown;
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        endPos = startPos + travelDir * clampedDist;

        // Orienta o lado VAZADO (frente +Z ou trás -Z, escolhendo o menor giro <= 90°) na direção da viagem
        // Assim o CoreCrystal NUNCA passa por dentro das paredes laterais sólidas do casco!
        Vector3 openFaceDir = Vector3.Dot(transform.forward, travelDir) >= 0f ? travelDir : -travelDir;
        Quaternion startRot = transform.rotation;
        Quaternion travelRot = Quaternion.LookRotation(openFaceDir) * Quaternion.Euler(0f, modelOpenFaceYawOffset, 0f);

        // Rotação final olhando para o jogador ao chegar no destino
        Quaternion endLookRot = travelRot;
        if (playerTransform != null)
        {
            Vector3 finalToPlayer = playerTransform.position - endPos;
            finalToPlayer.y = 0f;
            if (finalToPlayer.sqrMagnitude > 0.01f)
                endLookRot = Quaternion.LookRotation(finalToPlayer);
        }

        // Captura offsets reais de altura/posição local de Core e Body em relação à raiz
        Transform parentSpace = (coreCrystalTransform != null && coreCrystalTransform.parent != null)
            ? coreCrystalTransform.parent
            : transform;
        Vector3 modelLocalOffset = (parentSpace != transform) ? parentSpace.localPosition : Vector3.zero;
        Vector3 coreOffsetInRoot = modelLocalOffset + coreBaseLocalPos;
        Vector3 bodyOffsetInRoot = modelLocalOffset + bodyBaseLocalPos;

        float totalDuration = Mathf.Clamp(clampedDist / Mathf.Max(1.2f, repositionSpeed), 1.4f, Mathf.Max(1.6f, maxRepositionDuration));
        float lag = Mathf.Clamp(coreLeadFraction, 0.03f, 0.20f);

        // Cronograma normalizado dentro de 1.0s:
        // • Core translada de [0.08 .. 1.0 - lag] (~10% adiantado)
        // • Body translada de [0.08 + lag .. 1.0] (seguindo suavemente ~10% atrás)
        float moveStartCore = 0.08f;
        float moveEndCore = Mathf.Max(moveStartCore + 0.4f, 1.0f - lag);
        float moveStartBody = moveStartCore + lag;
        float moveEndBody = 1.0f;

        isAnimatingReposition = hasSplitParts;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (health != null && health.CurrentHealth <= 0)
            {
                isAnimatingReposition = false;
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / totalDuration);

            // Progresso com easing cúbico suave (aceleração e chegada macias, sem rigidez)
            float uCore = Mathf.Clamp01((t - moveStartCore) / Mathf.Max(0.01f, moveEndCore - moveStartCore));
            float uBody = Mathf.Clamp01((t - moveStartBody) / Mathf.Max(0.01f, moveEndBody - moveStartBody));
            float easeCore = EaseInOutCubic(uCore);
            float easeBody = EaseInOutCubic(uBody);

            // Leve flutuação vertical em arco suave durante o deslocamento
            float coreLift = Mathf.Sin(easeCore * Mathf.PI) * repositionArcLift;
            float bodyLift = Mathf.Sin(easeBody * Mathf.PI) * repositionArcLift;

            // Orientação sincronizada: primeiro alinha o lado vazado com travelDir, no final vira pro player
            Quaternion coreRot = EvaluateRepositionRotation(t, startRot, travelRot, endLookRot, 0f);
            Quaternion bodyRot = EvaluateRepositionRotation(Mathf.Clamp01(t - lag * 0.65f), startRot, travelRot, endLookRot, 0f);

            Vector3 coreRootPos = Vector3.Lerp(startPos, endPos, easeCore) + Vector3.up * coreLift;
            Vector3 bodyRootPos = Vector3.Lerp(startPos, endPos, easeBody) + Vector3.up * bodyLift;

            // Raiz/Collider acompanha o casco suavemente
            transform.position = new Vector3(bodyRootPos.x, fixedY, bodyRootPos.z);
            transform.rotation = bodyRot;

            if (hasSplitParts)
            {
                animCoreWorldPos = coreRootPos + (coreRot * coreOffsetInRoot);
                animCoreWorldRot = coreRot;
                animBodyWorldPos = bodyRootPos + (bodyRot * bodyOffsetInRoot);
                animBodyWorldRot = bodyRot;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Encaixe final exato no destino olhando para o jogador
        transform.position = endPos;
        transform.rotation = endLookRot;
        isAnimatingReposition = false;

        currentState = State.Cooldown;
        yield return new WaitForSeconds(0.25f);
    }

    private static float EaseInOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
    }

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    private static Quaternion EvaluateRepositionRotation(float t, Quaternion startRot, Quaternion travelRot, Quaternion endLookRot, float _)
    {
        if (t < 0.24f)
        {
            float r = EaseOutCubic(t / 0.24f);
            return Quaternion.Slerp(startRot, travelRot, r);
        }
        if (t > 0.78f)
        {
            float r = EaseInOutCubic((t - 0.78f) / 0.22f);
            return Quaternion.Slerp(travelRot, endLookRot, r);
        }
        return travelRot;
    }

    // ── Estado Firing (laser ativo — Feixe simples no normal, Feixe em V fechando no player no Elite) ─
    private void GetActiveBeamAngles(out float angleA, out float angleB, out bool dualBeam)
    {
        dualBeam = IsElite;
        if (!dualBeam)
        {
            angleA = currentLaserAngle;
            angleB = currentLaserAngle;
            return;
        }

        float startHalf = Mathf.Clamp(eliteVBeamStartHalfAngle, 10f, 65f);
        float endHalf = Mathf.Clamp(eliteVBeamEndHalfAngle, 0.5f, startHalf);
        float half = startHalf;

        if (currentState == State.Firing)
        {
            // Fecha suavemente em tesoura mirando onde o player está!
            float closeDur = Mathf.Max(0.8f, eliteVBeamCloseDuration);
            float tClose = Mathf.Clamp01((Time.time - fireStartTime) / closeDur);
            float easedClose = EaseInOutCubic(tClose);
            half = Mathf.Lerp(startHalf, endHalf, easedClose);

            // Após fechar quase por completo, mantém leve tensão pulsante na ponta
            if (tClose >= 1f)
                half = endHalf + Mathf.Abs(Mathf.Sin((Time.time - fireStartTime) * 6f)) * 1.5f;
        }

        angleA = currentLaserAngle - half;
        angleB = currentLaserAngle + half;
    }

    void HandleFiring()
    {
        // No Elite (tesoura em V), NÃO persegue mais o player durante o fechamento:
        // o ponto alvo ficou travado onde o player estava e os dois braços fecham exatamente ali!
        if (!IsElite)
        {
            Vector3 dirToPlayer = playerTransform.position - transform.position;
            dirToPlayer.y = 0f;

            float targetAngle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;
            float angleDifference = Mathf.DeltaAngle(currentLaserAngle, targetAngle);
            float maxRotation = rotationSpeed * Time.deltaTime;

            if (Mathf.Abs(angleDifference) > maxRotation)
                currentLaserAngle += Mathf.Sign(angleDifference) * maxRotation;
            else
                currentLaserAngle = targetAngle;

            transform.rotation = Quaternion.Euler(0f, currentLaserAngle, 0f);
        }

        if (vfx != null)
            vfx.UpdateLaserDirection(AngleToDirection(currentLaserAngle));

        UpdateLaserVisual();
        CheckLaserHit();
    }

    void CheckLaserHit()
    {
        damageTimer += Time.deltaTime;

        Vector3 origin = GetLaserOrigin();
        float minHeight = transform.position.y + 1.2f;
        if (origin.y < minHeight) origin.y = minHeight;

        GetActiveBeamAngles(out float angleA, out float angleB, out bool dualBeam);

        bool hitPlayer = CheckSingleBeamHit(origin, AngleToDirection(angleA));
        if (!hitPlayer && dualBeam)
            hitPlayer = CheckSingleBeamHit(origin, AngleToDirection(angleB));

        SetImpactAudioHitting(hitPlayer);
    }

    private bool CheckSingleBeamHit(Vector3 origin, Vector3 laserDirection)
    {
        float finalLaserDist = laserRange;
        RaycastHit[] wallHits = Physics.RaycastAll(origin, laserDirection, laserRange, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit wh in wallHits)
        {
            if (!wh.collider.CompareTag("Player") && !wh.collider.CompareTag("Enemy") && wh.collider.GetComponent<DummyHealth>() == null)
            {
                if (wh.distance < finalLaserDist)
                    finalLaserDist = wh.distance;
            }
        }

        RaycastHit[] hits = Physics.SphereCastAll(origin, laserWidth, laserDirection, laserRange, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.CompareTag("Player") && h.distance <= finalLaserDist + 0.5f)
            {
                if (damageTimer >= damageTickRate)
                {
                    PlayerHealth playerHealth = h.collider.GetComponent<PlayerHealth>() ?? h.collider.GetComponentInParent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        int finalDamage = isBuffed ? Mathf.RoundToInt(laserDamage * 1.35f) : laserDamage;
                        if (IsElite) finalDamage = Mathf.RoundToInt(finalDamage * 1.15f);
                        playerHealth.TakeDamage(finalDamage, gameObject);
                        damageTimer = 0f;

                        if (vfx != null)
                            vfx.SpawnPlayerHitBurst(h.point.sqrMagnitude > 0.01f ? h.point : h.collider.bounds.center);
                    }
                }
                return true;
            }
        }

        return false;
    }

    // ── Sistema de Áudio ────────────────────────────────────────────
    private void SetupAudioSources()
    {
        if (hoverSound != null)
        {
            GameObject hoverObj = new GameObject("Audio_Hover");
            hoverObj.transform.SetParent(transform, false);
            hoverAudioSource = hoverObj.AddComponent<AudioSource>();
            hoverAudioSource.clip = hoverSound;
            hoverAudioSource.volume = hoverSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            hoverAudioSource.loop = true;
            hoverAudioSource.spatialBlend = 1f;
            hoverAudioSource.minDistance = 3f;
            hoverAudioSource.maxDistance = 25f;
            if (baseAudioSource != null) hoverAudioSource.outputAudioMixerGroup = baseAudioSource.outputAudioMixerGroup;
            hoverAudioSource.Play();
        }

        if (firingSound != null)
        {
            GameObject firingObj = new GameObject("Audio_Firing");
            firingObj.transform.SetParent(transform, false);
            firingAudioSource = firingObj.AddComponent<AudioSource>();
            firingAudioSource.clip = firingSound;
            firingAudioSource.volume = firingSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            firingAudioSource.loop = true;
            firingAudioSource.spatialBlend = 1f;
            firingAudioSource.minDistance = 3f;
            firingAudioSource.maxDistance = 30f;
            if (baseAudioSource != null) firingAudioSource.outputAudioMixerGroup = baseAudioSource.outputAudioMixerGroup;
        }

        if (impactSound != null)
        {
            GameObject impactObj = new GameObject("Audio_Impact");
            impactObj.transform.SetParent(transform, false);
            impactAudioSource = impactObj.AddComponent<AudioSource>();
            impactAudioSource.clip = impactSound;
            impactAudioSource.volume = impactSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            impactAudioSource.loop = true;
            impactAudioSource.spatialBlend = 1f;
            impactAudioSource.minDistance = 3f;
            impactAudioSource.maxDistance = 30f;
            if (baseAudioSource != null) impactAudioSource.outputAudioMixerGroup = baseAudioSource.outputAudioMixerGroup;
        }
    }

    private void PlayChargeSound()
    {
        if (chargeSound != null)
            PlayClipAtPointWithPitch(chargeSound, transform.position, 1.0f, chargeSoundVolume);
    }

    private void StartFiringAudioLoop()
    {
        if (firingAudioSource != null && firingSound != null)
        {
            firingAudioSource.volume = firingSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            if (!firingAudioSource.isPlaying) firingAudioSource.Play();
        }
    }

    private void StopFiringAudioLoop()
    {
        if (firingAudioSource != null && firingAudioSource.isPlaying)
            firingAudioSource.Stop();
    }

    private void SetImpactAudioHitting(bool hitting)
    {
        if (impactAudioSource != null && impactSound != null)
        {
            impactAudioSource.volume = impactSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            if (hitting && !impactAudioSource.isPlaying) impactAudioSource.Play();
            else if (!hitting && impactAudioSource.isPlaying) impactAudioSource.Stop();
        }
    }

    private void StopImpactAudioLoop() => SetImpactAudioHitting(false);

    private void StopAllAudioSources()
    {
        if (hoverAudioSource != null && hoverAudioSource.isPlaying) hoverAudioSource.Stop();
        StopFiringAudioLoop();
        StopImpactAudioLoop();
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;
        float effectiveVolume = volume * GlobalAudioManager.SFXVolumeMultiplier;
        if (effectiveVolume <= 0.001f) return;

        if (baseAudioSource != null)
        {
            baseAudioSource.pitch = pitch;
            if (baseAudioSource.outputAudioMixerGroup == null && GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                baseAudioSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
            baseAudioSource.PlayOneShot(clip, effectiveVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, position, effectiveVolume);
        }
    }

    // ── Visual do Laser (3 Camadas URP: Halo + Núcleo Pulsante + Arco Elétrico Espiral) ─
    private Material CreateAdditiveBeamMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        return mat;
    }

    private BeamVisualSet CreateBeamSet(string prefix)
    {
        BeamVisualSet set = new BeamVisualSet();

        GameObject glowObj = new GameObject(prefix + "_Glow");
        glowObj.transform.SetParent(transform, false);
        set.glow = glowObj.AddComponent<LineRenderer>();
        set.glow.positionCount = BeamSegments;
        set.glow.useWorldSpace = true;
        set.glow.numCapVertices = 8;
        set.glow.numCornerVertices = 4;
        set.glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        set.glow.sharedMaterial = runtimeGlowMat;
        set.glow.enabled = false;

        GameObject coreObj = new GameObject(prefix + "_Core");
        coreObj.transform.SetParent(transform, false);
        set.core = coreObj.AddComponent<LineRenderer>();
        set.core.positionCount = BeamSegments;
        set.core.useWorldSpace = true;
        set.core.numCapVertices = 8;
        set.core.numCornerVertices = 4;
        set.core.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        set.core.sharedMaterial = runtimeCoreMat;
        set.core.enabled = false;

        GameObject arcObj = new GameObject(prefix + "_ElectricArc");
        arcObj.transform.SetParent(transform, false);
        set.arc = arcObj.AddComponent<LineRenderer>();
        set.arc.positionCount = BeamSegments;
        set.arc.useWorldSpace = true;
        set.arc.numCapVertices = 4;
        set.arc.numCornerVertices = 4;
        set.arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        set.arc.sharedMaterial = runtimeArcMat;
        set.arc.enabled = false;

        return set;
    }

    void SetupLaserVisual()
    {
        runtimeGlowMat = CreateAdditiveBeamMaterial();
        runtimeCoreMat = CreateAdditiveBeamMaterial();
        runtimeArcMat = CreateAdditiveBeamMaterial();

        primaryBeam = CreateBeamSet("LaserPrimary");
        secondaryBeam = CreateBeamSet("LaserSecondary");

        ApplyLaserColorPalette(isBuffed);
    }

    void ShowLaserCharging(bool show)
    {
        SetBeamEnabled(primaryBeam, show, show, false);
        SetBeamEnabled(secondaryBeam, show && IsElite, show && IsElite, false);

        if (show)
            UpdateLaserVisual();
    }

    void ShowLaserFiring(bool show)
    {
        SetBeamEnabled(primaryBeam, show, show, show);
        SetBeamEnabled(secondaryBeam, show && IsElite, show && IsElite, show && IsElite);

        if (show)
            UpdateLaserVisual();
    }

    void UpdateLaserVisual()
    {
        if (primaryBeam.core == null || !primaryBeam.core.enabled) return;

        Vector3 origin = GetLaserOrigin();
        float minHeight = transform.position.y + 1.2f;
        if (origin.y < minHeight) origin.y = minHeight;

        GetActiveBeamAngles(out float angleA, out float angleB, out bool dualBeam);

        UpdateSingleBeamVisual(primaryBeam, origin, AngleToDirection(angleA), updateImpactVfx: true, phaseOffset: 0f);

        if (dualBeam)
        {
            if (secondaryBeam.core != null && !secondaryBeam.core.enabled)
            {
                bool firing = currentState == State.Firing;
                SetBeamEnabled(secondaryBeam, true, true, firing);
            }
            UpdateSingleBeamVisual(secondaryBeam, origin, AngleToDirection(angleB), updateImpactVfx: false, phaseOffset: 2.1f);
        }
        else
        {
            SetBeamEnabled(secondaryBeam, false, false, false);
        }
    }

    private void UpdateSingleBeamVisual(BeamVisualSet beam, Vector3 origin, Vector3 direction, bool updateImpactVfx, float phaseOffset)
    {
        if (beam.core == null || !beam.core.enabled) return;

        float finalLaserDist = laserRange;
        Vector3 hitNormal = -direction;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction, laserRange, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (!h.collider.CompareTag("Player") && !h.collider.CompareTag("Enemy") && h.collider.GetComponent<DummyHealth>() == null)
            {
                if (h.distance < finalLaserDist)
                {
                    finalLaserDist = h.distance;
                    hitNormal = h.normal;
                }
            }
        }

        Vector3 endPoint = origin + direction * finalLaserDist;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 up = Vector3.up;

        bool isFiringNow = currentState == State.Firing;
        float t = Time.time;

        for (int i = 0; i < BeamSegments; i++)
        {
            float u = i / (float)(BeamSegments - 1);
            Vector3 p = Vector3.Lerp(origin, endPoint, u);
            float endFade = Mathf.Sin(u * Mathf.PI);

            if (isFiringNow)
            {
                float jitter = Mathf.Sin(u * 26f - t * 34f + phaseOffset) * 0.025f * endFade;
                beam.core.SetPosition(i, p + right * jitter);

                if (beam.glow != null)
                    beam.glow.SetPosition(i, p);

                if (beam.arc != null)
                {
                    float spiralAngle = u * 18f - t * 22f + phaseOffset;
                    float spiralRad = (0.20f + 0.08f * Mathf.Sin(u * 9f - t * 14f + phaseOffset)) * endFade;
                    Vector3 spiralOffset = (right * Mathf.Cos(spiralAngle) + up * Mathf.Sin(spiralAngle)) * spiralRad;
                    beam.arc.SetPosition(i, p + spiralOffset);
                }
            }
            else
            {
                beam.core.SetPosition(i, p);
                if (beam.glow != null) beam.glow.SetPosition(i, p);
            }
        }

        if (isFiringNow)
        {
            if (updateImpactVfx && vfx != null) vfx.UpdateLaserImpact(endPoint, hitNormal);

            float pulse = Mathf.Sin(t * 14f + phaseOffset) * 0.14f;
            beam.core.startWidth = 0.20f + pulse * 0.25f;
            beam.core.endWidth = 0.12f + pulse * 0.15f;
            if (beam.glow != null)
            {
                beam.glow.startWidth = 0.88f + pulse * 0.9f;
                beam.glow.endWidth = 0.50f + pulse * 0.5f;
            }
            if (beam.arc != null)
            {
                beam.arc.startWidth = 0.06f + Mathf.Abs(pulse) * 0.18f;
                beam.arc.endWidth = 0.035f;
            }
        }
        else
        {
            float blink = 0.75f + 0.25f * Mathf.Sin(t * (10f + currentChargeProgress * 24f));
            beam.core.startWidth = Mathf.Lerp(0.025f, 0.09f, currentChargeProgress) * blink;
            beam.core.endWidth = Mathf.Lerp(0.015f, 0.05f, currentChargeProgress) * blink;
            if (beam.glow != null)
            {
                beam.glow.startWidth = Mathf.Lerp(0.08f, 0.26f, currentChargeProgress) * blink;
                beam.glow.endWidth = Mathf.Lerp(0.04f, 0.14f, currentChargeProgress) * blink;
            }
        }
    }

    Vector3 GetLaserOrigin()
    {
        if (coreCrystalTransform != null)
            return coreCrystalTransform.position;
        if (modelRenderer != null)
            return modelRenderer.bounds.center;
        return transform.position + Vector3.up * 1.5f;
    }

    private void ApplyLaserColorPalette(bool buffed)
    {
        Gradient glowGradient = new Gradient();
        glowGradient.SetKeys(
            buffed
                ? new GradientColorKey[] {
                    new GradientColorKey(new Color(0.18f, 0.88f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.10f, 0.58f, 0.96f), 0.5f),
                    new GradientColorKey(new Color(0.22f, 0.90f, 1.0f), 1.0f),
                }
                : new GradientColorKey[] {
                    new GradientColorKey(new Color(0.62f, 0.20f, 0.96f), 0.0f),
                    new GradientColorKey(new Color(0.45f, 0.10f, 0.82f), 0.5f),
                    new GradientColorKey(new Color(0.68f, 0.26f, 0.98f), 1.0f),
                },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.42f, 0.0f),
                new GradientAlphaKey(0.28f, 0.5f),
                new GradientAlphaKey(0.08f, 1.0f),
            }
        );

        Gradient coreGradient = new Gradient();
        coreGradient.SetKeys(
            buffed
                ? new GradientColorKey[] {
                    new GradientColorKey(new Color(0.82f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.32f, 0.92f, 1.0f), 0.35f),
                    new GradientColorKey(new Color(0.15f, 0.74f, 1.0f), 1.0f),
                }
                : new GradientColorKey[] {
                    new GradientColorKey(new Color(0.92f, 0.76f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.76f, 0.38f, 1.0f), 0.35f),
                    new GradientColorKey(new Color(0.58f, 0.16f, 0.94f), 1.0f),
                },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.95f, 0.0f),
                new GradientAlphaKey(0.85f, 0.5f),
                new GradientAlphaKey(0.55f, 1.0f),
            }
        );

        Gradient arcGradient = new Gradient();
        arcGradient.SetKeys(
            buffed
                ? new GradientColorKey[] {
                    new GradientColorKey(new Color(0.75f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.25f, 0.90f, 1.0f), 1.0f),
                }
                : new GradientColorKey[] {
                    new GradientColorKey(new Color(0.88f, 0.65f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.65f, 0.25f, 1.0f), 1.0f),
                },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.85f, 0.0f),
                new GradientAlphaKey(0.65f, 0.6f),
                new GradientAlphaKey(0.10f, 1.0f),
            }
        );

        ApplyGradientsToBeam(primaryBeam, glowGradient, coreGradient, arcGradient);
        ApplyGradientsToBeam(secondaryBeam, glowGradient, coreGradient, arcGradient);
    }

    private static void ApplyGradientsToBeam(BeamVisualSet beam, Gradient glow, Gradient core, Gradient arc)
    {
        if (beam.glow != null) beam.glow.colorGradient = glow;
        if (beam.core != null) beam.core.colorGradient = core;
        if (beam.arc != null) beam.arc.colorGradient = arc;
    }

    Vector3 AngleToDirection(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }

    // ── Buff do Crystal Tuner ───────────────────────────────────────
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            chargeTime = originalChargeTime * 0.72f;
            rotationSpeed = originalRotationSpeed * 1.35f;
            repositionSpeed = originalRepositionSpeed * 1.20f;
            ApplyLaserColorPalette(true);
            if (vfx != null) vfx.SetBuffTint(true);
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            chargeTime = originalChargeTime;
            rotationSpeed = originalRotationSpeed;
            repositionSpeed = originalRepositionSpeed;
            ApplyLaserColorPalette(false);
            if (vfx != null) vfx.SetBuffTint(false);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        Gizmos.color = Color.red;
        Vector3 dir = AngleToDirection(currentLaserAngle);
        Gizmos.DrawRay(GetLaserOrigin(), dir * laserRange);

        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, preferredDistance);
    }
}
