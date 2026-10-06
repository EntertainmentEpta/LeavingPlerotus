using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class HomingHazard : MonoBehaviour
{
    public enum HazardState { Emergence, Chasing, Fusing, Exploded }

    [Header("Movimento & Perseguição")]
    [Tooltip("A velocidade com que a caveira persegue o jogador.")]
    public float moveSpeed = 3.5f;

    [Header("Emergência da Cabeça do Totem")]
    [Tooltip("Duração do movimento ascendente suave ao sair da cabeça do Totem.")]
    public float emergeDuration = 0.25f;
    [Tooltip("Velocidade de subida suave ao sair do Totem (sobe apenas o suficiente para desencostar do topo).")]
    public float emergeUpSpeed = 1.0f;

    [Header("Flutuação Suave")]
    public float bobAmplitude = 0.12f;
    public float bobSpeed = 2.5f;

    [Header("Bomba de Caveira Goblin (Fusível & Explosão)")]
    [Tooltip("Distância do jogador para a caveira parar e armar o fusível.")]
    public float fuseTriggerDistance = 2.5f;

    [Tooltip("Tempo em segundos que a caveira fica armada antes de explodir.")]
    public float fuseDuration = 0.9f;

    [Tooltip("Raio da área de dano da explosão.")]
    public float explosionRadius = 3.5f;

    [Tooltip("Dano causado ao jogador na explosão.")]
    public int explosionDamage = 25;

    [Header("Áudio e Game Feel")]
    [Tooltip("Som de explosão da caveira.")]
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionSoundVolume = 1.0f;

    [Tooltip("Som de contagem regressiva / fusível da caveira.")]
    public AudioClip fuseTickSound;
    [Range(0f, 1f)] public float fuseTickVolume = 0.75f;

    [Header("Tremor de Tela (Camera Shake)")]
    public bool enableCameraShake = true;
    public float cameraShakeDuration = 0.28f;
    public float cameraShakeIntensity = 0.42f;

    [Header("Física do Impacto")]
    [Tooltip("Força de knockback aplicada ao jogador caso esteja na área de explosão.")]
    public float knockbackForce = 12f;

    [Header("Efeitos Visuais de Explosão")]
    public GameObject explosionVFXPrefab;
    public Color explosionColor = new Color(1f, 0.4f, 0.05f, 1f);
    public Color shockwaveRingColor = new Color(1f, 0.65f, 0.2f, 1f);

    private Transform playerTransform;
    private Rigidbody rb;
    private float bobOffset;
    private HazardState currentState = HazardState.Emergence;

    private float stateTimer = 0f;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private DummyHealth health;
    private Material[] cachedMaterials;
    private AudioSource fuseAudioSource;
    private Vector3 fusingAnchorPos;
    private Light internalPointLight;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);
        originalScale = transform.localScale;

        health = GetComponent<DummyHealth>();
        internalPointLight = GetComponentInChildren<Light>();

        // DESATIVAÇÃO DEFINITIVA DE PULSOS CONTÍNUOS ANTIGOS
        // Desativa qualquer DamageZone ou PulseVisualizer residual
        PulseVisualizer pv = GetComponentInChildren<PulseVisualizer>();
        if (pv != null) pv.enabled = false;

        DamageZone dz = GetComponent<DamageZone>();
        if (dz != null) dz.enabled = false;

        Transform areaVis = transform.Find("AreaVisualizer");
        if (areaVis != null) areaVis.gameObject.SetActive(false);

        // Garante que os colisores da caveira sejam Triggers para fluir sem prender no Totem
        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            if (c != null) c.isTrigger = true;
        }

        // Cache de renderizadores e materiais para piscar durante a fusão sem alocação de GC
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            originalColors = new Color[renderers.Length];
            cachedMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    cachedMaterials[i] = renderers[i].material;
                    if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty("_Color"))
                    {
                        originalColors[i] = cachedMaterials[i].color;
                    }
                    else
                    {
                        originalColors[i] = Color.white;
                    }
                }
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Transform targetPoint = player.transform.Find("TorsoTarget");
            playerTransform = (targetPoint != null) ? targetPoint : player.transform;
        }

        if (currentState == HazardState.Emergence && stateTimer <= 0f)
        {
            stateTimer = emergeDuration;
        }
    }

    public void InitializeEmergence(Vector3 startPos)
    {
        transform.position = startPos;
        currentState = HazardState.Emergence;
        stateTimer = emergeDuration;
    }

    void Update()
    {
        if (health != null && health.CurrentHealth <= 0 && currentState != HazardState.Exploded)
        {
            Explode();
            return;
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Transform targetPoint = player.transform.Find("TorsoTarget");
                playerTransform = (targetPoint != null) ? targetPoint : player.transform;
            }
            else return;
        }

        // FUSÍVEL: para, treme de leve, muda de cor e se prepara para explodir
        if (currentState == HazardState.Fusing)
        {
            stateTimer -= Time.deltaTime;
            float fuseProgress = Mathf.Clamp01(1f - (stateTimer / fuseDuration));

            // 1. Áudio de contagem regressiva acelerando
            if (fuseAudioSource != null && fuseAudioSource.isPlaying)
            {
                fuseAudioSource.pitch = Mathf.Lerp(1.0f, 2.2f, fuseProgress);
            }

            // 2. Mudança de cor: pisca e fica incandescente/vermelho
            float flashSpeed = Mathf.Lerp(8f, 28f, fuseProgress);
            bool isFlash = (Mathf.Sin(Time.time * flashSpeed) > 0);
            Color warningColor = Color.Lerp(new Color(1f, 0.45f, 0.1f), Color.red, fuseProgress);

            if (cachedMaterials != null)
            {
                for (int i = 0; i < cachedMaterials.Length; i++)
                {
                    if (cachedMaterials[i] != null && cachedMaterials[i].HasProperty("_Color"))
                    {
                        cachedMaterials[i].color = isFlash ? warningColor : originalColors[i];
                    }
                }
            }

            if (internalPointLight != null)
            {
                internalPointLight.color = Color.Lerp(Color.yellow, Color.red, fuseProgress);
                internalPointLight.intensity = Mathf.Lerp(2f, 7f, fuseProgress);
            }

            // 3. Tremedinha (leve tremor de instabilidade antes de estourar)
            float jitterAmount = Mathf.Lerp(0.015f, 0.065f, fuseProgress);
            transform.position = fusingAnchorPos + Random.insideUnitSphere * jitterAmount;

            if (stateTimer <= 0f)
            {
                Explode();
            }
        }
    }

    void FixedUpdate()
    {
        if (playerTransform == null || currentState == HazardState.Exploded) return;

        // 1. EMERGÊNCIA SUAVE: sobe só um pouquinho e já direciona na direção do jogador
        if (currentState == HazardState.Emergence)
        {
            Vector3 toPlayer = (playerTransform.position - transform.position);
            Vector3 horizDir = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;

            // Sobe muito pouco (apenas para sair da cabeça do Totem) e já dá um impulso suave para a frente
            rb.linearVelocity = (Vector3.up * emergeUpSpeed) + (horizDir * (moveSpeed * 0.4f));
            transform.LookAt(playerTransform);

            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f)
            {
                currentState = HazardState.Chasing;
            }
            return;
        }

        // 2. PERSEGUIÇÃO: voa DIRETAMENTE na direção do TorsoTarget sem arcos exagerados
        if (currentState == HazardState.Chasing)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Chegou perto do jogador? Para e arma o fusível!
            if (distanceToPlayer <= fuseTriggerDistance)
            {
                ArmFuse();
                return;
            }

            // Direção direta em 3D para o TorsoTarget
            Vector3 toPlayer = (playerTransform.position - transform.position);
            Vector3 direction = toPlayer.normalized;

            // Leve flutuação natural de voo
            float bob = Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobAmplitude;
            Vector3 vel = direction * moveSpeed;
            vel.y += bob;

            rb.linearVelocity = vel;
            transform.LookAt(playerTransform);
        }
        else if (currentState == HazardState.Fusing)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    private void ArmFuse()
    {
        currentState = HazardState.Fusing;
        stateTimer = fuseDuration;
        fusingAnchorPos = transform.position;
        rb.linearVelocity = Vector3.zero;

        // Inicia som de ticking do fusível
        if (fuseTickSound != null)
        {
            if (fuseAudioSource == null)
            {
                GameObject aObj = new GameObject("Audio_FuseTick");
                aObj.transform.SetParent(transform, false);
                fuseAudioSource = aObj.AddComponent<AudioSource>();
                fuseAudioSource.clip = fuseTickSound;
                fuseAudioSource.volume = fuseTickVolume;
                fuseAudioSource.loop = true;
                fuseAudioSource.spatialBlend = 1f;
                fuseAudioSource.minDistance = 3f;
                fuseAudioSource.maxDistance = 30f;
            }
            fuseAudioSource.pitch = 1.0f;
            fuseAudioSource.Play();
        }

        Debug.Log("[GOBLIN SKULL BOMB] Caveira armando fusível com contagem regressiva!");
    }

    public void Explode()
    {
        if (currentState == HazardState.Exploded) return;
        currentState = HazardState.Exploded;

        Vector3 explosionCenter = transform.position;
        Debug.Log("[GOBLIN SKULL BOMB] BOOM! Caveira explodiu com impacto!");

        // Para o som de contagem se estiver tocando
        if (fuseAudioSource != null && fuseAudioSource.isPlaying)
        {
            fuseAudioSource.Stop();
        }

        // 1. Oculta instantaneamente os renderizadores da caveira para não ficar congelada
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = false;
            }
        }
        Collider[] allCols = GetComponentsInChildren<Collider>();
        foreach (var c in allCols)
        {
            if (c != null) c.enabled = false;
        }

        bool dealtDamage = false;

        // 2. Aplica dano e knockback via OverlapSphere
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, explosionRadius);
        foreach (var col in hitColliders)
        {
            if (col != null && (col.CompareTag("Player") || col.name.Contains("Player") || col.GetComponent<PlayerHealth>() != null || col.GetComponentInParent<PlayerHealth>() != null))
            {
                PlayerHealth ph = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(explosionDamage, gameObject);
                    dealtDamage = true;
                    ApplyPlayerKnockback(col.gameObject, explosionCenter);
                    break;
                }
            }
        }

        // Se não encontrou por Overlap (ex: colisor do player em filho), checa distância direta
        if (!dealtDamage && playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(explosionCenter, playerTransform.position);
            if (distToPlayer <= explosionRadius + 0.8f)
            {
                PlayerHealth ph = playerTransform.GetComponent<PlayerHealth>() ?? playerTransform.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(explosionDamage, gameObject);
                    ApplyPlayerKnockback(playerTransform.gameObject, explosionCenter);
                }
            }
        }

        // 3. Som de Explosão 3D Potente com variação de pitch
        PlayExplosionAudio(explosionCenter);

        // 4. Tremor de Câmera Visceral (Impact Feel)
        if (enableCameraShake)
        {
            float dist = playerTransform != null ? Vector3.Distance(explosionCenter, playerTransform.position) : 99f;
            float maxShakeDist = explosionRadius * 2.8f;
            if (dist < maxShakeDist)
            {
                float proximityFactor = 1f - Mathf.Clamp01(dist / maxShakeDist);
                float intensity = cameraShakeIntensity * Mathf.Lerp(0.5f, 1.2f, proximityFactor);
                CameraShakeFeedback.TriggerShake(cameraShakeDuration, intensity, 34f);
            }
        }

        // 5. Instancia Prefab de Efeito (se houver)
        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, explosionCenter, Quaternion.identity);
        }

        // 6. Efeito Procedural Completo (Flash de Luz, Anel de Choque, Fumaça e Estilhaços)
        GameObject fxHost = new GameObject("SkullExplosionFX_Host");
        fxHost.transform.position = explosionCenter;
        SkullExplosionFXRunner fxRunner = fxHost.AddComponent<SkullExplosionFXRunner>();
        fxRunner.Run(explosionRadius, explosionColor, shockwaveRingColor);

        Destroy(gameObject);
    }

    private void ApplyPlayerKnockback(GameObject playerObj, Vector3 center)
    {
        if (knockbackForce <= 0f) return;

        Rigidbody pRb = playerObj.GetComponent<Rigidbody>() ?? playerObj.GetComponentInParent<Rigidbody>();
        if (pRb != null)
        {
            Vector3 pushDir = playerObj.transform.position - center;
            pushDir.y = 0.25f; // leve impulso ascendente para tirar atrito com o piso
            if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector3.up;
            pRb.AddForce(pushDir.normalized * knockbackForce, ForceMode.Impulse);
        }
    }

    private void PlayExplosionAudio(Vector3 position)
    {
        if (explosionSound == null) return;

        GameObject audioObj = new GameObject("TempSkullExplosionAudio");
        audioObj.transform.position = position;

        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = explosionSound;
        aSource.volume = explosionSoundVolume;
        aSource.pitch = Random.Range(0.92f, 1.08f); // variação orgânica
        aSource.spatialBlend = 1f; // Som 3D espacializado
        aSource.minDistance = 4f;
        aSource.maxDistance = 40f;
        aSource.Play();

        Destroy(audioObj, explosionSound.length + 0.15f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fuseTriggerDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

/// <summary>
/// Runner procedural desacoplado que cuida do show visual da explosão:
/// - Clarão de luz exponencial
/// - Domo de fogo inicial
/// - Anéis concêntricos de choque expandindo pelo chão
/// - Puffs de fumaça escura subindo
/// - Estilhaços e fagulhas incandescentes disparados em 3D
/// </summary>
public class SkullExplosionFXRunner : MonoBehaviour
{
    private float radius;
    private Color mainColor;
    private Color ringColor;
    private int pendingCoroutines = 0;

    public void Run(float explosionRadius, Color coreColor, Color ringCol)
    {
        radius = explosionRadius;
        mainColor = coreColor;
        ringColor = ringCol;

        // Dispara todos os elementos visuais sincronizados
        StartCoroutine(FlashLuz());
        StartCoroutine(FireCoreDome());
        StartCoroutine(ShockwaveRing(0f, radius * 1.15f));
        StartCoroutine(ShockwaveRing(0.06f, radius * 1.55f));
        StartCoroutine(SmokePuffs());
        StartCoroutine(FlyingSparks(16));
    }

    // ── 1. Clarão de luz potente e rápido ────────────────────────────────────
    private IEnumerator FlashLuz()
    {
        pendingCoroutines++;
        GameObject lightObj = new GameObject("ExplosionFlashLight");
        lightObj.transform.position = transform.position;

        Light light = lightObj.AddComponent<Light>();
        light.color = Color.Lerp(mainColor, Color.white, 0.4f);
        light.range = radius * 3.2f;
        light.intensity = 14f;

        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Decaimento quadrático para flash impactante
            light.intensity = Mathf.Lerp(14f, 0f, t * t);
            yield return null;
        }

        Destroy(lightObj);
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    // ── 2. Domo / Núcleo de Fogo Volumétrico ─────────────────────────────────
    private IEnumerator FireCoreDome()
    {
        pendingCoroutines++;
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.transform.position = transform.position;
        core.transform.localScale = Vector3.one * 0.3f;

        Collider c = core.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = core.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(mainColor.r, mainColor.g, mainColor.b, 0.95f);
        rend.material = mat;

        float duration = 0.18f;
        float elapsed = 0f;
        float targetScale = radius * 1.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            core.transform.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.one * targetScale, eased);
            Color col = mat.color;
            col.a = Mathf.Lerp(0.95f, 0f, t * t);
            mat.color = col;

            yield return null;
        }

        Destroy(mat);
        Destroy(core);
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    // ── 3. Anel de Choque Rápido no Solo (Shockwave Ring) ─────────────────────
    private IEnumerator ShockwaveRing(float delay, float maxRingRadius)
    {
        pendingCoroutines++;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        GameObject ringObj = new GameObject("ShockRing");
        ringObj.transform.position = transform.position + Vector3.up * 0.06f;

        LineRenderer lr = ringObj.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.positionCount = 49;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material ringMat = new Material(Shader.Find("Sprites/Default"));
        lr.material = ringMat;

        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 2.5f);
            float currentR = Mathf.Lerp(0.1f, maxRingRadius, eased);

            float alpha = 1f - Mathf.Pow(t, 2f);
            float width = Mathf.Lerp(0.35f, 0.02f, t);

            Color c = Color.Lerp(Color.white, ringColor, t);
            c.a = alpha;

            ringMat.color = c;
            lr.startWidth = width;
            lr.endWidth = width;

            for (int i = 0; i <= 48; i++)
            {
                float ang = (float)i / 48 * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * currentR, 0f, Mathf.Sin(ang) * currentR));
            }

            yield return null;
        }

        Destroy(ringMat);
        Destroy(ringObj);
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    // ── 4. Puffs de Fumaça Dinâmica ──────────────────────────────────────────
    private IEnumerator SmokePuffs()
    {
        pendingCoroutines++;
        for (int i = 0; i < 7; i++)
        {
            StartCoroutine(SingleSmokePuff(Random.Range(0.1f, radius * 0.5f)));
            yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
        }
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    private IEnumerator SingleSmokePuff(float baseOffset)
    {
        pendingCoroutines++;
        GameObject puff = new GameObject("SmokePuff");
        Vector2 rand = Random.insideUnitCircle * baseOffset;
        puff.transform.position = transform.position + new Vector3(rand.x, 0.08f, rand.y);

        LineRenderer lr = puff.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.positionCount = 25;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;

        float duration = Random.Range(0.55f, 0.95f);
        float maxR = Random.Range(0.4f, 1.1f);
        float riseSpeed = Random.Range(1.2f, 2.4f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            puff.transform.position += Vector3.up * riseSpeed * Time.deltaTime;
            float currentR = Mathf.Lerp(0.08f, maxR, t);
            float alpha = Mathf.Lerp(0.6f, 0f, t);
            float width = Mathf.Lerp(0.12f, 0.01f, t);

            Color c = Color.Lerp(new Color(0.15f, 0.15f, 0.15f), new Color(0.55f, 0.55f, 0.55f), t);
            c.a = alpha;
            mat.color = c;
            lr.startWidth = width;
            lr.endWidth = width;

            for (int i = 0; i <= 24; i++)
            {
                float ang = (float)i / 24 * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * currentR, 0f, Mathf.Sin(ang) * currentR));
            }

            yield return null;
        }

        Destroy(mat);
        Destroy(puff);
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    // ── 5. Fagulhas e Estilhaços Incandescentes (Flying Sparks) ───────────────
    private IEnumerator FlyingSparks(int count)
    {
        pendingCoroutines++;

        for (int i = 0; i < count; i++)
        {
            StartCoroutine(SingleSparkRoutine());
        }

        yield return null;
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    private IEnumerator SingleSparkRoutine()
    {
        pendingCoroutines++;
        GameObject spark = new GameObject("SparkTrail");
        spark.transform.position = transform.position;

        TrailRenderer tr = spark.AddComponent<TrailRenderer>();
        tr.time = 0.12f;
        tr.startWidth = 0.08f;
        tr.endWidth = 0.01f;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material sparkMat = new Material(Shader.Find("Sprites/Default"));
        sparkMat.color = Color.Lerp(Color.yellow, new Color(1f, 0.35f, 0f), Random.value);
        tr.material = sparkMat;

        // Direção 3D com tendência ascendente em leque
        Vector3 velocity = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(7f, 15f);
        float life = Random.Range(0.25f, 0.5f);
        float elapsed = 0f;

        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            velocity += Vector3.down * 18f * Time.deltaTime; // Gravidade
            velocity = Vector3.Lerp(velocity, Vector3.zero, 1.8f * Time.deltaTime); // Resistência do ar
            spark.transform.position += velocity * Time.deltaTime;

            yield return null;
        }

        Destroy(sparkMat);
        Destroy(spark);
        pendingCoroutines--;
        CheckSelfDestruct();
    }

    private void CheckSelfDestruct()
    {
        if (pendingCoroutines <= 0)
        {
            Destroy(gameObject);
        }
    }
}