using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("Telas de UI")]
    public GameObject pausePanel;
    public GameObject optionsPanel;

    [Header("Configuração")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Sliders de Áudio")]
    public Slider sliderBGM;
    public Slider sliderSFX;
    public Slider sliderMaster;

    [Header("Modo de Mira")]
    public Button btnAimMode;
    public TMPro.TextMeshProUGUI txtAimMode;

    private bool isPaused = false;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false); 

        // === CORREÇÃO VISUAL DOS SLIDERS ===
        if (sliderBGM != null) sliderBGM.value = PlayerPrefs.GetFloat("SavedBGM", 0.75f);
        if (sliderSFX != null) sliderSFX.value = PlayerPrefs.GetFloat("SavedSFX", 0.75f);
        if (sliderMaster != null) sliderMaster.value = PlayerPrefs.GetFloat("SavedMaster", 1f);

        if (btnAimMode != null) btnAimMode.onClick.AddListener(ToggleAimMode);
        UpdateAimModeUI();
    }

    void Update()
    {
        // === TRAVA DO ESC ===
        // Se a tela de Morte ou Vitória estiver ativa, aborta o Update e ignora o ESC
        if (IsGameOverScreenActive()) return;

        // === TESTE RÁPIDO DE CÂMERA (F4) ===
        if (Input.GetKeyDown(KeyCode.F4))
        {
            ToggleIsometricMode(!isIsometricActive);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (optionsPanel != null && optionsPanel.activeSelf)
            {
                CloseOptions();
            }
            else
            {
                if (isPaused) ResumeGame();
                else PauseGame();
            }
        }
    }

    // ==========================================
    // SISTEMA DE CÂMERA ISOMÉTRICA (DUAS CÂMERAS) E SLIDER
    // ==========================================
    [Header("Configuração de Câmeras")]
    public GameObject mainCameraObj;
    public GameObject isometricCameraObj;
    
    [Header("UI do Slider de Câmera")]
    public Slider cameraSlider;

    private bool isIsometricActive = false;

    // Nova funo para o Slider
    public void OnCameraSliderChanged(float value)
    {
        bool wantIsometric = (value > 0.5f);
        ToggleIsometricMode(wantIsometric);

        // Opcional: Travar o slider no 0 ou 1 para ele agir como um interruptor (On/Off)
        if (cameraSlider != null)
        {
            cameraSlider.value = wantIsometric ? 1f : 0f;
        }
    }

    public void ToggleIsometricMode(bool isIsometric)
    {
        if (mainCameraObj == null || isometricCameraObj == null)
        {
            Debug.LogWarning("[Câmera] Você esqueceu de arrastar as câmeras para o PauseMenuController no Inspector!");
            return;
        }

        if (isIsometric && !isIsometricActive)
        {
            mainCameraObj.SetActive(false);
            isometricCameraObj.SetActive(true);
            isIsometricActive = true;
            Debug.Log("[Câmera] Ligou a Câmera Isométrica!");
        }
        else if (!isIsometric && isIsometricActive)
        {
            isometricCameraObj.SetActive(false);
            mainCameraObj.SetActive(true);
            isIsometricActive = false;
            Debug.Log("[Câmera] Ligou a Câmera Normal!");
        }
    }

    // Função que verifica se o jogo já acabou (Morte ou Vitória)
    private bool IsGameOverScreenActive()
    {
        // Checa a tela de morte
        if (DeathScreenUI.Instance != null && 
            DeathScreenUI.Instance.deathPanel != null && 
            DeathScreenUI.Instance.deathPanel.activeSelf)
        {
            return true;
        }

        // Checa a tela de vitória
        if (VictoryScreenUI.Instance != null && 
            VictoryScreenUI.Instance.victoryPanel != null && 
            VictoryScreenUI.Instance.victoryPanel.activeSelf)
        {
            return true;
        }

        return false;
    }

    public void PauseGame()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        GamePause.Acquire(this);
        isPaused = true;
        
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        
        GamePause.Release(this);
        isPaused = false;
    }

    public void OpenOptions()
    {
        pausePanel.SetActive(false);
        optionsPanel.SetActive(true); 
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
        pausePanel.SetActive(true); 
    }

    public void ReturnToMainMenu()
    {
        GamePause.ResetAll();
        GameManager.EnsureInstance().LoadSceneWithLoading(mainMenuSceneName, false);
    }

    private void OnDestroy() => GamePause.Release(this);

    // ==========================================
    // CONTROLES DE ÁUDIO (COM HARD MUTE)
    // ==========================================
    public void OnBGMVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.SetBGMVolume(value);
        }
        if (MusicManager.instance != null)
        {
            MusicManager.instance.SetVolume(value);
        }
        if (MenuAudioFX.Instance != null)
        {
            MenuAudioFX.Instance.SetMusicVolume(value);
        }
        PlayerPrefs.SetFloat("SavedBGM", value);
        PlayerPrefs.Save();
    }

    public void OnSFXVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.SetSFXVolume(value);
            GlobalAudioManager.Instance.PlayPreviewSFX();
        }
        if (MenuAudioFX.Instance != null)
        {
            MenuAudioFX.Instance.SetSFXVolume(value);
        }
        PlayerPrefs.SetFloat("SavedSFX", value);
        PlayerPrefs.Save();
    }

    public void OnMasterVolumeChanged(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.SetMasterVolume(value);
        }
        PlayerPrefs.SetFloat("SavedMaster", value);
        PlayerPrefs.Save();
    }

    // ==========================================
    // CONTROLES DE MIRA (MOUSE VS WASD)
    // ==========================================
    public void ToggleAimMode()
    {
        AimSettings.ToggleMode();
        UpdateAimModeUI();
    }

    public void SetAimModeMouse()
    {
        AimSettings.CurrentMode = AimMode.MouseCursor;
        UpdateAimModeUI();
    }

    public void SetAimModePlayerDirection()
    {
        AimSettings.CurrentMode = AimMode.PlayerDirection;
        UpdateAimModeUI();
    }

    private void UpdateAimModeUI()
    {
        if (txtAimMode != null)
        {
            txtAimMode.text = AimSettings.GetModeDisplayName();
        }
    }
}