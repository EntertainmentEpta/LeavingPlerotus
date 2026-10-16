using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Widget de HUD que exibe a diretriz/missão em tempo real na tela de jogo.
/// Posicionado no meio do lado direito da tela.
/// Ao concluir uma missão, ela não é apagada: é riscada com efeito visual permanente.
/// </summary>
public class QuestHUDTracker : MonoBehaviour
{
    private static QuestHUDTracker _instance;
    public static QuestHUDTracker Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<QuestHUDTracker>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("QuestHUDTracker_Auto");
                    _instance = go.AddComponent<QuestHUDTracker>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("📐 Posição e Dimensões (Meio do Lado Direito)")]
    [Tooltip("Distância da borda direita da tela (valores negativos ficam para dentro da tela).")]
    public float offsetX = -35f;

    [Tooltip("Deslocamento vertical relativo ao centro da tela (0 = centro perfeito).")]
    public float offsetY = 40f;

    [Tooltip("Largura do painel do quest log.")]
    public float widgetWidth = 430f;

    [Tooltip("Largura compacta do widget durante a gameplay.")]
    public float compactWidth = 260f;

    // ── Cores do Design System ──
    private static readonly Color BG_COLOR        = new Color(0.04f, 0.03f, 0.08f, 0.92f);
    private static readonly Color BORDER_ACTIVE   = new Color(0.96f, 0.76f, 0.20f, 0.95f); // Dourado
    private static readonly Color BORDER_DONE     = new Color(0.28f, 0.86f, 0.48f, 0.95f); // Verde
    private static readonly Color TEXT_TITLE      = new Color(0.96f, 0.94f, 1.00f, 1.00f);
    private static readonly Color TEXT_SUB        = new Color(0.76f, 0.70f, 0.90f, 1.00f);
    private static readonly Color TEXT_DONE_TITLE = new Color(0.60f, 0.58f, 0.70f, 0.90f);
    private static readonly Color TEXT_DONE_SUB   = new Color(0.48f, 0.46f, 0.58f, 0.85f);
    private static readonly Color DIVIDER_COLOR   = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color BORDER_MASTER   = new Color(1.00f, 0.72f, 0.16f, 1.00f); // Dourado solar supremo

    private GameObject      canvasObj;
    private Canvas          hudCanvas;
    private GameObject      widgetObj;
    private RectTransform   widgetRect;
    private Image           borderAccent;
    private CanvasGroup     canvasGroup;
    private TMP_FontAsset   customFont;

    // ── Seção Compacta (Gameplay / Resumido) ──
    private GameObject      compactContainer;
    private TextMeshProUGUI compactMasterText;
    private TextMeshProUGUI compactActiveText;
    private TextMeshProUGUI compactHintText;

    // ── Seção Expandida (Inventário / Detalhado) ──
    private GameObject      expandedContainer;
    private bool            isCurrentlyExpanded = false;

    // ── Seção da Meta Suprema (Núcleos da Nave) ──
    private GameObject      masterSection;
    private TextMeshProUGUI masterBadgeText;
    private TextMeshProUGUI masterTitleText;
    private TextMeshProUGUI masterProgressText;
    private GameObject      masterDividerLine;

    // ── Seção de Missão Concluída (Riscada) ──
    private GameObject      completedSection;
    private TextMeshProUGUI completedBadgeText;
    private TextMeshProUGUI completedTitleText;
    private TextMeshProUGUI completedProgressText;

    // ── Linha Divisória ──
    private GameObject      dividerLine;

    // ── Seção de Missão Ativa ──
    private GameObject      activeSection;
    private TextMeshProUGUI activeBadgeText;
    private TextMeshProUGUI activeTitleText;
    private TextMeshProUGUI activeProgressText;

    private Coroutine       pulseRoutine;
    private string          lastFinishedQuestId = "";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        BuildUI();
    }

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StateChanged += RefreshDisplay;
            QuestManager.Instance.QuestCompletedEvent += HandleQuestCompleted;
        }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StateChanged -= RefreshDisplay;
            QuestManager.Instance.QuestCompletedEvent -= HandleQuestCompleted;
        }
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        RefreshDisplay();
    }

    private void Update()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool inMainMenu = currentScene == "MainMenu" || currentScene == "Menu";
        bool inDialog = DialogManager.IsOpen;
        bool inMenu = EptinhoMenuController.instancia != null && EptinhoMenuController.instancia.IsOpen();
        bool isWakingUp = PlayerWakeUpEffect.IsWakingUp;

        // O widget de missões só surge na tela após o diálogo inicial do Eptinho ter sido concluído
        bool introCompleted = EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_INTRO_EPTINHO);

        if (canvasGroup != null)
        {
            float targetAlpha = (inMainMenu || inDialog || inMenu || isWakingUp || !introCompleted) ? 0f : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 6f);
        }

        UpdateDisplayMode();
    }

    private bool IsInventoryOpen()
    {
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen()) return true;
        if (SyntheticBagUI.Instance != null && SyntheticBagUI.Instance.IsOpen()) return true;
        return false;
    }

    private void UpdateDisplayMode()
    {
        bool shouldExpand = IsInventoryOpen();

        if (isCurrentlyExpanded != shouldExpand)
        {
            isCurrentlyExpanded = shouldExpand;
            if (compactContainer != null) compactContainer.SetActive(!isCurrentlyExpanded);
            if (expandedContainer != null) expandedContainer.SetActive(isCurrentlyExpanded);
        }

        if (widgetRect != null)
        {
            float targetW = isCurrentlyExpanded ? widgetWidth : compactWidth;
            float currentW = widgetRect.sizeDelta.x;
            if (Mathf.Abs(currentW - targetW) > 0.5f)
            {
                widgetRect.sizeDelta = new Vector2(Mathf.Lerp(currentW, targetW, Time.unscaledDeltaTime * 14f), widgetRect.sizeDelta.y);
            }
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "MainMenu" || scene.name == "Menu")
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
        RefreshDisplay();
    }

    private void BuildUI()
    {
        if (canvasObj != null) return;

        canvasObj = new GameObject("QuestHUD_Canvas");
        DontDestroyOnLoad(canvasObj);

        hudCanvas = canvasObj.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 55; // Acima da tela normal, abaixo de modais/menus

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        // Container Pill (Lado Direito, perto do meio vertical)
        widgetObj = new GameObject("QuestHUD_Widget");
        widgetObj.transform.SetParent(canvasObj.transform, false);

        Image bgImg = widgetObj.AddComponent<Image>();
        bgImg.color = BG_COLOR;

        widgetRect = widgetObj.GetComponent<RectTransform>();
        // Ancora no meio da borda direita da tela
        widgetRect.anchorMin = new Vector2(1f, 0.5f);
        widgetRect.anchorMax = new Vector2(1f, 0.5f);
        widgetRect.pivot = new Vector2(1f, 0.5f);
        widgetRect.anchoredPosition = new Vector2(offsetX, offsetY);
        widgetRect.sizeDelta = new Vector2(compactWidth, 80f);

        // Layout vertical dinâmico com auto-ajuste de tamanho
        VerticalLayoutGroup vlg = widgetObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 16, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = widgetObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Borda de acento (esquerda do painel, ignora o LayoutGroup para esticar no fundo)
        GameObject borderGO = new GameObject("AccentBorder");
        borderGO.transform.SetParent(widgetObj.transform, false);
        borderAccent = borderGO.AddComponent<Image>();
        borderAccent.color = BORDER_ACTIVE;
        RectTransform bRect = borderGO.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0f, 0f);
        bRect.anchorMax = new Vector2(0f, 1f);
        bRect.pivot = new Vector2(0f, 0.5f);
        bRect.sizeDelta = new Vector2(5f, 0f);
        bRect.anchoredPosition = Vector2.zero;
        LayoutElement bLe = borderGO.AddComponent<LayoutElement>();
        bLe.ignoreLayout = true;

        // ═══════════════════════════════════════════════════════════════════════
        // A. CONTAINER COMPACTO (GAMEPLAY - RESUMIDO)
        // ═══════════════════════════════════════════════════════════════════════
        compactContainer = new GameObject("Compact_Container");
        compactContainer.transform.SetParent(widgetObj.transform, false);
        VerticalLayoutGroup cVlg = compactContainer.AddComponent<VerticalLayoutGroup>();
        cVlg.spacing = 3f;
        cVlg.padding = new RectOffset(0, 0, 0, 0);
        cVlg.childControlWidth = true;
        cVlg.childControlHeight = true;
        cVlg.childForceExpandWidth = true;
        cVlg.childForceExpandHeight = false;

        // Linha 1: Meta Núcleos
        GameObject cMasterGO = new GameObject("Compact_Master");
        cMasterGO.transform.SetParent(compactContainer.transform, false);
        compactMasterText = cMasterGO.AddComponent<TextMeshProUGUI>();
        compactMasterText.text = "★ NÚCLEOS: 0 / 4 [☆☆☆☆]";
        compactMasterText.fontSize = 13;
        compactMasterText.fontStyle = FontStyles.Bold;
        compactMasterText.color = BORDER_MASTER;
        if (customFont != null) compactMasterText.font = customFont;

        // Linha 2: Missão Ativa
        GameObject cActiveGO = new GameObject("Compact_Active");
        cActiveGO.transform.SetParent(compactContainer.transform, false);
        compactActiveText = cActiveGO.AddComponent<TextMeshProUGUI>();
        compactActiveText.text = "● DIRETRIZ ATIVA";
        compactActiveText.fontSize = 14;
        compactActiveText.fontStyle = FontStyles.Bold;
        compactActiveText.color = TEXT_TITLE;
        compactActiveText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) compactActiveText.font = customFont;

        // Linha 3: Dica de Inventário
        GameObject cHintGO = new GameObject("Compact_Hint");
        cHintGO.transform.SetParent(compactContainer.transform, false);
        compactHintText = cHintGO.AddComponent<TextMeshProUGUI>();
        compactHintText.text = "[TAB] Missão completa";
        compactHintText.fontSize = 10;
        compactHintText.color = new Color(0.7f, 0.65f, 0.85f, 0.6f);
        if (customFont != null) compactHintText.font = customFont;

        // ═══════════════════════════════════════════════════════════════════════
        // B. CONTAINER EXPANDIDO (INVENTÁRIO / TAB - DETALHADO)
        // ═══════════════════════════════════════════════════════════════════════
        expandedContainer = new GameObject("Expanded_Container");
        expandedContainer.transform.SetParent(widgetObj.transform, false);
        VerticalLayoutGroup eVlg = expandedContainer.AddComponent<VerticalLayoutGroup>();
        eVlg.spacing = 10f;
        eVlg.padding = new RectOffset(0, 0, 0, 0);
        eVlg.childControlWidth = true;
        eVlg.childControlHeight = true;
        eVlg.childForceExpandWidth = true;
        eVlg.childForceExpandHeight = false;

        // ═══════════════════════════════════════════════════════════════════════
        // 0. SEÇÃO DA META SUPREMA (NÚCLEOS DA NAVE)
        // ═══════════════════════════════════════════════════════════════════════
        masterSection = CreateSectionContainer("Section_Master");

        // Badge Meta Suprema
        GameObject mBadgeGO = new GameObject("Badge");
        mBadgeGO.transform.SetParent(masterSection.transform, false);
        masterBadgeText = mBadgeGO.AddComponent<TextMeshProUGUI>();
        masterBadgeText.text = "★ META SUPREMA DO JOGO";
        masterBadgeText.fontSize = 14;
        masterBadgeText.fontStyle = FontStyles.Bold;
        masterBadgeText.color = BORDER_MASTER;
        if (customFont != null) masterBadgeText.font = customFont;

        // Título Meta Suprema
        GameObject mTitleGO = new GameObject("Title");
        mTitleGO.transform.SetParent(masterSection.transform, false);
        masterTitleText = mTitleGO.AddComponent<TextMeshProUGUI>();
        masterTitleText.text = "NÚCLEOS DA NAVE";
        masterTitleText.fontSize = 20;
        masterTitleText.fontStyle = FontStyles.Bold;
        masterTitleText.color = TEXT_TITLE;
        if (customFont != null) masterTitleText.font = customFont;

        // Progresso Núcleos
        GameObject mProgGO = new GameObject("Progress");
        mProgGO.transform.SetParent(masterSection.transform, false);
        masterProgressText = mProgGO.AddComponent<TextMeshProUGUI>();
        masterProgressText.text = "Restaurados: 0 / 4";
        masterProgressText.fontSize = 15;
        masterProgressText.fontStyle = FontStyles.Bold;
        masterProgressText.color = BORDER_MASTER;
        if (customFont != null) masterProgressText.font = customFont;

        // Linha divisória após Master Objective
        masterDividerLine = new GameObject("MasterDividerLine");
        masterDividerLine.transform.SetParent(expandedContainer.transform, false);
        Image mDivImg = masterDividerLine.AddComponent<Image>();
        mDivImg.color = new Color(BORDER_MASTER.r, BORDER_MASTER.g, BORDER_MASTER.b, 0.25f);
        LayoutElement mDivLe = masterDividerLine.AddComponent<LayoutElement>();
        mDivLe.minHeight = 1f;
        mDivLe.preferredHeight = 1f;

        // ═══════════════════════════════════════════════════════════════════════
        // 1. SEÇÃO DE MISSÃO CONCLUÍDA (RISCADA)
        // ═══════════════════════════════════════════════════════════════════════
        completedSection = CreateSectionContainer("Section_Completed");

        // Badge concluída
        GameObject cBadgeGO = new GameObject("Badge");
        cBadgeGO.transform.SetParent(completedSection.transform, false);
        completedBadgeText = cBadgeGO.AddComponent<TextMeshProUGUI>();
        completedBadgeText.text = "✔ CONCLUÍDA";
        completedBadgeText.fontSize = 15;
        completedBadgeText.fontStyle = FontStyles.Bold;
        completedBadgeText.color = BORDER_DONE;
        if (customFont != null) completedBadgeText.font = customFont;

        // Título riscado
        GameObject cTitleGO = new GameObject("Title");
        cTitleGO.transform.SetParent(completedSection.transform, false);
        completedTitleText = cTitleGO.AddComponent<TextMeshProUGUI>();
        completedTitleText.text = "<s>NOME DA MISSÃO</s>";
        completedTitleText.fontSize = 20;
        completedTitleText.fontStyle = FontStyles.Bold;
        completedTitleText.color = TEXT_DONE_TITLE;
        completedTitleText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) completedTitleText.font = customFont;

        // Subtexto riscado
        GameObject cProgGO = new GameObject("Progress");
        cProgGO.transform.SetParent(completedSection.transform, false);
        completedProgressText = cProgGO.AddComponent<TextMeshProUGUI>();
        completedProgressText.text = "<s>✔ Diretriz concluída</s>";
        completedProgressText.fontSize = 15;
        completedProgressText.color = TEXT_DONE_SUB;
        if (customFont != null) completedProgressText.font = customFont;

        // ═══════════════════════════════════════════════════════════════════════
        // 2. LINHA DIVISÓRIA
        // ═══════════════════════════════════════════════════════════════════════
        dividerLine = new GameObject("DividerLine");
        dividerLine.transform.SetParent(expandedContainer.transform, false);
        Image divImg = dividerLine.AddComponent<Image>();
        divImg.color = DIVIDER_COLOR;
        LayoutElement divLe = dividerLine.AddComponent<LayoutElement>();
        divLe.minHeight = 1f;
        divLe.preferredHeight = 1f;

        // ═══════════════════════════════════════════════════════════════════════
        // 3. SEÇÃO DE MISSÃO ATIVA
        // ═══════════════════════════════════════════════════════════════════════
        activeSection = CreateSectionContainer("Section_Active");

        // Badge ativa
        GameObject aBadgeGO = new GameObject("Badge");
        aBadgeGO.transform.SetParent(activeSection.transform, false);
        activeBadgeText = aBadgeGO.AddComponent<TextMeshProUGUI>();
        activeBadgeText.text = "● DIRETRIZ ATIVA";
        activeBadgeText.fontSize = 15;
        activeBadgeText.fontStyle = FontStyles.Bold;
        activeBadgeText.color = BORDER_ACTIVE;
        if (customFont != null) activeBadgeText.font = customFont;

        // Título ativo
        GameObject aTitleGO = new GameObject("Title");
        aTitleGO.transform.SetParent(activeSection.transform, false);
        activeTitleText = aTitleGO.AddComponent<TextMeshProUGUI>();
        activeTitleText.text = "TÍTULO DA MISSÃO";
        activeTitleText.fontSize = 22;
        activeTitleText.fontStyle = FontStyles.Bold;
        activeTitleText.color = TEXT_TITLE;
        activeTitleText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) activeTitleText.font = customFont;

        // Subtexto / Progresso ativo
        GameObject aProgGO = new GameObject("Progress");
        aProgGO.transform.SetParent(activeSection.transform, false);
        activeProgressText = aProgGO.AddComponent<TextMeshProUGUI>();
        activeProgressText.text = "Progresso: 0 / 1  •  [C] Oráculo";
        activeProgressText.fontSize = 16;
        activeProgressText.fontStyle = FontStyles.Bold;
        activeProgressText.color = TEXT_SUB;
        if (customFont != null) activeProgressText.font = customFont;

        // Inicia em modo compacto (gameplay)
        compactContainer.SetActive(true);
        expandedContainer.SetActive(false);
        isCurrentlyExpanded = false;
    }

    private GameObject CreateSectionContainer(string name)
    {
        GameObject sec = new GameObject(name);
        sec.transform.SetParent(expandedContainer != null ? expandedContainer.transform : widgetObj.transform, false);
        VerticalLayoutGroup v = sec.AddComponent<VerticalLayoutGroup>();
        v.spacing = 3f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return sec;
    }

    public void RefreshDisplay()
    {
        if (widgetObj == null) BuildUI();

        // Atualiza a posição caso o usuário tenha ajustado os campos no Inspector
        if (widgetRect != null)
        {
            widgetRect.anchoredPosition = new Vector2(offsetX, offsetY);
            widgetRect.sizeDelta = new Vector2(isCurrentlyExpanded ? widgetWidth : compactWidth, widgetRect.sizeDelta.y);
        }

        PersistentSaveData save = SaveManager.instance != null ? SaveManager.instance.CachedData : null;
        QuestData active = QuestManager.Instance != null ? QuestManager.Instance.currentQuest : null;

        if (active == null && save != null && !string.IsNullOrEmpty(save.activeQuestId))
        {
            active = QuestManager.Instance != null 
                ? QuestManager.Instance.FindQuestById(save.activeQuestId) 
                : Resources.Load<QuestData>("Quests/" + save.activeQuestId);
        }

        // Recupera a última missão concluída registrada
        QuestData completedQuest = null;
        if (save != null && save.completedQuestIds != null && save.completedQuestIds.Count > 0)
        {
            string lastId = save.completedQuestIds[save.completedQuestIds.Count - 1];
            completedQuest = QuestManager.Instance != null 
                ? QuestManager.Instance.FindQuestById(lastId) 
                : Resources.Load<QuestData>("Quests/" + lastId);
        }

        bool hasCompleted = completedQuest != null;
        bool hasActive = active != null;
        int shipParts = SaveManager.instance != null ? SaveManager.instance.GetShipPartsRecovered() : 0;

        string coresIcons = "";
        for (int i = 0; i < 4; i++)
        {
            coresIcons += (i < shipParts) ? "★" : "☆";
        }

        // ── Atualização do Modo Compacto (Gameplay) ──
        if (compactMasterText != null)
        {
            compactMasterText.text = $"★ NÚCLEOS: {shipParts} / 4  [{coresIcons}]";
        }

        if (compactActiveText != null)
        {
            if (hasActive)
            {
                int prog = save != null ? save.activeQuestProgress : 0;
                int req = Mathf.Max(1, active.requiredAmount);
                compactActiveText.text = $"● {active.questTitle.ToUpper()}  ({prog}/{req})";
                compactActiveText.color = TEXT_TITLE;
            }
            else if (hasCompleted)
            {
                compactActiveText.text = "✔ TODAS AS DIRETRIZES CONCLUÍDAS";
                compactActiveText.color = BORDER_DONE;
            }
            else
            {
                compactActiveText.text = "★ RESTAURE A NAVE";
                compactActiveText.color = BORDER_MASTER;
            }
        }

        // O widget exibe a meta suprema e as diretrizes do jogo
        widgetObj.SetActive(true);

        // 0. Configura a Seção da Meta Suprema (Núcleos da Nave)
        if (masterSection != null)
        {
            masterSection.SetActive(true);
            masterProgressText.text = $"Núcleos: {shipParts} / 4  [{coresIcons}]";
        }

        if (masterDividerLine != null)
        {
            masterDividerLine.SetActive(hasCompleted || hasActive);
        }

        // 1. Configura a Seção Concluída (Riscada)
        if (hasCompleted)
        {
            completedSection.SetActive(true);
            completedBadgeText.text = "✔ CONCLUÍDA";
            completedBadgeText.color = BORDER_DONE;
            
            string cTitle = completedQuest.questTitle.ToUpper();
            completedTitleText.text = $"<s>{cTitle}</s>";
            completedProgressText.text = "<s>✔ Diretriz concluída</s>";
        }
        else
        {
            completedSection.SetActive(false);
        }

        // 2. Linha divisória se ambas existirem
        dividerLine.SetActive(hasCompleted && hasActive);

        // 3. Configura a Seção Ativa
        if (hasActive)
        {
            activeSection.SetActive(true);
            borderAccent.color = BORDER_ACTIVE;
            activeBadgeText.text = "● DIRETRIZ ATIVA";
            activeBadgeText.color = BORDER_ACTIVE;
            activeTitleText.text = active.questTitle.ToUpper();

            int prog = save != null ? save.activeQuestProgress : 0;
            int req = Mathf.Max(1, active.requiredAmount);
            activeProgressText.text = $"Objetivo: {prog} / {req}  •  [C] Oráculo";
        }
        else
        {
            // Se tudo já foi concluído e não há nova diretriz ativa,
            // a borda fica verde destacando que tudo está finalizado
            activeSection.SetActive(false);
            borderAccent.color = hasCompleted ? BORDER_DONE : BORDER_MASTER;
        }
    }

    private void HandleQuestCompleted(QuestData completed)
    {
        if (completed != null)
        {
            lastFinishedQuestId = completed.questId;
        }

        RefreshDisplay();

        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseWidget());
    }

    private IEnumerator PulseWidget()
    {
        if (widgetRect == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = new Vector3(1.06f, 1.06f, 1f);

        float elapsed = 0f;
        float duration = 0.18f;

        // Pulso suave de conclusão
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            widgetRect.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            widgetRect.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            yield return null;
        }

        widgetRect.localScale = originalScale;
    }
}
