using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Carrega os sprites dos inimigos em runtime e cria EnemyData dinamicamente na memória.
/// Não requer configuração manual no editor. Os sprites são carregados da pasta Resources.
/// </summary>
public class EnemyDataAutoLoader : MonoBehaviour
{
    private static EnemyDataAutoLoader _instancia;
    public static EnemyDataAutoLoader instancia
    {
        get
        {
            if (_instancia == null)
            {
                _instancia = FindFirstObjectByType<EnemyDataAutoLoader>();
                if (_instancia == null)
                {
                    GameObject go = new GameObject("EnemyDataAutoLoader_Auto");
                    _instancia = go.AddComponent<EnemyDataAutoLoader>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instancia;
        }
        private set { _instancia = value; }
    }

    // Lista dos dados de inimigos criados em runtime
    private List<EnemyData> _enemyDataCache = new List<EnemyData>();

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

        InitializeEnemyData();
    }

    private void InitializeEnemyData()
    {
        // 1. Primeiro tenta carregar EnemyData assets do Resources (se existirem)
        EnemyData[] loaded = Resources.LoadAll<EnemyData>("EnemyData");
        foreach (var data in loaded)
        {
            if (data != null && !_enemyDataCache.Exists(d => d.enemyName == data.enemyName))
            {
                _enemyDataCache.Add(data);
                Debug.Log($"[ENEMY DATA LOADER] Asset carregado: {data.enemyName}");
            }
        }

        // 2. Garante que todos os mobs do jogo estejam presentes na memória
        List<EnemyData> dadosPadrao = CriarDadosEmMemoria();
        foreach (var padrao in dadosPadrao)
        {
            if (!_enemyDataCache.Exists(d => string.Equals(d.enemyName, padrao.enemyName, System.StringComparison.OrdinalIgnoreCase)))
            {
                _enemyDataCache.Add(padrao);
            }
        }

        Debug.Log($"[ENEMY DATA LOADER] {_enemyDataCache.Count} EnemyData registrados no cache.");
    }

    private List<EnemyData> CriarDadosEmMemoria()
    {
        var lista = new List<EnemyData>();

        lista.Add(CriarEnemyData("Goblin",          "EnemySprites/Inimigo_Goblin", "Mob Menor",        "Uma criatura ágil com picareta. Preferem atacar em grupo e recuar rapidamente."));
        lista.Add(CriarEnemyData("Golem",           "EnemySprites/Golem",          "Tanque",           "Construção mineral animada. Extremamente resistente, mas lento e previsível."));
        lista.Add(CriarEnemyData("Aranha",          "EnemySprites/spider",         "Atirador",         "Criatura ágil que dispara projéteis de teia. Difícil de alcançar em espaços abertos."));
        lista.Add(CriarEnemyData("Overstare",       "EnemySprites/ALLSEEING",      "Elite",            "Entidade biomecânica com olho cristalino flutuante. Seu olhar fixo vigia a masmorra e rastreia intrusos à distância."));
        lista.Add(CriarEnemyData("Sentinela",       "EnemySprites/ALLSEEING",      "Elite",            "Olho cristalino que patrulha áreas. Detecta movimento e alerta outros inimigos."));
        lista.Add(CriarEnemyData("Vigia",           "EnemySprites/ALLSEEING",      "Atirador",         "Sentinela flutuante armada com prismas que disparam feixes pontiagudos de cristal."));
        lista.Add(CriarEnemyData("Crystal Watcher", "EnemySprites/ALLSEEING",      "Atirador",         "Sentinela flutuante armada com prismas que disparam feixes pontiagudos de cristal."));
        lista.Add(CriarEnemyData("Totem",           "EnemySprites/Inimigo_Totem",  "Suporte",          "Estrutura estática que invoca caveiras e emite ondas periódicas de repulsão."));
        lista.Add(CriarEnemyData("Cristallus",      "EnemySprites/Cristalus",      "Blindado",         "Entidade biomineral cristalina com carapaça reforçada que atenua danos de tiros de pistola e projeta espinhos."));
        lista.Add(CriarEnemyData("Cristalus",       "EnemySprites/Cristalus",      "Blindado",         "Entidade biomineral cristalina com carapaça reforçada que atenua danos de tiros de pistola e projeta espinhos."));
        lista.Add(CriarEnemyData("Geobionte",       "EnemySprites/Cristalus",      "Elite Tanque",     "Criatura colossal de pedra e cristais que absorve minerais do solo para reparar sua carapaça."));
        lista.Add(CriarEnemyData("Sharp Blur",      "EnemySprites/Enxame",         "Elite Assassino",  "Humanoide corrompido que se move em alta velocidade e retalha com lâminas de energia focada."));
        lista.Add(CriarEnemyData("SharpBlur",       "EnemySprites/Enxame",         "Elite Assassino",  "Humanoide corrompido que se move em alta velocidade e retalha com lâminas de energia focada."));
        lista.Add(CriarEnemyData("Peixe",           "EnemySprites/Peixe",          "Ambush",           "Predador das profundezas com carapaça resistente a projéteis e couraça de espinhos."));
        lista.Add(CriarEnemyData("Sintonizador",    "EnemySprites/Sintonizador",   "Suporte",          "Prisma ressonante flutuante que conecta feixes e protege inimigos próximos."));
        lista.Add(CriarEnemyData("Enxame",          "EnemySprites/Enxame",         "Enxame",           "Cardume de estilhaços cristalinos velozes que cercam e fatiam o invasor."));

        return lista;
    }

    private EnemyData CriarEnemyData(string nome, string spritePath, string classe, string lore)
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.enemyName = nome;
        data.enemyClass = classe;
        data.descricao = lore;
        data.icon = Resources.Load<Sprite>(spritePath);
        return data;
    }

    /// <summary>
    /// Retorna todos os EnemyData disponíveis (assets + memória).
    /// </summary>
    public List<EnemyData> GetAllEnemyData() => _enemyDataCache;

    /// <summary>
    /// Retorna EnemyData pelo nome do inimigo com busca exata e fallback inteligente para sinônimos.
    /// </summary>
    public EnemyData GetByName(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;

        string clean = nome.Replace("(Clone)", "").Trim();

        // 1. Busca direta (case-insensitive)
        EnemyData match = _enemyDataCache.Find(d => string.Equals(d.enemyName, clean, System.StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 2. Busca aproximada / normalização de aliases
        string lower = clean.ToLowerInvariant();

        if (lower.Contains("geobionte"))
            match = _enemyDataCache.Find(d => d.enemyName == "Geobionte");
        else if (lower.Contains("sharp"))
            match = _enemyDataCache.Find(d => d.enemyName == "Sharp Blur" || d.enemyName == "SharpBlur");
        else if (lower.Contains("overstare"))
            match = _enemyDataCache.Find(d => d.enemyName == "Overstare" || d.enemyName == "Sentinela");
        else if (lower.Contains("watcher") || lower.Contains("vigia"))
            match = _enemyDataCache.Find(d => d.enemyName == "Vigia" || d.enemyName == "Crystal Watcher");
        else if (lower.Contains("sentinela"))
            match = _enemyDataCache.Find(d => d.enemyName == "Sentinela" || d.enemyName == "Overstare");
        else if (lower.Contains("cristal") || lower.Contains("cristallus"))
            match = _enemyDataCache.Find(d => d.enemyName == "Cristallus" || d.enemyName == "Cristalus");
        else if (lower.Contains("aranha") || lower.Contains("spider"))
            match = _enemyDataCache.Find(d => d.enemyName == "Aranha");
        else if (lower.Contains("golem"))
            match = _enemyDataCache.Find(d => d.enemyName == "Golem");
        else if (lower.Contains("goblin"))
            match = _enemyDataCache.Find(d => d.enemyName == "Goblin");
        else if (lower.Contains("totem"))
            match = _enemyDataCache.Find(d => d.enemyName == "Totem");
        else if (lower.Contains("peixe") || lower.Contains("fish") || lower.Contains("drag"))
            match = _enemyDataCache.Find(d => d.enemyName == "Peixe");
        else if (lower.Contains("sintonizador") || lower.Contains("tuner"))
            match = _enemyDataCache.Find(d => d.enemyName == "Sintonizador");
        else if (lower.Contains("enxame") || lower.Contains("swarm") || lower.Contains("star"))
            match = _enemyDataCache.Find(d => d.enemyName == "Enxame");

        if (match != null) return match;

        // 3. Fallback dinâmico: cria EnemyData genérico na memória para nunca falhar com null
        EnemyData fallback = ScriptableObject.CreateInstance<EnemyData>();
        fallback.enemyName = clean;
        fallback.enemyClass = "Desconhecido";
        fallback.descricao = $"Forma de vida misteriosa registrada nos túneis: {clean}.";
        fallback.icon = Resources.Load<Sprite>("EnemySprites/ALLSEEING");
        _enemyDataCache.Add(fallback);
        return fallback;
    }
}
