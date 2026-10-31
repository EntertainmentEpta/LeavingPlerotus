using UnityEngine;
using TMPro;

/// <summary>
/// Script de interação física com a Mesa de Trabalho (Robozinho).
/// - Detecta proximidade e abre a UI de Crafting.
/// - Fade Suave (CanvasGroup) no texto de interação.
/// - Conectado ao KeybindManager (Lê "KeyInteract" do PlayerPrefs automaticamente).
/// - Suporte a efeitos sonoros de fala, síntese bem-sucedida e falha.
/// </summary>
public class CraftingTableInteraction : MonoBehaviour
{
    public static CraftingTableInteraction Instance { get; private set; }

    [Header("Áudio / Sons do Robozinho")]
    [Tooltip("Sons reproduzidos ao interagir/falar com o robô. Um será escolhido aleatoriamente a cada interação.")]
    public AudioClip[] talkSounds;

    [Tooltip("Som reproduzido ao sintetizar/craftar um item com sucesso.")]
    public AudioClip synthesizeSuccessSound;

    [Tooltip("Som reproduzido quando não for possível sintetizar (falta de materiais ou requisitos).")]
    public AudioClip synthesizeFailSound;

    [Range(0f, 1f)]
    [Tooltip("Volume dos efeitos sonoros do robô.")]
    public float soundVolume = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Spatial Blend: 0 = 2D direto no fone, 1 = 3D atenuado pela distância.")]
    public float spatialBlend = 0.75f;

    [Header("Configuração de Interação")]
    [Tooltip("Texto da ação (ex: Interagir, Craftar, Falar).")]
    public string actionText = "Interagir";

    [Header("UI do Robozinho")]
    public GameObject promptUI;
    public TMP_Text promptTextMesh;
    public float fadeSpeed = 6f;

    [Header("Modelos")]
    public Transform robotModelTransform;

    [Header("Depuração e Objetivos")]
    [Tooltip("Marque para forçar a exibição da exclamação no Robozinho para testes rápidos.")]
    public bool testForceExclamation = false;

    public static bool hasInteractedWithRobot = false;

    private ObjectiveMarker roboMarker;
    private bool playerPerto = false;
    private Transform playerTransform;
    private AudioSource audioSource;
    private CanvasGroup promptCanvasGroup;

    void Awake()
    {
        if (gameObject.name.Contains("Clone") || gameObject.name.Contains("Preview")) return;
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = spatialBlend;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 18f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            // Áudio 3D configurado acima
        }

        // Checa se o progresso salvo já passou do ponto inicial do Robozinho
        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            if (SaveManager.instance.CachedData.craftedEquipmentIds.Contains("eq_consertar_porta") ||
                SaveManager.instance.CachedData.tutorialStep >= (int)TutorialStep.AbrirAbaArmas)
            {
                hasInteractedWithRobot = true;
            }
            else
            {
                hasInteractedWithRobot = false;
            }
        }
        else
        {
            hasInteractedWithRobot = false;
        }
    }

    void Start()
    {
        EnsureColliderAndPromptUI();

        if (promptUI != null)
        {
            promptCanvasGroup = promptUI.GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
            }
            
            promptCanvasGroup.alpha = 0f;
            promptUI.SetActive(false);

            // Remove bordas ciano e ajusta fundo para cinza sutil transparente
            foreach (var img in promptUI.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img.gameObject.name.ToLower().Contains("border"))
                {
                    img.gameObject.SetActive(false);
                }
                else if (img.gameObject.name.ToLower().Contains("bg") || img.gameObject.name.ToLower().Contains("background"))
                {
                    img.color = new Color(0.06f, 0.06f, 0.07f, 0.35f);
                }
            }
        }

        if (promptTextMesh != null)
        {
            promptTextMesh.outlineWidth = 0.22f;
            promptTextMesh.outlineColor = new Color32(0, 0, 0, 220);
        }

        if (robotModelTransform == null) robotModelTransform = transform;
        
        UpdatePromptText();

        // Configura marcador de objetivo automático no Robozinho
        roboMarker = GetComponent<ObjectiveMarker>();
        if (roboMarker == null) roboMarker = gameObject.AddComponent<ObjectiveMarker>();

        roboMarker.condition = ObjectiveMarker.TriggerCondition.Custom;
        roboMarker.heightOffset = 2.85f; // Acima da cabeça/antena do Robozin (mesh de 2.5m de altura)
        roboMarker.markerSize = new Vector2(48f, 48f);
        roboMarker.customVisibilityCondition = () =>
        {
            if (testForceExclamation) return true;

            // Se a mesa estiver aberta, não polui a tela com a exclamação flutuante
            bool isMesaOpen = (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen());
            if (isMesaOpen) return false;

            // Se o jogador já interagiu com o Robozinho nesta sessão, a exclamação desaparece permanentemente
            if (hasInteractedWithRobot) return false;

            // Se o save já avançou além do passo de falar com o robozinho ou já consertou a porta da ala
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep >= TutorialStep.AbrirAbaArmas)
                return false;

            if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
            {
                if (SaveManager.instance.CachedData.craftedEquipmentIds.Contains("eq_consertar_porta"))
                    return false;
            }

            // Caso contrário, a exclamação permanece visível para guiar o jogador até ele!
            return true;
        };
        roboMarker.CheckVisibility();
    }

    /// <summary>
    /// Busca a tecla de interação atualizada diretamente do sistema de Keybinds (PlayerPrefs).
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
    /// Atualiza o texto na tela para refletir a tecla exata configurada pelo jogador.
    /// </summary>
    private void UpdatePromptText()
    {
        if (promptTextMesh != null)
        {
            KeyCode currentKey = GetCurrentInteractKey();
            promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerTransform = other.transform;
        playerPerto = true;
        UpdatePromptText();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Se a interface estiver aberta ou o jogador ainda estiver fisicamente dentro do raio (<= 3.5m),
        // ignora o evento para prevenir fechamento prematuro por atualização de física.
        float dist = Vector3.Distance(transform.position, other.transform.position);
        if (dist <= 3.5f)
        {
            return;
        }

        playerPerto = false;
        
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
        {
            CraftingUI.Instance.CloseCrafting();
            if (RobotPreviewManager.Instance != null) RobotPreviewManager.Instance.Deactivate();
        }
    }

    void Update()
    {
        // === 0. VERIFICAÇÃO DE PROXIMIDADE CONTÍNUA (FAILSAFE) ===
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= 3.0f)
            {
                playerPerto = true;
            }
            else if (dist > 3.8f && (CraftingUI.Instance == null || !CraftingUI.Instance.IsOpen()))
            {
                playerPerto = false;
            }
        }

        // === 1. LÓGICA DO FADE SUAVE DO PROMPT ===
        if (promptUI != null && promptCanvasGroup != null)
        {
            bool isCraftingOpen = CraftingUI.Instance != null && CraftingUI.Instance.IsOpen();
            bool shouldShowPrompt = playerPerto && !isCraftingOpen;

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

        // === 2. LÓGICA DE INTERAÇÃO (USANDO A TECLA DINÂMICA OU T) ===
        KeyCode interactKey = GetCurrentInteractKey();

        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.T))
        {
            if (CraftingUI.Instance != null)
            {
                if (CraftingUI.Instance.IsOpen())
                {
                    CraftingUI.Instance.CloseCrafting();
                    if (RobotPreviewManager.Instance != null) RobotPreviewManager.Instance.Deactivate();
                    roboMarker?.CheckVisibility();
                }
                else
                {
                    PlayTalkSound();
                    hasInteractedWithRobot = true;

                    // Garante que o passo do tutorial avance para AbrirAbaArmas (passo 2)
                    if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep <= TutorialStep.FalarComRobozinho)
                    {
                        TutorialManager.Instance.currentStep = TutorialStep.AbrirAbaArmas;
                        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
                        {
                            SaveManager.instance.CachedData.tutorialStep = (int)TutorialStep.AbrirAbaArmas;
                            SaveManager.instance.SavePersistentData();
                        }
                    }

                    CraftingUI.Instance.OpenCrafting();
                    roboMarker?.CheckVisibility();
                    
                    if (RobotPreviewManager.Instance != null) 
                    {
                        RobotPreviewManager.Instance.Activate(robotModelTransform != null ? robotModelTransform : transform);
                    }
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
            {
                CraftingUI.Instance.CloseCrafting();
                if (RobotPreviewManager.Instance != null) RobotPreviewManager.Instance.Deactivate();
                roboMarker?.CheckVisibility();
            }
        }
    }

    // ─── Métodos de Áudio ────────────────────────────────────────────────────

    public void PlayTalkSound()
    {
        if (talkSounds != null && talkSounds.Length > 0)
        {
            AudioClip clip = talkSounds[Random.Range(0, talkSounds.Length)];
            PlayAudio(clip);
        }
    }

    public void PlaySynthesizeSuccessSound()
    {
        PlayAudio(synthesizeSuccessSound);

        // Dispara o Salto e Giro 360° com Luz de Comemoração!
        var animator = GetComponentInChildren<RobozinhoProceduralAnimator>();
        if (animator == null) animator = FindFirstObjectByType<RobozinhoProceduralAnimator>();
        if (animator != null) animator.TriggerCraftReaction();
    }

    public void PlaySynthesizeFailSound()
    {
        PlayAudio(synthesizeFailSound);
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.96f, 1.04f);
            audioSource.PlayOneShot(clip, soundVolume * GlobalAudioManager.SFXVolumeMultiplier);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, soundVolume * GlobalAudioManager.SFXVolumeMultiplier);
        }
    }

    /// <summary>
    /// Garante que o BoxCollider e o Prompt visual de interação [ F ] existam mesmo se o jogador trocar ou recriar o prefab.
    /// </summary>
    private void EnsureColliderAndPromptUI()
    {
        // Garante BoxCollider Trigger para detecção do jogador
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.5f, 2.5f, 3.5f);
            box.center = new Vector3(0f, 1f, 0f);
        }
        else
        {
            box.isTrigger = true;
            if (box.size.x < 1.5f || box.size.z < 1.5f)
            {
                box.size = new Vector3(3.5f, 2.5f, 3.5f);
            }
        }

        // Se o promptUI estiver nulo, busca nos filhos ou cria um novo automaticamente
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
        promptObj.transform.localPosition = new Vector3(0f, 2.2f, 0f); // Acima do robô

        Canvas canvas = promptObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;

        RectTransform rect = promptObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(350f, 80f);
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
        promptTextMesh.fontSize = 32;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.color = Color.white;
        promptTextMesh.outlineWidth = 0.28f;
        promptTextMesh.outlineColor = new Color32(0, 0, 0, 240);

        promptUI = promptObj;
        promptUI.SetActive(false);
    }

}