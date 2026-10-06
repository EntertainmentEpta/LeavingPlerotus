using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Fase 3 do Boss Cromático — Moveset da Floresta Tóxica.
///
/// RESPONSABILIDADES:
///   • Spawn aleatório de poças ácidas sob o jogador em intervalos randomizados
///   • Acid Spit: o boss cospe um projétil ácido pela boca em direção ao jogador
///   • Controla o limite máximo de poças simultâneas na arena
///   • Usa AttackCooldownMultiplier do BossPhase3Modifiers automaticamente (se presente)
///
/// SETUP NO UNITY:
///   1. Adicione este componente no mesmo GameObject do BossController
///   2. Crie um Prefab de poça: GameObject vazio → CapsuleCollider (isTrigger=true) + AcidPuddle.cs
///   3. Arraste o prefab no campo "acidPuddlePrefab"
///   4. (Cuspida) Reutilize o projétil já existente: crie um Prefab com Rigidbody + Collider (isTrigger)
///      + CrystalSpikeProjectile.cs (dê a ele um visual/material ácido) e arraste em "acidSpitProjectilePrefab"
///   5. BossController é encontrado automaticamente (ou arraste manualmente)
///
/// DEPENDÊNCIAS:
///   • BossPhase3Modifiers (opcional, no mesmo GameObject) — aplica -50% ao cooldown dos ataques
///   • AcidPuddle.cs — script do prefab da poça
///   • CrystalSpikeProjectile.cs — projétil já existente, reutilizado pela cuspida de ácido
/// </summary>
public class BossPhase3AcidPuddles : MonoBehaviour
{
    // =====================================================
    // INSPECTOR
    // =====================================================

    [Header("Referências")]
    [Tooltip("Prefab da poça ácida. Deve ter CapsuleCollider (isTrigger=true) + AcidPuddle.cs.")]
    public GameObject acidPuddlePrefab;

    [Tooltip("BossController deste boss. Auto-encontrado se nulo.")]
    public BossController bossController;

    [Header("Spawn Aleatório — Sob o Player")]
    [Tooltip("Intervalo mínimo (segundos) entre spawns aleatórios de poças ácidas.")]
    public float spawnIntervalMin = 3f;

    [Tooltip("Intervalo máximo (segundos) entre spawns aleatórios de poças ácidas.")]
    public float spawnIntervalMax = 6f;

    [Header("Limite de Poças na Arena")]
    [Tooltip("Máximo de poças ácidas simultâneas. Novos spawns são bloqueados ao atingir o limite.")]
    [Range(1, 20)]
    public int maxSimultaneousPuddles = 6;

    [Header("Debug")]
    public bool showDebugLog = true;

    // =====================================================
    // ESTADO INTERNO
    // =====================================================

    private Transform playerTransform;
    private BossPhase3Modifiers phase3Modifiers;
    private Coroutine randomSpawnCoroutine;
    private bool phase3Active = false;

    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================

    void Awake()
    {
        if (bossController == null)
            bossController = GetComponent<BossController>();

        phase3Modifiers = GetComponent<BossPhase3Modifiers>();

        // Fallback: carrega da pasta Resources se a referência se perder por override de prefab aninhado
        if (acidPuddlePrefab == null)
        {
            acidPuddlePrefab = Resources.Load<GameObject>("Acid Puddle VFX");
            if (acidPuddlePrefab != null)
                Debug.Log("[Phase3AcidPuddles] Prefab carregado via Resources.Load.");
            else
                Debug.LogError("[Phase3AcidPuddles] ❌ Prefab não encontrado em Resources/Acid Puddle VFX — mova o prefab para Assets/_Project/Resources/");
        }

        Debug.Log($"[Phase3AcidPuddles] Awake em '{gameObject.name}' (ID:{gameObject.GetInstanceID()}) | acidPuddlePrefab: {(acidPuddlePrefab != null ? acidPuddlePrefab.name : "NULL ⚠️")}");
    }

    void OnEnable()
    {
        BossEvents. OnPhaseChanged += OnPhaseChanged;
        BossEvents.OnBossDefeated += OnBossDefeated;
    }

    void OnDisable()
    {
        BossEvents.OnPhaseChanged -= OnPhaseChanged;
        BossEvents.OnBossDefeated -= OnBossDefeated;
        StopAllPhase3Coroutines();
    }

    // =====================================================
    // HANDLERS DE EVENTO
    // =====================================================

    private void OnPhaseChanged(int newPhase)
    {
        Debug.Log($"[Phase3AcidPuddles] OnPhaseChanged({newPhase}) recebido.");
        if (newPhase == 3)
            ActivatePhase3();
    }

    private void OnBossDefeated()
    {
        StopAllPhase3Coroutines();
        AcidPuddle.ResetStaticCounters();
    }

    // =====================================================
    // ATIVAÇÃO
    // =====================================================

    private void ActivatePhase3()
    {
        if (phase3Active) return;
        phase3Active = true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        else
            Debug.LogWarning("[Phase3AcidPuddles] ⚠️ Player não encontrado pela tag 'Player'!");

        randomSpawnCoroutine = StartCoroutine(RandomPuddleSpawnRoutine());

        if (showDebugLog)
            Debug.Log("[Phase3AcidPuddles] 🧪 Sistema de poças ácidas ativado!");
    }

    private void StopAllPhase3Coroutines()
    {
        phase3Active = false;

        if (randomSpawnCoroutine != null)
        {
            StopCoroutine(randomSpawnCoroutine);
            randomSpawnCoroutine = null;
        }
    }

    // =====================================================
    // SPAWN ALEATÓRIO — SOB O PLAYER
    // =====================================================

    private IEnumerator RandomPuddleSpawnRoutine()
    {
        // Beat inicial antes de começar
        yield return new WaitForSeconds(spawnIntervalMin);

        while (phase3Active)
        {
            float interval = Random.Range(spawnIntervalMin, spawnIntervalMax);
            if (showDebugLog) Debug.Log($"[Phase3AcidPuddles] Próxima poça em {interval:F1}s | Ativas: {AcidPuddle.ActiveCount}/{maxSimultaneousPuddles} | PlayerNull: {playerTransform == null}");
            yield return new WaitForSeconds(interval);

            if (!phase3Active) yield break;
            if (IsBossPaused()) { Debug.Log("[Phase3AcidPuddles] Boss pausado, pulando spawn."); continue; }

            SpawnPuddleAt(GetPlayerPosition());
        }
    }

    // =====================================================
    // SPAWNER
    // =====================================================

    private void SpawnPuddleAt(Vector3 position)
    {
        Debug.Log($"[Phase3AcidPuddles] SpawnPuddleAt chamado | obj='{gameObject.name}'(ID:{gameObject.GetInstanceID()}) | prefab={( acidPuddlePrefab != null ? acidPuddlePrefab.name : "NULL")} | Ativas={AcidPuddle.ActiveCount}/{maxSimultaneousPuddles} | pos={position}");

        if (acidPuddlePrefab == null)
        {
            Debug.LogWarning("[Phase3AcidPuddles] ⚠️ acidPuddlePrefab não atribuído no Inspector!");
            return;
        }

        if (AcidPuddle.ActiveCount >= maxSimultaneousPuddles)
        {
            if (showDebugLog)
                Debug.Log($"[Phase3AcidPuddles] Cap atingido ({maxSimultaneousPuddles} poças). Spawn ignorado.");
            return;
        }

        GameObject spawned = Instantiate(acidPuddlePrefab, position, Quaternion.identity);
        AcidPuddle puddle = spawned.GetComponentInChildren<AcidPuddle>();
        if (puddle == null) Debug.LogWarning("[Phase3AcidPuddles] ⚠️ AcidPuddle script não encontrado no prefab ou seus filhos!");

        if (showDebugLog)
            Debug.Log($"[Phase3AcidPuddles] 🟢 Poça spawnou em {position} | Ativas: {AcidPuddle.ActiveCount + 1}/{maxSimultaneousPuddles}");
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private bool IsBossPaused()
    {
        if (bossController == null) return false;
        return bossController.IsStunned || bossController.IsDead;
    }

    private Vector3 GetPlayerPosition()
    {
        if (playerTransform == null) return transform.position;
        return playerTransform.position;
    }
}
