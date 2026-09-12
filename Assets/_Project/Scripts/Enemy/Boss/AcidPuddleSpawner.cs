using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SETUP:
///   1. Adicione em qualquer GameObject da cena (sala do boss, corredor, mapa)
///   2. Arraste o prefab da poça ácida no campo Acid Puddle Prefab
///   3. Escolha o Spawn Mode e o Activation Mode
/// </summary>
public class AcidPuddleSpawner : MonoBehaviour
{
    public enum ActivationMode { BossPhase, OnStart, Manual }
    public enum SpawnMode { UnderPlayer, RandomInArena, Mixed }

    [Header("Prefab")]
    [Tooltip("Prefab com CapsuleCollider (isTrigger=true) + AcidPuddle.cs.")]
    public GameObject acidPuddlePrefab;

    [Header("Spawn")]
    public float spawnIntervalMin = 3f;
    public float spawnIntervalMax = 6f;

    [Range(1, 20)]
    public int maxSimultaneousPuddles = 6;

    [Tooltip("UnderPlayer: sempre sob o jogador. RandomInArena: posição aleatória na arena. Mixed: alterna aleatoriamente.")]
    public SpawnMode spawnMode = SpawnMode.UnderPlayer;

    [Header("Arena (RandomInArena / Mixed)")]
    [Tooltip("Centro da arena. Arraste o Arena_Floor ou Arena_Center aqui.")]
    public Transform arenaCenter;

    [Tooltip("Raio da área de spawn dentro da arena.")]
    public float arenaRadius = 20f;

    [Tooltip("Distância mínima entre duas poças spawnadas aleatoriamente.")]
    public float minDistanceBetween = 3f;

    [Header("Ativação")]
    public ActivationMode activationMode = ActivationMode.BossPhase;

    [Tooltip("0 = início da luta. 1/2/3 = transição de fase. Ignorado fora do modo BossPhase.")]
    [Range(0, 3)]
    public int activateOnPhase = 3;

    [Header("Debug")]
    public bool showDebugLog = true;

    // ── Estado ───────────────────────────────────────────────────────────────
    private Transform playerTransform;
    private Coroutine spawnCoroutine;
    private bool isActive;
    // Posições já usadas nesta sessão de spawn para respeitar minDistanceBetween
    private readonly List<Vector3> usedPositions = new List<Vector3>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (activationMode == ActivationMode.OnStart)
            Activate();
    }

    void OnEnable()
    {
        if (activationMode != ActivationMode.BossPhase) return;
        BossEvents.OnPhaseChanged     += OnPhaseChanged;
        BossEvents.OnBossFightStarted += OnFightStarted;
        BossEvents.OnBossDefeated     += OnBossDefeated;
    }

    void OnDisable()
    {
        if (activationMode != ActivationMode.BossPhase) return;
        BossEvents.OnPhaseChanged     -= OnPhaseChanged;
        BossEvents.OnBossFightStarted -= OnFightStarted;
        BossEvents.OnBossDefeated     -= OnBossDefeated;
    }

    void OnDestroy()
    {
        Deactivate();
    }

    // ── Handlers BossEvents ───────────────────────────────────────────────────
    private void OnFightStarted()
    {
        if (activateOnPhase == 0) Activate();
    }

    private void OnPhaseChanged(int newPhase)
    {
        if (activateOnPhase > 0 && newPhase == activateOnPhase) Activate();
        else if (activateOnPhase > 0 && newPhase != activateOnPhase) Deactivate();
    }

    private void OnBossDefeated()
    {
        Deactivate();
        AcidPuddle.ResetStaticCounters();
    }

    // ── API Pública ───────────────────────────────────────────────────────────
    public void Activate()
    {
        if (isActive) return;

        if (acidPuddlePrefab == null)
        {
            Debug.LogWarning("[AcidPuddleSpawner] Acid Puddle Prefab não atribuído!");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        else
            Debug.LogWarning("[AcidPuddleSpawner] Player não encontrado pela tag 'Player'!");

        isActive = true;
        spawnCoroutine = StartCoroutine(SpawnRoutine());

        if (showDebugLog)
            Debug.Log($"[AcidPuddleSpawner] Ativado em '{gameObject.name}' | modo: {activationMode}");
    }

    public void Deactivate()
    {
        isActive = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    // Spawn-on-demand para o projétil ácido do boss — ignora o loop temporizado.
    public void SpawnAtPosition(Vector3 worldPos)
    {
        if (acidPuddlePrefab == null) return;
        if (AcidPuddle.ActiveCount >= maxSimultaneousPuddles) return;

        Instantiate(acidPuddlePrefab, worldPos, Quaternion.identity);

        if (showDebugLog)
            Debug.Log($"[AcidPuddleSpawner] SpawnAtPosition em {worldPos} | Ativas: {AcidPuddle.ActiveCount + 1}/{maxSimultaneousPuddles}");
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────
    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(spawnIntervalMin);

        while (isActive)
        {
            float interval = Random.Range(spawnIntervalMin, spawnIntervalMax);

            if (showDebugLog)
                Debug.Log($"[AcidPuddleSpawner] Próxima poça em {interval:F1}s | Ativas: {AcidPuddle.ActiveCount}/{maxSimultaneousPuddles}");

            yield return new WaitForSeconds(interval);

            if (!isActive) yield break;

            if (AcidPuddle.ActiveCount >= maxSimultaneousPuddles)
            {
                if (showDebugLog)
                    Debug.Log($"[AcidPuddleSpawner] Cap atingido ({maxSimultaneousPuddles}). Spawn ignorado.");
                continue;
            }

            // Fallback para a posição do spawner se o player não for encontrado
            Vector3 pos = GetSpawnPosition();
            Instantiate(acidPuddlePrefab, pos, Quaternion.identity);
            usedPositions.Add(pos);

            if (showDebugLog)
                Debug.Log($"[AcidPuddleSpawner] Poça spawnou em {pos} | Ativas: {AcidPuddle.ActiveCount + 1}/{maxSimultaneousPuddles}");
        }
    }

    // ── Posição de Spawn ──────────────────────────────────────────────────────
    private Vector3 GetSpawnPosition()
    {
        bool useRandom = spawnMode == SpawnMode.RandomInArena ||
                        (spawnMode == SpawnMode.Mixed && Random.value > 0.5f);

        if (useRandom && arenaCenter != null)
            return GetRandomArenaPosition();

        return playerTransform != null ? playerTransform.position : transform.position;
    }

    private Vector3 GetRandomArenaPosition()
    {
        const int maxAttempts = 20;
        Vector3 center = arenaCenter.position;

        for (int i = 0; i < maxAttempts; i++)
        {
            float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(arenaRadius * 0.2f, arenaRadius * 0.95f);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            bool tooClose = false;
            foreach (Vector3 p in usedPositions)
            {
                if (Vector3.Distance(candidate, p) < minDistanceBetween) { tooClose = true; break; }
            }
            if (!tooClose) return candidate;
        }

        // Fallback se não achar posição válida
        return center + Random.insideUnitSphere * arenaRadius;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (spawnMode == SpawnMode.UnderPlayer) return;
        Vector3 center = arenaCenter != null ? arenaCenter.position : transform.position;
        Gizmos.color = new Color(0.6f, 1f, 0.1f, 0.2f);
        Gizmos.DrawSphere(center, arenaRadius);
        Gizmos.color = new Color(0.6f, 1f, 0.1f, 1f);
        Gizmos.DrawWireSphere(center, arenaRadius);
    }
#endif
}
