using UnityEngine;

/// <summary>
/// Nó de minério fixo no mapa.
/// O Geobionte busca esses objetos para se fundir e se transformar no Bismutado.
/// Coloque este script em um GameObject com um Collider (para detecção de proximidade).
/// </summary>
public class OreNode : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Nome do minério (para identificação e expansão futura)")]
    public string oreName = "Crystal";

    [Tooltip("Valor de essência deste cristal (usado pelo Geobionte para calcular drop)")]
    public int oreValue = 50;

    [Tooltip("Se true, este minério já foi consumido por um Geobionte")]
    [HideInInspector] public bool isConsumed = false;

    [Header("Visual Placeholder")]
    [Tooltip("Cor do brilho emissivo do minério")]
    public Color emissionColor = new Color(0.5f, 0.8f, 1f, 1f); // Azul cristalino
    [Tooltip("Intensidade do brilho")]
    public float glowIntensity = 2f;

    private Renderer oreRenderer;
    private Material oreMaterial;

    void Start()
    {
        SetupVisual();
    }

    /// <summary>
    /// Configura o visual placeholder do minério apenas se não houver modelo/material texturizado.
    /// </summary>
    void SetupVisual()
    {
        oreRenderer = GetComponentInChildren<Renderer>();
        if (oreRenderer != null)
        {
            // Se o objeto já possui um material válido com textura ou shader configurado, NÃO sobrescreve!
            if (oreRenderer.sharedMaterial != null)
            {
                Material shared = oreRenderer.sharedMaterial;
                bool hasTexture = shared.mainTexture != null || 
                                  (shared.HasProperty("_BaseMap") && shared.GetTexture("_BaseMap") != null);
                bool isCustomNamed = !shared.name.ToLower().Contains("default") && 
                                     !shared.name.ToLower().Contains("internal");

                if (hasTexture || isCustomNamed)
                {
                    // Preserva o material autêntico do modelo 3D (ex: Cristal)
                    return;
                }
            }

            // Cria material emissivo apenas para placeholders sem material próprio
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            oreMaterial = new Material(shader);
            oreMaterial.color = emissionColor;
            oreMaterial.EnableKeyword("_EMISSION");
            oreMaterial.SetColor("_EmissionColor", emissionColor * glowIntensity);

            // URP usa _BaseColor ao invés de _Color
            if (oreMaterial.HasProperty("_BaseColor"))
                oreMaterial.SetColor("_BaseColor", emissionColor);

            oreRenderer.material = oreMaterial;
        }
    }

    /// <summary>
    /// Chamado pelo Geobionte quando ele absorve este minério.
    /// Marca como consumido e desativa visualmente.
    /// </summary>
    public void Consume()
    {
        if (isConsumed) return;

        isConsumed = true;
        Debug.Log("[ORE] " + oreName + " consumido por um Geobionte! Valor: " + oreValue);

        // Destrói o cristal — ele some da cena ao ser absorvido
        Destroy(gameObject);
    }

    /// <summary>
    /// Verifica se este minério está disponível para fusão.
    /// </summary>
    public bool IsAvailable()
    {
        return !isConsumed && gameObject.activeSelf;
    }

    void OnDestroy()
    {
        if (oreMaterial != null) Destroy(oreMaterial);
    }

    // Gizmo para visualização no Editor
    void OnDrawGizmos()
    {
        Gizmos.color = isConsumed ? Color.gray : new Color(0.5f, 0.8f, 1f, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.3f);

        if (!isConsumed)
        {
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
