using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sistema de Cura do Traje do Eptinho (Mecânica Central de Sobrevivência).
/// - Medidor contínuo de Biomassa (0% a 100%), começa em 0%.
/// - A Biomassa NÃO recarrega por tempo. Ela só é acumulada por ações do jogador:
///     * Acertar golpe: +2%   (DummyHealth / ShardSwarmHealth)
///     * Matar inimigo: +8% comum / +40% Elite
///     * Esquiva Perfeita: +35% (DashM)
/// - Ao completar 100% de Biomassa, 1 carga é guardada em um Slot de Cura (base: 2 slots;
///   com a melhoria do robozinho 'equip_reservatorio_cura': 3 slots), permitindo segurar curas e continuar acumulando.
/// - Pressionar [Q] consome 1 Slot guardado e cura:
///     * Em combate: 15% da vida máxima.
///     * Fora de combate / descanso / anomalia: 30% da vida máxima.
/// </summary>
public class SuitHealingSystem : MonoBehaviour
{
    public static SuitHealingSystem Instance { get; private set; }

    public const float MaxBiomass = 100f;
    public const int BaseMaxSlots = 2;
    public const int UpgradedMaxSlots = 3;

    /// <summary>Disparado sempre que a Biomassa muda. Parâmetro: valor atual (0 a 100).</summary>
    public event Action<float> OnBiomassChanged;

    /// <summary>Disparado quando mudam os slots guardados, o limite de slots ou a biomassa atual.</summary>
    public event Action<int, int, float> OnHealingStateChanged;

    [Header("Biomassa & Slots Guardados")]
    [Tooltip("Biomassa atual em progresso para o próximo slot (0% a 100%).")]
    [SerializeField, Range(0f, MaxBiomass)] private float currentBiomass = 0f;

    [Tooltip("Quantidade de cargas completas (100%) guardadas prontas para usar em [Q].")]
    [SerializeField] private int storedSlots = 0;

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

    public int MaxSlots =>
        (EquipmentManager.Instance != null &&
         (EquipmentManager.Instance.IsEquipped("equip_reservatorio_cura") || EquipmentManager.Instance.IsOwned("equip_reservatorio_cura")))
            ? UpgradedMaxSlots
            : BaseMaxSlots;

    public int StoredSlots => Mathf.Clamp(storedSlots, 0, MaxSlots);
    public bool HasStoredHeal => StoredSlots > 0;
    public bool AreAllSlotsFull => StoredSlots >= MaxSlots;

    public float CurrentBiomass => currentBiomass;
    /// <summary>Biomassa normalizada (0 a 1) para barras de preenchimento.</summary>
    public float BiomassNormalized => AreAllSlotsFull ? 1f : Mathf.Clamp01(currentBiomass / MaxBiomass);
    /// <summary>Biomassa em porcentagem inteira (0 a 100).</summary>
    public int BiomassPercent => AreAllSlotsFull ? 100 : Mathf.Clamp(Mathf.FloorToInt(currentBiomass), 0, 100);
    /// <summary>True quando existe pelo menos 1 slot de cura pronto para usar.</summary>
    public bool IsBiomassFull => HasStoredHeal || currentBiomass >= MaxBiomass;
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
    /// Chamado ao morrer ou ao respawnar/acordar na Base. Os slots e a Biomassa voltam a 0.
    /// </summary>
    public void ResetOnDeathOrRespawn()
    {
        suitIntegrity = 1.0f;
        isInCombat = false;
        isInAnomalyRoom = false;
        storedSlots = 0;
        SetBiomass(0f);
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? FindFirstObjectByType<PlayerHealth>();
        StopRechargeLoopAudio();
        Debug.Log("🧪 [SuitHealing] Sistema de cura resetado pós-morte/respawn! Slots: 0 | Biomassa: 0%");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BIOMASSA & SLOTS GUARDADOS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Soma Biomassa ao medidor. Ao completar 100%, armazena 1 slot (até o limite de MaxSlots).
    /// </summary>
    public void AddBiomass(float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;

        int maxSlots = MaxSlots;
        if (amount > 0f && storedSlots >= maxSlots)
        {
            if (!Mathf.Approximately(currentBiomass, 0f))
            {
                currentBiomass = 0f;
                OnBiomassChanged?.Invoke(currentBiomass);
            }
            OnHealingStateChanged?.Invoke(storedSlots, maxSlots, currentBiomass);
            return;
        }

        float nextBiomass = Mathf.Max(0f, currentBiomass + amount);
        int prevSlots = storedSlots;

        while (nextBiomass >= MaxBiomass && storedSlots < maxSlots)
        {
            nextBiomass -= MaxBiomass;
            storedSlots++;
        }

        if (storedSlots >= maxSlots)
        {
            nextBiomass = 0f;
        }

        currentBiomass = Mathf.Clamp(nextBiomass, 0f, MaxBiomass);

        if (storedSlots > prevSlots)
        {
            PlaySlotReadyAudio(storedSlots >= maxSlots);
        }

        OnBiomassChanged?.Invoke(currentBiomass);
        OnHealingStateChanged?.Invoke(storedSlots, maxSlots, currentBiomass);
    }

    /// <summary>Adiciona 1 carga completa (100% de Biomassa -> +1 Slot) para testes/cheat.</summary>
    [ContextMenu("Debug/Encher +1 Slot de Biomassa (100%)")]
    public void FillBiomass()
    {
        AddBiomass(MaxBiomass);
        Debug.Log($"🧪 [SuitHealing] DEBUG: Biomassa adicionada (+100%). Slots: {StoredSlots}/{MaxSlots} | Barra: {currentBiomass:F0}%");
    }

    /// <summary>Zera os slots e a Biomassa (testes).</summary>
    [ContextMenu("Debug/Zerar Slots e Biomassa")]
    public void EmptyBiomass()
    {
        storedSlots = 0;
        SetBiomass(0f);
    }

    private void SetBiomass(float value)
    {
        int maxSlots = MaxSlots;
        float clamped = storedSlots >= maxSlots ? 0f : Mathf.Clamp(value, 0f, MaxBiomass);
        int prevSlots = storedSlots;

        while (clamped >= MaxBiomass && storedSlots < maxSlots)
        {
            clamped -= MaxBiomass;
            storedSlots++;
        }
        if (storedSlots >= maxSlots)
        {
            clamped = 0f;
        }

        bool changed = !Mathf.Approximately(clamped, currentBiomass) || storedSlots != prevSlots;
        currentBiomass = clamped;
        if (!changed) return;

        if (storedSlots > prevSlots)
        {
            PlaySlotReadyAudio(storedSlots >= maxSlots);
        }

        OnBiomassChanged?.Invoke(currentBiomass);
        OnHealingStateChanged?.Invoke(storedSlots, maxSlots, currentBiomass);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        storedSlots = Mathf.Clamp(storedSlots, 0, UpgradedMaxSlots);
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

        // Caso a biomassa tenha chegado em 100% pelo Inspector, converte em slot antes de consumir
        if (currentBiomass >= MaxBiomass && storedSlots < MaxSlots)
        {
            SetBiomass(currentBiomass);
        }

        // Só permite curar com pelo menos 1 slot guardado
        if (storedSlots <= 0)
        {
            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.FlashEmptyWarning();
            }
            Debug.LogWarning($"⚠️ [SuitHealing] Nenhum slot pronto ({currentBiomass:F0}%/100%). Acerte golpes, mate inimigos ou faça Esquivas Perfeitas!");
            return;
        }

        // Não gasta o slot se a cura estiver bloqueada (ex: debuff que impede cura)
        if (!playerHealth.canHeal)
        {
            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.FlashEmptyWarning();
            }
            Debug.LogWarning("⚠️ [SuitHealing] Cura bloqueada no momento. Slot preservado.");
            return;
        }

        // Consome 1 slot guardado, preservando os demais slots e o progresso parcial da barra atual
        storedSlots = Mathf.Max(0, storedSlots - 1);
        OnBiomassChanged?.Invoke(currentBiomass);
        OnHealingStateChanged?.Invoke(storedSlots, MaxSlots, currentBiomass);

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

        Debug.Log($"💚 [SuitHealing] Slot consumido! Curado: +{healAmount} HP ({(pct * 100):F0}% da vida max) [{(inCombatHeal ? "COMBATE" : "DESCANSO")}]. Restam: {StoredSlots}/{MaxSlots} slots | Barra: {currentBiomass:F0}%");
    }

    private void CheckCombatState()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 25f);
        bool foundEnemy = false;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy") || hit.GetComponent<DummyHealth>() != null || hit.GetComponent<ShardSwarmHealth>() != null)
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

    private void PlaySlotReadyAudio(bool allFull)
    {
        EnsureAudioClipsLoaded();
        AudioClip clip = allFull ? (allCapsulesReadyClip ?? firstCapsuleReadyClip) : (firstCapsuleReadyClip ?? allCapsulesReadyClip);
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
                          !AreAllSlotsFull &&
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
