using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;

public class GlobalAudioManager : MonoBehaviour
{
    private static GlobalAudioManager _instance;
    public static GlobalAudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindFirstObjectByType<GlobalAudioManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GlobalAudioManager_Auto");
                    _instance = go.AddComponent<GlobalAudioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public static float CurrentSFXVolume { get; private set; } = 0.75f;
    public static float CurrentMasterVolume { get; private set; } = 0.8f;
    public static float CurrentBGMVolume { get; private set; } = 0.75f;
    public static float SFXVolumeMultiplier => (Instance != null) ? CurrentSFXVolume : PlayerPrefs.GetFloat("SavedSFX", 0.75f);
    public static System.Action<float> OnSFXVolumeChanged;

    private float nextPreviewSFXTime = 0f;
    private AudioSource previewSource;

    [Header("Mesa de Som Global (Audio Mixer)")]
    public AudioMixer mainMixer;
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;

    [Header("Música Padrão")]
    public AudioClip defaultMusic; 
    [Range(0f, 1f)] public float defaultVolume = 0.35f;

    private AudioSource bgmSource;
    private float duckMultiplier = 1.0f;
    private Coroutine duckCoroutine;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Garante que o MusicManager exista neste mesmo objeto para atender scripts legados (ex: Merchant)
        if (MusicManager.instance == null)
        {
            MusicManager mm = GetComponent<MusicManager>();
            if (mm == null) mm = gameObject.AddComponent<MusicManager>();
        }

        SetupBGM();
        LoadSavedVolume();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Toca a música padrão em qualquer cena que iniciar o jogo (MainMenu, Base, GameScene)
        if (defaultMusic != null && !IsPlaying())
        {
            PlayMusic(defaultMusic);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reseta qualquer ducking pendente (ex: do Mercador) ao mudar de cena
        duckMultiplier = 1.0f;
        if (duckCoroutine != null)
        {
            StopCoroutine(duckCoroutine);
            duckCoroutine = null;
        }

        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.volume = defaultVolume;
        }

        // Garante que a trilha sonora continue tocando em todas as cenas do jogo (MainMenu, Base, GameScene)
        if (defaultMusic != null && !IsPlaying())
        {
            PlayMusic(defaultMusic);
        }
    }

    private void SetupBGM()
    {
        bgmSource = gameObject.GetComponent<AudioSource>();
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = defaultVolume;

        if (bgmGroup != null) bgmSource.outputAudioMixerGroup = bgmGroup;
    }

    private void LoadSavedVolume()
    {
        // Carrega volumes salvos com padrões equilibrados
        float masterVal = PlayerPrefs.GetFloat("SavedMaster", 0.8f);
        float bgmVal = PlayerPrefs.GetFloat("SavedBGM", 0.75f);
        float sfxVal = PlayerPrefs.GetFloat("SavedSFX", 0.75f);

        CurrentMasterVolume = masterVal;
        CurrentBGMVolume = bgmVal;
        CurrentSFXVolume = sfxVal;

        AudioListener.volume = masterVal;
        defaultVolume = bgmVal;
        if (bgmSource != null)
        {
            bgmSource.volume = defaultVolume * duckMultiplier;
        }

        if (mainMixer != null)
        {
            float mDb = (masterVal <= 0.001f) ? -80f : Mathf.Log10(Mathf.Clamp(masterVal, 0.0001f, 1f)) * 20f;
            float bDb = (bgmVal <= 0.001f) ? -80f : Mathf.Log10(Mathf.Clamp(bgmVal, 0.0001f, 1f)) * 20f;
            float sDb = (sfxVal <= 0.001f) ? -80f : Mathf.Log10(Mathf.Clamp(sfxVal, 0.0001f, 1f)) * 20f;

            mainMixer.SetFloat("MasterVolume", mDb);
            mainMixer.SetFloat("BGMVolume", bDb);
            mainMixer.SetFloat("SFXVolume", sDb);
        }
    }

    public void SetBGMVolume(float volume)
    {
        defaultVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
        {
            bgmSource.volume = defaultVolume * duckMultiplier;
        }
        if (mainMixer != null)
        {
            float db = (volume <= 0.001f) ? -80f : Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
            mainMixer.SetFloat("BGMVolume", db);
        }
        PlayerPrefs.SetFloat("SavedBGM", defaultVolume);
    }

    public void SetMasterVolume(float volume)
    {
        float val = Mathf.Clamp01(volume);
        AudioListener.volume = val;
        if (mainMixer != null)
        {
            float db = (val <= 0.001f) ? -80f : Mathf.Log10(Mathf.Clamp(val, 0.0001f, 1f)) * 20f;
            mainMixer.SetFloat("MasterVolume", db);
        }
        PlayerPrefs.SetFloat("SavedMaster", val);
    }

    public void SetSFXVolume(float volume)
    {
        float val = Mathf.Clamp01(volume);
        CurrentSFXVolume = val;

        if (mainMixer != null)
        {
            float db = (val <= 0.001f) ? -80f : Mathf.Log10(Mathf.Clamp(val, 0.0001f, 1f)) * 20f;
            mainMixer.SetFloat("SFXVolume", db);
        }
        PlayerPrefs.SetFloat("SavedSFX", val);
        PlayerPrefs.Save();

        OnSFXVolumeChanged?.Invoke(val);
    }

    public void PlayPreviewSFX(AudioClip previewClip = null)
    {
        if (Time.unscaledTime < nextPreviewSFXTime) return;
        nextPreviewSFXTime = Time.unscaledTime + 0.10f;

        if (previewSource == null)
        {
            previewSource = gameObject.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.loop = false;
            previewSource.spatialBlend = 0f;
            if (sfxGroup != null) previewSource.outputAudioMixerGroup = sfxGroup;
        }

        float effectiveVolume = CurrentSFXVolume;
        if (effectiveVolume <= 0.001f) return;

        AudioClip clipToPlay = previewClip;
        if (clipToPlay == null && MenuAudioFX.Instance != null && MenuAudioFX.Instance.customClickSound != null)
        {
            clipToPlay = MenuAudioFX.Instance.customClickSound;
        }

        if (clipToPlay != null)
        {
            previewSource.pitch = 1.0f;
            previewSource.PlayOneShot(clipToPlay, effectiveVolume);
        }
        else
        {
            AudioClip beep = GetSynthPreviewClip();
            previewSource.pitch = 1.0f;
            previewSource.PlayOneShot(beep, effectiveVolume * 0.75f);
        }
    }

    private static AudioClip _synthPreviewClip;
    private AudioClip GetSynthPreviewClip()
    {
        if (_synthPreviewClip != null) return _synthPreviewClip;
        int sampleRate = 44100;
        int numSamples = Mathf.FloorToInt(0.08f * sampleRate);
        float[] samples = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            float freq = Mathf.Lerp(600f, 320f, t);
            float wave = Mathf.Sin(t * freq * Mathf.PI * 2f);
            float env = Mathf.Exp(-t * 30f);
            samples[i] = wave * env * 0.5f;
        }
        _synthPreviewClip = AudioClip.Create("SFXPreviewBeep", numSamples, 1, sampleRate, false);
        _synthPreviewClip.SetData(samples, 0);
        return _synthPreviewClip;
    }

    public void PlayMusic(AudioClip musicClip)
    {
        if (bgmSource == null) SetupBGM();
        if (musicClip == null) return;
        if (bgmSource.clip == musicClip && bgmSource.isPlaying) return;

        bgmSource.clip = musicClip;
        bgmSource.volume = defaultVolume * duckMultiplier;
        bgmSource.Play();
    }

    public void StopMusic()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }

    public bool IsPlaying()
    {
        return bgmSource != null && bgmSource.isPlaying;
    }

    public void SetPaused(bool paused)
    {
        if (bgmSource == null) return;
        if (paused) bgmSource.Pause();
        else bgmSource.UnPause();
    }

    /// <summary>
    /// Ducking de áudio (reduz ou restaura o volume da música suavemente, ex: perto do Mercador).
    /// </summary>
    public void SetMusicDucking(float targetMultiplier, float duration = 1.0f)
    {
        if (duckCoroutine != null) StopCoroutine(duckCoroutine);
        duckCoroutine = StartCoroutine(AnimateDucking(Mathf.Clamp01(targetMultiplier), duration));
    }

    private IEnumerator AnimateDucking(float target, float duration)
    {
        float start = duckMultiplier;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            duckMultiplier = Mathf.Lerp(start, target, elapsed / duration);
            if (bgmSource != null)
            {
                bgmSource.volume = defaultVolume * duckMultiplier;
            }
            yield return null;
        }
        duckMultiplier = target;
        if (bgmSource != null)
        {
            bgmSource.volume = defaultVolume * duckMultiplier;
        }
        duckCoroutine = null;
    }
}