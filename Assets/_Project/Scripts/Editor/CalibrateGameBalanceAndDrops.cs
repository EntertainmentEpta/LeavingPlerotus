using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Ferramenta de Editor para calibrar e sincronizar os Drops, HPs, Essências e Danos
/// de todos os prefabs de inimigos de acordo com a Matriz de Balanceamento GDD.
///
/// Execução via Menu: EPTA Tools -> Calibrate Game Balance and Drops
/// Ou via linha de comando Unity batchmode.
/// </summary>
public static class CalibrateGameBalanceAndDrops
{
    private const string ESSENCE_PREFAB_PATH = "Assets/_Project/Items_and_Crafting/Drops Prefabs/Essência.prefab";
    private const string DROPS_FOLDER = "Assets/_Project/Items_and_Crafting/Drops";

    [MenuItem("EPTA Tools/Calibrate Game Balance and Drops")]
    public static void ExecuteCalibration()
    {
        Debug.Log("=================================================================");
        Debug.Log("🎯 [BALANCEAMENTO] INICIANDO CALIBRAÇÃO GERAL DE INIMIGOS E DROPS...");
        Debug.Log("=================================================================");

        GameObject essencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ESSENCE_PREFAB_PATH);
        if (essencePrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("Essência t:prefab");
            if (guids.Length > 0)
            {
                essencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        if (essencePrefab == null)
        {
            Debug.LogError($"[BALANCEAMENTO] Prefab de Essência não encontrado em {ESSENCE_PREFAB_PATH}!");
            return;
        }

        // 1. Calibra Peixe
        CalibratePeixe(essencePrefab);

        // 2. Calibra Goblin
        CalibrateGoblin(essencePrefab, "Assets/_Project/Enemies/Goblin/Goblin.prefab");
        CalibrateGoblin(essencePrefab, "Assets/_Project/Enemies/Goblin/Goblin_Elite.prefab");

        // 3. Calibra Star / Shard Swarm
        CalibrateStar(essencePrefab, "Assets/_Project/Enemies/ShardSwarm/Star.prefab");
        CalibrateStar(essencePrefab, "Assets/_Project/Enemies/ShardSwarm/Shard Swarm.prefab");

        // 4. Calibra Crystal Watcher
        CalibrateCrystalWatcher(essencePrefab);

        // 5. Calibra Golem (reduz essência de 100 para 30)
        CalibrateGolem(essencePrefab);

        // 6. Calibra Cristalus (reduz essência de 100 para 15)
        CalibrateCristalus(essencePrefab);

        // 7. Calibra Geobionte
        CalibrateGeobionte(essencePrefab);

        // 8. Calibra Aranha e Sharp (garantia de consistência)
        CalibrateSpider(essencePrefab);
        CalibrateSharp(essencePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=================================================================");
        Debug.Log("✅ [BALANCEAMENTO] TODOS OS PREFABS FORAM CALIBRADOS COM SUCESSO!");
        Debug.Log("=================================================================");
    }

    private static void CalibratePeixe(GameObject essencePrefab)
    {
        string path = "Assets/_Project/Enemies/Peixe/Peixe.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Corrige HP de 300 para 75
            DummyHealth health = root.GetComponent<DummyHealth>();
            if (health != null)
            {
                health.maxHealth = 75;
            }

            // Configura EnemyDrops
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 12;
            drops.essenceVariation = 3;
            drops.globalDropChance = 0.18f;
            drops.itemAmount = 1;

            drops.lootPool.Clear();
            AddLoot(drops, "peixe_escama_t1", 60f);
            AddLoot(drops, "peixe_escama_t1 2", 25f);
            AddLoot(drops, "peixe_escama_t1 3", 10f);
            AddLoot(drops, "peixe_escama_t1 1", 5f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[OK] Peixe calibrado: HP=75, Drops=18%, 4 tiers vinculados.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateGoblin(GameObject essencePrefab, string path)
    {
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            DummyHealth health = root.GetComponent<DummyHealth>();
            if (health != null && health.maxHealth != 95)
            {
                health.maxHealth = 95;
            }

            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 12;
            drops.essenceVariation = 3;
            drops.globalDropChance = 0.18f;
            drops.itemAmount = 1;

            drops.lootPool.Clear();
            AddLoot(drops, "goblin_coin_t1", 60f);
            AddLoot(drops, "goblin_braço_t2", 25f);
            AddLoot(drops, "goblin_amulet_t3", 10f);
            AddLoot(drops, "goblin_bomb_t4", 5f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[OK] {Path.GetFileName(path)} calibrado com EnemyDrops (4 itens da linhagem goblin).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateStar(GameObject essencePrefab, string path)
    {
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 20;
            drops.essenceVariation = 4;
            drops.globalDropChance = 0.35f;
            drops.itemAmount = 1;

            drops.lootPool.Clear();
            AddLoot(drops, "shard_splinter_t1", 40f);
            AddLoot(drops, "shard_resonant_t2", 35f);
            AddLoot(drops, "shard_refract_t3", 20f);
            AddLoot(drops, "shard_prismatic_t4", 5f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[OK] {Path.GetFileName(path)} calibrado: LootPool preenchido com 4 itens shard.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateCrystalWatcher(GameObject essencePrefab)
    {
        string path = "Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 12;
            drops.essenceVariation = 3;
            drops.globalDropChance = 0.18f;
            drops.itemAmount = 1;

            drops.lootPool.Clear();
            AddLoot(drops, "watcher_lens_t1", 60f);
            AddLoot(drops, "watcher_eye_t2", 25f);
            AddLoot(drops, "watcher_sight_t3", 10f);
            AddLoot(drops, "watcher_beacon_t4", 5f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[OK] CrystalWatcher calibrado com 4 itens de drop.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateGolem(GameObject essencePrefab)
    {
        string path = "Assets/_Project/Enemies/Golem/Golem.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 30; // Corrigido de 100 para 30
            drops.essenceVariation = 5;
            drops.globalDropChance = 0.35f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[OK] Golem calibrado: essência reduzida de 100 para 30.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateCristalus(GameObject essencePrefab)
    {
        string path = "Assets/_Project/Enemies/Cristalus/Cristalus.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 15; // Corrigido de 100 para 15
            drops.essenceVariation = 3;
            drops.globalDropChance = 0.25f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[OK] Cristalus calibrado: essência reduzida de 100 para 15.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateGeobionte(GameObject essencePrefab)
    {
        string path = "Assets/GameAssets/Prefabs-Gabriel/Enemies Prefabs/Geobionte.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 60;
            drops.essenceVariation = 10;
            drops.globalDropChance = 1.0f; // Drop garantido para Elite

            drops.lootPool.Clear();
            AddLoot(drops, "bismutado_placa_t1", 10f);
            AddLoot(drops, "geobionte_bismuto_t2", 30f);
            AddLoot(drops, "bismutado_glândula_t3", 40f);
            AddLoot(drops, "sentinel_leg_t4", 20f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[OK] Geobionte calibrado com 4 itens (T1-T4) e 100% drop garantido.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateSpider(GameObject essencePrefab)
    {
        string path = "Assets/_Project/Enemies/Spider/Spider.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 10;
            drops.essenceVariation = 3;
            drops.globalDropChance = 0.10f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CalibrateSharp(GameObject essencePrefab)
    {
        string path = "Assets/_Project/Enemies/SharpBlur/Meshy_AI_Amethyst_Wraith_biped/ANt_Sharp.prefab";
        if (!File.Exists(path)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyDrops drops = root.GetComponent<EnemyDrops>() ?? root.AddComponent<EnemyDrops>();
            drops.essencePrefab = essencePrefab;
            drops.essenceAmount = 10;
            drops.essenceVariation = 3;
            drops.globalDropChance = 0.10f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AddLoot(EnemyDrops drops, string prefabName, float weight)
    {
        string path = $"{DROPS_FOLDER}/{prefabName}.prefab";
        GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (item == null)
        {
            // Tenta buscar por nome sem extensão
            string[] guids = AssetDatabase.FindAssets($"{prefabName} t:prefab", new[] { DROPS_FOLDER });
            if (guids.Length > 0)
            {
                item = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        if (item != null)
        {
            drops.lootPool.Add(new EnemyDrops.LootPoolItem
            {
                itemPrefab = item,
                weight = weight
            });
        }
        else
        {
            Debug.LogWarning($"[AVISO] Item de drop não encontrado: {path}");
        }
    }
}
