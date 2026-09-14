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

    private Resolution[] resolutions;

    void Start()
    {
        ConfigurarAudio();
        ConfigurarVideo();
        
        // Garante que o menu sempre comece mostrando a aba Geral
        MostrarAbaGeral(); 
    }

    void ConfigurarAudio()
    {
        if (masterSlider != null)
        {
            masterSlider.value = AudioListener.volume;
            masterSlider.onValueChanged.AddListener((v) => AudioListener.volume = v);
        }

        if (MenuAudioFX.Instance != null)
        {
            if (sfxSlider != null)
            {
                sfxSlider.value = MenuAudioFX.Instance.sfxVolume;
                sfxSlider.onValueChanged.AddListener((v) => MenuAudioFX.Instance.sfxVolume = v);
            }

            if (bgmSlider != null)
            {
                bgmSlider.value = MenuAudioFX.Instance.musicVolume;
                bgmSlider.onValueChanged.AddListener((v) => MenuAudioFX.Instance.musicVolume = v);
            }
        }
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
}