using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Janela do Editor para inspeção, calibração e correção em massa de todas as salas da dungeon.
/// Acesse via Menu Unity: EPTA Tools -> Alinhador e Calibrador de Salas
/// </summary>
public class RoomAlignmentWindow : EditorWindow
{
    private Vector2 scrollPos;
    private List<RoomPrefabData> detectedRooms = new List<RoomPrefabData>();

    private class RoomPrefabData
    {
        public string name;
        public string path;
        public GameObject prefab;
        public int entradaCount;
        public int saidaCount;
        public bool hasTiltedSockets;
    }

    [MenuItem("EPTA Tools/Alinhador e Calibrador de Salas")]
    public static void OpenWindow()
    {
        RoomAlignmentWindow window = GetWindow<RoomAlignmentWindow>("Alinhador de Salas");
        window.minSize = new Vector2(650, 450);
        window.ScanRooms();
        window.Show();
    }

    private void OnEnable()
    {
        ScanRooms();
    }

    public void ScanRooms()
    {
        detectedRooms.Clear();
        string[] searchFolders = new string[] { "Assets/_Project/Enviroment/Map/New map" };
        string[] guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            ConnectionPoint[] cps = prefab.GetComponentsInChildren<ConnectionPoint>(true);
            if (cps.Length == 0) continue;

            RoomPrefabData data = new RoomPrefabData
            {
                name = prefab.name,
                path = path,
                prefab = prefab,
                entradaCount = 0,
                saidaCount = 0,
                hasTiltedSockets = false
            };

            foreach (var cp in cps)
            {
                if (cp.pointType == ConnectionPoint.PointType.Entrada) data.entradaCount++;
                else if (cp.pointType == ConnectionPoint.PointType.Saida) data.saidaCount++;

                float dotUp = Vector3.Dot(cp.transform.up, Vector3.up);
                if (dotUp < 0.98f) data.hasTiltedSockets = true;
            }

            detectedRooms.Add(data);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🏛️ Alinhador & Calibrador de Proporção de Salas", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Verifique e corrija os pontos de conexão de todas as salas da dungeon em 1 clique.", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);

        // ── BOTÕES DE AÇÃO GLOBAL ────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔄 Atualizar Lista de Salas", GUILayout.Height(28)))
        {
            ScanRooms();
        }

        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("📐 Nivelar Todos os Sockets (Zero Pitch/Roll)", GUILayout.Height(28)))
        {
            LevelAllSockets();
        }

        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("🔧 Corrigir Tipos Invertidos Conhecidos", GUILayout.Height(28)))
        {
            FixKnownInvertedTypes();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // ── TABELA DE SALAS ──────────────────────────────────────────────────
        EditorGUILayout.LabelField($"Salas Encontradas ({detectedRooms.Count}):", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Cabeçalho da Tabela
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Prefab da Sala", EditorStyles.boldLabel, GUILayout.Width(200));
        EditorGUILayout.LabelField("Entradas", EditorStyles.boldLabel, GUILayout.Width(70));
        EditorGUILayout.LabelField("Saídas", EditorStyles.boldLabel, GUILayout.Width(70));
        EditorGUILayout.LabelField("Status / Alertas", EditorStyles.boldLabel, GUILayout.Width(180));
        EditorGUILayout.LabelField("Ação", EditorStyles.boldLabel, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        foreach (var room in detectedRooms)
        {
            EditorGUILayout.BeginHorizontal();

            // Nome da Sala
            EditorGUILayout.LabelField(room.name, GUILayout.Width(200));

            // Entradas
            Color defaultColor = GUI.color;
            if (room.entradaCount == 0 && !room.name.ToLower().Contains("safe"))
                GUI.color = new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField(room.entradaCount.ToString(), GUILayout.Width(70));
            GUI.color = defaultColor;

            // Saídas
            if (room.saidaCount == 0 && !room.name.ToLower().Contains("exit"))
                GUI.color = new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField(room.saidaCount.ToString(), GUILayout.Width(70));
            GUI.color = defaultColor;

            // Status
            bool isTerminalRoom = room.name.ToLower().Contains("exit") || 
                                 room.name.ToLower().Contains("deadend") || 
                                 room.name.ToLower().Contains("merchant");

            if (room.hasTiltedSockets)
            {
                GUI.color = new Color(1f, 0.8f, 0.2f);
                EditorGUILayout.LabelField("⚠️ Sockets Inclinados", GUILayout.Width(180));
                GUI.color = defaultColor;
            }
            else if (room.entradaCount == 0 && !room.name.ToLower().Contains("safe"))
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField("❌ Sem Ponto de Entrada!", GUILayout.Width(180));
                GUI.color = defaultColor;
            }
            else if (room.saidaCount == 0 && !isTerminalRoom)
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField("❌ Sem Ponto de Saída!", GUILayout.Width(180));
                GUI.color = defaultColor;
            }
            else
            {
                GUI.color = new Color(0.3f, 1f, 0.3f);
                EditorGUILayout.LabelField("✅ Alinhamento OK", GUILayout.Width(180));
                GUI.color = defaultColor;
            }

            // Botão Selecionar
            if (GUILayout.Button("Abrir Prefab", GUILayout.Width(90)))
            {
                Selection.activeObject = room.prefab;
                EditorGUIUtility.PingObject(room.prefab);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Nivela horizontalmente o forward de todos os ConnectionPoints de todas as salas,
    /// eliminando qualquer inclinação vertical (Pitch/Roll) para o corredor sair perfeitamente reto.
    /// </summary>
    private void LevelAllSockets()
    {
        int fixedCount = 0;
        foreach (var room in detectedRooms)
        {
            string assetPath = room.path;
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null) continue;

            bool changed = false;
            ConnectionPoint[] cps = root.GetComponentsInChildren<ConnectionPoint>(true);
            foreach (var cp in cps)
            {
                Vector3 fwd = cp.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

                Quaternion targetRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                if (Quaternion.Angle(cp.transform.rotation, targetRot) > 0.5f)
                {
                    cp.transform.rotation = targetRot;
                    changed = true;
                    fixedCount++;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        ScanRooms();
        EditorUtility.DisplayDialog("Nivelamento Concluído", $"{fixedCount} ConnectionPoints foram nivelados horizontalmente em 0° de inclinação!", "OK");
    }

    /// <summary>
    /// Corrige casos conhecidos de prefabs com tipos invertidos:
    /// - Transition.prefab: primeiro socket vira Entrada (tinha 2 saídas)
    /// - LakeRoom.prefab: segundo socket vira Saída (tinha 2 entradas)
    /// - DoubleQuadRoom.prefab: primeiro socket vira Entrada (tinha 2 saídas)
    /// </summary>
    private void FixKnownInvertedTypes()
    {
        int fixedPrefabs = 0;

        void FixPrefab(string prefabName, System.Action<ConnectionPoint[]> fixAction)
        {
            var data = detectedRooms.Find(r => r.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase));
            if (data == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(data.path);
            if (root == null) return;

            ConnectionPoint[] cps = root.GetComponentsInChildren<ConnectionPoint>(true);
            fixAction(cps);

            PrefabUtility.SaveAsPrefabAsset(root, data.path);
            PrefabUtility.UnloadPrefabContents(root);
            fixedPrefabs++;
        }

        // 1. Transition.prefab: Primeiro socket vira Entrada
        FixPrefab("Transition", cps =>
        {
            if (cps.Length >= 2)
            {
                cps[0].pointType = ConnectionPoint.PointType.Entrada;
                cps[1].pointType = ConnectionPoint.PointType.Saida;
            }
        });

        // 2. LakeRoom.prefab: Segundo socket vira Saída
        FixPrefab("LakeRoom", cps =>
        {
            if (cps.Length >= 2)
            {
                cps[0].pointType = ConnectionPoint.PointType.Entrada;
                cps[1].pointType = ConnectionPoint.PointType.Saida;
            }
        });

        // 3. DoubleQuadRoom.prefab: Primeiro socket vira Entrada
        FixPrefab("DoubleQuadRoom", cps =>
        {
            if (cps.Length >= 2)
            {
                cps[0].pointType = ConnectionPoint.PointType.Entrada;
                cps[1].pointType = ConnectionPoint.PointType.Saida;
            }
        });

        ScanRooms();
        EditorUtility.DisplayDialog("Correção Concluída", $"{fixedPrefabs} salas foram corrigidas com suas Entradas e Saídas devidamente configuradas!", "OK");
    }
}
