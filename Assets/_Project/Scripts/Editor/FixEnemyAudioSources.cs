using UnityEngine;
using UnityEditor;

public class FixEnemyAudioSources
{
    [MenuItem("Tools/Fix Enemy AudioSources")]
    public static void FixAllEnemies()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Enemies" });
        int fixedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                // Verifica se tem algum script de AI (qualquer coisa com _AI no nome ou DummyHealth)
                bool hasAI = prefab.GetComponentInChildren<DummyHealth>() != null;
                if (hasAI)
                {
                    AudioSource audio = prefab.GetComponent<AudioSource>();
                    if (audio == null)
                    {
                        audio = prefab.AddComponent<AudioSource>();
                        audio.spatialBlend = 1f; // 3D sound!
                        audio.minDistance = 5f;
                        audio.maxDistance = 20f;
                        EditorUtility.SetDirty(prefab);
                        fixedCount++;
                        Debug.Log("Added AudioSource to " + prefab.name);
                    }
                }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Finished fixing " + fixedCount + " enemy prefabs.");
    }
}
