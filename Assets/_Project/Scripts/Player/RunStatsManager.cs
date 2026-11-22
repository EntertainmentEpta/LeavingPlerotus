using UnityEngine;
using System;

/// <summary>
/// =================================================================================
/// RASTREADOR DE ESTATÍSTICAS DA RUN & TIMER DO MODO ENDLESS (SPEEDRUN)
/// =================================================================================
/// Desenvolvido para: RogueLikeProject
/// 
/// Métricas Rastradas:
/// 1. Tempo Sobrevivido (Speedrun Timer)
/// 2. Dano Total Causado a Inimigos
/// 3. Dano Total Recebido pelo Jogador
/// 4. Inimigos/Mobs Derrotados
/// 5. Essências Coletadas na Run
/// 6. Essências Gastas (Mercador/Crafting)
/// 7. Fase/Local da Morte
/// =================================================================================
/// </summary>
public class RunStatsManager : MonoBehaviour
{
    public static RunStatsManager Instance { get; private set; }

    public const string EndlessBestLevelKey = "EndlessBestLevel";
    public const string EndlessBestTimeKey = "EndlessBestTime";

    [Header("Estado da Run")]
    public bool isRunActive = false;
    public bool isEndlessMode = false;
    public float survivalTimer = 0f;

    [Header("Recordes do Modo Endless (PlayerPrefs)")]
    public int BestEndlessLevel { get; private set; }
    public float BestEndlessTime { get; private set; }
    public bool HasBeatenBestLevelThisRun { get; private set; }
    public bool HasBeatenBestTimeThisRun { get; private set; }

    private int runStartBestLevel = 0;
    private float runStartBestTime = 0f;

    [Header("Métricas Acumuladas")]
    public long totalDamageDealt = 0;
    public long totalDamageTaken = 0;
    public int totalMobsKilled = 0;
    public int totalEssenceCollected = 0;
    public int totalEssenceSpent = 0;
    public string deathStage = "Base";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("RunStatsManager_AutoInit");
            DontDestroyOnLoad(go);
            go.AddComponent<RunStatsManager>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadEndlessRecords();
    }

    public void LoadEndlessRecords()
    {
        BestEndlessLevel = PlayerPrefs.GetInt(EndlessBestLevelKey, 0);
        BestEndlessTime = PlayerPrefs.GetFloat(EndlessBestTimeKey, 0f);
    }

    void Update()
    {
        if (isRunActive)
        {
            survivalTimer += Time.deltaTime;
        }
    }

    public void StartRunTracking(bool endless = false)
    {
        ResetStats();
        LoadEndlessRecords();
        runStartBestLevel = BestEndlessLevel;
        runStartBestTime = BestEndlessTime;
        HasBeatenBestLevelThisRun = false;
        HasBeatenBestTimeThisRun = false;

        isRunActive = true;
        isEndlessMode = endless;
        survivalTimer = 0f;
        Debug.Log($"⏱️ [RUN STATS] Rastreamento iniciado! Modo Endless: {isEndlessMode}");
    }

    /// <summary>
    /// Promove uma run já em andamento (ex: Fenda Pós-Boss) para o Modo Endless sem resetar tempo nem estatísticas.
    /// </summary>
    public void PromoteToEndlessMode(int currentLevel)
    {
        LoadEndlessRecords();
        if (!isEndlessMode)
        {
            runStartBestLevel = BestEndlessLevel;
            runStartBestTime = BestEndlessTime;
            HasBeatenBestLevelThisRun = false;
            HasBeatenBestTimeThisRun = false;
        }

        isEndlessMode = true;
        isRunActive = true;
        CommitEndlessRecord(currentLevel);
        Debug.Log($"♾️ [RUN STATS] Run promovida para Modo Endless no Nível {currentLevel} (tempo preservado: {FormatTime(survivalTimer)}).");
    }

    public void StopRunTracking()
    {
        if (isEndlessMode && RunManager.instance != null)
        {
            CommitEndlessRecord(RunManager.instance.currentLevel);
        }
        isRunActive = false;
        Debug.Log($"⏱️ [RUN STATS] Rastreamento parado. Tempo final: {FormatTime(survivalTimer)}");
    }

    /// <summary>
    /// Atualiza e persiste via PlayerPrefs o maior nível e o melhor tempo no Modo Endless.
    /// </summary>
    public bool CommitEndlessRecord(int levelReached)
    {
        bool endlessActive = isEndlessMode || (RunManager.instance != null && RunManager.instance.isEndlessMode);
        if (!endlessActive) return false;

        bool changed = false;

        if (levelReached > BestEndlessLevel)
        {
            BestEndlessLevel = levelReached;
            PlayerPrefs.SetInt(EndlessBestLevelKey, BestEndlessLevel);
            HasBeatenBestLevelThisRun = true;
            changed = true;
        }

        if (survivalTimer > BestEndlessTime)
        {
            BestEndlessTime = survivalTimer;
            PlayerPrefs.SetFloat(EndlessBestTimeKey, BestEndlessTime);
            HasBeatenBestTimeThisRun = true;
            changed = true;
        }

        if (changed)
        {
            PlayerPrefs.Save();
            Debug.Log($"🏆 [ENDLESS RECORDE] Novo recorde salvo! Nível: {BestEndlessLevel} | Tempo: {FormatTime(BestEndlessTime)}");
        }

        return changed;
    }

    public bool IsBeatingEndlessRecordLive(int currentLevel)
    {
        bool endlessActive = isEndlessMode || (RunManager.instance != null && RunManager.instance.isEndlessMode);
        if (!endlessActive) return false;

        if (HasBeatenBestLevelThisRun || HasBeatenBestTimeThisRun) return true;
        if (currentLevel > BestEndlessLevel && BestEndlessLevel > 0) return true;
        if (survivalTimer > BestEndlessTime && BestEndlessTime > 0f) return true;
        if (runStartBestLevel == 0 && runStartBestTime <= 0f && survivalTimer > 1f) return true;

        return false;
    }

    public int GetLiveBestEndlessLevel(int currentLevel)
    {
        bool endlessActive = isEndlessMode || (RunManager.instance != null && RunManager.instance.isEndlessMode);
        return endlessActive ? Mathf.Max(BestEndlessLevel, currentLevel) : BestEndlessLevel;
    }

    public float GetLiveBestEndlessTime()
    {
        bool endlessActive = isEndlessMode || (RunManager.instance != null && RunManager.instance.isEndlessMode);
        return endlessActive ? Mathf.Max(BestEndlessTime, survivalTimer) : BestEndlessTime;
    }

    public void ResetStats()
    {
        survivalTimer = 0f;
        totalDamageDealt = 0;
        totalDamageTaken = 0;
        totalMobsKilled = 0;
        totalEssenceCollected = 0;
        totalEssenceSpent = 0;
        deathStage = "Base";
    }

    public void RecordDamageDealt(long damage)
    {
        if (!isRunActive || damage <= 0) return;
        totalDamageDealt += damage;
    }

    public void RecordDamageTaken(long damage)
    {
        if (!isRunActive || damage <= 0) return;
        totalDamageTaken += damage;
    }

    public void RecordEnemyKilled()
    {
        if (!isRunActive) return;
        totalMobsKilled++;
    }

    public void RecordEssenceCollected(int amount)
    {
        if (!isRunActive || amount <= 0) return;
        totalEssenceCollected += amount;
    }

    public void RecordEssenceSpent(int amount)
    {
        if (!isRunActive || amount <= 0) return;
        totalEssenceSpent += amount;
    }

    public string FormatTime(float timeInSeconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(timeInSeconds);
        if (t.Hours > 0)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        }
        return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
    }

    public string FormatNumber(long number)
    {
        return number.ToString("N0");
    }
}
