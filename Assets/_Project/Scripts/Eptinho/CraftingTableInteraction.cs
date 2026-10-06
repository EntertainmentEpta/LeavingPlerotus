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
    [Tooltip("Som reproduzido ao interagir/falar com o robô de síntese.")]
    public AudioClip talkSound;

    [Tooltip("Som reproduzido ao sintetizar/craftar um item com sucesso.")]
    public AudioClip synthesizeSuccessSound;

    [Tooltip("Som reproduzido quando não for possível sintetizar (falta de materiais ou requisitos).")]
    public AudioClip synthesizeFailSound;

    [Range(0f, 1f)]
    [Tooltip("Volume dos efeitos sonoros do robô.")]
    public float soundVolume = 1f;

    [Header("Configuração de Interação")]
    [Tooltip("Texto da ação (ex: Interagir, Craftar, Falar).")]
    public string actionText = "Interagir";

    [Header("UI do Robozinho")]
    public GameObject promptUI;
    public TextMeshProUGUI promptTextMesh;
    public float fadeSpeed = 6f;

    [Header("Modelos")]
    public Transform robotModelTransform;

    private bool playerPerto = false;
    private AudioSource audioSource;
    private CanvasGroup promptCanvasGroup;

    void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D som limpo de interface/NPC
        }
    }

    void Start()
    {
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

        if (robotModelTransform == null) robotModelTransform = transform;
        
        UpdatePromptText();
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
            promptTextMesh.text = $"[ {currentKey.ToString()} ] {actionText}";
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerPerto = true;
        UpdatePromptText();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerPerto = false;
        
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
        {
            CraftingUI.Instance.CloseCrafting();
            if (RobotPreviewManager.Instance != null) RobotPreviewManager.Instance.Deactivate();
        }
    }

    void Update()
    {
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

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

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

        // === 2. LÓGICA DE INTERAÇÃO (USANDO A TECLA DINÂMICA OU T) ===
        KeyCode interactKey = GetCurrentInteractKey();

        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.T))
        {
            PlayTalkSound();

            if (CraftingUI.Instance != null)
            {
                if (CraftingUI.Instance.IsOpen())
                {
                    CraftingUI.Instance.CloseCrafting();
                    if (RobotPreviewManager.Instance != null) RobotPreviewManager.Instance.Deactivate();
                }
                else
                {
                    CraftingUI.Instance.OpenCrafting();
                    
                    if (RobotPreviewManager.Instance != null) 
                    {
                        RobotPreviewManager.Instance.Activate(robotModelTransform);
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
            }
        }
    }

    // ─── Métodos de Áudio ────────────────────────────────────────────────────

    public void PlayTalkSound()
    {
        PlayAudio(talkSound);
    }

    public void PlaySynthesizeSuccessSound()
    {
        PlayAudio(synthesizeSuccessSound);
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
            audioSource.PlayOneShot(clip, soundVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, soundVolume);
        }
    }
}
