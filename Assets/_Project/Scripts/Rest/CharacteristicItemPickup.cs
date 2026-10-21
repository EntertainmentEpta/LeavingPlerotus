using UnityEngine;

/// <summary>
/// Pickup de Item Característico do Inimigo
/// Cada mob dropa um item único que dará buffs ao player
/// </summary>
public class CharacteristicItemPickup : MonoBehaviour
{
    [Header("Identificação do Item")]
    [Tooltip("ID único do item (ex: spider_silk, golem_core, crystal_shard)")]
    public string itemId = "unknown";
    [Tooltip("Nome de exibição do item")]
    public string itemName = "Item Misterioso";
    [Tooltip("Descrição do item")]
    [TextArea(2, 4)]
    public string itemDescription = "Um item misterioso dropado por um inimigo.";

    [Header("Visual")]
    public float rotateSpeed = 45f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.2f;

    [Header("Coleta")]
    public float pickupDelay = 0.15f;
    public float lifetime = 60f; // Itens duram mais que essência

    [Header("Atração Magnética (Estilo Essência)")]
    [Tooltip("Distância máxima para começar a atrair o item ao jogador")]
    public float attractDistance = 8.5f;
    [Tooltip("Velocidade inicial de atração magnética")]
    public float attractSpeed = 16f;
    [Tooltip("Offset vertical do ponto de atração do jogador (1.0 = cintura/peito)")]
    public float attractYOffset = 1.0f;

    private bool isBeingAttracted = false;
    private float currentAttractSpeed;
    private Vector3 startPosition;
    private float spawnTime;
    private bool canBePickedUp = false;
    private Transform playerTransform;
    private bool isCollected = false;

    void Start()
    {
        startPosition = transform.position;
        spawnTime = Time.time;
        currentAttractSpeed = attractSpeed;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        // Garante alcance confortável para o colisor
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc != null && sc.radius < 1.0f)
        {
            sc.radius = 1.25f;
        }

        // Snap vertical no solo real ignorando o corpo e colisores do mob moribundo
        SnapToFloor();

        ConvertCubesToSpheres();

        if (lifetime > 0)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void SnapToFloor()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 2.0f, Vector3.down, 25f);
        float bestGroundY = float.MinValue;
        bool foundGround = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.transform.root == transform.root) continue;
            if (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Player")) continue;
            if (hit.collider.GetComponentInParent<DummyHealth>() != null) continue;
            if (hit.collider.GetComponentInParent<ShardSwarmHealth>() != null) continue;

            // Superfície física de chão/cenário abaixo do spawn
            if (hit.point.y <= transform.position.y + 1.2f)
            {
                if (hit.point.y > bestGroundY)
                {
                    bestGroundY = hit.point.y;
                    foundGround = true;
                }
            }
        }

        if (foundGround)
        {
            startPosition = new Vector3(transform.position.x, bestGroundY + 0.45f, transform.position.z);
            transform.position = startPosition;
        }
        else
        {
            // Fallback se estiver sobre colisor atípico: ancora na altura do pé do jogador se disponível
            float fallbackY = transform.position.y;
            if (playerTransform != null && Mathf.Abs(playerTransform.position.y - transform.position.y) > 0.5f)
            {
                fallbackY = playerTransform.position.y + 0.45f;
            }
            startPosition = new Vector3(transform.position.x, fallbackY, transform.position.z);
            transform.position = startPosition;
        }
    }

    private void ConvertCubesToSpheres()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        Mesh sphereMesh = null;

        foreach (var mf in meshFilters)
        {
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLower().Contains("cube"))
            {
                if (sphereMesh == null)
                {
                    GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
                    Destroy(tempSphere);
                }
                mf.sharedMesh = sphereMesh;
            }
        }

        ApplyCleanMaterialToRenderers();
    }

    [Header("Luz e Iluminação do Drop")]
    [Tooltip("Raio de alcance da luz do item no cenário (em metros)")]
    public float lightRange = 2.0f;
    [Tooltip("Intensidade mínima da luz na oscilação")]
    public float minLightIntensity = 1.2f;
    [Tooltip("Intensidade máxima da luz na oscilação")]
    public float maxLightIntensity = 1.6f;
    [Tooltip("Velocidade da oscilação do brilho da luz")]
    public float lightPulseSpeed = 2.4f;

    private Light cachedPointLight;

    private void ApplyCleanMaterialToRenderers()
    {
        try
        {
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
            if (defaultShader == null) defaultShader = Shader.Find("Standard");

            Color tierColor = new Color(0.2f, 0.75f, 1.0f);
            if (ItemDatabase.Instance != null && !string.IsNullOrEmpty(itemId))
            {
                ItemData data = ItemDatabase.Instance.GetItemData(itemId);
                if (data != null) tierColor = data.GetTierColor();
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.gameObject.name.Contains("Text") || r.gameObject.name.Contains("Canvas") || r.gameObject.name.Contains("glow"))
                    continue;

                try
                {
                    Material mat = null;
                    if (r.sharedMaterial != null)
                    {
                        mat = r.material;
                    }

                    if (mat == null || mat.name.Contains("Default") || mat.name.Contains("Internal"))
                    {
                        mat = new Material(defaultShader);
                        r.material = mat;
                    }

                    mat.color = tierColor;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", tierColor * 0.7f);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ITEM PICKUP] Fallback de material em {r.gameObject.name}: {ex.Message}");
                }
            }

            cachedPointLight = GetComponentInChildren<Light>(true);
            if (cachedPointLight == null)
            {
                GameObject lightGo = new GameObject("TierGlowLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.2f;
                cachedPointLight = lightGo.AddComponent<Light>();
                cachedPointLight.type = LightType.Point;
            }

            cachedPointLight.color = tierColor;
            cachedPointLight.intensity = minLightIntensity;
            cachedPointLight.range = lightRange;
            cachedPointLight.enabled = true;

            // Aplica a cor do Tier em Sistemas de Partículas (Rays, Small Stars, CFXR3)
            ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = tierColor;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ITEM PICKUP] Erro em ApplyCleanMaterialToRenderers: {ex.Message}");
        }
    }

    private Vector3 initialScale;
    private bool baseScaleSaved = false;

    private void UpdatePulseEffect()
    {
        if (!baseScaleSaved)
        {
            initialScale = transform.localScale;
            baseScaleSaved = true;
        }

        // Animação sutil de respiração: oscila entre 85% e 100% da escala inicial
        float pulseT = (Mathf.Sin(Time.time * 2.6f) + 1.0f) * 0.5f;
        float scaleRatio = Mathf.Lerp(0.85f, 1.0f, pulseT);
        transform.localScale = initialScale * scaleRatio;
    }

    private void UpdateLightPulse()
    {
        if (cachedPointLight == null)
        {
            cachedPointLight = GetComponentInChildren<Light>(true);
        }

        if (cachedPointLight != null)
        {
            cachedPointLight.range = lightRange;
            float lightT = (Mathf.Sin(Time.time * lightPulseSpeed) + 1.0f) * 0.5f;
            cachedPointLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, lightT);
        }
    }

    void Update()
    {
        // Animação sutil de respiração e oscilação de luz
        UpdatePulseEffect();
        UpdateLightPulse();

        // Delay antes de poder coletar
        if (!canBePickedUp)
        {
            if (Time.time - spawnTime >= pickupDelay)
            {
                canBePickedUp = true;
            }
        }

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // Checa se o player está no raio de atração magnética
        if (canBePickedUp && playerTransform != null)
        {
            float currentAttractDist = attractDistance;
            PlayerAttributesDefensive defStats = playerTransform.GetComponent<PlayerAttributesDefensive>();
            if (defStats != null)
            {
                currentAttractDist *= defStats.magnetRangeMultiplier;
            }

            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (!isBeingAttracted && distToPlayer <= currentAttractDist)
            {
                isBeingAttracted = true;
            }
        }

        // Atração magnética fluida e acelerada em direção ao jogador (estilo Essência)
        if (isBeingAttracted && playerTransform != null && canBePickedUp)
        {
            currentAttractSpeed = Mathf.Min(currentAttractSpeed + 28f * Time.deltaTime, 26f);
            Vector3 targetPos = playerTransform.position + Vector3.up * attractYOffset;

            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentAttractSpeed * Time.deltaTime);

            float distToTarget = Vector3.Distance(transform.position, targetPos);

            // Encolhe suavemente ao se aproximar do centro para efeito de absorção física
            if (distToTarget < 1.2f && baseScaleSaved)
            {
                float tScale = Mathf.Clamp01(distToTarget / 1.2f);
                transform.localScale = Vector3.Lerp(Vector3.zero, initialScale, tScale);
            }

            // Absorve ao encostar no player
            if (distToTarget < 0.45f)
            {
                CollectItem(playerTransform.gameObject);
                return;
            }
        }
        else
        {
            // Rotação e movimento de flutuação no chão
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    void OnTriggerEnter(Collider other) => CheckAndCollect(other);
    void OnTriggerStay(Collider other)  => CheckAndCollect(other);

    void CheckAndCollect(Collider other)
    {
        if (!canBePickedUp || other == null) return;

        if (other.CompareTag("Player") ||
            other.GetComponentInParent<PlayerHealth>() != null ||
            other.GetComponentInParent<PlayerInventory>() != null)
        {
            CollectItem(other.gameObject);
        }
    }

    void CollectItem(GameObject player)
    {
        if (isCollected) return;
        isCollected = true;

        // Desativa colisores e este script imediatamente para impedir coletas concorrentes
        Collider[] allCols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < allCols.Length; i++) allCols[i].enabled = false;
        this.enabled = false;
        // Tenta encontrar o inventário do player (mesmo padrão do EssencePickup)
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        
        // Fallback: busca nos pais (ex: colisor está num filho do Player)
        if (inventory == null)
        {
            inventory = player.GetComponentInParent<PlayerInventory>();
        }

        // Fallback final: busca global (Player é DontDestroyOnLoad)
        if (inventory == null)
        {
            inventory = Object.FindFirstObjectByType<PlayerInventory>();
        }
        
        if (inventory == null)
        {
            // Sem inventário → item permanece no chão para tentar de novo
            Debug.LogWarning("[ITEM] PlayerInventory não encontrado! Item permanece no chão: " + itemName);
            return; // NÃO destrói o item
        }

        bool added = inventory.AddItem(itemId, 1);
        
        if (!added)
        {
            // Inventário cheio (10/10 slots): o drop é destruído/descartado automaticamente
            Debug.Log($"[ITEM] Mochila cheia (10/10 slots)! {itemName} foi descartado ao passar por cima.");
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso($"Mochila cheia (10/10)! {itemName} descartado.");
            }
            Destroy(gameObject);
            return;
        }
        
        Debug.Log("[ITEM] Coletou: " + itemName + " (ID: " + itemId + ")");

        // Integração com o catálogo de sinergias
        if (SynergyCatalogManager.Instance != null)
        {
            SynergyCatalogManager.Instance.DiscoverNode(itemId);
        }

        // Diálogo explicativo da mecânica de Sinergia e fusão de monstros
        EptinhoStoryDialogs.TriggerFirstMonsterLoot();

        // VFX/SFX de coleta aqui se quiser

        Destroy(gameObject);
    }
}
