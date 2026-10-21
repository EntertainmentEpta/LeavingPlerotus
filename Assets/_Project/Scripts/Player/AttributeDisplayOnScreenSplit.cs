using UnityEngine;
using TMPro;

/// <summary>
/// Exibe os atributos completos do jogador na tela em tempo real com estilo Sci-Fi HUD.
/// Exibe Locomoção/Velocidade (em m/s e multiplicador), Ofensivo, Defensivo/Vitalidade e Infusões.
/// Integrado com o Robozinho através do Módulo de Telemetria (receita gratuita) ou tecla F5.
/// </summary>
public class AttributeDisplayOnScreenSplit : MonoBehaviour
{
    public static AttributeDisplayOnScreenSplit Instance { get; private set; }

    [Header("References")]
    public PlayerAttributesOffensive offensiveAttributes;
    public PlayerAttributesDefensive defensiveAttributes;
    public PlayerHealth playerHealth;
    public PlayerM playerMovement;
    public InfusionManager infusionManager;
    
    [Header("UI Settings")]
    public bool showDisplay = true;
    public KeyCode toggleKey = KeyCode.F5;
    
    [Header("Display Appearance")]
    [Tooltip("Largura do painel")]
    public float panelWidth = 440f;
    
    [Tooltip("Altura do painel")]
    public float panelHeight = 620f;
    
    [Tooltip("Tamanho da fonte")]
    public float fontSize = 13f;
    
    [Tooltip("Opacidade do fundo (0 = transparente, 1 = opaco)")]
    public float backgroundOpacity = 0.88f;
    
    [Header("Display Position")]
    [Tooltip("Distância da borda esquerda")]
    public float offsetX = 16f;
    
    [Tooltip("Distância da borda superior")]
    public float offsetY = -16f;
    
    [Header("Display Settings")]
    [Tooltip("Atualizar a cada X segundos")]
    public float updateInterval = 0.25f;

    public static bool isModuleEquipped = true; // Por padrão ativo para testes fáceis, alternável via Robozinho ou F5
    
    private TextMeshProUGUI displayText;
    private float updateTimer = 0f;
    private GameObject displayPanel;

    public static void EnsureExistsAndSetVisible(bool visible)
    {
        isModuleEquipped = visible;
        if (Instance != null)
        {
            Instance.showDisplay = visible;
            if (Instance.displayPanel != null)
            {
                Instance.displayPanel.SetActive(visible);
            }
        }
    }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        FindPlayerReferences();
        CreateDisplayUI();
        
        Debug.Log("✅ [TELEMETRIA] Monitor Telemétrico de Atributos ativo! Pressione F5 para alternar exibição.");
    }

    void FindPlayerReferences()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = gameObject;

        if (offensiveAttributes == null && p != null)
            offensiveAttributes = p.GetComponentInChildren<PlayerAttributesOffensive>();
        
        if (defensiveAttributes == null && p != null)
            defensiveAttributes = p.GetComponentInChildren<PlayerAttributesDefensive>();

        if (playerHealth == null && p != null)
            playerHealth = p.GetComponentInChildren<PlayerHealth>();

        if (playerMovement == null && p != null)
            playerMovement = p.GetComponentInChildren<PlayerM>();

        if (infusionManager == null && p != null)
            infusionManager = p.GetComponentInChildren<InfusionManager>() ?? Object.FindFirstObjectByType<InfusionManager>();
    }
    
    void Update()
    {
        // Toggle display via tecla de atalho
        if (Input.GetKeyDown(toggleKey))
        {
            showDisplay = !showDisplay;
            if (displayPanel != null)
            {
                displayPanel.SetActive(showDisplay);
            }
        }
        
        // Atualizar texto em tempo real
        if (showDisplay && displayText != null)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                UpdateDisplayText();
                updateTimer = 0f;
            }
        }
    }
    
    void CreateDisplayUI()
    {
        // Canvas dedicado para o display de atributos
        GameObject canvasObj = new GameObject("AttributeDisplayCanvas");
        DontDestroyOnLoad(canvasObj);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        
        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Painel Principal Estilo Tech Sci-Fi
        displayPanel = new GameObject("AttributeDisplayPanel");
        displayPanel.transform.SetParent(canvas.transform, false);
        
        RectTransform panelRect = displayPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(offsetX, offsetY);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        
        // Fundo escuro azulado elegante
        UnityEngine.UI.Image bgImage = displayPanel.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.04f, 0.07f, 0.12f, backgroundOpacity);
        
        // Borda e Sombra de Estilo Tecnológico
        UnityEngine.UI.Outline outline = displayPanel.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0.80f, 1f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        UnityEngine.UI.Shadow shadow = displayPanel.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(4f, -4f);
        
        // Objeto de Texto Formatado
        GameObject textObj = new GameObject("AttributeText");
        textObj.transform.SetParent(displayPanel.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16, 16);
        textRect.offsetMax = new Vector2(-16, -16);
        
        displayText = textObj.AddComponent<TextMeshProUGUI>();
        displayText.fontSize = fontSize;
        displayText.alignment = TextAlignmentOptions.TopLeft;
        displayText.color = Color.white;
        displayText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        displayText.richText = true;
        
        displayPanel.SetActive(showDisplay);
        UpdateDisplayText();
    }
    
    void UpdateDisplayText()
    {
        if (displayText == null) return;
        FindPlayerReferences();
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Cabeçalho Sci-Fi
        sb.AppendLine("<b><color=#00E5FF>◈ TELEMETRIA BIOMÉTRICA DO TRAJE ◈</color></b>");
        sb.AppendLine("<size=75%><color=#7E97B8>DIAGNÓSTICO EM TEMPO REAL | ALTERNAR: [F5]</color></size>");
        sb.AppendLine("<color=#005577>────────────────────────────────────────────</color>");

        // 1. LOCOMOÇÃO & VELOCIDADE
        sb.AppendLine("<b><color=#00FFAA>⚡ LOCOMOÇÃO & MOBILIDADE</color></b>");
        float baseWalk = 6f;
        float baseSprint = 11f;
        float debuff = 1f;
        if (playerMovement != null)
        {
            baseSprint = playerMovement.sprintSpeed;
            baseWalk = playerMovement.walkSpeed;
            debuff = playerMovement.debuffSpeedMultiplier;
        }
        float speedMult = defensiveAttributes != null ? defensiveAttributes.speedMultiplier : 1f;
        float finalSprintSpeed = baseSprint * speedMult * debuff;
        
        sb.AppendLine($"  Velocidade de Corrida: <color=#00FFCC><b>{finalSprintSpeed:F1} m/s</b></color> <size=80%><color=#8899AA>({speedMult:F2}x)</color></size>");
        if (defensiveAttributes != null)
        {
            sb.AppendLine($"  Recarga do Dash: <color=#00E5FF>{defensiveAttributes.dashCooldownMultiplier:F2}x</color>");
            sb.AppendLine($"  Cargas de Dash: <color=#FFD700>{defensiveAttributes.dashCounts}</color> <size=80%><color=#8899AA>(Invul: {defensiveAttributes.dashInvulnerability:F2}s)</color></size>");
        }
        sb.AppendLine();

        // 2. SISTEMA OFENSIVO
        sb.AppendLine("<b><color=#FF5555>⚔️ POTÊNCIA OFENSIVA</color></b>");
        if (offensiveAttributes != null)
        {
            sb.AppendLine($"  Dano Base: <color=#FF4444><b>{offensiveAttributes.baseDamageMultiplier:F2}x</b></color>");
            sb.AppendLine($"  Vel. de Ataque: <color=#FFAA00>{offensiveAttributes.attackSpeedMelee:F2}x</color>");
            sb.AppendLine($"  Crítico: <color=#FFFF00>{offensiveAttributes.critChance:F1}%</color> <size=80%><color=#8899AA>(Dano: {offensiveAttributes.critMultiplier:F2}x)</color></size>");
            sb.AppendLine($"  Alcance da Arma: <color=#33CCFF>{offensiveAttributes.weaponRangeMelee:F2}x</color>");
            sb.AppendLine($"  Impacto / Repulsão: <color=#FF66CC>{offensiveAttributes.knockback:F1}</color>");
            if (offensiveAttributes.piercing > 0)
                sb.AppendLine($"  Perfuração de Alvos: <color=#BB88FF>+{offensiveAttributes.piercing}</color>");
        }
        else
        {
            sb.AppendLine("  <color=#8888AA><i>Atributos ofensivos aguardando carregamento...</i></color>");
        }
        sb.AppendLine();

        // 3. DEFESA & VITALIDADE
        sb.AppendLine("<b><color=#3399FF>🛡️ DEFESA & BIOMÉTRICA</color></b>");
        if (playerHealth != null)
        {
            sb.AppendLine($"  Integridade Vital (HP): <color=#00FF88><b>{playerHealth.currentHealth} / {playerHealth.maxHealth}</b></color>");
            sb.AppendLine($"  Blindagem (Armadura): <color=#00CCFF><b>{playerHealth.CurrentArmor} / {playerHealth.maxArmor}</b></color>");
        }
        if (defensiveAttributes != null)
        {
            sb.AppendLine($"  Regen. Armadura: <color=#00FFAA>{defensiveAttributes.armorRegen:F2}x</color>");
            if (defensiveAttributes.dodgeChance > 0f)
                sb.AppendLine($"  Esquiva Reativa: <color=#FFFF00>{defensiveAttributes.dodgeChance:F1}%</color>");
            if (defensiveAttributes.damageNegation > 0f)
                sb.AppendLine($"  Negação de Dano: <color=#FFAA33>{defensiveAttributes.damageNegation:F1}%</color>");
            if (defensiveAttributes.thorns > 0)
                sb.AppendLine($"  Retaliação (Espinhos): <color=#FF8855>{defensiveAttributes.thorns}</color>");
            if (defensiveAttributes.healthRegen > 0f)
                sb.AppendLine($"  Regeneração Vital: <color=#00FF88>+{defensiveAttributes.healthRegen:F1}/s</color>");
        }
        sb.AppendLine();

        // 4. INFUSÕES & ARSENAL
        sb.AppendLine("<b><color=#E0AAFF>🧪 SINTONIA DE INFUSÕES</color></b>");
        if (infusionManager != null)
        {
            int curInf = infusionManager.CurrentInfusionCount;
            int maxCap = infusionManager.GetMaxInfusionCapacity();
            float ptotal = infusionManager.GetTotalWeight();
            float inflationPercent = (infusionManager.GetInflationFactor() - 1f) * 100f;

            sb.AppendLine($"  Peças Infundidas: <color=#FFD700><b>{curInf}/{maxCap}</b></color>");
            sb.AppendLine($"  Peso de Inflação: <color=#FF88AA>{ptotal:F1}</color> <size=80%><color=#FFAA55>(Custo: +{inflationPercent:F0}%)</color></size>");
        }
        else
        {
            sb.AppendLine("  <color=#8888AA><i>Nenhuma infusão detectada na arma.</i></color>");
        }

        displayText.text = sb.ToString();
    }
    
    public void ForceUpdate()
    {
        UpdateDisplayText();
    }
}
