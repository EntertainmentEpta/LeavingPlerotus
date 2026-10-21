using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CheatConsole : MonoBehaviour
{
    public static CheatConsole Instance { get; private set; }

    [Header("UI do Console")]
    [Tooltip("Arraste o objeto CheatConsole_Input aqui do seu Canvas")]
    public TMP_InputField consoleInput; 
    
    [Header("Referências do Player")]
    [Tooltip("O Animator do personagem onde vai tocar o breakdance")]
    public Animator playerAnimator; 
    
    // Propriedade estática global para sabermos se o console tá aberto ou fechado
    private static bool _isOpen = false;
    public static bool IsOpen
    {
        get => _isOpen;
        private set => _isOpen = value;
    }

    private bool isConsoleActive = false;

    // ── Painel Secreto de Comandos (Dev Cheat Sheet) ─────────────────────────
    private GameObject secretCanvasGO;
    private GameObject secretPanelGO;
    private bool isSecretPanelOpen = false;

    private struct CheatEntry
    {
        public string category;
        public string command;
        public string aliases;
        public string description;
        public string executePayload;
    }

    private static readonly List<CheatEntry> SecretCheats = new List<CheatEntry>
    {
        // 💎 Economia & Materiais
        new CheatEntry { category = "💎 ECONOMIA & MATERIAIS", command = "recursos", aliases = "giveall, allitems, cheats", description = "Adiciona +999 de todos os itens, minérios e materiais à base.", executePayload = "recursos" },
        new CheatEntry { category = "💎 ECONOMIA & MATERIAIS", command = "orbs", aliases = "money", description = "Adiciona +99.999 Orbs à carteira do jogador para compras e upgrades.", executePayload = "orbs" },
        new CheatEntry { category = "💎 ECONOMIA & MATERIAIS", command = "unlockall", aliases = "equip", description = "Destrava todas as melhorias da nave e todas as 10 sinergias.", executePayload = "unlockall" },
        new CheatEntry { category = "💎 ECONOMIA & MATERIAIS", command = "door", aliases = "porta, lab, opendoor, abrirporta", description = "Conserta o mecanismo e abre a porta do laboratório da nave.", executePayload = "door" },

        // ✨ Sinergias & Lore
        new CheatEntry { category = "✨ SINERGIAS & LORE", command = "sinergias", aliases = "synergies, synergy, mapa, unlocksynergies", description = "Cataloga e revela todas as 10 sinergias no Mapa Cósmico [M].", executePayload = "sinergias" },
        new CheatEntry { category = "✨ SINERGIAS & LORE", command = "unlocklore", aliases = "escrituras, scriptures, caderno, livro, journal", description = "Destrava todas as 18 escrituras e blueprints e abre o Caderno.", executePayload = "unlocklore" },
        new CheatEntry { category = "✨ SINERGIAS & LORE", command = "resetcaderno", aliases = "resetlivro, resetbook", description = "Reseta o caderno para testar a coleta no chão da Safe Room.", executePayload = "resetcaderno" },
        new CheatEntry { category = "✨ SINERGIAS & LORE", command = "limparescrituras", aliases = "clearscriptures, clearlore", description = "Reseta todas as escrituras coletadas para páginas em branco.", executePayload = "limparescrituras" },

        // ⚔️ Combate, Dano & Atributos
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "forca 10", aliases = "dano [X], str [X], strength [X]", description = "Multiplica o dano base por 10x (aceita qualquer valor: ex: forca 5, forca 50).", executePayload = "forca 10" },
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "heal", aliases = "vida, curar, god", description = "Restaura instantaneamente toda a vida e armadura do jogador ao máximo.", executePayload = "heal" },
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "poison", aliases = "veneno", description = "Ativa o efeito Tier 4 Veneno (dano contínuo verde nos inimigos).", executePayload = "poison" },
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "thorns", aliases = "espinhos, espinho", description = "Concede +20 de dano refletido por espinhos contra qualquer atacante.", executePayload = "thorns" },
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "pact", aliases = "pacto, mercador", description = "Alterna o visual das garras corrompidas do Pacto do Mercador.", executePayload = "pact" },
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "aim", aliases = "mira", description = "Alterna o modo de mira entre Mouse (padrão) e WASD.", executePayload = "aim" },

        // 👾 Inimigos, Spawns & Boss
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "killall", aliases = "-", description = "Elimina todos os inimigos da sala atual instantaneamente.", executePayload = "killall" },
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "boss", aliases = "loadboss, gotoboss", description = "Força o round do boss e teleporta direto para a arena do Boss.", executePayload = "boss" },
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "bosssolo", aliases = "soloboss, bosstest, solo", description = "Teleporta para o Boss em modo Solo (sem minions nem spawners).", executePayload = "bosssolo" },
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "allmobs", aliases = "spawnall, mobs, bichos, showcase", description = "Spawna 1 de cada tipo de mob na sala atual imediatamente.", executePayload = "allmobs" },
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "togglemobs", aliases = "normalmobs", description = "Alterna entre spawn normal e modo Todos os Mobs.", executePayload = "togglemobs" },
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "rebirth", aliases = "eliterebirth, rebirthelite, renascer", description = "Alterna o Modo Renascimento Elite 100% (todo mob morto renasce Elite com drops T2-T4).", executePayload = "rebirth" },
        new CheatEntry { category = "⚔️ COMBATE, DANO & ATRIBUTOS", command = "sinfonia", aliases = "chaos, pactchaos, pactcaos", description = "Alterna o Pacto A Sinfonia do Caos (+20% drops, +Elites, +35% renascer elite).", executePayload = "sinfonia" },
        new CheatEntry { category = "👾 INIMIGOS, SPAWNS & BOSS", command = "endless", aliases = "-", description = "Alterna o Modo Endless (fases infinitas sem encerrar no chefe).", executePayload = "endless" },

        // 🧪 Teste Rápido de Fusões & Sinergias (Sem Reiniciar)
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "clearinv", aliases = "limparinv, emptyinv", description = "Esvazia todos os 10 slots da mochila instantaneamente para novos testes.", executePayload = "clearinv" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 1", aliases = "combo 1", description = "Chuva de Lâminas (Goblin + SharpBlur).", executePayload = "fusao 1" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 2", aliases = "combo 2", description = "Parede Intransponível (+30 Armadura / Golem + Sentinela).", executePayload = "fusao 2" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 3", aliases = "combo 3", description = "Corte Congelante (Cristalus + SharpBlur).", executePayload = "fusao 3" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 4", aliases = "combo 4", description = "Assassino Fantasma (Aranha + Pedra Mágica).", executePayload = "fusao 4" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 5", aliases = "combo 5", description = "Domínio de Espaço (Totem + Watcher).", executePayload = "fusao 5" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 6", aliases = "combo 6", description = "Tempestade Focada (Tuner + Shard Swarm).", executePayload = "fusao 6" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 7", aliases = "combo 7", description = "Aura Espinhosa (Peixe + Cristalus).", executePayload = "fusao 7" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 8", aliases = "combo 8", description = "Fuga Explosiva (Totem + Goblin).", executePayload = "fusao 8" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 9", aliases = "combo 9", description = "Berserker Imortal (4 Itens: Golem T4 + Bismutado + Istar + Aranha).", executePayload = "fusao 9" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 10", aliases = "combo 10", description = "Veneno Corrosivo (Peixe + Aranha).", executePayload = "fusao 10" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 11", aliases = "combo 11", description = "Baluarte de Obsidiana (+40 Armadura / Golem T2 + Pedra Mágica).", executePayload = "fusao 11" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 18", aliases = "combo 18", description = "Fortaleza de Titânio (+30 Armadura / Sentinela + Totem).", executePayload = "fusao 18" },
        new CheatEntry { category = "🧪 TESTE DE FUSÕES (SEM REINICIAR)", command = "fusao 21", aliases = "combo 21", description = "A Ascensão: Modo Deus (+100 Armadura / 4x Lendários T4).", executePayload = "fusao 21" },

        // ⚡ Miscelânea & Sistema
        new CheatEntry { category = "⚡ MISCELÂNEA & SISTEMA", command = "help", aliases = "ajuda, comandos, commands, secret, ?", description = "Abre esta lista secreta de comandos do desenvolvedor [F1].", executePayload = "help" },
        new CheatEntry { category = "⚡ MISCELÂNEA & SISTEMA", command = "reset", aliases = "novojogo, newgame, resetall, resetsave", description = "Reinicia a jornada do zero, limpando o save persistente.", executePayload = "reset" },
        new CheatEntry { category = "⚡ MISCELÂNEA & SISTEMA", command = "EPTA", aliases = "-", description = "Toca a lendária animação de Breakdance do personagem!", executePayload = "EPTA" },
    };

    // Scripts para congelar o jogador
    private PlayerM movementScript;
    private PrimaryAttackKnife attackScript;
    private DashM dashScript;
    private PlayerInteraction interactionScript;
    private Player_WeaponManager weaponManagerScript;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (secretCanvasGO != null)
        {
            Destroy(secretCanvasGO);
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
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Se a lista secreta está aberta e o usuário aperta ESC, fecha ela
        if (isSecretPanelOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSecretCheatPanel();
            return;
        }

        // Atalho secreto de Desenvolvedor: Ctrl + Shift + F1 abre/fecha a lista secreta
        if (ctrl && shift && Input.GetKeyDown(KeyCode.F1))
        {
            ToggleSecretCheatPanel();
            return;
        }

        // Se o console está aberto e o jogador aperta ESC, fecha o console
        if (isConsoleActive && Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("🔍 CHEAT: Tecla ESC pressionada! Fechando console.");
            ToggleConsole();
            return;
        }

        // Abrir / Alternar o console com atalho exclusivo do desenvolvedor:
        // Ctrl + Shift + C  OU  Ctrl + /  OU  Ctrl + ;
        bool openConsoleTriggered = (ctrl && shift && Input.GetKeyDown(KeyCode.C)) ||
                                    (ctrl && (Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.KeypadDivide) || Input.GetKeyDown(KeyCode.Semicolon)));

        if (openConsoleTriggered)
        {
            Debug.Log("🔍 CHEAT: Atalho secreto do console acionado pelo desenvolvedor!");
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

    private void UpdateOpenState()
    {
        IsOpen = isConsoleActive || isSecretPanelOpen;
    }

    private void FreezePlayer(bool freeze)
    {
        if (movementScript != null) movementScript.enabled = !freeze;
        if (attackScript != null) attackScript.enabled = !freeze;
        if (dashScript != null) dashScript.enabled = !freeze;
        if (interactionScript != null) interactionScript.enabled = !freeze;
        if (weaponManagerScript != null) weaponManagerScript.enabled = !freeze;

        // Congela fisicamente (zera velocidade) para ele não deslizar
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && freeze) rb.linearVelocity = Vector3.zero;
    }

    void ToggleConsole()
    {
        if (consoleInput == null) return;

        isConsoleActive = !isConsoleActive; // Inverte o estado
        UpdateOpenState();
        
        // Ativa ou desativa a telinha do console
        consoleInput.gameObject.SetActive(isConsoleActive);

        // Se a Bolsa Sintética / Inventário estiver aberto por engano, fecha-o
        if (isConsoleActive && SyntheticBagUI.Instance != null)
        {
            SyntheticBagUI.Instance.CloseBag();
        }

        FreezePlayer(isConsoleActive || isSecretPanelOpen);
        
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

    // ─── GESTÃO DO PAINEL SECRETO DE COMANDOS ────────────────────────────────
    public void OpenSecretCheatPanel()
    {
        if (secretCanvasGO == null)
        {
            BuildSecretCheatPanelUI();
        }

        isSecretPanelOpen = true;
        UpdateOpenState();

        if (secretCanvasGO != null) secretCanvasGO.SetActive(true);

        // Se a bolsa sintética estiver aberta por engano, fecha
        if (SyntheticBagUI.Instance != null)
        {
            SyntheticBagUI.Instance.CloseBag();
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        FreezePlayer(true);

        LogAllCheatsToConsole();
    }

    public void CloseSecretCheatPanel()
    {
        isSecretPanelOpen = false;
        UpdateOpenState();

        if (secretCanvasGO != null) secretCanvasGO.SetActive(false);

        if (!isConsoleActive)
        {
            FreezePlayer(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    public void ToggleSecretCheatPanel()
    {
        if (isSecretPanelOpen)
            CloseSecretCheatPanel();
        else
            OpenSecretCheatPanel();
    }

    public void ExecuteFromSecretPanel(string payload)
    {
        Debug.Log($"🔒 [SECRET CHEAT] Executando pelo painel: '{payload}'");
        CheckConsoleCommand(payload, closeConsoleAfter: false);
    }

    private void LogAllCheatsToConsole()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("==================================================================");
        sb.AppendLine("🔒 [DEV SECRETO] MANUAL DE COMANDOS DO CHEAT CONSOLE");
        sb.AppendLine("==================================================================");
        string curCat = "";
        foreach (var c in SecretCheats)
        {
            if (c.category != curCat)
            {
                curCat = c.category;
                sb.AppendLine($"\n--- {curCat} ---");
            }
            sb.AppendLine($"• {c.command,-12} [{c.aliases}] -> {c.description}");
        }
        sb.AppendLine("==================================================================");
        Debug.Log(sb.ToString());
    }

    private void BuildSecretCheatPanelUI()
    {
        // 1. Canvas principal
        secretCanvasGO = new GameObject("SecretCheatPanelCanvas");
        DontDestroyOnLoad(secretCanvasGO);

        Canvas canvas = secretCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;

        CanvasScaler scaler = secretCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        secretCanvasGO.AddComponent<GraphicRaycaster>();

        // 2. Dimmer de fundo (bloqueia cliques no jogo)
        GameObject dimmer = new GameObject("Dimmer");
        dimmer.transform.SetParent(secretCanvasGO.transform, false);
        RectTransform dr = dimmer.AddComponent<RectTransform>();
        dr.anchorMin = Vector2.zero;
        dr.anchorMax = Vector2.one;
        dr.sizeDelta = Vector2.zero;

        Image dimImg = dimmer.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);

        Button dimBtn = dimmer.AddComponent<Button>();
        dimBtn.onClick.AddListener(CloseSecretCheatPanel);

        // 3. Janela Principal (Painel)
        secretPanelGO = new GameObject("SecretWindow");
        secretPanelGO.transform.SetParent(secretCanvasGO.transform, false);

        RectTransform pr = secretPanelGO.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(1060f, 740f);

        Image bg = secretPanelGO.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);

        // Borda ciano/neon com Outline
        Outline winOutline = secretPanelGO.AddComponent<Outline>();
        winOutline.effectColor = new Color(0.18f, 0.82f, 0.92f, 0.85f);
        winOutline.effectDistance = new Vector2(2f, -2f);

        // 4. Header
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(secretPanelGO.transform, false);
        RectTransform hr = headerObj.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = new Vector2(0f, -10f);
        hr.sizeDelta = new Vector2(0f, 65f);

        // Título e Subtítulo no Header
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform tr = titleObj.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0.45f);
        tr.anchorMax = new Vector2(0.9f, 1f);
        tr.anchoredPosition = new Vector2(25f, 0f);
        tr.sizeDelta = Vector2.zero;

        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "🔒 <color=#00E5FF>PAINEL SECRETO DE COMANDOS</color> <color=#778899>// CHEAT SHEET DO DESENVOLVEDOR</color>";
        titleTxt.fontSize = 20f;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = Color.white;
        titleTxt.alignment = TextAlignmentOptions.Left;

        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(headerObj.transform, false);
        RectTransform sr = subObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(0.9f, 0.45f);
        sr.anchoredPosition = new Vector2(25f, 0f);
        sr.sizeDelta = Vector2.zero;

        TextMeshProUGUI subTxt = subObj.AddComponent<TextMeshProUGUI>();
        subTxt.text = "Atalho: Pressione <color=#FFE066>[Ctrl+Shift+F1]</color> ou <color=#FFE066>[ESC]</color> para fechar | Clique em <color=#00FF99>[EXECUTAR]</color> para testar qualquer cheat direto!";
        subTxt.fontSize = 12f;
        subTxt.color = new Color(0.7f, 0.8f, 0.9f);
        subTxt.alignment = TextAlignmentOptions.Left;

        // Botão Fechar [X]
        GameObject closeBtnObj = new GameObject("BtnClose");
        closeBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform cbr = closeBtnObj.AddComponent<RectTransform>();
        cbr.anchorMin = new Vector2(1f, 0.5f);
        cbr.anchorMax = new Vector2(1f, 0.5f);
        cbr.pivot = new Vector2(1f, 0.5f);
        cbr.sizeDelta = new Vector2(40f, 40f);
        cbr.anchoredPosition = new Vector2(-20f, 0f);

        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);

        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseSecretCheatPanel);

        GameObject closeTxtObj = new GameObject("X");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform ctr = closeTxtObj.AddComponent<RectTransform>();
        ctr.anchorMin = Vector2.zero;
        ctr.anchorMax = Vector2.one;
        ctr.sizeDelta = Vector2.zero;

        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.fontSize = 20f;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;

        // 5. Scroll View
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(secretPanelGO.transform, false);
        RectTransform scr = scrollObj.AddComponent<RectTransform>();
        scr.anchorMin = new Vector2(0f, 0f);
        scr.anchorMax = new Vector2(1f, 1f);
        scr.offsetMin = new Vector2(20f, 15f);
        scr.offsetMax = new Vector2(-20f, -80f);

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 35f;

        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vr = viewport.AddComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.sizeDelta = Vector2.zero;
        vr.pivot = new Vector2(0f, 1f);

        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vr;

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform cr = content.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(6, 6, 6, 12);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = cr;

        // Scrollbar vertical
        GameObject scrollbarObj = new GameObject("Scrollbar");
        scrollbarObj.transform.SetParent(scrollObj.transform, false);
        RectTransform sbr = scrollbarObj.AddComponent<RectTransform>();
        sbr.anchorMin = new Vector2(1f, 0f);
        sbr.anchorMax = new Vector2(1f, 1f);
        sbr.pivot = new Vector2(1f, 1f);
        sbr.sizeDelta = new Vector2(10f, 0f);
        sbr.anchoredPosition = Vector2.zero;

        Image sbarBg = scrollbarObj.AddComponent<Image>();
        sbarBg.color = new Color(0.1f, 0.14f, 0.2f, 0.6f);

        Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Sliding Area
        GameObject slideArea = new GameObject("SlidingArea");
        slideArea.transform.SetParent(scrollbarObj.transform, false);
        RectTransform sar = slideArea.AddComponent<RectTransform>();
        sar.anchorMin = Vector2.zero;
        sar.anchorMax = Vector2.one;
        sar.sizeDelta = Vector2.zero;

        // Handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(slideArea.transform, false);
        RectTransform hrt = handle.AddComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.sizeDelta = Vector2.zero;

        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.2f, 0.7f, 0.9f, 0.85f);

        scrollbar.handleRect = hrt;
        scrollbar.targetGraphic = handleImg;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 4f;

        // Popula as categorias e comandos
        string lastCategory = "";
        foreach (var entry in SecretCheats)
        {
            if (entry.category != lastCategory)
            {
                lastCategory = entry.category;
                CreateCategoryHeader(content.transform, entry.category);
            }
            CreateCheatRow(content.transform, entry);
        }
    }

    private void CreateCategoryHeader(Transform parent, string categoryName)
    {
        GameObject catObj = new GameObject("Cat_" + categoryName);
        catObj.transform.SetParent(parent, false);

        LayoutElement le = catObj.AddComponent<LayoutElement>();
        le.preferredHeight = 32f;
        le.minHeight = 32f;

        Image catBg = catObj.AddComponent<Image>();
        catBg.color = new Color(0.12f, 0.18f, 0.28f, 0.85f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(catObj.transform, false);
        RectTransform tr = textObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 0f);
        tr.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = categoryName;
        txt.fontSize = 13f;
        txt.fontStyle = FontStyles.Bold;
        txt.color = new Color(1.0f, 0.88f, 0.45f);
        txt.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void CreateCheatRow(Transform parent, CheatEntry entry)
    {
        GameObject rowObj = new GameObject("Row_" + entry.command);
        rowObj.transform.SetParent(parent, false);

        LayoutElement le = rowObj.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;
        le.minHeight = 52f;

        Image rowBg = rowObj.AddComponent<Image>();
        rowBg.color = new Color(0.07f, 0.09f, 0.14f, 0.9f);

        // Container do Texto (lado esquerdo)
        GameObject textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(rowObj.transform, false);
        RectTransform tcr = textContainer.AddComponent<RectTransform>();
        tcr.anchorMin = new Vector2(0f, 0f);
        tcr.anchorMax = new Vector2(1f, 1f);
        tcr.offsetMin = new Vector2(14f, 4f);
        tcr.offsetMax = new Vector2(-130f, -4f);

        TextMeshProUGUI cmdTxt = textContainer.AddComponent<TextMeshProUGUI>();
        string aliasText = string.IsNullOrEmpty(entry.aliases) || entry.aliases == "-" 
            ? "" 
            : $" <color=#778899>({entry.aliases})</color>";
        cmdTxt.text = $"<b><color=#00F0FF>{entry.command}</color></b>{aliasText}\n<size=11><color=#CCDDEE>{entry.description}</color></size>";
        cmdTxt.fontSize = 13f;
        cmdTxt.alignment = TextAlignmentOptions.MidlineLeft;

        // Botão Executar (lado direito)
        GameObject btnObj = new GameObject("BtnExec_" + entry.command);
        btnObj.transform.SetParent(rowObj.transform, false);
        RectTransform br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0.5f);
        br.anchorMax = new Vector2(1f, 0.5f);
        br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(110f, 34f);
        br.anchoredPosition = new Vector2(-10f, 0f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.48f, 0.32f, 0.95f);

        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.3f, 0.9f, 0.6f, 0.8f);
        btnOutline.effectDistance = new Vector2(1f, -1f);

        Button btn = btnObj.AddComponent<Button>();
        string payload = entry.executePayload;
        btn.onClick.AddListener(() => ExecuteFromSecretPanel(payload));

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        RectTransform btr = btnTxtObj.AddComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.sizeDelta = Vector2.zero;

        TextMeshProUGUI bTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
        bTxt.text = "EXECUTAR";
        bTxt.fontSize = 12f;
        bTxt.fontStyle = FontStyles.Bold;
        bTxt.color = Color.white;
        bTxt.alignment = TextAlignmentOptions.Center;
    }

    // ─── PROCESSAMENTO DE COMANDOS DIGITADOS ─────────────────────────────────
    public void CheckConsoleCommand(string typedText)
    {
        CheckConsoleCommand(typedText, closeConsoleAfter: true);
    }

    public void CheckConsoleCommand(string typedText, bool closeConsoleAfter)
    {
        // Se typedText foi passado diretamente (ex: pelo botão EXECUTAR), usa ele.
        // Se estiver vazio, pega da caixa de texto consoleInput.
        string textoReal = typedText;
        if (string.IsNullOrWhiteSpace(textoReal) && consoleInput != null)
        {
            textoReal = consoleInput.text;
        }

        if (string.IsNullOrWhiteSpace(textoReal)) 
        {
            Debug.LogWarning("🔍 CHEAT: A caixa de texto foi lida como completamente vazia.");
            if (closeConsoleAfter && isConsoleActive) ToggleConsole();
            return;
        }

        string command = textoReal.Replace("/", "").Trim();
        Debug.Log($"🔍 CHEAT: O script limpou o texto recebido e tentará bater esta palavra com a senha: '{command}'");

        // ── AJUDA / MANUAL SECRETO ──
        if (string.Equals(command, "help", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "ajuda", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "comandos", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "commands", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "lista", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "secret", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "segredo", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "cheats", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "cheatlist", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "?", System.StringComparison.OrdinalIgnoreCase))
        {
            if (isConsoleActive) ToggleConsole();
            OpenSecretCheatPanel();
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("🔒 Painel Secreto de Comandos Aberto! [F1]");
            }
            return;
        }
        else if (string.Equals(command, "EPTA", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Comando EPTA Recebido! Iniciando Breakdance...");
            
            if (playerAnimator != null)
            {
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

            DummyHealth[] dummies = Object.FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
            int count = 0;
            foreach (DummyHealth dummy in dummies)
            {
                if (dummy != null)
                {
                    if (dummy.CompareTag("Player")) continue;
                    
                    dummy.isInvulnerable = false;
                    dummy.TakeDamage(999999);
                    count++;
                }
            }

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

            string statusText = $"{count} inimigos eliminados com sucesso!";
            Debug.Log($"💻 CHEAT: {statusText}");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
            }
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
        else if (string.Equals(command, "heal", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "vida", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "curar", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "cura", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "god", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
            if (health != null)
            {
                health.Heal(9999);
                string msg = "VIDA E ARMADURA RESTAURADAS AO MÁXIMO!";
                Debug.Log($"💻 CHEAT: {msg}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
            else
            {
                Debug.LogWarning("💻 CHEAT: PlayerHealth não encontrado na cena!");
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
        else if (string.Equals(command, "rebirth", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "eliterebirth", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "rebirthelite", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "forcerebirth", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "renascer", System.StringComparison.OrdinalIgnoreCase))
        {
            DummyHealth.forceEliteRebirth = !DummyHealth.forceEliteRebirth;
            ShardSwarmHealth.forceEliteRebirth = DummyHealth.forceEliteRebirth;
            string status = DummyHealth.forceEliteRebirth ? "ATIVADO (100% de chance ao matar)" : "DESATIVADO (10% normal)";
            string statusText = $"RENASCIMENTO ELITE: {status}";
            Debug.Log($"💻 CHEAT: {statusText}");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
            }
        }
        else if (string.Equals(command, "sinfonia", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "chaos", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "pactchaos", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "pactcaos", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayerHealth ph = Object.FindFirstObjectByType<PlayerHealth>();
            if (ph != null)
            {
                ph.hasChaosSymphony = !ph.hasChaosSymphony;
                ph.hasDoubleLoot = ph.hasChaosSymphony;
                string status = ph.hasChaosSymphony ? "ATIVADA (+20% Drops, +Elites, Rebirth 35%)" : "DESATIVADA";
                string statusText = $"A SINFONIA DO CAOS: {status}";
                Debug.Log($"💻 CHEAT: {statusText}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(statusText);
                }
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
            Debug.Log("💻 CHEAT APROVADO: Destravando todas as melhorias e sinergias!");
            if (DevCheatConsole.Instance != null)
            {
                DevCheatConsole.Instance.UnlockAllEquipment();
            }
            if (SynergyCatalogManager.Instance != null)
            {
                SynergyCatalogManager.Instance.DiscoverAllSynergies();
            }
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("Todas as Melhorias e Sinergias Destravadas!");
            }
        }
        else if (string.Equals(command, "sinergias", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "synergies", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "synergy", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "unlocksynergies", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "mapasinergias", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "mapa", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Destravando todas as 10 sinergias para o mapa!");
            if (SynergyCatalogManager.Instance != null)
            {
                SynergyCatalogManager.Instance.DiscoverAllSynergies();
            }
            if (SynergyMapPrototype.Instance != null && SynergyMapPrototype.Instance.IsOpen)
            {
                SynergyMapPrototype.Instance.RefreshConstellation();
            }
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("✨ Todas as 10 Sinergias Alquímicas Catalogadas!\nPressione [M] para abrir o Mapa Cósmico.");
            }
        }
        else if (string.Equals(command, "unlocklore", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "lore", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "escrituras", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "scriptures", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "caderno", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "livro", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "journal", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "book", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Destravando todas as 18 escrituras e o Caderno do Pesquisador!");
            LoreManager.Instance.UnlockAllScripturesAndBook();
            JournalBookUI.Instance?.OpenJournal();
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
            CloseSecretCheatPanel();
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
                GameManager.EnsureInstance().LoadSceneWithLoading("BossRoom_Cromatico", false);
            }
            else if (Application.CanStreamedLevelBeLoaded("Geobionte"))
            {
                GameManager.EnsureInstance().LoadSceneWithLoading("Geobionte", false);
            }
        }
        else if (string.Equals(command, "boss", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "loadboss", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "gotoboss", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Forçando Boss Round e Carregando Sala do Boss Padrão!");
            CloseSecretCheatPanel();
            BossPhase1_MobSpawner.SetBossSoloMode(false);

            if (RunManager.instance != null)
            {
                RunManager.instance.currentLevel = RunManager.instance.totalLevels;
                RunManager.instance.forceBossNextRun = true;
                Debug.Log($"💻 CHEAT: currentLevel setado para {RunManager.instance.currentLevel}/{RunManager.instance.totalLevels}.");
            }

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("Boss Round ATIVADO!\nEntrando no combate...");
            }

            if (Application.CanStreamedLevelBeLoaded("BossRoom_Cromatico"))
            {
                GameManager.EnsureInstance().LoadSceneWithLoading("BossRoom_Cromatico", false);
            }
            else if (Application.CanStreamedLevelBeLoaded("Geobionte"))
            {
                GameManager.EnsureInstance().LoadSceneWithLoading("Geobionte", false);
            }
        }
        else if (string.Equals(command, "poison", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "veneno", System.StringComparison.OrdinalIgnoreCase))
        {
            Tier4EffectManager t4 = Object.FindFirstObjectByType<Tier4EffectManager>();
            if (t4 != null)
            {
                t4.ActivateEffect(Tier4EffectType.Poison);
                string msg = "EFEITO T4 POISON ATIVADO!\nAtaques aplicam veneno com números verdes!";
                Debug.Log($"💻 CHEAT: {msg}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
            else
            {
                Debug.LogWarning("💻 CHEAT: Tier4EffectManager não encontrado na cena!");
            }
        }
        else if (string.Equals(command, "thorns", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "espinhos", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "espinho", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayerAttributesDefensive def = Object.FindFirstObjectByType<PlayerAttributesDefensive>();
            if (def != null)
            {
                def.thorns += 20;
                string msg = $"THORNS AUMENTADO!\nRefletindo {def.thorns} de dano a atacantes!";
                Debug.Log($"💻 CHEAT: {msg}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
        }
        else if (string.Equals(command, "pact", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "mercador", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "pacto", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayerHealth ph = Object.FindFirstObjectByType<PlayerHealth>();
            if (ph != null)
            {
                ph.SetPactCorrupted(!ph.hasPactCorrupted);
                string msg = ph.hasPactCorrupted ? "PACTO DO MERCADOR ATIVADO!\nAnimação de garras e sangue corrompido!" : "PACTO DO MERCADOR REMOVIDO!";
                Debug.Log($"💻 CHEAT: {msg}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
        }
        else if (string.Equals(command, "aim", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "mira", System.StringComparison.OrdinalIgnoreCase))
        {
            AimSettings.ToggleMode();
            string msg = $"MODO DE MIRA ALTERADO: {(AimSettings.IsMouseAim ? "MOUSE (PADRÃO)" : "DIREÇÃO DO PERSONAGEM (WASD)")}";
            Debug.Log($"💻 CHEAT: {msg}");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(msg);
            }
        }
        else if (string.Equals(command, "resetcaderno", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "resetlivro", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "resetbook", System.StringComparison.OrdinalIgnoreCase))
        {
            if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
            {
                SaveManager.instance.CachedData.hasFoundResearcherBook = false;
                SaveManager.instance.SavePersistentData();
            }
            if (FloatingBookDisplay.Instance != null)
            {
                FloatingBookDisplay.Instance.CheckBookVisibility();
            }
            string msg = "CADERNO RESETADO!\nAgora você pode encontrá-lo no chão da Safe Room novamente.";
            Debug.Log($"💻 CHEAT: {msg}");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(msg);
            }
        }
        else if (string.Equals(command, "limparescrituras", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "clearscriptures", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "clearlore", System.StringComparison.OrdinalIgnoreCase))
        {
            if (LoreManager.Instance != null)
            {
                LoreManager.Instance.LimparEscriturasSalvas();
                string msg = "ESCRITURAS E BLUEPRINTS RESETADOS!\nTodas as páginas voltaram a ficar em branco.";
                Debug.Log($"💻 CHEAT: {msg}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
        }
        else if (string.Equals(command, "door", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "porta", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "lab", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "opendoor", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "abrirporta", System.StringComparison.OrdinalIgnoreCase))
        {
            if (SaveManager.instance != null)
            {
                SaveManager.instance.AddCraftedEquipment("eq_consertar_porta");
                SaveManager.instance.SavePersistentData();
            }
            LabDoorManager doorManager = Object.FindFirstObjectByType<LabDoorManager>();
            if (doorManager != null)
            {
                doorManager.UpdateDoorState();
            }
            string msg = "PORTA DO LABORATÓRIO CONSERTADA E ABERTA!";
            Debug.Log($"💻 CHEAT: {msg}");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(msg);
            }
        }
        else if (string.Equals(command, "clearinv", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "limparinv", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "limparmochila", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "emptyinv", System.StringComparison.OrdinalIgnoreCase))
        {
            PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
            if (inv != null)
            {
                inv.ClearInventory();
                string msg = "MOCHILA ESVAZIADA (0/10 SLOTS)!\nPronta para novos testes de fusão!";
                Debug.Log($"💻 CHEAT: {msg}");
                if (EptinhoPopupController.instancia != null)
                {
                    EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
        }
        else if (command.StartsWith("fusao", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("combo", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("synergy", System.StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            int comboNum = 1;
            if (parts.Length > 1) int.TryParse(parts[1], out comboNum);
            GiveSynergyComboItems(comboNum);
        }
        else if (command.StartsWith("giveitem", System.StringComparison.OrdinalIgnoreCase) ||
                 command.StartsWith("item", System.StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                string itemId = parts[1].Trim();
                PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
                if (inv != null)
                {
                    bool ok = inv.AddItem(itemId, 1);
                    string msg = ok ? $"ITEM CONCEDIDO: {itemId}!" : $"FALHA: Mochila cheia (10/10)!";
                    Debug.Log($"💻 CHEAT: {msg}");
                    if (EptinhoPopupController.instancia != null) EptinhoPopupController.instancia.MostrarPopupAviso(msg);
                }
            }
        }
        else if (string.Equals(command, "reset", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "novojogo", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "newgame", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "resetall", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(command, "resetsave", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("💻 CHEAT APROVADO: Reiniciando jogo e zerando save!");
            CloseSecretCheatPanel();
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("REINICIANDO JORNADA DO ZERO...");
            }
            SaveManager.RestartGameFromZero();
        }
        else
        {
            Debug.LogWarning($"💻 Console: Comando '{command}' não reconhecido.");
        }

        // Depois de tentar o cheat, fecha o console pro jogo voltar ao normal (se solicitado e estiver ativo)
        if (closeConsoleAfter && isConsoleActive)
        {
            ToggleConsole(); 
        }
    }

    public static void GiveSynergyComboItems(int comboNum)
    {
        PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv == null)
        {
            Debug.LogWarning("💻 CHEAT: PlayerInventory não encontrado!");
            return;
        }

        // Limpa a mochila primeiro para que os itens caibam sem conflito
        inv.ClearInventory();

        string comboName = "";
        string[] itemsToGive = null;

        switch (comboNum)
        {
            case 1:
                comboName = "Chuva de Lâminas (Goblin + SharpBlur)";
                itemsToGive = new[] { "goblin_coin_t1", "01sb" };
                break;
            case 2:
                comboName = "Parede Intransponível (+30 Armadura / Golem + Sentinela)";
                itemsToGive = new[] { "golem_chip_t1", "bismutado_placa_t2" };
                break;
            case 3:
                comboName = "Corte Congelante (Cristalus + SharpBlur)";
                itemsToGive = new[] { "02cl", "03sb" };
                break;
            case 4:
                comboName = "Assassino Fantasma (Aranha + Pedra Mágica)";
                itemsToGive = new[] { "spider_silk_t2", "magic_rune_t2" };
                break;
            case 5:
                comboName = "Domínio de Espaço (Totem + Watcher)";
                itemsToGive = new[] { "totem_carved_t2", "02cw" };
                break;
            case 6:
                comboName = "Tempestade Focada (Tuner + Shard Swarm)";
                itemsToGive = new[] { "tuner_lens_t2", "shard_resonant_t2" };
                break;
            case 7:
                comboName = "Aura Espinhosa (Peixe + Cristalus)";
                itemsToGive = new[] { "peixe_escama_t1", "01cl" };
                break;
            case 8:
                comboName = "Fuga Explosiva (Totem + Goblin)";
                itemsToGive = new[] { "totem_stone_t1", "goblin_braço_t2" };
                break;
            case 9:
                comboName = "Berserker Imortal (4 Itens: Golem T4 + Bismutado + Istar + Aranha)";
                itemsToGive = new[] { "golem_heart_t4", "bismutado_glândula_t3", "shard_splinter_t1", "spider_leg_t1" };
                break;
            case 10:
                comboName = "Veneno Corrosivo (Peixe + Aranha)";
                itemsToGive = new[] { "Peixe_barbatana_t3", "spider_fang_t3" };
                break;
            case 11:
                comboName = "Baluarte de Obsidiana (+40 Armadura / Golem T2 + Pedra Mágica)";
                itemsToGive = new[] { "golem_plate_t2", "magic_dust_t1" };
                break;
            case 12:
                comboName = "Impacto Sísmico (Golem + Totem)";
                itemsToGive = new[] { "golem_core_t3", "totem_ancient_t3" };
                break;
            case 13:
                comboName = "Mira Focada do Vigia (Watcher + SharpBlur)";
                itemsToGive = new[] { "watcher_lens_t1", "02sb" };
                break;
            case 14:
                comboName = "Eco Hipnótico (Tuner + Cristalus)";
                itemsToGive = new[] { "tuner_shard_t1", "03cl" };
                break;
            case 15:
                comboName = "Toxina Ardente (Peixe + Goblin)";
                itemsToGive = new[] { "Peixe_guelra_t2", "goblin_amulet_t3" };
                break;
            case 16:
                comboName = "Cintilação Espectral (Pedra Mágica + Aranha)";
                itemsToGive = new[] { "magic_essence_t3", "spider_silk_t2" };
                break;
            case 17:
                comboName = "Frenesi de Estilhaços (Shard Swarm + SharpBlur)";
                itemsToGive = new[] { "shard_refract_t3", "01sb" };
                break;
            case 18:
                comboName = "Fortaleza de Titânio (+30 Armadura / Sentinela + Totem)";
                itemsToGive = new[] { "bismutado_placa_t2", "totem_carved_t2" };
                break;
            case 19:
                comboName = "Cataclismo Arcano (Bismutado + Tuner)";
                itemsToGive = new[] { "bismutado_t2", "tuner_prism_t3" };
                break;
            case 20:
                comboName = "Supercondutividade Alquímica (Cristalus + Goblin)";
                itemsToGive = new[] { "03cl", "goblin_bomb_t4" };
                break;
            case 21:
            default:
                comboName = "A Ascensão: Modo Deus (+100 Armadura / 4x Lendários T4)";
                itemsToGive = new[] { "golem_heart_t4", "goblin_bomb_t4", "watcher_beacon_t4", "sentinel_leg_t4" };
                break;
        }

        foreach (var item in itemsToGive)
        {
            inv.AddItem(item, 1);
        }

        string popup = $"FUSÃO #{comboNum} PRONTA!\n{comboName}\nPressione [ I ] para fundir!";
        Debug.Log($"💻 CHEAT: {popup}");
        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso(popup);
        }
    }
}
