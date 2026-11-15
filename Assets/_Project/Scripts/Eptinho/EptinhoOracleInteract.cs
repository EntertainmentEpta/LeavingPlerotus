using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia a interação do jogador com o Eptinho Oráculo na Base.
/// Adicionado automaticamente pelo EptinhoController no filho "Trigger Menu".
/// Quando o player entra no BoxCollider trigger, exibe um prompt de tela diretamente
/// acima do Eptinho e permite abrir o menu do Bestiário/Oráculo ao pressionar a tecla de interação.
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
    public float fadeSpeed = 7f;

    [Header("Posicionamento Acima do Eptinho")]
    [Tooltip("Altura em metros acima do Eptinho para exibir o prompt.")]
    public float promptHeightOffset = 0.95f;
    [Tooltip("Avanço em metros em direção à câmera.")]
    public float promptForwardOffset = 0.25f;
    [Tooltip("Escala geral do prompt no mundo.")]
    public float promptScale = 0.008f;
    [Tooltip("Tamanho da fonte.")]
    public float fontSize = 30f;

    public GameObject promptUI;
    public TextMeshProUGUI promptTextMesh;
    private CanvasGroup promptCanvasGroup;

    // ─── Estado ──────────────────────────────────────────────────────────────
    private bool playerNoPerto = false;
    private Transform eptinhoTransform;

    void Start()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            bc.isTrigger = true;
            // Garante raio de colisão confortável para interação
            if (bc.size.x < 2.5f) bc.size = new Vector3(2.6f, 2.0f, 2.6f);
        }

        // Identifica o transform real do modelo do Eptinho
        if (transform.name.Contains("Trigger") && transform.parent != null)
        {
            eptinhoTransform = transform.parent;
        }
        else
        {
            eptinhoTransform = transform;
        }

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
        // Não parenta em transform.root para nunca herdar offsets de salas distantes
        promptUI.transform.SetParent(eptinhoTransform != null ? eptinhoTransform : transform, false);

        Canvas canvas = promptUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        RectTransform rect = promptUI.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 70f);
        rect.localScale = Vector3.one * promptScale;

        promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        // Fundo cinza minimalista com ZTest Always para nunca ser cortado pelo cenário
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptUI.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.07f, 0.35f);
        bgImg.raycastTarget = false;

        Shader uiShader = Shader.Find("UI/Default");
        if (uiShader != null)
        {
            Material bgMat = new Material(uiShader);
            bgMat.SetFloat(Shader.PropertyToID("unity_GUIZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            bgMat.renderQueue = 3999;
            bgImg.material = bgMat;
        }

        // Texto com fonte maior e tipografia consistente
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptUI.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        promptTextMesh = textObj.AddComponent<TextMeshProUGUI>();
        promptTextMesh.fontSize = fontSize;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.color = Color.white;
        promptTextMesh.raycastTarget = false;
        promptTextMesh.outlineWidth = 0.22f;
        promptTextMesh.outlineColor = new Color32(0, 0, 0, 220);
        promptTextMesh.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (font != null) promptTextMesh.font = font;

        if (promptTextMesh.fontSharedMaterial != null)
        {
            Material fontOverlayMat = new Material(promptTextMesh.fontSharedMaterial);
            Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay") ??
                                   Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            if (overlayShader != null) fontOverlayMat.shader = overlayShader;
            fontOverlayMat.SetFloat(Shader.PropertyToID("unity_GUIZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            fontOverlayMat.SetFloat(Shader.PropertyToID("_ZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            fontOverlayMat.renderQueue = 4000;
            promptTextMesh.fontSharedMaterial = fontOverlayMat;
        }

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

        // Fade suave e posicionamento dinâmico diretamente sobre o Eptinho
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

            if (promptUI.activeSelf && Camera.main != null && eptinhoTransform != null)
            {
                Vector3 eptinhoPos = eptinhoTransform.position;
                Vector3 camPos = Camera.main.transform.position;
                Vector3 toCam = (camPos - eptinhoPos).normalized;

                promptUI.transform.position = eptinhoPos + (Vector3.up * promptHeightOffset) + (toCam * promptForwardOffset);
                promptUI.transform.forward = Camera.main.transform.forward;
                promptUI.transform.localScale = Vector3.one * promptScale;
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
        if (barkClip == null)
        {
            barkClip = Resources.Load<AudioClip>("SoundFX/Eptinho/Bark_Eptinho");
        }

        if (barkClip != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.PlayOneShot(barkClip, barkVolume);
        }
    }
}
