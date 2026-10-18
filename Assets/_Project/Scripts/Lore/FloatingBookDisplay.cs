using UnityEngine;
using TMPro;

/// <summary>
/// Exibe o Caderno do Pesquisador flutuando no Laboratório da Base (estilo Mesa de Encantamento do Minecraft).
/// - Fica flutuando e girando suavemente no ar sobre o pedestal.
/// - Só aparece se o jogador já tiver encontrado o caderno na dungeon (hasFoundResearcherBook == true).
/// - Ao se aproximar e pressionar a tecla de interação [E/F], abre a interface do Caderno/Códice (JournalBookUI).
/// </summary>
public class FloatingBookDisplay : MonoBehaviour
{
    public static FloatingBookDisplay Instance { get; private set; }

    [Header("Modelos e Efeitos Visuais")]
    [Tooltip("Objeto visual do livro 3D que flutua e gira.")]
    public Transform bookModel;

    [Tooltip("Efeito de partículas opcional de runas/poeira mística flutuando.")]
    public GameObject sparkleVfx;

    [Tooltip("Luz pontual suave que ilumina o livro.")]
    public Light bookPointLight;

    [Header("Animação Minecraft (Flutuar & Girar)")]
    [Tooltip("Velocidade de rotação contínua no modo ocioso (graus por segundo)")]
    public float rotationSpeed = 30f;

    [Tooltip("Velocidade de rotação ao encarar o jogador que se aproxima")]
    public float facePlayerSpeed = 4.2f;

    [Tooltip("Velocidade do balanço vertical (bobbing)")]
    public float bobSpeed = 2.2f;

    [Tooltip("Amplitude da subida e descida (em metros)")]
    public float bobHeight = 0.08f;

    [Tooltip("Inclinação natural do livro aberto/fechado")]
    public Vector3 baseTiltEuler = new Vector3(15f, 0f, 5f);

    [Header("Interação com o Jogador")]
    [Tooltip("Distância máxima para poder interagir com o livro.")]
    public float interactionDistance = 2.8f;

    [Tooltip("Texto descritivo da ação.")]
    public string actionText = "Abrir Caderno de Campo";

    [Header("UI Prompt")]
    public GameObject promptUI;
    public TextMeshProUGUI promptTextMesh;
    public float fadeSpeed = 7f;

    [Header("Áudio")]
    public AudioClip bookOpenSound;
    public AudioClip pageFlipSound;
    [Range(0f, 1f)] public float soundVolume = 0.85f;

    private Vector3 initialLocalPos;
    private bool playerInRange = false;
    private CanvasGroup promptCanvasGroup;
    private AudioSource audioSource;
    private Transform playerTransform;

    void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (bookModel == null)
        {
            Transform found = transform.Find("FloatingBook") ?? transform.Find("Book") ?? transform.Find("BookModel");
            if (found != null) bookModel = found;
            else
            {
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("book") || child.GetComponentInChildren<MeshRenderer>() != null)
                    {
                        bookModel = child;
                        break;
                    }
                }
            }
        }

        if (bookModel != null)
        {
            initialLocalPos = bookModel.localPosition;
        }
        else
        {
            initialLocalPos = Vector3.up * 1.15f;
        }
    }

    void Start()
    {
        SetupPromptUI();
        CheckBookVisibility();

        if (LoreManager.Instance != null)
        {
            LoreManager.OnResearcherBookDiscovered += CheckBookVisibility;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (LoreManager.Instance != null)
        {
            LoreManager.OnResearcherBookDiscovered -= CheckBookVisibility;
        }
    }

    /// <summary>
    /// Verifica se o livro já foi desbloqueado no save. Se não, oculta o modelo flutuante.
    /// </summary>
    public void CheckBookVisibility()
    {
        bool hasBook = LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook;

        // Se estiver em modo de teste no editor, pode manter ativo se bookModel existir
        if (bookModel != null)
        {
            bookModel.gameObject.SetActive(hasBook);
        }

        if (sparkleVfx != null)
        {
            sparkleVfx.SetActive(hasBook);
        }

        if (bookPointLight != null)
        {
            bookPointLight.enabled = hasBook;
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
            promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
        }
    }

    void Update()
    {
        bool hasBook = LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook;

        // ── 1. Checagem de Proximidade do Jogador ──
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null && hasBook)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            playerInRange = (dist <= interactionDistance);
        }
        else
        {
            playerInRange = false;
        }

        // ── 2. Animação Flutuante Estilo Minecraft Enchantment Table ──
        if (bookModel != null && hasBook && bookModel.gameObject.activeSelf)
        {
            // Bobbing suave (subir e descer)
            float newY = initialLocalPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            bookModel.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);

            if (playerInRange && playerTransform != null)
            {
                // Estilo Mesa de Encantamento: O livro suavemente vira para encarar o jogador!
                Vector3 lookDir = (playerTransform.position - bookModel.position);
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir) * Quaternion.Euler(baseTiltEuler);
                    bookModel.rotation = Quaternion.Slerp(bookModel.rotation, targetRot, Time.deltaTime * facePlayerSpeed);
                }
            }
            else
            {
                // Modo ocioso: giro contínuo suave no eixo Y
                bookModel.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }

            // Pulso místico suave da luz
            if (bookPointLight != null)
            {
                float targetIntensity = playerInRange ? 2.5f : 1.7f;
                bookPointLight.intensity = Mathf.Lerp(bookPointLight.intensity, targetIntensity + Mathf.Sin(Time.time * 2.8f) * 0.25f, Time.deltaTime * 3f);
            }
        }

        // ── 3. Fade Suave do Prompt WorldSpace ──
        if (promptUI != null && promptCanvasGroup != null)
        {
            bool isJournalOpen = JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen;
            bool shouldShow = playerInRange && hasBook && !isJournalOpen;

            float targetAlpha = shouldShow ? 1f : 0f;
            if (shouldShow && !promptUI.activeSelf) promptUI.SetActive(true);

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

        // ── 4. Entrada de Teclado (Interagir) ──
        if (!playerInRange || !hasBook) return;
        if (DialogManager.BlocksGameplayInput) return;

        KeyCode interactKey = GetCurrentInteractKey();
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            ToggleJournal();
        }
    }

    public void ToggleJournal()
    {
        if (JournalBookUI.Instance != null)
        {
            if (JournalBookUI.Instance.IsOpen)
            {
                JournalBookUI.Instance.CloseJournal();
            }
            else
            {
                PlayAudio(bookOpenSound);
                JournalBookUI.Instance.OpenJournal();
            }
        }
        else
        {
            Debug.Log("[FloatingBookDisplay] JournalBookUI não encontrado na cena! Criando fallback...");
            JournalBookUI.CreateOrGet();
            JournalBookUI.Instance?.OpenJournal();
        }
    }

    public void PlayAudio(AudioClip clip)
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }

    // ─── AUTO-INICIALIZAÇÃO NO LABORATÓRIO DA BASE ──────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSceneListener()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        string sceneName = scene.name != null ? scene.name.ToLower() : "";
        if (sceneName.Contains("base") && !sceneName.Contains("arena") && !sceneName.Contains("boss"))
        {
            if (FindFirstObjectByType<FloatingBookDisplay>() == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Lore/FloatingBookPedestal");
                if (prefab != null)
                {
                    Vector3 spawnPos = new Vector3(-39.2f, 0.05f, 5.8f);
                    GameObject craftSys = GameObject.Find("CraftingSystem");
                    if (craftSys != null)
                    {
                        spawnPos = craftSys.transform.position + new Vector3(2.5f, -2.09f, 0.4f);
                    }

                    GameObject inst = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, -45f, 0f));
                    inst.name = "FloatingBookPedestal_Auto";
                    Debug.Log($"[FloatingBookDisplay] 📖 Pedestal Flutuante do Caderno auto-instanciado na Base em {spawnPos}!");
                }
            }
        }
    }
}
