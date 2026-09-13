using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Controla o menu do Oráculo Eptinho.
/// Interface completamente gerada por código, estilo dark-glass (mesma paleta que SyntheticBagUI).
/// Abre via EptinhoOracleInteract (trigger F perto do Eptin) ou via tecla I.
/// </summary>
public class EptinhoMenuController : MonoBehaviour
{
    public static EptinhoMenuController instancia;

    // ─── Configuração do Inspector (todos opcionais) ──────────────────────────
    [Header("UI Gerada por Código (auto-configurado)")]
    public GameObject menuUI; // Será criado automaticamente se nulo
    public GameObject HUDCanvas; // Referência ao HUD para esconder ao abrir o menu

    // ─── Constantes Visuais ───────────────────────────────────────────────────
    private static readonly Color PANEL_BG     = new Color(0.04f, 0.04f, 0.07f, 0.92f);
    private static readonly Color PANEL_BORDER = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color HEADER_COLOR = new Color(0.85f, 0.80f, 0.95f, 1.00f);
    private static readonly Color TAB_ACTIVE   = new Color(0.55f, 0.35f, 0.90f, 1.00f);
    private static readonly Color TAB_INACTIVE = new Color(0.20f, 0.18f, 0.28f, 1.00f);
    private static readonly Color CARD_BG      = new Color(0.10f, 0.08f, 0.16f, 0.95f);
    private static readonly Color CARD_BORDER  = new Color(0.55f, 0.35f, 0.90f, 0.40f);
    private static readonly Color TEXT_DIM     = new Color(0.65f, 0.60f, 0.80f, 1.00f);

    // ─── Referências Internas ─────────────────────────────────────────────────
    private Canvas      menuCanvas;
    private GameObject  canvasObj;
    private GameObject  panelObj;
    private GameObject  tabBestiary;
    private GameObject  tabCatalogo;
    private GameObject  tabSinergia;
    private GameObject  contentBestiary;
    private GameObject  contentCatalogo;
    private GameObject  contentSinergia;
    private int         currentTab = 0; // 0 = Bestiary, 1 = Catalogo, 2 = Sinergia
    private bool        isOpen = false;
    private bool        uiBuilt = false;
    private TMP_FontAsset customFont;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (instancia == null)
        {
            instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Garante que o EptinhoController exista neste GameObject para spawnar o Eptinho físico
        if (GetComponent<EptinhoController>() == null)
        {
            gameObject.AddComponent<EptinhoController>();
        }

        if (customFont == null)
        {
            customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        }
    }

    void Start()
    {
        BuildUI();
    }

    void Update()
    {
        // O menu do Eptinho agora abre apenas ao interagir diretamente com ele no mundo (Interação F).
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            FecharMenu();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API Pública
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsOpen() => gameObject.activeInHierarchy && isOpen;

    public void AbrirMenu()
    {
        if (!uiBuilt) BuildUI();
        if (isOpen) return;

        isOpen = true;
        panelObj.SetActive(true);
        if (HUDCanvas != null) HUDCanvas.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshCurrentTab();
        Debug.Log("[EPTINHO MENU] Aberto.");
    }

    public void FecharMenu()
    {
        if (!isOpen) return;
        isOpen = false;
        panelObj.SetActive(false);
        if (HUDCanvas != null) HUDCanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[EPTINHO MENU] Fechado.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Construção da UI
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        if (uiBuilt) return;

        // Canvas root
        canvasObj = new GameObject("EptinhoMenu_Canvas");
        DontDestroyOnLoad(canvasObj);
        menuCanvas = canvasObj.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Panel principal (780x620)
        panelObj = CreatePanel(canvasObj.transform, "OraclePanel", new Vector2(780f, 620f), PANEL_BG);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        // ── Header ──
        CreateLabel(panelObj.transform, "Header", "EPTINHO  ·  ORÁCULO",
            new Vector2(0f, 270f), 26, HEADER_COLOR, true);

        // Ícone do Eptinho no header
        Sprite eptonhoSprite = Resources.Load<Sprite>("EPTONHO");
        if (eptonhoSprite != null)
        {
            GameObject iconGO = new GameObject("EptonhoIcon");
            iconGO.transform.SetParent(panelObj.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = eptonhoSprite;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(40f, 40f);
            iconRect.anchoredPosition = new Vector2(-280f, 270f);
        }

        // Linha divisória
        GameObject line = new GameObject("Line");
        line.transform.SetParent(panelObj.transform, false);
        Image lineImg = line.AddComponent<Image>();
        lineImg.color = PANEL_BORDER;
        RectTransform lineRect = line.GetComponent<RectTransform>();
        lineRect.sizeDelta = new Vector2(700f, 1f);
        lineRect.anchoredPosition = new Vector2(0f, 245f);

        // ── Botão Fechar ──
        CreateButton(panelObj.transform, "BtnClose", "X", new Vector2(355f, 270f),
            new Vector2(40f, 40f), new Color(0.8f, 0.3f, 0.3f, 0.8f), () => FecharMenu());

        // ── Abas ──
        float tabY = 215f;
        tabBestiary = CreateButton(panelObj.transform, "TabBestiary", "BESTIÁRIO",
            new Vector2(-220f, tabY), new Vector2(200f, 40f), TAB_ACTIVE, () => MostrarAba(0));

        tabCatalogo = CreateButton(panelObj.transform, "TabCatalogo", "CATÁLOGO DE ITENS",
            new Vector2(0f, tabY), new Vector2(200f, 40f), TAB_INACTIVE, () => MostrarAba(1));

        tabSinergia = CreateButton(panelObj.transform, "TabSinergia", "MAPA DE SINERGIAS",
            new Vector2(220f, tabY), new Vector2(200f, 40f), TAB_INACTIVE, () => MostrarAba(2));

        // ── Área de conteúdo ──
        contentBestiary = CreateScrollArea(panelObj.transform, "ContentBestiary",
            new Vector2(0f, -30f), new Vector2(720f, 430f));

        contentCatalogo = CreateScrollArea(panelObj.transform, "ContentCatalogo",
            new Vector2(0f, -30f), new Vector2(720f, 430f));
        contentCatalogo.SetActive(false);

        // Painel do Mapa de Sinergias
        contentSinergia = CreatePanel(panelObj.transform, "ContentSinergia", new Vector2(720f, 430f), new Color(0,0,0,0));
        RectTransform sinRect = contentSinergia.GetComponent<RectTransform>();
        sinRect.anchoredPosition = new Vector2(0f, -30f);
        contentSinergia.AddComponent<SynergyMapBuilder>();
        contentSinergia.SetActive(false);

        menuUI = panelObj;
        panelObj.SetActive(false);
        uiBuilt = true;
    }

    private void MostrarAba(int abaIndex)
    {
        currentTab = abaIndex;
        contentBestiary.SetActive(abaIndex == 0);
        contentCatalogo.SetActive(abaIndex == 1);
        contentSinergia.SetActive(abaIndex == 2);

        SetButtonColor(tabBestiary, abaIndex == 0 ? TAB_ACTIVE : TAB_INACTIVE);
        SetButtonColor(tabCatalogo, abaIndex == 1 ? TAB_ACTIVE : TAB_INACTIVE);
        SetButtonColor(tabSinergia, abaIndex == 2 ? TAB_ACTIVE : TAB_INACTIVE);

        RefreshCurrentTab();
    }

    private void RefreshCurrentTab()
    {
        // Verifica se a tab de Sinergia está desbloqueada
        bool hasSynergyMap = false;
        if (SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_mapa_sinergias"))
        {
            hasSynergyMap = true;
        }

        if (hasSynergyMap)
        {
            tabSinergia.SetActive(true);
        }
        else
        {
            tabSinergia.SetActive(false);
            if (currentTab == 2) MostrarAba(0);
        }

        if (currentTab == 0) RefreshBestiary();
        else if (currentTab == 1) RefreshCatalogo();
    }

    private void RefreshBestiary()
    {
        Transform grid = contentBestiary.transform.Find("Viewport/Content");
        if (grid == null)
        {
            Debug.LogError("[EPTINHO MENU] Viewport/Content nao encontrado em ContentBestiary!");
            return;
        }

        foreach (Transform child in grid) Destroy(child.gameObject);

        if (BestiarioManager.instancia == null)
        {
            Debug.LogError("[EPTINHO MENU] BestiarioManager.instancia esta nulo!");
            return;
        }

        Debug.Log($"[EPTINHO MENU] Atualizando bestiario. Encontrados no save/memoria: {BestiarioManager.instancia.inimigosEncontrados.Count} inimigos.");

        if (BestiarioManager.instancia.inimigosEncontrados.Count == 0)
        {
            CreateLabel(grid, "Empty", "Nenhum inimigo catalogado ainda...",
                Vector2.zero, 18, TEXT_DIM, false);
            return;
        }

        foreach (EnemyData inimigo in BestiarioManager.instancia.inimigosEncontrados)
        {
            if (inimigo == null) continue;
            CreateEnemyCard(grid, inimigo);
        }
    }

    private void RefreshCatalogo()
    {
        Transform grid = contentCatalogo.transform.Find("Viewport/Content");
        if (grid == null)
        {
            Debug.LogError("[EPTINHO MENU] Viewport/Content nao encontrado em ContentCatalogo!");
            return;
        }

        foreach (Transform child in grid) Destroy(child.gameObject);

        if (CatalogoManager.instancia == null) return;

        if (CatalogoManager.instancia.itensCatalogados.Count == 0)
        {
            CreateLabel(grid, "Empty", "Nenhum item catalogado ainda...",
                Vector2.zero, 18, TEXT_DIM, false);
            return;
        }

        foreach (ItemData item in CatalogoManager.instancia.itensCatalogados)
        {
            if (item == null) continue;
            CreateItemCard(grid, item);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Criação de Cards
    // ─────────────────────────────────────────────────────────────────────────

    private void CreateEnemyCard(Transform parent, EnemyData data)
    {
        // Card container (680 x 120)
        GameObject card = new GameObject("Card_" + data.enemyName);
        card.transform.SetParent(parent, false);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = CARD_BG;
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(680f, 120f);

        LayoutElement le = card.AddComponent<LayoutElement>();
        le.minHeight = 120f;
        le.preferredHeight = 120f;

        // Borda colorida no lado esquerdo
        GameObject border = new GameObject("Border");
        border.transform.SetParent(card.transform, false);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = CARD_BORDER;
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.sizeDelta = new Vector2(4f, 120f);
        borderRect.anchorMin = new Vector2(0f, 0.5f);
        borderRect.anchorMax = new Vector2(0f, 0.5f);
        borderRect.pivot = new Vector2(0f, 0.5f);
        borderRect.anchoredPosition = Vector2.zero;

        // Ícone do inimigo
        if (data.icon != null)
        {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(card.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = data.icon;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(90f, 90f);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(15f, 0f);
        }

        // Texto - Nome
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = data.enemyName.ToUpper();
        nameTMP.fontSize = 18;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = HEADER_COLOR;
        if (customFont != null) nameTMP.font = customFont;
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(120f, 0f);
        nameRect.offsetMax = new Vector2(-10f, 0f);
        nameRect.anchoredPosition = new Vector2(120f, -12f);
        nameRect.sizeDelta = new Vector2(-130f, 26f);

        // Texto - Classe
        if (!string.IsNullOrEmpty(data.enemyClass))
        {
            GameObject classGO = new GameObject("Class");
            classGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI classTMP = classGO.AddComponent<TextMeshProUGUI>();
            classTMP.text = "<i>" + data.enemyClass + "</i>";
            classTMP.fontSize = 13;
            classTMP.color = TAB_ACTIVE;
            RectTransform classRect = classGO.GetComponent<RectTransform>();
            classRect.anchorMin = new Vector2(0f, 1f);
            classRect.anchorMax = new Vector2(1f, 1f);
            classRect.pivot = new Vector2(0f, 1f);
            classRect.anchoredPosition = new Vector2(120f, -40f);
            classRect.sizeDelta = new Vector2(-130f, 20f);
        }

        // Texto - Descrição/Lore
        if (!string.IsNullOrEmpty(data.descricao))
        {
            GameObject descGO = new GameObject("Desc");
            descGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text = data.descricao;
            descTMP.fontSize = 12;
            descTMP.color = TEXT_DIM;
            descTMP.textWrappingMode = TextWrappingModes.Normal;
            RectTransform descRect = descGO.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0f, 1f);
            descRect.offsetMin = new Vector2(120f, 10f);
            descRect.offsetMax = new Vector2(-10f, -60f);
        }
    }

    private void CreateItemCard(Transform parent, ItemData data)
    {
        GameObject card = new GameObject("Card_" + data.itemId);
        card.transform.SetParent(parent, false);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = CARD_BG;
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(680f, 80f);

        LayoutElement le = card.AddComponent<LayoutElement>();
        le.minHeight = 80f;
        le.preferredHeight = 80f;

        // Ícone
        if (data.icon != null)
        {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(card.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = data.icon;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(60f, 60f);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
        }

        // Nome
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = data.itemName;
        nameTMP.fontSize = 16;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = HEADER_COLOR;
        if (customFont != null) nameTMP.font = customFont;
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(1f, 0.5f);
        nameRect.anchoredPosition = new Vector2(80f, 10f);
        nameRect.sizeDelta = new Vector2(-90f, 24f);

        // Descrição
        if (!string.IsNullOrEmpty(data.description))
        {
            GameObject descGO = new GameObject("Desc");
            descGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text = data.description;
            descTMP.fontSize = 12;
            descTMP.color = TEXT_DIM;
            RectTransform descRect = descGO.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0.5f);
            descRect.anchorMax = new Vector2(1f, 0.5f);
            descRect.anchoredPosition = new Vector2(80f, -12f);
            descRect.sizeDelta = new Vector2(-90f, 20f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers UI
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        return go;
    }

    private GameObject CreateLabel(Transform parent, string name, string text, Vector2 pos,
        float fontSize, Color color, bool bold)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        if (customFont != null) tmp.font = customFont;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(700f, 40f);
        rt.anchoredPosition = pos;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        return go;
    }

    private GameObject CreateButton(Transform parent, string name, string text, Vector2 pos,
        Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (customFont != null) tmp.font = customFont;
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return go;
    }

    private GameObject CreateScrollArea(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        // ScrollView root
        var svGO = new GameObject(name);
        svGO.transform.SetParent(parent, false);
        Image svImg = svGO.AddComponent<Image>();
        svImg.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect sr = svGO.AddComponent<ScrollRect>();
        RectTransform svRect = svGO.GetComponent<RectTransform>();
        svRect.sizeDelta = size;
        svRect.anchoredPosition = pos;

        // Viewport
        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(svGO.transform, false);
        RectMask2D mask = vpGO.AddComponent<RectMask2D>();
        RectTransform vpRect = vpGO.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;

        // Content (VerticalLayoutGroup)
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        RectTransform contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        sr.viewport = vpRect;
        sr.content = contentRect;
        sr.horizontal = false;
        sr.vertical = true;

        return svGO;
    }

    private void SetButtonColor(GameObject btn, Color color)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }
}



