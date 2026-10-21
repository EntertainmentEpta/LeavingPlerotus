using UnityEngine;
using TMPro;

/// <summary>
/// Objeto físico no chão da dungeon que representa o Caderno Antigo do Pesquisador.
/// - Aparece em uma sala inicial ou sala pré-determinada.
/// - Se o jogador já tiver pego o caderno (hasFoundResearcherBook == true), destrói a si mesmo no início.
/// - Ao interagir com [E], o caderno é catalogado permanentemente, salvo no disco,
///   Eptinho dispara o aviso com sua análise, e o livro vai para o display do laboratório.
/// </summary>
public class ResearcherBookPickup : MonoBehaviour
{
    [Header("Identificação e Texto")]
    public string promptActionText = "Pegar Caderno do Pesquisador";

    [Header("Interação")]
    public float interactionDistance = 2.5f;

    [Header("Efeitos Visuais e Sonoros")]
    public GameObject pickupVfxPrefab;
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float soundVolume = 0.9f;

    [Header("UI Prompt")]
    public GameObject promptUI;
    public TextMeshProUGUI promptTextMesh;
    public float fadeSpeed = 7f;

    private bool isCollected = false;
    private bool playerInRange = false;
    private CanvasGroup promptCanvasGroup;
    private Transform playerTransform;

    void Awake()
    {
        // Se o jogador já encontrou o caderno anteriormente, não reaparece no chão
        if (LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook)
        {
            Destroy(gameObject);
            return;
        }

        SetupPromptUI();
    }

    void Start()
    {
        if (LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook)
        {
            Destroy(gameObject);
            return;
        }

        // Garante que o caderno nunca fique afundado no piso
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + 0.35f, transform.position.z);
        }

        EnsureCollider();

        if (promptUI == null)
        {
            BuildDefaultPromptUI();
        }
    }

    private void EnsureCollider()
    {
        Collider c = GetComponent<Collider>();
        if (c == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = interactionDistance;
        }
    }

    private void BuildDefaultPromptUI()
    {
        promptUI = new GameObject("BookPrompt_WorldCanvas");
        promptUI.transform.SetParent(transform, false);
        promptUI.transform.localPosition = new Vector3(0, 1.0f, 0);

        Canvas canvas = promptUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        RectTransform canvasRt = promptUI.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(420, 90);
        canvasRt.localScale = Vector3.one * 0.0075f;

        promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptUI.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        UnityEngine.UI.Image bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.12f, 0.90f);

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptUI.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        promptTextMesh = textObj.AddComponent<TextMeshProUGUI>();
        promptTextMesh.fontSize = 24f;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.color = Color.white;
        promptTextMesh.enableWordWrapping = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                          ?? TMP_Settings.defaultFontAsset;
        if (font != null) promptTextMesh.font = font;

        UpdatePromptText();
        promptUI.SetActive(false);
    }

    private void SetupPromptUI()
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
            promptTextMesh.text = $"[ <color=#FFE500>F</color> ]  <color=#FFFFFF><b>Aperte <color=#FFE500>F</color> para recolher o Caderno</b></color>";
        }
    }

    void Update()
    {
        if (isCollected) return;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            playerInRange = (dist <= interactionDistance);
        }

        // Fade suave do prompt
        if (promptUI != null && promptCanvasGroup != null)
        {
            float targetAlpha = playerInRange ? 1f : 0f;
            if (playerInRange && !promptUI.activeSelf) promptUI.SetActive(true);

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            if (!playerInRange && promptCanvasGroup.alpha <= 0.01f && promptUI.activeSelf)
            {
                promptUI.SetActive(false);
            }

            if (promptUI.activeSelf && Camera.main != null)
            {
                promptUI.transform.forward = Camera.main.transform.forward;
            }
        }

        if (!playerInRange) return;
        if (DialogManager.BlocksGameplayInput) return;

        KeyCode interactKey = GetCurrentInteractKey();
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        Debug.Log("[LORE] 📖 Caderno do Pesquisador coletado no chão!");

        // 1. Registra no LoreManager e salva permanentemente no disco
        if (LoreManager.Instance != null)
        {
            LoreManager.Instance.DiscoverResearcherBook();
        }
        else if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            SaveManager.instance.CachedData.hasFoundResearcherBook = true;
            SaveManager.instance.SavePersistentData();
        }

        // 2. Toca som de coleta
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        }

        // 3. Spawna partículas de coleta
        if (pickupVfxPrefab != null)
        {
            Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        }

        // 4. Se houver Quest do caderno ativa no QuestManager, avança progresso
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.AdvanceQuest(QuestType.Interact, "researcher_book_pickup", 1);
        }

        // 5. Destrói o objeto no chão
        Destroy(gameObject, 0.2f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerInRange = true;
            playerTransform = other.transform;
            UpdatePromptText();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerInRange = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
