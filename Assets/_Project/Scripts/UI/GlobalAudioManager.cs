using UnityEngine;
using UnityEngine.Audio;

public class GlobalAudioManager : MonoBehaviour
{
    public static GlobalAudioManager Instance { get; private set; }

    [Header("Mesa de Som Global (Audio Mixer)")]
    public AudioMixer mainMixer;
    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;

    [Header("Música Padrão")]
    public AudioClip defaultMusic; 

    private AudioSource bgmSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupBGM();
        LoadSavedVolume();
    }

    private void Start()
    {
        if (defaultMusic != null)
        {
            PlayMusic(defaultMusic);
        }
    }

    private void SetupBGM()
    {
        bgmSource = gameObject.GetComponent<AudioSource>();
        if (bgmSource == null)
        {
            bgmSource = gameObject.gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        if (bgmGroup != null) bgmSource.outputAudioMixerGroup = bgmGroup;
    }

    private void LoadSavedVolume()
    {
        // Carrega todos os volumes salvos (incluindo o Master)
        float masterVal = PlayerPrefs.GetFloat("SavedMaster", 1f);
        float bgmVal = PlayerPrefs.GetFloat("SavedBGM", 0.75f);
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
        if (bgmSource == null || musicClip == null) return;
        if (bgmSource.clip == musicClip && bgmSource.isPlaying) return;

        bgmSource.clip = musicClip;
        bgmSource.Play();
    }
}