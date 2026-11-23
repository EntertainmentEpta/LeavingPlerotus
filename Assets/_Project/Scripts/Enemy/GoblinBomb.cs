using UnityEngine;
using System.Collections;

/// <summary>
/// Bomba do Goblin.
/// - Voa em arco balístico exato com rastro sutil de pavio e marcador limpo no chão onde vai cair
/// - Explode ao tocar chão/sólidos (ignora Player no ar e inimigos)
/// - Usa prefabs reais de explosão (CFXR Explosion 1 + CFXR2 Ground Hit) em URP
/// </summary>
public class BombaExplosiva : ShieldProjectile
{
    [Header("Dano")]
    public int danoExplosao = 30;
    [Tooltip("Raio base sem buff. O Crystal Tuner pode aumentar.")]
    public float raioExplosao = 2.4f;
    public float forcaKnockback = 10f;

    [Header("Elite — Bomb Mark System")]
    [Tooltip("Ative SOMENTE no prefab da bomba da Goblin Elite. Ao acertar o player, aciona o BombMarkSystem (marca + stun) ao inves de dano direto.")]
    public bool marksOnHit = false;

    [Header("Timer")]
    public float tempoParaExplodir = 3f;

    [Header("Visual & VFX")]
    [Tooltip("Prefab principal da explosão (ex.: CFXR Explosion 1).")]
    public GameObject efeitoExplosao;
    [Tooltip("Prefab opcional de onda de impacto no chão (ex.: CFXR2 Ground Hit).")]
    public GameObject groundHitVfxPrefab;
    [Tooltip("Material limpo anti-clipping para o marcador de aterrissagem no chão.")]
    public Material telegraphMaterial;
    public Color corExplosao = new Color(1f, 0.48f, 0.12f, 1f);

    [Header("Indicador de Perigo")]
    public Renderer bombaRenderer;

    [Header("Áudio")]
    [Tooltip("Som de tick da bomba acesa voando (loop enquanto está no ar)")]
    public AudioClip tickSound;
    [Tooltip("Volume do som de tick")]
    [Range(0f, 1f)]
    public float tickSoundVolume = 0.6f;

    [Tooltip("Som da explosão ao detonar")]
    public AudioClip explosionSound;
    [Tooltip("Volume do som da explosão")]
    [Range(0f, 1f)]
    public float explosionSoundVolume = 1.0f;

    protected override int ReflectedDamage => danoExplosao;
    protected override void Redirect(Vector3 aim)
    {
        if (activeLandingTelegraph != null)
        {
            Destroy(activeLandingTelegraph);
            activeLandingTelegraph = null;
        }
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) body.linearVelocity = aim * Mathf.Max(12f, body.linearVelocity.magnitude);
        transform.forward = aim;
    }

    private bool explodiu = false;
    private AudioSource audioSource;
    private GameObject activeLandingTelegraph;
    private Material fuseTrailMat;
    private Material instanceBombMat;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (bombaRenderer == null)
            bombaRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

        // Ignora colisão com o Player no ar para explodir no chão/impacto com área clara
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Collider cPlayer = player.GetComponent<Collider>();
            Collider cBomba = GetComponent<Collider>();
            if (cPlayer != null && cBomba != null)
                Physics.IgnoreCollision(cBomba, cPlayer);
        }

        // Rastro sutil de pavio aceso para leitura clara da parábola no ar
        SetupFuseTrail();

        // Toca o som em loop do tick respeitando GlobalAudioManager
        if (tickSound != null && audioSource != null)
        {
            float effVol = tickSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            if (effVol > 0.001f)
            {
                if (audioSource.outputAudioMixerGroup == null && GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                    audioSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;

                audioSource.clip = tickSound;
                audioSource.volume = effVol;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        if (bombaRenderer != null)
        {
            instanceBombMat = bombaRenderer.material;
            Color orig = instanceBombMat.HasProperty("_BaseColor")
                ? instanceBombMat.GetColor("_BaseColor")
                : (instanceBombMat.HasProperty("_Color") ? instanceBombMat.GetColor("_Color") : Color.white);
            StartCoroutine(CountdownVisual(orig));
        }

        Invoke(nameof(Explodir), tempoParaExplodir);
    }

    public void SetLandingTelegraph(Vector3 landingGroundPos, float flightDuration, Color? overrideColor = null)
    {
        if (activeLandingTelegraph != null)
            Destroy(activeLandingTelegraph);

        Color col = overrideColor ?? corExplosao;
        GolemGroundTelegraph telegraph = GolemGroundTelegraph.Spawn(
            landingGroundPos,
            raioExplosao,
            Mathf.Max(0.20f, flightDuration),
            col,
            telegraphMaterial
        );
        activeLandingTelegraph = telegraph.gameObject;
    }

    private void SetupFuseTrail()
    {
        TrailRenderer tr = gameObject.GetComponent<TrailRenderer>();
        if (tr == null) tr = gameObject.AddComponent<TrailRenderer>();

        tr.time = 0.24f;
        tr.startWidth = 0.14f;
        tr.endWidth = 0.01f;
        tr.minVertexDistance = 0.06f;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");
        fuseTrailMat = new Material(sh);
        if (fuseTrailMat.HasProperty("_Surface")) fuseTrailMat.SetFloat("_Surface", 1f);
        if (fuseTrailMat.HasProperty("_ZWrite")) fuseTrailMat.SetFloat("_ZWrite", 0f);
        if (fuseTrailMat.HasProperty("_Blend")) fuseTrailMat.SetFloat("_Blend", 2f);
        fuseTrailMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fuseTrailMat.renderQueue = 3010;
        if (fuseTrailMat.HasProperty("_BaseColor")) fuseTrailMat.SetColor("_BaseColor", corExplosao);
        if (fuseTrailMat.HasProperty("_Color")) fuseTrailMat.SetColor("_Color", corExplosao);
        if (fuseTrailMat.HasProperty("_SrcBlend") && fuseTrailMat.HasProperty("_DstBlend"))
        {
            fuseTrailMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fuseTrailMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        tr.sharedMaterial = fuseTrailMat;

        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(corExplosao, Color.white, 0.35f), 0f), new GradientColorKey(corExplosao, 1f) },
            new[] { new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        tr.colorGradient = g;
    }

    void OnCollisionEnter(Collision col)
    {
        if (explodiu) return;
        if (!IsFriendly && (col.gameObject.CompareTag("Enemy") || col.gameObject.layer == 6)) return;
        Explodir();
    }

    void OnDestroy()
    {
        if (activeLandingTelegraph != null)
            Destroy(activeLandingTelegraph);
        if (fuseTrailMat != null)
            Destroy(fuseTrailMat);
    }

    void Explodir()
    {
        if (explodiu) return;
        explodiu = true;
        CancelInvoke();

        if (activeLandingTelegraph != null)
        {
            Destroy(activeLandingTelegraph);
            activeLandingTelegraph = null;
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        // Som 3D da explosão respeitando GlobalAudioManager
        if (explosionSound != null)
        {
            float effVol = explosionSoundVolume * GlobalAudioManager.SFXVolumeMultiplier;
            if (effVol > 0.001f)
            {
                GameObject audioObj = new GameObject("TempExplosionAudio");
                audioObj.transform.position = transform.position;
                AudioSource aSource = audioObj.AddComponent<AudioSource>();
                aSource.clip = explosionSound;
                aSource.volume = effVol;
                aSource.pitch = Random.Range(0.92f, 1.08f);
                aSource.spatialBlend = 1f;
                aSource.minDistance = 5f;
                aSource.maxDistance = 45f;

                if (audioSource != null && audioSource.outputAudioMixerGroup != null)
                    aSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
                else if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                    aSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;

                aSource.Play();
                Destroy(audioObj, explosionSound.length + 0.1f);
            }
        }

        // Esconde e congela a bomba imediatamente
        if (bombaRenderer != null) bombaRenderer.enabled = false;
        Rigidbody rbBomba = GetComponent<Rigidbody>();
        if (rbBomba != null) { rbBomba.linearVelocity = Vector3.zero; rbBomba.isKinematic = true; }
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Vector3 vfxPos = transform.position + Vector3.up * 0.10f;
        float radiusScale = Mathf.Clamp(raioExplosao / 2.2f, 0.70f, 2.2f);

        // 1. VFX principal da explosão (CFXR Explosion 1)
        if (efeitoExplosao != null)
        {
            GameObject fx = Instantiate(efeitoExplosao, vfxPos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * (0.95f * radiusScale);
            Destroy(fx, 2.4f);
        }

        // 2. Onda de impacto estilizada no chão (CFXR2 Ground Hit)
        if (groundHitVfxPrefab != null)
        {
            GameObject ring = Instantiate(groundHitVfxPrefab, vfxPos, Quaternion.identity);
            ring.transform.localScale = Vector3.one * (1.45f * radiusScale);
            Destroy(ring, 1.6f);
        }

        // 3. Breve flash de luz no ponto da explosão
        SpawnBriefExplosionLight(vfxPos, raioExplosao, corExplosao);

        AplicarDano();
        Destroy(gameObject);
    }

    private static void SpawnBriefExplosionLight(Vector3 pos, float radius, Color color)
    {
        GameObject lightHost = new GameObject("BombExplosionFlash");
        lightHost.transform.position = pos + Vector3.up * 0.5f;
        BombFlashLight flash = lightHost.AddComponent<BombFlashLight>();
        flash.Init(color, radius * 2.6f, 5.5f, 0.22f);
    }

    void AplicarDano()
    {
        Vector3 flatCenter = new Vector3(transform.position.x, 0f, transform.position.z);
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, raioExplosao + 1.2f);
        var damagedEnemies = IsFriendly ? new System.Collections.Generic.HashSet<DummyHealth>() : null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (IsFriendly)
            {
                DummyHealth enemy = hit.GetComponentInParent<DummyHealth>();
                if (enemy != null && damagedEnemies.Add(enemy))
                {
                    Vector3 flatEnemy = new Vector3(enemy.transform.position.x, 0f, enemy.transform.position.z);
                    if (Vector3.Distance(flatCenter, flatEnemy) <= raioExplosao + 0.6f)
                        enemy.TakeDamage(danoExplosao, isProjectile: true);
                }
                continue;
            }

            if (!hit.CompareTag("Player")) continue;

            Vector3 flatTarget = new Vector3(hit.transform.position.x, 0f, hit.transform.position.z);
            if (Vector3.Distance(flatCenter, flatTarget) > raioExplosao) continue;

            PlayerHealth ph = hit.GetComponent<PlayerHealth>() ?? hit.GetComponentInParent<PlayerHealth>();
            if (ph == null) continue;

            if (marksOnHit)
            {
                if (BombMarkSystem.Instance != null)
                    BombMarkSystem.Instance.OnBombHit(hit.gameObject);
                else
                    ph.TakeDamage(danoExplosao, owner);
            }
            else
            {
                ph.TakeDamage(danoExplosao, owner);
            }

            // Knockback apenas no plano XZ
            Rigidbody rbPlayer = hit.GetComponent<Rigidbody>() ?? hit.GetComponentInParent<Rigidbody>();
            if (rbPlayer != null)
            {
                Vector3 dir = hit.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
                rbPlayer.AddForce(dir.normalized * forcaKnockback, ForceMode.Impulse);
            }
            break;
        }
    }

    IEnumerator CountdownVisual(Color corOriginal)
    {
        float timer = 0f;
        while (!explodiu && timer < tempoParaExplodir)
        {
            timer += Time.deltaTime;
            float vel = Mathf.Lerp(8f, 24f, timer / Mathf.Max(0.2f, tempoParaExplodir));
            float pulso = Mathf.Abs(Mathf.Sin(timer * vel));
            if (instanceBombMat != null)
            {
                Color c = Color.Lerp(corOriginal, new Color(1f, 0.25f, 0.1f, 1f), pulso * 0.75f);
                if (instanceBombMat.HasProperty("_BaseColor")) instanceBombMat.SetColor("_BaseColor", c);
                if (instanceBombMat.HasProperty("_Color")) instanceBombMat.SetColor("_Color", c);
            }
            yield return null;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, raioExplosao);
    }
}

public class BombFlashLight : MonoBehaviour
{
    private Light flashLight;
    private float startIntensity;
    private float duration;
    private float elapsed;

    public void Init(Color color, float range, float intensity, float fadeDuration)
    {
        flashLight = gameObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = color;
        flashLight.range = range;
        flashLight.intensity = intensity;
        flashLight.shadows = LightShadows.None;

        startIntensity = intensity;
        duration = Mathf.Max(0.05f, fadeDuration);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        if (flashLight != null)
            flashLight.intensity = startIntensity * (1f - t) * (1f - t);

        if (elapsed >= duration)
            Destroy(gameObject);
    }
}
