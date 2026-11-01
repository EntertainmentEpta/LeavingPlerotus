using UnityEngine;

public class KeybindApplier : MonoBehaviour
{
    private static KeybindApplier _instance;

    // A linha direta de comunicação com fallback robusto
    public static KeybindApplier Instance
    {
        get
        {
            if (_instance == null)
            {
                if (PlayerPersistence.instance != null)
                {
                    _instance = PlayerPersistence.instance.GetComponent<KeybindApplier>() ?? 
                                PlayerPersistence.instance.GetComponentInChildren<KeybindApplier>();
                }
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<KeybindApplier>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    void Awake()
    {
        // Se já existe uma instância e esta pertence a um Player cópia que será destruído pelo PlayerPersistence, não sobrescreve
        if (Instance != null && Instance != this)
        {
            PlayerPersistence persistence = GetComponentInParent<PlayerPersistence>();
            if (persistence != null && PlayerPersistence.instance != null && PlayerPersistence.instance != persistence)
            {
                return;
            }
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    void Start()
    {
        // Roda automaticamente assim que o Player nasce na cena
        ApplyKeys();
    }

    /// <summary>
    /// Aplica as configurações salvas em todos os componentes de controle do jogo imediatamente.
    /// </summary>
    public static void ApplyKeysToAll()
    {
        KeybindApplier[] allAppliers = Object.FindObjectsByType<KeybindApplier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allAppliers != null && allAppliers.Length > 0)
        {
            foreach (var applier in allAppliers)
            {
                if (applier != null) applier.ApplyKeys();
            }
        }
        else if (Instance != null)
        {
            Instance.ApplyKeys();
        }

        // Failsafe direto em todos os DashM e PlayerM encontrados na cena (inclusive inativos)
        DashM[] allDashes = Object.FindObjectsByType<DashM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allDashes != null)
        {
            foreach (var d in allDashes)
            {
                if (d != null) d.ReloadKeybinds();
            }
        }

        if (DashM.Instance != null)
        {
            DashM.Instance.ReloadKeybinds();
        }

        PlayerM[] allPlayers = Object.FindObjectsByType<PlayerM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allPlayers != null)
        {
            foreach (var p in allPlayers)
            {
                if (p != null) p.ReloadKeybinds();
            }
        }
    }

    public void ApplyKeys()
    {
        // 1. Entrega as teclas de Movimento e Dash
        DashM dash = GetComponent<DashM>() ?? GetComponentInChildren<DashM>() ?? GetComponentInParent<DashM>();
        if (dash != null)
        {
            dash.keyUp = ParseKey("KeyUp", "W");
            dash.keyDown = ParseKey("KeyDown", "S");
            dash.keyLeft = ParseKey("KeyLeft", "A");
            dash.keyRight = ParseKey("KeyRight", "D");
            dash.dashKey = ParseKey("KeyDash", "E");
        }

        PlayerM pm = GetComponent<PlayerM>() ?? GetComponentInChildren<PlayerM>() ?? GetComponentInParent<PlayerM>();
        if (pm != null)
        {
            pm.keyUp = ParseKey("KeyUp", "W");
            pm.keyDown = ParseKey("KeyDown", "S");
            pm.keyLeft = ParseKey("KeyLeft", "A");
            pm.keyRight = ParseKey("KeyRight", "D");
        }

        // 2. Entrega as teclas de Ação
        PrimaryAttackKnife attack = GetComponentInChildren<PrimaryAttackKnife>() ?? GetComponentInParent<PrimaryAttackKnife>();
        if (attack != null) attack.attackKey = ParseKey("KeyAttack", "Q");

        PlayerUltimate ult = GetComponent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>() ?? GetComponentInParent<PlayerUltimate>();
        if (ult != null) ult.ultimateKey = ParseKey("KeyUlt", "U");

        PlayerInteraction interact = GetComponent<PlayerInteraction>() ?? GetComponentInChildren<PlayerInteraction>() ?? GetComponentInParent<PlayerInteraction>();
        if (interact != null) interact.interactKey = ParseKey("KeyInteract", "F");

        GunAttack gun = GetComponentInChildren<GunAttack>() ?? GetComponentInParent<GunAttack>();
        if (gun != null) gun.reloadKey = ParseKey("KeyReload", "R");

        // Cura / Cápsula do Traje
        SuitHealingSystem suitHeal = GetComponent<SuitHealingSystem>() ?? GetComponentInChildren<SuitHealingSystem>() ?? GetComponentInParent<SuitHealingSystem>();
        if (suitHeal != null) suitHeal.healKey = ParseKey("KeyHeal", "Q");

        // 3. Entrega a tecla para o Inventário
        if (InventoryUI.Instance != null)
        {
            InventoryUI.Instance.toggleKey = ParseKey("KeyInventory", "Tab");
        }
    }

    public static KeyCode ParseKey(string prefsKey, string defaultKey)
    {
        string savedKey = PlayerPrefs.GetString(prefsKey, defaultKey);
        if (System.Enum.TryParse<KeyCode>(savedKey, true, out KeyCode result))
        {
            return result;
        }
        if (System.Enum.TryParse<KeyCode>(defaultKey, true, out KeyCode defaultResult))
        {
            return defaultResult;
        }
        return KeyCode.None;
    }
}
