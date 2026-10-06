#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Ferramenta de Editor para popular automaticamente todas as salas do jogo 
/// com a lista oficial de coletáveis da Bolsa Sintética (Fauna, Flora, Minérios e Peixe Lampião).
/// </summary>
public class PopulateRoomPrefabsEditor : EditorWindow
{
    [MenuItem("RogueLike/Popular Coletáveis em Todas as Salas")]
    public static void PopulateAllRoomPrefabs()
    {
        int modifiedCount = 0;

        // Busca todas as salas (prefabs) no projeto
        string[] searchFolders = new string[] { "Assets/_Project/Enviroment/Map", "Assets/_Project/Prefabs" };
        string[] guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);

        // Carrega referências base dos itens da Bolsa Sintética
        GameObject flyPrefab = Resources.Load<GameObject>("SpawnItems/Fly") ?? Resources.Load<GameObject>("Fly");
        GameObject quebradicoPrefab = Resources.Load<GameObject>("SpawnItems/Quebradiço") ?? Resources.Load<GameObject>("SpawnItems/CarangueijoQuebradisso");
        GameObject ovoPrefab = Resources.Load<GameObject>("SpawnItems/Ovocristal");
        GameObject melacotusPrefab = Resources.Load<GameObject>("SpawnItems/Melacotus");
        GameObject lotusPrefab = Resources.Load<GameObject>("SpawnItems/Lotus");
        GameObject lanternasPrefab = Resources.Load<GameObject>("SpawnItems/Lanternas");
        GameObject cristalPrefab = Resources.Load<GameObject>("SpawnItems/Cristal");

        ItemData flyData = Resources.Load<ItemData>("ItemData/vagalume_cristalizado");
        ItemData quebradicoData = Resources.Load<ItemData>("ItemData/caracol_geodo");
        ItemData ovoData = Resources.Load<ItemData>("ItemData/ovo_cristal_crawler");
        ItemData melacotusData = Resources.Load<ItemData>("ItemData/melocactus");
        ItemData lotusData = Resources.Load<ItemData>("ItemData/lithos");
        ItemData lanternasData = Resources.Load<ItemData>("ItemData/arvore_vagens");
        ItemData cristalData = Resources.Load<ItemData>("ItemData/po_de_cristal");
        ItemData peixeData = Resources.Load<ItemData>("ItemData/peixe_lagoa");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null) continue;

            RoomController roomController = prefab.GetComponent<RoomController>();
            if (roomController != null)
            {
                bool modified = false;

                // Popular Fauna
                if (roomController.faunaSpawnEntries == null || roomController.faunaSpawnEntries.Count == 0)
                {
                    roomController.faunaSpawnEntries = new List<CollectibleSpawnEntry>()
                    {
                        new CollectibleSpawnEntry() { itemName = "Vagalume Cristalizado", prefab = flyPrefab, itemData = flyData, weight = 1.0f },
                        new CollectibleSpawnEntry() { itemName = "Caracol Geodo (Quebradiço)", prefab = quebradicoPrefab, itemData = quebradicoData, weight = 1.0f },
                        new CollectibleSpawnEntry() { itemName = "Ovos de Cristal Crawler", prefab = ovoPrefab, itemData = ovoData, weight = 1.0f }
                    };
                    modified = true;
                }

                // Popular Flora
                if (roomController.floraSpawnEntries == null || roomController.floraSpawnEntries.Count == 0)
                {
                    roomController.floraSpawnEntries = new List<CollectibleSpawnEntry>()
                    {
                        new CollectibleSpawnEntry() { itemName = "Melocactus", prefab = melacotusPrefab, itemData = melacotusData, weight = 1.0f },
                        new CollectibleSpawnEntry() { itemName = "Lithos (Lotus)", prefab = lotusPrefab, itemData = lotusData, weight = 1.0f },
                        new CollectibleSpawnEntry() { itemName = "Árvore de Vagens (Lanternas)", prefab = lanternasPrefab, itemData = lanternasData, weight = 1.0f }
                    };
                    modified = true;
                }

                // Popular Minérios
                if (roomController.mineralSpawnEntries == null || roomController.mineralSpawnEntries.Count == 0)
                {
                    roomController.mineralSpawnEntries = new List<CollectibleSpawnEntry>()
                    {
                        new CollectibleSpawnEntry() { itemName = "Cristal (Pó de Cristal)", prefab = cristalPrefab, itemData = cristalData, weight = 1.0f }
                    };
                    modified = true;
                }

                // Popular Peixe Lampião (LakeRoom)
                if (roomController.waterFaunaEntry == null || roomController.waterFaunaEntry.itemData == null)
                {
                    roomController.waterFaunaEntry = new CollectibleSpawnEntry()
                    {
                        itemName = "Peixe Lampião",
                        prefab = flyPrefab,
                        itemData = peixeData,
                        weight = 1.0f
                    };
                    modified = true;
                }

                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                    modifiedCount++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Bolsa Sintética - Salas Populadas", 
            $"Sucesso! Um total de {modifiedCount} prefabs de salas foram populados com os coletáveis da Bolsa Sintética.", "OK");
    }
}
#endif
