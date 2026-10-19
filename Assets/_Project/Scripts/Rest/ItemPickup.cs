using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Script unificado de coleta de itens no chão (Drops de Mobs e Recursos de Mapa / Bolsa Sintética).
/// Substitui: CharacteristicItemPickup + DetectorDoItem + ItemCollectable.
///
/// RECURSOS DE MAPA (Fauna, Flora, Minerais):
///   • Preserva estritamente os modelos 3D, malhas e materiais originais texturizados (sem alterar cor nem texturas).
///   • Bichos terrestres/ovos NÃO recebem moscas por cima (moscas exclusivas do Vagalume Cristalizado / Fly).
///   • Adiciona luz pontual suave para destacar o item no escuro.
///   • Exibe prompt WorldSpace [ F ] Coletar <Nome do Recurso> com fade suave.
///   • Coleta imediata para a Bolsa Sintética (SaveManager.AddResourceToBase) sem bloqueios indevidos de inimigos distantes.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class ItemPickup : MonoBehaviour
{
    [Header("Destaque e Escala dos Recursos")]
    [Tooltip("Filho do prefab com efeito de brilho (opcional)")]
    public GameObject glowObject;
    [Tooltip("UI 'Pressione F' em World Space, filho do prefab")]
    public GameObject pressFUI;
    [Tooltip("Escala/Tamanho do modelo visual das moscas (padrão: 2.5)")]
    public float flyScale = 2.5f;
    [Tooltip("Altura Y do enxame de moscas acima do chão (padrão: 1.0)")]
    public float flyHeightOffset = 1.0f;

    [Header("Coleta")]
    [Tooltip("Delay após spawn antes de poder ser coletado")]
    public float pickupDelay = 0.15f; // Reduzido para coleta ágil e fluida
    [Tooltip("Tempo em segundos até o item desaparecer. 0 = nunca")]
    public float lifetime = 0f; // Recursos de mapa não expiram por padrão

    [Header("Forçar Categoria (Definido por Room Spawner)")]
    public string forceCategory = "";

    [Header("Luz e Iluminação do Drop")]
    public float lightRange = 2.5f;
    public float minLightIntensity = 1.2f;
    public float maxLightIntensity = 1.6f;
    public float lightPulseSpeed = 2.4f;

    private Interactable interactable;
    private float spawnTime;
    private bool canBePickedUp = false;
    private GameObject playerNearby = null;
    private bool isInitialized = false;

    private CanvasGroup autoPromptCanvasGroup;
    private TextMeshProUGUI autoPromptTMP;
    private Light cachedPointLight;
    private Vector3 initialScale;
    private bool baseScaleSaved = false;

    private static readonly Dictionary<string, string> NameToItemIdMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Lanternas", "arvore_vagens" },
        { "Arvore_vagens", "arvore_vagens" },
        { "Ovocristal", "ovo_cristal_crawler" },
        { "Ovocristalcrawler", "ovo_cristal_crawler" },
        { "Quebradiço", "caracol_geodo" },
        { "Quebradico", "caracol_geodo" },
        { "Caracol", "caracol_geodo" },
        { "Fly", "vagalume_cristalizado" },
        { "Vagalume", "vagalume_cristalizado" },
        { "Melacotus", "melocactus" },
        { "Melocactus", "melocactus" },
        { "Lotus", "lithos" },
        { "Lithos", "lithos" },
        { "Cristal", "po_de_cristal" },
        { "Peixe", "peixe_lagoa" }
    };

    public void EnsureItemData()
    {
        if (interactable == null)
            interactable = GetComponent<Interactable>() ?? gameObject.AddComponent<Interactable>();

        if (interactable.itemData != null)
            return;

        string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
        string targetId = null;

        foreach (var kvp in NameToItemIdMap)
        {
            if (cleanName.IndexOf(kvp.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetId = kvp.Value;
                break;
            }
        }

        if (string.IsNullOrEmpty(targetId))
        {
            targetId = cleanName.ToLower();
        }

        ItemData resolved = null;
        if (ItemDatabase.Instance != null)
        {
            resolved = ItemDatabase.Instance.GetItemData(targetId);
        }

        if (resolved == null)
        {
            resolved = Resources.Load<ItemData>($"ItemData/{targetId}");
        }

        if (resolved != null)
        {
            interactable.itemData = resolved;
            if (string.IsNullOrEmpty(interactable.objetoNome))
            {
                interactable.objetoNome = resolved.itemName;
            }
            if (resolved.returnsToBase)
            {
                forceCategory = resolved.enemySource;
            }
            Debug.Log($"[ITEM PICKUP] ItemData resolvido dinamicamente para '{gameObject.name}' -> {resolved.itemId} ({resolved.itemName})");
        }
    }

    private void SnapToGround()
    {
        bool isFly = (interactable != null && interactable.itemData != null && (interactable.itemData.itemId == "vagalume_cristalizado" || interactable.itemData.itemId == "fly")) ||
                     (gameObject.name.ToLower().Contains("fly") && !gameObject.name.ToLower().Contains("peixe"));
        if (isFly) return;

        Vector3 rayStart = transform.position + Vector3.up * 1.5f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 6.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        float bestDist = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;
        bool found = false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.transform.IsChildOf(transform)) continue;
            if (h.collider.CompareTag("Player") || h.collider.CompareTag("Enemy")) continue;

            string n = h.collider.gameObject.name.ToLower();
            if (n.Contains("crystal") || n.Contains("prop") || h.collider.CompareTag("Prop")) continue;
            if (h.collider.isTrigger && !n.Contains("water")) continue;

            float d = Mathf.Abs(h.point.y - transform.position.y);
            if (d < bestDist)
            {
                bestDist = d;
                bestPoint = h.point;
                found = true;
            }
        }

        if (found)
        {
            transform.position = new Vector3(transform.position.x, bestPoint.y + 0.05f, transform.position.z);
        }
    }

    void Awake()
    {
        interactable = GetComponent<Interactable>() ?? gameObject.AddComponent<Interactable>();
        EnsureItemData();
        EnsureTriggerCollider();

        if (glowObject != null && !IsVisualRendererObject(glowObject)) 
        {
            glowObject.SetActive(false);
        }

        EnsurePromptUI();
    }

    private void EnsureTriggerCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        bool hasTrigger = false;
        foreach (var c in colliders)
        {
            if (c != null && c.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(2.5f, 2.5f, 2.5f);
        }
    }

    private bool IsVisualRendererObject(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.name.Contains("Fly") || obj.name.Contains("Swarm") || obj.name.Contains("Flora") || obj.name.Contains("Mineral")) return true;
        if (obj.GetComponent<SkinnedMeshRenderer>() != null || obj.GetComponent<FlySwarmFX>() != null) return true;
        return false;
    }

    public void InitializeItem(string category)
    {
        forceCategory = category;
        isInitialized = false;
        InitializeItem();
    }

    void Start()
    {
        spawnTime = Time.time;
        if (lifetime > 0) Destroy(gameObject, lifetime);

        EnsureItemData();
        EnsureTriggerCollider();

        if (!IsMapResource())
        {
            // Converte qualquer Mesh de Cubo para Esfera (Círculo 3D) APENAS para drops de mob tipo orbe
            ConvertCubesToSpheres();
        }

        if (!isInitialized)
        {
            InitializeItem();
        }

        SnapToGround();
        EnsurePromptUI();
    }

    public bool IsMapResource()
    {
        if (forceCategory == "Fauna" || forceCategory == "Flora" || forceCategory == "Minerals") return true;

        if (interactable != null && interactable.itemData != null)
        {
            if (interactable.itemData.returnsToBase) return true;
            if (interactable.itemData.enemySource == "Fauna" || interactable.itemData.enemySource == "Flora" || interactable.itemData.enemySource == "Minerals") return true;
        }

        string nameLower = gameObject.name.ToLower();
        if (nameLower.Contains("melacotus") || nameLower.Contains("lotus") || nameLower.Contains("lothus") || 
            nameLower.Contains("lantern") || nameLower.Contains("cristal") || nameLower.Contains("crystal") ||
            nameLower.Contains("quebradi") || nameLower.Contains("ovocristal") || nameLower.Contains("fly") || 
            nameLower.Contains("vagalume") || nameLower.Contains("planta") || nameLower.Contains("plant") ||
            nameLower.Contains("frog") || nameLower.Contains("sapo") || nameLower.Contains("tinker") ||
            nameLower.Contains("stone") || nameLower.Contains("pedra") || nameLower.Contains("carpet") ||
            nameLower.Contains("tapete"))
        {
            return true;
        }

        return false;
    }

    private void ConvertCubesToSpheres()
    {
        if (IsMapResource()) return;

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

    private void ApplyCleanMaterialToRenderers()
    {
        if (IsMapResource()) return;
        ApplyTierColorsToItem();
    }

    private void ApplyTierColorsToItem()
    {
        if (IsMapResource()) return;

        // Se o objeto não for um drop de mob simples tipo orbe ou não vier da pasta de Drops,
        // mas tiver materiais com texturas próprias de modelo 3D, PRESERVA o material e a textura!
        bool isMonsterDrop = gameObject.name.ToLower().Contains("drop") ||
                             gameObject.name.ToLower().Contains("shard") ||
                             gameObject.name.ToLower().Contains("spider") ||
                             gameObject.name.ToLower().Contains("golem") ||
                             gameObject.name.ToLower().Contains("watcher") ||
                             gameObject.name.ToLower().Contains("totem") ||
                             gameObject.name.ToLower().Contains("tuner") ||
                             gameObject.name.ToLower().Contains("peixe_escama") ||
                             gameObject.name.ToLower().Contains("magic_") ||
                             gameObject.name.ToLower().Contains("essência") ||
                             gameObject.name.ToLower().Contains("essencia");

        if (!isMonsterDrop)
        {
            // É um item/modelo 3D do mundo. Preserva malha e texturas autênticas!
            return;
        }

        Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Color tierColor = Color.white;
        if (interactable != null && interactable.itemData != null)
        {
            tierColor = interactable.itemData.GetTierColor();
        }

        // Aplica a cor do Tier no material e na emissão dos MeshRenderers do orbe de mob
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.gameObject.name.Contains("Text") || r.gameObject.name.Contains("Canvas") || r.gameObject.name.Contains("glow"))
                continue;

            // Se o renderizador tem uma textura real, não sobrescreve com cor sólida
            if (r.sharedMaterial != null && (r.sharedMaterial.mainTexture != null || (r.sharedMaterial.HasProperty("_BaseMap") && r.sharedMaterial.GetTexture("_BaseMap") != null)))
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

        // Luz PointLight do item dropado
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

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.startColor = tierColor;
        }

        if (glowObject != null)
        {
            SpriteRenderer glowSr = glowObject.GetComponent<SpriteRenderer>();
            if (glowSr != null) glowSr.color = new Color(tierColor.r, tierColor.g, tierColor.b, 0.8f);
        }
    }

    public void InitializeItem()
    {
        if (isInitialized) return;
        EnsureItemData();
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
            if (nameLower.Contains("planta") || nameLower.Contains("musgo") || nameLower.Contains("lotus") || nameLower.Contains("melacotus")) source = "Flora";
            else if (nameLower.Contains("frog") || nameLower.Contains("sapo") || nameLower.Contains("larva") || nameLower.Contains("tinker") || nameLower.Contains("quebradi") || nameLower.Contains("ovocristal")) source = "Fauna";
            else if (nameLower.Contains("crystal") || nameLower.Contains("cristal") || nameLower.Contains("stone") || nameLower.Contains("pedra")) source = "Minerals";
        }

        // Se for um recurso de mapa (Fauna, Flora, Minérios), direciona para a Bolsa Sintética
        if (source == "Fauna" || source == "Flora" || source == "Minerals" || IsMapResource())
        {
            if (interactable.itemData != null)
            {
                interactable.itemData.returnsToBase = true;
            }
            CustomizeItemVisuals(source);
        }
    }

    private void CustomizeItemVisuals(string source)
    {
        if (source == "Fauna")
        {
            bool isFish = (interactable != null && interactable.itemData != null && interactable.itemData.itemId == "peixe_lagoa") ||
                          gameObject.name.ToLower().Contains("peixe");
            bool isFly = !isFish && (((interactable != null && interactable.itemData != null && (interactable.itemData.itemId == "vagalume_cristalizado" || interactable.itemData.itemId == "fly")) ||
                         gameObject.name.ToLower().Contains("fly") || gameObject.name.ToLower().Contains("vagalume")));

            if (isFish)
            {
                Transform fishTransform = transform.Find("LakeFishVisual");
                if (fishTransform == null)
                {
                    GameObject fishGo = new GameObject("LakeFishVisual");
                    fishGo.transform.SetParent(transform, false);
                    fishGo.transform.localPosition = Vector3.zero;

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

                if (fishTransform != null) fishTransform.gameObject.SetActive(true);
            }
            else if (isFly)
            {
                // Enxame de moscas APENAS para Vagalume Cristalizado / Fly
                Transform flyTransform = transform.Find("FlyVisual");
                if (flyTransform == null)
                {
                    GameObject flyPrefab = Resources.Load<GameObject>("SpawnItems/Fly") ?? Resources.Load<GameObject>("Fly");
                    if (flyPrefab != null)
                    {
                        GameObject flyInstance = Instantiate(flyPrefab, transform);
                        flyInstance.name = "FlyVisual";
                        flyInstance.transform.localPosition = Vector3.up * flyHeightOffset;
                        flyInstance.transform.localRotation = Quaternion.identity;
                        flyInstance.transform.localScale = new Vector3(flyScale, flyScale, flyScale);
                        flyTransform = flyInstance.transform;

                        foreach (var c in flyInstance.GetComponentsInChildren<Collider>()) Destroy(c);
                        if (flyInstance.GetComponent<FlySwarmFX>() == null) flyInstance.AddComponent<FlySwarmFX>();
                    }
                }

                if (flyTransform != null) flyTransform.gameObject.SetActive(true);
            }
            else
            {
                // Bicho terrestre/ovos (ex: Caracol Geodo / Quebradiço, Ovos de Cristal):
                // NUNCA coloca moscas em cima! Mantém o modelo 3D original com sua textura nativa!
                EnsureMeshRenderersActiveAndLit();
            }

            // Colisor adequado para interação
            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.5f, 0f);
            boxCol.size = new Vector3(2.5f, 2.5f, 2.5f);
        }
        else if (source == "Flora")
        {
            // Mantém os modelos 3D originais com todas as suas texturas (Melocactus, Lotus, Lanternas)
            EnsureMeshRenderersActiveAndLit();

            // Luz suave bioluminescente
            string itemId = interactable != null && interactable.itemData != null ? interactable.itemData.itemId : "";
            Color floraLightColor = new Color(0.1f, 0.95f, 0.4f);
            if (itemId == "melocactus") floraLightColor = new Color(0.1f, 1.0f, 0.3f);
            else if (itemId == "lithos") floraLightColor = new Color(1.0f, 0.4f, 0.8f);
            else floraLightColor = new Color(0.3f, 0.95f, 0.6f);

            Light light = GetComponentInChildren<Light>();
            if (light == null)
            {
                GameObject lightGo = new GameObject("FloraGlowLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.4f;
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
            }
            light.color = floraLightColor;
            light.range = 3.2f;
            light.intensity = 1.4f;

            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.5f, 0f);
            boxCol.size = new Vector3(2.5f, 2.5f, 2.5f);
        }
        else if (source == "Minerals")
        {
            // Mantém o modelo 3D do minério com sua textura/material original
            EnsureMeshRenderersActiveAndLit();

            string itemId = interactable != null && interactable.itemData != null ? interactable.itemData.itemId : "po_de_cristal";
            Color mineralColor = new Color(0.9f, 0.6f, 0.2f);
            if (itemId == "cristal_explosivo") mineralColor = new Color(1.0f, 0.3f, 0.0f);
            else if (itemId == "geodo") mineralColor = new Color(0.7f, 0.1f, 0.9f);
            else if (itemId == "prismalita") mineralColor = new Color(0.0f, 0.9f, 1.0f);
            else mineralColor = new Color(0.9f, 0.9f, 0.95f);

            Light light = GetComponentInChildren<Light>();
            if (light == null)
            {
                GameObject lightGo = new GameObject("MineralGlowLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.4f;
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
            }
            light.color = mineralColor;
            light.range = 3.2f;
            light.intensity = 1.4f;

            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.5f, 0f);
            boxCol.size = new Vector3(2.5f, 2.5f, 2.5f);
        }
    }

    private void EnsureMeshRenderersActiveAndLit()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.gameObject.name.Contains("Canvas") || r.gameObject.name.Contains("Prompt") || r.gameObject.name.Contains("glow"))
                continue;

            r.enabled = true;
            r.gameObject.SetActive(true);
        }
    }

    private void EnsurePromptUI()
    {
        bool hasValidPrompt = false;
        if (pressFUI != null)
        {
            // Verifica se o pressFUI configurado possui texto legível
            TextMeshProUGUI tmp = pressFUI.GetComponentInChildren<TextMeshProUGUI>(true);
            Text legacyTxt = pressFUI.GetComponentInChildren<Text>(true);
            if (tmp != null || legacyTxt != null)
            {
                hasValidPrompt = true;
            }
        }

        if (!hasValidPrompt)
        {
            // Se pressFUI for nulo ou for apenas um Canvas vazio sem texto, recria limpo
            if (pressFUI != null && autoPromptTMP == null)
            {
                Destroy(pressFUI);
                pressFUI = null;
            }
            BuildAutoPromptUI();
        }
        else
        {
            pressFUI.SetActive(false);
        }
    }

    private void BuildAutoPromptUI()
    {
        if (pressFUI != null && autoPromptTMP != null) return;

        GameObject promptRoot = new GameObject("ItemPickup_PromptUI");
        promptRoot.transform.SetParent(transform, false);
        promptRoot.transform.localPosition = Vector3.up * 1.0f;

        Canvas canvas = promptRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 150;

        RectTransform rect = promptRoot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 65f);
        rect.localScale = Vector3.one * 0.01f;

        autoPromptCanvasGroup = promptRoot.AddComponent<CanvasGroup>();
        autoPromptCanvasGroup.alpha = 0f;

        // Fundo sutil translúcido
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptRoot.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.75f);
        bgImg.raycastTarget = false;

        // Texto com TMPro
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptRoot.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        autoPromptTMP = textObj.AddComponent<TextMeshProUGUI>();
        autoPromptTMP.fontSize = 20f;
        autoPromptTMP.fontStyle = FontStyles.Bold;
        autoPromptTMP.alignment = TextAlignmentOptions.Center;
        autoPromptTMP.color = Color.white;
        autoPromptTMP.raycastTarget = false;
        autoPromptTMP.enableAutoSizing = true;
        autoPromptTMP.fontSizeMin = 14f;
        autoPromptTMP.fontSizeMax = 22f;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (font != null) autoPromptTMP.font = font;

        pressFUI = promptRoot;
        pressFUI.SetActive(false);
    }

    private void UpdatePulseEffect()
    {
        if (IsMapResource()) return;

        if (!baseScaleSaved)
        {
            initialScale = transform.localScale;
            baseScaleSaved = true;
        }

        float pulseT = (Mathf.Sin(Time.time * 2.6f) + 1.0f) * 0.5f;
        float scaleRatio = Mathf.Lerp(0.85f, 1.0f, pulseT);
        transform.localScale = initialScale * scaleRatio;
    }

    private void UpdateLightPulse()
    {
        if (IsMapResource()) return;

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
        if (!IsMapResource())
        {
            UpdatePulseEffect();
            UpdateLightPulse();
        }

        // Libera coleta após o delay de spawn
        if (!canBePickedUp && Time.time - spawnTime >= pickupDelay)
        {
            canBePickedUp = true;
        }

        // Atualiza visibilidade e orientação do prompt WorldSpace
        UpdatePromptUI();

        // Coleta de itens:
        // Drops de combate / monstros são coletados imediatamente ao encostar (walkover)!
        // Recursos permanentes da Bolsa Sintética utilizam a tecla de interação configurada.
        if (playerNearby != null && canBePickedUp)
        {
            EnsureItemData();
            bool isCombatDrop = (interactable != null && interactable.itemData != null && !interactable.itemData.returnsToBase);

            if (isCombatDrop)
            {
                TryCollect(playerNearby);
            }
            else if (Input.GetKeyDown(GetCurrentInteractKey()))
            {
                TryCollect(playerNearby);
            }
        }
    }

    private void UpdatePromptUI()
    {
        if (pressFUI == null) EnsurePromptUI();
        if (pressFUI == null) return;

        bool isCombatDrop = (interactable != null && interactable.itemData != null && !interactable.itemData.returnsToBase);
        bool showPrompt = (playerNearby != null && canBePickedUp && !isCombatDrop);

        if (autoPromptCanvasGroup != null)
        {
            float targetAlpha = showPrompt ? 1f : 0f;
            autoPromptCanvasGroup.alpha = Mathf.MoveTowards(autoPromptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 8f);
            pressFUI.SetActive(autoPromptCanvasGroup.alpha > 0.01f);

            if (showPrompt && autoPromptTMP != null && interactable != null)
            {
                KeyCode key = GetCurrentInteractKey();
                string displayName = !string.IsNullOrEmpty(interactable.NomeDisplay) ? interactable.NomeDisplay : "Item";
                autoPromptTMP.text = $"[ <color=#FFE500>{key}</color> ]  <color=#FFE500><b>Coletar {displayName}</b></color>";
            }

            if (Camera.main != null && pressFUI.activeSelf)
            {
                pressFUI.transform.forward = Camera.main.transform.forward;
            }
        }
        else
        {
            pressFUI.SetActive(showPrompt);
            if (showPrompt && Camera.main != null)
            {
                pressFUI.transform.forward = Camera.main.transform.forward;
            }
        }
    }

    private KeyCode GetCurrentInteractKey()
    {
        string savedKey = PlayerPrefs.GetString("KeyInteract", "F");
        try 
        {
            return (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey);
        }
        catch 
        {
            return KeyCode.F;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        playerNearby = other.GetComponentInParent<PlayerHealth>()?.gameObject ?? 
                       other.GetComponentInParent<PlayerInventory>()?.gameObject ?? 
                       other.gameObject;
        if (glowObject != null) glowObject.SetActive(true);

        // Se já puder ser coletado e for drop de combate, coleta no mesmo frame
        if (canBePickedUp)
        {
            EnsureItemData();
            if (interactable != null && interactable.itemData != null && !interactable.itemData.returnsToBase)
            {
                TryCollect(playerNearby);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        playerNearby = null;
        if (glowObject != null) glowObject.SetActive(false);
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.GetComponentInParent<PlayerHealth>() != null) return true;
        if (other.GetComponentInParent<PlayerInventory>() != null) return true;
        return false;
    }

    /// <summary>
    /// Executa a coleta imediata:
    ///   • returnsToBase == true  → Bolsa Sintética via SaveManager (persistente)
    ///   • returnsToBase == false → Inventário de run via PlayerInventory (temporário)
    /// </summary>
    private void TryCollect(GameObject player)
    {
        if (interactable == null || interactable.foiCatalogado) return;

        EnsureItemData();

        if (interactable.itemData == null)
        {
            Debug.LogWarning($"[ITEM] '{gameObject.name}' não tem ItemData no Interactable!");
            return;
        }

        // Toca som de coleta se disponível
        PlayerInventory playerInv = player.GetComponentInParent<PlayerInventory>() ?? 
                                    player.GetComponent<PlayerInventory>() ?? 
                                    FindFirstObjectByType<PlayerInventory>();
        if (playerInv != null)
        {
            playerInv.PlayItemPickupSound();
        }

        if (interactable.itemData.returnsToBase)
        {
            // Recurso permanente — vai direto para a Bolsa Sintética
            if (SaveManager.instance != null)
            {
                SaveManager.instance.AddResourceToBase(interactable.itemData.itemId, 1);
                EptinhoStoryDialogs.TriggerFirstResourceMined();
            }
            else
            {
                Debug.LogWarning("[ITEM] SaveManager não encontrado! Recurso perdido: " + interactable.NomeDisplay);
                return;
            }
        }
        else
        {
            // Item de run — vai para o inventário temporário
            if (playerInv != null)
            {
                bool added = playerInv.AddItem(interactable.itemData.itemId, 1);
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

        // Registra no Catálogo do Eptinho
        if (CatalogoManager.instancia != null)
            CatalogoManager.instancia.Catalogar(interactable);

        Debug.Log("[ITEM] Coletado com sucesso: " + interactable.NomeDisplay);
        Destroy(gameObject);
    }
}
