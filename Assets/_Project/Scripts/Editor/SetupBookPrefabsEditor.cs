#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Cria e configura automaticamente os prefabs do Caderno do Pesquisador:
/// 1. ResearcherBookPickup (o item no chão da dungeon para coletar)
/// 2. FloatingBookPedestal (a estante/pedestal com o livro flutuante estilo Minecraft no laboratório)
/// </summary>
public class SetupBookPrefabsEditor : EditorWindow
{
    [InitializeOnLoadMethod]
    public static void CheckAndAutoCreatePrefabs()
    {
        EditorApplication.delayCall += () =>
        {
            string p1 = "Assets/_Project/Prefabs/Lore/ResearcherBookPickup.prefab";
            string p2 = "Assets/_Project/Prefabs/Lore/FloatingBookPedestal.prefab";
            if (!File.Exists(p1) || !File.Exists(p2))
            {
                CreateAllBookPrefabs(silent: true);
            }
        };
    }

    [MenuItem("RogueLike/Caderno/Criar Prefabs do Livro (Chão e Estante)")]
    public static void MenuCreateAllBookPrefabs()
    {
        CreateAllBookPrefabs(silent: false);
    }

    [MenuItem("RogueLike/Caderno/Instanciar Estante Flutuante na Cena Atual")]
    public static void SpawnPedestalInCurrentScene()
    {
        string path = "Assets/_Project/Prefabs/Lore/FloatingBookPedestal.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            CreateAllBookPrefabs(silent: true);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        if (prefab != null)
        {
            Vector3 pos = Vector3.zero;
            Camera sceneCam = SceneView.lastActiveSceneView?.camera;
            if (sceneCam != null)
            {
                pos = sceneCam.transform.position + sceneCam.transform.forward * 3.5f;
                pos.y = 0f;
            }
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = pos;
            Selection.activeGameObject = inst;
            Undo.RegisterCreatedObjectUndo(inst, "Instanciar FloatingBookPedestal");
            Debug.Log($"[BOOK SETUP] 📖 FloatingBookPedestal instanciado com sucesso em {pos}!");
        }
    }

    [MenuItem("RogueLike/Caderno/Testar Interface do Caderno (Abrir ou Fechar UI)")]
    public static void TestOpenJournalUI()
    {
        JournalBookUI ui = JournalBookUI.CreateOrGet();
        if (ui != null)
        {
            if (ui.IsOpen) ui.CloseJournal();
            else ui.OpenJournal();
        }
    }

    public static void CreateAllBookPrefabs(bool silent = false)
    {
        // 1. Garante que os diretórios existam
        EnsureFolder("Assets/_Project/Prefabs/Lore");
        EnsureFolder("Assets/_Project/Resources/Lore");

        // 2. Carrega modelo 3D FBX e Material
        string fbxPath = "Assets/_Project/Enviroment/Map/vintage-book/source/Book.fbx";
        GameObject bookFbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (bookFbx == null)
        {
            Debug.LogError($"[BOOK SETUP] FBX não encontrado em {fbxPath}!");
            return;
        }

        string userPrefabPath = "Assets/_Project/Enviroment/Map/Book.prefab";
        GameObject bookSource = AssetDatabase.LoadAssetAtPath<GameObject>(userPrefabPath) ?? bookFbx;

        string matPath = "Assets/_Project/Enviroment/Map/vintage-book/Book_Mat.mat";
        Material bookMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        // ─── 3. PREFAB 1: RESEARCHER BOOK PICKUP (Chão da Dungeon) ───
        GameObject pickupRoot = new GameObject("ResearcherBookPickup");
        ResearcherBookPickup pickupComp = pickupRoot.AddComponent<ResearcherBookPickup>();

        // Colisor Trigger
        SphereCollider col = pickupRoot.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2.5f;
        col.center = new Vector3(0f, 0.2f, 0f);

        // Modelo 3D do Livro pousado no chão
        GameObject bookModel = (GameObject)PrefabUtility.InstantiatePrefab(bookSource, pickupRoot.transform);
        bookModel.name = "BookModel";
        bookModel.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        bookModel.transform.localRotation = Quaternion.Euler(0f, Random.Range(-45f, 45f), 0f);
        bookModel.transform.localScale = Vector3.one * 40f;

        if (bookMat != null)
        {
            foreach (var r in bookModel.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = bookMat;
            }
        }

        // Luz dourada de destaque no chão
        GameObject lightObj = new GameObject("PickupGlowLight");
        lightObj.transform.SetParent(pickupRoot.transform, false);
        lightObj.transform.localPosition = Vector3.up * 0.4f;
        Light pLight = lightObj.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(1.0f, 0.85f, 0.35f);
        pLight.range = 3.2f;
        pLight.intensity = 1.6f;

        // Partículas suaves
        GameObject psObj = new GameObject("GlowParticles");
        psObj.transform.SetParent(pickupRoot.transform, false);
        psObj.transform.localPosition = Vector3.up * 0.15f;
        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(1.0f, 0.9f, 0.4f, 0.8f);
        main.startSize = 0.2f;
        main.startSpeed = 0.3f;
        main.startLifetime = 1.5f;
        main.maxParticles = 20;
        var emission = ps.emission;
        emission.rateOverTime = 8;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;

        // Salva Prefab do Pickup
        string pickupPrefabPath = "Assets/_Project/Prefabs/Lore/ResearcherBookPickup.prefab";
        string pickupResourcePath = "Assets/_Project/Resources/Lore/ResearcherBookPickup.prefab";
        PrefabUtility.SaveAsPrefabAsset(pickupRoot, pickupPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(pickupRoot, pickupResourcePath);
        DestroyImmediate(pickupRoot);
        Debug.Log($"[BOOK SETUP] ✅ Prefab de Coleta salvo em {pickupPrefabPath}");

        // ─── 4. PREFAB 2: FLOATING BOOK PEDESTAL (Estante no Laboratório) ───
        GameObject pedestalRoot = new GameObject("FloatingBookPedestal");
        FloatingBookDisplay displayComp = pedestalRoot.AddComponent<FloatingBookDisplay>();

        // Estante / Pedestal 3D de suporte
        GameObject standGeo = CreatePedestalGeometry(pedestalRoot.transform);

        // Modelo 3D do Livro Flutuante
        GameObject floatBook = (GameObject)PrefabUtility.InstantiatePrefab(bookSource, pedestalRoot.transform);
        floatBook.name = "FloatingBook";
        floatBook.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        floatBook.transform.localRotation = Quaternion.Euler(20f, 0f, 10f);
        floatBook.transform.localScale = Vector3.one * 42f;

        if (bookMat != null)
        {
            foreach (var r in floatBook.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = bookMat;
            }
        }

        // Luz ciano/dourada mística no livro
        GameObject bookLightObj = new GameObject("EnchantmentGlowLight");
        bookLightObj.transform.SetParent(floatBook.transform, false);
        bookLightObj.transform.localPosition = Vector3.up * 0.2f;
        Light bLight = bookLightObj.AddComponent<Light>();
        bLight.type = LightType.Point;
        bLight.color = new Color(0.35f, 0.9f, 1.0f);
        bLight.range = 3.5f;
        bLight.intensity = 2.0f;

        // Partículas estilo Minecraft enchantment table (runas subindo)
        GameObject enchantPsObj = new GameObject("EnchantmentRunes");
        enchantPsObj.transform.SetParent(pedestalRoot.transform, false);
        enchantPsObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        ParticleSystem eps = enchantPsObj.AddComponent<ParticleSystem>();
        var emain = eps.main;
        emain.startColor = new Color(0.3f, 0.95f, 1.0f, 0.85f);
        emain.startSize = 0.15f;
        emain.startSpeed = 0.4f;
        emain.startLifetime = 1.8f;
        emain.maxParticles = 25;
        var eemission = eps.emission;
        eemission.rateOverTime = 12;
        var eshape = eps.shape;
        eshape.shapeType = ParticleSystemShapeType.Circle;
        eshape.radius = 0.6f;

        // Configura referências no script
        displayComp.bookModel = floatBook.transform;
        displayComp.bookPointLight = bLight;
        displayComp.sparkleVfx = enchantPsObj;
        displayComp.rotationSpeed = 32f;
        displayComp.facePlayerSpeed = 4.2f;
        displayComp.bobSpeed = 2.2f;
        displayComp.bobHeight = 0.08f;
        displayComp.interactionDistance = 2.8f;

        // Salva Prefab da Estante Flutuante
        string pedestalPrefabPath = "Assets/_Project/Prefabs/Lore/FloatingBookPedestal.prefab";
        string pedestalResourcePath = "Assets/_Project/Resources/Lore/FloatingBookPedestal.prefab";
        PrefabUtility.SaveAsPrefabAsset(pedestalRoot, pedestalPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(pedestalRoot, pedestalResourcePath);
        DestroyImmediate(pedestalRoot);
        Debug.Log($"[BOOK SETUP] ✅ Prefab do Pedestal Flutuante salvo em {pedestalPrefabPath}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!silent)
        {
            EditorUtility.DisplayDialog("Caderno do Pesquisador", 
                "Prefabs criados com sucesso!\n\n" +
                "1. Assets/_Project/Prefabs/Lore/ResearcherBookPickup.prefab\n" +
                "2. Assets/_Project/Prefabs/Lore/FloatingBookPedestal.prefab\n\n" +
                "Agora você pode colocar o FloatingBookPedestal no Laboratório da Base!", "OK");
        }
    }

    private static GameObject CreatePedestalGeometry(Transform parent)
    {
        GameObject standRoot = new GameObject("StandGeometry");
        standRoot.transform.SetParent(parent, false);

        // Material metálico escuro/rocha para o pedestal
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material standMat = new Material(shader);
        standMat.color = new Color(0.12f, 0.13f, 0.16f);
        standMat.SetFloat("_Smoothness", 0.35f);
        standMat.SetFloat("_Metallic", 0.2f);

        // Base inferior
        GameObject baseStep = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseStep.name = "Pedestal_Base";
        baseStep.transform.SetParent(standRoot.transform, false);
        baseStep.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        baseStep.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
        baseStep.GetComponent<Renderer>().sharedMaterial = standMat;
        DestroyImmediate(baseStep.GetComponent<Collider>());

        // Coluna central
        GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        column.name = "Pedestal_Column";
        column.transform.SetParent(standRoot.transform, false);
        column.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        column.transform.localScale = new Vector3(0.55f, 0.44f, 0.55f);
        column.GetComponent<Renderer>().sharedMaterial = standMat;
        DestroyImmediate(column.GetComponent<Collider>());

        // Tampo / Estante superior
        GameObject topPlinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topPlinth.name = "Pedestal_Top";
        topPlinth.transform.SetParent(standRoot.transform, false);
        topPlinth.transform.localPosition = new Vector3(0f, 0.92f, 0f);
        topPlinth.transform.localScale = new Vector3(0.85f, 0.05f, 0.85f);
        topPlinth.GetComponent<Renderer>().sharedMaterial = standMat;

        // Mantém apenas o colisor do topo para colisão física no mundo
        topPlinth.GetComponent<Collider>().isTrigger = false;

        return standRoot;
    }

    private static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
#endif
