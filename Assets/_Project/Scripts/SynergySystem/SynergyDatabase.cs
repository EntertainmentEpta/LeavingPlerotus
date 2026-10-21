using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Definição completa de uma Receita Oficial de Sinergia.
/// </summary>
[System.Serializable]
public class SynergyRecipe
{
    public string id;
    public string name;
    public string description;
    public string discoveryHint;
    public string recipeIngredientsText;
    public float essenceDiscount;
    public List<string[]> requirementSlots = new List<string[]>();
    public List<StatModifier> modifiers = new List<StatModifier>();
    public Vector2 mapPosition;
    public Color themeColor;

    /// <summary>
    /// Verifica se a seleção de IDs satisfaz todos os slots requeridos desta receita.
    /// </summary>
    public bool IsSatisfiedBy(IReadOnlyList<string> selectedIds, out List<string> matchedIds)
    {
        matchedIds = new List<string>();
        if (selectedIds == null || selectedIds.Count < requirementSlots.Count)
            return false;

        // Se for A Ascensão (Modo Deus): precisa de 4 itens T4 Lendários
        if (id == "combo_ascensao_modo_deus")
        {
            int legendaryCount = 0;
            foreach (var id in selectedIds)
            {
                ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(id) : null;
                if (data != null && data.tier == ItemTier.Legendary)
                {
                    legendaryCount++;
                    matchedIds.Add(id);
                }
            }
            return legendaryCount >= 4;
        }

        List<string> remainingSelected = new List<string>(selectedIds);

        foreach (var acceptedAliases in requirementSlots)
        {
            string foundMatch = null;
            foreach (var sel in remainingSelected)
            {
                if (MatchesAlias(sel, acceptedAliases))
                {
                    foundMatch = sel;
                    break;
                }
            }

            if (foundMatch != null)
            {
                matchedIds.Add(foundMatch);
                remainingSelected.Remove(foundMatch);
            }
            else
            {
                matchedIds.Clear();
                return false;
            }
        }

        return true;
    }

    private bool MatchesAlias(string selectedId, string[] aliases)
    {
        if (string.IsNullOrEmpty(selectedId)) return false;
        string selLower = selectedId.ToLowerInvariant();

        foreach (var alias in aliases)
        {
            if (string.IsNullOrEmpty(alias)) continue;
            string aLower = alias.ToLowerInvariant();

            if (selLower == aLower || selLower.Contains(aLower) || aLower.Contains(selLower))
                return true;
        }

        // Tenta também pelo ItemData (enemySource e tier)
        if (ItemDatabase.Instance != null)
        {
            ItemData data = ItemDatabase.Instance.GetItemData(selectedId);
            if (data != null)
            {
                foreach (var alias in aliases)
                {
                    string aLower = alias.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(data.enemySource) && aLower.Contains(data.enemySource.ToLowerInvariant()))
                    {
                        // Verifica se o tier bate (ex: t1, t2, t3, t4)
                        if (aLower.EndsWith("_t1") && data.tier == ItemTier.Common) return true;
                        if (aLower.EndsWith("_t2") && data.tier == ItemTier.Uncommon) return true;
                        if (aLower.EndsWith("_t3") && data.tier == ItemTier.Rare) return true;
                        if (aLower.EndsWith("_t4") && data.tier == ItemTier.Legendary) return true;
                    }
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Repositório Mestre e Central de todas as 10 Sinergias Oficiais de Plerotus.
/// Fonte única da verdade para Combos, Teia de Sinergias, Mapa Cósmico e Inventário.
/// </summary>
public static class SynergyDatabase
{
    private static List<SynergyRecipe> _allRecipes;

    public static IReadOnlyList<SynergyRecipe> AllRecipes
    {
        get
        {
            if (_allRecipes == null) Initialize();
            return _allRecipes;
        }
    }

    public static SynergyRecipe GetRecipe(string id)
    {
        if (_allRecipes == null) Initialize();
        return _allRecipes.Find(r => r.id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Avalia uma lista de IDs e retorna todas as receitas ativadas e os itens consumidos nelas.
    /// </summary>
    public static List<SynergyRecipe> EvaluateSynergies(IReadOnlyList<string> selectedIds, out HashSet<string> usedIds)
    {
        usedIds = new HashSet<string>();
        List<SynergyRecipe> triggered = new List<SynergyRecipe>();
        if (selectedIds == null || selectedIds.Count == 0) return triggered;

        if (_allRecipes == null) Initialize();

        foreach (var recipe in _allRecipes)
        {
            if (recipe.IsSatisfiedBy(selectedIds, out List<string> matched))
            {
                triggered.Add(recipe);
                foreach (var m in matched) usedIds.Add(m);
            }
        }

        return triggered;
    }

    /// <summary>
    /// Retorna todos os IDs de itens presentes no inventário que compõem pelo menos uma sinergia válida com outro item do inventário.
    /// </summary>
    public static HashSet<string> GetSynergisticItemsInList(IEnumerable<string> inventoryItemIds, out Dictionary<string, string> itemToRecipeName)
    {
        HashSet<string> synergisticSet = new HashSet<string>();
        itemToRecipeName = new Dictionary<string, string>();
        if (inventoryItemIds == null) return synergisticSet;

        List<string> itemsList = new List<string>(inventoryItemIds);
        if (itemsList.Count < 2) return synergisticSet;

        if (_allRecipes == null) Initialize();

        foreach (var recipe in _allRecipes)
        {
            if (recipe.IsSatisfiedBy(itemsList, out List<string> matched))
            {
                foreach (var id in matched)
                {
                    synergisticSet.Add(id);
                    if (!itemToRecipeName.ContainsKey(id))
                        itemToRecipeName[id] = recipe.name;
                }
            }
        }

        return synergisticSet;
    }

    /// <summary>
    /// Inicializa as 10 receitas oficiais com balanceamento matemático, posicionamento de mapa e bônus.
    /// </summary>
    private static void Initialize()
    {
        _allRecipes = new List<SynergyRecipe>();

        // 1. Chuva de Lâminas (Goblin T1 + SharpBlur T1)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_chuva_de_laminas",
            name = "Chuva de Lâminas",
            description = "Dispara projéteis cortantes adicionais a cada finalização de combo melee.",
            discoveryHint = "Experimente fundir itens do Goblin e do SharpBlur na Câmara de Infusão!",
            recipeIngredientsText = "• Goblin T1 (Multi-Tiro / Moeda)\n• SharpBlur T1 (Dano Base / Lasca)",
            essenceDiscount = 0.15f,
            themeColor = new Color(1f, 0.45f, 0.15f), // Laranja Solar
            mapPosition = new Vector2(520f, 0f),
            requirementSlots = new List<string[]>
            {
                new[] { "goblin_coin_t1", "goblin_ear_t1", "goblin_t1" },
                new[] { "01sb", "sharp_shard_t1", "sharpblur_t1" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "MultiShotChance", baseValue = 0.20f, multiplierValue = 0f },
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.15f }
            }
        });

        // 2. Parede Intransponível (Golem T1 + Sentinela T2/T4)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_parede_intransponivel",
            name = "Parede Intransponível",
            description = "Ao quebrar a Armadura, reflete 100% do dano absorvido em uma explosão de espinhos sísmicos.",
            discoveryHint = "Combine a couraça rochosa do Golem com a blindagem colossal do Sentinela!",
            recipeIngredientsText = "• Golem T1 (Armor Máxima / Lasca)\n• Sentinela T2/T4 (Placagem / Pata Sentinela)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.35f, 0.65f, 1f), // Azul Titânio
            mapPosition = new Vector2(450f, 260f),
            requirementSlots = new List<string[]>
            {
                new[] { "golem_chip_t1", "01go", "golem_t1" },
                new[] { "sentinel_leg_t4", "bismutado_placa_t2", "sentinela_t2", "sentinela_t4" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "MaxArmor", baseValue = 30f, multiplierValue = 0f },
                new StatModifier { statName = "DamageNegation", baseValue = 0.10f, multiplierValue = 0f },
                new StatModifier { statName = "Thorns", baseValue = 25f, multiplierValue = 0f }
            }
        });

        // 3. Corte Congelante (Cristalus T2 + SharpBlur T3)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_corte_congelante",
            name = "Corte Congelante",
            description = "Inimigos sob efeito de Lentidão sofrem +40% de dano crítico extra.",
            discoveryHint = "Una o pó desacelerador do Cristalus ao bisturi afiado do SharpBlur!",
            recipeIngredientsText = "• Cristalus T2 (Pó Entorpecente / Slow)\n• SharpBlur T3 (Bisturi de Ametista / Crit)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.2f, 0.9f, 1f), // Ciano Glacial
            mapPosition = new Vector2(260f, 450f),
            requirementSlots = new List<string[]>
            {
                new[] { "02cl", "cristalus_t2" },
                new[] { "03sb", "sharp_prism_t3", "sharpblur_t3" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "SlowOnHit", baseValue = 0.30f, multiplierValue = 0f },
                new StatModifier { statName = "CritMultiplier", baseValue = 0f, multiplierValue = 0.40f }
            }
        });

        // 4. Assassino Fantasma (Aranha T2 + Pedra Mágica T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_assassino_fantasma",
            name = "Assassino Fantasma",
            description = "Esquivar de um golpe reseta instantaneamente 1 carga de Dash e concede +20% de Velocidade de Ataque.",
            discoveryHint = "Sintetize a glândula elástica da Aranha com o cristal de esquiva da Pedra Mágica!",
            recipeIngredientsText = "• Aranha T2 (Glândula Elástica / Dash CD)\n• Pedra Mágica T2 (Cristal Ilusório / Esquiva)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.75f, 0.35f, 1f), // Roxo Espectral
            mapPosition = new Vector2(0f, 520f),
            requirementSlots = new List<string[]>
            {
                new[] { "spider_silk_t2", "aranha_t2" },
                new[] { "magic_rune_t2", "pedra_magica_t2" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "DodgeChance", baseValue = 0.10f, multiplierValue = 0f },
                new StatModifier { statName = "DashCooldownMultiplier", baseValue = 0f, multiplierValue = -0.20f },
                new StatModifier { statName = "AttackSpeedMelee", baseValue = 0f, multiplierValue = 0.20f }
            }
        });

        // 5. Domínio de Espaço (Totem T2 + Watcher T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_dominio_de_espaco",
            name = "Domínio de Espaço",
            description = "O alcance dos seus ataques melee aumenta em +30% e empurra inimigos violentamente contra paredes.",
            discoveryHint = "Fundir o pilar repulsor do Totem com as lentes ópticas do Vigia gera controle supremo!",
            recipeIngredientsText = "• Totem T2 (Pilar Repulsor / Knockback)\n• Watcher T2 (Prisma Ocular / Alcance Melee)",
            essenceDiscount = 0.15f,
            themeColor = new Color(0.3f, 0.5f, 0.95f), // Azul Índigo Vórtice
            mapPosition = new Vector2(-260f, 450f),
            requirementSlots = new List<string[]>
            {
                new[] { "totem_carved_t2", "totem_t2" },
                new[] { "02cw", "watcher_eye_t2", "watcher_carcaça_t2", "watcher_t2" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "WeaponRangeMelee", baseValue = 0f, multiplierValue = 0.30f },
                new StatModifier { statName = "Knockback", baseValue = 0f, multiplierValue = 0.40f }
            }
        });

        // 6. Tempestade Focada (Tuner T2 + Shard Swarm T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_tempestade_focada",
            name = "Tempestade Focada",
            description = "Projéteis que quicam em alvos garantem acerto crítico no próximo ricochete.",
            discoveryHint = "Una o ressonador do Sintonizador com a carambola cortante da Istar!",
            recipeIngredientsText = "• Tuner T2 (Ressonador Acústico / Quicar)\n• Shard Swarm T2 (Carambola / Dano Crítico)",
            essenceDiscount = 0.20f,
            themeColor = new Color(1f, 0.9f, 0.2f), // Amarelo Relâmpago
            mapPosition = new Vector2(-450f, 260f),
            requirementSlots = new List<string[]>
            {
                new[] { "tuner_lens_t2", "tuner_t2" },
                new[] { "shard_resonant_t2", "swarm_t2", "star_t2" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "BounceCount", baseValue = 1f, multiplierValue = 0f },
                new StatModifier { statName = "CritMultiplier", baseValue = 0f, multiplierValue = 0.35f },
                new StatModifier { statName = "CritChance", baseValue = 0.10f, multiplierValue = 0f }
            }
        });

        // 7. Aura Espinhosa (Peixe T1 + Cristalus T1)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_aura_espinhosa",
            name = "Aura Espinhosa",
            description = "Enquanto sua Armadura estiver cheia, emite uma aura de dano contínuo de espinhos aos inimigos próximos.",
            discoveryHint = "Experimente fundir a escama espinhada do Peixe com a membrana cristalina do Cristalus!",
            recipeIngredientsText = "• Peixe T1 (Espinho Cristalino / Retaliação)\n• Cristalus T1 (Membrana Cristalina / Regen)",
            essenceDiscount = 0.15f,
            themeColor = new Color(0.2f, 0.95f, 0.45f), // Verde Esmeralda
            mapPosition = new Vector2(-520f, 0f),
            requirementSlots = new List<string[]>
            {
                new[] { "01px", "peixe_escama_t1", "peixe_t1" },
                new[] { "01cl", "cristalus_t1" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "Thorns", baseValue = 30f, multiplierValue = 0f },
                new StatModifier { statName = "ArmorRegen", baseValue = 1.5f, multiplierValue = 0f }
            }
        });

        // 8. Fuga Explosiva (Totem T1 + Goblin T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_fuga_explosiva",
            name = "Fuga Explosiva",
            description = "Cada carga de Dash utilizada espalha 3 estilhaços explosivos pelo chão.",
            discoveryHint = "Conecte as insígnias de dash do Totem ao pavio rápido explosivo do Goblin!",
            recipeIngredientsText = "• Totem T1 (Insígnia dos Crânios / +1 Dash)\n• Goblin T2 (Pavio Rápido / Dispersão)",
            essenceDiscount = 0.20f,
            themeColor = new Color(1f, 0.25f, 0.3f), // Vermelho Detonação
            mapPosition = new Vector2(-450f, -260f),
            requirementSlots = new List<string[]>
            {
                new[] { "totem_stone_t1", "totem_t1" },
                new[] { "goblin_braço_t2", "goblin_trinket_t2", "goblin_t2", "goblin_pouch_t2" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "DashCounts", baseValue = 1f, multiplierValue = 0f },
                new StatModifier { statName = "Spread", baseValue = 0f, multiplierValue = 0.25f }
            }
        });

        // 9. BERSERKER IMORTAL (Combo 4 Partes)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_berserker_imortal",
            name = "Berserker Imortal",
            description = "Quando o HP cai <30%, ativa modo Berserk: imune a stuns, +30% Dano, +30% Vel Ataque e +50 HP.",
            discoveryHint = "Sinergia ancestral de 4 partes! Requer Golem T4 + Bismutado T3 + Istar T1 + Aranha T1.",
            recipeIngredientsText = "• Golem T4 (Coração de Granito)\n• Bismutado T3 (Glândula / Cutelo)\n• Istar T1 (Fragmento Estelar)\n• Aranha T1 (Pata Ágil)",
            essenceDiscount = 0.35f,
            themeColor = new Color(1f, 0.15f, 0.45f), // Rubi Sangue
            mapPosition = new Vector2(-140f, 0f),
            requirementSlots = new List<string[]>
            {
                new[] { "golem_heart_t4", "04go", "golem_t4" },
                new[] { "bismutado_glândula_t3", "bismutado_t3" },
                new[] { "shard_splinter_t1", "swarm_t1", "star_t1" },
                new[] { "spider_leg_t1", "aranha_t1" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.30f },
                new StatModifier { statName = "AttackSpeedMelee", baseValue = 0f, multiplierValue = 0.30f },
                new StatModifier { statName = "MaxHealth", baseValue = 50f, multiplierValue = 0f }
            }
        });

        // 11. Veneno Corrosivo (Peixe T3 + Aranha T3)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_veneno_corrosivo",
            name = "Veneno Corrosivo",
            description = "Seus ataques aplicam toxina cáustica que dissolve 30% da armadura dos inimigos e causa dano contínuo.",
            discoveryHint = "Funda a barbatana cortante do Peixe com a peçonha quelicerada da Aranha!",
            recipeIngredientsText = "• Peixe T3 (Barbatana Venenosa)\n• Aranha T3 (Presa Cáustica)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.4f, 0.95f, 0.15f), // Verde Ácido
            mapPosition = new Vector2(-260f, -450f),
            requirementSlots = new List<string[]>
            {
                new[] { "Peixe_barbatana_t3", "peixe_t3", "03px" },
                new[] { "spider_fang_t3", "spider_t3", "03sp", "aranha_t3" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "Thorns", baseValue = 15f, multiplierValue = 0f },
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.15f },
                new StatModifier { statName = "SlowOnHit", baseValue = 0.20f, multiplierValue = 0f }
            }
        });

        // 12. Baluarte de Obsidiana (Golem T2 + Pedra Mágica T1)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_baluarte_obsidiana",
            name = "Baluarte de Obsidiana",
            description = "Reforça o chassi molecular: concede +40 de Armadura Máxima e 15% de resistência passiva a dano.",
            discoveryHint = "Una a placa de rocha do Golem à poeira mineral da Pedra Mágica!",
            recipeIngredientsText = "• Golem T2 (Placa de Rocha Reforçada)\n• Pedra Mágica T1 (Pó Arcano de Proteção)",
            essenceDiscount = 0.15f,
            themeColor = new Color(0.35f, 0.45f, 0.90f), // Azul Titânio Escuro
            mapPosition = new Vector2(0f, -520f),
            requirementSlots = new List<string[]>
            {
                new[] { "golem_plate_t2", "02go", "golem_t2" },
                new[] { "magic_dust_t1", "01ms", "pedra_magica_t1" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "MaxArmor", baseValue = 40f, multiplierValue = 0f },
                new StatModifier { statName = "DamageNegation", baseValue = 0.15f, multiplierValue = 0f }
            }
        });

        // 13. Impacto Sísmico (Golem T3 + Totem T3)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_impacto_sismico",
            name = "Impacto Sísmico",
            description = "O Ultimate e golpes pesados geram réplicas sísmicas que arremessam e atordoam inimigos menores.",
            discoveryHint = "Conecte o núcleo de alta densidade do Golem ao monólito milenar do Totem!",
            recipeIngredientsText = "• Golem T3 (Núcleo de Alta Densidade)\n• Totem T3 (Monólito Sísmico Ancestral)",
            essenceDiscount = 0.25f,
            themeColor = new Color(0.90f, 0.40f, 0.15f), // Terracota Forjado
            mapPosition = new Vector2(260f, -450f),
            requirementSlots = new List<string[]>
            {
                new[] { "golem_core_t3", "03go", "golem_t3" },
                new[] { "totem_ancient_t3", "totem_t3" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "Knockback", baseValue = 0f, multiplierValue = 0.50f },
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.20f }
            }
        });

        // 14. Mira Focada do Vigia (Watcher T1 + SharpBlur T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_mira_focada_vigia",
            name = "Mira Focada do Vigia",
            description = "Sincronia ocular perfeita: concede +15% de Chance Crítica e +20% de alcance para armas melee e tiros.",
            discoveryHint = "Sintetize a lente focal do Vigia com o prisma cortante do SharpBlur!",
            recipeIngredientsText = "• Watcher T1 (Lente Óptica Focal)\n• SharpBlur T2 (Prisma de Refração / Crit)",
            essenceDiscount = 0.15f,
            themeColor = new Color(1f, 0.80f, 0.10f), // Âmbar Dourado
            mapPosition = new Vector2(450f, -260f),
            requirementSlots = new List<string[]>
            {
                new[] { "watcher_lens_t1", "01cw", "watcher_t1" },
                new[] { "SharpDrop2", "02sb", "sharp_shard_t2", "sharpblur_t2" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "CritChance", baseValue = 0.15f, multiplierValue = 0f },
                new StatModifier { statName = "WeaponRangeMelee", baseValue = 0f, multiplierValue = 0.20f }
            }
        });

        // 15. Eco Hipnótico (Tuner T1 + Cristalus T3)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_eco_hipnotico",
            name = "Eco Hipnótico",
            description = "Acertos contínuos ressoam uma frequência que atrasa o tempo de ataque dos inimigos em 25%.",
            discoveryHint = "Fundir o diapasão do Sintonizador com o cristal harmônico do Cristalus gera desaceleração sônica!",
            recipeIngredientsText = "• Tuner T1 (Diapasão de Cristal)\n• Cristalus T3 (Prisma Ressonante)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.15f, 0.90f, 0.95f), // Ciano Sônico
            mapPosition = new Vector2(290f, 0f),
            requirementSlots = new List<string[]>
            {
                new[] { "tuner_shard_t1", "tuner_t1" },
                new[] { "CristalusDrop3", "03cl", "cristalus_t3" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "SlowOnHit", baseValue = 0.25f, multiplierValue = 0f },
                new StatModifier { statName = "AttackSpeedMelee", baseValue = 0f, multiplierValue = 0.15f }
            }
        });

        // 16. Toxina Ardente (Peixe T2 + Goblin T3)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_toxina_ardente",
            name = "Toxina Ardente",
            description = "Golpes liberam uma névoa cáustica que inflama inimigos próximos e aumenta a velocidade do jogador em +15%.",
            discoveryHint = "Experimente combinar a guelra aquática do Peixe com o amuleto ígneo do Goblin!",
            recipeIngredientsText = "• Peixe T2 (Guelra Bioluminescente)\n• Goblin T3 (Amuleto de Fólio Explosivo)",
            essenceDiscount = 0.20f,
            themeColor = new Color(1.0f, 0.35f, 0.20f), // Coral em Chamas
            mapPosition = new Vector2(180f, 227f),
            requirementSlots = new List<string[]>
            {
                new[] { "Peixe_guelra_t2", "peixe_t2", "02px" },
                new[] { "goblin_amulet_t3", "goblin_t3" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "SpeedMultiplier", baseValue = 0f, multiplierValue = 0.15f },
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.15f }
            }
        });

        // 17. Cintilação Espectral (Pedra Mágica T3 + Aranha T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_cintilacao_espectral",
            name = "Cintilação Espectral",
            description = "O Dash torna-se etéreo, atravessando alvos, reduzindo a recarga da esquiva em 25% e concedendo +12% de Evasão.",
            discoveryHint = "Una o cristal etéreo da Pedra Mágica aos fios tenazes da Aranha!",
            recipeIngredientsText = "• Pedra Mágica T3 (Cristal de Fenda Etérea)\n• Aranha T2 (Glândula de Seda Flexível)",
            essenceDiscount = 0.25f,
            themeColor = new Color(0.85f, 0.25f, 1.0f), // Violeta Astral
            mapPosition = new Vector2(-64f, 283f),
            requirementSlots = new List<string[]>
            {
                new[] { "magic_essence_t3", "03ms", "pedra_magica_t3" },
                new[] { "spider_silk_t2", "aranha_t2", "02sp" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "DashCooldownMultiplier", baseValue = 0f, multiplierValue = -0.25f },
                new StatModifier { statName = "DodgeChance", baseValue = 0.12f, multiplierValue = 0f }
            }
        });

        // 18. Frenesi de Estilhaços (Shard Swarm T3 + SharpBlur T1)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_frenesi_estilhacos",
            name = "Frenesi de Estilhaços",
            description = "Acertos críticos liberam uma erupção de micro-cristais afiados, concedendo +35% de Dano Crítico e +20% Vel de Ataque.",
            discoveryHint = "Combine o estilhaço carambola da Istar com a lâmina lascada do SharpBlur!",
            recipeIngredientsText = "• Shard Swarm T3 (Prisma Fraturante de Istar)\n• SharpBlur T1 (Lasca Cortante Primordial)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.75f, 0.40f, 1.0f), // Diamante Ametista
            mapPosition = new Vector2(-261f, 126f),
            requirementSlots = new List<string[]>
            {
                new[] { "shard_refract_t3", "swarm_t3", "star_t3" },
                new[] { "SharpDrop1", "01sb", "sharp_shard_t1", "sharpblur_t1" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "CritMultiplier", baseValue = 0f, multiplierValue = 0.35f },
                new StatModifier { statName = "AttackSpeedMelee", baseValue = 0f, multiplierValue = 0.20f }
            }
        });

        // 19. Fortaleza de Titânio (Sentinela T2 + Totem T2)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_fortaleza_titanio",
            name = "Fortaleza de Titânio",
            description = "Blindagem impenetrável: +30 de Armadura Máxima e regeneração passiva de 2.0 pontos de armadura por segundo.",
            discoveryHint = "Sintetize a couraça sentinela com a estela rúnica do Totem!",
            recipeIngredientsText = "• Sentinela T2 (Placa Bismutada)\n• Totem T2 (Totem Esculpido Protetor)",
            essenceDiscount = 0.20f,
            themeColor = new Color(0.45f, 0.75f, 0.95f), // Aço Polido
            mapPosition = new Vector2(-261f, -126f),
            requirementSlots = new List<string[]>
            {
                new[] { "Bismutado_PlacaT2", "bismutado_placa_t2", "sentinela_t2" },
                new[] { "totem_carved_t2", "totem_t2" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "MaxArmor", baseValue = 30f, multiplierValue = 0f },
                new StatModifier { statName = "ArmorRegen", baseValue = 2.0f, multiplierValue = 0f }
            }
        });

        // 20. Cataclismo Arcano (Bismutado T2 + Tuner T3)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_cataclismo_arcano",
            name = "Cataclismo Arcano",
            description = "Detonações ressoam através das galerias: aumenta a área de dispersão em +30% e amplifica todo dano em +25%.",
            discoveryHint = "Funda os rotores mecânicos do Bismutado com os prismas sônicos do Sintonizador!",
            recipeIngredientsText = "• Bismutado T2 (Placa de Impacto Dinâmico)\n• Tuner T3 (Prisma Acústico Harmonizado)",
            essenceDiscount = 0.25f,
            themeColor = new Color(0.95f, 0.20f, 0.70f), // Magenta Cósmico
            mapPosition = new Vector2(-64f, -283f),
            requirementSlots = new List<string[]>
            {
                new[] { "Bismutado_PlacaT2", "bismutado_t2" },
                new[] { "tuner_prism_t3", "tuner_t3" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "Spread", baseValue = 0f, multiplierValue = 0.30f },
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.25f }
            }
        });

        // 21. Supercondutividade Alquímica (Cristalus T3 + Goblin T4)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_supercondutividade_alquimica",
            name = "Supercondutividade Alquímica",
            description = "Explosões criam zonas de choque eletro-cristalino: +25% Multi-Tiro, +20% Lentidão e +20% Dano Global.",
            discoveryHint = "A fusão suprema da química de bombas goblin com a pureza supercondutora do Cristalus!",
            recipeIngredientsText = "• Cristalus T3 (Prisma Supercondutor)\n• Goblin T4 (Bomba de Fissão Alquímica)",
            essenceDiscount = 0.30f,
            themeColor = new Color(0.25f, 0.95f, 0.85f), // Ciano Eletrostático
            mapPosition = new Vector2(180f, -227f),
            requirementSlots = new List<string[]>
            {
                new[] { "CristalusDrop3", "03cl", "cristalus_t3" },
                new[] { "goblin_bomb_t4", "goblin_t4" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "MultiShotChance", baseValue = 0.25f, multiplierValue = 0f },
                new StatModifier { statName = "SlowOnHit", baseValue = 0.20f, multiplierValue = 0f },
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.20f }
            }
        });
        // 10. A ASCENSÃO: MODO DEUS (Combo dos Lendários T4)
        _allRecipes.Add(new SynergyRecipe
        {
            id = "combo_ascensao_modo_deus",
            name = "A Ascensão: Modo Deus",
            description = "Conquista absoluta. O jogador atinge a simbiose cristalina suprema (+50% Dano, +100 HP, +100 Armadura).",
            discoveryHint = "Requer fundir 4 compostos Lendários T4 simultaneamente na câmara expandida!",
            recipeIngredientsText = "• 4x Itens Lendários T4 Simultâneos",
            essenceDiscount = 0.50f,
            themeColor = new Color(1f, 0.85f, 0.25f), // Ouro Divino
            mapPosition = new Vector2(140f, 0f),
            requirementSlots = new List<string[]>
            {
                new[] { "t4_legendary_any" },
                new[] { "t4_legendary_any" },
                new[] { "t4_legendary_any" },
                new[] { "t4_legendary_any" }
            },
            modifiers = new List<StatModifier>
            {
                new StatModifier { statName = "BaseDamageMultiplier", baseValue = 0f, multiplierValue = 0.50f },
                new StatModifier { statName = "MaxHealth", baseValue = 100f, multiplierValue = 0f },
                new StatModifier { statName = "MaxArmor", baseValue = 100f, multiplierValue = 0f }
            }
        });
    }
}
