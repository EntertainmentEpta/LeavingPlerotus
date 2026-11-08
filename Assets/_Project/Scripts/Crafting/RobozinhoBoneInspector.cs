using System.Text;
using UnityEngine;

/// <summary>
/// Script utilitário para identificar e mapear os ossos (bones) do Robozinho na cena.
/// Arraste este componente para o GameObject raiz do Robozinho ou do modelo FBX.
/// - Imprime no Console a hierarquia completa de ossos (Pai -> Filhos).
/// - Desenha no Scene View o nome e uma esfera colorida em cima de cada osso!
/// </summary>
[ExecuteInEditMode]
public class RobozinhoBoneInspector : MonoBehaviour
{
    [Header("Visualização no Scene View")]
    public bool showGizmos = true;
    public float boneSphereRadius = 0.04f;
    public Color boneColor = Color.cyan;
    public bool drawLinesToChildren = true;

    [ContextMenu("Imprimir Hierarquia no Console")]
    public void ImprimirHierarquiaCompleta()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"🤖 [ROBOZINHO BONE INSPECTOR] Hierarquia de ossos de: {gameObject.name}\n");
        ImprimirRecursivo(transform, 0, sb);
        Debug.Log(sb.ToString(), this);
    }

    private void ImprimirRecursivo(Transform t, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 3) + (depth > 0 ? "└── " : "");
        sb.AppendLine($"{indent}<b>{t.name}</b> (Posição Local: {t.localPosition:F2})");

        for (int i = 0; i < t.childCount; i++)
        {
            ImprimirRecursivo(t.GetChild(i), depth + 1, sb);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = boneColor;
        DesenharBonesRecursivo(transform);
    }

    private void DesenharBonesRecursivo(Transform current)
    {
        // Se for um osso
        if (current.name.Contains("Bone") || current.name.Contains("joint") || current.name.Contains("Armature"))
        {
            Gizmos.color = boneColor;
            Gizmos.DrawWireSphere(current.position, boneSphereRadius);
            UnityEditor.Handles.Label(current.position + Vector3.up * (boneSphereRadius * 1.5f), current.name);
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (drawLinesToChildren && (child.name.Contains("Bone") || current.name.Contains("Bone")))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(current.position, child.position);
            }
            DesenharBonesRecursivo(child);
        }
    }
#endif
}
