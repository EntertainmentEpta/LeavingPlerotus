using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Configuração de Navegação")]
    public string playSceneName = "Base";

    [Header("Painéis de UI")]
    public GameObject mainButtonsPanel;
    public GameObject optionsPanel;
    public GameObject creditsPanel;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;

        ShowMainPanel();
    }

    public void OnPlayClicked()
    {
        Time.timeScale = 1f;
        PlayerHealth.shouldPlayWakeUpAnimation = true;
        SceneManager.LoadScene(playSceneName);
    }

    public void OnOptionsClicked()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void OnCreditsClicked()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowMainPanel()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    // ==========================================
    // CONTROLE DE VOLUME DOS SLIDERS
    // ==========================================

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