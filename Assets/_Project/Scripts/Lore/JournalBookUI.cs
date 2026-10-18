using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Interface do Caderno do Pesquisador (Codex / Diário de Campo de Plerotus).
/// - Abre ao interagir com o livro flutuante na Base (estilo Minecraft Enchantment Table).
/// - Exibe páginas duplas (Left Page: Crônica/Lore, Right Page: Esquema/Blueprint).
/// - Folhas em branco / rasgadas para páginas ainda não descobertas no mapa.
/// - Sistema de troca de páginas com som de papel (card.wav) e animação suave.
/// - Barra superior exibindo porcentagem de progresso de páginas encontradas.
/// </summary>
public class JournalBookUI : MonoBehaviour
{
    private static JournalBookUI _instance;
    public static JournalBookUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<JournalBookUI>();
                if (_instance == null)
                {
                    CreateOrGet();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Sprites Personalizáveis (Fornecidos pelo Artista)")]
    [Tooltip("Capa de couro aberta ao fundo")]
    public Sprite binderCoverSprite;

    [Tooltip("Folha em branco de caderno (papel envelhecido com grade técnica)")]
    public Sprite blankPageSprite;

    [Tooltip("Folha preenchida colada por cima quando a página é descoberta")]
    public Sprite discoveredPageOverlaySprite;

    [Header("Áudio")]
    public AudioClip pageFlipSound;
    public AudioClip bookOpenSound;
    public AudioClip bookCloseSound;
    [Range(0f, 1f)] public float soundVolume = 0.85f;

    [Header("Navegação")]
    public int currentPageIndex = 0; // 0 a 23 (24 escrituras)

    // UI References
    private GameObject canvasObj;
    private Canvas journalCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform bookContainerRect;
    private Image binderCoverImg;
    private Image pagesSpreadImg;
    private Image leftPageBgImg;
    private Image rightPageBgImg;
    private Image rightPageIllustrationImg;

    // Textos da Página Esquerda (Lore / Crônica)
    private TextMeshProUGUI leftPageHeaderTMP;
    private TextMeshProUGUI leftPageTitleTMP;
    private TextMeshProUGUI leftPageBodyTMP;
    private TextMeshProUGUI leftPageStatusTMP;

    // Textos da Página Direita (Blueprint / Anotações)
    private TextMeshProUGUI rightPageHeaderTMP;
    private TextMeshProUGUI rightPageTitleTMP;
    private TextMeshProUGUI rightPageBodyTMP;
    private TextMeshProUGUI rightPageRecipeTMP;
    private TextMeshProUGUI rightPageStatusTMP;

    // Cabeçalho e Rodapé
    private TextMeshProUGUI progressHeaderTMP;
    private TextMeshProUGUI pageNumberFooterTMP;
    private Button prevButton;
    private Button nextButton;
    private Button closeButton;

    private TMP_FontAsset customFont;
    private AudioSource audioSource;
    private bool isOpen = false;
    public bool IsOpen => isOpen;

    public static JournalBookUI CreateOrGet()
    {
        if (_instance != null) return _instance;

        GameObject root = new GameObject("JournalBookUI_Canvas");
        _instance = root.AddComponent<JournalBookUI>();
        DontDestroyOnLoad(root);
        _instance.BuildUI();
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-carrega sprites estilizados do Caderno (Resources/Lore/UI) se não foram setados manualmente
        if (binderCoverSprite == null) binderCoverSprite = Resources.Load<Sprite>("Lore/UI/Journal_Binder_Cover");
        if (blankPageSprite == null) blankPageSprite = Resources.Load<Sprite>("Lore/UI/Journal_Blank_Pages");
        if (discoveredPageOverlaySprite == null) discoveredPageOverlaySprite = Resources.Load<Sprite>("Lore/UI/Journal_Page_Schematics");

        customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Tenta carregar sons padrões
        pageFlipSound = Resources.Load<AudioClip>("SoundFX/Player/card") ?? Resources.Load<AudioClip>("card");
        bookOpenSound = Resources.Load<AudioClip>("SoundFX/Player/InventoryOpen") ?? Resources.Load<AudioClip>("InventoryOpen");
        bookCloseSound = Resources.Load<AudioClip>("SoundFX/Player/InventoryClose") ?? Resources.Load<AudioClip>("InventoryClose");

        BuildUI();
    }

    void Start()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (canvasObj != null) canvasObj.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;

        // Teclas de atalho para virar página
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q))
        {
            PreviousPage();
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E))
        {
            NextPage();
        }

        // Fechar
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            CloseJournal();
        }
    }

    public void OpenJournal()
    {
        isOpen = true;
        UpdateCanvasCamera();
        if (canvasObj != null) canvasObj.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        PlayAudio(bookOpenSound);
        RefreshCurrentPage();
    }

    public void CloseJournal()
    {
        isOpen = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        PlayAudio(bookCloseSound);
        if (canvasObj != null) canvasObj.SetActive(false);
    }

    public void NextPage()
    {
        int maxPages = GetTotalPages();
        if (currentPageIndex < maxPages - 1)
        {
            currentPageIndex++;
            PlayAudio(pageFlipSound);
            RefreshCurrentPage();
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            PlayAudio(pageFlipSound);
            RefreshCurrentPage();
        }
    }

    private int GetTotalPages()
    {
        return LoreManager.Instance != null && LoreManager.Instance.allScriptures.Count > 0
            ? LoreManager.Instance.allScriptures.Count
            : 24;
    }

    public void RefreshCurrentPage()
    {
        if (LoreManager.Instance == null) return;

        int total = LoreManager.Instance.allScriptures.Count;
        if (total == 0) return;

        if (currentPageIndex < 0) currentPageIndex = 0;
        if (currentPageIndex >= total) currentPageIndex = total - 1;

        LoreScriptureData scripture = LoreManager.Instance.allScriptures[currentPageIndex];
        bool isDiscovered = LoreManager.Instance.IsScriptureDiscovered(scripture.scriptureId);

        // Atualiza Barra Superior de Progresso
        int discoveredCount = LoreManager.Instance.DiscoveredScripturesCount;
        float percent = LoreManager.Instance.CompletionPercentage;
        if (progressHeaderTMP != null)
        {
            progressHeaderTMP.text = $"📖 <color=#FFE500>CADERNO DO PESQUISADOR</color>  —  RECUPERAÇÃO: <color=#4DEEEA><b>{percent:F0}%</b></color>  ({discoveredCount}/{total} PÁGINAS)";
        }

        // Atualiza Rodapé de Navegação
        if (pageNumberFooterTMP != null)
        {
            pageNumberFooterTMP.text = $"PÁGINA {currentPageIndex + 1} DE {total}  •  [A / D] FOLHEAR";
        }

        if (prevButton != null) prevButton.interactable = (currentPageIndex > 0);
        if (nextButton != null) nextButton.interactable = (currentPageIndex < total - 1);

        // ── PÁGINA ESQUERDA: CRÔNICA / REGISTRO (Tinta sobre Pergaminho) ──
        if (isDiscovered)
        {
            if (leftPageHeaderTMP != null)
            {
                leftPageHeaderTMP.text = scripture.hasBlueprint
                    ? "⚙ <color=#006E7F>[PROJETO DE ENGENHARIA]</color>"
                    : "📜 <color=#8B2500>[CRÔNICA DE CAMPO]</color>";
            }

            if (leftPageTitleTMP != null)
            {
                leftPageTitleTMP.text = scripture.title.ToUpper();
                leftPageTitleTMP.color = new Color(0.12f, 0.10f, 0.08f);
            }

            if (leftPageBodyTMP != null)
            {
                leftPageBodyTMP.text = $"{scripture.excerptLore}\n\n<size=85%><color=#4A3C2C><i>\"Notas recuperadas dos escombros minerais de Plerotus.\"</i></color></size>";
                leftPageBodyTMP.color = new Color(0.20f, 0.16f, 0.12f);
            }

            if (leftPageStatusTMP != null)
            {
                leftPageStatusTMP.text = "✔ <color=#006B44>REGISTRO INTEGRADO AO SISTEMA</color>";
            }
        }
        else
        {
            // Página Não Descoberta (Em Branco / Rasgada - estilo grafite suave)
            if (leftPageHeaderTMP != null) leftPageHeaderTMP.text = "<color=#7A7065>[PÁGINA RASGADA / NÃO RECUPERADA]</color>";
            if (leftPageTitleTMP != null)
            {
                leftPageTitleTMP.text = "??? PÁGINA NÃO RECUPERADA";
                leftPageTitleTMP.color = new Color(0.55f, 0.50f, 0.45f);
            }
            if (leftPageBodyTMP != null)
            {
                leftPageBodyTMP.text = "<color=#6B6258><i>As páginas deste capítulo foram arrancadas ou perdidas nas profundezas de Plerotus.\n\nExplore as galerias, becos sem saída e salas de anomalia durante suas expedições para recuperar este fragmento.</i></color>";
            }
            if (leftPageStatusTMP != null)
            {
                leftPageStatusTMP.text = "✖ <color=#8C4040>FRAGMENTO NÃO CATALOGADO</color>";
            }
        }

        // ── PÁGINA DIREITA: BLUEPRINT OU ESBOÇOS TÉCNICOS ──
        if (isDiscovered)
        {
            if (scripture.hasBlueprint)
            {
                bool bpUnlocked = LoreManager.Instance.IsBlueprintUnlocked(scripture.blueprintEquipmentId);

                if (rightPageHeaderTMP != null) rightPageHeaderTMP.text = "🛠 <color=#006E7F>[ESQUEMA DO ROBOZINHO]</color>";
                if (rightPageTitleTMP != null)
                {
                    rightPageTitleTMP.text = scripture.blueprintTitle.ToUpper();
                    rightPageTitleTMP.color = new Color(0.08f, 0.35f, 0.45f);
                }

                if (rightPageBodyTMP != null)
                {
                    rightPageBodyTMP.text = $"<b>EFEITO TÁTICO:</b>\n{scripture.blueprintDescription}";
                    rightPageBodyTMP.color = new Color(0.20f, 0.16f, 0.12f);
                }

                if (rightPageIllustrationImg != null)
                {
                    rightPageIllustrationImg.gameObject.SetActive(true);
                    if (discoveredPageOverlaySprite != null) rightPageIllustrationImg.sprite = discoveredPageOverlaySprite;
                }

                if (rightPageRecipeTMP != null)
                {
                    string rec = "<b>MATERIAIS REQUERIDOS:</b>\n";
                    if (scripture.ingredients != null && scripture.ingredients.Count > 0)
                    {
                        foreach (var ing in scripture.ingredients)
                        {
                            rec += $" • {ing.itemId} x{ing.quantity}\n";
                        }
                    }
                    else
                    {
                        rec += " • Síntese direta no Robozinho\n";
                    }
                    rightPageRecipeTMP.text = rec;
                    rightPageRecipeTMP.color = new Color(0.12f, 0.28f, 0.18f);
                }

                if (rightPageStatusTMP != null)
                {
                    rightPageStatusTMP.text = bpUnlocked
                        ? "✔ <color=#006B44>RECEITA DESBLOQUEADA NA MESA</color>"
                        : "● <color=#8B5A00>DISPONÍVEL PARA SÍNTESE</color>";
                }
            }
            else
            {
                // Lore pura (anotações de sobrevivência)
                if (rightPageHeaderTMP != null) rightPageHeaderTMP.text = "🔍 <color=#8B5A00>[OBSERVAÇÃO AMBIENTAL]</color>";
                if (rightPageTitleTMP != null)
                {
                    rightPageTitleTMP.text = "DADOS DA EXPEDIÇÃO";
                    rightPageTitleTMP.color = new Color(0.22f, 0.18f, 0.14f);
                }
                if (rightPageBodyTMP != null)
                {
                    rightPageBodyTMP.text = "<i>\"O parasita consome os minerais orgânicos e replica padrões geométricos nas carapaças das criaturas. Cada criatura abatida devolve à terra os elementos catalisadores para novas aberrações.\"</i>\n\n<b>Classificação:</b> Bioma Sílica / Plerotus Profundo.";
                    rightPageBodyTMP.color = new Color(0.20f, 0.16f, 0.12f);
                }

                if (rightPageIllustrationImg != null)
                {
                    rightPageIllustrationImg.gameObject.SetActive(false);
                }

                if (rightPageRecipeTMP != null)
                {
                    rightPageRecipeTMP.text = "<b>DICA DE SOBREVIVÊNCIA:</b>\nEquipe armas adaptadas e conserve fluido vital contra os perigos biológicos.";
                    rightPageRecipeTMP.color = new Color(0.25f, 0.22f, 0.18f);
                }
                if (rightPageStatusTMP != null)
                {
                    rightPageStatusTMP.text = "✔ <color=#8B5A00>ARQUIVADO NO CÓDICE</color>";
                }
            }
        }
        else
        {
            // Página Direita Vazia
            if (rightPageHeaderTMP != null) rightPageHeaderTMP.text = "<color=#7A7065>[DIAGRAMA AUSENTE]</color>";
            if (rightPageTitleTMP != null)
            {
                rightPageTitleTMP.text = "ESQUEMA DESCONHECIDO";
                rightPageTitleTMP.color = new Color(0.55f, 0.50f, 0.45f);
            }
            if (rightPageBodyTMP != null)
            {
                rightPageBodyTMP.text = "<color=#6B6258><i>As ilustrações e cálculos desta página estão ilegíveis ou incompletos.\n\nEncontre a página correspondente na mina para desvendar esta tecnologia.</i></color>";
            }

            if (rightPageIllustrationImg != null)
            {
                rightPageIllustrationImg.gameObject.SetActive(false);
            }

            if (rightPageRecipeTMP != null)
            {
                rightPageRecipeTMP.text = "<color=#807870>• Dados de síntese indisponíveis</color>";
            }
            if (rightPageStatusTMP != null)
            {
                rightPageStatusTMP.text = "✖ <color=#8C4040>BLOQUEADO</color>";
            }
        }
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    // ─── CONSTRUÇÃO DINÂMICA DA UI NO CANVAS ────────────────────────────────
    public void UpdateCanvasCamera()
    {
        if (journalCanvas == null && canvasObj != null)
        {
            journalCanvas = canvasObj.GetComponent<Canvas>();
        }
        if (journalCanvas == null) return;

        journalCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        journalCanvas.sortingOrder = 750;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
        if (canvasObj != null)
        {
            SetLayerRecursively(canvasObj, uiLayer);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    private void BuildUI()
    {
        if (canvasObj != null) return;

        // Garante a existência do EventSystem para suportar cliques de mouse
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem_Auto");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(es);
            Debug.Log("[JournalBookUI] 🖱️ EventSystem auto-criado para suportar cliques no Códice!");
        }

        canvasObj = new GameObject("JournalBook_CanvasRoot");
        canvasObj.transform.SetParent(transform, false);

        journalCanvas = canvasObj.AddComponent<Canvas>();
        UpdateCanvasCamera();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        // Fundo escuro semitransparente (overlay cinematográfico)
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(canvasObj.transform, false);
        RectTransform bRect = backdrop.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        Image bImg = backdrop.AddComponent<Image>();
        bImg.color = new Color(0.02f, 0.02f, 0.04f, 0.88f);

        // Container Central do Livro (Dimensões 1376 x 768 na proporção das artes geradas)
        GameObject bookContainer = new GameObject("BookContainer");
        bookContainer.transform.SetParent(canvasObj.transform, false);
        bookContainerRect = bookContainer.AddComponent<RectTransform>();
        bookContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        bookContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        bookContainerRect.pivot = new Vector2(0.5f, 0.5f);
        bookContainerRect.sizeDelta = new Vector2(1376f, 768f);

        // 1. Capa de Couro Rústica Aberta (Fundo Base do Caderno)
        binderCoverImg = bookContainer.AddComponent<Image>();
        if (binderCoverSprite != null)
        {
            binderCoverImg.sprite = binderCoverSprite;
            binderCoverImg.color = Color.white;
        }
        else
        {
            binderCoverImg.color = new Color(0.12f, 0.10f, 0.15f, 0.98f);
        }

        // Borda dourada/latão envelhecido
        Outline outline = bookContainer.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.70f, 0.35f, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 2. Folhas Abertas de Pergaminho Técnico (Páginas Duplas com Espiral Central)
        GameObject pagesSpreadObj = new GameObject("PagesSpread");
        pagesSpreadObj.transform.SetParent(bookContainer.transform, false);
        RectTransform psRect = pagesSpreadObj.AddComponent<RectTransform>();
        psRect.anchorMin = Vector2.zero;
        psRect.anchorMax = Vector2.one;
        psRect.sizeDelta = Vector2.zero;
        pagesSpreadImg = pagesSpreadObj.AddComponent<Image>();
        if (blankPageSprite != null)
        {
            pagesSpreadImg.sprite = blankPageSprite;
            pagesSpreadImg.color = Color.white;
        }
        else
        {
            pagesSpreadImg.color = new Color(0.92f, 0.90f, 0.85f, 0.95f);
        }

        // ── BARRA SUPERIOR (Progresso e Título do Caderno) ──
        GameObject headerObj = new GameObject("HeaderProgress");
        headerObj.transform.SetParent(bookContainer.transform, false);
        RectTransform hRect = headerObj.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot = new Vector2(0.5f, 1f);
        hRect.anchoredPosition = new Vector2(0f, -22f);
        hRect.sizeDelta = new Vector2(-60f, 40f);

        progressHeaderTMP = headerObj.AddComponent<TextMeshProUGUI>();
        progressHeaderTMP.text = "📖 CADERNO DO PESQUISADOR";
        progressHeaderTMP.fontSize = 20f;
        progressHeaderTMP.fontStyle = FontStyles.Bold;
        progressHeaderTMP.alignment = TextAlignmentOptions.Center;
        progressHeaderTMP.color = Color.white;
        if (customFont != null) progressHeaderTMP.font = customFont;
        Shadow headShadow = headerObj.AddComponent<Shadow>();
        headShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        headShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // ── PÁGINA ESQUERDA: CRÔNICA / REGISTRO DE CAMPO (Dimensões: 530 x 620) ──
        GameObject leftPageObj = new GameObject("LeftPage");
        leftPageObj.transform.SetParent(bookContainer.transform, false);
        RectTransform lpRect = leftPageObj.AddComponent<RectTransform>();
        lpRect.anchorMin = new Vector2(0f, 0.5f);
        lpRect.anchorMax = new Vector2(0f, 0.5f);
        lpRect.pivot = new Vector2(0f, 0.5f);
        lpRect.anchoredPosition = new Vector2(75f, -12f);
        lpRect.sizeDelta = new Vector2(530f, 620f);

        leftPageBgImg = leftPageObj.AddComponent<Image>();
        leftPageBgImg.color = Color.clear; // O fundo de papel já vem da camada PagesSpread

        VerticalLayoutGroup lpVlg = leftPageObj.AddComponent<VerticalLayoutGroup>();
        lpVlg.padding = new RectOffset(25, 25, 25, 25);
        lpVlg.spacing = 10f;
        lpVlg.childControlWidth = true;
        lpVlg.childControlHeight = false;

        leftPageHeaderTMP = CreateTextElement(leftPageObj.transform, "Header", "📜 [CRÔNICA DE CAMPO]", 15f, FontStyles.Bold, new Color(0.55f, 0.15f, 0.00f));
        leftPageTitleTMP = CreateTextElement(leftPageObj.transform, "Title", "TÍTULO DA ESCRITURA", 21f, FontStyles.Bold, new Color(0.12f, 0.10f, 0.08f));
        leftPageBodyTMP = CreateTextElement(leftPageObj.transform, "Body", "Trecho do manuscrito...", 16f, FontStyles.Normal, new Color(0.20f, 0.16f, 0.12f));
        leftPageBodyTMP.lineSpacing = 6f;
        leftPageStatusTMP = CreateTextElement(leftPageObj.transform, "Status", "STATUS", 14f, FontStyles.Bold, new Color(0.00f, 0.42f, 0.27f));

        // ── PÁGINA DIREITA: BLUEPRINT OU ESQUEMA TÉCNICO (Dimensões: 530 x 620) ──
        GameObject rightPageObj = new GameObject("RightPage");
        rightPageObj.transform.SetParent(bookContainer.transform, false);
        RectTransform rpRect = rightPageObj.AddComponent<RectTransform>();
        rpRect.anchorMin = new Vector2(1f, 0.5f);
        rpRect.anchorMax = new Vector2(1f, 0.5f);
        rpRect.pivot = new Vector2(1f, 0.5f);
        rpRect.anchoredPosition = new Vector2(-75f, -12f);
        rpRect.sizeDelta = new Vector2(530f, 620f);

        rightPageBgImg = rightPageObj.AddComponent<Image>();
        rightPageBgImg.color = Color.clear;

        VerticalLayoutGroup rpVlg = rightPageObj.AddComponent<VerticalLayoutGroup>();
        rpVlg.padding = new RectOffset(25, 25, 25, 25);
        rpVlg.spacing = 10f;
        rpVlg.childControlWidth = true;
        rpVlg.childControlHeight = false;

        rightPageHeaderTMP = CreateTextElement(rightPageObj.transform, "Header", "🛠 [ESQUEMA TÉCNICO]", 15f, FontStyles.Bold, new Color(0.08f, 0.40f, 0.50f));
        rightPageTitleTMP = CreateTextElement(rightPageObj.transform, "Title", "PROJETO DO ROBOZINHO", 21f, FontStyles.Bold, new Color(0.12f, 0.10f, 0.08f));
        rightPageBodyTMP = CreateTextElement(rightPageObj.transform, "Body", "Efeito tático do equipamento...", 16f, FontStyles.Normal, new Color(0.20f, 0.16f, 0.12f));
        rightPageBodyTMP.lineSpacing = 6f;

        // Ilustração do Diagrama / Blueprint
        GameObject illObj = new GameObject("Illustration");
        illObj.transform.SetParent(rightPageObj.transform, false);
        RectTransform illRect = illObj.AddComponent<RectTransform>();
        illRect.sizeDelta = new Vector2(480f, 150f);
        LayoutElement illLe = illObj.AddComponent<LayoutElement>();
        illLe.minHeight = 150f;
        illLe.preferredHeight = 150f;
        illLe.flexibleWidth = 1f;
        rightPageIllustrationImg = illObj.AddComponent<Image>();
        rightPageIllustrationImg.preserveAspect = true;
        if (discoveredPageOverlaySprite != null) rightPageIllustrationImg.sprite = discoveredPageOverlaySprite;
        rightPageIllustrationImg.gameObject.SetActive(false);

        rightPageRecipeTMP = CreateTextElement(rightPageObj.transform, "Recipe", "MATERIAIS REQUERIDOS: ...", 15f, FontStyles.Normal, new Color(0.12f, 0.28f, 0.18f));
        rightPageStatusTMP = CreateTextElement(rightPageObj.transform, "Status", "STATUS RECEITA", 14f, FontStyles.Bold, new Color(0.00f, 0.42f, 0.27f));

        // ── BOTÕES DE NAVEGAÇÃO (< Anterior / Próxima >) ──
        prevButton = CreateNavButton(bookContainer.transform, "BtnPrev", "◀ ANTERIOR [A]", new Vector2(0f, 0f), new Vector2(170f, 44f), new Vector2(75f, 25f), PreviousPage);
        nextButton = CreateNavButton(bookContainer.transform, "BtnNext", "PRÓXIMA [D] ▶", new Vector2(1f, 0f), new Vector2(170f, 44f), new Vector2(-75f, 25f), NextPage);

        // ── RODAPÉ INDICADOR DE PÁGINA ──
        GameObject footerObj = new GameObject("FooterPageNum");
        footerObj.transform.SetParent(bookContainer.transform, false);
        RectTransform footRect = footerObj.AddComponent<RectTransform>();
        footRect.anchorMin = new Vector2(0.5f, 0f);
        footRect.anchorMax = new Vector2(0.5f, 0f);
        footRect.pivot = new Vector2(0.5f, 0f);
        footRect.anchoredPosition = new Vector2(0f, 25f);
        footRect.sizeDelta = new Vector2(420f, 36f);

        pageNumberFooterTMP = footerObj.AddComponent<TextMeshProUGUI>();
        pageNumberFooterTMP.text = "PÁGINA 1 DE 24";
        pageNumberFooterTMP.fontSize = 15f;
        pageNumberFooterTMP.alignment = TextAlignmentOptions.Center;
        pageNumberFooterTMP.color = new Color(1f, 0.92f, 0.75f);
        if (customFont != null) pageNumberFooterTMP.font = customFont;
        Shadow footShadow = footerObj.AddComponent<Shadow>();
        footShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        footShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // ── BOTÃO FECHAR (X) ──
        closeButton = CreateNavButton(bookContainer.transform, "BtnClose", "✕ FECHAR [ESC]", new Vector2(1f, 1f), new Vector2(150f, 38f), new Vector2(-35f, -22f), CloseJournal);

        UpdateCanvasCamera();
    }

    private TextMeshProUGUI CreateTextElement(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) tmp.font = customFont;

        ContentSizeFitter csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return tmp;
    }

    private Button CreateNavButton(Transform parent, string name, string label, Vector2 anchor, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.16f, 0.12f, 0.09f, 0.96f);

        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.70f, 0.55f, 0.30f, 0.65f);
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.16f, 0.12f, 0.09f, 0.96f);
        cb.highlightedColor = new Color(0.32f, 0.25f, 0.18f, 1f);
        cb.pressedColor = new Color(0.09f, 0.07f, 0.05f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(action);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform tr = textObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.72f);
        if (customFont != null) tmp.font = customFont;

        return btn;
    }
}
