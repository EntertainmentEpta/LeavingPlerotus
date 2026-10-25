using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Ferramenta de setup automático para prefabs de SpawnItems e Drop Resources.
///
/// O QUE FAZ:
///   1. Para cada prefab em SpawnItems/Prefabs/ ou Drops/, verifica se existe um ItemData
///   2. Cria o ItemData (.asset) caso não exista
///   3. Abre o prefab em modo de edição e adiciona ItemPickup + Interactable se faltarem
///   4. Atribui o ItemData ao Interactable
///   5. Salva o prefab
///
/// COMO USAR:
///   Unity Menu → Tools → RogueLike → ⚙️ Setup SpawnItems Prefabs
///   Unity Menu → Tools → RogueLike → ⚙️ Setup Drop Resources (Pó de Cristal, Ovo de Aranha...)
/// </summary>
public class SpawnItemsSetupTool : Editor
{
    // ── SpawnItems (itens de mundo, returnsToBase = false) ────────────────────
    private const string PrefabsFolder   = "Assets/_Project/Items_and_Crafting/SpawnItems/Prefabs";
    private const string ItemDataFolder  = "Assets/_Project/Items_and_Crafting/SpawnItems";

    // ── Drop Resources (recursos de crafting, returnsToBase = true) ───────────
    private const string DropPrefabsFolder  = "Assets/_Project/Items_and_Crafting/Drops";
    private const string DropItemDataFolder = "Assets/_Project/Items_and_Crafting/Resources/ItemData";

    /// <summary>
    /// Recursos de drop de inimigos destinados ao crafting (returnsToBase = true).
    /// Adicione aqui novos recursos de drop e rode:
    ///   Tools → RogueLike → ⚙️ Setup Drop Resources
    /// O prefab base deve já existir em Drops/ (pode duplicar spider_leg_t1.prefab).
    /// </summary>
    private static readonly (string prefabName, string itemId, string itemName, ItemTier tier)[] DropResourceDefinitions =
    {
        // ── Aranha ─────────────────────────────────────────────────────────────
        ("po_de_cristal",  "po_de_cristal",  "Pó de Cristal",  ItemTier.Common),
        ("ovo_de_aranha",  "ovo_de_aranha",  "Ovo de Aranha",  ItemTier.Common),
        // Adicione mais recursos aqui seguindo o mesmo padrão
    };

    // Mapeamento: nome do prefab → (itemId, itemName, tier)
    // Adicione novas entradas aqui se criar mais prefabs no futuro
    private static readonly (string prefabName, string itemId, string itemName, ItemTier tier)[] PrefabDefinitions =
    {
        ("little_frog",   "little_frog",   "Sapo",          ItemTier.Common),
        ("Crystal",       "crystal",       "Cristal",       ItemTier.Common),
        ("Magic Carpet",  "magic_carpet",  "Tapete Mágico", ItemTier.Uncommon),
        ("Planta",        "planta",        "Planta",        ItemTier.Common),
        ("stone_low+",    "stone_low",     "Pedra",         ItemTier.Common),
        ("tinker",        "tinker",        "Tinker",        ItemTier.Uncommon),
    };

    [MenuItem("Tools/RogueLike/⚙️ Setup SpawnItems Prefabs")]
    public static void SetupAllSpawnItemPrefabs()
    {
        int created = 0;
        int updated = 0;
        int skipped = 0;

        foreach (var def in PrefabDefinitions)
        {
            string prefabPath = $"{PrefabsFolder}/{def.prefabName}.prefab";
            string itemDataPath = $"{ItemDataFolder}/{def.itemId}.asset";

            // ── Passo 1: Garante que o ItemData existe ─────────────────────────
            ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(itemDataPath);
            if (itemData == null)
            {
                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.itemId   = def.itemId;
                itemData.itemName = def.itemName;
                itemData.tier     = def.tier;
                itemData.recycleEssenceValue  = TierToRecycleValue(def.tier);
                itemData.infusionEssenceCost  = TierToInfusionCost(def.tier);
                itemData.returnsToBase = false;

                AssetDatabase.CreateAsset(itemData, itemDataPath);
                Debug.Log($"[SpawnItemsSetup] ItemData criado: {itemDataPath}");
                created++;
            }

            // ── Passo 2: Abre o prefab para edição ────────────────────────────
            if (!File.Exists(Path.Combine(Application.dataPath, "../", prefabPath)))
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    Debug.LogWarning($"[SpawnItemsSetup] Prefab não encontrado: {prefabPath} — pulando.");
                    skipped++;
                    continue;
                }
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool dirty = false;

            // ── Passo 3: Adiciona Interactable se não existir ─────────────────
            Interactable interactable = prefabRoot.GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = prefabRoot.AddComponent<Interactable>();
                dirty = true;
            }

            // ── Passo 4: Atribui o ItemData ao Interactable ───────────────────
            if (interactable.itemData != itemData)
            {
                interactable.itemData = itemData;
                dirty = true;
            }

            // ── Passo 5: Adiciona ItemPickup se não existir ───────────────────
            ItemPickup pickup = prefabRoot.GetComponent<ItemPickup>();
            if (pickup == null)
            {
                pickup = prefabRoot.AddComponent<ItemPickup>();
                dirty = true;
            }

            // Limpa pressFUI se for um Canvas dummy sem texto
            if (pickup.pressFUI != null)
            {
                if (pickup.pressFUI.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) == null &&
                    pickup.pressFUI.GetComponentInChildren<UnityEngine.UI.Text>(true) == null)
                {
                    pickup.pressFUI = null;
                    dirty = true;
                }
            }

            // ── Passo 6: Garante BoxCollider Trigger para interação ────────────
            BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = prefabRoot.AddComponent<BoxCollider>();
                dirty = true;
            }
            if (!col.isTrigger || col.size != new Vector3(2.5f, 2.5f, 2.5f))
            {
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(2.5f, 2.5f, 2.5f);
                dirty = true;
            }

            // ── Passo 7: Salva o prefab ───────────────────────────────────────
            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                updated++;
                Debug.Log($"[SpawnItemsSetup] Prefab configurado: {def.prefabName}");
            }
            else
            {
                skipped++;
                Debug.Log($"[SpawnItemsSetup] Sem mudanças: {def.prefabName}");
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Setup Concluído",
            $"SpawnItems configurados!\n\n" +
            $"ItemData criados: {created}\n" +
            $"Prefabs atualizados: {updated}\n" +
            $"Sem mudanças: {skipped}",
            "OK"
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup de Map Resources da Bolsa Sintética (Fauna, Flora, Minerais)
    // ─────────────────────────────────────────────────────────────────────────
    private const string MapResourcesFolder = "Assets/_Project/Items_and_Crafting/Resources/SpawnItems";

    private static readonly (string prefabName, string itemId)[] MapResourceDefinitions =
    {
        ("Cristal",    "po_de_cristal"),
        ("Fly",        "vagalume_cristalizado"),
        ("Lanternas",  "arvore_vagens"),
        ("Lotus",      "lithos"),
        ("Melacotus",  "melocactus"),
        ("Ovocristal", "ovo_cristal_crawler"),
        ("Quebradiço", "caracol_geodo")
    };

    [MenuItem("Tools/RogueLike/⚙️ Setup Map Resources (Bolsa Sintética)")]
    public static void SetupMapResources()
    {
        int updated = 0;
        int skipped = 0;

        foreach (var def in MapResourceDefinitions)
        {
            string prefabPath = $"{MapResourcesFolder}/{def.prefabName}.prefab";
            string itemDataPath = $"Assets/_Project/Items_and_Crafting/Resources/ItemData/{def.itemId}.asset";

            ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(itemDataPath);
            if (itemData == null)
            {
                Debug.LogWarning($"[MapResourcesSetup] ItemData não encontrado: {itemDataPath}");
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                skipped++;
                continue;
            }

            bool dirty = false;

            Interactable interactable = prefabRoot.GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = prefabRoot.AddComponent<Interactable>();
                dirty = true;
            }
            if (itemData != null && interactable.itemData != itemData)
            {
                interactable.itemData = itemData;
                interactable.objetoNome = itemData.itemName;
                dirty = true;
            }

            ItemPickup pickup = prefabRoot.GetComponent<ItemPickup>();
            if (pickup == null)
            {
                pickup = prefabRoot.AddComponent<ItemPickup>();
                dirty = true;
            }
            if (pickup.pressFUI != null)
            {
                if (pickup.pressFUI.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) == null &&
                    pickup.pressFUI.GetComponentInChildren<UnityEngine.UI.Text>(true) == null)
                {
                    pickup.pressFUI = null;
                    dirty = true;
                }
            }

            BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = prefabRoot.AddComponent<BoxCollider>();
                dirty = true;
            }
            if (!col.isTrigger || col.size != new Vector3(2.5f, 2.5f, 2.5f))
            {
                col.isTrigger = true;
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(2.5f, 2.5f, 2.5f);
                dirty = true;
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                updated++;
                Debug.Log($"[MapResourcesSetup] Prefab configurado com sucesso: {def.prefabName}");
            }
            else
            {
                skipped++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Setup Concluído",
            $"Recursos de Mapa (Bolsa Sintética) configurados!\n\n" +
            $"Prefabs atualizados: {updated}\n" +
            $"Sem mudanças: {skipped}",
            "OK"
        );
    }

    // ── Helpers de valores padrão por tier ────────────────────────────────────

    private static int TierToRecycleValue(ItemTier tier) => tier switch
    {
        ItemTier.Common    => 10,
        ItemTier.Uncommon  => 25,
        ItemTier.Rare      => 60,
        ItemTier.Legendary => 120,
        _                  => 10
    };

    private static int TierToInfusionCost(ItemTier tier) => tier switch
    {
        ItemTier.Common    => 60,
        ItemTier.Uncommon  => 180,
        ItemTier.Rare      => 300,
        ItemTier.Legendary => 420,
        _                  => 60
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Setup de Drop Resources (recursos de crafting: returnsToBase = true)
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/RogueLike/⚙️ Setup Drop Resources")]
    public static void SetupDropResources()
    {
        int created = 0;
        int updated = 0;
        int skipped = 0;

        foreach (var def in DropResourceDefinitions)
        {
            string prefabPath    = $"{DropPrefabsFolder}/{def.prefabName}.prefab";
            string itemDataPath  = $"{DropItemDataFolder}/{def.itemId}.asset";

            // ── Cria ou carrega o ItemData ────────────────────────────────────
            ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(itemDataPath);
            if (itemData == null)
            {
                itemData = ScriptableObject.CreateInstance<ItemData>();
                itemData.itemId              = def.itemId;
                itemData.itemName            = def.itemName;
                itemData.tier                = def.tier;
                itemData.returnsToBase       = true;   // recurso de crafting — vai para a Bolsa Sintética
                itemData.recycleEssenceValue = TierToRecycleValue(def.tier);
                itemData.infusionEssenceCost = TierToInfusionCost(def.tier);

                AssetDatabase.CreateAsset(itemData, itemDataPath);
                Debug.Log($"[DropResources] ItemData criado: {itemDataPath}");
                created++;
            }
            else
            {
                // Garante returnsToBase mesmo em assets já existentes
                if (!itemData.returnsToBase)
                {
                    itemData.returnsToBase = true;
                    EditorUtility.SetDirty(itemData);
                    Debug.Log($"[DropResources] returnsToBase corrigido para: {def.itemId}");
                    updated++;
                }
            }

            // ── Configura o prefab (se existir) ──────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"[DropResources] Prefab não encontrado: {prefabPath}\n" +
                                 $"  → Duplique um prefab em Drops/ e renomeie para '{def.prefabName}.prefab'");
                skipped++;
                continue;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool dirty = false;

            Interactable interactable = prefabRoot.GetComponent<Interactable>();
            if (interactable == null) { interactable = prefabRoot.AddComponent<Interactable>(); dirty = true; }

            if (interactable.itemData != itemData) { interactable.itemData = itemData; dirty = true; }

            if (prefabRoot.GetComponent<ItemPickup>() == null) { prefabRoot.AddComponent<ItemPickup>(); dirty = true; }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                updated++;
                Debug.Log($"[DropResources] Prefab configurado: {def.prefabName}");
            }
            else skipped++;

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Drop Resources — Setup Concluído",
            $"Recursos de crafting configurados!\n\n" +
            $"ItemData criados : {created}\n" +
            $"Atualizados      : {updated}\n" +
            $"Prefab ausente   : {skipped}\n\n" +
            "Itens com 'Prefab não encontrado' precisam do prefab criado manualmente em Drops/.",
            "OK"
        );
    }
}
