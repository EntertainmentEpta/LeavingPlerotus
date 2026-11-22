using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ferramenta de Editor para posicionar automaticamente o GameModeTerminal ao lado da porta de saída (StartRunDoor) na Base.
/// Menu: Tools/Leaving Plerotus/Setup Mode Terminal na Base
/// </summary>
public static class CreateGameModeTerminalTool
{
    private const string TerminalObjectName = "GameModeTerminal";
    private const string MaterialsFolder = "Assets/_Project/Materials/ModeTerminal";

    [MenuItem("Tools/Leaving Plerotus/Setup Mode Terminal na Base")]
    public static void SetupModeTerminalInBase()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            EditorUtility.DisplayDialog(
                "Setup Mode Terminal",
                "Nenhuma cena válida está aberta. Abra a cena Base.unity primeiro.",
                "OK");
            return;
        }

        // Verifica se já existe um GameModeTerminal na cena aberta
        GameModeTerminalInteraction existing = Object.FindFirstObjectByType<GameModeTerminalInteraction>(FindObjectsInactive.Include);
        if (existing != null)
        {
            bool recreate = EditorUtility.DisplayDialog(
                "GameModeTerminal já existe",
                $"Já existe um '{existing.gameObject.name}' nesta cena.\n\nDeseja recriar do zero ao lado da porta de saída?",
                "Recriar",
                "Manter e Selecionar");

            if (!recreate)
            {
                Selection.activeGameObject = existing.gameObject;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // Localiza a porta de saída da Base (componente StartRunDoor de StartDoorRun.cs)
        StartRunDoor startDoor = Object.FindFirstObjectByType<StartRunDoor>(FindObjectsInactive.Include);
        Transform doorTransform = startDoor != null ? startDoor.transform : FindFallbackDoorTransform();

        Vector3 spawnPos;
        Quaternion spawnRot;

        if (doorTransform != null)
        {
            spawnPos = ResolvePositionNextToDoor(doorTransform);
            Vector3 lookDir = -doorTransform.forward;
            lookDir.y = 0f;
            spawnRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                : Quaternion.identity;
        }
        else
        {
            Debug.LogWarning("[Setup Mode Terminal] StartRunDoor não encontrado na cena aberta. Criando GameModeTerminal na origem (0, 0, 0).");
            spawnPos = Vector3.zero;
            spawnRot = Quaternion.identity;
        }

        // Cria materiais persistentes compatíveis com URP (evita perda ao salvar/reabrir a cena)
        Material bodyMat = LoadOrCreateMaterial("ModeTerminal_Body.mat", new Color(0.11f, 0.14f, 0.17f, 1f), false);
        Material trimMat = LoadOrCreateMaterial("ModeTerminal_Trim.mat", new Color(0.20f, 0.25f, 0.30f, 1f), false);
        Material screenMat = LoadOrCreateMaterial("ModeTerminal_StatusScreen.mat", new Color(0.20f, 0.85f, 0.88f, 1f), true);

        // Cria raiz do Terminal
        GameObject root = new GameObject(TerminalObjectName);
        Undo.RegisterCreatedObjectUndo(root, "Create GameModeTerminal");
        root.transform.position = spawnPos;
        root.transform.rotation = spawnRot;

        // Visual placeholder limpo de console sci-fi
        GameObject visualRoot = new GameObject("ConsoleVisual");
        visualRoot.transform.SetParent(root.transform, false);

        // 1. Pedestal base
        GameObject pedestal = CreatePrimitiveChild("BasePedestal", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0f, 0.12f, 0f), new Vector3(1.15f, 0.24f, 0.85f), Quaternion.identity, bodyMat, keepCollider: true);

        // 2. Coluna principal do console
        CreatePrimitiveChild("ConsolePillar", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0f, 0.78f, -0.05f), new Vector3(0.72f, 1.12f, 0.48f), Quaternion.identity, bodyMat, keepCollider: true);

        // 3. Moldura inclinada da tela
        CreatePrimitiveChild("ScreenBezel", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0f, 1.48f, 0.08f), new Vector3(0.96f, 0.62f, 0.14f), Quaternion.Euler(22f, 0f, 0f), trimMat, keepCollider: false);

        // 4. Tela emissiva de status (muda entre ciano Normal e laranja Endless)
        GameObject statusScreen = CreatePrimitiveChild("StatusScreen", PrimitiveType.Cube, visualRoot.transform,
            new Vector3(0f, 1.49f, 0.14f), new Vector3(0.84f, 0.50f, 0.04f), Quaternion.Euler(22f, 0f, 0f), screenMat, keepCollider: false);

        // 5. Barra/farol superior de status
        GameObject statusBeacon = CreatePrimitiveChild("StatusBeacon", PrimitiveType.Cylinder, visualRoot.transform,
            new Vector3(0f, 1.88f, -0.02f), new Vector3(0.22f, 0.07f, 0.22f), Quaternion.identity, screenMat, keepCollider: false);

        // 6. Luz PointLight de status (ciano por padrão)
        GameObject lightGo = new GameObject("StatusLight");
        lightGo.transform.SetParent(visualRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.05f, 0.25f);
        Light statusLight = lightGo.AddComponent<Light>();
        statusLight.type = LightType.Point;
        statusLight.color = new Color(0.20f, 0.85f, 0.88f, 1f);
        statusLight.range = 6.0f;
        statusLight.intensity = 3.2f;

        // Trigger BoxCollider de interação (>= 3.5 x 2.5 x 3.5)
        BoxCollider triggerBox = root.AddComponent<BoxCollider>();
        triggerBox.isTrigger = true;
        triggerBox.size = new Vector3(3.6f, 2.6f, 3.6f);
        triggerBox.center = new Vector3(0f, 1.3f, 0f);

        // Anexa GameModeTerminalInteraction e conecta referências de luz e emissão
        GameModeTerminalInteraction interaction = root.AddComponent<GameModeTerminalInteraction>();
        interaction.actionText = "Interagir";
        interaction.statusLight = statusLight;
        interaction.statusEmissionRenderers = new Renderer[]
        {
            statusScreen.GetComponent<Renderer>(),
            statusBeacon.GetComponent<Renderer>()
        };

        // Garante que o pedestal não tenha collider marcado como trigger
        if (pedestal != null)
        {
            Collider pedCol = pedestal.GetComponent<Collider>();
            if (pedCol != null) pedCol.isTrigger = false;
        }

        // Seleciona, foca na Scene View e marca a cena como dirty
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }

        EditorSceneManager.MarkSceneDirty(activeScene);

        string doorInfo = doorTransform != null
            ? $"ao lado de '{doorTransform.name}' em {spawnPos}"
            : "na origem (StartRunDoor não encontrado)";

        Debug.Log($"[Setup Mode Terminal] ✅ GameModeTerminal criado com sucesso {doorInfo}! Salve a cena (Ctrl+S).");
    }

    private static Transform FindFallbackDoorTransform()
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string n = all[i].name.ToLower();
            if (n.Contains("startdoor") || n.Contains("startrundoor") || n.Contains("door_run") || n.Contains("portarun"))
            {
                return all[i].transform;
            }
        }
        return null;
    }

    private static Vector3 ResolvePositionNextToDoor(Transform door)
    {
        // Tenta posicionar à direita e ligeiramente recuado da porta; se houver obstáculo, tenta à esquerda
        Vector3 rightCandidate = door.position + door.right * 3.0f - door.forward * 1.1f;
        Vector3 leftCandidate = door.position - door.right * 3.0f - door.forward * 1.1f;

        Vector3 chosen = Physics.CheckSphere(rightCandidate + Vector3.up * 1.0f, 0.55f, ~0, QueryTriggerInteraction.Ignore)
            ? leftCandidate
            : rightCandidate;

        // Ajusta Y ao chão sólido abaixo
        if (Physics.Raycast(chosen + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            chosen.y = hit.point.y;
        }

        return chosen;
    }

    private static GameObject CreatePrimitiveChild(
        string name,
        PrimitiveType type,
        Transform parent,
        Vector3 localPos,
        Vector3 localScale,
        Quaternion localRot,
        Material mat,
        bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;

        if (!keepCollider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.sharedMaterial = mat;
        }

        return go;
    }

    private static Material LoadOrCreateMaterial(string fileName, Color color, bool emissive)
    {
        EnsureFolderExists(MaterialsFolder);
        string assetPath = $"{MaterialsFolder}/{fileName}";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", emissive ? 0.1f : 0.45f);

        if (emissive && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.6f);
        }

        AssetDatabase.CreateAsset(mat, assetPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
