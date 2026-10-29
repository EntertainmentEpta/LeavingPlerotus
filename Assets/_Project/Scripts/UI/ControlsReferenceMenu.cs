using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu de Referência de Controles — overlay in-game.
/// Pressione F2 para abrir/fechar.
/// Lista todos os comandos do jogo organizados por categoria.
///
/// SETUP:
///   1. Adicione este script ao GameObject do Player (ou qualquer persistente)
///   2. O menu é criado automaticamente por código
///   3. Persiste entre cenas (DontDestroyOnLoad no Canvas)
///
/// EXTENSÃO:
///   Para adicionar novos controles, edite o método PopulateControls().
///   Basta adicionar novas chamadas a AddKeybind() dentro da categoria desejada.
/// </summary>
public class ControlsReferenceMenu : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Tecla para abrir/fechar o menu de controles")]
    public KeyCode toggleKey = KeyCode.F2;

    // ── UI ──
    private GameObject rootCanvas;
    private GameObject panel;
    private bool isVisible = false;

    // ── Paleta ──
    static readonly Color C_BG        = new Color(0.03f, 0.03f, 0.08f, 0.94f);
    static readonly Color C_SECTION   = new Color(0.10f, 0.08f, 0.20f, 1.00f);
    static readonly Color C_KEY_BG    = new Color(0.15f, 0.12f, 0.28f, 1.00f);
    static readonly Color C_KEY_BRD   = new Color(0.45f, 0.30f, 0.80f, 0.50f);
    static readonly Color C_ACCENT    = new Color(0.55f, 0.35f, 0.95f, 1.00f);
    static readonly Color C_TEXT      = new Color(0.88f, 0.85f, 0.95f, 1.00f);
    static readonly Color C_TEXT_DIM  = new Color(0.50f, 0.48f, 0.60f, 1.00f);
    static readonly Color C_CYAN      = new Color(0.25f, 0.80f, 0.90f, 1.00f);
    static readonly Color C_GOLD      = new Color(1.00f, 0.82f, 0.20f, 1.00f);
    static readonly Color C_RED       = new Color(0.95f, 0.40f, 0.40f, 1.00f);

    // ── Layout ──
    const float PANEL_W   = 440f;
    const float ROW_H     = 28f;
    const float ROW_GAP   = 3f;
    const float SEC_H     = 26f;
    const float PAD       = 16f;
    const float KEY_W     = 90f;

    void Start()
    {
        BuildUI();
        panel.SetActive(false);
        isVisible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenu();
        }
    }

    /// <summary>
    /// Abre/fecha o menu de controles. Pode ser chamado externamente.
    /// </summary>
    public void ToggleMenu()
    {
        isVisible = !isVisible;
        panel.SetActive(isVisible);
    }

    // ═══════════════════════════════════════════
    //  BUILD
    // ═══════════════════════════════════════════

    void BuildUI()
    {
        // Canvas dedicado
        rootCanvas = new GameObject("ControlsMenuCanvas");
        Canvas cv = rootCanvas.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 600;
        CanvasScaler sc = rootCanvas.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        rootCanvas.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(rootCanvas);

        // Painel — centro da tela
        panel = MakeImg(rootCanvas.transform, "ControlsPanel", C_BG);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(PANEL_W, 100f); // altura ajustada depois

        // Borda
        GameObject border = MakeImg(panel.transform, "Border", C_ACCENT * 0.35f);
        RectTransform brt = border.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-2, -2); brt.offsetMax = new Vector2(2, 2);
        border.transform.SetAsFirstSibling();
        border.GetComponent<Image>().raycastTarget = false;

        // Top accent
        GameObject accent = MakeImg(panel.transform, "TopLine", C_ACCENT);
        RectTransform art = accent.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0.05f, 1f); art.anchorMax = new Vector2(0.95f, 1f);
        art.pivot = new Vector2(0.5f, 1f);
        art.anchoredPosition = Vector2.zero; art.sizeDelta = new Vector2(0, 3f);
        accent.GetComponent<Image>().raycastTarget = false;

        float y = -PAD;

        // Header
        var hdr = MakeTMP(panel.transform, "🎮  CONTROLES  —  SENTIENCE", 20, C_ACCENT);
        hdr.fontStyle = FontStyles.Bold;
        hdr.alignment = TextAlignmentOptions.Center;
        SetRect(hdr.gameObject, PAD, y, PANEL_W - PAD * 2, 30f);
        y -= 36f;

        // Dica
        var hint = MakeTMP(panel.transform, "Pressione F2 para fechar", 11, C_TEXT_DIM);
        hint.alignment = TextAlignmentOptions.Center;
        SetRect(hint.gameObject, PAD, y, PANEL_W - PAD * 2, 16f);
        y -= 22f;

        // Separador
        MakeSep(panel.transform, y);
        y -= 8f;

        // ── Conteúdo ──
        y = PopulateControls(panel.transform, y);

        y -= PAD;
        pr.sizeDelta = new Vector2(PANEL_W, -y);
    }

    /// <summary>
    /// Preenche o menu com todos os controles.
    /// PARA ADICIONAR NOVOS CONTROLES: basta chamar AddKeybind() aqui.
    /// </summary>
    float PopulateControls(Transform parent, float y)
    {
        // ══ MOVIMENTAÇÃO ══
        y = AddSection(parent, "🚀  MOVIMENTAÇÃO", y);
        y = AddKeybind(parent, "WASD", "Mover personagem", y);
        y = AddKeybind(parent, "E", "Dash", y);

        y -= 6f;

        // ══ COMBATE & SOBREVIVÊNCIA ══
        y = AddSection(parent, "⚔  COMBATE & SOBREVIVÊNCIA", y);
        y = AddKeybind(parent, "LMB", "Ataque Primário (combo)", y);
        y = AddKeybind(parent, "Q", "Recuperar Vida (Cápsula do Traje)", y, C_CYAN);
        y = AddKeybind(parent, "LMB (Pistola)", "Disparar Arma / Pistola", y);
        y = AddKeybind(parent, "R", "Recarregar Bateria da Arma", y);
        y = AddKeybind(parent, "U", "Ativar Ultimate", y, C_GOLD);
        y = AddKeybind(parent, "Mira", AimSettings.IsMouseAim ? "Modo: Mouse (Cursor visível)" : "Modo: Direção (WASD)", y, C_CYAN);

        y -= 6f;

        // ══ INTERFACE ══
        y = AddSection(parent, "📦  INTERFACE / HUDs", y);
        y = AddKeybind(parent, "Tab", "Inventário", y);
        y = AddKeybind(parent, "F5", "HUD de Atributos", y, C_CYAN);
        y = AddKeybind(parent, "F3", "HUD de Economia da Run", y, C_CYAN);
        y = AddKeybind(parent, "F2", "Este menu (Controles)", y, C_CYAN);
        y = AddKeybind(parent, "ESC", "Fechar menus abertos", y);

        y -= 6f;

        // ══ DEBUG / DEV ══
        y = AddSection(parent, "🔧  DEBUG / DEV", y);
        y = AddKeybind(parent, "/", "Cheat Console", y, C_RED);
        y = AddKeybind(parent, "F1", "Debug (reservado)", y, C_TEXT_DIM);
        y = AddKeybind(parent, ";", "Dev tools (reservado)", y, C_TEXT_DIM);

        return y;
    }

    // ═══════════════════════════════════════════
    //  ROW BUILDERS
    // ═══════════════════════════════════════════

    float AddSection(Transform parent, string label, float y)
    {
        y -= 4f;
        GameObject bg = MakeImg(parent, "Sec", C_SECTION);
        SetRect(bg, PAD, y, PANEL_W - PAD * 2, SEC_H);

        var t = MakeTMP(bg.transform, label, 12, C_ACCENT);
        Fill(t.gameObject);
        t.margin = new Vector4(8, 0, 0, 0);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.fontStyle = FontStyles.Bold;

        return y - SEC_H - 4f;
    }

    float AddKeybind(Transform parent, string key, string description, float y, Color? descColor = null)
    {
        Color dColor = descColor ?? C_TEXT;
        float rowX = PAD + 6f;

        // Key badge
        GameObject keyBg = MakeImg(parent, "Key", C_KEY_BG);
        SetRect(keyBg, rowX, y, KEY_W, ROW_H);

        // Key border
        GameObject keyBrd = MakeImg(keyBg.transform, "KB", C_KEY_BRD);
        RectTransform kbrt = keyBrd.GetComponent<RectTransform>();
        kbrt.anchorMin = Vector2.zero; kbrt.anchorMax = Vector2.one;
        kbrt.offsetMin = kbrt.offsetMax = Vector2.zero;
        keyBrd.GetComponent<Image>().raycastTarget = false;

        var keyTxt = MakeTMP(keyBg.transform, key, 13, C_TEXT);
        Fill(keyTxt.gameObject);
        keyTxt.alignment = TextAlignmentOptions.Center;
        keyTxt.fontStyle = FontStyles.Bold;

        // Description
        float descX = rowX + KEY_W + 12f;
        float descW = PANEL_W - descX - PAD;
        var desc = MakeTMP(parent, description, 14, dColor);
        SetRect(desc.gameObject, descX, y, descW, ROW_H);
        desc.alignment = TextAlignmentOptions.MidlineLeft;

        return y - ROW_H - ROW_GAP;
    }

    // ═══════════════════════════════════════════
    //  PRIMITIVAS
    // ═══════════════════════════════════════════

    void MakeSep(Transform parent, float y)
    {
        GameObject sep = MakeImg(parent, "Sep", C_ACCENT * 0.2f);
        SetRect(sep, PAD, y, PANEL_W - PAD * 2, 1f);
        sep.GetComponent<Image>().raycastTarget = false;
    }

    GameObject MakeImg(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    TextMeshProUGUI MakeTMP(Transform parent, string text, int size, Color color)
    {
        GameObject go = new GameObject("T");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    void SetRect(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    void Fill(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
