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
            mapPosition = new Vector2(450f, 0f),
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
            mapPosition = new Vector2(318f, 318f),
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
            mapPosition = new Vector2(0f, 450f),
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
            mapPosition = new Vector2(-318f, 318f),
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
            mapPosition = new Vector2(-450f, 0f),
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
            mapPosition = new Vector2(-318f, -318f),
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
            mapPosition = new Vector2(0f, -450f),
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
            mapPosition = new Vector2(318f, -318f),
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
