using UnityEngine;
using System.Collections;

/// <summary>
/// Bomba do Goblin.
/// - Explode ao tocar chão/sólidos (ignora Player no ar)
/// - Efeitos (anéis + fumaça) rodam em objeto independente: não bloqueiam destroy
/// </summary>
public class BombaExplosiva : MonoBehaviour
{
    [Header("Dano")]
    public int   danoExplosao   = 30;
    [Tooltip("Raio base sem buff. O Crystal Tuner pode aumentar via SetExplosionRadius().")]
    public float raioExplosao   = 2f;
    public float forcaKnockback = 10f;

    [Header("Timer")]
    public float tempoParaExplodir = 3f;

    [Header("Visual")]
    public GameObject efeitoExplosao;
    public Color corExplosao = new Color(1f, 0.45f, 0f, 1f);

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

    [HideInInspector] public GameObject owner;

    private bool explodiu = false;
    private AudioSource tickAudioSource;
    private AudioSource audioSource;

    // ─────────────────────────────────────────────────────────────────
    void Start()
        {
            audioSource = GetComponent<AudioSource>();

            // Ignora colisão com o Player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Collider cPlayer = player.GetComponent<Collider>();
                Collider cBomba  = GetComponent<Collider>();
                if (cPlayer != null && cBomba != null)
                    Physics.IgnoreCollision(cBomba, cPlayer);
            }

            // Toca o som em loop do tick usando o Audio Source oficial da bomba
            if (tickSound != null && audioSource != null)
            {
                audioSource.clip = tickSound;
                audioSource.volume = tickSoundVolume;
                audioSource.loop = true;
                audioSource.Play();
            }

            if (bombaRenderer != null)
                StartCoroutine(CountdownVisual(bombaRenderer.material.color));

            Invoke(nameof(Explodir), tempoParaExplodir);
        }
    void OnCollisionEnter(Collision col)
    {
        if (explodiu) return;
        if (col.gameObject.CompareTag("Enemy")) return;
        Explodir();
    }

    // ─────────────────────────────────────────────────────────────────
void Explodir()
    {
        if (explodiu) return;
        explodiu = true;
        CancelInvoke();

        // Para o som de tick
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Toca o som de explosão no local
        if (explosionSound != null)
        {
            GameObject audioObj = new GameObject("TempExplosionAudio");
            audioObj.transform.position = transform.position;
            AudioSource aSource = audioObj.AddComponent<AudioSource>();
            aSource.clip = explosionSound;
            aSource.volume = explosionSoundVolume;
            aSource.spatialBlend = 1f; // Som 3D
            aSource.minDistance = 5f;
            aSource.maxDistance = 45f;

            // === O PULO DO GATO ESTÁ AQUI ===
            // Passa a rota da Mesa de Som (SFX) da bomba para o áudio fantasma!
            if (audioSource != null)
            {
                aSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            }

            aSource.Play();
            Destroy(audioObj, explosionSound.length + 0.1f);
        }

        // Para o movimento e esconde a bomba imediatamente
        // ... (O restante da função Explodir continua igual daqui para baixo)

        // Para o movimento e esconde a bomba imediatamente
        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        Rigidbody rbBomba = GetComponent<Rigidbody>();
        if (rbBomba != null) { rbBomba.linearVelocity = Vector3.zero; rbBomba.isKinematic = true; }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (efeitoExplosao != null)
            Instantiate(efeitoExplosao, transform.position, Quaternion.identity);

        // Cria objeto FX independente para os efeitos procedurais
        // Ele não é filho da bomba: vive após Destroy(gameObject)
        GameObject fxHost = new GameObject("BombFX");
        fxHost.transform.position = transform.position;
        BombFXRunner fx = fxHost.AddComponent<BombFXRunner>();
        fx.Run(raioExplosao, corExplosao);

        AplicarDano();
        Destroy(gameObject);
    }

    void AplicarDano()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, raioExplosao);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph == null) continue;

            ph.TakeDamage(danoExplosao, owner);

            // Knockback apenas no plano XZ
            Rigidbody rbPlayer = hit.GetComponent<Rigidbody>();
            if (rbPlayer != null)
            {
                Vector3 dir = hit.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
                rbPlayer.AddForce(dir.normalized * forcaKnockback, ForceMode.Impulse);
            }
        }
    }

    // ── Countdown pisca vermelho acelerando ──────────────────────────
    IEnumerator CountdownVisual(Color corOriginal)
    {
        float piscarDuracao = tempoParaExplodir * 0.5f;
        yield return new WaitForSeconds(tempoParaExplodir - piscarDuracao);

        float timer = 0f;
        while (!explodiu && timer < piscarDuracao)
        {
            timer += Time.deltaTime;
            float vel  = Mathf.Lerp(4f, 20f, timer / piscarDuracao);
            float pulso = Mathf.Sin(timer * vel);
            if (bombaRenderer != null)
                bombaRenderer.material.color = Color.Lerp(corOriginal, Color.red, Mathf.Abs(pulso));
            yield return null;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, raioExplosao);
    }
}

// ═══════════════════════════════════════════════════════════════════
/// Componente auxiliar que roda os efeitos visuais da explosão
/// de forma completamente independente da bomba.
/// Auto-destrói o próprio GameObject ao terminar.
// ═══════════════════════════════════════════════════════════════════
public class BombFXRunner : MonoBehaviour
{
    private float raio;
    private Color cor;
    private int pendentes = 0;

    public void Run(float raioExplosao, Color corExplosao)
    {
        raio = raioExplosao;
        cor  = corExplosao;

        StartCoroutine(AnimarAnel(0f,    raio));
        StartCoroutine(AnimarAnel(0.05f, raio * 1.3f));
        StartCoroutine(AnimarAnel(0.10f, raio * 1.6f));
        StartCoroutine(FlashLuz());
        StartCoroutine(Fumaca());
    }

    // ── Anel de choque rápido ────────────────────────────────────────
    IEnumerator AnimarAnel(float delay, float raioFinal)
    {
        pendentes++;
        yield return new WaitForSeconds(delay);

        GameObject obj = new GameObject("ShockRing");
        obj.transform.position = transform.position + Vector3.up * 0.05f;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.loop = true; lr.useWorldSpace = false;
        lr.positionCount = 49; lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        float duracao = 0.25f, timer = 0f;
        while (timer < duracao)
        {
            timer += Time.deltaTime;
            float t     = timer / duracao;
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float r     = Mathf.Lerp(0.05f, raioFinal, eased);
            float alpha = 1f - Mathf.Pow(t, 1.8f);
            float w     = Mathf.Lerp(0.2f, 0.01f, t);
            Color c = Color.Lerp(Color.white, cor, t); c.a = alpha;
            lr.material.color = c; lr.startWidth = w; lr.endWidth = w;
            for (int i = 0; i <= 48; i++)
            {
                float ang = (float)i / 48 * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
            yield return null;
        }
        Destroy(obj);
        pendentes--;
        TryDestroySelf();
    }

    // ── Fumaça: puffs que sobem e desvanecem ─────────────────────────
    IEnumerator Fumaca()
    {
        pendentes++;
        for (int i = 0; i < 8; i++)
        {
            StartCoroutine(PuffFumaca(Random.Range(0.05f, raio * 0.5f)));
            yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
        }
        pendentes--;
        TryDestroySelf();
    }

    IEnumerator PuffFumaca(float raioBase)
    {
        pendentes++;
        GameObject obj = new GameObject("Smoke");
        // SEM SetParent — independente do FX host
        Vector2 rand2D = Random.insideUnitCircle * raioBase;
        obj.transform.position = transform.position + new Vector3(rand2D.x, 0.05f, rand2D.y);

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.loop = true; lr.useWorldSpace = false;
        lr.positionCount = 25; lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        float duracao = Random.Range(0.7f, 1.2f);
        float raioFin = Random.Range(0.3f, 1.0f);
        float subida  = Random.Range(1.0f, 2.0f);
        float timer   = 0f;

        while (timer < duracao)
        {
            timer += Time.deltaTime;
            float t = timer / duracao;
            obj.transform.position += Vector3.up * subida * Time.deltaTime;
            float r     = Mathf.Lerp(0.05f, raioFin, t);
            float alpha = Mathf.Lerp(0.55f, 0f, t);
            float w     = Mathf.Lerp(0.1f, 0.01f, t);
            Color c = Color.Lerp(new Color(0.2f, 0.2f, 0.2f), new Color(0.7f, 0.7f, 0.7f), t);
            c.a = alpha; lr.material.color = c; lr.startWidth = w; lr.endWidth = w;
            for (int i = 0; i <= 24; i++)
            {
                float ang = (float)i / 24 * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
            yield return null;
        }
        Destroy(obj);
        pendentes--;
        TryDestroySelf();
    }

    // ── Flash de luz ─────────────────────────────────────────────────
    IEnumerator FlashLuz()
    {
        pendentes++;
        GameObject lightObj = new GameObject("ExplosionFlash");
        lightObj.transform.position = transform.position;
        Light luz = lightObj.AddComponent<Light>();
        luz.color = cor; luz.intensity = 8f; luz.range = raio * 3f;

        float duracao = 0.25f, t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            luz.intensity = Mathf.Lerp(8f, 0f, t / duracao);
            yield return null;
        }
        Destroy(lightObj);
        pendentes--;
        TryDestroySelf();
    }

    // Auto-destrói quando todos os efeitos terminarem
    void TryDestroySelf()
    {
        if (pendentes <= 0)
            Destroy(gameObject);
    }
}
