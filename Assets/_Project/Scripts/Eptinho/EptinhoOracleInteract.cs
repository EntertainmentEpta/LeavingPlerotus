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

    // ─── Configuração do Prompt ──────────────────────────────────────────────
    [Header("UI do Eptinho (Estilo Robozinho)")]
    public string actionText = "Falar com Eptinho";
    public float fadeSpeed = 6f;
    public Vector3 promptOffset = new Vector3(0f, 2.2f, 0f);

    public GameObject promptUI;
    public TextMeshProUGUI promptTextMesh;
    private CanvasGroup promptCanvasGroup;

    // ─── Estado ──────────────────────────────────────────────────────────────
    private bool playerNoPerto = false;
    private Transform eptinhoRoot;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;

        eptinhoRoot = transform.root;

        BuildWorldPromptUI();
        UpdatePromptText();
    }

    private KeyCode GetCurrentInteractKey()
    {
        string savedKey = PlayerPrefs.GetString("KeyInteract", "F");
        try
        {
            return (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey);
        }
        catch
        {
            return KeyCode.F;
        }
    }

    private void UpdatePromptText()
    {
        if (promptTextMesh != null)
        {
            KeyCode currentKey = GetCurrentInteractKey();
            promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
        }
    }

    private void BuildWorldPromptUI()
    {
        if (promptUI != null)
        {
            promptCanvasGroup = promptUI.GetComponent<CanvasGroup>() ?? promptUI.AddComponent<CanvasGroup>();
            promptCanvasGroup.alpha = 0f;
            promptUI.SetActive(false);
            return;
        }

        promptUI = new GameObject("Eptinho_PromptUI");
        promptUI.transform.SetParent(eptinhoRoot != null ? eptinhoRoot : transform, false);
        promptUI.transform.localPosition = promptOffset;

        Canvas canvas = promptUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;

        RectTransform rect = promptUI.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 60f);
        rect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        // Fundo cinza bem sutil e transparente (sem bordas ciano)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptUI.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.07f, 0.35f);
        bgImg.raycastTarget = false;

        // Borda ciano removida completamente para visual minimalista e flutuante

        // Texto com tipografia consistente
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptUI.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        promptTextMesh = textObj.AddComponent<TextMeshProUGUI>();
        promptTextMesh.fontSize = 22f;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.color = Color.white;
        promptTextMesh.raycastTarget = false;
        promptTextMesh.outlineWidth = 0.22f;
        promptTextMesh.outlineColor = new Color32(0, 0, 0, 220);

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (font != null) promptTextMesh.font = font;

        promptUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNoPerto = true;
        UpdatePromptText();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNoPerto = false;
    }

    void Update()
    {
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused)
        {
            if (promptCanvasGroup != null) promptCanvasGroup.alpha = 0f;
            return;
        }

        bool isMenuOpen = EptinhoMenuController.instancia != null && EptinhoMenuController.instancia.IsOpen();
        bool shouldShow = playerNoPerto && !isMenuOpen;

        // Fade suave idêntico ao CraftingTableInteraction
        if (promptUI != null && promptCanvasGroup != null)
        {
            float targetAlpha = shouldShow ? 1f : 0f;

            if (shouldShow && !promptUI.activeSelf)
            {
                promptUI.SetActive(true);
                UpdatePromptText();
            }

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            if (!shouldShow && promptCanvasGroup.alpha <= 0.01f && promptUI.activeSelf)
            {
                promptUI.SetActive(false);
            }

            if (promptUI.activeSelf && Camera.main != null)
            {
                promptUI.transform.forward = Camera.main.transform.forward;
            }
        }

        if (!playerNoPerto || isMenuOpen) return;

        KeyCode interactKey = GetCurrentInteractKey();
        if (Input.GetKeyDown(interactKey))
        {
            AbrirMenuOraculo();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void AbrirMenuOraculo()
    {
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
            audioSource.spatialBlend = 0f; // 2D para audibilidade limpa
        }

        audioSource.PlayOneShot(barkClip, barkVolume);
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
