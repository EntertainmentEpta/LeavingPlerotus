using UnityEngine;

/// <summary>
/// Script de diagnóstico para verificar se PlayerAttributes está funcionando.
/// Adicione ao Player e pressione F12 para ver relatório completo.
/// </summary>
public class AttributeDiagnostic : MonoBehaviour
{
    private PlayerAttributesOffensive offensiveAttributes;
    private PlayerAttributesDefensive defensiveAttributes;
    private PlayerHealth playerHealth;
    private PrimaryAttackKnife attackScript;
    
    void Start()
    {
        offensiveAttributes = GetComponentInChildren<PlayerAttributesOffensive>();
        defensiveAttributes = GetComponent<PlayerAttributesDefensive>();
        playerHealth = GetComponent<PlayerHealth>();
        attackScript = GetComponentInChildren<PrimaryAttackKnife>();
        
        Debug.Log("=== ATTRIBUTE DIAGNOSTIC START ===");
        
        if (offensiveAttributes == null)
        {
            Debug.LogError("❌ CRÍTICO: PlayerAttributesOffensive NÃO ENCONTRADO!");
            Debug.LogError("   SOLUÇÃO: Adicione ao GameObject 'astronaut' (filho)");
        }
        else
        {
            Debug.Log("✅ PlayerAttributesOffensive encontrado no filho!");
        }
        
        if (defensiveAttributes == null)
        {
            Debug.LogError("❌ CRÍTICO: PlayerAttributesDefensive NÃO ENCONTRADO!");
            Debug.LogError("   SOLUÇÃO: Adicione ao GameObject 'Player' (pai)");
        }
        else
        {
            Debug.Log("✅ PlayerAttributesDefensive encontrado!");
        }
        
        if (playerHealth == null)
            Debug.LogWarning("⚠️ PlayerHealth não encontrado!");
        else
            Debug.Log("✅ PlayerHealth encontrado!");
            
        if (attackScript == null)
            Debug.LogWarning("⚠️ PrimaryAttackKnife não encontrado!");
        else
            Debug.Log("✅ PrimaryAttackKnife encontrado!");
            
        Debug.Log("=== DIAGNOSTIC END ===");
        Debug.Log("Pressione F12 durante o jogo para ver relatório completo");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            GenerateReport();
        }
    }
    
    void GenerateReport()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        Debug.Log("╔════════════════════════════════════════════════╗");
        Debug.Log("║     RELATÓRIO DE DIAGNÓSTICO (F12)             ║");
        Debug.Log("╠════════════════════════════════════════════════╣");
        
        if (offensiveAttributes != null)
        {
            Debug.Log("║ ATRIBUTOS OFENSIVOS (astronaut):               ║");
            Debug.Log($"║  attackSpeedMelee: {offensiveAttributes.attackSpeedMelee}");
            Debug.Log($"║  critChance: {offensiveAttributes.critChance}%");
            Debug.Log($"║  critMultiplier: {offensiveAttributes.critMultiplier}x");
            Debug.Log($"║  weaponRangeMelee: {offensiveAttributes.weaponRangeMelee}x");
            Debug.Log($"║  knockback: {offensiveAttributes.knockback}");
            Debug.Log($"║  piercing: {offensiveAttributes.piercing}");
        }
        else
        {
            Debug.Log("║ ATRIBUTOS OFENSIVOS: ❌ NÃO ENCONTRADO         ║");
        }
        
        Debug.Log("╠════════════════════════════════════════════════╣");
        
        if (playerHealth != null)
        {
            Debug.Log("║ STATUS VITAIS (PlayerHealth):                  ║");
            Debug.Log($"║  maxHealth: {playerHealth.maxHealth} (Atual: {playerHealth.currentHealth})");
            Debug.Log($"║  maxArmor: {playerHealth.maxArmor} (Atual: {playerHealth.CurrentArmor})");
            Debug.Log("╠════════════════════════════════════════════════╣");
        }

        if (defensiveAttributes != null)
        {
            Debug.Log("║ ATRIBUTOS DEFENSIVOS (Player):                 ║");
            Debug.Log($"║  armorRegen: {defensiveAttributes.armorRegen}x");
            Debug.Log($"║  dodgeChance: {defensiveAttributes.dodgeChance}%");
            Debug.Log($"║  damageNegation: {defensiveAttributes.damageNegation}%");
            Debug.Log($"║  thorns: {defensiveAttributes.thorns}");
            Debug.Log("╠════════════════════════════════════════════════╣");
            Debug.Log("║ ATRIBUTOS DE MOBILIDADE (Player):              ║");
            Debug.Log($"║  speedMultiplier: {defensiveAttributes.speedMultiplier}x");
            Debug.Log($"║  dashCooldownMultiplier: {defensiveAttributes.dashCooldownMultiplier}x");
            Debug.Log($"║  dashCounts: {defensiveAttributes.dashCounts}");
            Debug.Log($"║  dashInvulnerability: {defensiveAttributes.dashInvulnerability}s");
        }
        else
        {
            Debug.Log("║ ATRIBUTOS DEFENSIVOS: ❌ NÃO ENCONTRADO        ║");
        }
        
        Debug.Log("╚════════════════════════════════════════════════╝");
    }
}
