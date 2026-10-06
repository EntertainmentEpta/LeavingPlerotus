using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// =================================================================================
/// GERENCIADOR DE POPUPS E EXPRESSÕES EMOCIONAIS DO EPTINHO
/// =================================================================================
/// Desenvolvido por: Vicenzo (Branch: VicenzoWS)
/// 
/// Funcionalidades Principais Implementadas:
/// 1. Suporte às Expressões Faciais do Eptinho: Normal (eptinhoNormalSprite), Tenso (eptinhoTensoSprite) e Medo (eptinhoMedoSprite).
/// 2. Método MostrarPopupPactoMedo() ativado automaticamente ao aceitar qualquer Pacto de Sangue do Mercador.
/// 3. Trava de Proporção Fixo (75px x 75px) no Retrato do Eptinho com remoção automática de AspectRatioFitter para evitar distorções.
/// 4. Auto-Fit Dinâmico da Caixa de Diálogo sem truncar texto nem alterar o tamanho da fonte.
/// =================================================================================
/// </summary>
public class EptinhoPopupController : MonoBehaviour
{
    private static EptinhoPopupController _instancia;
    public static EptinhoPopupController instancia
    {
        get
        {
            if (_instancia == null)
            {
                _instancia = FindFirstObjectByType<EptinhoPopupController>();
                if (_instancia == null)
                {
                    GameObject go = new GameObject("EptinhoPopupController_Auto");
                    _instancia = go.AddComponent<EptinhoPopupController>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[EPTINHO POPUP] Criado automaticamente sob demanda.");
                }
            }
            return _instancia;
        }
        private set { _instancia = value; }
    }

    public GameObject popupUI;
    public Image imagemDoItem;
    public TextMeshProUGUI textoDoItem;

    [Header("📐 Ajuste Dinâmico Automático do Painel (Auto-Fit)")]
    [Tooltip("Se verdadeiro, o painel expandirá e encolherá dinamicamente dependendo da quantidade de texto!")]
    [SerializeField] private bool autoResizePanel = true;

    [Tooltip("Largura mínima do painel (para textos curtos)")]
    [SerializeField] private float minPanelWidth = 380f;

    [Tooltip("Largura máxima do painel (para textos longos antes de quebrar linha)")]
    [SerializeField] private float maxPanelWidth = 850f;

    [Tooltip("Altura mínima do painel")]
    [SerializeField] private float minPanelHeight = 90f;

    [Tooltip("Altura máxima permitida para o painel")]
    [SerializeField] private float maxPanelHeight = 350f;

    [Tooltip("Preenchimento horizontal e vertical em volta do texto")]
    [SerializeField] private float paddingX = 25f;
    [SerializeField] private float paddingY = 18f;

    [Header("Ajustes Manuais do Painel")]
    [Range(200f, 1000f)] [SerializeField] private float panelWidth = 650f;
    [Range(50f, 400f)] [SerializeField] private float panelHeight = 120f;
    [SerializeField] private float marginX = -40f;
    [SerializeField] private float marginY = -40f;
    
    [Space(5)]
    [SerializeField] private Color panelColor = new Color(0.23f, 0.28f, 0.70f, 0.5f);
    [SerializeField] private Color borderColor = new Color(0.52f, 0.45f, 0.15f, 0.5f);
    [Range(0f, 40f)] [SerializeField] private float borderThickness = 20f;

    [Header("🎭 Sprites de Expressão do Eptinho")]
    [Tooltip("Sprite padrão do Eptinho (EPTONHO)")]
    [SerializeField] public Sprite eptinhoNormalSprite;
    [Tooltip("Sprite do Eptinho Tenso / Assustado (usado ao se aproximar do Mercador)")]
    [SerializeField] public Sprite eptinhoTensoSprite;
    [Tooltip("Sprite do Eptinho com Medo ao aceitar um Pacto de Sangue (eptinhomedo)")]
    [SerializeField] public Sprite eptinhoMedoSprite;

    [Header("Retrato do Eptinho (Tamanho Fixo e Protegido)")]
    [Range(30f, 200f)] [SerializeField] private float portraitSize = 75f;
    [SerializeField] private float portraitMarginLeft = 20f;
    [SerializeField] private float portraitMarginRight = 20f;
    [SerializeField] private Color portraitBgColor = new Color(0.08f, 0.05f, 0.18f, 0.95f);
    [SerializeField] private Color portraitBorderColor = new Color(0.62f, 0.38f, 0.92f, 1.0f);

    [Header("Texto do Eptinho (Tamanho da Fonte Fixo)")]
    [Range(10f, 80f)] [SerializeField] private float textFontSize = 32f;
    [SerializeField] private Color textColor = new Color(0.72f, 0.76f, 0.78f, 1.0f);
    [SerializeField] private bool previewPermanente = false;

    [Header("🔊 Som do Popup")]
    [Tooltip("Arraste o AudioClip 'Beep_Eptinho' aqui (Audio/SoundFX/Eptinho).")]
    [SerializeField] private AudioClip popupBeepClip;
    [Range(0f, 1f)]
    [SerializeField] private float popupBeepVolume = 0.7f;
    private AudioSource popupAudioSource;

    private Coroutine esconderCoroutine;

    private void Reset()
    {
        autoResizePanel     = true;
        minPanelWidth       = 380f;
        maxPanelWidth       = 850f;
        minPanelHeight      = 90f;
        maxPanelHeight      = 350f;
        paddingX            = 25f;
        paddingY            = 18f;
        panelWidth          = 650f;
        panelHeight         = 120f;
        marginX             = -40f;
        marginY             = -40f;
        panelColor          = new Color(0.23f, 0.28f, 0.70f, 0.5f);
        borderColor         = new Color(0.52f, 0.45f, 0.15f, 0.5f);
        borderThickness     = 20f;
        portraitSize        = 75f;
        portraitMarginLeft  = 20f;
        portraitMarginRight = 20f;
        portraitBgColor     = new Color(0.08f, 0.05f, 0.18f, 0.95f);
        portraitBorderColor = new Color(0.62f, 0.38f, 0.92f, 1.0f);
        textFontSize        = 32f;
        textColor           = new Color(0.72f, 0.76f, 0.78f, 1.0f);
        previewPermanente   = false;
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (esconderCoroutine != null)
        {
            StopCoroutine(esconderCoroutine);
            esconderCoroutine = null;
        }
        if (popupUI != null) popupUI.SetActive(false);
    }

    void Awake()
    {
        if (_instancia == null)
        {
            _instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsurePopupUIExists();
    }

    private void EnsurePopupUIExists()
    {
        if (popupUI == null)
        {
            GameObject existing = GameObject.Find("PopupUI_Auto");
            if (existing != null)
            {
                popupUI = existing;
                Debug.Log("[EPTINHO POPUP] Conectou ao PopupUI_Auto existente.");
            }
            else
            {
                GameObject prefab = Resources.Load<GameObject>("PopupUI");
                if (prefab != null)
                {
                    popupUI = Instantiate(prefab);
                    popupUI.name = "PopupUI_Auto";
                    DontDestroyOnLoad(popupUI);
                    Debug.Log("[EPTINHO POPUP] Instanciou novo PopupUI a partir de Resources.");
                }
                else
                {
                    Debug.LogError("[EPTINHO POPUP] ERRO: Nao encontrou prefab 'PopupUI' na pasta Resources!");
                }
            }
        }

        if (popupUI != null)
        {
            if (imagemDoItem == null)
            {
                Transform faceTransform = popupUI.transform.Find("PopupPanel/EptinhoFace");
                if (faceTransform == null) faceTransform = popupUI.transform.Find("EptinhoFace");
                
                if (faceTransform != null)
                {
                    imagemDoItem = faceTransform.GetComponent<Image>();
                }
                else
                {
                    foreach (var img in popupUI.GetComponentsInChildren<Image>(true))
                    {
                        if (img.gameObject.name == "EptinhoFace")
                        {
                            imagemDoItem = img;
                            break;
                        }
                    }
                }
            }

            if (textoDoItem == null)
            {
                Transform textTransform = popupUI.transform.Find("PopupPanel/Text (TMP)");
                if (textTransform == null) textTransform = popupUI.transform.Find("Text (TMP)");

                if (textTransform != null)
                {
                    textoDoItem = textTransform.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    foreach (var tmp in popupUI.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (tmp.gameObject.name == "Text (TMP)")
                        {
                            textoDoItem = tmp;
                            break;
                        }
                    }
                }
            }
            EstilizarUIEmTempoReal();
        }
    }

    private void EstilizarUIEmTempoReal()
    {
        if (popupUI == null) return;

        // 1. Estiliza o painel principal (PopupPanel) e desativa esticamento automático de filhotes
        Transform panel = popupUI.transform.Find("PopupPanel");
        if (panel != null)
        {
            HorizontalLayoutGroup hlg = panel.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
            }

            VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth = false;
                vlg.childControlHeight = false;
            }

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(1f, 1f);
                panelRect.sizeDelta = new Vector2(panelWidth, panelHeight); 
                panelRect.anchoredPosition = new Vector2(marginX, marginY); 
            }

            Image panelImg = panel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = panelColor;
                Outline outline = panel.GetComponent<Outline>();
                if (outline == null) outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = borderColor; 
                outline.effectDistance = new Vector2(borderThickness, borderThickness);
            }
        }

        // 2. Estiliza e TRAVA RIGIDAMENTE o tamanho do retrato do Eptinho (Ignora expansão de Layout)
        if (imagemDoItem != null)
        {
            // Remove qualquer AspectRatioFitter que possa deformar o tamanho
            AspectRatioFitter arf = imagemDoItem.GetComponent<AspectRatioFitter>();
            if (arf != null) DestroyImmediate(arf);

            imagemDoItem.type = Image.Type.Simple;
            imagemDoItem.preserveAspect = true;

            LayoutElement le = imagemDoItem.GetComponent<LayoutElement>();
            if (le == null) le = imagemDoItem.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // Impede que LayoutGroups da Unity estiquem a foto do Eptinho!
            le.preferredWidth = portraitSize;
            le.preferredHeight = portraitSize;
            le.minWidth = portraitSize;
            le.minHeight = portraitSize;

            RectTransform imgRect = imagemDoItem.GetComponent<RectTransform>();
            if (imgRect != null)
            {
                imgRect.anchorMin = new Vector2(0f, 0.5f);
                imgRect.anchorMax = new Vector2(0f, 0.5f);
                imgRect.pivot = new Vector2(0f, 0.5f);
                imgRect.localScale = Vector3.one;
                imgRect.sizeDelta = new Vector2(portraitSize, portraitSize); 
                imgRect.anchoredPosition = new Vector2(portraitMarginLeft, 0f); 
            }

            Transform parent = imagemDoItem.transform.parent;
            if (parent != null)
            {
                Transform existingBg = parent.Find("PortraitBg");
                if (existingBg != null)
                {
                    DestroyImmediate(existingBg.gameObject);
                }
            }
        }

        // 3. Estiliza o texto (Fonte Fixa e Organizada)
        if (textoDoItem != null)
        {
            textoDoItem.color = textColor;
            textoDoItem.fontSize = textFontSize;
            textoDoItem.alignment = TextAlignmentOptions.MidlineLeft;
            textoDoItem.textWrappingMode = TextWrappingModes.Normal;

            LayoutElement textLe = textoDoItem.GetComponent<LayoutElement>();
            if (textLe != null) textLe.ignoreLayout = true;
            
            RectTransform textRect = textoDoItem.GetComponent<RectTransform>();
            if (textRect != null)
            {
                float portraitOccupiedWidth = portraitMarginLeft + portraitSize + portraitMarginRight;
                textRect.anchorMin = new Vector2(0f, 0.5f);
                textRect.anchorMax = new Vector2(1f, 0.5f);
                textRect.pivot = new Vector2(0f, 0.5f);
                textRect.offsetMin = new Vector2(portraitOccupiedWidth, -panelHeight/2f + 8f); 
                textRect.offsetMax = new Vector2(-15f, panelHeight/2f - 8f);  
            }
        }

        if (panel != null)
        {
            Transform abrirBtn = panel.Find("AbrirEptinho");
            if (abrirBtn != null)
            {
                RectTransform btnRect = abrirBtn.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    btnRect.anchorMin = new Vector2(1f, 0f);
                    btnRect.anchorMax = new Vector2(1f, 0f);
                    btnRect.pivot = new Vector2(1f, 0f);
                    btnRect.anchoredPosition = new Vector2(-15f, 8f);
                    btnRect.sizeDelta = new Vector2(140f, 24f);
                }
            }
        }
    }

    /// <summary>
    /// Calcula e ajusta dinamicamente as dimensões do painel com base no comprimento do texto,
    /// mantendo o retrato RIGIDAMENTE travado no tamanho correto!
    /// </summary>
    private void AjustarTamanhoDoPainelDinamico(string mensagem)
    {
        if (!autoResizePanel || popupUI == null || textoDoItem == null) return;

        Transform panel = popupUI.transform.Find("PopupPanel");
        if (panel == null) return;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null) return;

        // Configura o texto para medir as dimensões renderizadas reais
        textoDoItem.text = mensagem;
        textoDoItem.fontSize = textFontSize;
        textoDoItem.textWrappingMode = TextWrappingModes.Normal;

        // Calcula a largura que o retrato e suas margens ocupam
        float portraitOccupiedWidth = portraitMarginLeft + portraitSize + portraitMarginRight;
        
        // Define a largura limite para que o texto dobre de linha quando necessário
        float maxAvailableTextWidth = maxPanelWidth - portraitOccupiedWidth - paddingX;

        // Mede com precisão a largura e altura reais que a mensagem precisa
        Vector2 textSize = textoDoItem.GetPreferredValues(mensagem, maxAvailableTextWidth, 0f);

        // Calcula a nova largura do painel (respeitando min/max)
        float newWidth = Mathf.Clamp(portraitOccupiedWidth + textSize.x + paddingX, minPanelWidth, maxPanelWidth);

        // Calcula a nova altura do painel (respeitando min/max e garantindo caber o texto)
        float minHeightNeeded = Mathf.Max(minPanelHeight, portraitSize + (paddingY * 1.2f));
        float newHeight = Mathf.Clamp(textSize.y + (paddingY * 2f), minHeightNeeded, maxPanelHeight);

        // Aplica o tamanho calculado ao painel
        panelWidth = newWidth;
        panelHeight = newHeight;

        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        // Garante que o retrato PERMANEÇA rigorosamente no tamanho portraitSize
        if (imagemDoItem != null)
        {
            RectTransform imgRect = imagemDoItem.GetComponent<RectTransform>();
            if (imgRect != null)
            {
                imgRect.sizeDelta = new Vector2(portraitSize, portraitSize);
                imgRect.anchoredPosition = new Vector2(portraitMarginLeft, 0f);
            }
        }

        // Reajusta o RectTransform do texto para preencher perfeitamente o novo painel
        RectTransform textRect = textoDoItem.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(1f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.offsetMin = new Vector2(portraitOccupiedWidth, -panelHeight / 2f + 8f);
            textRect.offsetMax = new Vector2(-15f, panelHeight / 2f - 8f);
        }
    }

    /// <summary>Popup de aviso genérico ou restrição (usado em bloqueios de combate).</summary>
    public void MostrarPopupAviso(string mensagem)
    {
        Sprite eptinhoFace = eptinhoNormalSprite;
        if (eptinhoFace == null) eptinhoFace = Resources.Load<Sprite>("EPTONHO");
        MostrarPopupGenerico(eptinhoFace, mensagem);
    }

    /// <summary>Popup especial de alerta do Mercador (usa a expressão Eptinho Tenso no retrato de forma protegida).</summary>
    public void MostrarPopupMercador(string mensagem)
    {
        Sprite faceTenso = eptinhoTensoSprite;
        if (faceTenso == null) faceTenso = Resources.Load<Sprite>("tensoeptinho_0");
        if (faceTenso == null) faceTenso = Resources.Load<Sprite>("EPTONHO_TENSO");
        if (faceTenso == null) faceTenso = Resources.Load<Sprite>("EPTONHO");

        MostrarPopupGenerico(faceTenso, mensagem);
    }

    /// <summary>Popup especial de pânico do Eptinho (usa a expressão eptinhomedo ao aceitar o Pacto).</summary>
    public void MostrarPopupPactoMedo(string mensagem)
    {
        Sprite faceMedo = eptinhoMedoSprite;
        if (faceMedo == null) faceMedo = Resources.Load<Sprite>("eptinhomedo");
        if (faceMedo == null) faceMedo = Resources.Load<Sprite>("EPTONHO_MEDO");
        if (faceMedo == null) faceMedo = eptinhoTensoSprite;
        if (faceMedo == null) faceMedo = Resources.Load<Sprite>("tensoeptinho_0");
        if (faceMedo == null) faceMedo = Resources.Load<Sprite>("EPTONHO");

        MostrarPopupGenerico(faceMedo, mensagem);
    }

    /// <summary>Popup com retrato customizado do Eptinho (ex: Eptinho Tenso/Assustado no Mercador).</summary>
    public void MostrarPopupCustomizado(Sprite customSprite, string mensagem)
    {
        MostrarPopupGenerico(customSprite, mensagem);
    }

    /// <summary>Popup ao catalogar um novo ItemData.</summary>
    public void MostrarPopup(ItemData item)
    {
        if (item == null) return;
        MostrarPopupGenerico(item.icon, "Eptinho analisou: " + item.itemName);
    }

    /// <summary>Popup ao registrar um novo EnemyData no Bestiário.</summary>
    public void MostrarPopupInimigo(EnemyData inimigo)
    {
        if (inimigo == null) return;
        MostrarPopupGenerico(inimigo.icon, "Novo inimigo encontrado: " + inimigo.enemyName);
    }

    void MostrarPopupGenerico(Sprite icone, string mensagem)
    {
        EnsurePopupUIExists();
        if (popupUI == null)
        {
            Debug.LogWarning("[EPTINHO POPUP] Nao pode mostrar popup: popupUI e nulo!");
            return;
        }

        popupUI.SetActive(true);

        Transform panel = popupUI.transform.Find("PopupPanel");
        if (panel != null)
        {
            panel.gameObject.SetActive(true);
        }

        Sprite iconeFinal = icone;
        if (iconeFinal == null)
        {
            iconeFinal = Resources.Load<Sprite>("EPTONHO");
        }

        if (imagemDoItem != null)
        {
            imagemDoItem.gameObject.SetActive(true);
            imagemDoItem.sprite = iconeFinal;
        }

        // Aplica o estilo base
        EstilizarUIEmTempoReal();

        if (textoDoItem != null)
        {
            textoDoItem.gameObject.SetActive(true);
            textoDoItem.text = mensagem;
            textoDoItem.fontSize = textFontSize;
            textoDoItem.color = textColor;

            TMP_FontAsset customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
            if (customFont != null)
            {
                textoDoItem.font = customFont;
            }
        }

        // 📐 AJUSTE DINÂMICO: Redimensiona o painel de acordo com a mensagem
        AjustarTamanhoDoPainelDinamico(mensagem);

        // 🔊 Toca o beep do popup
        PlayPopupBeep();

        Debug.Log("[EPTINHO POPUP] MOSTRANDO POPUP AUTO-AJUSTADO: " + mensagem);

        if (esconderCoroutine != null)
            StopCoroutine(esconderCoroutine);

        if (!previewPermanente)
        {
            esconderCoroutine = StartCoroutine(EsconderApos(3.5f));
        }
    }

    private void PlayPopupBeep()
    {
        if (popupBeepClip == null) return;

        if (popupAudioSource == null)
        {
            popupAudioSource = gameObject.AddComponent<AudioSource>();
            popupAudioSource.playOnAwake = false;
            popupAudioSource.spatialBlend = 0f; // 2D para garantir que o player sempre ouça
        }

        popupAudioSource.PlayOneShot(popupBeepClip, popupBeepVolume);
    }

    IEnumerator EsconderApos(float segundos)
    {
        yield return new WaitForSecondsRealtime(segundos);
        if (popupUI != null) popupUI.SetActive(false);
    }

    // ─── MÉTODOS DE TESTE E PREVIEW ──────────────────────────────────────────

    [ContextMenu("Testar Popup Temporário (3.5s)")]
    public void TestarPopupTemporario()
    {
        MostrarPopupAviso("Eptinho: Teste de Popup temporário com texto curto!");
    }

    [ContextMenu("Testar Popup Longo")]
    public void TestarPopupLongo()
    {
        MostrarPopupAviso("Estou sentindo uma energia muito perturbadora vindo dele... Tenha muito cuidado! Se você chegar perto, ele pode te atacar com toda a força!");
    }

    [ContextMenu("Ativar Preview Permanente")]
    public void AtivarPreviewPermanente()
    {
        previewPermanente = true;
        MostrarPopupAviso("Eptinho: Preview Permanente Ativado!");
    }

    [ContextMenu("Desativar Preview Permanente")]
    public void DesativarPreviewPermanente()
    {
        previewPermanente = false;
        if (popupUI != null) popupUI.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && popupUI != null)
        {
            EnsurePopupUIExists();
            
            if (previewPermanente)
            {
                popupUI.SetActive(true);
                Transform panel = popupUI.transform.Find("PopupPanel");
                if (panel != null) panel.gameObject.SetActive(true);

                if (esconderCoroutine != null) StopCoroutine(esconderCoroutine);

                Sprite eptFace = Resources.Load<Sprite>("EPTONHO");
                MostrarPopupGenerico(eptFace, "Eptinho: Preview permanente ativo com ajuste dinâmico ao texto!");
            }
            else
            {
                popupUI.SetActive(false);
            }
        }
    }
#endif
}
