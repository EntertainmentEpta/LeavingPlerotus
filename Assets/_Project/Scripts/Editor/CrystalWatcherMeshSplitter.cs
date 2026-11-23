#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ferramenta de Editor para separar o mesh do Crystal Watcher em 2 objetos independentes:
/// - Body (estrutura externa + pedras periféricas)
/// - CoreCrystal (cristal central flutuante com pivô centralizado e escala/rotação limpas 1,1,1 / 0,0,0)
/// Preserva 100% das UVs, normais, tangentes e material original.
/// </summary>
public static class CrystalWatcherMeshSplitter
{
    private const string FbxPath = "Assets/_Project/Enemies/CrystalWatcher/Meshy_AI_Crystal_Rock_Monster_0704142804_texture_fbx/Meshy_AI_Crystal_Rock_Monster_0704142804_texture.fbx";
    private const string PrefabPath = "Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab";
    private const string OutputFolder = "Assets/_Project/Enemies/CrystalWatcher/SeparatedMeshes";
    private const string BodyMeshPath = OutputFolder + "/CrystalWatcher_Body.asset";
    private const string CoreMeshPath = OutputFolder + "/CrystalWatcher_CoreCrystal.asset";

    [MenuItem("Tools/Crystal Watcher/Separar Cristal Central (2 Objetos)")]
    public static void SplitCrystalWatcher()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[CrystalWatcherMeshSplitter] FBX não encontrado em: {FbxPath}");
            return;
        }

        bool wasReadable = importer.isReadable;
        if (!wasReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        try
        {
            Mesh sourceMesh = LoadMainMeshFromFbx(FbxPath);
            if (sourceMesh == null)
            {
                Debug.LogError("[CrystalWatcherMeshSplitter] Nenhum Mesh encontrado dentro do FBX do Crystal Watcher.");
                return;
            }

            // Lê a transformação local usada atualmente no prefab para assar escala/rotação limpas (1,1,1 / 0,0,0)
            Vector3 modelLocalPos = new Vector3(0f, 1.5f, 0f);
            Quaternion modelLocalRot = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 modelLocalScale = new Vector3(100f, 100f, 100f);
            Material[] sharedMats = null;

            GameObject prefabPreview = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabPreview != null)
            {
                MeshRenderer existingMr = prefabPreview.GetComponentInChildren<MeshRenderer>(true);
                if (existingMr != null)
                {
                    sharedMats = existingMr.sharedMaterials;
                    Transform t = existingMr.transform;
                    // Só lê rotação/escala do filho se ainda for o FBX original (garante idempotência se rodar 2x)
                    if (t.name.StartsWith("Meshy_AI_Crystal_Rock_Monster") && t.parent == prefabPreview.transform)
                    {
                        modelLocalPos = t.localPosition;
                        modelLocalRot = t.localRotation;
                        modelLocalScale = t.localScale;
                    }
                    else if (t.parent != null && t.parent.name.StartsWith("Meshy_AI_Crystal_Rock_Monster") && t.parent.parent == prefabPreview.transform)
                    {
                        modelLocalPos = t.parent.localPosition;
                        modelLocalRot = t.parent.localRotation;
                        modelLocalScale = t.parent.localScale;
                    }
                }
            }

            // 1. Encontra todas as ilhas fisicamente desconectadas (Union-Find por triângulos + vértices coincidentes)
            Vector3[] srcVerts = sourceMesh.vertices;
            int vertCount = srcVerts.Length;
            int[] parent = new int[vertCount];
            for (int i = 0; i < vertCount; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            // Une vértices que compartilham triângulos
            int subMeshCount = sourceMesh.subMeshCount;
            List<int[]> subMeshTris = new List<int[]>(subMeshCount);
            for (int s = 0; s < subMeshCount; s++)
            {
                int[] tris = sourceMesh.GetTriangles(s);
                subMeshTris.Add(tris);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Union(tris[i], tris[i + 1]);
                    Union(tris[i], tris[i + 2]);
                }
            }

            // Solda vértices duplicados por costura de UV (mesma posição 3D)
            float weldScale = 1f / Mathf.Max(1e-5f, sourceMesh.bounds.size.magnitude * 0.0005f);
            Dictionary<Vector3Int, int> posMap = new Dictionary<Vector3Int, int>(vertCount);
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 v = srcVerts[i];
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(v.x * weldScale),
                    Mathf.RoundToInt(v.y * weldScale),
                    Mathf.RoundToInt(v.z * weldScale)
                );
                if (posMap.TryGetValue(key, out int existingIdx))
                    Union(i, existingIdx);
                else
                    posMap[key] = i;
            }

            // Agrupa vértices por ilha
            Dictionary<int, List<int>> islands = new Dictionary<int, List<int>>();
            for (int i = 0; i < vertCount; i++)
            {
                int root = Find(i);
                if (!islands.TryGetValue(root, out List<int> list))
                {
                    list = new List<int>();
                    islands[root] = list;
                }
                list.Add(i);
            }

            if (islands.Count < 2)
            {
                Debug.LogError("[CrystalWatcherMeshSplitter] O mesh só possui 1 ilha conectada; não foi possível separar automaticamente.");
                return;
            }

            // Identifica a maior ilha (casca externa) e a ilha central (menor ilha mais próxima do centro global)
            int largestRoot = -1;
            int largestCount = -1;
            Vector3 fullCenter = sourceMesh.bounds.center;

            foreach (var kv in islands)
            {
                if (kv.Value.Count > largestCount)
                {
                    largestCount = kv.Value.Count;
                    largestRoot = kv.Key;
                }
            }

            int coreRoot = -1;
            float bestDistToCenter = float.MaxValue;
            foreach (var kv in islands)
            {
                Bounds b = ComputeBounds(srcVerts, kv.Value);
                float dist = Vector3.Distance(b.center, fullCenter);
                Debug.Log($"[CrystalWatcherMeshSplitter] Ilha root={kv.Key} | verts={kv.Value.Count} | center={b.center:F4} | distCentro={dist:F4}");

                if (kv.Key == largestRoot || kv.Value.Count < 20) continue;
                if (dist < bestDistToCenter)
                {
                    bestDistToCenter = dist;
                    coreRoot = kv.Key;
                }
            }

            if (coreRoot == -1)
            {
                Debug.LogError("[CrystalWatcherMeshSplitter] Não foi possível identificar o cristal central.");
                return;
            }

            // 2. Gera os 2 meshes com rotação (-90,0,0) e escala (100,100,100) assadas para pivô limpo (1,1,1 / 0,0,0)
            Vector3 bakedCoreCenter;
            Mesh bodyMesh = BuildBakedMesh(sourceMesh, subMeshTris, Find, coreRoot, includeMatchingRoot: false, modelLocalRot, modelLocalScale, centerPivot: false, out _);
            Mesh coreMesh = BuildBakedMesh(sourceMesh, subMeshTris, Find, coreRoot, includeMatchingRoot: true, modelLocalRot, modelLocalScale, centerPivot: true, out bakedCoreCenter);

            bodyMesh.name = "CrystalWatcher_Body";
            coreMesh.name = "CrystalWatcher_CoreCrystal";

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
                AssetDatabase.Refresh();
            }

            SaveOrReplaceMeshAsset(bodyMesh, BodyMeshPath);
            SaveOrReplaceMeshAsset(coreMesh, CoreMeshPath);

            Mesh savedBodyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            Mesh savedCoreMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CoreMeshPath);

            // 3. Atualiza o CrystalWatcher.prefab automaticamente com Body + CoreCrystal
            UpdatePrefab(PrefabPath, savedBodyMesh, savedCoreMesh, sharedMats, modelLocalPos, bakedCoreCenter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CrystalWatcherMeshSplitter] Sucesso! Meshes salvos em '{OutputFolder}' e prefab '{PrefabPath}' atualizado com 2 objetos: Body ({savedBodyMesh.vertexCount} verts) e CoreCrystal ({savedCoreMesh.vertexCount} verts).");
        }
        finally
        {
            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
    }

    private static Mesh LoadMainMeshFromFbx(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Mesh best = null;
        foreach (Object obj in assets)
        {
            if (obj is Mesh m && (best == null || m.vertexCount > best.vertexCount))
                best = m;
        }
        return best;
    }

    private static Bounds ComputeBounds(Vector3[] verts, List<int> indices)
    {
        Bounds b = new Bounds(verts[indices[0]], Vector3.zero);
        for (int i = 1; i < indices.Count; i++)
            b.Encapsulate(verts[indices[i]]);
        return b;
    }

    private static Mesh BuildBakedMesh(
        Mesh source,
        List<int[]> subMeshTris,
        System.Func<int, int> findRoot,
        int targetRoot,
        bool includeMatchingRoot,
        Quaternion bakeRot,
        Vector3 bakeScale,
        bool centerPivot,
        out Vector3 pivotOffset)
    {
        Vector3[] srcVerts = source.vertices;
        Vector3[] srcNormals = source.normals;
        Vector4[] srcTangents = source.tangents;
        Vector2[] srcUv = source.uv;
        Vector2[] srcUv2 = source.uv2;
        Color[] srcColors = source.colors;

        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;
        bool hasUv = srcUv != null && srcUv.Length == srcVerts.Length;
        bool hasUv2 = srcUv2 != null && srcUv2.Length == srcVerts.Length;
        bool hasColors = srcColors != null && srcColors.Length == srcVerts.Length;

        Dictionary<int, int> oldToNew = new Dictionary<int, int>();
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNormals = hasNormals ? new List<Vector3>() : null;
        List<Vector4> newTangents = hasTangents ? new List<Vector4>() : null;
        List<Vector2> newUv = hasUv ? new List<Vector2>() : null;
        List<Vector2> newUv2 = hasUv2 ? new List<Vector2>() : null;
        List<Color> newColors = hasColors ? new List<Color>() : null;

        List<List<int>> newSubTris = new List<List<int>>(subMeshTris.Count);

        int MapVertex(int oldIdx)
        {
            if (oldToNew.TryGetValue(oldIdx, out int mapped))
                return mapped;

            int newIdx = newVerts.Count;
            oldToNew[oldIdx] = newIdx;

            Vector3 bakedPos = bakeRot * Vector3.Scale(srcVerts[oldIdx], bakeScale);
            newVerts.Add(bakedPos);

            if (hasNormals) newNormals.Add((bakeRot * srcNormals[oldIdx]).normalized);
            if (hasTangents)
            {
                Vector4 t = srcTangents[oldIdx];
                Vector3 bt = (bakeRot * new Vector3(t.x, t.y, t.z)).normalized;
                newTangents.Add(new Vector4(bt.x, bt.y, bt.z, t.w));
            }
            if (hasUv) newUv.Add(srcUv[oldIdx]);
            if (hasUv2) newUv2.Add(srcUv2[oldIdx]);
            if (hasColors) newColors.Add(srcColors[oldIdx]);

            return newIdx;
        }

        for (int s = 0; s < subMeshTris.Count; s++)
        {
            int[] tris = subMeshTris[s];
            List<int> dstTris = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                bool matches = findRoot(tris[i]) == targetRoot;
                if (matches != includeMatchingRoot) continue;

                dstTris.Add(MapVertex(tris[i]));
                dstTris.Add(MapVertex(tris[i + 1]));
                dstTris.Add(MapVertex(tris[i + 2]));
            }
            newSubTris.Add(dstTris);
        }

        pivotOffset = Vector3.zero;
        if (centerPivot && newVerts.Count > 0)
        {
            Bounds b = new Bounds(newVerts[0], Vector3.zero);
            for (int i = 1; i < newVerts.Count; i++) b.Encapsulate(newVerts[i]);
            pivotOffset = b.center;
            for (int i = 0; i < newVerts.Count; i++) newVerts[i] -= pivotOffset;
        }

        Mesh m = new Mesh();
        if (newVerts.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(newVerts);
        if (hasNormals) m.SetNormals(newNormals);
        if (hasTangents) m.SetTangents(newTangents);
        if (hasUv) m.SetUVs(0, newUv);
        if (hasUv2) m.SetUVs(1, newUv2);
        if (hasColors) m.SetColors(newColors);

        m.subMeshCount = newSubTris.Count;
        for (int s = 0; s < newSubTris.Count; s++)
            m.SetTriangles(newSubTris[s], s);

        m.RecalculateBounds();
        if (!hasNormals) m.RecalculateNormals();
        return m;
    }

    private static void SaveOrReplaceMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
    }

    private static void UpdatePrefab(
        string prefabPath,
        Mesh bodyMesh,
        Mesh coreMesh,
        Material[] sharedMats,
        Vector3 modelLocalPos,
        Vector3 bakedCoreCenter)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            // Remove o filho FBX antigo ou contêiner anterior se já existir
            List<GameObject> toRemove = new List<GameObject>();
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name.StartsWith("Meshy_AI_Crystal_Rock_Monster") || child.name == "CrystalWatcher_Model")
                    toRemove.Add(child.gameObject);
            }
            foreach (GameObject go in toRemove)
                Object.DestroyImmediate(go);

            // Container limpo na posição exata do modelo
            GameObject modelContainer = new GameObject("CrystalWatcher_Model");
            modelContainer.transform.SetParent(root.transform, false);
            modelContainer.transform.localPosition = modelLocalPos;
            modelContainer.transform.localRotation = Quaternion.identity;
            modelContainer.transform.localScale = Vector3.one;

            // Objeto 1: Corpo externo
            GameObject bodyObj = new GameObject("Body");
            bodyObj.layer = root.layer;
            bodyObj.transform.SetParent(modelContainer.transform, false);
            bodyObj.transform.localPosition = Vector3.zero;
            bodyObj.transform.localRotation = Quaternion.identity;
            bodyObj.transform.localScale = Vector3.one;

            MeshFilter bodyMf = bodyObj.AddComponent<MeshFilter>();
            bodyMf.sharedMesh = bodyMesh;
            MeshRenderer bodyMr = bodyObj.AddComponent<MeshRenderer>();
            if (sharedMats != null && sharedMats.Length > 0) bodyMr.sharedMaterials = sharedMats;

            // Objeto 2: Cristal central com pivô no centro exato dele (pronto para girar/flutuar/pulsar)
            GameObject coreObj = new GameObject("CoreCrystal");
            coreObj.layer = root.layer;
            coreObj.transform.SetParent(modelContainer.transform, false);
            coreObj.transform.localPosition = bakedCoreCenter;
            coreObj.transform.localRotation = Quaternion.identity;
            coreObj.transform.localScale = Vector3.one;

            MeshFilter coreMf = coreObj.AddComponent<MeshFilter>();
            coreMf.sharedMesh = coreMesh;
            MeshRenderer coreMr = coreObj.AddComponent<MeshRenderer>();
            if (sharedMats != null && sharedMats.Length > 0) coreMr.sharedMaterials = sharedMats;

            // Conecta automaticamente as referências no CrystalWatcher_AI
            CrystalWatcher_AI ai = root.GetComponent<CrystalWatcher_AI>();
            if (ai != null)
            {
                ai.coreCrystalTransform = coreObj.transform;
                ai.outerBodyTransform = bodyObj.transform;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
