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
    public string nomeInimigo => enemyData != null ? enemyData.enemyName : gameObject.name.Replace("(Clone)", "").Trim();
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

        // Garante que o detector de proximidade/visão esteja configurado
        DetectorDeInimigo detector = GetComponent<DetectorDeInimigo>();
        if (detector == null)
        {
            detector = gameObject.AddComponent<DetectorDeInimigo>();
        }

        detector.enemyIdentity = this;
        detector.usarTrigger = true;
        detector.usarDistancia = true;
        detector.distanciaDeteccao = Mathf.Max(detector.distanciaDeteccao, 25f);
        detector.debugLogs = false;
    }

    /// <summary>
    /// Força o registro deste inimigo no Bestiário (se ainda não catalogado).
    /// </summary>
    public void RegisterInBestiary(string origem = "manual")
    {
        if (foiEncontrado) return;

        if (enemyData == null && EnemyDataAutoLoader.instancia != null)
        {
            enemyData = EnemyDataAutoLoader.instancia.GetByName(nomeInimigo);
        }

        if (BestiarioManager.instancia != null)
        {
            BestiarioManager.instancia.Registrar(this);
        }
    }
}
