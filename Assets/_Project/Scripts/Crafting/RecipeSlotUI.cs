using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Componente visual de um slot individual de receita na UI de crafting.
/// Exibe ícone, nome da receita e indicador de disponibilidade (verde/vermelho).
/// Detecta clique para selecionar a receita.
/// </summary>
public class RecipeSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Image backgroundImage;
    private Image borderImage;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI statusText;

    private CraftingRecipe currentRecipe;
    private bool canCraft = false;
    private bool isSelected = false;
    private bool isHovered = false;

    // Callback de seleção
    private System.Action<CraftingRecipe> onSelected;

    // Cores
    private static readonly Color SLOT_BG = new Color(0.10f, 0.14f, 0.17f, 1f);
    private static readonly Color SLOT_HOVER = new Color(0.14f, 0.22f, 0.24f, 1f);
    private static readonly Color SLOT_SELECTED = new Color(0.12f, 0.31f, 0.31f, 1f);
    private static readonly Color BORDER_DEFAULT = new Color(0.35f, 0.43f, 0.45f, 0.45f);
    private static readonly Color BORDER_CAN_CRAFT = new Color(0.32f, 0.82f, 0.75f, 0.9f);
    private static readonly Color BORDER_CANT_CRAFT = new Color(0.72f, 0.36f, 0.34f, 0.65f);
    private static readonly Color TEXT_CAN_CRAFT = new Color(0.42f, 0.88f, 0.75f, 1f);
    private static readonly Color TEXT_CANT_CRAFT = new Color(0.92f, 0.48f, 0.45f, 1f);

    /// <summary>
    /// Inicializa o slot criando todos os elementos visuais.
    /// </summary>
    public void Initialize(System.Action<CraftingRecipe> selectionCallback, float slotWidth, float slotHeight)
    {
        onSelected = selectionCallback;
        int layer = gameObject.layer;

        // Background
        backgroundImage = gameObject.AddComponent<Image>();
        backgroundImage.color = SLOT_BG;
        backgroundImage.raycastTarget = true;

        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(slotWidth, slotHeight);

        // Borda
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(transform, false);
        borderObj.layer = layer;
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderObj.AddComponent<CanvasRenderer>();
        borderImage = borderObj.AddComponent<Image>();
        borderImage.color = BORDER_DEFAULT;
        borderImage.raycastTarget = false;
        borderImage.type = Image.Type.Sliced;
        borderImage.fillCenter = false;

        // Nome da receita
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(transform, false);
        nameObj.layer = layer;
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.04f, 0.35f);
        nameRect.anchorMax = new Vector2(0.96f, 0.95f);
        nameRect.sizeDelta = Vector2.zero;
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        nameObj.AddComponent<CanvasRenderer>();
        nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 22f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(0.9f, 0.88f, 0.95f, 1f);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.raycastTarget = false;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        // Status (disponível/indisponível)
        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(transform, false);
        statusObj.layer = layer;
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0.05f);
        statusRect.anchorMax = new Vector2(1f, 0.40f);
        statusRect.sizeDelta = Vector2.zero;
        statusRect.offsetMin = new Vector2(16f, 0f);
        statusRect.offsetMax = new Vector2(-16f, 0f);
        statusObj.AddComponent<CanvasRenderer>();
        statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 15f;
        statusText.alignment = TextAlignmentOptions.MidlineLeft;
        statusText.raycastTarget = false;

        // Carrega e aplica a fonte Oswald Bold SDF consistente
        TMP_FontAsset customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (customFont != null)
        {
            nameText.font = customFont;
            statusText.font = customFont;
        }
    }

    /// <summary>
    /// Atualiza o slot com os dados de uma receita.
    /// </summary>
    public void SetRecipe(CraftingRecipe recipe, bool craftable)
    {
        currentRecipe = recipe;
        canCraft = craftable;

        if (recipe == null)
        {
            nameText.text = "noname";
            statusText.text = "";
            borderImage.color = BORDER_DEFAULT;
            return;
        }

        nameText.text = string.IsNullOrWhiteSpace(recipe.recipeName) ? "noname" : recipe.recipeName;

        if (craftable)
        {
            statusText.text = "Materiais disponíveis";
            statusText.color = TEXT_CAN_CRAFT;
            borderImage.color = isSelected ? BORDER_CAN_CRAFT : BORDER_DEFAULT;
        }
        else
        {
            statusText.text = "Materiais insuficientes";
            statusText.color = TEXT_CANT_CRAFT;
            borderImage.color = isSelected ? BORDER_CANT_CRAFT : BORDER_DEFAULT;
        }

        UpdateVisual();
    }

    /// <summary>
    /// Marca/desmarca este slot como selecionado.
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (isSelected)
        {
            backgroundImage.color = SLOT_SELECTED;
            borderImage.color = canCraft ? BORDER_CAN_CRAFT : BORDER_CANT_CRAFT;
        }
        else if (isHovered)
        {
            backgroundImage.color = SLOT_HOVER;
        }
        else
        {
            backgroundImage.color = SLOT_BG;
            borderImage.color = BORDER_DEFAULT;
        }
    }

    // ─── EVENTOS DE MOUSE ────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        UpdateVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        UpdateVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentRecipe != null)
            onSelected?.Invoke(currentRecipe);
    }
}
