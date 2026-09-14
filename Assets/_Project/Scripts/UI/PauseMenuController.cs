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
    // SISTEMA DE CÂMERA ISOMÉTRICA (ESTILO HADES)
    // ==========================================
    private Camera mainCam;
    private Vector3 originalCamPos;
    private Quaternion originalCamRot;
    private bool originalOrthographic;
    private float originalOrthoSize;
    private bool isIsometricActive = false;

    public void ToggleIsometricMode(bool isIsometric)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        if (isIsometric && !isIsometricActive)
        {
            // Salvar estado atual da cmera do jogo
            originalCamPos = mainCam.transform.position;
            originalCamRot = mainCam.transform.rotation;
            originalOrthographic = mainCam.orthographic;
            originalOrthoSize = mainCam.orthographicSize;

            // Forar o modo Isomtrico
            mainCam.orthographic = true;
            mainCam.orthographicSize = 8f;
            mainCam.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            
            // Tentar focar no jogador
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 targetPos = player != null ? player.transform.position : Vector3.zero;
            mainCam.transform.position = targetPos + new Vector3(-15f, 15f, -15f);

            isIsometricActive = true;
            Debug.Log("[Câmera] Modo Isométrico 2.5D ATIVADO!");
        }
        else if (!isIsometric && isIsometricActive)
        {
            // Restaurar cmera original (3a pessoa, etc)
            mainCam.orthographic = originalOrthographic;
            mainCam.orthographicSize = originalOrthoSize;
            mainCam.transform.position = originalCamPos;
            mainCam.transform.rotation = originalCamRot;

            isIsometricActive = false;
            Debug.Log("[Câmera] Câmera Normal RESTAURADA!");
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