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
    public Button btnAttack; public Button btnHeavyAttack; public Button btnDash; public Button btnUlt; 
    public Button btnInteract; public Button btnInventory; public Button btnReload; public Button btnHeal;
    
    public TextMeshProUGUI txtAttack; public TextMeshProUGUI txtHeavyAttack; public TextMeshProUGUI txtDash; public TextMeshProUGUI txtUlt; 
    public TextMeshProUGUI txtInteract; public TextMeshProUGUI txtInventory; public TextMeshProUGUI txtReload; public TextMeshProUGUI txtHeal;

    [Header("Modo de Mira")]
    public Button btnAimMode;
    public TextMeshProUGUI txtAimMode;

    private string keyToRebind = "";
    private bool isWaitingForKey = false;

    // Lista de todas as chaves salvas para podermos procurar duplicatas
    private string[] allPrefsKeys = { 
        "KeyUp", "KeyDown", "KeyLeft", "KeyRight", 
        "KeyAttack", "KeyHeavyAttack", "KeyDash", "KeyUlt", "KeyInteract", "KeyInventory", "KeyReload", "KeyHeal" 
    };

    private void OnEnable()
    {
        AimSettings.OnAimModeChanged += HandleAimModeChanged;
        UpdateAllButtonTexts();
    }

    private void OnDisable()
    {
        AimSettings.OnAimModeChanged -= HandleAimModeChanged;
    }

    private void HandleAimModeChanged(AimMode mode)
    {
        UpdateAllButtonTexts();
    }

    void Start()
    {
        // 1. Garante referências caso não tenham sido conectadas no Inspector (sem instanciar nem alterar posições)
        EnsureButtonReferences();

        // 2. Carrega todas as teclas ao iniciar
        UpdateAllButtonTexts();

        // 3. Avisa aos botões o que devem fazer
        if (btnUp != null) btnUp.onClick.AddListener(() => StartRebind("KeyUp", txtUp));
        if (btnDown != null) btnDown.onClick.AddListener(() => StartRebind("KeyDown", txtDown));
        if (btnLeft != null) btnLeft.onClick.AddListener(() => StartRebind("KeyLeft", txtLeft));
        if (btnRight != null) btnRight.onClick.AddListener(() => StartRebind("KeyRight", txtRight));
        
        if (btnAttack != null) btnAttack.onClick.AddListener(() => StartRebind("KeyAttack", txtAttack));
        if (btnHeavyAttack != null) btnHeavyAttack.gameObject.SetActive(false);
        if (btnDash != null) btnDash.onClick.AddListener(() => StartRebind("KeyDash", txtDash));
        if (btnUlt != null) btnUlt.onClick.AddListener(() => StartRebind("KeyUlt", txtUlt));
        if (btnInteract != null) btnInteract.onClick.AddListener(() => StartRebind("KeyInteract", txtInteract));
        if (btnInventory != null) btnInventory.onClick.AddListener(() => StartRebind("KeyInventory", txtInventory));
        if (btnReload != null) btnReload.onClick.AddListener(() => StartRebind("KeyReload", txtReload));
        if (btnHeal != null) btnHeal.onClick.AddListener(() => StartRebind("KeyHeal", txtHeal));

        if (btnAimMode != null) btnAimMode.onClick.AddListener(OnAimModeButtonClicked);
    }

    // Centralizamos a atualização visual para rodar toda vez que uma tecla mudar
    public void UpdateAllButtonTexts()
    {
        if (txtUp != null) txtUp.text = "Cima: " + PlayerPrefs.GetString("KeyUp", "W");
        if (txtDown != null) txtDown.text = "Baixo: " + PlayerPrefs.GetString("KeyDown", "S");
        if (txtLeft != null) txtLeft.text = "Esquerda: " + PlayerPrefs.GetString("KeyLeft", "A");
        if (txtRight != null) txtRight.text = "Direita: " + PlayerPrefs.GetString("KeyRight", "D");

        if (txtAttack != null) txtAttack.text = "Ataque: " + PlayerPrefs.GetString("KeyAttack", "Q");
        if (txtHeavyAttack != null) txtHeavyAttack.text = "Ataque Pesado: " + PlayerPrefs.GetString("KeyHeavyAttack", "Mouse1");
        if (txtDash != null) txtDash.text = "Dash: " + PlayerPrefs.GetString("KeyDash", "E");
        if (txtUlt != null) txtUlt.text = "Ultimate: " + PlayerPrefs.GetString("KeyUlt", "U");
        if (txtInteract != null) txtInteract.text = "Interagir: " + PlayerPrefs.GetString("KeyInteract", "F");
        if (txtInventory != null) txtInventory.text = "Inventário: " + PlayerPrefs.GetString("KeyInventory", "Tab");
        if (txtReload != null) txtReload.text = "Recarregar: " + PlayerPrefs.GetString("KeyReload", "R");
        if (txtHeal != null) txtHeal.text = "Recuperar Vida: " + PlayerPrefs.GetString("KeyHeal", "Q");

        if (txtAimMode != null) txtAimMode.text = AimSettings.GetModeDisplayName();
    }

    public void OnAimModeButtonClicked()
    {
        AimSettings.ToggleMode();
        UpdateAllButtonTexts();
        Debug.Log($"[KeybindManager] Modo de mira alternado para: {AimSettings.CurrentMode}");
    }

    private void EnsureButtonReferences()
    {
        if (btnHeal == null)
        {
            Transform found = transform.Find("Btn_Curar") ?? transform.Find("Btn_RecuperarVida") ?? transform.Find("Btn_Heal");
            if (found != null)
            {
                btnHeal = found.GetComponent<Button>();
                txtHeal = found.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (btnReload == null)
        {
            Transform found = transform.Find("Btn_Reload") ?? transform.Find("Btn_Recarregar");
            if (found != null)
            {
                btnReload = found.GetComponent<Button>();
                txtReload = found.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (btnAimMode == null)
        {
            Transform found = transform.Find("Btn_AimMode") ?? transform.Find("Btn_Mira");
            if (found != null)
            {
                btnAimMode = found.GetComponent<Button>();
                txtAimMode = found.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        if (btnHeavyAttack == null)
        {
            Transform found = transform.Find("Btn_AtaquePesado") ?? transform.Find("Btn_HeavyAttack");
            if (found != null)
            {
                btnHeavyAttack = found.GetComponent<Button>();
                txtHeavyAttack = found.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        if (btnHeavyAttack != null)
        {
            btnHeavyAttack.gameObject.SetActive(false);
        }
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
                    // Se o cara apertar ESC ou clicar com Mouse0 no texto para cancelar
                    if (keyCode == KeyCode.Escape)
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
            case "KeyAttack": return "Q"; case "KeyHeavyAttack": return "Mouse1"; case "KeyDash": return "E"; case "KeyUlt": return "U"; 
            case "KeyInteract": return "F"; case "KeyInventory": return "Tab"; case "KeyReload": return "R"; case "KeyHeal": return "Q"; default: return "None";
        }
    }
}
