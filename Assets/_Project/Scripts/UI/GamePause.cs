using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Proprietário único de congelamento de tempo ("time is frozen").
/// Cada modal (diálogo, pause, loja, etc.) adquire um token.
/// O tempo só volta ao normal quando TODOS os tokens forem liberados,
/// restaurando a escala exata anterior ao primeiro congelamento (preserva slow-motion de bosses).
/// </summary>
public static class GamePause
{
    private static readonly List<Object> s_owners = new List<Object>(4);
    private static float s_resumeScale = 1f;

    public static bool IsPaused { get { Prune(); return s_owners.Count > 0; } }

    /// <summary>Modal mais recente no topo da pilha. Apenas ele consome inputs.</summary>
    public static Object TopOwner { get { Prune(); return s_owners.Count > 0 ? s_owners[s_owners.Count - 1] : null; } }

    public static void Acquire(Object owner)
    {
        if (owner == null || s_owners.Contains(owner)) return;
        if (s_owners.Count == 0) s_resumeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        s_owners.Add(owner);
        Time.timeScale = 0f;
    }

    public static void Release(Object owner)
    {
        if (!s_owners.Remove(owner)) return;
        Prune();
        if (s_owners.Count == 0) Time.timeScale = s_resumeScale;
    }

    /// <summary>Reset forçado (ex: retorno ao MainMenu ou reload global).</summary>
    public static void ResetAll()
    {
        s_owners.Clear();
        s_resumeScale = 1f;
        Time.timeScale = 1f;
    }

    // Donos destruídos sem chamar Release (troca de cena com diálogo aberto) aparecem como fake-null na Unity.
    private static void Prune() => s_owners.RemoveAll(o => o == null);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        s_owners.Clear();
        s_resumeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Garante que modais descartados na cena anterior não mantenham a nova cena congelada.
    private static void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Prune();
        if (s_owners.Count == 0 && Time.timeScale == 0f) Time.timeScale = s_resumeScale;
    }
}
