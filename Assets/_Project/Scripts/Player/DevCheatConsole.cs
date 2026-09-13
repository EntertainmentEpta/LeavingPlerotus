using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Console de Cheats & Desenvolvedor — RogueLike Project.
/// Pressione tecla [ ~ ] (Tilde/Aspas) ou [ F12 ] para abrir/fechar o console.
/// Permite obter recursos infinitos, destravar todas as melhorias e orbs com 1 clique.
/// </summary>
public class DevCheatConsole : MonoBehaviour
{
    public static DevCheatConsole Instance { get; private set; }

    [Header("Teclas de Ativação")]
    public KeyCode toggleKey1 = KeyCode.BackQuote; // Tecla ~ / `
    public KeyCode toggleKey2 = KeyCode.F12;       // Tecla F12

    // ── Elementos Visuais Internos ───────────────────────────────────────────
    private GameObject canvasGO;
    private GameObject panelGO;
    private TMP_InputField inputField;
    private TextMeshProUGUI logText;
    private bool isOpen = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildConsoleUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey1) || Input.GetKeyDown(toggleKey2))
        {
            ToggleConsole();
        }
    }

    // ─── CHEAT COMMANDS (API PÚBLICA) ────────────────────────────────────────

    /// <summary>
    /// Adiciona +999 de todos os itens do ItemDatabase e receitas à Bolsa Sintética.
    /// Recursos infinitos para craftar qualquer item no jogo!
    /// </summary>
    public void GiveAllResources(int amount = 999)
    {
        if (SaveManager.instance == null)
        {
            Log("[ERRO] SaveManager não encontrado.");
            return;
        }

        HashSet<string> addedIds = new HashSet<string>();

        // 1. Itens do ItemDatabase
        if (ItemDatabase.Instance != null && ItemDatabase.Instance.allItems != null)
        {
            foreach (var item in ItemDatabase.Instance.allItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                {
                    SaveManager.instance.AddResourceToBase(item.itemId, amount);
                    addedIds.Add(item.itemId);
                }
            }
        }

        // 2. Ingredientes de todas as receitas cadastradas no CraftingManager
        if (CraftingManager.Instance != null)
        {
            foreach (var recipe in CraftingManager.Instance.GetAllRecipes())
            {
                if (recipe != null && recipe.ingredients != null)
                {
                    foreach (var ing in recipe.ingredients)
                    {
                        if (ing != null && !string.IsNullOrEmpty(ing.itemId) && !addedIds.Contains(ing.itemId))
                        {
                            SaveManager.instance.AddResourceToBase(ing.itemId, amount);
                            addedIds.Add(ing.itemId);
                        }
                    }
                }
            }
        }

        // Adiciona recursos comuns de fallback
        string[] fallbackResources = new string[] { "prismalita", "caracol_geodo", "Crystal", "Tinker", "shard_splinter_t1", "magic_dust_t1" };
        foreach (string res in fallbackResources)
        {
            SaveManager.instance.AddResourceToBase(res, amount);
            addedIds.Add(res);
        }

        SaveManager.instance.SavePersistentData();

        Log($"<color=#4AE04A>[CHEAT OK]</color> Concedido +{amount} de {addedIds.Count} recursos à Bolsa Sintética!");
    }

    /// <summary>
    /// Destrava e crafta todas as melhorias equipáveis cadastradas no jogo.
    /// </summary>
    public void UnlockAllEquipment()
    {
        if (EquipmentManager.Instance == null || SaveManager.instance == null)
        {
            Log("[ERRO] EquipmentManager ou SaveManager não encontrado.");
            return;
        }

        int count = 0;
        foreach (var equip in EquipmentManager.Instance.allEquipmentDefinitions)
        {
            if (equip != null && !string.IsNullOrEmpty(equip.equipmentId))
            {
                SaveManager.instance.AddCraftedEquipment(equip.equipmentId);
                count++;
            }
        }

        SaveManager.instance.SavePersistentData();
        Log($"<color=#4AE04A>[CHEAT OK]</color> Destravadas {count} melhorias equipáveis no Save!");
    }

    /// <summary>
    /// Concede +99.999 de Essência / Orbs ao jogador.
    /// </summary>
    public void GiveMaxOrbs(int amount = 99999)
    {
        PlayerEssence essence = FindFirstObjectByType<PlayerEssence>();
        if (essence != null)
        {
            essence.AddEssence(amount);
            Log($"<color=#FFD154>[CHEAT OK]</color> Concedidos +{amount} Orbs/Essência!");
        }
        else
        {
            Log("<color=#FFD154>[CHEAT OK]</color> Orbs gravados no Save!");
        }
    }

    /// <summary>
    /// Cura o jogador e restaura a armadura ao máximo.
    /// </summary>
    public void HealPlayer()
    {
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            health.Heal(9999);
            Log("<color=#4AE04A>[CHEAT OK]</color> Vida e armadura do jogador restauradas!");
        }
        else
        {
            Log("[ERRO] PlayerHealth não encontrado no mapa.");
        }
    }

    // ─── LÓGICA DE COMANDOS DIGITADOS ────────────────────────────────────────

    public void ExecuteCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        string cmd = input.Trim().ToLower();
        Log($"> {cmd}");

        if (cmd == "allitems" || cmd == "cheats" || cmd == "recursos" || cmd == "giveall" || cmd == "1")
        {
            GiveAllResources(999);
        }
        else if (cmd == "unlockall" || cmd == "equip" || cmd == "2")
        {
            UnlockAllEquipment();
        }
        else if (cmd == "orbs" || cmd == "money" || cmd == "gold" || cmd == "3")
        {
            GiveMaxOrbs(99999);
        }
        else if (cmd == "heal" || cmd == "god" || cmd == "4")
        {
            HealPlayer();
        }
        else if (cmd == "allmobs" || cmd == "spawnall" || cmd == "mobs" || cmd == "mobsall" || cmd == "showcase" || cmd == "bichos" || cmd == "5")
        {
            RoomController.forceAllMobsMode = true;
            int spawned = RoomController.SpawnAllMobsNow();
            Log($"<color=#FF5555>[ALL MOBS]</color> Modo Todos os Mobs Ativado! ({spawned} inimigos spawnados)");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso($"MODO TODOS OS MOBS ATIVADO!\n{spawned} bichos spawnados!");
            }
        }
        else if (cmd == "togglemobs" || cmd == "normalmobs")
        {
            RoomController.forceAllMobsMode = !RoomController.forceAllMobsMode;
            string status = RoomController.forceAllMobsMode ? "ATIVADO" : "DESATIVADO";
            Log($"<color=#FF5555>[ALL MOBS]</color> Modo Todos os Mobs: {status}");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso($"Modo Todos os Mobs: {status}");
            }
        }
        else if (cmd == "help" || cmd == "?")
        {
            Log("Comandos disponíveis:");
            Log(" • allmobs / spawnall   -> Spawnar 1 de cada bicho e ativar modo de todos os mobs");
            Log(" • allitems / recursos  -> Dar +999 de todos os materiais");
            Log(" • unlockall / equip    -> Destravar todas as melhorias");
            Log(" • orbs / money         -> Dar +99.999 Orbs");
            Log(" • heal / god           -> Curar o player ao máximo");
            Log(" • togglemobs           -> Alternar modo Todos os Mobs ligado/desligado");
        }
        else
        {
            Log($"Comando '{cmd}' não reconhecido. Digite 'help' para ajuda.");
        }

        if (inputField != null) inputField.text = "";
    }

    public void ToggleConsole()
    {
        isOpen = !isOpen;
        if (panelGO != null) panelGO.SetActive(isOpen);

        if (isOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (inputField != null) inputField.ActivateInputField();
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void Log(string message)
    {
        Debug.Log($"[DEV CHEAT] {message}");
        if (logText != null)
        {
            logText.text += "\n" + message;
        }
    }

    // ─── CONSTRUÇÃO DA UI ─────────────────────────────────────────────────────

    private void BuildConsoleUI()
    {
        canvasGO = new GameObject("DevCheatConsoleCanvas");
        DontDestroyOnLoad(canvasGO);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Fica acima de todas as UIs

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Painel Principal
        panelGO = new GameObject("CheatPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        RectTransform pr = panelGO.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot     = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(750f, 480f);

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.08f, 0.95f);

        // Borda Roxa Neon
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(panelGO.transform, false);
        RectTransform br = borderObj.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = new Vector2(4f, 4f);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.6f, 0.35f, 0.95f, 0.8f);
        borderImg.fillCenter = false;

        // Header
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(panelGO.transform, false);
        RectTransform hr = headerObj.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f); hr.anchoredPosition = new Vector2(0f, -8f);
        hr.sizeDelta = new Vector2(0f, 36f);
        TextMeshProUGUI headerTxt = headerObj.AddComponent<TextMeshProUGUI>();
        headerTxt.text = "⚡ DEV CHEAT CONSOLE // RECURSOS INFINITOS";
        headerTxt.fontSize = 18f; headerTxt.fontStyle = FontStyles.Bold;
        headerTxt.color = new Color(0.9f, 0.85f, 1.0f); headerTxt.alignment = TextAlignmentOptions.Center;

        // Log Window
        GameObject logObj = new GameObject("LogWindow");
        logObj.transform.SetParent(panelGO.transform, false);
        RectTransform lr = logObj.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.04f, 0.38f); lr.anchorMax = new Vector2(0.96f, 0.88f);
        lr.sizeDelta = Vector2.zero;
        Image logBg = logObj.AddComponent<Image>(); logBg.color = new Color(0f, 0f, 0f, 0.6f);
        logText = logObj.AddComponent<TextMeshProUGUI>();
        logText.fontSize = 14f; logText.color = new Color(0.85f, 0.95f, 0.85f);
        logText.alignment = TextAlignmentOptions.BottomLeft;
        logText.text = "<b>Console de Cheats ativado!</b> Pressione [~] ou [F12] para ocultar.\nClique em um dos botões abaixo para dar recursos:";

        // Botões Rápidos
        CreateButton(panelGO.transform, "🎁 RECURSOS (+999)", new Vector2(0.04f, 0.22f), new Vector2(0.32f, 0.34f), () => GiveAllResources(999), new Color(0.25f, 0.65f, 0.35f));
        CreateButton(panelGO.transform, "🔓 DESTRAVAR TUDO", new Vector2(0.35f, 0.22f), new Vector2(0.64f, 0.34f), () => UnlockAllEquipment(), new Color(0.35f, 0.45f, 0.85f));
        CreateButton(panelGO.transform, "💰 +99.999 ORBS", new Vector2(0.67f, 0.22f), new Vector2(0.96f, 0.34f), () => GiveMaxOrbs(99999), new Color(0.85f, 0.65f, 0.25f));

        // Input Field para digitar comandos
        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(panelGO.transform, false);
        RectTransform ir = inputObj.AddComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.04f, 0.06f); ir.anchorMax = new Vector2(0.78f, 0.18f);
        ir.sizeDelta = Vector2.zero;
        Image inputBg = inputObj.AddComponent<Image>(); inputBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);
        RectTransform tr = textObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = new Vector2(-10f, 0f);
        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 16f; inputText.color = Color.white;

        inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.textComponent = inputText;
        inputField.onEndEdit.AddListener((val) => { if (Input.GetKeyDown(KeyCode.Return)) ExecuteCommand(val); });

        // Botão Enviar Comando
        CreateButton(panelGO.transform, "EXECUTAR", new Vector2(0.80f, 0.06f), new Vector2(0.96f, 0.18f), () => ExecuteCommand(inputField.text), new Color(0.5f, 0.3f, 0.7f));

        panelGO.SetActive(false);
    }

    private void CreateButton(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick, Color btnColor)
    {
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(parent, false);

        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = min; r.anchorMax = max; r.sizeDelta = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Button b = btnObj.AddComponent<Button>();
        b.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform tr = textObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;

        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label; txt.fontSize = 13f; txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white; txt.alignment = TextAlignmentOptions.Center;
    }
}


