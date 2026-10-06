using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// GENERATOR AUTOMÁTICO - Cria todos os 32 prefabs + configurações com 1 clique
/// Este é o atalho final para não ter que fazer nada Manualmente!
/// </summary>
public class AutoDropGenerator : Editor
{
    [MenuItem("Tools/RogueLike/⚡ GERAR TUDO UMA VEZ (32 Prefabs + Configuração)")]
    public static void GenerateEverythingAtOnce()
    {
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("⚡ INICIANDO GERAÇÃO AUTOMÁTICA DOS 32 ITEMS...");
        Debug.Log("═══════════════════════════════════════════════════");

        var config = ScriptableObject.CreateInstance<DropDataConfig>();
        config.GenerateDefaultData();

        // Cria pastas
        string prefabFolder = "Assets/_Project/Items_and_Crafting/Drops";
        Directory.CreateDirectory(Path.Combine(Application.dataPath, prefabFolder.Replace("Assets/", "")));
        AssetDatabase.Refresh();

        int created = 0;
        var prefabsByEnemy = new Dictionary<string, List<GameObject>>();

        // ════════════════════════════════════════════════════════════════
        // STEP 1: Cria todos os 32 prefabs
        // ════════════════════════════════════════════════════════════════

        foreach (var dropItem in config.allItems)
        {
            string prefabPath = $"{prefabFolder}/{dropItem.itemId}.prefab";
            GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // Verifica se já existe
            if (itemPrefab != null)
            {
                Debug.LogWarning($"  ⏭️  {dropItem.itemId} já existe, pulando...");
            }

            if (itemPrefab == null)
            {
                // Cria GameObject
                GameObject itemGO = new GameObject(dropItem.itemId);

                // Adiciona CharacteristicItemPickup
                var pickup = itemGO.AddComponent<CharacteristicItemPickup>();
                pickup.itemId = dropItem.itemId;
                pickup.itemName = dropItem.itemName;
                pickup.itemDescription = string.Join(", ", dropItem.attributes);

                // Configura physics
                var collider = itemGO.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
                collider.isTrigger = true;

                var rb = itemGO.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;

                // Adiciona visual (cubo colorido por tier)
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(itemGO.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                // Colore por tier
                Color tierColor = GetTierColor(dropItem.tier);
                var renderer = visual.GetComponent<Renderer>();
                Material tempMat = new Material(Shader.Find("Standard"));
                tempMat.color = tierColor;
                renderer.sharedMaterial = tempMat;

                // Remove colisores do visual
                DestroyImmediate(visual.GetComponent<Collider>());
                DestroyImmediate(visual.GetComponent<Rigidbody>());

                // Salva como prefab
                PrefabUtility.SaveAsPrefabAsset(itemGO, prefabPath);
                DestroyImmediate(itemGO);

                itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Debug.Log($"  ✅ Criado: {dropItem.itemId}");
                created++;
            }

            // Guarda referência por inimigo (sempre, criado ou já existente)
            if (!prefabsByEnemy.ContainsKey(dropItem.enemyName))
                prefabsByEnemy[dropItem.enemyName] = new List<GameObject>();

            if (itemPrefab != null)
            {
                prefabsByEnemy[dropItem.enemyName].Add(itemPrefab);
            }
        }

        AssetDatabase.Refresh();

        // ════════════════════════════════════════════════════════════════
        // STEP 2: Configura os EnemyDrops nos inimigos
        // Compatível com EnemyDrops atual (characteristicItemPrefab)
        // ════════════════════════════════════════════════════════════════

        int configuredEnemies = ConfigureEnemyPrefabs(prefabsByEnemy);

        DestroyImmediate(config);
        AssetDatabase.Refresh();

        // ════════════════════════════════════════════════════════════════
        // RESULTADO FINAL
        // ════════════════════════════════════════════════════════════════

        string message = $"✅ SUCESSO!\n\n" +
            $"Prefabs criados: {created}\n" +
            $"Inimigos configurados: {configuredEnemies}\n\n" +
            $"Próximo passo:\n" +
            $"1️⃣  Teste matando inimigos em uma run\n" +
            $"2️⃣  Verifique se os items dão drop\n" +
            $"3️⃣  Depois implemente os efeitos de T4\n\n" +
            $"🎉 Tudo pronto para simular runs!";

        EditorUtility.DisplayDialog("⚡ AUTO-GENERATOR COMPLETO", message, "Vamos lá! 🚀");
        Debug.Log(message);
    }

    [MenuItem("Tools/RogueLike/🔗 RELINK DROPS NOS INIMIGOS")]
    public static void RelinkDropsOnExistingEnemies()
    {
        var config = ScriptableObject.CreateInstance<DropDataConfig>();
        config.GenerateDefaultData();

        var prefabsByEnemy = BuildDropMap(config);
        int configuredEnemies = ConfigureEnemyPrefabs(prefabsByEnemy);

        DestroyImmediate(config);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Relink finalizado",
            $"✅ Inimigos configurados: {configuredEnemies}\n\nVerifique os prefabs em Assets/Prefabs/Enemies.",
            "OK"
        );
    }

    static Dictionary<string, List<GameObject>> BuildDropMap(DropDataConfig config)
    {
        var prefabsByEnemy = new Dictionary<string, List<GameObject>>();

        foreach (var dropItem in config.allItems)
        {
            string prefabPath = $"Assets/_Project/Items_and_Crafting/Drops/{dropItem.itemId}.prefab";
            GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (itemPrefab == null) continue;

            if (!prefabsByEnemy.ContainsKey(dropItem.enemyName))
                prefabsByEnemy[dropItem.enemyName] = new List<GameObject>();

            prefabsByEnemy[dropItem.enemyName].Add(itemPrefab);
        }

        return prefabsByEnemy;
    }

    static int ConfigureEnemyPrefabs(Dictionary<string, List<GameObject>> prefabsByEnemy)
    {
        // Procura pelos prefabs de inimigos em todas as pastas do projeto
        var enemyPrefabs = AssetDatabase.FindAssets("t:prefab", new[] { 
            "Assets/_Project/Enemies", 
            "Assets/_Project/Enemies Shortcut/Enemies", 
            "Assets/GameAssets/Prefabs-Gabriel/Enemies Prefabs" 
        });

        // Carrega o prefab de Essência oficial para vincular
        GameObject defaultEssencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Items_and_Crafting/Resources/SpawnItems/EssencePickup.prefab");
        if (defaultEssencePrefab == null)
        {
            string[] essenceGuids = AssetDatabase.FindAssets("EssencePickup t:prefab");
            if (essenceGuids.Length > 0)
            {
                defaultEssencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(essenceGuids[0]));
            }
        }

        int configuredEnemies = 0;
        foreach (string guid in enemyPrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject enemyRoot = PrefabUtility.LoadPrefabContents(path);

            if (enemyRoot == null) continue;

            // Verifica se é um dos nossos inimigos
            string prefabName = enemyRoot.name.ToLower();
            string matchedEnemy = null;

            foreach (var enemy in prefabsByEnemy.Keys)
            {
                if (MatchesEnemy(prefabName, enemy))
                {
                    matchedEnemy = enemy;
                    break;
                }
            }

            if (matchedEnemy == null)
            {
                PrefabUtility.UnloadPrefabContents(enemyRoot);
                continue;
            }

            // Configura o EnemyDrops
            var enemyDrops = enemyRoot.GetComponent<EnemyDrops>();
            if (enemyDrops == null)
                enemyDrops = enemyRoot.AddComponent<EnemyDrops>();

            if (enemyDrops.essencePrefab == null && defaultEssencePrefab != null)
            {
                enemyDrops.essencePrefab = defaultEssencePrefab;
            }

            // Escolhe o item T1 como fallback e preenche a roleta de tiers.
            GameObject selectedItemPrefab = null;
            foreach (var itemPrefab in prefabsByEnemy[matchedEnemy])
            {
                if (itemPrefab != null && itemPrefab.name.EndsWith("_t1"))
                {
                    selectedItemPrefab = itemPrefab;
                    break;
                }
            }

            if (selectedItemPrefab == null && prefabsByEnemy[matchedEnemy].Count > 0)
            {
                selectedItemPrefab = prefabsByEnemy[matchedEnemy][0];
            }

            // Preenche lootPool com todos os tiers deste inimigo
            enemyDrops.lootPool.Clear();
            foreach (var itemPrefab in prefabsByEnemy[matchedEnemy])
            {
                if (itemPrefab == null) continue;

                enemyDrops.lootPool.Add(new EnemyDrops.LootPoolItem
                {
                    itemPrefab = itemPrefab,
                    weight = GetTierWeightFromName(itemPrefab.name)
                });
            }

            enemyDrops.globalDropChance = 0.35f;
            enemyDrops.itemAmount = 1;

            // Salva o prefab
            PrefabUtility.SaveAsPrefabAsset(enemyRoot, path);
            PrefabUtility.UnloadPrefabContents(enemyRoot);
            Debug.Log($"  ✅ Configurado: {Path.GetFileNameWithoutExtension(path)} com {enemyDrops.lootPool.Count} tiers (fallback {selectedItemPrefab?.name})");
            configuredEnemies++;
        }

        return configuredEnemies;
    }

    static Color GetTierColor(int tier)
    {
        return tier switch
        {
            1 => Color.white,      // T1: Branco
            2 => new Color(0f, 0.6f, 1f), // T2: Azul
            3 => new Color(0.67f, 0f, 1f), // T3: Roxo
            4 => new Color(1, 0.84f, 0),  // T4: Dourado
            _ => Color.gray
        };
    }

    static bool MatchesEnemy(string prefabNameLower, string enemyName)
    {
        string enemy = enemyName.ToLower();

        if (prefabNameLower.Contains(enemy)) return true;

        // Alias para nomes reais e variantes de prefabs no projeto
        return enemy switch
        {
            "crystal tuner" => prefabNameLower.Contains("crystaltuner") || prefabNameLower.Contains("tuner"),
            "magic crystal" => prefabNameLower.Contains("magicstone") || prefabNameLower.Contains("magic"),
            "shard swarm"   => prefabNameLower.Contains("shard") || prefabNameLower.Contains("star"),
            "cristalus"     => prefabNameLower.Contains("cristal") || prefabNameLower.Contains("cristalus"),
            "sharp blur"    => prefabNameLower.Contains("sharp") || prefabNameLower.Contains("blur") || prefabNameLower.Contains("sh"),
            "bismutado"     => prefabNameLower.Contains("bismut") || prefabNameLower.Contains("bismuto"),
            "golem"         => prefabNameLower.Contains("golem"),
            "spider"        => prefabNameLower.Contains("spider") || prefabNameLower.Contains("aranha"),
            "goblin"        => prefabNameLower.Contains("goblin"),
            "dragon"        => prefabNameLower.Contains("dragon") || prefabNameLower.Contains("fish") || prefabNameLower.Contains("cristaldrag"),
            "geobionte"     => prefabNameLower.Contains("geobionte") || prefabNameLower.Contains("boss"),
            _ => false
        };
    }

    static float GetTierWeightFromName(string prefabName)
    {
        string lower = prefabName.ToLower();

        if (lower.EndsWith("_t1")) return 60f;
        if (lower.EndsWith("_t2")) return 25f;
        if (lower.EndsWith("_t3")) return 10f;
        if (lower.EndsWith("_t4")) return 5f;

        return 1f;
    }
}
