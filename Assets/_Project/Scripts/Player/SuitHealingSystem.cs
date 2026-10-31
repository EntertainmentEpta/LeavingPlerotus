using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de Cura do Traje do Eptinho (Mecânica Central de Sobrevivência).
/// - 2 Cápsulas de cura no traje.
/// - Eficácia: 15% HP em combate ativo, 30% HP fora de combate / descanso.
/// - Síntese contínua: exatamente 60s por cápsula.
/// - Aceleração / Desaceleração:
///   * Tomar dano desacelera a síntese para 50% (taxa 0.5x por 8s).
///   * Clean Room (sair ileso da fase) avança +20s na barra.
///   * Perfect Dash (Q) injeta +30s na síntese.
/// </summary>
public class SuitHealingSystem : MonoBehaviour
{
    public static SuitHealingSystem Instance { get; private set; }

    [Header("Cargas de Cápsulas")]
    public int maxPotions = 2;
    [Tooltip("Inicia com 1 cápsula pronta e a 2ª sintetizando para o jogador ver a barra crescer.")]
    public int currentPotions = 1;

    [Header("Eficácia de Cura (% da Vida Máxima)")]
    [Tooltip("Percentual da vida curada durante combate ativo (padrão: 0.15 = 15%)")]
    public float combatHealPercent = 0.15f;
    [Tooltip("Percentual da vida curada fora de combate / salas de descanso (padrão: 0.30 = 30%)")]
    public float safeRoomHealPercent = 0.30f;

    [Header("Síntese do Eptinho")]
    [Tooltip("Tempo base em segundos para sintetizar 1 cápsula de cura (1 minuto = 60s)")]
    public float baseSynthesisDuration = 60f;
    private float currentSynthesisTimer = 0f;

    [Header("Dinâmica de Síntese")]
    private float damageSlowdownTimer = 0f;

    [Header("Integridade do Traje")]
    [Range(0.4f, 1.0f)]
    public float suitIntegrity = 1.0f;

    [Header("Controles")]
    public KeyCode healKey = KeyCode.Q;

    // Componentes e Estado
    private PlayerHealth playerHealth;
    private bool tookDamageInCurrentRoom = false;
    private bool isInCombat = false;
    public bool isInAnomalyRoom = false;
    private float combatCheckTimer = 0f;

    public float SynthesisProgress => Mathf.Clamp01(currentSynthesisTimer / Mathf.Max(1f, baseSynthesisDuration));
    public float RemainingSynthesisTime => Mathf.Max(0f, baseSynthesisDuration - currentSynthesisTimer);
    public float ElapsedSynthesisTime => Mathf.Clamp(currentSynthesisTimer, 0f, baseSynthesisDuration);
    public bool IsInCombat => isInCombat;
    public float SuitIntegrityPercent => suitIntegrity * 100f;
    public bool IsSlowedByDamage => damageSlowdownTimer > 0f;

    public float CurrentSynthesisRate
    {
        get
        {
            if (damageSlowdownTimer > 0f) return 0.50f; // Tomou dano: desacelera para 50%
            if (isInAnomalyRoom) return 1.50f;
            if (!isInCombat) return 1.25f;              // Fora de combate / descanso: 1.25x
            return 1.0f;                                // Em combate padrão: 1.0x
        }
    }

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
            healKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey);
        }
        catch
        {
            healKey = KeyCode.Q;
        }

        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();

        PlayerHealth.OnPlayerDamaged += OnPlayerTookDamage;
        RoomController.OnRoomCleared += OnRoomCleared;

        if (FindFirstObjectByType<SuitHealingUI>() == null)
        {
            GameObject uiObj = new GameObject("SuitHealingUI_Auto");
            DontDestroyOnLoad(uiObj);
            uiObj.AddComponent<SuitHealingUI>();
        }
    }

    /// <summary>
    /// Chamado ao morrer ou ao respawnar/acordar na Base para garantir que a síntese de cura nunca fique travada.
    /// </summary>
    public void ResetOnDeathOrRespawn()
    {
        damageSlowdownTimer = 0f;
        suitIntegrity = 1.0f;
        isInCombat = false;
        isInAnomalyRoom = false;
        currentPotions = Mathf.Max(1, currentPotions);
        currentSynthesisTimer = 0f;
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? FindFirstObjectByType<PlayerHealth>();
        Debug.Log($"🧪 [SuitHealing] Sistema de cura resetado com sucesso pós-morte/respawn! Cápsulas: {currentPotions}/{maxPotions}");
    }

    private void OnDestroy()
    {
        PlayerHealth.OnPlayerDamaged -= OnPlayerTookDamage;
        RoomController.OnRoomCleared -= OnRoomCleared;
    }

    private void Update()
    {
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused) return;
        if (CheatConsole.IsOpen) return;
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) return;
        if (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen) return;

        combatCheckTimer += Time.deltaTime;
        if (combatCheckTimer >= 0.5f)
        {
            combatCheckTimer = 0f;
            CheckCombatState();
        }

        if (damageSlowdownTimer > 0f)
        {
            damageSlowdownTimer -= Time.deltaTime;
        }

        // Atualiza limite máximo de cápsulas com base em melhorias do robozinho
        int targetMax = 2;
        if (EquipmentManager.Instance != null && (EquipmentManager.Instance.IsEquipped("equip_reservatorio_cura") || EquipmentManager.Instance.IsOwned("equip_reservatorio_cura")))
        {
            targetMax = 3;
        }
        maxPotions = targetMax;

        // Síntese de cápsulas (se tiver menos que o máximo)
        if (currentPotions < maxPotions)
        {
            currentSynthesisTimer += Time.deltaTime * CurrentSynthesisRate;

            if (currentSynthesisTimer >= baseSynthesisDuration)
            {
                currentSynthesisTimer = 0f;
                currentPotions = Mathf.Min(maxPotions, currentPotions + 1);
                Debug.Log($"🧪 [SuitHealing] Eptinho sintetizou +1 cápsula de cura! Total: {currentPotions}/{maxPotions}");
            }
        }
        else
        {
            currentSynthesisTimer = 0f;
        }

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
        bool hadSobrecarga = false;
        DashM dash = DashM.Instance ?? GetComponent<DashM>() ?? GetComponentInParent<DashM>();
        if (dash != null && dash.HasPerfectDodgeCharge)
        {
            hadSobrecarga = true;
            dash.ConsumePerfectDodgeCharge();
        }

        bool usedPotion = false;

        // Se o jogador estiver vivo e com cápsula disponível:
        if (playerHealth != null && !playerHealth.isDead && currentPotions > 0)
        {
            currentPotions--;
            usedPotion = true;

            int maxHp = playerHealth.maxHealth > 0 ? playerHealth.maxHealth : 100;
            float pct = (isInCombat && !isInAnomalyRoom) ? combatHealPercent : safeRoomHealPercent;
            int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHp * pct));

            playerHealth.Heal(healAmount);
            TriggerHealVFX();

            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.TriggerHealFeedback();
            }

            Debug.Log($"💚 [SuitHealing] Cápsula utilizada! Curado: +{healAmount} HP ({(pct * 100):F0}% da vida max). Restam: {currentPotions}/{maxPotions}");
        }

        // Se o jogador utilizou a Sobrecarga do Perfect Dash, injeta +30s de Síntese
        // (aproveitando imediatamente o slot liberado se uma poção foi gasta, ou preenchendo a síntese atual!)
        if (hadSobrecarga)
        {
            AccelerateSynthesis(30f);
            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.TriggerSobrecargaConsumedFeedback();
            }
        }
        else if (!usedPotion && currentPotions <= 0)
        {
            if (SuitHealingUI.Instance != null)
            {
                SuitHealingUI.Instance.FlashEmptyWarning();
            }
            Debug.LogWarning("⚠️ [SuitHealing] Nenhuma cápsula disponível! Aguarde a síntese do Eptinho.");
        }
    }

    public void AccelerateSynthesis(float seconds)
    {
        if (currentPotions < maxPotions)
        {
            currentSynthesisTimer += seconds;
            Debug.Log($"⚡ [SuitHealing] Síntese acelerada em +{seconds}s! Progresso atual: {currentSynthesisTimer:F1}/{baseSynthesisDuration:F0}s");
            if (currentSynthesisTimer >= baseSynthesisDuration)
            {
                currentSynthesisTimer = 0f;
                currentPotions = Mathf.Min(maxPotions, currentPotions + 1);
            }
        }
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
        damageSlowdownTimer = 8.0f; // Tomar dano desacelera a síntese por 8 segundos
        suitIntegrity = Mathf.Max(0.40f, suitIntegrity - 0.15f);
        Debug.Log($"⚠️ [SuitHealing] Dano recebido! Síntese desacelerada (0.5x por 8s). Integridade: {SuitIntegrityPercent:F0}%");

        if (!EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_HEALING_TUTORIAL))
        {
            EptinhoStoryDialogs.TriggerHealingTutorial();
        }
    }

    private void OnRoomCleared(RoomController room)
    {
        Debug.Log("🚪 [SuitHealing] Sala concluída!");

        // Se saiu ileso da fase (Clean Room):
        if (!tookDamageInCurrentRoom)
        {
            Debug.Log("⭐ [SuitHealing] CLEAN ROOM! Jogador saiu ileso! +20s de avanço imediato na síntese!");
            suitIntegrity = 1.0f;
            damageSlowdownTimer = 0f;
            AccelerateSynthesis(20f);
        }
        else
        {
            suitIntegrity = Mathf.Min(1.0f, suitIntegrity + 0.20f);
        }

        tookDamageInCurrentRoom = false;
    }

    public void OnEnemyKilled()
    {
        AccelerateSynthesis(1.5f);
    }

    public void OnPerfectDodgeBonus(float bonusSeconds = 15f)
    {
        AccelerateSynthesis(bonusSeconds);
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
}
