using UnityEngine;
using TMPro;

/// <summary>
/// Exibe os atributos do jogador na tela em tempo real.
/// Funciona com o sistema de atributos separados (Offensive + Defensive + Health).
/// </summary>
public class AttributeDisplayOnScreenSplit : MonoBehaviour
{
    [Header("References")]
    public PlayerAttributesOffensive offensiveAttributes;
    public PlayerAttributesDefensive defensiveAttributes;
    public PlayerHealth playerHealth;
    
    [Header("UI Settings")]
    public bool showDisplay = true;
    public KeyCode toggleKey = KeyCode.F5;
    
    [Header("Display Appearance")]
    [Tooltip("Largura do painel")]
    [Range(200f, 800f)]
    public float panelWidth = 400f;
    
    [Tooltip("Altura do painel")]
    [Range(200f, 800f)]
    public float panelHeight = 550f;
    
    [Tooltip("Tamanho da fonte")]
    [Range(10f, 30f)]
    public float fontSize = 14f;
    
    [Tooltip("Opacidade do fundo (0 = transparente, 1 = opaco)")]
    [Range(0f, 1f)]
    public float backgroundOpacity = 0.7f;
    
    [Header("Display Position")]
    [Tooltip("Distância da borda esquerda")]
    public float offsetX = 10f;
    
    [Tooltip("Distância da borda superior")]
    public float offsetY = -10f;
    
    [Header("Display Settings")]
    [Tooltip("Atualizar a cada X segundos (0 = todo frame)")]
    public float updateInterval = 0.5f;
    
    private TextMeshProUGUI displayText;
    private float updateTimer = 0f;
    private GameObject displayPanel;
    
    void Start()
    {
        // Buscar atributos
        if (offensiveAttributes == null)
        {
            offensiveAttributes = GetComponentInChildren<PlayerAttributesOffensive>();
        }
        
        if (defensiveAttributes == null)
        {
            defensiveAttributes = GetComponent<PlayerAttributesDefensive>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }
        
        // Criar UI
        CreateDisplayUI();
        
        Debug.Log("✅ Attribute Display criado! Pressione F5 para mostrar/ocultar");
    }
    
    void Update()
    {
        // Toggle display
        if (Input.GetKeyDown(toggleKey))
        {
            showDisplay = !showDisplay;
            if (displayPanel != null)
            {
                displayPanel.SetActive(showDisplay);
            }
        }
        
        // Aplicar mudanças do Inspector em tempo real
        if (displayPanel != null)
        {
            RectTransform panelRect = displayPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
                panelRect.anchoredPosition = new Vector2(offsetX, offsetY);
            }
            
            UnityEngine.UI.Image bgImage = displayPanel.GetComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                Color bgColor = bgImage.color;
                bgColor.a = backgroundOpacity;
                bgImage.color = bgColor;
            }
        }
        
        if (displayText != null)
        {
            displayText.fontSize = fontSize;
        }
        
        // Atualizar texto
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
        // SEMPRE criar um Canvas dedicado para o display de atributos
        GameObject canvasObj = new GameObject("AttributeDisplayCanvas");
        DontDestroyOnLoad(canvasObj); // Persistir entre cenas (como o Player)
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Garantir que fique na frente
        
        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        Debug.Log("✅ Canvas dedicado criado para Attribute Display");
        
        // Criar painel
        displayPanel = new GameObject("AttributeDisplayPanel");
        displayPanel.transform.SetParent(canvas.transform, false);
        
        RectTransform panelRect = displayPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(offsetX, offsetY);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        
        // Background semi-transparente
        UnityEngine.UI.Image bgImage = displayPanel.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0, 0, 0, backgroundOpacity);
        
        // Criar texto
        GameObject textObj = new GameObject("AttributeText");
        textObj.transform.SetParent(displayPanel.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
        displayText = textObj.AddComponent<TextMeshProUGUI>();
        displayText.fontSize = fontSize;
        displayText.alignment = TextAlignmentOptions.TopLeft;
        displayText.color = Color.white;
        displayText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        
        displayPanel.SetActive(showDisplay);
    }
    
    void UpdateDisplayText()
    {
        if (displayText == null) return;
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        
        string text = "<b><color=#FFD700>═══ PLAYER ATTRIBUTES ═══</color></b>\n\n";
        
        // Atributos Ofensivos
        if (offensiveAttributes != null)
        {
            text += "<b><color=#FF6B6B>⚔️ OFFENSIVE</color></b>\n";
            text += $"  Base Damage: <color=#FF4500>{offensiveAttributes.baseDamageMultiplier:F2}x</color>\n";
            text += $"  Attack Speed: <color=#00FF00>{offensiveAttributes.attackSpeedMelee:F2}x</color>\n";
            text += $"  Crit Chance: <color=#FFFF00>{offensiveAttributes.critChance:F1}%</color>\n";
            text += $"  Crit Multiplier: <color=#FF8C00>{offensiveAttributes.critMultiplier:F2}x</color>\n";
            text += $"  Weapon Range: <color=#00BFFF>{offensiveAttributes.weaponRangeMelee:F2}x</color>\n";
            text += $"  Knockback: <color=#FF1493>{offensiveAttributes.knockback:F2}</color>\n";
            text += $"  Piercing: <color=#9370DB>{offensiveAttributes.piercing}</color>\n";
        }
        else
        {
            text += "<color=#FF0000>❌ Offensive Attributes Missing</color>\n";
        }
        
        text += "\n";
        
        // Atributos Defensivos & Vida
        if (defensiveAttributes != null || playerHealth != null)
        {
            text += "<b><color=#4169E1>🛡️ DEFENSIVE & VITAL</color></b>\n";
            if (playerHealth != null)
            {
                text += $"  Max Health: <color=#00FF88>{playerHealth.maxHealth}</color> (Atual: {playerHealth.currentHealth})\n";
                text += $"  Max Armor: <color=#00BFFF>{playerHealth.maxArmor}</color> (Atual: {playerHealth.CurrentArmor})\n";
            }
            if (defensiveAttributes != null)
            {
                text += $"  Armor Regen: <color=#00FF00>{defensiveAttributes.armorRegen:F2}x</color>\n";
                text += $"  Dodge Chance: <color=#FFFF00>{defensiveAttributes.dodgeChance:F1}%</color>\n";
                text += $"  Damage Negation: <color=#FFA500>{defensiveAttributes.damageNegation:F1}%</color>\n";
                text += $"  Thorns: <color=#8B4513>{defensiveAttributes.thorns}</color>\n";
                if (defensiveAttributes.healthRegen > 0f)
                    text += $"  Health Regen: <color=#00FF88>+{defensiveAttributes.healthRegen:F1}/s</color>\n";
            }
            
            if (defensiveAttributes != null)
            {
                text += "\n<b><color=#32CD32>⚡ MOBILITY</color></b>\n";
                text += $"  Speed: <color=#00FF00>{defensiveAttributes.speedMultiplier:F2}x</color>\n";
                text += $"  Dash Cooldown: <color=#00BFFF>{defensiveAttributes.dashCooldownMultiplier:F2}x</color>\n";
                text += $"  Dash Counts: <color=#FFD700>{defensiveAttributes.dashCounts}</color>\n";
                text += $"  Dash i-frames: <color=#FF69B4>{defensiveAttributes.dashInvulnerability:F2}s</color>\n";
            }
        }
        else
        {
            text += "<color=#FF0000>❌ Defensive Attributes Missing</color>\n";
        }
        
        text += "\n<size=10><color=#808080>Press F5 to toggle | F1 for debug console</color></size>";
        
        displayText.text = text;
    }
    
    /// <summary>
    /// Força atualização imediata do display.
    /// </summary>
    public void ForceUpdate()
    {
        UpdateDisplayText();
    }
}
