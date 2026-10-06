using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Audio; // <-- Adicionamos a biblioteca de Áudio da Unity!

public class MenuAudioFX : MonoBehaviour
{
    public static MenuAudioFX Instance { get; private set; }

    [Header("Roteamento da Mesa de Som (Mixer)")]
    public AudioMixerGroup bgmMixerGroup; // <-- Tomada para a Música
    public AudioMixerGroup sfxMixerGroup; // <-- Tomada para os Efeitos

    [Header("Áudio de Fundo (BGM)")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 1f; // Recomendo deixar no 1 e controlar pelo Slider

    [Header("Efeitos de Interface (UI SFX)")]
    public AudioClip customHoverSound;
    public AudioClip customClickSound;
    [Range(0f, 1f)] public float sfxVolume = 1f; // Recomendo deixar no 1 e controlar pelo Slider

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private AudioClip synthHoverClip;
    private AudioClip synthClickClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupAudioSources();
        GenerateProceduralClips();
    }

    private void Start()
    {
        PlayBackgroundMusic();
        AttachAudioToAllButtons();
    }

    private void SetupAudioSources()
    {
        // Cria a fonte de Música e PLUGA na mesa de som!
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = musicVolume;
        if (bgmMixerGroup != null) bgmSource.outputAudioMixerGroup = bgmMixerGroup;

        // Cria a fonte de SFX e PLUGA na mesa de som!
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
        if (sfxMixerGroup != null) sfxSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    private void GenerateProceduralClips()
    {
        // 1. Hover Sound: Tick cristalino rápido (0.04s, 1200Hz -> 1800Hz)
        synthHoverClip = CreateSynthClip("HoverSFX", 0.04f, (t) => {
            float freq = Mathf.Lerp(1200f, 1800f, t);
            float wave = Mathf.Sin(t * freq * Mathf.PI * 2f);
            float env = Mathf.Exp(-t * 85f); // Fast decay
            return wave * env * 0.45f;
        });

        // 2. Click Sound: Impacto encorpado grave cristalino (0.12s, 440Hz -> 90Hz)
        synthClickClip = CreateSynthClip("ClickSFX", 0.12f, (t) => {
            float freq = Mathf.Lerp(480f, 90f, t);
            float wave1 = Mathf.Sin(t * freq * Mathf.PI * 2f);
            float wave2 = Mathf.Sin(t * (freq * 0.5f) * Mathf.PI * 2f); // Sub bass
            float env = Mathf.Exp(-t * 25f);
            return (wave1 * 0.6f + wave2 * 0.4f) * env * 0.8f;
        });
    }

    private AudioClip CreateSynthClip(string name, float duration, System.Func<float, float> generator)
    {
        int sampleRate = 44100;
        int numSamples = Mathf.FloorToInt(duration * sampleRate);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / numSamples;
            samples[i] = Mathf.Clamp(generator(t), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(name, numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayBackgroundMusic()
    {
        // AVISO: Se você estiver usando o MusicManager em vez deste script para tocar música, 
        // a música não vai abaixar a menos que o MusicManager também esteja plugado na Mesa de Som!
        if (MusicManager.instance != null)
        {
            MusicManager.instance.PlayTrack(0);
            return;
        }

        if (backgroundMusic != null && bgmSource != null)
        {
            bgmSource.clip = backgroundMusic;
            bgmSource.Play();
        }
    }

    public void PlayHoverSound()
    {
        if (sfxSource == null) return;
        AudioClip clipToPlay = customHoverSound != null ? customHoverSound : synthHoverClip;
        if (clipToPlay != null)
        {
            sfxSource.PlayOneShot(clipToPlay, sfxVolume * 0.65f);
        }
    }

    public void PlayClickSound()
    {
        if (sfxSource == null) return;
        AudioClip clipToPlay = customClickSound != null ? customClickSound : synthClickClip;
        if (clipToPlay != null)
        {
            sfxSource.PlayOneShot(clipToPlay, sfxVolume);
        }
    }

    public void AttachAudioToAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button btn in buttons)
        {
            if (btn == null) continue;
            btn.onClick.RemoveListener(PlayClickSound);
            btn.onClick.AddListener(PlayClickSound);
        }
    }
}