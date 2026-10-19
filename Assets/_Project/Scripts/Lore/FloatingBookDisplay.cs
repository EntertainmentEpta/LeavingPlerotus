using UnityEngine;
using TMPro;

/// <summary>
/// Exibe o Caderno do Pesquisador flutuando no Laboratório da Base (estilo Mesa de Encantamento do Minecraft).
/// - Fica flutuando e girando suavemente no ar sobre o pedestal.
/// - Ao se aproximar do jogador, gira para encará-lo suavemente como na Enchantment Table do Minecraft.
/// - Se desbloqueado (hasFoundResearcherBook == true), interagir [E/F] abre o Caderno (JournalBookUI).
/// - Se bloqueado, exibe aviso e orienta sobre o comando/atalho F8 para destravar para testes.
/// - Atalho F8 desbloqueia instantaneamente todas as escrituras e o caderno.
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
    public float interactionDistance = 3.2f;

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

    private Vector3 initialLocalPos = new Vector3(0f, 1.25f, 0f);
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

        EnsureStandGeometry();
        EnsureBookModel();
        EnsureLightAndVfx();
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
    /// Garante que o pedestal físico (cilindro de pedra/metal) exista mesmo se não estiver no prefab.
    /// </summary>
    public void EnsureStandGeometry()
    {
        if (transform.Find("StandGeometry") != null) return;

        GameObject standRoot = new GameObject("StandGeometry");
        standRoot.transform.SetParent(transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material standMat = new Material(shader);
        standMat.color = new Color(0.12f, 0.13f, 0.16f);

        // Base inferior
        GameObject baseStep = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseStep.name = "Pedestal_Base";
        baseStep.transform.SetParent(standRoot.transform, false);
        baseStep.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        baseStep.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
        baseStep.GetComponent<Renderer>().sharedMaterial = standMat;
        Destroy(baseStep.GetComponent<Collider>());

        // Coluna central
        GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        column.name = "Pedestal_Column";
        column.transform.SetParent(standRoot.transform, false);
        column.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        column.transform.localScale = new Vector3(0.55f, 0.44f, 0.55f);
        column.GetComponent<Renderer>().sharedMaterial = standMat;
        Destroy(column.GetComponent<Collider>());

        // Tampo superior
        GameObject topPlinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topPlinth.name = "Pedestal_Top";
        topPlinth.transform.SetParent(standRoot.transform, false);
        topPlinth.transform.localPosition = new Vector3(0f, 0.92f, 0f);
        topPlinth.transform.localScale = new Vector3(0.85f, 0.05f, 0.85f);
        topPlinth.GetComponent<Renderer>().sharedMaterial = standMat;
    }

    /// <summary>
    /// Garante que o modelo 3D do livro esteja instanciado e visível.
    /// </summary>
    public void EnsureBookModel()
    {
        if (bookModel != null)
        {
            initialLocalPos = bookModel.localPosition;
            bookModel.gameObject.SetActive(true);
            return;
        }

        // 1. Procura em filhos já existentes
        Transform found = transform.Find("FloatingBook") ?? transform.Find("Book") ?? transform.Find("BookModel");
        if (found != null)
        {
            bookModel = found;
            initialLocalPos = bookModel.localPosition;
            bookModel.gameObject.SetActive(true);
            return;
        }

        foreach (Transform child in transform)
        {
            if (child.name.ToLower().Contains("book") || (child.name != "StandGeometry" && child.GetComponentInChildren<MeshRenderer>() != null))
            {
                bookModel = child;
                initialLocalPos = bookModel.localPosition;
                bookModel.gameObject.SetActive(true);
                return;
            }
        }

        // 2. Tenta carregar prefab dos Resources
        GameObject bookPrefab = Resources.Load<GameObject>("Lore/Book") ?? Resources.Load<GameObject>("Lore/vintage-book");
        if (bookPrefab != null)
        {
            GameObject inst = Instantiate(bookPrefab, transform);
            inst.name = "FloatingBook";
            inst.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            inst.transform.localRotation = Quaternion.Euler(20f, 0f, 10f);
            inst.transform.localScale = Vector3.one * 42f;
            bookModel = inst.transform;
            initialLocalPos = bookModel.localPosition;
            bookModel.gameObject.SetActive(true);
            Debug.Log("[FloatingBookDisplay] 📖 Modelo 3D do livro instanciado via Resources!");
            return;
        }

        // 3. Fallback procedural: cria um modelo 3D estilizado de livro
        GameObject procBook = CreateProceduralBook();
        procBook.transform.SetParent(transform, false);
        procBook.name = "FloatingBook_Procedural";
        procBook.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        procBook.transform.localRotation = Quaternion.Euler(20f, 0f, 10f);
        bookModel = procBook.transform;
        initialLocalPos = bookModel.localPosition;
        bookModel.gameObject.SetActive(true);
        Debug.Log("[FloatingBookDisplay] 📖 Modelo procedural do livro criado como garantia!");
    }

    private GameObject CreateProceduralBook()
    {
        GameObject bookRoot = new GameObject("ProceduralBook");

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Capa de Couro Escuro com Detalhes Dourados
        Material coverMat = new Material(litShader);
        coverMat.color = new Color(0.18f, 0.12f, 0.08f);

        // Miolo de Páginas Amareladas
        Material pagesMat = new Material(litShader);
        pagesMat.color = new Color(0.92f, 0.88f, 0.78f);

        // Bloco de Páginas
        GameObject pages = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pages.name = "PagesBlock";
        pages.transform.SetParent(bookRoot.transform, false);
        pages.transform.localPosition = Vector3.zero;
        pages.transform.localScale = new Vector3(0.55f, 0.10f, 0.75f);
        pages.GetComponent<Renderer>().sharedMaterial = pagesMat;
        Destroy(pages.GetComponent<Collider>());

        // Capa Superior
        GameObject topCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topCover.name = "Cover_Top";
        topCover.transform.SetParent(bookRoot.transform, false);
        topCover.transform.localPosition = new Vector3(0f, 0.055f, 0f);
        topCover.transform.localScale = new Vector3(0.58f, 0.015f, 0.78f);
        topCover.GetComponent<Renderer>().sharedMaterial = coverMat;
        Destroy(topCover.GetComponent<Collider>());

        // Capa Inferior
        GameObject bottomCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bottomCover.name = "Cover_Bottom";
        bottomCover.transform.SetParent(bookRoot.transform, false);
        bottomCover.transform.localPosition = new Vector3(0f, -0.055f, 0f);
        bottomCover.transform.localScale = new Vector3(0.58f, 0.015f, 0.78f);
        bottomCover.GetComponent<Renderer>().sharedMaterial = coverMat;
        Destroy(bottomCover.GetComponent<Collider>());

        // Lombada
        GameObject spine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spine.name = "Cover_Spine";
        spine.transform.SetParent(bookRoot.transform, false);
        spine.transform.localPosition = new Vector3(-0.29f, 0f, 0f);
        spine.transform.localScale = new Vector3(0.02f, 0.12f, 0.78f);
        spine.GetComponent<Renderer>().sharedMaterial = coverMat;
        Destroy(spine.GetComponent<Collider>());

        return bookRoot;
    }

    public void EnsureLightAndVfx()
    {
        if (bookPointLight == null)
        {
            GameObject lightObj = new GameObject("BookEnchantmentLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            bookPointLight = lightObj.AddComponent<Light>();
            bookPointLight.type = LightType.Point;
            bookPointLight.color = new Color(0.35f, 0.95f, 1.0f);
            bookPointLight.range = 4.0f;
            bookPointLight.intensity = 2.2f;
        }

        if (sparkleVfx == null)
        {
            Transform found = transform.Find("EnchantmentRunes");
            if (found != null) sparkleVfx = found.gameObject;
        }
    }

    /// <summary>
    /// Atualiza visuais e intensidade da iluminação com base no estado do save.
    /// O modelo do livro é mantido visível para visualização da mesa de encantamento.
    /// </summary>
    public void CheckBookVisibility()
    {
        bool hasBook = LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook;

        EnsureBookModel();

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
            bookPointLight.enabled = true;
            bookPointLight.intensity = hasBook ? 2.2f : 0.4f;
            bookPointLight.color = hasBook ? new Color(0.35f, 0.95f, 1.0f) : new Color(0.5f, 0.6f, 0.75f);
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

        bool hasBook = LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook;
        UpdatePromptText(hasBook);
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

    private void UpdatePromptText(bool hasBook)
    {
        if (promptTextMesh != null)
        {
            KeyCode currentKey = GetCurrentInteractKey();
            if (hasBook)
            {
                promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
            }
            else
            {
                promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#AAAAAA><b>Pedestal Vazio</b></color>\n<size=75%><color=#888888>(Caderno perdido em Plerotus | <color=#FFE500>F8</color> Cheat)</color></size>";
            }
        }
    }

    void Update()
    {
        // ── 0. Atalho Global F8 para desbloqueio imediato para testes ──
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Debug.Log("[FloatingBookDisplay] 🔑 Tecla F8 pressionada: Desbloqueando todas as 18 escrituras e o Caderno!");
            LoreManager.Instance.UnlockAllScripturesAndBook();
            ToggleJournal();
        }

        bool hasBook = LoreManager.Instance != null && LoreManager.Instance.HasFoundResearcherBook;

        // ── 1. Checagem de Proximidade do Jogador ──
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
        else
        {
            playerInRange = false;
        }

        // ── 2. Animação Flutuante Estilo Minecraft Enchantment Table ──
        if (bookModel != null && bookModel.gameObject.activeSelf)
        {
            // Bobbing suave (subir e descer)
            float newY = initialLocalPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            bookModel.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);

            if (playerInRange && playerTransform != null)
            {
                // Estilo Mesa de Encantamento do Minecraft: O livro suavemente vira para encarar o jogador!
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
                float targetIntensity = playerInRange ? (hasBook ? 2.6f : 1.6f) : (hasBook ? 1.8f : 0.9f);
                bookPointLight.intensity = Mathf.Lerp(bookPointLight.intensity, targetIntensity + Mathf.Sin(Time.time * 2.8f) * 0.25f, Time.deltaTime * 3f);
            }
        }

        // ── 3. Fade Suave do Prompt WorldSpace ──
        if (promptUI != null && promptCanvasGroup != null)
        {
            UpdatePromptText(hasBook);

            bool isJournalOpen = JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen;
            bool shouldShow = playerInRange && !isJournalOpen;

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
        if (!playerInRange) return;
        if (DialogManager.BlocksGameplayInput) return;

        KeyCode interactKey = GetCurrentInteractKey();
        if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            if (!hasBook)
            {
                Debug.Log("[FloatingBookDisplay] Interação no pedestal vazio.");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(
                        "PEDESTAL VAZIO\nO Caderno de Campo deve ser resgatado em Plerotus.\n(Pressione F8 se quiser desbloqueá-lo como Cheat de teste)"
                    );
                }
                return;
            }
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
            // Coordenadas centrais e abertas do Laboratório da Base, desclipadas de todas as paredes
            Vector3 targetSpawnPos = new Vector3(-34.0f, 0.05f, -1.0f);
            Quaternion targetSpawnRot = Quaternion.Euler(0f, 90f, 0f);

            FloatingBookDisplay existing = FindFirstObjectByType<FloatingBookDisplay>();
            if (existing != null)
            {
                // Se a estante existente estiver clipada na parede norte (Z > 2.0f ou distante do centro)
                if (existing.transform.position.z > 2.0f || Vector3.Distance(existing.transform.position, targetSpawnPos) > 6.0f)
                {
                    existing.transform.position = targetSpawnPos;
                    existing.transform.rotation = targetSpawnRot;
                    Debug.Log($"[FloatingBookDisplay] 📖 Pedestal existente reposicionado para o centro aberto do laboratório em {targetSpawnPos}!");
                }
                existing.EnsureStandGeometry();
                existing.EnsureBookModel();
                existing.EnsureLightAndVfx();
                existing.CheckBookVisibility();
                return;
            }

            GameObject prefab = Resources.Load<GameObject>("Lore/FloatingBookPedestal");
            if (prefab != null)
            {
                GameObject inst = Instantiate(prefab, targetSpawnPos, targetSpawnRot);
                inst.name = "FloatingBookPedestal_Auto";
                FloatingBookDisplay disp = inst.GetComponent<FloatingBookDisplay>();
                if (disp != null)
                {
                    disp.EnsureStandGeometry();
                    disp.EnsureBookModel();
                    disp.EnsureLightAndVfx();
                    disp.CheckBookVisibility();
                }
                Debug.Log($"[FloatingBookDisplay] 📖 Pedestal Flutuante auto-instanciado no centro aberto do Laboratório em {targetSpawnPos}!");
            }
        }
    }
}
