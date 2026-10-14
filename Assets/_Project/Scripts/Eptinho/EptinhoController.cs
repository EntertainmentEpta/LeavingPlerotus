using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EptinhoController : MonoBehaviour
{
    public GameObject MenuCanvas;
    public GameObject HUDCanvas;

    private Interactable objetoAtual;

    void Awake()
    {
        // Garante que EnemyDataAutoLoader existe
        if (EnemyDataAutoLoader.instancia == null)
        {
            GameObject loaderGO = new GameObject("EnemyDataAutoLoader");
            loaderGO.AddComponent<EnemyDataAutoLoader>();
            DontDestroyOnLoad(loaderGO);
        }
    }

    void Start()
    {
        if (MenuCanvas != null) MenuCanvas.SetActive(false);
        
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Base" || sceneName == "BaseLab - Teste Itens" || sceneName.Contains("Eptinho"))
        {
            SpawnEptinhoPhysicalModel();
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Base" || scene.name == "BaseLab - Teste Itens" || scene.name.Contains("Eptinho"))
        {
            SpawnEptinhoPhysicalModel();
        }
    }

    private void SpawnEptinhoPhysicalModel()
    {
        // 1. Procura se já existe um Eptinho colocado estaticamente na cena
        GameObject eptinho = GameObject.Find("EptOracle");
        if (eptinho == null)
        {
            eptinho = GameObject.Find("Eptin");
        }

        // 2. Se não existir, spawna como fallback
        if (eptinho == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Eptin");
            if (prefab == null)
            {
                Debug.LogWarning("[EPTINHO] Prefab 'Eptin' nao encontrado na pasta Resources.");
                return;
            }

            Vector3 spawnPos;
            Quaternion spawnRot = Quaternion.Euler(0f, 135f, 0f);

            GameObject table = GameObject.Find("crafting table");
            if (table != null)
            {
                spawnPos = table.transform.position + new Vector3(-1.8f, 1.3f, 1.2f);
            }
            else
            {
                GameObject lab = GameObject.Find("Sector_Laboratory");
                spawnPos = lab != null
                    ? lab.transform.position + new Vector3(0f, 1.5f, 2f)
                    : new Vector3(0f, 1.5f, 0f);
            }

            eptinho = Instantiate(prefab, spawnPos, spawnRot);
            eptinho.name = "EptOracle";
            Debug.Log($"[EPTINHO] Eptinho fisico nao encontrado na cena. Spawnado fallback em {spawnPos}.");
        }
        else
        {
            eptinho.name = "EptOracle";
            Debug.Log("[EPTINHO] Usando Eptinho estatico ja existente na cena.");
        }

        // 3. Garante que o root tem Rigidbody kinematic (necessário para OnTriggerEnter funcionar nos filhos)
        Rigidbody rb = eptinho.GetComponent<Rigidbody>();
        if (rb == null) rb = eptinho.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        // 4. Adiciona EptinhoOracleInteract no filho 'Trigger Menu'
        bool foundTriggerMenu = false;
        foreach (Transform child in eptinho.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Trigger Menu")
            {
                BoxCollider bc = child.GetComponent<BoxCollider>();
                if (bc == null) bc = child.gameObject.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                // Garante tamanho razoável para detecção
                if (bc.size.magnitude < 0.5f) bc.size = new Vector3(2f, 2f, 2f);

                if (child.GetComponent<EptinhoOracleInteract>() == null)
                    child.gameObject.AddComponent<EptinhoOracleInteract>();

                foundTriggerMenu = true;
                Debug.Log("[EPTINHO] EptinhoOracleInteract adicionado ao filho 'Trigger Menu'.");
                break;
            }
        }

        // Fallback: sem filho 'Trigger Menu', adiciona SphereCollider no root
        if (!foundTriggerMenu)
        {
            SphereCollider sc = eptinho.GetComponent<SphereCollider>();
            if (sc == null) sc = eptinho.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.5f;

            if (eptinho.GetComponent<EptinhoOracleInteract>() == null)
                eptinho.AddComponent<EptinhoOracleInteract>();

            Debug.Log("[EPTINHO] 'Trigger Menu' nao encontrado - EptinhoOracleInteract adicionado no root com SphereCollider.");
        }

        // 5. Garante que animações do Eptinho continuem ativas mesmo com Time.timeScale = 0 (diálogos e pauses)
        foreach (Animator anim in eptinho.GetComponentsInChildren<Animator>(true))
        {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.C)) return;
        if (DialogManager.IsOpen || GamePause.IsPaused) return;
        if (MenuCanvas == null || HUDCanvas == null) return;

        bool open = !MenuCanvas.activeSelf;
        MenuCanvas.SetActive(open);
        HUDCanvas.SetActive(!open);
    }

    public void AbrirMenuDoObjeto(Interactable obj)
    {
        if (obj == null) return;
        objetoAtual = obj;

        if (EptinhoMenuController.instancia != null)
        {
            EptinhoMenuController.instancia.AbrirMenu();
        }
        else
        {
            if (MenuCanvas != null) MenuCanvas.SetActive(true);
            if (HUDCanvas != null) HUDCanvas.SetActive(false);
        }

        Debug.Log("Menu aberto para: " + objetoAtual.objetoNome);

        AtualizarUI(objetoAtual);
    }

    public void FecharMenu()
    {
        if (EptinhoMenuController.instancia != null)
        {
            EptinhoMenuController.instancia.FecharMenu();
        }
        else
        {
            if (MenuCanvas != null) MenuCanvas.SetActive(false);
            if (HUDCanvas != null) HUDCanvas.SetActive(true);
        }
        objetoAtual = null;
    }

    void AtualizarUI(Interactable obj)
    {
        //Inserir elementos do objeto no menu

        Debug.Log("Atualizando UI com dados do objeto: " + obj.objetoNome);
    }
}
