using UnityEngine;
using TMPro;

/// <summary>
/// Script de interação física com a Mesa de Trabalho (Robozinho).
/// - Detecta proximidade e abre a UI de Crafting.
/// - Fade Suave (CanvasGroup) no texto de interação.
/// - Conectado ao KeybindManager (Lê "KeyInteract" do PlayerPrefs automaticamente).
/// </summary>
public class CraftingTableInteraction : MonoBehaviour
{
    [Header("Configuração de Interação")]
    [Tooltip("Texto da ação (ex: Interagir, Craftar, Falar).")]
    public string actionText = "Interagir";

    [Header("UI do Robozinho")]
    public GameObject promptUI;
    public TextMeshProUGUI promptTextMesh;
    public float fadeSpeed = 6f;

    [Header("Som e Modelos")]
    public AudioClip roboSound;
    public Transform robotModelTransform;

    [Header("Depuração e Objetivos")]
    [Tooltip("Marque para forçar a exibição da exclamação no Robozinho para testes rápidos.")]
    public bool testForceExclamation = false;

    private ObjectiveMarker roboMarker;
    private bool playerPerto = false;
    private CanvasGroup promptCanvasGroup;

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
        roboMarker.heightOffset = 1.8f;
        roboMarker.markerSize = new Vector2(48f, 48f);
        roboMarker.customVisibilityCondition = () =>
        {
            if (testForceExclamation) return true;

            // 1. Passo do Tutorial: Falar com Robozinho
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialStep.FalarComRobozinho)
                return true;

            // 2. Quest da Porta: se a missão ativa for consertar a porta e ela ainda não foi consertada
            if (QuestManager.Instance != null && QuestManager.Instance.currentQuest != null)
            {
                if (QuestManager.Instance.currentQuest.questId == "quest_consertar_porta")
                {
                    bool portaJaConsertada = SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_consertar_porta");
                    if (!portaJaConsertada) return true;
                }
            }

            return false;
        };
    }

    /// <summary>
    /// Busca a tecla de interação atualizada diretamente do sistema de Keybinds (PlayerPrefs).
    /// </summary>
    private KeyCode GetCurrentInteractKey()
    {
        // Lê a mesma chave que o seu KeybindManager usa! Fallback é "F".
        string savedKey = PlayerPrefs.GetString("KeyInteract", "F");
        try 
        {
            return (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey);
        }
        catch 
        {
            return KeyCode.F; // Proteção caso ocorra algum erro na conversão
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
        playerPerto = true;
        
        // Atualiza o texto sempre que o jogador chegar perto (caso ele tenha mudado a tecla no menu há pouco)
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

        // === 2. LÓGICA DE INTERAÇÃO (USANDO A TECLA DINÂMICA) ===
        KeyCode interactKey = GetCurrentInteractKey();

        if (Input.GetKeyDown(interactKey))
        {
            if (roboSound != null)
            {
                AudioSource src = GetComponent<AudioSource>();
                if (src == null) src = gameObject.AddComponent<AudioSource>();
                src.PlayOneShot(roboSound);
            }

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
                    TutorialManager.Instance?.CompleteStep(TutorialStep.FalarComRobozinho);
                    
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
}