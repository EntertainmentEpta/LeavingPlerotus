using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Configuracao de spawn de um inimigo com suporte a Prefab Variant (elite/shiny).
///
/// ARQUITETURA DE SPAWN DUPLO:
///   O sistema decide qual inimigo spawnar normalmente (pagando os pontos).
///   Entao intercepta e rola "mutationChance" para decidir se spawna a variante elite.
///   O custo de pontos NAO muda — a variante e gratuita em termos de budget.
/// </summary>
[System.Serializable]
public class EnemySpawnSetup
{
    [Tooltip("Prefab base do inimigo (o inimigo normal). Deve ser o mesmo que esta nas listas acima.")]
    public GameObject basePrefab;

    [Tooltip("Prefab Variant do inimigo elite. Criado via 'Create > Prefab Variant' na Unity.")]
    public GameObject variantPrefab;

    [Range(0f, 1f)]
    [Tooltip("Chance de spawnar a variante elite ao inves do base. Ex: 0.15 = 15%.")]
    public float mutationChance = 0.15f;
}

/// <summary>
/// ScriptableObject central que define as listas de inimigos para as salas de combate da dungeon.
///
/// VANTAGEM:
///   Todas as salas do jogo passam a ler este unico arquivo. Quando voce cria um mob novo,
///   basta adicioná-lo aqui e AUTOMATICAMENTE todas as salas da dungeon ja comecam a spawna-lo!
/// </summary>
[CreateAssetMenu(fileName = "DefaultEnemyPool", menuName = "EPTA/Config/Enemy Pool Config")]
public class EnemyPoolConfig : ScriptableObject
{
    [Header("Mob Menor (1 ponto — Enxame / Leve)")]
    [Tooltip("Mobs rapidos e leves para compor enxame. Ex: Spider, SharpBlur, Totem.")]
    public List<GameObject> mobMenorPrefabs = new List<GameObject>();

    [Header("Atirador (2 pontos — Ranged / Distancia)")]
    [Tooltip("Inimigos que disparam projeteis a distancia. Ex: Goblin, CrystalDragon, CrystalWatcher.")]
    public List<GameObject> atiradorPrefabs = new List<GameObject>();

    [Header("Tanque (4 pontos — Pesado / Alta Vida)")]
    [Tooltip("Inimigos de alta durabilidade e controle de area. Ex: Golem, MagicStone.")]
    public List<GameObject> tanquePrefabs = new List<GameObject>();

    [Header("Elite (10 pontos — Mini-Boss / Sub-Chefe)")]
    [Tooltip("Inimigos de grande porte com ataques devastadores. Ex: Shard Swarm, Geobionte.")]
    public List<GameObject> elitePrefabs = new List<GameObject>();

    [Header("Suporte (3 pontos — Buff / Healer)")]
    [Tooltip("Inimigos que curam ou aplicam buffs aos aliados. Ex: Cristalus, CrystalTuner.")]
    public List<GameObject> suportePrefabs = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────
    [Header("Variacoes Elite (Shiny) — Spawn Duplo")]
    [Tooltip("Para cada inimigo que tiver uma variante elite, adicione uma entrada aqui. " +
             "O basePrefab deve ser o mesmo prefab que esta nas listas acima. " +
             "NAO e necessario remover o basePrefab das listas acima.")]
    public List<EnemySpawnSetup> mutationSetups = new List<EnemySpawnSetup>();

    /// <summary>
    /// Dado um prefab base sorteado normalmente, retorna a configuracao de mutacao
    /// correspondente (se existir). Retorna null se nao houver variante configurada.
    /// </summary>
    public EnemySpawnSetup GetMutationSetup(GameObject basePrefab)
    {
        if (basePrefab == null || mutationSetups == null) return null;
        foreach (var setup in mutationSetups)
        {
            if (setup != null && setup.basePrefab == basePrefab)
                return setup;
        }
        return null;
    }

    /// <summary>
    /// Retorna todos os prefabs unicos cadastrados em todas as categorias.
    /// </summary>
    public List<GameObject> GetAllUniquePrefabs()
    {
        List<GameObject> list = new List<GameObject>();
        void AddList(List<GameObject> source)
        {
            if (source == null) return;
            foreach (var p in source)
            {
                if (p != null && !list.Contains(p)) list.Add(p);
            }
        }

        AddList(mobMenorPrefabs);
        AddList(atiradorPrefabs);
        AddList(tanquePrefabs);
        AddList(elitePrefabs);
        AddList(suportePrefabs);

        return list;
    }
}
