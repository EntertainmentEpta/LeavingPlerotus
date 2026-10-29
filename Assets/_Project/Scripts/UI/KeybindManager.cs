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
    public Button btnInteract; public Button btnInventory; public Button btnReload; public Button btnHeal;
    
    public TextMeshProUGUI txtAttack; public TextMeshProUGUI txtDash; public TextMeshProUGUI txtUlt; 
    public TextMeshProUGUI txtInteract; public TextMeshProUGUI txtInventory; public TextMeshProUGUI txtReload; public TextMeshProUGUI txtHeal;

    [Header("Modo de Mira")]
    public Button btnAimMode;
    public TextMeshProUGUI txtAimMode;

    private string keyToRebind = "";
    private bool isWaitingForKey = false;

    // Lista de todas as chaves salvas para podermos procurar duplicatas
    private string[] allPrefsKeys = { 
        "KeyUp", "KeyDown", "KeyLeft", "KeyRight", 
        "KeyAttack", "KeyDash", "KeyUlt", "KeyInteract", "KeyInventory", "KeyReload", "KeyHeal" 
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
        // 1. Garante que o botão de Modo de Mira existe e está configurado
        EnsureAimModeButton();
        EnsureReloadButton();
        EnsureHealButton();

        // 2. Carrega todas as teclas ao iniciar
        UpdateAllButtonTexts();

        // 3. Avisa aos botões o que devem fazer
        if (btnUp != null) btnUp.onClick.AddListener(() => StartRebind("KeyUp", txtUp));
        if (btnDown != null) btnDown.onClick.AddListener(() => StartRebind("KeyDown", txtDown));
        if (btnLeft != null) btnLeft.onClick.AddListener(() => StartRebind("KeyLeft", txtLeft));
        if (btnRight != null) btnRight.onClick.AddListener(() => StartRebind("KeyRight", txtRight));
        
        if (btnAttack != null) btnAttack.onClick.AddListener(() => StartRebind("KeyAttack", txtAttack));
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

    
    private void EnsureHealButton()
    {
        if (btnHeal != null) return;

        Transform found = transform.Find("Btn_Curar") ?? transform.Find("Btn_RecuperarVida") ?? transform.Find("Btn_Heal");
        if (found != null)
        {
            btnHeal = found.GetComponent<Button>();
            txtHeal = found.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }
    }

    private void EnsureReloadButton()
    {
        if (btnReload != null) return;

        Transform found = transform.Find("Btn_Reload") ?? transform.Find("Btn_Recarregar");
        if (found != null)
        {
            btnReload = found.GetComponent<Button>();
            txtReload = found.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        Button template = btnInventory != null ? btnInventory : (btnInteract != null ? btnInteract : btnAttack);
        if (template != null)
        {
            GameObject cloned = Instantiate(template.gameObject, template.transform.parent);
            cloned.name = "Btn_Reload";
            btnReload = cloned.GetComponent<Button>();
            txtReload = cloned.GetComponentInChildren<TextMeshProUGUI>();

            btnReload.onClick.RemoveAllListeners();
            btnReload.onClick.AddListener(() => StartRebind("KeyReload", txtReload));

            RectTransform rect = cloned.GetComponent<RectTransform>();
            RectTransform templateRect = template.GetComponent<RectTransform>();
            if (rect != null && templateRect != null)
            {
                rect.anchoredPosition = new Vector2(templateRect.anchoredPosition.x, templateRect.anchoredPosition.y - 45f);
            }
        }
    }

    private void EnsureAimModeButton()
    {
        if (btnAimMode != null) return;

        Transform found = transform.Find("Btn_AimMode") ?? transform.Find("Btn_Mira");
        if (found != null)
        {
            btnAimMode = found.GetComponent<Button>();
            txtAimMode = found.GetComponentInChildren<TextMeshProUGUI>();
            RectTransform foundRect = found.GetComponent<RectTransform>();
            if (foundRect != null)
            {
                foundRect.anchoredPosition = new Vector2(240f, 33f);
            }
            return;
        }

        Button template = btnAttack != null ? btnAttack : (btnDash != null ? btnDash : GetComponentInChildren<Button>());
        if (template != null)
        {
            GameObject cloned = Instantiate(template.gameObject, template.transform.parent);
            cloned.name = "Btn_AimMode";
            btnAimMode = cloned.GetComponent<Button>();
            txtAimMode = cloned.GetComponentInChildren<TextMeshProUGUI>();

            btnAimMode.onClick.RemoveAllListeners();

            RectTransform rect = cloned.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(240f, 33f);
            }
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
            case "KeyInteract": return "F"; case "KeyInventory": return "Tab"; case "KeyHeal": return "Q"; default: return "None";
        }
    }
}