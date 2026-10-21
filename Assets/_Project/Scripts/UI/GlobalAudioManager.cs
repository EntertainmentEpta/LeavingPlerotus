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
            }
            return _instance;
        }
        private set => _instance = value;
    }

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
        float bgmVal = PlayerPrefs.GetFloat("SavedBGM", 0.5f);
        float sfxVal = PlayerPrefs.GetFloat("SavedSFX", 0.75f);

        if (mainMixer != null)
        {
            mainMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Clamp(masterVal, 0.0001f, 1f)) * 20f);
            mainMixer.SetFloat("BGMVolume", Mathf.Log10(Mathf.Clamp(bgmVal, 0.0001f, 1f)) * 20f);
            mainMixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Clamp(sfxVal, 0.0001f, 1f)) * 20f);
        }
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