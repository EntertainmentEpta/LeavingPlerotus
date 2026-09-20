using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Menu Principal do jogo SENTIENCE.
/// Cria toda a UI por código (sem prefabs).
/// 
/// SETUP:
///   1. Crie uma cena chamada "MainMenu"
///   2. Crie um GameObject vazio e adicione este script
///   3. Adicione a cena ao Build Settings como índice 0
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Navegação")]
    public string playSceneName = "Base";

    [Header("Créditos")]
    public string studioName = "EPTA Entertainment";
    public string[] teamMembers = { "Equipe EPTA" };
    public string gameVersion = "v0.1 Alpha";

    private GameObject mainPanel;
    private GameObject optionsPanel;
    private GameObject creditsPanel;

    // Paleta
    static readonly Color C_BG       = new Color(0.02f, 0.02f, 0.06f, 0.97f);
    static readonly Color C_PANEL    = new Color(0.05f, 0.04f, 0.12f, 0.95f);
    static readonly Color C_ACCENT   = new Color(0.45f, 0.25f, 0.90f, 1f);
    static readonly Color C_CYAN     = new Color(0.20f, 0.75f, 0.90f, 1f);
    static readonly Color C_BTN      = new Color(0.12f, 0.10f, 0.22f, 1f);
    static readonly Color C_BTN_HOV  = new Color(0.22f, 0.17f, 0.38f, 1f);
    static readonly Color C_BTN_PRS  = new Color(0.35f, 0.20f, 0.55f, 1f);
    static readonly Color C_TEXT     = new Color(0.90f, 0.88f, 0.95f, 1f);
    static readonly Color C_DIM      = new Color(0.50f, 0.48f, 0.60f, 1f);
    static readonly Color C_SEC      = new Color(0.08f, 0.06f, 0.16f, 1f);

    const float PW = 540f;

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        BuildAll();
        ShowMain();
    }

    void BuildAll()
    {
        // Canvas
        GameObject cObj = new GameObject("MainMenuCanvas");
        Canvas cv = cObj.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 1000;
        var sc = cObj.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        cObj.AddComponent<GraphicRaycaster>();

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Fullscreen BG
        var bg = Img(cObj.transform, "BG", C_BG);
        Stretch(bg);

        mainPanel    = BuildMainPanel(cObj.transform);
        optionsPanel = BuildOptionsPanel(cObj.transform);
        creditsPanel = BuildCreditsPanel(cObj.transform);
    }

    // ═══════════════════════════════════════
    //  MAIN PANEL
    // ═══════════════════════════════════════
    GameObject BuildMainPanel(Transform parent)
    {
        var p = Panel(parent, "MainPanel");
        float y = -36f;

        // Título
        var title = Txt(p.transform, "SENTIENCE", 52, C_ACCENT);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        Place(title.gameObject, 30f, y, PW - 60f, 66f); y -= 74f;

        var sub = Txt(p.transform, "- EPTA Entertainment -", 15, C_DIM);
        sub.alignment = TextAlignmentOptions.Center;
        Place(sub.gameObject, 30f, y, PW - 60f, 24f); y -= 40f;

        // Separador
        Sep(p.transform, y); y -= 24f;

        // Botões
        Btn(p.transform, "JOGAR", y, OnPlay); y -= 76f;
        Btn(p.transform, "OPCOES", y, OnOptions); y -= 76f;
        Btn(p.transform, "CREDITOS", y, OnCredits); y -= 76f;
        Btn(p.transform, "SAIR", y, OnQuit); y -= 76f;

        var ver = Txt(p.transform, gameVersion, 12, C_DIM);
        ver.alignment = TextAlignmentOptions.Center;
        Place(ver.gameObject, 30f, y, PW - 60f, 18f); y -= 36f;

        p.GetComponent<RectTransform>().sizeDelta = new Vector2(PW, -y);
        return p;
    }

    // ═══════════════════════════════════════
    //  OPTIONS PANEL
    // ═══════════════════════════════════════
    GameObject BuildOptionsPanel(Transform parent)
    {
        var p = Panel(parent, "OptionsPanel");
        float y = -36f;

        var hdr = Txt(p.transform, "OPCOES", 30, C_ACCENT);
        hdr.fontStyle = FontStyles.Bold;
        hdr.alignment = TextAlignmentOptions.Center;
        Place(hdr.gameObject, 30f, y, PW - 60f, 42f); y -= 56f;

        Sep(p.transform, y); y -= 20f;

        // Áudio
        y = Section(p.transform, "AUDIO", y);
        y = SliderRow(p.transform, "Volume Geral", y, 0.7f);
        y = SliderRow(p.transform, "Volume Efeitos", y, 0.8f);
        y = SliderRow(p.transform, "Volume Música", y, 0.6f);
        y -= 12f;

        // Vídeo
        y = Section(p.transform, "VIDEO", y);
        y = ToggleRow(p.transform, "Tela Cheia", y, Screen.fullScreen);
        y = CycleRow(p.transform, "Qualidade", y, new[]{"Baixa","Média","Alta","Ultra"}, 2);
        y -= 12f;

        // Gameplay
        y = Section(p.transform, "GAMEPLAY", y);
        y = NavRow(p.transform, "Controles", "->", y, () => {
            var ctrl = Object.FindFirstObjectByType<ControlsReferenceMenu>(FindObjectsInactive.Include);
            if (ctrl != null)
            {
                // Simulate pressing the toggle key
                ctrl.gameObject.SetActive(true);
                ctrl.SendMessage("ToggleMenu", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.LogWarning("ControlsReferenceMenu not found in scene.");
            }
        });

        y = NavRow(p.transform, "Resetar Progresso", "RESET", y, () => {
            SaveManager.ResetProfile();
            Debug.Log("[DEBUG] Progresso do jogo resetado via menu principal.");
        });
        y -= 28f;

        Btn(p.transform, "VOLTAR", y, ShowMain); y -= 76f;

        p.GetComponent<RectTransform>().sizeDelta = new Vector2(PW, -y);
        return p;
    }

    // ═══════════════════════════════════════
    //  CREDITS PANEL
    // ═══════════════════════════════════════
    GameObject BuildCreditsPanel(Transform parent)
    {
        var p = Panel(parent, "CreditsPanel");
        float y = -24f;

        var hdr = Txt(p.transform, "CREDITOS", 28, C_ACCENT);
        hdr.fontStyle = FontStyles.Bold;
        hdr.alignment = TextAlignmentOptions.Center;
        Place(hdr.gameObject, 24f, y, PW - 48f, 38f); y -= 48f;

        Sep(p.transform, y); y -= 20f;

        var devBy = Txt(p.transform, "Desenvolvido por", 14, C_DIM);
        devBy.alignment = TextAlignmentOptions.Center;
        Place(devBy.gameObject, 24f, y, PW - 48f, 20f); y -= 26f;

        var sn = Txt(p.transform, studioName, 32, C_CYAN);
        sn.fontStyle = FontStyles.Bold;
        sn.alignment = TextAlignmentOptions.Center;
        Place(sn.gameObject, 24f, y, PW - 48f, 42f); y -= 52f;

        Sep(p.transform, y); y -= 16f;

        var tl = Txt(p.transform, "EQUIPE", 13, C_DIM);
        tl.alignment = TextAlignmentOptions.Center;
        Place(tl.gameObject, 24f, y, PW - 48f, 18f); y -= 28f;

        if (teamMembers != null)
        {
            foreach (string m in teamMembers)
            {
                var mt = Txt(p.transform, m, 18, C_TEXT);
                mt.alignment = TextAlignmentOptions.Center;
                Place(mt.gameObject, 24f, y, PW - 48f, 24f); y -= 30f;
            }
        }

        y -= 6f;
        var v = Txt(p.transform, gameVersion, 12, C_DIM);
        v.alignment = TextAlignmentOptions.Center;
        Place(v.gameObject, 24f, y, PW - 48f, 18f); y -= 30f;

        Btn(p.transform, "VOLTAR", y, ShowMain); y -= 66f;

        p.GetComponent<RectTransform>().sizeDelta = new Vector2(PW, -y);
        return p;
    }

    // ═══════════════════════════════════════
    //  ACTIONS
    // ═══════════════════════════════════════
    void OnPlay()
    {
        PlayerHealth.shouldPlayWakeUpAnimation = true;
        SceneManager.LoadScene(playSceneName);
    }
    void OnOptions() { ShowOptions(); }
    void OnCredits() { ShowCredits(); }
    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowMain()    { mainPanel.SetActive(true);  optionsPanel.SetActive(false); creditsPanel.SetActive(false); }
    void ShowOptions() { mainPanel.SetActive(false); optionsPanel.SetActive(true);  creditsPanel.SetActive(false); }
    void ShowCredits() { mainPanel.SetActive(false); optionsPanel.SetActive(false); creditsPanel.SetActive(true);  }

    // ═══════════════════════════════════════
    //  ROW BUILDERS
    // ═══════════════════════════════════════

    float Section(Transform parent, string label, float y)
    {
        y -= 8f;
        var bg = Img(parent, "Sec", C_SEC);
        Place(bg, 24f, y, PW - 48f, 30f);
        var t = Txt(bg.transform, label, 13, C_ACCENT);
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.margin = new Vector4(10, 0, 0, 0);
        Stretch(t.gameObject);
        return y - 42f;
    }

    float SliderRow(Transform parent, string label, float y, float val)
    {
        float lw = 190f;
        float sw = PW - 100f - lw - 60f;

        var lbl = Txt(parent, label, 15, C_TEXT);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        Place(lbl.gameObject, 36f, y, lw, 44f);

        // --- Slider (construção correta para Unity) ---
        var sliderGO = new GameObject("Slider_" + label);
        sliderGO.AddComponent<RectTransform>();
        sliderGO.transform.SetParent(parent, false);
        Place(sliderGO, 36f + lw, y - 10f, sw, 24f);

        // Background
        var bgGO = Img(sliderGO.transform, "Background", new Color(0.10f, 0.08f, 0.18f));
        Stretch(bgGO);

        // Fill Area — container que o Slider redimensiona
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRT.offsetMin = new Vector2(5f, 0f);
        fillAreaRT.offsetMax = new Vector2(-5f, 0f);

        // Fill image
        var fillGO = Img(fillAreaGO.transform, "Fill", new Color(0.50f, 0.30f, 0.85f));
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

        // Handle Slide Area
        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = new Vector2(0f, 0f);
        handleAreaRT.anchorMax = new Vector2(1f, 1f);
        handleAreaRT.offsetMin = new Vector2(5f, 0f);
        handleAreaRT.offsetMax = new Vector2(-5f, 0f);

        // Handle
        var handleGO = Img(handleAreaGO.transform, "Handle", Color.white);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0f, 0f);
        handleRT.anchorMax = new Vector2(0f, 1f);
        handleRT.sizeDelta = new Vector2(10f, 0f);
        handleRT.anchoredPosition = Vector2.zero;

        // Componente Slider
        Slider slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleGO.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = val;

        // Texto do valor
        var valTxt = Txt(parent, Mathf.RoundToInt(val * 100) + "%", 14, C_DIM);
        valTxt.alignment = TextAlignmentOptions.MidlineRight;
        Place(valTxt.gameObject, PW - 80f, y, 50f, 44f);

        var valRef = valTxt;
        slider.onValueChanged.AddListener((v) => { valRef.text = Mathf.RoundToInt(v * 100) + "%"; });

        return y - 52f;
    }

    float ToggleRow(Transform parent, string label, float y, bool on)
    {
        var lbl = Txt(parent, label, 15, C_TEXT);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        Place(lbl.gameObject, 36f, y, 200f, 42f);

        var tObj = Img(parent, "Tog", C_BTN);
        Place(tObj, PW - 170f, y + 6f, 120f, 30f);
        tObj.GetComponent<Image>().raycastTarget = true;

        var tTxt = Txt(tObj.transform, on ? "LIGADO" : "DESLIGADO", 14, on ? C_ACCENT : C_DIM);
        tTxt.fontStyle = FontStyles.Bold;
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.raycastTarget = false;
        Stretch(tTxt.gameObject);

        bool state = on;
        var btn = tObj.AddComponent<Button>();
        var txtRef = tTxt;
        btn.onClick.AddListener(() => {
            state = !state;
            txtRef.text = state ? "LIGADO" : "DESLIGADO";
            txtRef.color = state ? C_ACCENT : C_DIM;
            Screen.fullScreen = state;
        });

        return y - 50f;
    }

    float CycleRow(Transform parent, string label, float y, string[] opts, int idx)
    {
        var lbl = Txt(parent, label, 15, C_TEXT);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        Place(lbl.gameObject, 36f, y, 200f, 42f);

        var dObj = Img(parent, "Cyc", C_BTN);
        Place(dObj, PW - 200f, y + 6f, 150f, 30f);
        dObj.GetComponent<Image>().raycastTarget = true;

        int cur = idx;
        var dTxt = Txt(dObj.transform, "< " + opts[cur] + " >", 14, C_TEXT);
        dTxt.alignment = TextAlignmentOptions.Center;
        dTxt.raycastTarget = false;
        Stretch(dTxt.gameObject);

        var btn = dObj.AddComponent<Button>();
        var dRef = dTxt;
        var oRef = opts;
        btn.onClick.AddListener(() => {
            cur = (cur + 1) % oRef.Length;
            dRef.text = "< " + oRef[cur] + " >";
        });

        return y - 50f;
    }

    float NavRow(Transform parent, string label, string arrow, float y, UnityEngine.Events.UnityAction act)
    {
        var lbl = Txt(parent, label, 15, C_TEXT);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        Place(lbl.gameObject, 36f, y, 200f, 42f);

        var navObj = Img(parent, "Nav", C_BTN);
        Place(navObj, PW - 170f, y - 6f, 120f, 30f);
        navObj.GetComponent<Image>().raycastTarget = true;

        var navTxt = Txt(navObj.transform, arrow, 16, C_ACCENT);
        navTxt.fontStyle = FontStyles.Bold;
        navTxt.alignment = TextAlignmentOptions.Center;
        navTxt.raycastTarget = false;
        Stretch(navTxt.gameObject);

        var btn = navObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = C_BTN; cb.highlightedColor = C_BTN_HOV;
        cb.pressedColor = C_BTN_PRS; cb.selectedColor = C_BTN_HOV;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(act);

        return y - 50f;
    }

    // ═══════════════════════════════════════
    //  PRIMITIVAS
    // ═══════════════════════════════════════

    GameObject Panel(Transform parent, string name)
    {
        var go = Img(parent, name, C_PANEL);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(PW, 500f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    void Btn(Transform parent, string label, float y, UnityEngine.Events.UnityAction act)
    {
        float bw = PW - 100f;
        var go = Img(parent, "Btn", C_BTN);
        Place(go, 50f, y, bw, 58f);
        go.GetComponent<Image>().raycastTarget = true;

        var b = go.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = C_BTN; cb.highlightedColor = C_BTN_HOV;
        cb.pressedColor = C_BTN_PRS; cb.selectedColor = C_BTN_HOV;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.onClick.AddListener(act);

        var t = Txt(go.transform, label, 22, C_TEXT);
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        Stretch(t.gameObject);
    }

    void Sep(Transform parent, float y)
    {
        var s = Img(parent, "Sep", C_ACCENT * 0.25f);
        Place(s, 24f, y, PW - 48f, 1f);
        s.GetComponent<Image>().raycastTarget = false;
    }

    GameObject Img(Transform p, string n, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        return go;
    }

    TextMeshProUGUI Txt(Transform p, string text, int size, Color c)
    {
        var go = new GameObject("T");
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c;
        t.richText = true; t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    void Place(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
