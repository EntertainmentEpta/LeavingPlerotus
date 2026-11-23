using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Painel procedural uGUI de Seleção de Modo de Jogo (Normal vs Modo Sem Fim / Endless).
/// - Estilo sci-fi escuro coerente com CraftingUI e VictoryScreenUI.
/// - Auto-cria Canvas e hierarquia completa sem exigir montagem manual na cena.
/// - Exibe o recorde salvo do Modo Endless (EndlessBestLevel e EndlessBestTime) e o modo ativo atual.
/// - Pausa/bloqueia gameplay via GamePause e libera cursor enquanto aberto.
/// </summary>
public class GameModeSelectionUI : MonoBehaviour
{
    public static GameModeSelectionUI Instance { get; private set; }

    // Paleta sci-fi escura alinhada com CraftingUI
    private static readonly Color OVERLAY_BG = new Color(0.02f, 0.03f, 0.05f, 0.84f);
    private static readonly Color PANEL_BG = new Color(0.055f, 0.075f, 0.095f, 0.98f);
    private static readonly Color CARD_BG = new Color(0.085f, 0.11f, 0.135f, 0.98f);
    private static readonly Color PANEL_BORDER = new Color(0.33f, 0.82f, 0.76f, 0.45f);
    private static readonly Color NORMAL_ACCENT = new Color(0.32f, 0.82f, 0.75f, 1f);
    private static readonly Color ENDLESS_ACCENT = new Color(1.0f, 0.667f, 0.0f, 1f); // #ffaa00
    private static readonly Color BTN_NORMAL_BG = new Color(0.16f, 0.48f, 0.46f, 1f);
    private static readonly Color BTN_ENDLESS_BG = new Color(0.68f, 0.36f, 0.06f, 1f);
    private static readonly Color BTN_ACTIVE_BG = new Color(0.18f, 0.24f, 0.28f, 1f);

    private bool isOpen = false;
    private bool uiBuilt = false;

    private GameObject canvasObj;
    private GameObject rootOverlay;
    private GameModeTerminalInteraction activeTerminal;

    // Elementos dinâmicos atualizados no RefreshUI()
    private TextMeshProUGUI txtActiveStatus;
    private TextMeshProUGUI txtNormalBadge;
    private TextMeshProUGUI txtEndlessBadge;
    private TextMeshProUGUI txtEndlessRecord;
    private Outline normalCardOutline;
    private Outline endlessCardOutline;
    private Image normalBtnImage;
    private Image endlessBtnImage;
    private TextMeshProUGUI txtNormalBtn;
    private TextMeshProUGUI txtEndlessBtn;

    public static GameModeSelectionUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameModeSelectionUI existing = FindFirstObjectByType<GameModeSelectionUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("GameModeSelectionUI_Auto");
        Instance = go.AddComponent<GameModeSelectionUI>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            isOpen = false;
            GamePause.Release(this);
        }

        if (canvasObj != null)
        {
            Destroy(canvasObj);
            canvasObj = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public bool IsOpen() => gameObject != null && gameObject.activeInHierarchy && isOpen;

    public void Open(GameModeTerminalInteraction terminal = null)
    {
        if (isOpen) return;

        activeTerminal = terminal;
        EnsureEventSystem();
        if (!uiBuilt || canvasObj == null)
        {
            BuildUI();
        }

        isOpen = true;
        GamePause.Acquire(this);

        if (canvasObj != null) canvasObj.SetActive(true);
        if (rootOverlay != null) rootOverlay.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshUI();
    }

    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;

        if (rootOverlay != null) rootOverlay.SetActive(false);
        if (canvasObj != null) canvasObj.SetActive(false);

        GamePause.Release(this);
        AimSettings.HideAimCursorIfNoUI();
    }

    public void SelectMode(bool endless)
    {
        RunManager rm = EnsureRunManager();
        rm.SetEndlessMode(endless);

        if (activeTerminal != null)
        {
            activeTerminal.OnModeSelected(endless);
        }
        else if (GameModeTerminalInteraction.Instance != null)
        {
            GameModeTerminalInteraction.Instance.OnModeSelected(endless);
        }

        RefreshUI();
        Close();
    }

    private static RunManager EnsureRunManager()
    {
        if (RunManager.instance != null) return RunManager.instance;

        RunManager existing = FindFirstObjectByType<RunManager>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject go = new GameObject("RunManager");
        return go.AddComponent<RunManager>();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem_ModeTerminalFallback");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    private void RefreshUI()
    {
        bool isEndless = RunManager.instance != null && RunManager.instance.isEndlessMode;

        if (txtActiveStatus != null)
        {
            txtActiveStatus.text = isEndless
                ? "<color=#B8C8D0>MODO CONFIGURADO NA PORTA DE SAÍDA:</color>  <color=#FFAA00><b>MODO SEM FIM (ENDLESS)</b></color>"
                : "<color=#B8C8D0>MODO CONFIGURADO NA PORTA DE SAÍDA:</color>  <color=#52D1BF><b>MODO NORMAL</b></color>";
        }

        if (txtNormalBadge != null)
        {
            txtNormalBadge.text = !isEndless
                ? "<color=#52D1BF><b>[ ATIVO ]</b></color>"
                : "<color=#6E808A>DISPONÍVEL</color>";
        }

        if (txtEndlessBadge != null)
        {
            txtEndlessBadge.text = isEndless
                ? "<color=#FFAA00><b>[ ATIVO ]</b></color>"
                : "<color=#6E808A>DISPONÍVEL</color>";
        }

        if (normalCardOutline != null)
        {
            normalCardOutline.effectColor = !isEndless
                ? NORMAL_ACCENT
                : new Color(0.32f, 0.82f, 0.75f, 0.25f);
            normalCardOutline.effectDistance = !isEndless ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
        }

        if (endlessCardOutline != null)
        {
            endlessCardOutline.effectColor = isEndless
                ? ENDLESS_ACCENT
                : new Color(1.0f, 0.667f, 0.0f, 0.25f);
            endlessCardOutline.effectDistance = isEndless ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
        }

        if (normalBtnImage != null) normalBtnImage.color = !isEndless ? BTN_ACTIVE_BG : BTN_NORMAL_BG;
        if (endlessBtnImage != null) endlessBtnImage.color = isEndless ? BTN_ACTIVE_BG : BTN_ENDLESS_BG;

        if (txtNormalBtn != null)
        {
            txtNormalBtn.text = !isEndless ? "ATIVO  (CONFIRMAR)" : "ATIVAR MODO NORMAL";
        }

        if (txtEndlessBtn != null)
        {
            txtEndlessBtn.text = isEndless ? "ATIVO  (CONFIRMAR)" : "ATIVAR MODO ENDLESS";
        }

        if (txtEndlessRecord != null)
        {
            int bestLevel = PlayerPrefs.GetInt(RunStatsManager.EndlessBestLevelKey, 0);
            float bestTime = PlayerPrefs.GetFloat(RunStatsManager.EndlessBestTimeKey, 0f);

            if (RunStatsManager.Instance != null)
            {
                bestLevel = Mathf.Max(bestLevel, RunStatsManager.Instance.BestEndlessLevel);
                bestTime = Mathf.Max(bestTime, RunStatsManager.Instance.BestEndlessTime);
            }

            if (bestLevel > 0 || bestTime > 0f)
            {
                string formattedTime = FormatTimeSafe(bestTime);
                txtEndlessRecord.text =
                    $"<color=#9EC5FE><b>RECORDE ENDLESS SALVO</b></color>\n" +
                    $"<size=20><color=#FFFFFF><b>Nível {bestLevel}</b></color>  <color=#78909C>|</color>  <color=#FFCC66><b>{formattedTime}</b></color></size>";
            }
            else
            {
                txtEndlessRecord.text =
                    "<color=#9EC5FE><b>RECORDE ENDLESS SALVO</b></color>\n" +
                    "<size=17><color=#8EA2AE>Nenhuma expedição Endless registrada ainda</color></size>";
            }
        }
    }

    private static string FormatTimeSafe(float timeInSeconds)
    {
        if (RunStatsManager.Instance != null)
        {
            return RunStatsManager.Instance.FormatTime(timeInSeconds);
        }

        System.TimeSpan t = System.TimeSpan.FromSeconds(Mathf.Max(0f, timeInSeconds));
        return t.Hours > 0
            ? string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds)
            : string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
    }

    // =====================================================
    // CONSTRUÇÃO PROCEDURAL DA UI (uGUI)
    // =====================================================

    private void BuildUI()
    {
        if (canvasObj != null) Destroy(canvasObj);

        canvasObj = new GameObject("GameModeSelection_Canvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Overlay escurecido
        rootOverlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        rootOverlay.transform.SetParent(canvasObj.transform, false);
        RectTransform overlayRt = rootOverlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        rootOverlay.GetComponent<Image>().color = OVERLAY_BG;

        // Painel principal
        GameObject mainPanel = new GameObject("MainPanel", typeof(RectTransform), typeof(Image));
        mainPanel.transform.SetParent(rootOverlay.transform, false);
        RectTransform panelRt = mainPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(960f, 600f);
        mainPanel.GetComponent<Image>().color = PANEL_BG;

        Outline panelOutline = mainPanel.AddComponent<Outline>();
        panelOutline.effectColor = PANEL_BORDER;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        // Cabeçalho
        CreateText(
            mainPanel.transform,
            "HeaderTitle",
            "<color=#F0F5F2><b>TERMINAL DE EXPEDIÇÃO</b></color>  <color=#52D1BF>—</color>  <color=#D6E4E8>SELEÇÃO DE MODO</color>",
            28f,
            new Vector2(0f, 250f),
            new Vector2(880f, 44f),
            TextAlignmentOptions.Center);

        txtActiveStatus = CreateText(
            mainPanel.transform,
            "ActiveStatusText",
            "",
            18f,
            new Vector2(0f, 206f),
            new Vector2(880f, 32f),
            TextAlignmentOptions.Center);

        // Linha divisória sutil
        GameObject divider = new GameObject("HeaderDivider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(mainPanel.transform, false);
        RectTransform divRt = divider.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.5f, 0.5f);
        divRt.anchorMax = new Vector2(0.5f, 0.5f);
        divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.anchoredPosition = new Vector2(0f, 180f);
        divRt.sizeDelta = new Vector2(880f, 2f);
        divider.GetComponent<Image>().color = new Color(0.33f, 0.82f, 0.76f, 0.22f);

        // Card 1: Modo Normal (Esquerda)
        BuildNormalCard(mainPanel.transform, new Vector2(-225f, -15f));

        // Card 2: Modo Sem Fim / Endless (Direita)
        BuildEndlessCard(mainPanel.transform, new Vector2(225f, -15f));

        // Rodapé: Botão Fechar
        CreateActionButton(
            mainPanel.transform,
            "Btn_Close",
            "FECHAR  [ESC]",
            new Vector2(0f, -252f),
            new Vector2(250f, 44f),
            new Color(0.14f, 0.18f, 0.22f, 1f),
            new Color(0.45f, 0.58f, 0.64f, 0.65f),
            16f,
            Close);

        rootOverlay.SetActive(false);
        canvasObj.SetActive(false);
        uiBuilt = true;
    }

    private void BuildNormalCard(Transform parent, Vector2 anchoredPos)
    {
        GameObject card = new GameObject("Card_Normal", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(420f, 360f);
        card.GetComponent<Image>().color = CARD_BG;

        normalCardOutline = card.AddComponent<Outline>();
        normalCardOutline.effectColor = NORMAL_ACCENT;
        normalCardOutline.effectDistance = new Vector2(2f, -2f);

        txtNormalBadge = CreateText(
            card.transform,
            "BadgeNormal",
            "",
            15f,
            new Vector2(0f, 148f),
            new Vector2(360f, 26f),
            TextAlignmentOptions.Center);

        CreateText(
            card.transform,
            "TitleNormal",
            "<color=#52D1BF><b>MODO NORMAL</b></color>",
            28f,
            new Vector2(0f, 108f),
            new Vector2(370f, 42f),
            TextAlignmentOptions.Center);

        CreateText(
            card.transform,
            "DescNormal",
            "Expedição padrão pelo subsolo em 3 andares progressivos até o confronto final contra o <b>Boss Cromático</b>.\n\n" +
            "<color=#A9C4CC>» Progressão clássica de salas e inimigos\n" +
            "» Encontro final no Nível 4 (Boss)\n" +
            "» Fenda Pós-Boss disponível após a vitória</color>",
            17f,
            new Vector2(0f, 4f),
            new Vector2(365f, 165f),
            TextAlignmentOptions.TopLeft);

        Button btn = CreateActionButton(
            card.transform,
            "Btn_SelectNormal",
            "ATIVAR MODO NORMAL",
            new Vector2(0f, -132f),
            new Vector2(340f, 52f),
            BTN_NORMAL_BG,
            NORMAL_ACCENT,
            18f,
            () => SelectMode(false));

        normalBtnImage = btn.GetComponent<Image>();
        txtNormalBtn = btn.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void BuildEndlessCard(Transform parent, Vector2 anchoredPos)
    {
        GameObject card = new GameObject("Card_Endless", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        RectTransform rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(420f, 360f);
        card.GetComponent<Image>().color = CARD_BG;

        endlessCardOutline = card.AddComponent<Outline>();
        endlessCardOutline.effectColor = ENDLESS_ACCENT;
        endlessCardOutline.effectDistance = new Vector2(2f, -2f);

        txtEndlessBadge = CreateText(
            card.transform,
            "BadgeEndless",
            "",
            15f,
            new Vector2(0f, 148f),
            new Vector2(360f, 26f),
            TextAlignmentOptions.Center);

        CreateText(
            card.transform,
            "TitleEndless",
            "<color=#FFAA00><b>MODO SEM FIM</b></color>",
            28f,
            new Vector2(0f, 108f),
            new Vector2(370f, 42f),
            TextAlignmentOptions.Center);

        CreateText(
            card.transform,
            "DescEndless",
            "Sobreviva a ciclos infinitos com dificuldade crescente e recompensas amplificadas.\n\n" +
            "<color=#DFC9A6>» Mutações do Abismo sorteadas por andar\n" +
            "» Boss do Ciclo a cada 3 andares\n" +
            "» Escalonamento progressivo de Elites e velocidade</color>",
            16f,
            new Vector2(0f, 26f),
            new Vector2(365f, 130f),
            TextAlignmentOptions.TopLeft);

        // Caixa de Recorde salvo
        GameObject recordBox = new GameObject("RecordBox", typeof(RectTransform), typeof(Image));
        recordBox.transform.SetParent(card.transform, false);
        RectTransform recRt = recordBox.GetComponent<RectTransform>();
        recRt.anchorMin = new Vector2(0.5f, 0.5f);
        recRt.anchorMax = new Vector2(0.5f, 0.5f);
        recRt.pivot = new Vector2(0.5f, 0.5f);
        recRt.anchoredPosition = new Vector2(0f, -68f);
        recRt.sizeDelta = new Vector2(365f, 58f);
        recordBox.GetComponent<Image>().color = new Color(0.05f, 0.065f, 0.09f, 0.95f);

        Outline recOutline = recordBox.AddComponent<Outline>();
        recOutline.effectColor = new Color(1.0f, 0.667f, 0.0f, 0.35f);
        recOutline.effectDistance = new Vector2(1.5f, -1.5f);

        txtEndlessRecord = CreateText(
            recordBox.transform,
            "TxtEndlessRecord",
            "",
            14f,
            Vector2.zero,
            new Vector2(345f, 50f),
            TextAlignmentOptions.Center);

        Button btn = CreateActionButton(
            card.transform,
            "Btn_SelectEndless",
            "ATIVAR MODO ENDLESS",
            new Vector2(0f, -132f),
            new Vector2(340f, 52f),
            BTN_ENDLESS_BG,
            ENDLESS_ACCENT,
            18f,
            () => SelectMode(true));

        endlessBtnImage = btn.GetComponent<Image>();
        txtEndlessBtn = btn.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string content,
        float fontSize,
        Vector2 anchoredPos,
        Vector2 size,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateActionButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        Color bgColor,
        Color borderColor,
        float fontSize,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnGo.GetComponent<Image>();
        img.color = bgColor;

        Outline outline = btnGo.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button btn = btnGo.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
        cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(10f, 4f);
        txtRt.offsetMax = new Vector2(-10f, -4f);

        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }
}
