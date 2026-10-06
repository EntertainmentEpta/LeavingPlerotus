using UnityEngine;

/// <summary>
/// Atributos OFENSIVOS do jogador (ficam no GameObject filho "astronaut").
/// Usado por PrimaryAttackKnife e outros scripts de combate.
/// </summary>
public class PlayerAttributesOffensive : MonoBehaviour
{
    public static PlayerAttributesOffensive Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ========== ATRIBUTOS OFENSIVOS (3.1) ==========
    [Header("Offensive Attributes")]
    [Tooltip("Multiplicador de dano base. 1.0 = normal, 1.5 = +50% dano base (afeta críticos)")]
    public float baseDamageMultiplier = 1.0f;
    
    [Tooltip("Multiplicador de velocidade de ataque corpo-a-corpo. 1.0 = normal, 2.0 = dobro")]
    public float attackSpeedMelee = 1.0f;
    
    [Tooltip("Chance percentual de causar dano crítico (0-100)")]
    [Range(0f, 100f)]
    public float critChance = 5.0f;
    
    [Tooltip("Multiplicador de dano em acertos críticos (ex: 1.5 = +50% dano)")]
    public float critMultiplier = 1.5f;
    
    [Tooltip("Multiplicador de alcance para armas corpo-a-corpo. 1.0 = normal")]
    public float weaponRangeMelee = 1.0f;
    
    [Tooltip("Distância máxima que projéteis viajam antes de desaparecer")]
    public float weaponRangeProjectile = 10.0f;
    
    [Tooltip("Força física aplicada ao inimigo ao ser atingido. 1.0 = normal")]
    public float knockback = 1.0f;
    
    [Tooltip("Número de inimigos que um ataque pode atravessar. 0 = sem piercing")]
    public int piercing = 0;
    
    [Tooltip("Chance percentual de projétil ricochetear (0-100)")]
    [Range(0f, 100f)]
    public float bounceChance = 0f;
    
    [Tooltip("Quantidade máxima de ricochetes por projétil")]
    public int bounceCount = 0;
    
    [Tooltip("Chance percentual de disparar projéteis adicionais (0-100)")]
    [Range(0f, 100f)]
    public float multiShotChance = 0f;
    
    [Tooltip("Ângulo de dispersão dos disparos. 0 = preciso, valores maiores = mais spread")]
    public float spread = 0f;
    
    [Tooltip("Percentual de slow aplicado aos inimigos ao atacar (0-1)")]
    public float slowOnHit = 0f;

    /// <summary>
    /// Modifica um atributo pelo nome.
    /// </summary>
    public void ModifyAttribute(string attributeName, float value, bool isMultiplier = false)
    {
        switch (attributeName.ToLower())
        {
            case "basedamagemultiplier":
            case "forca":
            case "strength":
            case "dano":
            case "damage":
                baseDamageMultiplier = isMultiplier ? baseDamageMultiplier * value : baseDamageMultiplier + value;
                break;
            case "attackspeedmelee":
                // Cap de balanceamento: no máximo 1.6x (+60% de velocidade de ataque)
                attackSpeedMelee = Mathf.Clamp(isMultiplier ? attackSpeedMelee * value : attackSpeedMelee + value, 0.2f, 1.60f);
                break;
            case "critchance":
                // Cap de balanceamento: no máximo 50% de chance crítica
                critChance = Mathf.Clamp(isMultiplier ? critChance * value : critChance + value, 0f, 50.0f);
                break;
            case "critmultiplier":
                critMultiplier = isMultiplier ? critMultiplier * value : critMultiplier + value;
                break;
            case "weaponrangemelee":
                weaponRangeMelee = isMultiplier ? weaponRangeMelee * value : weaponRangeMelee + value;
                break;
            case "weaponrangeprojectile":
                weaponRangeProjectile = isMultiplier ? weaponRangeProjectile * value : weaponRangeProjectile + value;
                break;
            case "knockback":
                knockback = isMultiplier ? knockback * value : knockback + value;
                break;
            case "piercing":
                piercing = Mathf.Max(0, isMultiplier ? Mathf.RoundToInt(piercing * value) : piercing + Mathf.RoundToInt(value));
                break;
            case "bouncechance":
                bounceChance = Mathf.Clamp(isMultiplier ? bounceChance * value : bounceChance + value, 0f, 100f);
                break;
            case "bouncecount":
                bounceCount = Mathf.Max(0, isMultiplier ? Mathf.RoundToInt(bounceCount * value) : bounceCount + Mathf.RoundToInt(value));
                break;
            case "multishotchance":
                multiShotChance = Mathf.Clamp(isMultiplier ? multiShotChance * value : multiShotChance + value, 0f, 100f);
                break;
            case "spread":
                spread = isMultiplier ? spread * value : spread + value;
                break;
            case "slowonhit":
                slowOnHit = isMultiplier ? slowOnHit * value : slowOnHit + value;
                break;
            default:
                Debug.LogWarning($"PlayerAttributesOffensive: Atributo '{attributeName}' não encontrado!");
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
            case "basedamagemultiplier":
            case "forca":
            case "strength":
            case "dano":
            case "damage":
                return baseDamageMultiplier;
            case "attackspeedmelee": return attackSpeedMelee;
            case "critchance": return critChance;
            case "critmultiplier": return critMultiplier;
            case "weaponrangemelee": return weaponRangeMelee;
            case "weaponrangeprojectile": return weaponRangeProjectile;
            case "knockback": return knockback;
            case "piercing": return piercing;
            case "bouncechance": return bounceChance;
            case "bouncecount": return bounceCount;
            case "multishotchance": return multiShotChance;
            case "spread": return spread;
            case "slowonhit": return slowOnHit;
            default:
                Debug.LogWarning($"PlayerAttributesOffensive: Atributo '{attributeName}' não encontrado!");
                return 0f;
        }
    }
    
    /// <summary>
    /// Reseta todos os atributos para valores padrão.
    /// </summary>
    public void ResetToDefaults()
    {
        baseDamageMultiplier = 1.0f;
        attackSpeedMelee = 1.0f;
        critChance = 5.0f;
        critMultiplier = 1.5f;
        weaponRangeMelee = 1.0f;
        weaponRangeProjectile = 10.0f;
        knockback = 1.0f;
        piercing = 0;
        bounceChance = 0f;
        bounceCount = 0;
        multiShotChance = 0f;
        spread = 0f;
        slowOnHit = 0f;
        
        Debug.Log("PlayerAttributesOffensive: Atributos ofensivos resetados.");
    }
}
