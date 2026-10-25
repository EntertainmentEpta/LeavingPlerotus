using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD de Economia — painel no canto superior direito da tela.
/// Pressione TAB para mostrar/esconder.
/// Adicione este script ao Player ou GameManager.
/// </summary>
public class EconomyHUD : MonoBehaviour
{
    [Header("Controles")]
    public KeyCode toggleKey   = KeyCode.F3;
    public float updateInterval = 0.5f;

    [Header("Layout")]
    public float panelWidth  = 420f;
    public float marginRight = 16f;
    public float marginTop   = 16f;
    [Tooltip("Tamanho base da fonte. Todos os textos escalam a partir deste valor.")]
    public int   baseFontSize = 16;

    // Referências (auto-encontradas)
    private RunManager       runManager;
    private PlayerEssence    playerEssence;
    private InfusionManager  infusionManager;

    // UI
    private GameObject        rootCanvas;
    private GameObject        panel;
    private TextMeshProUGUI   txtRoom;
    private Image             roomBar;
    private TextMeshProUGUI   txtEssence;
    private TextMeshProUGUI   txtCombat;
    private TextMeshProUGUI   txtInflation;
    private Image[]           chartBars;

    private const int CHART_SIZE  = 10;
    private const int TOTAL_ROOMS = 32;
    private bool  isVisible    = true;
    private float timer        = 0f;
    private bool  needsRebuild = false; // setado pelo OnValidate

    // Paleta
    static readonly Color C_BG      = new Color(0.04f, 0.04f, 0.09f, 0.93f);
    static readonly Color C_SECTION = new Color(0.13f, 0.10f, 0.22f, 1.00f);
    static readonly Color C_ACCENT  = new Color(0.65f, 0.40f, 1.00f, 1.00f);
    static readonly Color C_GOLD    = new Color(1.00f, 0.82f, 0.20f, 1.00f);
    static readonly Color C_BAR_CUR = new Color(0.65f, 0.30f, 1.00f, 1.00f);
    static readonly Color C_BAR_FUT = new Color(0.30f, 0.55f, 0.95f, 0.80f);
    static readonly Color C_BAR_PST = new Color(0.22f, 0.22f, 0.32f, 0.60f);

    // ─────────────────────────────────────────
    void Start()
    {
        toggleKey = KeyCode.F3; // Força F3 ignorando o que estava salvo no Unity Inspector
        FindRefs();
        BuildUI();
        Refresh();
    }

    void Update()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu" || currentScene == "Menu")
        {
            if (rootCanvas != null && rootCanvas.activeSelf) rootCanvas.SetActive(false);
            return;
        }
        else
        {
            if (rootCanvas != null && !rootCanvas.activeSelf && isVisible) rootCanvas.SetActive(true);
        }

        // Reconstrói se panelWidth/margins foram alterados no Inspector
        if (needsRebuild && Application.isPlaying)
        {
            needsRebuild = false;
            RebuildHUD();
        }

        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            if (panel != null) panel.SetActive(isVisible);
        }

        if (!isVisible) return;

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            FindRefs();
            Refresh();
        }
    }

    // Chamado pelo Unity quando um campo público muda no Inspector
    void OnValidate()
    {
        needsRebuild = true;
    }

    void RebuildHUD()
    {
        if (rootCanvas != null)
        {
            Destroy(rootCanvas);
            rootCanvas = null;
            panel = null;
        }
        FindRefs();
        BuildUI();
        Refresh();
    }

    void FindRefs()
    {
        if (runManager     == null) runManager     = RunManager.instance;
        if (playerEssence  == null) playerEssence  = Object.FindFirstObjectByType<PlayerEssence>();
        if (infusionManager == null) infusionManager = Object.FindFirstObjectByType<InfusionManager>();
    }

    // ─────────────────────────────────────────
    // REFRESH
    // ─────────────────────────────────────────
    void Refresh()
    {
        int   n       = runManager      != null ? runManager.currentRoomNumber        : 1;
        int   essence = playerEssence   != null ? playerEssence.currentEssence        : 0;
        int   total   = playerEssence   != null ? playerEssence.totalEssenceCollected : 0;
        float ptotal  = infusionManager != null ? infusionManager.GetTotalWeight()    : 0f;
        float alpha   = infusionManager != null ? infusionManager.inflationAlpha      : 0.1f;

        float dropMult  = 1f + 0.05f * n;
        int   budget    = Mathf.RoundToInt(10 + 0.9f * n);
        float inflMult  = 1f + alpha * ptotal;
        int   biome     = Mathf.CeilToInt((float)n / 8f);

        // ── Sala ──
        if (runManager != null && runManager.isEndlessMode)
        {
            txtRoom.text =
                $"<color=#FF4444>[MODO ENDLESS - Nível {runManager.currentLevel}]</color>\n" +
                $"<color=#BBAAFF>Sala</color> <b>{n}</b><color=#666688> / ♾️</color>" +
                $"     <color=#BBAAFF>Bioma</color> <b>{biome}</b>";
            roomBar.fillAmount = 1f;
        }
        else
        {
            txtRoom.text =
                $"<color=#BBAAFF>Sala</color> <b>{n}</b><color=#666688> / {TOTAL_ROOMS}</color>" +
                $"     <color=#BBAAFF>Bioma</color> <b>{biome}</b>";
            roomBar.fillAmount = Mathf.Clamp01((float)n / TOTAL_ROOMS);
        }

        // ── Essência ──
        string mc = dropMult >= 1.5f ? "#FF8844" : dropMult >= 1.2f ? "#FFCC44" : "#88FF99";
        txtEssence.text =
            $"<color=#FFD700>Atual:</color>  <b>{essence} ✨</b>\n" +
            $"<color=#FFD700>Total coletado:</color>  {total} ✨\n" +
            $"<color=#FFD700>Mult. de drop:</color>  <color={mc}><b>×{dropMult:F2}</b></color>  " +
            $"<color=#444466><size=80%>E({n}) = d×{dropMult:F2}</size></color>";

        // ── Combate ──
        var (e, t, a, m) = Estimate(budget);
        string comp = "";
        if (e > 0) comp += $"<color=#FFD700><b>{e}×Elite</b></color>   ";
        if (t > 0) comp += $"<color=#FF6666><b>{t}×Tanque</b></color>   ";
        if (a > 0) comp += $"<color=#66AAFF><b>{a}×Atirador</b></color>   ";
        if (m > 0) comp += $"<color=#AAAAAA><b>{m}×Mob</b></color>";
        if (comp == "") comp = "—";

        txtCombat.text =
            $"<color=#FF8888>Budget P({n}):</color>  <b>{budget} pts</b>\n" +
            $"<color=#FF8888>Composição típica:</color>\n" +
            $"  {comp}";

        // ── Inflação ──
        string pc = ptotal > 10f ? "#FF6666" : ptotal > 5f ? "#FFAA33" : "#77FF99";
        int c1 = Mathf.RoundToInt(60  * inflMult);
        int c2 = Mathf.RoundToInt(180 * inflMult);
        int c3 = Mathf.RoundToInt(300 * inflMult);
        int c4 = Mathf.RoundToInt(420 * inflMult);

        txtInflation.text =
            $"<color=#CC88FF>Ptotal:</color>  <color={pc}><b>{ptotal:F2}</b></color>   " +
            $"<color=#CC88FF>Multiplicador:</color>  <b>×{inflMult:F2}</b>\n" +
            $"<color=#888888>Custo real da próxima infusão:</color>\n" +
            $"<color=#CCCCCC>T1</color> <b>{c1}✨</b>   <color=#44FF88>T2</color> <b>{c2}✨</b>   " +
            $"<color=#4488FF>T3</color> <b>{c3}✨</b>   <color=#FFD700>T4</color> <b>{c4}✨</b>";

        RefreshChart(n);
    }

    void RefreshChart(int current)
    {
        float maxBgt = 10 + 0.9f * TOTAL_ROOMS;
        for (int i = 0; i < CHART_SIZE; i++)
        {
            int roomN = current - 2 + i; // 0,1=passado  2=atual  3-9=futuro
            float bgt = roomN >= 1 ? (10 + 0.9f * roomN) : 0f;
            float frac = Mathf.Clamp01(bgt / maxBgt);

            var rt = chartBars[i].rectTransform;
            rt.anchorMin = new Vector2((float)i / CHART_SIZE, 0f);
            rt.anchorMax = new Vector2((float)(i + 1) / CHART_SIZE, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(1.5f, 14f);
            rt.offsetMax = new Vector2(-1.5f, 14f + 52f * frac);

            chartBars[i].color = i == 2 ? C_BAR_CUR : i < 2 ? C_BAR_PST : C_BAR_FUT;
        }
    }

    // ─────────────────────────────────────────
    // ESTIMATE COMPOSITION (determinística)
    // ─────────────────────────────────────────
    (int e, int t, int a, int m) Estimate(int budget)
    {
        int maxMob = Mathf.FloorToInt(budget * 0.5f);
        int r = budget, e = 0, t = 0, a = 0, m = 0, mp = 0;
        if (r >= 10) { e = 1; r -= 10; }
        while (r >= 4 && t < 4)      { t++; r -= 4; }
        while (r >= 4)                { a += 2; r -= 4; }
        while (r >= 1 && mp < maxMob) { m++; mp++; r--; }
        return (e, t, a, m);
    }

    // ─────────────────────────────────────────
    // BUILD UI  (âncora: canto SUPERIOR DIREITO)
    // ─────────────────────────────────────────
    void BuildUI()
    {
        // Derivados do baseFontSize
        float lh  = baseFontSize * 1.55f;  // altura de uma linha de texto
        int   fHd = baseFontSize + 4;      // fonte do header
        int   fSc = Mathf.Max(9, baseFontSize - 5);  // fonte das seções
        int   fBd = baseFontSize;          // fonte do corpo
        float secH = Mathf.Max(18f, lh);  // altura da barra de seção

        // Canvas — ConstantPixelSize para coordenadas 1:1 com pixels de tela
        rootCanvas = new GameObject("EconomyHUD_Canvas");
        Canvas cv = rootCanvas.AddComponent<Canvas>();
        cv.renderMode  = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 500;
        rootCanvas.AddComponent<CanvasScaler>(); // ConstantPixelSize por padrão
        rootCanvas.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(rootCanvas);

        // Painel — top-right, cresce para baixo
        panel = Img(rootCanvas.transform, "Panel", C_BG);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(1f, 1f); // top-right
        pr.pivot     = new Vector2(1f, 1f);
        pr.anchoredPosition = new Vector2(-marginRight, -marginTop);
        pr.sizeDelta = new Vector2(panelWidth, 10f); // altura definida ao final

        float y  = -10f;
        float px = 12f;
        float W  = panelWidth - px * 2f;

        // ══ HEADER ══════════════════════════════
        {
            var h = Img(panel.transform, "Hdr", new Color(0.10f, 0.06f, 0.22f));
            float hdrH = fHd * 2.2f;
            SetRect(h, px, y, W, hdrH); y -= hdrH + 4f;
            var t = TMP(h.transform, "📊  ECONOMIA DA RUN", fHd, C_ACCENT);
            Fill(t.gameObject); t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold;
        }

        // ══ SALA & PROGRESSO ════════════════════
        Section(panel.transform, "PROGRESSO DA RUN", ref y, px, W, fSc, secH);

        txtRoom = TMP(panel.transform, "", fBd, Color.white);
        SetRect(txtRoom.gameObject, px, y, W, lh + 4f); y -= lh + 8f;

        {
            var bg = Img(panel.transform, "BarBG", new Color(0.18f, 0.15f, 0.28f));
            SetRect(bg, px, y, W, 8f);
            var fill = Img(bg.transform, "Fill", C_ACCENT);
            Fill(fill);
            roomBar = fill.GetComponent<Image>();
            roomBar.type = Image.Type.Filled;
            roomBar.fillMethod = Image.FillMethod.Horizontal;
            y -= 14f;
        }

        // ══ ESSÊNCIA ════════════════════════════
        Section(panel.transform, "ESSÊNCIA", ref y, px, W, fSc, secH);
        txtEssence = TMP(panel.transform, "", fBd, Color.white);
        float essH = lh * 3f + 8f;
        SetRect(txtEssence.gameObject, px, y, W, essH); y -= essH + 4f;

        // ══ COMBATE ═════════════════════════════
        Section(panel.transform, "COMBATE ESTIMADO", ref y, px, W, fSc, secH);
        txtCombat = TMP(panel.transform, "", fBd, Color.white);
        float comH = lh * 3f + 8f;
        SetRect(txtCombat.gameObject, px, y, W, comH); y -= comH + 4f;

        // ══ INFLAÇÃO ════════════════════════════
        Section(panel.transform, "INFLAÇÃO DE INFUSÃO", ref y, px, W, fSc, secH);
        txtInflation = TMP(panel.transform, "", fBd, Color.white);
        float infH = lh * 4f + 8f;
        SetRect(txtInflation.gameObject, px, y, W, infH); y -= infH + 4f;

        // ══ GRÁFICO P(n) ════════════════════════
        Section(panel.transform, $"BUDGET P(n)  —  PRÓXIMAS {CHART_SIZE - 2} SALAS", ref y, px, W, fSc, secH);
        {
            float chartH = 70f;
            var cbg = Img(panel.transform, "ChartBG", new Color(0.06f, 0.05f, 0.13f));
            SetRect(cbg, px, y, W, chartH); y -= chartH + 4f;

            chartBars = new Image[CHART_SIZE];
            for (int i = 0; i < CHART_SIZE; i++)
            {
                var b = Img(cbg.transform, $"B{i}", C_BAR_FUT);
                chartBars[i] = b.GetComponent<Image>();
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2((float)i / CHART_SIZE, 0f);
                rt.anchorMax = new Vector2((float)(i + 1) / CHART_SIZE, 0f);
                rt.pivot     = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(1.5f, 14f);
                rt.offsetMax = new Vector2(-1.5f, 40f);
            }

            // Legenda
            var leg = TMP(cbg.transform, "◀ passado  |  <color=#9966FF>▮ atual</color>  |  ▶ futuro", 9,
                          new Color(0.55f, 0.55f, 0.65f));
            var lr = leg.gameObject.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot     = new Vector2(0.5f, 0f);
            lr.offsetMin = new Vector2(0f, 1f);
            lr.offsetMax = new Vector2(0f, 14f);
            leg.alignment = TextAlignmentOptions.Center;
        }

        // Fecha o painel com a altura total calculada
        y -= 8f; // padding final
        pr.sizeDelta = new Vector2(panelWidth, -y);
    }

    void Section(Transform parent, string label, ref float y, float px, float W, int fontSize, float height)
    {
        y -= 6f;
        var bg = Img(parent, "Sec", C_SECTION);
        SetRect(bg, px, y, W, height);
        var t = TMP(bg.transform, $"▸ {label}", fontSize, new Color(0.70f, 0.65f, 0.85f));
        Fill(t.gameObject);
        t.margin    = new Vector4(6, 0, 0, 0);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        y -= height + 4f;
    }

    // ─────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────
    GameObject Img(Transform p, string n, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        return go;
    }

    void SetRect(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    void Fill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI TMP(Transform p, string text, int size, Color color)
    {
        var go = new GameObject("T");
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text              = text;
        t.fontSize          = size;
        t.color             = color;
        t.richText          = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode      = TextOverflowModes.Overflow;
        return t;
    }
}
