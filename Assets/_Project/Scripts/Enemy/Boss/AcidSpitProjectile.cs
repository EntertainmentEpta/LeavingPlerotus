using UnityEngine;

/// <summary>
/// Projétil da cuspida ácida — arco parabólico, cria AcidPuddle ao aterrissar.
/// O prefab precisa ter: AcidSpitProjectile + Rigidbody + Collider (non-trigger).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AcidSpitProjectile : MonoBehaviour
{
    [Tooltip("Prefab da AcidPuddle instanciada ao aterrissar (fallback se não houver AcidPuddleSpawner na cena).")]
    public GameObject acidPuddlePrefab;

    [Tooltip("Segundos até auto-destruição caso nunca atinja o chão.")]
    public float lifetime = 6f;

    [Header("VFX")]
    [Tooltip("Particle System de rastro (opcional). Se nulo usa TrailRenderer verde como fallback.")]
    public GameObject trailVFXPrefab;

    private Rigidbody rb;
    private bool hasLanded = false;
    private float spawnTime;
    // Y do chão capturado no Launch — evita raycasts que acertam o player ou triggers da arena
    private float groundY;
    private GameObject spawnedTrail;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Garante collider sólido — sem ele OnCollisionEnter nunca dispara
        if (GetComponent<Collider>() == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.15f;
            sc.isTrigger = false;
        }

        if (trailVFXPrefab != null)
        {
            spawnedTrail = Instantiate(trailVFXPrefab, transform);
            spawnedTrail.transform.localPosition = Vector3.zero;
        }
        else
        {
            CreateFallbackTrail();
        }
    }

    private void CreateFallbackTrail()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.3f, 1f, 0.1f), 0f), new GradientColorKey(new Color(0.05f, 0.55f, 0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        trail.colorGradient = g;
    }

    private void DetachTrail()
    {
        if (spawnedTrail == null) return;
        spawnedTrail.transform.SetParent(null);
        ParticleSystem ps = spawnedTrail.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(spawnedTrail, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            Destroy(spawnedTrail, 1f);
        }
    }

    void Update()
    {
        // Raycast fallback: detecta o chão quando caindo
        if (hasLanded || rb == null || rb.linearVelocity.y >= 0f) return;
        if (Time.time - spawnTime < 0.15f) return;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.3f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag("Enemy") && !hit.collider.CompareTag("Player")
                && !hit.collider.transform.root.CompareTag("Enemy"))
            {
                hasLanded = true;
                SpawnPuddle(hit.point);
                Destroy(gameObject);
            }
        }
    }

    // Fallback para prefabs com collider trigger
    void OnTriggerEnter(Collider other)
    {
        if (hasLanded) return;
        if (Time.time - spawnTime < 0.15f) return;
        // Ignora o boss e todos os seus filhos
        if (other.CompareTag("Enemy") || other.transform.root.CompareTag("Enemy")) return;
        if (other.CompareTag("Player")) return;

        hasLanded = true;
        SpawnPuddle(transform.position);
        Destroy(gameObject);
    }

    /// <summary>
    /// Velocidade inicial calculada para aterrissar exatamente em targetWorldPos.
    /// </summary>
    public void Launch(Vector3 targetWorldPos, float arcHeight = 3f)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        spawnTime = Time.time;
        groundY = targetWorldPos.y;
        rb.linearVelocity = CalculateLaunchVelocity(transform.position, targetWorldPos, arcHeight);
        Destroy(gameObject, lifetime);
    }

    private Vector3 CalculateLaunchVelocity(Vector3 from, Vector3 to, float arcHeight)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        float peakY = Mathf.Max(from.y, to.y) + arcHeight;

        float vy = Mathf.Sqrt(2f * g * (peakY - from.y));
        float timeUp = vy / g;
        float timeDown = Mathf.Sqrt(2f * (peakY - to.y) / g);
        float totalTime = timeUp + timeDown;

        Vector3 horizontal = to - from;
        horizontal.y = 0f;
        float hSpeed = totalTime > 0.001f ? horizontal.magnitude / totalTime : 0f;

        return horizontal.normalized * hSpeed + Vector3.up * vy;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasLanded) return;
        if (Time.time - spawnTime < 0.15f) return;
        if (collision.gameObject.CompareTag("Enemy")) return;
        if (collision.gameObject.CompareTag("Player")) return;
        if (collision.transform.root.CompareTag("Enemy")) return;

        // Ignora paredes — só pousa em superfícies aproximadamente horizontais
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y < 0.5f) return;

        hasLanded = true;
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        SpawnPuddle(hitPoint);
        Destroy(gameObject);
    }

    private void SpawnPuddle(Vector3 pos)
    {
        DetachTrail();
        // Usa o Y do alvo capturado no Launch — playerTransform.position.y está sempre no chão
        pos.y = groundY + 0.02f;

        AcidPuddleSpawner spawner = FindFirstObjectByType<AcidPuddleSpawner>();
        if (spawner != null)
        {
            spawner.SpawnAtPosition(pos);
            return;
        }

        if (acidPuddlePrefab != null && AcidPuddle.ActiveCount < 6)
            Instantiate(acidPuddlePrefab, pos, Quaternion.identity);
    }
}
