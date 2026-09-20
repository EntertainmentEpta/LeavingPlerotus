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
    public float pickupDelay = 0.5f;
    public float lifetime = 60f; // Itens duram mais que essência

    private Vector3 startPosition;
    private float spawnTime;
    private bool canBePickedUp = false;

    void Start()
    {
        startPosition = transform.position;
        spawnTime = Time.time;

        ConvertCubesToSpheres();

        if (lifetime > 0)
        {
            Destroy(gameObject, lifetime);
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

            Material mat = r.material;
            if (mat == null || mat.name.Contains("Default") || mat.name.Contains("Internal"))
            {
                mat = new Material(defaultShader);
                r.material = mat;
            }

            mat.color = tierColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", tierColor * 0.7f);
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
        // Animação sutil de respiração e oscilação de luz (0.8 a 1.2)
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

        // Rotação e movimento de flutuação
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        
        float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canBePickedUp) return;

        if (other.CompareTag("Player"))
        {
            CollectItem(other.gameObject);
        }
    }

    void CollectItem(GameObject player)
    {
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
            // Inventário cheio — item permanece no chão
            Debug.Log("[ITEM] Inventário cheio! Não coletou: " + itemName);
            return; // NÃO destrói o item
        }
        
        Debug.Log("[ITEM] Coletou: " + itemName + " (ID: " + itemId + ")");

        // VFX/SFX de coleta aqui se quiser

        Destroy(gameObject);
    }
}
