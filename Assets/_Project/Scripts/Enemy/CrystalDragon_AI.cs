using UnityEngine;
using System.Collections;

/// <summary>
/// IA do Crystal Dragon — patrulha no chão, ataca com spikes e tail sweep.
/// O voo é apenas uma animação de baixa altitude sem ataque.
/// Usa DummyHealth para vida e PlayerHealth para causar dano.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class CrystalDragon_AI : MonoBehaviour
{
    private Transform playerTransform;
    private DummyHealth health;
    private Rigidbody rb;

    [Header("Ativação")]
    public float activationDistance = 20f;
    private bool isActivated = false;

    [Header("Patrulha")]
    public float patrolRadius = 6f;
    public float patrolPointTime = 4f;
    private Vector3 patrolTarget;
    private float patrolTimer = 0f;
    private Vector3 startPosition;

    [Header("Movimento")]
    public float groundSpeed = 5f;
    public float rotationSpeed = 1.0f;
    public float flightCooldown = 8f;
    private float flightCooldownTimer = 0f;

    [Header("Ataques")]
    public float spikeRange = 18f;
    public float tailRange = 3f;

    [Header("Voo (animação de baixa altitude)")]
    [Tooltip("Distância para iniciar o voo (m)")]
    public float flightTriggerDistance = 4f;
    [Tooltip("Tempo de subida (segundos)")]
    public float flightAscendTime = 0.6f;
    [Tooltip("Tempo de hover no ar (segundos)")]
    public float flightHangTime = 1.0f;
    [Tooltip("Tempo de descida (segundos)")]
    public float flightDescendTime = 0.5f;
    [Tooltip("Altura do voo acima do solo (metros)")]
    public float flightPeakHeight = 1.5f;

    [Header("Tática")]
    [Tooltip("Distância que o dragão tenta manter para usar Crystal Spikes (m)")]
    public float preferredSpikeDistance = 6f;
    [Tooltip("Velocidade de recuo/posição quando mantendo distância (multiplicador)")]
    public float kiteSpeedMultiplier = 1.0f;
    [Header("Orbit")]
    [Tooltip("Velocidade de órbita em graus por segundo")]
    public float orbitSpeed = 45f;
    [Tooltip("Intervalo em segundos para possivel mudança de direção de órbita")]
    public float orbitChangeInterval = 4f;

    private float orbitAngle = 0f;
    private int orbitDirection = 1;
    private float orbitTimer = 0f;
    [Tooltip("Velocidade linear (m/s) usada ao orbitar ao redor do player")]
    public float orbitLinearSpeed = 3f;
    [Tooltip("Tolerância radial (m) para considerar-se na distância preferida e então orbitar tangencialmente")]
    public float orbitRadialTolerance = 0.6f;
    [Header("Animator")]
    [Tooltip("Animator do modelo — trigger de voo")]
    public Animator modelAnimator;
    public string flightAnimTrigger = "Fly";
    [Tooltip("Trigger de animação do Tail Sweep")]
    public string tailSweepAnimTrigger = "TailSweep";

    [Header("Tail Sweep")]
    [Tooltip("Ângulo total varrido pelo dragão durante o sweep (graus)")]
    public float tailSweepArcDegrees = 240f;
    [Tooltip("Duração do arco de rotação do sweep (segundos)")]
    public float tailSweepDuration = 0.55f;
    [Tooltip("Pausa de telegraph antes de iniciar o spin (segundos)")]
    public float tailSweepTelegraph = 0.35f;
    [Tooltip("Pausa de recovery após o sweep (segundos)")]
    public float tailSweepRecovery = 0.3f;
    [Tooltip("Cooldown mínimo entre volleys quando na distância ideal (segundos)")]
    public float spikeCooldownMin = 2f;
    [Tooltip("Cooldown máximo entre volleys quando fora da distância ideal (segundos)")]
    public float spikeCooldownMax = 5f;
    public float tailCooldown = 2f;
    public int spikeDamage = 8;
    public int tailDamage = 15;
    public int spikeCount = 3;
    public float spikeInterval = 0.15f;
    public float tailHitRadius = 2.5f;
    public GameObject attackEffect;
    public Transform attackPoint;

    [Header("Ataque frontal")]
    [Tooltip("Origem das crystal spikes. Se null, usa a posição do próprio inimigo.")]
    public Transform spikeOrigin;
    public float spikeConeAngle = 35f;

    [Header("Crystal Spike Projectile")]
    [Tooltip("Prefab do projétil lançado pelo Crystal Dragon.")]
    public GameObject crystalSpikeProjectilePrefab;
    public float spikeProjectileSpeed = 8f;
    public float spikeProjectileLifetime = 4f;
    public float spikeProjectileVerticalOffset = 0.4f;
    [Tooltip("Número de projéteis disparados em cada volley (forma de cone).")]
    public int spikeProjectilesPerVolley = 3;
    [Tooltip("Ângulo total em graus do cone (os projéteis são distribuídos igualmente).")]
    public float spikeVolleySpreadAngle = 20f;

    [Header("Model Offset")]
    [Tooltip("Offset de rotação local aplicado ao modelo filho para corrigir orientação importada (em Euler degrees)")]
    public Vector3 modelLocalEulerOffset = new Vector3(0f, 180f, 0f);
    [Tooltip("Se true, o offset será aplicado no Start() mesmo que o modelo já tenha rotação local configurada. Se false, preserva a rotação manual definida no prefab.")]
    public bool forceApplyModelOffset = true;

    [Header("Model World Alignment")]
    [Tooltip("Se true, o modelo visual será alinhado no mundo para manter a cabeça para cima enquanto a raiz gira.")]
    public bool preserveModelWorldUpright = true;
    [Tooltip("Euler offsets em world space aplicados ao modelo durante o alinhamento.")]
    public Vector3 modelWorldEulerOffset = new Vector3(-90f, 0f, 0f);

    private Transform modelTransform;
    private Quaternion desiredModelLocalRotation;
    private Quaternion lastLoggedModelLocalRotation;
    private float currentYaw = 0f;

    private float spikeTimer = 0f;
    private float tailTimer = 0f;
    private bool isCharging = false;
    private bool isFlying = false;
    private bool isChargingForSpikes = false;
    private bool isChargingForTail = false;
    private bool hasFlownOnce = false;

    private void Start()
    {
        // 1. Auto-busca Animator se não foi arrastado no Inspector
        if (modelAnimator == null)
        {
            modelAnimator = GetComponentInChildren<Animator>();
        }

        // 2. Fallback dinâmico para o projectile prefab
        if (crystalSpikeProjectilePrefab == null)
        {
            crystalSpikeProjectilePrefab = Resources.Load<GameObject>("Crystal Spike");
            if (crystalSpikeProjectilePrefab != null)
            {
                Debug.Log("[CrystalDragon_AI] Projétil 'Crystal Spike' carregado com sucesso via Resources.");
            }
            else
            {
                Debug.LogError("[CrystalDragon_AI] Falha ao carregar o prefab 'Crystal Spike' das Resources!");
            }
        }

        // Garante a tag "Enemy" e a Layer "Enemy" no root e em todos os filhos
        gameObject.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
        {
            gameObject.layer = enemyLayer;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = enemyLayer;
                child.gameObject.tag = "Enemy";
            }
        }

        // 3. Garante que o root tenha um colisor adequado para o jogador conseguir acertá-lo
        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1f, 0f);
            bc.size = new Vector3(2.5f, 2f, 2.5f);
            bc.isTrigger = true;
            Debug.Log("[CrystalDragon_AI] BoxCollider adicionado automaticamente ao root do dragão.");
        }
        else
        {
            if (existingCollider is BoxCollider)
            {
                BoxCollider bc = (BoxCollider)existingCollider;
                bc.center = new Vector3(0f, 1f, 0f);
                bc.size = new Vector3(2.5f, 2f, 2.5f);
                bc.isTrigger = true;
            }
            else if (existingCollider is CapsuleCollider)
            {
                CapsuleCollider cc = (CapsuleCollider)existingCollider;
                cc.center = new Vector3(0f, 1f, 0f);
                cc.height = 2f;
                cc.radius = 1.25f;
                cc.isTrigger = true;
            }
        }

        // Garante que todos os colidores do dragão sejam triggers e ignorem colisão com outros inimigos (para não servirem de rampa ao Golem)
        Collider[] dragonColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider dc in dragonColliders)
        {
            dc.isTrigger = true;
        }

        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider otherCol in allColliders)
        {
            if (otherCol != null && otherCol.transform.root != transform.root)
            {
                if (otherCol.gameObject.layer == enemyLayer || otherCol.CompareTag("Enemy"))
                {
                    foreach (Collider dc in dragonColliders)
                    {
                        Physics.IgnoreCollision(dc, otherCol, true);
                    }
                }
            }
        }

        rb = GetComponent<Rigidbody>();
        health = GetComponent<DummyHealth>();
        startPosition = transform.position;
        patrolTarget = GetRandomPatrolPoint();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 4. Auto-busca dinâmica do Player no Start()
        FindPlayerReference();

        // 5. Auto-busca do spikeOrigin se null
        if (spikeOrigin == null)
        {
            Transform foundOrigin = transform.Find("spikeOrigin");
            if (foundOrigin == null) foundOrigin = transform.Find("Mouth");
            if (foundOrigin == null) foundOrigin = transform.Find("Head");
            spikeOrigin = foundOrigin != null ? foundOrigin : transform;
        }

        // Init orbit angle/direction
        if (playerTransform != null)
        {
            Vector3 offset = transform.position - playerTransform.position;
            orbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            orbitDirection = (Random.value > 0.5f) ? 1 : -1;
            orbitTimer = Random.Range(1f, orbitChangeInterval);
        }

        currentYaw = transform.eulerAngles.y;

        // Corrige a orientação do modelo filho importado (se existir)
        modelTransform = transform.Find("blue_eyes_alt_dragon");
        if (modelTransform == null)
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null) modelTransform = rend.transform;
        }

        if (modelTransform != null)
        {
            if (forceApplyModelOffset || modelTransform.localEulerAngles == Vector3.zero)
            {
                desiredModelLocalRotation = Quaternion.Euler(modelLocalEulerOffset);
                modelTransform.localRotation = desiredModelLocalRotation;
                lastLoggedModelLocalRotation = desiredModelLocalRotation;
                Debug.Log("[CRYSTAL DRAGON] Applied local euler offset: " + modelLocalEulerOffset);
            }
            else
            {
                Debug.Log("[CRYSTAL DRAGON] Preserving manual model local rotation: " + modelTransform.localEulerAngles);
            }
        }
    }

    private void FindPlayerReference()
    {
        if (playerTransform != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerHealth ph = Object.FindFirstObjectByType<PlayerHealth>();
            if (ph != null) player = ph.gameObject;
        }

        if (player != null)
        {
            playerTransform = player.transform;
            Debug.Log("[CrystalDragon_AI] Referência do Player localizada com sucesso!");
        }
    }

    private void LateUpdate()
    {
        if (modelTransform == null) return;

        if (forceApplyModelOffset)
        {
            if (preserveModelWorldUpright)
            {
                Quaternion targetRotation;
                if (modelTransform == transform)
                {
                    targetRotation = Quaternion.Euler(modelWorldEulerOffset.x, currentYaw + modelWorldEulerOffset.y, modelWorldEulerOffset.z);
                    if (transform.rotation != targetRotation)
                        transform.rotation = targetRotation;
                }
                else
                {
                    targetRotation = Quaternion.Euler(modelWorldEulerOffset);
                    if (modelTransform.localRotation != targetRotation)
                        modelTransform.localRotation = targetRotation;
                }
            }
            else
            {
                if (modelTransform.localRotation != desiredModelLocalRotation)
                {
                    modelTransform.localRotation = desiredModelLocalRotation;
                }
            }
        }
        else
        {
            if (modelTransform.localRotation != lastLoggedModelLocalRotation)
            {
                Debug.LogWarning("[CRYSTAL DRAGON] Model local rotation changed at runtime: " + modelTransform.localEulerAngles);
                lastLoggedModelLocalRotation = modelTransform.localRotation;
            }
        }
    }

    private void Update()
    {
        // Garante busca dinâmica do Player no Update caso não estivesse presente na cena ao instanciar
        if (playerTransform == null)
        {
            FindPlayerReference();
            if (playerTransform == null) return;
        }

        if (health != null)
        {
            if (health.CurrentHealth <= 0) return;
            // Se sofrer dano de longe, ativa o combate imediatamente
            if (health.CurrentHealth < health.maxHealth && !isActivated)
            {
                isActivated = true;
                Debug.Log("[CRYSTAL DRAGON] Ativado por dano sofrido!");
            }
        }

        if (spikeTimer > 0f) spikeTimer -= Time.deltaTime;
        if (tailTimer > 0f) tailTimer -= Time.deltaTime;
        if (hasFlownOnce && flightCooldownTimer > 0f) flightCooldownTimer -= Time.deltaTime;

        if (!isActivated)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= activationDistance)
            {
                isActivated = true;
                Debug.Log("[CRYSTAL DRAGON] Ativado! Player detectado a " + dist.ToString("F1") + "m");

                EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                if (id != null && BestiarioManager.instancia != null)
                {
                    BestiarioManager.instancia.Registrar(id);
                }
            }
            return;
        }

        if (isCharging || isFlying) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= tailRange && tailTimer <= 0f && IsPlayerAtSideOrRear())
        {
            StartCoroutine(PerformTailSweep());
            return;
        }

        if (distanceToPlayer <= flightTriggerDistance && (flightCooldownTimer <= 0f || !hasFlownOnce))
        {
            StartCoroutine(PerformLowFlight());
            return;
        }

        if (distanceToPlayer <= spikeRange && spikeTimer <= 0f)
        {
            StartCoroutine(PerformCrystalSpikes());
            return;
        }
    }

    private void FixedUpdate()
    {
        if (!isActivated || isCharging || isFlying) return;
        if (health != null && health.CurrentHealth <= 0) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= activationDistance)
        {
            // If player is within spike range, try to maintain preferred distance (kiting)
            if (distanceToPlayer <= spikeRange)
            {
                // Orbit / strafe behavior while within spike range
                // Update orbit direction timer
                orbitTimer -= Time.deltaTime;
                if (orbitTimer <= 0f)
                {
                    orbitDirection = (Random.value > 0.5f) ? 1 : -1;
                    orbitTimer = orbitChangeInterval;
                }

                // Advance orbit angle
                orbitAngle += orbitSpeed * orbitDirection * Time.deltaTime;

                // Compute desired orbit position at preferred distance
                Vector3 toPlayer = transform.position - playerTransform.position;
                Vector3 desiredPos = playerTransform.position + Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward * preferredSpikeDistance;

                // Radial difference (positive = we're farther than preferred)
                float radialDiff = toPlayer.magnitude - preferredSpikeDistance;

                // If we're outside the radial tolerance, approach/retreat radially
                // SEU FIXEDUPDATE CORRIGIDO:
                if (Mathf.Abs(radialDiff) > orbitRadialTolerance)
                {
                    // CORREÇÃO: O vetor agora aponta do Dragão para o Player para aproximar corretamente
                    Vector3 radialDirToPlayer = (playerTransform.position - transform.position).normalized; 
                    
                    // Se o dragão estiver longe demais, ele vai em direção ao player. 
                    // Se estiver perto demais (radialDiff negativo), ele se afasta até a preferredSpikeDistance.
                    Vector3 radialTarget = playerTransform.position - radialDirToPlayer * preferredSpikeDistance;
                    
                    MoveTowards(radialTarget, groundSpeed * kiteSpeedMultiplier);
                }
                else
                {
                    // We're close to preferred radius: orbit tangentially smoothly
                    Vector3 radialNorm = toPlayer.normalized;
                    Vector3 tangential = Vector3.Cross(Vector3.up, radialNorm) * orbitDirection; // right/left around player
                    Vector3 tangentialVel = tangential.normalized * orbitLinearSpeed;

                    // Small radial correction to keep stable distance
                    Vector3 radialCorrection = Vector3.zero;
                    if (Mathf.Abs(radialDiff) > 0.05f)
                        radialCorrection = radialNorm * (radialDiff * -0.5f);

                    Vector3 desiredVelocity = tangentialVel + radialCorrection;
                    desiredVelocity.y = 0f;

                    // Smooth velocity change to avoid jitter
                    rb.linearVelocity = Vector3.Lerp(new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z), desiredVelocity, 0.16f) + new Vector3(0f, rb.linearVelocity.y, 0f);
                }
            }
            else
            {
                // If spike is on cooldown, don't rush onto the player: try to move to preferred spike distance
                if (spikeTimer > 0f)
                {
                    Vector3 radialDir = (transform.position - playerTransform.position).normalized;
                    Vector3 radialTarget = playerTransform.position + radialDir * preferredSpikeDistance;
                    MoveTowards(radialTarget, groundSpeed * 0.6f);
                }
                else
                {
                    // Approach to get within spike range
                    MoveTowards(playerTransform.position, groundSpeed);
                }
            }
        }
        else
        {
            Patrol();
        }
    }

    private void MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        Vector3 velocity = direction * speed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    private void Patrol()
    {
        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f || Vector3.Distance(transform.position, patrolTarget) < 1f)
        {
            patrolTarget = GetRandomPatrolPoint();
            patrolTimer = patrolPointTime;
        }
        MoveTowards(patrolTarget, groundSpeed * 0.6f);
    }

    private Vector3 GetRandomPatrolPoint()
    {
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        Vector3 point = startPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
        return point;
    }

    private bool IsPlayerAtSideOrRear()
    {
        Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
        toPlayer.y = 0f;
        float forwardDot = Vector3.Dot(transform.forward, toPlayer);
        return forwardDot < 0.5f;
    }

    private IEnumerator PerformCrystalSpikes() =>
        DummyHealth.PauseAttackWhileStunned(this, PerformCrystalSpikesSteps());

    private IEnumerator PerformCrystalSpikesSteps()
    {
        isCharging = true;
        isChargingForSpikes = true;
        // dynamic cooldown based on distance to preferred spike distance
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float closeness = 0f;
        if (preferredSpikeDistance > 0f)
        {
            closeness = 1f - Mathf.Clamp01(Mathf.Abs(distanceToPlayer - preferredSpikeDistance) / preferredSpikeDistance);
        }
        float dynamicCooldown = Mathf.Lerp(spikeCooldownMax, spikeCooldownMin, closeness);
        spikeTimer = dynamicCooldown;

        Debug.Log("[CRYSTAL DRAGON] Crystal Spikes! Preparando disparo frontal.");
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // Pequena telegraph
        yield return new WaitForSeconds(0.4f);

        Vector3 spawnPosition = (spikeOrigin != null && spikeOrigin != transform) ? spikeOrigin.position : (transform.position + transform.forward * 1.5f + Vector3.up * 1.6f);
        if (spawnPosition.y < transform.position.y + 0.8f) spawnPosition.y = transform.position.y + 1.6f;

        // Spawn a single volley of N projectiles distributed in a cone
        Vector3 baseTarget = playerTransform != null ? playerTransform.position : (spawnPosition + transform.forward * 10f);
        baseTarget.y = spawnPosition.y;
        Vector3 baseDir = (baseTarget - spawnPosition).normalized;
        if (baseDir == Vector3.zero) baseDir = transform.forward;

        int nProjectiles = Mathf.Max(1, spikeProjectilesPerVolley);
        float half = spikeVolleySpreadAngle * 0.5f;

        for (int j = 0; j < nProjectiles; j++)
        {
            float t = (nProjectiles == 1) ? 0f : ((float)j / (nProjectiles - 1));
            float angle = Mathf.Lerp(-half, half, t);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * baseDir;

            GameObject projectileObj = null;
            if (crystalSpikeProjectilePrefab != null)
            {
                projectileObj = Instantiate(crystalSpikeProjectilePrefab, spawnPosition, Quaternion.LookRotation(dir));
            }
            else
            {
                // Fallback visual: esfera pequena para representar projétil
                projectileObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectileObj.transform.position = spawnPosition;
                projectileObj.transform.rotation = Quaternion.LookRotation(dir);
                projectileObj.transform.localScale = Vector3.one * 0.22f;
                Collider col = projectileObj.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
                Rigidbody rbp = projectileObj.AddComponent<Rigidbody>();
                rbp.useGravity = false;

                // Apply crystal-like material color (purple/pink crystal)
                Renderer r = projectileObj.GetComponent<Renderer>();
                if (r != null)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                        ?? Shader.Find("Standard");
                    Material mat = new Material(litShader);
                    Color baseColor = new Color(0.46f, 0.29f, 0.62f); // purple
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                    mat.color = baseColor;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", baseColor * 0.6f);
                    r.material = mat;
                }
            }

            // If prefab has a renderer, tint it to the crystal color so it matches the VFX image
            if (projectileObj != null)
            {
                Renderer existingRend = projectileObj.GetComponentInChildren<Renderer>();
                if (existingRend != null)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                        ?? Shader.Find("Standard");
                    Material mat2 = new Material(litShader);
                    Color tint = new Color(0.46f, 0.29f, 0.62f);
                    if (mat2.HasProperty("_BaseColor")) mat2.SetColor("_BaseColor", tint);
                    mat2.color = tint;
                    mat2.EnableKeyword("_EMISSION");
                    mat2.SetColor("_EmissionColor", tint * 0.6f);
                    existingRend.material = mat2;
                }
            }

            // Attach or configure projectile behaviour
            CrystalSpikeProjectile spikeProjectile = projectileObj.GetComponent<CrystalSpikeProjectile>();
            if (spikeProjectile == null)
            {
                spikeProjectile = projectileObj.AddComponent<CrystalSpikeProjectile>();
            }

            spikeProjectile.owner = gameObject;
            spikeProjectile.damage = spikeDamage;
            spikeProjectile.Launch(dir, spikeProjectileSpeed, spikeProjectileLifetime);
        }

        // Single telegraph delay after volley
        yield return new WaitForSeconds(spikeInterval);

        isCharging = false;
        isChargingForSpikes = false;
    }

    private IEnumerator PerformTailSweep() =>
        DummyHealth.PauseAttackWhileStunned(this, PerformTailSweepSteps());

    private IEnumerator PerformTailSweepSteps()
    {
        isCharging = true;
        isChargingForTail = true;
        tailTimer = tailCooldown;

        Debug.Log("[CRYSTAL DRAGON] Tail Sweep! Telegraph.");
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // Dispara trigger de animação
        if (modelAnimator != null && !string.IsNullOrEmpty(tailSweepAnimTrigger))
            modelAnimator.SetTrigger(tailSweepAnimTrigger);

        // Telegraph — dragão para e "carrega" o giro
        yield return new WaitForSeconds(tailSweepTelegraph);

        // Arco de rotação física
        float elapsed = 0f;
        float startYaw = currentYaw;
        bool hitDealt = false;

        while (elapsed < tailSweepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / tailSweepDuration);
            currentYaw = startYaw + tailSweepArcDegrees * t;
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            // Verifica hit durante toda a duração do giro
            if (!hitDealt)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, tailHitRadius);
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Player"))
                    {
                        PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            playerHealth.TakeDamage(tailDamage, gameObject);
                            Debug.Log("[CRYSTAL DRAGON] Tail Sweep acertou o player! Dano: " + tailDamage);
                        }
                        hitDealt = true;
                        break;
                    }
                }
            }

            yield return null;
        }

        // Recovery
        yield return new WaitForSeconds(tailSweepRecovery);
        isCharging = false;
        isChargingForTail = false;
    }

 private IEnumerator PerformLowFlight() =>
        DummyHealth.PauseAttackWhileStunned(this, PerformLowFlightSteps());

    private IEnumerator PerformLowFlightSteps()
    {
        if (playerTransform == null) yield break;

        isFlying = true;
        isCharging = true;
        hasFlownOnce = true;

        if (health != null) health.isInvulnerable = true;

        Debug.Log("[CRYSTAL DRAGON] Low flight: decolando.");

        if (modelAnimator != null && !string.IsNullOrEmpty(flightAnimTrigger))
            modelAnimator.SetTrigger(flightAnimTrigger);

        rb.linearVelocity = Vector3.zero;
        rb.useGravity = false;

        // Subida suave
        Vector3 startPos = transform.position;
        Vector3 peakPos = new Vector3(transform.position.x, transform.position.y + flightPeakHeight, transform.position.z);

        float elapsed = 0f;
        while (elapsed < flightAscendTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / flightAscendTime));
            rb.MovePosition(Vector3.Lerp(startPos, peakPos, t));
            yield return null;
        }
        rb.MovePosition(peakPos);

        // Hover no ar
        yield return new WaitForSeconds(flightHangTime);

        // Descida suave
        Vector3 landPos = new Vector3(peakPos.x, startPos.y, peakPos.z);
        elapsed = 0f;
        while (elapsed < flightDescendTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / flightDescendTime));
            rb.MovePosition(Vector3.Lerp(peakPos, landPos, t));
            yield return null;
        }
        rb.MovePosition(landPos);

        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        isFlying = false;
        isCharging = false;
        flightCooldownTimer = flightCooldown;

        if (health != null) health.isInvulnerable = false;
        Debug.Log("[CRYSTAL DRAGON] Low flight completo.");
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spikeRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, tailRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, flightTriggerDistance);
    }
    public void Attack()
    {
    Instantiate(
        attackEffect,
        attackPoint.position,
        attackPoint.rotation
    );
    }
}
