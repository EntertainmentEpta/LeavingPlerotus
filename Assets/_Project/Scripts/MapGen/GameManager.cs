using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Referências Globais")]
    public GameObject loadingScreenCanvas;
    public Slider loadingBar;
    [Tooltip("Tempo mínimo (em segundos) que a tela de loading com o Eptinho andando ficará visível.")]
    public float minimumLoadingTime = 3.0f;
    
    [HideInInspector]
    public GameObject currentPlayer;

    private bool isLevelReady = false;
    private bool isAsyncLoading = false;
    private float currentBarProgress = 0f;
    private GameObject persistentLoadingRoot;
    private CanvasGroup loadingCanvasGroup;
    private Canvas activeParentCanvas;
    private int savedParentSortingOrder = 0;
    private bool hasSavedSortingOrder = false;

    public static GameManager EnsureInstance()
    {
        if (instance != null) return instance;

        GameManager[] allManagers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allManagers != null && allManagers.Length > 0)
        {
            // Prioriza o GameManager que já possui referências ou RunManager junto
            GameManager best = allManagers[0];
            foreach (var gm in allManagers)
            {
                if (gm.GetComponent<RunManager>() != null || gm.loadingScreenCanvas != null)
                {
                    best = gm;
                    break;
                }
            }

            instance = best;
            if (!instance.gameObject.activeSelf) instance.gameObject.SetActive(true);
            DontDestroyOnLoad(instance.gameObject);
            instance.FindLoadingReferences();
            return instance;
        }

        GameObject go = new GameObject("GameManager");
        instance = go.AddComponent<GameManager>();
        return instance;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // Preserva referências caso este GameManager duplicado as tenha configuradas no Inspector
            if (instance.loadingScreenCanvas == null && this.loadingScreenCanvas != null)
                instance.loadingScreenCanvas = this.loadingScreenCanvas;
            if (instance.loadingBar == null && this.loadingBar != null)
                instance.loadingBar = this.loadingBar;

            // IMPORTANTE: Se este objeto também contém RunManager ou SaveManager, NÃO destrói o GameObject inteiro!
            if (GetComponent<RunManager>() != null || GetComponent<SaveManager>() != null)
            {
                DontDestroyOnLoad(gameObject);
                Destroy(this);
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }

        FindLoadingReferences();

        if (loadingScreenCanvas != null && !isAsyncLoading)
            HideLoadingScreenImmediate();
    }

    public void FindLoadingReferences(bool forceRebindToActiveScene = false)
    {
        if (forceRebindToActiveScene)
        {
            RestoreParentCanvasSorting();
            loadingScreenCanvas = null;
            loadingBar = null;
            loadingCanvasGroup = null;
        }

        // 1. Verifica se a referência atual ainda é válida e pertence a um Canvas de cena válido
        if (loadingScreenCanvas == null || (persistentLoadingRoot != null && loadingScreenCanvas.transform.IsChildOf(persistentLoadingRoot.transform)))
        {
            GameObject sceneLoadingObj = FindSceneLoadingScreenObject();
            if (sceneLoadingObj != null)
            {
                loadingScreenCanvas = sceneLoadingObj;
                loadingBar = null;

                // Se tínhamos criado um Canvas procedural temporário (ex: no MainMenu) e agora
                // estamos numa cena com Player Canvas Real, removemos o temporário!
                if (persistentLoadingRoot != null)
                {
                    Destroy(persistentLoadingRoot);
                    persistentLoadingRoot = null;
                }
            }
        }

        // 2. Se a cena atual não possui Player Canvas Real / LoadingScreen (ex: MainMenu),
        // constrói um Canvas de fallback temporário em ScreenSpaceOverlay
        if (loadingScreenCanvas == null)
        {
            BuildProceduralLoadingScreen();
        }

        // 3. Configura referências visuais (Slider, CanvasGroup, LoadingSpinner) SEM remover do Player Canvas Real!
        if (loadingScreenCanvas != null)
        {
            if (loadingBar == null)
            {
                loadingBar = loadingScreenCanvas.GetComponentInChildren<Slider>(true);
            }

            ConfigureLoadingScreenVisuals();
        }
    }

    private GameObject FindSceneLoadingScreenObject()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        // Prioridade 1: Busca dentro dos Canvases da cena ativa (ex: Player Canvas Real -> LoadingScreen)
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c == null || (persistentLoadingRoot != null && c.gameObject == persistentLoadingRoot))
                continue;

            // Prefere o Canvas da cena ativa
            if (c.gameObject.scene == activeScene)
            {
                Transform t = c.transform.Find("LoadingScreen");
                if (t == null) t = c.transform.Find("LoadingScreenCanvas");
                if (t == null) t = c.transform.Find("Canvas_Loading");
                if (t != null) return t.gameObject;
            }
        }

        // Prioridade 2: Busca em qualquer Canvas carregado (caso o Player Canvas Real seja DontDestroyOnLoad)
        foreach (var c in canvases)
        {
            if (c == null || (persistentLoadingRoot != null && c.gameObject == persistentLoadingRoot))
                continue;

            Transform t = c.transform.Find("LoadingScreen");
            if (t == null) t = c.transform.Find("LoadingScreenCanvas");
            if (t == null) t = c.transform.Find("Canvas_Loading");
            if (t != null) return t.gameObject;
        }

        // Prioridade 3: Busca pelo componente LoadingSpinner em objetos ativos ou inativos da cena
        LoadingSpinner[] spinners = Resources.FindObjectsOfTypeAll<LoadingSpinner>();
        foreach (var sp in spinners)
        {
            if (sp != null && sp.gameObject.scene.IsValid() && sp.transform.parent != null)
            {
                if (persistentLoadingRoot != null && sp.transform.IsChildOf(persistentLoadingRoot.transform))
                    continue;
                return sp.transform.parent.gameObject;
            }
        }

        return null;
    }

    private void BuildProceduralLoadingScreen()
    {
        if (persistentLoadingRoot == null)
        {
            persistentLoadingRoot = new GameObject("PersistentLoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            persistentLoadingRoot.layer = 5; // UI Layer

            Canvas c = persistentLoadingRoot.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 999;

            CanvasScaler scaler = persistentLoadingRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1089f);
            scaler.matchWidthOrHeight = 0.5f;

            DontDestroyOnLoad(persistentLoadingRoot);
        }

        GameObject screenObj = new GameObject("LoadingScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        screenObj.layer = 5;
        screenObj.transform.SetParent(persistentLoadingRoot.transform, false);
        RectTransform screenRt = screenObj.GetComponent<RectTransform>();
        screenRt.anchorMin = Vector2.zero;
        screenRt.anchorMax = Vector2.one;
        screenRt.anchoredPosition = Vector2.zero;
        screenRt.sizeDelta = Vector2.zero;
        screenRt.localScale = Vector3.one;

        Image bgImg = screenObj.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 1f);
        loadingCanvasGroup = screenObj.GetComponent<CanvasGroup>();

        // Barra de Loading (Slider) na mesma posição do Player Canvas Real (720, -500)
        GameObject sliderObj = new GameObject("Slider", typeof(RectTransform));
        sliderObj.layer = 5;
        sliderObj.transform.SetParent(screenObj.transform, false);
        RectTransform sliderRt = sliderObj.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRt.anchoredPosition = new Vector2(720f, -500f);
        sliderRt.sizeDelta = new Vector2(400f, 20f);

        GameObject bgBarObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgBarObj.layer = 5;
        bgBarObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgBarRt = bgBarObj.GetComponent<RectTransform>();
        bgBarRt.anchorMin = new Vector2(0f, 0.25f);
        bgBarRt.anchorMax = new Vector2(1f, 0.75f);
        bgBarRt.offsetMin = Vector2.zero;
        bgBarRt.offsetMax = Vector2.zero;
        Image bgBarImg = bgBarObj.GetComponent<Image>();
        bgBarImg.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        GameObject fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObj.layer = 5;
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.layer = 5;
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.color = Color.white;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;

        // Eptinho (750, -300) com LoadingSpinner
        GameObject eptinObj = new GameObject("Eptin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        eptinObj.layer = 5;
        eptinObj.transform.SetParent(screenObj.transform, false);
        RectTransform eptinRt = eptinObj.GetComponent<RectTransform>();
        eptinRt.anchorMin = new Vector2(0.5f, 0.5f);
        eptinRt.anchorMax = new Vector2(0.5f, 0.5f);
        eptinRt.anchoredPosition = new Vector2(750f, -300f);
        eptinRt.sizeDelta = new Vector2(1000f, 1000f);
        eptinRt.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        Image eptinImg = eptinObj.GetComponent<Image>();
        eptinImg.type = Image.Type.Simple;
        eptinImg.preserveAspect = true;

        LoadingSpinner spinner = eptinObj.AddComponent<LoadingSpinner>();
        spinner.loadingBarToFollow = slider;

        loadingScreenCanvas = screenObj;
        loadingBar = slider;
        screenObj.SetActive(false);
    }

    private void ConfigureLoadingScreenVisuals()
    {
        if (loadingScreenCanvas == null) return;

        // Garante que o RectTransform ocupe 100% do Player Canvas Real
        RectTransform rt = loadingScreenCanvas.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        // Garante fundo 100% opaco para não vazar geração de salas atrás do loading
        Image bg = loadingScreenCanvas.GetComponent<Image>();
        if (bg != null)
        {
            Color c = bg.color;
            c.a = 1f;
            bg.color = c;
        }

        loadingCanvasGroup = loadingScreenCanvas.GetComponent<CanvasGroup>();
        if (loadingCanvasGroup == null)
        {
            loadingCanvasGroup = loadingScreenCanvas.AddComponent<CanvasGroup>();
        }

        if (loadingBar != null)
        {
            loadingBar.interactable = false;
            loadingBar.minValue = 0f;
            loadingBar.maxValue = 1f;
            if (loadingBar.handleRect != null)
            {
                loadingBar.handleRect.gameObject.SetActive(false);
            }
        }

        LoadingSpinner spinner = loadingScreenCanvas.GetComponentInChildren<LoadingSpinner>(true);
        if (spinner != null && loadingBar != null && spinner.loadingBarToFollow == null)
        {
            spinner.loadingBarToFollow = loadingBar;
        }
    }

    private void ShowLoadingScreenImmediate(float alpha = 1f)
    {
        if (loadingScreenCanvas == null) return;

        Canvas parentCanvas = loadingScreenCanvas.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            if (!parentCanvas.gameObject.activeSelf)
                parentCanvas.gameObject.SetActive(true);

            // Garante que o Canvas pai esteja em ScreenSpaceOverlay para cobrir a Game View (câmera do jogador)
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (activeParentCanvas != parentCanvas)
            {
                RestoreParentCanvasSorting();
                activeParentCanvas = parentCanvas;
                savedParentSortingOrder = parentCanvas.sortingOrder;
                hasSavedSortingOrder = true;
                parentCanvas.sortingOrder = 999;
            }
        }

        loadingScreenCanvas.SetActive(true);
        loadingScreenCanvas.transform.SetAsLastSibling();

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = alpha;
            loadingCanvasGroup.blocksRaycasts = true;
        }

        if (loadingBar != null)
        {
            loadingBar.value = currentBarProgress;
        }
    }

    private void HideLoadingScreenImmediate()
    {
        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 1f;
            loadingCanvasGroup.blocksRaycasts = false;
        }

        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(false);
        }

        RestoreParentCanvasSorting();
    }

    private void RestoreParentCanvasSorting()
    {
        if (hasSavedSortingOrder && activeParentCanvas != null)
        {
            activeParentCanvas.sortingOrder = savedParentSortingOrder;
        }
        activeParentCanvas = null;
        hasSavedSortingOrder = false;
    }

    private void EnsurePlayerReference()
    {
        if (currentPlayer != null) return;

        if (PlayerPersistence.instance != null)
        {
            currentPlayer = PlayerPersistence.instance.gameObject;
            return;
        }

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            currentPlayer = found;
        }
    }

    // --- NOVA LÓGICA DE CENA ---
    // Inscreve-se no evento para saber quando uma cena carregou
    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // Chamado automaticamente no EXATO frame em que uma cena termina de carregar (antes de renderizar o 1º frame!)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isAsyncLoading)
        {
            // Conecta imediatamente ao LoadingScreen do Player Canvas Real da nova cena
            // e o ativa com alpha=1 ANTES da câmera renderizar o primeiro frame da nova cena!
            FindLoadingReferences(true);
            ShowLoadingScreenImmediate(1f);
            return;
        }

        // Se chegamos na Base fora de uma corrotina assíncrona (ex: abertura direta da cena Base no Editor)
        FindLoadingReferences(true);
        HideLoadingScreenImmediate();

        if (scene.name == "BaseLab" || scene.name == "Base")
        {
            PositionPlayerAtBase();

            if (currentPlayer != null)
            {
                ScreenFader fader = currentPlayer.GetComponentInChildren<ScreenFader>();
                if (fader != null) StartCoroutine(fader.FadeIn());
            }
        }
    }

    private void PositionPlayerAtBase()
    {
        EnsurePlayerReference();
        if (currentPlayer == null) return;

        // 1. Encontra o Ponto de Spawn da Base
        GameObject baseSpawn = GameObject.Find("Base_SpawnPoint");
        if (baseSpawn != null)
        {
            // FIX: Desativa o NavMeshAgent antes de mover para evitar conflito de posição
            NavMeshAgent agent = currentPlayer.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            currentPlayer.transform.position = baseSpawn.transform.position;
            currentPlayer.transform.rotation = baseSpawn.transform.rotation;

            // Reativa o agente na Base (que tem NavMesh assado)
            if (agent != null) agent.enabled = true;
        }
        else
        {
            Debug.LogWarning("GameManager: 'Base_SpawnPoint' não encontrado na Base!");
        }

        // 2. Manda o jogador tocar a animação de acordar
        PlayerHealth pHealth = currentPlayer.GetComponent<PlayerHealth>();
        if (pHealth != null)
        {
            pHealth.TriggerBaseRespawn();
        }
    }
    // ---------------------------

    public void RegisterPlayer(GameObject player)
    {
        if (currentPlayer == null)
        {
            currentPlayer = player;
            // Carrega a progressão permanente assim que o player é registrado
            SaveManager.instance?.LoadPersistentData(currentPlayer);
        }
        else if (currentPlayer != player) Destroy(player);
    }

    public void LoadGameLevel()
    {
        if (isAsyncLoading) return;

        EnsurePlayerReference();

        // Reseta estado da Run completo (sala 1, round 1, inflação zerada)
        if (RunManager.instance != null)
            RunManager.instance.StartNewRun();

        if (currentPlayer != null)
        {
            InfusionManager im = currentPlayer.GetComponent<InfusionManager>();
            if (im != null) im.ResetRunInflation();
        }

        StartCoroutine(LoadLevelAsync("GameScene", true));
    }

    /// <summary>
    /// Carrega o próximo nível SEM resetar a run.
    /// Chamado pelo NextLevelTrigger quando o jogador usa a Exit Room.
    /// Avança o round no RunManager (Round 1 → 2 → 3 → Boss).
    /// </summary>
    public void LoadNextLevel()
    {
        if (isAsyncLoading) return;

        if (RunManager.instance != null)
            RunManager.instance.AdvanceLevel();

        StartCoroutine(LoadLevelAsync("GameScene", true));
    }

    public void ReturnToBase()
    {
        if (isAsyncLoading) return;

        PlayerHealth.shouldPlayWakeUpAnimation = true;
        Time.timeScale = 1f;
        StartCoroutine(LoadLevelAsync("Base", false));
    }

    public void LoadSceneWithLoading(string sceneName, bool waitForLevelGenerator = false)
    {
        if (isAsyncLoading) return;

        Time.timeScale = 1f;
        StartCoroutine(LoadLevelAsync(sceneName, waitForLevelGenerator));
    }

    /// <summary>
    /// Usado quando a GameScene é iniciada diretamente no Editor da Unity (sem passar pela Base/Menu).
    /// Mostra a tela de loading enquanto o LevelGenerator gera as salas da cena atual.
    /// </summary>
    public void GenerateCurrentSceneWithLoading(LevelGenerator levelGen)
    {
        if (isAsyncLoading || levelGen == null) return;
        StartCoroutine(GenerateCurrentSceneRoutine(levelGen));
    }

    private IEnumerator GenerateCurrentSceneRoutine(LevelGenerator levelGen)
    {
        isAsyncLoading = true;
        currentBarProgress = 0f;
        Time.timeScale = 1f;

        FindLoadingReferences(true);
        EnsurePlayerReference();

        ShowLoadingScreenImmediate(1f);

        if (currentPlayer != null)
        {
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = false;
        }

        isLevelReady = false;
        float elapsedTime = 0f;
        float minDuration = Mathf.Max(2.8f, minimumLoadingTime);

        levelGen.GenerateLevel();

        while (!isLevelReady || elapsedTime < minDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float timeRatio = Mathf.Clamp01(elapsedTime / minDuration);
            float targetBar = isLevelReady ? timeRatio : Mathf.Min(timeRatio, 0.92f);
            currentBarProgress = Mathf.MoveTowards(currentBarProgress, targetBar, Time.unscaledDeltaTime * 1.2f);
            if (loadingBar != null)
            {
                loadingBar.value = currentBarProgress;
            }
            yield return null;
        }

        currentBarProgress = 1f;
        if (loadingBar != null) loadingBar.value = 1f;
        yield return new WaitForSecondsRealtime(0.2f);

        if (currentPlayer != null)
        {
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = true;
        }

        if (loadingScreenCanvas != null && loadingCanvasGroup != null)
        {
            float fadeOutDuration = 0.35f;
            float fadeTimer = 0f;
            while (fadeTimer < fadeOutDuration)
            {
                fadeTimer += Time.unscaledDeltaTime;
                loadingCanvasGroup.alpha = 1f - Mathf.Clamp01(fadeTimer / fadeOutDuration);
                yield return null;
            }
        }

        HideLoadingScreenImmediate();
        isAsyncLoading = false;
    }

    private IEnumerator LoadLevelAsync(string sceneName, bool waitForLevelGenerator)
    {
        isAsyncLoading = true;
        currentBarProgress = 0f;
        Time.timeScale = 1f;

        bool isBaseDestination = (sceneName == "Base" || sceneName == "BaseLab");
        bool pendingWakeUp = PlayerHealth.shouldPlayWakeUpAnimation;
        if (isBaseDestination)
        {
            // Segura a animação de abrir os olhos até a tela de loading terminar,
            // para que ela não toque escondida atrás da tela de loading!
            PlayerHealth.shouldPlayWakeUpAnimation = false;
        }

        FindLoadingReferences(false);
        EnsurePlayerReference();

        if (currentPlayer != null)
        {
            ScreenFader fader = currentPlayer.GetComponentInChildren<ScreenFader>();
            if (fader != null && fader.fadeImage != null)
            {
                fader.fadeImage.color = new Color(0f, 0f, 0f, 0f);
            }

            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = false;
        }

        // Ativa o LoadingScreen dentro do Player Canvas Real da cena atual e faz fade-in suave
        ShowLoadingScreenImmediate(0f);
        if (loadingCanvasGroup != null)
        {
            float fadeInDuration = 0.2f;
            float fadeTimer = 0f;
            while (fadeTimer < fadeInDuration)
            {
                fadeTimer += Time.unscaledDeltaTime;
                loadingCanvasGroup.alpha = Mathf.Clamp01(fadeTimer / fadeInDuration);
                yield return null;
            }
            loadingCanvasGroup.alpha = 1f;
        }

        isLevelReady = !waitForLevelGenerator;
        float elapsedTime = 0f;
        // Força pelo menos ~2.8s a 3.0s de tela de loading para valorizar a animação do Eptinho andando
        // e evitar a sensação de piscada/corte brusco quando o PC carrega a cena muito rápido
        float minDuration = Mathf.Max(2.8f, minimumLoadingTime);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // 1. Aguarda o carregamento da cena em si
        while (!operation.isDone)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsedTime / minDuration);
            currentBarProgress = Mathf.Min(sceneProgress * 0.45f, timeProgress);
            if (loadingBar != null)
            {
                loadingBar.value = currentBarProgress;
            }
            yield return null;
        }

        // Garante que o LoadingScreen da nova cena (Player Canvas Real da nova cena) esteja ativo e visível no Game View
        if (loadingScreenCanvas == null || !loadingScreenCanvas.activeInHierarchy)
        {
            FindLoadingReferences(true);
        }
        ShowLoadingScreenImmediate(1f);
        EnsurePlayerReference();

        // 2. Se for cena procedural (GameScene), inicia o LevelGenerator
        if (waitForLevelGenerator)
        {
            LevelGenerator levelGen = Object.FindFirstObjectByType<LevelGenerator>();
            if (levelGen != null)
            {
                levelGen.GenerateLevel();
            }
            else
            {
                isLevelReady = true;
            }
        }
        else
        {
            isLevelReady = true;
        }

        // 3. Aguarda TANTO a geração do mapa terminar (isLevelReady) QUANTO o tempo mínimo estético (minDuration)
        // mantendo o LoadingScreen 100% visível na Game View!
        while (!isLevelReady || elapsedTime < minDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float timeRatio = Mathf.Clamp01(elapsedTime / minDuration);

            // Se o nível ainda está gerando, limita a barra a 92% até ficar pronto
            float targetBar = isLevelReady ? timeRatio : Mathf.Min(timeRatio, 0.92f);
            currentBarProgress = Mathf.MoveTowards(currentBarProgress, targetBar, Time.unscaledDeltaTime * 1.2f);

            if (loadingScreenCanvas == null || !loadingScreenCanvas.activeInHierarchy)
            {
                FindLoadingReferences(true);
                ShowLoadingScreenImmediate(1f);
            }
            else if (loadingBar != null)
            {
                loadingBar.value = currentBarProgress;
            }

            yield return null;
        }

        currentBarProgress = 1f;
        if (loadingBar != null) loadingBar.value = 1f;

        // Pequena pausa em 100% para leitura visual completa da barra cheia
        yield return new WaitForSecondsRealtime(0.2f);

        // 4. Se o destino for a Base, posiciona o player e dispara o acordar (piscar de olhos) AGORA,
        // exatamente quando o loading terminou!
        if (isBaseDestination)
        {
            PlayerHealth.shouldPlayWakeUpAnimation = pendingWakeUp;
            PositionPlayerAtBase();
        }
        else if (currentPlayer != null)
        {
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = true;
        }

        // Fade-out suave da tela de loading revelando a cena já pronta
        if (loadingScreenCanvas != null && loadingCanvasGroup != null)
        {
            float fadeOutDuration = 0.35f;
            float fadeTimer = 0f;
            while (fadeTimer < fadeOutDuration)
            {
                fadeTimer += Time.unscaledDeltaTime;
                loadingCanvasGroup.alpha = 1f - Mathf.Clamp01(fadeTimer / fadeOutDuration);
                yield return null;
            }
        }

        HideLoadingScreenImmediate();
        isAsyncLoading = false;
    }

    public void OnLevelReady(Transform spawnPoint)
    {
        EnsurePlayerReference();

        if (currentPlayer != null && spawnPoint != null)
        {
            // FIX: Desativa o NavMeshAgent antes de teleportar para evitar
            // conflito com a mudança brusca de transform.position.
            NavMeshAgent agent = currentPlayer.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            currentPlayer.transform.position = spawnPoint.position;
            currentPlayer.transform.rotation = spawnPoint.rotation;

            // Reativa o agente — o NavMesh já foi assado em runtime pelo LevelGenerator
            if (agent != null) agent.enabled = true;
            
            // Mantém o movimento travado enquanto a tela de loading ainda estiver ativa
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = !isAsyncLoading;
            
            // Conecta Mercador e UI da fase
            PlayerHealth pHealth = currentPlayer.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.FindUIReferences();
            
            DashM pDash = currentPlayer.GetComponent<DashM>();
            if (pDash != null) pDash.FindUIReferences();

            MerchantUIController merchantUI = Object.FindFirstObjectByType<MerchantUIController>(FindObjectsInactive.Include);
            if (merchantUI != null) merchantUI.ConnectPlayer(pHealth);
        }

        // Notify GameManager that the level is ready and player has been teleported!
        isLevelReady = true;
    }
}
