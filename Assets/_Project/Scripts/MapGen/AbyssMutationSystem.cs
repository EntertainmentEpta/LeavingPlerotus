using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tipos de Mutação do Abismo sorteadas a cada andar do Modo Endless.
/// </summary>
public enum AbyssMutationType
{
    None = 0,
    HordaDeElites = 1,
    FrenesiAbissal = 2,
    SinfoniaDoCaos = 3,
    FortunaDoAbismo = 4
}

/// <summary>
/// Definição configurável de uma Mutação do Abismo (nome, descrição curta e modificadores de gameplay).
/// </summary>
[Serializable]
public class AbyssMutationDefinition
{
    public AbyssMutationType type = AbyssMutationType.None;
    public string displayName = "Nenhuma";
    public string shortDescription = "";
    public string accentHexColor = "#FFAA00";

    [Header("Elites & Composição")]
    [Tooltip("Bônus aditivo na chance de variantes/elites procedurais no spawner (ex: 0.25 = +25%).")]
    [Range(0f, 0.6f)] public float eliteSpawnChanceBonus = 0f;

    [Tooltip("Bônus aditivo na chance de renascimento Elite ao morrer (ex: 0.22 = +22%).")]
    [Range(0f, 0.5f)] public float eliteRebirthBonus = 0f;

    [Tooltip("Quantidade extra no limite máximo de Elites por sala.")]
    [Range(0, 3)] public int bonusMaxElitesPerRoom = 0;

    [Tooltip("Entradas extras de peso para EnemyClass.Elite na roleta da sala.")]
    [Range(0, 3)] public int bonusEliteWeightEntries = 0;

    [Tooltip("Multiplicador no orçamento de spawn da sala (ex: 1.25 = +25% budget).")]
    [Range(0.5f, 2f)] public float spawnBudgetMultiplier = 1f;

    [Header("Velocidade & Agressividade Inimiga")]
    [Tooltip("Multiplicador de velocidade de movimento dos inimigos.")]
    [Range(1f, 1.5f)] public float enemyMoveSpeedMultiplier = 1f;

    [Tooltip("Multiplicador de velocidade de ataque / redução de cooldown dos inimigos.")]
    [Range(1f, 1.5f)] public float enemyAttackSpeedMultiplier = 1f;

    [Header("Sinfonia do Caos (Adrenalina do Player)")]
    [Tooltip("Chance de abates comuns dispararem Adrenalina no jogador (Elites sempre disparam quando > 0).")]
    [Range(0f, 1f)] public float chaosKillAdrenalineChance = 0f;

    [Tooltip("Duração base (s) da Adrenalina de abate comum no Caos.")]
    public float chaosAdrenalineDuration = 4.5f;

    [Tooltip("Duração amplificada (s) da Adrenalina ao abater Elite no Caos.")]
    public float chaosEliteAdrenalineDuration = 7.0f;

    [Tooltip("Bônus de velocidade de movimento concedido pela Adrenalina em abate de Elite (ex: 0.22 = +22%).")]
    public float chaosEliteMoveBonus = 0.22f;

    [Tooltip("Bônus de velocidade de ataque concedido pela Adrenalina em abate de Elite (ex: 0.28 = +28%).")]
    public float chaosEliteAttackSpeedBonus = 0.28f;

    [Header("Fortuna do Abismo (Drops)")]
    [Tooltip("Multiplicador na essência dropada pelos inimigos.")]
    [Range(1f, 3f)] public float essenceDropMultiplier = 1f;

    [Tooltip("Bônus aditivo na chance de drop de itens característicos (ex: 0.25 = +25%).")]
    [Range(0f, 0.75f)] public float itemDropChanceBonus = 0f;

    [Tooltip("Chance de conceder +1 item adicional ao dropar loot.")]
    [Range(0f, 1f)] public float extraItemDropChance = 0f;

    [Tooltip("Bônus aditivo nas rolagens de Tier de drops Elite (T2/T3/T4).")]
    [Range(0f, 0.5f)] public float eliteTierChanceBonus = 0f;
}

/// <summary>
/// Catálogo padrão das Mutações do Abismo, usado como seed inicial do Inspector e fallback seguro em runtime.
/// </summary>
public static class AbyssMutationCatalog
{
    public static List<AbyssMutationDefinition> CreateDefaultDefinitions()
    {
        return new List<AbyssMutationDefinition>
        {
            new AbyssMutationDefinition
            {
                type = AbyssMutationType.HordaDeElites,
                displayName = "Horda de Elites",
                shortDescription = "+Elites nas salas e renascimento Elite",
                accentHexColor = "#FF6B35",
                eliteSpawnChanceBonus = 0.25f,
                eliteRebirthBonus = 0.22f,
                bonusMaxElitesPerRoom = 2,
                bonusEliteWeightEntries = 2,
                spawnBudgetMultiplier = 1.10f
            },
            new AbyssMutationDefinition
            {
                type = AbyssMutationType.FrenesiAbissal,
                displayName = "Frenesi Abissal",
                shortDescription = "Inimigos +velozes no movimento e ataque",
                accentHexColor = "#FF3860",
                enemyMoveSpeedMultiplier = 1.22f,
                enemyAttackSpeedMultiplier = 1.25f
            },
            new AbyssMutationDefinition
            {
                type = AbyssMutationType.SinfoniaDoCaos,
                displayName = "Sinfonia do Caos",
                shortDescription = "Ondas caóticas | Abates dão Adrenalina",
                accentHexColor = "#C06CFF",
                eliteSpawnChanceBonus = 0.12f,
                eliteRebirthBonus = 0.10f,
                bonusMaxElitesPerRoom = 1,
                bonusEliteWeightEntries = 1,
                spawnBudgetMultiplier = 1.25f,
                enemyMoveSpeedMultiplier = 1.08f,
                enemyAttackSpeedMultiplier = 1.12f,
                chaosKillAdrenalineChance = 0.45f,
                chaosAdrenalineDuration = 4.5f,
                chaosEliteAdrenalineDuration = 7.0f,
                chaosEliteMoveBonus = 0.22f,
                chaosEliteAttackSpeedBonus = 0.28f
            },
            new AbyssMutationDefinition
            {
                type = AbyssMutationType.FortunaDoAbismo,
                displayName = "Fortuna do Abismo",
                shortDescription = "+65% Essência, +25% Drop e loot extra",
                accentHexColor = "#FFD166",
                essenceDropMultiplier = 1.65f,
                itemDropChanceBonus = 0.25f,
                extraItemDropChance = 0.35f,
                eliteTierChanceBonus = 0.15f
            }
        };
    }

    public static AbyssMutationDefinition GetDefault(AbyssMutationType type)
    {
        List<AbyssMutationDefinition> defaults = CreateDefaultDefinitions();
        for (int i = 0; i < defaults.Count; i++)
        {
            if (defaults[i].type == type) return defaults[i];
        }
        return new AbyssMutationDefinition { type = AbyssMutationType.None };
    }
}
