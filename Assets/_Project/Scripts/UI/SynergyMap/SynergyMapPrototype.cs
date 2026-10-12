using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Script ÚNICO e auto-contido que monta o Mapa de Sinergias inteiro por código (Protótipo).
/// Coloque num GameObject vazio na cena. Aperte M para abrir/fechar.
/// Não precisa configurar NADA no Inspector.
/// </summary>
public class SynergyMapPrototype : MonoBehaviour
{
    // ── Config ─────────────────────────────────────────────────
    [Header("Tecla de Toggle")]
    [SerializeField] private Key toggleKey = Key.M;

    [Header("Área do Mapa")]
    [SerializeField] private float contentWidth = 4000f;
    [SerializeField] private float contentHeight = 3000f;

    [Header("Nós")]
    [SerializeField] private int nodeCount = 12;
    [SerializeField] private float nodeSize = 70f;

    [Header("Estrelas")]
    [SerializeField] private int starCount = 50;

    [Header("Textos Padrão")]
    [SerializeField] private string nodeTitle = "Título";
    [SerializeField] [TextArea(2, 4)] private string nodeDescription = "Descrição";

    // ── Runtime refs ──────────────────────────────────────────
    private GameObject _mapRoot;
    private GameObject _tooltipGO;
    private TextMeshProUGUI _tooltipTitle;
    private TextMeshProUGUI _tooltipDesc;
    private RectTransform _tooltipRect;
    private Canvas _canvas;
    private bool _isOpen;

    private static readonly Color[] NodeColors = {
        new Color(1f, 0.35f, 0.15f),   // laranja fogo
        new Color(0.3f, 0.7f, 1f),     // azul gelo
        new Color(0.9f, 0.85f, 0.2f),  // amarelo raio
        new Color(0.5f, 0.9f, 0.3f),   // verde veneno
        new Color(0.8f, 0.3f, 0.9f),   // roxo espectral
        new Color(0.4f, 0.6f, 1f),     // azul escudo
        new Color(1f, 0.5f, 0.2f),     // laranja dragão
        new Color(0.2f, 0.3f, 0.6f),   // azul escuro
        new Color(1f, 0.2f, 0.2f),     // vermelho fúria
        new Color(0.3f, 1f, 0.5f),     // verde vital
        new Color(0.6f, 0.5f, 0.8f),   // lilás
        new Color(0.9f, 0.8f, 0.5f),   // dourado
        new Color(0.4f, 0.9f, 0.9f),   // ciano
        new Color(1f, 0.6f, 0.8f),     // rosa
        new Color(0.5f, 1f, 1f),       // cristal
        new Color(0.3f, 0.2f, 0.4f),   // sombra
    };

    // ════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════

    private void Start()
    {
        EnsureEventSystem();
        BuildMap();
        _mapRoot.SetActive(false);
        _isOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            _isOpen = !_isOpen;
            _mapRoot.SetActive(_isOpen);
            if (!_isOpen) HideTooltip();
        }

        // Tooltip segue o mouse
        if (_isOpen && _tooltipGO != null && _tooltipGO.activeSelf)
        {
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, mouse, null, out Vector2 local);
            _tooltipRect.localPosition = local + new Vector2(20f, -20f);
        }
    }

    // ════════════════════════════════════════════════════════════
    // BUILDER — monta tudo por código
    // ════════════════════════════════════════════════════════════

    private void BuildMap()
    {
        // ── 1. Canvas ──────────────────────────────────────────
        _mapRoot = new GameObject("SynergyMap_Runtime");
        _canvas = _mapRoot.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

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
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.sizeDelta = new Vector2(contentWidth, contentHeight);
        contentRT.anchoredPosition = Vector2.zero;

        // ── 4. ScrollRect ──────────────────────────────────────
        ScrollRect scroll = _mapRoot.AddComponent<ScrollRect>();
        scroll.content = contentRT;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 10f;

        // ── 5. Background estrelado ────────────────────────────
        GameObject bg = CreateUIObject("Background", content.transform);
        StretchFull(bg);
        RawImage rawImg = bg.AddComponent<RawImage>();
        rawImg.texture = GenerateStarfield(512, 512, starCount);
        rawImg.uvRect = new Rect(0, 0, 3, 2);
        rawImg.color = Color.white;
        rawImg.raycastTarget = true;

        // ── 6. Nós ─────────────────────────────────────────────
        Random.InitState(42);
        float halfW = contentWidth * 0.4f;
        float halfH = contentHeight * 0.4f;

        for (int i = 0; i < nodeCount; i++)
        {
            Vector2 pos = new Vector2(
                Random.Range(-halfW, halfW),
                Random.Range(-halfH, halfH)
            );
            CreateNode(content.transform, pos, i);
        }

        // ── 7. Tooltip ─────────────────────────────────────────
        BuildTooltip();
    }

    // ════════════════════════════════════════════════════════════
    // NÓ
    // ════════════════════════════════════════════════════════════

    private void CreateNode(Transform parent, Vector2 position, int index)
    {
        int colorIdx = index % NodeColors.Length;
        GameObject nodeGO = CreateUIObject($"Node_{index + 1}", parent);
        RectTransform rt = nodeGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(nodeSize, nodeSize);
        rt.anchoredPosition = position;

        Image img = nodeGO.AddComponent<Image>();
        img.sprite = CreateCircleSprite();
        img.color = NodeColors[colorIdx];
        img.raycastTarget = true;

        // Glow / borda
        GameObject glowGO = CreateUIObject("Glow", nodeGO.transform);
        RectTransform glowRT = glowGO.GetComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.sizeDelta = new Vector2(16, 16);
        glowRT.anchoredPosition = Vector2.zero;
        Image glowImg = glowGO.AddComponent<Image>();
        glowImg.sprite = CreateCircleSprite();
        Color glowColor = NodeColors[colorIdx];
        glowColor.a = 0.25f;
        glowImg.color = glowColor;
        glowImg.raycastTarget = false;

        // Label abaixo do nó
        GameObject labelGO = CreateUIObject("Label", nodeGO.transform);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.5f, 0f);
        labelRT.anchorMax = new Vector2(0.5f, 0f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.sizeDelta = new Vector2(140, 24);
        labelRT.anchoredPosition = new Vector2(0, -8);
        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = nodeTitle;
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        label.raycastTarget = false;

        // Hover handler
        var trigger = nodeGO.AddComponent<EventTrigger>();

        // PointerEnter
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) => ShowTooltip(nodeTitle, nodeDescription));
        trigger.triggers.Add(enterEntry);

        // PointerExit
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => HideTooltip());
        trigger.triggers.Add(exitEntry);
    }

    // ════════════════════════════════════════════════════════════
    // TOOLTIP
    // ════════════════════════════════════════════════════════════

    private void BuildTooltip()
    {
        _tooltipGO = CreateUIObject("Tooltip", _mapRoot.transform);
        _tooltipRect = _tooltipGO.GetComponent<RectTransform>();
        _tooltipRect.anchorMin = Vector2.zero;
        _tooltipRect.anchorMax = Vector2.zero;
        _tooltipRect.pivot = new Vector2(0, 1);
        _tooltipRect.sizeDelta = new Vector2(300, 100);

        Image panel = _tooltipGO.AddComponent<Image>();
        panel.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        panel.raycastTarget = false;

        // Borda sutil
        Outline outline = _tooltipGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.5f, 1f, 0.5f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Título
        GameObject titleGO = CreateUIObject("Title", _tooltipGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.55f);
        titleRT.anchorMax = new Vector2(0.95f, 0.95f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        _tooltipTitle = titleGO.AddComponent<TextMeshProUGUI>();
        _tooltipTitle.fontSize = 16;
        _tooltipTitle.fontStyle = FontStyles.Bold;
        _tooltipTitle.color = Color.white;
        _tooltipTitle.raycastTarget = false;

        // Descrição
        GameObject descGO = CreateUIObject("Desc", _tooltipGO.transform);
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.05f, 0.05f);
        descRT.anchorMax = new Vector2(0.95f, 0.55f);
        descRT.offsetMin = Vector2.zero;
        descRT.offsetMax = Vector2.zero;
        _tooltipDesc = descGO.AddComponent<TextMeshProUGUI>();
        _tooltipDesc.fontSize = 12;
        _tooltipDesc.color = new Color(0.75f, 0.75f, 0.8f);
        _tooltipDesc.raycastTarget = false;

        _tooltipGO.SetActive(false);
    }

    private void ShowTooltip(string title, string desc)
    {
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
    // HELPERS
    // ════════════════════════════════════════════════════════════

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem_SynergyMap");
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

    private Texture2D GenerateStarfield(int w, int h, int stars)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.02f, 0.02f, 0.05f);

        var oldState = Random.state;
        Random.InitState(123);
        for (int i = 0; i < stars; i++)
        {
            int x = Random.Range(0, w);
            int y = Random.Range(0, h);
            float b = Random.Range(0.3f, 1f);
            float tint = Random.Range(0f, 1f);
            Color c;
            if (tint < 0.7f) c = new Color(b, b, b);
            else if (tint < 0.85f) c = new Color(b, b * 0.8f, b * 0.5f); // amarelada
            else c = new Color(b * 0.6f, b * 0.7f, b); // azulada
            pixels[y * w + x] = c;
        }
        Random.state = oldState;

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // Cache do sprite circular
    private static Sprite _cachedCircle;
    private Sprite CreateCircleSprite()
    {
        if (_cachedCircle != null) return _cachedCircle;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = center - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist < radius - 1f)
                    tex.SetPixel(x, y, Color.white);
                else if (dist < radius)
                    tex.SetPixel(x, y, new Color(1, 1, 1, radius - dist)); // borda suave
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        return _cachedCircle;
    }
}
