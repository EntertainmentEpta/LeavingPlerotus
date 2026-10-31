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

    // Modal de Transcrição e Leitura de Diálogos da Lore
    private GameObject dialogTranscriptModal;
    private TextMeshProUGUI dialogModalTitleTMP;
    private TextMeshProUGUI dialogModalContextTMP;
    private Transform dialogModalScrollContent;
    private string currentTranscriptDialogId;

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
    private GameObject  tabLore;
    private GameObject  tabMercador;
    private GameObject  contentQuests;
    private GameObject  contentBestiary;
    private GameObject  contentCatalogo;
    private GameObject  contentLore;
    private GameObject  contentMercador;
    private int         currentTab = 0; // 0 = Quests, 1 = Bestiary, 2 = Catalogo, 3 = Lore, 4 = Mercador
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
            if (dialogTranscriptModal != null && dialogTranscriptModal.activeSelf)
                FecharTranscricaoDialogo();
            else if (enemyDetailModal != null && enemyDetailModal.activeSelf)
                FecharInspecaoInimigo();
            else
                FecharMenu();
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

        FecharTranscricaoDialogo();
        FecharInspecaoInimigo();

        PlayBeep();
        if (panelObj != null) panelObj.SetActive(false);

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

        // ── 5 Abas de Navegação Tática ──
        float tabY = 265f;
        float tabW = 216f;
        tabQuests = CreateButton(panelObj.transform, "TabQuests", "MISSÕES",
            new Vector2(-448f, tabY), new Vector2(tabW, 48f), TAB_ACTIVE, () => MostrarAba(0));

        tabBestiary = CreateButton(panelObj.transform, "TabBestiary", "BESTIÁRIO",
            new Vector2(-224f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(1));

        tabCatalogo = CreateButton(panelObj.transform, "TabCatalogo", "CATÁLOGO",
            new Vector2(0f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(2));

        tabLore = CreateButton(panelObj.transform, "TabLore", "REGISTROS LORE",
            new Vector2(224f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(3));

        tabMercador = CreateButton(panelObj.transform, "TabMercador", "O MERCADOR",
            new Vector2(448f, tabY), new Vector2(tabW, 48f), TAB_INACTIVE, () => MostrarAba(4));

        // ── Áreas de Conteúdo com Scroll ──
        contentQuests = CreateScrollArea(panelObj.transform, "ContentQuests",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));

        contentBestiary = CreateScrollArea(panelObj.transform, "ContentBestiary",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));
        contentBestiary.SetActive(false);

        contentCatalogo = CreateScrollArea(panelObj.transform, "ContentCatalogo",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));
        contentCatalogo.SetActive(false);

        contentLore = CreateScrollArea(panelObj.transform, "ContentLore",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));
        contentLore.SetActive(false);

        contentMercador = CreateScrollArea(panelObj.transform, "ContentMercador",
            new Vector2(0f, -80f), new Vector2(1120f, 560f));
        contentMercador.SetActive(false);

        menuUI = panelObj;
        panelObj.SetActive(false);
        uiBuilt = true;

        UpdateTabVisuals(tabQuests, true);
        UpdateTabVisuals(tabBestiary, false);
        UpdateTabVisuals(tabCatalogo, false);
        UpdateTabVisuals(tabLore, false);
        UpdateTabVisuals(tabMercador, false);
    }

    private void MostrarAba(int abaIndex)
    {
        FecharTranscricaoDialogo();
        FecharInspecaoInimigo();

        currentTab = abaIndex;
        PlayBeep();

        contentQuests.SetActive(abaIndex == 0);
        contentBestiary.SetActive(abaIndex == 1);
        contentCatalogo.SetActive(abaIndex == 2);
        contentLore.SetActive(abaIndex == 3);
        contentMercador.SetActive(abaIndex == 4);

        UpdateTabVisuals(tabQuests, abaIndex == 0);
        UpdateTabVisuals(tabBestiary, abaIndex == 1);
        UpdateTabVisuals(tabCatalogo, abaIndex == 2);
        UpdateTabVisuals(tabLore, abaIndex == 3);
        UpdateTabVisuals(tabMercador, abaIndex == 4);

        RefreshCurrentTab();
    }

    private void RefreshCurrentTab()
    {
        if (currentTab == 0) RefreshQuests();
        else if (currentTab == 1) RefreshBestiary();
        else if (currentTab == 2) RefreshCatalogo();
        else if (currentTab == 3) RefreshLore();
        else if (currentTab == 4) RefreshMercador();
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
        StyleCardBase(card, 112f, SUCCESS_COLOR, 6f);

        // Badge concluída
        GameObject checkGO = new GameObject("Check");
        checkGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI checkTMP = checkGO.AddComponent<TextMeshProUGUI>();
        checkTMP.text = "✔ DIRETRIZ CONCLUÍDA";
        checkTMP.fontSize = 15;
        checkTMP.fontStyle = FontStyles.Bold;
        checkTMP.color = SUCCESS_COLOR;
        if (customFont != null) checkTMP.font = customFont;
        RectTransform checkRect = checkGO.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 1f);
        checkRect.anchorMax = new Vector2(1f, 1f);
        checkRect.pivot = new Vector2(0f, 1f);
        checkRect.anchoredPosition = new Vector2(24f, -12f);
        checkRect.sizeDelta = new Vector2(-48f, 18f);

        // Título nítido e proporcional
        string title = data != null ? data.questTitle : questId;
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title.ToUpper();
        titleTMP.fontSize = 21;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TEXT_TITLE;
        if (customFont != null) titleTMP.font = customFont;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(24f, -34f);
        titleRect.sizeDelta = new Vector2(-48f, 26f);

        // Descrição curta limpa, sem cortes
        string desc = data != null && !string.IsNullOrEmpty(data.questDescription) ? data.questDescription : "Diretriz cumprida e registrada pelo Eptinho.";
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(card.transform, false);
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = desc;
        descTMP.fontSize = 15;
        descTMP.color = TEXT_BODY;
        descTMP.lineSpacing = 1.15f;
        descTMP.textWrappingMode = TextWrappingModes.Normal;
        RectTransform descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.offsetMin = new Vector2(24f, 10f);
        descRect.offsetMax = new Vector2(-24f, -64f);
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

    // ── Aba: REGISTROS LORE (Transmissões e Diálogos do Eptinho) ──

    private void RefreshLore()
    {
        Transform grid = contentLore.transform.Find("Viewport/Content");
        if (grid == null)
        {
            Debug.LogError("[EPTINHO MENU] Viewport/Content nao encontrado em ContentLore!");
            return;
        }

        foreach (Transform child in grid) Destroy(child.gameObject);

        // Card de Topo: Cabeçalho elegante e limpo (altura 84f)
        GameObject topCard = new GameObject("HeaderCard");
        topCard.transform.SetParent(grid, false);
        StyleCardBase(topCard, 84f, TECH_CYAN, 4f);

        // Tag superior do cabeçalho
        GameObject hTagGO = new GameObject("HeaderTag");
        hTagGO.transform.SetParent(topCard.transform, false);
        TextMeshProUGUI hTagTMP = hTagGO.AddComponent<TextMeshProUGUI>();
        hTagTMP.text = "● REGISTROS DO ORÁCULO // ARQUIVO DE TRANSMISSÕES";
        hTagTMP.fontSize = 13;
        hTagTMP.fontStyle = FontStyles.Bold;
        hTagTMP.color = TECH_CYAN;
        hTagTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) hTagTMP.font = customFont;
        RectTransform hTagRt = hTagGO.GetComponent<RectTransform>();
        hTagRt.anchorMin = new Vector2(0f, 0.5f);
        hTagRt.anchorMax = new Vector2(1f, 0.5f);
        hTagRt.pivot = new Vector2(0f, 0.5f);
        hTagRt.anchoredPosition = new Vector2(24f, 15f);
        hTagRt.sizeDelta = new Vector2(-48f, 22f);

        // Título do cabeçalho
        GameObject hTitleGO = new GameObject("HeaderTitle");
        hTitleGO.transform.SetParent(topCard.transform, false);
        TextMeshProUGUI hTitleTMP = hTitleGO.AddComponent<TextMeshProUGUI>();
        hTitleTMP.text = "HISTÓRICO DE DIRETRIZES TÁTICAS & DIÁLOGOS DE CAMPO";
        hTitleTMP.fontSize = 21;
        hTitleTMP.fontStyle = FontStyles.Bold;
        hTitleTMP.color = TEXT_TITLE;
        hTitleTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) hTitleTMP.font = customFont;
        RectTransform hTitleRt = hTitleGO.GetComponent<RectTransform>();
        hTitleRt.anchorMin = new Vector2(0f, 0.5f);
        hTitleRt.anchorMax = new Vector2(1f, 0.5f);
        hTitleRt.pivot = new Vector2(0f, 0.5f);
        hTitleRt.anchoredPosition = new Vector2(24f, -14f);
        hTitleRt.sizeDelta = new Vector2(-48f, 28f);

        var records = EptinhoStoryDialogs.GetAllRecords();
        foreach (var rec in records)
        {
            CreateLoreCard(grid, rec);
        }
    }

    private void CreateLoreCard(Transform parent, EptinhoStoryDialogs.StoryDialogRecord rec)
    {
        bool seen = EptinhoStoryDialogs.HasSeenDialog(rec.id);
        float cardHeight = 84f;

        GameObject card = new GameObject("Lore_" + rec.id);
        card.transform.SetParent(parent, false);

        Color borderColor = seen ? new Color(0.20f, 0.65f, 0.85f, 0.70f) : new Color(0.15f, 0.20f, 0.28f, 0.40f);
        StyleCardBase(card, cardHeight, borderColor, 4f);

        // Se visto, o card inteiro pode ser clicado para abrir o diálogo
        if (seen)
        {
            Button cardBtn = card.AddComponent<Button>();
            EptinhoStoryDialogs.StoryDialogRecord capturedRec = rec;
            cardBtn.onClick.AddListener(() => ExibirTranscricaoDialogo(capturedRec));
        }

        // Tag / Contexto (Centralizado verticalmente na metade superior: y = +15)
        // Container vertical à esquerda para Tag e Título (evita sobreposições e centraliza perfeitamente)
        GameObject textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(card.transform, false);
        RectTransform tcRt = textContainer.AddComponent<RectTransform>();
        tcRt.anchorMin = new Vector2(0f, 0f);
        tcRt.anchorMax = new Vector2(1f, 1f);
        tcRt.offsetMin = new Vector2(24f, 8f);
        tcRt.offsetMax = new Vector2(-200f, -8f);

        VerticalLayoutGroup tcVlg = textContainer.AddComponent<VerticalLayoutGroup>();
        tcVlg.spacing = 4f;
        tcVlg.childAlignment = TextAnchor.MiddleLeft;
        tcVlg.childControlWidth = true;
        tcVlg.childControlHeight = true;
        tcVlg.childForceExpandWidth = true;
        tcVlg.childForceExpandHeight = false;

        // Tag / Contexto
        GameObject tagGO = new GameObject("Tag");
        tagGO.transform.SetParent(textContainer.transform, false);
        TextMeshProUGUI tagTMP = tagGO.AddComponent<TextMeshProUGUI>();
        tagTMP.fontSize = 12;
        tagTMP.fontStyle = FontStyles.Bold;
        tagTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) tagTMP.font = customFont;
        LayoutElement tagLe = tagGO.AddComponent<LayoutElement>();
        tagLe.preferredHeight = 18f;

        // Título
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(textContainer.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.fontSize = 20;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) titleTMP.font = customFont;
        LayoutElement titleLe = titleGO.AddComponent<LayoutElement>();
        titleLe.preferredHeight = 28f;

        if (seen)
        {
            tagTMP.text = $"<color=#00FFAA>●</color> <color=#38BDF8>{rec.eventContext.ToUpper()}</color>";
            titleTMP.text = rec.title.ToUpper();
            titleTMP.color = TEXT_TITLE;

            EptinhoStoryDialogs.StoryDialogRecord capturedRec = rec;
            GameObject btnLer = CreateButton(card.transform, "BtnLer", "LER DIÁLOGO",
                new Vector2(-20f, 0f), new Vector2(170f, 44f), new Color(0.10f, 0.28f, 0.44f, 0.95f), () =>
                {
                    ExibirTranscricaoDialogo(capturedRec);
                });
            RectTransform bRt = btnLer.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-20f, 0f);
            Outline bOut = btnLer.GetComponent<Outline>();
            if (bOut != null) bOut.effectColor = TECH_CYAN;
            Transform lbl = btnLer.transform.Find("Label");
            if (lbl != null)
            {
                var lTmp = lbl.GetComponent<TextMeshProUGUI>();
                if (lTmp != null)
                {
                    lTmp.fontSize = 15;
                    lTmp.fontStyle = FontStyles.Bold;
                }
            }
        }
        else
        {
            tagTMP.text = $"<color=#64748B>○</color> <color=#94A3B8>{rec.eventContext.ToUpper()}</color>";
            titleTMP.text = $"[ BLOQUEADO ]  {rec.title.ToUpper()}";
            titleTMP.color = new Color(0.55f, 0.60f, 0.70f, 0.55f);

            GameObject btnLock = CreateButton(card.transform, "BtnLock", "BLOQUEADO",
                new Vector2(-20f, 0f), new Vector2(150f, 40f), new Color(0.12f, 0.15f, 0.20f, 0.60f), null);
            RectTransform bRt = btnLock.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-20f, 0f);
            Button bComp = btnLock.GetComponent<Button>();
            if (bComp != null) bComp.interactable = false;
            Outline bOut = btnLock.GetComponent<Outline>();
            if (bOut != null) bOut.effectColor = new Color(0.25f, 0.35f, 0.45f, 0.30f);
            Transform lbl = btnLock.transform.Find("Label");
            if (lbl != null)
            {
                var lTmp = lbl.GetComponent<TextMeshProUGUI>();
                if (lTmp != null)
                {
                    lTmp.fontSize = 14;
                    lTmp.color = new Color(0.50f, 0.55f, 0.65f, 0.70f);
                }
            }
        }
    }

    // ── Aba: O MERCADOR (Decodificação Progressiva de Frequência) ──

    private void RefreshMercador()
    {
        Transform grid = contentMercador.transform.Find("Viewport/Content");
        if (grid == null)
        {
            Debug.LogError("[EPTINHO MENU] Viewport/Content nao encontrado em ContentMercador!");
            return;
        }

        foreach (Transform child in grid) Destroy(child.gameObject);

        int encounterCount = SaveManager.instance != null && SaveManager.instance.CachedData != null
            ? SaveManager.instance.CachedData.merchantEncounterCount
            : 0;

        int totalEnigmas = MerchantEncounterDialogueSystem.TotalEnigmas;
        int pct = Mathf.Clamp(encounterCount * 5, 0, 100);

        // ── Card Superior: Dossiê e Estado de Cifragem da Entidade ──
        GameObject topCard = new GameObject("MerchantDossierCard");
        topCard.transform.SetParent(grid, false);
        float topHeight = encounterCount >= 20 ? 195f : 165f;
        Color topBorder = encounterCount >= 20 ? new Color(0.95f, 0.20f, 0.25f, 0.90f) : TECH_CYAN;
        StyleCardBase(topCard, topHeight, topBorder, 4f);

        string statusText = encounterCount == 0
            ? "◆ STATUS: 0% DECODIFICADO // SINAL DESCONHECIDO"
            : (encounterCount >= 20
                ? "<color=#EF4444>⚠ ALERTA CRÍTICO // IDENTIDADE REVELADA: ENTIDADE HOSTIL SUPREMA (100%)</color>"
                : $"<color=#38BDF8>✦ DECODIFICAÇÃO DE FREQUÊNCIA: {pct}% CONCLUÍDA ({encounterCount}/{totalEnigmas} ENCONTROS)</color>");

        Color statusColor = encounterCount == 0 ? new Color(0.95f, 0.35f, 0.35f, 1f) : TECH_CYAN;

        string titleText = encounterCount == 0
            ? "A ENTIDADE DAS PROFUNDEZAS // O MERCADOR"
            : (encounterCount >= 20
                ? "O ARQUITETO DO ESPORO // EX-MERCADOR"
                : "DOSSIÊ DE CAMPO // O MERCADOR DAS SOMBRAS");

        string briefingText = "";
        if (encounterCount == 0)
        {
            briefingText = "Sensores da nave captaram vibrações sonoras e atrito mineral na faixa ultra-baixa vindas dos níveis inferiores.\n" +
                           "Exploradores relatam uma figura misteriosa negociando artefatos nas cavernas. Nenhuma linha de seu dialeto foi decodificada.\n" +
                           "<color=#FFE500>Diretriz:</color> Encontre a entidade durante suas incursões pelo planeta Plerotus para interceptar transmissões e decifrar o mistério.";
        }
        else if (encounterCount < 5)
        {
            briefingText = "A entidade comunica-se por atritos ásperos de anéis minerais nos dedos. Ela parece consciente da queda da nossa nave na cratera e demonstra desdém pelos tripulantes humanos.";
        }
        else if (encounterCount < 10)
        {
            briefingText = "O Oráculo identificou menções a uma civilização antiga chamada 'Os Lapidadores'. O Mercador revela uma fixação obsessiva e perturbadora pelo sangue do Piloto, tratando-o como catalisador.";
        }
        else if (encounterCount < 15)
        {
            briefingText = "O suposto comerciante conhece os segredos da infecção por esporos e os segredos das armas ancestrais. O comércio de Cartas de Maldição foi planejado para drenar a força vital dos invasores.";
        }
        else if (encounterCount < 20)
        {
            briefingText = "A linguagem foi quase 100% traduzida! O tom do Mercador abandonou a fachada de negócios. Uma revelação crítica sobre o destino do planeta Plerotus está prestes a emergir.";
        }
        else
        {
            briefingText = "<color=#FFAAAA>A MÁSCARA CAIU! O Mercador é o arquiteto da calamidade de Plerotus, responsável direto pelo esporo parasitário e pela ruína dos mineradores originais. Ele não quer negociar — ele quer seu sangue ancestral!</color>";
        }

        // 1. Tag de Status (Top-Left)
        GameObject statusGO = new GameObject("StatusTag");
        statusGO.transform.SetParent(topCard.transform, false);
        TextMeshProUGUI statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text = statusText;
        statusTMP.fontSize = 14;
        statusTMP.fontStyle = FontStyles.Bold;
        statusTMP.color = statusColor;
        if (customFont != null) statusTMP.font = customFont;
        RectTransform statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(24f, -14f);
        statusRect.sizeDelta = new Vector2(-48f, 20f);

        // 2. Título Imponente e Nítido
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(topCard.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = titleText;
        titleTMP.fontSize = 22;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = TEXT_TITLE;
        if (customFont != null) titleTMP.font = customFont;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(24f, -38f);
        titleRect.sizeDelta = new Vector2(-48f, 28f);

        // 3. Briefing de Leitura Limpa e Confortável
        GameObject briefGO = new GameObject("Briefing");
        briefGO.transform.SetParent(topCard.transform, false);
        TextMeshProUGUI briefTMP = briefGO.AddComponent<TextMeshProUGUI>();
        briefTMP.text = briefingText;
        briefTMP.fontSize = 15;
        briefTMP.color = TEXT_BODY;
        briefTMP.lineSpacing = 1.15f;
        briefTMP.textWrappingMode = TextWrappingModes.Normal;
        RectTransform briefRect = briefGO.GetComponent<RectTransform>();
        briefRect.anchorMin = new Vector2(0f, 1f);
        briefRect.anchorMax = new Vector2(1f, 1f);
        briefRect.pivot = new Vector2(0f, 1f);
        briefRect.anchoredPosition = new Vector2(24f, -70f);
        briefRect.sizeDelta = new Vector2(-48f, topHeight - 78f);

        // ── Lista de 20 Registros de Encontros Decodificados ──
        for (int i = 0; i < totalEnigmas; i++)
        {
            bool isUnlocked = i < encounterCount;
            CreateMerchantEncounterCard(grid, i, isUnlocked);
        }
    }

    private void CreateMerchantEncounterCard(Transform parent, int encounterIndex, bool isUnlocked)
    {
        int encNum = encounterIndex + 1;
        int pct = encNum * 5;

        GameObject card = new GameObject($"MerchantEncounter_{encNum}");
        card.transform.SetParent(parent, false);

        if (isUnlocked)
        {
            var line = MerchantEncounterDialogueSystem.GetEncounterByIndex(encounterIndex);
            float cardHeight = 152f;

            Color borderColor = encNum == 20
                ? new Color(0.95f, 0.20f, 0.25f, 0.90f)
                : new Color(0.25f, 0.70f, 0.90f, 0.70f);

            StyleCardBase(card, cardHeight, borderColor, 4f);

            string badgeColor = encNum == 20 ? "#EF4444" : "#38BDF8";

            // Tag do Encontro
            GameObject tagGO = new GameObject("Tag");
            tagGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI tagTMP = tagGO.AddComponent<TextMeshProUGUI>();
            tagTMP.text = $"<color={badgeColor}>● ENCONTRO #{encNum} // {line.percentageBadge}</color>";
            tagTMP.fontSize = 14;
            tagTMP.fontStyle = FontStyles.Bold;
            tagTMP.color = TECH_CYAN;
            if (customFont != null) tagTMP.font = customFont;
            RectTransform tagRt = tagGO.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0f, 1f);
            tagRt.anchorMax = new Vector2(1f, 1f);
            tagRt.pivot = new Vector2(0f, 1f);
            tagRt.anchoredPosition = new Vector2(24f, -14f);
            tagRt.sizeDelta = new Vector2(-48f, 20f);

            // Fala do Mercador
            GameObject speechGO = new GameObject("Speech");
            speechGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI speechTMP = speechGO.AddComponent<TextMeshProUGUI>();
            speechTMP.text = "<i>\"" + line.merchantSpeech + "\"</i>";
            speechTMP.fontSize = 15;
            speechTMP.color = new Color(0.92f, 0.94f, 0.97f, 1f);
            speechTMP.lineSpacing = 1.15f;
            speechTMP.textWrappingMode = TextWrappingModes.Normal;
            RectTransform speechRt = speechGO.GetComponent<RectTransform>();
            speechRt.anchorMin = new Vector2(0f, 1f);
            speechRt.anchorMax = new Vector2(1f, 1f);
            speechRt.pivot = new Vector2(0f, 1f);
            speechRt.anchoredPosition = new Vector2(24f, -38f);
            speechRt.sizeDelta = new Vector2(-48f, 44f);

            // Anotação do Eptinho
            GameObject noteGO = new GameObject("EptinhoNote");
            noteGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI noteTMP = noteGO.AddComponent<TextMeshProUGUI>();
            noteTMP.text = $"<color=#FBBF24>ANOTAÇÃO DO EPTINHO:</color> {line.eptinhoComment}";
            noteTMP.fontSize = 14;
            noteTMP.color = new Color(0.70f, 0.85f, 1.0f, 0.95f);
            noteTMP.lineSpacing = 1.15f;
            noteTMP.textWrappingMode = TextWrappingModes.Normal;
            RectTransform noteRt = noteGO.GetComponent<RectTransform>();
            noteRt.anchorMin = new Vector2(0f, 1f);
            noteRt.anchorMax = new Vector2(1f, 1f);
            noteRt.pivot = new Vector2(0f, 1f);
            noteRt.anchoredPosition = new Vector2(24f, -90f);
            noteRt.sizeDelta = new Vector2(-48f, 48f);
        }
        else
        {
            StyleCardBase(card, 84f, new Color(0.15f, 0.20f, 0.28f, 0.40f), 4f);

            // Tag Bloqueada
            GameObject tagGO = new GameObject("Tag");
            tagGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI tagTMP = tagGO.AddComponent<TextMeshProUGUI>();
            tagTMP.text = $"○ ENCONTRO #{encNum} // SINAL CRIPTOGRAFADO ({pct}%)";
            tagTMP.fontSize = 14;
            tagTMP.fontStyle = FontStyles.Bold;
            tagTMP.color = TEXT_DIM;
            if (customFont != null) tagTMP.font = customFont;
            RectTransform tagRt = tagGO.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0f, 1f);
            tagRt.anchorMax = new Vector2(1f, 1f);
            tagRt.pivot = new Vector2(0f, 1f);
            tagRt.anchoredPosition = new Vector2(24f, -16f);
            tagRt.sizeDelta = new Vector2(-48f, 20f);

            // Texto criptografado
            GameObject encGO = new GameObject("Encrypted");
            encGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI encTMP = encGO.AddComponent<TextMeshProUGUI>();
            encTMP.text = "██████████████████████████████████████████████████ (Intercepte o Mercador em campo para decodificar)";
            encTMP.fontSize = 14;
            encTMP.color = new Color(0.40f, 0.45f, 0.55f, 0.6f);
            encTMP.textWrappingMode = TextWrappingModes.Normal;
            RectTransform encRt = encGO.GetComponent<RectTransform>();
            encRt.anchorMin = new Vector2(0f, 1f);
            encRt.anchorMax = new Vector2(1f, 1f);
            encRt.pivot = new Vector2(0f, 1f);
            encRt.anchoredPosition = new Vector2(24f, -44f);
            encRt.sizeDelta = new Vector2(-48f, 26f);
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

    public void AtualizarAbaLore()
    {
        if (contentLore != null && contentLore.activeSelf)
        {
            RefreshLore();
        }
    }

    public void AtualizarAbaMercador()
    {
        if (contentMercador != null && contentMercador.activeSelf)
        {
            RefreshMercador();
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
        r.anchorMin = new Vector2(0.015f, 0.03f);
        r.anchorMax = new Vector2(0.985f, 0.97f);
        r.sizeDelta = Vector2.zero;

        Image bg = enemyDetailModal.AddComponent<Image>();
        bg.color = new Color(0.035f, 0.045f, 0.070f, 0.98f);

        Outline outline = enemyDetailModal.AddComponent<Outline>();
        outline.effectColor = TECH_CYAN;
        outline.effectDistance = new Vector2(2f, -2f);

        // ── Top Header Bar (Dedicação exclusiva para título e botão fechar) ──
        GameObject headerBar = new GameObject("ModalHeaderBar");
        headerBar.transform.SetParent(enemyDetailModal.transform, false);
        RectTransform headerRt = headerBar.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 52f);

        Image headerBg = headerBar.AddComponent<Image>();
        headerBg.color = new Color(0.055f, 0.075f, 0.115f, 0.92f);

        GameObject hLine = new GameObject("HeaderSepLine");
        hLine.transform.SetParent(headerBar.transform, false);
        RectTransform hlRt = hLine.AddComponent<RectTransform>();
        hlRt.anchorMin = new Vector2(0f, 0f);
        hlRt.anchorMax = new Vector2(1f, 0f);
        hlRt.pivot = new Vector2(0.5f, 0f);
        hlRt.sizeDelta = new Vector2(0f, 1.5f);
        Image hlImg = hLine.AddComponent<Image>();
        hlImg.color = new Color(0.25f, 0.45f, 0.70f, 0.50f);

        CreateLabel(headerBar.transform, "HeaderTag", "● REGISTRO DO ORÁCULO // ANÁLISE DE ESPÉCIME BIOLÓGICO",
            new Vector2(20f, -15f), 16, TECH_CYAN, true, TextAlignmentOptions.Left, new Vector2(650f, 26f));

        GameObject btnCloseModal = CreateButton(headerBar.transform, "BtnCloseModal", "←  VOLTAR AO BESTIÁRIO",
            new Vector2(-16f, -7f), new Vector2(220f, 38f), new Color(0.65f, 0.18f, 0.22f, 0.95f), () => FecharInspecaoInimigo());
        RectTransform bCloseRt = btnCloseModal.GetComponent<RectTransform>();
        bCloseRt.anchorMin = new Vector2(1f, 1f);
        bCloseRt.anchorMax = new Vector2(1f, 1f);
        bCloseRt.pivot = new Vector2(1f, 1f);
        bCloseRt.anchoredPosition = new Vector2(-16f, -7f);
        Transform bCloseTr = btnCloseModal.transform.Find("Label");
        if (bCloseTr != null)
        {
            TextMeshProUGUI bTmp = bCloseTr.GetComponent<TextMeshProUGUI>();
            if (bTmp != null) bTmp.fontSize = 15;
        }

        // ── Lado Esquerdo: Moldura Holográfica de Análise do Espécime ──
        GameObject viewContainer = new GameObject("Holo_SpecimenContainer");
        viewContainer.transform.SetParent(enemyDetailModal.transform, false);
        RectTransform vcr = viewContainer.AddComponent<RectTransform>();
        vcr.anchorMin = new Vector2(0.025f, 0.04f);
        vcr.anchorMax = new Vector2(0.44f, 1f);
        vcr.offsetMin = new Vector2(0f, 10f);
        vcr.offsetMax = new Vector2(0f, -66f);

        Image viewBg = viewContainer.AddComponent<Image>();
        viewBg.color = new Color(0.015f, 0.020f, 0.035f, 1f);

        Outline viewOutline = viewContainer.AddComponent<Outline>();
        viewOutline.effectColor = new Color(0.25f, 0.65f, 0.95f, 0.5f);
        viewOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject iconFrameObj = new GameObject("SpecimenFrame");
        iconFrameObj.transform.SetParent(viewContainer.transform, false);
        RectTransform iconFrameRect = iconFrameObj.AddComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0.06f, 0.12f);
        iconFrameRect.anchorMax = new Vector2(0.94f, 0.92f);
        iconFrameRect.sizeDelta = Vector2.zero;
        Image ifImg = iconFrameObj.AddComponent<Image>();
        ifImg.color = new Color(0.02f, 0.035f, 0.06f, 0.95f);
        Outline ifOutline = iconFrameObj.AddComponent<Outline>();
        ifOutline.effectColor = new Color(0.35f, 0.75f, 0.95f, 0.35f);

        GameObject iconImgObj = new GameObject("SpecimenIcon");
        iconImgObj.transform.SetParent(iconFrameObj.transform, false);
        RectTransform iconImgRect = iconImgObj.AddComponent<RectTransform>();
        iconImgRect.anchorMin = new Vector2(0.15f, 0.15f);
        iconImgRect.anchorMax = new Vector2(0.85f, 0.85f);
        iconImgRect.sizeDelta = Vector2.zero;
        modalEnemyIconImg = iconImgObj.AddComponent<Image>();
        modalEnemyIconImg.preserveAspect = true;
        modalEnemyIconImg.color = new Color(1f, 1f, 1f, 0.15f);

        GameObject comingSoonBox = new GameObject("3D_ComingSoonBox");
        comingSoonBox.transform.SetParent(iconFrameObj.transform, false);
        RectTransform csRect = comingSoonBox.AddComponent<RectTransform>();
        csRect.anchorMin = new Vector2(0.06f, 0.12f);
        csRect.anchorMax = new Vector2(0.94f, 0.88f);
        csRect.sizeDelta = Vector2.zero;

        Image csBg = comingSoonBox.AddComponent<Image>();
        csBg.color = new Color(0.015f, 0.025f, 0.045f, 0.88f);
        Outline csOutline = comingSoonBox.AddComponent<Outline>();
        csOutline.effectColor = new Color(0.20f, 0.65f, 0.95f, 0.40f);
        csOutline.effectDistance = new Vector2(1.5f, -1.5f);

        CreateLabel(comingSoonBox.transform, "LabelEmBreve", "✦ EM BREVE ✦",
            new Vector2(0f, 52f), 30, MASTER_GOLD, true, TextAlignmentOptions.Center, new Vector2(340f, 38f));

        CreateLabel(comingSoonBox.transform, "LabelSub", "MODELO 3D INTERATIVO",
            new Vector2(0f, 16f), 17, TECH_CYAN, true, TextAlignmentOptions.Center, new Vector2(340f, 26f));

        CreateLabel(comingSoonBox.transform, "LabelDesc", "A representação tridimensional e rotação em 360° deste espécime será adicionada em uma futura transmissão do Oráculo.",
            new Vector2(0f, -42f), 15, TEXT_BODY, false, TextAlignmentOptions.Center, new Vector2(330f, 65f));

        CreateLabel(viewContainer.transform, "SpecimenHint", "● ANÁLISE DO ESPÉCIME BIOLÓGICO ●",
            new Vector2(0f, -220f), 15, TECH_CYAN, true, TextAlignmentOptions.Center, new Vector2(400f, 30f));

        // ── Lado Direito: Dossiê e Estatísticas ──
        GameObject dossier = new GameObject("DossierContainer");
        dossier.transform.SetParent(enemyDetailModal.transform, false);
        RectTransform dr = dossier.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.47f, 0.04f);
        dr.anchorMax = new Vector2(0.975f, 1f);
        dr.offsetMin = new Vector2(0f, 16f);
        dr.offsetMax = new Vector2(0f, -66f);

        VerticalLayoutGroup dVlg = dossier.AddComponent<VerticalLayoutGroup>();
        dVlg.padding = new RectOffset(6, 6, 8, 8);
        dVlg.spacing = 10f;
        dVlg.childControlWidth = true;
        dVlg.childControlHeight = false;
        dVlg.childForceExpandWidth = true;
        dVlg.childForceExpandHeight = false;

        // 1. Nome do Inimigo
        GameObject nameGO = new GameObject("ModalEnemyName");
        nameGO.transform.SetParent(dossier.transform, false);
        modalEnemyNameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        modalEnemyNameTMP.fontSize = 32;
        modalEnemyNameTMP.fontStyle = FontStyles.Bold;
        modalEnemyNameTMP.color = TEXT_TITLE;
        modalEnemyNameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        if (customFont != null) modalEnemyNameTMP.font = customFont;
        LayoutElement nameLe = nameGO.AddComponent<LayoutElement>();
        nameLe.preferredHeight = 38f;
        nameLe.flexibleHeight = 0f;

        // 2. Classe / Categoria
        GameObject classGO = new GameObject("ModalEnemyClass");
        classGO.transform.SetParent(dossier.transform, false);
        modalEnemyClassTMP = classGO.AddComponent<TextMeshProUGUI>();
        modalEnemyClassTMP.fontSize = 17;
        modalEnemyClassTMP.fontStyle = FontStyles.Bold;
        modalEnemyClassTMP.color = TECH_CYAN;
        modalEnemyClassTMP.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) modalEnemyClassTMP.font = customFont;
        LayoutElement classLe = classGO.AddComponent<LayoutElement>();
        classLe.preferredHeight = 24f;
        classLe.flexibleHeight = 0f;

        // 3. Linha divisória
        GameObject dLine = new GameObject("DossierSepLine");
        dLine.transform.SetParent(dossier.transform, false);
        Image dlImg = dLine.AddComponent<Image>();
        dlImg.color = new Color(0.20f, 0.40f, 0.60f, 0.45f);
        LayoutElement dlLe = dLine.AddComponent<LayoutElement>();
        dlLe.preferredHeight = 2f;
        dlLe.flexibleHeight = 0f;

        // 4. Estatísticas de Combate
        GameObject statsGO = new GameObject("ModalEnemyStats");
        statsGO.transform.SetParent(dossier.transform, false);
        modalEnemyStatsTMP = statsGO.AddComponent<TextMeshProUGUI>();
        modalEnemyStatsTMP.fontSize = 16;
        modalEnemyStatsTMP.color = SUCCESS_COLOR;
        modalEnemyStatsTMP.richText = true;
        modalEnemyStatsTMP.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) modalEnemyStatsTMP.font = customFont;
        LayoutElement statsLe = statsGO.AddComponent<LayoutElement>();
        statsLe.preferredHeight = 30f;
        statsLe.flexibleHeight = 0f;

        // 5. Cabeçalho Descrição
        GameObject descHeadGO = new GameObject("DescHeader");
        descHeadGO.transform.SetParent(dossier.transform, false);
        TextMeshProUGUI descHeadTMP = descHeadGO.AddComponent<TextMeshProUGUI>();
        descHeadTMP.text = "● DIRETRIZES TÁTICAS & COMPORTAMENTO:";
        descHeadTMP.fontSize = 13;
        descHeadTMP.fontStyle = FontStyles.Bold;
        descHeadTMP.color = TECH_CYAN;
        if (customFont != null) descHeadTMP.font = customFont;
        LayoutElement dhLe = descHeadGO.AddComponent<LayoutElement>();
        dhLe.preferredHeight = 20f;
        dhLe.flexibleHeight = 0f;

        // 6. Descrição (expande verticalmente de forma limpa)
        GameObject descGO = new GameObject("ModalEnemyDesc");
        descGO.transform.SetParent(dossier.transform, false);
        modalEnemyDescTMP = descGO.AddComponent<TextMeshProUGUI>();
        modalEnemyDescTMP.fontSize = 17;
        modalEnemyDescTMP.color = TEXT_BODY;
        modalEnemyDescTMP.textWrappingMode = TextWrappingModes.Normal;
        modalEnemyDescTMP.lineSpacing = 1.15f;
        if (customFont != null) modalEnemyDescTMP.font = customFont;
        LayoutElement descLe = descGO.AddComponent<LayoutElement>();
        descLe.minHeight = 80f;
        descLe.preferredHeight = 160f;
        descLe.flexibleHeight = 1f;

        enemyDetailModal.SetActive(false);
    }

    public void ExibirInspecaoInimigo(EnemyData data)
    {
        if (data == null) return;
        EnsureEnemyDetailModal();

        PlayBeep();

        Color classColor = ObterCorDaClasse(data.enemyClass);
        modalEnemyNameTMP.text = data.enemyName.ToUpper();
        string cName = !string.IsNullOrEmpty(data.enemyClass) ? data.enemyClass.ToUpper() : "ESPÉCIME ANÔMALO";
        modalEnemyClassTMP.text = $"CLASSE: <color=#{ColorUtility.ToHtmlStringRGB(classColor)}>{cName}</color>";

        string hpStr = data.vidaBase > 0 ? $"{data.vidaBase}" : "Não quantificado";
        string dmgStr = data.danoBase > 0 ? $"{data.danoBase:F0}" : "Não quantificado";
        modalEnemyStatsTMP.text = $"<b>VIDA:</b> <color=#FFE500>{hpStr}</color>   ✦   <b>DANO:</b> <color=#FF5555>{dmgStr}</color>   ✦   <b>BUDGET:</b> <color=#38BDF8>{data.custoBudget}</color>";

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

    // ─────────────────────────────────────────────────────────────────────────
    // Modal de Transcrição e Leitura de Diálogos da Lore
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureDialogTranscriptModal()
    {
        if (dialogTranscriptModal != null) return;

        dialogTranscriptModal = new GameObject("DialogTranscriptModal");
        dialogTranscriptModal.transform.SetParent(panelObj.transform, false);

        RectTransform r = dialogTranscriptModal.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.02f, 0.03f);
        r.anchorMax = new Vector2(0.98f, 0.97f);
        r.sizeDelta = Vector2.zero;

        Image bg = dialogTranscriptModal.AddComponent<Image>();
        bg.color = new Color(0.035f, 0.045f, 0.070f, 0.98f);

        Outline outline = dialogTranscriptModal.AddComponent<Outline>();
        outline.effectColor = TECH_CYAN;
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = dialogTranscriptModal.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(10f, -10f);

        AddCornerBrackets(dialogTranscriptModal, TECH_CYAN_DIM, 26f, 2.5f);

        // ── Top Header Bar (Dedicação para contexto, título e botão fechar) ──
        GameObject headerBar = new GameObject("ModalHeaderBar");
        headerBar.transform.SetParent(dialogTranscriptModal.transform, false);
        RectTransform headerRt = headerBar.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 62f);

        Image headerBg = headerBar.AddComponent<Image>();
        headerBg.color = new Color(0.055f, 0.075f, 0.115f, 0.95f);

        GameObject hLine = new GameObject("HeaderSepLine");
        hLine.transform.SetParent(headerBar.transform, false);
        RectTransform hlRt = hLine.AddComponent<RectTransform>();
        hlRt.anchorMin = new Vector2(0f, 0f);
        hlRt.anchorMax = new Vector2(1f, 0f);
        hlRt.pivot = new Vector2(0.5f, 0f);
        hlRt.sizeDelta = new Vector2(0f, 1.5f);
        Image hlImg = hLine.AddComponent<Image>();
        hlImg.color = new Color(0.25f, 0.45f, 0.70f, 0.50f);

        // Contexto (Tag no topo esquerdo da barra)
        GameObject contextGO = new GameObject("ModalContext");
        contextGO.transform.SetParent(headerBar.transform, false);
        dialogModalContextTMP = contextGO.AddComponent<TextMeshProUGUI>();
        dialogModalContextTMP.fontSize = 13;
        dialogModalContextTMP.fontStyle = FontStyles.Bold;
        dialogModalContextTMP.color = TECH_CYAN;
        dialogModalContextTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) dialogModalContextTMP.font = customFont;
        RectTransform ctxRt = contextGO.GetComponent<RectTransform>();
        ctxRt.anchorMin = new Vector2(0f, 0.5f);
        ctxRt.anchorMax = new Vector2(1f, 0.5f);
        ctxRt.pivot = new Vector2(0f, 0.5f);
        ctxRt.anchoredPosition = new Vector2(24f, 13f);
        ctxRt.sizeDelta = new Vector2(-260f, 20f);

        // Título (Linha de baixo da barra)
        GameObject titleGO = new GameObject("ModalTitle");
        titleGO.transform.SetParent(headerBar.transform, false);
        dialogModalTitleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        dialogModalTitleTMP.fontSize = 22;
        dialogModalTitleTMP.fontStyle = FontStyles.Bold;
        dialogModalTitleTMP.color = TEXT_TITLE;
        dialogModalTitleTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) dialogModalTitleTMP.font = customFont;
        RectTransform titRt = titleGO.GetComponent<RectTransform>();
        titRt.anchorMin = new Vector2(0f, 0.5f);
        titRt.anchorMax = new Vector2(1f, 0.5f);
        titRt.pivot = new Vector2(0f, 0.5f);
        titRt.anchoredPosition = new Vector2(24f, -13f);
        titRt.sizeDelta = new Vector2(-260f, 26f);

        // Botão Fechar no Top-Right
        GameObject btnClose = CreateButton(headerBar.transform, "BtnCloseModal", "←  VOLTAR À LISTA",
            new Vector2(-16f, -11f), new Vector2(190f, 40f), new Color(0.65f, 0.18f, 0.22f, 0.95f), () => FecharTranscricaoDialogo());
        RectTransform bCloseRt = btnClose.GetComponent<RectTransform>();
        bCloseRt.anchorMin = new Vector2(1f, 1f);
        bCloseRt.anchorMax = new Vector2(1f, 1f);
        bCloseRt.pivot = new Vector2(1f, 1f);
        bCloseRt.anchoredPosition = new Vector2(-16f, -11f);
        Transform bCloseTr = btnClose.transform.Find("Label");
        if (bCloseTr != null)
        {
            TextMeshProUGUI bTmp = bCloseTr.GetComponent<TextMeshProUGUI>();
            if (bTmp != null) bTmp.fontSize = 15;
        }

        // ── Scroll Area para Transcrição de Falas ──
        GameObject scrollRoot = CreateScrollArea(dialogTranscriptModal.transform, "TranscriptScroll",
            new Vector2(0f, -8f), new Vector2(1100f, 530f));
        RectTransform srRt = scrollRoot.GetComponent<RectTransform>();
        srRt.anchorMin = new Vector2(0f, 0f);
        srRt.anchorMax = new Vector2(1f, 1f);
        srRt.pivot = new Vector2(0.5f, 0.5f);
        srRt.offsetMin = new Vector2(24f, 65f);
        srRt.offsetMax = new Vector2(-24f, -68f);

        dialogModalScrollContent = scrollRoot.transform.Find("Viewport/Content");

        // ── Bottom Action Bar (Ações adicionais: Reouvir / Atalho ESC) ──
        GameObject bottomBar = new GameObject("ModalBottomBar");
        bottomBar.transform.SetParent(dialogTranscriptModal.transform, false);
        RectTransform botRt = bottomBar.AddComponent<RectTransform>();
        botRt.anchorMin = new Vector2(0f, 0f);
        botRt.anchorMax = new Vector2(1f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.anchoredPosition = Vector2.zero;
        botRt.sizeDelta = new Vector2(0f, 54f);

        Image botBg = bottomBar.AddComponent<Image>();
        botBg.color = new Color(0.04f, 0.055f, 0.085f, 0.90f);

        GameObject bLine = new GameObject("BottomSepLine");
        bLine.transform.SetParent(bottomBar.transform, false);
        RectTransform blRt = bLine.AddComponent<RectTransform>();
        blRt.anchorMin = new Vector2(0f, 1f);
        blRt.anchorMax = new Vector2(1f, 1f);
        blRt.pivot = new Vector2(0.5f, 1f);
        blRt.sizeDelta = new Vector2(0f, 1.5f);
        Image blImg = bLine.AddComponent<Image>();
        blImg.color = new Color(0.20f, 0.40f, 0.60f, 0.40f);

        // Atalho ESC
        GameObject hintGO = new GameObject("ModalHint");
        hintGO.transform.SetParent(bottomBar.transform, false);
        TextMeshProUGUI hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        hintTMP.text = "Pressione [ESC] ou clique em Voltar para retornar à lista de transmissões.";
        hintTMP.fontSize = 14;
        hintTMP.color = TEXT_DIM;
        hintTMP.alignment = TextAlignmentOptions.Left;
        if (customFont != null) hintTMP.font = customFont;
        RectTransform hintRt = hintGO.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0.5f);
        hintRt.anchorMax = new Vector2(0.6f, 0.5f);
        hintRt.pivot = new Vector2(0f, 0.5f);
        hintRt.anchoredPosition = new Vector2(24f, 0f);
        hintRt.sizeDelta = new Vector2(0f, 30f);

        // Botão Reouvir no HUD da Nave (Opcional)
        GameObject btnReplay = CreateButton(bottomBar.transform, "BtnModalReplay", "▷  REOUVIR NO HUD DA NAVE",
            new Vector2(-20f, 0f), new Vector2(240f, 38f), new Color(0.12f, 0.28f, 0.44f, 0.95f), () =>
            {
                string idToPlay = currentTranscriptDialogId;
                FecharMenu();
                if (!string.IsNullOrEmpty(idToPlay))
                {
                    EptinhoStoryDialogs.ReplayDialog(idToPlay);
                }
            });
        RectTransform brRt = btnReplay.GetComponent<RectTransform>();
        brRt.anchorMin = new Vector2(1f, 0.5f);
        brRt.anchorMax = new Vector2(1f, 0.5f);
        brRt.pivot = new Vector2(1f, 0.5f);
        brRt.anchoredPosition = new Vector2(-20f, 0f);
        Outline brOut = btnReplay.GetComponent<Outline>();
        if (brOut != null) brOut.effectColor = TECH_CYAN;
        Transform brLbl = btnReplay.transform.Find("Label");
        if (brLbl != null)
        {
            var lTmp = brLbl.GetComponent<TextMeshProUGUI>();
            if (lTmp != null) lTmp.fontSize = 14;
        }

        dialogTranscriptModal.SetActive(false);
    }

    public void ExibirTranscricaoDialogo(EptinhoStoryDialogs.StoryDialogRecord rec)
    {
        if (string.IsNullOrEmpty(rec.id)) return;
        EnsureDialogTranscriptModal();
        currentTranscriptDialogId = rec.id;

        dialogModalContextTMP.text = $"● CONTEXTO // {rec.eventContext.ToUpper()}";
        dialogModalTitleTMP.text = rec.title.ToUpper();

        if (dialogModalScrollContent != null)
        {
            foreach (Transform child in dialogModalScrollContent) Destroy(child.gameObject);

            if (rec.lines != null && rec.lines.Length > 0)
            {
                for (int i = 0; i < rec.lines.Length; i++)
                {
                    string rawLine = rec.lines[i];
                    string speaker = "EPTINHO";
                    string body = rawLine;

                    if (rawLine.StartsWith("Eptinho:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        body = rawLine.Substring("Eptinho:".Length).Trim();
                        if (body.StartsWith("\"") && body.EndsWith("\"") && body.Length >= 2)
                        {
                            body = body.Substring(1, body.Length - 2).Trim();
                        }
                    }

                    // Card do Balão de Fala
                    GameObject bubbleGO = new GameObject($"Bubble_{i}");
                    bubbleGO.transform.SetParent(dialogModalScrollContent, false);
                    Image bubbleBg = bubbleGO.AddComponent<Image>();
                    bubbleBg.color = new Color(0.055f, 0.08f, 0.125f, 0.95f);

                    Outline bubbleOut = bubbleGO.AddComponent<Outline>();
                    bubbleOut.effectColor = new Color(0.20f, 0.45f, 0.70f, 0.40f);
                    bubbleOut.effectDistance = new Vector2(1.5f, -1.5f);

                    // Faixa de destaque na lateral esquerda
                    GameObject barGO = new GameObject("SideBar");
                    barGO.transform.SetParent(bubbleGO.transform, false);
                    Image barImg = barGO.AddComponent<Image>();
                    barImg.color = TECH_CYAN;
                    RectTransform barRt = barGO.GetComponent<RectTransform>();
                    barRt.anchorMin = new Vector2(0f, 0f);
                    barRt.anchorMax = new Vector2(0f, 1f);
                    barRt.pivot = new Vector2(0f, 0.5f);
                    barRt.sizeDelta = new Vector2(4f, 0f);
                    barRt.anchoredPosition = Vector2.zero;

                    VerticalLayoutGroup bVlg = bubbleGO.AddComponent<VerticalLayoutGroup>();
                    bVlg.padding = new RectOffset(24, 24, 18, 18);
                    bVlg.spacing = 10f;
                    bVlg.childControlWidth = true;
                    bVlg.childControlHeight = true;
                    bVlg.childForceExpandWidth = true;
                    bVlg.childForceExpandHeight = false;

                    ContentSizeFitter bCsf = bubbleGO.AddComponent<ContentSizeFitter>();
                    bCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // Orador e Parte
                    GameObject spkGO = new GameObject("SpeakerTag");
                    spkGO.transform.SetParent(bubbleGO.transform, false);
                    TextMeshProUGUI spkTMP = spkGO.AddComponent<TextMeshProUGUI>();
                    string partTag = rec.lines.Length > 1 ? $"  ✦  PARTE {i + 1} DE {rec.lines.Length}" : "";
                    spkTMP.text = $"<color=#38BDF8><b>{speaker}</b></color> <color=#64748B>// ORÁCULO DE BORDO{partTag}</color>";
                    spkTMP.fontSize = 15;
                    spkTMP.alignment = TextAlignmentOptions.Left;
                    if (customFont != null) spkTMP.font = customFont;

                    // Linha do Texto Falado
                    GameObject txtGO = new GameObject("SpeechText");
                    txtGO.transform.SetParent(bubbleGO.transform, false);
                    TextMeshProUGUI txtTMP = txtGO.AddComponent<TextMeshProUGUI>();
                    txtTMP.text = body;
                    txtTMP.fontSize = 20;
                    txtTMP.color = new Color(0.92f, 0.96f, 1.0f, 1.0f);
                    txtTMP.lineSpacing = 1.25f;
                    txtTMP.textWrappingMode = TextWrappingModes.Normal;
                    txtTMP.alignment = TextAlignmentOptions.Left;
                    if (customFont != null) txtTMP.font = customFont;
                }
            }
        }

        dialogTranscriptModal.SetActive(true);
        PlayBeep();
    }

    public void FecharTranscricaoDialogo()
    {
        if (dialogTranscriptModal != null && dialogTranscriptModal.activeSelf)
        {
            dialogTranscriptModal.SetActive(false);
            PlayBeep();
        }
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


