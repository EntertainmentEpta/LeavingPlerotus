using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using Christina.UI; // Isso faz a Unity reconhecer as suas chavinhas animadas!

public class OptionsMenuManager : MonoBehaviour
{
    [Header("Abas de Opções")]
    public GameObject abaGeral;
    public GameObject abaControles;
    public GameObject abaGraficos;

    [Header("Sliders de Áudio")]
    public Slider masterSlider;
    public Slider sfxSlider;
    public Slider bgmSlider;

    [Header("Vídeo & UI")]
    public TMP_Dropdown resolutionDropdown;

    // As variáveis agora pedem a sua nova chavinha
    public ToggleSwitch fullscreenToggle;
    public ToggleSwitch vsyncToggle;

    [Header("Gráficos - Dropdowns")]
    public TMP_Dropdown gfxPresetDropdown;
    public TMP_Dropdown renderScaleDropdown;
    public TMP_Dropdown antiAliasingDropdown;
    public TMP_Dropdown abyssModeDropdown;
    public TMP_Dropdown fpsLimitDropdown;

    [Header("Gráficos - Toggles")]
    public ToggleSwitch shadowsToggle;
    public ToggleSwitch postProcessToggle;
    public ToggleSwitch speedrunTimerToggle;
    public ToggleSwitch showFpsToggle;

    [Header("Gráficos - Textos de Status")]
    public TextMeshProUGUI txtShadowsStatus;
    public TextMeshProUGUI txtPostProcessStatus;
    public TextMeshProUGUI txtSpeedrunStatus;
    public TextMeshProUGUI txtShowFpsStatus;

    [Header("Mira / Disparo")]
    public ToggleSwitch aimModeToggle;
    public Button btnAimMode;
    public TextMeshProUGUI txtAimMode;

    private Resolution[] resolutions;
    private bool _isSyncingGraphicsUI;

    void Awake()
    {
        GameGraphicsSettings.EnsureInitialized();
    }

    void Start()
    {
        ConfigurarAudio();
        ConfigurarVideo();
        ConfigurarModoDeMira();
        ConfigurarGraficos();
        RefreshGraphicsUIValues();

        // Garante que o menu sempre comece mostrando a aba Geral
        MostrarAbaGeral();
    }

    void OnEnable()
    {
        RefreshGraphicsUIValues();
        if (vsyncToggle != null)
        {
            vsyncToggle.ToggleByGroupManager(QualitySettings.vSyncCount > 0);
        }
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
            var options = new List<string>();

            for (int i = 0; i < resolutions.Length; i++)
            {
                options.Add($"{resolutions[i].width} x {resolutions[i].height}");
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                    currentRes = i;
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentRes;
            resolutionDropdown.RefreshShownValue();

            resolutionDropdown.onValueChanged.RemoveAllListeners();
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
        if (abaGraficos != null) abaGraficos.SetActive(false);
    }

    public void MostrarAbaControles()
    {
        if (abaGeral != null) abaGeral.SetActive(false);
        if (abaControles != null) abaControles.SetActive(true);
        if (abaGraficos != null) abaGraficos.SetActive(false);
    }

    public void MostrarAbaGraficos()
    {
        if (abaGeral != null) abaGeral.SetActive(false);
        if (abaControles != null) abaControles.SetActive(false);
        if (abaGraficos != null) abaGraficos.SetActive(true);
        RefreshGraphicsUIValues();
    }

    // ==========================================
    // FUNÇÕES PARA O SEU TOGGLE SWITCH (ABA GERAL)
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

    // ==========================================
    // ABA DE GRÁFICOS (DROPDOWNS + TOGGLES + FPS)
    // ==========================================

    private void ConfigurarGraficos()
    {
        SetupDropdown(
            gfxPresetDropdown,
            new List<string> { "Baixo (PC Fraco)", "Médio", "Alto", "Personalizado" },
            OnPresetDropdownChanged);

        SetupDropdown(
            renderScaleDropdown,
            new List<string> { "70% (Leve)", "85% (Médio)", "100% (Nativo)" },
            OnRenderScaleDropdownChanged);

        SetupDropdown(
            antiAliasingDropdown,
            new List<string> { "Desligado (Leve)", "Médio (2x)", "Alto (4x + SSAO)" },
            OnAntiAliasingDropdownChanged);

        SetupDropdown(
            abyssModeDropdown,
            new List<string> { "Desligado (Máx FPS)", "Apenas Fog (Leve)", "Abismo Leve", "Completo c/ Bruma" },
            OnAbyssDropdownChanged);

        SetupDropdown(
            fpsLimitDropdown,
            new List<string> { "Ilimitado", "120 FPS", "60 FPS", "30 FPS" },
            OnFpsLimitDropdownChanged);
    }

    private static void SetupDropdown(
        TMP_Dropdown dropdown,
        List<string> labels,
        UnityEngine.Events.UnityAction<int> callback)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(labels);
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(callback);
    }

    private void OnPresetDropdownChanged(int index)
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetPresetByIndex(index);
        RefreshGraphicsUIValues();
    }

    private void OnRenderScaleDropdownChanged(int index)
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetRenderScaleIndex(index);
        RefreshGraphicsUIValues();
    }

    private void OnAntiAliasingDropdownChanged(int index)
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetAntiAliasingMode(index);
        RefreshGraphicsUIValues();
    }

    private void OnAbyssDropdownChanged(int index)
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetAbyssFogMode(index);
        RefreshGraphicsUIValues();
    }

    private void OnFpsLimitDropdownChanged(int index)
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetTargetFpsIndex(index);
        RefreshGraphicsUIValues();
    }

    public void AtivarSombras()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetShadowsEnabled(true);
        RefreshGraphicsUIValues();
    }

    public void DesativarSombras()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetShadowsEnabled(false);
        RefreshGraphicsUIValues();
    }

    public void AtivarPosProcessamento()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetPostProcessEnabled(true);
        RefreshGraphicsUIValues();
    }

    public void DesativarPosProcessamento()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetPostProcessEnabled(false);
        RefreshGraphicsUIValues();
    }

    public void AtivarSpeedrun()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetSpeedrunTimerEnabled(true);
        RefreshGraphicsUIValues();
    }

    public void DesativarSpeedrun()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetSpeedrunTimerEnabled(false);
        RefreshGraphicsUIValues();
    }

    public void AtivarMostrarFPS()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetShowFpsEnabled(true);
        RefreshGraphicsUIValues();
    }

    public void DesativarMostrarFPS()
    {
        if (_isSyncingGraphicsUI) return;
        GameGraphicsSettings.SetShowFpsEnabled(false);
        RefreshGraphicsUIValues();
    }

    private void RefreshGraphicsUIValues()
    {
        _isSyncingGraphicsUI = true;
        try
        {
            SetDropdownValueSilent(gfxPresetDropdown, GameGraphicsSettings.GetPresetDropdownIndex());
            SetDropdownValueSilent(renderScaleDropdown, GameGraphicsSettings.GetRenderScaleDropdownIndex());
            SetDropdownValueSilent(antiAliasingDropdown, GameGraphicsSettings.GetAntiAliasingDropdownIndex());
            SetDropdownValueSilent(abyssModeDropdown, GameGraphicsSettings.GetAbyssDropdownIndex());
            SetDropdownValueSilent(fpsLimitDropdown, GameGraphicsSettings.GetTargetFpsDropdownIndex());

            SetToggleSilent(shadowsToggle, GameGraphicsSettings.ShadowsEnabled);
            SetToggleSilent(postProcessToggle, GameGraphicsSettings.PostProcessEnabled);
            SetToggleSilent(speedrunTimerToggle, GameGraphicsSettings.SpeedrunTimerEnabled);
            SetToggleSilent(showFpsToggle, GameGraphicsSettings.ShowFpsEnabled);

            UpdateStatusText(txtShadowsStatus, GameGraphicsSettings.ShadowsEnabled);
            UpdateStatusText(txtPostProcessStatus, GameGraphicsSettings.PostProcessEnabled);
            UpdateStatusText(txtSpeedrunStatus, GameGraphicsSettings.SpeedrunTimerEnabled);
            UpdateStatusText(txtShowFpsStatus, GameGraphicsSettings.ShowFpsEnabled);
        }
        finally
        {
            _isSyncingGraphicsUI = false;
        }
    }

    private static void SetDropdownValueSilent(TMP_Dropdown dropdown, int index)
    {
        if (dropdown == null) return;
        dropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, Mathf.Max(0, dropdown.options.Count - 1)));
        dropdown.RefreshShownValue();
    }

    private static void SetToggleSilent(ToggleSwitch toggle, bool value)
    {
        if (toggle != null && toggle.CurrentValue != value)
        {
            toggle.ToggleByGroupManager(value);
        }
    }

    private static void UpdateStatusText(TextMeshProUGUI tmp, bool enabled)
    {
        if (tmp == null) return;
        tmp.text = enabled ? "LIGADO" : "DESLIGADO";
        tmp.color = enabled
            ? new Color(0.36f, 0.92f, 0.62f, 1f)
            : new Color(0.66f, 0.70f, 0.80f, 1f);
    }
}

/// <summary>
/// Gerenciador persistente de configurações gráficas (URP, sombras, escala, abismo/fog, pós-processamento, FPS)
/// e contadores de HUD (Speedrun e Mostrar FPS), compartilhado entre Menu Principal e Menu de Pause.
/// </summary>
public static class GameGraphicsSettings
{
    public const string KEY_PRESET = "Settings_GfxPreset";         // 0=Baixo, 1=Médio, 2=Alto, -1=Custom
    public const string KEY_RENDER_SCALE = "Settings_RenderScale"; // 0.70f, 0.85f, 1.00f
    public const string KEY_SHADOWS = "Settings_Shadows";          // 0 ou 1
    public const string KEY_AA_SSAO = "Settings_AntiAliasingSSAO"; // 0=Off, 1=2x, 2=4x+SSAO
    public const string KEY_ABYSS_MODE = "Settings_AbyssFogMode";  // 0=Off, 1=FogLeve, 2=AbismoLeve, 3=Completo
    public const string KEY_POST_PROCESS = "Settings_PostProcess"; // 0 ou 1
    public const string KEY_FPS_LIMIT = "Settings_TargetFPS";      // 0=Ilimitado, 120, 60, 30
    public const string KEY_SPEEDRUN = "Settings_SpeedrunTimer";   // 0 ou 1
    public const string KEY_SHOW_FPS = "Settings_ShowFPS";         // 0 ou 1

    public static event Action OnSettingsChanged;

    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        _initialized = true;
        ApplyAll();
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        ApplyAll();
    }

    public static int GraphicsPreset
    {
        get => PlayerPrefs.GetInt(KEY_PRESET, 2);
        private set => PlayerPrefs.SetInt(KEY_PRESET, value);
    }

    public static float RenderScale
    {
        get => PlayerPrefs.GetFloat(KEY_RENDER_SCALE, 1f);
        private set => PlayerPrefs.SetFloat(KEY_RENDER_SCALE, value);
    }

    public static bool ShadowsEnabled
    {
        get => PlayerPrefs.GetInt(KEY_SHADOWS, 1) == 1;
        private set => PlayerPrefs.SetInt(KEY_SHADOWS, value ? 1 : 0);
    }

    public static int AntiAliasingMode
    {
        get => PlayerPrefs.GetInt(KEY_AA_SSAO, 2);
        private set => PlayerPrefs.SetInt(KEY_AA_SSAO, value);
    }

    public static int AbyssFogMode
    {
        get => PlayerPrefs.GetInt(KEY_ABYSS_MODE, 2);
        private set => PlayerPrefs.SetInt(KEY_ABYSS_MODE, value);
    }

    public static bool PostProcessEnabled
    {
        get => PlayerPrefs.GetInt(KEY_POST_PROCESS, 1) == 1;
        private set => PlayerPrefs.SetInt(KEY_POST_PROCESS, value ? 1 : 0);
    }

    public static int TargetFps
    {
        get => PlayerPrefs.GetInt(KEY_FPS_LIMIT, 0);
        private set => PlayerPrefs.SetInt(KEY_FPS_LIMIT, value);
    }

    public static bool SpeedrunTimerEnabled
    {
        get => PlayerPrefs.GetInt(KEY_SPEEDRUN, 0) == 1;
        private set => PlayerPrefs.SetInt(KEY_SPEEDRUN, value ? 1 : 0);
    }

    public static bool ShowFpsEnabled
    {
        get => PlayerPrefs.GetInt(KEY_SHOW_FPS, 0) == 1;
        private set => PlayerPrefs.SetInt(KEY_SHOW_FPS, value ? 1 : 0);
    }

    public static void SetPresetByIndex(int index)
    {
        if (index >= 3)
        {
            GraphicsPreset = -1;
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
            return;
        }

        GraphicsPreset = Mathf.Clamp(index, 0, 2);
        switch (GraphicsPreset)
        {
            case 0: // Baixo (PC Fraco)
                RenderScale = 0.70f;
                ShadowsEnabled = false;
                AntiAliasingMode = 0;
                AbyssFogMode = 1;
                PostProcessEnabled = false;
                break;
            case 1: // Médio
                RenderScale = 0.85f;
                ShadowsEnabled = true;
                AntiAliasingMode = 1;
                AbyssFogMode = 2;
                PostProcessEnabled = true;
                break;
            default: // Alto
                RenderScale = 1.00f;
                ShadowsEnabled = true;
                AntiAliasingMode = 2;
                AbyssFogMode = 2;
                PostProcessEnabled = true;
                break;
        }

        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetRenderScaleIndex(int index)
    {
        switch (index)
        {
            case 0: RenderScale = 0.70f; break;
            case 1: RenderScale = 0.85f; break;
            default: RenderScale = 1.00f; break;
        }
        GraphicsPreset = -1;
        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetAntiAliasingMode(int mode)
    {
        AntiAliasingMode = Mathf.Clamp(mode, 0, 2);
        GraphicsPreset = -1;
        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetAbyssFogMode(int mode)
    {
        AbyssFogMode = Mathf.Clamp(mode, 0, 3);
        GraphicsPreset = -1;
        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetTargetFpsIndex(int index)
    {
        switch (index)
        {
            case 1: TargetFps = 120; break;
            case 2: TargetFps = 60; break;
            case 3: TargetFps = 30; break;
            default: TargetFps = 0; break;
        }
        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetShadowsEnabled(bool enabled)
    {
        ShadowsEnabled = enabled;
        GraphicsPreset = -1;
        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetPostProcessEnabled(bool enabled)
    {
        PostProcessEnabled = enabled;
        GraphicsPreset = -1;
        PlayerPrefs.Save();
        ApplyAll();
    }

    public static void SetSpeedrunTimerEnabled(bool enabled)
    {
        SpeedrunTimerEnabled = enabled;
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    public static void SetShowFpsEnabled(bool enabled)
    {
        ShowFpsEnabled = enabled;
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    public static int GetPresetDropdownIndex()
    {
        int p = GraphicsPreset;
        return (p >= 0 && p <= 2) ? p : 3;
    }

    public static int GetRenderScaleDropdownIndex()
    {
        if (RenderScale < 0.78f) return 0;
        if (RenderScale < 0.93f) return 1;
        return 2;
    }

    public static int GetAntiAliasingDropdownIndex() => Mathf.Clamp(AntiAliasingMode, 0, 2);

    public static int GetAbyssDropdownIndex() => Mathf.Clamp(AbyssFogMode, 0, 3);

    public static int GetTargetFpsDropdownIndex()
    {
        switch (TargetFps)
        {
            case 120: return 1;
            case 60: return 2;
            case 30: return 3;
            default: return 0;
        }
    }

    public static void ApplyAll()
    {
        // 1. Quality Level: índice 0 (Mobile_RPAsset, sem SSAO) quando AA/SSAO < 2; índice 1 (PC_RPAsset) quando 2
        int targetQuality = (AntiAliasingMode >= 2 && QualitySettings.names.Length > 1) ? 1 : 0;
        if (QualitySettings.names.Length > targetQuality && QualitySettings.GetQualityLevel() != targetQuality)
        {
            QualitySettings.SetQualityLevel(targetQuality, true);
        }

        // 2. URP Asset settings
        bool shadows = ShadowsEnabled;
        QualitySettings.shadows = shadows ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.Disable;
        QualitySettings.softParticles = AntiAliasingMode >= 2;

        UniversalRenderPipelineAsset urp = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)
            ?? (QualitySettings.renderPipeline as UniversalRenderPipelineAsset);
        if (urp != null)
        {
            urp.renderScale = Mathf.Clamp(RenderScale, 0.5f, 1.0f);
            urp.msaaSampleCount = AntiAliasingMode == 0 ? 1 : (AntiAliasingMode == 1 ? 2 : 4);
            urp.shadowDistance = shadows ? (GraphicsPreset == 1 ? 30f : 50f) : 0f;
        }

        // 3. Cameras (Post-processing e Anti-aliasing)
        bool postProcess = PostProcessEnabled;
        Camera[] cams = Camera.allCameras;
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] == null) continue;
            UniversalAdditionalCameraData data = cams[i].GetComponent<UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.renderPostProcessing = postProcess;
                data.antialiasing = AntiAliasingMode == 0
                    ? AntialiasingMode.None
                    : AntialiasingMode.FastApproximateAntialiasing;
            }
        }

        // 4. FPS limit
        int fps = TargetFps;
        if (fps > 0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }
        else
        {
            Application.targetFrameRate = -1;
        }

        OnSettingsChanged?.Invoke();
    }
}
