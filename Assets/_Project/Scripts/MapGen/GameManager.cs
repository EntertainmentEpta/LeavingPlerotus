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
    public float minimumLoadingTime = 2.0f; // Tempo mínimo de carregamento em segundos
    
    [HideInInspector]
    public GameObject currentPlayer;

    private bool isLevelReady = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        FindLoadingReferences();

        if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(false);
    }

    public void FindLoadingReferences()
    {
        // Try to automatically find the loading canvas if it's null
        if (loadingScreenCanvas == null)
        {
            GameObject go = GameObject.Find("LoadingScreenCanvas");
            if (go == null) go = GameObject.Find("LoadingScreen");
            if (go == null) go = GameObject.Find("Canvas_Loading");
            
            if (go != null)
            {
                loadingScreenCanvas = go;
            }
        }

        // Keep the loading canvas alive across scene changes
        if (loadingScreenCanvas != null)
        {
            DontDestroyOnLoad(loadingScreenCanvas);
            
            // Try to find the slider if not assigned
            if (loadingBar == null)
            {
                loadingBar = loadingScreenCanvas.GetComponentInChildren<Slider>(true);
            }
        }
    }

    // --- NOVA LÓGICA DE CENA ---
    // Inscreve-se no evento para saber quando uma cena carregou
    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // Chamado automaticamente toda vez que uma cena termina de carregar
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Se chegamos na Base...
        if (scene.name == "BaseLab" || scene.name == "Base")
        {
            if (currentPlayer != null)
            {
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

            // Esconde a tela de loading (caso esteja ativa)
            if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(false);
            
            // Fade In da tela (se o jogador tiver ScreenFader)
            if (currentPlayer != null)
            {
                ScreenFader fader = currentPlayer.GetComponentInChildren<ScreenFader>();
                if (fader != null) StartCoroutine(fader.FadeIn());
            }
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
        // Reseta estado da Run completo (sala 1, round 1, inflação zerrada)
        if (RunManager.instance != null)
            RunManager.instance.StartNewRun();

        if (currentPlayer != null)
        {
            InfusionManager im = currentPlayer.GetComponent<InfusionManager>();
            if (im != null) im.ResetRunInflation();
        }

        StartCoroutine(LoadLevelAsync("GameScene"));
    }

    /// <summary>
    /// Carrega o próximo nível SEM resetar a run.
    /// Chamado pelo NextLevelTrigger quando o jogador usa a Exit Room.
    /// Avança o round no RunManager (Round 1 → 2 → 3 → Boss).
    /// </summary>
    public void LoadNextLevel()
    {
        if (RunManager.instance != null)
            RunManager.instance.AdvanceLevel();

        StartCoroutine(LoadLevelAsync("GameScene"));
    }

    public void ReturnToBase()
    {
        PlayerHealth.shouldPlayWakeUpAnimation = true;
        FindLoadingReferences();
        if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(true);
        SceneManager.LoadScene("Base");
    }

    private IEnumerator LoadLevelAsync(string sceneName)
    {
        FindLoadingReferences();
        if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(true);

        if (currentPlayer != null)
        {
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = false;
        }

        // Reset the ready flag before loading
        isLevelReady = false;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        
        // 1. Wait until scene loads (takes up to 50% of the bar)
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            if (loadingBar != null) loadingBar.value = progress * 0.5f;
            yield return null;
        }

        // 2. Generate level immediately so the player snaps to the correct floor/spawn point
        LevelGenerator levelGen = Object.FindFirstObjectByType<LevelGenerator>();
        if (levelGen != null)
        {
            levelGen.GenerateLevel();
            
            // 3. Wait until the LevelGenerator calls OnLevelReady and sets isLevelReady = true
            float timer = 0f;
            while (!isLevelReady)
            {
                timer += Time.deltaTime;
                if (loadingBar != null)
                {
                    // Smoothly animate the loading bar from 50% to 95% while waiting for generation
                    loadingBar.value = Mathf.Lerp(0.5f, 0.95f, timer / 2f);
                }
                yield return null;
            }
        }
        else
        {
            isLevelReady = true; // Fallback if no generator
        }

        if (loadingBar != null) loadingBar.value = 1f;

        // Brief delay for visual completion
        yield return new WaitForSeconds(0.2f);

        // 4. Hide loading screen and enable player movement
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(false);
        }

        if (currentPlayer != null)
        {
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = true;

            // Smoothly fade in the level
            ScreenFader fader = currentPlayer.GetComponentInChildren<ScreenFader>();
            if (fader != null) StartCoroutine(fader.FadeIn());
        }
    }

    public void OnLevelReady(Transform spawnPoint)
    {
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
            
            // Keep player movement disabled during the loading wait
            PlayerM playerMovement = currentPlayer.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = false;
            
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