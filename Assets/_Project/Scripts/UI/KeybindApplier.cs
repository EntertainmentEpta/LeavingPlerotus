using UnityEngine;

public class KeybindApplier : MonoBehaviour
{
    // A linha direta de comunicação!
    public static KeybindApplier Instance { get; private set; }

    void Awake()
    {
        // Assim que o Player nascer, ele avisa: "Eu sou o Applier oficial desta cena!"
        Instance = this;
    }

    void Start()
    {
        // Roda automaticamente assim que o Player nasce na cena
        ApplyKeys();
    }

    public void ApplyKeys()
    {
        // 1. Entrega as teclas de Movimento e Dash
        DashM dash = GetComponent<DashM>();
        if (dash != null)
        {
            dash.keyUp = ParseKey("KeyUp", "W");
            dash.keyDown = ParseKey("KeyDown", "S");
            dash.keyLeft = ParseKey("KeyLeft", "A");
            dash.keyRight = ParseKey("KeyRight", "D");
            dash.dashKey = ParseKey("KeyDash", "E");
        }

        PlayerM pm = GetComponent<PlayerM>();
        if (pm != null)
        {
            pm.keyUp = ParseKey("KeyUp", "W");
            pm.keyDown = ParseKey("KeyDown", "S");
            pm.keyLeft = ParseKey("KeyLeft", "A");
            pm.keyRight = ParseKey("KeyRight", "D");
        }

        // 2. Entrega as teclas de Ação
        PrimaryAttackKnife attack = GetComponentInChildren<PrimaryAttackKnife>();
        if (attack != null) attack.attackKey = ParseKey("KeyAttack", "Q");

        PlayerUltimate ult = GetComponent<PlayerUltimate>();
        if (ult != null) ult.ultimateKey = ParseKey("KeyUlt", "U");

        PlayerInteraction interact = GetComponent<PlayerInteraction>();
        if (interact != null) interact.interactKey = ParseKey("KeyInteract", "F");

        // 3. Entrega a tecla para o Inventário
        if (InventoryUI.Instance != null)
        {
            InventoryUI.Instance.toggleKey = ParseKey("KeyInventory", "Tab");
        }
    }

    private KeyCode ParseKey(string prefsKey, string defaultKey)
    {
        string savedKey = PlayerPrefs.GetString(prefsKey, defaultKey);
        return (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey);
    }
}