using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Coloque este script em cada círculo (nó) dentro do Content do ScrollRect.
/// Ele lê os dados do ScriptableObject SynergyNodeData já existente no projeto
/// e dispara o Tooltip ao receber hover do mouse.
/// </summary>
[RequireComponent(typeof(Image))]
public class SynergyMapNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ── Dados ──────────────────────────────────────────────────

    [Header("Dados do Nó")]
    [Tooltip("Arraste aqui o ScriptableObject SynergyNodeData correspondente.")]
    [SerializeField] private SynergyNodeData nodeData;

    [Header("Overrides Visuais (Inspector)")]
    [Tooltip("Cor do círculo. Mude livremente pelo Inspector.")]
    [SerializeField] private Color nodeColor = Color.white;

    [Tooltip("Sprite opcional para trocar o ícone do nó.")]
    [SerializeField] private Sprite iconOverride;

    // ── Cache ──────────────────────────────────────────────────

    private Image _image;

    // ── Propriedades Públicas ──────────────────────────────────

    /// <summary>Título exibido no tooltip — vem do SO.</summary>
    public string Title => nodeData != null ? nodeData.DisplayName : "Sem Nome";

    /// <summary>Descrição exibida no tooltip — monta a partir do SO.</summary>
    public string Description
    {
        get
        {
            if (nodeData == null) return "Sem dados atribuídos.";

            // Monta uma descrição útil com o tipo + modificadores
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Tipo: {nodeData.NodeType}");

            if (nodeData.StatModifiers != null && nodeData.StatModifiers.Count > 0)
            {
                sb.AppendLine("Modificadores:");
                foreach (var mod in nodeData.StatModifiers)
                {
                    if (mod.baseValue != 0f)
                        sb.AppendLine($"  {mod.statName}: +{mod.baseValue}");
                    if (mod.multiplierValue != 0f)
                        sb.AppendLine($"  {mod.statName}: +{mod.multiplierValue * 100f:F0}%");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }

    // ── Lifecycle ──────────────────────────────────────────────

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void OnValidate()
    {
        // Aplica a cor e o sprite sempre que algo mudar no Inspector
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.color = nodeColor;
            if (iconOverride != null)
                img.sprite = iconOverride;
        }
    }

    // ── Pointer Events ────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        SynergyTooltip.Instance?.Show(Title, Description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SynergyTooltip.Instance?.Hide();
    }
}
