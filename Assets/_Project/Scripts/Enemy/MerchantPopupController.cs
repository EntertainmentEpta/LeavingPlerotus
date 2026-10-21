using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador do HUD do Mercador das Sombras.
///
/// RECURSOS:
/// 1. Layout Lateral: Rosto na direita (300x300+), texto à esquerda.
/// 2. Distorção da Borda do Rosto: Outline vibra e cresce na tela, oscilando predominantemente no eixo X.
/// 3. Tradução Seletiva de Fontes:
///    - Palavras TRADUZIDAS: Ficam na fonte padrão legível (LiberationSans SDF). NÃO mudam de fonte.
///    - Palavras NÃO TRADUZIDAS: Ficam nas fontes esquisitas (RubikGlitch, Nosifer, etc.) em glitch vermelho.
/// 4. Slider de Teste (0 a 20): 0 = normal (SaveGame); 1 a 20 = força nível de teste ao vivo!
/// </summary>
public class MerchantPopupController : MonoBehaviour
{
    private static MerchantPopupController _instance;
    public static MerchantPopupController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MerchantPopupController>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MerchantPopupController_Auto");
                    _instance = go.AddComponent<MerchantPopupController>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("🎭 Retrato do Mercador (Colado na Direita, 10x Maior)")]
    [Tooltip("Imagem recortada do rosto do Mercador")]
    public Sprite merchantPortraitSprite;
    [Tooltip("Posição do rosto colado no canto superior direito")]
    public Vector2 facePosition = new Vector2(-20f, -288.4f);
    [Tooltip("Tamanho imponente do rosto")]
    public Vector2 faceSize = new Vector2(300f, 300f);
    public bool showFaceOutline = true;
    public Color faceOutlineColor = new Color(0.9f, 0.12f, 0.20f, 0.95f);
    public Vector2 faceOutlineDistance = new Vector2(6f, 1f);

    [Header("👻 Distorção da Borda do Rosto (Efeito na Tela)")]
    [Tooltip("Ativa a pulsação viva da borda vermelha distorcendo a tela")]
    public bool enableOutlineGlitch = true;
    [Tooltip("Velocidade da distorção da aura")]
    [Range(1f, 15f)] public float outlinePulseSpeed = 3.5f;
    [Tooltip("Amplitude de variação no eixo X (limitada a no máximo 12f)")]
    [Range(1f, 12f)] public float outlineAmplitudeX = 12f;
    [Tooltip("Amplitude de variação no eixo Y (movimento vertical sutil)")]
    [Range(0f, 15f)] public float outlineAmplitudeY = 3.5f;

    [Header("💬 Texto do Diálogo (Posicionado à Esquerda do Rosto)")]
    [Tooltip("Posição da caixa de texto à esquerda do rosto")]
    public Vector2 textPosition = new Vector2(-360f, -380f);
    [Tooltip("Largura máxima da caixa de fala")]
    public float textWidth = 500f;
    [Tooltip("Tamanho da fonte da fala do Mercador")]
    [Range(14f, 36f)] public float dialogueFontSize = 22f;
    [Tooltip("Tamanho da fonte do badge")]
    [Range(10f, 24f)] public float badgeFontSize = 14f;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.TopRight;

    [Header("⌨️ Efeito Typewriter (Cadenciado Palavra por Palavra)")]
    public bool enableTypewriter = true;
    [Tooltip("Intervalo entre palavras no typewriter (maior = mais lento e cadenciado)")]
    [Range(0.1f, 2.0f)] public float wordRevealSpeed = 1.0f;

    [Header("🔤 Fonte Base Limpa (Palavras Traduzidas NUNCA Mudam de Fonte)")]
    [Tooltip("Fonte estática e legível usada para as palavras traduzidas (ex: LiberationSans SDF)")]
    public TMP_FontAsset cleanFontAsset;

    [Header("⚡ Fontes Ocultas e Alternância")]
    public List<TMP_FontAsset> occultFonts = new List<TMP_FontAsset>();
    [Tooltip("Intervalo entre a troca das fontes místicas e runas (em segundos; maior = mais lento e calmo)")]
    [Range(0.1f, 3.0f)] public float fontShiftInterval = 0.45f;

    [Header("🧪 Teste / Debug no Inspector (Prévia dos 20 Encontros)")]
    [Tooltip("-1 = 0% Decodificado (Enigma Puro); 0 = Normal (SaveGame); 1 a 20 = Força encontro de 5% a 100%!")]
    [Range(-1, 20)] public int debugEncounterCount = 0;
    public bool useDebugEncounter => debugEncounterCount > 0;

    [Header("UI References")]
    public GameObject popupUI;
    public Image merchantFaceImage;
    public Outline faceOutline;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI titleBadgeText;

    private RectTransform panelRect;
    private RectTransform faceRect;
    private RectTransform titleRect;
    private RectTransform textRect;

    private Coroutine typewriterGlitchRoutine;
    private Coroutine hideRoutine;
    private string currentRawSpeech = "";
    private int currentDecryptionPercent = 10;
    private bool requestImmediateTextUpdate = false;

    // Caracteres alienígenas enigmáticos para palavras não traduzidas
    private static readonly string occultCharPool = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+=-~<>?/[]{}§≈ø∂çµ∆";
    private static readonly string[] darkRedColors = new string[]
    {
        "#FF2233", "#CC0011", "#990000", "#770000", "#DD1122", "#880011", "#AA0515", "#FF4455", "#660000"
    };

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        LoadOccultFontsIfEmpty();
        EnsureUIExists();
    }

    void Update()
    {
        // Animação viva de distorção mística da borda (predominantemente no eixo X)
        if (popupUI != null && popupUI.activeSelf && faceOutline != null && showFaceOutline && enableOutlineGlitch)
        {
            float t = Time.unscaledTime * outlinePulseSpeed;

            // Distorção predominantemente no eixo X: cresce e diminui como se estivesse mexendo na tela (limitada a no máximo 12f)
            float waveX = Mathf.Abs(Mathf.Sin(t)) * outlineAmplitudeX;
            float waveY = Mathf.Abs(Mathf.Cos(t * 0.85f)) * outlineAmplitudeY;

            faceOutline.effectDistance = new Vector2(faceOutlineDistance.x + waveX, faceOutlineDistance.y + waveY);

            // Micro-vibração orgânica na tela para parecer que o Mercador está interferindo na interface física
            if (faceRect != null)
            {
                float jitterX = Mathf.Sin(t * 4.2f) * 1.8f;
                float jitterY = Mathf.Cos(t * 2.1f) * 0.9f;
                faceRect.anchoredPosition = new Vector2(facePosition.x + jitterX, facePosition.y + jitterY);
            }
        }
    }

    void OnValidate()
    {
        EnsureUIExists();
        ApplyLayoutSettings();
        requestImmediateTextUpdate = true;

        // Se estiver jogando e alterou o slider de debug, atualiza na hora com a nova porcentagem
        if (Application.isPlaying && popupUI != null && popupUI.activeSelf && debugEncounterCount != 0)
        {
            var line = MerchantEncounterDialogueSystem.GetCurrentEncounterLine();
            MostrarPopupMercador(line.merchantSpeech, line.percentageBadge, duracao: 0f);
        }
    }

    public void LoadOccultFontsIfEmpty()
    {
        if (occultFonts != null && occultFonts.Count > 0) return;

        occultFonts = new List<TMP_FontAsset>();

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset", new string[] { "Assets/_Project/NPC/Merchant/Fonts" });
        foreach (var guid in guids)
        {
            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f != null && !occultFonts.Contains(f)) occultFonts.Add(f);
        }
#endif

        if (occultFonts.Count == 0)
        {
            TMP_FontAsset[] loaded = Resources.LoadAll<TMP_FontAsset>("Fonts & Materials");
            foreach (var f in loaded)
            {
                if (f != null && !occultFonts.Contains(f) && !f.name.Contains("Liberation"))
                {
                    occultFonts.Add(f);
                }
            }
        }
    }

    public void EnsureUIExists()
    {
        if (popupUI != null) return;

        LoadOccultFontsIfEmpty();

        // 1. Canvas dedicado para o HUD do Mercador
        GameObject canvasGo = new GameObject("MerchantPopupHUD_Canvas");
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 2. Container geral transparente ancorado no topo direito
        popupUI = new GameObject("MerchantPopupRoot");
        popupUI.transform.SetParent(canvasGo.transform, false);
        panelRect = popupUI.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1400f, 700f);

        // 3. Imagem do Rosto do Mercador (COLADO NA DIREITA, 10x MAIOR)
        GameObject faceGo = new GameObject("MerchantFaceAvatar");
        faceGo.transform.SetParent(popupUI.transform, false);
        faceRect = faceGo.AddComponent<RectTransform>();
        faceRect.anchorMin = new Vector2(1f, 1f);
        faceRect.anchorMax = new Vector2(1f, 1f);
        faceRect.pivot = new Vector2(1f, 1f);
        faceRect.anchoredPosition = facePosition;
        faceRect.sizeDelta = faceSize;

        merchantFaceImage = faceGo.AddComponent<Image>();
        merchantFaceImage.preserveAspect = true;
        merchantFaceImage.raycastTarget = false;

        Sprite portrait = ResolveMerchantPortrait();
        if (portrait != null)
        {
            merchantFaceImage.sprite = portrait;
            merchantFaceImage.color = Color.white;
        }
        else
        {
            merchantFaceImage.color = new Color(0.7f, 0.15f, 0.20f, 0.95f);
        }

        faceOutline = faceGo.AddComponent<Outline>();
        faceOutline.effectColor = faceOutlineColor;
        faceOutline.effectDistance = faceOutlineDistance;
        faceOutline.enabled = showFaceOutline;

        // 4. Badge de Decodificação (Posicionado à esquerda do rosto, acima do texto)
        GameObject titleGo = new GameObject("DecryptionBadge");
        titleGo.transform.SetParent(popupUI.transform, false);
        titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(1f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(textPosition.x, textPosition.y + 30f);
        titleRect.sizeDelta = new Vector2(textWidth, 24f);

        titleBadgeText = titleGo.AddComponent<TextMeshProUGUI>();
        titleBadgeText.fontSize = badgeFontSize;
        titleBadgeText.fontStyle = FontStyles.Bold;
        titleBadgeText.alignment = textAlignment;
        titleBadgeText.color = new Color(1f, 0.35f, 0.40f, 1f);
        titleBadgeText.text = "MERCADOR DAS SOMBRAS";
        titleBadgeText.raycastTarget = false;

        // 5. Texto de Fala (Posicionado à ESQUERDA do rosto) — PURE TEXT, SEM FUNDO
        GameObject textGo = new GameObject("ConversationalSpeechText");
        textGo.transform.SetParent(popupUI.transform, false);
        textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(1f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(1f, 1f);
        textRect.anchoredPosition = textPosition;
        textRect.sizeDelta = new Vector2(textWidth, 320f);

        dialogueText = textGo.AddComponent<TextMeshProUGUI>();
        dialogueText.fontSize = dialogueFontSize;
        dialogueText.fontStyle = FontStyles.Bold;
        dialogueText.color = new Color(0.96f, 0.93f, 0.93f, 1f);
        dialogueText.alignment = textAlignment;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.lineSpacing = 10f;
        dialogueText.raycastTarget = false;

        // Garante fonte base limpa para palavras traduzidas
        if (cleanFontAsset == null)
        {
            cleanFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (cleanFontAsset == null) cleanFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            if (cleanFontAsset == null && TMP_Settings.defaultFontAsset != null) cleanFontAsset = TMP_Settings.defaultFontAsset;
        }
        if (cleanFontAsset != null) dialogueText.font = cleanFontAsset;

        popupUI.SetActive(false);
    }

    public void ApplyLayoutSettings()
    {
        if (faceRect != null)
        {
            faceRect.anchoredPosition = facePosition;
            faceRect.sizeDelta = faceSize;
        }

        if (faceOutline != null)
        {
            faceOutline.enabled = showFaceOutline;
            faceOutline.effectColor = faceOutlineColor;
            faceOutline.effectDistance = faceOutlineDistance;
        }

        if (titleRect != null)
        {
            titleRect.anchoredPosition = new Vector2(textPosition.x, textPosition.y + 30f);
            titleRect.sizeDelta = new Vector2(textWidth, 24f);
        }

        if (titleBadgeText != null)
        {
            titleBadgeText.fontSize = badgeFontSize;
            titleBadgeText.alignment = textAlignment;
        }

        if (textRect != null)
        {
            textRect.anchoredPosition = textPosition;
            textRect.sizeDelta = new Vector2(textWidth, 320f);
        }

        if (dialogueText != null)
        {
            dialogueText.fontSize = dialogueFontSize;
            dialogueText.alignment = textAlignment;
        }
    }

    /// <summary>
    /// Exibe a fala do Mercador com efeito typewriter cadenciado e tradução seletiva de fontes.
    /// </summary>
    public void MostrarPopupMercador(string fala, string badgePercentual = "", float duracao = 0f)
    {
        EnsureUIExists();
        ApplyLayoutSettings();

        if (popupUI == null || dialogueText == null) return;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        // Garante o Sprite do rosto atualizado
        if (merchantFaceImage != null)
        {
            if (merchantPortraitSprite != null)
            {
                merchantFaceImage.sprite = merchantPortraitSprite;
                merchantFaceImage.color = Color.white;
            }
            else if (merchantFaceImage.sprite == null)
            {
                Sprite resolved = ResolveMerchantPortrait();
                if (resolved != null)
                {
                    merchantFaceImage.sprite = resolved;
                    merchantFaceImage.color = Color.white;
                }
            }
        }

        // Garante a fonte limpa vinculada ao TextMeshPro
        if (cleanFontAsset == null)
        {
            cleanFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (cleanFontAsset == null) cleanFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            if (cleanFontAsset == null && TMP_Settings.defaultFontAsset != null) cleanFontAsset = TMP_Settings.defaultFontAsset;
        }
        if (dialogueText != null && cleanFontAsset != null && dialogueText.font != cleanFontAsset)
        {
            dialogueText.font = cleanFontAsset;
        }

        // Se o slider de debug for != 0, força o encontro selecionado (-1 = 0%, 1 a 20 = 5% a 100%)
        if (debugEncounterCount == -1)
        {
            currentDecryptionPercent = 0;
            badgePercentual = "0% DECODIFICADO";
        }
        else if (debugEncounterCount > 0)
        {
            currentDecryptionPercent = Mathf.Clamp(debugEncounterCount * 5, 5, 100);
            badgePercentual = $"{currentDecryptionPercent}% DECODIFICADO";
        }
        else
        {
            currentDecryptionPercent = ExtractDecryptionPercentage(badgePercentual);
        }

        if (titleBadgeText != null)
        {
            titleBadgeText.text = string.IsNullOrEmpty(badgePercentual)
                ? "<color=#FF4455>MERCADOR DAS SOMBRAS</color>"
                : $"<color=#FF4455>MERCADOR</color> <color=#4DEEEA>[{badgePercentual}]</color>";
        }

        currentRawSpeech = fala;
        popupUI.SetActive(true);

        if (typewriterGlitchRoutine != null) StopCoroutine(typewriterGlitchRoutine);
        typewriterGlitchRoutine = StartCoroutine(AnimateTypewriterAndGlitch(duracao));
    }

    private int ExtractDecryptionPercentage(string badge)
    {
        if (string.IsNullOrEmpty(badge)) return 5;
        try
        {
            int idx = badge.IndexOf('%');
            if (idx > 0)
            {
                int start = idx - 1;
                while (start >= 0 && char.IsDigit(badge[start])) start--;
                string numStr = badge.Substring(start + 1, idx - (start + 1));
                if (int.TryParse(numStr, out int val)) return Mathf.Clamp(val, 5, 100);
            }
        }
        catch { }
        return 5;
    }

    /// <summary>
    /// Anima o surgimento palavra por palavra com intervalo deliberado estilo Eptinho.
    /// Palavras traduzidas usam fonte limpa fixa.
    /// Palavras não traduzidas alternam as fontes místicas em glitch vermelho.
    /// </summary>
    private IEnumerator AnimateTypewriterAndGlitch(float duracao)
    {
        string[] allWords = currentRawSpeech.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int revealedCount = enableTypewriter ? 0 : allWords.Length;

        int fontIdx = 0;
        float startTime = Time.time;
        float typewriterTimer = 0f;
        float fontShiftTimer = 0f;

        // Renderiza o primeiro estado imediatamente
        TMP_FontAsset occultFontToUse = (occultFonts != null && occultFonts.Count > 0) ? occultFonts[0] : null;
        if (dialogueText != null)
        {
            dialogueText.text = BuildConversationalSpeech(allWords, revealedCount, currentDecryptionPercent, occultFontToUse);
        }

        while (popupUI != null && popupUI.activeSelf)
        {
            float dt = Time.unscaledDeltaTime;
            typewriterTimer += dt;
            fontShiftTimer += dt;

            bool needsTextUpdate = false;

            // 1. Typewriter: revela palavra por palavra com intervalo deliberado
            float currentWordSpeed = Mathf.Max(0.05f, wordRevealSpeed);
            if (enableTypewriter && revealedCount < allWords.Length)
            {
                if (typewriterTimer >= currentWordSpeed)
                {
                    typewriterTimer = 0f;
                    revealedCount++;
                    needsTextUpdate = true;
                }
            }
            else if (!enableTypewriter && revealedCount != allWords.Length)
            {
                revealedCount = allWords.Length;
                needsTextUpdate = true;
            }

            // 2. Troca da fonte mística das palavras não traduzidas (ritmo cadenciado e controlado)
            float currentShiftInterval = Mathf.Max(0.05f, fontShiftInterval);
            if (fontShiftTimer >= currentShiftInterval)
            {
                fontShiftTimer = 0f;
                fontIdx++;
                needsTextUpdate = true;
            }

            // Se houve alteração no Inspector, força atualização imediata
            if (requestImmediateTextUpdate)
            {
                requestImmediateTextUpdate = false;
                needsTextUpdate = true;
            }

            // 3. Monta o texto SOMENTE quando o temporizador disparar ou uma nova palavra for revelada!
            // Isso impede que os caracteres alienígenas mutem 60 a 144 vezes por segundo a cada frame,
            // garantindo que a velocidade configurada no Inspector funcione com precisão absoluta.
            if (needsTextUpdate && dialogueText != null)
            {
                if (occultFonts != null && occultFonts.Count > 0)
                {
                    occultFontToUse = occultFonts[fontIdx % occultFonts.Count];
                }

                dialogueText.text = BuildConversationalSpeech(allWords, revealedCount, currentDecryptionPercent, occultFontToUse);
            }

            if (duracao > 0f && revealedCount >= allWords.Length && (Time.time - startTime) >= duracao)
            {
                break;
            }

            yield return null;
        }

        if (duracao > 0f)
        {
            EsconderPopup();
        }
        typewriterGlitchRoutine = null;
    }

    /// <summary>
    /// Monta o texto aplicando a regra exata de decodificação:
    /// - Palavras Traduzidas: Fonte padrão/limpa (LiberationSans SDF), NUNCA mudam de fonte, brancas e legíveis.
    /// - Palavras NÃO Traduzidas: Fontes místicas (RubikGlitch, Nosifer, etc.), mutação de caracteres e vermelho escuro.
    /// </summary>
    private string BuildConversationalSpeech(string[] words, int revealedCount, int decryptionPercent, TMP_FontAsset occultFont)
    {
        if (words == null || words.Length == 0 || revealedCount <= 0) return "";

        int count = Mathf.Min(revealedCount, words.Length);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string occultFontName = (occultFont != null) ? occultFont.name : "RubikGlitch-Regular SDF";

        for (int i = 0; i < count; i++)
        {
            string w = words[i];

            // Determina se esta palavra específica já foi traduzida
            bool isDecoded = false;
            if (decryptionPercent <= 0)
            {
                isDecoded = false;
            }
            else if (decryptionPercent >= 100)
            {
                isDecoded = true;
            }
            else
            {
                // Limpa asteriscos e pontuações para hash determinístico e consistente
                string cleanWord = w.Trim('*', '.', ',', '!', '?', ';', ':', '-', '"');
                int hash = Mathf.Abs((cleanWord.GetHashCode() + (i * 31))) % 100;
                isDecoded = hash < decryptionPercent;
            }

            if (isDecoded)
            {
                // PALAVRA TRADUZIDA:
                // NÃO usa tag <font>! Como o componente dialogueText.font já é a fonte limpa (LiberationSans SDF),
                // a palavra traduzida fica garantidamente limpa, estática, em branco/destaque, 100% legível!
                string lower = w.ToLower();
                if (lower.Contains("sangue") || lower.Contains("silício") || lower.Contains("morte") || 
                    lower.Contains("plerotus") || lower.Contains("parasita") || lower.Contains("lapidadores"))
                {
                    sb.Append($"<color=#FF3344><b>{w}</b></color> ");
                }
                else if (w.Contains("*"))
                {
                    sb.Append($"<color=#FFAAAA><i>{w}</i></color> ");
                }
                else
                {
                    sb.Append($"<color=#FFFFFF><b>{w}</b></color> ");
                }
            }
            else
            {
                // PALAVRA NÃO TRADUZIDA:
                // Usa a fonte mística mutante (Rubik Glitch, Nosifer, etc.) com cor carmesim e caracteres mutantes!
                string col = darkRedColors[UnityEngine.Random.Range(0, darkRedColors.Length)];
                string scrambled = ScrambleWordCharacters(w);
                sb.Append($"<font=\"{occultFontName}\"><color={col}>{scrambled}</color></font> ");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private string ScrambleWordCharacters(string originalWord)
    {
        if (originalWord.StartsWith("*") && originalWord.EndsWith("*")) return originalWord;

        int len = Mathf.Max(2, originalWord.Length);
        System.Text.StringBuilder sb = new System.Text.StringBuilder(len);
        for (int i = 0; i < len; i++)
        {
            sb.Append(occultCharPool[UnityEngine.Random.Range(0, occultCharPool.Length)]);
        }
        return sb.ToString();
    }

    public void EsconderPopup()
    {
        if (typewriterGlitchRoutine != null)
        {
            StopCoroutine(typewriterGlitchRoutine);
            typewriterGlitchRoutine = null;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (popupUI != null)
        {
            popupUI.SetActive(false);
        }
    }

    [ContextMenu("Testar Fala do Encontro no Editor")]
    public void TestarEncontroAtual()
    {
        if (debugEncounterCount == 0) debugEncounterCount = 1;
        var line = MerchantEncounterDialogueSystem.GetCurrentEncounterLine();
        MostrarPopupMercador(line.merchantSpeech, line.percentageBadge, duracao: 0f);
    }

    public void SetMerchantPortrait(Sprite sprite)
    {
        merchantPortraitSprite = sprite;
        if (merchantFaceImage != null && sprite != null)
        {
            merchantFaceImage.sprite = sprite;
            merchantFaceImage.color = Color.white;
        }
    }

    private Sprite ResolveMerchantPortrait()
    {
        if (merchantPortraitSprite != null) return merchantPortraitSprite;

        Sprite loaded = Resources.Load<Sprite>("merchant face");
        if (loaded == null) loaded = Resources.Load<Sprite>("MerchantFace");
        if (loaded != null) return loaded;

#if UNITY_EDITOR
        Sprite edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/NPC/Merchant/merchant face.png");
        if (edSprite != null) return edSprite;

        var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/NPC/Merchant/merchant face.png");
        if (subAssets != null)
        {
            foreach (var asset in subAssets)
            {
                if (asset is Sprite s) return s;
            }
        }
#endif

        try
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project/NPC/Merchant/merchant face.png");
            if (System.IO.File.Exists(path))
            {
                byte[] rawData = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(rawData))
                {
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[MerchantPopupController] Não foi possível carregar merchant face.png: " + ex.Message);
        }

        return null;
    }
}
