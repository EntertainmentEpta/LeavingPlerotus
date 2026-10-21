using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador Visual da Interface de Upgrades (InfusÃƒÂ£o/FusÃƒÂ£o e Reciclagem).
/// EstilizaÃƒÂ§ÃƒÂ£o visual Premium Sci-Fi / Tech com ordenaÃƒÂ§ÃƒÂ£o correta de camadas,
/// botÃƒÂµes auto-ajustÃƒÂ¡veis sem vazamento de texto e animaÃƒÂ§ÃƒÂµes dinÃƒÂ¢micas de pulso.
/// </summary>
public class InfusionUI : MonoBehaviour
{
    [Header("ConexÃƒÂ£o com o Motor")]
    [Tooltip("Arraste o Player (que tem o script InfusionManager) aqui!")]
    public InfusionManager infusionManager;
    
    [Header("Painel Principal")]
    [Tooltip("O GameObject inteiro da janela de Upgrades")]
    public GameObject painelUpgrades; 
    
    [Header("ÃƒÂrea de InformaÃƒÂ§ÃƒÂ£o do Item Selecionado")]
    public Image itemIcon;
    public TextMeshProUGUI itemTitle;
    public TextMeshProUGUI itemRarity;
    public TextMeshProUGUI itemStatsDescription;
    public TextMeshProUGUI recycleValueText;
    
    [Header("BotÃƒÂµes Interativos")]
    public Button btnInfundir;
    public Button btnReciclar;
    public Button btnFechar;

    [Header("EstilizaÃƒÂ§ÃƒÂ£o & Tipografia (Opcional)")]
    public TMP_FontAsset customFont;

    // Componentes Internos de Estilo
    private Image tierGlowImage;
    private Image itemBorderImage;
    private Image cardBgImage;
    private Image panelBgImage;
    private static Sprite radialGlowSprite;
    private string selectedItemId = "";
    private System.Collections.Generic.List<string> selectedItemIds = new System.Collections.Generic.List<string>();
    private System.Collections.Generic.List<UnityEngine.GameObject> spawnedIcons = new System.Collections.Generic.List<UnityEngine.GameObject>();
    private Color currentTierColor = Color.white;
    private Coroutine openAnimCoroutine;
    private Coroutine floatAnimCoroutine;

    // Cores do Tema Sci-Fi / Tech Frosted Glass
    private static readonly Color PANEL_BG = new Color(0.04f, 0.05f, 0.09f, 0.94f);
    private static readonly Color PANEL_BORDER_OUTER = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color PANEL_BORDER_INNER = new Color(0.3f, 0.5f, 0.9f, 0.25f);
    private static readonly Color HEADER_ACCENT = new Color(0.40f, 0.70f, 1.0f, 1f);
    private static readonly Color CARD_SLOT_BG = new Color(0.06f, 0.08f, 0.13f, 0.95f);

    private static InfusionUI _instance;
    public static InfusionUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = EnsureInstance();
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    public static InfusionUI EnsureInstance()
    {
        if (_instance != null) return _instance;

        InfusionUI existing = Object.FindFirstObjectByType<InfusionUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return existing;
        }

        GameObject prefab = Resources.Load<GameObject>("PainelUpgrades") ?? 
                            Resources.Load<GameObject>("UI/PainelUpgrades");
        if (prefab != null)
        {
            Canvas targetCanvas = InventoryUI.CanvasInstance;
            if (targetCanvas == null)
            {
                Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in canvases)
                {
                    if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }

            GameObject instance = Object.Instantiate(prefab, targetCanvas != null ? targetCanvas.transform : null);
            instance.name = "PainelUpgrades";
            existing = instance.GetComponentInChildren<InfusionUI>(true);
            if (existing != null)
            {
                existing.painelUpgrades = instance;
                _instance = existing;
            }
            return existing;
        }

        Debug.LogError("[INFUSION UI] Falha ao carregar PainelUpgrades do Resources!");
        return null;
    }

    void Awake()
    {
        _instance = this;

        // Tenta carregar Oswald Bold SDF do Resources se não estiver atribuída
        if (customFont == null)
        {
            customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        }

        // Auto-estilização e reforço da UI
        ApplyThemeAndEnhancements();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        if (btnFechar != null) btnFechar.onClick.AddListener(ClosePanel);
        if (btnInfundir != null) btnInfundir.onClick.AddListener(OnInfuseButtonClicked);
        if (btnReciclar != null) btnReciclar.onClick.AddListener(OnBtnReciclarClicked);

        // Prepara botÃƒÂµes com animaÃƒÂ§ÃƒÂ£o de hover e presenÃƒÂ§a visual
        SetupPremiumButton(btnInfundir, 1.04f);
        SetupPremiumButton(btnReciclar, 1.04f);
        SetupPremiumButton(btnFechar, 1.12f);

        ApplyThemeAndEnhancements();
    }

    void Update()
    {
        // AnimaÃƒÂ§ÃƒÂ£o de respiraÃƒÂ§ÃƒÂ£o contÃƒÂ­nua e forte para o glow por trÃƒÂ¡s do item
        if (tierGlowImage != null && tierGlowImage.enabled && gameObject.activeInHierarchy)
        {
            float pulse = 0.55f + Mathf.PingPong(Time.unscaledTime * 2.2f, 0.35f);
            tierGlowImage.color = new Color(currentTierColor.r, currentTierColor.g, currentTierColor.b, pulse);
        }
    }

    /// <summary>
    /// Separa levemente os botÃƒÂµes INFUNDIR e RECICLAR para dar espaÃƒÂ§o ÃƒÂ  animaÃƒÂ§ÃƒÂ£o de escala de hover sem colisÃƒÂ£o.
    /// </summary>
    private void SeperateButtonsSpacing()
    {
        if (btnInfundir != null && btnReciclar != null)
        {
            RectTransform infRt = btnInfundir.GetComponent<RectTransform>();
            RectTransform recRt = btnReciclar.GetComponent<RectTransform>();
            if (infRt != null && recRt != null)
            {
                // Garante que haja um espaÃƒÂ§o de respiro confortÃƒÂ¡vel entre os dois botÃƒÂµes
                if (Mathf.Abs(infRt.anchoredPosition.x - recRt.anchoredPosition.x) < 180f)
                {
                    if (infRt.anchoredPosition.x < recRt.anchoredPosition.x)
                    {
                        infRt.anchoredPosition -= new Vector2(15f, 0f);
                        recRt.anchoredPosition += new Vector2(15f, 0f);
                    }
                    else
                    {
                        infRt.anchoredPosition += new Vector2(15f, 0f);
                        recRt.anchoredPosition -= new Vector2(15f, 0f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Aplica a identidade visual Sci-Fi / Tech ao painel e organiza as camadas Z.
    /// </summary>
    private void ApplyThemeAndEnhancements()
    {
        if (painelUpgrades == null) painelUpgrades = gameObject;

        int uiLayer = painelUpgrades.layer;

        // === Fundo Frosted Glass do Painel ===
        panelBgImage = painelUpgrades.GetComponent<Image>();
        if (panelBgImage == null) panelBgImage = painelUpgrades.AddComponent<Image>();
        panelBgImage.color = PANEL_BG;
        panelBgImage.raycastTarget = true;

        // Drop Shadow para efeito de profundidade suspensa
        Shadow shadow = painelUpgrades.GetComponent<Shadow>();
        if (shadow == null) shadow = painelUpgrades.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(10f, -10f);

        // === Borda Superior de Reflexo de Vidro (Glass Highlight) ===
        Transform glassHighlightT = painelUpgrades.transform.Find("GlassHighlight_Infusion");
        if (glassHighlightT == null)
        {
            GameObject glassObj = new GameObject("GlassHighlight_Infusion");
            glassObj.transform.SetParent(painelUpgrades.transform, false);
            glassObj.layer = uiLayer;

            RectTransform gRt = glassObj.AddComponent<RectTransform>();
            gRt.anchorMin = new Vector2(0f, 1f);
            gRt.anchorMax = new Vector2(1f, 1f);
            gRt.pivot = new Vector2(0.5f, 1f);
            gRt.anchoredPosition = new Vector2(0f, -2f);
            gRt.sizeDelta = new Vector2(-4f, 3f);

            Image gImg = glassObj.AddComponent<Image>();
            gImg.color = new Color(1f, 1f, 1f, 0.20f);
            gImg.raycastTarget = false;
        }

        // === Bordas Duplas Tech de Contorno ===
        Transform borderT = painelUpgrades.transform.Find("PanelBorder_Infusion");
        if (borderT == null)
        {
            GameObject borderObj = new GameObject("PanelBorder_Infusion");
            borderObj.transform.SetParent(painelUpgrades.transform, false);
            borderObj.layer = uiLayer;

            RectTransform bRt = borderObj.AddComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.sizeDelta = new Vector2(4f, 4f);
            bRt.anchoredPosition = Vector2.zero;

            Image bImg = borderObj.AddComponent<Image>();
            bImg.color = PANEL_BORDER_OUTER;
            bImg.raycastTarget = false;
            bImg.type = Image.Type.Sliced;
            bImg.fillCenter = false;
            borderObj.transform.SetAsFirstSibling();
        }

        Transform borderInnerT = painelUpgrades.transform.Find("PanelBorderInner_Infusion");
        if (borderInnerT == null)
        {
            GameObject bInnerObj = new GameObject("PanelBorderInner_Infusion");
            bInnerObj.transform.SetParent(painelUpgrades.transform, false);
            bInnerObj.layer = uiLayer;

            RectTransform biRt = bInnerObj.AddComponent<RectTransform>();
            biRt.anchorMin = Vector2.zero;
            biRt.anchorMax = Vector2.one;
            biRt.sizeDelta = new Vector2(-8f, -8f);
            biRt.anchoredPosition = Vector2.zero;

            Image biImg = bInnerObj.AddComponent<Image>();
            biImg.color = PANEL_BORDER_INNER;
            biImg.raycastTarget = false;
            biImg.type = Image.Type.Sliced;
            biImg.fillCenter = false;
        }

        // === Cantos Decorativos Sci-Fi (Corners) ===
        AddTechCorner(painelUpgrades.transform, "Corner_TL", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(16f, 16f));
        AddTechCorner(painelUpgrades.transform, "Corner_TR", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(16f, 16f));
        AddTechCorner(painelUpgrades.transform, "Corner_BL", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 16f));
        AddTechCorner(painelUpgrades.transform, "Corner_BR", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(16f, 16f));

        // === ESPAÃƒâ€¡AMENTO ENTRE OS BOTÃƒâ€¢ES INFUNDIR E RECICLAR ===
        SeperateButtonsSpacing();

        // === GARANTIA ABSOLUTA DE CAMADAS Z DO ITEM (ÃƒÂCONE TOTALMENTE NA FRENTE, SEM NADA POR CIMA) ===
        if (itemIcon != null)
        {
            itemIcon.color = Color.white; // Imagem 100% limpa com cores originais e vibrantes
            itemIcon.raycastTarget = false;
            itemIcon.preserveAspect = true;
            itemIcon.rectTransform.anchoredPosition = new Vector2(-320f, itemIcon.rectTransform.anchoredPosition.y);
            itemIcon.rectTransform.sizeDelta = new Vector2(140f, 140f);

            Transform parentT = itemIcon.transform.parent;
            if (parentT != null)
            {
                // DestrÃƒÂ³i qualquer overlay antigo que estivesse cobrindo a imagem
                Transform oldBorderT = parentT.Find("CardBorder_Infusion");
                if (oldBorderT != null)
                {
                    Destroy(oldBorderT.gameObject);
                }

                // Fundo escuro do slot do card (Camada 0 - Mais ao fundo)
                Transform cardBgT = parentT.Find("CardBg_Infusion");
                if (cardBgT == null)
                {
                    GameObject cBgObj = new GameObject("CardBg_Infusion");
                    cBgObj.transform.SetParent(parentT, false);
                    cBgObj.layer = uiLayer;

                    RectTransform cbRt = cBgObj.AddComponent<RectTransform>();
                    cbRt.anchorMin = itemIcon.rectTransform.anchorMin;
                    cbRt.anchorMax = itemIcon.rectTransform.anchorMax;
                    cbRt.pivot = itemIcon.rectTransform.pivot;
                    cbRt.anchoredPosition = itemIcon.rectTransform.anchoredPosition;
                    cbRt.sizeDelta = itemIcon.rectTransform.sizeDelta + new Vector2(20f, 20f);

                    cardBgImage = cBgObj.AddComponent<Image>();
                    cardBgImage.color = CARD_SLOT_BG;
                    cardBgImage.raycastTarget = false;

                    // Adiciona Outline no prÃƒÂ³prio fundo para criar a borda nitidamente colorida por tier
                    Outline cardOutline = cBgObj.AddComponent<Outline>();
                    cardOutline.effectColor = new Color(0.4f, 0.6f, 1f, 0.6f);
                    cardOutline.effectDistance = new Vector2(3f, -3f);
                }
                else
                {
                    cardBgImage = cardBgT.GetComponent<Image>();
                }
                if (cardBgT != null) cardBgT.SetSiblingIndex(0);

                // Brilho Radial Amplo e Ultra-Radiante do Tier (Camada 1 - AtrÃƒÂ¡s do ÃƒÂcone)
                Transform glowT = parentT.Find("TierGlow_Infusion");
                if (glowT == null)
                {
                    GameObject glowObj = new GameObject("TierGlow_Infusion");
                    glowObj.transform.SetParent(parentT, false);
                    glowObj.layer = uiLayer;

                    RectTransform gRect = glowObj.AddComponent<RectTransform>();
                    gRect.anchorMin = itemIcon.rectTransform.anchorMin;
                    gRect.anchorMax = itemIcon.rectTransform.anchorMax;
                    gRect.pivot = itemIcon.rectTransform.pivot;
                    gRect.anchoredPosition = itemIcon.rectTransform.anchoredPosition;
                    gRect.sizeDelta = itemIcon.rectTransform.sizeDelta + new Vector2(140f, 140f);

                    tierGlowImage = glowObj.AddComponent<Image>();
                    if (radialGlowSprite == null) GenerateRadialGlowSprite();
                    tierGlowImage.sprite = radialGlowSprite;
                    tierGlowImage.raycastTarget = false;
                    tierGlowImage.enabled = false;
                }
                else
                {
                    tierGlowImage = glowT.GetComponent<Image>();
                }
                if (glowT != null) glowT.SetSiblingIndex(1);

                // Cantos Decorativos de Raridade no Card Slot
                AddTechCorner(parentT, "CardCorner_TL", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(12f, 12f));
                AddTechCorner(parentT, "CardCorner_TR", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(12f, 12f));
                AddTechCorner(parentT, "CardCorner_BL", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(12f, 12f));
                AddTechCorner(parentT, "CardCorner_BR", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f));

                // O ÃƒÂCONE DO ITEM Ãƒâ€° MOVIDO PARA O TOPO ABSOLUTO (NADA FICA NA FRENTE DELE)
                itemIcon.transform.SetAsLastSibling();
            }
        }

        // === AplicaÃƒÂ§ÃƒÂ£o da Fonte Customizada & Auto-Sizing (Sem vazamento de texto) ===
        ApplyAutoSizingText(itemTitle);
        ApplyAutoSizingText(itemRarity);
        ApplyAutoSizingText(itemStatsDescription);
        ApplyAutoSizingText(recycleValueText);

        SetupButtonTextFormatting(btnInfundir);
        SetupButtonTextFormatting(btnReciclar);
        SetupButtonTextFormatting(btnFechar);

        // EstilizaÃƒÂ§ÃƒÂ£o visual Tech dos botÃƒÂµes
        StyleActionButton(btnInfundir, new Color(0.07f, 0.12f, 0.22f, 0.95f), new Color(0.00f, 0.85f, 1.00f, 0.90f));
        StyleActionButton(btnReciclar, new Color(0.18f, 0.11f, 0.05f, 0.95f), new Color(1.00f, 0.65f, 0.00f, 0.90f));
    }

    private void AddTechCorner(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        if (parent.Find(name) != null) return;
        GameObject cObj = new GameObject(name);
        cObj.transform.SetParent(parent, false);
        cObj.layer = parent.gameObject.layer;

        RectTransform rt = cObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = cObj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.7f, 1f, 0.45f);
        img.raycastTarget = false;
    }

    private void ApplyAutoSizingText(TextMeshProUGUI tmpText)
    {
        if (tmpText == null) return;
        if (customFont != null) tmpText.font = customFont;
        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = 10f;
        tmpText.fontSizeMax = Mathf.Max(14f, tmpText.fontSize);
        tmpText.overflowMode = TextOverflowModes.Ellipsis;
        tmpText.margin = new Vector4(4f, 2f, 4f, 2f);
    }

    private void SetupButtonTextFormatting(Button btn)
    {
        if (btn == null) return;

        TextMeshProUGUI tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null)
        {
            if (customFont != null) tmpText.font = customFont;
            tmpText.enableAutoSizing = true;
            tmpText.fontSizeMin = 10f;
            tmpText.fontSizeMax = 20f;
            tmpText.overflowMode = TextOverflowModes.Ellipsis;
            tmpText.textWrappingMode = TextWrappingModes.Normal;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.margin = new Vector4(6f, 2f, 6f, 2f);
        }
    }

    private void StyleActionButton(Button btn, Color bgColor, Color borderGlowColor)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = bgColor;
        }

        Shadow shadow = btn.GetComponent<Shadow>();
        if (shadow == null) shadow = btn.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(4f, -4f);

        // Outline neon brilhante de estilo Sci-Fi
        Outline outline = btn.GetComponent<Outline>();
        if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
        outline.effectColor = borderGlowColor;
        outline.effectDistance = new Vector2(2f, -2f);
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
                alpha = alpha * alpha; // Quadratic falloff para iluminaÃƒÂ§ÃƒÂ£o suave
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }

    public bool OpenPanel()
    {
        // Reconexão de Segurança
        if (infusionManager == null)
        {
            infusionManager = FindFirstObjectByType<InfusionManager>();
        }

        if (painelUpgrades == null)
        {
            painelUpgrades = gameObject;
        }

        if (painelUpgrades != null) 
        {
            Transform p = painelUpgrades.transform.parent;
            while (p != null)
            {
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                p = p.parent;
            }

            painelUpgrades.SetActive(true);
            painelUpgrades.transform.SetAsLastSibling();
            
            Canvas c = painelUpgrades.GetComponent<Canvas>();
            if (c == null) 
            {
                c = painelUpgrades.AddComponent<Canvas>();
            }
            if (painelUpgrades.GetComponent<GraphicRaycaster>() == null)
            {
                painelUpgrades.AddComponent<GraphicRaycaster>();
            }
            c.overrideSorting = true;
            c.sortingOrder = 999; 

            // Animação de Abertura
            if (openAnimCoroutine != null) StopCoroutine(openAnimCoroutine);
            openAnimCoroutine = StartCoroutine(AnimatePanelOpen());
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        ClearSelection();
        return true;
    }

    private IEnumerator AnimatePanelOpen()
    {
        CanvasGroup cg = painelUpgrades.GetComponent<CanvasGroup>();
        if (cg == null) cg = painelUpgrades.AddComponent<CanvasGroup>();

        float duration = 0.28f;
        float time = 0f;
        
        RectTransform rt = painelUpgrades.GetComponent<RectTransform>();
        
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f); // Out Back Curve
            
            cg.alpha = Mathf.Lerp(0f, 1f, easeT);
            rt.localScale = Vector3.Lerp(new Vector3(0.85f, 0.85f, 0.85f), Vector3.one, easeT);
            
            yield return null;
        }
    }

    public void ClosePanel()
    {
        if (painelUpgrades != null) painelUpgrades.SetActive(false);
    }

        public void SelectMultipleItems(System.Collections.Generic.List<string> itemIds)
    {
        // Cópia defensiva: antes a UI guardava a MESMA referência da lista do InventoryUI
        selectedItemIds = itemIds != null ? new System.Collections.Generic.List<string>(itemIds) : new System.Collections.Generic.List<string>();
        itemIds = selectedItemIds;
        selectedItemId = itemIds.Count == 1 ? itemIds[0] : ""; // Habilita Reciclar para seleção única
        
        if (ItemDatabase.Instance == null) return;
        
        // Limpar icones antigos
        foreach (var obj in spawnedIcons)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedIcons.Clear();

        if (itemIcon != null) itemIcon.enabled = false;

        if (itemIds.Count == 0) return;

        ItemData firstData = ItemDatabase.Instance.GetItemData(itemIds[0]);
        if (firstData != null) currentTierColor = firstData.GetTierColor();

        int count = itemIds.Count;
        float centerLeftX = -320f;
        float spacing = count > 1 ? 110f : 0f;
        float startX = centerLeftX - (count - 1) * spacing / 2f;

        // Atualiza slot de fundo (CardBg) e Glow para abraçar os ícones na esquerda
        if (cardBgImage != null)
        {
            RectTransform cbRt = cardBgImage.GetComponent<RectTransform>();
            if (cbRt != null)
            {
                cbRt.anchoredPosition = new UnityEngine.Vector2(centerLeftX, cbRt.anchoredPosition.y);
                float cardW = count > 1 ? 240f : 160f;
                cbRt.sizeDelta = new UnityEngine.Vector2(cardW, 160f);
            }
            cardBgImage.color = new UnityEngine.Color(currentTierColor.r * 0.40f + 0.06f, currentTierColor.g * 0.40f + 0.06f, currentTierColor.b * 0.40f + 0.08f, 0.98f);
        }

        if (tierGlowImage != null)
        {
            RectTransform gRt = tierGlowImage.GetComponent<RectTransform>();
            if (gRt != null)
            {
                gRt.anchoredPosition = new UnityEngine.Vector2(centerLeftX, gRt.anchoredPosition.y);
                float glowW = count > 1 ? 340f : 260f;
                gRt.sizeDelta = new UnityEngine.Vector2(glowW, 260f);
            }
            tierGlowImage.color = new UnityEngine.Color(currentTierColor.r, currentTierColor.g, currentTierColor.b, 0.95f);
            tierGlowImage.enabled = true;
        }

        for (int i = 0; i < count; i++)
        {
            ItemData data = ItemDatabase.Instance.GetItemData(itemIds[i]);
            if (data == null) continue;

            if (itemIcon != null)
            {
                UnityEngine.UI.Image newIcon = Instantiate(itemIcon, itemIcon.transform.parent);
                newIcon.name = "DynamicIcon_" + i;
                newIcon.sprite = data.icon;
                newIcon.color = UnityEngine.Color.white;
                newIcon.preserveAspect = true;
                newIcon.enabled = true;
                
                RectTransform rt = newIcon.GetComponent<RectTransform>();
                rt.anchoredPosition = new UnityEngine.Vector2(startX + i * spacing, rt.anchoredPosition.y);
                rt.sizeDelta = count > 1 ? new UnityEngine.Vector2(105f, 105f) : new UnityEngine.Vector2(140f, 140f);
                
                spawnedIcons.Add(newIcon.gameObject);
                
                // Add "+" sign between icons
                if (i < count - 1)
                {
                    UnityEngine.GameObject plusObj = new UnityEngine.GameObject("PlusSign");
                    plusObj.transform.SetParent(itemIcon.transform.parent, false);
                    RectTransform plusRt = plusObj.AddComponent<RectTransform>();
                    plusRt.anchoredPosition = new UnityEngine.Vector2(startX + i * spacing + (spacing / 2f), rt.anchoredPosition.y);
                    plusRt.sizeDelta = new UnityEngine.Vector2(40f, 40f);
                    
                    plusObj.AddComponent<UnityEngine.CanvasRenderer>();
                    TMPro.TextMeshProUGUI plusText = plusObj.AddComponent<TMPro.TextMeshProUGUI>();
                    plusText.text = "+";
                    plusText.fontSize = 42f;
                    plusText.color = UnityEngine.Color.white;
                    plusText.alignment = TMPro.TextAlignmentOptions.Center;
                    if (customFont != null) plusText.font = customFont;
                    
                    spawnedIcons.Add(plusObj);
                }
            }
        }

        if (infusionManager == null) infusionManager = Object.FindFirstObjectByType<InfusionManager>();
        InfusionRequest request = infusionManager != null ? infusionManager.BuildInfusionRequest(itemIds) : default;

        if (itemTitle != null)
        {
            if (request.IsValid && request.mode == InfusionMode.Fused)
                itemTitle.text = $"FUSÃO ×{request.stackMultiplier:0.##}: " + request.data.itemName.ToUpper();
            else if (count == 1 && firstData != null)
                itemTitle.text = "INFUSÃO: " + firstData.itemName.ToUpper();
            else
                itemTitle.text = "FUSÃO MÚLTIPLA (" + count + " ITENS)";
            
            itemTitle.color = HEADER_ACCENT;
        }

        UpdateDynamicTexts(itemIds);

        // FIX: OpenPanel() → ClearSelection() desativa os botões. Sem reativar aqui,
        // o botão INFUNDIR ficava travado (interactable = false) e nunca disparava o clique.
        bool hasCap = infusionManager == null || infusionManager.HasInfusionCapacity(1);
        if (btnInfundir != null) { btnInfundir.interactable = hasCap; UpdateButtonVisuals(btnInfundir); }
        if (btnReciclar != null) { btnReciclar.interactable = count == 1; UpdateButtonVisuals(btnReciclar); }
    }

    private void UpdateDynamicTexts(System.Collections.Generic.List<string> ids)
    {
        if (ItemDatabase.Instance == null) return;
        if (infusionManager == null) infusionManager = Object.FindFirstObjectByType<InfusionManager>();

        // -- FLUXO PADRÃO (1 item / 2 iguais): preview exato do que TryInfuse vai executar --
        if (infusionManager != null)
        {
            InfusionRequest request = infusionManager.BuildInfusionRequest(ids);
            if (request.IsValid)
            {
                if (itemRarity != null)
                    itemRarity.text = request.data.GetTierName() + (request.mode == InfusionMode.Fused ? $"  •  FUSÃO ×{request.stackMultiplier:0.##}" : "");
                if (recycleValueText != null) recycleValueText.text = "+" + request.data.recycleEssenceValue;

                UpdateStandardInfusionTexts(request);
                return;
            }
        }

        // -- FLUXO DE SINERGIA (itens diferentes ou múltiplos) --
        int totalCost = 0;
        int totalRecycle = 0;
        string rarityString = "";
        string descString = "";
        
        float currentWeight = (infusionManager != null) ? infusionManager.GetTotalWeight() : 0f;
        
        foreach (var id in ids)
        {
            ItemData data = ItemDatabase.Instance.GetItemData(id);
            if (data != null)
            {
                float costF = (infusionManager != null)
                    ? data.infusionEssenceCost * (1f + infusionManager.inflationAlpha * currentWeight)
                    : data.infusionEssenceCost * (1f + 0.1f * currentWeight);
                
                int cost = UnityEngine.Mathf.RoundToInt(costF);
                
                totalCost += cost;
                totalRecycle += data.recycleEssenceValue;
                
                rarityString += $"[{data.GetTierName()}] ";

                string tierHex = ColorUtility.ToHtmlStringRGB(data.GetTierColor());
                descString += $"<color=#{tierHex}><b>{data.itemName.ToUpper()}</b></color> <size=80%>({data.GetTierName()})</size>\n";
                bool isDecoded = CatalogoManager.instancia != null && CatalogoManager.instancia.IsItemInfused(data.itemId);
                if (!isDecoded)
                {
                    descString += "  <color=#FFCC00>• [ ? ? ? ] Efeito Oculto (Não Analisado)</color>\n";
                }
                else if (data.itemAttributes != null && data.itemAttributes.Count > 0)
                {
                    foreach (var buff in data.itemAttributes)
                    {
                        string signal = buff.value > 0 ? "+" : "";
                        string tipoMultiplier = buff.isMultiplier ? "%" : "";
                        float displayVal = buff.isMultiplier ? Mathf.Round((buff.value - 1f) * 100f) : buff.value;
                        descString += $"  <color=#00FFAA>• {signal}{displayVal}{tipoMultiplier}</color> {FormatterName(buff.attributeType.ToString())}\n";
                    }
                }
                else
                {
                    descString += $"  <color=#8888AA><i>{data.description}</i></color>\n";
                }

                currentWeight += data.GetTierWeight();
            }
        }

        // -- SINERGIA PREVIEW --
        SynergyResult previewResult = SynergyInfusionCalculator.Instance != null ? SynergyInfusionCalculator.Instance.EvaluateInfusion(ids) : null;
        if (previewResult != null && (previewResult.hasSynergy || previewResult.totalEssenceDiscount > 0f || (previewResult.extraModifiers != null && previewResult.extraModifiers.Count > 0)))
        {
            string recipeNames = (previewResult.triggeredRecipeNames != null && previewResult.triggeredRecipeNames.Count > 0)
                ? string.Join(", ", previewResult.triggeredRecipeNames)
                : "COMBINAÇÃO ESPECIAL";

            descString += $"\n<color=#FFD700><b>★ SINERGIA DETECTADA: {recipeNames.ToUpper()}!</b></color>\n";
            if (previewResult.totalEssenceDiscount > 0f)
            {
                descString += $"  <color=#00FFAA>• Desconto de Essência:</color> -{Mathf.RoundToInt(previewResult.totalEssenceDiscount * 100f)}%\n";
            }
            if (previewResult.extraModifiers != null)
            {
                foreach (var mod in previewResult.extraModifiers)
                {
                    string signal = mod.baseValue > 0 ? "+" : "";
                    if (mod.baseValue != 0f) descString += $"  <color=#00FFAA>• Bônus Sinergia: {signal}{mod.baseValue}</color> {FormatterName(mod.statName)}\n";
                    if (mod.multiplierValue != 0f) descString += $"  <color=#00FFAA>• Bônus Sinergia: +{mod.multiplierValue * 100f}%</color> {FormatterName(mod.statName)}\n";
                }
            }

            // Aviso caso algum item selecionado não faça parte da receita de sinergia
            if (previewResult.hasSynergy && previewResult.usedItemIDs != null && previewResult.usedItemIDs.Count < ids.Count)
            {
                int unusedCount = ids.Count - previewResult.usedItemIDs.Count;
                descString += $"\n<color=#FF8800><i>⚠ Atenção: {unusedCount} item(ns) não fazem parte da receita de sinergia e fornecerão apenas atributos base normais.</i></color>\n";
            }
        }

        if (infusionManager != null)
        {
            int curInf = infusionManager.CurrentInfusionCount;
            int maxCap = infusionManager.GetMaxInfusionCapacity();
            string capColor = curInf >= maxCap ? "#FF5555" : "#00FFAA";
            descString += $"\n<size=85%><color=#8888AA>CAPACIDADE DO TRAJE:</color> <color={capColor}><b>{curInf}/{maxCap}</b></color></size>";
        }

        int discountedCost = previewResult != null ? UnityEngine.Mathf.RoundToInt(totalCost * (1f - previewResult.totalEssenceDiscount)) : totalCost;

        if (itemRarity != null) itemRarity.text = rarityString.Trim();
        if (itemStatsDescription != null) itemStatsDescription.text = descString;

        bool hasCapacity = infusionManager == null || infusionManager.HasInfusionCapacity(1);
        if (btnInfundir != null)
        {
            TMPro.TextMeshProUGUI txt = btnInfundir.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (!hasCapacity)
            {
                btnInfundir.interactable = false;
                if (txt != null)
                    txt.text = $"<b>TRAJE SOBRECARREGADO</b>\n<color=#FF5555><size=80%>CAPACIDADE MÁXIMA ATINGIDA</size></color>";
            }
            else
            {
                btnInfundir.interactable = true;
                if (txt != null) 
                {
                    if (previewResult != null && previewResult.totalEssenceDiscount > 0f)
                    {
                        txt.text = $"<b>INFUNDIR COMBINADO</b>\n<color=#00FFAA>{discountedCost} ESSÊNCIAS</color> <size=65%><color=#FF6666>(-{Mathf.RoundToInt(previewResult.totalEssenceDiscount * 100f)}%)</color></size>";
                    }
                    else
                    {
                        txt.text = $"<b>INFUNDIR COMBINADO</b>\n<color=#FFD700>{totalCost} ESSÊNCIAS</color>";
                    }
                }
            }
            UpdateButtonVisuals(btnInfundir);
        }
        if (recycleValueText != null) recycleValueText.text = "";
    }
    public void SelectItem(string itemId)
    {
        selectedItemId = itemId;
        selectedItemIds = new System.Collections.Generic.List<string> { itemId };
        
        if (ItemDatabase.Instance == null)
        {
            Debug.LogWarning("[INFUSION UI] ItemDatabase.Instance ÃƒÂ© null! NÃƒÂ£o ÃƒÂ© possÃƒÂ­vel mostrar dados do item.");
            return;
        }

        ItemData data = ItemDatabase.Instance.GetItemData(itemId);
        if (data == null) return;

        currentTierColor = data.GetTierColor();

        // Configuração do Ícone (Sem alterar a cor da imagem) e Glow por Tier
        if (itemIcon != null) 
        {
            itemIcon.sprite = data.icon;
            itemIcon.color = Color.white; // Imagem limpa sem filtro
            itemIcon.preserveAspect = true;
            itemIcon.rectTransform.anchoredPosition = new Vector2(-320f, itemIcon.rectTransform.anchoredPosition.y);
            itemIcon.rectTransform.sizeDelta = new Vector2(140f, 140f);
            itemIcon.enabled = (data.icon != null);
            itemIcon.transform.SetAsLastSibling(); // Garante 100% que o ícone fica no topo das camadas

            // Efeito visual ao selecionar
            if (gameObject.activeInHierarchy)
            {
                if (floatAnimCoroutine != null) StopCoroutine(floatAnimCoroutine);
                floatAnimCoroutine = StartCoroutine(PulseEffect(itemIcon.transform, 1.18f, 0.18f));
            }
        }

        if (cardBgImage != null)
        {
            RectTransform cbRt = cardBgImage.GetComponent<RectTransform>();
            if (cbRt != null)
            {
                cbRt.anchoredPosition = new Vector2(-320f, cbRt.anchoredPosition.y);
                cbRt.sizeDelta = new Vector2(160f, 160f);
            }

            // Tint visual vibrante e marcante no fundo do slot do card baseado na cor da raridade (Tier)
            cardBgImage.color = new Color(currentTierColor.r * 0.40f + 0.06f, currentTierColor.g * 0.40f + 0.06f, currentTierColor.b * 0.40f + 0.08f, 0.98f);
            
            Outline cardOutline = cardBgImage.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.effectColor = new Color(currentTierColor.r, currentTierColor.g, currentTierColor.b, 1.00f);
                cardOutline.effectDistance = new Vector2(4f, -4f);
            }

            Shadow cardShadow = cardBgImage.GetComponent<Shadow>();
            if (cardShadow == null) cardShadow = cardBgImage.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(currentTierColor.r, currentTierColor.g, currentTierColor.b, 0.85f);
            cardShadow.effectDistance = new Vector2(0f, 0f);
        }

        if (tierGlowImage != null)
        {
            RectTransform gRt = tierGlowImage.GetComponent<RectTransform>();
            if (gRt != null)
            {
                gRt.anchoredPosition = new Vector2(-320f, gRt.anchoredPosition.y);
                gRt.sizeDelta = new Vector2(260f, 260f);
            }
            tierGlowImage.color = new Color(currentTierColor.r, currentTierColor.g, currentTierColor.b, 0.95f);
            tierGlowImage.enabled = true;
        }

        if (itemBorderImage != null)
        {
            itemBorderImage.color = new Color(currentTierColor.r, currentTierColor.g, currentTierColor.b, 0.95f);
        }

        // Título e Rarity
        if (itemTitle != null)
        {
            itemTitle.text = data.itemName.ToUpper();
            itemTitle.color = currentTierColor;
        }
        
        if (itemRarity != null)
        {
            itemRarity.text = $"<mark=#" + ColorUtility.ToHtmlStringRGBA(new Color(currentTierColor.r * 0.5f, currentTierColor.g * 0.5f, currentTierColor.b * 0.5f, 0.45f)) + ">  — " + data.GetTierName().ToUpper() + " —  </mark>";
            itemRarity.color = currentTierColor;
        }

        if (recycleValueText != null)
            recycleValueText.text = "<color=#AAAAAA><size=75%>(Descarta o item do inventário)</size></color>";

        // Descrição e Atributos Formatados com Estilo Sci-Fi Astronauta (com Fog of War de Descoberta)
        if (itemStatsDescription != null)
        {
            bool isDecoded = CatalogoManager.instancia != null && CatalogoManager.instancia.IsItemInfused(data.itemId);
            if (!isDecoded)
            {
                itemStatsDescription.text = "<color=#FFCC00><b>[ ? ? ? ] PROPRIEDADES NÃO ANALISADAS</b></color>\n\n" +
                                            "<color=#00FFAA>• [ ? ? ? ] Efeito Oculto</color>\n\n" +
                                            "<size=85%><color=#8899AA><i>Este composto nunca foi infundido no traje.\n" +
                                            "Infunda pela 1ª vez para que o Eptinho analise sua composição e revele seus atributos permanentemente no Catálogo!</i></color></size>";
            }
            else if (data.itemAttributes != null && data.itemAttributes.Count > 0)
            {
                string desc = "<color=#80C0FF><b>PROPRIEDADES EXTRAÍVEIS</b></color>\n\n";
                foreach(var buff in data.itemAttributes)
                {
                    string signal = buff.value > 0 ? "+" : "";
                    string tipoMultiplier = buff.isMultiplier ? "%" : "";
                    float displayVal = buff.isMultiplier ? Mathf.Round((buff.value - 1f) * 100f) : buff.value;
                    
                    desc += $"<color=#00FFAA><b>• {signal}{displayVal}{tipoMultiplier}</b></color>  <color=#EEEEEE>{FormatterName(buff.attributeType.ToString())}</color>\n";
                }
                itemStatsDescription.text = desc;
            }
            else
            {
                itemStatsDescription.text = "\n<color=#8888AA><i>Este item é um recurso de síntese.\nNão possui propriedades extraíveis para o traje.</i></color>";
            }
        }
        
        // Botão Infundir
        if (btnInfundir != null) 
        {
            btnInfundir.interactable = true;

            // Preview do fluxo padrão (Simples / Acumulado): sobrescreve descrição e texto do botão
            InfusionRequest request = infusionManager != null
                ? infusionManager.BuildInfusionRequest(selectedItemIds)
                : default;

            if (request.IsValid)
            {
                UpdateStandardInfusionTexts(request);
            }
            else
            {
                bool hasCapacity = infusionManager == null || infusionManager.HasInfusionCapacity(1);
                TextMeshProUGUI btnTxt = btnInfundir.GetComponentInChildren<TextMeshProUGUI>();
                if (!hasCapacity)
                {
                    btnInfundir.interactable = false;
                    if (btnTxt != null)
                        btnTxt.text = $"<b>TRAJE SOBRECARREGADO</b>\n<color=#FF5555><size=80%>CAPACIDADE MÁXIMA ATINGIDA</size></color>";
                }
                else
                {
                    if (btnTxt != null)
                        btnTxt.text = $"<b>INFUNDIR</b>\n<color=#FFD700><size=88%>-{data.infusionEssenceCost} ESSÊNCIAS</size></color>";
                }
            }

            UpdateButtonVisuals(btnInfundir);
        }
            
        // BotÃƒÂ£o Reciclar
        if (btnReciclar != null) 
        {
            btnReciclar.interactable = true;
            TextMeshProUGUI recTxt = btnReciclar.GetComponentInChildren<TextMeshProUGUI>();
            if (recTxt != null) 
                recTxt.text = "<b>DESCARTAR</b>\n<color=#FFAA77><size=75%>(JOGAR FORA)</size></color>";
            
            UpdateButtonVisuals(btnReciclar);
        }
    }

    private string FormatterName(string attribute)
    {
         if (attribute.ToLower().Contains("health")) return "Vida MÃƒÂ¡xima";
         if (attribute.ToLower().Contains("damage")) return "Poder de Dano";
         if (attribute.ToLower().Contains("speed")) return "Velocidade de Movimento";
         if (attribute.ToLower().Contains("armor")) return "Armadura";
         if (attribute.ToLower().Contains("critchance")) return "Chance de CrÃƒÂ­tico";
         if (attribute.ToLower().Contains("critmultiplier")) return "Dano CrÃƒÂ­tico";
         return attribute;
    }

    private void ClearSelection()
    {
        selectedItemId = "";
        selectedItemIds.Clear(); // Evita que uma seleção múltipla antiga "vaze" para o próximo clique
        currentTierColor = Color.white;

        foreach (var obj in spawnedIcons)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedIcons.Clear();
        
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }

        if (cardBgImage != null)
        {
            cardBgImage.color = CARD_SLOT_BG;
            Outline cardOutline = cardBgImage.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.effectColor = new Color(0.4f, 0.6f, 1f, 0.5f);
                cardOutline.effectDistance = new Vector2(2f, -2f);
            }
        }

        if (tierGlowImage != null) tierGlowImage.enabled = false;
        if (itemBorderImage != null) itemBorderImage.color = new Color(0.4f, 0.5f, 0.7f, 0.4f);
        
        if (itemTitle != null)
        {
            itemTitle.text = "ANALISADOR DE RELÃƒÂQUIAS";
            itemTitle.color = HEADER_ACCENT;
        }
        if (itemRarity != null) itemRarity.text = "";
        
        if (itemStatsDescription != null) 
            itemStatsDescription.text = "\n<color=#8888AA>CLIQUE EM UM ITEM DO SEU INVENTÃƒ RIO PARA ANALISAR SEUS PODERES OU RECICLÃƒ -LO.</color>";
        
        if (recycleValueText != null) recycleValueText.text = "";

        if (btnInfundir != null) { btnInfundir.interactable = false; UpdateButtonVisuals(btnInfundir); }
        if (btnReciclar != null) { btnReciclar.interactable = false; UpdateButtonVisuals(btnReciclar); }
    }

    /// <summary>
    /// Handler do botão INFUNDIR (público: também pode ser ligado pelo OnClick no Inspector).
    ///   • 1 item com 1 unidade        → Fluxo Simples   (buffs ×1)
    ///   • 1 item com 2+ unidades      → Fluxo Acumulado (consome 2, buffs ×2)
    ///   • Itens diferentes            → Fluxo de Sinergia (InfuseMultipleItems)
    /// Toda a regra (Essence, buffs, inflação, consumo) vive no InfusionManager.TryInfuse.
    /// </summary>
    public void OnBtnInfundirClicked() => OnInfuseButtonClicked();

    public void OnInfuseButtonClicked()
    {
        if (selectedItemIds == null || selectedItemIds.Count == 0)
        {
            ShowInfusionFeedback("Selecione um item para infundir.");
            return;
        }

        if (infusionManager == null) infusionManager = Object.FindFirstObjectByType<InfusionManager>();
        if (infusionManager == null)
        {
            Debug.LogWarning("[INFUSION UI] InfusionManager não encontrado! Botão Infundir ignorado.");
            return;
        }

        // Fluxo padrão: Simples / Acumulado
        if (infusionManager.IsStandardInfusion(selectedItemIds))
        {
            int cost = infusionManager.GetInfusionCost(selectedItemIds); // capturado antes, para a mensagem de erro
            InfusionResult result = infusionManager.TryInfuse(selectedItemIds);
            HandleInfusionResult(result, cost);
            return;
        }

        // Fluxo de Sinergia (combinação de itens diferentes)
        if (infusionManager.InfuseMultipleItems(selectedItemIds))
            PlayInfusionSuccessFX(false);
        else
        {
            if (!infusionManager.HasInfusionCapacity(1))
                ShowInfusionFeedback("Capacidade do traje esgotada! Melhore o traje no Robozinho.");
            else
                ShowInfusionFeedback("Infusão bloqueada! Essência insuficiente.");
        }
    }

    private void HandleInfusionResult(InfusionResult result, int cost)
    {
        switch (result)
        {
            case InfusionResult.Success:          PlayInfusionSuccessFX(false); break;
            case InfusionResult.Fused:            PlayInfusionSuccessFX(true);  break;
            case InfusionResult.NotEnoughEssence: ShowInfusionFeedback($"Essência insuficiente! Precisa de {cost}."); break;
            case InfusionResult.LegendaryLimit:   ShowInfusionFeedback("Lendário já infundido nesta run!"); break;
            case InfusionResult.CapacityLimit:    ShowInfusionFeedback("Capacidade do traje esgotada! Melhore o traje no Robozinho."); break;
            case InfusionResult.MissingItems:     ShowInfusionFeedback("Você não tem itens suficientes."); break;
            default:                              ShowInfusionFeedback("Não foi possível infundir."); break;
        }
    }

    private void PlayInfusionSuccessFX(bool fused)
    {
        // Dourado = Simples | Ciano = Acumulado (×2)
        Color fx = fused ? new Color(0f, 0.9f, 1f, 0.45f) : new Color(1f, 0.85f, 0f, 0.45f);
        StartCoroutine(ScreenFlash(fx));
        StartCoroutine(ShockwaveRingFX(new Color(fx.r, fx.g, fx.b, 0.8f)));
        ClearSelection();
        if (InventoryUI.Instance != null)
        {
            InventoryUI.Instance.ClearSelection();
        }
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.AdvanceQuest(QuestType.Interact, "infusion_completed", 1);
        }
        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso(fused ? "Fusao Concluida! Traje amplificado!" : "Infusao Concluida! Traje aprimorado com sucesso!");
        }
    }

    /// <summary>Feedback visual (popup do Eptinho + flash vermelho) e log de debug.</summary>
    private void ShowInfusionFeedback(string message)
    {
        Debug.Log("[INFUSION UI] " + message);
        if (EptinhoPopupController.instancia != null)
            EptinhoPopupController.instancia.MostrarPopupAviso(message);

        if (painelUpgrades != null && painelUpgrades.activeInHierarchy)
            StartCoroutine(ScreenFlash(new Color(1f, 0.2f, 0.25f, 0.30f)));
    }

    /// <summary>Atualiza descrição e botão com o preview exato do fluxo (custo, ×M, buffs escalados).</summary>
    private void UpdateStandardInfusionTexts(InfusionRequest request)
    {
        bool fused = request.mode == InfusionMode.Fused;
        int cost = infusionManager != null ? infusionManager.GetInfusionCost(request) : request.data.infusionEssenceCost;

        string buffPreview = BuildBuffPreview(request.data, request.stackMultiplier);
        if (infusionManager != null)
        {
            int curInf = infusionManager.CurrentInfusionCount;
            int maxCap = infusionManager.GetMaxInfusionCapacity();
            string capColor = curInf >= maxCap ? "#FF5555" : "#00FFAA";
            buffPreview += $"\n<size=85%><color=#8888AA>CAPACIDADE DO TRAJE:</color> <color={capColor}><b>{curInf}/{maxCap}</b></color></size>";
        }

        if (itemStatsDescription != null)
            itemStatsDescription.text = buffPreview;

        if (btnInfundir == null) return;
        TextMeshProUGUI txt = btnInfundir.GetComponentInChildren<TextMeshProUGUI>();
        if (txt == null) return;

        bool hasCapacity = infusionManager == null || infusionManager.HasInfusionCapacity(1);
        if (!hasCapacity)
        {
            btnInfundir.interactable = false;
            txt.text = $"<b>TRAJE SOBRECARREGADO</b>\n<color=#FF5555><size=80%>CAPACIDADE MÁXIMA ATINGIDA</size></color>";
            UpdateButtonVisuals(btnInfundir);
            return;
        }

        btnInfundir.interactable = true;
        string label = fused ? $"FUNDIR ×{request.stackMultiplier:0.##}" : "INFUNDIR";
        string units = fused ? $" <size=60%>(-{request.itemCount} un.)</size>" : "";
        string inflaTag = cost > request.data.infusionEssenceCost
            ? $" <size=60%><color=#FF6666>({request.data.infusionEssenceCost} base)</color></size>"
            : "";

        txt.text = $"<b>{label}</b>\n<color=#FFD700><size=88%>-{cost} ESSÊNCIAS</size></color>{units}{inflaTag}";
        UpdateButtonVisuals(btnInfundir);
    }

    /// <summary>Lista os buffs já escalados pelo mesmo ScaleBuff usado na aplicação real.</summary>
    private string BuildBuffPreview(ItemData data, float stackMultiplier)
    {
        if (data.itemAttributes == null || data.itemAttributes.Count == 0)
            return "\n<color=#8888AA><i>Este item é um recurso de síntese.\nNão possui propriedades extraíveis para o traje.</i></color>";

        // FOG OF WAR / CATÁLOGO DO EPTINHO: Atributos ocultos até a 1ª infusão
        bool isDecoded = CatalogoManager.instancia != null && CatalogoManager.instancia.IsItemInfused(data.itemId);
        if (!isDecoded)
        {
            return "<color=#FFCC00><b>[ ? ? ? ] PROPRIEDADES NÃO ANALISADAS</b></color>\n\n" +
                   "<color=#00FFAA>• [ ? ? ? ] Efeito Oculto</color>\n\n" +
                   "<size=85%><color=#8899AA><i>Este composto nunca foi infundido no traje.\n" +
                   "Infunda pela 1ª vez para que o Eptinho analise sua composição e revele seus atributos permanentemente no Catálogo!</i></color></size>";
        }

        string header = stackMultiplier > 1f ? $"PROPRIEDADES FUNDIDAS (×{stackMultiplier:0.##})" : "PROPRIEDADES EXTRAÍVEIS";
        System.Text.StringBuilder sb = new System.Text.StringBuilder($"<color=#80C0FF><b>{header}</b></color>\n\n");

        foreach (var baseBuff in data.itemAttributes)
        {
            ItemAttributeParam buff = InfusionManager.ScaleBuff(baseBuff, stackMultiplier);
            // Multiplicadores são fatores (1.10 = +10%)
            float displayVal = buff.isMultiplier ? (buff.value - 1f) * 100f : buff.value;
            string signal = displayVal > 0 ? "+" : "";
            string suffix = buff.isMultiplier ? "%" : "";
            sb.Append($"<color=#00FFAA><b>• {signal}{displayVal:0.##}{suffix}</b></color>  <color=#EEEEEE>{FormatterName(buff.attributeType.ToString())}</color>\n");
        }

        return sb.ToString();
    }
    private void OnBtnReciclarClicked()
    {
        if (string.IsNullOrEmpty(selectedItemId)) return;

        if (infusionManager == null)
            infusionManager = FindFirstObjectByType<InfusionManager>();

        if (infusionManager == null)
        {
            Debug.LogWarning("[INFUSION UI] InfusionManager não encontrado! Botão Reciclar ignorado.");
            return;
        }

        bool sucesso = infusionManager.RecycleItem(selectedItemId);
        if (sucesso)
        {
            StartCoroutine(ScreenFlash(new Color(1f, 0.7f, 0.0f, 0.45f))); // Flash Dourado Energético
            StartCoroutine(ShockwaveRingFX(new Color(1f, 0.7f, 0f, 0.8f)));
            ClearSelection(); 
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.ClearSelection();
            }
        }
    }

    // ==========================================
    // ANIMAÃƒâ€¡Ãƒâ€¢ES E INTERAÃƒâ€¡Ãƒâ€¢ES VISUAIS
    // ==========================================

    private void SetupPremiumButton(Button btn, float hoverScale = 1.08f)
    {
        if (btn == null) return;
        
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        
        EventTrigger.Entry entryEnter = new EventTrigger.Entry();
        entryEnter.eventID = EventTriggerType.PointerEnter;
        entryEnter.callback.AddListener((data) => { 
            if (btn.interactable) 
            {
                StartCoroutine(LerpScale(btn.transform, hoverScale)); 
                Outline o = btn.GetComponent<Outline>();
                if (o != null) o.effectDistance = new Vector2(4.5f, -4.5f);
            }
        });
        trigger.triggers.Add(entryEnter);

        EventTrigger.Entry entryExit = new EventTrigger.Entry();
        entryExit.eventID = EventTriggerType.PointerExit;
        entryExit.callback.AddListener((data) => { 
            StartCoroutine(LerpScale(btn.transform, 1.0f)); 
            Outline o = btn.GetComponent<Outline>();
            if (o != null) o.effectDistance = new Vector2(2f, -2f);
        });
        trigger.triggers.Add(entryExit);
    }

    private void UpdateButtonVisuals(Button btn)
    {
        CanvasGroup cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = btn.interactable ? 1f : 0.4f;
    }

    private IEnumerator LerpScale(Transform t, float targetScale)
    {
        Vector3 target = new Vector3(targetScale, targetScale, 1f);
        float speed = 14f;
        while (Vector3.Distance(t.localScale, target) > 0.003f)
        {
            t.localScale = Vector3.Lerp(t.localScale, target, Time.unscaledDeltaTime * speed);
            yield return null;
        }
        t.localScale = target;
    }
    
    private IEnumerator PulseEffect(Transform t, float peakScale, float duration)
    {
        Vector3 orig = Vector3.one;
        yield return LerpScale(t, peakScale);
        yield return LerpScale(t, orig.x);
    }

    private IEnumerator ShockwaveRingFX(Color ringColor)
    {
        if (itemIcon == null) yield break;

        GameObject shockObj = new GameObject("ShockwaveRing_Infusion");
        shockObj.transform.SetParent(itemIcon.transform.parent, false);
        shockObj.transform.SetAsLastSibling();
        shockObj.layer = painelUpgrades.layer;

        RectTransform rt = shockObj.AddComponent<RectTransform>();
        rt.anchorMin = itemIcon.rectTransform.anchorMin;
        rt.anchorMax = itemIcon.rectTransform.anchorMax;
        rt.pivot = itemIcon.rectTransform.pivot;
        rt.anchoredPosition = itemIcon.rectTransform.anchoredPosition;
        rt.sizeDelta = itemIcon.rectTransform.sizeDelta;

        Image img = shockObj.AddComponent<Image>();
        if (radialGlowSprite == null) GenerateRadialGlowSprite();
        img.sprite = radialGlowSprite;
        img.color = ringColor;
        img.raycastTarget = false;

        float duration = 0.35f;
        float elapsed = 0f;
        Vector2 startSize = itemIcon.rectTransform.sizeDelta;
        Vector2 endSize = startSize + new Vector2(100f, 100f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            rt.sizeDelta = Vector2.Lerp(startSize, endSize, t);
            img.color = new Color(ringColor.r, ringColor.g, ringColor.b, Mathf.Lerp(ringColor.a, 0f, t));
            yield return null;
        }

        Destroy(shockObj);
    }

    private IEnumerator ScreenFlash(Color flashColor)
    {
        GameObject flashObj = new GameObject("FlashOverlay_Infusion");
        flashObj.transform.SetParent(painelUpgrades.transform, false);
        flashObj.transform.SetAsLastSibling();
        
        Image img = flashObj.AddComponent<Image>();
        img.color = flashColor;
        img.raycastTarget = false;
        
        RectTransform rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = Vector2.zero;
        rt.offsetMin = Vector2.zero;

        float elapsed = 0f;
        float dur = 0.35f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, elapsed / dur);
            img.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }
        Destroy(flashObj);
    }
}





