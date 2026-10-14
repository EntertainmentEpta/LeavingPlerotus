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

    /// <summary>
    /// Referência ao GameObject dono (Peixe que disparou).
    /// Atribuída pelo PeixeAI no momento do Instantiate.
    /// </summary>
    [HideInInspector] public GameObject owner;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
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
        // Ignora colisões com inimigos
        if (other.CompareTag("Enemy")) return;

        // Ignora outros Espinhos (os 3 tiros do spread nascem sobrepostos)
        if (other.GetComponentInParent<Espinho>() != null) return;

        // Ignora colisão com o próprio dono (Peixe que atirou)
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            return;

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

        // Colidiu com parede ou outro obstáculo — destrói o projétil
        Destroy(gameObject);
    }
}
