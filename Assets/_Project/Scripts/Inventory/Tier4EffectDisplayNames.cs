public static class Tier4EffectDisplayNames
{
    public static string GetName(Tier4EffectType effectType)
    {
        switch (effectType)
        {
            case Tier4EffectType.ExplosiveDash:
                return "Dash Explosivo (maior alcance + explosão em área)";
            case Tier4EffectType.Poison:
                return "Ataques aplicam veneno nos inimigos";
            case Tier4EffectType.HomingProjectiles:
                return "Disparos teleguiados";
            case Tier4EffectType.SharpBlurT4Effect:
                return "Sangramento em acertos críticos (10 de dano por 3s)";
            case Tier4EffectType.Cristalus_T4Effect:
                return "Regeneração de escudo sem tempo de recarga";
            case Tier4EffectType.Totem_T4Effect:
                return "Regenera 20% do escudo ao usar dash";
            case Tier4EffectType.MagicCatalyst_T4Effect:
                return "Regenera 20% da vida ao completar uma sala";
            case Tier4EffectType.CrystalDragon_T4Effect:
                return "Atordoa o inimigo ao refletir dano";
            case Tier4EffectType.ShardSwarm_T4Effect:
                return "Ao acertar, solta cristais teleguiados";
            case Tier4EffectType.Sentinel_T4Effect:
                return "Roubo de vida de 5% com base no dano causado";
            case Tier4EffectType.Bismutado_T4Effect:
                return "Causa 20% do dano em área nos inimigos próximos";
            case Tier4EffectType.Tuner_T4Effect:
                return "Marca inimigos aleatórios (+20% de dano recebido)";
            case Tier4EffectType.Golem_T4Effect:
                return "Esquiva Perfeita atordoa inimigos em área";
            default:
                return effectType.ToString();
        }
    }
}
