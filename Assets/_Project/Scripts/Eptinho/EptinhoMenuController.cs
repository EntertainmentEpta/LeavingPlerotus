using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Controla o menu do Oráculo Eptinho.
/// Interface completamente gerada por código, estilo dark-glass (mesma paleta que SyntheticBagUI).
/// Abre via EptinhoOracleInteract (trigger F perto do Eptin) ou via tecla I.
/// </summary>
public class EptinhoMenuController : MonoBehaviour
{
    public static EptinhoMenuController instancia;
    public static EptinhoMenuController Instance => instancia;

    // Modal de Inspeção 3D de Inimigos no Bestiário
    private GameObject enemyDetailModal;
    private RawImage enemyPreviewRawImage;
    private Image modalEnemyIconImg;
    private TextMeshProUGUI modalEnemyNameTMP;
    private TextMeshProUGUI modalEnemyClassTMP;
    private TextMeshProUGUI modalEnemyDescTMP;
    private TextMeshProUGUI modalEnemyStatsTMP;

    // ─── Configuração do Inspector (todos opcionais) ──────────────────────────
    [Header("UI Gerada por Código (auto-configurado)")]
    public GameObject menuUI; // Será criado automaticamente se nulo
    public GameObject HUDCanvas; // Referência ao HUD para esconder ao abrir o menu

    // ─── Constantes Visuais (Inspirado no Inventário / Frosted Slate Glass) ───
    // Fundo profundo em ardósia/grafite escuro (sem tons roxos)
    private static readonly Color PANEL_BG           = new Color(0.040f, 0.045f, 0.075f, 0.94f);
    private static readonly Color PANEL_BORDER       = new Color(0.35f, 0.55f, 0.80f, 0.40f);
    private static readonly Color PANEL_BORDER_INNER = new Color(0.20f, 0.40f, 0.65f, 0.25f);
    private static readonly Color HEADER_COLOR       = new Color(0.92f, 0.95f, 0.98f, 1.00f);
    private static readonly Color SUBTITLE_COLOR     = new Color(0.40f, 0.80f, 0.95f, 0.95f);

    // Abas de navegação (Estilo abas táticas de vidro escuro)
    private static readonly Color TAB_ACTIVE         = new Color(0.10f, 0.16f, 0.26f, 0.98f);
    private static readonly Color TAB_ACTIVE_BORDER  = new Color(0.35f, 0.80f, 1.00f, 0.90f);
    private static readonly Color TAB_INACTIVE       = new Color(0.055f, 0.065f, 0.095f, 0.85f);
    private static readonly Color TAB_INACT_BORDER   = new Color(0.18f, 0.26f, 0.38f, 0.45f);

    // Cards e Dossiês (Estilo slots e cartões do inventário)
    private static readonly Color CARD_BG            = new Color(0.065f, 0.080f, 0.120f, 0.95f);
    private static readonly Color CARD_BORDER        = new Color(0.22f, 0.34f, 0.50f, 0.55f);
    private static readonly Color CARD_INSET_BG      = new Color(0.030f, 0.035f, 0.055f, 1.00f);
    private static readonly Color CARD_INSET_BORDER  = new Color(0.25f, 0.45f, 0.65f, 0.55f);

    // Tipografia nítida de alta legibilidade
    private static readonly Color TEXT_TITLE         = new Color(0.94f, 0.96f, 1.00f, 1.00f);
    private static readonly Color TEXT_BODY          = new Color(0.78f, 0.84f, 0.92f, 1.00f);
    private static readonly Color TEXT_DIM           = new Color(0.50f, 0.56f, 0.68f, 1.00f);

    // Cores de Acento e Status
    private static readonly Color ACTIVE_BORDER      = new Color(1.00f, 0.78f, 0.22f, 1.00f); // Dourado âmbar tático
    private static readonly Color SUCCESS_COLOR      = new Color(0.25f, 0.85f, 0.55f, 1.00f); // Verde esmeralda tático
    private static readonly Color TECH_CYAN          = new Color(0.25f, 0.85f, 0.95f, 1.00f); // Ciano neon brilhante
    private static readonly Color TECH_AZURE         = new Color(0.30f, 0.65f, 0.95f, 1.00f); // Azul cobalto de espécime
    private static readonly Color TECH_CYAN_DIM      = new Color(0.25f, 0.85f, 0.95f, 0.40f); // Ciano para cantoneiras
    private static readonly Color MASTER_GOLD         = new Color(1.00f, 0.72f, 0.16f, 1.00f); // Dourado solar supremo da nave

    // Barra de progresso
    private static readonly Color PROGRESS_BG        = new Color(0.08f, 0.10f, 0.15f, 1.00f);
    private static readonly Color PROGRESS_FILL      = new Color(0.25f, 0.80f, 0.95f, 1.00f);

    // ─── Referências Internas ─────────────────────────────────────────────────
    private Canvas      menuCanvas;
    private GameObject  canvasObj;
    private GameObject  panelObj;
    private GameObject  tabQuests;
    private GameObject  tabBestiary;
    private GameObject  tabCatalogo;
    private GameObject  tabSinergia;
    private GameObject  contentQuests;
    private GameObject  contentBestiary;
    private GameObject  contentCatalogo;
    private GameObject  contentSinergia;
    private int         currentTab = 0; // 0 = Quests, 1 = Bestiary, 2 = Catalogo, 3 = Sinergia
    private bool        isOpen = false;
    private bool        uiBuilt = false;
    private TMP_FontAsset customFont;
    private AudioSource audioSource;
    private AudioClip   beepClip;

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

    void OnEnable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.StateChanged += HandleQuestStateChanged;
    }

    void OnDisable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.StateChanged -= HandleQuestStateChanged;
    }

    private void HandleQuestStateChanged()
    {
        if (isOpen && currentTab == 0) RefreshQuests();
    }

    void Start()
    {
        BuildUI();
    }

        void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            if (enemyDetailModal != null && enemyDetailModal.activeSelf)
                FecharInspecaoInimigo();
            else if (currentTab == 3)
                MostrarAba(0); // Se estiver no Mapa em tela cheia, o ESC apenas volta para o menu
            else
                FecharMenu();  // Senão, fecha tudo
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API Pública
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsOpen() => gameObject.activeInHierarchy && isOpen;

    public void AbrirMenu()
    {
        if (!uiBuilt) BuildUI();
        if (isOpen) return;

        // Garante subscrição caso o QuestManager tenha nascido depois
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StateChanged -= HandleQuestStateChanged;
            QuestManager.Instance.StateChanged += HandleQuestStateChanged;
        }

        isOpen = true;
        panelObj.SetActive(true);
        if (HUDCanvas != null) HUDCanvas.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PlayBeep();
        RefreshCurrentTab();
        Debug.Log("[EPTINHO MENU] Aberto.");
    }

    public void FecharMenu()
    {
        if (!isOpen) return;
        isOpen = false;

        FecharInspecaoInimigo();

        PlayBeep();
        if (panelObj != null) panelObj.SetActive(false);
        if (contentSinergia != null) contentSinergia.SetActive(false); // Garante que a tela cheia fecha também

        if (HUDCanvas != null) HUDCanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[EPTINHO MENU] Fechado.");
    }

    private void PlayBeep()
    {
        if (audioSource == null)
        {
            if (canvasObj != null)
            {
                audioSource = canvasObj.GetComponent<AudioSource>();
                if (audioSource == null) audioSource = canvasObj.AddComponent<AudioSource>();
            }
            else
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (beepClip == null)
        {
            AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var c in allClips)
            {
                if (c != null && (c.name.Contains("Beep_Eptinho") || c.name.Contains("card") || c.name.Contains("hover")))
                {
                    beepClip = c;
                    break;
                }
            }
        }

        if (beepClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(beepClip, 0.65f);
        }
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

        // AudioSource para feedback de interface
        audioSource = canvasObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Panel principal expandido (1200x820)
        panelObj = CreatePanel(canvasObj.transform, "OraclePanel", new Vector2(1200f, 820f), PANEL_BG);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        // ── Header ──
        // Moldura do ícone do Eptinho
        GameObject iconFrame = new GameObject("EptonhoIconFrame");
        iconFrame.transform.SetParent(panelObj.transform, false);
        Image frameImg = iconFrame.AddComponent<Image>();
        frameImg.color = CARD_INSET_BG;
        RectTransform frameRect = iconFrame.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(64f, 64f);
        frameRect.anchoredPosition = new Vector2(-510f, 350f);

        Outline iconFrameOutline = iconFrame.AddComponent<Outline>();
        iconFrameOutline.effectColor = CARD_INSET_BORDER;
        iconFrameOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Sprite eptonhoSprite = Resources.Load<Sprite>("EPTONHO");
        if (eptonhoSprite != null)
        {
            GameObject iconGO = new GameObject("EptonhoIcon");
            iconGO.transform.SetParent(iconFrame.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = eptonhoSprite;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(56f, 56f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        // Título e Subtítulo
        CreateLabel(panelObj.transform, "HeaderTitle", "EPTINHO  ·  ORÁCULO DA BASE",
            new Vector2(-150f, 365f), 32, HEADER_COLOR, true, TextAlignmentOptions.Left, new Vector2(640f, 34f));

        CreateLabel(panelObj.transform, "HeaderSubtitle", "SISTEMA DE ANÁLISE E DIRETRIZES TÁTICAS",
            new Vector2(-150f, 340f), 18, SUBTITLE_COLOR, true, TextAlignmentOptions.Left, new Vector2(640f, 20f));

        // ── Botão Fechar ──
        GameObject btnClose = CreateButton(panelObj.transform, "BtnClose", "✕", new Vector2(535f, 350f),
            new Vector2(46f, 46f), new Color(0.65f, 0.18f, 0.22f, 0.85f), () => FecharMenu());
        Outline closeOutline = btnClose.GetComponent<Outline>();
        if (closeOutline != null) closeOutline.effectColor = new Color(0.95f, 0.35f, 0.40f, 0.65f);
        Transform bCloseTr = btnClose.transform.Find("Label");
        if (bCloseTr != null)
        {
            TextMeshProUGUI cTmp = bCloseTr.GetComponent<TextMeshProUGUI>();
            if (cTmp != null) cTmp.fontSize = 24;
        }

        // Linha divisória tática
        GameObject line = new GameObject("Line");
        line.transform.SetParent(panelObj.transform, false);
        Image lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(0.25f, 0.45f, 0.65f, 0.35f);
        RectTransform lineRect = line.GetComponent<RectTransform>();
        lineRect.sizeDelta = new Vector2(1120f, 2f);
        lineRect.anchoredPosition = new Vector2(0f, 310f);

        // ── Abas ──
        float tabY = 265f;
        float tabW = 265f;
        tabQuests = CreateButton(panelObj.transform, "TabQuests", "MISSÕES",
            new Vector2(-400f, tabY), new Vector2(tabW, 48f), TAB_ACTIVE, () => MostrarAba(0));

        tabBestiary = CreateButton(panelObj.transform, "TabBestiary", "BESTIÁRIO",
            new Vector2(-133f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(1));

        tabCatalogo = CreateButton(panelObj.transform, "TabCatalogo", "CATÁLOGO",
            new Vector2(133f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(2));

        tabSinergia = CreateButton(panelObj.transform, "TabSinergia", "MAPA DE SINERGIAS",
            new Vector2(400f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(3));

        // ── Área de conteúdo ──
        contentQuests = CreateScrollArea(panelObj.transform, "ContentQuests",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));

        contentBestiary = CreateScrollArea(panelObj.transform, "ContentBestiary",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));
        contentBestiary.SetActive(false);

        contentCatalogo = CreateScrollArea(panelObj.transform, "ContentCatalogo",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));
        contentCatalogo.SetActive(false);

        // Painel do Mapa de Sinergias (FULL SCREEN)
        contentSinergia = CreatePanel(canvasObj.transform, "ContentSinergia", Vector2.zero, PANEL_BG);
        RectTransform sinRect = contentSinergia.GetComponent<RectTransform>();
        sinRect.anchorMin = Vector2.zero; sinRect.anchorMax = Vector2.one;
        sinRect.sizeDelta = Vector2.zero; // Stretch for Full Screen
        
        // Close Button (X) for Synergy Map
        GameObject btnCloseSin = CreateButton(contentSinergia.transform, "BtnCloseSinergia", "✕ VOLTAR AO MENU", Vector2.zero, new Vector2(200f, 50f), new Color(0.12f, 0.18f, 0.28f, 0.95f), () => MostrarAba(0));
        Outline bSinOutline = btnCloseSin.GetComponent<Outline>();
        if (bSinOutline != null) bSinOutline.effectColor = TECH_CYAN;
        RectTransform btnRect = btnCloseSin.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 1);
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.anchoredPosition = new Vector2(-120f, -50f); // Top-Right offset

        contentSinergia.AddComponent<SynergyMapBuilder>();
        contentSinergia.SetActive(false);

        menuUI = panelObj;
        panelObj.SetActive(false);
        uiBuilt = true;

        UpdateTabVisuals(tabQuests, true);
        UpdateTabVisuals(tabBestiary, false);
        UpdateTabVisuals(tabCatalogo, false);
        UpdateTabVisuals(tabSinergia, false);
    }

    private void MostrarAba(int abaIndex)
    {
        currentTab = abaIndex;
        PlayBeep();

        contentQuests.SetActive(abaIndex == 0);
        contentBestiary.SetActive(abaIndex == 1);
        contentCatalogo.SetActive(abaIndex == 2);
        contentSinergia.SetActive(abaIndex == 3);

        UpdateTabVisuals(tabQuests, abaIndex == 0);
        UpdateTabVisuals(tabBestiary, abaIndex == 1);
        UpdateTabVisuals(tabCatalogo, abaIndex == 2);
        UpdateTabVisuals(tabSinergia, abaIndex == 3);

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
            if (currentTab == 3) MostrarAba(0);
        }

        if (currentTab == 0) RefreshQuests();
        else if (currentTab == 1) RefreshBestiary();
        else if (currentTab == 2) RefreshCatalogo();
    }

    private void RefreshQuests()
    {
        Transform grid = contentQuests.transform.Find("Viewport/Content");
        if (grid == null)
        {
            Debug.LogError("[EPTINHO MENU] Viewport/Content nao encontrado em ContentQuests!");
            return;
        }

        foreach (Transform child in grid) Destroy(child.gameObject);

        PersistentSaveData save = SaveManager.instance != null ? SaveManager.instance.CachedData : null;
        QuestData activeQuest = QuestManager.Instance != null ? QuestManager.Instance.currentQuest : null;

        if (activeQuest == null && save != null && !string.IsNullOrEmpty(save.activeQuestId))
        {
            activeQuest = QuestManager.Instance != null 
                ? QuestManager.Instance.FindQuestById(save.activeQuestId) 
                : Resources.Load<QuestData>("Quests/" + save.activeQuestId);
            if (activeQuest != null && QuestManager.Instance != null && QuestManager.Instance.currentQuest == null)
            {
                QuestManager.Instance.currentQuest = activeQuest;
            }
        }

        // 1. CARD DA META SUPREMA DO JOGO (RECUPERAR NÚCLEOS DA NAVE)
        int shipParts = SaveManager.instance != null ? SaveManager.instance.GetShipPartsRecovered() : 0;
        CreateMasterObjectiveCard(grid, shipParts);

        // 2. SEPARADOR / HEADER DE DIRETRIZ ATIVA
        CreateSectionHeader(grid, "DIRETRIZ ATUAL");

        // 3. CARD DA MISSÃO ATIVA
        if (activeQuest != null)
        {
            int progress = save != null ? save.activeQuestProgress : 0;
            int required = Mathf.Max(1, activeQuest.requiredAmount);
            CreateActiveQuestCard(grid, activeQuest, progress, required);
        }
        else
        {
            CreateNoActiveQuestCard(grid);
        }

        // 4. SEPARADOR / HEADER DE CONCLUÍDAS
        CreateSectionHeader(grid, "DIRETRIZES CONCLUÍDAS");

        // 5. HISTÓRICO DE MISSÕES CONCLUÍDAS
        List<string> completedIds = save != null ? save.completedQuestIds : null;
        if (completedIds == null || completedIds.Count == 0)
        {
            CreateLabel(grid, "EmptyCompleted", "Nenhuma diretriz concluída registrada até o momento.",
                Vector2.zero, 14, TEXT_DIM, false);
        }
        else
        {
            for (int i = completedIds.Count - 1; i >= 0; i--) // Mais recentes primeiro
            {
                string qId = completedIds[i];
                QuestData qData = QuestManager.Instance != null ? QuestManager.Instance.FindQuestById(qId) : Resources.Load<QuestData>("Quests/" + qId);
                CreateCompletedQuestCard(grid, qId, qData);
            }
        }
    }

    private void CreateMasterObjectiveCard(Transform parent, int recoveredParts)
    {
        GameObject card = new GameObject("Card_MasterObjective");
        card.transform.SetParent(parent, false);
        StyleCardBase(card, 255f, MASTER_GOLD, 8f);

        // Badge: "★ META SUPREMA DO JOGO"
        GameObject badgeGO = new GameObject("Badge");
        badgeGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI badgeTMP = badgeGO.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = "★ META SUPREMA DO JOGO";
        badgeTMP.fontSize = 20;
        badgeTMP.fontStyle = FontStyles.Bold;
        badgeTMP.color = MASTER_GOLD;
        if (customFont != null) badgeTMP.font = customFont;
        RectTransform badgeRect = badgeGO.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(24f, -14f);
        badgeRect.sizeDelta = new Vector2(-48f, 24f);

        // Título: "RECUPERAR OS 4 NÚCLEOS DA NAVE"
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "RECUPERAR OS 4 NÚCLEOS PERDIDOS DA NAVE";
        titleTMP.fontSize = 30;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TEXT_TITLE;
        if (customFont != null) titleTMP.font = customFont;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(24f, -48f);
        titleRect.sizeDelta = new Vector2(-48f, 34f);

        // Descrição
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = "Derrote as anomalias titânicas nos confins de cada setor da instalação para extrair os núcleos perdidos e restaurar os sistemas de propulsão e navegação da nave.";
        descTMP.fontSize = 21;
        descTMP.color = TEXT_BODY;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descTMP.lineSpacing = 1.15f;
        descRect.anchoredPosition = new Vector2(24f, -92f);
        descRect.sizeDelta = new Vector2(-48f, 72f);

        // Container de 4 Segmentos dos Núcleos
        GameObject segContainer = new GameObject("SegmentsContainer");
        segContainer.transform.SetParent(card.transform, false);
        RectTransform segRt = segContainer.AddComponent<RectTransform>();
        segRt.anchorMin = new Vector2(0f, 0f);
        segRt.anchorMax = new Vector2(1f, 0f);
        segRt.pivot = new Vector2(0.5f, 0f);
        segRt.anchoredPosition = new Vector2(0f, 18f);
        segRt.sizeDelta = new Vector2(-48f, 36f);

        HorizontalLayoutGroup hlg = segContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        for (int i = 0; i < 4; i++)
        {
            bool isRecovered = i < recoveredParts;
            GameObject seg = new GameObject($"Core_Segment_{i + 1}");
            seg.transform.SetParent(segContainer.transform, false);

            Image segImg = seg.AddComponent<Image>();
            segImg.color = isRecovered 
                ? new Color(0.95f, 0.65f, 0.12f, 0.35f)
                : new Color(0.08f, 0.10f, 0.15f, 0.90f);

            Outline segOutline = seg.AddComponent<Outline>();
            segOutline.effectColor = isRecovered ? MASTER_GOLD : new Color(0.35f, 0.40f, 0.50f, 0.50f);
            segOutline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject segTxtGO = new GameObject("Label");
            segTxtGO.transform.SetParent(seg.transform, false);
            TextMeshProUGUI segTmp = segTxtGO.AddComponent<TextMeshProUGUI>();
            segTmp.text = isRecovered ? $"★ NÚCLEO 0{i + 1} [RECUPERADO]" : $"◇ NÚCLEO 0{i + 1} [PENDENTE]";
            segTmp.fontSize = 18;
            segTmp.fontStyle = FontStyles.Bold;
            segTmp.alignment = TextAlignmentOptions.Center;
            segTmp.color = isRecovered ? MASTER_GOLD : TEXT_DIM;
            if (customFont != null) segTmp.font = customFont;

            RectTransform txtRt = segTxtGO.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }
    }

    private void CreateActiveQuestCard(Transform parent, QuestData quest, int progress, int required)
    {
        GameObject card = new GameObject("Card_ActiveQuest");
        card.transform.SetParent(parent, false);
        StyleCardBase(card, 210f, ACTIVE_BORDER, 8f);

        // Badge: "● DIRETRIZ EM ANDAMENTO"
        GameObject badgeGO = new GameObject("Badge");
        badgeGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI badgeTMP = badgeGO.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = "● DIRETRIZ ATIVA";
        badgeTMP.fontSize = 20;
        badgeTMP.fontStyle = FontStyles.Bold;
        badgeTMP.color = ACTIVE_BORDER;
        if (customFont != null) badgeTMP.font = customFont;
        RectTransform badgeRect = badgeGO.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(24f, -14f);
        badgeRect.sizeDelta = new Vector2(-48f, 22f);

        // Título da Missão
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = quest.questTitle.ToUpper();
        titleTMP.fontSize = 32;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TEXT_TITLE;
        if (customFont != null) titleTMP.font = customFont;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(24f, -44f);
        titleRect.sizeDelta = new Vector2(-48f, 34f);

        // Descrição detalhada
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = !string.IsNullOrEmpty(quest.questDescription) ? quest.questDescription : "Siga as coordenadas e orientações do Oráculo.";
        descTMP.fontSize = 22;
        descTMP.color = TEXT_BODY;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descTMP.lineSpacing = 1.15f;
        descRect.anchoredPosition = new Vector2(24f, -86f);
        descRect.sizeDelta = new Vector2(-48f, 58f);

        // Barra de Progresso - Fundo
        GameObject progressBgGO = new GameObject("ProgressBg");
        progressBgGO.transform.SetParent(card.transform, false);
        Image progressBgImg = progressBgGO.AddComponent<Image>();
        progressBgImg.color = PROGRESS_BG;
        Outline pBgOutline = progressBgGO.AddComponent<Outline>();
        pBgOutline.effectColor = new Color(0.20f, 0.35f, 0.55f, 0.45f);
        pBgOutline.effectDistance = new Vector2(1f, -1f);
        RectTransform progressBgRect = progressBgGO.GetComponent<RectTransform>();
        progressBgRect.anchorMin = new Vector2(0f, 0f);
        progressBgRect.anchorMax = new Vector2(1f, 0f);
        progressBgRect.pivot = new Vector2(0f, 0f);
        progressBgRect.anchoredPosition = new Vector2(24f, 18f);
        progressBgRect.sizeDelta = new Vector2(-48f, 28f);

        // Barra de Progresso - Preenchimento
        float fillRatio = Mathf.Clamp01((float)progress / required);
        GameObject progressFillGO = new GameObject("ProgressFill");
        progressFillGO.transform.SetParent(progressBgGO.transform, false);
        Image progressFillImg = progressFillGO.AddComponent<Image>();
        progressFillImg.color = PROGRESS_FILL;
        RectTransform progressFillRect = progressFillGO.GetComponent<RectTransform>();
        progressFillRect.anchorMin = new Vector2(0f, 0f);
        progressFillRect.anchorMax = new Vector2(fillRatio, 1f);
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;

        // Texto sobre a barra
        GameObject progressTxtGO = new GameObject("ProgressText");
        progressTxtGO.transform.SetParent(progressBgGO.transform, false);
        TextMeshProUGUI progressTMP = progressTxtGO.AddComponent<TextMeshProUGUI>();
        int pct = Mathf.RoundToInt(fillRatio * 100f);
        progressTMP.text = $"Progresso: {progress} / {required}  ({pct}%)";
        progressTMP.fontSize = 20;
        progressTMP.fontStyle = FontStyles.Bold;
        progressTMP.alignment = TextAlignmentOptions.Center;
        progressTMP.color = Color.white;
        if (customFont != null) progressTMP.font = customFont;
        RectTransform progressTxtRect = progressTxtGO.GetComponent<RectTransform>();
        progressTxtRect.anchorMin = Vector2.zero;
        progressTxtRect.anchorMax = Vector2.one;
        progressTxtRect.offsetMin = Vector2.zero;
        progressTxtRect.offsetMax = Vector2.zero;
    }

    private void CreateNoActiveQuestCard(Transform parent)
    {
        GameObject card = new GameObject("Card_NoActiveQuest");
        card.transform.SetParent(parent, false);
        StyleCardBase(card, 110f, TECH_CYAN_DIM, 4f);

        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "NENHUMA DIRETRIZ PENDENTE";
        titleTMP.fontSize = 26;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TEXT_TITLE;
        titleTMP.alignment = TextAlignmentOptions.Center;
        if (customFont != null) titleTMP.font = customFont;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 14f);
        titleRect.sizeDelta = new Vector2(0f, 28f);

        GameObject subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text = "Todas as missões da base foram concluídas. Continue explorando e coletando amostras na câmara de run!";
        subTMP.fontSize = 20;
        subTMP.color = TEXT_BODY;
        subTMP.alignment = TextAlignmentOptions.Center;
        RectTransform subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.5f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.anchoredPosition = new Vector2(0f, -16f);
        subRect.sizeDelta = new Vector2(0f, 24f);
    }

    private void CreateSectionHeader(Transform parent, string title)
    {
        GameObject headerGO = new GameObject("SectionHeader_" + title);
        headerGO.transform.SetParent(parent, false);
        RectTransform rt = headerGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1080f, 44f);

        LayoutElement le = headerGO.AddComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;

        GameObject textGO = new GameObject("Title");
        textGO.transform.SetParent(headerGO.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = SUBTITLE_COLOR;
        if (customFont != null) tmp.font = customFont;
        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(1f, 0.5f);
        trt.anchoredPosition = new Vector2(10f, 0f);
        trt.sizeDelta = new Vector2(-20f, 28f);

        GameObject subLine = new GameObject("SectionLine");
        subLine.transform.SetParent(headerGO.transform, false);
        Image slImg = subLine.AddComponent<Image>();
        slImg.color = new Color(0.25f, 0.45f, 0.65f, 0.30f);
        RectTransform slRt = subLine.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0f, 0f);
        slRt.anchorMax = new Vector2(1f, 0f);
        slRt.pivot = new Vector2(0.5f, 0f);
        slRt.anchoredPosition = new Vector2(0f, 0f);
        slRt.sizeDelta = new Vector2(0f, 1.5f);
    }

    private void CreateCompletedQuestCard(Transform parent, string questId, QuestData data)
    {
        GameObject card = new GameObject("Card_Completed_" + questId);
        card.transform.SetParent(parent, false);
        StyleCardBase(card, 95f, SUCCESS_COLOR, 6f);

        // Badge concluída
        GameObject checkGO = new GameObject("Check");
        checkGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI checkTMP = checkGO.AddComponent<TextMeshProUGUI>();
        checkTMP.text = "✔ DIRETRIZ CONCLUÍDA";
        checkTMP.fontSize = 18;
        checkTMP.fontStyle = FontStyles.Bold;
        checkTMP.color = SUCCESS_COLOR;
        if (customFont != null) checkTMP.font = customFont;
        RectTransform checkRect = checkGO.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 1f);
        checkRect.anchorMax = new Vector2(1f, 1f);
        checkRect.pivot = new Vector2(0f, 1f);
        checkRect.anchoredPosition = new Vector2(24f, -10f);
        checkRect.sizeDelta = new Vector2(-48f, 20f);

        // Título
        string title = data != null ? data.questTitle : questId;
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title.ToUpper();
        titleTMP.fontSize = 26;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TEXT_TITLE;
        if (customFont != null) titleTMP.font = customFont;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(24f, -34f);
        titleRect.sizeDelta = new Vector2(-48f, 26f);

        // Descrição curta
        string desc = data != null && !string.IsNullOrEmpty(data.questDescription) ? data.questDescription : "Diretriz cumprida e registrada pelo Eptinho.";
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = desc;
        descTMP.fontSize = 21;
        descTMP.color = TEXT_BODY;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.offsetMin = new Vector2(24f, 8f);
        descRect.offsetMax = new Vector2(-24f, -62f);
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


    // =========================================================================
    // INSPEÇÃO 3D DO INIMIGO NO BESTIÁRIO
    // =========================================================================

    public void AtualizarAbaBestiario()
    {
        if (contentBestiary != null && contentBestiary.activeSelf)
        {
            RefreshBestiary();
        }
    }

    public void AtualizarAbaCatalogo()
    {
        if (contentCatalogo != null && contentCatalogo.activeSelf)
        {
            RefreshCatalogo();
        }
    }

    /// <summary>
    /// Retorna uma cor de destaque temática e de alto contraste de acordo com a classe do inimigo.
    /// Ex: Elite = Dourado Radiante, Tanque = Verde Robusto, Atirador = Vermelho Carmesim, etc.
    /// </summary>
    public static Color ObterCorDaClasse(string enemyClass)
    {
        if (string.IsNullOrEmpty(enemyClass)) return new Color(0.0f, 0.90f, 1.0f, 1f);

        string c = enemyClass.ToLower().Trim();
        if (c.Contains("elite") || c.Contains("boss") || c.Contains("chefe"))
            return new Color(1.0f, 0.84f, 0.0f, 1f); // Dourado radiante
        if (c.Contains("tanque") || c.Contains("blindado"))
            return new Color(0.0f, 0.90f, 0.46f, 1f); // Verde esmeralda robusto
        if (c.Contains("atirador") || c.Contains("ranged") || c.Contains("assassin") || c.Contains("aracn"))
            return new Color(1.0f, 0.24f, 0.44f, 1f); // Vermelho carmesim neon
        if (c.Contains("suporte") || c.Contains("healer") || c.Contains("guardian") || c.Contains("totem"))
            return new Color(0.0f, 0.90f, 1.0f, 1f); // Azul elétrico protetor
        if (c.Contains("enxame") || c.Contains("swarm"))
            return new Color(0.83f, 0.0f, 0.98f, 1f); // Roxo ametista vibrante
        if (c.Contains("mob") || c.Contains("menor") || c.Contains("comum"))
            return new Color(0.56f, 0.79f, 0.98f, 1f); // Azul metálico suave

        return new Color(0.0f, 0.90f, 1.0f, 1f);
    }


    private void EnsureEnemyDetailModal()
    {
        if (enemyDetailModal != null) return;

        enemyDetailModal = new GameObject("EnemyDetailModal");
        enemyDetailModal.transform.SetParent(panelObj.transform, false);

        RectTransform r = enemyDetailModal.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.02f, 0.04f);
        r.anchorMax = new Vector2(0.98f, 0.84f);
        r.sizeDelta = Vector2.zero;

        Image bg = enemyDetailModal.AddComponent<Image>();
        bg.color = new Color(0.035f, 0.045f, 0.070f, 0.98f);

        Outline outline = enemyDetailModal.AddComponent<Outline>();
        outline.effectColor = TECH_CYAN;
        outline.effectDistance = new Vector2(2f, -2f);

        // Botão Fechar Modal posicionado com perfeição no canto superior direito
        GameObject btnCloseModal = CreateButton(enemyDetailModal.transform, "BtnCloseModal", "✕  VOLTAR AO BESTIÁRIO",
            new Vector2(-24f, -20f), new Vector2(230f, 42f), new Color(0.65f, 0.18f, 0.22f, 0.95f), () => FecharInspecaoInimigo());
        RectTransform bCloseRt = btnCloseModal.GetComponent<RectTransform>();
        bCloseRt.anchorMin = new Vector2(1f, 1f);
        bCloseRt.anchorMax = new Vector2(1f, 1f);
        bCloseRt.pivot = new Vector2(1f, 1f);
        bCloseRt.anchoredPosition = new Vector2(-24f, -20f);
        Transform bCloseTr = btnCloseModal.transform.Find("Label");
        if (bCloseTr != null)
        {
            TextMeshProUGUI bTmp = bCloseTr.GetComponent<TextMeshProUGUI>();
            if (bTmp != null) bTmp.fontSize = 18;
        }

        // --- Lado Esquerdo: Moldura Holográfica de Análise do Espécime ---
        GameObject viewContainer = new GameObject("Holo_SpecimenContainer");
        viewContainer.transform.SetParent(enemyDetailModal.transform, false);
        RectTransform vcr = viewContainer.AddComponent<RectTransform>();
        vcr.anchorMin = new Vector2(0.04f, 0.08f);
        vcr.anchorMax = new Vector2(0.48f, 0.94f);
        vcr.sizeDelta = Vector2.zero;

        Image viewBg = viewContainer.AddComponent<Image>();
        viewBg.color = new Color(0.015f, 0.020f, 0.035f, 1f);

        Outline viewOutline = viewContainer.AddComponent<Outline>();
        viewOutline.effectColor = new Color(0.25f, 0.65f, 0.95f, 0.5f);
        viewOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Moldura interna do retrato
        GameObject iconFrameObj = new GameObject("SpecimenFrame");
        iconFrameObj.transform.SetParent(viewContainer.transform, false);
        RectTransform iconFrameRect = iconFrameObj.AddComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0.08f, 0.14f);
        iconFrameRect.anchorMax = new Vector2(0.92f, 0.92f);
        iconFrameRect.sizeDelta = Vector2.zero;
        Image ifImg = iconFrameObj.AddComponent<Image>();
        ifImg.color = new Color(0.02f, 0.035f, 0.06f, 0.95f);
        Outline ifOutline = iconFrameObj.AddComponent<Outline>();
        ifOutline.effectColor = new Color(0.35f, 0.75f, 0.95f, 0.35f);

        // Imagem central do ícone do espécime (translúcida de fundo)
        GameObject iconImgObj = new GameObject("SpecimenIcon");
        iconImgObj.transform.SetParent(iconFrameObj.transform, false);
        RectTransform iconImgRect = iconImgObj.AddComponent<RectTransform>();
        iconImgRect.anchorMin = new Vector2(0.15f, 0.15f);
        iconImgRect.anchorMax = new Vector2(0.85f, 0.85f);
        iconImgRect.sizeDelta = Vector2.zero;
        modalEnemyIconImg = iconImgObj.AddComponent<Image>();
        modalEnemyIconImg.preserveAspect = true;
        modalEnemyIconImg.color = new Color(1f, 1f, 1f, 0.15f); // Marca d'água sutil atrás

        // Card central estilizado: "✦ EM BREVE ✦" (Modelo 3D em desenvolvimento)
        GameObject comingSoonBox = new GameObject("3D_ComingSoonBox");
        comingSoonBox.transform.SetParent(iconFrameObj.transform, false);
        RectTransform csRect = comingSoonBox.AddComponent<RectTransform>();
        csRect.anchorMin = new Vector2(0.06f, 0.10f);
        csRect.anchorMax = new Vector2(0.94f, 0.90f);
        csRect.sizeDelta = Vector2.zero;

        Image csBg = comingSoonBox.AddComponent<Image>();
        csBg.color = new Color(0.015f, 0.025f, 0.045f, 0.88f);
        Outline csOutline = comingSoonBox.AddComponent<Outline>();
        csOutline.effectColor = new Color(0.20f, 0.65f, 0.95f, 0.40f);
        csOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Texto 1: "✦ EM BREVE ✦"
        CreateLabel(comingSoonBox.transform, "LabelEmBreve", "✦ EM BREVE ✦",
            new Vector2(0f, 48f), 30, Color.white, true, TextAlignmentOptions.Center, new Vector2(340f, 38f));

        // Texto 2: "MODELO 3D EM DESENVOLVIMENTO"
        CreateLabel(comingSoonBox.transform, "LabelSub", "MODELO 3D INTERATIVO",
            new Vector2(0f, 12f), 17, TECH_CYAN, true, TextAlignmentOptions.Center, new Vector2(340f, 26f));

        // Texto 3: Nota explicativa solicitada pelo jogador
        CreateLabel(comingSoonBox.transform, "LabelDesc", "A representação tridimensional 360° deste espécime será adicionada em uma futura atualização do Oráculo.",
            new Vector2(0f, -42f), 14, TEXT_BODY, false, TextAlignmentOptions.Center, new Vector2(310f, 60f));

        // Subtítulo / Legenda técnica inferior
        CreateLabel(viewContainer.transform, "SpecimenHint", "✦ ANÁLISE DO ESPÉCIME BIOLÓGICO ✦",
            new Vector2(0f, -200f), 16, TECH_CYAN, true, TextAlignmentOptions.Center, new Vector2(400f, 30f));

        // --- Lado Direito: Dossiê e Estatísticas ---
        GameObject dossier = new GameObject("DossierContainer");
        dossier.transform.SetParent(enemyDetailModal.transform, false);
        RectTransform dr = dossier.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.52f, 0.08f);
        dr.anchorMax = new Vector2(0.96f, 0.94f);
        dr.sizeDelta = Vector2.zero;

        // Nome
        GameObject nameGO = new GameObject("ModalEnemyName");
        nameGO.transform.SetParent(dossier.transform, false);
        modalEnemyNameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        modalEnemyNameTMP.fontSize = 32;
        modalEnemyNameTMP.fontStyle = FontStyles.Bold;
        modalEnemyNameTMP.color = TEXT_TITLE;
        if (customFont != null) modalEnemyNameTMP.font = customFont;
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, 0f);
        nameRect.sizeDelta = new Vector2(-260f, 40f);

        // Classe
        GameObject classGO = new GameObject("ModalEnemyClass");
        classGO.transform.SetParent(dossier.transform, false);
        modalEnemyClassTMP = classGO.AddComponent<TextMeshProUGUI>();
        modalEnemyClassTMP.fontSize = 20;
        modalEnemyClassTMP.fontStyle = FontStyles.Bold;
        modalEnemyClassTMP.color = TECH_CYAN;
        if (customFont != null) modalEnemyClassTMP.font = customFont;
        RectTransform classRect = classGO.GetComponent<RectTransform>();
        classRect.anchorMin = new Vector2(0f, 1f);
        classRect.anchorMax = new Vector2(1f, 1f);
        classRect.pivot = new Vector2(0f, 1f);
        classRect.anchoredPosition = new Vector2(0f, -44f);
        classRect.sizeDelta = new Vector2(-260f, 28f);

        // Estatísticas de Combate
        GameObject statsGO = new GameObject("ModalEnemyStats");
        statsGO.transform.SetParent(dossier.transform, false);
        modalEnemyStatsTMP = statsGO.AddComponent<TextMeshProUGUI>();
        modalEnemyStatsTMP.fontSize = 19;
        modalEnemyStatsTMP.color = SUCCESS_COLOR;
        modalEnemyStatsTMP.richText = true;
        if (customFont != null) modalEnemyStatsTMP.font = customFont;
        RectTransform statsRect = statsGO.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0f, 1f);
        statsRect.anchorMax = new Vector2(1f, 1f);
        statsRect.pivot = new Vector2(0f, 1f);
        statsRect.anchoredPosition = new Vector2(0f, -80f);
        statsRect.sizeDelta = new Vector2(0f, 40f);

        // Descrição / Lore
        GameObject descGO = new GameObject("ModalEnemyDesc");
        descGO.transform.SetParent(dossier.transform, false);
        modalEnemyDescTMP = descGO.AddComponent<TextMeshProUGUI>();
        modalEnemyDescTMP.fontSize = 22;
        modalEnemyDescTMP.color = TEXT_BODY;
        modalEnemyDescTMP.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) modalEnemyDescTMP.font = customFont;
        RectTransform descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.offsetMin = new Vector2(0f, 15f);
        descRect.offsetMax = new Vector2(0f, -135f);

        enemyDetailModal.SetActive(false);
    }

    public void ExibirInspecaoInimigo(EnemyData data)
    {
        if (data == null) return;
        EnsureEnemyDetailModal();

        PlayBeep();

        Color classColor = ObterCorDaClasse(data.enemyClass);
        modalEnemyNameTMP.text = data.enemyName.ToUpper();
        modalEnemyClassTMP.text = $"[ CLASSE / AMEAÇA: {(!string.IsNullOrEmpty(data.enemyClass) ? data.enemyClass.ToUpper() : "ESPÉCIME ANÔMALO")} ]";
        modalEnemyClassTMP.color = classColor;

        string hpStr = data.vidaBase > 0 ? $"{data.vidaBase}" : "Não quantificado";
        string dmgStr = data.danoBase > 0 ? $"{data.danoBase:F0}" : "Não quantificado";
        modalEnemyStatsTMP.text = $"<b>VIDA BASE:</b> <color=#FFE500>{hpStr}</color>   |   <b>DANO:</b> <color=#FF5555>{dmgStr}</color>   |   <b>SPAWN BUDGET:</b> <color=#38BDF8>{data.custoBudget}</color>";

        modalEnemyDescTMP.text = !string.IsNullOrEmpty(data.descricao)
            ? data.descricao
            : "Registro preliminar coletado pelos sensores de bordo da nave. Padrões de ataque e fraquezas em análise.";

        if (modalEnemyIconImg != null)
        {
            if (data.icon != null)
            {
                modalEnemyIconImg.sprite = data.icon;
                modalEnemyIconImg.color = Color.white;
            }
            else
            {
                modalEnemyIconImg.sprite = null;
                modalEnemyIconImg.color = new Color(0.2f, 0.4f, 0.6f, 0.3f);
            }
        }

        enemyDetailModal.SetActive(true);
    }

    public void FecharInspecaoInimigo()
    {
        if (enemyDetailModal != null)
        {
            enemyDetailModal.SetActive(false);
        }
    }

    private void CreateEnemyCard(Transform parent, EnemyData data)
    {
        Color classColor = ObterCorDaClasse(data.enemyClass);

        // Card container (1080 x 160) com borda na cor da classe!
        GameObject card = new GameObject("Card_" + data.enemyName);
        card.transform.SetParent(parent, false);
        StyleCardBase(card, 160f, classColor, 6f);

        Button cardBtn = card.AddComponent<Button>();
        EnemyData capturedData = data;
        cardBtn.onClick.AddListener(() => ExibirInspecaoInimigo(capturedData));

        // Moldura Inset para o Ícone do Inimigo estilo slot de inventário
        GameObject iconFrame = new GameObject("IconFrame");
        iconFrame.transform.SetParent(card.transform, false);
        Image frameBg = iconFrame.AddComponent<Image>();
        frameBg.color = CARD_INSET_BG;
        RectTransform frameRect = iconFrame.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(125f, 125f);
        frameRect.anchorMin = new Vector2(0f, 0.5f);
        frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.anchoredPosition = new Vector2(18f, 0f);

        // Borda e sombra da moldura
        Outline iconOutline = iconFrame.AddComponent<Outline>();
        iconOutline.effectColor = classColor;
        iconOutline.effectDistance = new Vector2(1.5f, -1.5f);
        Shadow iconShadow = iconFrame.AddComponent<Shadow>();
        iconShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        iconShadow.effectDistance = new Vector2(2f, -2f);

        if (data.icon != null)
        {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(iconFrame.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = data.icon;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(115f, 115f);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        // Texto - Nome
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = data.enemyName.ToUpper();
        nameTMP.fontSize = 28;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = TEXT_TITLE;
        if (customFont != null) nameTMP.font = customFont;
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(165f, -16f);
        nameRect.sizeDelta = new Vector2(-380f, 30f);

        // Badge de Status (Top-Right): "[ 🔍 INSPECIONAR ]"
        GameObject statusGO = new GameObject("StatusTag");
        statusGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text = "🔍 INSPECIONAR";
        statusTMP.fontSize = 16;
        statusTMP.fontStyle = FontStyles.Bold;
        statusTMP.color = classColor;
        statusTMP.alignment = TextAlignmentOptions.Right;
        if (customFont != null) statusTMP.font = customFont;
        RectTransform statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(1f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(1f, 1f);
        statusRect.anchoredPosition = new Vector2(-22f, -16f);
        statusRect.sizeDelta = new Vector2(240f, 26f);

        // Texto - Classe
        if (!string.IsNullOrEmpty(data.enemyClass))
        {
            GameObject classGO = new GameObject("Class");
            classGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI classTMP = classGO.AddComponent<TextMeshProUGUI>();
            classTMP.text = "<i>[ CLASSE: " + data.enemyClass.ToUpper() + " ]</i>";
            classTMP.fontSize = 18;
            classTMP.fontStyle = FontStyles.Bold;
            classTMP.color = classColor;
            RectTransform classRect = classGO.GetComponent<RectTransform>();
            classRect.anchorMin = new Vector2(0f, 1f);
            classRect.anchorMax = new Vector2(1f, 1f);
            classRect.pivot = new Vector2(0f, 1f);
            classRect.anchoredPosition = new Vector2(165f, -48f);
            classRect.sizeDelta = new Vector2(-380f, 22f);
        }

        // Texto - Descrição/Lore
        if (!string.IsNullOrEmpty(data.descricao))
        {
            GameObject descGO = new GameObject("Desc");
            descGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text = data.descricao;
            descTMP.fontSize = 21;
            descTMP.color = TEXT_BODY;
            descTMP.textWrappingMode = TextWrappingModes.Normal;
            RectTransform descRect = descGO.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0f, 1f);
            descRect.offsetMin = new Vector2(165f, 14f);
            descRect.offsetMax = new Vector2(-22f, -74f);
        }
    }

    private void CreateItemCard(Transform parent, ItemData data)
    {
        bool isInfused = CatalogoManager.instancia != null && CatalogoManager.instancia.IsItemInfused(data.itemId);
        Color accentColor = isInfused ? SUCCESS_COLOR : new Color(1.00f, 0.75f, 0.20f, 1.00f);

        // Card container (1080 x 130)
        GameObject card = new GameObject("Card_" + data.itemId);
        card.transform.SetParent(parent, false);
        StyleCardBase(card, 130f, accentColor, 5f);

        // Moldura do Ícone estilo slot de inventário
        GameObject iconFrame = new GameObject("IconFrame");
        iconFrame.transform.SetParent(card.transform, false);
        Image frameBg = iconFrame.AddComponent<Image>();
        frameBg.color = CARD_INSET_BG;
        RectTransform frameRect = iconFrame.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(90f, 90f);
        frameRect.anchorMin = new Vector2(0f, 0.5f);
        frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.anchoredPosition = new Vector2(18f, 0f);

        Outline iconOutline = iconFrame.AddComponent<Outline>();
        iconOutline.effectColor = isInfused ? new Color(0.25f, 0.85f, 0.55f, 0.6f) : CARD_INSET_BORDER;
        iconOutline.effectDistance = new Vector2(1.5f, -1.5f);
        Shadow iconShadow = iconFrame.AddComponent<Shadow>();
        iconShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        iconShadow.effectDistance = new Vector2(2f, -2f);

        if (data.icon != null)
        {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(iconFrame.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = data.icon;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(80f, 80f);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        // Nome do Item
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = data.itemName.ToUpper();
        nameTMP.fontSize = 22;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = TEXT_TITLE;
        if (customFont != null) nameTMP.font = customFont;
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(125f, -16f);
        nameRect.sizeDelta = new Vector2(-420f, 28f);

        // Badge de Categoria (Top-Right): "● ATRIBUTOS DECODIFICADOS" / "● NÃO ANALISADO"
        GameObject tagGO = new GameObject("ItemTag");
        tagGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI tagTMP = tagGO.AddComponent<TextMeshProUGUI>();
        tagTMP.text = isInfused ? "● ATRIBUTOS DECODIFICADOS" : "● NÃO ANALISADO";
        tagTMP.fontSize = 13;
        tagTMP.fontStyle = FontStyles.Bold;
        tagTMP.color = accentColor;
        tagTMP.alignment = TextAlignmentOptions.Right;
        if (customFont != null) tagTMP.font = customFont;
        RectTransform tagRect = tagGO.GetComponent<RectTransform>();
        tagRect.anchorMin = new Vector2(1f, 1f);
        tagRect.anchorMax = new Vector2(1f, 1f);
        tagRect.pivot = new Vector2(1f, 1f);
        tagRect.anchoredPosition = new Vector2(-22f, -16f);
        tagRect.sizeDelta = new Vector2(280f, 24f);

        // Descrição e Atributos do Item
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        
        string descText = !string.IsNullOrEmpty(data.description) ? data.description : "";
        if (isInfused)
        {
            if (data.itemAttributes != null && data.itemAttributes.Count > 0)
            {
                string buffsStr = "";
                foreach (var buff in data.itemAttributes)
                {
                    float displayVal = buff.isMultiplier ? (buff.value - 1f) * 100f : buff.value;
                    string signal = displayVal > 0 ? "+" : "";
                    string suffix = buff.isMultiplier ? "%" : "";
                    buffsStr += $"  <color=#00FFAA>• {signal}{displayVal:0.##}{suffix}</color> {buff.attributeType}";
                }
                descText += $"\n<size=85%><color=#80C0FF><b>Atributos decodificados:</b></color>{buffsStr}</size>";
            }
        }
        else
        {
            descText += "\n<size=85%><color=#FFCC00>• [ ? ? ? ] Propriedades Ocultas — Infunda no traje para decodificar no Catálogo.</color></size>";
        }

        descTMP.text = descText;
        descTMP.fontSize = 20;
        descTMP.color = TEXT_BODY;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.offsetMin = new Vector2(125f, 10f);
        descRect.offsetMax = new Vector2(-22f, -48f);
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

        if (size != Vector2.zero)
        {
            // Drop Shadow para efeito de profundidade suspensa (estilo inventário)
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.70f);
            shadow.effectDistance = new Vector2(10f, -10f);

            // Borda exterior nítida (Outline)
            Outline outerOutline = go.AddComponent<Outline>();
            outerOutline.effectColor = PANEL_BORDER;
            outerOutline.effectDistance = new Vector2(2f, -2f);

            // Borda interior decorativa com brilho tech
            GameObject innerBorderObj = new GameObject("PanelInnerBorder");
            innerBorderObj.transform.SetParent(go.transform, false);
            RectTransform innerRt = innerBorderObj.AddComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(5f, 5f);
            innerRt.offsetMax = new Vector2(-5f, -5f);
            Image innerImg = innerBorderObj.AddComponent<Image>();
            innerImg.color = new Color(0.18f, 0.35f, 0.55f, 0.08f);
            innerImg.raycastTarget = false;
            Outline innerOutline = innerBorderObj.AddComponent<Outline>();
            innerOutline.effectColor = PANEL_BORDER_INNER;
            innerOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Reflexo superior de vidro (Glass Highlight)
            GameObject glassHighlight = new GameObject("GlassHighlight");
            glassHighlight.transform.SetParent(go.transform, false);
            RectTransform ghRt = glassHighlight.AddComponent<RectTransform>();
            ghRt.anchorMin = new Vector2(0f, 1f);
            ghRt.anchorMax = new Vector2(1f, 1f);
            ghRt.pivot = new Vector2(0.5f, 1f);
            ghRt.anchoredPosition = new Vector2(0f, -2f);
            ghRt.sizeDelta = new Vector2(-12f, 2.5f);
            Image ghImg = glassHighlight.AddComponent<Image>();
            ghImg.color = new Color(1f, 1f, 1f, 0.18f);
            ghImg.raycastTarget = false;

            // Acento luminoso no topo (Top Accent)
            GameObject topAccent = new GameObject("TopAccent");
            topAccent.transform.SetParent(go.transform, false);
            RectTransform taRt = topAccent.AddComponent<RectTransform>();
            taRt.anchorMin = new Vector2(0.18f, 1f);
            taRt.anchorMax = new Vector2(0.82f, 1f);
            taRt.pivot = new Vector2(0.5f, 1f);
            taRt.anchoredPosition = new Vector2(0f, 0f);
            taRt.sizeDelta = new Vector2(0f, 3f);
            Image taImg = topAccent.AddComponent<Image>();
            taImg.color = TECH_CYAN;
            taImg.raycastTarget = false;

            // Cantoneiras táticas nos 4 cantos
            AddCornerBrackets(go, TECH_CYAN_DIM, 26f, 2.5f);
        }

        return go;
    }

    private void StyleCardBase(GameObject card, float height, Color accentBorderColor, float accentWidth = 6f)
    {
        Image cardBg = card.GetComponent<Image>();
        if (cardBg == null) cardBg = card.AddComponent<Image>();
        cardBg.color = CARD_BG;

        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect == null) cardRect = card.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(1080f, height);

        LayoutElement le = card.GetComponent<LayoutElement>();
        if (le == null) le = card.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        // Sombra suave de profundidade
        Shadow shadow = card.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(4f, -4f);

        // Borda perimetral completa estilo slot do inventário
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = CARD_BORDER;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Reflexo fino de vidro no topo do card
        GameObject gh = new GameObject("CardGlassHighlight");
        gh.transform.SetParent(card.transform, false);
        RectTransform ghRt = gh.AddComponent<RectTransform>();
        ghRt.anchorMin = new Vector2(0f, 1f);
        ghRt.anchorMax = new Vector2(1f, 1f);
        ghRt.pivot = new Vector2(0.5f, 1f);
        ghRt.anchoredPosition = new Vector2(0f, 0f);
        ghRt.sizeDelta = new Vector2(0f, 1.5f);
        Image ghImg = gh.AddComponent<Image>();
        ghImg.color = new Color(1f, 1f, 1f, 0.08f);
        ghImg.raycastTarget = false;

        // Borda de acento no lado esquerdo
        GameObject border = new GameObject("Border");
        border.transform.SetParent(card.transform, false);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = accentBorderColor;
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.sizeDelta = new Vector2(accentWidth, height);
        borderRect.anchorMin = new Vector2(0f, 0.5f);
        borderRect.anchorMax = new Vector2(0f, 0.5f);
        borderRect.pivot = new Vector2(0f, 0.5f);
        borderRect.anchoredPosition = Vector2.zero;
    }

    private void AddCornerBrackets(GameObject parent, Color bracketColor, float length, float thickness)
    {
        CreateCornerL(parent.transform, "CornerTL", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), length, thickness, bracketColor);
        CreateCornerL(parent.transform, "CornerTR", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), length, thickness, bracketColor);
        CreateCornerL(parent.transform, "CornerBL", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), length, thickness, bracketColor);
        CreateCornerL(parent.transform, "CornerBR", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), length, thickness, bracketColor);
    }

    private void CreateCornerL(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float len, float thick, Color color)
    {
        GameObject cornerObj = new GameObject(name);
        cornerObj.transform.SetParent(parent, false);
        RectTransform cRt = cornerObj.AddComponent<RectTransform>();
        cRt.anchorMin = anchorMin;
        cRt.anchorMax = anchorMax;
        cRt.pivot = pivot;
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(len, len);

        GameObject hBar = new GameObject("H");
        hBar.transform.SetParent(cornerObj.transform, false);
        Image hImg = hBar.AddComponent<Image>();
        hImg.color = color;
        hImg.raycastTarget = false;
        RectTransform hRt = hBar.GetComponent<RectTransform>();
        hRt.anchorMin = pivot;
        hRt.anchorMax = pivot;
        hRt.pivot = pivot;
        hRt.sizeDelta = new Vector2(len, thick);
        hRt.anchoredPosition = Vector2.zero;

        GameObject vBar = new GameObject("V");
        vBar.transform.SetParent(cornerObj.transform, false);
        Image vImg = vBar.AddComponent<Image>();
        vImg.color = color;
        vImg.raycastTarget = false;
        RectTransform vRt = vBar.GetComponent<RectTransform>();
        vRt.anchorMin = pivot;
        vRt.anchorMax = pivot;
        vRt.pivot = pivot;
        vRt.sizeDelta = new Vector2(thick, len);
        vRt.anchoredPosition = Vector2.zero;
    }

    private GameObject CreateLabel(Transform parent, string name, string text, Vector2 pos,
        float fontSize, Color color, bool bold, TextAlignmentOptions alignment = TextAlignmentOptions.Center, Vector2? size = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        if (customFont != null) tmp.font = customFont;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size ?? new Vector2(700f, 40f);
        rt.anchoredPosition = pos;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = rt.sizeDelta.y;
        le.preferredHeight = rt.sizeDelta.y;

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
        btn.onClick.AddListener(() =>
        {
            PlayBeep();
            onClick?.Invoke();
        });
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        // Borda perimetral do botão
        Outline btnOutline = go.AddComponent<Outline>();
        btnOutline.effectColor = TAB_INACT_BORDER;
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Indicador inferior ativo (barra ciano que acende na aba selecionada)
        GameObject indicatorObj = new GameObject("ActiveIndicator");
        indicatorObj.transform.SetParent(go.transform, false);
        RectTransform indRt = indicatorObj.AddComponent<RectTransform>();
        indRt.anchorMin = new Vector2(0f, 0f);
        indRt.anchorMax = new Vector2(1f, 0f);
        indRt.pivot = new Vector2(0.5f, 0f);
        indRt.sizeDelta = new Vector2(0f, 3f);
        indRt.anchoredPosition = Vector2.zero;
        Image indImg = indicatorObj.AddComponent<Image>();
        indImg.color = TECH_CYAN;
        indImg.raycastTarget = false;
        indicatorObj.SetActive(false);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 15;
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
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(16, 16, 16, 16);
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
        sr.scrollSensitivity = 35f;

        return svGO;
    }

    private void UpdateTabVisuals(GameObject tabObj, bool isActive)
    {
        if (tabObj == null) return;
        Image img = tabObj.GetComponent<Image>();
        if (img != null) img.color = isActive ? TAB_ACTIVE : TAB_INACTIVE;

        Outline outline = tabObj.GetComponent<Outline>();
        if (outline != null) outline.effectColor = isActive ? TAB_ACTIVE_BORDER : TAB_INACT_BORDER;

        Transform indTr = tabObj.transform.Find("ActiveIndicator");
        if (indTr != null) indTr.gameObject.SetActive(isActive);

        Transform labelTr = tabObj.transform.Find("Label");
        if (labelTr != null)
        {
            TextMeshProUGUI tmp = labelTr.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = isActive ? Color.white : TEXT_BODY;
                tmp.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
                tmp.fontSize = isActive ? 22 : 20;
            }
        }
    }
}


