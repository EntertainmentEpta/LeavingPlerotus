using System.Collections;
using UnityEngine;

/// <summary>
/// Controlador de Proximidade e Interação do Mercador das Sombras (Pacto de Sangue).
/// 
/// Atmosfera Oculta Completa:
/// 1. Áudio de Cochicho/Sussurros: Toca em loop com oscilação contínua de tom grave audível e perturbador.
/// 2. Ducking Musical: A música de fundo do jogo reduz ao se aproximar do Mercador.
/// 3. Névoa Sombria nos Pés: Partículas intensificadas dinamicamente.
/// 4. HUD Glitch no Canto Direito: O avatar do Mercador surge com as escrituras místicas em glitch vermelho,
///    alternando fontes e revelando palavras progressivamente conforme a tradução de silício.
/// </summary>
public class Merchant : MonoBehaviour
{
    [Header("Raio de Interação")]
    [Tooltip("Tamanho da zona de interação (em metros).")]
    public float interactionRadius = 4.5f;

    [Header("Configuração de Áudio (Sussurros)")]
    [Tooltip("Clip de sussurro e murmúrios do Mercador")]
    public AudioClip whisperingClip;
    [Range(0.1f, 1f)] public float whisperingVolume = 1.0f;
    [Tooltip("Tempo de fade out dos sussurros e restauração da música ao sair (segundos).")]
    public float exitFadeDuration = 1.5f;

    [Header("Distorção e Tom do Som (Audível e Cavernoso)")]
    [Range(0.5f, 1.2f)] public float basePitch = 0.85f;

    [Header("Oscilação Contínua do Pitch (Onda LFO / Pulsação Hipnótica)")]
    public bool enableSmoothPitchOscillation = true;
    [Range(0.1f, 5.0f)] public float pitchOscillationSpeed = 1.5f;
    [Range(0.5f, 1.0f)] public float minGravePitch = 0.72f;
    [Range(0.7f, 1.2f)] public float maxGravePitch = 0.95f;

    [Header("Efeito Visual: Névoa Sombria nos Pés")]
    public ParticleSystem merchantFogParticles;
    public float idleFogEmission = 25f;
    public float activeFogEmission = 60f;
    public float pactFogEmission = 180f;

    [Header("Aviso do Eptinho")]
    public Sprite eptinhoTensoSprite;
    [TextArea(2, 4)]
    public string eptinhoWarningMessage = "Estou sentindo uma energia perturbadora vindo dele... Tenha muito cuidado!";

    // ── Referências Internas ──────────────────────────────────────────────────
    private AudioSource audioSource;
    private Coroutine audioTransitionRoutine;
    private MerchantUIController uiController;
    private bool canInteract = false;
    private bool eptinhoWarnedThisApproach = false;

    void Awake()
    {
        EnsureAudioSourceConfigured();

        // Garante Rigidbody Cinemático para colisão confiável
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        EnsureTriggerColliderExists();
        EnsureFogParticlesExist();
        EnsureProceduralAnimationExists();
    }

    private void EnsureAudioSourceConfigured()
    {
        // Configura AudioSource para os sussurros
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources != null && sources.Length > 0)
        {
            foreach (var s in sources)
            {
                if (s.clip == whisperingClip)
                {
                    audioSource = s;
                    break;
                }
            }
            if (audioSource == null) audioSource = sources[0];
        }
        else
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Sanitização de tom para manter os formantes vocais nítidos e audíveis
        if (minGravePitch < 0.75f) minGravePitch = 0.80f;
        if (maxGravePitch < 0.90f) maxGravePitch = 1.02f;
        if (basePitch < 0.80f) basePitch = 0.88f;
        if (whisperingVolume < 0.5f) whisperingVolume = 1.0f;

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.0f; // 2D puro para audibilidade total sem corte por distância 3D
        audioSource.volume = Mathf.Clamp01(whisperingVolume);
        audioSource.pitch = basePitch;
        audioSource.priority = 0; // Prioridade máxima no áudio da Unity
        audioSource.mute = false;

        LoadAudioAndSpriteResources();
    }

    private void EnsureProceduralAnimationExists()
    {
        if (GetComponent<MerchantProceduralAnimation>() == null)
        {
            gameObject.AddComponent<MerchantProceduralAnimation>();
        }
    }

    void Start()
    {
        uiController = MerchantUIController.Instance;
        EnsureAudioSourceConfigured();
    }

    private void LoadAudioAndSpriteResources()
    {
        if (whisperingClip == null)
        {
#if UNITY_EDITOR
            whisperingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/NPC/Merchant/freesound_community-murmullos-7133.mp3");
#endif
            if (whisperingClip == null) whisperingClip = Resources.Load<AudioClip>("freesound_community-murmullos-7133");
            if (whisperingClip == null) whisperingClip = Resources.Load<AudioClip>("Whispering");
            if (whisperingClip == null) whisperingClip = Resources.Load<AudioClip>("whisper");
        }

        if (whisperingClip != null && audioSource != null)
        {
            audioSource.clip = whisperingClip;
        }

        if (eptinhoTensoSprite == null)
        {
            eptinhoTensoSprite = Resources.Load<Sprite>("tensoeptinho_0");
            if (eptinhoTensoSprite == null) eptinhoTensoSprite = Resources.Load<Sprite>("EPTONHO_TENSO");
        }
    }

    private void EnsureFogParticlesExist()
    {
        if (merchantFogParticles == null)
        {
            merchantFogParticles = GetComponentInChildren<ParticleSystem>();
        }

        if (merchantFogParticles != null)
        {
            var main = merchantFogParticles.main;
            main.useUnscaledTime = true;

            SetFogEmission(idleFogEmission);
            if (!merchantFogParticles.isPlaying) merchantFogParticles.Play();
        }
    }

    private void SetFogEmission(float emissionRate)
    {
        if (merchantFogParticles != null)
        {
            var main = merchantFogParticles.main;
            main.useUnscaledTime = true;
            var emission = merchantFogParticles.emission;
            emission.rateOverTime = emissionRate;
        }
    }

    public void SetPactFogIntense(bool intense)
    {
        EnsureFogParticlesExist();

        if (merchantFogParticles != null)
        {
            var main = merchantFogParticles.main;
            main.useUnscaledTime = true;

            var emission = merchantFogParticles.emission;
            float targetRate = intense ? pactFogEmission : (canInteract ? activeFogEmission : idleFogEmission);
            emission.rateOverTime = targetRate;
        }
    }

    void Update()
    {
        if (canInteract)
        {
            // Oscilação Contínua e Suave do Tom Grave (Onda LFO / Pulsação Hipnótica)
            if (enableSmoothPitchOscillation && audioSource != null && audioSource.isPlaying)
            {
                float wave = (Mathf.Sin(Time.time * pitchOscillationSpeed) + 1.0f) * 0.5f;
                audioSource.pitch = Mathf.Lerp(minGravePitch, maxGravePitch, wave);
            }

            bool uiIsOpen = uiController != null && uiController.IsUiOpen();

            if (!uiIsOpen && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E)))
            {
                Debug.Log("[MERCHANT] ⚔️ Tecla de interação pressionada! Abrindo o Pacto...");
                SetPactFogIntense(true);

                // 1. Restaura a música do jogo para o volume normal
                if (MusicManager.instance != null)
                {
                    MusicManager.instance.SetMusicDucking(1.0f, 0.5f);
                }

                // 2. Mantém os sussurros tocando bem baixinho de fundo
                if (audioSource != null)
                {
                    if (!audioSource.isPlaying && whisperingClip != null)
                    {
                        audioSource.clip = whisperingClip;
                        audioSource.Play();
                    }
                    audioSource.volume = 0.22f;
                    audioSource.pitch = basePitch;
                }

                if (uiController == null || uiController.gameObject == null) uiController = MerchantUIController.Instance;
                if (uiController != null)
                {
                    uiController.OpenPanel(this.transform);
                }
            }
        }
    }

    private void EnsureTriggerColliderExists()
    {
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = interactionRadius;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponentInParent<PlayerHealth>() != null) return true;
        if (other.GetComponentInParent<PlayerM>() != null) return true;
        if (other.name.ToLower().Contains("player")) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            canInteract = true;
            Debug.Log("[MERCHANT] 🧙‍♂️ Player ENTROU na zona do Mercador!");

            if (audioTransitionRoutine != null)
            {
                StopCoroutine(audioTransitionRoutine);
                audioTransitionRoutine = null;
            }

            // 1. Pára / reduz a música do jogo e ativa os sussurros
            if (MusicManager.instance != null)
            {
                MusicManager.instance.SetMusicDucking(0.0f, 0.8f);
            }

            // Toca os sussurros com volume pleno e audível
            if (audioSource != null)
            {
                if (audioSource.clip == null) LoadAudioAndSpriteResources();
                if (audioSource.clip != null)
                {
                    audioSource.volume = Mathf.Clamp01(whisperingVolume);
                    audioSource.pitch = basePitch;
                    if (!audioSource.isPlaying) audioSource.Play();
                    Debug.Log($"[MERCHANT AUDIO] ✅ Sussurros tocando! Clip: {audioSource.clip.name}, Vol: {audioSource.volume}, Pitch: {audioSource.pitch}");
                }
                else
                {
                    Debug.LogWarning("[MERCHANT AUDIO] ⚠️ whisperingClip não encontrado!");
                }
            }

            // 2. Intensifica a névoa nos pés
            SetFogEmission(activeFogEmission);
            if (merchantFogParticles != null)
            {
                merchantFogParticles.Emit(25);
            }

            // 3. Exibe o Avatar do Mercador e a fala com typewriter no HUD (canto direito)
            if (MerchantPopupController.Instance != null)
            {
                var line = MerchantEncounterDialogueSystem.GetCurrentEncounterLine();
                MerchantPopupController.Instance.MostrarPopupMercador(line.merchantSpeech, line.percentageBadge, duracao: 0f);
            }

            // 4. Prompt de interação e aviso do Eptinho
            if (uiController == null) uiController = MerchantUIController.Instance;
            if (uiController != null)
            {
                uiController.ShowPrompt(true);
            }

            if (!EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_MERCHANT_ENCOUNTER))
            {
                eptinhoWarnedThisApproach = true;
                EptinhoStoryDialogs.TriggerMerchantEncounter(() =>
                {
                    if (QuestManager.Instance != null)
                    {
                        QuestManager.Instance.AdvanceQuest(QuestType.Interact, "merchant_interact", 1);
                    }
                });
            }
            else if (!eptinhoWarnedThisApproach && EptinhoPopupController.instancia != null)
            {
                eptinhoWarnedThisApproach = true;
                EptinhoPopupController.instancia.MostrarPopupMercador(eptinhoWarningMessage);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            canInteract = false;
            eptinhoWarnedThisApproach = false;
            Debug.Log("[MERCHANT] Player SAIU da zona do Mercador.");

            // Esconde o popup do HUD
            if (MerchantPopupController.Instance != null)
            {
                MerchantPopupController.Instance.EsconderPopup();
            }

            if (uiController != null)
            {
                uiController.ShowPrompt(false);
            }

            // Retorna a névoa para repouso
            SetFogEmission(idleFogEmission);

            // Transição suave de áudio ao sair (fade dos sussurros e restauração da música)
            SmoothTransitionOnExit(exitFadeDuration);
        }
    }

    private void SmoothTransitionOnExit(float duration)
    {
        if (audioTransitionRoutine != null) StopCoroutine(audioTransitionRoutine);
        audioTransitionRoutine = StartCoroutine(SmoothExitRoutine(duration));
    }

    private IEnumerator SmoothExitRoutine(float duration)
    {
        // 1. Restaura a música do jogo
        if (MusicManager.instance != null)
        {
            MusicManager.instance.SetMusicDucking(1.0f, duration);
        }

        // 2. Fade out dos sussurros
        if (audioSource != null && audioSource.isPlaying)
        {
            float startVol = audioSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                audioSource.volume = Mathf.Lerp(startVol, 0f, t);
                yield return null;
            }

            audioSource.Stop();
            audioSource.volume = 0f;
            audioSource.pitch = 1.0f;
        }
        audioTransitionRoutine = null;
    }

    [ContextMenu("Testar Sussurros do Mercador")]
    public void TestarAudioSussurros()
    {
        EnsureAudioSourceConfigured();
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.volume = 1.0f;
            audioSource.pitch = basePitch;
            audioSource.Play();
            Debug.Log($"[MERCHANT AUDIO TEST] ✅ Tocando sussurro: {audioSource.clip.name}");
        }
    }

    /// <summary>
    /// Faz o Mercador desaparecer na cena após o pacto ser realizado.
    /// </summary>
    public void VanishAfterPact()
    {
        canInteract = false;

        if (MerchantPopupController.Instance != null)
        {
            MerchantPopupController.Instance.EsconderPopup();
        }

        if (MusicManager.instance != null)
        {
            MusicManager.instance.SetMusicDucking(1.0f, 1.0f);
        }

        if (merchantFogParticles != null)
        {
            var main = merchantFogParticles.main;
            main.useUnscaledTime = true;
            merchantFogParticles.Emit(120);
        }

        StartCoroutine(VanishRoutine());
    }

    private IEnumerator VanishRoutine()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        gameObject.SetActive(false);
    }

    public void TakeDamage(int damage, bool isCritical = false)
    {
        MerchantVFX vfx = GetComponent<MerchantVFX>() ?? GetComponentInChildren<MerchantVFX>();
        if (vfx != null)
        {
            vfx.TriggerMerchantHitReaction();
            return;
        }

        InvulnerableShieldNPC shield = GetComponent<InvulnerableShieldNPC>() ?? GetComponentInChildren<InvulnerableShieldNPC>();
        if (shield != null)
        {
            shield.TriggerShield();
        }
    }

    public void RequestSurgery(InfusedItem item)
    {
        if (uiController == null || uiController.gameObject == null) uiController = MerchantUIController.Instance;
        if (uiController != null) uiController.RequestSurgery(item);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}