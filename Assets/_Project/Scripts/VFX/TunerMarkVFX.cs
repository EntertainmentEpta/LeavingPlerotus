using System.Collections;
using UnityEngine;

/// <summary>
/// Controlador visual da Marca Crystal Tuner (T4).
/// Responsável por exibir a runa cristalina flutuando sobre o inimigo marcado,
/// atualizar o anel de cooldown radial 360° que apaga no sentido horário conforme o tempo acaba,
/// sinalizar urgência nos momentos finais e disparar o efeito de estilhaçamento (shatter) ao ser consumida.
/// </summary>
[DisallowMultipleComponent]
public class TunerMarkVFX : MonoBehaviour
{
    [Header("Alvo e Posicionamento")]
    [Tooltip("Transform do inimigo marcado.")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("Distancia vertical acima do topo do colisor ou renderizador do inimigo.")]
    [Range(0.2f, 2.5f)]
    public float heightOffset = 0.5f;

    [Tooltip("Offset manual caso algum inimigo especifico precise de ajuste fino de posicao.")]
    public Vector3 manualPositionOffset = Vector3.zero;

    [Tooltip("Velocidade de suavizacao para acompanhar a movimentacao do inimigo.")]
    [Range(5f, 35f)]
    public float followSpeed = 20f;

    [Tooltip("Calcula a posicao com base no topo do colisor ou renderizador do inimigo.")]
    public bool useBoundsTop = true;

    [Header("Duracao e Tempo")]
    [Tooltip("Tempo total de duracao da marca em segundos.")]
    public float totalDuration = 4.0f;

    [Tooltip("Tempo restante atual da marca.")]
    [SerializeField] private float remainingDuration = 4.0f;

    [Tooltip("Tempo restante em segundos para iniciar o piscar rapido de urgencia.")]
    [Range(0.5f, 2.5f)]
    public float criticalThreshold = 1.5f;

    [Tooltip("Se verdadeiro, faz o fade-out e se destroi quando o tempo zerar.")]
    public bool autoExpireOnTimeout = true;

    [Header("Visual da Runa")]
    [Tooltip("Cor principal do cristal amarelo.")]
    public Color normalColor = new Color(1.0f, 0.88f, 0.22f, 1.0f);

    [Tooltip("Cor de alerta quando o tempo esta quase expirando.")]
    public Color criticalColor = new Color(1.0f, 0.45f, 0.1f, 1.0f);

    [Tooltip("Escala geral da runa de cristal.")]
    [Range(0.3f, 3.0f)]
    public float runeScale = 0.85f;

    [Tooltip("Frequencia da oscilacao vertical de flutuacao.")]
    public float bobFrequency = 3.5f;

    [Tooltip("Amplitude da oscilacao vertical de flutuacao.")]
    public float bobAmplitude = 0.1f;

    [Tooltip("Velocidade de rotacao no eixo frontal.")]
    public float spinSpeed = 0f;

    [Header("Anel de Cooldown Radial (360°)")]
    [Tooltip("Exibir o anel circular de contagem regressiva.")]
    public bool showCountdownRing = true;

    [Tooltip("Raio fixo do anel circular (mantem tamanho constante, sem encolher a escala).")]
    [Range(0.3f, 1.5f)]
    public float ringRadius = 0.55f;

    [Tooltip("Espessura da linha do anel.")]
    [Range(0.02f, 0.12f)]
    public float ringWidth = 0.045f;

    [Tooltip("Resolucao de pontos da circunferencia de 360°.")]
    [Range(32, 96)]
    public int ringSegments = 64;

    [Header("Efeito de Estilhacamento")]
    [Tooltip("Prefab de particulas instanciado ao estourar a marca. Se nulo, instancia o efeito procedural.")]
    public GameObject shatterVFXPrefab;

    [Tooltip("Efeito sonoro ao estilhaçar a marca.")]
    public AudioClip shatterAudioClip;

    [Tooltip("Volume do som de estilhacamento.")]
    [Range(0f, 1f)]
    public float shatterVolume = 0.85f;

    // Referencias internas
    private Collider targetCollider;
    private Renderer targetRenderer;
    private DummyHealth targetDummyHealth;
    private ShardSwarmHealth targetSwarmHealth;

    private Transform visualContainer;
    private SpriteRenderer runeSpriteRenderer;
    private MeshRenderer runeMeshRenderer;
    private Material runeMaterial;

    private LineRenderer activeRingRenderer;

    private bool isInitialized = false;
    private bool isShattered = false;
    private bool isExpiring = false;
    private Vector3 currentWorldPosition;
    private Camera cachedCamera;

    public float RemainingTime => remainingDuration;
    public float RemainingRatio => totalDuration > 0.001f ? Mathf.Clamp01(remainingDuration / totalDuration) : 0f;
    public Transform TargetTransform => targetTransform;

    private void Awake()
    {
        cachedCamera = Camera.main;
        currentWorldPosition = transform.position;
    }

    private void Start()
    {
        if (!isInitialized && targetTransform != null)
        {
            Initialize(targetTransform, totalDuration);
        }
    }

    /// <summary>
    /// Metodo estatico para instanciar e aplicar a marca visual diretamente em um inimigo.
    /// </summary>
    public static TunerMarkVFX AttachMark(GameObject enemy, float duration, GameObject customPrefab = null)
    {
        if (enemy == null) return null;

        TunerMarkVFX existingMark = enemy.GetComponentInChildren<TunerMarkVFX>();
        if (existingMark != null)
        {
            existingMark.Expire();
        }

        GameObject markObj;
        if (customPrefab != null)
        {
            markObj = Instantiate(customPrefab);
        }
        else
        {
            markObj = new GameObject("[TunerMark_VFX]");
        }

        TunerMarkVFX markComponent = markObj.GetComponent<TunerMarkVFX>() ?? markObj.AddComponent<TunerMarkVFX>();
        markComponent.Initialize(enemy.transform, duration);
        return markComponent;
    }

    /// <summary>
    /// Inicializa os parametros, referencias do alvo e controi a hierarquia visual.
    /// </summary>
    public void Initialize(Transform targetEnemy, float duration = 4.0f)
    {
        targetTransform = targetEnemy;
        totalDuration = Mathf.Max(0.5f, duration);
        remainingDuration = totalDuration;
        isShattered = false;
        isExpiring = false;

        if (targetTransform != null)
        {
            targetCollider = targetTransform.GetComponent<Collider>() 
                          ?? targetTransform.GetComponentInChildren<Collider>();
            targetRenderer = targetTransform.GetComponentInChildren<Renderer>();
            targetDummyHealth = targetTransform.GetComponent<DummyHealth>() 
                             ?? targetTransform.GetComponentInParent<DummyHealth>();
            targetSwarmHealth = targetTransform.GetComponent<ShardSwarmHealth>() 
                             ?? targetTransform.GetComponentInParent<ShardSwarmHealth>();

            currentWorldPosition = CalculateTargetHeadPosition();
            transform.position = currentWorldPosition;
        }

        BuildVisualHierarchy();
        isInitialized = true;
    }

    /// <summary>
    /// Configura os componentes da runa e do anel de cooldown.
    /// </summary>
    private void BuildVisualHierarchy()
    {
        Transform existingContainer = transform.Find("VisualRoot");
        if (existingContainer != null)
        {
            visualContainer = existingContainer;
        }
        else
        {
            GameObject containerObj = new GameObject("VisualRoot");
            containerObj.transform.SetParent(transform, false);
            visualContainer = containerObj.transform;
        }

        visualContainer.localScale = Vector3.one * runeScale;

        runeSpriteRenderer = visualContainer.GetComponentInChildren<SpriteRenderer>();
        runeMeshRenderer = visualContainer.GetComponentInChildren<MeshRenderer>();

        if (runeSpriteRenderer == null && runeMeshRenderer == null)
        {
            BuildProceduralTriangleRune();
        }

        if (showCountdownRing)
        {
            BuildProceduralRadialRing();
        }
    }

    /// <summary>
    /// Cria a malha triangular do cristal com ponta voltada para baixo.
    /// </summary>
    private void BuildProceduralTriangleRune()
    {
        GameObject runeObj = new GameObject("RuneMesh");
        runeObj.transform.SetParent(visualContainer, false);

        MeshFilter mf = runeObj.AddComponent<MeshFilter>();
        runeMeshRenderer = runeObj.AddComponent<MeshRenderer>();

        Mesh triangleMesh = new Mesh();
        triangleMesh.name = "CrystalRune_Triangle";

        Vector3[] vertices = new Vector3[] {
            new Vector3(-0.35f,  0.30f, 0f),
            new Vector3( 0.35f,  0.30f, 0f),
            new Vector3( 0.00f, -0.35f, 0f),
            new Vector3( 0.00f,  0.05f, -0.08f)
        };

        int[] triangles = new int[] {
            0, 3, 2,
            3, 1, 2,
            0, 1, 3,
            0, 2, 1
        };

        Vector2[] uv = new Vector2[] {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0.5f)
        };

        triangleMesh.vertices = vertices;
        triangleMesh.triangles = triangles;
        triangleMesh.uv = uv;
        triangleMesh.RecalculateNormals();
        triangleMesh.RecalculateBounds();

        mf.sharedMesh = triangleMesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");

        runeMaterial = new Material(shader);
        runeMaterial.color = normalColor;
        if (runeMaterial.HasProperty("_BaseColor")) runeMaterial.SetColor("_BaseColor", normalColor);
        if (runeMaterial.HasProperty("_EmissionColor"))
        {
            runeMaterial.EnableKeyword("_EMISSION");
            runeMaterial.SetColor("_EmissionColor", normalColor * 1.4f);
        }

        runeMeshRenderer.material = runeMaterial;
        runeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        runeMeshRenderer.receiveShadows = false;
    }

    /// <summary>
    /// Configura o LineRenderer exclusivo para o anel de cooldown radial 360°.
    /// </summary>
    private void BuildProceduralRadialRing()
    {
        // Limpa quaisquer LineRenderers residuais nos filhos para evitar circulos duplicados
        LineRenderer[] existingLrs = visualContainer.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < existingLrs.Length; i++)
        {
            if (Application.isPlaying)
            {
                Destroy(existingLrs[i].gameObject);
            }
            else
            {
                DestroyImmediate(existingLrs[i].gameObject);
            }
        }

        GameObject ringObj = new GameObject("RadialCooldownRing");
        ringObj.transform.SetParent(visualContainer, false);

        activeRingRenderer = ringObj.AddComponent<LineRenderer>();
        activeRingRenderer.useWorldSpace = false;
        activeRingRenderer.loop = false;
        activeRingRenderer.startWidth = ringWidth;
        activeRingRenderer.endWidth = ringWidth;
        activeRingRenderer.numCapVertices = 4;
        activeRingRenderer.numCornerVertices = 4;
        activeRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        activeRingRenderer.receiveShadows = false;

        Shader ringShader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit");

        Material ringMat = new Material(ringShader);
        ringMat.color = normalColor;
        activeRingRenderer.material = ringMat;

        UpdateRadialArc(1.0f);
    }

    /// <summary>
    /// Atualiza o arco circular diminuindo de 360° ate 0° no sentido horario (efeito de radial cooldown fill).
    /// Conforme o tempo acaba, o anel vai sendo apagado radialmente a partir do topo no sentido horario.
    /// </summary>
    private void UpdateRadialArc(float ratio)
    {
        if (activeRingRenderer == null || !showCountdownRing) return;

        if (ratio <= 0.01f)
        {
            activeRingRenderer.positionCount = 0;
            return;
        }

        int activePoints = Mathf.Max(3, Mathf.RoundToInt(ringSegments * ratio));
        activeRingRenderer.positionCount = activePoints;

        // O apagador comeca no topo (12 horas = PI/2) e vai varrendo no sentido horario.
        // O arco restante vai do ponto de corte atual ate fechar no topo (12 horas).
        float elapsed = 1f - ratio;
        float startAngle = (Mathf.PI * 0.5f) - (elapsed * Mathf.PI * 2f);
        float totalArcSpan = (Mathf.PI * 2f) * ratio;

        for (int i = 0; i < activePoints; i++)
        {
            float t = (float)i / (activePoints - 1);
            float angle = startAngle - (t * totalArcSpan);

            float x = Mathf.Cos(angle) * ringRadius;
            float y = Mathf.Sin(angle) * ringRadius;
            activeRingRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private void LateUpdate()
    {
        if (isShattered || isExpiring) return;

        // 1. Validacao do Alvo
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy || IsTargetDead())
        {
            Expire();
            return;
        }

        // 2. Contagem de Tempo
        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            remainingDuration = 0f;
            if (autoExpireOnTimeout)
            {
                Expire();
                return;
            }
        }

        // 3. Acompanhamento suave da cabeca do inimigo
        Vector3 targetHeadPos = CalculateTargetHeadPosition();
        currentWorldPosition = Vector3.Lerp(currentWorldPosition, targetHeadPos, Time.deltaTime * followSpeed);
        transform.position = currentWorldPosition;

        // 4. Orientacao para a Camera (Billboard)
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera != null)
        {
            transform.rotation = cachedCamera.transform.rotation;
        }

        // 5. Flutuacao da Runa
        if (visualContainer != null)
        {
            float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            visualContainer.localPosition = new Vector3(0f, bobOffset, 0f);

            if (spinSpeed != 0f)
            {
                visualContainer.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);
            }
        }

        // 6. Atualizacao do Arco Radial e Piscar de Urgencia
        UpdateVisualTimerFeedback();
    }

    /// <summary>
    /// Atualiza o preenchimento do arco radial de 360° e o efeito de pulso nos segundos finais.
    /// </summary>
    private void UpdateVisualTimerFeedback()
    {
        float ratio = RemainingRatio;

        // Atualiza o arco radial apagando no sentido horario com raio fixo
        UpdateRadialArc(ratio);

        bool isCritical = remainingDuration <= criticalThreshold;
        Color currentColor;

        if (isCritical)
        {
            float urgencyRatio = 1f - Mathf.Clamp01(remainingDuration / criticalThreshold);
            float blinkFrequency = Mathf.Lerp(4f, 14f, urgencyRatio);
            float blink = (Mathf.Sin(Time.time * blinkFrequency * Mathf.PI * 2f) + 1f) * 0.5f;

            currentColor = Color.Lerp(normalColor, criticalColor, blink);
            if (blink > 0.8f)
            {
                currentColor = Color.Lerp(currentColor, Color.white, 0.4f);
            }
        }
        else
        {
            float idlePulse = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
            currentColor = Color.Lerp(normalColor, normalColor * 1.15f, idlePulse);
        }

        ApplyColorToVisuals(currentColor);
    }

    private void ApplyColorToVisuals(Color color)
    {
        if (runeSpriteRenderer != null)
        {
            runeSpriteRenderer.color = color;
        }
        if (runeMaterial != null)
        {
            runeMaterial.color = color;
            if (runeMaterial.HasProperty("_BaseColor")) runeMaterial.SetColor("_BaseColor", color);
            if (runeMaterial.HasProperty("_EmissionColor")) runeMaterial.SetColor("_EmissionColor", color * 1.5f);
        }
        if (activeRingRenderer != null)
        {
            activeRingRenderer.startColor = color;
            activeRingRenderer.endColor = color;
            if (activeRingRenderer.material != null) activeRingRenderer.material.color = color;
        }
    }

    /// <summary>
    /// Calcula a posicao acima do topo do inimigo com base em colisor ou renderizador.
    /// </summary>
    private Vector3 CalculateTargetHeadPosition()
    {
        if (targetTransform == null) return transform.position;

        Vector3 targetBasePos = targetTransform.position;
        float topY = targetBasePos.y;

        if (useBoundsTop)
        {
            if (targetCollider != null && targetCollider.enabled)
            {
                topY = Mathf.Max(topY, targetCollider.bounds.max.y);
            }
            else if (targetRenderer != null && targetRenderer.enabled)
            {
                topY = Mathf.Max(topY, targetRenderer.bounds.max.y);
            }
            else
            {
                topY += 1.8f;
            }
        }
        else
        {
            topY += 1.8f;
        }

        return new Vector3(targetBasePos.x, topY + heightOffset, targetBasePos.z) + manualPositionOffset;
    }

    private bool IsTargetDead()
    {
        if (targetDummyHealth != null && targetDummyHealth.CurrentHealth <= 0) return true;
        if (targetSwarmHealth != null && targetSwarmHealth.CurrentHealth <= 0) return true;
        return false;
    }

    /// <summary>
    /// Dispara o efeito de estilhacamento, desativa a marca e instancia as particulas.
    /// </summary>
    [ContextMenu("Trigger Shatter")]
    public void TriggerShatter()
    {
        if (isShattered || isExpiring) return;
        isShattered = true;

        Vector3 spawnPosition = transform.position;

        if (shatterVFXPrefab != null)
        {
            Instantiate(shatterVFXPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            GameObject shatterObj = new GameObject("[TunerMark_ShatterBurst]");
            shatterObj.transform.position = spawnPosition;
            var shatterScript = shatterObj.AddComponent<TunerMarkShatterVFX>();
            shatterScript.crystalCoreColor = normalColor;
            shatterScript.crystalAmberColor = criticalColor;
            shatterScript.shatterAudioClip = shatterAudioClip;
            shatterScript.audioVolume = shatterVolume;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Realiza a animacao suave de fade-out e destruicao ao expirar o tempo.
    /// </summary>
    [ContextMenu("Trigger Expire")]
    public void Expire()
    {
        if (isShattered || isExpiring) return;
        isExpiring = true;
        StartCoroutine(AnimateFadeOutAndDestroy());
    }

    private IEnumerator AnimateFadeOutAndDestroy()
    {
        float fadeDuration = 0.35f;
        float elapsed = 0f;
        Vector3 initialScale = visualContainer != null ? visualContainer.localScale : Vector3.one;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            float alpha = Mathf.Lerp(1f, 0f, t);

            if (visualContainer != null)
            {
                visualContainer.localScale = Vector3.Lerp(initialScale, initialScale * 1.3f, t);
            }

            Color fadedColor = normalColor;
            fadedColor.a = alpha;
            ApplyColorToVisuals(fadedColor);

            yield return null;
        }

        Destroy(gameObject);
    }
}
