using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente de UI do Timer de Speedrun do Modo Endless.
/// Exibe um relógio digital no canto superior esquerdo do Canvas enquanto a Run Endless estiver ativa.
/// </summary>
public class EndlessTimerHUD : MonoBehaviour
{
    public static EndlessTimerHUD Instance { get; private set; }

    [Header("Referências da UI (Canvas)")]
    [Tooltip("Texto TMP do Timer de Speedrun no Canvas.")]
    public TextMeshProUGUI timerText;

    [Tooltip("Container/Painel pai do Timer no Canvas.")]
    public GameObject timerPanel;

    public bool IsTimerVisible => timerPanel != null && timerPanel.activeInHierarchy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            EndlessTimerHUD existing = FindFirstObjectByType<EndlessTimerHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return;
            }
            GameObject go = new GameObject("EndlessTimerHUD_AutoInit");
            DontDestroyOnLoad(go);
            go.AddComponent<EndlessTimerHUD>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        EnsureUIReferences();
        if (timerPanel != null)
        {
            timerPanel.SetActive(false);
        }
    }

    private void Update()
    {
        bool isEndless = (RunManager.instance != null && RunManager.instance.isEndlessMode) ||
                         (RunStatsManager.Instance != null && RunStatsManager.Instance.isEndlessMode);

        bool speedrunOption = GameGraphicsSettings.SpeedrunTimerEnabled;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isGameplayScene = sceneName != "MainMenu" && sceneName != "Menu" && !sceneName.Contains("Loading");

        bool isRunActive = (RunManager.instance != null && RunManager.instance.currentRoomNumber >= 1) ||
                           (RunStatsManager.Instance != null && RunStatsManager.Instance.isRunActive);

        if (speedrunOption && isGameplayScene && LevelGenerator.Instance != null &&
            RunStatsManager.Instance != null && !RunStatsManager.Instance.isRunActive)
        {
            RunStatsManager.Instance.StartRunTracking(false);
            isRunActive = true;
        }

        bool shouldShow = (isEndless && isRunActive) || (speedrunOption && isGameplayScene);

        if (shouldShow)
        {
            EnsureUIReferences();
        }

        if (timerPanel != null && timerPanel.activeSelf != shouldShow)
        {
            timerPanel.SetActive(shouldShow);
        }

        if (shouldShow && timerText != null && RunStatsManager.Instance != null)
        {
            if (isEndless && isRunActive)
            {
                string timeStr = RunStatsManager.Instance.FormatTime(RunStatsManager.Instance.survivalTimer);
                timerText.text = $"<color=#ffaa00><b>ENDLESS</b></color>  <color=#ffffff><b>{timeStr}</b></color>";
            }
            else
            {
                float sec = RunStatsManager.Instance.survivalTimer;
                System.TimeSpan t = System.TimeSpan.FromSeconds(sec);
                int centiseconds = Mathf.FloorToInt((sec - Mathf.Floor(sec)) * 100f);
                string timeStr = t.Hours > 0
                    ? string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D2}", t.Hours, t.Minutes, t.Seconds, centiseconds)
                    : string.Format("{0:D2}:{1:D2}.{2:D2}", t.Minutes, t.Seconds, centiseconds);
                timerText.text = $"<color=#48D1CC><b>SPEEDRUN</b></color>  <color=#ffffff><b>{timeStr}</b></color>";
            }
        }
    }

    private void EnsureUIReferences()
    {
        if (timerText == null)
        {
            timerText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (timerPanel == null && timerText != null)
        {
            timerPanel = timerText.gameObject;
        }

        // Se não houver referências atribuídas no Inspector, gera uma estrutura limpa no topo esquerdo do Canvas real do Player
        if (timerText == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindTargetPlayerCanvas();
            }

            if (parentCanvas != null)
            {
                GameObject panelGo = new GameObject("EndlessTimerPanel");
                panelGo.transform.SetParent(parentCanvas.transform, false);

                RectTransform rt = panelGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -25f);
                rt.sizeDelta = new Vector2(250f, 45f);

                Image bg = panelGo.AddComponent<Image>();
                bg.color = new Color(0.04f, 0.04f, 0.08f, 0.75f);

                Shadow shadow = panelGo.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(3f, -3f);

                GameObject textGo = new GameObject("txtTimer");
                textGo.transform.SetParent(panelGo.transform, false);
                RectTransform textRt = textGo.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(10f, 0f);
                textRt.offsetMax = new Vector2(-10f, 0f);

                timerText = textGo.AddComponent<TextMeshProUGUI>();
                timerText.fontSize = 22f;
                timerText.fontStyle = FontStyles.Bold;
                timerText.alignment = TextAlignmentOptions.MidlineLeft;
                timerText.color = Color.white;

                timerPanel = panelGo;
            }
        }
    }

    private static Canvas FindTargetPlayerCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy)
            {
                if (c.name.Contains("Player Canvas") || c.name.Contains("PlayerCanvas") || c.name.Contains("HUD"))
                {
                    return c;
                }
            }
        }
        foreach (var c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy && c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                if (c.name.Contains("Pause") || c.name.Contains("Loading") || c.name.Contains("Opcoes")) continue;
                return c;
            }
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }
}

/// <summary>
/// Exibe o contador de FPS em tempo real na tela quando a opção "Mostrar FPS" está ativada em Gráficos.
/// Preparado para UI customizada: se <see cref="fpsText"/> / <see cref="fpsPanel"/> forem atribuídos no Inspector
/// (ex: no futuro Canvas/HUD definitiva), usa sua UI diretamente sem criar nada por script nem mover sua posição.
/// </summary>
public class FpsCounterHUD : MonoBehaviour
{
    public static FpsCounterHUD Instance { get; private set; }

    [Header("Referências da UI (Opcional - HUD Customizada)")]
    [Tooltip("Painel/container customizado do contador de FPS. Se vazio, usa o texto ou cria um overlay padrão.")]
    public GameObject fpsPanel;

    [Tooltip("Texto TMP customizado do contador de FPS. Se preenchido, substitui o overlay criado por script.")]
    public TextMeshProUGUI fpsText;

    [Tooltip("Quando verdadeiro, mantém a posição definida no seu RectTransform em vez de deslocar automaticamente abaixo do timer de Speedrun.")]
    public bool keepCustomLayoutPosition;

    private Canvas overlayCanvas;
    private RectTransform fpsPanelRt;
    private bool _usingScriptOverlay;

    private int frameCount;
    private float accumulatedTime;
    private float lastApplyTime;

    private const float UpdateInterval = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            FpsCounterHUD existing = FindFirstObjectByType<FpsCounterHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return;
            }
            GameObject go = new GameObject("FpsCounterHUD_AutoInit");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsCounterHUD>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Se colocado diretamente na HUD definitiva, detecta referências filhas e preserva a posição customizada
        if (fpsText == null)
        {
            fpsText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
        if (fpsPanel == null && fpsText != null)
        {
            fpsPanel = fpsText.gameObject;
        }
        if (fpsPanel != null)
        {
            fpsPanelRt = fpsPanel.GetComponent<RectTransform>();
            keepCustomLayoutPosition = true;
        }
    }

    private void OnEnable()
    {
        GameGraphicsSettings.OnSettingsChanged += RefreshVisibility;
        RefreshVisibility();
    }

    private void OnDisable()
    {
        GameGraphicsSettings.OnSettingsChanged -= RefreshVisibility;
    }

    private void Update()
    {
        bool shouldShow = GameGraphicsSettings.ShowFpsEnabled;
        if (!shouldShow)
        {
            if (fpsPanel != null && fpsPanel.activeSelf)
            {
                fpsPanel.SetActive(false);
            }
            return;
        }

        EnsureOverlayUI();
        if (fpsPanel != null && !fpsPanel.activeSelf)
        {
            fpsPanel.SetActive(true);
        }

        // Posiciona abaixo do contador de Speedrun/Endless apenas no overlay automático para nunca sobrepor
        if (_usingScriptOverlay && !keepCustomLayoutPosition && fpsPanelRt != null)
        {
            bool timerVisible = EndlessTimerHUD.Instance != null && EndlessTimerHUD.Instance.IsTimerVisible;
            float targetY = timerVisible ? -78f : -25f;
            if (!Mathf.Approximately(fpsPanelRt.anchoredPosition.y, targetY))
            {
                fpsPanelRt.anchoredPosition = new Vector2(25f, targetY);
            }
        }

        frameCount++;
        accumulatedTime += Time.unscaledDeltaTime;

        if (Time.unscaledTime - lastApplyTime >= UpdateInterval && accumulatedTime > 0.0001f)
        {
            float fps = frameCount / accumulatedTime;
            float ms = (accumulatedTime / frameCount) * 1000f;
            int roundedFps = Mathf.RoundToInt(fps);

            if (fpsText != null)
            {
                string colorHex = roundedFps >= 55 ? "#5CE68A" : (roundedFps >= 30 ? "#FFD24C" : "#FF5A5A");
                fpsText.text = $"<color={colorHex}><b>FPS: {roundedFps}</b></color> <size=15><color=#C8D4E8>({ms:0.0} ms)</color></size>";
            }

            frameCount = 0;
            accumulatedTime = 0f;
            lastApplyTime = Time.unscaledTime;
        }
    }

    private void RefreshVisibility()
    {
        if (fpsPanel != null)
        {
            fpsPanel.SetActive(GameGraphicsSettings.ShowFpsEnabled);
        }
    }

    private void EnsureOverlayUI()
    {
        if (fpsText != null)
        {
            if (fpsPanel == null) fpsPanel = fpsText.gameObject;
            if (fpsPanelRt == null) fpsPanelRt = fpsPanel.GetComponent<RectTransform>();
            return;
        }
        if (fpsPanel != null) return;

        _usingScriptOverlay = true;

        GameObject canvasGo = new GameObject("Canvas_FpsOverlay", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        overlayCanvas = canvasGo.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        fpsPanel = new GameObject("FpsPanel", typeof(RectTransform), typeof(Image), typeof(Shadow));
        fpsPanel.transform.SetParent(canvasGo.transform, false);

        fpsPanelRt = fpsPanel.GetComponent<RectTransform>();
        fpsPanelRt.anchorMin = new Vector2(0f, 1f);
        fpsPanelRt.anchorMax = new Vector2(0f, 1f);
        fpsPanelRt.pivot = new Vector2(0f, 1f);
        fpsPanelRt.anchoredPosition = new Vector2(25f, -25f);
        fpsPanelRt.sizeDelta = new Vector2(215f, 38f);

        Image bg = fpsPanel.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.08f, 0.78f);
        bg.raycastTarget = false;

        Shadow shadow = fpsPanel.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);

        GameObject txtGo = new GameObject("Txt_FPS", typeof(RectTransform));
        txtGo.transform.SetParent(fpsPanel.transform, false);
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(10f, 0f);
        txtRt.offsetMax = new Vector2(-10f, 0f);

        fpsText = txtGo.AddComponent<TextMeshProUGUI>();
        fpsText.fontSize = 20f;
        fpsText.fontStyle = FontStyles.Bold;
        fpsText.alignment = TextAlignmentOptions.MidlineLeft;
        fpsText.color = Color.white;
        fpsText.raycastTarget = false;
        fpsText.text = "<color=#5CE68A><b>FPS: --</b></color>";
    }
}
