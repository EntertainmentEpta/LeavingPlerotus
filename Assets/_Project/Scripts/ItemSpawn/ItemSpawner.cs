using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ItemSpawnData
{
    public GameObject prefab;
    [Range(0f, 1f)]
    public float chance;
}

public class ItemSpawner : MonoBehaviour
{
    [Header("Configurao Geral")]
    [Range(0f, 1f)]
    public float spawnChancePerPoint = 0.5f;

    [Header("Itens Coletveis")]
    public List<ItemSpawnData> collectableItems;

    [Header("Decorao do Cenrio")]
    public List<ItemSpawnData> decorationItems;

    [Header("Armadilhas")]
    public List<ItemSpawnData> trapItems;

    GameObject GetRandomItem(List<ItemSpawnData> list)
    {
        if (list == null || list.Count == 0)
            return null;

        float total = list.Sum(i => i.chance);
        float random = Random.value * total;

        float current = 0f;
        foreach (var item in list)
        {
            current += item.chance;
            if (random <= current)
                return item.prefab;
        }

        return null;
    }

    public void SpawnItems()
    {
        if (collectableItems == null || collectableItems.Count == 0)
        {
            collectableItems = new List<ItemSpawnData>();
            GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>("SpawnItems");
            foreach (var prefab in loadedPrefabs)
            {
                if (prefab != null)
                {
                    collectableItems.Add(new ItemSpawnData { prefab = prefab, chance = 1.0f });
                }
            }
            Debug.Log($"[ItemSpawner] Auto-loaded {collectableItems.Count} prefabs from Resources/SpawnItems.");
        }

        var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        foreach (var point in spawnPoints)
        {
            if (Random.value > spawnChancePerPoint)
                continue;

            // Junta tudo numa lista temporria
            List<ItemSpawnData> allItems = new();
            allItems.AddRange(collectableItems);
            allItems.AddRange(decorationItems);
            allItems.AddRange(trapItems);

            GameObject item = GetRandomItem(allItems);

            if (item != null)
            {
                // Snap no chão antes e depois do spawn para garantir que nada spawne enterrado
                Vector3 spawnPos = point.transform.position;
                if (Physics.Raycast(spawnPos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = new Vector3(spawnPos.x, hit.point.y + 0.15f, spawnPos.z);
                }

                GameObject spawned = Instantiate(item, spawnPos, Quaternion.identity);
                if (collectableItems != null && collectableItems.Any(ci => ci.prefab == item))
                {
                    ItemPickup pickup = spawned.GetComponent<ItemPickup>();
                    if (pickup == null) pickup = spawned.AddComponent<ItemPickup>();
                    pickup.EnsureItemData();
                    pickup.InitializeItem();
                }
            }
        }
    }
}






//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//[System.Serializable]
//public class ItemSpawnData
//{
//    public GameObject itemPrefab;
//    [Range(0f, 1f)]
//    public float chance;
//}


//public class ItemSpawner : MonoBehaviour
//{
//    public float spawnChancePerPoint = 0.5f;
//    public List<ItemSpawnData> possibleItems;


//    GameObject GetRandomItem()
//    {
//        float total = possibleItems.Sum(i => i.chance);
//        float random = Random.value * total;

//        float current = 0f;
//        foreach (var item in possibleItems)
//        {
//            current += item.chance;
//            if (random <= current)
//                return item.itemPrefab;
//        }

//        return null;
//    }

//    public void SpawnItems()
//    {
//        var spawnPoints = GameObject.FindGameObjectsWithTag("ItemSpawnPoint");

//        foreach (var point in spawnPoints)
//        {
//            if (Random.value > spawnChancePerPoint)
//                continue;

//            GameObject item = GetRandomItem();
//            if (item != null)
//                Instantiate(item, point.transform.position, Quaternion.identity);
//        }
//    }
//}
