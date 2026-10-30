using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Componente visual de um slot individual de receita na UI de crafting.
/// Exibe nome da receita e indicador de disponibilidade / status.
/// Detecta clique para selecionar a receita.
/// </summary>
public class RecipeSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Image backgroundImage;
    private Outline slotOutline;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI statusText;

    private CraftingRecipe currentRecipe;
    private bool canCraft = false;
    private bool isSelected = false;
    private bool isHovered = false;
    private bool isFaded = false;
    private bool isAlreadyBuilt = false;

    // Callback de seleção
    private System.Action<CraftingRecipe> onSelected;

    // Cores
    private static readonly Color SLOT_BG = new Color(0.10f, 0.14f, 0.17f, 1f);
    private static readonly Color SLOT_HOVER = new Color(0.14f, 0.22f, 0.24f, 1f);
    private static readonly Color SLOT_SELECTED = new Color(0.12f, 0.31f, 0.31f, 1f);
    private static readonly Color BORDER_DEFAULT = new Color(0.25f, 0.35f, 0.40f, 0.85f);
    private static readonly Color BORDER_CAN_CRAFT = new Color(0.32f, 0.82f, 0.75f, 0.95f);
    private static readonly Color BORDER_CANT_CRAFT = new Color(0.72f, 0.36f, 0.34f, 0.85f);
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
        backgroundImage = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        backgroundImage.color = SLOT_BG;
        backgroundImage.raycastTarget = true;

        // Garante dimensão e LayoutElement para respeitar a altura na lista
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(slotWidth, slotHeight);

        LayoutElement le = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        le.minHeight = slotHeight;
        le.preferredHeight = slotHeight;
        le.flexibleHeight = 0;

        // Borda elegante via Outline (nunca cobre o texto nem preenche o interior)
        slotOutline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        slotOutline.effectColor = BORDER_DEFAULT;
        slotOutline.effectDistance = new Vector2(2f, -2f);

        // Nome da receita
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(transform, false);
        nameObj.layer = layer;
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.42f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(16f, 0f);
        nameRect.offsetMax = new Vector2(-16f, -6f);

        nameObj.AddComponent<CanvasRenderer>();
        nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 20f;
        nameText.fontSizeMin = 12f;
        nameText.fontSizeMax = 22f;
        nameText.enableAutoSizing = true;
        nameText.fontStyle = FontStyles.Normal;
        nameText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.raycastTarget = false;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        // Status
        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(transform, false);
        statusObj.layer = layer;
        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0.42f);
        statusRect.offsetMin = new Vector2(16f, 6f);
        statusRect.offsetMax = new Vector2(-16f, 0f);

        statusObj.AddComponent<CanvasRenderer>();
        statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 15f;
        statusText.fontSizeMin = 10f;
        statusText.fontSizeMax = 17f;
        statusText.enableAutoSizing = true;
        statusText.alignment = TextAlignmentOptions.MidlineLeft;
        statusText.raycastTarget = false;

        TMP_FontAsset customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (customFont != null)
        {
            nameText.font = customFont;
            statusText.font = customFont;
        }
    }

    /// <summary>
    /// Configuração avançada de exibição para a aba de armas (suporta cards opacos, já forjados ou apagados/classificados).
    /// </summary>
    public void SetWeaponSlot(CraftingRecipe recipe, string displayName, string customStatus, bool alreadyBuilt, bool craftable, bool faded)
    {
        currentRecipe = recipe;
        canCraft = craftable;
        isAlreadyBuilt = alreadyBuilt;
        isFaded = faded;

        if (nameText != null)
        {
            nameText.text = displayName;
            if (faded)
            {
                nameText.color = new Color(0.48f, 0.52f, 0.58f, 0.40f);
            }
            else if (alreadyBuilt)
            {
                nameText.color = new Color(0.85f, 0.98f, 0.92f, 1f);
            }
            else
            {
                nameText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
            }
        }

        if (statusText != null)
        {
            statusText.text = customStatus;
            if (faded)
            {
                statusText.color = new Color(0.38f, 0.42f, 0.48f, 0.40f);
            }
            else if (alreadyBuilt)
            {
                statusText.color = new Color(0.0f, 0.95f, 0.65f, 1f);
            }
            else if (craftable)
            {
                statusText.color = TEXT_CAN_CRAFT;
            }
            else
            {
                statusText.color = TEXT_CANT_CRAFT;
            }
        }

        UpdateVisual();
    }

    /// <summary>
    /// Atualiza o slot com os dados de uma receita.
    /// </summary>
    public void SetRecipe(CraftingRecipe recipe, bool craftable)
    {
        currentRecipe = recipe;
        canCraft = craftable;
        isFaded = false;
        isAlreadyBuilt = false;

        if (recipe == null)
        {
            if (nameText != null) nameText.text = "Item desconhecido";
            if (statusText != null) statusText.text = "";
            UpdateVisual();
            return;
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(recipe.recipeName) ? "Item sem nome" : recipe.recipeName;
            nameText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
        }

        if (statusText != null)
        {
            if (craftable)
            {
                statusText.text = "Materiais disponíveis";
                statusText.color = TEXT_CAN_CRAFT;
            }
            else
            {
                statusText.text = "Materiais insuficientes";
                statusText.color = TEXT_CANT_CRAFT;
            }
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
        if (backgroundImage == null) return;

        if (isFaded)
        {
            backgroundImage.color = new Color(0.06f, 0.08f, 0.10f, 0.45f);
            if (slotOutline != null)
            {
                slotOutline.effectColor = isSelected ? new Color(0.4f, 0.45f, 0.5f, 0.4f) : new Color(0.2f, 0.24f, 0.28f, 0.35f);
            }
            return;
        }

        if (isSelected)
        {
            backgroundImage.color = SLOT_SELECTED;
            if (slotOutline != null)
            {
                slotOutline.effectColor = isAlreadyBuilt ? new Color(0.0f, 0.95f, 0.65f, 0.95f) : (canCraft ? BORDER_CAN_CRAFT : BORDER_CANT_CRAFT);
            }
        }
        else if (isHovered)
        {
            backgroundImage.color = SLOT_HOVER;
            if (slotOutline != null)
            {
                slotOutline.effectColor = isAlreadyBuilt ? new Color(0.2f, 0.75f, 0.55f, 0.85f) : BORDER_DEFAULT;
            }
        }
        else
        {
            backgroundImage.color = SLOT_BG;
            if (slotOutline != null)
            {
                slotOutline.effectColor = isAlreadyBuilt ? new Color(0.15f, 0.55f, 0.45f, 0.65f) : BORDER_DEFAULT;
            }
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
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onSelected?.Invoke(currentRecipe);
        }
    }
}
