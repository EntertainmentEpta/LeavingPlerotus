using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Fenda do Abismo instanciada na arena após a derrota do Boss.
/// - Segue o padrão de interação de CraftingTableInteraction (Trigger + failsafe de distância + prompt WorldSpace billboard lendo KeyInteract).
/// - Possui visual procedural chamativo (vórtice + anel orbital + PointLight pulsante + partículas) substituível por prefab no Inspector.
/// - Abre modal uGUI procedural adaptado ao Modo Normal (entrar no Loop Endless mantendo 100% da build ou encerrar vitória)
///   ou ao Modo Endless (avançar próximo nível ou extrair para a Base salvando recorde).
/// </summary>
public class EndlessRiftPortal : MonoBehaviour
{
    public static bool IsAnyModalOpen { get; private set; }

    [Header("Visual da Fenda")]
    [Tooltip("Prefab visual opcional. Se nulo, cria um vórtice procedural com luz pulsante e partículas.")]
    public GameObject customVisualPrefab;

    [Tooltip("Cor principal do vórtice/luz da Fenda (roxo abissal).")]
    public Color primaryRiftColor = new Color(0.62f, 0.16f, 0.98f, 1f);

    [Tooltip("Cor secundária do vórtice/luz da Fenda (laranja energia).")]
    public Color secondaryRiftColor = new Color(1.0f, 0.48f, 0.10f, 1f);

    [Tooltip("Intensidade mínima da luz pulsante da Fenda.")]
    public float lightMinIntensity = 2.8f;

    [Tooltip("Intensidade máxima da luz pulsante da Fenda.")]
    public float lightMaxIntensity = 6.8f;

    [Header("Configuração de Interação")]
    [Tooltip("Texto da ação exibido no prompt flutuante.")]
    public string actionText = "Interagir com a Fenda";

    [Tooltip("Raio de ativação do failsafe de proximidade.")]
    public float interactRadius = 3.4f;

    [Tooltip("Raio de saída para ocultar o prompt.")]
    public float exitRadius = 4.2f;

    [Tooltip("Velocidade de fade do prompt WorldSpace.")]
    public float fadeSpeed = 6f;

    [Header("Prompt WorldSpace (Opcional)")]
    public GameObject promptUI;
    public TMP_Text promptTextMesh;

    private bool playerPerto = false;
    private bool isModalOpen = false;
    private bool isTransitioning = false;
    private Transform playerTransform;
    private CanvasGroup promptCanvasGroup;

    // Referências do visual procedural para animação em Update
    private Transform coreTransform;
    private Transform ringTransform;
    private Transform innerCoreTransform;
    private Light riftPointLight;

    // Modal uGUI procedural
    private GameObject modalCanvasObj;

    // =====================================================
    // SPAWN SEGURO & BANNER DE AVISO (API ESTÁTICA)
    // =====================================================

    /// <summary>
    /// Instancia a Fenda do Abismo em uma posição segura da arena, ajustada ao NavMesh e ao chão via Raycast
    /// para nunca ficar flutuando ou presa em paredes.
    /// </summary>
    public static EndlessRiftPortal SpawnAtSafeArenaPosition(
        Vector3 preferredPosition,
        Vector3 fallbackCenter,
        GameObject optionalRiftPrefab = null,
        Transform ignoredBossRoot = null)
    {
        Vector3 safePos = ResolveSafeGroundPosition(preferredPosition, fallbackCenter, ignoredBossRoot);

        GameObject riftObj;
        EndlessRiftPortal portal;

        if (optionalRiftPrefab != null)
        {
            riftObj = Instantiate(optionalRiftPrefab, safePos, Quaternion.identity);
            portal = riftObj.GetComponent<EndlessRiftPortal>() ?? riftObj.AddComponent<EndlessRiftPortal>();
        }
        else
        {
            riftObj = new GameObject("EndlessRiftPortal");
            riftObj.transform.position = safePos;
            riftObj.transform.rotation = Quaternion.identity;
            portal = riftObj.AddComponent<EndlessRiftPortal>();
        }

        Debug.Log($"[EndlessRiftPortal] 🌀 Fenda do Abismo aberta em {safePos}.");
        return portal;
    }

    private static Vector3 ResolveSafeGroundPosition(Vector3 preferredPos, Vector3 fallbackCenter, Transform ignoredBossRoot)
    {
        Vector3 candidate = preferredPos;

        // 1. Garante que o ponto está dentro de uma área caminhável da NavMesh
        if (NavMesh.SamplePosition(preferredPos, out NavMeshHit navHit, 8f, NavMesh.AllAreas))
        {
            candidate = navHit.position;
        }
        else if (NavMesh.SamplePosition(fallbackCenter, out NavMeshHit fallbackHit, 12f, NavMesh.AllAreas))
        {
            candidate = fallbackHit.position;
        }
        else
        {
            candidate = fallbackCenter != Vector3.zero ? fallbackCenter : preferredPos;
        }

        // 2. Ajusta exatamente à superfície sólida do chão via Raycast (ignora triggers, Boss e Player)
        RaycastHit[] hits = Physics.RaycastAll(
            candidate + Vector3.up * 3.0f,
            Vector3.down,
            12.0f,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestY = float.MinValue;
        bool foundFloor = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.isTrigger) continue;
            if (ignoredBossRoot != null && (col.transform == ignoredBossRoot || col.transform.IsChildOf(ignoredBossRoot))) continue;
            if (col.CompareTag("Player") || col.CompareTag("Enemy")) continue;
            if (col.GetComponentInParent<BossController>() != null) continue;
            if (col.GetComponentInParent<DummyHealth>() != null) continue;
            if (col.GetComponentInParent<PlayerHealth>() != null) continue;

            if (hits[i].normal.y >= 0.65f &&
                hits[i].point.y <= candidate.y + 1.5f &&
                hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                foundFloor = true;
            }
        }

        if (foundFloor)
        {
            candidate.y = bestY;
        }

        return candidate;
    }

    /// <summary>
    /// Exibe um aviso curto não-bloqueante (~4s) no topo da tela informando que o Boss foi derrotado e a Fenda se abriu.
    /// </summary>
    public static void ShowRiftOpenedBanner(bool isAlreadyEndless, float duration = 4.2f)
    {
        GameObject bannerGo = new GameObject("RiftOpenedBanner_Canvas");
        Canvas canvas = bannerGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;

        CanvasScaler scaler = bannerGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelGo = new GameObject("BannerPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGo.transform.SetParent(bannerGo.transform, false);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -108f);
        panelRt.sizeDelta = new Vector2(780f, 82f);

        Image bg = panelGo.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.03f, 0.12f, 0.86f);
        bg.raycastTarget = false;

        Outline outline = panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.30f, 1.0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textGo = new GameObject("BannerText", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(18f, 8f);
        textRt.offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        tmp.fontSize = 24f;
        tmp.text = isAlreadyEndless
            ? "<color=#FFAA33><b>BOSS DO CICLO DERROTADO!</b></color>\n<size=18><color=#E5D4FF>A Fenda do Abismo se abriu — avance para o próximo nível ou extraia seu recorde.</color></size>"
            : "<color=#00FF99><b>BOSS DERROTADO!</b></color>\n<size=18><color=#E5D4FF>A Fenda do Abismo se abriu na arena. Aproxime-se para escolher seu destino.</color></size>";

        RiftToastRunner runner = bannerGo.AddComponent<RiftToastRunner>();
        runner.Play(panelGo.GetComponent<CanvasGroup>(), duration);
    }

    private class RiftToastRunner : MonoBehaviour
    {
        public void Play(CanvasGroup group, float holdSeconds)
        {
            StartCoroutine(RunRoutine(group, holdSeconds));
        }

        private IEnumerator RunRoutine(CanvasGroup group, float holdSeconds)
        {
            if (group == null)
            {
                Destroy(gameObject);
                yield break;
            }

            const float fadeIn = 0.35f;
            const float fadeOut = 0.65f;
            float t = 0f;

            group.alpha = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            group.alpha = 1f;

            float hold = Mathf.Max(1.5f, holdSeconds - fadeIn - fadeOut);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
                yield return null;
            }

            Destroy(gameObject);
        }
    }

    // =====================================================
    // CICLO DE VIDA & VISUAL PROCEDURAL
    // =====================================================

    private void Start()
    {
        SetupVisual();
        EnsureColliderAndPromptUI();

        if (promptUI != null)
        {
            promptCanvasGroup = promptUI.GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
            }
            promptCanvasGroup.alpha = 0f;
            promptUI.SetActive(false);
        }

        UpdatePromptText();
    }

    private void OnDestroy()
    {
        if (isModalOpen)
        {
            isModalOpen = false;
            IsAnyModalOpen = false;
            GamePause.Release(this);
        }

        if (modalCanvasObj != null)
        {
            Destroy(modalCanvasObj);
            modalCanvasObj = null;
        }
    }

    private void SetupVisual()
    {
        if (customVisualPrefab != null)
        {
            GameObject custom = Instantiate(customVisualPrefab, transform);
            custom.transform.localPosition = Vector3.zero;
            custom.transform.localRotation = Quaternion.identity;
            return;
        }

        CreateProceduralRiftVisual();
    }

    private void CreateProceduralRiftVisual()
    {
        GameObject root = new GameObject("RiftVisual_Procedural");
        root.transform.SetParent(transform, false);

        Material coreMat = CreateGlowMaterial(primaryRiftColor, 0.85f, "RiftCoreMat");
        Material innerMat = CreateGlowMaterial(secondaryRiftColor, 0.92f, "RiftInnerMat");
        Material ringMat = CreateGlowMaterial(Color.Lerp(primaryRiftColor, secondaryRiftColor, 0.55f), 0.90f, "RiftRingMat");
        Material haloMat = CreateGlowMaterial(primaryRiftColor, 0.42f, "RiftHaloMat");

        // 1. Vórtice vertical principal
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        core.name = "RiftCore";
        core.transform.SetParent(root.transform, false);
        core.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        core.transform.localScale = new Vector3(0.75f, 1.55f, 0.35f);
        RemovePrimitiveCollider(core);
        ApplyMaterial(core, coreMat);
        coreTransform = core.transform;

        // 2. Núcleo interno pulsante (laranja/energia)
        GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        inner.name = "RiftInnerCore";
        inner.transform.SetParent(root.transform, false);
        inner.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        inner.transform.localScale = new Vector3(0.35f, 1.25f, 0.22f);
        RemovePrimitiveCollider(inner);
        ApplyMaterial(inner, innerMat);
        innerCoreTransform = inner.transform;

        // 3. Anel orbital inclinado
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "RiftOrbitalRing";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        ring.transform.localRotation = Quaternion.Euler(68f, 0f, 15f);
        ring.transform.localScale = new Vector3(2.15f, 0.04f, 2.15f);
        RemovePrimitiveCollider(ring);
        ApplyMaterial(ring, ringMat);
        ringTransform = ring.transform;

        // 4. Halo de energia no solo
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        halo.name = "RiftGroundHalo";
        halo.transform.SetParent(root.transform, false);
        halo.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        halo.transform.localScale = new Vector3(2.8f, 0.02f, 2.8f);
        RemovePrimitiveCollider(halo);
        ApplyMaterial(halo, haloMat);

        // 5. PointLight pulsante roxa/laranja
        GameObject lightGo = new GameObject("RiftPointLight");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        riftPointLight = lightGo.AddComponent<Light>();
        riftPointLight.type = LightType.Point;
        riftPointLight.color = primaryRiftColor;
        riftPointLight.range = 12f;
        riftPointLight.intensity = lightMaxIntensity;

        // 6. Partículas ascendentes simples em código
        CreateRiftParticles(root.transform);
    }

    private void CreateRiftParticles(Transform parent)
    {
        GameObject psGo = new GameObject("RiftParticles");
        psGo.transform.SetParent(parent, false);
        psGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.20f);
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(2.2f);
        vel.radial = new ParticleSystem.MinMaxCurve(-0.35f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(primaryRiftColor, 0f),
                new GradientColorKey(secondaryRiftColor, 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = grad;

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            Shader particleShader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Particles/Standard Unlit");
            if (particleShader != null)
            {
                Material pMat = new Material(particleShader) { name = "RiftParticleMat" };
                pMat.color = Color.white;
                if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", Color.white);
                psRenderer.material = pMat;
            }
        }
    }

    private static Material CreateGlowMaterial(Color color, float alpha, string matName)
    {
        Color c = new Color(color.r, color.g, color.b, alpha);
        Material mat = AimSettings.CreateGroundIndicatorMaterial(c, matName, false);
        if (mat != null) return mat;

        Shader fallback = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
        Material m = new Material(fallback) { name = matName };
        m.color = c;
        return m;
    }

    private static void RemovePrimitiveCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    private static void ApplyMaterial(GameObject go, Material mat)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // =====================================================
    // PROXIMIDADE, PROMPT WORLDSPACE & INPUT (PADRÃO CRAFTING TABLE)
    // =====================================================

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

    private void UpdatePromptText()
    {
        if (promptTextMesh != null)
        {
            KeyCode currentKey = GetCurrentInteractKey();
            promptTextMesh.text = $"[ <color=#FFE500>{currentKey}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
        }
    }

    private void EnsureColliderAndPromptUI()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4.2f, 3.0f, 4.2f);
            box.center = new Vector3(0f, 1.5f, 0f);
        }
        else
        {
            box.isTrigger = true;
            if (box.size.x < 2.0f || box.size.z < 2.0f)
            {
                box.size = new Vector3(4.2f, 3.0f, 4.2f);
                box.center = new Vector3(0f, 1.5f, 0f);
            }
        }

        if (promptUI == null)
        {
            Transform existing = transform.Find("PromptUI") ?? transform.Find("PromptUI_AutoGenerated");
            if (existing != null)
            {
                promptUI = existing.gameObject;
                promptTextMesh = promptUI.GetComponentInChildren<TMP_Text>();
            }
            else
            {
                CreateAutoPromptUI();
            }
        }
    }

    private void CreateAutoPromptUI()
    {
        GameObject promptObj = new GameObject("PromptUI_AutoGenerated");
        promptObj.transform.SetParent(transform, false);
        promptObj.transform.localPosition = new Vector3(0f, 3.55f, 0f);

        Canvas canvas = promptObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 85;

        RectTransform rect = promptObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460f, 80f);
        rect.localScale = Vector3.one * 0.008f;

        promptCanvasGroup = promptObj.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        GameObject textObj = new GameObject("Text (TMP)");
        textObj.transform.SetParent(promptObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        promptTextMesh = textObj.AddComponent<TextMeshProUGUI>();
        promptTextMesh.fontSize = 30f;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.color = Color.white;
        promptTextMesh.outlineWidth = 0.28f;
        promptTextMesh.outlineColor = new Color32(0, 0, 0, 240);

        promptUI = promptObj;
        promptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerTransform = other.transform;
        playerPerto = true;
        UpdatePromptText();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        float dist = Vector3.Distance(transform.position, other.transform.position);
        if (dist <= interactRadius + 0.3f) return;

        playerPerto = false;
    }

    private void Update()
    {
        AnimateProceduralVisual();

        // === 0. VERIFICAÇÃO DE PROXIMIDADE CONTÍNUA (FAILSAFE) ===
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= interactRadius)
            {
                if (!playerPerto)
                {
                    playerPerto = true;
                    UpdatePromptText();
                }
            }
            else if (dist > exitRadius && !isModalOpen)
            {
                playerPerto = false;
            }
        }

        // === 1. FADE SUAVE DO PROMPT WORLDSPACE ===
        if (promptUI != null && promptCanvasGroup != null)
        {
            bool victoryOpen = VictoryScreenUI.Instance != null &&
                               VictoryScreenUI.Instance.victoryPanel != null &&
                               VictoryScreenUI.Instance.victoryPanel.activeSelf;

            bool shouldShowPrompt = playerPerto && !isModalOpen && !isTransitioning && !victoryOpen;
            float targetAlpha = shouldShowPrompt ? 1f : 0f;

            if (shouldShowPrompt && !promptUI.activeSelf)
            {
                promptUI.SetActive(true);
            }

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            if (!shouldShowPrompt && promptCanvasGroup.alpha <= 0.01f && promptUI.activeSelf)
            {
                promptUI.SetActive(false);
            }

            if (promptUI.activeSelf && Camera.main != null)
            {
                promptUI.transform.forward = Camera.main.transform.forward;
            }
        }

        // === 2. INPUT DO MODAL ===
        if (isModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseModal(restoreCursor: true);
            }
            return;
        }

        if (!playerPerto || isTransitioning) return;
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused) return;
        if (VictoryScreenUI.Instance != null && VictoryScreenUI.Instance.victoryPanel != null && VictoryScreenUI.Instance.victoryPanel.activeSelf) return;
        if (DeathScreenUI.Instance != null && DeathScreenUI.Instance.deathPanel != null && DeathScreenUI.Instance.deathPanel.activeSelf) return;

        KeyCode interactKey = GetCurrentInteractKey();
        if (Input.GetKeyDown(interactKey))
        {
            OpenModal();
        }
    }

    private void AnimateProceduralVisual()
    {
        float dt = Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.8f);

        if (coreTransform != null)
        {
            coreTransform.Rotate(Vector3.up, 70f * dt, Space.World);
            float s = Mathf.Lerp(0.92f, 1.08f, pulse);
            coreTransform.localScale = new Vector3(0.75f * s, 1.55f * Mathf.Lerp(0.96f, 1.04f, pulse), 0.35f * s);
        }

        if (innerCoreTransform != null)
        {
            innerCoreTransform.Rotate(Vector3.up, -110f * dt, Space.World);
        }

        if (ringTransform != null)
        {
            ringTransform.Rotate(Vector3.up, 95f * dt, Space.Self);
        }

        if (riftPointLight != null)
        {
            riftPointLight.intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, pulse);
            riftPointLight.color = Color.Lerp(primaryRiftColor, secondaryRiftColor, pulse * 0.65f);
        }
    }

    // =====================================================
    // MODAL UGUI PROCEDURAL
    // =====================================================

    public void OpenModal()
    {
        if (isModalOpen || isTransitioning) return;

        isModalOpen = true;
        IsAnyModalOpen = true;
        GamePause.Acquire(this);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();
        BuildModalUI();
    }

    public void CloseModal(bool restoreCursor)
    {
        if (!isModalOpen) return;

        isModalOpen = false;
        IsAnyModalOpen = false;

        if (modalCanvasObj != null)
        {
            Destroy(modalCanvasObj);
            modalCanvasObj = null;
        }

        GamePause.Release(this);

        if (restoreCursor)
        {
            AimSettings.HideAimCursorIfNoUI();
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem_RiftFallback");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    private void BuildModalUI()
    {
        if (modalCanvasObj != null)
        {
            Destroy(modalCanvasObj);
        }

        bool isAlreadyEndless = RunManager.instance != null && RunManager.instance.isEndlessMode;
        int currentLvl = RunManager.instance != null ? RunManager.instance.currentLevel : 4;
        int endlessFloor = RunManager.instance != null ? Mathf.Max(1, RunManager.instance.endlessFloorCount) : 1;

        modalCanvasObj = new GameObject("EndlessRiftModal_Canvas");
        Canvas canvas = modalCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        CanvasScaler scaler = modalCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        modalCanvasObj.AddComponent<GraphicRaycaster>();

        // Fundo escurecido
        GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(modalCanvasObj.transform, false);
        RectTransform overlayRt = overlay.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0.02f, 0.01f, 0.05f, 0.78f);

        // Card central
        GameObject card = new GameObject("ModalCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(overlay.transform, false);
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(720f, 470f);
        card.GetComponent<Image>().color = new Color(0.07f, 0.05f, 0.13f, 0.96f);

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.68f, 0.25f, 1.0f, 0.9f);
        cardOutline.effectDistance = new Vector2(3f, -3f);

        // Título
        string titleText = isAlreadyEndless
            ? "<color=#FFAA33><b>FENDA DO ABISMO — CICLO CONCLUÍDO</b></color>"
            : "<color=#C87DFF><b>FENDA DO ABISMO</b></color>";

        CreateLabel(card.transform, "Title", titleText, 34f, new Vector2(0f, 175f), new Vector2(660f, 52f));

        // Subtítulo + contexto
        string subtitle;
        if (isAlreadyEndless)
        {
            string timeStr = RunStatsManager.Instance != null
                ? RunStatsManager.Instance.FormatTime(RunStatsManager.Instance.survivalTimer)
                : "00:00";
            int bestLvl = RunStatsManager.Instance != null
                ? RunStatsManager.Instance.GetLiveBestEndlessLevel(currentLvl)
                : currentLvl;
            subtitle =
                $"<color=#FFFFFF>Boss do Ciclo derrotado no <b>Nível {currentLvl}</b> (Andar Endless {endlessFloor})!</color>\n" +
                $"<size=18><color=#A9D1FF>Tempo atual: {timeStr}  •  Recorde: Nível {bestLvl}</color></size>";
        }
        else
        {
            subtitle =
                "<color=#FFFFFF>O Boss foi derrotado e uma fenda dimensional se abriu.</color>\n" +
                "<size=18><color=#9EFFC7>Entrar no Modo Endless preserva 100% da sua build, armas, infusões, pactos e essências.</color></size>";
        }

        CreateLabel(card.transform, "Subtitle", subtitle, 21f, new Vector2(0f, 92f), new Vector2(650f, 84f));

        // Botões adaptados ao modo
        if (!isAlreadyEndless)
        {
            CreateModalButton(
                card.transform,
                "Btn_EnterEndless",
                "ENTRAR NA FENDA (MODO ENDLESS)",
                "Mantém toda a sua build e inicia o Loop Endless com Mutações do Abismo",
                new Vector2(0f, -15f),
                new Vector2(580f, 82f),
                new Color(0.45f, 0.14f, 0.72f, 1f),
                new Color(0.85f, 0.45f, 1.0f, 1f),
                OnEnterEndlessFromNormalClicked);

            CreateModalButton(
                card.transform,
                "Btn_FinishVictory",
                "ENCERRAR VITÓRIA / VOLTAR À BASE",
                "Abre a tela de Vitória com o resumo da missão e retorna à Base",
                new Vector2(0f, -115f),
                new Vector2(580f, 82f),
                new Color(0.11f, 0.36f, 0.28f, 1f),
                new Color(0.25f, 0.85f, 0.62f, 1f),
                OnFinishVictoryClicked);
        }
        else
        {
            CreateModalButton(
                card.transform,
                "Btn_AdvanceEndless",
                "AVANÇAR NA FENDA (PRÓXIMO NÍVEL ENDLESS)",
                "Salva o recorde atual e continua para o próximo andar do Abismo",
                new Vector2(0f, -15f),
                new Vector2(580f, 82f),
                new Color(0.55f, 0.18f, 0.78f, 1f),
                new Color(1.0f, 0.60f, 0.22f, 1f),
                OnAdvanceEndlessClicked);

            CreateModalButton(
                card.transform,
                "Btn_ExtractEndless",
                "EXTRAIR PARA A BASE (SALVAR RECORDE)",
                "Registra seu recorde e encerra a expedição com segurança",
                new Vector2(0f, -115f),
                new Vector2(580f, 82f),
                new Color(0.14f, 0.34f, 0.48f, 1f),
                new Color(0.42f, 0.78f, 1.0f, 1f),
                OnExtractEndlessClicked);
        }

        // Botão secundário para fechar e continuar na arena
        CreateCloseButton(card.transform, new Vector2(0f, -195f));
    }

    private static void CreateLabel(Transform parent, string name, string content, float fontSize, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private static void CreateModalButton(
        Transform parent,
        string name,
        string mainLabel,
        string subLabel,
        Vector2 anchoredPos,
        Vector2 size,
        Color bgColor,
        Color borderColor,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnGo.GetComponent<Image>();
        img.color = bgColor;

        Outline outline = btnGo.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button btn = btnGo.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = colors.highlightedColor;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        GameObject txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(16f, 6f);
        txtRt.offsetMax = new Vector2(-16f, -6f);

        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = $"<b>{mainLabel}</b>\n<size=15><color=#E8E8F2>{subLabel}</color></size>";
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    private void CreateCloseButton(Transform parent, Vector2 anchoredPos)
    {
        GameObject btnGo = new GameObject("Btn_CloseModal", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(220f, 40f);

        btnGo.GetComponent<Image>().color = new Color(0.16f, 0.14f, 0.22f, 0.92f);
        Button btn = btnGo.GetComponent<Button>();
        btn.onClick.AddListener(() => CloseModal(restoreCursor: true));

        GameObject txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "FECHAR  [ESC]";
        tmp.fontSize = 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.82f, 0.82f, 0.90f, 1f);
        tmp.raycastTarget = false;
    }

    // =====================================================
    // AÇÕES DOS BOTÕES DO MODAL
    // =====================================================

    private static RunManager EnsureRunManager()
    {
        if (RunManager.instance != null) return RunManager.instance;
        RunManager existing = FindFirstObjectByType<RunManager>();
        if (existing != null) return existing;

        GameObject go = new GameObject("RunManager_AutoCreated");
        return go.AddComponent<RunManager>();
    }

    private void OnEnterEndlessFromNormalClicked()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        CloseModal(restoreCursor: false);
        Time.timeScale = 1f;

        RunManager rm = EnsureRunManager();
        rm.EnterEndlessLoopFromBoss();

        GameManager.EnsureInstance().LoadNextLevel();
    }

    private void OnFinishVictoryClicked()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        CloseModal(restoreCursor: false);

        if (VictoryScreenUI.Instance != null)
        {
            VictoryScreenUI.Instance.ShowVictoryScreen();
        }
        else
        {
            Debug.LogWarning("[EndlessRiftPortal] VictoryScreenUI não encontrado na cena. Retornando direto para a Base.");
            Time.timeScale = 1f;
            GameManager.EnsureInstance().ReturnToBase();
        }
    }

    private void OnAdvanceEndlessClicked()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (RunManager.instance != null && RunStatsManager.Instance != null)
        {
            RunStatsManager.Instance.CommitEndlessRecord(RunManager.instance.currentLevel);
        }

        CloseModal(restoreCursor: false);
        Time.timeScale = 1f;

        GameManager.EnsureInstance().LoadNextLevel();
    }

    private void OnExtractEndlessClicked()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (RunManager.instance != null && RunStatsManager.Instance != null)
        {
            RunStatsManager.Instance.CommitEndlessRecord(RunManager.instance.currentLevel);
            RunStatsManager.Instance.StopRunTracking();
        }

        CloseModal(restoreCursor: false);

        if (VictoryScreenUI.Instance != null)
        {
            VictoryScreenUI.Instance.ShowVictoryScreen();
        }
        else
        {
            Time.timeScale = 1f;
            GameManager.EnsureInstance().ReturnToBase();
        }
    }
}
