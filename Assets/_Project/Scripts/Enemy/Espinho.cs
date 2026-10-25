using UnityEngine;

/// <summary>
/// Projétil "Espinho" disparado pelo inimigo Peixe.
/// Move-se para frente constantemente e causa dano ao jogador ao colidir.
/// Destrói-se ao atingir o jogador, paredes ou após o tempo de vida máximo.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Espinho : MonoBehaviour
{
    // ── Configuração ────────────────────────────────────────────────
    [Header("Projétil")]
    [Tooltip("Velocidade do projétil (unidades/segundo).")]
    public float speed = 14f;

    [Tooltip("Dano causado ao jogador ao colidir.")]
    public int damage = 10;

    [Tooltip("Tempo de vida máximo em segundos antes de se autodestruir.")]
    public float maxLifetime = 4f;

    [Header("Envenenamento (Poison)")]
    [Tooltip("Dano por tick do veneno (1 tick = 1 segundo).")]
    public int poisonDamagePerTick = 3;

    [Tooltip("Redução de velocidade durante o veneno (0.20 = 20%).")]
    public float poisonSlow = 0.20f;

    [Tooltip("Duração do envenenamento em segundos.")]
    public float poisonDuration = 4f;

    [Header("Orientação")]
    [Tooltip("Garante que o projétil viaje deitado horizontalmente alinhado à trajetória.")]
    public bool orientHorizontally = true;

    /// <summary>
    /// Referência ao GameObject dono (Peixe que disparou).
    /// Atribuída pelo PeixeAI no momento do Instantiate.
    /// </summary>
    [HideInInspector] public GameObject owner;
    private float spawnTime;

    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        spawnTime = Time.time;
        if (orientHorizontally)
        {
            // Alinha o CapsuleCollider ao eixo Z (horizontal na direção do voo)
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null && capsule.direction != 2)
            {
                capsule.direction = 2; // 2 = Z-Axis
            }

            // Se o MeshFilter estiver no root do projétil, move-o para um filho Visual
            // rotacionado 90° em X para que meshes de cilindro/cápsula fiquem horizontais
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null && transform.childCount == 0)
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(transform, false);
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                MeshFilter childMf = visual.AddComponent<MeshFilter>();
                childMf.sharedMesh = mf.sharedMesh;

                MeshRenderer mr = GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    MeshRenderer childMr = visual.AddComponent<MeshRenderer>();
                    childMr.sharedMaterials = mr.sharedMaterials;
                    childMr.shadowCastingMode = mr.shadowCastingMode;
                    childMr.receiveShadows = mr.receiveShadows;
                    Destroy(mr);
                }

                Destroy(mf);
            }
        }
    }

    void Start()
    {
        spawnTime = Time.time;
        // Agenda a destruição pelo tempo de vida máximo
        Destroy(gameObject, maxLifetime);
    }

    // ─────────────────────────────────────────────────────────────────
    void Update()
    {
        // Move o projétil para frente constantemente
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    // ── Colisão ─────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Ignora triggers que não sejam o jogador (evita sumir em RoomTriggerProxy, zonas de água, etc.)
        if (other.isTrigger && !other.CompareTag("Player") && other.GetComponentInParent<PlayerHealth>() == null)
            return;

        // Ignora colisões com inimigos (qualquer mob com DummyHealth ou tag Enemy)
        if (other.CompareTag("Enemy") || other.GetComponentInParent<DummyHealth>() != null) return;

        // Ignora outros Espinhos (os 3 tiros do spread nascem sobrepostos)
        if (other.GetComponentInParent<Espinho>() != null) return;

        // Ignora colisão com o próprio dono (Peixe que atirou) ou suas partes
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform) || other.transform.root == owner.transform.root))
            return;

        // Período de graça mínimo de spawn (0.05s) para não colidir com o piso no nascimento
        if (Time.time - spawnTime < 0.05f) return;

        // Verifica se atingiu o jogador
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>()
                                 ?? other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, owner != null ? owner : gameObject);

            // Aplica envenenamento (DoT + slow) no jogador
            PoisonedStatus.ApplyPoison(playerHealth.gameObject, poisonDamagePerTick, poisonSlow, poisonDuration);

            Destroy(gameObject);
            return;
        }

        // Colidiu com parede ou outro obstáculo sólido
        Destroy(gameObject);
    }
}
