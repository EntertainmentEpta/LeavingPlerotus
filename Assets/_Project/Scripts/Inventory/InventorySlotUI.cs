using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Componente visual de um slot individual do inventÃ¡rio.
/// Exibe Ã­cone do item, quantidade e borda colorida por tier.
/// Detecta hover do mouse para exibir tooltip.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Componentes (Opcional - Prefab)")]
    public Image backgroundImage;
    public Image borderImage;
    public Image iconImage;
    public Image glowImage;
    public TextMeshProUGUI quantityText;
    public TextMeshProUGUI lockText;

    // Dados do slot
    private string currentItemId;
    private int currentQuantity;
    private ItemData currentItemData;
    private bool isOccupied = false;
    private bool isLocked = false;

    // ReferÃªncia ao tooltip
    private InventoryTooltip tooltip;

    // Cores
    private static readonly Color SLOT_EMPTY_BG = new Color(0.12f, 0.12f, 0.16f, 0.9f);
    private static readonly Color SLOT_OCCUPIED_BG = new Color(0.18f, 0.18f, 0.24f, 0.95f);
    private static readonly Color SLOT_EMPTY_BORDER = new Color(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color SLOT_HOVER_BG = new Color(0.25f, 0.25f, 0.32f, 0.95f);

    private bool isHovered = false;

    // Micro-animaÃ§Ã£o de hover
    private Vector3 targetScale = Vector3.one;
    private float scaleSpeed = 15f;

    // Sprite estÃ¡tico para glow radial de tier
    private static Sprite radialGlowSprite;

    /// <summary>
    /// Inicializa o slot criando todos os elementos visuais ou usando referÃªncias existentes do Prefab
    /// <summary>
    /// Inicializa o slot criando todos os elementos visuais ou usando referÃªncias existentes do Prefab
    /// </summary>
    public void Initialize(InventoryTooltip tooltipRef, float slotSize)
    {
        tooltip = tooltipRef;

        // Se jÃ¡ tiver as referÃªncias do Prefab atribuÃ­das, nÃ£o cria nada por cÃ³digo!
        if (backgroundImage != null && borderImage != null && iconImage != null && quantityText != null)
        {
            if (lockText != null) lockText.enabled = false;
            if (glowImage != null) glowImage.enabled = false;
            SetEmpty();
            return;
        }

        int uiLayer = gameObject.layer;

        // === Background do Slot ===
        backgroundImage = gameObject.AddComponent<Image>();
        backgroundImage.color = SLOT_EMPTY_BG;
        backgroundImage.raycastTarget = true;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(slotSize, slotSize);

        // === Glow de Rarity/Tier ===
        if (glowImage == null)
        {
            GameObject glowObj = new GameObject("TierGlow");
            glowObj.transform.SetParent(transform, false);
            glowObj.layer = uiLayer;
            RectTransform glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.05f, 0.05f);
            glowRect.anchorMax = new Vector2(0.95f, 0.95f);
            glowRect.sizeDelta = Vector2.zero;
            glowRect.anchoredPosition = Vector2.zero;

            glowObj.AddComponent<CanvasRenderer>();
            glowImage = glowObj.AddComponent<Image>();
            if (radialGlowSprite == null) GenerateRadialGlowSprite();
            glowImage.sprite = radialGlowSprite;
            glowImage.raycastTarget = false;
        }
        glowImage.enabled = false;

        // === Borda ===
        GameObject borderObj = new GameObject("SlotBorder");
        borderObj.transform.SetParent(transform, false);
        borderObj.layer = uiLayer;

        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderRect.anchoredPosition = Vector2.zero;

        borderObj.AddComponent<CanvasRenderer>();
        borderImage = borderObj.AddComponent<Image>();
        borderImage.color = SLOT_EMPTY_BORDER;
        borderImage.raycastTarget = false;
        borderImage.type = Image.Type.Sliced;
        borderImage.fillCenter = false;
        borderImage.pixelsPerUnitMultiplier = 1f;

        // === Ãcone do Item ===
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(transform, false);
        iconObj.layer = uiLayer;

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.15f);
        iconRect.anchorMax = new Vector2(0.9f, 0.95f);
        iconRect.sizeDelta = Vector2.zero;
        iconRect.anchoredPosition = Vector2.zero;

        iconObj.AddComponent<CanvasRenderer>();
        iconImage = iconObj.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false; // Escondido atÃ© ter item

        // === Texto de Quantidade ===
        GameObject textObj = new GameObject("Quantity");
        textObj.transform.SetParent(transform, false);
        textObj.layer = uiLayer;

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.0f, 0.0f);
        textRect.anchorMax = new Vector2(1.0f, 0.35f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        textObj.AddComponent<CanvasRenderer>();
        quantityText = textObj.AddComponent<TextMeshProUGUI>();
        quantityText.fontSize = slotSize * 0.22f;
        quantityText.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        quantityText.alignment = TextAlignmentOptions.BottomRight;
        quantityText.fontStyle = FontStyles.Bold;
        quantityText.raycastTarget = false;
        quantityText.margin = new Vector4(0, 0, 4, 2);
        quantityText.enabled = false;

        // === Texto de Cadeado (Lock) ===
        if (lockText == null)
        {
            GameObject lockObj = new GameObject("LockText");
            lockObj.transform.SetParent(transform, false);
            lockObj.layer = uiLayer;

            RectTransform lockRect = lockObj.AddComponent<RectTransform>();
            lockRect.anchorMin = Vector2.zero;
            lockRect.anchorMax = Vector2.one;
            lockRect.sizeDelta = Vector2.zero;
            lockRect.anchoredPosition = Vector2.zero;

            lockObj.AddComponent<CanvasRenderer>();
            lockText = lockObj.AddComponent<TextMeshProUGUI>();
            lockText.fontSize = slotSize * 0.35f;
            lockText.color = new Color(0.5f, 0.5f, 0.6f, 0.4f);
            lockText.alignment = TextAlignmentOptions.Center;
            lockText.fontStyle = FontStyles.Bold;
            lockText.raycastTarget = false;
            lockText.text = "ðŸ”’";
        }
        lockText.enabled = false;

        SetEmpty();
    }

    void Update()
    {
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }
    }

    private void GenerateRadialGlowSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha; // quadratic falloff for soft neon glow
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }

    public void SetFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;
        if (quantityText != null) quantityText.font = fontAsset;
        if (lockText != null) lockText.font = fontAsset;
    }

    /// <summary>
    /// Atualiza o slot com dados de um item
    /// </summary>
    public void SetItem(string itemId, int quantity, ItemData itemData)
    {
        currentItemId = itemId;
        currentQuantity = quantity;
        currentItemData = itemData;
        isOccupied = true;

        // Background mais claro quando ocupado
        if (!isHovered)
            backgroundImage.color = SLOT_OCCUPIED_BG;

        // Ãcone
        if (itemData != null && itemData.icon != null)
        {
            iconImage.sprite = itemData.icon;
            iconImage.enabled = true;
        }
        else
        {
            // Fallback: mostra slot ocupado sem Ã­cone (item sem sprite configurado)
            iconImage.enabled = false;
        }

        // Quantidade
        if (quantity > 1)
        {
            quantityText.text = "x" + quantity;
            quantityText.enabled = true;
        }
        else
        {
            quantityText.text = "";
            quantityText.enabled = false;
        }

        // Borda com cor do Tier e Glow
        if (itemData != null)
        {
            Color tierColor = itemData.GetTierColor();
            borderImage.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.8f);
            if (glowImage != null)
            {
                glowImage.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.35f);
                glowImage.enabled = true;
            }
        }
        else
        {
            borderImage.color = SLOT_EMPTY_BORDER;
            if (glowImage != null) glowImage.enabled = false;
        }
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (locked)
        {
            currentItemId = null;
            currentQuantity = 0;
            currentItemData = null;
            isOccupied = false;

            backgroundImage.color = new Color(0.06f, 0.06f, 0.08f, 0.8f);
            borderImage.color = new Color(0.2f, 0.2f, 0.25f, 0.3f);
            iconImage.enabled = false;
            quantityText.enabled = false;
            if (lockText != null) lockText.enabled = true;
            if (glowImage != null) glowImage.enabled = false;
        }
        else
        {
            if (lockText != null) lockText.enabled = false;
            SetEmpty();
        }
    }

    /// <summary>
    /// Limpa o slot (slot vazio)
    /// </summary>
    public void SetEmpty()
    {
        currentItemId = null;
        currentQuantity = 0;
        currentItemData = null;
        isOccupied = false;
        isLocked = false;

        if (!isHovered)
            backgroundImage.color = SLOT_EMPTY_BG;

        iconImage.enabled = false;
        quantityText.enabled = false;
        borderImage.color = SLOT_EMPTY_BORDER;
        if (lockText != null) lockText.enabled = false;
        if (glowImage != null) glowImage.enabled = false;
    }

    // === Hover Events para Tooltip ===

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isLocked) return;
        isHovered = true;
        targetScale = new Vector3(1.08f, 1.08f, 1.08f);

        if (isOccupied)
        {
            backgroundImage.color = SLOT_HOVER_BG;

            if (tooltip != null && currentItemData != null)
            {
                tooltip.Show(currentItemData, currentQuantity);
            }
        }
        else
        {
            backgroundImage.color = new Color(SLOT_EMPTY_BG.r + 0.05f, SLOT_EMPTY_BG.g + 0.05f, SLOT_EMPTY_BG.b + 0.05f, SLOT_EMPTY_BG.a);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isLocked) return;
        isHovered = false;
        targetScale = Vector3.one;

        backgroundImage.color = isOccupied ? SLOT_OCCUPIED_BG : SLOT_EMPTY_BG;

        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }

    // === Click Event para enviar o item para o Upgrade (InfusÃ£o) ===

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked) return;

        // SÃ³ faz algo se fomos clicados e se tem um item aqui dentro
        if (isOccupied && !string.IsNullOrEmpty(currentItemId))
        {
            InfusionUI telaDeUpgrades = Object.FindFirstObjectByType<InfusionUI>(FindObjectsInactive.Include);

            if (telaDeUpgrades != null)
            {
                // SÃ³ seleciona o item se o painel conseguiu abrir (sem inimigos por perto)
                bool abriu = telaDeUpgrades.OpenPanel();
                if (abriu)
                {
                    telaDeUpgrades.SelectItem(currentItemId);
                    Debug.Log($"[SLOT] Item '{currentItemId}' enviado para InfusÃ£o.");
                }
            }
            else
            {
                Debug.LogWarning($"[SLOT] InfusionUI nÃ£o encontrada na cena!");
            }
        }
    }
}

