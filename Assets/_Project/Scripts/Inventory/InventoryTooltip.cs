using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tooltip flutuante que exibe detalhes do item ao passar o mouse sobre um slot.
/// Criado e gerenciado pelo InventoryUI.
/// </summary>
public class InventoryTooltip : MonoBehaviour
{
    private RectTransform tooltipRect;
    private CanvasGroup canvasGroup;

    [Header("Componentes (Opcional - Prefab)")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tierText;
    public TextMeshProUGUI quantityText;
    public Image separatorLine;
    public Image tierIndicator;

    // Configurações
    private static readonly float TOOLTIP_WIDTH = 320f;
    private static readonly float PADDING = 14f;
    private static readonly Color BG_COLOR = new Color(0.08f, 0.08f, 0.12f, 0.95f);
    private static readonly Color BORDER_COLOR = new Color(0.4f, 0.4f, 0.5f, 0.7f);

    private Canvas parentCanvas;

    /// <summary>
    /// Cria e inicializa o tooltip
    /// </summary>
    public void Initialize(Canvas canvas)
    {
        parentCanvas = canvas;

        tooltipRect = GetComponent<RectTransform>();
        if (tooltipRect == null) tooltipRect = gameObject.AddComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // Se já tiver as referências do Prefab atribuídas, não cria nada por código!
        if (nameText != null)
        {
            return;
        }

        int uiLayer = gameObject.layer;
        canvasGroup.interactable = false;

        // === Background ===
        GameObject bgObj = new GameObject("TooltipBG");
        bgObj.transform.SetParent(transform, false);
        bgObj.layer = uiLayer;

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        bgObj.AddComponent<CanvasRenderer>();
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = BG_COLOR;
        bgImage.raycastTarget = false;

        // === Borda ===
        GameObject borderObj = new GameObject("TooltipBorder");
        borderObj.transform.SetParent(transform, false);
        borderObj.layer = uiLayer;

        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(4f, 4f);
        borderRect.anchoredPosition = Vector2.zero;

        borderObj.AddComponent<CanvasRenderer>();
        Image borderImage = borderObj.AddComponent<Image>();
        borderImage.color = BORDER_COLOR;
        borderImage.raycastTarget = false;
        borderImage.type = Image.Type.Sliced;
        borderImage.fillCenter = false;
        borderObj.transform.SetAsFirstSibling();

        // === Indicador de Tier (barra lateral esquerda fina) ===
        GameObject tierBarObj = new GameObject("TierIndicator");
        tierBarObj.transform.SetParent(transform, false);
        tierBarObj.layer = uiLayer;

        RectTransform tierBarRect = tierBarObj.AddComponent<RectTransform>();
        tierBarRect.anchorMin = new Vector2(0f, 0f);
        tierBarRect.anchorMax = new Vector2(0f, 1f);
        tierBarRect.pivot = new Vector2(0f, 0.5f);
        tierBarRect.anchoredPosition = new Vector2(0f, 0f);
        tierBarRect.sizeDelta = new Vector2(4f, 0f);

        tierBarObj.AddComponent<CanvasRenderer>();
        tierIndicator = tierBarObj.AddComponent<Image>();
        tierIndicator.color = Color.white;
        tierIndicator.raycastTarget = false;

        // === Nome do Item ===
        GameObject nameObj = new GameObject("ItemName");
        nameObj.transform.SetParent(transform, false);
        nameObj.layer = uiLayer;

        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(PADDING + 4f, -PADDING);
        nameRect.sizeDelta = new Vector2(-PADDING * 2 - 4f, 28f);

        nameObj.AddComponent<CanvasRenderer>();
        nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 16f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        nameText.raycastTarget = false;
        nameText.textWrappingMode = TextWrappingModes.Normal;

        // === Separador ===
        GameObject sepObj = new GameObject("Separator");
        sepObj.transform.SetParent(transform, false);
        sepObj.layer = uiLayer;

        RectTransform sepRect = sepObj.AddComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0f, 1f);
        sepRect.anchorMax = new Vector2(1f, 1f);
        sepRect.pivot = new Vector2(0.5f, 1f);
        sepRect.anchoredPosition = new Vector2(0f, -(PADDING + 30f));
        sepRect.sizeDelta = new Vector2(-PADDING * 2, 1f);

        sepObj.AddComponent<CanvasRenderer>();
        separatorLine = sepObj.AddComponent<Image>();
        separatorLine.color = new Color(0.5f, 0.5f, 0.6f, 0.4f);
        separatorLine.raycastTarget = false;

        // === Descrição ===
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(transform, false);
        descObj.layer = uiLayer;

        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.anchoredPosition = new Vector2(PADDING + 4f, -(PADDING + 36f));
        descRect.sizeDelta = new Vector2(-PADDING * 2 - 4f, 90f);

        descObj.AddComponent<CanvasRenderer>();
        descriptionText = descObj.AddComponent<TextMeshProUGUI>();
        descriptionText.fontSize = 13f;
        descriptionText.color = new Color(0.75f, 0.75f, 0.8f, 0.9f);
        descriptionText.alignment = TextAlignmentOptions.TopLeft;
        descriptionText.raycastTarget = false;
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.overflowMode = TextOverflowModes.Ellipsis;

        // === Tier + Quantidade (linha inferior) ===
        GameObject infoObj = new GameObject("InfoLine");
        infoObj.transform.SetParent(transform, false);
        infoObj.layer = uiLayer;

        RectTransform infoRect = infoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0f);
        infoRect.anchorMax = new Vector2(0.5f, 0f);
        infoRect.pivot = new Vector2(0f, 0f);
        infoRect.anchoredPosition = new Vector2(PADDING + 4f, PADDING);
        infoRect.sizeDelta = new Vector2(-PADDING, 20f);

        infoObj.AddComponent<CanvasRenderer>();
        tierText = infoObj.AddComponent<TextMeshProUGUI>();
        tierText.fontSize = 11f;
        tierText.fontStyle = FontStyles.Italic;
        tierText.alignment = TextAlignmentOptions.BottomLeft;
        tierText.raycastTarget = false;

        // === Quantidade (direita inferior) ===
        GameObject qtyObj = new GameObject("QuantityInfo");
        qtyObj.transform.SetParent(transform, false);
        qtyObj.layer = uiLayer;

        RectTransform qtyRect = qtyObj.AddComponent<RectTransform>();
        qtyRect.anchorMin = new Vector2(0.5f, 0f);
        qtyRect.anchorMax = new Vector2(1f, 0f);
        qtyRect.pivot = new Vector2(1f, 0f);
        qtyRect.anchoredPosition = new Vector2(-PADDING, PADDING);
        qtyRect.sizeDelta = new Vector2(-PADDING, 20f);

        qtyObj.AddComponent<CanvasRenderer>();
        quantityText = qtyObj.AddComponent<TextMeshProUGUI>();
        quantityText.fontSize = 11f;
        quantityText.color = new Color(0.7f, 0.7f, 0.75f, 0.9f);
        quantityText.alignment = TextAlignmentOptions.BottomRight;
        quantityText.raycastTarget = false;

        Hide();
    }

    /// <summary>
    /// Exibe o tooltip com os dados do item
    /// </summary>
    public void Show(ItemData itemData, int quantity, string extraNote = null)
    {
        if (itemData == null) return;

        // Nome com cor do Tier
        Color tierColor = itemData.GetTierColor();
        nameText.text = itemData.itemName;
        nameText.color = tierColor;

        // Descrição
        string desc = !string.IsNullOrEmpty(itemData.description)
            ? itemData.description
            : "Sem descrição disponível.";

        if (!string.IsNullOrEmpty(extraNote))
        {
            desc += "\n\n" + extraNote;
        }

        descriptionText.text = desc;

        // Tier
        tierText.text = itemData.GetTierName();
        tierText.color = tierColor;

        // Indicador lateral com cor do Tier
        tierIndicator.color = tierColor;

        // Quantidade
        quantityText.text = "Qtd: " + quantity;

        // Mostra — garante que nunca bloqueia raycasts (cliques nos slots)
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable   = false;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Esconde o tooltip
    /// </summary>
    public void Hide()
    {
        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        if (canvasGroup.alpha <= 0f) return;

        // Segue o mouse
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            Input.mousePosition,
            parentCanvas.worldCamera,
            out mousePos
        );

        // Offset para não cobrir o cursor
        Vector2 offset = new Vector2(16f, -16f);
        tooltipRect.anchoredPosition = mousePos + offset;

        // Clamp para não sair da tela
        Vector2 canvasSize = (parentCanvas.transform as RectTransform).sizeDelta;
        Vector2 pos = tooltipRect.anchoredPosition;
        Vector2 size = tooltipRect.sizeDelta;

        // Se sair pela direita, muda para a esquerda do cursor
        if (pos.x + size.x > canvasSize.x / 2f)
        {
            pos.x = mousePos.x - size.x - 8f;
        }

        // Se sair por baixo, move para cima
        if (pos.y - size.y < -canvasSize.y / 2f)
        {
            pos.y = mousePos.y + size.y + 8f;
        }

        tooltipRect.anchoredPosition = pos;
    }

    public void SetFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;
        if (nameText != null) nameText.font = fontAsset;
        if (descriptionText != null) descriptionText.font = fontAsset;
        if (tierText != null) tierText.font = fontAsset;
        if (quantityText != null) quantityText.font = fontAsset;
    }
}
