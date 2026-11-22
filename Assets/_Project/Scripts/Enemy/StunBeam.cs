using UnityEngine;

/// <summary>
/// Área de efeito de stun - Usado pelo Golem
/// Aplica stun ao jogador que estiver dentro da área
/// </summary>
public class StunBeam : MonoBehaviour
{
    [Header("Configurações")]
    public float radius = 4f;
    public float stunDuration = 1.5f;

    [Header("Configurações de VFX")]
    [Tooltip("Cor principal do Stun VFX.")]
    public Color beamColor = Color.cyan;
    [Tooltip("Cor do flash inicial de luz.")]
    public Color flashColor = Color.white;
    [Tooltip("Espessura dos anéis de onda de choque.")]
    public float ringWidth = 0.5f;
    [Tooltip("Altura máxima do pilar de energia vertical.")]
    public float pillarHeight = 8f;
    [Tooltip("Duração do pilar vertical de energia.")]
    public float pillarDuration = 0.4f;
    [Tooltip("Quantidade de partículas emitidas no burst.")]
    public int particleCount = 30;

    private bool hasAppliedStun = false;
    private object stunSequence;

    // Somente o Golem Elite fornece a identidade dos tres impactos.
    public void Initialize(float newRadius, float newStunDuration, object sequence)
    {
        stunSequence = sequence;
        Initialize(newRadius, newStunDuration);
    }

    /// <summary>
    /// Inicializa o beam com valores do Golem
    /// </summary>
    public void Initialize(float newRadius, float newStunDuration)
    {
        radius = newRadius;
        stunDuration = newStunDuration;
        
        // Aplica o stun imediatamente
        ApplyStunToPlayersInArea();
    }

    void Start()
    {
        // Se não foi inicializado externamente, aplica com valores padrão
        if (!hasAppliedStun)
        {
            ApplyStunToPlayersInArea();
        }

        // Cria o efeito visual
        CreatePlaceholderVisual();
    }

    void ApplyStunToPlayersInArea()
    {
        hasAppliedStun = true;

        if (stunSequence != null)
        {
            ApplySequenceStunToPlayersInArea();
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.ApplyStun(stunDuration);
                    Debug.Log("StunBeam aplicou stun no player por " + stunDuration + "s!");
                }
            }
        }
    }

    void ApplySequenceStunToPlayersInArea()
    {
        // Mantem a esfera. Resolve colliders filhos e inclui triggers
        // sem depender da tag do filho nem da configuracao global.
        var affected = new System.Collections.Generic.HashSet<PlayerHealth>();
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || !affected.Add(playerHealth)) continue;
            if (playerHealth.TryApplySequenceStun(stunDuration, stunSequence))
                Debug.Log("Golem Elite: stun aplicado por " + stunDuration + "s.");
        }
    }

    void CreatePlaceholderVisual()
    {
        // Cria o VFX do stun e passa as configurações expostas no prefab
        GameObject vfxObj = new GameObject("GolemStunVFX");
        vfxObj.transform.position = transform.position;
        
        GolemStunVFX vfx = vfxObj.AddComponent<GolemStunVFX>();
        vfx.SetRadius(radius);
        vfx.stunColor = beamColor;
        vfx.flashColor = flashColor;
        vfx.ringWidth = ringWidth;
        vfx.pillarHeight = pillarHeight;
        vfx.pillarDuration = pillarDuration;
        vfx.particleCount = particleCount;
        
        // Autodestruction do StunBeam (VFX se destrói sozinho)
        Destroy(gameObject, 0.1f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
