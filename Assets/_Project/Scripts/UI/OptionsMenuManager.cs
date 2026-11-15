using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Christina.UI; // Isso faz a Unity reconhecer as suas chavinhas animadas!

public class OptionsMenuManager : MonoBehaviour
{
    [Header("Abas de Opções")]
    public GameObject abaGeral;
    public GameObject abaControles;

    [Header("Sliders de Áudio")]
    public Slider masterSlider;
    public Slider sfxSlider;
    public Slider bgmSlider;

    [Header("Vídeo & UI")]
    public TMP_Dropdown resolutionDropdown;
    
    // As variáveis agora pedem a sua nova chavinha
    public ToggleSwitch fullscreenToggle;
    public ToggleSwitch vsyncToggle; 

    [Header("Mira / Disparo")]
    public ToggleSwitch aimModeToggle;
    public Button btnAimMode;
    public TextMeshProUGUI txtAimMode;

    private Resolution[] resolutions;

    void Start()
    {
        ConfigurarAudio();
        ConfigurarVideo();
        ConfigurarModoDeMira();
        
        // Garante que o menu sempre comece mostrando a aba Geral
        MostrarAbaGeral(); 
    }

    void ConfigurarAudio()
    {
        float savedMaster = PlayerPrefs.GetFloat("SavedMaster", AudioListener.volume > 0.001f ? AudioListener.volume : 0.8f);
        float savedBGM = PlayerPrefs.GetFloat("SavedBGM", 0.75f);
        float savedSFX = PlayerPrefs.GetFloat("SavedSFX", 0.75f);

        if (masterSlider != null)
        {
            masterSlider.value = savedMaster;
            masterSlider.onValueChanged.RemoveAllListeners();
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (bgmSlider != null)
        {
            bgmSlider.value = savedBGM;
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = savedSFX;
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        // Aplica os volumes carregados imediatamente
        SetMasterVolume(savedMaster);
        SetMusicVolume(savedBGM);
        SetSFXVolume(savedSFX);
    }

    public void SetMasterVolume(float value)
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

    public void SetMusicVolume(float value)
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

    public void SetSFXVolume(float value)
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

    void ConfigurarVideo()
    {
        if (fullscreenToggle != null)
        {
            fullscreenToggle.ToggleByGroupManager(Screen.fullScreen);
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.ToggleByGroupManager(QualitySettings.vSyncCount > 0);
        }

        if (resolutionDropdown != null)
        {
            resolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            
            int currentRes = 0;
            var options = new System.Collections.Generic.List<string>();
            
            for (int i = 0; i < resolutions.Length; i++)
            {
                options.Add($"{resolutions[i].width} x {resolutions[i].height}");
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                    currentRes = i;
            }
            
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentRes;
            resolutionDropdown.RefreshShownValue();
            
            // OLHA O DEBUG DA RESOLUÇÃO AQUI! 👇
            resolutionDropdown.onValueChanged.AddListener((i) => {
                Screen.SetResolution(resolutions[i].width, resolutions[i].height, Screen.fullScreen);
                Debug.Log("Comando enviado: RESOLUÇÃO ALTERADA PARA " + resolutions[i].width + " x " + resolutions[i].height);
            });
        }
    }

    // ==========================================
    // SISTEMA DE ABAS (TABS)
    // ==========================================

    public void MostrarAbaGeral()
    {
        if (abaGeral != null) abaGeral.SetActive(true);
        if (abaControles != null) abaControles.SetActive(false);
    }

    public void MostrarAbaControles()
    {
        if (abaGeral != null) abaGeral.SetActive(false);
        if (abaControles != null) abaControles.SetActive(true);
    }

    // ==========================================
    // FUNÇÕES PARA O SEU TOGGLE SWITCH
    // ==========================================

    public void AtivarTelaCheia() 
    { 
        Screen.fullScreen = true; 
        Debug.Log("Comando enviado: TELA CHEIA ATIVADA"); 
    }
    
    public void DesativarTelaCheia() 
    { 
        Screen.fullScreen = false; 
        Debug.Log("Comando enviado: MODO JANELA"); 
    }

    public void AtivarVSync() 
    { 
        QualitySettings.vSyncCount = 1; 
        Debug.Log("Comando enviado: VSYNC LIGADO"); 
    }
    
    public void DesativarVSync() 
    { 
        QualitySettings.vSyncCount = 0; 
        Debug.Log("Comando enviado: VSYNC DESLIGADO"); 
    }

    // ==========================================
    // FUNÇÕES PARA A CÂMERA ISOMÉTRICA (VIA TOGGLE SWITCH)
    // ==========================================
    [Header("Sistema de Câmeras (Toggle)")]
    [Tooltip("Arraste as câmeras aqui para o toggle poder ligar/desligar elas")]
    public GameObject mainCameraObj;
    public GameObject isometricCameraObj;

    public void AtivarCameraIsometrica()
    {
        if (mainCameraObj != null) mainCameraObj.SetActive(false);
        if (isometricCameraObj != null) isometricCameraObj.SetActive(true);
        Debug.Log("Comando enviado: CÂMERA ISOMÉTRICA LIGADA");
    }

    public void DesativarCameraIsometrica()
    {
        if (isometricCameraObj != null) isometricCameraObj.SetActive(false);
        if (mainCameraObj != null) mainCameraObj.SetActive(true);
        Debug.Log("Comando enviado: CÂMERA NORMAL LIGADA");
    }

    // ==========================================
    // FUNÇÕES PARA O MODO DE MIRA (MOUSE VS WASD)
    // ==========================================
    void ConfigurarModoDeMira()
    {
        if (aimModeToggle != null)
        {
            aimModeToggle.ToggleByGroupManager(AimSettings.IsMouseAim);
        }

        if (btnAimMode != null)
        {
            btnAimMode.onClick.AddListener(AlternarModoMira);
        }

        AtualizarTextoMira();
    }

    public void AtivarMiraMouse()
    {
        AimSettings.CurrentMode = AimMode.MouseCursor;
        AtualizarTextoMira();
        Debug.Log("Comando enviado: MIRA NO MOUSE ATIVADA");
    }

    public void AtivarMiraDirecaoPlayer()
    {
        AimSettings.CurrentMode = AimMode.PlayerDirection;
        AtualizarTextoMira();
        Debug.Log("Comando enviado: MIRA NA DIREÇÃO DO PLAYER ATIVADA");
    }

    public void AlternarModoMira()
    {
        AimSettings.ToggleMode();
        if (aimModeToggle != null)
        {
            aimModeToggle.ToggleByGroupManager(AimSettings.IsMouseAim);
        }
        AtualizarTextoMira();
    }

    private void AtualizarTextoMira()
    {
        if (txtAimMode != null)
        {
            txtAimMode.text = AimSettings.GetModeDisplayName();
        }
    }
}