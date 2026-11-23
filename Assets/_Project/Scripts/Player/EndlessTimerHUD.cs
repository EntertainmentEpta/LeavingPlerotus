using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente de UI do Timer de Speedrun e HUD do Modo Endless (Tempo, Recorde e Mutações do Abismo).
/// Exibe o painel no canto superior esquerdo do Canvas sem sobrepor o banner central do RunManager nem o FpsCounterHUD.
/// </summary>
public class EndlessTimerHUD : MonoBehaviour
{
    public static EndlessTimerHUD Instance { get; private set; }

    [Header("Referências da UI (Canvas)")]
    [Tooltip("Texto TMP do Timer de Speedrun/Endless no Canvas.")]
    public TextMeshProUGUI timerText;

    [Tooltip("Container/Painel pai do Timer no Canvas.")]
    public GameObject timerPanel;

    private bool _createdDefaultPanel = false;
    private RectTransform _panelRectTransform;

    public bool IsTimerVisible => timerPanel != null && timerPanel.activeInHierarchy;

    /// <summary>
    /// Retorna a coordenada Y segura (âncora topo-esquerdo) para posicionar o FpsCounterHUD logo abaixo deste painel sem sobreposição.
    /// </summary>
    public float GetFpsAnchorOffsetY()
    {
        if (!IsTimerVisible) return -25f;

        if (_panelRectTransform == null && timerPanel != null)
        {
            _panelRectTransform = timerPanel.GetComponent<RectTransform>();
        }

        if (_panelRectTransform != null)
        {
            float topY = _panelRectTransform.anchoredPosition.y;
            float height = Mathf.Max(45f, _panelRectTransform.sizeDelta.y);
            return topY - height - 8f;
        }

        return -78f;
    }

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
        bool isBaseScene = sceneName == "Base" || sceneName == "BaseLab";

        bool statsTrackingActive = RunStatsManager.Instance != null && RunStatsManager.Instance.isRunActive;
        bool isRunActive = statsTrackingActive ||
                           (!isBaseScene && RunManager.instance != null && RunManager.instance.currentRoomNumber >= 1);

        if ((speedrunOption || isEndless) && isGameplayScene && !isBaseScene && LevelGenerator.Instance != null &&
            RunStatsManager.Instance != null && !RunStatsManager.Instance.isRunActive)
        {
            RunStatsManager.Instance.StartRunTracking(isEndless);
            isRunActive = true;
        }

        bool showEndlessHud = isEndless && isRunActive && isGameplayScene && !isBaseScene;
        bool shouldShow = showEndlessHud || (speedrunOption && isGameplayScene);

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
            if (showEndlessHud)
            {
                RunStatsManager stats = RunStatsManager.Instance;
                RunManager rm = RunManager.instance;

                int currentLvl = rm != null ? rm.currentLevel : 1;
                int endlessFloor = rm != null ? Mathf.Max(1, rm.endlessFloorCount) : 1;
                string timeStr = stats.FormatTime(stats.survivalTimer);

                bool beatingRecord = stats.IsBeatingEndlessRecordLive(currentLvl);
                int displayBestLvl = beatingRecord ? stats.GetLiveBestEndlessLevel(currentLvl) : stats.BestEndlessLevel;
                float displayBestTime = beatingRecord ? stats.GetLiveBestEndlessTime() : stats.BestEndlessTime;
                string displayBestTimeStr = stats.FormatTime(displayBestTime);

                string recordLine;
                if (beatingRecord)
                {
                    recordLine = $"<size=15><color=#5CE68A><b>* NOVO RECORDE!</b></color> <color=#D8FFE6>NV {displayBestLvl} | {displayBestTimeStr}</color></size>";
                }
                else if (displayBestLvl > 0 || displayBestTime > 0f)
                {
                    recordLine = $"<size=15><color=#9EC5FE>RECORDE: NV {displayBestLvl} | {displayBestTimeStr}</color></size>";
                }
                else
                {
                    recordLine = $"<size=15><color=#9EC5FE>RECORDE: NV {currentLvl} | {timeStr}</color></size>";
                }

                List<AbyssMutationDefinition> activeMutations = rm != null
                    ? rm.GetActiveMutationDefinitions()
                    : null;

                string mutationBlock = "";
                int mutationCount = activeMutations != null ? activeMutations.Count : 0;
                for (int i = 0; i < mutationCount; i++)
                {
                    AbyssMutationDefinition def = activeMutations[i];
                    mutationBlock += $"\n<size=14><color={def.accentHexColor}><b>» {def.displayName}:</b></color> <color=#E8E8EE>{def.shortDescription}</color></size>";
                }

                UpdateDefaultPanelSize(isEndlessLayout: true, mutationCount: mutationCount);

                timerText.text =
                    $"<color=#ffaa00><b>ENDLESS</b></color>  <color=#ffffff><b>{timeStr}</b></color>  <size=15><color=#D0D7E2>NV {currentLvl} • ANDAR {endlessFloor}</color></size>\n" +
                    recordLine +
                    mutationBlock;
            }
            else
            {
                UpdateDefaultPanelSize(isEndlessLayout: false, mutationCount: 0);

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

    private void UpdateDefaultPanelSize(bool isEndlessLayout, int mutationCount)
    {
        if (!_createdDefaultPanel || _panelRectTransform == null) return;

        Vector2 targetSize;
        if (!isEndlessLayout)
        {
            targetSize = new Vector2(250f, 45f);
        }
        else if (mutationCount >= 2)
        {
            targetSize = new Vector2(430f, 126f);
        }
        else if (mutationCount == 1)
        {
            targetSize = new Vector2(430f, 102f);
        }
        else
        {
            targetSize = new Vector2(360f, 74f);
        }

        if (_panelRectTransform.sizeDelta != targetSize)
        {
            _panelRectTransform.sizeDelta = targetSize;
        }

        if (timerText != null)
        {
            timerText.alignment = isEndlessLayout ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
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
                bg.color = new Color(0.04f, 0.04f, 0.08f, 0.78f);
                bg.raycastTarget = false;

                Shadow shadow = panelGo.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(3f, -3f);

                GameObject textGo = new GameObject("txtTimer");
                textGo.transform.SetParent(panelGo.transform, false);
                RectTransform textRt = textGo.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(12f, 6f);
                textRt.offsetMax = new Vector2(-12f, -6f);

                timerText = textGo.AddComponent<TextMeshProUGUI>();
                timerText.fontSize = 21f;
                timerText.fontStyle = FontStyles.Bold;
                timerText.alignment = TextAlignmentOptions.TopLeft;
                timerText.textWrappingMode = TextWrappingModes.Normal;
                timerText.color = Color.white;
                timerText.raycastTarget = false;

                timerPanel = panelGo;
                _panelRectTransform = rt;
                _createdDefaultPanel = true;
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
            float targetY = (EndlessTimerHUD.Instance != null && EndlessTimerHUD.Instance.IsTimerVisible)
                ? EndlessTimerHUD.Instance.GetFpsAnchorOffsetY()
                : -25f;
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
