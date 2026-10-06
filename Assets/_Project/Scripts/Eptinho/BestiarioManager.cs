using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia o Bestiário do Eptinho. Rastreia inimigos encontrados PERMANENTEMENTE entre sessões.
/// Integrado com SaveManager para persistência em disco (semelhante ao player_progress).
/// </summary>
public class BestiarioManager : MonoBehaviour
{
    private static BestiarioManager _instancia;
    public static BestiarioManager instancia
    {
        get
        {
            if (_instancia == null)
            {
                _instancia = FindFirstObjectByType<BestiarioManager>();
                if (_instancia == null)
                {
                    GameObject go = new GameObject("BestiarioManager_Auto");
                    _instancia = go.AddComponent<BestiarioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instancia;
        }
        private set { _instancia = value; }
    }

    // Lista de EnemyData descobertos nesta sessão
    public List<EnemyData> inimigosEncontrados = new List<EnemyData>();
    private HashSet<string> nomesRegistrados = new HashSet<string>();

    void Awake()
    {
        if (_instancia == null)
        {
            _instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instancia != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        CarregarDoSave();
    }

    /// <summary>
    /// Carrega lista de inimigos já descobertos em sessões anteriores.
    /// </summary>
    private void CarregarDoSave()
    {
        if (SaveManager.instance == null) return;

        var dados = SaveManager.instance.CachedData;
        if (dados == null || dados.inimigosDescobertos == null) return;

        // Pré-popula o HashSet para evitar duplicatas ao registrar nesta sessão
        foreach (string nome in dados.inimigosDescobertos)
        {
            nomesRegistrados.Add(nome);
        }

        // Reconstrói lista visual a partir dos EnemyData existentes na pasta Resources
        EnemyData[] todos = Resources.LoadAll<EnemyData>("EnemyData");
        foreach (EnemyData data in todos)
        {
            if (data != null && nomesRegistrados.Contains(data.enemyName) && !inimigosEncontrados.Contains(data))
            {
                inimigosEncontrados.Add(data);
            }
        }

        // Também tenta pelo EnemyDataAutoLoader (dados criados em memória)
        if (EnemyDataAutoLoader.instancia != null)
        {
            foreach (EnemyData data in EnemyDataAutoLoader.instancia.GetAllEnemyData())
            {
                if (data != null && nomesRegistrados.Contains(data.enemyName) && !inimigosEncontrados.Contains(data))
                {
                    inimigosEncontrados.Add(data);
                }
            }
        }

        Debug.Log($"[BESTIÁRIO] Carregados {inimigosEncontrados.Count} inimigo(s) do save.");
    }

    /// <summary>
    /// Registra um inimigo no Bestiário. Se já descoberto (mesmo em sessões anteriores), ignora.
    /// </summary>
    public void Registrar(EnemyIdentity inimigo)
    {
        if (inimigo == null || inimigo.enemyData == null) return;
        if (nomesRegistrados.Contains(inimigo.enemyData.enemyName)) return;

        inimigo.foiEncontrado = true;
        inimigosEncontrados.Add(inimigo.enemyData);
        nomesRegistrados.Add(inimigo.enemyData.enemyName);

        // Persiste imediatamente no save
        if (SaveManager.instance != null)
        {
            var dados = SaveManager.instance.CachedData;
            if (dados != null && !dados.inimigosDescobertos.Contains(inimigo.enemyData.enemyName))
            {
                dados.inimigosDescobertos.Add(inimigo.enemyData.enemyName);
                SaveManager.instance.SavePersistentData();
            }
        }

        if (EptinhoPopupController.instancia != null)
            EptinhoPopupController.instancia.MostrarPopupInimigo(inimigo.enemyData);

        // Integração com o catálogo de sinergias
        if (!string.IsNullOrEmpty(inimigo.enemyData.synergyNodeID))
            SynergyCatalogManager.Instance.DiscoverNode(inimigo.enemyData.synergyNodeID);

        Debug.Log($"[BESTIÁRIO] Novo inimigo registrado e salvo: {inimigo.enemyData.enemyName}");
    }

    /// <summary>
    /// Verifica se um inimigo já foi descoberto (inclusive em sessões anteriores).
    /// </summary>
    public bool JaRegistrado(string nomeInimigo)
    {
        return nomesRegistrados.Contains(nomeInimigo);
    }
}
