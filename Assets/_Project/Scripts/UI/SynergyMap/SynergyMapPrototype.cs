using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Mapa Cósmico de Sinergias Alquímicas.
/// Totalmente autossuficiente e gerado por código.
/// Abre ao pressionar a tecla [M], pelo menu do Eptinho ou por chamada externa.
/// Exibe a constelação sagrada das 10 sinergias oficiais de Plerotus.
/// </summary>
public class SynergyMapPrototype : MonoBehaviour
{
    public static SynergyMapPrototype Instance { get; private set; }

    // ── Config ─────────────────────────────────────────────────
    [Header("Teclas")]
    [SerializeField] private Key toggleKey = Key.M;

    [Header("Área do Mapa")]
    [SerializeField] private float contentWidth = 3200f;
    [SerializeField] private float contentHeight = 2400f;

    [Header("Dimensões dos Nós")]
    [SerializeField] private float nodeSize = 75f;
    [SerializeField] private float centralNodeSize = 95f;

    [Header("Estrelas de Fundo")]
    [SerializeField] private int starCount = 65;

    // ── Runtime refs ──────────────────────────────────────────
    private GameObject _mapRoot;
    private Transform _contentContainer;
    private Transform _linesContainer;
    private Transform _nodesContainer;
    private GameObject _tooltipGO;
    private TextMeshProUGUI _tooltipTitle;
    private TextMeshProUGUI _tooltipDesc;
    private RectTransform _tooltipRect;
    private Canvas _canvas;
    private TextMeshProUGUI _counterText;
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;

    // Cache de sprites gerados proceduralmente
    private static Sprite _circleSprite;

    // ════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        if (FindFirstObjectByType<SynergyMapPrototype>() == null && FindFirstObjectByType<SynergyMapToggle>() == null)
        {
            var go = new GameObject("SynergyMap_CelestialSystem");
            go.AddComponent<SynergyMapPrototype>();
            DontDestroyOnLoad(go);
            Debug.Log("[SYNERGY MAP] Mapa Cósmico de Sinergias inicializado automaticamente.");
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        EnsureEventSystem();
        BuildBaseUI();
        if (_mapRoot != null)
        {
            _mapRoot.SetActive(false);
        }
        _isOpen = false;
    }

    private void Update()
    {
        bool toggleRequested = false;

        // Suporte a New Input System
        if (Keyboard.current != null)
        {
            if (Keyboard.current[toggleKey].wasPressedThisFrame)
                toggleRequested = true;

            if (_isOpen && Keyboard.current[Key.Escape].wasPressedThisFrame)
            {
                Close();
                return;
            }
        }

        // Fallback para Input clássico
        if (!toggleRequested && Input.GetKeyDown(KeyCode.M))
        {
            toggleRequested = true;
        }
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (toggleRequested)
        {
            Toggle();
        }

        // Posicionamento do Tooltip seguindo o mouse
        if (_isOpen && _tooltipGO != null && _tooltipGO.activeSelf && _canvas != null)
        {
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, mouse, null, out Vector2 local);

            // Ajuste para não vazar a borda da tela
            Vector2 finalPos = local + new Vector2(25f, -25f);
            if (finalPos.x + 360f > Screen.width * 0.5f) finalPos.x -= 385f;
            if (finalPos.y - 180f < -Screen.height * 0.5f) finalPos.y += 200f;

            _tooltipRect.localPosition = finalPos;
        }
    }

    // ════════════════════════════════════════════════════════════
    // CONTROLE DE VISIBILIDADE
    // ════════════════════════════════════════════════════════════

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_mapRoot == null) BuildBaseUI();

        _isOpen = true;
        _mapRoot.SetActive(true);
        RefreshConstellation();

        // Libera cursor para navegação e inspeção com hover
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[SYNERGY MAP] Mapa Celestial ABERTO.");
    }

    public void RevealAllSynergiesAction()
    {
        if (SynergyCatalogManager.Instance != null)
        {
            SynergyCatalogManager.Instance.DiscoverAllSynergies();
            RefreshConstellation();
            if (EptinhoPopupController.instancia != null)
                EptinhoPopupController.instancia.MostrarPopupAviso("✨ Todas as 10 Sinergias Alquímicas Reveladas no Mapa!");
        }
    }

    public void Close()
    {
        _isOpen = false;
        if (_mapRoot != null)
        {
            _mapRoot.SetActive(false);
        }
        HideTooltip();

        Debug.Log("[SYNERGY MAP] Mapa Celestial FECHADO.");
    }

    // ════════════════════════════════════════════════════════════
    // BUILDER — Monta estrutura da tela
    // ════════════════════════════════════════════════════════════

    private void BuildBaseUI()
    {
        if (_mapRoot != null) return;

        // ── 1. Canvas Raiz ─────────────────────────────────────
        _mapRoot = new GameObject("SynergyMap_CelestialCanvas");
        _mapRoot.transform.SetParent(transform, false);

        _canvas = _mapRoot.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 998;

        var scaler = _mapRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _mapRoot.AddComponent<GraphicRaycaster>();

        // ── 2. Viewport ────────────────────────────────────────
        GameObject viewport = CreateUIObject("Viewport", _mapRoot.transform);
        StretchFull(viewport);
        viewport.AddComponent<RectMask2D>();

        // ── 3. Content ─────────────────────────────────────────
        GameObject content = CreateUIObject("Content", viewport.transform);
        _contentContainer = content.transform;
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.sizeDelta = new Vector2(contentWidth, contentHeight);
        contentRT.anchoredPosition = Vector2.zero;

        // ── 4. ScrollRect Cósmico ──────────────────────────────
        ScrollRect scroll = _mapRoot.AddComponent<ScrollRect>();
        scroll.content = contentRT;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 15f;

        // ── 5. Background Estrelado Nebular ────────────────────
        GameObject bg = CreateUIObject("Background_Starfield", content.transform);
        StretchFull(bg);
        RawImage rawImg = bg.AddComponent<RawImage>();
        rawImg.texture = GenerateStarfield(512, 512, starCount);
        rawImg.uvRect = new Rect(0, 0, 4, 3);
        rawImg.color = Color.white;
        rawImg.raycastTarget = true;

        // ── 6. Contêineres de Linhas e Nós ─────────────────────
        GameObject linesGO = CreateUIObject("Lines_Layer", content.transform);
        StretchFull(linesGO);
        _linesContainer = linesGO.transform;

        GameObject nodesGO = CreateUIObject("Nodes_Layer", content.transform);
        StretchFull(nodesGO);
        _nodesContainer = nodesGO.transform;

        // ── 7. Header e Barra Superior ─────────────────────────
        BuildHeader(_mapRoot.transform);

        // ── 8. Tooltip Flutuante ───────────────────────────────
        BuildTooltip(_mapRoot.transform);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject headerObj = CreateUIObject("TopHeaderBar", parent);
        RectTransform rt = headerObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 110f);
        rt.anchoredPosition = Vector2.zero;

        Image bg = headerObj.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        Outline outline = headerObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.6f, 0.9f, 0.35f);
        outline.effectDistance = new Vector2(0, -2);

        // Título Principal
        GameObject titleObj = CreateUIObject("TitleText", headerObj.transform);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(800f, 40f);
        titleRt.anchoredPosition = new Vector2(0f, 18f);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "★  MAPA CÓSMICO DE SINERGIAS ALQUÍMICAS  ★";
        titleText.fontSize = 24f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.92f, 0.96f, 1.0f);

        // Subtítulo / Progresso
        GameObject subObj = CreateUIObject("SubtitleText", headerObj.transform);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.sizeDelta = new Vector2(900f, 30f);
        subRt.anchoredPosition = new Vector2(0f, -18f);

        _counterText = subObj.AddComponent<TextMeshProUGUI>();
        _counterText.fontSize = 15f;
        _counterText.alignment = TextAlignmentOptions.Center;
        _counterText.color = new Color(0.55f, 0.75f, 0.95f);

        // Botão Fechar [X]
        GameObject closeBtn = CreateUIObject("BtnClose", headerObj.transform);
        RectTransform btnRt = closeBtn.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0.5f);
        btnRt.anchorMax = new Vector2(1f, 0.5f);
        btnRt.pivot = new Vector2(1f, 0.5f);
        btnRt.sizeDelta = new Vector2(210f, 44f);
        btnRt.anchoredPosition = new Vector2(-30f, 0f);

        Image btnBg = closeBtn.AddComponent<Image>();
        btnBg.color = new Color(0.12f, 0.16f, 0.25f, 0.95f);

        Outline btnOutline = closeBtn.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.35f, 0.8f, 1f, 0.8f);
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = closeBtn.AddComponent<Button>();
        btn.onClick.AddListener(Close);

        GameObject btnTxtObj = CreateUIObject("Text", closeBtn.transform);
        StretchFull(btnTxtObj);
        TextMeshProUGUI btnTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
        btnTxt.text = "✕ FECHAR (M ou ESC)";
        btnTxt.fontSize = 14f;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = new Color(0.9f, 0.95f, 1f);
    }

    // ════════════════════════════════════════════════════════════
    // REFRESH DA CONSTELAÇÃO (Nós e Linhas)
    // ════════════════════════════════════════════════════════════

    public void RefreshConstellation()
    {
        if (_linesContainer == null || _nodesContainer == null) return;

        // Limpa instâncias anteriores
        foreach (Transform child in _linesContainer) Destroy(child.gameObject);
        foreach (Transform child in _nodesContainer) Destroy(child.gameObject);

        var recipes = SynergyDatabase.AllRecipes;
        int discoveredCount = 0;

        // Mapeia posições e estados de descoberta
        Dictionary<string, SynergyRecipe> recipeMap = new Dictionary<string, SynergyRecipe>();
        Dictionary<string, bool> discoveredMap = new Dictionary<string, bool>();

        foreach (var r in recipes)
        {
            recipeMap[r.id] = r;
            bool isDisc = SynergyCatalogManager.Instance != null && SynergyCatalogManager.Instance.IsNodeDiscovered(r.id);
            discoveredMap[r.id] = isDisc;
            if (isDisc) discoveredCount++;
        }

        // Atualiza contador no topo
        if (_counterText != null)
        {
            _counterText.text = $"Sinergias Catalogadas: <color=#00FFAA><b>{discoveredCount}/{recipes.Count}</b></color>  •  " +
                                $"Arraste o mouse para explorar  •  Passe o cursor sobre os nós para ver a receita";
        }

        // 1. Desenha Linhas da Constelação
        // Anel octogonal externo (1 a 8)
        for (int i = 0; i < 8; i++)
        {
            int nextIdx = (i + 1) % 8;
            DrawConstellationLine(recipes[i], recipes[nextIdx], discoveredMap);
        }

        // Diagonais cruzadas e conexões sagradas com o santuário interno (9 e 10)
        DrawConstellationLine(recipes[0], recipes[4], discoveredMap); // Leste - Oeste
        DrawConstellationLine(recipes[2], recipes[6], discoveredMap); // Norte - Sul
        DrawConstellationLine(recipes[1], recipes[5], discoveredMap); // Nordeste - Sudoeste
        DrawConstellationLine(recipes[3], recipes[7], discoveredMap); // Noroeste - Sudeste

        // Conexões centrais
        if (recipes.Count >= 10)
        {
            var berserker = recipes[8];
            var ascensao = recipes[9];
            DrawConstellationLine(berserker, ascensao, discoveredMap);
            DrawConstellationLine(berserker, recipes[4], discoveredMap);
            DrawConstellationLine(ascensao, recipes[0], discoveredMap);
        }

        // 2. Instancia os Nós Cósmicos
        for (int i = 0; i < recipes.Count; i++)
        {
            CreateRecipeNode(recipes[i], i + 1, discoveredMap[recipes[i].id]);
        }
    }

    private void DrawConstellationLine(SynergyRecipe a, SynergyRecipe b, Dictionary<string, bool> discMap)
    {
        if (a == null || b == null) return;

        bool bothDiscovered = discMap.TryGetValue(a.id, out bool dA) && dA &&
                              discMap.TryGetValue(b.id, out bool dB) && dB;

        GameObject lineObj = CreateUIObject($"Line_{a.name}_{b.name}", _linesContainer);
        Image img = lineObj.AddComponent<Image>();

        if (bothDiscovered)
        {
            // Linha dourada/ciano brilhante
            img.color = new Color(0.2f, 0.95f, 0.9f, 0.75f);
        }
        else
        {
            // Linha tênue espectral
            img.color = new Color(0.35f, 0.45f, 0.65f, 0.20f);
        }

        RectTransform rect = lineObj.GetComponent<RectTransform>();
        Vector2 posA = a.mapPosition;
        Vector2 posB = b.mapPosition;
        Vector2 dir = (posB - posA).normalized;
        float dist = Vector2.Distance(posA, posB);

        float thickness = bothDiscovered ? 4.5f : 2.0f;
        rect.sizeDelta = new Vector2(dist, thickness);
        rect.anchoredPosition = posA + dir * dist * 0.5f;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void CreateRecipeNode(SynergyRecipe recipe, int displayIndex, bool isDiscovered)
    {
        bool isCentral = (displayIndex >= 9);
        float currentSize = isCentral ? centralNodeSize : nodeSize;

        GameObject nodeObj = CreateUIObject($"Node_{recipe.id}", _nodesContainer);
        RectTransform rt = nodeObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(currentSize, currentSize);
        rt.anchoredPosition = recipe.mapPosition;

        // Aura / Halo exterior
        GameObject haloObj = CreateUIObject("GlowHalo", nodeObj.transform);
        RectTransform haloRt = haloObj.GetComponent<RectTransform>();
        haloRt.anchorMin = Vector2.zero;
        haloRt.anchorMax = Vector2.one;
        haloRt.sizeDelta = new Vector2(currentSize * 0.45f, currentSize * 0.45f);
        haloRt.anchoredPosition = Vector2.zero;

        Image haloImg = haloObj.AddComponent<Image>();
        haloImg.sprite = GetCircleSprite();
        haloImg.raycastTarget = false;

        if (isDiscovered)
        {
            Color hColor = recipe.themeColor;
            hColor.a = 0.35f;
            haloImg.color = hColor;
        }
        else
        {
            haloImg.color = new Color(0.15f, 0.2f, 0.3f, 0.15f);
        }

        // Círculo Principal do Nó
        Image mainImg = nodeObj.AddComponent<Image>();
        mainImg.sprite = GetCircleSprite();
        mainImg.raycastTarget = true;

        if (isDiscovered)
        {
            mainImg.color = recipe.themeColor;
            Outline outline = nodeObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
        else
        {
            // Nó Oculto / Mistério
            mainImg.color = new Color(0.08f, 0.10f, 0.16f, 0.95f);
            Outline outline = nodeObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.35f, 0.5f, 0.5f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // Símbolo Interno (Romano ou ?)
        GameObject glyphObj = CreateUIObject("CenterGlyph", nodeObj.transform);
        StretchFull(glyphObj);
        TextMeshProUGUI glyphText = glyphObj.AddComponent<TextMeshProUGUI>();
        glyphText.fontSize = isCentral ? 32f : 24f;
        glyphText.fontStyle = FontStyles.Bold;
        glyphText.alignment = TextAlignmentOptions.Center;
        glyphText.raycastTarget = false;

        if (isDiscovered)
        {
            glyphText.text = ToRoman(displayIndex);
            glyphText.color = new Color(0.05f, 0.05f, 0.1f, 0.95f); // Letra escura contrastante
        }
        else
        {
            glyphText.text = "?";
            glyphText.color = new Color(0.45f, 0.55f, 0.7f, 0.8f);
        }

        // Rótulo Inferior com o Nome da Sinergia
        GameObject labelObj = CreateUIObject("Label", nodeObj.transform);
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0f);
        labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.sizeDelta = new Vector2(220f, 30f);
        labelRt.anchoredPosition = new Vector2(0f, -12f);

        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.fontSize = 14f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        if (isDiscovered)
        {
            labelText.text = recipe.name;
            labelText.color = new Color(0.95f, 0.97f, 1f);
        }
        else
        {
            labelText.text = $"[ ? ? ? ]";
            labelText.color = new Color(0.45f, 0.52f, 0.62f);
        }

        // EventTrigger para Hover do Mouse e Exibição do Tooltip
        var trigger = nodeObj.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) =>
        {
            if (isDiscovered)
            {
                string themeHex = ColorUtility.ToHtmlStringRGB(recipe.themeColor);
                string title = $"★ {recipe.name}";
                string desc =
                    $"<b><color=#{themeHex}>[SINERGIA ALQUÍMICA CATALOGADA]</color></b>\n" +
                    $"<b>Compostos Requeridos:</b>\n{recipe.recipeIngredientsText}\n\n" +
                    $"<b>Economia de Essência:</b> <color=#FFD700>-{recipe.essenceDiscount * 100f:F0}%</color>\n" +
                    $"<b>Efeito Alquímico:</b>\n{recipe.description}";

                ShowTooltip(title, desc);
            }
            else
            {
                string title = $"[ ? ? ? ] Sinergia Oculta";
                string desc =
                    $"<b><color=#8899AA>[NÃO CATALOGADA]</color></b>\n" +
                    $"<i>Realize a infusão desta combinação para decifrar este nó celestial.</i>\n\n" +
                    $"<b>Dica:</b>\n<color=#66DDFF>{recipe.discoveryHint}</color>";

                ShowTooltip(title, desc);
            }
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => HideTooltip());
        trigger.triggers.Add(exit);
    }

    // ════════════════════════════════════════════════════════════
    // TOOLTIP CÓSMICO
    // ════════════════════════════════════════════════════════════

    private void BuildTooltip(Transform parent)
    {
        _tooltipGO = CreateUIObject("CelestialTooltip", parent);
        _tooltipRect = _tooltipGO.GetComponent<RectTransform>();
        _tooltipRect.anchorMin = Vector2.zero;
        _tooltipRect.anchorMax = Vector2.zero;
        _tooltipRect.pivot = new Vector2(0, 1);
        _tooltipRect.sizeDelta = new Vector2(380f, 220f);

        Image panel = _tooltipGO.AddComponent<Image>();
        panel.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);
        panel.raycastTarget = false;

        Outline outline = _tooltipGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.75f, 1f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Título
        GameObject titleObj = CreateUIObject("Title", _tooltipGO.transform);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.75f);
        titleRt.anchorMax = new Vector2(0.95f, 0.95f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        _tooltipTitle = titleObj.AddComponent<TextMeshProUGUI>();
        _tooltipTitle.fontSize = 17f;
        _tooltipTitle.fontStyle = FontStyles.Bold;
        _tooltipTitle.color = Color.white;
        _tooltipTitle.raycastTarget = false;

        // Descrição / Receita
        GameObject descObj = CreateUIObject("Description", _tooltipGO.transform);
        RectTransform descRt = descObj.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0.05f, 0.05f);
        descRt.anchorMax = new Vector2(0.95f, 0.75f);
        descRt.offsetMin = Vector2.zero;
        descRt.offsetMax = Vector2.zero;
        _tooltipDesc = descObj.AddComponent<TextMeshProUGUI>();
        _tooltipDesc.fontSize = 13f;
        _tooltipDesc.color = new Color(0.85f, 0.88f, 0.95f);
        _tooltipDesc.raycastTarget = false;

        _tooltipGO.SetActive(false);
    }

    private void ShowTooltip(string title, string desc)
    {
        if (_tooltipGO == null) return;
        _tooltipTitle.text = title;
        _tooltipDesc.text = desc;
        _tooltipGO.SetActive(true);
    }

    private void HideTooltip()
    {
        if (_tooltipGO != null)
            _tooltipGO.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS & PROCEDURAL GRAPHICS
    // ════════════════════════════════════════════════════════════

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem_CelestialMap");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = center - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist < radius - 1.5f)
                    tex.SetPixel(x, y, Color.white);
                else if (dist < radius)
                    tex.SetPixel(x, y, new Color(1, 1, 1, radius - dist));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        return _circleSprite;
    }

    private Texture2D GenerateStarfield(int w, int h, int stars)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.015f, 0.02f, 0.04f, 1f);

        var oldState = Random.state;
        Random.InitState(777);
        for (int i = 0; i < stars; i++)
        {
            int x = Random.Range(0, w);
            int y = Random.Range(0, h);
            float b = Random.Range(0.4f, 1f);
            float tint = Random.Range(0f, 1f);
            Color c;
            if (tint < 0.6f) c = new Color(b, b, b);
            else if (tint < 0.85f) c = new Color(b * 0.7f, b * 0.85f, b); // Azul cósmico
            else c = new Color(b, b * 0.85f, b * 0.5f); // Ouro estelar
            pixels[y * w + x] = c;
        }
        Random.state = oldState;

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private string ToRoman(int number)
    {
        switch (number)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            case 10: return "X";
            default: return number.ToString();
        }
    }
}
