using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Controlador principal da UI do Inventário.
/// Cria e gerencia o painel visual de grade (estilo Minecraft).
/// Inclui input handler (Tab para abrir/fechar) e gerenciamento de cursor.
/// 
/// PERSISTÊNCIA:
///   Singleton com DontDestroyOnLoad — cria seu próprio Canvas persistente
///   e reconecta ao PlayerInventory automaticamente ao trocar de cena.
/// 
/// EXTENSÃO: Para alterar aparência, modifique as constantes de configuração.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // === Seleção Múltipla para Infusão ===
    public System.Collections.Generic.List<InventorySlotUI> selectedSlots = new System.Collections.Generic.List<InventorySlotUI>();
    private GameObject mergeButtonObj;
    private UnityEngine.UI.Button mergeButton;
    private TextMeshProUGUI mergeButtonText;

    // Singleton
    public static InventoryUI Instance { get; private set; }
    [Header("Input")]
    [Tooltip("Tecla para abrir/fechar o inventário")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Layout")]
    [Tooltip("Número de colunas na grade")]
    public int columns = 5;
    [Tooltip("Tamanho de cada slot em pixels")]
    public float slotSize = 105f;
    [Tooltip("Espaçamento entre slots")]
    public float slotSpacing = 8f;
    [Tooltip("Número total de slots visíveis na tela (incluindo bloqueados).")]
    public int absoluteMaxSlots = 30;
    [Tooltip("Fonte customizada (Oswald Bold por padrão).")]
    public TMP_FontAsset customFont;

    [Tooltip("Exibe um modelo 3D rotativo do jogador no inventário.")]
    public bool showPlayerPreview = true;

    [Header("Ajustes de Posição da UI (Tempo Real)")]
    [Tooltip("Posição (X, Y, Z) do painel principal de slots.")]
    public Vector3 inventoryPanelPosition = new Vector3(-280f, 135f, 0f);
    [Tooltip("Escala do painel principal de slots.")]
    public float inventoryScale = 2.25f;
    [Tooltip("Posição (X, Y, Z) do painel de preview 3D do player.")]
    public Vector3 previewPanelPosition = new Vector3(600f, 120f, 0f);
    [Tooltip("Tamanho do painel de preview 3D do player.")]
    public Vector2 previewPanelSize = new Vector2(450f, 750f);
    [Tooltip("Escala do painel de preview 3D do player.")]
    public float previewScale = 1.0f;

    [Header("Prefab-Based UI (Optional)")]
    [Tooltip("Ative para usar a interface baseada em Prefabs desenhada na Unity. Se desativado, o inventário será gerado dinamicamente por script (Modo Provisório).")]
    public bool useCustomPrefabUI = false;
    [Tooltip("Painel principal da UI do Inventário no Prefab.")]
    public GameObject customPanel;
    [Tooltip("Transform que contém o Grid Layout Group do Prefab.")]
    public Transform customGridParent;
    [Tooltip("Prefab do Slot customizado.")]
    public GameObject customSlotPrefab;
    [Tooltip("Texto do cabeçalho customizado (TMP).")]
    public TextMeshProUGUI customHeaderText;
    [Tooltip("Tooltip customizado flutuante.")]
    public InventoryTooltip customTooltip;

    // Referências internas
    public static Canvas CanvasInstance;
    private PlayerInventory playerInventory;
    private Canvas inventoryCanvas;
    private GameObject canvasObject; // Canvas próprio (DontDestroyOnLoad)
    private GameObject panelObject;
    private RectTransform panelRect;
    private TextMeshProUGUI headerText;
    private InventoryTooltip tooltip;
    private List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private ScrollRect scrollRect;
    private RectTransform contentRect;
    private RectTransform scrollViewRect;
    private Scrollbar verticalScrollbar;
    private RawImage previewRawImage;
    private TextMeshProUGUI statsHpText;
    private TextMeshProUGUI statsArmorText;
    private TextMeshProUGUI statsOrbsText;
    private GameObject screenOverlayObject;
    private GameObject previewPanelObject;
    private GameObject statsPanelObject;

    private Vector3 lastInventoryPanelPosition;
    private Vector3 lastPreviewPanelPosition;
    private Vector2 lastPreviewPanelSize;
    private float lastInventoryScale = 1.0f;
    private float lastPreviewScale = 1.0f;

    // Estado
    private bool isOpen = false;
    private bool uiBuilt = false;

    // Cores do painel
    private static readonly Color PANEL_BG = new Color(0.04f, 0.04f, 0.07f, 0.82f); // frosted glass background
    private static readonly Color PANEL_BORDER = new Color(1f, 1f, 1f, 0.12f); // light reflection border
    private static readonly Color HEADER_COLOR = new Color(0.85f, 0.80f, 0.95f, 1f);
    private static readonly Color HEADER_ACCENT = new Color(0.6f, 0.45f, 0.90f, 1f);

    // Máximo de linhas visíveis sem scroll
    private const int MAX_VISIBLE_ROWS = 3;

    void Awake()
    {
        Debug.Log($"[INVENTORY UI Awake] Entrou. name: {gameObject.name} (ID: {gameObject.GetInstanceID()}). Instance atual: {(Instance != null ? Instance.gameObject.name + " (ID: " + Instance.gameObject.GetInstanceID() + ")" : "null")}");

        // Limpa referência estática "suja" caso o objeto da Unity tenha sido destruído
        if (Instance != null && Instance.gameObject == null)
        {
            Debug.Log("[INVENTORY UI Awake] Detectada referência estática a objeto destruído. Limpando Instance...");
            Instance = null;
        }

        // Singleton: se já existe um InventoryUI, destroi este duplicado
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[INVENTORY UI Awake] AUTO-DESTRUIÇÃO DETECTADA! Destruindo {gameObject.name} (ID: {gameObject.GetInstanceID()}) porque Instance já é {Instance.gameObject.name} (ID: {Instance.gameObject.GetInstanceID()})");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Tenta carregar Oswald Bold SDF como fallback se nenhuma fonte estiver definida no Inspector
        if (customFont == null)
        {
            customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
            if (customFont != null)
            {
                Debug.Log("[INVENTORY UI] Fonte Oswald Bold SDF carregada com sucesso do Resources.");
            }
        }

        // Correção de segurança: se os valores antigos do prefab/cena sobrescreverem os novos defaults no Inspector
        if (inventoryPanelPosition == new Vector3(200f, 0f, 0f) && inventoryScale == 1.0f)
        {
            inventoryPanelPosition = new Vector3(-280f, 135f, 0f);
            inventoryScale = 2.25f;
            previewPanelPosition = new Vector3(600f, 120f, 0f);
            previewPanelSize = new Vector2(450f, 750f);
            previewScale = 1.0f;
            Debug.Log("[INVENTORY UI] Valores padrões do inventário corrigidos no Awake.");
        }

        Debug.Log($"[INVENTORY UI Awake] {gameObject.name} (ID: {gameObject.GetInstanceID()}) registrado com sucesso como Instance.");
    }

    void Start()
    {
        // Conecta ao PlayerInventory
        ConnectToPlayerInventory();

        // Cria toda a UI (Canvas próprio, persistente)
        CreateInventoryUI();

        // Começa fechado (garante que todos os painéis e overlays estejam desativados)
        isOpen = false;
        if (panelObject != null) panelObject.SetActive(false);
        if (customPanel != null) customPanel.SetActive(false);
        if (screenOverlayObject != null) screenOverlayObject.SetActive(false);
        if (previewPanelObject != null) previewPanelObject.SetActive(false);
        if (statsPanelObject != null) statsPanelObject.SetActive(false);
        if (PlayerPreviewManager.Instance != null) PlayerPreviewManager.Instance.Deactivate();
        uiBuilt = true;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (playerInventory != null)
        {
            playerInventory.onInventoryChanged.RemoveListener(RefreshUI);
        }

        // Limpa Canvas persistente se este é o singleton sendo destruído
        if (Instance == this)
        {
            Instance = null;
            if (canvasObject != null) Destroy(canvasObject);
        }
    }

    /// <summary>
    /// Reconecta ao PlayerInventory (que é DontDestroyOnLoad) para manter o sistema funcionando.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!uiBuilt) return; // Start() ainda não rodou

        ConnectToPlayerInventory();

        // Garante que TODOS os elementos do inventário (incluindo painel de orbs/status e preview 3D) estejam fechados ao trocar de cena
        isOpen = false;
        if (panelObject != null) panelObject.SetActive(false);
        if (customPanel != null) customPanel.SetActive(false);
        if (screenOverlayObject != null) screenOverlayObject.SetActive(false);
        if (previewPanelObject != null) previewPanelObject.SetActive(false);
        if (statsPanelObject != null) statsPanelObject.SetActive(false);
        if (PlayerPreviewManager.Instance != null) PlayerPreviewManager.Instance.Deactivate();
        if (tooltip != null) tooltip.Hide();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[INVENTORY UI] Reconectado e UI resetada após carregar cena: " + scene.name);
    }

    /// <summary>
    /// Encontra e conecta ao PlayerInventory na cena.
    /// Remove listener antigo e adiciona novo para evitar duplicação.
    /// </summary>
    void ConnectToPlayerInventory()
    {
        // Remove listener antigo se existir
        if (playerInventory != null)
        {
            playerInventory.onInventoryChanged.RemoveListener(RefreshUI);
        }

        // Tenta se conectar à instância persistente oficial (veterana) se ela existir
        if (PlayerPersistence.instance != null)
        {
            playerInventory = PlayerPersistence.instance.GetComponent<PlayerInventory>();
        }
        else
        {
            // Fallback para quando inicia direto de uma cena de teste sem o Loader
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerInventory == null)
        {
            Debug.LogWarning("[INVENTORY UI] PlayerInventory não encontrado na cena!");
        }
        else
        {
            // Se inscreve para mudanças no inventário
            playerInventory.onInventoryChanged.AddListener(RefreshUI);
            Debug.Log("[INVENTORY UI] Conectado ao PlayerInventory oficial.");
        }
    }

    void Update()
    {
        // Toggle com a tecla configurada
        if (Input.GetKeyDown(toggleKey))
        {
            Debug.Log($"[INVENTORY UI] Tecla de atalho detectada ({toggleKey}). isOpen atual: {isOpen}.");
            if (isOpen)
                CloseInventory();
            else
                OpenInventory();
        }

        // ESC para fechar se estiver aberto
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseInventory();
        }

        // Aplica posições e tamanhos em tempo real caso a UI esteja aberta (para ajustes no Inspector)
        if (isOpen)
        {
            if (panelRect != null)
            {
                // Se mudou pelo Inspector do Script, aplica na UI
                if (inventoryPanelPosition != lastInventoryPanelPosition)
                {
                    panelRect.anchoredPosition3D = inventoryPanelPosition;
                    lastInventoryPanelPosition = inventoryPanelPosition;
                }
                // Se mudou arrastando na Scene ou alterando o RectTransform, sincroniza o Script
                else if (panelRect.anchoredPosition3D != lastInventoryPanelPosition)
                {
                    inventoryPanelPosition = panelRect.anchoredPosition3D;
                    lastInventoryPanelPosition = panelRect.anchoredPosition3D;
                }

                if (inventoryScale != lastInventoryScale)
                {
                    panelRect.localScale = new Vector3(inventoryScale, inventoryScale, 1f);
                    lastInventoryScale = inventoryScale;
                }
                else if (panelRect.localScale.x != lastInventoryScale)
                {
                    inventoryScale = panelRect.localScale.x;
                    lastInventoryScale = panelRect.localScale.x;
                }
            }
            if (previewPanelObject != null)
            {
                RectTransform previewRect = previewPanelObject.GetComponent<RectTransform>();
                if (previewRect != null)
                {
                    if (previewPanelPosition != lastPreviewPanelPosition)
                    {
                        previewRect.anchoredPosition3D = previewPanelPosition;
                        lastPreviewPanelPosition = previewPanelPosition;
                    }
                    else if (previewRect.anchoredPosition3D != lastPreviewPanelPosition)
                    {
                        previewPanelPosition = previewRect.anchoredPosition3D;
                        lastPreviewPanelPosition = previewRect.anchoredPosition3D;
                    }

                    if (previewPanelSize != lastPreviewPanelSize)
                    {
                        previewRect.sizeDelta = previewPanelSize;
                        lastPreviewPanelSize = previewPanelSize;
                        if (statsPanelObject != null)
                        {
                            RectTransform statsRectT = statsPanelObject.GetComponent<RectTransform>();
                            if (statsRectT != null)
                            {
                                statsRectT.sizeDelta = new Vector2(previewPanelSize.x, 80f);
                                statsRectT.anchoredPosition3D = new Vector3(previewRect.anchoredPosition3D.x, previewRect.anchoredPosition3D.y - (previewPanelSize.y / 2f) - 55f, 0f);
                            }
                        }
                    }
                    else if (previewRect.sizeDelta != lastPreviewPanelSize)
                    {
                        previewPanelSize = previewRect.sizeDelta;
                        lastPreviewPanelSize = previewRect.sizeDelta;
                        if (statsPanelObject != null)
                        {
                            RectTransform statsRectT = statsPanelObject.GetComponent<RectTransform>();
                            if (statsRectT != null)
                            {
                                statsRectT.sizeDelta = new Vector2(previewPanelSize.x, 80f);
                                statsRectT.anchoredPosition3D = new Vector3(previewRect.anchoredPosition3D.x, previewRect.anchoredPosition3D.y - (previewPanelSize.y / 2f) - 55f, 0f);
                            }
                        }
                    }

                    if (previewScale != lastPreviewScale)
                    {
                        previewRect.localScale = new Vector3(previewScale, previewScale, 1f);
                        lastPreviewScale = previewScale;
                        if (statsPanelObject != null)
                        {
                            RectTransform statsRectT = statsPanelObject.GetComponent<RectTransform>();
                            if (statsRectT != null) statsRectT.localScale = new Vector3(previewScale, previewScale, 1f);
                        }
                    }
                    else if (previewRect.localScale.x != lastPreviewScale)
                    {
                        previewScale = previewRect.localScale.x;
                        lastPreviewScale = previewRect.localScale.x;
                        if (statsPanelObject != null)
                        {
                            RectTransform statsRectT = statsPanelObject.GetComponent<RectTransform>();
                            if (statsRectT != null) statsRectT.localScale = new Vector3(previewScale, previewScale, 1f);
                        }
                    }
                }
            }

            // Atualiza as estatísticas em tempo real
            if (showPlayerPreview && statsPanelObject != null && statsPanelObject.activeSelf)
            {
                UpdatePlayerStatsText();
            }
        }
    }

    /// <summary>
    /// Abre o painel do inventário
    /// </summary>
    public void OpenInventory()
    {
        if (isOpen) return;

        // Reconexão de segurança: se o PlayerInventory sumiu, tenta encontrar de novo
        if (playerInventory == null)
        {
            ConnectToPlayerInventory();
        }

        lastInventoryPanelPosition = inventoryPanelPosition;
        lastPreviewPanelPosition = previewPanelPosition;
        lastPreviewPanelSize = previewPanelSize;
        lastInventoryScale = inventoryScale;
        lastPreviewScale = previewScale;

        // Garante que o Canvas persistente esteja ativo na cena atual
        if (inventoryCanvas != null && !inventoryCanvas.gameObject.activeSelf)
        {
            inventoryCanvas.gameObject.SetActive(true);
        }

        isOpen = true;
        if (panelObject != null) panelObject.SetActive(true);
        if (screenOverlayObject != null) screenOverlayObject.SetActive(true);
        if (showPlayerPreview && previewPanelObject != null) previewPanelObject.SetActive(true);
        if (showPlayerPreview && statsPanelObject != null) statsPanelObject.SetActive(true);

        // Ativa o preview 3D do player
        if (showPlayerPreview && PlayerPreviewManager.Instance != null)
        {
            PlayerPreviewManager.Instance.Activate();
        }

        // Mostra cursor para interagir com a UI
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshUI();

        // Sincroniza com a Bolsa Sintética
        if (SyntheticBagUI.Instance != null && !SyntheticBagUI.Instance.IsOpen())
        {
            SyntheticBagUI.Instance.OpenBag();
        }

        Debug.Log("[INVENTORY UI] Inventário aberto");
    }

    /// <summary>
    /// Fecha o painel do inventário
    /// </summary>
    public void CloseInventory()
    {
        ClearSelection();
        isOpen = false;
        if (panelObject != null) panelObject.SetActive(false);
        if (customPanel != null) customPanel.SetActive(false);
        if (screenOverlayObject != null) screenOverlayObject.SetActive(false);
        if (previewPanelObject != null) previewPanelObject.SetActive(false);
        if (statsPanelObject != null) statsPanelObject.SetActive(false);

        // Desativa o preview 3D do player
        if (showPlayerPreview && PlayerPreviewManager.Instance != null)
        {
            PlayerPreviewManager.Instance.Deactivate();
        }

        // Esconde cursor e trava para gameplay apenas se o Mercador não estiver aberto
        bool isMerchantOpen = MerchantUIController.HasInstance && MerchantUIController.Instance.IsUiOpen();
        if (!isMerchantOpen)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (tooltip != null)
            tooltip.Hide();

        // Segue o líder: Se o fechar sumiu o inventário, esconde a tela de Fusão junto.
        InfusionUI telaDeUpgrades = Object.FindFirstObjectByType<InfusionUI>(FindObjectsInactive.Include);
        if (telaDeUpgrades != null) 
        {
            telaDeUpgrades.ClosePanel();
        }

        // Sincroniza com a Bolsa Sintética
        if (SyntheticBagUI.Instance != null && SyntheticBagUI.Instance.IsOpen())
        {
            SyntheticBagUI.Instance.CloseBag();
        }

        Debug.Log("[INVENTORY UI] Inventário fechado");
    }

    /// <summary>
    /// Retorna se o inventário está aberto
    /// </summary>
    public bool IsOpen()
    {
        if (!gameObject.activeInHierarchy) return false;
        return isOpen;
    }

    /// <summary>
    /// Atualiza todos os slots visuais com os dados atuais do inventário
    /// </summary>
    public void RefreshUI()
    {
        ClearSelection();
        if (playerInventory == null)
        {
            ConnectToPlayerInventory();
            if (playerInventory == null) return;
        }

        int maxSlots = playerInventory.MaxSlots;

        // Atualiza header
        headerText.text = "INVENTÁRIO  <color=#" + ColorUtility.ToHtmlStringRGB(HEADER_ACCENT) + ">"
            + playerInventory.OccupiedSlots + "/" + maxSlots + "</color>";

        // Recria slots se a quantidade mudou de absoluteMaxSlots
        if (slots.Count != absoluteMaxSlots)
        {
            RebuildSlots(absoluteMaxSlots);
        }

        // Preenche os slots com dados do inventário
        Dictionary<string, int> items = playerInventory.GetAllItems();
        int slotIndex = 0;

        foreach (var kvp in items)
        {
            if (slotIndex >= maxSlots || slotIndex >= slots.Count) break;

            ItemData itemData = null;
            if (ItemDatabase.Instance != null)
            {
                itemData = ItemDatabase.Instance.GetItemData(kvp.Key);
            }

            slots[slotIndex].SetLocked(false);
            slots[slotIndex].SetItem(kvp.Key, kvp.Value, itemData);
            slotIndex++;
        }

        // Slots restantes que estão desbloqueados ficam vazios
        for (int i = slotIndex; i < maxSlots; i++)
        {
            if (i >= slots.Count) break;
            slots[i].SetLocked(false);
            slots[i].SetEmpty();
        }

        // Slots além de maxSlots ficam travados/bloqueados
        for (int i = maxSlots; i < slots.Count; i++)
        {
            slots[i].SetLocked(true);
        }
    }

    // ============================================================
    //  CRIAÇÃO DA UI
    // ============================================================

    void CreateInventoryUI()
    {
        /* 
         * =================================================================================
         * COMO ATIVAR A INTERFACE CUSTOMIZADA POR PREFAB DEPOIS:
         * 1. No Inspector do prefab 'InventorySystem', marque a caixa 'Use Custom Prefab UI' como TRUE.
         * 2. Se desejar, arraste manualmente as referências para:
         *    - Custom Panel (o GameObject do seu painel de inventário)
         *    - Custom Grid Parent (o GameObject com o Grid Layout Group onde os slots vão ficar)
         *    - Custom Slot Prefab (o prefab do seu slot individual, ex: InventorySlotPrefab.prefab)
         *    - Custom Header Text (o texto TMP para o título)
         *    - Custom Tooltip (o tooltip pré-desenhado)
         * 3. Caso não arraste nada, o código tentará encontrar os filhos chamados 'InventoryPanel', 
         *    'SlotGrid' e o prefab de slot em 'Resources/InventorySlotPrefab'.
         * =================================================================================
         */
        if (useCustomPrefabUI)
        {
            // Tenta auto-detectar referências caso estejam vazias para facilitar
            if (customPanel == null)
            {
                Transform panelT = transform.Find("InventoryPanel");
                if (panelT != null) customPanel = panelT.gameObject;
            }

            if (customPanel != null)
            {
                if (customGridParent == null) customGridParent = customPanel.transform.Find("SlotGrid");
                if (customHeaderText == null)
                {
                    Transform headerT = customPanel.transform.Find("Inventário");
                    if (headerT != null) customHeaderText = headerT.GetComponent<TextMeshProUGUI>();
                }

                if (customSlotPrefab == null)
                {
                    customSlotPrefab = Resources.Load<GameObject>("InventorySlotPrefab");
                }

                panelObject = customPanel;
                panelRect = panelObject.GetComponent<RectTransform>();
                headerText = customHeaderText;
                tooltip = customTooltip;

                if (tooltip != null)
                {
                    Canvas rootCanvas = panelObject.GetComponentInParent<Canvas>();
                    if (rootCanvas == null) rootCanvas = FindFirstObjectByType<Canvas>();
                    tooltip.Initialize(rootCanvas);
                }

                // O gridParent será o container do Prefab
                int slotsCount = absoluteMaxSlots;
                if (customGridParent != null)
                {
                    // Garante que o SlotGrid tenha o componente Grid Layout Group para alinhar os slots
                    GridLayoutGroup customGridGroup = customGridParent.GetComponent<GridLayoutGroup>();
                    if (customGridGroup == null)
                    {
                        customGridGroup = customGridParent.gameObject.AddComponent<GridLayoutGroup>();
                        customGridGroup.cellSize = new Vector2(slotSize, slotSize);
                        customGridGroup.spacing = new Vector2(slotSpacing, slotSpacing);
                        customGridGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
                        customGridGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
                        customGridGroup.childAlignment = TextAnchor.UpperLeft;
                        customGridGroup.constraint = GridLayoutGroup.Constraint.Flexible;
                        Debug.LogWarning("[INVENTORY UI] Grid Layout Group estava ausente em SlotGrid. Adicionado e configurado automaticamente.");
                    }

                    BuildSlots(customGridParent, slotsCount);
                }

                uiBuilt = true;
                return;
            }
        }

        // Cria Canvas próprio persistente (como EconomyHUD faz)
        canvasObject = new GameObject("InventoryUI_Canvas");
        inventoryCanvas = canvasObject.AddComponent<Canvas>();
        inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        inventoryCanvas.sortingOrder = 100;
        CanvasInstance = inventoryCanvas;
        
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObject);

        // Garante EventSystem
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        int uiLayer = gameObject.layer;
        int rows = Mathf.CeilToInt((float)absoluteMaxSlots / columns);

        // === Overlay Preto de Fundo ===
        screenOverlayObject = new GameObject("InventoryOverlay");
        screenOverlayObject.transform.SetParent(inventoryCanvas.transform, false);
        screenOverlayObject.layer = uiLayer;
        
        RectTransform overlayRect = screenOverlayObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;
        
        screenOverlayObject.AddComponent<CanvasRenderer>();
        Image overlayImg = screenOverlayObject.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f); // preto translúcido premium
        overlayImg.raycastTarget = true;
        screenOverlayObject.SetActive(false);

        // Calcula tamanho do painel
        float gridWidth = columns * (slotSize + slotSpacing) - slotSpacing;
        float gridHeight = rows * (slotSize + slotSpacing) - slotSpacing;
        float panelPadding = 20f;
        float headerHeight = 44f;
        float previewWidth = 0f; // Desacoplado do tamanho do painel do inventário
        float totalWidth = gridWidth + panelPadding * 2 + previewWidth;
        float totalHeight = gridHeight + panelPadding * 2 + headerHeight;

        // === Painel Principal ===
        panelObject = new GameObject("InventoryPanel");
        panelObject.transform.SetParent(inventoryCanvas.transform, false);
        panelObject.layer = uiLayer;

        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(totalWidth, totalHeight);
        panelRect.anchoredPosition3D = inventoryPanelPosition; // Desloca mais para a direita para dar espaço ao preview independente
        panelRect.localScale = new Vector3(inventoryScale, inventoryScale, 1f);

        // Background do painel
        panelObject.AddComponent<CanvasRenderer>();
        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = PANEL_BG;
        panelBg.raycastTarget = true;

        // Efeito de sombra (Drop Shadow) para dar profundidade de vidro suspenso
        Shadow panelShadow = panelObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        panelShadow.effectDistance = new Vector2(6f, -6f);

        // Acento reflexivo superior de vidro (Glass Highlight)
        GameObject glassHighlightObj = new GameObject("GlassHighlight");
        glassHighlightObj.transform.SetParent(panelObject.transform, false);
        glassHighlightObj.layer = uiLayer;

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

        // === Borda do Painel ===
        GameObject panelBorderObj = new GameObject("PanelBorder");
        panelBorderObj.transform.SetParent(panelObject.transform, false);
        panelBorderObj.layer = uiLayer;

        RectTransform panelBorderRect = panelBorderObj.AddComponent<RectTransform>();
        panelBorderRect.anchorMin = Vector2.zero;
        panelBorderRect.anchorMax = Vector2.one;
        panelBorderRect.sizeDelta = new Vector2(4f, 4f);
        panelBorderRect.anchoredPosition = Vector2.zero;

        panelBorderObj.AddComponent<CanvasRenderer>();
        Image panelBorderImg = panelBorderObj.AddComponent<Image>();
        panelBorderImg.color = PANEL_BORDER;
        panelBorderImg.raycastTarget = false;
        panelBorderImg.type = Image.Type.Sliced;
        panelBorderImg.fillCenter = false;
        panelBorderObj.transform.SetAsFirstSibling();

        // === Acento Superior (barra fina no topo) ===
        GameObject accentObj = new GameObject("TopAccent");
        accentObj.transform.SetParent(panelObject.transform, false);
        accentObj.layer = uiLayer;

        RectTransform accentRect = accentObj.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0.1f, 1f);
        accentRect.anchorMax = new Vector2(0.9f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = new Vector2(0f, 0f);
        accentRect.sizeDelta = new Vector2(0f, 3f);

        accentObj.AddComponent<CanvasRenderer>();
        Image accentImg = accentObj.AddComponent<Image>();
        accentImg.color = HEADER_ACCENT;
        accentImg.raycastTarget = false;

        // === Header ===
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(panelObject.transform, false);
        headerObj.layer = uiLayer;

        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -8f);
        headerRect.sizeDelta = new Vector2(-panelPadding * 2, headerHeight);

        headerObj.AddComponent<CanvasRenderer>();
        headerText = headerObj.AddComponent<TextMeshProUGUI>();
        if (customFont != null) headerText.font = customFont;
        headerText.fontSize = 18f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.color = HEADER_COLOR;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.raycastTarget = false;

        int occupiedSlots = playerInventory != null ? playerInventory.OccupiedSlots : 0;
        int currentMaxSlots = playerInventory != null ? playerInventory.MaxSlots : 10;
        headerText.text = "INVENTÁRIO  <color=#" + ColorUtility.ToHtmlStringRGB(HEADER_ACCENT) + ">"
            + occupiedSlots + "/" + currentMaxSlots + "</color>";

        // === ScrollView container ===
        float maxVisibleGridHeight = MAX_VISIBLE_ROWS * (slotSize + slotSpacing) - slotSpacing;
        float scrollViewHeight = Mathf.Min(gridHeight, maxVisibleGridHeight);

        // Recalcula tamanho do painel baseado na área visível
        totalHeight = scrollViewHeight + panelPadding * 2 + headerHeight;
        panelRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        // ScrollView
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(panelObject.transform, false);
        scrollViewObj.layer = uiLayer;

        scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollViewRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollViewRect.pivot = new Vector2(0.5f, 0.5f);
        scrollViewRect.sizeDelta = new Vector2(gridWidth, scrollViewHeight);
        
        // Desloca o ScrollView para a direita se houver o preview do jogador na esquerda
        float scrollXOffset = 0f; // Centralizado no painel
        scrollViewRect.anchoredPosition = new Vector2(scrollXOffset, -headerHeight / 2f);

        scrollViewObj.AddComponent<CanvasRenderer>();
        Image scrollViewMask = scrollViewObj.AddComponent<Image>();
        scrollViewMask.color = new Color(0, 0, 0, 0.01f); // quase invisível, necessário pro mask
        scrollViewMask.raycastTarget = true;
        scrollViewObj.AddComponent<Mask>().showMaskGraphic = false;

        // Cria o RawImage para renderizar o Player 3D se ativo (Painel Independente e Alto)
        if (showPlayerPreview)
        {
            previewPanelObject = new GameObject("PlayerPreviewPanel");
            previewPanelObject.transform.SetParent(inventoryCanvas.transform, false);
            previewPanelObject.layer = uiLayer;

            RectTransform previewRect = previewPanelObject.AddComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            // Posiciona à esquerda do painel do inventário
            previewRect.anchoredPosition3D = previewPanelPosition;
            previewRect.sizeDelta = previewPanelSize; // Muito maior e mais alto que o inventário
            previewRect.localScale = new Vector3(previewScale, previewScale, 1f);

            previewPanelObject.AddComponent<CanvasRenderer>();
            
            // Fundo escuro do preview
            Image previewBg = previewPanelObject.AddComponent<Image>();
            previewBg.color = PANEL_BG;
            previewBg.raycastTarget = true;

            // Sombra do preview
            Shadow previewShadow = previewPanelObject.AddComponent<Shadow>();
            previewShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            previewShadow.effectDistance = new Vector2(6f, -6f);

            // Glass Highlight superior do preview
            GameObject previewHighlightObj = new GameObject("PreviewGlassHighlight");
            previewHighlightObj.transform.SetParent(previewPanelObject.transform, false);
            previewHighlightObj.layer = uiLayer;

            RectTransform previewHighlightRect = previewHighlightObj.AddComponent<RectTransform>();
            previewHighlightRect.anchorMin = new Vector2(0f, 1f);
            previewHighlightRect.anchorMax = new Vector2(1f, 1f);
            previewHighlightRect.pivot = new Vector2(0.5f, 1f);
            previewHighlightRect.anchoredPosition = new Vector2(0f, -2f);
            previewHighlightRect.sizeDelta = new Vector2(-4f, 2f);

            previewHighlightObj.AddComponent<CanvasRenderer>();
            Image previewHighlightImg = previewHighlightObj.AddComponent<Image>();
            previewHighlightImg.color = new Color(1f, 1f, 1f, 0.12f);
            previewHighlightImg.raycastTarget = false;

            // Borda do painel de preview
            GameObject previewBorderObj = new GameObject("PreviewBorder");
            previewBorderObj.transform.SetParent(previewPanelObject.transform, false);
            previewBorderObj.layer = uiLayer;

            RectTransform borderRect = previewBorderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = new Vector2(4f, 4f);
            borderRect.anchoredPosition = Vector2.zero;

            previewBorderObj.AddComponent<CanvasRenderer>();
            Image borderImg = previewBorderObj.AddComponent<Image>();
            borderImg.color = PANEL_BORDER;
            borderImg.type = Image.Type.Sliced;
            borderImg.fillCenter = false;

            // RawImage interno para exibir a câmera
            GameObject rawImageObj = new GameObject("PreviewRawImage");
            rawImageObj.transform.SetParent(previewPanelObject.transform, false);
            rawImageObj.layer = uiLayer;

            RectTransform rawRect = rawImageObj.AddComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.sizeDelta = new Vector2(-20f, -20f); // margem interna de 10px
            rawRect.anchoredPosition = Vector2.zero;

            rawImageObj.AddComponent<CanvasRenderer>();
            previewRawImage = rawImageObj.AddComponent<RawImage>();
            previewRawImage.color = Color.white;

            previewPanelObject.SetActive(false); // Inicia fechado

            // Inicializa o PlayerPreviewManager
            PlayerPreviewManager previewManager = FindFirstObjectByType<PlayerPreviewManager>();
            if (previewManager == null)
            {
                GameObject pmObj = new GameObject("PlayerPreviewManager");
                previewManager = pmObj.AddComponent<PlayerPreviewManager>();
            }
            previewManager.SetupPreview(previewRawImage);

            // === Stats Panel (Independent Glassmorphic Panel) ===
            statsPanelObject = new GameObject("PlayerStatsPanel");
            statsPanelObject.transform.SetParent(inventoryCanvas.transform, false);
            statsPanelObject.layer = uiLayer;

            RectTransform statsRect = statsPanelObject.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.5f, 0.5f);
            statsRect.anchorMax = new Vector2(0.5f, 0.5f);
            statsRect.pivot = new Vector2(0.5f, 0.5f);
            // Alinhado horizontalmente com o previewPanelPosition, e posicionado logo abaixo dele
            statsRect.anchoredPosition3D = new Vector3(previewPanelPosition.x, previewPanelPosition.y - (previewPanelSize.y / 2f) - 55f, 0f);
            statsRect.sizeDelta = new Vector2(previewPanelSize.x, 80f); // Mesma largura do preview, altura de 80px
            statsRect.localScale = new Vector3(previewScale, previewScale, 1f);

            statsPanelObject.AddComponent<CanvasRenderer>();
            Image statsBg = statsPanelObject.AddComponent<Image>();
            statsBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f); // ligeiramente mais claro que o PANEL_BG
            statsBg.raycastTarget = true;

            // Glass Highlight superior do painel de stats
            GameObject statsHighlightObj = new GameObject("StatsGlassHighlight");
            statsHighlightObj.transform.SetParent(statsPanelObject.transform, false);
            statsHighlightObj.layer = uiLayer;

            RectTransform statsHighlightRect = statsHighlightObj.AddComponent<RectTransform>();
            statsHighlightRect.anchorMin = new Vector2(0f, 1f);
            statsHighlightRect.anchorMax = new Vector2(1f, 1f);
            statsHighlightRect.pivot = new Vector2(0.5f, 1f);
            statsHighlightRect.anchoredPosition = new Vector2(0f, -2f);
            statsHighlightRect.sizeDelta = new Vector2(-4f, 2f);

            statsHighlightObj.AddComponent<CanvasRenderer>();
            Image statsHighlightImg = statsHighlightObj.AddComponent<Image>();
            statsHighlightImg.color = new Color(1f, 1f, 1f, 0.12f);
            statsHighlightImg.raycastTarget = false;

            // Borda do painel de stats
            GameObject statsBorderObj = new GameObject("StatsBorder");
            statsBorderObj.transform.SetParent(statsPanelObject.transform, false);
            statsBorderObj.layer = uiLayer;

            RectTransform statsBorderRect = statsBorderObj.AddComponent<RectTransform>();
            statsBorderRect.anchorMin = Vector2.zero;
            statsBorderRect.anchorMax = Vector2.one;
            statsBorderRect.sizeDelta = new Vector2(4f, 4f);
            statsBorderRect.anchoredPosition = Vector2.zero;

            statsBorderObj.AddComponent<CanvasRenderer>();
            Image statsBorderImg = statsBorderObj.AddComponent<Image>();
            statsBorderImg.color = PANEL_BORDER;
            statsBorderImg.type = Image.Type.Sliced;
            statsBorderImg.fillCenter = false;

            // Layout horizontal para alinhar HP, Armadura e Orbs lado a lado
            HorizontalLayoutGroup layoutGroup = statsPanelObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.spacing = 15f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;

            // 1. HP Component
            GameObject hpObj = new GameObject("Stat_HP");
            hpObj.transform.SetParent(statsPanelObject.transform, false);
            hpObj.layer = uiLayer;
            hpObj.AddComponent<CanvasRenderer>();
            statsHpText = hpObj.AddComponent<TextMeshProUGUI>();
            statsHpText.fontSize = 14f;
            statsHpText.fontStyle = FontStyles.Bold;
            statsHpText.alignment = TextAlignmentOptions.Center;
            statsHpText.color = Color.white;
            statsHpText.lineSpacing = -10f;
            if (customFont != null) statsHpText.font = customFont;

            // 2. Armor Component
            GameObject armorObj = new GameObject("Stat_Armor");
            armorObj.transform.SetParent(statsPanelObject.transform, false);
            armorObj.layer = uiLayer;
            armorObj.AddComponent<CanvasRenderer>();
            statsArmorText = armorObj.AddComponent<TextMeshProUGUI>();
            statsArmorText.fontSize = 14f;
            statsArmorText.fontStyle = FontStyles.Bold;
            statsArmorText.alignment = TextAlignmentOptions.Center;
            statsArmorText.color = Color.white;
            statsArmorText.lineSpacing = -10f;
            if (customFont != null) statsArmorText.font = customFont;

            // 3. Orbs Component
            GameObject orbsObj = new GameObject("Stat_Orbs");
            orbsObj.transform.SetParent(statsPanelObject.transform, false);
            orbsObj.layer = uiLayer;
            orbsObj.AddComponent<CanvasRenderer>();
            statsOrbsText = orbsObj.AddComponent<TextMeshProUGUI>();
            statsOrbsText.fontSize = 14f;
            statsOrbsText.fontStyle = FontStyles.Bold;
            statsOrbsText.alignment = TextAlignmentOptions.Center;
            statsOrbsText.color = Color.white;
            statsOrbsText.lineSpacing = -10f;
            if (customFont != null) statsOrbsText.font = customFont;

            statsPanelObject.SetActive(false); // Inicia fechado
        }

        // Content (o grid real que rola)
        GameObject gridObj = new GameObject("SlotGrid");
        gridObj.transform.SetParent(scrollViewObj.transform, false);
        gridObj.layer = uiLayer;

        contentRect = gridObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        contentRect.anchoredPosition = Vector2.zero;

        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(slotSize, slotSize);
        gridLayout.spacing = new Vector2(slotSpacing, slotSpacing);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;

        ContentSizeFitter fitter = gridObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect
        scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.viewport = scrollViewRect;

        // === Scrollbar Vertical ===
        bool needsScroll = rows > MAX_VISIBLE_ROWS;
        CreateScrollbar(panelObject.transform, uiLayer, scrollViewHeight, headerHeight);
        scrollRect.verticalScrollbar = verticalScrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        if (verticalScrollbar != null)
        {
            verticalScrollbar.gameObject.SetActive(needsScroll);
        }

        // === Tooltip — criado ANTES dos slots para que slot.Initialize() receba a referência ===
        GameObject tooltipObj = new GameObject("InventoryTooltip");
        tooltipObj.transform.SetParent(inventoryCanvas.transform, false);
        tooltipObj.layer = uiLayer;
        tooltip = tooltipObj.AddComponent<InventoryTooltip>();
        tooltip.Initialize(inventoryCanvas);
        if (customFont != null) tooltip.SetFont(customFont);

        // === Cria os Slots ===
        BuildSlots(gridObj.transform, absoluteMaxSlots);

        // Reposiciona o tooltip como último filho do Canvas DEPOIS dos slots,
        // garantindo que está no topo da hierarquia de renderização
        tooltipObj.transform.SetAsLastSibling();

        // === Botão de Fechar (X) ===
        CreateCloseButton(panelObject.transform, uiLayer);

        // === Botão de Fusão (Oculto inicialmente) ===
        CreateMergeButton(panelObject.transform, uiLayer);

        lastInventoryPanelPosition = inventoryPanelPosition;
        lastPreviewPanelPosition = previewPanelPosition;
        lastPreviewPanelSize = previewPanelSize;
        lastInventoryScale = inventoryScale;
        lastPreviewScale = previewScale;

        Debug.Log("[INVENTORY UI] UI criada com " + absoluteMaxSlots + " slots (" + columns + "x" + rows + ")");
    }

    void BuildSlots(Transform gridParent, int count)
    {
        slots.Clear();

        for (int i = 0; i < count; i++)
        {
            GameObject slotObj;
            if (customSlotPrefab != null)
            {
                slotObj = Instantiate(customSlotPrefab, gridParent);
            }
            else
            {
                slotObj = new GameObject("Slot_" + i);
                slotObj.transform.SetParent(gridParent, false);
                slotObj.layer = gameObject.layer;
            }

            InventorySlotUI slot = slotObj.GetComponent<InventorySlotUI>();
            if (slot == null) slot = slotObj.AddComponent<InventorySlotUI>();
            slot.Initialize(tooltip, slotSize);
            if (customFont != null) slot.SetFont(customFont);

            slots.Add(slot);
        }
    }

    void RebuildSlots(int newCount)
    {
        // Destrói slots antigos
        foreach (var slot in slots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        slots.Clear();

        if (customPanel != null)
        {
            if (customGridParent != null)
            {
                BuildSlots(customGridParent, newCount);
            }
            return;
        }

        // Encontra o grid container (agora dentro do ScrollView)
        Transform scrollViewTransform = panelObject.transform.Find("ScrollView");
        if (scrollViewTransform == null)
        {
            Debug.LogError("[INVENTORY UI] ScrollView não encontrado para rebuild!");
            return;
        }

        Transform gridTransform = scrollViewTransform.Find("SlotGrid");
        if (gridTransform == null)
        {
            Debug.LogError("[INVENTORY UI] SlotGrid não encontrado para rebuild!");
            return;
        }

        // Recalcula tamanhos
        int rows = Mathf.CeilToInt((float)newCount / columns);
        float gridWidth = columns * (slotSize + slotSpacing) - slotSpacing;
        float gridHeight = rows * (slotSize + slotSpacing) - slotSpacing;
        float panelPadding = 20f;
        float headerHeight = 44f;
        float maxVisibleGridHeight = MAX_VISIBLE_ROWS * (slotSize + slotSpacing) - slotSpacing;
        float scrollViewHeight = Mathf.Min(gridHeight, maxVisibleGridHeight);

        // Atualiza tamanho do painel (mantém tamanho máximo fixo)
        float totalWidth = gridWidth + panelPadding * 2;
        float totalHeight = scrollViewHeight + panelPadding * 2 + headerHeight;
        panelRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        // Atualiza tamanho do scroll view
        if (scrollViewRect != null)
        {
            scrollViewRect.sizeDelta = new Vector2(gridWidth, scrollViewHeight);
        }

        // Atualiza tamanho do content (grid real)
        if (contentRect != null)
        {
            contentRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        }

        // Mostra/esconde scrollbar
        bool needsScroll = rows > MAX_VISIBLE_ROWS;
        if (verticalScrollbar != null)
        {
            verticalScrollbar.gameObject.SetActive(needsScroll);
        }

        // Cria novos slots
        BuildSlots(gridTransform, newCount);

        Debug.Log("[INVENTORY UI] Slots reconstruídos: " + newCount + " (" + columns + "x" + rows + ")");
    }

    void CreateScrollbar(Transform parent, int layer, float scrollViewHeight, float headerHeight)
    {
        float scrollbarWidth = 6f;
        float panelPadding = 20f;

        // Container da scrollbar
        GameObject scrollbarObj = new GameObject("VerticalScrollbar");
        scrollbarObj.transform.SetParent(parent, false);
        scrollbarObj.layer = layer;

        RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0.5f);
        scrollbarRect.anchorMax = new Vector2(1f, 0.5f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(scrollbarWidth, scrollViewHeight);
        scrollbarRect.anchoredPosition = new Vector2(-panelPadding / 2f, -headerHeight / 2f);

        scrollbarObj.AddComponent<CanvasRenderer>();
        Image scrollbarBg = scrollbarObj.AddComponent<Image>();
        scrollbarBg.color = new Color(0.15f, 0.12f, 0.22f, 0.5f);
        scrollbarBg.raycastTarget = true;

        // Sliding Area
        GameObject slidingArea = new GameObject("SlidingArea");
        slidingArea.transform.SetParent(scrollbarObj.transform, false);
        slidingArea.layer = layer;

        RectTransform slidingRect = slidingArea.AddComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.sizeDelta = Vector2.zero;
        slidingRect.anchoredPosition = Vector2.zero;

        // Handle (a barrinha que se move)
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(slidingArea.transform, false);
        handle.layer = layer;

        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = new Vector2(1f, 0.3f);
        handleRect.sizeDelta = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;

        handle.AddComponent<CanvasRenderer>();
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.6f, 0.45f, 0.90f, 0.6f);
        handleImg.raycastTarget = true;

        // Componente Scrollbar
        verticalScrollbar = scrollbarObj.AddComponent<Scrollbar>();
        verticalScrollbar.handleRect = handleRect;
        verticalScrollbar.targetGraphic = handleImg;
        verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Cores do handle no hover
        ColorBlock colors = verticalScrollbar.colors;
        colors.normalColor = new Color(0.6f, 0.45f, 0.90f, 0.6f);
        colors.highlightedColor = new Color(0.7f, 0.55f, 1f, 0.8f);
        colors.pressedColor = new Color(0.5f, 0.35f, 0.80f, 0.9f);
        verticalScrollbar.colors = colors;
    }

    void CreateCloseButton(Transform parent, int layer)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);
        btnObj.layer = layer;

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = new Vector2(-8f, -8f);
        btnRect.sizeDelta = new Vector2(28f, 28f);

        btnObj.AddComponent<CanvasRenderer>();
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.8f, 0.2f, 0.2f, 0.7f);
        btnBg.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(CloseInventory);

        // Cores do botão no hover
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);
        colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        btn.colors = colors;

        // Texto "X"
        GameObject xObj = new GameObject("X");
        xObj.transform.SetParent(btnObj.transform, false);
        xObj.layer = layer;

        RectTransform xRect = xObj.AddComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;
        xRect.anchoredPosition = Vector2.zero;

        xObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI xText = xObj.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 16f;
        xText.color = Color.white;
        xText.alignment = TextAlignmentOptions.Center;
        xText.raycastTarget = false;
    }

    private void UpdatePlayerStatsText()
    {
        // Encontra o player na cena para ler os dados reais
        PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
        PlayerEssence essence = Object.FindFirstObjectByType<PlayerEssence>();

        // Cores em hex para rich text do TMP
        string redHex   = "#FF5A5A"; // HP
        string blueHex  = "#5AABFF"; // Armor
        string goldHex  = "#FFD154"; // Orbs
        string dimWhite = "#AAAAAA"; // label dim

        if (statsHpText != null)
        {
            string val = health != null ? $"{health.currentHealth}/{health.maxHealth}" : "100/100";
            statsHpText.text = $"<size=10><color={dimWhite}>VIDA</color></size>\n<color={redHex}>{val}</color>";
        }

        if (statsArmorText != null)
        {
            string val = health != null ? $"{health.CurrentArmor}/{health.maxArmor}" : "0/200";
            statsArmorText.text = $"<size=10><color={dimWhite}>ARMADURA</color></size>\n<color={blueHex}>{val}</color>";
        }

        if (statsOrbsText != null)
        {
            string val = essence != null ? $"{essence.currentEssence}" : "0";
            statsOrbsText.text = $"<size=10><color={dimWhite}>ORBS</color></size>\n<color={goldHex}>{val}</color>";
        }
    }

    // ==========================================
    // SISTEMA DE SELEÇÃO E FUSÃO (SINERGIA)
    // ==========================================

    public void ToggleItemSelection(InventorySlotUI slot, string itemId)
    {
        if (selectedSlots.Contains(slot))
        {
            // Deseleciona
            slot.SetSelected(false);
            selectedSlots.Remove(slot);
        }
        else
        {
            // Seleciona (Limita a 2 itens)
            if (selectedSlots.Count >= 4)
            {
                // Remove o primeiro para dar espaço ao novo
                InventorySlotUI oldest = selectedSlots[0];
                oldest.SetSelected(false);
                selectedSlots.RemoveAt(0);
            }
            slot.SetSelected(true);
            selectedSlots.Add(slot);
        }

        UpdateMergeButtonState();
    }

    public void ClearSelection()
    {
        foreach (var slot in selectedSlots)
        {
            if (slot != null) slot.SetSelected(false);
        }
        selectedSlots.Clear();
        UpdateMergeButtonState();
    }

    private void UpdateMergeButtonState()
    {
        if (mergeButtonObj == null) return;

        if (selectedSlots.Count > 0 && selectedSlots.Count <= 4)
        {
            mergeButtonObj.SetActive(true);
        }
        else
        {
            mergeButtonObj.SetActive(false);
        }
    }

    private void CreateMergeButton(Transform parent, int layer)
    {
        mergeButtonObj = new GameObject("MergeButton");
        mergeButtonObj.transform.SetParent(parent, false);
        mergeButtonObj.layer = layer;

        RectTransform btnRect = mergeButtonObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, -0.05f); 
        btnRect.anchorMax = new Vector2(0.5f, -0.05f);
        btnRect.pivot = new Vector2(0.5f, 1f);
        btnRect.anchoredPosition = new Vector2(-150f, 5f);
        btnRect.sizeDelta = new Vector2(120f, 30f); 

        mergeButtonObj.AddComponent<CanvasRenderer>();
        UnityEngine.UI.Image btnBg = mergeButtonObj.AddComponent<UnityEngine.UI.Image>();
        btnBg.color = Color.white;
        btnBg.raycastTarget = true;

        mergeButton = mergeButtonObj.AddComponent<UnityEngine.UI.Button>();
        mergeButton.onClick.AddListener(OnMergeButtonClicked);
        
        UnityEngine.UI.ColorBlock cb = mergeButton.colors;
        cb.normalColor = new Color32(87, 72, 142, 255);
        cb.highlightedColor = new Color32(139, 168, 222, 255);
        cb.pressedColor = new Color32(31, 115, 233, 255);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color32(200, 200, 200, 128);
        cb.colorMultiplier = 1.5f;
        cb.fadeDuration = 0.1f;
        mergeButton.colors = cb;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(mergeButtonObj.transform, false);
        textObj.layer = layer;

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TMPro.TextMeshProUGUI tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmpText.text = "INFUSÃO";
        tmpText.fontSize = 12;
        tmpText.color = Color.white;
        tmpText.alignment = TMPro.TextAlignmentOptions.Center;
        // if (titleFont != null) tmpText.font = titleFont; // I'll leave the font logic since they might have Oswald Bold globally assigned.

        mergeButtonObj.SetActive(false);
    }
    private void OnMergeButtonClicked()
    {
        if (selectedSlots.Count == 0 || selectedSlots.Count > 4) return;

        System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>();
        foreach(var s in selectedSlots) ids.Add(s.currentItemId);

        InfusionUI telaDeUpgrades = Object.FindFirstObjectByType<InfusionUI>(FindObjectsInactive.Include);
        if (telaDeUpgrades != null)
        {
            bool abriu = telaDeUpgrades.OpenPanel();
            if (abriu)
            {
                telaDeUpgrades.SelectMultipleItems(ids);
            }
        }
    }
}
