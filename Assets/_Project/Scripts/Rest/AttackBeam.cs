using UnityEngine;

public class AttackBeam : MonoBehaviour
{
    [Header("Configurações do Raio")]
    public int damage = 25;
    public float radius = 2f;
    
    [Tooltip("Tempo em segundos que o efeito visual do raio fica na tela antes de desaparecer.")]
    public float lifetime = 0.5f;

    void Start()
    {
        // Detecta todos os colliders em um raio a partir da posição do raio
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in hits)
        {
            // Se encontrar o jogador, aplica o dano
            if (hit.CompareTag("Player"))
            {
                hit.GetComponent<PlayerHealth>().TakeDamage(damage);
            }
        }
        
        // Adicionar um efeito visual de explosão/impacto aqui seria ótimo.
        
        // Destrói o objeto do raio após o tempo de vida definido.
        Destroy(gameObject, lifetime);
    }
}