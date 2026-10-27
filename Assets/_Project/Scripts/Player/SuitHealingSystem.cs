using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de Cura do Traje do Eptinho (Mecânica Central de Sobrevivência).
/// - 2 Cápsulas de cura no traje (cada uma cura inicialmente 20% da vida máxima).
/// - Eptinho sintetiza novas cargas com o tempo (preenchendo a cápsula vazia de baixo para cima).
/// - Integridade do traje: tomar dano descalibra o traje e desacelera a síntese.
/// - Clean Room (Sair Ileso da fase): restaura o traje para 100% e sintetiza +1 poção imediatamente.
/// - Sala de Cura / Descanso: sintetiza 4x mais rápido e cura 40% da vida (em vez de 20% em combate).
/// - Esquiva Perfeita (Q): Pressionar Q enquanto a Sobrecarga estiver cheia consome a barra e injeta +30s na síntese da cura!
/// </summary>
public class SuitHealingSystem : MonoBehaviour
{
    public static SuitHealingSystem Instance { get; private set; }

    [Header("Cargas de Cápsulas")]
    public int maxPotions = 2;
    public int currentPotions = 2;

    [Header("Eficácia de Cura (% da Vida Máxima)")]
    [Tooltip("Percentual da vida curada durante combate ativo (padrão: 0.20 = 20%)")]
    public float combatHealPercent = 0.20f;
    [Tooltip("Percentual da vida curada em salas de descanso / seguras (padrão: 0.40 = 40%)")]
    public float safeRoomHealPercent = 0.40f;

    [Header("Síntese do Eptinho")]
    [Tooltip("Tempo base em segundos para sintetizar 1 cápsula de cura")]
    public float baseSynthesisDuration = 60f;
    private float currentSynthesisTimer = 0f;

    [Header("Integridade do Traje")]
    [Range(0.4f, 1.0f)]
    public float suitIntegrity = 1.0f; // 1.0 = 100%, tomar dano reduz e desacelera síntese

    [Header("Controles")]
    public KeyCode healKey = KeyCode.Q;

    // Componentes e Estado
    private PlayerHealth playerHealth;
    private bool tookDamageInCurrentRoom = false;
    private bool isInCombat = false;
    private float combatCheckTimer = 0f;

    public float SynthesisProgress => Mathf.Clamp01(currentSynthesisTimer / Mathf.Max(1f, baseSynthesisDuration));
    public float RemainingSynthesisTime => Mathf.Max(0f, baseSynthesisDuration - currentSynthesisTimer);
    public bool IsInCombat => isInCombat;
    public float SuitIntegrityPercent => suitIntegrity * 100f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>();

        // Registra listeners de dano e salas
        PlayerHealth.OnPlayerDamaged += OnPlayerTookDamage;
        RoomController.OnRoomCleared += OnRoomCleared;

        // Garante que o UI do Traje seja criado
        if (FindFirstObjectByType<SuitHealingUI>() == null)
        {
            GameObject uiObj = new GameObject("SuitHealingUI_Auto");
            uiObj.AddComponent<SuitHealingUI>();
        }
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

        // 1. Checa estado de combate a cada 0.5s (procura inimigos ativos)
        combatCheckTimer += Time.deltaTime;
        if (combatCheckTimer >= 0.5f)
        {
            combatCheckTimer = 0f;
            CheckCombatState();
        }

        // 2. Síntese contínua de cápsula pelo Eptinho (se tiver menos que o máximo)
        if (currentPotions < maxPotions)
        {
            // Em salas de descanso/sem inimigos, a síntese é 4x mais veloz!
            float safeRoomMultiplier = isInCombat ? 1.0f : 4.0f;
            // Integridade do traje acelera ou desacelera o processo
            float rate = safeRoomMultiplier * suitIntegrity;

            currentSynthesisTimer += Time.deltaTime * rate;

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

        // 3. Input de cura ou aceleração de síntese (Tecla Q)
        if (Input.GetKeyDown(healKey))
        {
            HandleHealKeyInput();
        }
    }

    private void HandleHealKeyInput()
    {
        bool consumedSobrecarga = false;

        // Opção C: Se estiver com a Sobrecarga do Perfect Dash ativa, consome a barra e injeta +30s na síntese!
        DashM dash = DashM.Instance ?? GetComponent<DashM>() ?? GetComponentInParent<DashM>();
        if (dash != null && dash.HasPerfectDodgeCharge)
        {
            dash.ConsumePerfectDodgeCharge();
            AccelerateSynthesis(30f);
            consumedSobrecarga = true;
        }

        // Se o player puder curar e tiver cápsula disponível, consome a cápsula e cura 20% HP
        if (playerHealth != null && !playerHealth.isDead && currentPotions > 0 && playerHealth.currentHealth < playerHealth.maxHealth)
        {
            currentPotions--;

            int maxHp = playerHealth.maxHealth > 0 ? playerHealth.maxHealth : 100;
            float pct = isInCombat ? combatHealPercent : safeRoomHealPercent;
            int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHp * pct));

            playerHealth.Heal(healAmount);
            TriggerHealVFX();

            Debug.Log($"💚 [SuitHealing] Cápsula utilizada! Curado: +{healAmount} HP ({(pct * 100):F0}% da vida max). Restam: {currentPotions}/{maxPotions}");
        }
        else if (!consumedSobrecarga && currentPotions <= 0)
        {
            Debug.LogWarning("⚠️ [SuitHealing] Nenhuma cápsula disponível! Aguarde a síntese do Eptinho.");
        }
    }

    public void AccelerateSynthesis(float seconds)
    {
        if (currentPotions < maxPotions)
        {
            currentSynthesisTimer += seconds;
            Debug.Log($"⚡ [SuitHealing] SOBRECARGA DO PERFECT DASH CONSUMIDA! Síntese acelerada em +{seconds}s!");
            if (currentSynthesisTimer >= baseSynthesisDuration)
            {
                currentSynthesisTimer = 0f;
                currentPotions = Mathf.Min(maxPotions, currentPotions + 1);
            }
        }
        else
        {
            Debug.Log("🧪 [SuitHealing] Todas as cápsulas de cura já estão cheias!");
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
        // Dano danifica o traje e desacelera síntese (mínimo de 0.4x)
        suitIntegrity = Mathf.Max(0.40f, suitIntegrity - 0.15f);
        Debug.Log($"⚠️ [SuitHealing] Traje danificado! Integridade atual: {SuitIntegrityPercent:F0}%. Síntese desacelerada.");
    }

    private void OnRoomCleared(RoomController room)
    {
        Debug.Log("🚪 [SuitHealing] Sala concluída!");

        // Se saiu ileso da fase (Clean Room):
        if (!tookDamageInCurrentRoom)
        {
            Debug.Log("⭐ [SuitHealing] CLEAN ROOM! Jogador saiu ileso! Traje calibrado ao máximo e bônus de cura concedido.");
            suitIntegrity = 1.0f; // Restaura integridade máxima do traje

            if (currentPotions < maxPotions)
            {
                currentPotions++;
                currentSynthesisTimer = 0f;
            }
            else
            {
                currentSynthesisTimer = baseSynthesisDuration * 0.5f;
            }
        }
        else
        {
            suitIntegrity = Mathf.Min(1.0f, suitIntegrity + 0.20f);
        }

        tookDamageInCurrentRoom = false;
    }

    /// <summary>
    /// Bônus acionado pelo Perfect Dash (Esquiva Perfeita): gera energia cinética e avança a síntese em +15s.
    /// </summary>
    public void OnPerfectDodgeBonus(float bonusSeconds = 15f)
    {
        AccelerateSynthesis(bonusSeconds);
    }

    private void TriggerHealVFX()
    {
        GameObject healFxObj = new GameObject("HealBurstFX");
        healFxObj.transform.position = transform.position + Vector3.up * 1.0f;
        Destroy(healFxObj, 1.2f);
    }
}
