using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Efeito visual de seleção para armas (no Expositor / WeaponRack e no chão / pickups).
/// Cria um contorno dourado radiante estritamente no FUNDO da arma (Inverted Hull Silhouette),
/// garantindo que a frente da arma permaneça 100% visível e limpa sem preenchimento frontal.
/// Usa estritamente os meshes atuais (Quantum Fang para Adaga, Mesh 1.0 para Machado).
/// </summary>
[DisallowMultipleComponent]
public class WeaponSelectionHighlight : MonoBehaviour
{
    [Header("Cores e Aparência do Outline")]
    [Tooltip("Cor dourada radiante do outline (HDR)")]
    [ColorUsage(true, true)]
    public Color goldenColor = new Color(2.0f, 1.6f, 0.25f, 1.0f);

    [Tooltip("Cor caso a arma esteja bloqueada / indisponível")]
    [ColorUsage(true, true)]
    public Color lockedColor = new Color(2.5f, 0.15f, 0.15f, 1.0f);

    [Tooltip("Espessura real da borda no espaço de mundo (metros)")]
    [Range(0.005f, 0.05f)]
    public float outlineWidth = 0.012f;

    [Tooltip("Velocidade do pulso de respiração")]
    public float pulseSpeed = 3.0f;

    [Tooltip("Intensidade da variação do brilho")]
    [Range(0.0f, 0.8f)]
    public float pulseIntensity = 0.25f;

    [Tooltip("Velocidade de transição fade in/out")]
    public float fadeSpeed = 9f;

    [Header("Iluminação Pontual Suave")]
    [Tooltip("Se verdadeiro, cria uma luz pontual dourada sob a arma")]
    public bool enablePointLight = false;
    public Color lightColor = new Color(1.0f, 0.84f, 0.25f, 1.0f);
    public float lightIntensity = 1.0f;
    public float lightRange = 1.2f;

    [Header("Sensação de Seleção Tátil")]
    [Tooltip("Multiplicador de escala da arma selecionada (1.0 = estritamente fixa sem alterar tamanho)")]
    public float selectionScaleMultiplier = 1.0f;

    [Header("Detecção Automática de Proximidade (Opcional para armas no chão)")]
    [Tooltip("Se ativado, detecta a distância até a tag 'Player' sozinho sem depender de script externo")]
    public bool autoProximity = false;
    public float detectionRadius = 2.4f;

    // Estado interno
    private bool isHighlighted = false;
    private bool isLocked = false;
    private float currentAlpha = 0f;
    private Vector3 baseLocalScale = Vector3.one;
    private bool scaleCaptured = false;

    private Material outlineMatInstance;
    private readonly List<GameObject> outlineProxies = new List<GameObject>();
    private Light pointLight;
    private Transform playerTransform;

    private void Awake()
    {
        CaptureBaseScale();
        InitializeOutlineMaterial();
        BuildOutlineProxies();
        if (enablePointLight)
        {
            BuildPointLight();
        }
    }

    private void Start()
    {
        UpdateAlpha(0f);
    }

    private void CaptureBaseScale()
    {
        if (!scaleCaptured)
        {
            baseLocalScale = transform.localScale;
            scaleCaptured = true;
        }
    }

    private void InitializeOutlineMaterial()
    {
        if (outlineMatInstance != null) return;

        Material template = Resources.Load<Material>("WeaponOutline_Material");
        if (template != null)
        {
            outlineMatInstance = new Material(template);
        }
        else
        {
            Shader s = Shader.Find("Custom/WeaponOutline_URP");
            if (s != null)
            {
                outlineMatInstance = new Material(s);
            }
        }

        if (outlineMatInstance != null)
        {
            outlineMatInstance.name = $"{gameObject.name}_OutlineMat_Instance";
            ApplyMaterialProperties();
        }
    }

    private void ApplyMaterialProperties()
    {
        if (outlineMatInstance == null) return;

        Color targetColor = isLocked ? lockedColor : goldenColor;
        outlineMatInstance.SetColor("_OutlineColor", targetColor);
        outlineMatInstance.SetFloat("_OutlineWidth", outlineWidth);
        outlineMatInstance.SetFloat("_PulseSpeed", pulseSpeed);
        outlineMatInstance.SetFloat("_PulseIntensity", pulseIntensity);
        outlineMatInstance.SetFloat("_Alpha", currentAlpha);
    }

    /// <summary>
    /// Encontra estritamente os meshes corretos dos modelos atuais:
    /// - Para Adaga: estritamente o mesh "Quantum Fang".
    /// - Para Machado: estritamente o mesh "1.0" / "Winged Axe".
    /// - Para Pistola: mesh ativo da pistola.
    /// Desativa e ignora quaisquer meshes legados ("dagger_low", "display crystal axe zero", etc.).
    /// </summary>
    public void BuildOutlineProxies()
    {
        ClearProxies();

        if (outlineMatInstance == null) return;

        // Detecta qual arma é para filtragem cirúrgica de mesh
        string rootName = gameObject.name.ToLower();
        bool isDagger = rootName.Contains("dagger") || rootName.Contains("adaga") || transform.Find("Quantum Fang") != null;
        bool isAxe = rootName.Contains("axe") || rootName.Contains("machado");

        // 1. MeshRenderers convencionais
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.gameObject.name.Contains("_OutlineProxy")) continue;

            string goName = mf.gameObject.name.ToLower();
            string meshName = mf.sharedMesh.name.ToLower();

            // Filtragem de Adaga: usar estritamente Quantum Fang
            if (isDagger)
            {
                bool isQuantum = goName.Contains("quantum") || goName.Contains("fang") || meshName.Contains("quantum") || meshName.Contains("fang");
                if (!isQuantum)
                {
                    // Desativa renderers antigos legados (ex: dagger_low, polysurface)
                    MeshRenderer legacyMr = mf.GetComponent<MeshRenderer>();
                    if (legacyMr != null) legacyMr.enabled = false;
                    continue;
                }
            }

            // Filtragem de Machado: usar estritamente Mesh 1.0 (Amethyst Winged Axe)
            if (isAxe)
            {
                bool isNewAxeMesh = goName.Contains("winged") || goName.Contains("amethyst") || meshName.Contains("1.0") || meshName.Contains("winged") || meshName.Contains("amethyst");
                bool isOldAxe = goName.Contains("crystal_axe") || goName.Contains("zero") || meshName.Contains("crystal_axe");
                if (isOldAxe || !isNewAxeMesh)
                {
                    // Desativa renderers antigos legados (ex: display crystal axe zero)
                    MeshRenderer legacyMr = mf.GetComponent<MeshRenderer>();
                    if (legacyMr != null) legacyMr.enabled = false;
                    continue;
                }
            }

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;

            GameObject proxy = new GameObject($"{mf.gameObject.name}_OutlineProxy");
            proxy.transform.SetParent(mf.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            MeshFilter proxyMf = proxy.AddComponent<MeshFilter>();
            proxyMf.sharedMesh = mf.sharedMesh;

            MeshRenderer proxyMr = proxy.AddComponent<MeshRenderer>();
            proxyMr.sharedMaterial = outlineMatInstance;
            proxyMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            proxyMr.receiveShadows = false;

            proxy.SetActive(false);
            outlineProxies.Add(proxy);
        }

        // 2. SkinnedMeshRenderers
        SkinnedMeshRenderer[] smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < smrs.Length; i++)
        {
            SkinnedMeshRenderer smr = smrs[i];
            if (smr == null || smr.sharedMesh == null) continue;
            if (smr.gameObject.name.Contains("_OutlineProxy")) continue;

            string goName = smr.gameObject.name.ToLower();
            string meshName = smr.sharedMesh.name.ToLower();

            if (isDagger && (!goName.Contains("quantum") && !meshName.Contains("quantum")))
            {
                smr.enabled = false;
                continue;
            }

            if (isAxe && (!goName.Contains("winged") && !meshName.Contains("1.0")))
            {
                smr.enabled = false;
                continue;
            }

            if (!smr.enabled) continue;

            GameObject proxy = new GameObject($"{smr.gameObject.name}_OutlineProxy");
            proxy.transform.SetParent(smr.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            SkinnedMeshRenderer proxySmr = proxy.AddComponent<SkinnedMeshRenderer>();
            proxySmr.sharedMesh = smr.sharedMesh;
            proxySmr.bones = smr.bones;
            proxySmr.rootBone = smr.rootBone;
            proxySmr.sharedMaterial = outlineMatInstance;
            proxySmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            proxySmr.receiveShadows = false;

            proxy.SetActive(false);
            outlineProxies.Add(proxy);
        }
    }

    private void BuildPointLight()
    {
        if (pointLight != null) return;

        GameObject lightGO = new GameObject("SelectionPointLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, -0.15f, 0f);

        pointLight = lightGO.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = lightColor;
        pointLight.range = lightRange;
        pointLight.intensity = 0f;
        pointLight.shadows = LightShadows.None;
        pointLight.enabled = false;
    }

    public void SetHighlighted(bool highlight, bool locked = false)
    {
        isHighlighted = highlight;
        isLocked = locked;
        ApplyMaterialProperties();
    }

    private void Update()
    {
        if (autoProximity)
        {
            HandleAutoProximity();
        }

        float targetAlpha = isHighlighted ? 1f : 0f;
        if (!Mathf.Approximately(currentAlpha, targetAlpha))
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            UpdateAlpha(currentAlpha);
        }
    }

    private void HandleAutoProximity()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            bool near = dist <= detectionRadius;
            if (near != isHighlighted)
            {
                SetHighlighted(near, false);
            }
        }
    }

    private void UpdateAlpha(float alpha)
    {
        bool visible = alpha > 0.005f;

        if (outlineMatInstance != null)
        {
            outlineMatInstance.SetFloat("_Alpha", alpha);
        }

        for (int i = 0; i < outlineProxies.Count; i++)
        {
            GameObject proxy = outlineProxies[i];
            if (proxy != null && proxy.activeSelf != visible)
            {
                proxy.SetActive(visible);
            }
        }

        if (pointLight != null)
        {
            pointLight.enabled = visible;
            if (visible)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseIntensity * 0.4f);
                pointLight.intensity = lightIntensity * alpha * pulse;
                pointLight.color = isLocked ? lockedColor : lightColor;
            }
        }

        if (scaleCaptured && selectionScaleMultiplier > 1.0f)
        {
            float scaleFactor = Mathf.Lerp(1.0f, selectionScaleMultiplier, alpha);
            transform.localScale = baseLocalScale * scaleFactor;
        }
    }

    private void ClearProxies()
    {
        for (int i = 0; i < outlineProxies.Count; i++)
        {
            if (outlineProxies[i] != null)
            {
                Destroy(outlineProxies[i]);
            }
        }
        outlineProxies.Clear();
    }

    private void OnDestroy()
    {
        ClearProxies();
        if (pointLight != null)
        {
            Destroy(pointLight.gameObject);
        }
        if (outlineMatInstance != null)
        {
            Destroy(outlineMatInstance);
        }
        if (scaleCaptured)
        {
            transform.localScale = baseLocalScale;
        }
    }
}
