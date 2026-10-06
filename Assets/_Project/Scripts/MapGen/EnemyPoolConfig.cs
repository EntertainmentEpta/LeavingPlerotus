using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject central que define as listas de inimigos para as salas de combate da dungeon.
/// 
/// VANTAGEM:
///   Todas as salas do jogo passam a ler este único arquivo. Quando você cria um mob novo,
///   basta adicioná-lo aqui e AUTOMATICAMENTE todas as salas da dungeon já começam a spawná-lo!
/// </summary>
[CreateAssetMenu(fileName = "DefaultEnemyPool", menuName = "EPTA/Config/Enemy Pool Config")]
public class EnemyPoolConfig : ScriptableObject
{
    [Header("Mob Menor (1 ponto — Enxame / Leve)")]
    [Tooltip("Mobs rápidos e leves para compor enxame. Ex: Spider, SharpBlur, Totem.")]
    public List<GameObject> mobMenorPrefabs = new List<GameObject>();

    [Header("Atirador (2 pontos — Ranged / Distância)")]
    [Tooltip("Inimigos que disparam projéteis à distância. Ex: Goblin, CrystalDragon, CrystalWatcher.")]
    public List<GameObject> atiradorPrefabs = new List<GameObject>();

    [Header("Tanque (4 pontos — Pesado / Alta Vida)")]
    [Tooltip("Inimigos de alta durabilidade e controle de área. Ex: Golem, MagicStone.")]
    public List<GameObject> tanquePrefabs = new List<GameObject>();

    [Header("Elite (10 pontos — Mini-Boss / Sub-Chefe)")]
    [Tooltip("Inimigos de grande porte com ataques devastadores. Ex: Shard Swarm, Geobionte.")]
    public List<GameObject> elitePrefabs = new List<GameObject>();

    [Header("Suporte (3 pontos — Buff / Healer)")]
    [Tooltip("Inimigos que curam ou aplicam buffs aos aliados. Ex: Cristalus, CrystalTuner.")]
    public List<GameObject> suportePrefabs = new List<GameObject>();

    /// <summary>
    /// Retorna todos os prefabs únicos cadastrados em todas as categorias.
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
