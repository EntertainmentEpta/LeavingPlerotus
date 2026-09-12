using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Interface de usuário do sistema de Crafting.
/// Singleton — criado programaticamente (mesmo padrão do InventoryUI.cs).
///
/// LAYOUT:
///   ┌─────────────────────────────────────────────────┐
///   │  MESA DE TRABALHO                          [X]  │
///   ├────────────────────┬────────────────────────────┤
///   │  Lista de Receitas │  Detalhes da Receita       │
///   │  ┌──────────────┐  │  Nome: Expansão de Inv.    │
///   │  │ Receita 1  ✓ │  │  Ingredientes:             │
///   │  ├──────────────┤  │    5/5 Shard Splinter      │
///   │  │ Receita 2  ✗ │  │    3/3 Magic Dust          │
///   │  ├──────────────┤  │  Resultado:                │
///   │  │ Receita 3  ✗ │  │    +5 Slots de Inventário  │
///   │  └──────────────┘  │  [   CRAFTAR   ]           │
///   ├────────────────────┴────────────────────────────┤
///   │  MELHORIAS CRAFTADAS                            │
///   │  ┌────────────┐ ┌────────────┐                  │
///   │  │ Melhoria 1 │ │ Melhoria 2 │                  │
///   │  │ [Equipar]  │ │[Desequipar]│                  │
///   │  └────────────┘ └────────────┘                  │
///   └─────────────────────────────────────────────────┘
///
/// DEPENDÊNCIAS:
///   - CraftingManager.Instance  (lógica de craft)
///   - EquipmentManager.Instance (equipar/desequipar)
///   - SaveManager.instance      (leitura de baseResources)
///   - ItemDatabase.Instance     (ícones e nomes dos itens)
/// </summary>
public class CraftingUI : MonoBehaviour
{
    public static CraftingUI Instance { get; private set; }

    // Referências internas
    private Canvas craftingCanvas;
    private GameObject canvasObject;
    private GameObject panelObject;
    private RectTransform panelRect;

    // Seção de receitas
    private Transform recipesContainer;
    private List<RecipeSlotUI> recipeSlots = new List<RecipeSlotUI>();

    // Seção de detalhes
    private TextMeshProUGUI detailNameText;
    private TextMeshProUGUI detailDescText;
    private TextMeshProUGUI ingredientsText;
    private TextMeshProUGUI resultText;
    private Button craftButton;
    private TextMeshProUGUI craftButtonText;

    // Seção de equipamentos
    private Transform equipmentContainer;
    private List<GameObject> equipmentSlotObjects = new List<GameObject>();

    // Estado
    private bool isOpen = false;
    private bool uiBuilt = false;
    private CraftingRecipe selectedRecipe;
    private int currentTab = 0;

    public TMP_FontAsset customFont;

    // Tooltip flutuante para descrição de melhoria (Hover)
    private GameObject upgradeTooltipPanel;
    private TextMeshProUGUI tooltipNameText;
    private TextMeshProUGUI tooltipDescText;
    private TextMeshProUGUI tooltipEffectText;

    // Dimensões Fixas (Conforme solicitado)
    private const float PANEL_WIDTH = 1100f;
    private const float PANEL_HEIGHT = 900f;

    [Header("Configurações de Design (Ajustáveis em Tempo Real)")]
    [Range(30f, 200f)] [SerializeField] private float recipeSlotHeight = 76f;
    [Range(60f, 400f)] [SerializeField] private float equipmentSlotSize = 200f;

    [SerializeField] private Color panelBg = new Color(0.02f, 0.02f, 0.04f, 0.92f);
    [SerializeField] private Color panelBorder = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color sectionBg = new Color(0.04f, 0.04f, 0.07f, 0.90f);
    [SerializeField] private Color headerColor = new Color(0.85f, 0.80f, 0.95f, 1f);
    [SerializeField] private Color accentColor = new Color(0.6f, 0.45f, 0.90f, 1f);

    private static readonly Color BTN_CRAFT_ENABLED = new Color(0.25f, 0.70f, 0.30f, 1f);
    private static readonly Color BTN_CRAFT_DISABLED = new Color(0.3f, 0.3f, 0.3f, 0.6f);
    private static readonly Color BTN_EQUIP = new Color(0.3f, 0.55f, 0.85f, 1f);
    private static readonly Color BTN_UNEQUIP = new Color(0.7f, 0.35f, 0.35f, 1f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Carrega Oswald Bold SDF como fallback consistente se não definida no Inspector
        if (customFont == null)
        {
            customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        }
    }

    void Start()
    {
        CreateCraftingUI();
        panelObject.SetActive(false);
        uiBuilt = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && uiBuilt && canvasObject != null)
        {
            bool wasOpen = isOpen;
            Destroy(canvasObject);
            CreateCraftingUI();
            panelObject.SetActive(wasOpen);
            RefreshUI();
        }
    }
#endif

    void OnEnable()
    {
        CraftingManager.OnCraftCompleted += OnCraftCompleted;
        SaveManager.OnBaseResourcesChanged += RefreshUI;
        EquipmentManager.OnEquipmentStateChanged += RefreshEquipmentSection;
        SaveManager.OnEquipmentChanged += RefreshEquipmentSection;
    }

    void OnDisable()
    {
        CraftingManager.OnCraftCompleted -= OnCraftCompleted;
        SaveManager.OnBaseResourcesChanged -= RefreshUI;
        EquipmentManager.OnEquipmentStateChanged -= RefreshEquipmentSection;
        SaveManager.OnEquipmentChanged -= RefreshEquipmentSection;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (canvasObject != null) Destroy(canvasObject);
        }
    }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseCrafting();
        }

        // Atalho T para realizar Craft se a UI estiver aberta e a receita for craftável
        if (isOpen && Input.GetKeyDown(KeyCode.T))
        {
            if (selectedRecipe != null && CraftingManager.Instance != null)
            {
                if (CraftingManager.Instance.CanCraft(selectedRecipe))
                {
                    OnCraftButtonClicked();
                }
            }
        }

        // CHEAT DEBUG: Pressionando P com a UI aberta adiciona todos os recursos para testes rápidos de craft!
        if (isOpen && Input.GetKeyDown(KeyCode.P))
        {
            if (ItemDatabase.Instance != null && SaveManager.instance != null)
            {
                foreach (var item in ItemDatabase.Instance.allItems)
                {
                    if (item != null && item.returnsToBase)
                    {
                        SaveManager.instance.AddResourceToBase(item.itemId, 20);
                    }
                }
                RefreshUI();
                Debug.Log("[DEBUG CHEAT] Adicionados 20 de cada recurso de base!");
            }
        }
    }

    // ─── API PÚBLICA ─────────────────────────────────────────────────────────

    /// <summary>Abre a tela de crafting.</summary>
    public void OpenCrafting()
    {
        if (isOpen) return;
        isOpen = true;
        panelObject.SetActive(true);
        selectedRecipe = null;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshUI();
        Debug.Log("[CRAFTING] UI aberta");
    }

    /// <summary>Fecha a tela de crafting.</summary>
    public void CloseCrafting()
    {
        if (!isOpen) return;
        isOpen = false;
        panelObject.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Debug.Log("[CRAFTING] UI fechada");
    }

    /// <summary>Retorna se a UI está aberta.</summary>
    public bool IsOpen() => gameObject.activeInHierarchy && isOpen;

    // ─── REFRESH ─────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (!uiBuilt || CraftingManager.Instance == null) return;

        RefreshRecipeList();
        RefreshDetails();
        RefreshEquipmentSection();
    }

    private void RefreshRecipeList()
    {
        List<CraftingRecipe> allRecipes = CraftingManager.Instance.GetAllRecipes();
        List<CraftingRecipe> recipes = new List<CraftingRecipe>();
        
        foreach (var r in allRecipes)
        {
            bool isBase = r.resultEquipment != null && r.resultEquipment.isBaseUpgrade;
            if (currentTab == 0 && !isBase) recipes.Add(r);
            else if (currentTab == 1 && isBase) recipes.Add(r);
        }

        // Limpa slots antigos
        foreach (var slot in recipeSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        recipeSlots.Clear();

        // Cria novos slots
        foreach (var recipe in recipes)
        {
            GameObject slotObj = new GameObject("RecipeSlot_" + recipe.recipeId);
            slotObj.transform.SetParent(recipesContainer, false);

            RecipeSlotUI slot = slotObj.AddComponent<RecipeSlotUI>();
            slot.Initialize(OnRecipeSelected, 230f, recipeSlotHeight);

            bool canCraft = CraftingManager.Instance.CanCraft(recipe);
            slot.SetRecipe(recipe, canCraft);
            slot.SetSelected(selectedRecipe == recipe);

            recipeSlots.Add(slot);
        }
    }

    private void RefreshDetails()
    {
        if (selectedRecipe == null)
        {
            detailNameText.text = "Selecione uma receita";
            detailDescText.text = "";
            ingredientsText.text = "";
            resultText.text = "";
            craftButton.interactable = false;
            craftButtonText.text = "CRAFTAR";
            craftButton.GetComponent<Image>().color = BTN_CRAFT_DISABLED;
            return;
        }

        detailNameText.text = selectedRecipe.recipeName;
        detailDescText.text = selectedRecipe.description;

        // Ingredientes
        string ingText = "<b>Ingredientes:</b>\n";
        foreach (var ing in selectedRecipe.ingredients)
        {
            int available = CraftingManager.Instance.GetAvailableAmount(ing.itemId);
            string itemName = ing.itemId;

            // Tenta pegar nome bonito do ItemDatabase
            if (ItemDatabase.Instance != null)
            {
                ItemData itemData = ItemDatabase.Instance.GetItemData(ing.itemId);
                if (itemData != null)
                    itemName = itemData.itemName;
            }

            string color = available >= ing.quantity ? "#4AE04A" : "#E04A4A";
            ingText += $"  <color={color}>{available}/{ing.quantity}</color> {itemName}\n";
        }
        ingredientsText.text = ingText;

        // Resultado
        switch (selectedRecipe.resultType)
        {
            case CraftingResultType.Equipment:
                if (selectedRecipe.resultEquipment != null)
                {
                    var eq = selectedRecipe.resultEquipment;
                    resultText.text = $"<b>Resultado:</b>\n  {eq.equipmentName}\n  <color=#9B7FD4>{GetEffectDescription(eq)}</color>";
                }
                break;
            case CraftingResultType.Item:
                string resultName = selectedRecipe.resultItemId;
                if (ItemDatabase.Instance != null)
                {
                    ItemData rd = ItemDatabase.Instance.GetItemData(selectedRecipe.resultItemId);
                    if (rd != null) resultName = rd.itemName;
                }
                resultText.text = $"<b>Resultado:</b>\n  {resultName}";
                break;
        }

        // Botão de craft
        bool canCraft = CraftingManager.Instance.CanCraft(selectedRecipe);
        craftButton.interactable = canCraft;
        craftButton.GetComponent<Image>().color = canCraft ? BTN_CRAFT_ENABLED : BTN_CRAFT_DISABLED;
        craftButtonText.text = canCraft ? "CRAFTAR [T]" : "MATERIAIS INSUFICIENTES";
    }

    private void RefreshEquipmentSection()
    {
        if (!uiBuilt || EquipmentManager.Instance == null) return;

        // Limpa slots antigos
        foreach (var obj in equipmentSlotObjects)
        {
            if (obj != null) Destroy(obj);
        }
        equipmentSlotObjects.Clear();

        // Oculta o tooltip ao atualizar a lista para evitar que fique órfão
        ShowUpgradeTooltip(null, false, Vector2.zero);

        // Cria slots para cada equipamento craftado
        List<EquipmentData> owned = EquipmentManager.Instance.GetOwnedEquipment();
        owned.RemoveAll(e => e.isBaseUpgrade);

        foreach (var equip in owned)
        {
            GameObject slotObj = CreateEquipmentSlot(equip);
            slotObj.transform.SetParent(equipmentContainer, false);
            equipmentSlotObjects.Add(slotObj);

            // Adiciona detector de hover para abrir o painel de descrição detalhada
            UpgradeHoverHandler hover = slotObj.AddComponent<UpgradeHoverHandler>();
            hover.equipment = equip;
            hover.onHover = (eq, isHovering) =>
            {
                if (isHovering)
                {
                    // Converte a posição do slot para coordenadas locais do Canvas pai principal (panelRect)
                    Vector3 worldPos = slotObj.transform.position;
                    Vector2 localPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        panelRect,
                        RectTransformUtility.WorldToScreenPoint(null, worldPos),
                        null,
                        out localPos
                    );

                    // Posiciona o tooltip acima do slot centralizado
                    float slotHeight = 180f;
                    if (equipmentContainer.parent != null)
                    {
                        RectTransform parentRect = equipmentContainer.parent.GetComponent<RectTransform>();
                        if (parentRect != null) slotHeight = parentRect.rect.height;
                    }

                    Vector2 tooltipPos = new Vector2(localPos.x, localPos.y + slotHeight / 2f + 15f);
                    ShowUpgradeTooltip(eq, true, tooltipPos);
                }
                else
                {
                    ShowUpgradeTooltip(null, false, Vector2.zero);
                }
            };
        }
    }

    // ─── CALLBACKS ───────────────────────────────────────────────────────────

    private void OnRecipeSelected(CraftingRecipe recipe)
    {
        selectedRecipe = recipe;

        // Atualiza seleção visual
        foreach (var slot in recipeSlots)
        {
            slot.SetSelected(false);
        }

        // Encontra e marca o slot selecionado
        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetAllRecipes();
            for (int i = 0; i < recipes.Count && i < recipeSlots.Count; i++)
            {
                if (recipes[i] == recipe)
                {
                    recipeSlots[i].SetSelected(true);
                    break;
                }
            }
        }

        RefreshDetails();
    }

    private void OnCraftButtonClicked()
    {
        if (selectedRecipe == null || CraftingManager.Instance == null) return;

        if (CraftingManager.Instance.Craft(selectedRecipe))
        {
            Debug.Log($"[CRAFTING UI] Craft realizado: {selectedRecipe.recipeName}");
        }
    }

    private void OnCraftCompleted(CraftingRecipe recipe)
    {
        RefreshUI();
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────

    private string GetEffectDescription(EquipmentData equip)
    {
        switch (equip.effectType)
        {
            case EquipmentEffectType.InventorySlotExpansion:
                return $"+{equip.effectValue:0} Slots de Inventário";
            case EquipmentEffectType.MaxHealthBoost:
                return $"+{equip.effectValue:0} Vida Máxima";
            case EquipmentEffectType.MaxArmorBoost:
                return $"+{equip.effectValue:0} Armadura Máxima";
            case EquipmentEffectType.SpeedBoost:
                return $"×{equip.effectValue:F1} Velocidade";
            case EquipmentEffectType.DamageBoost:
                return $"×{equip.effectValue:F1} Dano Base";
            case EquipmentEffectType.CritChanceBoost:
                return $"+{equip.effectValue:0}% Chance Crítico";
            case EquipmentEffectType.ArmorRegenBoost:
                return $"+{equip.effectValue:F1} Regen. Armadura";
            default:
                return equip.effectValue.ToString();
        }
    }

    // ─── CRIAÇÃO DA UI ───────────────────────────────────────────────────────

    private void CreateCraftingUI()
    {
        // Canvas próprio persistente
        canvasObject = new GameObject("CraftingUI_Canvas");
        craftingCanvas = canvasObject.AddComponent<Canvas>();
        craftingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        craftingCanvas.sortingOrder = 110; // Acima do inventário
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObject);

        // Painel principal
        panelObject = new GameObject("CraftingPanel");
        panelObject.transform.SetParent(craftingCanvas.transform, false);

        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);

        Image mainPanelBg = panelObject.AddComponent<Image>();
        mainPanelBg.color = panelBg;
        mainPanelBg.raycastTarget = true;

        // Efeito de sombra (Drop Shadow) para dar profundidade de vidro suspenso
        Shadow panelShadow = panelObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        panelShadow.effectDistance = new Vector2(6f, -6f);

        // Acento reflexivo superior de vidro (Glass Highlight)
        GameObject glassHighlightObj = new GameObject("GlassHighlight");
        glassHighlightObj.transform.SetParent(panelObject.transform, false);
        glassHighlightObj.layer = panelObject.layer;

        RectTransform glassHighlightRect = glassHighlightObj.AddComponent<RectTransform>();
        glassHighlightRect.anchorMin = new Vector2(0f, 1f);
        glassHighlightRect.anchorMax = new Vector2(1f, 1f);
        glassHighlightRect.pivot = new Vector2(0.5f, 1f);
        glassHighlightRect.anchoredPosition = new Vector2(0f, -2f);
        glassHighlightRect.sizeDelta = new Vector2(-4f, 2f);

        glassHighlightObj.AddComponent<CanvasRenderer>();
        Image glassHighlightImg = glassHighlightObj.AddComponent<Image>();
        glassHighlightImg.color = new Color(1f, 1f, 1f, 0.12f); // reflexo superior fino
        glassHighlightImg.raycastTarget = false;

        // Borda do painel
        CreateBorder(panelObject.transform);

        // Acento superior
        CreateTopAccent(panelObject.transform);

        // Header
        CreateHeader(panelObject.transform);

        // Área de conteúdo dividida em duas colunas
        CreateRecipeListSection(panelObject.transform);
        CreateDetailSection(panelObject.transform);

        // Seção de equipamentos (abaixo)
        CreateEquipmentSection(panelObject.transform);

        // Botão de fechar
        CreateCloseButton(panelObject.transform);

        // Botão de Cheats (Recursos Infinitos)
        CreateCheatButton(panelObject.transform);

        // Tooltip flutuante de melhorias (por cima de tudo)
        CreateUpgradeTooltip(panelObject.transform);

        Debug.Log("[CRAFTING] UI criada.");
    }

    private void CreateBorder(Transform parent)
    {
        GameObject borderObj = new GameObject("PanelBorder");
        borderObj.transform.SetParent(parent, false);
        RectTransform r = borderObj.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = new Vector2(4f, 4f);
        borderObj.AddComponent<CanvasRenderer>();
        Image img = borderObj.AddComponent<Image>();
        img.color = panelBorder;
        img.type = Image.Type.Sliced;
        img.fillCenter = false;
        img.raycastTarget = false;
    }

    private void CreateTopAccent(Transform parent)
    {
        GameObject obj = new GameObject("TopAccent");
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.05f, 1f);
        r.anchorMax = new Vector2(0.95f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(0f, 3f);
        obj.AddComponent<CanvasRenderer>();
        Image img = obj.AddComponent<Image>();
        img.color = accentColor;
        img.raycastTarget = false;
    }

    private void CreateHeader(Transform parent)
    {
        GameObject obj = new GameObject("Header");
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, -6f);
        r.sizeDelta = new Vector2(-40f, 36f);
        obj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI txt = obj.AddComponent<TextMeshProUGUI>();
        txt.text = "MESA DE TRABALHO";
        txt.fontSize = 22f; // Revertido para o padrão limpo e compacto
        txt.fontStyle = FontStyles.Bold;
        txt.color = headerColor;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        if (customFont != null) txt.font = customFont;
    }

    private void CreateCheatButton(Transform parent)
    {
        GameObject btnObj = new GameObject("CheatButton");
        btnObj.transform.SetParent(parent, false);

        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(16f, -10f);
        r.sizeDelta = new Vector2(180f, 30f);

        btnObj.AddComponent<CanvasRenderer>();
        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.20f, 0.55f, 0.30f, 0.90f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (DevCheatConsole.Instance != null)
            {
                DevCheatConsole.Instance.GiveAllResources(999);
            }
            else
            {
                // Fallback direto
                if (SaveManager.instance != null && ItemDatabase.Instance != null)
                {
                    foreach (var item in ItemDatabase.Instance.allItems)
                    {
                        if (item != null) SaveManager.instance.AddResourceToBase(item.itemId, 999);
                    }
                }
            }
            RefreshUI();
        });

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tr = txtObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;

        txtObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = "🎁 CHEAT: +999 RECURSOS";
        txt.fontSize = 11f;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void CreateRecipeListSection(Transform parent)
    {
        // Container da lista de receitas (lado esquerdo)
        GameObject listPanel = new GameObject("RecipeListPanel");
        listPanel.transform.SetParent(parent, false);
        RectTransform r = listPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0.28f);
        r.anchorMax = new Vector2(0.38f, 0.92f);
        r.offsetMin = new Vector2(12f, 0f);
        r.offsetMax = new Vector2(0f, -8f);

        listPanel.AddComponent<CanvasRenderer>();
        Image bg = listPanel.AddComponent<Image>();
        bg.color = sectionBg;
        bg.raycastTarget = true;

        // Label
        GameObject labelObj = new GameObject("RecipesLabel");
        labelObj.transform.SetParent(listPanel.transform, false);
        RectTransform lr = labelObj.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(0f, 22f);
        lr.anchoredPosition = new Vector2(0f, -2f);
        labelObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "RECEITAS";
        labelText.fontSize = 14f; // Revertido para o padrão limpo e compacto
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = accentColor;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;
        if (customFont != null) labelText.font = customFont;

        // ScrollView para as receitas
        GameObject scrollObj = new GameObject("RecipeScroll");
        scrollObj.transform.SetParent(listPanel.transform, false);
        RectTransform sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.offsetMin = new Vector2(4f, 4f);
        sr.offsetMax = new Vector2(-4f, -26f);

        scrollObj.AddComponent<CanvasRenderer>();
        Image scrollMask = scrollObj.AddComponent<Image>();
        scrollMask.color = new Color(0, 0, 0, 0.01f);
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject contentObj = new GameObject("RecipeContent");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect
        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = cr;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        recipesContainer = contentObj.transform;
    }

    private void CreateDetailSection(Transform parent)
    {
        // Container dos detalhes (lado direito)
        GameObject detailPanel = new GameObject("DetailPanel");
        detailPanel.transform.SetParent(parent, false);
        RectTransform r = detailPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.40f, 0.28f);
        r.anchorMax = new Vector2(1f, 0.92f);
        r.offsetMin = new Vector2(0f, 0f);
        r.offsetMax = new Vector2(-12f, -8f);

        detailPanel.AddComponent<CanvasRenderer>();
        Image bg = detailPanel.AddComponent<Image>();
        bg.color = sectionBg;
        bg.raycastTarget = false;

        // Nome da receita (Extra Grande: 32f)
        detailNameText = CreateTextElement(detailPanel.transform, "DetailName",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -15f), new Vector2(-16f, 38f), 32f, FontStyles.Bold, headerColor);

        // Descrição (Extra Grande: 22f)
        detailDescText = CreateTextElement(detailPanel.transform, "DetailDesc",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(-16f, 48f), 22f, FontStyles.Italic,
            new Color(0.7f, 0.68f, 0.78f, 1f));

        // Ingredientes (Extra Grande: 24f)
        ingredientsText = CreateTextElement(detailPanel.transform, "Ingredients",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -115f), new Vector2(-16f, 180f), 24f, FontStyles.Normal,
            new Color(0.85f, 0.83f, 0.92f, 1f));
        ingredientsText.richText = true;

        // Resultado (Extra Grande: 24f)
        resultText = CreateTextElement(detailPanel.transform, "Result",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -310f), new Vector2(-16f, 80f), 24f, FontStyles.Normal,
            new Color(0.85f, 0.83f, 0.92f, 1f));
        resultText.richText = true;

        // Botão de Craftar (Ajustado para tamanho 50f)
        GameObject btnObj = new GameObject("CraftButton");
        btnObj.transform.SetParent(detailPanel.transform, false);
        RectTransform br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.1f, 0f);
        br.anchorMax = new Vector2(0.9f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 16f);
        br.sizeDelta = new Vector2(0f, 50f);

        btnObj.AddComponent<CanvasRenderer>();
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = BTN_CRAFT_DISABLED;
        btnBg.raycastTarget = true;

        craftButton = btnObj.AddComponent<Button>();
        craftButton.onClick.AddListener(OnCraftButtonClicked);
        craftButton.interactable = false;

        // Texto do botão (Extra Grande: 28f)
        GameObject btnTextObj = new GameObject("CraftBtnText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btr = btnTextObj.AddComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.sizeDelta = Vector2.zero;
        btnTextObj.AddComponent<CanvasRenderer>();
        craftButtonText = btnTextObj.AddComponent<TextMeshProUGUI>();
        craftButtonText.text = "CRAFTAR";
        craftButtonText.fontSize = 28f; 
        craftButtonText.fontStyle = FontStyles.Bold;
        craftButtonText.color = Color.white;
        craftButtonText.alignment = TextAlignmentOptions.Center;
        craftButtonText.raycastTarget = false;
        if (customFont != null) craftButtonText.font = customFont;

        // Inicializa com "selecione uma receita"
        detailNameText.text = "Selecione uma receita";
        detailDescText.text = "";
        ingredientsText.text = "";
        resultText.text = "";
    }

    private void CreateEquipmentSection(Transform parent)
    {
        // Seção de melhorias (parte inferior)
        GameObject equipPanel = new GameObject("EquipmentPanel");
        equipPanel.transform.SetParent(parent, false);
        RectTransform r = equipPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.26f);
        r.offsetMin = new Vector2(12f, 8f);
        r.offsetMax = new Vector2(-12f, -2f);

        equipPanel.AddComponent<CanvasRenderer>();
        Image bg = equipPanel.AddComponent<Image>();
        bg.color = sectionBg;
        bg.raycastTarget = false;

        // Label
        GameObject labelObj = new GameObject("EquipmentLabel");
        labelObj.transform.SetParent(equipPanel.transform, false);
        RectTransform lr = labelObj.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(0f, 20f);
        lr.anchoredPosition = new Vector2(0f, -2f);
        labelObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "MELHORIAS CRAFTADAS";
        label.fontSize = 14f; // Revertido para o padrão limpo e compacto
        label.fontStyle = FontStyles.Bold;
        label.color = accentColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (customFont != null) label.font = customFont;

        // ScrollView horizontal para equipamentos
        GameObject scrollObj = new GameObject("EquipScroll");
        scrollObj.transform.SetParent(equipPanel.transform, false);
        RectTransform sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.offsetMin = new Vector2(4f, 4f);
        sr.offsetMax = new Vector2(-4f, -24f);

        scrollObj.AddComponent<CanvasRenderer>();
        Image scrollMask = scrollObj.AddComponent<Image>();
        scrollMask.color = new Color(0, 0, 0, 0.01f);
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObj = new GameObject("EquipContent");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 0f);
        cr.anchorMax = new Vector2(0f, 1f);
        cr.pivot = new Vector2(0f, 0.5f);
        cr.sizeDelta = new Vector2(0f, 0f);

        HorizontalLayoutGroup hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(4, 4, 4, 4);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = false;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = cr;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        equipmentContainer = contentObj.transform;
    }

    private GameObject CreateEquipmentSlot(EquipmentData equip)
    {
        bool isEquipped = EquipmentManager.Instance.IsEquipped(equip.equipmentId);

        GameObject slotObj = new GameObject("EquipSlot_" + equip.equipmentId);
        RectTransform r = slotObj.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(equipmentSlotSize, 0f);

        slotObj.AddComponent<CanvasRenderer>();
        Image bg = slotObj.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        bg.raycastTarget = true; // Necessário para detectar o PointerEnter/Exit do hover

        // Borda interna do slot de melhoria para ficar mais premium
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(slotObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderObj.AddComponent<CanvasRenderer>();
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(1f, 1f, 1f, 0.08f);
        borderImg.type = Image.Type.Sliced;
        borderImg.fillCenter = false;
        borderImg.raycastTarget = false;

        float nameOffsetLeft = 6f;

        // Ícone da melhoria se disponível
        if (equip.icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.06f, 0.60f);
            iconRect.anchorMax = new Vector2(0.24f, 0.92f);
            iconRect.sizeDelta = Vector2.zero;
            
            iconObj.AddComponent<CanvasRenderer>();
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = equip.icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            nameOffsetLeft = 56f;
        }

        // Nome da Melhoria (Revertido para 13f limpo)
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(slotObj.transform, false);
        RectTransform nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 0.60f);
        nr.anchorMax = new Vector2(1f, 0.92f);
        nr.sizeDelta = Vector2.zero;
        nr.offsetMin = new Vector2(nameOffsetLeft, 0f);
        nr.offsetMax = new Vector2(-6f, 0f);
        nameObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = equip.equipmentName;
        nameText.fontSize = 13f; 
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(0.9f, 0.88f, 0.95f);
        nameText.alignment = (equip.icon != null) ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        nameText.raycastTarget = false;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) nameText.font = customFont;

        // Efeito (Revertido para 11f limpo)
        GameObject effectObj = new GameObject("Effect");
        effectObj.transform.SetParent(slotObj.transform, false);
        RectTransform er = effectObj.AddComponent<RectTransform>();
        er.anchorMin = new Vector2(0.06f, 0.30f);
        er.anchorMax = new Vector2(0.94f, 0.55f);
        er.sizeDelta = Vector2.zero;
        effectObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI effectText = effectObj.AddComponent<TextMeshProUGUI>();
        effectText.text = GetEffectDescription(equip);
        effectText.fontSize = 11f; 
        effectText.color = new Color(0.4f, 0.85f, 0.4f);
        effectText.alignment = TextAlignmentOptions.Center;
        effectText.raycastTarget = false;
        effectText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) effectText.font = customFont;

        // Botão Equipar/Desequipar
        GameObject btnObj = new GameObject("EquipBtn");
        btnObj.transform.SetParent(slotObj.transform, false);
        RectTransform br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.1f, 0.05f);
        br.anchorMax = new Vector2(0.9f, 0.25f);
        br.sizeDelta = Vector2.zero;

        btnObj.AddComponent<CanvasRenderer>();
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = isEquipped ? BTN_UNEQUIP : BTN_EQUIP;
        btnBg.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        string eqId = equip.equipmentId;
        btn.onClick.AddListener(() =>
        {
            if (EquipmentManager.Instance == null) return;
            if (EquipmentManager.Instance.IsEquipped(eqId))
                EquipmentManager.Instance.Unequip(eqId);
            else
                EquipmentManager.Instance.Equip(eqId);
        });

        // Texto do botão (Revertido para 12f limpo)
        GameObject btnTextObj = new GameObject("BtnText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btr = btnTextObj.AddComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.sizeDelta = Vector2.zero;
        btnTextObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = isEquipped ? "DESEQUIPAR" : "EQUIPAR";
        btnText.fontSize = 12f; 
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.raycastTarget = false;
        if (customFont != null) btnText.font = customFont;

        return slotObj;
    }

    private void CreateCloseButton(Transform parent)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);
        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(1f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -8f);
        r.sizeDelta = new Vector2(28f, 28f);

        btnObj.AddComponent<CanvasRenderer>();
        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.8f, 0.2f, 0.2f, 0.7f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(CloseCrafting);

        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);
        colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        btn.colors = colors;

        GameObject xObj = new GameObject("X");
        xObj.transform.SetParent(btnObj.transform, false);
        RectTransform xr = xObj.AddComponent<RectTransform>();
        xr.anchorMin = Vector2.zero;
        xr.anchorMax = Vector2.one;
        xr.sizeDelta = Vector2.zero;
        xObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI x = xObj.AddComponent<TextMeshProUGUI>();
        x.text = "X";
        x.fontSize = 18f; // Revertido para o padrão limpo e compacto
        x.color = Color.white;
        x.alignment = TextAlignmentOptions.Center;
        x.raycastTarget = false;
        if (customFont != null) x.font = customFont;
    }

    private TextMeshProUGUI CreateTextElement(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        float fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = anchoredPos;
        r.sizeDelta = sizeDelta;
        r.offsetMin = new Vector2(12f, r.offsetMin.y);
        r.offsetMax = new Vector2(-12f, r.offsetMax.y);
        obj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI txt = obj.AddComponent<TextMeshProUGUI>();
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAlignmentOptions.TopLeft;
        txt.raycastTarget = false;
        txt.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) txt.font = customFont;
        return txt;
    }

    // ─── TOOLTIP DE UPGRADES (HOVER) ──────────────────────────────────────────

    private void CreateUpgradeTooltip(Transform parent)
    {
        upgradeTooltipPanel = new GameObject("UpgradeTooltip");
        upgradeTooltipPanel.transform.SetParent(parent, false);
        upgradeTooltipPanel.layer = parent.gameObject.layer;

        RectTransform r = upgradeTooltipPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0f); // Pivô inferior central (ficará acima do slot)
        r.sizeDelta = new Vector2(600f, 300f); // Aumentado em 2x exatas (anteriormente 300f, 150f)

        upgradeTooltipPanel.AddComponent<CanvasRenderer>();
        Image bg = upgradeTooltipPanel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.04f, 0.98f); // Vidro escuro opaco
        bg.raycastTarget = false;

        // Borda reflexiva fina
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(upgradeTooltipPanel.transform, false);
        RectTransform br = borderObj.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = new Vector2(2f, 2f);
        borderObj.AddComponent<CanvasRenderer>();
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.6f, 0.45f, 0.90f, 0.60f); // Borda neon roxa
        borderImg.type = Image.Type.Sliced;
        borderImg.fillCenter = false;
        borderImg.raycastTarget = false;

        // Nome da melhoria no Tooltip (Extra Grande: 30f - 2x exatas)
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(upgradeTooltipPanel.transform, false);
        RectTransform nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 0.76f);
        nr.anchorMax = new Vector2(1f, 0.94f);
        nr.sizeDelta = Vector2.zero;
        nr.offsetMin = new Vector2(20f, 0f);
        nr.offsetMax = new Vector2(-20f, 0f);
        nameObj.AddComponent<CanvasRenderer>();
        tooltipNameText = nameObj.AddComponent<TextMeshProUGUI>();
        tooltipNameText.fontSize = 30f;
        tooltipNameText.fontStyle = FontStyles.Bold;
        tooltipNameText.color = new Color(0.9f, 0.88f, 0.95f);
        tooltipNameText.alignment = TextAlignmentOptions.BottomLeft;
        tooltipNameText.raycastTarget = false;
        if (customFont != null) tooltipNameText.font = customFont;

        // Linha divisória
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(upgradeTooltipPanel.transform, false);
        RectTransform lr = lineObj.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.03f, 0.74f);
        lr.anchorMax = new Vector2(0.97f, 0.74f);
        lr.sizeDelta = new Vector2(0f, 1f);
        lineObj.AddComponent<CanvasRenderer>();
        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.15f);
        lineImg.raycastTarget = false;

        // Descrição detalhada da melhoria (Extra Grande: 24f - 2x exatas)
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(upgradeTooltipPanel.transform, false);
        RectTransform dr = descObj.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0f, 0.32f);
        dr.anchorMax = new Vector2(1f, 0.70f);
        dr.sizeDelta = Vector2.zero;
        dr.offsetMin = new Vector2(20f, 0f);
        dr.offsetMax = new Vector2(-20f, 0f);
        descObj.AddComponent<CanvasRenderer>();
        tooltipDescText = descObj.AddComponent<TextMeshProUGUI>();
        tooltipDescText.fontSize = 24f;
        tooltipDescText.fontStyle = FontStyles.Italic;
        tooltipDescText.color = new Color(0.75f, 0.73f, 0.85f);
        tooltipDescText.alignment = TextAlignmentOptions.TopLeft;
        tooltipDescText.raycastTarget = false;
        tooltipDescText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) tooltipDescText.font = customFont;

        // Efeito da melhoria (Extra Grande: 26f - 2x exatas)
        GameObject effectObj = new GameObject("Effect");
        effectObj.transform.SetParent(upgradeTooltipPanel.transform, false);
        RectTransform er = effectObj.AddComponent<RectTransform>();
        er.anchorMin = new Vector2(0f, 0.06f);
        er.anchorMax = new Vector2(1f, 0.28f);
        er.sizeDelta = Vector2.zero;
        er.offsetMin = new Vector2(20f, 0f);
        er.offsetMax = new Vector2(-20f, 0f);
        effectObj.AddComponent<CanvasRenderer>();
        tooltipEffectText = effectObj.AddComponent<TextMeshProUGUI>();
        tooltipEffectText.fontSize = 26f;
        tooltipEffectText.color = new Color(0.4f, 0.85f, 0.4f);
        tooltipEffectText.alignment = TextAlignmentOptions.MidlineLeft;
        tooltipEffectText.raycastTarget = false;
        if (customFont != null) tooltipEffectText.font = customFont;

        upgradeTooltipPanel.SetActive(false);
    }

    private void ShowUpgradeTooltip(EquipmentData equip, bool show, Vector2 localPos)
    {
        if (upgradeTooltipPanel == null) return;

        if (show && equip != null)
        {
            tooltipNameText.text = equip.equipmentName;
            tooltipDescText.text = equip.description;
            tooltipEffectText.text = GetEffectDescription(equip);

            RectTransform r = upgradeTooltipPanel.GetComponent<RectTransform>();
            r.anchoredPosition = localPos;
            upgradeTooltipPanel.SetActive(true);
        }
        else
        {
            upgradeTooltipPanel.SetActive(false);
        }
    }
}

// ─── CLASSE AUXILIAR DETECTOR DE HOVER ────────────────────────────────────────

public class UpgradeHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public EquipmentData equipment;
    public System.Action<EquipmentData, bool> onHover;

    public void OnPointerEnter(PointerEventData eventData)
    {
        onHover?.Invoke(equipment, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHover?.Invoke(equipment, false);
    }
}

