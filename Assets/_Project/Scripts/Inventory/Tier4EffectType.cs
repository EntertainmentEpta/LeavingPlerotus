/// <summary>
/// Tipos de efeitos especiais concedidos por itens Tier 4 (Lendários).
/// Cada valor corresponde a um comportamento único que modifica o gameplay do player.
/// 
/// Novos efeitos T4 devem ser adicionados aqui conforme novos itens lendários forem criados.
/// </summary>
public enum Tier4EffectType
{
    None = 0,               // Sem efeito especial (padrão para itens que não são T4)
    ExplosiveDash = 1,      // SharpItem4: dash mais longo + explosão AoE no final com dano e knockback
    Poison = 2,             // Peixe T4: ataques do player aplicam Poison (DoT) nos inimigos atingidos
    HomingProjectiles = 3   // Olho do Vigia: projéteis perseguem inimigos próximos
    // Futuros efeitos:
    // VampiricStrike, ChainLightning, FrostNova, etc.
}
