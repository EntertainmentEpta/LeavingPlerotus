using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector para ConnectionPoint:
/// Oferece ferramentas de snap ao chão (BoxCollider e Raycast), nivelamento horizontal e calibração proporcional de escala de salas.
/// </summary>
[CustomEditor(typeof(ConnectionPoint))]
[CanEditMultipleObjects]
public class ConnectionPointEditor : Editor
{
    private static float customMeasuredWidth = 4.0f;

    public override void OnInspectorGUI()
    {
        ConnectionPoint cp = (ConnectionPoint)target;
        serializedObject.Update();

        // ── CABEÇALHO COM TIPO E COR ─────────────────────────────────────────
        Color headerColor = (cp.pointType == ConnectionPoint.PointType.Entrada)
            ? new Color(0.1f, 0.7f, 1f)
            : new Color(1f, 0.5f, 0f);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.color = headerColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.color = Color.white;
        string typeLabel = cp.pointType == ConnectionPoint.PointType.Entrada
            ? "🚪 PONTO DE ENTRADA (Recebe Conexões)"
            : "🚪 PONTO DE SAÍDA (Gera Novas Salas)";
        EditorGUILayout.LabelField(typeLabel, headerStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ── CHECAGEM DE INCLINAÇÃO (PITCH & ROLL) ────────────────────────────
        float dotUp = Vector3.Dot(cp.transform.up, Vector3.up);
        bool isTilted = dotUp < 0.98f;

        if (isTilted)
        {
            EditorGUILayout.HelpBox(
                "⚠️ ATENÇÃO: Este ConnectionPoint está inclinado (Pitch/Roll)! " +
                "Isso fará com que as salas conectadas fiquem tortas ou com desnível no piso.",
                MessageType.Warning);

            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("📐 Nivelar Orientação Horizontal (Zero Pitch & Roll)", GUILayout.Height(28)))
            {
                Undo.RecordObject(cp.transform, "Nivelar Orientação do ConnectionPoint");
                Vector3 fwd = cp.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                cp.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                EditorUtility.SetDirty(cp.transform);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(6);
        }

        // ── CAMPOS PADRÃO DO INSPECTOR ───────────────────────────────────────
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // ── FERRAMENTAS DE ALINHAMENTO RÁPIDO ────────────────────────────────
        EditorGUILayout.LabelField("🛠️ Ferramentas de Alinhamento do Piso", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 1. Snap ao Chão Inteligente (BoxCollider + Raycast + Mesh)
        GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
        if (GUILayout.Button("⬇️ Snap ao Piso (Detectar BoxCollider / Chão)", GUILayout.Height(30)))
        {
            SnapToGroundRobust(cp);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        // 2. Atalho para ZERAR Y
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Zerar Y Local (Y = 0)"))
        {
            Undo.RecordObject(cp.transform, "Zerar Y Local");
            Vector3 lp = cp.transform.localPosition;
            cp.transform.localPosition = new Vector3(lp.x, 0f, lp.z);
            cp.floorOffset = 0f;
            EditorUtility.SetDirty(cp.transform);
            EditorUtility.SetDirty(cp);
        }

        if (GUILayout.Button("Zerar Y de Mundo (Y = 0)"))
        {
            Undo.RecordObject(cp.transform, "Zerar Y de Mundo");
            Vector3 wp = cp.transform.position;
            cp.transform.position = new Vector3(wp.x, 0f, wp.z);
            cp.floorOffset = 0f;
            EditorUtility.SetDirty(cp.transform);
            EditorUtility.SetDirty(cp);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 3. Inverter Direção 180°
        if (GUILayout.Button("🔄 Inverter Direção 180° (Apontar para Fora)", GUILayout.Height(24)))
        {
            Undo.RecordObject(cp.transform, "Inverter Direção ConnectionPoint");
            cp.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * cp.transform.rotation;
            EditorUtility.SetDirty(cp.transform);
        }

        // 4. Forçar nivelamento horizontal
        if (GUILayout.Button("📐 Nivelar Rotação Horizontal (0° Pitch/Roll)", GUILayout.Height(24)))
        {
            Undo.RecordObject(cp.transform, "Nivelar Orientação");
            Vector3 fwd = cp.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            cp.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            EditorUtility.SetDirty(cp.transform);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // ── CALIBRADOR PROPORCIONAL DE ESCALA DA SALA ────────────────────────
        EditorGUILayout.LabelField("📏 Calibrador de Proporção da Sala (IA Mesh Scaler)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.HelpBox(
            "Se o vão desta porta na malha 3D for maior ou menor do que o gabarito padrão, " +
            "informe a largura atual da porta e clique abaixo para reescalar a sala inteira proporcionalmente.",
            MessageType.Info);

        customMeasuredWidth = EditorGUILayout.FloatField("Largura Atual no Mesh (m):", customMeasuredWidth);
        EditorGUILayout.LabelField($"Largura Alvo (Gabarito): {cp.doorWidth:F1} metros");

        if (customMeasuredWidth > 0.05f && Mathf.Abs(customMeasuredWidth - cp.doorWidth) > 0.05f)
        {
            float scaleFactor = cp.doorWidth / customMeasuredWidth;
            EditorGUILayout.LabelField($"Fator de Escala Proporcional: {scaleFactor:F3}x", EditorStyles.miniLabel);

            GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
            if (GUILayout.Button($"⚖️ Reescalar Sala Proporcionalmente ({scaleFactor:F2}x)", GUILayout.Height(28)))
            {
                Transform rootTransform = cp.transform.root;
                if (rootTransform != null)
                {
                    Undo.RecordObject(rootTransform, "Reescalar Sala Proporcionalmente");
                    rootTransform.localScale *= scaleFactor;
                    EditorUtility.SetDirty(rootTransform);
                    Debug.Log($"[ConnectionPoint] Sala '{rootTransform.name}' reescalada proporcionalmente em {scaleFactor:F3}x para bater com a moldura de {cp.doorWidth}m.");
                }
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Detecta o chão de forma 100% à prova de falhas:
    /// 1. Varre os BoxColliders da sala (mesmo em Prefab Mode).
    /// 2. Se não achar, usa Raycast na cena local do prefab.
    /// 3. Se não achar, usa os limites da malha (MeshRenderer).
    /// </summary>
    private void SnapToGroundRobust(ConnectionPoint cp)
    {
        Undo.RecordObject(cp.transform, "Snap ConnectionPoint ao Chão");
        Undo.RecordObject(cp, "Reset Floor Offset");

        Vector3 cpPos = cp.transform.position;

        // 1. Procura em todos os BoxColliders da sala aquele que fica logo abaixo deste ponto
        BoxCollider[] allBoxes = cp.transform.root.GetComponentsInChildren<BoxCollider>(true);
        BoxCollider bestBox = null;
        float highestFloorY = float.MinValue;

        foreach (var box in allBoxes)
        {
            if (box.transform.IsChildOf(cp.transform)) continue;

            Bounds b = box.bounds;
            // Checa se o ponto está horizontalmente sobre o BoxCollider (com margem de 1.5m para a soleira da porta)
            if (cpPos.x >= b.min.x - 1.5f && cpPos.x <= b.max.x + 1.5f &&
                cpPos.z >= b.min.z - 1.5f && cpPos.z <= b.max.z + 1.5f)
            {
                // Queremos a superfície que está abaixo ou no nível do socket (não o teto!)
                if (b.max.y <= cpPos.y + 1.2f && b.max.y > highestFloorY)
                {
                    highestFloorY = b.max.y;
                    bestBox = box;
                }
            }
        }

        if (bestBox != null)
        {
            cp.transform.position = new Vector3(cpPos.x, highestFloorY, cpPos.z);
            cp.floorOffset = 0f;
            Debug.Log($"✅ [ConnectionPoint] '{cp.name}' cravado com sucesso no topo do BoxCollider '{bestBox.name}' em Y = {highestFloorY:F3}m!");
            EditorUtility.SetDirty(cp.transform);
            EditorUtility.SetDirty(cp);
            return;
        }

        // 2. Se não achou por BoxCollider, tenta raycast de física considerando todas as camadas e triggers
        Vector3 rayOrigin = cpPos + Vector3.up * 3f;
        PhysicsScene pScene = cp.gameObject.scene.GetPhysicsScene();
        RaycastHit hit;
        bool hitFound = false;

        if (pScene.IsValid())
            hitFound = pScene.Raycast(rayOrigin, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Collide);
        else
            hitFound = Physics.Raycast(rayOrigin, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Collide);

        if (hitFound && !hit.collider.transform.IsChildOf(cp.transform))
        {
            cp.transform.position = new Vector3(cpPos.x, hit.point.y, cpPos.z);
            cp.floorOffset = 0f;
            Debug.Log($"✅ [ConnectionPoint] '{cp.name}' cravado via Raycast no colisor '{hit.collider.name}' em Y = {hit.point.y:F3}m!");
            EditorUtility.SetDirty(cp.transform);
            EditorUtility.SetDirty(cp);
            return;
        }

        // 3. Fallback visual: procura a base dos MeshRenderers do chão
        MeshRenderer[] renderers = cp.transform.root.GetComponentsInChildren<MeshRenderer>(true);
        MeshRenderer bestFloorMesh = null;
        float bestMeshFloorY = float.MinValue;

        foreach (var mr in renderers)
        {
            if (mr.transform.IsChildOf(cp.transform)) continue;
            Bounds b = mr.bounds;
            if (cpPos.x >= b.min.x - 1.5f && cpPos.x <= b.max.x + 1.5f &&
                cpPos.z >= b.min.z - 1.5f && cpPos.z <= b.max.z + 1.5f)
            {
                if (b.min.y <= cpPos.y + 0.8f && b.min.y > bestMeshFloorY)
                {
                    bestMeshFloorY = b.min.y;
                    bestFloorMesh = mr;
                }
            }
        }

        if (bestFloorMesh != null)
        {
            cp.transform.position = new Vector3(cpPos.x, bestMeshFloorY, cpPos.z);
            cp.floorOffset = 0f;
            Debug.Log($"✅ [ConnectionPoint] '{cp.name}' alinhado na base do MeshRenderer '{bestFloorMesh.name}' em Y = {bestMeshFloorY:F3}m!");
            EditorUtility.SetDirty(cp.transform);
            EditorUtility.SetDirty(cp);
            return;
        }

        Debug.LogWarning("[ConnectionPoint] Não foi possível detectar colisor ou malha logo abaixo deste ponto. Use os botões 'Zerar Y' ou ajuste a altura manualmente.");
    }
}
