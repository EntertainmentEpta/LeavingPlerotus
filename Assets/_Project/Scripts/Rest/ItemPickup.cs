using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script unificado de coleta de itens.
/// Substitui: CharacteristicItemPickup + DetectorDoItem + ItemCollectable.
///
/// RESPONSABILIDADES:
///   • Animação de flutuação e rotação do item no chão
///   • Ativar glowObject e pressFUI quando o player se aproxima
///   • Ao pressionar F: adiciona ao PlayerInventory + registra no CatalogoManager
///   • Respeita pickupDelay e lifetime configuráveis
///
/// SETUP NO PREFAB:
///   Adicione este script + Interactable (com ItemData preenchido) + Collider (Is Trigger).
///   Atribua glowObject e pressFUI no Inspector (filhos do prefab em World Space).
/// </summary>
[RequireComponent(typeof(Interactable))]
public class ItemPickup : MonoBehaviour
{
    [Header("Destaque e Escala dos Recursos")]
    [Tooltip("Filho do prefab com efeito de brilho (ativado quando player está perto)")]
    public GameObject glowObject;
    [Tooltip("UI 'Pressione F' em World Space, filho do prefab")]
    public GameObject pressFUI;
    [Tooltip("Escala/Tamanho do modelo visual das moscas (padrão: 2.5)")]
    public float flyScale = 2.5f;
    [Tooltip("Altura Y do enxame de moscas acima do chão (padrão: 1.0)")]
    public float flyHeightOffset = 1.0f;

    [Header("Coleta")]
    [Tooltip("Delay após spawn antes de poder ser coletado")]
    public float pickupDelay = 0.5f;
    [Tooltip("Tempo em segundos até o item desaparecer. 0 = nunca")]
    public float lifetime = 60f;

    private Interactable interactable;
    private float spawnTime;
    private bool canBePickedUp = false;
    private GameObject playerNearby = null;

    void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (glowObject != null && !IsVisualRendererObject(glowObject)) 
        {
            glowObject.SetActive(false);
        }
        if (pressFUI != null) pressFUI.SetActive(false);
    }

    private bool IsVisualRendererObject(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.name.Contains("Fly") || obj.name.Contains("Swarm") || obj.name.Contains("Flora") || obj.name.Contains("Mineral")) return true;
        if (obj.GetComponent<SkinnedMeshRenderer>() != null || obj.GetComponent<FlySwarmFX>() != null) return true;
        return false;
    }

    [Header("Forçar Categoria (Definido por Room Spawner)")]
    [HideInInspector]
    public string forceCategory = "";

    private bool isInitialized = false;

    public void InitializeItem(string category)
    {
        forceCategory = category;
        isInitialized = false; // Permite re-executar a inicialização visual com a categoria informada
        InitializeItem();
    }

    void Start()
    {
        spawnTime = Time.time;
        if (lifetime > 0) Destroy(gameObject, lifetime);
        
        // Converte qualquer Mesh de Cubo para Esfera (Círculo 3D) automaticamente
        ConvertCubesToSpheres();

        if (!isInitialized)
        {
            InitializeItem();
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

        // Garante que nenhum material rosa/magenta apareça nos orbes
        ApplyCleanMaterialToRenderers();
    }

    private void ApplyCleanMaterialToRenderers()
    {
        ApplyTierColorsToItem();
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

    private void ApplyTierColorsToItem()
    {
        Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
        if (defaultShader == null) defaultShader = Shader.Find("Standard");

        // Pega a cor exata da raridade (Tier 1 = Branco, Tier 2 = Azul, Tier 3 = Roxo, Tier 4 = Dourado)
        Color tierColor = Color.white;
        if (interactable != null && interactable.itemData != null)
        {
            tierColor = interactable.itemData.GetTierColor();
        }

        // 1. Aplica a cor do Tier no material e na emissão dos MeshRenderers da esfera
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

        // 2. Aplica a cor do Tier na Luz PointLight do item dropado (Ex: CFXR3 Point Light)
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

        // 3. Aplica a cor do Tier em Sistemas de Partículas (Rays, Small Stars, CFXR3)
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.startColor = tierColor;
        }

        // 4. Aplica a cor do Tier na imagem de brilho/glow caso exista
        if (glowObject != null)
        {
            SpriteRenderer glowSr = glowObject.GetComponent<SpriteRenderer>();
            if (glowSr != null) glowSr.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.8f);
        }
    }

    private void InitializeItem()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (interactable == null || interactable.itemData == null) return;

        string source = forceCategory;
        if (string.IsNullOrEmpty(source) && interactable.itemData != null)
        {
            source = interactable.itemData.enemySource;
        }

        if (string.IsNullOrEmpty(source))
        {
            string nameLower = gameObject.name.ToLower();
            if (nameLower.Contains("planta") || nameLower.Contains("musgo")) source = "Flora";
            else if (nameLower.Contains("frog") || nameLower.Contains("sapo") || nameLower.Contains("larva") || nameLower.Contains("tinker") || nameLower.Contains("carpet")) source = "Fauna";
            else if (nameLower.Contains("crystal") || nameLower.Contains("stone") || nameLower.Contains("pedra")) source = "Minerals";
        }

        // Preserva estritamente o ItemData se já foi atribuído pelo spawner ou prefab
        bool isGenericPlaceholder = (interactable.itemData.itemId == "crystal" || 
                                     interactable.itemData.itemId == "little_frog" || 
                                     interactable.itemData.itemId == "planta" || 
                                     interactable.itemData.itemId == "tinker" || 
                                     string.IsNullOrEmpty(interactable.itemData.enemySource));

        if (isGenericPlaceholder && ItemDatabase.Instance != null && !string.IsNullOrEmpty(source))
        {
            List<ItemData> matchingItems = new List<ItemData>();
            foreach (var item in ItemDatabase.Instance.allItems)
            {
                if (item != null && item.enemySource == source && item.returnsToBase)
                {
                    matchingItems.Add(item);
                }
            }

            if (matchingItems.Count > 0)
            {
                ItemData chosen = matchingItems[UnityEngine.Random.Range(0, matchingItems.Count)];
                interactable.itemData = chosen;
                interactable.objetoNome = chosen.itemName;
                interactable.descricao = chosen.description;
                interactable.icon = chosen.icon;
                gameObject.name = $"{chosen.itemId}_Pickup";
            }
        }

        // Aplica a customização visual (Fly.prefab para Fauna, Flora, Minérios) em TODOS os itens da categoria
        if (!string.IsNullOrEmpty(source))
        {
            CustomizeItemVisuals(source);
        }
    }

    private void CustomizeItemVisuals(string source)
    {
        // Garante que todos os recursos de mapa (Fauna, Flora, Minérios) sejam direcionados para a Bolsa Sintética
        if (interactable != null && interactable.itemData != null)
        {
            if (source == "Fauna" || source == "Flora" || source == "Minerals")
            {
                interactable.itemData.returnsToBase = true;
            }
        }

        if (source == "Fauna")
        {
            bool isFish = (interactable != null && interactable.itemData != null && interactable.itemData.itemId == "peixe_lagoa");

            if (isFish)
            {
                Transform fishTransform = transform.Find("LakeFishVisual");
                if (fishTransform == null)
                {
                    GameObject fishGo = new GameObject("LakeFishVisual");
                    fishGo.transform.SetParent(transform, false);
                    fishGo.transform.localPosition = Vector3.zero; // Na superfície da água

                    // Cria partículas de marola e salpico d'água bioluminescente
                    ParticleSystem ps = fishGo.AddComponent<ParticleSystem>();
                    var main = ps.main;
                    main.startColor = new Color(0.2f, 0.9f, 1.0f, 0.8f);
                    main.startSize = 0.45f;
                    main.startSpeed = 0.6f;
                    main.maxParticles = 35;

                    var emission = ps.emission;
                    emission.rateOverTime = 10;

                    var shape = ps.shape;
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.6f;

                    Light light = fishGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(0.1f, 0.85f, 1.0f);
                    light.range = 2.5f;
                    light.intensity = 1.5f;

                    fishTransform = fishGo.transform;
                }

                if (fishTransform != null)
                {
                    fishTransform.gameObject.SetActive(true);
                }
            }
            else
            {
                Transform flyTransform = transform.Find("FlyVisual");

                if (flyTransform == null)
                {
                    // Instancia o prefab de Moscas (Fly.prefab) como modelo visual oficial de Fauna
                    GameObject flyPrefab = Resources.Load<GameObject>("SpawnItems/Fly") ?? 
                                           Resources.Load<GameObject>("Fly") ?? 
                                           #if UNITY_EDITOR
                                           UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Items_and_Crafting/Resources/SpawnItems/Fly.prefab") ??
                                           UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enviroment/Ambiente/Fly.prefab");
                                           #else
                                           null;
                                           #endif

                    if (flyPrefab != null)
                    {
                        GameObject flyInstance = Instantiate(flyPrefab, transform);
                        flyInstance.name = "FlyVisual";
                        flyInstance.transform.localPosition = Vector3.up * flyHeightOffset;
                        flyInstance.transform.localRotation = Quaternion.identity;
                        flyInstance.transform.localScale = new Vector3(flyScale, flyScale, flyScale);
                        flyTransform = flyInstance.transform;

                        foreach (var c in flyInstance.GetComponentsInChildren<Collider>())
                        {
                            Destroy(c);
                        }

                        if (flyInstance.GetComponent<FlySwarmFX>() == null)
                        {
                            flyInstance.AddComponent<FlySwarmFX>();
                        }

                        RuntimeAnimatorController flyCtrl = null;
                        #if UNITY_EDITOR
                        flyCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Enviroment/FlySwarmController.controller");
                        #endif

                        Animator rootAnim = flyInstance.GetComponent<Animator>();
                        if (rootAnim == null) rootAnim = flyInstance.AddComponent<Animator>();
                        if (flyCtrl != null) rootAnim.runtimeAnimatorController = flyCtrl;
                        rootAnim.enabled = true;

                        foreach (var childAnim in flyInstance.GetComponentsInChildren<Animator>(true))
                        {
                            if (flyCtrl != null && childAnim.runtimeAnimatorController == null)
                            {
                                childAnim.runtimeAnimatorController = flyCtrl;
                            }
                            childAnim.enabled = true;
                        }
                    }
                }

                // Garante visibilidade permanente desde qualquer distância
                if (flyTransform != null)
                {
                    flyTransform.gameObject.SetActive(true);
                    foreach (var r in flyTransform.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r != null)
                        {
                            r.enabled = true;
                            r.gameObject.SetActive(true);
                        }
                    }
                }
            }

            // Desativa explicitamente os modelos antigos de sapo/placeholder fora do visual ativo
            foreach (Transform child in transform)
            {
                if (child == null || child.name == "FlyVisual" || child.name == "LakeFishVisual") continue;
                string cName = child.name.ToLower();
                if (cName.Contains("frog") || cName.Contains("sapo") || cName.Contains("larva") || cName.Contains("cube"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // Colisor prolongado até o chão para coleta com F
            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.center = new Vector3(0f, -0.65f, 0f);
                boxCol.size = new Vector3(2.4f, 2.8f, 2.4f);
            }
        }
        else if (source == "Flora")
        {
            // ESTRUTURA PARA OS 3 RECURSOS DE FLORA (Melocactus, Árvore de Vagens e Lithos)
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name.Contains("Text") || r.gameObject.name.Contains("Canvas") || r.gameObject.name.Contains("glow"))
                    continue;
                r.enabled = false;
            }

            string itemId = interactable != null && interactable.itemData != null ? interactable.itemData.itemId : "lithos";
            Color floraLightColor = new Color(0.1f, 0.95f, 0.4f);

            if (itemId == "melocactus") floraLightColor = new Color(0.1f, 1.0f, 0.3f);       // Verde Cacto
            else if (itemId == "lithos") floraLightColor = new Color(1.0f, 0.4f, 0.8f);       // Rosa/Magenta Flor
            else floraLightColor = new Color(0.3f, 0.95f, 0.6f);                               // Árvore de Vagens

            GameObject floraPrefab = Resources.Load<GameObject>("SpawnItems/Planta") ?? 
                                     Resources.Load<GameObject>("Flora") ?? 
                                     #if UNITY_EDITOR
                                     UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enviroment/Ambiente/ArvoreDeVagens.prefab") ??
                                     UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enviroment/Ambiente/Flora.prefab") ??
                                     UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enviroment/Flora.prefab");
                                     #else
                                     null;
                                     #endif

            if (floraPrefab != null && transform.Find("FloraVisual") == null)
            {
                GameObject floraInstance = Instantiate(floraPrefab, transform);
                floraInstance.name = "FloraVisual";
                floraInstance.transform.localPosition = Vector3.zero;
                floraInstance.transform.localRotation = Quaternion.identity;
                
                foreach (var c in floraInstance.GetComponentsInChildren<Collider>())
                {
                    Destroy(c);
                }
            }

            // Luz de destaque da Flora
            Light light = GetComponentInChildren<Light>();
            if (light == null)
            {
                GameObject lightGo = new GameObject("FloraGlowLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.3f;
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
            }
            light.color = floraLightColor;
            light.range = 3.0f;
            light.intensity = 1.4f;
        }
        else if (source == "Minerals")
        {
            // ESTRUTURA PREPARADA PARA OS 4 MINÉRIOS OFICIAIS (Pó de Cristal, Prismalita, Geodo, Cristal Explosivo)
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name.Contains("Text") || r.gameObject.name.Contains("Canvas") || r.gameObject.name.Contains("glow"))
                    continue;
                r.enabled = false;
            }

            string itemId = interactable != null && interactable.itemData != null ? interactable.itemData.itemId : "po_de_cristal";
            Color mineralColor = new Color(0.9f, 0.6f, 0.2f);

            if (itemId == "cristal_explosivo") mineralColor = new Color(1.0f, 0.3f, 0.0f); // Laranja Explosivo
            else if (itemId == "geodo") mineralColor = new Color(0.7f, 0.1f, 0.9f);       // Púrputa Vulcânico
            else if (itemId == "prismalita") mineralColor = new Color(0.0f, 0.9f, 1.0f);   // Ciano Prismático
            else mineralColor = new Color(0.9f, 0.9f, 0.95f);                              // Pó de Cristal (Branco Radiante)

            if (transform.Find("MineralVisual") == null && transform.Find("MineralSphere") == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "MineralSphere";
                sphere.transform.SetParent(transform, false);
                sphere.transform.localPosition = Vector3.up * 0.25f;
                sphere.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

                Collider col = sphere.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer sphereRenderer = sphere.GetComponent<MeshRenderer>();
                if (sphereRenderer != null)
                {
                    sphereRenderer.material.color = mineralColor;
                    sphereRenderer.material.EnableKeyword("_EMISSION");
                    sphereRenderer.material.SetColor("_EmissionColor", mineralColor * 0.8f);
                }
            }

            Light light = GetComponentInChildren<Light>();
            if (light == null)
            {
                GameObject lightGo = new GameObject("MineralGlowLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.3f;
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
            }
            light.color = mineralColor;
            light.range = 3.0f;
            light.intensity = 1.4f;
        }

        // Aplica cores de tier de mob APENAS se for um drop de mob com tier (e não um recurso de mapa da Bolsa Sintética)
        bool isMapResource = (interactable != null && interactable.itemData != null && interactable.itemData.returnsToBase) ||
                             (source == "Fauna" || source == "Flora" || source == "Minerals");

        if (!isMapResource)
        {
            ApplyTierColorsToItem();
        }
    }

    private Vector3 initialScale;
    private bool baseScaleSaved = false;

    private void UpdatePulseEffect()
    {
        bool isMapResource = (interactable != null && interactable.itemData != null && interactable.itemData.returnsToBase);
        if (isMapResource) return; // Recursos de mapa NÃO crescem nem diminuem!

        if (!baseScaleSaved)
        {
            initialScale = transform.localScale;
            baseScaleSaved = true;
        }

        // Animação sutil de respiração apenas para drops de mobs com tiers
        float pulseT = (Mathf.Sin(Time.time * 2.6f) + 1.0f) * 0.5f;
        float scaleRatio = Mathf.Lerp(0.85f, 1.0f, pulseT);
        transform.localScale = initialScale * scaleRatio;
    }

    private void UpdateLightPulse()
    {
        bool isMapResource = (interactable != null && interactable.itemData != null && interactable.itemData.returnsToBase);
        if (isMapResource) return; // Recursos de mapa NÃO usam iluminação de tier de mob!

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
        // Executa animações de tier apenas se for drop de mob
        UpdatePulseEffect();
        UpdateLightPulse();

        // Libera coleta após o delay de spawn
        if (!canBePickedUp && Time.time - spawnTime >= pickupDelay)
        {
            canBePickedUp = true;
            // Se o player já estava dentro da zona quando o delay terminou, ativa o UI agora
            if (playerNearby != null)
            {
                if (glowObject != null) glowObject.SetActive(true);
                if (pressFUI != null) pressFUI.SetActive(true);
            }
        }

        // Coleta por tecla F (somente quando player está na zona)
        if (playerNearby != null && canBePickedUp && Input.GetKeyDown(KeyCode.F))
        {
            // Bloqueia coleta se há inimigos ativos
            bool hasActiveEnemies = false;
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.activeInHierarchy) continue;
                DummyHealth health = enemy.GetComponentInChildren<DummyHealth>();
                if (health != null)
                {
                    if (health.CurrentHealth > 0) { hasActiveEnemies = true; break; }
                }
                else
                {
                    hasActiveEnemies = true; break;
                }
            }

            if (hasActiveEnemies)
            {
                if (EptinhoPopupController.instancia != null)
                    EptinhoPopupController.instancia.MostrarPopupAviso("Não é hora de distrações — há inimigos por aqui!");
                return;
            }

            TryCollect(playerNearby);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Sempre registra o player; UI só aparece se o delay já passou
        playerNearby = other.gameObject;
        if (!canBePickedUp) return;

        if (glowObject != null) glowObject.SetActive(true);
        if (pressFUI != null) pressFUI.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerNearby = null;
        if (glowObject != null) glowObject.SetActive(false);
        if (pressFUI != null) pressFUI.SetActive(false);
    }

    /// <summary>
    /// Executa a coleta:
    ///   • returnsToBase == true  → Bolsa Sintética via SaveManager (persistente)
    ///   • returnsToBase == false → Inventário de run via PlayerInventory (temporário)
    /// Aborta sem destruir se o inventário de run estiver cheio.
    /// </summary>
    private void TryCollect(GameObject player)
    {
        if (interactable == null || interactable.foiCatalogado) return;

        if (interactable.itemData == null)
        {
            Debug.LogWarning($"[ITEM] '{gameObject.name}' não tem ItemData no Interactable! " +
                             "Configure o campo 'Item Data' no Inspector do prefab.");
            return;  // não destrói o item enquanto estiver mal configurado
        }
        else if (interactable.itemData.returnsToBase)
        {
            // Recurso permanente — vai direto para a Bolsa Sintética
            if (SaveManager.instance != null)
                SaveManager.instance.AddResourceToBase(interactable.itemData.itemId, 1);
            else
            {
                Debug.LogWarning("[ITEM] SaveManager não encontrado! Recurso perdido: " + interactable.NomeDisplay);
                return;  // não destrói o item se não puder registrar
            }
        }
        else
        {
            // Item de run — vai para o inventário temporário
            PlayerInventory inventory = player.GetComponentInParent<PlayerInventory>();
            if (inventory == null) inventory = player.GetComponent<PlayerInventory>();

            if (inventory != null)
            {
                bool added = inventory.AddItem(interactable.itemData.itemId, 1);
                if (!added)
                {
                    Debug.Log("[ITEM] Inventário cheio! Não coletou: " + interactable.NomeDisplay);
                    return;
                }
            }
            else
            {
                Debug.LogWarning("[ITEM] PlayerInventory não encontrado no player!");
            }
        }

        // Registra no Catálogo do Eptinho (dispara o popup)
        if (CatalogoManager.instancia != null)
            CatalogoManager.instancia.Catalogar(interactable);

        Debug.Log("[ITEM] Coletado: " + interactable.NomeDisplay);
        Destroy(gameObject);
    }
}
