using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia o Bestiário do Eptinho. Rastreia inimigos encontrados PERMANENTEMENTE entre sessões.
/// Integrado com SaveManager para persistência em disco (semelhante ao player_progress).
/// Inclui atalho de desenvolvedor (F1) para desbloquear instantaneamente todos os inimigos.
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

    /// <summary>
    /// Filtra inimigos de teste, depreciados ou que não devem constar no jogo oficial
    /// (ex: Pedra Mágica, Bismutado, Star, Dummy).
    /// </summary>
    public static bool IsInimigoInvalido(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return true;
        string n = nome.ToLower().Trim();
        return n.Contains("pedra") || n.Contains("magic") || n.Contains("stone") ||
               n.Contains("bismutado") ||
               n.Contains("star") ||
               n.Contains("dummy");
    }


    private void Awake()
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

    private void Start()
    {
        CarregarDoSave();
    }

    private void Update()
    {
        // Tecla de atalho de desenvolvedor: F1 desbloqueia TUDO do Oráculo Eptinho (Bestiário + Catálogo)
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (CheatConsole.Instance != null)
            {
                CheatConsole.Instance.DesbloquearTudoEptinho();
            }
            else
            {
                DesbloquearTodosInimigos();
                if (CatalogoManager.instancia != null) CatalogoManager.instancia.DesbloquearTodosItens();
            }
        }
    }

    /// <summary>
    /// Desbloqueia e registra todos os inimigos existentes no jogo de uma só vez,
    /// persistindo no SaveManager e atualizando a interface do Eptinho em tempo real.
    /// </summary>
    [ContextMenu("Desbloquear Todos os Inimigos (F1)")]
    public void DesbloquearTodosInimigos()
    {
        List<EnemyData> todosOsInimigos = new List<EnemyData>();

        // 1. Carrega assets de EnemyData existentes em Resources
        EnemyData[] assets = Resources.LoadAll<EnemyData>("EnemyData");
        if (assets != null)
        {
            foreach (var a in assets)
            {
                if (a != null && !todosOsInimigos.Contains(a))
                    todosOsInimigos.Add(a);
            }
        }

        // 2. Carrega todos os dados gerados pelo EnemyDataAutoLoader
        if (EnemyDataAutoLoader.instancia != null)
        {
            foreach (var auto in EnemyDataAutoLoader.instancia.GetAllEnemyData())
            {
                if (auto != null && !todosOsInimigos.Contains(auto))
                    todosOsInimigos.Add(auto);
            }
        }

        int novosDesbloqueados = 0;

        foreach (EnemyData inimigo in todosOsInimigos)
        {
            if (inimigo == null || string.IsNullOrEmpty(inimigo.enemyName) || IsInimigoInvalido(inimigo.enemyName)) continue;

            if (!nomesRegistrados.Contains(inimigo.enemyName))
            {
                nomesRegistrados.Add(inimigo.enemyName);
                inimigosEncontrados.Add(inimigo);
                novosDesbloqueados++;

                // Integração com catálogo de sinergias
                if (!string.IsNullOrEmpty(inimigo.synergyNodeID) && SynergyCatalogManager.Instance != null)
                {
                    SynergyCatalogManager.Instance.DiscoverNode(inimigo.synergyNodeID);
                }

                // Integração com quests
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.AdvanceQuest(QuestType.CatalogEnemies, inimigo.enemyName, 1);
                }
            }
            else if (!inimigosEncontrados.Contains(inimigo))
            {
                inimigosEncontrados.Add(inimigo);
            }
        }

        // 3. Persistência permanente no SaveManager
        if (SaveManager.instance != null)
        {
            var dados = SaveManager.instance.CachedData;
            if (dados != null)
            {
                if (dados.inimigosDescobertos == null)
                    dados.inimigosDescobertos = new List<string>();

                foreach (string nome in nomesRegistrados)
                {
                    if (!dados.inimigosDescobertos.Contains(nome))
                        dados.inimigosDescobertos.Add(nome);
                }

                SaveManager.instance.SavePersistentData();
            }
        }

        // 4. Se o menu do Eptinho estiver aberto, atualiza a aba do Bestiário imediatamente
        if (EptinhoMenuController.Instance != null && EptinhoMenuController.Instance.gameObject.activeInHierarchy)
        {
            EptinhoMenuController.Instance.AtualizarAbaBestiario();
        }

        Debug.Log($"<color=#00FFAA><b>[BESTIÁRIO - F1]</b></color> Sucesso! {inimigosEncontrados.Count} inimigos cadastrados no Bestiário (+{novosDesbloqueados} novos salvos).");
    }

    /// <summary>
    /// Carrega lista de inimigos já descobertos em sessões anteriores.
    /// </summary>
    private void CarregarDoSave()
    {
        if (SaveManager.instance == null) return;

        var dados = SaveManager.instance.CachedData;
        if (dados == null || dados.inimigosDescobertos == null) return;

        if (dados.inimigosDescobertos != null)
        {
            dados.inimigosDescobertos.RemoveAll(nome => IsInimigoInvalido(nome));
        }

        foreach (string nome in dados.inimigosDescobertos)
        {
            if (!IsInimigoInvalido(nome))
                nomesRegistrados.Add(nome);
        }

        // Reconstrói lista visual a partir dos EnemyData existentes na pasta Resources
        EnemyData[] todos = Resources.LoadAll<EnemyData>("EnemyData");
        foreach (EnemyData data in todos)
        {
            if (data != null && !IsInimigoInvalido(data.enemyName) && nomesRegistrados.Contains(data.enemyName) && !inimigosEncontrados.Contains(data))
            {
                inimigosEncontrados.Add(data);
            }
        }

        // Também tenta pelo EnemyDataAutoLoader (dados criados em memória)
        if (EnemyDataAutoLoader.instancia != null)
        {
            foreach (EnemyData data in EnemyDataAutoLoader.instancia.GetAllEnemyData())
            {
                if (data != null && !IsInimigoInvalido(data.enemyName) && nomesRegistrados.Contains(data.enemyName) && !inimigosEncontrados.Contains(data))
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
        if (IsInimigoInvalido(inimigo.enemyData.enemyName)) return;
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

                // Integração com Quests (CatalogEnemies)
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.AdvanceQuest(QuestType.CatalogEnemies, inimigo.enemyData.enemyName, 1);
                }
            }
        }

        if (EptinhoPopupController.instancia != null)
            EptinhoPopupController.instancia.MostrarPopupInimigo(inimigo.enemyData);

        // Integração com o catálogo de sinergias
        if (!string.IsNullOrEmpty(inimigo.enemyData.synergyNodeID) && SynergyCatalogManager.Instance != null)
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
