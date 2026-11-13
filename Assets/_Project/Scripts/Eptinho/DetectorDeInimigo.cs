using UnityEngine;

public class DetectorDeInimigo : MonoBehaviour
{
    [Header("Referencias")]
    public EnemyIdentity enemyIdentity;

    [Header("Modo de Deteccao")]
    [Tooltip("Se ativo, registra quando o Player entra em um Trigger deste objeto.")]
    public bool usarTrigger = true;

    [Tooltip("Se ativo, registra por distancia no Update.")]
    public bool usarDistancia = true;

    [Tooltip("Distancia usada no modo por distancia.")]
    public float distanciaDeteccao = 25f;

    [Tooltip("Se ativo, registra se o inimigo for renderizado na tela da câmera do Player (visão direta).")]
    public bool usarVisaoCamera = true;
    public float distanciaVisaoCamera = 40f;

    [Header("Debug")]
    public bool debugLogs = false;

    private Transform playerTransform;
    private bool jaTentouRegistrar = false;
    private float checkTimer = 0f;

    void Awake()
    {
        EnsureIdentity();
    }

    void Start()
    {
        CachePlayer();
        EnsureIdentity();
    }

    private void EnsureIdentity()
    {
        if (enemyIdentity == null)
        {
            enemyIdentity = GetComponent<EnemyIdentity>()
                           ?? GetComponentInChildren<EnemyIdentity>()
                           ?? GetComponentInParent<EnemyIdentity>();
        }
    }

    void Update()
    {
        if (jaTentouRegistrar) return;
        EnsureIdentity();
        if (enemyIdentity != null && enemyIdentity.foiEncontrado) return;

        // Limita a verificação a cada 0.2s para alta performance
        checkTimer += Time.deltaTime;
        if (checkTimer < 0.2f) return;
        checkTimer = 0f;

        if (playerTransform == null)
            CachePlayer();

        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // 1. Verificação por visão direta da câmera do jogador
        if (usarVisaoCamera && dist <= distanciaVisaoCamera)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(transform.position);
                bool naTela = vp.z > 0f && vp.x >= -0.05f && vp.x <= 1.05f && vp.y >= -0.05f && vp.y <= 1.05f;
                if (naTela)
                {
                    TentarRegistrar("visao_camera");
                    return;
                }
            }
        }

        // 2. Verificação por distância / proximidade
        if (usarDistancia && dist <= distanciaDeteccao)
        {
            TentarRegistrar("distancia");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!usarTrigger) return;
        if (jaTentouRegistrar) return;
        EnsureIdentity();
        if (enemyIdentity != null && enemyIdentity.foiEncontrado) return;
        if (!other.CompareTag("Player")) return;

        TentarRegistrar("trigger");
    }

    public void TentarRegistrar(string origem)
    {
        if (jaTentouRegistrar) return;

        EnsureIdentity();
        if (enemyIdentity == null)
        {
            enemyIdentity = gameObject.AddComponent<EnemyIdentity>();
        }

        if (BestiarioManager.instancia == null)
        {
            if (debugLogs)
                Debug.LogWarning("[DETECTOR INIMIGO] BestiarioManager.instancia NULL ao registrar via " + origem);
            // Permite nova tentativa quando o manager estiver disponível
            return;
        }

        jaTentouRegistrar = true;
        BestiarioManager.instancia.Registrar(enemyIdentity);

        if (debugLogs)
        {
            string nome = enemyIdentity != null && enemyIdentity.enemyData != null
                ? enemyIdentity.enemyData.enemyName
                : gameObject.name;
            Debug.Log($"[DETECTOR INIMIGO] Sucesso ao registrar via {origem}: {nome}");
        }
    }

    private void CachePlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, distanciaDeteccao);
    }
}
