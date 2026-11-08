using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI de Contador de Salas / Detector de Ondas — Canto inferior direito (acima da Essência).
/// Ativado ao equipar o "Detector de Barreiras" (equip_detector_barreiras) no Crafting.
/// Design Glassmorphic Premium Sci-Fi:
///   - Painel escuro translúcido com borda suave e cantos arredondados (sem sobreposição feia)
///   - Holograma/Sonar de radar pulsante à esquerda (verde/ciano para área segura, dourado/vermelho em combate)
///   - Tipografia limpa, hierárquica e bem espaçada que nunca colide ou quebra indevidamente.
/// </summary>
public class BarrierCounterUI : MonoBehaviour
{
    public static BarrierCounterUI Instance { get; private set; }

    [Header("Visual do Painel")]
    public int   fontSize    = 16;
    public Color textColor   = new Color(0.95f, 0.88f, 0.55f, 1.00f); // dourado claro
    public Color labelColor  = new Color(0.65f, 0.78f, 0.88f, 0.90f); // ciano/cinza metálico
    public Color panelColor  = new Color(0.04f, 0.05f, 0.08f, 0.88f); // vidro escuro espacial
    public Color borderColor = new Color(0.28f, 0.40f, 0.65f, 0.60f); // ciano/azul neon suave

    [Header("Posição (Canto Inferior Direito)")]
    public float offsetX = -40f;
    public float offsetY = 105f;

    [Header("Tamanho do Painel")]
    public float panelWidth  = 240f;
    public float panelHeight = 64f;

    [Header("Pulso ao Limpar Sala")]
    public float pulseDuration = 0.25f;
    public float pulseScale    = 1.15f;

    // ── Referências Internas ──────────────────────────────────────────────────
    private Canvas          parentCanvas;
    private TextMeshProUGUI counterText;
    private TextMeshProUGUI labelText;
    private RectTransform   containerRect;
    private Image           bgImage;
    private Image           radarIcon;
    private Image           radarRing;
    private Coroutine       pulseRoutine;
    private float           updateTimer = 0f;
    private bool            isVisible = false;

    public const string EQUIPMENT_ID = "equip_detector_barreiras";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Garante dimensoes anti-sobreposicao e tamanho de fonte ideal
        if (panelWidth < 220f) panelWidth = 240f;
        if (panelHeight < 60f) panelHeight = 64f;
        if (fontSize > 18 || fontSize < 14) fontSize = 16;
    }

    void OnEnable()
    {
        EquipmentManager.OnEquipmentStateChanged += CheckEquipmentState;
        RoomController.OnRoomCleared             += OnRoomClearedHandler;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        CheckEquipmentState();
        UpdateDisplay();
    }

    void OnDisable()
    {
        EquipmentManager.OnEquipmentStateChanged -= CheckEquipmentState;
        RoomController.OnRoomCleared             -= OnRoomClearedHandler;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        CheckEquipmentState();
        UpdateDisplay();
    }

    void Update()
    {
        if (isVisible)
        {
            AnimateRadarPing();

            updateTimer += Time.deltaTime;
            if (updateTimer >= 0.5f)
            {
                updateTimer = 0f;
                UpdateDisplay();
            }
        }
    }

    private void AnimateRadarPing()
    {
        if (radarRing != null)
        {
            float ping = (Time.time * 1.8f) % 1f;
            float scale = Mathf.Lerp(0.8f, 1.45f, ping);
            radarRing.rectTransform.localScale = new Vector3(scale, scale, 1f);
            Color c = radarRing.color;
            c.a = Mathf.Lerp(0.7f, 0.0f, ping);
            radarRing.color = c;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyRealtimeVisuals();
    }
#endif

    public void ApplyRealtimeVisuals()
    {
        if (containerRect != null)
        {
            // Converte se estiver com coordenadas absolutas antigas (ex: 1700f de tela 1080p)
            float fixedX = offsetX;
            if (fixedX > 500f) fixedX = -(1920f - fixedX);

            containerRect.anchoredPosition = new Vector2(fixedX, offsetY);
            containerRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        }

        if (bgImage != null) bgImage.color = panelColor;
        if (labelText != null) labelText.color = labelColor;
    }

    // ─── API PÚBLICA ──────────────────────────────────────────────────────────

    public static void EnsureExistsAndSetVisible(bool visible)
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("BarrierCounterUI_Root");
            Instance = go.AddComponent<BarrierCounterUI>();
        }

        if (Instance != null)
        {
            Instance.SetVisible(visible);
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (visible)
        {
            EnsureCanvasAndUIBuilt();
            gameObject.SetActive(true);
            UpdateDisplay();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void CheckEquipmentState()
    {
        bool equipped = false;
        if (EquipmentManager.Instance != null)
        {
            equipped = EquipmentManager.Instance.IsEquipped(EQUIPMENT_ID);
        }

        SetVisible(equipped);
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        CheckEquipmentState();
        UpdateDisplay();
    }

    private void OnRoomClearedHandler(RoomController room)
    {
        UpdateDisplay();

        if (gameObject.activeInHierarchy && pulseRoutine == null && containerRect != null)
        {
            pulseRoutine = StartCoroutine(PulseAnim());
        }
    }

    // ─── CÁLCULO E ATUALIZAÇÃO DO DISPLAY ─────────────────────────────────────

    public void UpdateDisplay()
    {
        if (counterText == null) return;

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        RoomController[] allRooms = FindObjectsByType<RoomController>(FindObjectsSortMode.None);

        if (activeScene == "Base" || allRooms == null || allRooms.Length == 0)
        {
            counterText.text = "<color=#5CE1E6>\u00C1REA SEGURA</color>";
            if (labelText != null) labelText.text = "DETECTOR DE ONDAS";
            SetRadarTheme(new Color(0.36f, 0.88f, 0.90f, 1f));
            return;
        }

        // Procura a sala de combate atualmente trancada/ativa
        RoomController activeRoom = null;
        foreach (var r in allRooms)
        {
            if (r != null && r.doorsAreLocked)
            {
                activeRoom = r;
                break;
            }
        }

        if (activeRoom == null)
        {
            // Nenhuma barreira trancada no momento
            if (labelText != null) labelText.text = "DETECTOR DE ONDAS";
            counterText.text = "<color=#66FF88>\u00C1REA LIVRE</color>";
            SetRadarTheme(new Color(0.40f, 1.0f, 0.53f, 1f));
        }
        else
        {
            if (labelText != null) labelText.text = "COMBATE ATIVO";
            int totalWaves = activeRoom.TotalWaves;
            int currWave   = activeRoom.CurrentWaveNumber;
            int remaining  = Mathf.Max(0, totalWaves - currWave + 1);

            if (remaining > 0)
            {
                counterText.text = $"<color=#FFD154>ONDA {currWave}/{totalWaves}</color> <size=12><color=#A8C0D4>({remaining} rest.)</color></size>";
                SetRadarTheme(new Color(1f, 0.82f, 0.33f, 1f));
            }
            else
            {
                counterText.text = "<color=#FF6688>\u00DALTIMA ONDA!</color>";
                SetRadarTheme(new Color(1f, 0.40f, 0.53f, 1f));
            }
        }
    }

    private void SetRadarTheme(Color themeColor)
    {
        if (radarIcon != null) radarIcon.color = themeColor;
        if (radarRing != null)
        {
            Color c = themeColor;
            c.a = 0.5f;
            radarRing.color = c;
        }
    }

    // ─── CONSTRUÇÃO DA UI (CANTO INFERIOR DIREITO) ───────────────────────────

    private void EnsureCanvasAndUIBuilt()
    {
        if (GetComponent<Canvas>() == null)
        {
            Canvas c = gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 20;

            CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
        }

        if (containerRect == null)
        {
            CreateUI();
        }
    }

    private void CreateUI()
    {
        int layer = gameObject.layer;

        // Limpa filhos antigos se houver
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        TMP_FontAsset mainFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                              ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF")
                              ?? TMP_Settings.defaultFontAsset;

        // 1. Container do Painel (Ancorado no canto inferior direito)
        GameObject panelGO = new GameObject("PanelContainer");
        panelGO.transform.SetParent(transform, false);
        panelGO.layer = layer;

        containerRect = panelGO.AddComponent<RectTransform>();
        containerRect.anchorMin        = new Vector2(1f, 0f);
        containerRect.anchorMax        = new Vector2(1f, 0f);
        containerRect.pivot            = new Vector2(1f, 0f);

        float fixedX = offsetX;
        if (fixedX > 500f) fixedX = -(1920f - fixedX);

        containerRect.anchoredPosition = new Vector2(fixedX, offsetY);
        containerRect.sizeDelta        = new Vector2(panelWidth, panelHeight);

        // 2. Fundo Glassmorphic com cantos arredondados (9-slice procedural)
        bgImage = panelGO.AddComponent<Image>();
        bgImage.sprite = MakeSlicedRoundedCardSprite();
        bgImage.type = Image.Type.Sliced;
        bgImage.color = panelColor;
        bgImage.raycastTarget = false;

        // Moldura/Borda externa elegante
        GameObject borderGO = new GameObject("BorderGlow");
        borderGO.transform.SetParent(panelGO.transform, false);
        borderGO.layer = layer;
        RectTransform borderR = borderGO.AddComponent<RectTransform>();
        borderR.anchorMin = Vector2.zero;
        borderR.anchorMax = Vector2.one;
        borderR.sizeDelta = Vector2.zero;
        Image bordImg = borderGO.AddComponent<Image>();
        bordImg.sprite = MakeSlicedBorderSprite();
        bordImg.type = Image.Type.Sliced;
        bordImg.color = borderColor;
        bordImg.raycastTarget = false;

        // Barra de destaque sutil no topo do card (Linha de luz)
        GameObject accentGO = new GameObject("TopAccent");
        accentGO.transform.SetParent(panelGO.transform, false);
        accentGO.layer = layer;
        RectTransform accR = accentGO.AddComponent<RectTransform>();
        accR.anchorMin = new Vector2(0.08f, 1f);
        accR.anchorMax = new Vector2(0.92f, 1f);
        accR.pivot     = new Vector2(0.5f, 1f);
        accR.sizeDelta = new Vector2(0f, 1.5f);
        accR.anchoredPosition = new Vector2(0f, -1f);
        Image accImg = accentGO.AddComponent<Image>();
        accImg.color = new Color(0.40f, 0.70f, 1.0f, 0.65f);
        accImg.raycastTarget = false;

        // 3. Holograma Radar/Sonar (Esquerda do painel)
        GameObject radarRoot = new GameObject("RadarWidget");
        radarRoot.transform.SetParent(panelGO.transform, false);
        radarRoot.layer = layer;
        RectTransform radarR = radarRoot.AddComponent<RectTransform>();
        radarR.anchorMin = new Vector2(0f, 0.5f);
        radarR.anchorMax = new Vector2(0f, 0.5f);
        radarR.pivot     = new Vector2(0.5f, 0.5f);
        radarR.anchoredPosition = new Vector2(28f, 0f);
        radarR.sizeDelta = new Vector2(36f, 36f);

        Sprite circleSprite = MakeCircleSprite(64);

        // Anel de pulso (animado no Update)
        GameObject ringGO = new GameObject("PulseRing");
        ringGO.transform.SetParent(radarRoot.transform, false);
        ringGO.layer = layer;
        RectTransform ringR = ringGO.AddComponent<RectTransform>();
        ringR.anchorMin = ringR.anchorMax = new Vector2(0.5f, 0.5f);
        ringR.pivot = new Vector2(0.5f, 0.5f);
        ringR.sizeDelta = new Vector2(30f, 30f);
        radarRing = ringGO.AddComponent<Image>();
        radarRing.sprite = circleSprite;
        radarRing.color = new Color(0.36f, 0.88f, 0.90f, 0.5f);
        radarRing.raycastTarget = false;

        // Núcleo do radar (ponto central brilhante)
        GameObject iconGO = new GameObject("RadarCore");
        iconGO.transform.SetParent(radarRoot.transform, false);
        iconGO.layer = layer;
        RectTransform iconR = iconGO.AddComponent<RectTransform>();
        iconR.anchorMin = iconR.anchorMax = new Vector2(0.5f, 0.5f);
        iconR.pivot = new Vector2(0.5f, 0.5f);
        iconR.sizeDelta = new Vector2(14f, 14f);
        radarIcon = iconGO.AddComponent<Image>();
        radarIcon.sprite = circleSprite;
        radarIcon.color = new Color(0.36f, 0.88f, 0.90f, 1.0f);
        radarIcon.raycastTarget = false;

        // 4. Bloco de Texto (À direita do radar)
        float textLeftOffset = 52f;
        float textRightMargin = 12f;

        // Título / Label Superior
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(panelGO.transform, false);
        labelGO.layer = layer;

        RectTransform labelR = labelGO.AddComponent<RectTransform>();
        labelR.anchorMin = new Vector2(0f, 0.52f);
        labelR.anchorMax = new Vector2(1f, 0.92f);
        labelR.pivot     = new Vector2(0f, 0.5f);
        labelR.offsetMin = new Vector2(textLeftOffset, 0f);
        labelR.offsetMax = new Vector2(-textRightMargin, 0f);

        labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = "DETECTOR DE ONDAS";
        labelText.fontSize = 11f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 1.2f;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.color = labelColor;
        labelText.raycastTarget = false;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        if (mainFont != null) labelText.font = mainFont;

        // Valor / Contador Inferior
        GameObject valGO = new GameObject("Value");
        valGO.transform.SetParent(panelGO.transform, false);
        valGO.layer = layer;

        RectTransform valR = valGO.AddComponent<RectTransform>();
        valR.anchorMin = new Vector2(0f, 0.08f);
        valR.anchorMax = new Vector2(1f, 0.52f);
        valR.pivot     = new Vector2(0f, 0.5f);
        valR.offsetMin = new Vector2(textLeftOffset, 0f);
        valR.offsetMax = new Vector2(-textRightMargin, 0f);

        counterText = valGO.AddComponent<TextMeshProUGUI>();
        counterText.text = "<color=#5CE1E6>\u00C1REA SEGURA</color>";
        counterText.fontSize = fontSize;
        counterText.fontStyle = FontStyles.Bold;
        counterText.alignment = TextAlignmentOptions.MidlineLeft;
        counterText.color = textColor;
        counterText.raycastTarget = false;
        counterText.textWrappingMode = TextWrappingModes.NoWrap;
        counterText.overflowMode = TextOverflowModes.Ellipsis;
        if (mainFont != null) counterText.font = mainFont;
    }

    // ─── Procedurais (Texturas & Sprites Sem Dependência) ─────────────────────

    private static Sprite MakeSlicedRoundedCardSprite()
    {
        int size = 64;
        int corner = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x);
                float dy = Mathf.Min(y, size - 1 - y);
                float a = 1f;

                if (dx < corner && dy < corner)
                {
                    float cx = corner - dx;
                    float cy = corner - dy;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);
                    if (dist > corner)
                        a = Mathf.Clamp01(1f - (dist - corner));
                }

                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
    }

    private static Sprite MakeSlicedBorderSprite()
    {
        int size = 64;
        int corner = 16;
        int borderWidth = 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x);
                float dy = Mathf.Min(y, size - 1 - y);
                float a = 1f;

                if (dx < corner && dy < corner)
                {
                    float cx = corner - dx;
                    float cy = corner - dy;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);
                    if (dist > corner)
                        a = Mathf.Clamp01(1f - (dist - corner));
                    else if (dist < corner - borderWidth)
                        a = 0f;
                }
                else
                {
                    bool isBorder = (x < borderWidth || x >= size - borderWidth || y < borderWidth || y >= size - borderWidth);
                    if (!isBorder) a = 0f;
                }

                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
    }

    private static Sprite MakeCircleSprite(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[res * res];
        float c = (res - 1) / 2f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - (d - (c - 1.2f)));
                px[y * res + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }

    private IEnumerator PulseAnim()
    {
        Vector3 orig = Vector3.one;
        Vector3 target = Vector3.one * pulseScale;
        float elapsed = 0f;

        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pulseDuration;
            if (containerRect != null)
                containerRect.localScale = Vector3.Lerp(orig, target, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        if (containerRect != null)
            containerRect.localScale = Vector3.one;
        pulseRoutine = null;
    }
}
