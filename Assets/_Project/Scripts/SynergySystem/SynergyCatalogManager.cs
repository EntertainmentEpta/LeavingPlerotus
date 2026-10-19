using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton que gerencia a descoberta de nós no catálogo de sinergias.
/// Quando um item é catalogado (CatalogoManager) ou um inimigo é registrado (BestiarioManager),
/// o sistema chama DiscoverNode(nodeID) para registrar a descoberta e disparar o evento.
/// </summary>
public class SynergyCatalogManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────

    private static SynergyCatalogManager _instance;
    public static SynergyCatalogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SynergyCatalogManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SynergyCatalogManager_Auto");
                    _instance = go.AddComponent<SynergyCatalogManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    // ── Dados ──────────────────────────────────────────────────

    /// <summary>
    /// IDs de nós já descobertos nesta sessão.
    /// </summary>
    [SerializeField] private List<string> discoveredNodeIDs = new List<string>();
    private HashSet<string> discoveredSet = new HashSet<string>();

    // ── Eventos ────────────────────────────────────────────────

    /// <summary>
    /// Disparado sempre que um nó novo é descoberto. O parâmetro é o nodeID.
    /// </summary>
    public event Action<string> OnNodeDiscovered;

    // ── Lifecycle ──────────────────────────────────────────────

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Sincroniza o HashSet com qualquer ID pré-populado no Inspector
            foreach (string id in discoveredNodeIDs)
                discoveredSet.Add(id);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LoadFromSave();
    }

    private bool saveCarregado = false;

    /// <summary>
    /// Carrega nós e sinergias descobertas de sessões anteriores via SaveManager.
    /// </summary>
    public void LoadFromSave()
    {
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null) return;
        saveCarregado = true;

        var cached = SaveManager.instance.CachedData;
        if (cached.discoveredSynergies != null)
        {
            foreach (string id in cached.discoveredSynergies)
            {
                if (!string.IsNullOrEmpty(id) && !discoveredSet.Contains(id))
                {
                    discoveredSet.Add(id);
                    if (!discoveredNodeIDs.Contains(id))
                        discoveredNodeIDs.Add(id);
                }
            }
        }
    }

    // ── API Pública ────────────────────────────────────────────

    /// <summary>
    /// Registra a descoberta de um nó ou receita de sinergia. Ignora IDs nulos, vazios ou já registrados.
    /// </summary>
    public void DiscoverNode(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID)) return;
        if (!saveCarregado) LoadFromSave();
        if (discoveredSet.Contains(nodeID)) return;

        discoveredSet.Add(nodeID);
        discoveredNodeIDs.Add(nodeID);

        // Persiste no save
        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            var cached = SaveManager.instance.CachedData;
            if (cached.discoveredSynergies == null) cached.discoveredSynergies = new List<string>();
            if (!cached.discoveredSynergies.Contains(nodeID))
            {
                cached.discoveredSynergies.Add(nodeID);
                SaveManager.instance.SavePersistentData();
            }
        }

        Debug.Log($"[SYNERGY CATALOG] Nó/Sinergia descoberto: {nodeID}");

        OnNodeDiscovered?.Invoke(nodeID);
    }

    /// <summary>
    /// Verifica se um nó ou receita de sinergia já foi descoberto.
    /// </summary>
    public bool IsNodeDiscovered(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID)) return false;
        if (!saveCarregado) LoadFromSave();
        return discoveredSet.Contains(nodeID);
    }

    /// <summary>
    /// Retorna a lista de IDs descobertos (somente leitura).
    /// </summary>
    public IReadOnlyList<string> GetDiscoveredIDs()
    {
        if (!saveCarregado) LoadFromSave();
        return discoveredNodeIDs;
    }
}
