/// <summary>
/// Tipos de efeitos especiais concedidos por itens Tier 4 (Lendários).
/// Cada valor corresponde a um comportamento único que modifica o gameplay do player.
/// 
/// Novos efeitos T4 devem ser adicionados aqui conforme novos itens lendários forem criados.
/// </summary>
public enum Tier4EffectType
{
    None,           // Sem efeito especial (padrão para itens que não são T4)
    ExplosiveDash   // SharpItem4: dash mais longo + explosão AoE no final com dano e knockback
    // Futuros efeitos:
    // VampiricStrike, ChainLightning, FrostNova, etc.
}
