using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class WeaponRackSlot
{
    [Header("Identificação da Arma")]
    public string weaponId = "dagger";
    public string displayName = "ADAGA DE PLASMA";
    [TextArea(2, 3)]
    public string description = "Ataques rápidos e ágeis de curto alcance.";
    public WeaponType weaponType = WeaponType.Dagger;

    [Header("Prefabs e Modelos")]
    [Tooltip("Prefab da arma real a ser instanciada na mão do jogador.")]
    public GameObject weaponPrefab;

    [Header("Posição no Expositor")]
    [Tooltip("Transform da âncora deste slot no cenário. Pode ser movido e rotacionado na Scene View.")]
    public Transform displayPoint;

    [Header("Regras de Desbloqueio")]
    [Tooltip("Se marcado, a arma já vem liberada para teste imediato.")]
    public bool unlockedByDefault = true;
    [Tooltip("ID do item ou equipamento craftado no save necessário para liberar.")]
    public string requiredCraftId = "";

    [HideInInspector] public GameObject displayInstance;
    [HideInInspector] public WeaponSelectionHighlight highlight;
}

/// <summary>
/// Gerencia o Expositor de Armas / Armeiro no Lobby da Base.
/// - 3 Baias Físicas Fixas com auto-correção de coordenadas (nunca mais ficam embaixo do chão!).
/// - Remove scripts intrusivos (ItemFloat, Dagger_Pickup, Rigidbodies) da réplica para não teleportar.
/// - Silhueta Total Black para armas bloqueadas (você vê a forma 3D da arma, mas toda preta e bloqueada).
/// - UI de interação projetada à frente e elevada (sem clipping com armas ou prateleiras).
/// </summary>
[ExecuteAlways]
public class WeaponRack : MonoBehaviour
{
    public static WeaponRack Instance { get; private set; }

    [Header("Âncoras dos Slots (Mova e Gire na Scene View com W / E)")]
    [Tooltip("Ponto onde a Adaga fica posicionada no suporte.")]
    public Transform slot1_AdagaAnchor;
    [Tooltip("Ponto onde o Machado fica posicionado no suporte.")]
    public Transform slot2_MachadoAnchor;
    [Tooltip("Ponto onde a Pistola fica posicionada no suporte.")]
    public Transform slot3_PistolaAnchor;

    [Header("Slots do Expositor")]
    public List<WeaponRackSlot> slots = new List<WeaponRackSlot>();

    [Header("Ajuste Fino de Rotação das Armas (Graus)")]
    [Tooltip("Rotação extra para a Adaga ficar perfeita no gancho.")]
    public Vector3 adagaRotationOffset = Vector3.zero;
    [Tooltip("Rotação extra para o Machado ficar perfeito no suporte (ex: 0, 90, 0 ou -90, 0, 0).")]
    public Vector3 machadoRotationOffset = Vector3.zero;
    [Tooltip("Rotação extra para a Pistola ficar perfeita no suporte.")]
    public Vector3 pistolaRotationOffset = Vector3.zero;

    [Header("Comportamento Visual")]
    [Tooltip("Se marcado, as armas giram suavemente no expositor. Se desmarcado, ficam estáticas penduradas no rack.")]
    public bool girarArmas = false;

    [Header("Animação de Flutuação")]
    public float floatAmplitude = 0.04f;
    public float floatFrequency = 2.0f;
    public float rotateSpeed = 30f;

    [Header("Interação")]
    public float interactionDistance = 2.5f;
    public float fadeSpeed = 7f;
    public KeyCode fallbackKey = KeyCode.F;

    [Header("Prompt Visual & Posição")]
    [Tooltip("Elevação em metros acima do ponto da arma para exibir o prompt (por cima do expositor).")]
    public float promptHeightOffset = 1.45f;
    [Tooltip("Avanço em metros na direção da câmera para ficar à frente da malha do expositor.")]
    public float promptForwardOffset = 1.10f;
    [Tooltip("Escala do prompt no mundo.")]
    public float promptScale = 0.007f;
    [Tooltip("Tamanho da fonte do prompt (aumentado em 2x para legibilidade).")]
    public float promptFontSize = 50f;
    [Tooltip("Se verdadeiro, renderiza o texto e fundo com ZTest Always (sempre por cima da malha do weaponrack).")]
    public bool renderPromptOnTop = true;

    [Header("Estilo Visual das Armas")]
    [Tooltip("Se marcado, armas bloqueadas viram silhueta preta. Se desmarcado (recomendado), exibem o modelo real e destacam com outline quando selecionadas.")]
    public bool usarSilhuetaArmaBloqueada = false;

    [Header("Feedback Visual & Áudio")]
    public AudioClip equipSound;
    public AudioClip lockedSound;

    private Transform playerTransform;
    private Player_WeaponManager playerWeaponManager;
    private WeaponRackSlot activeHoverSlot;

    // Raiz independente em coordenadas de mundo com escala pura 1.0
    private GameObject visualsContainer;

    // UI Flutuante World-Space
    private GameObject promptRoot;
    private TextMeshProUGUI promptTMP;
    private CanvasGroup promptCanvasGroup;

    // Cache de materiais para o efeito Black Silhouette nas armas bloqueadas
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Material darkSilhouetteMat;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureAnchors();
            EnsureDefaultSlots();
        }
    }

    private void Start()
    {
        EnsureAnchors();
        ResetAnchorRotations();
        EnsureDefaultSlots();

        if (Application.isPlaying)
        {
            EnsureRackModel();
            BuildVisualsContainer();
            BuildDisplayVisuals();
            BuildFloatingPromptUI();

            // Adiciona marcador de objetivo para o tutorial
            if (GetComponent<ObjectiveMarker>() == null)
            {
                ObjectiveMarker marker = gameObject.AddComponent<ObjectiveMarker>();
                marker.condition = ObjectiveMarker.TriggerCondition.ByTutorialStep;
                marker.activeOnStep = TutorialStep.EquiparNoRack;
                marker.heightOffset = 1.9f;
                marker.markerSize = new Vector2(48f, 48f);
            }
        }
    }

    private void OnEnable()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.stepChangedEvent += OnTutorialStepChanged;
        }
    }

    private void OnDisable()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.stepChangedEvent -= OnTutorialStepChanged;
        }
    }

    private void OnTutorialStepChanged(TutorialStep step)
    {
        UpdateFloatingAnimations();
    }

    private void OnDestroy()
    {
        if (visualsContainer != null) DestroyImmediate(visualsContainer);
        if (promptRoot != null) DestroyImmediate(promptRoot);
        if (Instance == this) Instance = null;
    }

    [ContextMenu("Resetar Posições Padrão das Âncoras")]
    public void ResetAnchors()
    {
        EnsureAnchors(true);
        ResetAnchorRotations();
        EnsureDefaultSlots();
    }

    /// <summary>
    /// Zera qualquer rotação de baia que tenha ficado em 90 graus no prefab ou cena.
    /// Garante que todas as armas fiquem retas com rotação 0.
    /// </summary>
    public void ResetAnchorRotations()
    {
        if (slot1_AdagaAnchor != null) slot1_AdagaAnchor.localRotation = Quaternion.identity;
        if (slot2_MachadoAnchor != null) slot2_MachadoAnchor.localRotation = Quaternion.identity;
        if (slot3_PistolaAnchor != null) slot3_PistolaAnchor.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Garante que existam 3 GameObjects filhos dedicados para as baias no Editor.
    /// Auto-corrige qualquer âncora que tenha ficado embaixo do chão por bugs anteriores!
    /// </summary>
    public void EnsureAnchors(bool forceReset = false)
    {
        Vector3 rackBasePos = transform.position;

        // Calcula direções horizontais niveladas no mundo
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.1f) fwd = Vector3.ProjectOnPlane(-transform.up, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.1f) fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.1f) fwd = Vector3.forward;

        Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;

        float minY = rackBasePos.y + 0.35f; // Qualquer âncora abaixo de 35cm da base está no chão ou abaixo!

        // Slot 1: Adaga
        if (slot1_AdagaAnchor == null || forceReset || slot1_AdagaAnchor.position.y < minY)
        {
            Transform existing = transform.Find("Slot_1_Adaga");
            if (existing != null && (forceReset || existing.position.y < minY))
            {
                DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                slot1_AdagaAnchor = existing;
                slot1_AdagaAnchor.localRotation = Quaternion.identity;
            }
            else
            {
                GameObject go = new GameObject("Slot_1_Adaga");
                go.transform.SetParent(transform, true);
                go.transform.position = rackBasePos - (rgt * 0.75f) + (Vector3.up * 1.15f) + (fwd * 0.22f);
                go.transform.localRotation = Quaternion.identity;
                slot1_AdagaAnchor = go.transform;
            }
        }

        // Slot 2: Machado
        if (slot2_MachadoAnchor == null || forceReset || slot2_MachadoAnchor.position.y < minY)
        {
            Transform existing = transform.Find("Slot_2_Machado");
            if (existing != null && (forceReset || existing.position.y < minY))
            {
                DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                slot2_MachadoAnchor = existing;
                slot2_MachadoAnchor.localRotation = Quaternion.identity;
            }
            else
            {
                GameObject go = new GameObject("Slot_2_Machado");
                go.transform.SetParent(transform, true);
                go.transform.position = rackBasePos + (Vector3.up * 1.15f) + (fwd * 0.22f);
                go.transform.localRotation = Quaternion.identity;
                slot2_MachadoAnchor = go.transform;
            }
        }

        // Slot 3: Pistola
        if (slot3_PistolaAnchor == null || forceReset || slot3_PistolaAnchor.position.y < minY)
        {
            Transform existing = transform.Find("Slot_3_Pistola");
            if (existing != null && (forceReset || existing.position.y < minY))
            {
                DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                slot3_PistolaAnchor = existing;
                slot3_PistolaAnchor.localRotation = Quaternion.identity;
            }
            else
            {
                GameObject go = new GameObject("Slot_3_Pistola");
                go.transform.SetParent(transform, true);
                go.transform.position = rackBasePos + (rgt * 0.75f) + (Vector3.up * 1.15f) + (fwd * 0.22f);
                go.transform.localRotation = Quaternion.identity;
                slot3_PistolaAnchor = go.transform;
            }
        }
    }

    private void EnsureDefaultSlots()
    {
        if (slots == null || slots.Count == 0)
        {
            slots = new List<WeaponRackSlot>();

            // Slot 0: Adaga de Plasma (Já liberada)
            slots.Add(new WeaponRackSlot
            {
                weaponId = "dagger",
                displayName = "ADAGA DE PLASMA",
                description = "Golpes velozes de curto alcance com acúmulo de combo ágil.",
                weaponType = WeaponType.Dagger,
                unlockedByDefault = true,
                requiredCraftId = "",
                weaponPrefab = Resources.Load<GameObject>("dagger_low")
            });

            // Slot 1: Machado de Cristal (Desbloqueado gratuitamente desde o início)
            slots.Add(new WeaponRackSlot
            {
                weaponId = "crystal_axe",
                displayName = "MACHADO DE CRISTAL",
                description = "Golpes pesados devastadores em área com alto impacto.",
                weaponType = WeaponType.Axe,
                unlockedByDefault = true,
                requiredCraftId = "eq_crystal_axe",
                weaponPrefab = Resources.Load<GameObject>("Crystal_Axe")
            });

            // Slot 2: Pistola de Plasma (Desbloqueável no Robozinho)
            slots.Add(new WeaponRackSlot
            {
                weaponId = "gunfake",
                displayName = "PISTOLA DE PLASMA",
                description = "Disparos de plasma de médio alcance com recarga rápida.",
                weaponType = WeaponType.Gun,
                unlockedByDefault = false,
                requiredCraftId = "eq_arma_projetil",
                weaponPrefab = Resources.Load<GameObject>("gunfake")
            });
        }

        if (slots.Count > 0) slots[0].displayPoint = slot1_AdagaAnchor;
        if (slots.Count > 1) slots[1].displayPoint = slot2_MachadoAnchor;
        if (slots.Count > 2) slots[2].displayPoint = slot3_PistolaAnchor;
    }

    private void BuildVisualsContainer()
    {
        if (visualsContainer != null) return;
        visualsContainer = new GameObject("WeaponRack_VisualsContainer");
        visualsContainer.transform.position = transform.position;
        visualsContainer.transform.rotation = Quaternion.identity;
        visualsContainer.transform.localScale = Vector3.one;
    }

    private void EnsureRackModel()
    {
        if (GetComponentInChildren<MeshFilter>() != null || GetComponentInChildren<SkinnedMeshRenderer>() != null)
            return;

        GameObject rackPrefab = Resources.Load<GameObject>("weaponrack");
        if (rackPrefab != null)
        {
            GameObject model = Instantiate(rackPrefab, transform);
            model.name = "RackModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(-90f, -90f, 0f);
            model.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        }
    }

    /// <summary>
    /// Instancia os modelos das armas flutuando sobre cada pedestal em escala real 1.0.
    /// Remove qualquer script de movimentação/pickup original (como ItemFloat ou Dagger_Pickup) para não teleportar!
    /// </summary>
    private void BuildDisplayVisuals()
    {
        if (visualsContainer == null) BuildVisualsContainer();

        for (int i = 0; i < slots.Count; i++)
        {
            WeaponRackSlot slot = slots[i];
            if (slot.displayPoint == null) continue;

            if (slot.weaponPrefab == null)
            {
                if (slot.weaponType == WeaponType.Dagger)
                    slot.weaponPrefab = Resources.Load<GameObject>("dagger_low");
                else if (slot.weaponType == WeaponType.Axe)
                    slot.weaponPrefab = Resources.Load<GameObject>("Crystal_Axe");
                else if (slot.weaponType == WeaponType.Gun)
                    slot.weaponPrefab = Resources.Load<GameObject>("gunfake");
            }

            if (slot.weaponPrefab != null && slot.displayInstance == null)
            {
                // Instancia DIRETAMENTE na posição do displayPoint
                GameObject displayObj = Instantiate(slot.weaponPrefab, slot.displayPoint.position, slot.displayPoint.rotation, visualsContainer.transform);
                displayObj.name = $"Display_{slot.weaponId}";
                displayObj.transform.localScale = slot.weaponPrefab.transform.localScale;

                // Destrói rigidbodies, colliders e MONOBEHAVIOURS intrusivos (ItemFloat, Dagger_Pickup, Hitboxes)
                // Isso impede 100% que a réplica teleporte para (0,0,0) ou caia pela física!
                foreach (var rb in displayObj.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
                foreach (var col in displayObj.GetComponentsInChildren<Collider>(true)) Destroy(col);
                foreach (var mb in displayObj.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);

                // Armazena materiais originais para restauração
                // Ativa estritamente os meshes corretos atuais e desativa meshes legados
                if (slot.weaponType == WeaponType.Dagger)
                {
                    // Adaga: Usar estritamente o modelo Quantum Fang
                    foreach (Transform child in displayObj.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == null || child == displayObj.transform) continue;
                        string cName = child.name.ToLower();
                        bool isQuantum = cName.Contains("quantum") || cName.Contains("fang");
                        child.gameObject.SetActive(isQuantum);
                    }
                }
                else if (slot.weaponType == WeaponType.Axe)
                {
                    // Machado: Usar estritamente o modelo 1.0 (Amethyst Winged Axe)
                    foreach (Transform child in displayObj.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == null || child == displayObj.transform) continue;
                        string cName = child.name.ToLower();
                        bool isNewAxe = cName.Contains("winged") || cName.Contains("amethyst");
                        bool isOldAxe = cName.Contains("crystal_axe") || cName.Contains("zero") || cName.Contains("pont");
                        if (isOldAxe) child.gameObject.SetActive(false);
                        else if (isNewAxe) child.gameObject.SetActive(true);
                    }
                }

                // Armazena materiais originais APENAS de renderers ativos e habilitados
                foreach (Renderer r in displayObj.GetComponentsInChildren<Renderer>(false))
                {
                    if (r != null && r.enabled && !r.gameObject.name.Contains("_OutlineProxy"))
                    {
                        originalMaterials[r] = r.sharedMaterials;
                    }
                }

                // Adiciona o efeito visual de selecao com outline dourado no FUNDO
                WeaponSelectionHighlight highlight = displayObj.AddComponent<WeaponSelectionHighlight>();
                highlight.outlineWidth = 0.012f;
                highlight.enablePointLight = false;
                highlight.selectionScaleMultiplier = 1.0f;
                slot.highlight = highlight;

                slot.displayInstance = displayObj;
            }
        }
    }


    private Material GetDarkSilhouetteMaterial()
    {
        if (darkSilhouetteMat == null)
        {
            darkSilhouetteMat = Resources.Load<Material>("LockedWeapon_DarkSilhouette");
            if (darkSilhouetteMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ??
                           Shader.Find("Universal Render Pipeline/Unlit") ??
                           Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s != null)
                {
                    darkSilhouetteMat = new Material(s);
                    darkSilhouetteMat.name = "LockedWeapon_DarkSilhouette_Dynamic";
                    Color matteBlack = new Color(0.05f, 0.05f, 0.06f, 1f);
                    if (darkSilhouetteMat.HasProperty("_BaseColor")) darkSilhouetteMat.SetColor("_BaseColor", matteBlack);
                    if (darkSilhouetteMat.HasProperty("_Color")) darkSilhouetteMat.SetColor("_Color", matteBlack);
                    if (darkSilhouetteMat.HasProperty("_Smoothness")) darkSilhouetteMat.SetFloat("_Smoothness", 0.15f);
                    if (darkSilhouetteMat.HasProperty("_Metallic")) darkSilhouetteMat.SetFloat("_Metallic", 0.1f);
                    if (darkSilhouetteMat.HasProperty("_EmissionColor")) darkSilhouetteMat.SetColor("_EmissionColor", Color.black);
                }
            }
        }
        return darkSilhouetteMat;
    }

    private void ApplySlotMaterialStyle(WeaponRackSlot slot, bool isUnlocked)
    {
        if (slot.displayInstance == null) return;

        foreach (Renderer r in slot.displayInstance.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            // NUNCA sobrescreve o material dos proxies de outline!
            if (r.gameObject.name.Contains("_OutlineProxy")) continue;

            if (!isUnlocked && usarSilhuetaArmaBloqueada)
            {
                Material silhouetteMat = GetDarkSilhouetteMaterial();
                if (silhouetteMat != null)
                {
                    Material[] blackMats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < blackMats.Length; m++) blackMats[m] = silhouetteMat;
                    r.sharedMaterials = blackMats;
                }
            }
            else
            {
                // Restaura as texturas e materiais originais para arma
                if (originalMaterials.TryGetValue(r, out Material[] orig) && orig != null && orig.Length > 0 && orig[0] != null)
                {
                    r.sharedMaterials = orig;
                }
                else if (slot.weaponType == WeaponType.Dagger)
                {
                    Material daggerMat = Resources.Load<Material>("Dagger");
                    if (daggerMat != null) r.sharedMaterial = daggerMat;
                }
            }
        }
    }

    private void BuildFloatingPromptUI()
    {
        if (promptRoot != null) return;

        promptRoot = new GameObject("WeaponRack_WorldPromptUI");
        promptRoot.transform.SetParent(visualsContainer.transform, false);

        Canvas canvas = promptRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        RectTransform rect = promptRoot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(680f, 130f);
        rect.localScale = Vector3.one * promptScale;

        promptCanvasGroup = promptRoot.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        // Fundo cinza bem sutil e transparente
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptRoot.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.07f, 0.35f);
        bgImg.raycastTarget = false;

        if (renderPromptOnTop)
        {
            Shader uiShader = Shader.Find("UI/Default");
            if (uiShader != null)
            {
                Material bgMat = new Material(uiShader);
                bgMat.SetFloat(Shader.PropertyToID("unity_GUIZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
                bgMat.renderQueue = 3999;
                bgImg.material = bgMat;
            }
        }

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptRoot.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        promptTMP = textObj.AddComponent<TextMeshProUGUI>();
        promptTMP.fontSize = promptFontSize;
        promptTMP.fontStyle = FontStyles.Bold;
        promptTMP.alignment = TextAlignmentOptions.Center;
        promptTMP.color = Color.white;
        promptTMP.raycastTarget = false;
        promptTMP.outlineWidth = 0.22f;
        promptTMP.outlineColor = new Color32(0, 0, 0, 220);
        promptTMP.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (font != null) promptTMP.font = font;

        if (renderPromptOnTop && promptTMP.fontSharedMaterial != null)
        {
            Material fontOverlayMat = new Material(promptTMP.fontSharedMaterial);
            Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay") ??
                                   Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            if (overlayShader != null)
            {
                fontOverlayMat.shader = overlayShader;
            }
            fontOverlayMat.SetFloat(Shader.PropertyToID("unity_GUIZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            fontOverlayMat.SetFloat(Shader.PropertyToID("_ZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            fontOverlayMat.renderQueue = 4000;
            promptTMP.fontMaterial = fontOverlayMat;
        }

        promptRoot.SetActive(false);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        UpdateFloatingAnimations();
        HandlePlayerProximity();
    }

    private Vector3 GetSlotRotationOffset(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return adagaRotationOffset;
            case 1: return machadoRotationOffset;
            case 2: return pistolaRotationOffset;
            default: return Vector3.zero;
        }
    }

    private void UpdateFloatingAnimations()
    {
        float time = Time.time;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponRackSlot slot = slots[i];
            if (slot.displayPoint == null || slot.displayInstance == null) continue;

            bool isUnlocked = IsSlotUnlocked(slot);
            bool isEquipped = IsWeaponCurrentlyEquipped(slot);

            // A Adaga fica 100% invisível no expositor se ainda estiver caída no chão do dormitório
            bool isDaggerStillOnFloor = (slot.weaponType == WeaponType.Dagger && !isUnlocked);
            bool shouldDisplayWeapon = !isEquipped && !isDaggerStillOnFloor;

            slot.displayInstance.SetActive(shouldDisplayWeapon);

            if (shouldDisplayWeapon)
            {
                // Aplica estilo: silhueta preta para bloqueadas, cores normais para liberadas
                ApplySlotMaterialStyle(slot, isUnlocked);

                float yBob = (floatAmplitude > 0f) ? Mathf.Sin(time * floatFrequency + i) * floatAmplitude : 0f;
                slot.displayInstance.transform.position = slot.displayPoint.position + (Vector3.up * yBob);

                Quaternion targetRot = slot.displayPoint.rotation * Quaternion.Euler(GetSlotRotationOffset(i));

                if (girarArmas)
                {
                    targetRot *= Quaternion.Euler(0f, time * rotateSpeed, 0f);
                }

                slot.displayInstance.transform.rotation = targetRot;
            }

        }
    }

    private void HandlePlayerProximity()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerWeaponManager = player.GetComponent<Player_WeaponManager>();
            }
        }

        if (playerTransform == null) return;

        // Procura a baia mais próxima do jogador
        WeaponRackSlot closestSlot = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < slots.Count; i++)
        {
            WeaponRackSlot slot = slots[i];
            if (slot.displayPoint == null) continue;

            float dist = Vector3.Distance(playerTransform.position, slot.displayPoint.position);
            if (dist < closestDist && dist <= interactionDistance)
            {
                closestDist = dist;
                closestSlot = slot;
            }
        }

        activeHoverSlot = closestSlot;

        bool isCraftingOpen = CraftingUI.Instance != null && CraftingUI.Instance.IsOpen();
        bool isDialogActive = DialogManager.BlocksGameplayInput || GamePause.IsPaused;
        bool shouldShowPrompt = activeHoverSlot != null && !isCraftingOpen && !isDialogActive;

        // Atualiza o efeito de outline dourado em todas as armas do expositor
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponRackSlot slot = slots[i];
            if (slot == null) continue;

            if (slot.highlight != null)
            {
                bool isSelected = (slot == activeHoverSlot && shouldShowPrompt);
                bool isLocked = !IsSlotUnlocked(slot);
                slot.highlight.SetHighlighted(isSelected, isLocked);
            }
        }

        if (promptRoot != null && promptCanvasGroup != null)
        {
            float targetAlpha = shouldShowPrompt ? 1f : 0f;

            if (shouldShowPrompt && !promptRoot.activeSelf)
            {
                promptRoot.SetActive(true);
            }

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            if (!shouldShowPrompt && promptCanvasGroup.alpha <= 0.01f && promptRoot.activeSelf)
            {
                promptRoot.SetActive(false);
            }

            if (promptRoot.activeSelf && Camera.main != null && activeHoverSlot != null && activeHoverSlot.displayPoint != null)
            {
                Vector3 slotPos = activeHoverSlot.displayPoint.position;
                Vector3 camPos = Camera.main.transform.position;
                Vector3 toCam = (camPos - slotPos).normalized;

                // Eleva por cima da arma/expositor e avança na direção da câmera para ficar por cima do mesh
                promptRoot.transform.position = slotPos + (Vector3.up * promptHeightOffset) + (toCam * promptForwardOffset);
                promptRoot.transform.forward = Camera.main.transform.forward;
                promptRoot.transform.localScale = Vector3.one * promptScale;
            }
        }

        if (!shouldShowPrompt) return;

        if (promptTMP != null && Mathf.Abs(promptTMP.fontSize - promptFontSize) > 0.1f)
        {
            promptTMP.fontSize = promptFontSize;
        }

        KeyCode key = GetInteractKey();
        bool isUnlocked = IsSlotUnlocked(activeHoverSlot);
        bool isEquipped = IsWeaponCurrentlyEquipped(activeHoverSlot);

        if (promptTMP != null)
        {
            if (isEquipped)
            {
                promptTMP.text = $"[ <color=#FFE500>{key}</color> ]  <color=#FFE500><b>Guardar {activeHoverSlot.displayName}</b></color>\n<size=75%><color=#D0D0D0>Devolver arma ao expositor</color></size>";
            }
            else if (isUnlocked)
            {
                bool isHoldingOther = playerWeaponManager != null && playerWeaponManager.currentWeapon != null;
                if (isHoldingOther)
                    promptTMP.text = $"[ <color=#FFE500>{key}</color> ]  <color=#FFE500><b>Trocar para {activeHoverSlot.displayName}</b></color>";
                else
                    promptTMP.text = $"[ <color=#FFE500>{key}</color> ]  <color=#FFE500><b>Equipar {activeHoverSlot.displayName}</b></color>";
            }
            else
            {
                if (activeHoverSlot.weaponType == WeaponType.Dagger)
                {
                    promptTMP.text = $"<color=#FFE500><b>{activeHoverSlot.displayName}</b></color>\n<size=75%><color=#E0E0E0>Colete no dormitório primeiro</color></size>";
                }
                else
                {
                    promptTMP.text = $"<color=#FFE500><b>{activeHoverSlot.displayName}</b></color>\n<size=75%><color=#E0E0E0>Forje no Robozinho para liberar</color></size>";
                }
            }
        }

        if (Input.GetKeyDown(key))
        {
            TryInteractSlot(activeHoverSlot);
        }
    }

    private void TryInteractSlot(WeaponRackSlot slot)
    {
        if (slot == null || playerWeaponManager == null) return;

        bool isUnlocked = IsSlotUnlocked(slot);
        bool isEquipped = IsWeaponCurrentlyEquipped(slot);

        // 1. Guardar de volta no expositor se já está na mão
        if (isEquipped)
        {
            playerWeaponManager.UnequipCurrentWeapon();
            if (equipSound != null) AudioSource.PlayClipAtPoint(equipSound, transform.position);
            Debug.Log($"[WeaponRack] {slot.displayName} guardada de volta no expositor.");
            return;
        }

        // 2. Se bloqueada -> Rigorosamente NÃO permite equipar!
        if (!isUnlocked)
        {
            if (lockedSound != null) AudioSource.PlayClipAtPoint(lockedSound, transform.position);
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("Esta arma ainda não foi forjada! Use o Robozinho para construí-la.");
            }
            return;
        }

        // 3. Equipar arma
        if (slot.weaponPrefab != null)
        {
            playerWeaponManager.EquipWeaponPrefab(slot.weaponPrefab);
            Debug.Log($"[WeaponRack] Equipou {slot.displayName} no jogador com sucesso!");

            if (equipSound != null) AudioSource.PlayClipAtPoint(equipSound, transform.position);

            TutorialManager.Instance?.CompleteStep(TutorialStep.EquiparNoRack);

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AdvanceQuest(QuestType.Interact, "weapon_equipped", 1);
            }
        }
    }

    private bool IsSlotUnlocked(WeaponRackSlot slot)
    {
        if (slot.weaponType == WeaponType.Dagger)
        {
            // A Adaga de Plasma só fica disponível no expositor após ser coletada no chão do dormitório
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialStep.PegarAdaga)
            {
                return false;
            }
            if (SaveManager.instance != null && SaveManager.instance.CachedData != null && SaveManager.instance.CachedData.tutorialStep == (int)TutorialStep.PegarAdaga)
            {
                return false;
            }
            return true;
        }

        if (slot.unlockedByDefault) return true;
        if (string.IsNullOrEmpty(slot.requiredCraftId)) return true;

        if (SaveManager.instance != null)
        {
            return SaveManager.instance.GetAllCraftedEquipmentIds().Contains(slot.requiredCraftId);
        }

        return false;
    }

    /// <summary>
    /// Desbloqueia todos os slots do expositor imediatamente para testes.
    /// </summary>
    public void UnlockAllSlots()
    {
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null) slot.unlockedByDefault = true;
            }
        }
    }

    private bool IsWeaponCurrentlyEquipped(WeaponRackSlot slot)
    {
        if (playerWeaponManager == null || playerWeaponManager.currentWeapon == null) return false;

        WeaponOffset wo = playerWeaponManager.currentWeapon.GetComponent<WeaponOffset>();
        if (wo != null && wo.weaponType == slot.weaponType) return true;

        return playerWeaponManager.currentWeapon.name.ToLower().Contains(slot.weaponId.ToLower());
    }

    private KeyCode GetInteractKey()
    {
        string saved = PlayerPrefs.GetString("KeyInteract", fallbackKey.ToString());
        try { return (KeyCode)System.Enum.Parse(typeof(KeyCode), saved); }
        catch { return fallbackKey; }
    }

    private void OnDrawGizmosSelected()
    {
        if (slot1_AdagaAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(slot1_AdagaAnchor.position, 0.20f);
            Gizmos.DrawRay(slot1_AdagaAnchor.position, slot1_AdagaAnchor.forward * 0.35f);
        }

        if (slot2_MachadoAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(slot2_MachadoAnchor.position, 0.22f);
            Gizmos.DrawRay(slot2_MachadoAnchor.position, slot2_MachadoAnchor.forward * 0.35f);
        }

        if (slot3_PistolaAnchor != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0f);
            Gizmos.DrawWireSphere(slot3_PistolaAnchor.position, 0.20f);
            Gizmos.DrawRay(slot3_PistolaAnchor.position, slot3_PistolaAnchor.forward * 0.35f);
        }
    }
}
