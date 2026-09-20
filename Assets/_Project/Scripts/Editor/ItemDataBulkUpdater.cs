using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class ItemDataBulkUpdater
{
    [MenuItem("Tools/Update Item ReturnsToBase")]
    public static void UpdateItems()
    {
        string[] environmentalItems = new string[] {
            "caracol_geodo", "cogumelo_oceano", "cristal_explosivo", "fibra", "fibra_silenciosa",
            "geodo", "larva_lapidadora", "lithos", "melocactus", "musgo_bioluminescente",
            "ovo_cristal_crawler", "peixe_lagoa", "po_de_cristal", "prismalita",
            "quartzo_basico", "vagalume_cristalizado", "resta_geobionte"
        };
        
        HashSet<string> envSet = new HashSet<string>(environmentalItems);
        
        string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_Project/Items_and_Crafting/Resources/ItemData" });
        int updated = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (data != null)
            {
                bool shouldReturn = envSet.Contains(data.name);
                if (data.returnsToBase != shouldReturn)
                {
                    data.returnsToBase = shouldReturn;
                    EditorUtility.SetDirty(data);
                    updated++;
                }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Updated returnsToBase on " + updated + " items.");
    }
}
