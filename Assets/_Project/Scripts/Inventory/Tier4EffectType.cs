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
    HomingProjectiles = 3,  // Olho do Vigia: projéteis perseguem inimigos próximos
    SharpBlurT4Effect = 4,  // SharpBlur: sangramento em acertos críticos
    Cristalus_T4Effect = 5, // Cristalus: regeneração de escudo sem atraso
    Totem_T4Effect = 6,     // Totem: recupera escudo ao usar Dash
    MagicCatalyst_T4Effect = 7, // Pedra mágica: cura vida ao completar uma sala
    CrystalDragon_T4Effect = 8, // Cristal do Dragão: dano refletido atordoa o inimigo
    ShardSwarm_T4Effect = 9,     // Shard Swarm: lança cristais teleguiados ao acertar
    Sentinel_T4Effect = 10,     // Sentinela: rouba vida com base no dano causado
    Bismutado_T4Effect = 11,    // Bismutado: ataques causam dano extra em área
    Tuner_T4Effect = 12         // Tuner: marca inimigos aleatórios
    // Futuros efeitos:
    // VampiricStrike, ChainLightning, FrostNova, etc.
}
