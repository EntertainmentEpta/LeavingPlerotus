using TMPro;
using UnityEngine;

/// <summary>
/// Script de interação física com o Terminal de Seleção de Modo na Base.
/// Segue exatamente o padrão de CraftingTableInteraction:
/// - BoxCollider Trigger (>= 3.5 x 2.5 x 3.5) + failsafe contínuo por distância no Update (<= 3.0m / > 3.8m).
/// - Prompt WorldSpace auto-gerado com billboard para Camera.main e fade suave via CanvasGroup.
/// - Lê KeyInteract de PlayerPrefs (padrão "F") e aceita KeyCode.E como atalho secundário.
/// - Atualiza luz/emissão do terminal (ciano para Normal, laranja #ffaa00 para Endless).
/// </summary>
public class GameModeTerminalInteraction : MonoBehaviour
{
    public static GameModeTerminalInteraction Instance { get; private set; }

    [Header("Configuração de Interação")]
    [Tooltip("Texto da ação exibido no prompt flutuante.")]
    public string actionText = "Interagir";

    [Header("UI do Terminal")]
    public GameObject promptUI;
    public TMP_Text promptTextMesh;
    public float fadeSpeed = 6f;

    [Header("Feedback Visual de Modo (Luz / Emissão)")]
    [Tooltip("PointLight do terminal que muda de cor conforme o modo ativo.")]
    public Light statusLight;

    [Tooltip("Renderers da tela/farol do terminal que mudam de cor/emissão conforme o modo ativo.")]
    public Renderer[] statusEmissionRenderers;

    [Tooltip("Cor do terminal no Modo Normal (ciano).")]
    public Color normalModeColor = new Color(0.20f, 0.85f, 0.88f, 1f);

    [Tooltip("Cor do terminal no Modo Sem Fim / Endless (laranja #ffaa00).")]
    public Color endlessModeColor = new Color(1.0f, 0.667f, 0.0f, 1f);

    [Tooltip("Intensidade base da luz de status do terminal.")]
    public float statusLightIntensity = 3.2f;

    [Header("Áudio (Opcional)")]
    public AudioClip interactSound;
    public AudioClip modeChangedSound;
    [Range(0f, 1f)] public float soundVolume = 0.55f;

    private bool playerPerto = false;
    private Transform playerTransform;
    private CanvasGroup promptCanvasGroup;
    private AudioSource audioSource;
    private bool lastKnownEndlessState = false;

    private void Awake()
    {
        if (gameObject.name.Contains("Clone") || gameObject.name.Contains("Preview")) return;
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.75f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 16f;
        }
    }

    private void Start()
    {
        EnsureColliderAndPromptUI();
        AutoBindVisualReferencesIfEmpty();

        if (promptUI != null)
        {
            promptCanvasGroup = promptUI.GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
            }
            promptCanvasGroup.alpha = 0f;
            promptUI.SetActive(false);
        }

        if (promptTextMesh != null)
        {
            promptTextMesh.outlineWidth = 0.22f;
            promptTextMesh.outlineColor = new Color32(0, 0, 0, 220);
        }

        lastKnownEndlessState = IsEndlessActive();
        RefreshModeVisuals();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static bool IsEndlessActive()
    {
        return RunManager.instance != null && RunManager.instance.isEndlessMode;
    }

    /// <summary>
    /// Busca a tecla de interação configurada pelo jogador no KeybindManager (PlayerPrefs).
    /// </summary>
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

    /// <summary>
    /// Atualiza o prompt WorldSpace exibindo a tecla configurada e o modo ativo no momento.
    /// </summary>
    public void UpdatePromptText()
    {
        if (promptTextMesh == null) return;

        KeyCode currentKey = GetCurrentInteractKey();
        bool isEndless = IsEndlessActive();
        string modeLabel = isEndless
            ? "<color=#FFAA00>Modo: Sem Fim</color>"
            : "<color=#52D1BF>Modo: Normal</color>";

        promptTextMesh.text =
            $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{actionText}</b></color> — {modeLabel}";
    }

    /// <summary>
    /// Chamado pelo GameModeSelectionUI ao trocar de modo para atualizar visual, som e aviso.
    /// </summary>
    public void OnModeSelected(bool endless)
    {
        lastKnownEndlessState = endless;
        RefreshModeVisuals();
        PlayAudio(modeChangedSound != null ? modeChangedSound : interactSound);

        if (EptinhoPopupController.instancia != null)
        {
            string aviso = endless
                ? "Terminal configurado: MODO SEM FIM (ENDLESS) ativado!"
                : "Terminal configurado: MODO NORMAL ativado!";
            EptinhoPopupController.instancia.MostrarPopupAviso(aviso);
        }
    }

    /// <summary>
    /// Atualiza a cor da luz e emissão do terminal (ciano = Normal, laranja #ffaa00 = Endless) e o texto do prompt.
    /// </summary>
    public void RefreshModeVisuals()
    {
        bool isEndless = IsEndlessActive();
        Color targetColor = isEndless ? endlessModeColor : normalModeColor;

        if (statusLight != null)
        {
            statusLight.color = targetColor;
            statusLight.intensity = statusLightIntensity;
        }

        if (statusEmissionRenderers != null)
        {
            for (int i = 0; i < statusEmissionRenderers.Length; i++)
            {
                Renderer r = statusEmissionRenderers[i];
                if (r == null) continue;

                Material mat = r.material;
                if (mat == null) continue;

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", targetColor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", targetColor * 1.6f);
                }
            }
        }

        UpdatePromptText();
    }

    private void AutoBindVisualReferencesIfEmpty()
    {
        if (statusLight == null)
        {
            statusLight = GetComponentInChildren<Light>(true);
        }

        if (statusEmissionRenderers == null || statusEmissionRenderers.Length == 0)
        {
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            System.Collections.Generic.List<Renderer> matched = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string lower = all[i].gameObject.name.ToLower();
                if (lower.Contains("screen") || lower.Contains("beacon") || lower.Contains("status") || lower.Contains("holo"))
                {
                    matched.Add(all[i]);
                }
            }
            if (matched.Count > 0)
            {
                statusEmissionRenderers = matched.ToArray();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerTransform = other.transform;
        playerPerto = true;
        UpdatePromptText();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        float dist = Vector3.Distance(transform.position, other.transform.position);
        if (dist <= 3.5f) return;

        playerPerto = false;

        if (GameModeSelectionUI.Instance != null && GameModeSelectionUI.Instance.IsOpen())
        {
            GameModeSelectionUI.Instance.Close();
        }
    }

    private void Update()
    {
        // Sincroniza visual se o modo for alterado externamente (ex.: CheatConsole)
        bool currentEndless = IsEndlessActive();
        if (currentEndless != lastKnownEndlessState)
        {
            lastKnownEndlessState = currentEndless;
            RefreshModeVisuals();
        }

        // Pulso sutil na luz de status para destacar o terminal na Base
        if (statusLight != null)
        {
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.4f);
            statusLight.intensity = statusLightIntensity * pulse;
        }

        // === 0. VERIFICAÇÃO DE PROXIMIDADE CONTÍNUA (FAILSAFE) ===
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        bool isUIOpen = GameModeSelectionUI.Instance != null && GameModeSelectionUI.Instance.IsOpen();

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= 3.0f)
            {
                if (!playerPerto)
                {
                    playerPerto = true;
                    UpdatePromptText();
                }
            }
            else if (dist > 3.8f)
            {
                playerPerto = false;
                if (isUIOpen)
                {
                    GameModeSelectionUI.Instance.Close();
                    isUIOpen = false;
                }
            }
        }

        // === 1. FADE SUAVE DO PROMPT WORLDSPACE ===
        if (promptUI != null && promptCanvasGroup != null)
        {
            bool shouldShowPrompt = playerPerto && !isUIOpen;
            float targetAlpha = shouldShowPrompt ? 1f : 0f;

            if (shouldShowPrompt && !promptUI.activeSelf)
            {
                promptUI.SetActive(true);
            }

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            if (!shouldShowPrompt && promptCanvasGroup.alpha <= 0.01f && promptUI.activeSelf)
            {
                promptUI.SetActive(false);
            }

            if (promptUI.activeSelf && Camera.main != null)
            {
                promptUI.transform.forward = Camera.main.transform.forward;
            }
        }

        if (!playerPerto) return;
        if (DialogManager.BlocksGameplayInput) return;

        // === 2. INPUT DE INTERAÇÃO (TECLA CONFIGURADA OU ATALHO SECUNDÁRIO E) ===
        KeyCode interactKey = GetCurrentInteractKey();
        bool pressedInteract = Input.GetKeyDown(interactKey) || (interactKey != KeyCode.E && Input.GetKeyDown(KeyCode.E));

        if (pressedInteract)
        {
            GameModeSelectionUI ui = GameModeSelectionUI.EnsureInstance();
            if (ui.IsOpen())
            {
                ui.Close();
            }
            else if (!GamePause.IsPaused)
            {
                PlayAudio(interactSound);
                ui.Open(this);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && isUIOpen)
        {
            GameModeSelectionUI.Instance.Close();
        }
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.97f, 1.03f);
            audioSource.PlayOneShot(clip, soundVolume * GlobalAudioManager.SFXVolumeMultiplier);
        }
    }

    /// <summary>
    /// Garante BoxCollider Trigger (>= 3.5 x 2.5 x 3.5) e Prompt visual WorldSpace mesmo se o objeto for recriado.
    /// </summary>
    private void EnsureColliderAndPromptUI()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.6f, 2.6f, 3.6f);
            box.center = new Vector3(0f, 1.3f, 0f);
        }
        else
        {
            box.isTrigger = true;
            Vector3 s = box.size;
            s.x = Mathf.Max(s.x, 3.5f);
            s.y = Mathf.Max(s.y, 2.5f);
            s.z = Mathf.Max(s.z, 3.5f);
            box.size = s;
        }

        if (promptUI == null)
        {
            Transform existing = transform.Find("PromptUI") ?? transform.Find("Text (TMP)") ?? transform.Find("PromptUI_AutoGenerated");
            if (existing != null)
            {
                promptUI = existing.gameObject;
                promptTextMesh = promptUI.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                CreateAutoPromptUI();
            }
        }
    }

    private void CreateAutoPromptUI()
    {
        GameObject promptObj = new GameObject("PromptUI_AutoGenerated");
        promptObj.transform.SetParent(transform, false);
        promptObj.transform.localPosition = new Vector3(0f, 2.35f, 0f);

        Canvas canvas = promptObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;

        RectTransform rect = promptObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(540f, 90f);
        rect.localScale = Vector3.one * 0.008f;

        promptCanvasGroup = promptObj.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        GameObject textObj = new GameObject("Text (TMP)");
        textObj.transform.SetParent(promptObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        promptTextMesh = textObj.AddComponent<TextMeshProUGUI>();
        promptTextMesh.fontSize = 30f;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.textWrappingMode = TextWrappingModes.Normal;
        promptTextMesh.color = Color.white;
        promptTextMesh.outlineWidth = 0.28f;
        promptTextMesh.outlineColor = new Color32(0, 0, 0, 240);
        promptTextMesh.raycastTarget = false;

        promptUI = promptObj;
        promptUI.SetActive(false);
    }
}
