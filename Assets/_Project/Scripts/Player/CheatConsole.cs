using UnityEngine;
using TMPro; // Usado para acessar campos de texto moderno da interface

public class CheatConsole : MonoBehaviour
{
    [Header("UI do Console")]
    [Tooltip("Arraste o objeto CheatConsole_Input aqui do seu Canvas")]
    public TMP_InputField consoleInput; 
    
    [Header("Referências do Player")]
    [Tooltip("O Animator do personagem que onde vai tocar o breakdance")]
    public Animator playerAnimator; 
    
    // Propriedade estática global para sabermos se o console tá aberto ou fechado
    public static bool IsOpen { get; private set; } = false;
    private bool isConsoleActive = false;

    // Scripts para congelar o jogador
    private PlayerM movementScript;
    private PrimaryAttackKnife attackScript;
    private DashM dashScript;
    private PlayerInteraction interactionScript;
    private Player_WeaponManager weaponManagerScript;

    void Start()
    {
        movementScript = GetComponent<PlayerM>() ?? GetComponentInChildren<PlayerM>();
        attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>();
        dashScript = GetComponent<DashM>() ?? GetComponentInChildren<DashM>();
        interactionScript = GetComponent<PlayerInteraction>() ?? GetComponentInChildren<PlayerInteraction>();
        weaponManagerScript = GetComponent<Player_WeaponManager>() ?? GetComponentInChildren<Player_WeaponManager>();

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        if (consoleInput == null)
        {
            FindConsoleInput();
        }

        // Garante que comece invisível
        if (consoleInput != null)
        {
            consoleInput.gameObject.SetActive(false);
        }
    }

    private void FindConsoleInput()
    {
        TMP_InputField[] inputs = Resources.FindObjectsOfTypeAll<TMP_InputField>();
        foreach (var input in inputs)
        {
            if (input.name == "CheatConsole_Input" && input.gameObject.scene.isLoaded)
            {
                consoleInput = input;
                break;
            }
        }

        if (consoleInput == null)
        {
            Debug.LogWarning("🔍 CHEAT: Não foi possível encontrar 'CheatConsole_Input' na cena ativa atual.");
        }
    }

    void Update()
    {
        // Se o console está aberto e o jogador aperta ESC ou Ponto e Vírgula ';', fecha o console
        if (isConsoleActive && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Semicolon)))
        {
            Debug.Log("🔍 CHEAT: Tecla de fechamento pressionada!");
            ToggleConsole();
            return;
        }

        // Ligar / Alternar o console com Barra '/', Divisão ou Ponto e Vírgula ';'
        if (Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Semicolon))
        {
            Debug.Log("🔍 CHEAT: Tecla de atalho do console pressionada!");
            if (consoleInput == null)
            {
                FindConsoleInput();
            }
            ToggleConsole();
            return;
        }

        // Se o console está aberto e ele dá um Enter
        if (isConsoleActive && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            Debug.Log("🔍 CHEAT: Tecla 'Enter' pressionada com o painel aberto!");
            if (consoleInput != null)
            {
                Debug.Log($"🔍 CHEAT: A caixa de texto no momento do clique estava com o valor: '{consoleInput.text}'");
                CheckConsoleCommand(consoleInput.text);
            }
            else
            {
                Debug.LogError("🚨 CHEAT ERROR: O componente 'consoleInput' está NULL! Você não arrastou ele no Inspector.");
            }
        }
    }

    void ToggleConsole()
    {
        if (consoleInput == null) return;

        isConsoleActive = !isConsoleActive; // Inverte o estado
        IsOpen = isConsoleActive;
        
        // Ativa ou desativa a telinha do console
        consoleInput.gameObject.SetActive(isConsoleActive);

        // Se a Bolsa Sintética / Inventário estiver aberto por engano, fecha-o
        if (isConsoleActive && SyntheticBagUI.Instance != null)
        {
            SyntheticBagUI.Instance.CloseBag();
        }

        // Congela/Descongela o movimento, dash, interação e ataque
        if (movementScript != null) movementScript.enabled = !isConsoleActive;
        if (attackScript != null) attackScript.enabled = !isConsoleActive;
        if (dashScript != null) dashScript.enabled = !isConsoleActive;
        if (interactionScript != null) interactionScript.enabled = !isConsoleActive;
        if (weaponManagerScript != null) weaponManagerScript.enabled = !isConsoleActive;

        // Congela fisicamente (zera velocidade) para ele não deslizar se abriu o console correndo
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && isConsoleActive) rb.linearVelocity = Vector3.zero;
        
        if (isConsoleActive)
        {
            consoleInput.text = ""; 
            consoleInput.ActivateInputField();
            Debug.Log("🔍 CHEAT: Console ABERTO e focado na tela.");
        }
        else
        {
            Debug.Log("🔍 CHEAT: Console FECHADO.");
        }
    }

    // Essa função DEVE ser conectada no "On End Edit" do seu componente TMP_InputField no Editor do Unity
    public void CheckConsoleCommand(string typedText)
    {
        // ---------------------------------------------------------
        // MAGIA NEGRA PARA IGNORAR O BUG DO INSPECTOR DO UNITY:
        // Se você sem querer linkou o botão no "Static Parameters", o Unity passa texto vazio.
        // Então nós ignoramos o que o Unity passou e arrancamos o texto à força de dentro da caixa!
        string textoReal = "";
        if (consoleInput != null) textoReal = consoleInput.text;
        // ---------------------------------------------------------

        // O jogador pode bater o Enter vazio ou tentar fechar
        if (string.IsNullOrWhiteSpace(textoReal)) 
        {
            Debug.LogWarning("🔍 CHEAT: A caixa de texto foi lida como completamente vazia.");
            ToggleConsole();
            return;
        }

        // Limpa possíveis carácteres sujos como a própria barra '/' que pode vazar pro texto
        string command = textoReal.Replace("/", "").Trim();
        Debug.Log($"🔍 CHEAT: O script limpou o texto recebido e tentará bater esta palavra com a senha: '{command}'");

        // Faz a checagem ignorando completamente se é maiúsculo ou minúsculo
        if (string.Equals(command, "EPTA", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Comando EPTA Recebido! Iniciando Breakdance...");
            
            if (playerAnimator != null)
            {
                // Dispara a trigger lá pro seu Animator
                playerAnimator.SetTrigger("Breakdance");
                Debug.Log("💻 CHEAT: Gatilho 'Breakdance' enviado com sucesso para o Animator!");
            }
            else
            {
                Debug.LogError("🚨 CHEAT ERROR: O comando rodou, mas você não arrastou o Animator para o script CheatConsole!");
            }
        }
        else if (string.Equals(command, "killall", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Comando KILLALL Recebido! Eliminando todos os inimigos...");

            // Busca e mata todos os DummyHealth ativos
            DummyHealth[] dummies = Object.FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            int count = 0;
            foreach (DummyHealth dummy in dummies)
            {
                if (dummy != null)
                {
                    // Evita matar o jogador por engano se o script estiver no jogador (não está, mas por segurança)
                    if (dummy.CompareTag("Player")) continue;
                    
                    dummy.isInvulnerable = false;
                    dummy.TakeDamage(999999);
                    count++;
                }
            }

            // Busca e mata todos os ShardSwarmHealth ativos
            ShardSwarmHealth[] swarms = Object.FindObjectsByType<ShardSwarmHealth>(FindObjectsSortMode.None);
            foreach (ShardSwarmHealth swarm in swarms)
            {
                if (swarm != null)
                {
                    swarm.isInvulnerable = false;
                    swarm.SetHealth(0);
                    count++;
                }
            }

            Debug.Log($"💻 CHEAT: {count} inimigos eliminados com sucesso.");
        }
        else if (string.Equals(command, "endless", System.StringComparison.OrdinalIgnoreCase))
        {
            if (RunManager.instance != null)
            {
                RunManager.instance.isEndlessMode = !RunManager.instance.isEndlessMode;
                RunManager.instance.UpdateEndlessUI();
                Debug.Log($"💻 CHEAT: Modo Endless alterado para: {RunManager.instance.isEndlessMode}");

                if (EptinhoPopupController.instancia != null)
                {
                    string statusText = RunManager.instance.isEndlessMode ? "Modo Endless ATIVADO!" : "Modo Endless DESATIVADO!";
                    EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
                }
            }
        }
        else if (command.StartsWith("forca", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("str", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("strength", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("dano", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("damage", System.StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = command.Split(' ');
            float val = 10f;
            if (parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedVal))
            {
                val = parsedVal;
            }

            PlayerAttributesOffensive offStats = PlayerAttributesOffensive.Instance ?? Object.FindFirstObjectByType<PlayerAttributesOffensive>();
            if (offStats != null)
            {
                offStats.baseDamageMultiplier = val;
                string statusText = $"FORÇA BASE ALTERADA!\nbaseDamageMultiplier = {val}x";
                Debug.Log($"💻 CHEAT: {statusText}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
                }
            }
            else
            {
                Debug.LogWarning("💻 CHEAT: PlayerAttributesOffensive não encontrado na cena!");
            }
        }
        else if (string.Equals(command, "allmobs", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "spawnall", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "mobs", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "mobsall", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "showcase", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "bichos", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "todosmobs", System.StringComparison.OrdinalIgnoreCase))
        {
            RoomController.forceAllMobsMode = true;
            int spawned = RoomController.SpawnAllMobsNow();

            string statusText = $"MODO TODOS OS MOBS ATIVADO!\n{spawned} bichos spawnados na sala!";
            Debug.Log($"💻 CHEAT: {statusText}");

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
            }
        }
        else if (string.Equals(command, "togglemobs", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "normalmobs", System.StringComparison.OrdinalIgnoreCase))
        {
            RoomController.forceAllMobsMode = !RoomController.forceAllMobsMode;
            string statusText = RoomController.forceAllMobsMode ? "Modo Todos os Mobs: ATIVADO!" : "Modo Todos os Mobs: DESATIVADO (Normal)";
            Debug.Log($"💻 CHEAT: {statusText}");

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
            }
        }
        else if (string.Equals(command, "recursos", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "allitems", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "giveall", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "cheats", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Recursos Infinitos! Adicionando +999 de todos os materiais...");
            if (DevCheatConsole.Instance != null)
            {
                DevCheatConsole.Instance.GiveAllResources(999);
            }
            else
            {
                if (SaveManager.instance != null && ItemDatabase.Instance != null)
                {
                    foreach (var item in ItemDatabase.Instance.allItems)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.itemId))
                            SaveManager.instance.AddResourceToBase(item.itemId, 999);
                    }
                    SaveManager.instance.SavePersistentData();
                }
            }

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("+999 Recursos Adicionados!");
            }
        }
        else if (string.Equals(command, "unlockall", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "equip", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Destravando todas as melhorias!");
            if (DevCheatConsole.Instance != null)
            {
                DevCheatConsole.Instance.UnlockAllEquipment();
            }
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("Todas as Melhorias Destravadas!");
            }
        }
        else if (string.Equals(command, "orbs", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "money", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: +99.999 Orbs!");
            if (DevCheatConsole.Instance != null)
            {
                DevCheatConsole.Instance.GiveMaxOrbs(99999);
            }
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("+99.999 Orbs Adicionados!");
            }
        }
        else if (string.Equals(command, "bosssolo", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "soloboss", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "bosstest", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "solo", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Carregando Sala do Boss em MODO SOLO (Sem Mobs)!");
            BossPhase1_MobSpawner.SetBossSoloMode(true);

            if (RunManager.instance != null)
            {
                RunManager.instance.currentLevel = RunManager.instance.totalLevels;
                RunManager.instance.forceBossNextRun = true;
            }

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("MODO SOLO DO BOSS ATIVADO!\nEntrando sem inimigos extras...");
            }

            if (Application.CanStreamedLevelBeLoaded("BossRoom_Cromatico"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("BossRoom_Cromatico");
            }
            else if (Application.CanStreamedLevelBeLoaded("Geobionte"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Geobionte");
            }
        }
        else if (string.Equals(command, "boss", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "loadboss", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "gotoboss", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Forçando Boss Round e Carregando Sala do Boss Padrão!");
            BossPhase1_MobSpawner.SetBossSoloMode(false);

            if (RunManager.instance != null)
            {
                // Seta nivel atual pro Boss Round
                RunManager.instance.currentLevel = RunManager.instance.totalLevels;
                RunManager.instance.forceBossNextRun = true;

                Debug.Log($"💻 CHEAT: currentLevel setado para {RunManager.instance.currentLevel}/{RunManager.instance.totalLevels}.");
            }

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("Boss Round ATIVADO!\nEntrando no combate...");
            }

            // Tenta carregar diretamente a cena do Boss
            if (Application.CanStreamedLevelBeLoaded("BossRoom_Cromatico"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("BossRoom_Cromatico");
            }
            else if (Application.CanStreamedLevelBeLoaded("Geobionte"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Geobionte");
            }
        }
        else
        {
            Debug.LogWarning($"💻 CHEAT RECUSADO: A palavra '{command}' não é reconhecida. Tente: EPTA, killall, endless, recursos, unlockall, orbs, boss ou bosssolo.");
        }

        // Depois de tentar o cheat, fecha o console pro jogo voltar ao normal
        ToggleConsole(); 
    }
}
