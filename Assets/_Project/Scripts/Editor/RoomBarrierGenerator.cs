using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor Tool: gera e repagina automaticamente as barreiras/bordas sólidas em todas as salas de combate.
/// 
/// O QUE FAZ:
///   1. Encontra todos os prefabs de sala com RoomController.
///   2. Em cada ConnectionPoint, cria uma barreira perfeitamente alinhada com BoxCollider sólido.
///   3. A barreira cobre toda a passagem (12m de largura x 8m de altura) do chão ao teto, impedindo o player de sair da sala antes do fim do combate.
///   4. Prepara o objeto filho "Barrier_Mesh" para receber malha/mesh 3D customizado quando o usuário enviar.
///   5. Adiciona e atualiza as barreiras na lista doors[] do RoomController.
///   6. Garante que salas sem trigger (ex: Zroom) recebam um Trigger Collider com RoomTriggerProxy para nunca falharem.
/// </summary>
public class RoomBarrierGenerator : EditorWindow
{

    [Header("Dimensões da Barreira")]
    public static float barrierWidth = 12.0f;
    public static float barrierHeight = 8.0f;
    public static float barrierThickness = 1.0f;

    [Header("Mesh & Material Customizados")]
    public Mesh customBarrierMesh;
    public Material customBarrierMaterial;

    [MenuItem("EPTA Tools/Gerar e Repaginar Bordas das Salas")]
    public static void GenerateBarriersMenu()
    {
        ExecuteGeneration(null, null, true);
    }



    [MenuItem("EPTA Tools/Configurar Malha das Bordas (Mesh e Altura)")]
    public static void OpenSettings()
    {
        GetWindow<RoomBarrierGenerator>("Configurar Bordas de Sala");
    }

    private void OnGUI()
    {
        GUILayout.Label("Configurações das Bordas / Barreiras de Sala", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Esta ferramenta gera barreiras sólidas em todos os ConnectionPoints das salas de combate.\n" +
            "Quando você tiver o Mesh novo das bordas, arraste-o abaixo e clique em 'Aplicar em Todos os Mapas'.",
            MessageType.Info);

        EditorGUILayout.Space();
        barrierWidth = EditorGUILayout.FloatField("Largura (X)", barrierWidth);
        barrierHeight = EditorGUILayout.FloatField("Altura (Y)", barrierHeight);
        barrierThickness = EditorGUILayout.FloatField("Espessura (Z)", barrierThickness);

        EditorGUILayout.Space();
        GUILayout.Label("Malha 3D Customizada (Mesh da Barreira)", EditorStyles.boldLabel);
        customBarrierMesh = (Mesh)EditorGUILayout.ObjectField("Novo Mesh da Borda", customBarrierMesh, typeof(Mesh), false);
        customBarrierMaterial = (Material)EditorGUILayout.ObjectField("Material da Borda", customBarrierMaterial, typeof(Material), false);

        EditorGUILayout.Space(15);
        if (GUILayout.Button("🔄 Gerar / Repaginar Todas as Bordas", GUILayout.Height(45)))
        {
            ExecuteGeneration(customBarrierMesh, customBarrierMaterial, true);
        }
    }

    public static void ExecuteGeneration(Mesh meshToAssign, Material matToAssign, bool showDialog = true)
    {
        string[] searchFolders = new string[] { "Assets/_Project/Enviroment/Map/New map" };
        string[] allPrefabGUIDs = AssetDatabase.FindAssets("t:Prefab", searchFolders);

        int prefabsProcessed = 0;
        int barriersCreated = 0;
        int triggersCreated = 0;

        foreach (string guid in allPrefabGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null) continue;

            RoomController rc = prefabAsset.GetComponentInChildren<RoomController>(true);
            if (rc == null) continue;

            // Pula salas puramente pacíficas (SafeRoom, Merchant, DeadEnd)
            if (rc.isSafeRoom || rc.roomCategory != RoomController.RoomCategory.Combat)
            {
                continue;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                GameObject root = scope.prefabContentsRoot;
                RoomController editRC = root.GetComponentInChildren<RoomController>(true);
                if (editRC == null) continue;

                ConnectionPoint[] cps = root.GetComponentsInChildren<ConnectionPoint>(true);
                if (cps.Length == 0) continue;

                List<GameObject> doorList = new List<GameObject>();

                // 1. Limpa barreiras antigas ou duplicadas
                RoomBarrier[] oldBarriers = root.GetComponentsInChildren<RoomBarrier>(true);
                foreach (RoomBarrier ob in oldBarriers)
                {
                    if (ob != null) DestroyImmediate(ob.gameObject);
                }

                // Remove filhos chamados "Barrier_*" que possam ter sobrado
                foreach (ConnectionPoint cp in cps)
                {
                    for (int i = cp.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = cp.transform.GetChild(i);
                        if (child.name.StartsWith("Barrier_"))
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }
                }

                // 2. Cria as novas barreiras perfeitamente ancoradas a cada ConnectionPoint
                foreach (ConnectionPoint cp in cps)
                {
                    string barrierName = $"Barrier_{cp.gameObject.name}";
                    GameObject barrierObj = new GameObject(barrierName);
                    barrierObj.transform.SetParent(cp.transform, false);
                    barrierObj.transform.localPosition = Vector3.zero;
                    barrierObj.transform.localRotation = Quaternion.identity;

                    // Compensa a escala herdada para que a escala no mundo seja sempre uniforme
                    Vector3 lossy = cp.transform.lossyScale;
                    barrierObj.transform.localScale = new Vector3(
                        lossy.x > 0 ? 1f / lossy.x : 1f,
                        lossy.y > 0 ? 1f / lossy.y : 1f,
                        lossy.z > 0 ? 1f / lossy.z : 1f
                    );

                    // BoxCollider sólido cobrindo o vão do chão (Y=0) até o teto (Y=8m)
                    BoxCollider col = barrierObj.AddComponent<BoxCollider>();
                    col.isTrigger = false;
                    col.size = new Vector3(barrierWidth, barrierHeight, barrierThickness);
                    col.center = new Vector3(0f, (barrierHeight * 0.5f) - cp.floorOffset, 0f);

                    // Componente RoomBarrier
                    RoomBarrier rb = barrierObj.AddComponent<RoomBarrier>();
                    rb.barrierCollider = col;

                    // 3. Objeto Visual (preparado para receber o novo Mesh 3D)
                    GameObject meshChild = new GameObject("Barrier_Mesh");
                    meshChild.transform.SetParent(barrierObj.transform, false);
                    meshChild.transform.localPosition = new Vector3(0f, (barrierHeight * 0.5f) - cp.floorOffset, 0f);
                    meshChild.transform.localRotation = Quaternion.identity;
                    meshChild.transform.localScale = new Vector3(barrierWidth, barrierHeight, 0.35f);

                    MeshFilter mf = meshChild.AddComponent<MeshFilter>();
                    MeshRenderer mr = meshChild.AddComponent<MeshRenderer>();

                    // Atribui mesh customizado se fornecido, ou um cubo padrão de gabarito
                    if (meshToAssign != null)
                    {
                        mf.sharedMesh = meshToAssign;
                        meshChild.transform.localScale = Vector3.one; // Preserva escala natural do mesh customizado
                    }
                    else
                    {
                        GameObject cubePrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        mf.sharedMesh = cubePrimitive.GetComponent<MeshFilter>().sharedMesh;
                        DestroyImmediate(cubePrimitive);
                    }

                    if (matToAssign != null)
                    {
                        mr.sharedMaterial = matToAssign;
                    }

                    rb.visualRoot = meshChild;
                    rb.meshFilter = mf;
                    rb.meshRenderer = mr;

                    // Começa inativo até a sala trancar no combate
                    barrierObj.SetActive(false);

                    doorList.Add(barrierObj);
                    barriersCreated++;
                }

                // 4. Garante que o RoomController tenha as portas registradas
                editRC.doors = doorList.ToArray();

                // 5. Verifica se a sala possui Trigger para detectar entrada do Player
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                bool hasTrigger = false;
                foreach (Collider c in colliders)
                {
                    if (c.isTrigger)
                    {
                        hasTrigger = true;
                        break;
                    }
                }

                // Se a sala não tem nenhum trigger (como o Zroom), cria um Combat Trigger central
                if (!hasTrigger)
                {
                    GameObject triggerObj = new GameObject("Room_CombatTrigger");
                    triggerObj.transform.SetParent(root.transform, false);
                    triggerObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);

                    BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
                    trigCol.isTrigger = true;
                    trigCol.size = new Vector3(25f, 8f, 25f);

                    RoomTriggerProxy proxy = triggerObj.AddComponent<RoomTriggerProxy>();
                    proxy.roomController = editRC;

                    triggersCreated++;
                    Debug.Log($"[RoomBarrierGen] 🎯 Criado Room_CombatTrigger para sala sem colisor: {path}");
                }

                prefabsProcessed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            $"✅ Bordas das salas repaginadas com sucesso!\n\n" +
            $"Salas processadas   : {prefabsProcessed}\n" +
            $"Barreiras geradas   : {barriersCreated}\n" +
            $"Triggers criados    : {triggersCreated}\n\n" +
            (meshToAssign != null ? $"Malha aplicada: {meshToAssign.name}" : "Malha base preparada para substituição!");

        Debug.Log($"[RoomBarrierGen] {summary}");
        if (showDialog && !Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Bordas das Salas - Concluído", summary, "OK");
        }
    }
}
