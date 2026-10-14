using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// StunVFX — Efeitos visuais do Stun no Player (100% Automático):
///
/// 1. ÍCONE ESPIRAL (World Space):
///    - Fica acima da cabeça do player.
///    - Rotação contínua enquanto o stun durar.
///    - Billboard via FaceCamera (olha sempre para a câmera, com rotação suave do sprite).
///    - Carrega automaticamente o sprite "stun_spiral" da pasta Resources (ou gera procedural caso não encontre).
///
/// 2. STUN TIME BAR (Screen Space / HUD - Estilo League of Legends):
///    - Barra roxa posicionada exatamente acima da healthbar do player.
///    - Mesma largura da barra de vida.
///    - Reduz suavemente ao longo da duração do stun até zerar e sumir.
///    - Localiza automaticamente a barra de vida via PlayerHealth.healthFillImage ou hierarchy ("Health_Fill").
/// </summary>
public class StunVFX : MonoBehaviour
{
    // ── Referências Opcionais (se vazias, o script busca e resolve automaticamente) ──
    [Header("Ícone Espiral (World Space)")]
    [Tooltip("Sprite da espiral. Se vazio, carrega automaticamente de Resources/stun_spiral.")]
    public Sprite stunSpiralSprite;

    [Tooltip("Altura acima do pivot do player onde o ícone aparece.")]
    public float iconHeightOffset = 2.4f;

    [Tooltip("Tamanho do ícone no mundo (reduzido para 5% do tamanho original).")]
    public float iconWorldSize = 0.04f;

    [Tooltip("Velocidade de rotação do ícone (graus/segundo).")]
    public float iconRotationSpeed = 540f;

    [Header("StunTimeBar (HUD)")]
    [Tooltip("Referência da HealthBar. Se vazia, localiza automaticamente no PlayerHealth ou na cena.")]
    public Image healthBarReference;

    [Tooltip("Cor da StunTimeBar (padrão CC roxo vibrante).")]
    public Color stunBarColor = new Color(0.68f, 0.2f, 1f, 1f); // Roxo vibrante estilo LoL

    [Tooltip("Cor de fundo da StunTimeBar.")]
    public Color stunBarBgColor = new Color(0.15f, 0.05f, 0.25f, 0.75f);

    [Tooltip("Altura da barra de stun em pixels.")]
    public float stunBarHeight = 8f;

    [Tooltip("Distância vertical acima da barra de vida (em pixels).")]
    public float stunBarYOffset = 4f;

    // ── Runtime ────────────────────────────────────────────────────────
    private PlayerHealth playerHealth;
    private Coroutine activeStunRoutine;
    private float currentRotationAngle = 0f;

    // World-space icon
    private GameObject iconRoot;
    private GameObject iconVisual;
    private SpriteRenderer iconRenderer;

    // HUD bar
    private GameObject stunBarRoot;
    private Image stunBarFill;
    private Image stunBarBackground;

    private static Sprite whitePixelSprite;

    void Awake()
    {
        // Força os novos valores caso o Inspector/Prefab tenha mantido valores antigos salvos
        if (iconWorldSize > 0.1f) iconWorldSize = 0.04f;
        if (iconRotationSpeed < 300f) iconRotationSpeed = 540f;

        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        ResolveReferences();
    }

    void Start()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Resolve todas as referências automaticamente caso o usuário não tenha arrastado nada no Inspector.
    /// </summary>
    public void ResolveReferences()
    {
        // 1. Sprite da espiral
        if (stunSpiralSprite == null)
        {
            stunSpiralSprite = Resources.Load<Sprite>("stun_spiral");
            if (stunSpiralSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("stun_spiral");
                if (tex != null)
                {
                    stunSpiralSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }

        // 2. Referência da barra de vida (Image)
        if (healthBarReference == null)
        {
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth != null && playerHealth.healthFillImage != null)
            {
                healthBarReference = playerHealth.healthFillImage;
            }
            else
            {
                GameObject healthObj = GameObject.Find("Health_Fill");
                if (healthObj != null)
                {
                    healthBarReference = healthObj.GetComponent<Image>();
                }
                else
                {
                    GameObject sliderObj = GameObject.Find("HealthBar_Slider");
                    if (sliderObj != null)
                    {
                        Slider slider = sliderObj.GetComponent<Slider>();
                        if (slider != null && slider.fillRect != null)
                        {
                            healthBarReference = slider.fillRect.GetComponent<Image>();
                        }
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // API pública
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispara os efeitos visuais de stun pela duração informada.
    /// </summary>
    public void ShowStun(float duration)
    {
        if (duration <= 0f) return;

        ResolveReferences();

        if (activeStunRoutine != null)
        {
            StopCoroutine(activeStunRoutine);
        }

        activeStunRoutine = StartCoroutine(StunVFXRoutine(duration));
    }

    /// <summary>
    /// Encerra imediatamente todos os efeitos de stun (ícone e barra).
    /// Chamado pelo PlayerHealth ao acordar do stun, morrer ou no fim do timer.
    /// </summary>
    public void HideStun()
    {
        if (activeStunRoutine != null)
        {
            StopCoroutine(activeStunRoutine);
            activeStunRoutine = null;
        }
        DestroyIcon();
        DestroyHUDBar();
    }

    void Update()
    {
        // Se o PlayerHealth não está mais stunado ou morreu, o ícone DEVE sumir imediatamente!
        if (playerHealth != null && (!playerHealth.isStunned || playerHealth.isDead))
        {
            if (iconRoot != null || stunBarRoot != null || activeStunRoutine != null)
            {
                HideStun();
            }
            return;
        }

        // Gira o sprite continuamente a cada frame enquanto o ícone existir
        if (iconVisual != null)
        {
            currentRotationAngle += iconRotationSpeed * Time.deltaTime;
            iconVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -currentRotationAngle);
        }
    }

    private IEnumerator StunVFXRoutine(float totalDuration)
    {
        try
        {
            SpawnWorldIcon();
            SpawnHUDBar();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[StunVFX] Erro ao instanciar efeitos de stun: {ex.Message}");
        }

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            // Se o player acordou antes do tempo, encerra imediatamente
            if (playerHealth != null && (!playerHealth.isStunned || playerHealth.isDead))
            {
                break;
            }

            elapsed += Time.deltaTime;
            float remainingRatio = Mathf.Clamp01(1f - (elapsed / totalDuration));

            // Atualiza a barra de stun (reduz da largura cheia até 0)
            if (stunBarFill != null)
            {
                stunBarFill.fillAmount = remainingRatio;
            }

            yield return null;
        }

        HideStun();
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. Ícone Espiral (World-Space)
    // ─────────────────────────────────────────────────────────────────

    void SpawnWorldIcon()
    {
        DestroyIcon();

        // Root container posicionado na cabeça do player com FaceCamera
        iconRoot = new GameObject("StunIcon_Root");
        iconRoot.transform.SetParent(transform, false);
        iconRoot.transform.localPosition = Vector3.up * iconHeightOffset;
        iconRoot.AddComponent<FaceCamera>();

        // Objeto visual filho que executa a rotação 2D
        iconVisual = new GameObject("StunIcon_Visual");
        iconVisual.transform.SetParent(iconRoot.transform, false);
        iconVisual.transform.localPosition = Vector3.zero;
        iconVisual.transform.localScale = Vector3.one * iconWorldSize;

        iconRenderer = iconVisual.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = GetOrLoadSprite();
        iconRenderer.color = Color.white; // Mantém a cor original do sprite limpa
        iconRenderer.sortingLayerName = "Default";
        iconRenderer.sortingOrder = 50;
    }

    void DestroyIcon()
    {
        if (iconRoot != null)
        {
            Destroy(iconRoot);
            iconRoot = null;
            iconVisual = null;
            iconRenderer = null;
            currentRotationAngle = 0f;
        }
    }

    private Sprite GetOrLoadSprite()
    {
        if (stunSpiralSprite != null) return stunSpiralSprite;

        ResolveReferences();
        if (stunSpiralSprite != null) return stunSpiralSprite;

        // Fallback procedural garantido caso nenhum arquivo tenha sido carregado
        stunSpiralSprite = GenerateProceduralSpiralSprite();
        return stunSpiralSprite;
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. StunTimeBar (HUD Screen-Space)
    // ─────────────────────────────────────────────────────────────────

    void SpawnHUDBar()
    {
        DestroyHUDBar();

        if (healthBarReference == null)
        {
            ResolveReferences();
        }

        if (healthBarReference == null)
        {
            return;
        }

        Canvas parentCanvas = healthBarReference.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        RectTransform healthRect = healthBarReference.rectTransform;
        Transform barParent = healthRect.parent != null ? healthRect.parent : parentCanvas.transform;

        // Container raiz da StunTimeBar instanciado com RectTransform nativo
        stunBarRoot = new GameObject("StunTimeBar", typeof(RectTransform));
        stunBarRoot.transform.SetParent(barParent, false);

        LayoutElement le = stunBarRoot.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        RectTransform rootRect = stunBarRoot.GetComponent<RectTransform>();

        // Copia âncoras e pivot para alinhamento perfeito com a healthbar
        rootRect.anchorMin = healthRect.anchorMin;
        rootRect.anchorMax = healthRect.anchorMax;
        rootRect.pivot = healthRect.pivot;

        // Determina altura da healthbar para posicionamento acima
        float refHeight = healthRect.rect.height;
        if (refHeight < 4f) refHeight = 24f;

        rootRect.anchoredPosition = healthRect.anchoredPosition + Vector2.up * (refHeight + stunBarYOffset);

        // Largura idêntica à healthbar
        float refWidth = healthRect.rect.width;
        if (refWidth < 20f) refWidth = 220f;
        rootRect.sizeDelta = new Vector2(refWidth, stunBarHeight);

        // Fundo da barra
        GameObject bgObj = new GameObject("StunBar_BG", typeof(RectTransform));
        bgObj.transform.SetParent(stunBarRoot.transform, false);
        stunBarBackground = bgObj.AddComponent<Image>();
        stunBarBackground.sprite = GetWhitePixelSprite();
        stunBarBackground.color = stunBarBgColor;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Barra de preenchimento (Fill) que encolhe
        GameObject fillObj = new GameObject("StunBar_Fill", typeof(RectTransform));
        fillObj.transform.SetParent(stunBarRoot.transform, false);
        stunBarFill = fillObj.AddComponent<Image>();
        stunBarFill.sprite = GetWhitePixelSprite();
        stunBarFill.color = stunBarColor;
        stunBarFill.type = Image.Type.Filled;
        stunBarFill.fillMethod = Image.FillMethod.Horizontal;
        stunBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        stunBarFill.fillAmount = 1f;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Efeito de entrada suave
        StartCoroutine(FlashEntrance());
    }

    IEnumerator FlashEntrance()
    {
        if (stunBarFill == null) yield break;

        float t = 0f;
        Color bright = Color.white;
        Color normal = stunBarColor;

        while (t < 0.12f)
        {
            t += Time.deltaTime;
            if (stunBarFill != null)
            {
                stunBarFill.color = Color.Lerp(bright, normal, t / 0.12f);
            }
            yield return null;
        }

        if (stunBarFill != null) stunBarFill.color = normal;
    }

    void DestroyHUDBar()
    {
        if (stunBarRoot != null)
        {
            Destroy(stunBarRoot);
            stunBarRoot = null;
            stunBarFill = null;
            stunBarBackground = null;
        }
    }

    void OnDisable()
    {
        HideStun();
    }

    void OnDestroy()
    {
        HideStun();
    }

    // ─────────────────────────────────────────────────────────────────
    // Utilitários de Textura / Sprite
    // ─────────────────────────────────────────────────────────────────

    private static Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whitePixelSprite;
    }

    private static Sprite GenerateProceduralSpiralSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color spiralCol = new Color(0.68f, 0.2f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.45f;

        for (float angle = 0f; angle < Mathf.PI * 8f; angle += 0.04f)
        {
            float r = (angle / (Mathf.PI * 8f)) * maxR;
            int px = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * r);
            int py = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * r);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = px + dx;
                    int ny = py + dy;
                    if (nx >= 0 && nx < size && ny >= 0 && ny < size)
                    {
                        tex.SetPixel(nx, ny, spiralCol);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
