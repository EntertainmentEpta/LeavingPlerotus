using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de Cura do Traje do Eptinho (Mecânica Central de Sobrevivência).
/// - 2 Cargas de Poção/Injetor no traje.
/// - Eptinho sintetiza novas cargas ao longo do tempo.
/// - Integridade do traje: tomar dano descalibra o traje e desacelera a síntese.
/// - Clean Room (Sair Ileso da fase): restaura o traje e sintetiza +1 poção imediatamente.
/// - Sala de Cura / Descanso: sintetiza 4x mais rápido e curar restaura 70 HP (em vez de 35 HP em combate).
/// - Esquiva Perfeita: gera energia cinética que avança em +15s a síntese da poção.
/// </summary>
public class SuitHealingSystem : MonoBehaviour
{
    public static SuitHealingSystem Instance { get; private set; }

    [Header("Cargas de Poção")]
    public int maxPotions = 2;
    public int currentPotions = 2;

    [Header("Eficácia de Cura")]
    [Tooltip("Quantidade de HP curada durante combate ativo")]
    public int combatHealAmount = 35;
    [Tooltip("Quantidade de HP curada em salas de descanso / seguras")]
    public int safeRoomHealAmount = 70;

    [Header("Síntese do Eptinho")]
    [Tooltip("Tempo base em segundos para sintetizar 1 frasco de cura")]
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

        // 2. Síntese contínua de poção pelo Eptinho (se tiver menos que o máximo)
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
                Debug.Log($"🧪 [SuitHealing] Eptinho sintetizou +1 frasco de cura! Total: {currentPotions}/{maxPotions}");
            }
        }
        else
        {
            currentSynthesisTimer = 0f;
        }

        // 3. Input de cura (Tecla Q)
        if (Input.GetKeyDown(healKey))
        {
            TryUseHealingPotion();
        }
    }

    public void TryUseHealingPotion()
    {
        if (playerHealth == null) return;
        if (playerHealth.isDead) return;

        if (currentPotions <= 0)
        {
            Debug.LogWarning("⚠️ [SuitHealing] Sem cargas de poção! Aguarde o Eptinho sintetizar.");
            return;
        }

        if (playerHealth.currentHealth >= playerHealth.maxHealth)
        {
            Debug.Log("⚠️ [SuitHealing] Vida já está cheia!");
            return;
        }

        // Consome 1 poção
        currentPotions--;

        // Determina quantidade de cura (em sala segura cura o dobro!)
        int healAmount = isInCombat ? combatHealAmount : safeRoomHealAmount;
        playerHealth.Heal(healAmount);

        // Feedback sonoro e visual
        TriggerHealVFX();

        Debug.Log($"💚 [SuitHealing] Poção utilizada! Curado: +{healAmount} HP (Modo: {(isInCombat ? "Combate" : "Sala de Descanso")}). Restam: {currentPotions}/{maxPotions}");
    }

    private void CheckCombatState()
    {
        // Se houver inimigos vivos em um raio de 25m, considera combate ativo
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
                // Já cheio: ganha bônus de progresso para a próxima
                currentSynthesisTimer = baseSynthesisDuration * 0.5f;
            }
        }
        else
        {
            // Recupera um pouco da integridade ao avançar de sala
            suitIntegrity = Mathf.Min(1.0f, suitIntegrity + 0.20f);
        }

        tookDamageInCurrentRoom = false;
    }

    /// <summary>
    /// Bônus acionado pelo Perfect Dash (Esquiva Perfeita): gera energia cinética e avança a síntese em +15s.
    /// </summary>
    public void OnPerfectDodgeBonus(float bonusSeconds = 15f)
    {
        if (currentPotions < maxPotions)
        {
            currentSynthesisTimer += bonusSeconds;
            Debug.Log($"⚡ [SuitHealing] Perfect Dash acelerou a síntese do Eptinho em +{bonusSeconds}s!");
            if (currentSynthesisTimer >= baseSynthesisDuration)
            {
                currentSynthesisTimer = 0f;
                currentPotions = Mathf.Min(maxPotions, currentPotions + 1);
            }
        }
    }

    private void TriggerHealVFX()
    {
        // Spawna partículas de cura simples ao redor do player
        GameObject healFxObj = new GameObject("HealBurstFX");
        healFxObj.transform.position = transform.position + Vector3.up * 1.0f;
        Destroy(healFxObj, 1.2f);
    }
}
