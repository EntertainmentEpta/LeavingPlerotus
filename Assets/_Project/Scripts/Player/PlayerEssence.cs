using UnityEngine;

/// <summary>
/// Componente do Player para armazenar essência coletada
/// </summary>
public class PlayerEssence : MonoBehaviour
{
    [Header("Essência")]
    public int currentEssence = 0;
    public int totalEssenceCollected = 0;

    [Header("Teste no Editor")]
    [Tooltip("Preserva a essência inicial configurada no Inspector durante testes no Editor.")]
    public bool preserveInitialEssenceInEditor = false;

    [Header("Eventos (Opcional)")]
    [Tooltip("Evento disparado quando coleta essência")]
    public UnityEngine.Events.UnityEvent<int> onEssenceCollected;
    public UnityEngine.Events.UnityEvent<int> onEssenceChanged;

    /// <summary>
    /// Adiciona essência ao player
    /// </summary>
    public void AddEssence(int amount)
    {
        currentEssence += amount;
        totalEssenceCollected += amount;

        RunStatsManager.Instance?.RecordEssenceCollected(amount);

        Debug.Log("[PLAYER ESSENCE] +" + amount + " | Total: " + currentEssence);

        onEssenceCollected?.Invoke(amount);
        onEssenceChanged?.Invoke(currentEssence);
    }

    /// <summary>
    /// Gasta essência (para upgrades, etc)
    /// </summary>
    public bool SpendEssence(int amount)
    {
        if (currentEssence >= amount)
        {
            currentEssence -= amount;
            RunStatsManager.Instance?.RecordEssenceSpent(amount);
            Debug.Log("[PLAYER ESSENCE] -" + amount + " | Restante: " + currentEssence);
            onEssenceChanged?.Invoke(currentEssence);
            return true;
        }

        Debug.Log("[PLAYER ESSENCE] Essência insuficiente! Precisa: " + amount + " | Tem: " + currentEssence);
        return false;
    }

    /// <summary>
    /// Retorna a quantidade atual de essência
    /// </summary>
    public int GetEssence()
    {
        return currentEssence;
    }

    public void ResetRunEssence()
    {
        if (Application.isEditor && preserveInitialEssenceInEditor)
            return;

        currentEssence = 0;
        onEssenceChanged?.Invoke(0);
    }
}
