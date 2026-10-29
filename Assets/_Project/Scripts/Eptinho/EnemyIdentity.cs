using UnityEngine;

/// <summary>
/// Componente de identidade do inimigo — coloque em cada prefab de inimigo.
/// Referencia o EnemyData ScriptableObject com todos os dados do inimigo.
/// Quando o player avista o inimigo, o BestiarioManager registra automaticamente.
/// </summary>
public class EnemyIdentity : MonoBehaviour
{
    [Header("Dados do Inimigo")]
    [Tooltip("ScriptableObject com todos os dados deste inimigo. " +
             "Crie via: Assets > Create > Eptinho > Enemy Data")]
    public EnemyData enemyData;

    [HideInInspector] public bool foiEncontrado = false;

    // Propriedades de compatibilidade para não quebrar scripts de IA que usavam os campos antigos
    public string nomeInimigo => enemyData != null ? enemyData.enemyName : gameObject.name;
    public Sprite icon => enemyData != null ? enemyData.icon : null;
    public string descricao => enemyData != null ? enemyData.descricao : "";

    void Awake()
    {
        // Garante que EnemyDataAutoLoader exista sob demanda
        if (EnemyDataAutoLoader.instancia == null)
        {
            GameObject loaderGO = new GameObject("EnemyDataAutoLoader_Auto");
            loaderGO.AddComponent<EnemyDataAutoLoader>();
            DontDestroyOnLoad(loaderGO);
        }

        // Auto-mapeia o EnemyData se estiver nulo
        if (enemyData == null && EnemyDataAutoLoader.instancia != null)
        {
            string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
            if (cleanName.Contains("Spider") || cleanName.Contains("spider") || cleanName.Contains("Aranha"))
                cleanName = "Aranha";
            else if (cleanName.Contains("Watcher") || cleanName.Contains("Sentinela") || cleanName.Contains("WatcherEnemy"))
                cleanName = "Sentinela";
            else if (cleanName.Contains("Totem"))
                cleanName = "Totem";
            else if (cleanName.Contains("Golem") || cleanName.Contains("golem"))
                cleanName = "Golem";
            else if (cleanName.Contains("Goblin") || cleanName.Contains("goblin"))
                cleanName = "Goblin";
            else if (cleanName.Contains("Cristalus") || cleanName.Contains("cristalus"))
                cleanName = "Cristalus";
            else if (cleanName.Contains("Peixe") || cleanName.Contains("peixe") || cleanName.Contains("Fish") || cleanName.Contains("fish"))
                cleanName = "Peixe";
            else if (cleanName.Contains("Tuner") || cleanName.Contains("tuner") || cleanName.Contains("Sintonizador") || cleanName.Contains("sintonizador"))
                cleanName = "Sintonizador";
            else if (cleanName.Contains("Swarm") || cleanName.Contains("swarm") || cleanName.Contains("Enxame") || cleanName.Contains("ShardSwarm"))
                cleanName = "Enxame";

            enemyData = EnemyDataAutoLoader.instancia.GetByName(cleanName);
        }
    }

    void Start()
    {
        // Se já está catalogado no Bestiário permanente, marca localmente e encerra
        if (BestiarioManager.instancia != null && BestiarioManager.instancia.JaRegistrado(nomeInimigo))
        {
            foiEncontrado = true;
            return;
        }

        // Se ainda não foi encontrado e não tem detector, anexa um detector de inimigo
        if (!foiEncontrado && GetComponent<DetectorDeInimigo>() == null)
        {
            DetectorDeInimigo detector = gameObject.AddComponent<DetectorDeInimigo>();
            detector.usarTrigger = true;
            detector.usarDistancia = true;
            detector.distanciaDeteccao = 15f;
            detector.debugLogs = true;
        }
    }
}
