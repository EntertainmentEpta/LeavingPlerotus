using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia a interação do jogador com o Eptinho Oráculo na Base.
/// Adicionado automaticamente pelo EptinhoController no filho "Trigger Menu".
/// Quando o player entra no BoxCollider trigger, exibe um prompt de tela e
/// permite abrir o menu do Bestiário/Oráculo ao pressionar F.
/// </summary>
public class EptinhoOracleInteract : MonoBehaviour
{
    // ─── Som de Interação ────────────────────────────────────────────────────
    [Header("🔊 Som de Interação (Latido)")]
    [Tooltip("Arraste o AudioClip 'Bark_Eptinho' aqui (Audio/SoundFX/Eptinho).")]
    [SerializeField] private AudioClip barkClip;
    [Range(0f, 1f)]
    [SerializeField] private float barkVolume = 0.8f;
    private AudioSource audioSource;

    // ─── Estado ──────────────────────────────────────────────────────────────
    private bool playerNoPerto = false;

    // ─── UI de prompt (ScreenSpace - criada em runtime) ───────────────────
    private static GameObject s_promptCanvas;
    private static TextMeshProUGUI s_promptText;
    private static int s_activeCount = 0; // Quantos triggers estão com player dentro

    // ─── Referência ao root do Eptinho para posição do label ─────────────
    private Transform eptinhoRoot;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Garante que o BoxCollider é trigger
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;

        // Sobe na hierarquia para pegar o root do prefab instanciado (EptOracle)
        eptinhoRoot = transform.root;

        // Cria o canvas de prompt uma única vez (compartilhado entre instâncias)
        if (s_promptCanvas == null)
            CriarPromptUI();
    }

    void OnDestroy()
    {
        if (playerNoPerto)
        {
            playerNoPerto = false;
            s_activeCount = Mathf.Max(0, s_activeCount - 1);
            AtualizarPrompt();
        }
    }

    void Update()
    {
        if (playerNoPerto && Input.GetKeyDown(KeyCode.F))
            AbrirMenuOraculo();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (playerNoPerto) return;

        playerNoPerto = true;
        s_activeCount++;
        AtualizarPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerNoPerto) return;

        playerNoPerto = false;
        s_activeCount = Mathf.Max(0, s_activeCount - 1);
        AtualizarPrompt();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void AbrirMenuOraculo()
    {
        // Toca o latido ao interagir
        PlayBark();

        if (EptinhoMenuController.instancia != null)
        {
            EptinhoMenuController.instancia.AbrirMenu();
            Debug.Log("[EPTINHO ORACLE] Menu do Oráculo aberto!");
        }
        else
        {
            Debug.LogWarning("[EPTINHO ORACLE] EptinhoMenuController.instancia é nulo!");
        }
    }

    private void PlayBark()
    {
        if (barkClip == null) return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D para garantir que o player sempre ouça
        }

        audioSource.PlayOneShot(barkClip, barkVolume);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Criação e controle do prompt de tela
    // ─────────────────────────────────────────────────────────────────────────

    private static void CriarPromptUI()
    {
        s_promptCanvas = new GameObject("EptinhoPrompt_Canvas");
        DontDestroyOnLoad(s_promptCanvas);

        Canvas canvas = s_promptCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = s_promptCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        s_promptCanvas.AddComponent<GraphicRaycaster>();

        // Painel de fundo semi-transparente
        GameObject painelGO = new GameObject("PromptPanel");
        painelGO.transform.SetParent(s_promptCanvas.transform, false);
        Image painelImg = painelGO.AddComponent<Image>();
        painelImg.color = new Color(0.04f, 0.04f, 0.10f, 0.80f);
        RectTransform painelRect = painelGO.GetComponent<RectTransform>();
        painelRect.anchorMin = new Vector2(0.5f, 0f);
        painelRect.anchorMax = new Vector2(0.5f, 0f);
        painelRect.pivot     = new Vector2(0.5f, 0f);
        painelRect.sizeDelta = new Vector2(340f, 50f);
        painelRect.anchoredPosition = new Vector2(0f, 60f);

        // Texto do prompt
        GameObject textoGO = new GameObject("PromptText");
        textoGO.transform.SetParent(painelGO.transform, false);
        s_promptText = textoGO.AddComponent<TextMeshProUGUI>();
        s_promptText.text = "<color=#D1C0FF>[ F ]</color>  Falar com Eptinho";
        s_promptText.fontSize = 20;
        s_promptText.alignment = TextAlignmentOptions.Center;
        s_promptText.color = new Color(0.9f, 0.9f, 1f, 1f);
        RectTransform txtRect = textoGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(10f, 5f);
        txtRect.offsetMax = new Vector2(-10f, -5f);

        s_promptCanvas.SetActive(false);
    }

    private static void AtualizarPrompt()
    {
        if (s_promptCanvas == null) return;
        s_promptCanvas.SetActive(s_activeCount > 0);
    }
}

/// <summary>Billboard simples — mantido para compatibilidade mas não usado na nova implementação.</summary>
public class EptinhoBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }
}
