using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controlador de Proximidade e Interação do Mercador das Sombras (Pacto de Sangue).
/// 
/// RECURSOS:
///   1. Névoa Sombria nos Pés (Controle Ultraleve de ParticleSystem):
///      O script apenas controla a taxa de emissão do ParticleSystem anexado no Prefab.
///   2. Oscilação Contínua do Tom Grave (Onda LFO): Frequência grave hipnótica em tempo real.
///   3. Escrituras Ocultas Místicas Vermelho Escuro e Retrato 'tensoeptinho_0'.
/// </summary>
public class Merchant : MonoBehaviour
{
    [Header("Raio de Interação")]
    [Tooltip("Tamanho da zona de interação (em metros). Ao entrar nesta área, o som reduz, a névoa se intensifica e o Eptinho alerta.")]
    public float interactionRadius = 3f;

    [Header("Configuração de Áudio (Sussurros)")]
    [Tooltip("Arraste o arquivo de áudio dos sussurros (Whispering) aqui ou coloque em Resources/Whispering.")]
    public AudioClip whisperingClip;
    [Range(0.1f, 3f)] public float whisperingVolume = 1.75f;
    [Tooltip("Tempo de fade out dos sussurros e restauração da música ao sair (segundos).")]
    public float exitFadeDuration = 2.0f;
    
    [Header("Distorção e Tom do Som (Pitch Grave / Demoníaco)")]
    [Tooltip("Tom base do áudio (valores menores que 1.0 deixam o som mais grave). Ex: 0.55")]
    [Range(0.2f, 1.5f)] public float basePitch = 0.55f;

    [Header("Oscilação Contínua do Pitch Grave (Onda LFO / Pulsação Hipnótica)")]
    [Tooltip("Ativa a oscilação suave e contínua do tom grave em tempo real.")]
    public bool enableSmoothPitchOscillation = true;
    [Tooltip("Velocidade da oscilação da onda (ex: 1.5 = pulsação média, 3.0 = rápida, 0.5 = lenta e profunda).")]
    [Range(0.1f, 8.0f)] public float pitchOscillationSpeed = 1.5f;
    [Tooltip("Tom grave mínimo da oscilação (ex: 0.35 = sub-grave super profundo).")]
    [Range(0.15f, 1.0f)] public float minGravePitch = 0.35f;
    [Tooltip("Tom grave máximo da oscilação (ex: 0.70 = grave cavernoso).")]
    [Range(0.3f, 1.5f)] public float maxGravePitch = 0.70f;

    [Header("Distorção Aleatória a Cada Ciclo")]
    [Tooltip("Ativa salto aleatório de tom a cada ciclo das escrituras.")]
    public bool enableRandomPitch = false;

    private AudioSource audioSource;
    private Coroutine audioTransitionRoutine;

    [Header("Efeito Visual: Névoa Sombria nos Pés (Ground Fog)")]
    [Tooltip("Arraste o ParticleSystem que você adicionou no Prefab do Mercador.")]
    public ParticleSystem merchantFogParticles;
    [Tooltip("Taxa de emissão da névoa em repouso (quando longe do Mercador).")]
    public float idleFogEmission = 25f;
    [Tooltip("Taxa de emissão da névoa quando o Player entra na área do Mercador.")]
    public float activeFogEmission = 60f;
    [Tooltip("Taxa de emissão da névoa quando o Pacto de Sangue é aberto (Névoa super intensa).")]
    public float pactFogEmission = 180f;

    [Header("Retrato e Fala do Eptinho Tenso / Assustado")]
    [Tooltip("Arraste o Sprite do Eptinho tenso aqui (ex: tensoeptinho_0).")]
    public Sprite eptinhoTensoSprite;
    [TextArea(2, 4)]
    public string eptinhoWarningMessage = "Estou sentindo uma energia perturbadora vindo dele... Tenha muito cuidado!";

    [Header("Escrituras Ocultas (Texto Místico Vermelho Escuro)")]
    [Tooltip("Fontes TMP personalizadas para alternar a cada ciclo (opcional).")]
    public List<TMP_FontAsset> occultFonts = new List<TMP_FontAsset>();
    [Tooltip("Intervalo entre cada troca aleatória de caracteres, tamanho, pitch e cor (em segundos).")]
    public float fontShiftInterval = 0.15f;

    [Header("Posição e Tamanho do Texto na Tela (Ajuste em Tempo Real)")]
    [Tooltip("Posição vertical na tela (0 = base da tela, 0.5 = centro, 1 = topo).")]
    [Range(0f, 1f)] public float textPosY = 0.65f;
    [Tooltip("Posição horizontal na tela (0.5 = centro da tela).")]
    [Range(0f, 1f)] public float textPosX = 0.5f;
    [Tooltip("Largura da área do texto.")]
    public float textWidth = 1000f;
    [Tooltip("Altura da área do texto.")]
    public float textHeight = 100f;

    [Header("Tamanho e Comprimento Aleatório do Texto")]
    public int minCharLength = 5;
    public int maxCharLength = 18;
    public int minFontSize = 20;
    public int maxFontSize = 34;

    // Conjunto de caracteres enigmáticos compatíveis com 100% das fontes TMP
    private static readonly string occultCharPool = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+=-~<>?/[]{}";

    // Tons de vermelho sangue escuro para variação contínua
    private static readonly string[] darkRedColors = new string[]
    {
        "#660000", "#550000", "#770000", "#440000", "#880011", "#330000", "#770505", "#500000", "#600303"
    };

    // ── Referências Internas ──────────────────────────────────────────────────
    private MerchantUIController uiController;
    private bool canInteract = false;
    private bool eptinhoWarnedThisApproach = false;

    private GameObject occultUIOverlay;
    private TextMeshProUGUI occultTextComp;
    private Coroutine occultTextRoutine;

    void Awake()
    {
        // Configura AudioSource para os sussurros
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.0f; // 2D puro para audibilidade total
        audioSource.volume = whisperingVolume;
        audioSource.pitch = basePitch;

        // Garante Rigidbody Cinemático para colisão confiável
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        EnsureTriggerColliderExists();
        EnsureFogParticlesExist();
        EnsureProceduralAnimationExists();
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

        LoadAudioAndSpriteResources();
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
            main.useUnscaledTime = true; // Garante que a névoa flua em tempo real com a UI aberta

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
            main.useUnscaledTime = true; // Ajusta parâmetro para funcionar em tempo real na UI

            var emission = merchantFogParticles.emission;
            float targetRate = intense ? pactFogEmission : (canInteract ? activeFogEmission : idleFogEmission);
            emission.rateOverTime = targetRate;
        }
    }

    /// <summary>
    /// Faz o Mercador desaparecer na cena após o pacto ser realizado.
    /// </summary>
    public void VanishAfterPact()
    {
        canInteract = false;
        StopOccultEffects();

        if (merchantFogParticles != null)
        {
            var main = merchantFogParticles.main;
            main.useUnscaledTime = true;
            merchantFogParticles.Emit(120);
        }

        StartCoroutine(VanishRoutine());
    }

    private System.Collections.IEnumerator VanishRoutine()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        gameObject.SetActive(false);
    }

    private void LoadAudioAndSpriteResources()
    {
        if (whisperingClip == null)
        {
            whisperingClip = Resources.Load<AudioClip>("Whispering");
            if (whisperingClip == null) whisperingClip = Resources.Load<AudioClip>("whisper");
            if (whisperingClip == null) whisperingClip = Resources.Load<AudioClip>("freesound_communi");
        }

        if (whisperingClip != null && audioSource != null)
        {
            audioSource.clip = whisperingClip;
        }

        if (eptinhoTensoSprite == null)
        {
            eptinhoTensoSprite = Resources.Load<Sprite>("tensoeptinho_0");
            if (eptinhoTensoSprite == null) eptinhoTensoSprite = Resources.Load<Sprite>("EPTONHO_TENSO");
            if (eptinhoTensoSprite == null) eptinhoTensoSprite = Resources.Load<Sprite>("EPTONHO_TRISTE");
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

            // 1. Reduz a música do jogo e ativa sussurros
            if (MusicManager.instance != null)
            {
                MusicManager.instance.SetMusicDucking(0.0f, 0.8f);
            }

            if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.volume = whisperingVolume;
                audioSource.pitch = enableSmoothPitchOscillation ? minGravePitch : (enableRandomPitch ? Random.Range(minGravePitch, maxGravePitch) : basePitch);
                audioSource.Play();
            }

            // 2. Intensifica a névoa sombria nos pés do Mercador e dispara uma onda instantânea de névoa
            SetFogEmission(activeFogEmission);
            if (merchantFogParticles != null)
            {
                merchantFogParticles.Emit(25);
            }

            // 3. Exibe escrituras vermelhas na tela
            StartOccultEffects();

            // 4. Dispara diálogo explicativo na 1ª vez ou popup tenso nas seguintes
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

            StopOccultEffects();

            // Retorna a névoa para a emissão em repouso
            SetFogEmission(idleFogEmission);

            // Transição ultrasuave de áudio ao sair
            SmoothTransitionOnExit(exitFadeDuration);

            if (uiController != null && uiController.IsUiOpen())
            {
                uiController.ClosePanel();
            }
        }
    }

    private void SmoothTransitionOnExit(float duration)
    {
        if (audioTransitionRoutine != null) StopCoroutine(audioTransitionRoutine);
        audioTransitionRoutine = StartCoroutine(FadeOutWhispersAndRestoreMusic(duration));
    }

    private IEnumerator FadeOutWhispersAndRestoreMusic(float duration)
    {
        float elapsed = 0f;
        float startWhisperVol = audioSource != null ? audioSource.volume : 0f;

        if (MusicManager.instance != null)
        {
            MusicManager.instance.SetMusicDucking(1.0f, duration);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.volume = Mathf.Lerp(startWhisperVol, 0f, t);
            }
            yield return null;
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.volume = 0f;
            audioSource.pitch = 1.0f;
        }
        audioTransitionRoutine = null;
    }

    void Update()
    {
        if (canInteract)
        {
            UpdateRealtimeTextPosition();

            // Oscilação Contínua e Suave do Tom Grave (Onda LFO / Pulsação Hipnótica)
            if (enableSmoothPitchOscillation && audioSource != null && audioSource.isPlaying)
            {
                float wave = (Mathf.Sin(Time.time * pitchOscillationSpeed) + 1.0f) * 0.5f;
                audioSource.pitch = Mathf.Lerp(minGravePitch, maxGravePitch, wave);
            }

            bool uiIsOpen = uiController != null && uiController.IsUiOpen();

            if (!uiIsOpen && Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log("[MERCHANT] ⚔️ Tecla [F] pressionada! Abrindo o Pacto...");
                StopOccultEffects();
                SetPactFogIntense(true);

                // 1. Restaura a música do jogo para o volume normal
                if (MusicManager.instance != null)
                {
                    MusicManager.instance.SetMusicDucking(1.0f, 1.0f);
                }

                // 2. Mantém os sussurros tocando bem baixinho de fundo
                if (audioSource != null)
                {
                    if (!audioSource.isPlaying && whisperingClip != null)
                    {
                        audioSource.clip = whisperingClip;
                        audioSource.Play();
                    }
                    audioSource.volume = 0.18f; // Volume baixo e sutil
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

    private void OnValidate()
    {
        EnsureTriggerColliderExists();
        UpdateRealtimeTextPosition();
    }

    private void UpdateRealtimeTextPosition()
    {
        if (occultTextComp != null)
        {
            RectTransform tr = occultTextComp.GetComponent<RectTransform>();
            if (tr != null)
            {
                tr.anchorMin = new Vector2(textPosX, textPosY);
                tr.anchorMax = new Vector2(textPosX, textPosY);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = Vector2.zero;
                tr.sizeDelta = new Vector2(textWidth, textHeight);
            }
        }
    }

    private void StartOccultEffects()
    {
        if (occultUIOverlay == null)
        {
            CreateOccultUI();
        }

        if (occultUIOverlay != null && !occultUIOverlay.activeSelf)
        {
            occultUIOverlay.SetActive(true);
        }

        if (occultTextRoutine == null)
        {
            occultTextRoutine = StartCoroutine(AnimateOccultText());
        }
    }

    private void StopOccultEffects()
    {
        if (occultTextRoutine != null)
        {
            StopCoroutine(occultTextRoutine);
            occultTextRoutine = null;
        }

        if (occultUIOverlay != null && occultUIOverlay.activeSelf)
        {
            occultUIOverlay.SetActive(false);
        }

        if (audioSource != null)
        {
            audioSource.pitch = 1.0f;
        }
    }

    private string GenerateRandomOccultString()
    {
        int length = Random.Range(minCharLength, maxCharLength + 1);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < length; i++)
        {
            char randomChar = occultCharPool[Random.Range(0, occultCharPool.Length)];
            sb.Append(randomChar);
            if (i < length - 1) sb.Append(" ");
        }

        string chosenColor = darkRedColors[Random.Range(0, darkRedColors.Length)];
        int randomSize = Random.Range(minFontSize, maxFontSize + 1);

        return $"<color={chosenColor}><b><size={randomSize}>{sb.ToString()}</size></b></color>";
    }

    private IEnumerator AnimateOccultText()
    {
        int fontIdx = 0;

        while (canInteract && (uiController == null || !uiController.IsUiOpen()))
        {
            if (occultTextComp != null)
            {
                occultTextComp.text = GenerateRandomOccultString();

                if (occultFonts != null && occultFonts.Count > 0)
                {
                    TMP_FontAsset fontToUse = occultFonts[fontIdx % occultFonts.Count];
                    if (fontToUse != null)
                    {
                        occultTextComp.font = fontToUse;
                    }
                    fontIdx++;
                }
            }

            // Se a oscilação contínua não estiver ativa, aplica a distorção por pulso
            if (!enableSmoothPitchOscillation && audioSource != null && audioSource.isPlaying)
            {
                if (enableRandomPitch)
                    audioSource.pitch = Random.Range(minGravePitch, maxGravePitch);
                else
                    audioSource.pitch = basePitch;
            }

            yield return new WaitForSeconds(fontShiftInterval);
        }

        if (audioSource != null) audioSource.pitch = 1.0f;
        occultTextRoutine = null;
    }

    private void CreateOccultUI()
    {
        if (occultUIOverlay != null) return;

        Canvas targetCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy && (c.name.Contains("HUD") || c.name.Contains("Player") || c.renderMode == RenderMode.ScreenSpaceOverlay))
            {
                targetCanvas = c;
                break;
            }
        }

        occultUIOverlay = new GameObject("MerchantOccultUI");

        if (targetCanvas != null)
        {
            occultUIOverlay.transform.SetParent(targetCanvas.transform, false);
        }
        else
        {
            Canvas c = occultUIOverlay.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 400;

            CanvasScaler cs = occultUIOverlay.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
        }

        RectTransform mainR = occultUIOverlay.GetComponent<RectTransform>();
        if (mainR == null) mainR = occultUIOverlay.AddComponent<RectTransform>();
        mainR.anchorMin = Vector2.zero; mainR.anchorMax = Vector2.one; mainR.sizeDelta = Vector2.zero;

        GameObject textObj = new GameObject("OccultScripturesText");
        textObj.transform.SetParent(occultUIOverlay.transform, false);

        occultTextComp = textObj.AddComponent<TextMeshProUGUI>();
        occultTextComp.fontStyle = FontStyles.Bold;
        occultTextComp.alignment = TextAlignmentOptions.Center;
        occultTextComp.raycastTarget = false;

        UpdateRealtimeTextPosition();
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

    /// <summary>
    /// Atalho para solicitar Cirurgia de Remoção através do Mercador.
    /// </summary>
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