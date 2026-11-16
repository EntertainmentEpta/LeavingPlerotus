using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sistema de Cura do Traje do Eptinho (Mecânica Central de Sobrevivência).
/// - Medidor contínuo de Biomassa (0% a 100%), começa em 0%.
/// - A Biomassa NÃO recarrega por tempo. Ela só é acumulada por ações do jogador:
///     * Acertar golpe: +2%   (DummyHealth)
///     * Matar inimigo: +8% comum / +40% Elite (DummyHealth)
///     * Esquiva Perfeita: +35% (DashM)
/// - Com 100% de Biomassa, pressionar [Q] consome TODA a biomassa e cura:
///     * Em combate: 15% da vida máxima.
///     * Fora de combate / descanso / anomalia: 30% da vida máxima.
/// </summary>
public class SuitHealingSystem : MonoBehaviour
{
    public static SuitHealingSystem Instance { get; private set; }

    public const float MaxBiomass = 100f;

    /// <summary>Disparado sempre que a Biomassa muda. Parâmetro: valor atual (0 a 100).</summary>
    public event Action<float> OnBiomassChanged;

    [Header("Biomassa (0% a 100%)")]
    [Tooltip("Biomassa atual do traje. Começa em 0 e só aumenta por golpes, kills e Esquiva Perfeita.")]
    [SerializeField, Range(0f, MaxBiomass)] private float currentBiomass = 0f;

    [Header("Eficácia de Cura (% da Vida Máxima)")]
    [Tooltip("Percentual da vida curada durante combate ativo (padrão: 0.15 = 15%)")]
    public float combatHealPercent = 0.15f;
    [Tooltip("Percentual da vida curada fora de combate / salas de descanso (padrão: 0.30 = 30%)")]
    public float safeRoomHealPercent = 0.30f;

    [Header("Integridade do Traje")]
    [Range(0.4f, 1.0f)]
    public float suitIntegrity = 1.0f;

    [Header("Controles")]
    public KeyCode healKey = KeyCode.Q;

    [Header("Áudio das Cápsulas de Cura (SFX)")]
    [Tooltip("Som quando a 1ª cápsula (ou carga intermediária) fica pronta.")]
    public AudioClip firstCapsuleReadyClip;
    [Tooltip("Som quando a 2ª cápsula (todas as cargas) fica pronta para uso.")]
    public AudioClip allCapsulesReadyClip;
    [Tooltip("Som ao usar a cápsula de cura [Q] (regeneração/injeção).")]
    public AudioClip capsuleUsedClip;
    [Tooltip("Som opcional enquanto uma cápsula está recarregando (ex: som contínuo suave de síntese).")]
    public AudioClip rechargingClip;

    [Range(0f, 1f)] public float sfxVolume = 0.85f;
    [Range(0f, 1f)] public float rechargingVolume = 0.25f;
    [Tooltip("Se marcado e 'rechargingClip' estiver definido, toca um loop suave enquanto estiver recarregando uma cápsula.")]
    public bool enableRechargingLoop = false;

    // Componentes e Estado
    private PlayerHealth playerHealth;
    private AudioSource sfxSource;
    private AudioSource rechargeLoopSource;
    private bool clipsLoaded = false;
    private bool tookDamageInCurrentRoom = false;
    private bool isInCombat = false;
    public bool isInAnomalyRoom = false;
    private float combatCheckTimer = 0f;

    public float CurrentBiomass => currentBiomass;
    /// <summary>Biomassa normalizada (0 a 1) para barras de preenchimento.</summary>
    public float BiomassNormalized => Mathf.Clamp01(currentBiomass / MaxBiomass);
    /// <summary>Biomassa em porcentagem inteira (0 a 100).</summary>
    public int BiomassPercent => Mathf.Clamp(Mathf.FloorToInt(currentBiomass), 0, 100);
    /// <summary>True quando a Biomassa está em 100% (cura pronta para uso).</summary>
    public bool IsBiomassFull => currentBiomass >= MaxBiomass;
    public bool IsInCombat => isInCombat;
    public float SuitIntegrityPercent => suitIntegrity * 100f;

    private void Awake()
    {
        // Se já existe uma instância e esta pertence a um Player cópia que será destruído pelo PlayerPersistence, não sobrescreve
        if (Instance != null && Instance != this)
        {
            PlayerPersistence persistence = GetComponentInParent<PlayerPersistence>();
            if (persistence != null && PlayerPersistence.instance != null && PlayerPersistence.instance != persistence)
            {
                Destroy(this);
                return;
            }
        }
        Instance = this;
    }

    private void Start()
    {
        string savedKey = PlayerPrefs.GetString("KeyHeal", "Q");
        try
        {
            healKey = (KeyCode)Enum.Parse(typeof(KeyCode), savedKey);
        }
        catch
        {
            healKey = KeyCode.Q;
        }

        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();
        InitializeAudio();

        PlayerHealth.OnPlayerDamaged += OnPlayerTookDamage;
        RoomController.OnRoomCleared += OnRoomCleared;

        if (FindFirstObjectByType<SuitHealingUI>() == null)
        {
            GameObject uiObj = new GameObject("SuitHealingUI_Auto");
            DontDestroyOnLoad(uiObj);
            uiObj.AddComponent<SuitHealingUI>();
        }
    }

    private void OnDestroy()
    {
        PlayerHealth.OnPlayerDamaged -= OnPlayerTookDamage;
        RoomController.OnRoomCleared -= OnRoomCleared;
        StopRechargeLoopAudio();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Chamado ao morrer ou ao respawnar/acordar na Base. A Biomassa volta a 0%.
    /// </summary>
    public void ResetOnDeathOrRespawn()
    {
        suitIntegrity = 1.0f;
        isInCombat = false;
        isInAnomalyRoom = false;
        SetBiomass(0f);
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? FindFirstObjectByType<PlayerHealth>();
        StopRechargeLoopAudio();
        Debug.Log("🧪 [SuitHealing] Sistema de cura resetado pós-morte/respawn! Biomassa: 0%");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BIOMASSA
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Soma Biomassa ao medidor (com clamp entre 0 e 100). Valores negativos reduzem.
    /// </summary>
    public void AddBiomass(float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;
        SetBiomass(currentBiomass + amount);
    }

    /// <summary>Enche a Biomassa para 100% (testes / cheat).</summary>
    [ContextMenu("Debug/Encher Biomassa (100%)")]
    public void FillBiomass()
    {
        SetBiomass(MaxBiomass);
        Debug.Log("🧪 [SuitHealing] DEBUG: Biomassa preenchida para 100%.");
    }

    /// <summary>Zera a Biomassa (testes).</summary>
    [ContextMenu("Debug/Zerar Biomassa (0%)")]
    public void EmptyBiomass()
    {
        SetBiomass(0f);
    }

    private void SetBiomass(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, MaxBiomass);
        if (Mathf.Approximately(clamped, currentBiomass)) return;

        bool wasFull = IsBiomassFull;
        currentBiomass = clamped;
        OnBiomassChanged?.Invoke(currentBiomass);

        if (!wasFull && IsBiomassFull)
        {
            PlayBiomassReadyAudio();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        currentBiomass = Mathf.Clamp(currentBiomass, 0f, MaxBiomass);
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE / INPUT
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused ||
            CheatConsole.IsOpen ||
            (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) ||
            (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen))
        {
            StopRechargeLoopAudio();
            return;
        }

        combatCheckTimer += Time.deltaTime;
        if (combatCheckTimer >= 0.5f)
        {
            combatCheckTimer = 0f;
            CheckCombatState();
        }

        UpdateRechargeLoopAudio();

        if (Input.GetKeyDown(healKey))
        {
            if (!EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_HEALING_TUTORIAL))
            {
                EptinhoStoryDialogs.TriggerHealingTutorial();
                return;
            }
            HandleHealKeyInput();
        }
    }

    private void HandleHealKeyInput()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? FindFirstObjectByType<PlayerHealth>();
        }
        if (playerHealth == null || playerHealth.isDead) return;

        // Só permite curar com a Biomassa em 100%
        if (!IsBiomassFull)
        {
            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.FlashEmptyWarning();
            }
            Debug.LogWarning($"⚠️ [SuitHealing] Biomassa insuficiente ({currentBiomass:F0}%/100%). Acerte golpes, mate inimigos ou faça Esquivas Perfeitas!");
            return;
        }

        // Não gasta a Biomassa se a cura estiver bloqueada (ex: debuff que impede cura)
        if (!playerHealth.canHeal)
        {
            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.FlashEmptyWarning();
            }
            Debug.LogWarning("⚠️ [SuitHealing] Cura bloqueada no momento. Biomassa preservada.");
            return;
        }

        // Consome TODA a biomassa
        SetBiomass(0f);

        bool inCombatHeal = isInCombat && !isInAnomalyRoom;
        int maxHp = playerHealth.maxHealth > 0 ? playerHealth.maxHealth : 100;
        float pct = inCombatHeal ? combatHealPercent : safeRoomHealPercent;
        int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHp * pct));

        playerHealth.Heal(healAmount, false);
        PlayCapsuleUsedAudio();
        TriggerHealVFX();

        if (SuitHealingUI.Instance != null)
        {
            SuitHealingUI.Instance.TriggerHealFeedback();
        }

        Debug.Log($"💚 [SuitHealing] Biomassa consumida! Curado: +{healAmount} HP ({(pct * 100):F0}% da vida max) [{(inCombatHeal ? "COMBATE" : "DESCANSO")}]. Biomassa: 0%");
    }

    private void CheckCombatState()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 25f);
        bool foundEnemy = false;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy") || hit.GetComponent<DummyHealth>() != null)
            {
                foundEnemy = true;
                break;
            }
        }
        isInCombat = foundEnemy;
    }

    private void OnPlayerTookDamage(int damage, GameObject attacker)
    {
        tookDamageInCurrentRoom = true;
        suitIntegrity = Mathf.Max(0.40f, suitIntegrity - 0.15f);
        Debug.Log($"⚠️ [SuitHealing] Dano recebido! Integridade: {SuitIntegrityPercent:F0}%");

        if (!EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_HEALING_TUTORIAL))
        {
            EptinhoStoryDialogs.TriggerHealingTutorial();
        }
    }

    private void OnRoomCleared(RoomController room)
    {
        Debug.Log("🚪 [SuitHealing] Sala concluída!");

        // Se saiu ileso da fase (Clean Room), restaura a integridade do traje por completo
        if (!tookDamageInCurrentRoom)
        {
            Debug.Log("⭐ [SuitHealing] CLEAN ROOM! Jogador saiu ileso! Integridade do traje restaurada.");
            suitIntegrity = 1.0f;
        }
        else
        {
            suitIntegrity = Mathf.Min(1.0f, suitIntegrity + 0.20f);
        }

        tookDamageInCurrentRoom = false;
    }

    private void TriggerHealVFX()
    {
        GameObject healFxObj = new GameObject("HealBurstFX");
        healFxObj.transform.position = transform.position + Vector3.up * 1.0f;

        Light l = healFxObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.1f, 1.0f, 0.5f);
        l.range = 8f;
        l.intensity = 4.5f;

        // Tenta carregar prefab de efeito caso exista em Resources
        GameObject prefab = Resources.Load<GameObject>("Healing") ?? Resources.Load<GameObject>("Magic effects pack/Prefabs/Character auras/Healing");
        if (prefab != null)
        {
            GameObject fx = Instantiate(prefab, transform.position, Quaternion.identity);
            fx.transform.SetParent(transform, true);
            Destroy(fx, 2.0f);
        }

        Destroy(healFxObj, 1.2f);
    }

    private void OnDisable()
    {
        StopRechargeLoopAudio();
    }

    private void InitializeAudio()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        if (rechargeLoopSource == null)
        {
            rechargeLoopSource = gameObject.AddComponent<AudioSource>();
            rechargeLoopSource.playOnAwake = false;
            rechargeLoopSource.loop = true;
            rechargeLoopSource.spatialBlend = 0f;
        }

        SyncAudioMixerGroup();
        EnsureAudioClipsLoaded();
    }

    private void SyncAudioMixerGroup()
    {
        if (GlobalAudioManager.Instance == null || GlobalAudioManager.Instance.sfxGroup == null) return;
        if (sfxSource != null && sfxSource.outputAudioMixerGroup == null)
            sfxSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
        if (rechargeLoopSource != null && rechargeLoopSource.outputAudioMixerGroup == null)
            rechargeLoopSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
    }

    private void EnsureAudioClipsLoaded()
    {
        if (clipsLoaded) return;
        clipsLoaded = true;

        firstCapsuleReadyClip ??= LoadHealingClip("SoundFX/SuitHealing/CargaPronta", "SoundFX/Shield 1/Carga Pronta = WAV", "Assets/_Project/Audio/SoundFX/Shield 1/Carga Pronta = WAV.wav");
        allCapsulesReadyClip ??= LoadHealingClip("SoundFX/SuitHealing/ProntasParaUso", "SoundFX/Shield 1/Prontas para uso = WAV", "Assets/_Project/Audio/SoundFX/Shield 1/Prontas para uso = WAV.wav");
        capsuleUsedClip ??= LoadHealingClip("SoundFX/SuitHealing/Regenerando", "SoundFX/Shield 1/Regenerando = WAV", "Assets/_Project/Audio/SoundFX/Shield 1/Regenerando = WAV.wav");
        rechargingClip ??= LoadHealingClip("SoundFX/SuitHealing/Recarregando", null, null);
    }

    private static AudioClip LoadHealingClip(string primaryResourcePath, string secondaryResourcePath, string editorAssetPath)
    {
        AudioClip clip = !string.IsNullOrEmpty(primaryResourcePath) ? Resources.Load<AudioClip>(primaryResourcePath) : null;
        if (clip != null) return clip;

        if (!string.IsNullOrEmpty(secondaryResourcePath))
        {
            clip = Resources.Load<AudioClip>(secondaryResourcePath);
            if (clip != null) return clip;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAssetPath))
        {
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(editorAssetPath);
        }
#endif
        return clip;
    }

    private void PlayBiomassReadyAudio()
    {
        EnsureAudioClipsLoaded();
        AudioClip clip = allCapsulesReadyClip ?? firstCapsuleReadyClip;
        PlayOneShotSfx(clip);
    }

    private void PlayCapsuleUsedAudio()
    {
        EnsureAudioClipsLoaded();
        PlayOneShotSfx(capsuleUsedClip);
    }

    private void PlayOneShotSfx(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource == null) InitializeAudio();
        SyncAudioMixerGroup();

        float effectiveVol = Mathf.Clamp01(sfxVolume) * GlobalAudioManager.SFXVolumeMultiplier;
        sfxSource.PlayOneShot(clip, effectiveVol);
    }

    private void UpdateRechargeLoopAudio()
    {
        bool shouldPlay = enableRechargingLoop &&
                          rechargingClip != null &&
                          !IsBiomassFull &&
                          currentBiomass > 0f &&
                          (playerHealth == null || !playerHealth.isDead);

        if (!shouldPlay)
        {
            StopRechargeLoopAudio();
            return;
        }

        if (rechargeLoopSource == null) InitializeAudio();
        SyncAudioMixerGroup();

        if (rechargeLoopSource.clip != rechargingClip)
        {
            rechargeLoopSource.clip = rechargingClip;
        }

        rechargeLoopSource.volume = Mathf.Clamp01(rechargingVolume) * GlobalAudioManager.SFXVolumeMultiplier;
        if (!rechargeLoopSource.isPlaying)
        {
            rechargeLoopSource.Play();
        }
    }

    private void StopRechargeLoopAudio()
    {
        if (rechargeLoopSource != null && rechargeLoopSource.isPlaying)
        {
            rechargeLoopSource.Stop();
        }
    }
}
