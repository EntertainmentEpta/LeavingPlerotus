using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KeybindManager : MonoBehaviour
{
    [Header("Botões de Movimento")]
    public Button btnUp; public Button btnDown; public Button btnLeft; public Button btnRight;
    public TextMeshProUGUI txtUp; public TextMeshProUGUI txtDown; public TextMeshProUGUI txtLeft; public TextMeshProUGUI txtRight;

    [Header("Botões de Ação")]
    public Button btnAttack; public Button btnDash; public Button btnUlt; 
    public Button btnInteract; public Button btnInventory;
    
    public TextMeshProUGUI txtAttack; public TextMeshProUGUI txtDash; public TextMeshProUGUI txtUlt; 
    public TextMeshProUGUI txtInteract; public TextMeshProUGUI txtInventory;

    private string keyToRebind = "";
    private bool isWaitingForKey = false;

    // Lista de todas as chaves salvas para podermos procurar duplicatas
    private string[] allPrefsKeys = { 
        "KeyUp", "KeyDown", "KeyLeft", "KeyRight", 
        "KeyAttack", "KeyDash", "KeyUlt", "KeyInteract", "KeyInventory" 
    };

    void Start()
    {
        // 1. Carrega todas as teclas ao iniciar
        UpdateAllButtonTexts();

        // 2. Avisa aos botões o que devem fazer
        if (btnUp != null) btnUp.onClick.AddListener(() => StartRebind("KeyUp", txtUp));
        if (btnDown != null) btnDown.onClick.AddListener(() => StartRebind("KeyDown", txtDown));
        if (btnLeft != null) btnLeft.onClick.AddListener(() => StartRebind("KeyLeft", txtLeft));
        if (btnRight != null) btnRight.onClick.AddListener(() => StartRebind("KeyRight", txtRight));
        
        if (btnAttack != null) btnAttack.onClick.AddListener(() => StartRebind("KeyAttack", txtAttack));
        if (btnDash != null) btnDash.onClick.AddListener(() => StartRebind("KeyDash", txtDash));
        if (btnUlt != null) btnUlt.onClick.AddListener(() => StartRebind("KeyUlt", txtUlt));
        if (btnInteract != null) btnInteract.onClick.AddListener(() => StartRebind("KeyInteract", txtInteract));
        if (btnInventory != null) btnInventory.onClick.AddListener(() => StartRebind("KeyInventory", txtInventory));
    }

    // Centralizamos a atualização visual para rodar toda vez que uma tecla mudar
    public void UpdateAllButtonTexts()
    {
        if (txtUp != null) txtUp.text = "Cima: " + PlayerPrefs.GetString("KeyUp", "W");
        if (txtDown != null) txtDown.text = "Baixo: " + PlayerPrefs.GetString("KeyDown", "S");
        if (txtLeft != null) txtLeft.text = "Esquerda: " + PlayerPrefs.GetString("KeyLeft", "A");
        if (txtRight != null) txtRight.text = "Direita: " + PlayerPrefs.GetString("KeyRight", "D");

        if (txtAttack != null) txtAttack.text = "Ataque: " + PlayerPrefs.GetString("KeyAttack", "Q");
        if (txtDash != null) txtDash.text = "Dash: " + PlayerPrefs.GetString("KeyDash", "E");
        if (txtUlt != null) txtUlt.text = "Ultimate: " + PlayerPrefs.GetString("KeyUlt", "U");
        if (txtInteract != null) txtInteract.text = "Interagir: " + PlayerPrefs.GetString("KeyInteract", "F");
        if (txtInventory != null) txtInventory.text = "Inventário: " + PlayerPrefs.GetString("KeyInventory", "Tab");
    }

    public void StartRebind(string prefsKey, TextMeshProUGUI buttonText)
    {
        if (isWaitingForKey || buttonText == null) return;

        keyToRebind = prefsKey;
        string nomeAcao = buttonText.text.Split(':')[0];
        buttonText.text = nomeAcao + ": [ _ ]"; 
        
        StartCoroutine(WaitForKeyPress(buttonText, nomeAcao));
    }

    private IEnumerator WaitForKeyPress(TextMeshProUGUI buttonText, string nomeAcao)
    {
        isWaitingForKey = true;
        yield return null;

        while (isWaitingForKey)
        {
            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(keyCode))
                {
                    // Se o cara apertar ESC, cancela
                    if (keyCode == KeyCode.Escape || keyCode == KeyCode.Mouse0)
                    {
                        UpdateAllButtonTexts();
                        isWaitingForKey = false;
                        break;
                    }

                    string novaTecla = keyCode.ToString();
                    string teclaAntiga = PlayerPrefs.GetString(keyToRebind, GetDefaultKey(keyToRebind));

                    // === SISTEMA ANTI-DUPLICAÇÃO (SWAP) ===
                    foreach (string k in allPrefsKeys)
                    {
                        // Varre todas as teclas. Se não for a tecla que estou mexendo agora...
                        if (k != keyToRebind)
                        {
                            // E se a tecla gravada lá for igual à que acabei de apertar...
                            if (PlayerPrefs.GetString(k, GetDefaultKey(k)) == novaTecla)
                            {
                                // Passa a tecla antiga para a ação que perdeu a tecla nova
                                PlayerPrefs.SetString(k, teclaAntiga);
                            }
                        }
                    }

                    // Salva a nova tecla na ação desejada!
                    PlayerPrefs.SetString(keyToRebind, novaTecla);
                    PlayerPrefs.Save();

                    // Atualiza TODOS os botões da tela na mesma hora
                    UpdateAllButtonTexts();

                    // === A MÁGICA ACONTECE AQUI ===
                    // Usa a linha direta com o Player para aplicar as teclas instantaneamente!
                    if (KeybindApplier.Instance != null)
                    {
                        KeybindApplier.Instance.ApplyKeys();
                        Debug.Log("Teclas atualizadas no Player com sucesso!");
                    }
                    else
                    {
                        Debug.LogWarning("O Player não foi encontrado! A tecla foi salva e será aplicada quando ele nascer.");
                    }

                    isWaitingForKey = false;
                    break;
                }
            }
            yield return null;
        }
    }

    private string GetDefaultKey(string prefsKey)
    {
        switch(prefsKey)
        {
            case "KeyUp": return "W"; case "KeyDown": return "S"; case "KeyLeft": return "A"; case "KeyRight": return "D";
            case "KeyAttack": return "Q"; case "KeyDash": return "E"; case "KeyUlt": return "U"; 
            case "KeyInteract": return "F"; case "KeyInventory": return "Tab"; default: return "None";
        }
    }
}