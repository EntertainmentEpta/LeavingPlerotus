using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Configuração de Navegação")]
    public string playSceneName = "Base";

    [Header("Painéis de UI")]
    public GameObject mainButtonsPanel;
    public GameObject optionsPanel;
    public GameObject creditsPanel;

    private Button btn1;
    private Button btn2;
    private Button btn3;
    private Button btn4;

    private TextMeshProUGUI text1;
    private TextMeshProUGUI text2;
    private TextMeshProUGUI text3;
    private TextMeshProUGUI text4;

    private enum MenuState
    {
        MainMenu,
        SlotSelect,
        SlotAction
    }

    private MenuState currentState = MenuState.MainMenu;
    private int selectedSlot = 1;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;

        FindExistingButtons();
        ShowMainMenuButtons();
    }

    private void Update()
    {
        // Tecla ESC retorna no fluxo de telas
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == MenuState.SlotAction)
            {
                ShowSlotSelect();
            }
            else if (currentState == MenuState.SlotSelect)
            {
                ShowMainMenuButtons();
            }
            else if (optionsPanel != null && optionsPanel.activeSelf)
            {
                ShowMainMenuButtons();
            }
            else if (creditsPanel != null && creditsPanel.activeSelf)
            {
                ShowMainMenuButtons();
            }
        }
    }

    /// <summary>
    /// Localiza e configura exclusivamente os 4 botões nativos já existentes na cena.
    /// NENHUM botão ou painel é criado via script.
    /// </summary>
    private void FindExistingButtons()
    {
        GameObject b1 = GameObject.Find("Btn_Play");
        if (b1 != null)
        {
            btn1 = b1.GetComponent<Button>();
            text1 = b1.GetComponentInChildren<TextMeshProUGUI>();
            SetupTextStyle(text1);
            if (btn1 != null && btn1.onClick.GetPersistentEventCount() == 0)
                btn1.onClick.AddListener(OnPlayClicked);
        }

        GameObject b2 = GameObject.Find("Btn_Opcoes");
        if (b2 != null)
        {
            btn2 = b2.GetComponent<Button>();
            text2 = b2.GetComponentInChildren<TextMeshProUGUI>();
            SetupTextStyle(text2);
            if (btn2 != null && btn2.onClick.GetPersistentEventCount() == 0)
                btn2.onClick.AddListener(OnOptionsClicked);
        }

        GameObject b3 = GameObject.Find("Btn_Creditos");
        if (b3 != null)
        {
            btn3 = b3.GetComponent<Button>();
            text3 = b3.GetComponentInChildren<TextMeshProUGUI>();
            SetupTextStyle(text3);
            if (btn3 != null && btn3.onClick.GetPersistentEventCount() == 0)
                btn3.onClick.AddListener(OnCreditsClicked);
        }

        GameObject b4 = GameObject.Find("Btn_Sair");
        if (b4 != null)
        {
            btn4 = b4.GetComponent<Button>();
            text4 = b4.GetComponentInChildren<TextMeshProUGUI>();
            SetupTextStyle(text4);
            if (btn4 != null && btn4.onClick.GetPersistentEventCount() == 0)
                btn4.onClick.AddListener(OnQuitClicked);
        }
    }

    private void SetupTextStyle(TextMeshProUGUI txt)
    {
        if (txt == null) return;
        txt.raycastTarget = false;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 14f;
        txt.fontSizeMax = 28f;
    }

    // ========================================================
    // TELA 1: MENU PRINCIPAL (JOGAR, OPÇÕES, CRÉDITOS, SAIR)
    // ========================================================

    public void ShowMainMenuButtons()
    {
        currentState = MenuState.MainMenu;
        SetActivePanel(mainButtons: true, options: false, credits: false);

        if (text1 != null) text1.text = "JOGAR";
        if (text2 != null) text2.text = "OPÇÕES";
        if (text3 != null) text3.text = "CRÉDITOS";
        if (text4 != null) text4.text = "SAIR";
    }

    // ========================================================
    // TELA 2: SELEÇÃO DE SLOTS (SLOT 1, SLOT 2, SLOT 3, VOLTAR)
    // ========================================================

    public void ShowSlotSelect()
    {
        currentState = MenuState.SlotSelect;
        SetActivePanel(mainButtons: true, options: false, credits: false);

        UpdateSlotButtonText(text1, 1);
        UpdateSlotButtonText(text2, 2);
        UpdateSlotButtonText(text3, 3);

        if (text4 != null) text4.text = "VOLTAR";
    }

    private void UpdateSlotButtonText(TextMeshProUGUI txt, int slot)
    {
        if (txt == null) return;

        if (SaveManager.SlotExists(slot))
        {
            PersistentSaveData data = SaveManager.GetSlotData(slot);
            string timeStr = data != null ? SaveManager.FormatPlayTime(data.playTimeSeconds) : "0m";
            txt.text = $"SLOT {slot} - {timeStr}";
        }
        else
        {
            txt.text = $"SLOT {slot}: NOVO JOGO";
        }
    }

    private void HandleSlotChosen(int slot)
    {
        if (SaveManager.SlotExists(slot))
        {
            ShowSlotAction(slot);
        }
        else
        {
            StartNewGame(slot);
        }
    }

    // ========================================================
    // TELA 3: AÇÕES DO SLOT (CONTINUAR, NOVO JOGO, DELETAR, VOLTAR)
    // ========================================================

    public void ShowSlotAction(int slot)
    {
        currentState = MenuState.SlotAction;
        selectedSlot = slot;
        SetActivePanel(mainButtons: true, options: false, credits: false);

        if (text1 != null) text1.text = $"CONTINUAR (SLOT {slot})";
        if (text2 != null) text2.text = "NOVO JOGO (DO ZERO)";
        if (text3 != null) text3.text = "DELETAR SAVE 🗑️";
        if (text4 != null) text4.text = "VOLTAR";
    }

    // ========================================================
    // ROTEAMENTO DOS 4 BOTÕES PRINCIPAIS
    // (Chamados diretamente pelos eventos persistentes do Inspector)
    // ========================================================

    public void OnPlayClicked()
    {
        switch (currentState)
        {
            case MenuState.MainMenu:
                ShowSlotSelect();
                break;
            case MenuState.SlotSelect:
                HandleSlotChosen(1);
                break;
            case MenuState.SlotAction:
                ContinueGame(selectedSlot);
                break;
        }
    }

    public void OnOptionsClicked()
    {
        switch (currentState)
        {
            case MenuState.MainMenu:
                SetActivePanel(mainButtons: false, options: true, credits: false);
                break;
            case MenuState.SlotSelect:
                HandleSlotChosen(2);
                break;
            case MenuState.SlotAction:
                StartNewGame(selectedSlot);
                break;
        }
    }

    public void OnCreditsClicked()
    {
        switch (currentState)
        {
            case MenuState.MainMenu:
                SetActivePanel(mainButtons: false, options: false, credits: true);
                break;
            case MenuState.SlotSelect:
                HandleSlotChosen(3);
                break;
            case MenuState.SlotAction:
                DeleteSlotSave(selectedSlot);
                break;
        }
    }

    public void OnQuitClicked()
    {
        switch (currentState)
        {
            case MenuState.MainMenu:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
            case MenuState.SlotSelect:
                ShowMainMenuButtons();
                break;
            case MenuState.SlotAction:
                ShowSlotSelect();
                break;
        }
    }

    // ========================================================
    // AÇÕES DE JOGO / SAVE
    // ========================================================

    private void ContinueGame(int slot)
    {
        SaveManager.LoadGameFromSlot(slot);
    }

    private void StartNewGame(int slot)
    {
        SaveManager.StartNewGameInSlot(slot);
    }

    private void DeleteSlotSave(int slot)
    {
        SaveManager.DeleteSlot(slot);
        ShowSlotSelect();
    }

    // ========================================================
    // UTILITÁRIOS DE PAINEL
    // ========================================================

    public void ShowMainPanel()
    {
        ShowMainMenuButtons();
    }

    private void SetActivePanel(bool mainButtons, bool options, bool credits)
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(mainButtons);
        if (optionsPanel != null) optionsPanel.SetActive(options);
        if (creditsPanel != null) creditsPanel.SetActive(credits);
    }

    // ========================================================
    // CONTROLE DE VOLUME DOS SLIDERS
    // ========================================================

    public void OnBGMVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            GlobalAudioManager.Instance.mainMixer.SetFloat("BGMVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f);
            PlayerPrefs.SetFloat("SavedBGM", value);
            PlayerPrefs.Save();
        }
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            GlobalAudioManager.Instance.mainMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f);
            PlayerPrefs.SetFloat("SavedSFX", value);
            PlayerPrefs.Save();
        }
    }

    public void OnMasterVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            GlobalAudioManager.Instance.mainMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f);
            PlayerPrefs.SetFloat("SavedMaster", value);
            PlayerPrefs.Save();
        }
    }
}