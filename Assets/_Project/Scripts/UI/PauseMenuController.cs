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

    private bool isPaused = false;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        Time.timeScale = 1f; 

        // === CORREÇÃO VISUAL DOS SLIDERS ===
        if (sliderBGM != null) sliderBGM.value = PlayerPrefs.GetFloat("SavedBGM", 0.75f);
        if (sliderSFX != null) sliderSFX.value = PlayerPrefs.GetFloat("SavedSFX", 0.75f);
        if (sliderMaster != null) sliderMaster.value = PlayerPrefs.GetFloat("SavedMaster", 1f);
    }

    void Update()
    {
        // === TRAVA DO ESC ===
        // Se a tela de Morte ou Vitória estiver ativa, aborta o Update e ignora o ESC
        if (IsGameOverScreenActive()) return;

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
        pausePanel.SetActive(true);
        Time.timeScale = 0f; 
        isPaused = true;
        
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        pausePanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        
        Time.timeScale = 1f; 
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
        Time.timeScale = 1f; 
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ==========================================
    // CONTROLES DE ÁUDIO (COM HARD MUTE)
    // ==========================================
    public void OnBGMVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            float volume = (value <= 0.001f) ? -80f : Mathf.Log10(value) * 20f;
            GlobalAudioManager.Instance.mainMixer.SetFloat("BGMVolume", volume);
            PlayerPrefs.SetFloat("SavedBGM", value);
            PlayerPrefs.Save();
        }
    }

    public void OnSFXVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            float volume = (value <= 0.001f) ? -80f : Mathf.Log10(value) * 20f;
            GlobalAudioManager.Instance.mainMixer.SetFloat("SFXVolume", volume);
            PlayerPrefs.SetFloat("SavedSFX", value);
            PlayerPrefs.Save();
        }
    }

    public void OnMasterVolumeChanged(float value)
    {
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            float volume = (value <= 0.001f) ? -80f : Mathf.Log10(value) * 20f;
            GlobalAudioManager.Instance.mainMixer.SetFloat("MasterVolume", volume);
            PlayerPrefs.SetFloat("SavedMaster", value);
            PlayerPrefs.Save();
        }
    }
}