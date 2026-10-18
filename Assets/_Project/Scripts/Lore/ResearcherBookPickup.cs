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

        EnsureCollider();
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
        string savedKey = PlayerPrefs.GetString("KeyInteract", "E");
        try
        {
            return (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey);
        }
        catch
        {
            return KeyCode.E;
        }
    }

    private void UpdatePromptText()
    {
        if (promptTextMesh != null)
        {
            KeyCode currentKey = GetCurrentInteractKey();
            promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{promptActionText}</b></color>";
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
