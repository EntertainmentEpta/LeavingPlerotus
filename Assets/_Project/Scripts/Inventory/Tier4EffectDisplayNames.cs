public static class Tier4EffectDisplayNames
{
    public static string GetName(Tier4EffectType effectType)
    {
        switch (effectType)
        {
            case Tier4EffectType.Cristalus_T4Effect:
                return "Regeneração de escudo sem tempo de recarga";
            case Tier4EffectType.Totem_T4Effect:
                return "Escudo instantâneo ao usar dash";
            case Tier4EffectType.MagicCatalyst_T4Effect:
                return "cura vida ao completar uma sala";
            case Tier4EffectType.CrystalDragon_T4Effect:
                return "dano refletido atordoa o inimigo";
            case Tier4EffectType.ShardSwarm_T4Effect:
                return "Ao acertar, solta cristais teleguiados";
            case Tier4EffectType.Sentinel_T4Effect:
                return "Ao causar dano, rouba vida com base no dano causado";
            case Tier4EffectType.Bismutado_T4Effect:
                return "Seus ataques causam dano extra em área.";
            case Tier4EffectType.Tuner_T4Effect:
                return "Recebe efeito de marca em inimigos aleatórios";
            default:
                return effectType.ToString();
        }
    }
}
