using UnityEngine;

/// <summary>
/// Atributos DEFENSIVOS e de MOBILIDADE do jogador (ficam no GameObject pai "Player").
/// Usado por PlayerHealth, PlayerM, DashM.
/// </summary>
public class PlayerAttributesDefensive : MonoBehaviour
{
    // ========== ATRIBUTOS DEFENSIVOS (3.2) ==========
    [Header("Defensive Attributes")]
    [Tooltip("Multiplicador de velocidade de regeneração de armadura. 1.0 = normal")]
    public float armorRegen = 1.0f;
    
    [Tooltip("Chance percentual de anular completamente o dano recebido (0-100)")]
    [Range(0f, 100f)]
    public float dodgeChance = 0f;
    
    [Tooltip("Redução percentual de todo dano recebido (0-100). Ex: 20 = -20% dano")]
    [Range(0f, 100f)]
    public float damageNegation = 0f;
    
    [Tooltip("Dano devolvido ao atacante quando jogador é atingido por contato físico")]
    public int thorns = 0;
    
    [Tooltip("Regeneração de vida por segundo (valor somado, ex: 1 = 1 vida por segundo)")]
    public float healthRegen = 0f;
    
    // ========== ATRIBUTOS DE MOBILIDADE (3.3) ==========
    [Header("Mobility Attributes")]
    [Tooltip("Multiplicador de tempo de recarga do Dash. 1.0 = normal, 0.5 = metade do tempo")]
    public float dashCooldownMultiplier = 1.0f;
    
    [Tooltip("Número máximo de cargas de Dash que podem ser armazenadas")]
    public int dashCounts = 2;
    
    [Tooltip("Duração da janela de invencibilidade durante o Dash (em segundos)")]
    public float dashInvulnerability = 0.2f;
    
    [SerializeField]
    private float _speedMultiplier = 1.0f;
    public float speedMultiplier
    {
        get { return _speedMultiplier + temporarySpeedBoost; }
        set { _speedMultiplier = value; }
    }

    [HideInInspector]
    public float temporarySpeedBoost = 0f;

    [Tooltip("Multiplicador do raio de atração do ímã (Magnet) para essências")]
    public float magnetRangeMultiplier = 1.0f;

    private void Update()
    {
        if (temporarySpeedBoost > 0f)
        {
            temporarySpeedBoost = Mathf.Max(0f, temporarySpeedBoost - Time.deltaTime * 0.05f);
        }
    }

    /// <summary>
    /// Modifica um atributo pelo nome.
    /// </summary>
    public void ModifyAttribute(string attributeName, float value, bool isMultiplier = false)
    {
        switch (attributeName.ToLower())
        {
            // Vida & Armadura (repassa para PlayerHealth se chamado aqui)
            case "maxarmor":
            case "armor":
                GetComponent<PlayerHealth>()?.ModifyAttribute("maxarmor", value, isMultiplier);
                break;
            case "maxhealth":
            case "health":
                GetComponent<PlayerHealth>()?.ModifyAttribute("maxhealth", value, isMultiplier);
                break;

            // Defensivos
            case "armorregen":
                armorRegen = isMultiplier ? armorRegen * value : armorRegen + value;
                break;
            case "healthregen":
                healthRegen = isMultiplier ? healthRegen * value : healthRegen + value;
                break;
            case "dodgechance":
            case "dodge":
                if (isMultiplier)
                {
                    float bonus = (value > 1.0f) ? (value - 1f) * 100f : value * 100f;
                    dodgeChance = Mathf.Clamp(dodgeChance + bonus, 0f, 100f);
                }
                else
                {
                    dodgeChance = Mathf.Clamp(dodgeChance + value, 0f, 100f);
                }
                break;
            case "damagenegation":
            case "negation":
                if (isMultiplier)
                {
                    float bonus = (value > 1.0f) ? (value - 1f) * 100f : value * 100f;
                    damageNegation = Mathf.Clamp(damageNegation + bonus, 0f, 100f);
                }
                else
                {
                    damageNegation = Mathf.Clamp(damageNegation + value, 0f, 100f);
                }
                break;
            case "thorns":
                if (isMultiplier)
                {
                    if (thorns > 0)
                        thorns = Mathf.Max(0, Mathf.RoundToInt(thorns * value));
                    else
                        thorns = Mathf.Max(1, Mathf.RoundToInt((value - 1f) * 50f));
                }
                else
                {
                    thorns = Mathf.Max(0, thorns + Mathf.RoundToInt(value));
                }
                break;
                
            // Mobilidade
            case "dashcooldownmultiplier":
                // Cap de balanceamento: no máximo -40% de cooldown (0.60x)
                dashCooldownMultiplier = Mathf.Clamp(isMultiplier ? dashCooldownMultiplier * value : dashCooldownMultiplier + value, 0.60f, 1.50f);
                break;
            case "dashcounts":
                // Cap de balanceamento: máximo de 4 cargas totais de Dash
                dashCounts = Mathf.Clamp(isMultiplier ? Mathf.RoundToInt(dashCounts * value) : dashCounts + Mathf.RoundToInt(value), 1, 4);
                break;
            case "dashinvulnerability":
                dashInvulnerability = isMultiplier ? dashInvulnerability * value : dashInvulnerability + value;
                break;
            case "speedmultiplier":
            case "speed":
                // Cap de balanceamento: no máximo +35% de velocidade de movimento (1.35x)
                _speedMultiplier = Mathf.Clamp(isMultiplier ? _speedMultiplier * value : _speedMultiplier + value, 0.5f, 1.35f);
                break;
            case "magnetrangemultiplier":
            case "magnetrange":
                magnetRangeMultiplier = isMultiplier ? magnetRangeMultiplier * value : magnetRangeMultiplier + value;
                break;
                
            default:
                Debug.LogWarning($"PlayerAttributesDefensive: Atributo '{attributeName}' não encontrado!");
                break;
        }
    }
    
    /// <summary>
    /// Retorna o valor de um atributo pelo nome.
    /// </summary>
    public float GetAttribute(string attributeName)
    {
        switch (attributeName.ToLower())
        {
            // Vida & Armadura
            case "maxarmor":
            case "armor":
                PlayerHealth phA = GetComponent<PlayerHealth>();
                return phA != null ? phA.maxArmor : 0f;
            case "maxhealth":
            case "health":
                PlayerHealth phH = GetComponent<PlayerHealth>();
                return phH != null ? phH.maxHealth : 0f;

            // Defensivos
            case "armorregen": return armorRegen;
            case "healthregen": return healthRegen;
            case "dodgechance":
            case "dodge": return dodgeChance;
            case "damagenegation":
            case "negation": return damageNegation;
            case "thorns": return thorns;
            case "magnetrangemultiplier":
            case "magnetrange": return magnetRangeMultiplier;
            
            // Mobilidade
            case "dashcooldownmultiplier": return dashCooldownMultiplier;
            case "dashcounts": return dashCounts;
            case "dashinvulnerability": return dashInvulnerability;
            case "speedmultiplier":
            case "speed": return speedMultiplier;
            
            default:
                Debug.LogWarning($"PlayerAttributesDefensive: Atributo '{attributeName}' não encontrado!");
                return 0f;
        }
    }
    
    /// <summary>
    /// Reseta todos os atributos para valores padrão.
    /// </summary>
    public void ResetToDefaults()
    {
        // Defensivos
        armorRegen = 1.0f;
        dodgeChance = 0f;
        damageNegation = 0f;
        thorns = 0;
        healthRegen = 0f;
        
        // Mobilidade
        dashCooldownMultiplier = 1.0f;
        dashCounts = 2;
        dashInvulnerability = 0.2f;
        speedMultiplier = 1.0f;
        
        Debug.Log("PlayerAttributesDefensive: Atributos defensivos e de mobilidade resetados.");
    }
}
