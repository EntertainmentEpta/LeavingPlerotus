using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor Tool: popula automaticamente as listas de inimigos de todos os
/// RoomController encontrados em prefabs do projeto.
///
/// USO:
///   Menu Unity → EPTA Tools → Populate Room Enemy Pools
///
/// CLASSIFICAÇÃO (baseada nos AIs implementados):
///   Mob Menor  (1 pt)  : Spider, SkullController
///   Atirador   (2 pts) : Goblin, CrystalWatcher
///   Tanque     (4 pts) : Golem, MagicStoneEnemy, Totem
///   Elite      (10 pts): Shard Swarm, CrystalTuner
///
/// Para alterar a classificação, edite os arrays abaixo e rode novamente.
/// </summary>
public class RoomControllerEnemyPopulator : EditorWindow
{
    // =========================================================
    // CLASSIFICAÇÃO DOS INIMIGOS
    // Edite os caminhos aqui se os prefabs mudarem de pasta.
    // =========================================================

    private static readonly string[] MOB_MENOR_PATHS = new[]
    {
        "Assets/_Project/Enemies/Spider/Spider.prefab",
        "Assets/_Project/Enemies/SharpBlur/Meshy_AI_Amethyst_Wraith_biped/ANt_Sharp.prefab",
        "Assets/_Project/Enemies/Totem/Totem 1.prefab",
    };

    private static readonly string[] ATIRADOR_PATHS = new[]
    {
        "Assets/_Project/Enemies/Peixe/Peixe.prefab",
        "Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab",
        "Assets/_Project/Enemies/Goblin/Goblin.prefab",
    };

    private static readonly string[] TANQUE_PATHS = new[]
    {
        "Assets/_Project/Enemies/Golem/Golem.prefab",
        "Assets/_Project/Enemies Shortcut/Enemies/MagicStoneEnemy.prefab",
        "Assets/_Project/Enemies/ShardSwarm/Star.prefab",
    };

    private static readonly string[] ELITE_PATHS = new[]
    {
        "Assets/GameAssets/Prefabs-Gabriel/Enemies Prefabs/Geobionte.prefab",
    };

    private static readonly string[] SUPORTE_PATHS = new[]
    {
        "Assets/_Project/Enemies/Cristalus/Cristalus.prefab",
        "Assets/_Project/Enemies/CrystalTunner/CrystalTuner_Root.prefab",
    };

    // =========================================================
    // MENU ENTRY
    // =========================================================

    [MenuItem("EPTA Tools/Populate Room Enemy Pools")]
    public static void PopulateAllRoomControllers()
    {
        EnemyPoolConfig centralConfig = AssetDatabase.LoadAssetAtPath<EnemyPoolConfig>("Assets/_Project/Resources/DefaultEnemyPool.asset");

        // Usa diretamente as listas que o usuário configurou no DefaultEnemyPool.asset!
        // Só usa os caminhos padrão caso alguma lista do DefaultEnemyPool esteja vazia.
        List<GameObject> mobMenor = (centralConfig != null && centralConfig.mobMenorPrefabs != null && centralConfig.mobMenorPrefabs.Count > 0)
            ? new List<GameObject>(centralConfig.mobMenorPrefabs.Where(p => p != null))
            : LoadPrefabs(MOB_MENOR_PATHS, "Mob Menor");

        List<GameObject> atirador = (centralConfig != null && centralConfig.atiradorPrefabs != null && centralConfig.atiradorPrefabs.Count > 0)
            ? new List<GameObject>(centralConfig.atiradorPrefabs.Where(p => p != null))
            : LoadPrefabs(ATIRADOR_PATHS, "Atirador");

        List<GameObject> tanque = (centralConfig != null && centralConfig.tanquePrefabs != null && centralConfig.tanquePrefabs.Count > 0)
            ? new List<GameObject>(centralConfig.tanquePrefabs.Where(p => p != null))
            : LoadPrefabs(TANQUE_PATHS, "Tanque");

        List<GameObject> elite = (centralConfig != null && centralConfig.elitePrefabs != null && centralConfig.elitePrefabs.Count > 0)
            ? new List<GameObject>(centralConfig.elitePrefabs.Where(p => p != null))
            : LoadPrefabs(ELITE_PATHS, "Elite");

        List<GameObject> suporte = (centralConfig != null && centralConfig.suportePrefabs != null && centralConfig.suportePrefabs.Count > 0)
            ? new List<GameObject>(centralConfig.suportePrefabs.Where(p => p != null))
            : LoadPrefabs(SUPORTE_PATHS, "Suporte");

        // Garante que o próprio DefaultEnemyPool.asset esteja sincronizado
        if (centralConfig != null)
        {
            centralConfig.mobMenorPrefabs = new List<GameObject>(mobMenor);
            centralConfig.atiradorPrefabs = new List<GameObject>(atirador);
            centralConfig.tanquePrefabs   = new List<GameObject>(tanque);
            centralConfig.elitePrefabs    = new List<GameObject>(elite);
            centralConfig.suportePrefabs  = new List<GameObject>(suporte);
            EditorUtility.SetDirty(centralConfig);
        }

        if (mobMenor.Count == 0 && atirador.Count == 0 && tanque.Count == 0 && elite.Count == 0 && suporte.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Erro",
                "Nenhum prefab de inimigo foi encontrado no DefaultEnemyPool nem nos caminhos configurados.",
                "OK"
            );
            return;
        }

        // Encontra todos os prefabs que contêm RoomController
        string[] allPrefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        int roomsUpdated = 0;
        int roomsSkipped = 0;

        foreach (string guid in allPrefabGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            RoomController[] controllers = prefab.GetComponentsInChildren<RoomController>(includeInactive: true);
            if (controllers.Length == 0) continue;

            bool hasCombatRoom = false;
            foreach (RoomController rc in controllers)
            {
                if (rc.isSafeRoom) roomsSkipped++;
                else hasCombatRoom = true;
            }

            if (!hasCombatRoom) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            RoomController[] editControllers = root.GetComponentsInChildren<RoomController>(includeInactive: true);
            foreach (RoomController editRc in editControllers)
            {
                if (editRc.isSafeRoom) continue;

                editRc.enemyPoolConfig = centralConfig;
                editRc.mobMenorPrefabs = new List<GameObject>(mobMenor);
                editRc.atiradorPrefabs = new List<GameObject>(atirador);
                editRc.tanquePrefabs   = new List<GameObject>(tanque);
                editRc.elitePrefabs    = new List<GameObject>(elite);
                editRc.suportePrefabs  = new List<GameObject>(suporte);
                EditorUtility.SetDirty(editRc);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            roomsUpdated++;
            Debug.Log($"[EnemyPopulator] ✅ Prefab atualizado e salvo: {path}");
        }

        // Também atualiza e sobrescreve todas as salas que possam estar instanciadas na cena ativa
        int sceneRoomsUpdated = 0;
        RoomController[] sceneControllers = Object.FindObjectsByType<RoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var rc in sceneControllers)
        {
            if (rc.isSafeRoom) continue;
            Undo.RecordObject(rc, "Populate Room Enemy Pools");
            rc.enemyPoolConfig = centralConfig;
            rc.mobMenorPrefabs = new List<GameObject>(mobMenor);
            rc.atiradorPrefabs = new List<GameObject>(atirador);
            rc.tanquePrefabs   = new List<GameObject>(tanque);
            rc.elitePrefabs    = new List<GameObject>(elite);
            rc.suportePrefabs  = new List<GameObject>(suporte);
            EditorUtility.SetDirty(rc);
            sceneRoomsUpdated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            $"✅ Sobrescrita e População Concluída a partir do DefaultEnemyPool!\n\n" +
            $"Prefabs de sala sobrescritos no disco: {roomsUpdated}\n" +
            $"Salas atualizadas na cena aberta: {sceneRoomsUpdated}\n" +
            $"Safe Rooms ignoradas: {roomsSkipped}\n\n" +
            $"Mob Menor (1 pt)  : {mobMenor.Count} ({string.Join(", ", mobMenor.Select(p => p.name))})\n" +
            $"Atirador  (2 pts) : {atirador.Count} ({string.Join(", ", atirador.Select(p => p.name))})\n" +
            $"Tanque    (4 pts) : {tanque.Count} ({string.Join(", ", tanque.Select(p => p.name))})\n" +
            $"Elite     (10 pts): {elite.Count} ({string.Join(", ", elite.Select(p => p.name))})\n" +
            $"Suporte   (3 pts) : {suporte.Count} ({string.Join(", ", suporte.Select(p => p.name))})";

        Debug.Log($"[EnemyPopulator] {summary}");
        EditorUtility.DisplayDialog("EPTA Tools — Enemy Pools", summary, "OK");
    }

    [MenuItem("EPTA Tools/Setup All Enemy Drops & Weights")]
    public static void SetupAllEnemyDrops()
    {
        GameObject essencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Items_and_Crafting/Drops Prefabs/Essência.prefab");

        // Dicionário mapeando cada prefab de inimigo aos seus 4 itens (T1, T2, T3, T4)
        var enemyDropMappings = new Dictionary<string, string[]>
        {
            { "Assets/_Project/Enemies/Spider/Spider.prefab", new[] { "spider_leg_t1", "spider_silk_t2", "spider_fang_t3", "spider_egg_t4" } },
            { "Assets/_Project/Enemies/SharpBlur/Meshy_AI_Amethyst_Wraith_biped/ANt_Sharp.prefab", new[] { "sharp_garras_t1", "sharp_cora", "sharp_cerebro_t3", "sharp_osso_t4" } },
            { "Assets/_Project/Enemies/Peixe/Peixe.prefab", new[] { "dragon_scale_t1", "dragon_claw_t2", "dragon_tail_t3", "dragon_crystal_t4" } },
            { "Assets/_Project/Enemies/Goblin/Goblin.prefab", new[] { "goblin_coin_t1", "goblin_bra", "goblin_amulet_t3", "goblin_bomb_t4" } },
            { "Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab", new[] { "watcher_lens_t1", "watcher_eye_t2", "watcher_sight_t3", "watcher_beacon_t4" } },
            { "Assets/_Project/Enemies/Golem/Golem.prefab", new[] { "golem_chip_t1", "golem_plate_t2", "golem_core_t3", "golem_heart_t4" } },
            { "Assets/_Project/Enemies/MagicStone/MagicStoneEnemy.prefab", new[] { "magic_dust_t1", "magic_rune_t2", "magic_essence_t3", "magic_catalyst_t4" } },
            { "Assets/_Project/Enemies Shortcut/Enemies/MagicStoneEnemy.prefab", new[] { "magic_dust_t1", "magic_rune_t2", "magic_essence_t3", "magic_catalyst_t4" } },
            { "Assets/_Project/Enemies/ShardSwarm/Star.prefab", new[] { "shard_splinter_t1", "shard_resonant_t2", "shard_refract_t3", "shard_prismatic_t4" } },
            { "Assets/_Project/Enemies/Totem/Totem 1.prefab", new[] { "totem_stone_t1", "totem_carved_t2", "totem_ancient_t3", "totem_monolith_t4" } },
            { "Assets/GameAssets/Prefabs-Gabriel/Enemies Prefabs/Geobionte.prefab", new[] { "bismutado_fragmento_t1", "geobionte_bismuto_t2", "bismutado_gl", "sentinel_leg_t4" } },
            { "Assets/_Project/Enemies/Bismutado/bismutado.prefab", new[] { "bismutado_fragmento_t1", "geobionte_bismuto_t2", "bismutado_gl", "bismutado_rotor_t4" } },
            { "Assets/_Project/Enemies/Cristalus/Cristalus.prefab", new[] { "Cristalus_Crystal_t1", "Cristalus_espinhos_t2", "Cristalus_olho_t3", "Cristalus_cabe" } },
            { "Assets/_Project/Enemies/CrystalTunner/CrystalTuner_Root.prefab", new[] { "tuner_shard_t1", "tuner_lens_t2", "tuner_prism_t3", "tuner_matrix_t4" } }
        };

        // Cache de todos os prefabs de drop da pasta Drops
        string[] allDropGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Items_and_Crafting/Drops" });
        var dropPrefabMap = new Dictionary<string, GameObject>();
        foreach (var guid in allDropGUIDs)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null)
            {
                dropPrefabMap[go.name.ToLower()] = go;
            }
        }

        int enemiesConfigured = 0;

        foreach (var kvp in enemyDropMappings)
        {
            string enemyPath = kvp.Key;
            string[] dropPatterns = kvp.Value;

            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"[EnemyDropsSetup] Prefab de inimigo não encontrado: {enemyPath}");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(enemyPath);
            if (root == null) continue;

            EnemyDrops dropsComp = root.GetComponent<EnemyDrops>();
            if (dropsComp == null)
            {
                dropsComp = root.AddComponent<EnemyDrops>();
            }

            if (essencePrefab != null)
            {
                dropsComp.essencePrefab = essencePrefab;
                dropsComp.essenceAmount = enemyPath.ToLower().Contains("golem") || enemyPath.ToLower().Contains("geobionte") || enemyPath.ToLower().Contains("bismutado") ? 35 : 10;
                dropsComp.essenceVariation = 3;
            }

            // Chance global calibrada por classe
            string pathLower = enemyPath.ToLower();
            if (pathLower.Contains("geobionte") || pathLower.Contains("bismutado"))
                dropsComp.globalDropChance = 1.0f;
            else if (pathLower.Contains("golem") || pathLower.Contains("magicstone") || pathLower.Contains("star"))
                dropsComp.globalDropChance = 0.35f;
            else if (pathLower.Contains("cristalus") || pathLower.Contains("tuner"))
                dropsComp.globalDropChance = 0.25f;
            else if (pathLower.Contains("peixe") || pathLower.Contains("goblin") || pathLower.Contains("watcher"))
                dropsComp.globalDropChance = 0.18f;
            else
                dropsComp.globalDropChance = 0.10f;

            dropsComp.itemAmount = 1;
            dropsComp.lootPool = new List<EnemyDrops.LootPoolItem>();

            float[] standardWeights = new float[] { 40f, 30f, 20f, 10f };

            for (int i = 0; i < dropPatterns.Length; i++)
            {
                string pattern = dropPatterns[i].ToLower();
                GameObject matchedDrop = null;

                foreach (var dropKvp in dropPrefabMap)
                {
                    if (dropKvp.Key.Contains(pattern))
                    {
                        matchedDrop = dropKvp.Value;
                        break;
                    }
                }

                if (matchedDrop != null)
                {
                    float w = i < standardWeights.Length ? standardWeights[i] : 10f;
                    dropsComp.lootPool.Add(new EnemyDrops.LootPoolItem { itemPrefab = matchedDrop, weight = w });
                }
                else
                {
                    Debug.LogWarning($"[EnemyDropsSetup] Item drop '{pattern}' não encontrado para {enemyPrefab.name}");
                }
            }

            EditorUtility.SetDirty(dropsComp);
            PrefabUtility.SaveAsPrefabAsset(root, enemyPath);
            PrefabUtility.UnloadPrefabContents(root);

            enemiesConfigured++;
            Debug.Log($"[EnemyDropsSetup] ✅ {enemyPrefab.name}: configurado com {dropsComp.lootPool.Count} itens na LootPool (Chance: {dropsComp.globalDropChance:P0})");
        }

        string msg = $"✅ Drops configurados com sucesso em {enemiesConfigured} inimigos!\n\n" +
                     $"• Essência vinculada a todos os prefabs\n" +
                     $"• LootPool (T1 a T4) com pesos matemáticos (40 / 30 / 20 / 10)\n" +
                     $"• Peixe e Goblin agora possuem EnemyDrops totalmente configurados!";
        Debug.Log($"[EnemyDropsSetup] {msg}");
        EditorUtility.DisplayDialog("EPTA Tools — Enemy Drops", msg, "OK");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("EPTA Tools/Apply Full Balance & Populate Everything")]
    public static void ApplyFullBalanceAndPopulateEverything()
    {
        PopulateAllRoomControllers();
        SetupAllEnemyDrops();
    }

    // =========================================================
    // HELPER
    // =========================================================

    private static List<GameObject> LoadPrefabs(string[] paths, string className)
    {
        var result = new List<GameObject>();
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                result.Add(prefab);
                Debug.Log($"[EnemyPopulator] {className}: carregado '{prefab.name}' de '{path}'");
            }
            else
            {
                Debug.LogWarning($"[EnemyPopulator] ⚠️ {className}: prefab não encontrado em '{path}'. " +
                                 $"Verifique o caminho em RoomControllerEnemyPopulator.cs.");
            }
        }
        return result;
    }
}
