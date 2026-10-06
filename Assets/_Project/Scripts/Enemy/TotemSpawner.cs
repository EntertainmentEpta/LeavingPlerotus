using UnityEngine;
using System.Collections.Generic; // Necessário para usar Listas

public class TotemSpawner : MonoBehaviour
{
    [Header("Referências")]
    public GameObject skullPrefab;
    private Transform playerTransform;

    [Header("Configurações de Spawn")]
    public int totalSkullsToSpawn = 3;
    public float spawnInterval = 5f;
    public float spawnRadius = 10f;
    public float spawnHeightOffset = 1.5f;

    [Header("Ativação")]
    public float activationDistance = 20f;

    private float originalSpawnInterval;
    private bool isBuffed = false;
    private int skullsSpawned = 0;
    private float spawnTimer;
    private bool isActivated = false;
    private DummyHealth health;

    // --- MUDANÇA 1: Lista para rastrear as caveiras criadas ---
    private List<GameObject> activeSkulls = new List<GameObject>();

    void Start()
    {
        health = GetComponent<DummyHealth>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
        spawnTimer = spawnInterval;
    }
    // --- LÓGICA DE BUFF DO SINTONIZADOR ---

    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            originalSpawnInterval = spawnInterval;
            spawnInterval /= 2f; // Spawna 2x mais rápido!
            // totalSkullsToSpawn += 5; // Opcional: Aumenta o limite total
            Debug.Log(gameObject.name + " foi BUFFADO pelo Sintonizador!");
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            spawnInterval = originalSpawnInterval; // Volta ao normal
        }
    }

    void Update()
    {
        // Se o totem morrer, o OnDestroy cuida das caveiras.
        if (health != null && health.CurrentHealth <= 0)
        {
            // O DummyHealth vai destruir este objeto em breve.
            return;
        }

        if (!isActivated)
        {
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) < activationDistance)
            {
                isActivated = true;

                EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                Debug.Log("[TOTEM] EnemyIdentity: " + (id != null ? id.nomeInimigo : "NULL") + " | BestiarioManager: " + (BestiarioManager.instancia != null));
                if (id != null && BestiarioManager.instancia != null)
                    BestiarioManager.instancia.Registrar(id);
            }
            return;
        }

        if (skullsSpawned < totalSkullsToSpawn)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnSkull();
                spawnTimer = spawnInterval;
            }
        }
    }

    [Header("Configurações do Buraco da Cabeça")]
    [Tooltip("Altura a partir da base por onde as caveiras emergem do buraco da cabeça do Totem.")]
    public float headHoleHeight = 2.2f;

    void SpawnSkull()
    {
        skullsSpawned++;

        // Spawna exatamente no buraco no topo da cabeça do Totem
        Transform headTransform = transform.Find("Head") ?? transform.Find("HeadHole") ?? transform.Find("HeadPoint");
        Vector3 spawnPosition = (headTransform != null) ? headTransform.position : (transform.position + Vector3.up * headHoleHeight);

        GameObject newSkull = Instantiate(skullPrefab, spawnPosition, Quaternion.identity);

        // 1. Ignora colisões entre o Totem e a Caveira para não prender no modelo do Totem
        Collider[] totemCols = GetComponentsInChildren<Collider>();
        Collider[] skullCols = newSkull.GetComponentsInChildren<Collider>();
        foreach (var tCol in totemCols)
        {
            foreach (var sCol in skullCols)
            {
                if (tCol != null && sCol != null)
                {
                    Physics.IgnoreCollision(tCol, sCol, true);
                }
            }
        }
        foreach (var sCol in skullCols)
        {
            if (sCol != null) sCol.isTrigger = true;
        }

        // 2. Desativa DamageZone e PulseVisualizer de anéis concêntricos contínuos durante o voo
        DamageZone dz = newSkull.GetComponent<DamageZone>();
        if (dz != null) dz.enabled = false;

        PulseVisualizer pv = newSkull.GetComponent<PulseVisualizer>();
        if (pv != null) pv.enabled = false;

        // 3. Inicializa o movimento de emergência da cabeça
        HomingHazard hazard = newSkull.GetComponent<HomingHazard>();
        if (hazard != null)
        {
            hazard.InitializeEmergence(spawnPosition);
        }

        activeSkulls.Add(newSkull);
    }

    // --- Função chamada automaticamente quando o Totem é destruído ---
    private void OnDestroy()
    {
        // Percorre a lista de caveiras criadas
        foreach (GameObject skull in activeSkulls)
        {
            if (skull != null)
            {
                Destroy(skull);
            }
        }
        Debug.Log("Totem destruído: todas as caveiras vinculadas foram removidas.");
    }
}