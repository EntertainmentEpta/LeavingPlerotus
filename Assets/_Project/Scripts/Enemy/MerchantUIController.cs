using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// =================================================================================
/// CONTROLADOR DA INTERFACE DO MERCADOR E SISTEMA DO TARÔ PROIBIDO
/// =================================================================================
/// Desenvolvido por: Vicenzo (Branch: VicenzoWS)
/// 
/// Funcionalidades Principais Implementadas:
/// 1. Pool Expandido de 8 Pactos de Sangue com Seleção Aleatória de 3 Cartas por visita.
/// 2. Animação Procedural 3D de Revelação de Carta (Rotação de 0° a 90° e 90° a 0° no eixo Y com Lerp Suave).
/// 3. Troca Dinâmica do Texto da Carta no Ponto Cego da Animação (90° de Rotação).
/// 4. Efeito de Zoom (1.85x Scale) e Camera Shake (Tremor da Câmera) ao Confirmar o Pacto.
/// 5. Notificação Automática com o Sprite do Eptinho Medo (eptinhomedo) ao Realizar o Pacto.
/// 6. Suporte Completo à Confirmação/Cancelamento e Encerramento Dinâmico com a Tecla ESC.
/// =================================================================================
/// </summary>
public class MerchantUIController : MonoBehaviour
{
    [Header("Referências da UI Geral")]
    public GameObject interactionPrompt; 
    public GameObject rootPanel; // Fundo escuro / Canvas Principal
    
    [Header("Menu Principal de Seleção")]
    public GameObject mainMenuPanel; // Contém as opções do pacto
    public Button btnPactoDeSangue; // Abre as cartas de Tarô
    public Button btnCambioSangue;
    public Button btnRemocao; // Abre a lista de remoção
    public Button btnComprarArtefato;

    [Header("Painel: O Tarô Proibido")]
    public GameObject tarotCardsPanel; 
    public Button[] tarotButtons; 
    public TextMeshProUGUI[] tarotNames;
    public TextMeshProUGUI[] tarotDescriptions;
    public TextMeshProUGUI[] tarotCosts;

    public TextMeshProUGUI txtCambioCost;
    public TextMeshProUGUI txtRemocaoCost;
    public TextMeshProUGUI txtArtefatoCost;

    [Header("Painel: Cirurgia de Remoção")]
    public GameObject removalListPanel;
    public Transform removalListContent; 
    public GameObject removalItemButtonPrefab; 
    
    [Header("Geral")]
    public Button closeButton;

    [Header("🔊 Sons do Mercador")]
    [Tooltip("Som tocado ao passar o mouse sobre uma carta do pacto.")]
    [SerializeField] private AudioClip cardHoverClip;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float cardHoverVolume = 0.7f;

    [Tooltip("Som de downgrade tocado ao aceitar o Pacto de Sangue.")]
    [SerializeField] private AudioClip pactAcceptClip;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float pactAcceptVolume = 1.0f;
    private AudioSource audioSource;

    [Header("Câmera do Pacto (Ajustes em Tempo Real)")]
    public bool enablePactCamera = true;
    public Vector3 cameraOffset = new Vector3(0f, 0.35f, 2.2f);
    public float cameraLookAtHeight = 2.0f;
    public float cameraFOV = 52f;

    [HideInInspector]
    public PlayerHealth playerHealth;
    private InfusionManager infusionManager;
    private PlayerEssence playerEssence;
    private PlayerInventory playerInventory;

    private bool hasMadePact = false;
    public bool HasMadePactInRun { get; private set; } = false;

    // Estado de Animação e Revelação da Carta 3D
    private bool isRevealingPactCard = false;
    private int pendingPactIndex = -1;
    private int pendingHealthCost = 0;
    private Vector2[] originalCardPositions = new Vector2[3];
    private Vector3[] originalCardScales = new Vector3[3];

    public void ResetPactState()
    {
        HasMadePactInRun = false;
        hasMadePact = false;
        isRevealingPactCard = false;
        pendingPactIndex = -1;
    }

    private void OnPactCompleted()
    {
        HasMadePactInRun = true;
        hasMadePact = true;

        if (playerHealth != null)
        {
            playerHealth.SetPactCorrupted(true);
        }

        if (merchantTransform != null)
        {
            Merchant m = merchantTransform.GetComponent<Merchant>() ?? merchantTransform.GetComponentInParent<Merchant>();
            if (m != null)
            {
                m.VanishAfterPact();
            }
            else
            {
                merchantTransform.gameObject.SetActive(false);
            }
        }

        ClosePanel();
    }

    private Camera pactCamera; 
    private GameObject pactCameraObj;
    private Camera cachedMainCamera;
    private bool isDynamicPactCamera = false;
    private Transform merchantTransform;

    [System.Serializable]
    public class PactData
    {
        public string name;
        public string description;
        public float healthCostPercent;
    }

    // Pool de 8 Maldições do Tarô Proibido
    private PactData[] pactPool = new PactData[]
    {
        new PactData {
            name = "A GANÂNCIA",
            description = "<color=#00ff99>✦ Dobro de loot e essência ao eliminar inimigos.</color>\n<color=#ff4455>✖ Você sofre 50% a mais de dano.</color>",
            healthCostPercent = 0.20f
        },
        new PactData {
            name = "O FRENESI",
            description = "<color=#00ff99>✦ Vel. Movimento +20% e Ataque +40%.</color>\n<color=#ff4455>✖ Armadura reduzida a ZERO (Sem Regen).</color>",
            healthCostPercent = 0.25f
        },
        new PactData {
            name = "O PARASITA",
            description = "<color=#00ff99>✦ Vampirismo (+Vida ao matar inimigos).</color>\n<color=#ff4455>✖ Dano de Necrose contínua após 5s sem matar.</color>",
            healthCostPercent = 0.30f
        },
        new PactData {
            name = "O ESPECTRO",
            description = "<color=#00ff99>✦ Chance de Esquiva (Dodge) +50%.</color>\n<color=#ff4455>✖ Seu Dano causado é reduzido em 30%.</color>",
            healthCostPercent = 0.15f
        },
        new PactData {
            name = "O SACRIFÍCIO",
            description = "<color=#00ff99>✦ Dano Base massivamente aumentado em +150%.</color>\n<color=#ff4455>✖ Bloqueia totalmente qualquer tipo de cura.</color>",
            healthCostPercent = 0.50f
        },
        new PactData {
            name = "SANGUE CRÍTICO",
            description = "<color=#00ff99>✦ 100% Chance de Crítico & +50% Dano Crítico.</color>\n<color=#ff4455>✖ Cada ataque seu consome 2 de Vida.</color>",
            healthCostPercent = 0.25f
        },
        new PactData {
            name = "O TITÃ DE CRISTAL",
            description = "<color=#00ff99>✦ +100% de Vida Máxima e Imunidade a Knockback.</color>\n<color=#ff4455>✖ Velocidade de Movimento reduzida em 35%.</color>",
            healthCostPercent = 0.30f
        },
        new PactData {
            name = "O COLAPSO TEMPORAL",
            description = "<color=#00ff99>✦ Recarga de Habilidades reduzida em 60%.</color>\n<color=#ff4455>✖ Inimigos causam +25% de dano e correm +20%.</color>",
            healthCostPercent = 0.20f
        }
    };

    private int[] currentDisplayedPactIndices = new int[3];

    public static bool HasInstance => _instance != null;

    private static MerchantUIController _instance;
    public static MerchantUIController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MerchantUIController>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    GameObject prefab = Resources.Load<GameObject>("MerchantCanvas");
                    if (prefab == null) prefab = Resources.Load<GameObject>("MerchantCanva");
                    if (prefab == null) prefab = Resources.Load<GameObject>("MerchantUI");

                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab);
                        _instance = go.GetComponent<MerchantUIController>();
                        if (_instance == null) _instance = go.AddComponent<MerchantUIController>();
                    }
                    else
                    {
                        GameObject go = new GameObject("MerchantUIController_Auto");
                        _instance = go.AddComponent<MerchantUIController>();
                    }
                    if (_instance != null) DontDestroyOnLoad(_instance.gameObject);
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseButtonClicked);
        
        if (btnPactoDeSangue != null) btnPactoDeSangue.onClick.AddListener(ShowTarotCards);
        if (btnCambioSangue != null) btnCambioSangue.onClick.AddListener(OnCambioSangueClicked);
        if (btnRemocao != null) btnRemocao.onClick.AddListener(ShowRemovalList);
        if (btnComprarArtefato != null) btnComprarArtefato.onClick.AddListener(OnComprarArtefatoClicked);

        EnsurePanelReferences();

        // AudioSource para SFX do Mercador (cria automaticamente se não existir)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>(true);
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (rootPanel != null) rootPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (removalListPanel != null) removalListPanel.SetActive(false);
        if (tarotCardsPanel != null) tarotCardsPanel.SetActive(false);
        
        SetupRightSideText();
    }

    private void EnsurePanelReferences()
    {
        if (rootPanel == null)
        {
            Transform canvasChild = transform.Find("Canvas");
            if (canvasChild != null) rootPanel = canvasChild.gameObject;
            
            if (rootPanel == null)
            {
                foreach (Transform t in transform)
                {
                    if (t.name.ToLower().Contains("panel") || t.name.ToLower().Contains("canvas") || t.name.ToLower().Contains("root"))
                    {
                        rootPanel = t.gameObject;
                        break;
                    }
                }
            }
            if (rootPanel == null && transform.childCount > 0) rootPanel = transform.GetChild(0).gameObject;
        }

        Transform searchRoot = (rootPanel != null) ? rootPanel.transform : transform;

        if (mainMenuPanel == null)
        {
            foreach (Transform t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("mainmenu") || t.name.ToLower().Contains("menu") || t.name.ToLower().Contains("principal"))
                {
                    mainMenuPanel = t.gameObject;
                    break;
                }
            }
        }

        if (tarotCardsPanel == null)
        {
            foreach (Transform t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("tarot") || t.name.ToLower().Contains("pacto") || t.name.ToLower().Contains("card"))
                {
                    tarotCardsPanel = t.gameObject;
                    break;
                }
            }
        }

        if (removalListPanel == null)
        {
            foreach (Transform t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("removal") || t.name.ToLower().Contains("remocao") || t.name.ToLower().Contains("cirurgia"))
                {
                    removalListPanel = t.gameObject;
                    break;
                }
            }
        }
    }

    public void ConnectPlayer(PlayerHealth player)
    {
        playerHealth = player;
        if (player != null)
        {
            infusionManager = player.GetComponent<InfusionManager>();
            playerEssence = player.GetComponent<PlayerEssence>();
            playerInventory = player.GetComponent<PlayerInventory>();
        }
    }

    public void ShowTarotCards()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (tarotCardsPanel != null) tarotCardsPanel.SetActive(true);

        GenerateRandom3Pacts();
    }

    private void GenerateRandom3Pacts()
    {
        List<int> pool = new List<int>();
        for (int i = 0; i < pactPool.Length; i++) pool.Add(i);

        for (int i = 0; i < 3; i++)
        {
            int r = Random.Range(0, pool.Count);
            currentDisplayedPactIndices[i] = pool[r];
            pool.RemoveAt(r);
        }

        SetupTarotButtons();
    }

    void SetupTarotButtons()
    {
        if (tarotCardsPanel != null)
        {
            Button[] foundButtons = tarotCardsPanel.GetComponentsInChildren<Button>(true);
            if (foundButtons != null && foundButtons.Length > 0)
            {
                tarotButtons = foundButtons;
            }
        }

        if (tarotButtons == null || tarotButtons.Length == 0) return;

        isRevealingPactCard = false;
        pendingPactIndex = -1;

        // Ativa EXATAMENTE 3 cartões no painel e oculta os demais (ex: se havia 4 no prefab)
        for (int i = 0; i < tarotButtons.Length; i++)
        {
            if (tarotButtons[i] != null)
            {
                tarotButtons[i].gameObject.SetActive(i < 3);
                tarotButtons[i].interactable = true;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (i >= tarotButtons.Length) break;
            Button btn = tarotButtons[i];
            if (btn == null) continue;

            RectTransform btnRect = btn.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.localEulerAngles = Vector3.zero;
                btnRect.localScale = Vector3.one;
            }

            MerchantCardHover hover = btn.GetComponent<MerchantCardHover>();
            if (hover != null)
            {
                hover.enabled = true;
                hover.ResetToNormal();
            }

            int pactIndex = currentDisplayedPactIndices[i];
            PactData pact = pactPool[pactIndex];

            TextMeshProUGUI nameTxt = null;
            TextMeshProUGUI descTxt = null;
            TextMeshProUGUI costTxt = null;

            // Busca os elementos de texto do cartão
            TextMeshProUGUI[] childTexts = btn.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in childTexts)
            {
                string objName = t.gameObject.name.ToLower();
                if (nameTxt == null && (objName.Contains("name") || objName.Contains("nome") || objName.Contains("title") || objName.Contains("titulo")))
                    nameTxt = t;
                else if (descTxt == null && (objName.Contains("desc") || objName.Contains("info") || objName.Contains("text")))
                    descTxt = t;
                else if (costTxt == null && (objName.Contains("cost") || objName.Contains("custo") || objName.Contains("price") || objName.Contains("vida")))
                    costTxt = t;
            }

            if (childTexts.Length > 0 && nameTxt == null) nameTxt = childTexts[0];
            if (childTexts.Length > 1 && descTxt == null) descTxt = (childTexts[1] != nameTxt) ? childTexts[1] : (childTexts.Length > 2 ? childTexts[2] : null);
            if (childTexts.Length > 2 && costTxt == null) costTxt = (childTexts[2] != nameTxt && childTexts[2] != descTxt) ? childTexts[2] : null;

            // Apenas o TÍTULO ACIMA da carta; NENHUMA informação dentro da carta em si!
            if (nameTxt != null)
            {
                nameTxt.gameObject.SetActive(true);
                nameTxt.text = $"<color=#ffd700><b>✦ {pact.name.ToUpper()} ✦</b></color>";
            }
            
            // Oculta textos internos da carta para manter a arte interna totalmente limpa
            if (descTxt != null) descTxt.gameObject.SetActive(false);
            if (costTxt != null) costTxt.gameObject.SetActive(false);

            ApplyCardTextVerticalLayout(nameTxt, descTxt, costTxt);

            int cardSlot = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnTarotCardClicked(cardSlot));

            if (btn.GetComponent<MerchantCardHover>() == null)
                btn.gameObject.AddComponent<MerchantCardHover>();

            // Injeta o som de hover configurado no MerchantUIController para cada carta
            MerchantCardHover hoverComp = btn.GetComponent<MerchantCardHover>();
            if (hoverComp != null && cardHoverClip != null)
                hoverComp.SetHoverAudio(cardHoverClip, cardHoverVolume, audioSource);
        }
    }

    private void ApplyCardTextVerticalLayout(TextMeshProUGUI nameTxt, TextMeshProUGUI descTxt, TextMeshProUGUI costTxt)
    {
        if (nameTxt != null)
        {
            RectTransform rt = nameTxt.rectTransform;
            // Posiciona o título ACIMA da borda superior da carta (anchor Y > 1.0)
            rt.anchorMin = new Vector2(-0.2f, 1.05f);
            rt.anchorMax = new Vector2(1.2f, 1.30f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.enableWordWrapping = false;
            nameTxt.overflowMode = TextOverflowModes.Overflow;
            nameTxt.enableAutoSizing = true;
            nameTxt.fontSizeMin = 18;
            nameTxt.fontSizeMax = 28;
        }

        // Garante que descTxt e costTxt permaneçam ocultos dentro da carta
        if (descTxt != null) descTxt.gameObject.SetActive(false);
        if (costTxt != null) costTxt.gameObject.SetActive(false);
    }
    

    void SetupRightSideText()
    {
        if (txtCambioCost != null) txtCambioCost.text = "<color=#ff3344>-15% Vida Máxima</color>\n<color=#ffcc00>+300 Essências</color>";
        if (txtRemocaoCost != null) txtRemocaoCost.text = "<color=#88ccff>Remove 1 Infusão</color>\n<color=#ffcc00>-150 Essências</color>";
        if (txtArtefatoCost != null) txtArtefatoCost.text = "<color=#ffaa00>Artefato Tier Alto</color>\n<color=#ffcc00>-600 Essências</color>";
    }

    private void AddHoverEffectToButtons()
    {
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        foreach (Button b in allButtons)
        {
            if (b != null && b.GetComponent<MerchantCardHover>() == null)
            {
                b.gameObject.AddComponent<MerchantCardHover>();
            }
        }
    }

    void Update()
    {
        if (IsUiOpen())
        {
            // Garante cursor visível e livre enquanto qualquer menu do Mercador estiver aberto!
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (enablePactCamera && merchantTransform != null)
            {
                SetupPactCamera();
            }

            // Tecla ESC para sair ou confirmar carta revelada
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnCloseButtonClicked();
            }
        }
    }

    private void OnCloseButtonClicked()
    {
        if (isRevealingPactCard)
        {
            ConfirmRevealedPactAndClose();
        }
        else
        {
            ClosePanel();
        }
    }

    private void ConfirmRevealedPactAndClose()
    {
        if (!isRevealingPactCard) return;

        isRevealingPactCard = false;
        if (pendingPactIndex >= 0)
        {
            // SFX de downgrade ao aceitar o pacto
            if (pactAcceptClip != null)
            {
                GameObject tempAudio = new GameObject("TempPactAudio");
                AudioSource aSource = tempAudio.AddComponent<AudioSource>();
                aSource.clip = pactAcceptClip;
                aSource.volume = pactAcceptVolume;
                aSource.spatialBlend = 0f; // Som 2D
                aSource.Play();
                Destroy(tempAudio, pactAcceptClip.length + 0.1f);
            }

            ApplyTarotEffect(pendingPactIndex, pendingHealthCost);
            OnPactCompleted();
        }
        else
        {
            ClosePanel();
        }
    }

    public void ShowPrompt(bool show)
    {
        if (interactionPrompt != null && !hasMadePact)
            interactionPrompt.SetActive(show);
    }

    public bool IsUiOpen()
    {
        if (!gameObject.activeInHierarchy) return false;
        if (rootPanel != null && !rootPanel.activeInHierarchy) return false;
        return (rootPanel != null && rootPanel.activeSelf) || (mainMenuPanel != null && mainMenuPanel.activeSelf) || (tarotCardsPanel != null && tarotCardsPanel.activeSelf);
    }

    public void OpenPanel(Transform merchantPos = null)
    {
        merchantTransform = merchantPos;

        gameObject.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (playerHealth == null)
        {
            PlayerHealth p = Object.FindFirstObjectByType<PlayerHealth>();
            if (p != null) ConnectPlayer(p);
        }

        EnsurePanelReferences();

        Canvas c = GetComponent<Canvas>();
        if (c == null) c = GetComponentInChildren<Canvas>(true);
        if (c != null)
        {
            c.enabled = true;
            c.sortingOrder = 999;
        }

        if (rootPanel != null) rootPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (tarotCardsPanel != null) tarotCardsPanel.SetActive(false);
        if (removalListPanel != null) removalListPanel.SetActive(false);

        AddHoverEffectToButtons();

        if (enablePactCamera)
        {
            SetupPactCamera();
        }

        Time.timeScale = 0f;
        ShowPrompt(false);
        Debug.Log("[MERCHANT UI] Painel do Pacto de Sangue aberto com sucesso.");
    }

    private void SetupPactCamera()
    {
        if (!enablePactCamera) return;

        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
        }

        pactCamera = null;
        if (merchantTransform != null)
        {
            pactCamera = merchantTransform.GetComponentInChildren<Camera>(true);
        }

        if (pactCamera == null)
        {
            GameObject camObj = GameObject.Find("MerchantCamera");
            if (camObj != null) pactCamera = camObj.GetComponent<Camera>();
        }

        if (pactCamera == null)
        {
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera c in allCams)
            {
                if (c.gameObject != null && c.gameObject.name.ToLower().Contains("merchant"))
                {
                    pactCamera = c;
                    break;
                }
            }
        }

        if (pactCamera == null)
        {
            Debug.LogWarning("[MerchantUIController] MerchantCamera não foi encontrada no prefab ou cena.");
            return;
        }

        pactCameraObj = pactCamera.gameObject;

        if (cachedMainCamera != null && cachedMainCamera != pactCamera)
        {
            cachedMainCamera.enabled = false;
        }

        pactCamera.depth = 100;
        pactCameraObj.SetActive(true);
        pactCamera.enabled = true;

        Canvas cComp = GetComponent<Canvas>();
        if (cComp == null) cComp = GetComponentInChildren<Canvas>(true);
        if (cComp != null && cComp.renderMode == RenderMode.ScreenSpaceCamera)
        {
            cComp.worldCamera = pactCamera;
        }
    }

    // ============================================
    // SERVIÇO 1: O Tarô Proibido & Animação 3D de Virada (Card Flip)
    // ============================================
    public void OnTarotCardClicked(int cardSlot)
    {
        if (playerHealth == null || isRevealingPactCard) return;
        if (cardSlot < 0 || cardSlot >= currentDisplayedPactIndices.Length) return;

        int pactIndex = currentDisplayedPactIndices[cardSlot];
        PactData pact = pactPool[pactIndex];

        float percentCost = pact.healthCostPercent;
        int healthCost = Mathf.RoundToInt(playerHealth.maxHealth * percentCost);

        if (playerHealth.currentHealth - healthCost >= 1)
        {
            // Inicia a Animação Cinemática de Virada de Carta 3D e Camera Shake!
            StartCoroutine(AnimateCardFlipSequence(cardSlot, pactIndex, healthCost));
        }
        else
        {
            Debug.LogWarning("[Mercador] Sangue insuficiente para este sacrifício!");
        }
    }

    /// <summary>
    /// Coroutine da Animação 3D:
    /// 1. Dispara um tremor rápido e sutil na Câmera 3D.
    /// 2. Esconde os outros 2 cartões.
    /// 3. Destaca e move o cartão escolhido para o centro da tela, aumentando a escala (1.85x).
    /// 4. Gira a carta 3D no eixo Y (0° ➔ 90°). No ponto cego de 90°, revela o verso com a maldição completa!
    /// 5. Termina a virada 3D (90° ➔ 0°) e dispara a fala assustada do Eptinho: "O que você fez?!"
    /// </summary>
    private IEnumerator AnimateCardFlipSequence(int cardSlot, int pactIndex, int healthCost)
    {
        isRevealingPactCard = true;
        pendingPactIndex = pactIndex;
        pendingHealthCost = healthCost;

        PactData pact = pactPool[pactIndex];

        // Desativa a interatividade dos botões para evitar duplo clique
        foreach (Button b in tarotButtons)
        {
            if (b != null) b.interactable = false;
        }

        // 1. CAMERA SHAKE RÁPIDO E SUTIL (Impacto seco sem tremer demais)
        StartCoroutine(CameraShakeRoutine(0.15f, 0.06f));

        // 2. Oculta os outros 2 cartões
        for (int i = 0; i < tarotButtons.Length; i++)
        {
            if (i < 3 && i != cardSlot && tarotButtons[i] != null)
            {
                tarotButtons[i].gameObject.SetActive(false);
            }
        }

        Button chosenBtn = tarotButtons[cardSlot];
        chosenBtn.transform.SetAsLastSibling(); // Coloca no topo da hierarquia visual
        
        // Desativa o MerchantCardHover para que a animação não dispute a posição no Update()
        MerchantCardHover hoverScript = chosenBtn.GetComponent<MerchantCardHover>();
        if (hoverScript != null) hoverScript.enabled = false;

        LayoutElement le = chosenBtn.GetComponent<LayoutElement>();
        if (le == null) le = chosenBtn.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true; // Libera do LayoutGroup para poder crescer livremente no centro

        RectTransform cardRect = chosenBtn.GetComponent<RectTransform>();

        Vector2 startPos = cardRect.anchoredPosition;
        Vector3 startScale = cardRect.localScale;
        Vector2 startSize = cardRect.sizeDelta;

        Vector2 centerPos = Vector2.zero;
        Vector3 targetScale = new Vector3(1.85f, 1.85f, 1.85f);
        Vector2 targetSize = new Vector2(340f, 520f);

        float duration = 0.25f;
        float elapsed = 0f;

        // FASE 1: Move para o centro da tela, expande o tamanho e gira 3D até 90° (perfil da carta)
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            cardRect.anchoredPosition = Vector2.Lerp(startPos, centerPos, smoothT);
            cardRect.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            cardRect.sizeDelta = Vector2.Lerp(startSize, targetSize, smoothT);

            float rotY = Mathf.Lerp(0f, 90f, smoothT);
            cardRect.localEulerAngles = new Vector3(0f, rotY, 0f);

            yield return null;
        }

        // FASE 2: PONTO MÉDIO DA VIRADA (90° = Carta de Perfil / Invisível)
        // Revela o verso da carta com o texto da maldição!
        TextMeshProUGUI nameTxt = null;
        TextMeshProUGUI descTxt = null;
        TextMeshProUGUI costTxt = null;

        TextMeshProUGUI[] childTexts = chosenBtn.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in childTexts)
        {
            string objName = txt.gameObject.name.ToLower();
            if (nameTxt == null && (objName.Contains("name") || objName.Contains("nome") || objName.Contains("title"))) nameTxt = txt;
            else if (descTxt == null && (objName.Contains("desc") || objName.Contains("info") || objName.Contains("text"))) descTxt = txt;
            else if (costTxt == null && (objName.Contains("cost") || objName.Contains("custo") || objName.Contains("price"))) costTxt = txt;
        }
        if (childTexts.Length > 0 && nameTxt == null) nameTxt = childTexts[0];
        if (childTexts.Length > 1 && descTxt == null) descTxt = (childTexts[1] != nameTxt) ? childTexts[1] : (childTexts.Length > 2 ? childTexts[2] : null);
        if (childTexts.Length > 2 && costTxt == null) costTxt = (childTexts[2] != nameTxt && childTexts[2] != descTxt) ? childTexts[2] : null;

        if (nameTxt != null) nameTxt.text = $"<color=#ffcc00><b>✦ {pact.name} ✦</b></color>";
        if (descTxt != null) descTxt.text = pact.description; // REVELA O VERSO COM O SEGREDO COMPLETO!
        if (costTxt != null) costTxt.text = $"<color=#ff2233><b>-{pact.healthCostPercent * 100}% VIDA MÁXIMA</b></color>";

        // FASE 3: Gira de 90° até 0°, desdobrando a face traseira no centro!
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            float rotY = Mathf.Lerp(90f, 0f, smoothT);
            cardRect.localEulerAngles = new Vector3(0f, rotY, 0f);

            yield return null;
        }

        cardRect.localEulerAngles = Vector3.zero;
        chosenBtn.interactable = true;

        // Dispara o Popup do Eptinho com a nova expressão 'eptinhomedo'!
        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupPactoMedo("O que você fez?!");
        }

        Debug.Log($"🎴 [CARTA REVELADA] {pact.name} virada e revelada no centro da tela!");
    }

    /// <summary>
    /// Coroutine de Camera Shake suave e rápido 3D durante o aceite da carta.
    /// </summary>
    private IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        Camera targetCam = (pactCamera != null && pactCamera.enabled) ? pactCamera : Camera.main;
        if (targetCam == null) yield break;

        Vector3 originalPos = targetCam.transform.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            targetCam.transform.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        targetCam.transform.localPosition = originalPos;
    }

    private void ApplyTarotEffect(int index, int healthCost)
    {
        playerHealth.TakeSacrificeDamage(healthCost);

        switch(index)
        {
            case 0: // A GANÂNCIA
                playerHealth.hasDoubleLoot = true;
                playerHealth.damageTakenMultiplier += 0.5f;
                break;

            case 1: // O FRENESI
                playerHealth.SetArmorToZero();

                PlayerM moveScript = playerHealth.playerMovement ?? playerHealth.GetComponent<PlayerM>();
                if (moveScript != null)
                {
                    moveScript.walkSpeed *= 1.20f;
                    moveScript.sprintSpeed *= 1.20f;
                }
                PlayerAttributesDefensive defStats = playerHealth.GetComponent<PlayerAttributesDefensive>() ?? playerHealth.GetComponentInChildren<PlayerAttributesDefensive>();
                if (defStats != null) defStats.speedMultiplier *= 1.20f;

                PrimaryAttackKnife attackScript = playerHealth.playerAttack ?? playerHealth.GetComponent<PrimaryAttackKnife>();
                if (attackScript != null)
                {
                    attackScript.attackAnimationSpeed *= 1.40f;
                    attackScript.defaultAttackSpeed *= 1.40f;
                }
                PlayerAttributesOffensive offStats = playerHealth.GetComponent<PlayerAttributesOffensive>() ?? playerHealth.GetComponentInChildren<PlayerAttributesOffensive>();
                if (offStats != null) offStats.attackSpeedMelee *= 1.40f;
                break;

            case 2: // O PARASITA
                playerHealth.hasVampirism = true;
                playerHealth.hasNecrosis = true;
                break;

            case 3: // O ESPECTRO
                playerHealth.ModifyAttribute("dodgechance", 50f, false);
                playerHealth.damageMultiplier -= 0.30f;
                break;

            case 4: // O SACRIFÍCIO
                playerHealth.damageMultiplier += 1.50f;
                playerHealth.canHeal = false;
                break;

            case 5: // SANGUE CRÍTICO
                playerHealth.ModifyAttribute("critchance", 100f, false);
                playerHealth.ModifyAttribute("critdamage", 50f, false);
                playerHealth.hasSelfDamageOnAttack = true;
                break;

            case 6: // O TITÃ DE CRISTAL
                playerHealth.ModifyAttribute("maxhealth", playerHealth.maxHealth, false); // +100% Vida Máxima
                playerHealth.isKnockbackImmune = true;
                PlayerM moveScript2 = playerHealth.playerMovement ?? playerHealth.GetComponent<PlayerM>();
                if (moveScript2 != null)
                {
                    moveScript2.walkSpeed *= 0.65f;
                    moveScript2.sprintSpeed *= 0.65f;
                }
                break;

            case 7: // O COLAPSO TEMPORAL
                playerHealth.abilityCooldownMultiplier *= 0.40f; // -60% Cooldown
                playerHealth.enemiesBuffed = true;
                break;
        }
    }

    // ============================================
    // SERVIÇO 2: Câmbio de Sangue por Essência
    // ============================================
    public void OnCambioSangueClicked()
    {
        if (playerHealth == null || playerEssence == null) return;

        int healthCost = Mathf.RoundToInt(playerHealth.maxHealth * 0.15f);
        if (playerHealth.currentHealth - healthCost >= 1)
        {
            playerHealth.TakeSacrificeDamage(healthCost);
            playerEssence.AddEssence(300);
            OnPactCompleted();
        }
    }

    // ============================================
    // SERVIÇO 3: Cirurgia de Remoção de Infusão
    // ============================================
    public void ShowRemovalList()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (removalListPanel != null) removalListPanel.SetActive(true);

        PopulateRemovalList();
    }

    private void PopulateRemovalList()
    {
        if (removalListContent == null || infusionManager == null) return;

        foreach (Transform child in removalListContent)
        {
            Destroy(child.gameObject);
        }

        List<ItemData> activeInfusions = infusionManager.infusedItems;
        if (activeInfusions == null || activeInfusions.Count == 0)
        {
            Debug.Log("[Mercador] Player não possui infusões para remover.");
            return;
        }

        foreach (ItemData inf in activeInfusions)
        {
            if (removalItemButtonPrefab == null) break;

            GameObject btnObj = Instantiate(removalItemButtonPrefab, removalListContent);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            if (txt != null) txt.text = $"Remover {inf.itemName} (150 Essências)";

            ItemData targetInf = inf;
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnRemoveInfusionClicked(targetInf));
            }
        }
    }

    private void OnRemoveInfusionClicked(ItemData inf)
    {
        if (playerEssence == null || infusionManager == null) return;

        if (playerEssence.currentEssence >= 150)
        {
            playerEssence.SpendEssence(150);
            infusionManager.RemoveInfusion(inf);
            OnPactCompleted();
        }
        else
        {
            Debug.LogWarning("[Mercador] Essência insuficiente para remover infusão!");
        }
    }

    // ============================================
    // SERVIÇO 4: Comprar Artefato Tier Alto
    // ============================================
    public void OnComprarArtefatoClicked()
    {
        if (playerEssence == null || playerInventory == null) return;

        if (playerEssence.currentEssence >= 600)
        {
            playerEssence.SpendEssence(600);
            Debug.Log("[Mercador] Artefato High Tier Adquirido!");
            OnPactCompleted();
        }
        else
        {
            Debug.LogWarning("[Mercador] Essência insuficiente para o Artefato!");
        }
    }

    public void ClosePanel()
    {
        if (isDynamicPactCamera && pactCameraObj != null)
        {
            Destroy(pactCameraObj);
            pactCameraObj = null;
        }
        else if (pactCameraObj != null)
        {
            pactCameraObj.SetActive(false);
        }

        if (cachedMainCamera != null)
        {
            cachedMainCamera.enabled = true;
        }

        Time.timeScale = 1f;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (tarotCardsPanel != null) tarotCardsPanel.SetActive(false);
        if (removalListPanel != null) removalListPanel.SetActive(false);
        if (rootPanel != null) rootPanel.SetActive(false);
        gameObject.SetActive(false);

        ShowPrompt(false);

        // Só trava e esconde o cursor se o inventário não estiver aberto
        bool isInventoryOpen = InventoryUI.Instance != null && InventoryUI.Instance.IsOpen();
        if (!isInventoryOpen)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}