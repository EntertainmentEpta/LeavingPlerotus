using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Marca um ponto de conexão em um prefab de sala com Gabarito de Porta e Régua Métrica 3D.
///
/// RECURSOS DE ALINHAMENTO:
///   • Moldura da Porta: Visualiza em 3D o vão retangular padrão (largura x altura).
///   • Régua Métrica: Marcas a cada 0.5m nos pilares da porta para conferir a escala real do mesh.
///   • Linha de Piso (Verde): Indica o nível exato do chão onde o jogador vai pisar.
///   • Seta de Direção: Mostra a direção horizontal perfeita de encaixe do corredor.
/// </summary>
[SelectionBase]
public class ConnectionPoint : MonoBehaviour
{
    public enum PointType { Entrada, Saida }

    [Header("Identificação do Ponto")]
    [Tooltip("Entrada = este ponto RECEBE conexões de outras salas.\n" +
             "Saida = este ponto GERA novas salas durante a geração procedural.")]
    public PointType pointType = PointType.Saida;

    [Tooltip("Tag de compatibilidade. Dois ConnectionPoints só se conectam se tiverem a mesma tag.\n" +
             "Use 'Standard' para a maioria das salas.")]
    public string connectionTag = "Standard";

    [Tooltip("Distância em metros do socket até o chão desta sala.\n" +
             "0 = socket está no nível do chão.\n" +
             "Valores positivos = socket está acima do chão.\n" +
             "O Gizmo verde mostra onde o chão será considerado.")]
    public float floorOffset = 0f;

    [Header("Gabarito de Porta & Régua Métrica")]
    [Tooltip("Largura padrão da passagem em metros (Gabarito de alinhamento).")]
    public float doorWidth = 3.0f;

    [Tooltip("Altura padrão da passagem em metros (Gabarito de alinhamento).")]
    public float doorHeight = 3.2f;

    [Tooltip("Profundidade de vedação recomendada no vão da porta (15 cm = 0.15m).")]
    public float overlapDepth = 0.15f;

    [Tooltip("Exibir a régua graduada a cada 0.5m na cena.")]
    public bool showRuler = true;

    [Tooltip("Exibir a moldura 3D da porta na cena.")]
    public bool showDoorFrame = true;

    /// <summary>
    /// Retorna a posição Y do chão desta sala no espaço de mundo.
    /// = posição Y do socket - floorOffset.
    /// </summary>
    public float GetFloorWorldY() => transform.position.y - floorOffset;

    /// <summary>
    /// Marcado como true pelo LevelGenerator quando este ponto já foi usado.
    /// </summary>
    [HideInInspector]
    public bool isOccupied = false;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        DrawGizmoInternal(false);
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmoInternal(true);
    }

    private void DrawGizmoInternal(bool selected)
    {
        Color mainColor = (pointType == PointType.Entrada)
            ? new Color(0f, 0.85f, 1f, selected ? 1f : 0.75f)   // Ciano = Entrada
            : new Color(1f, 0.55f, 0f, selected ? 1f : 0.75f);  // Laranja = Saída

        Gizmos.color = mainColor;

        // 1. Bolinha central do socket
        Gizmos.DrawSphere(transform.position, selected ? 0.25f : 0.18f);

        // 2. Seta de Direção (Forward para fora da sala)
        Vector3 forwardDir = transform.forward;
        Vector3 arrowStart = transform.position + Vector3.up * (doorHeight * 0.5f);
        Vector3 arrowEnd = arrowStart + forwardDir * 2.0f;

        Gizmos.color = mainColor;
        Gizmos.DrawLine(arrowStart, arrowEnd);
        // Ponta da seta
        Vector3 right = transform.right * 0.35f;
        Gizmos.DrawLine(arrowEnd, arrowEnd - forwardDir * 0.5f + right);
        Gizmos.DrawLine(arrowEnd, arrowEnd - forwardDir * 0.5f - right);

        // 3. Moldura Retangular da Porta (Gabarito)
        if (showDoorFrame)
        {
            float halfW = doorWidth * 0.5f;

            // Cantos em espaço local do socket
            Vector3 bLeft  = transform.position - transform.right * halfW;
            Vector3 bRight = transform.position + transform.right * halfW;
            Vector3 tLeft  = bLeft  + transform.up * doorHeight;
            Vector3 tRight = bRight + transform.up * doorHeight;

            // Pilares laterais e topo
            Gizmos.color = mainColor;
            Gizmos.DrawLine(bLeft, tLeft);   // Pilar esquerdo
            Gizmos.DrawLine(bRight, tRight); // Pilar direito
            Gizmos.DrawLine(tLeft, tRight);  // Topo / Lintrel

            // Base do chão (Verde brilhante para garantir nível do piso)
            Gizmos.color = new Color(0.2f, 1f, 0.3f, selected ? 1f : 0.8f);
            Gizmos.DrawLine(bLeft, bRight);

            // 4. RÉGUA FÍSICA 3D NO PISO (Medida Fixa de 15 cm na direção da seta)
            if (overlapDepth > 0f)
            {
                // Vetores horizontais fixos no mundo (NÃO giram com a câmera e NÃO distorcem com zoom)
                Vector3 fwdFlat = forwardDir;
                fwdFlat.y = 0f;
                if (fwdFlat.sqrMagnitude < 0.0001f) fwdFlat = Vector3.forward;
                fwdFlat.Normalize();

                Vector3 rgtFlat = Vector3.Cross(Vector3.up, fwdFlat).normalized;

                // 4.1 Linha de frente da parede através de toda a largura da porta (3.0m)
                Vector3 bLeftFront  = bLeft  + fwdFlat * overlapDepth;
                Vector3 bRightFront = bRight + fwdFlat * overlapDepth;

                Gizmos.color = new Color(0f, 1f, 0.8f, selected ? 0.95f : 0.5f);
                Gizmos.DrawLine(bLeftFront, bRightFront); // Linha da borda da parede a 15cm
                Gizmos.DrawLine(bLeft, bLeftFront);       // Fechamento lateral esquerdo
                Gizmos.DrawLine(bRight, bRightFront);     // Fechamento lateral direito

                // 4.2 Trena 3D Central Fixa no Chão (Trilho com marcas métricas de 5cm, 10cm e 15cm)
                Vector3 rulerCenter = transform.position;
                float rulerHalfW = 0.12f; // 24 cm de largura total da trena 3D

                Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f); // Amarelo Dourado

                // Trilhos laterais da régua (do ponto 0 até 15 cm exatos)
                Vector3 railL0 = rulerCenter - rgtFlat * rulerHalfW;
                Vector3 railL15 = railL0 + fwdFlat * overlapDepth;
                Vector3 railR0 = rulerCenter + rgtFlat * rulerHalfW;
                Vector3 railR15 = railR0 + fwdFlat * overlapDepth;

                Gizmos.DrawLine(railL0, railL15);
                Gizmos.DrawLine(railR0, railR15);

                // Eixo central da régua apontando na direção da seta
                Gizmos.DrawLine(rulerCenter, rulerCenter + fwdFlat * overlapDepth);

                // Marcas físicas 3D (Travessas da régua gravadas no chão):
                // Marca 0 cm (Início do Ponto de Conexão)
                Gizmos.DrawLine(railL0, railR0);

                // Marca 5 cm (0.05m)
                Vector3 m5L = rulerCenter + fwdFlat * 0.05f - rgtFlat * (rulerHalfW * 0.5f);
                Vector3 m5R = rulerCenter + fwdFlat * 0.05f + rgtFlat * (rulerHalfW * 0.5f);
                Gizmos.DrawLine(m5L, m5R);

                // Marca 10 cm (0.10m)
                Vector3 m10L = rulerCenter + fwdFlat * 0.10f - rgtFlat * (rulerHalfW * 0.75f);
                Vector3 m10R = rulerCenter + fwdFlat * 0.10f + rgtFlat * (rulerHalfW * 0.75f);
                Gizmos.DrawLine(m10L, m10R);

                // Marca 15 cm (0.15m - Linha Final da Parede) - Barra dupla e ponta de seta 3D
                Gizmos.DrawLine(railL15, railR15);
                Vector3 railL15_b = railL15 - fwdFlat * 0.015f;
                Vector3 railR15_b = railR15 - fwdFlat * 0.015f;
                Gizmos.DrawLine(railL15_b, railR15_b);

                // Ponta de seta 3D na marca de 15 cm
                Vector3 arrowTip = rulerCenter + fwdFlat * overlapDepth;
                Gizmos.DrawLine(arrowTip, arrowTip - fwdFlat * 0.04f + rgtFlat * 0.04f);
                Gizmos.DrawLine(arrowTip, arrowTip - fwdFlat * 0.04f - rgtFlat * 0.04f);
            }

            // 4. Régua Métrica Graduada (Marcas a cada 0.5m)
            if (showRuler)
            {
                Gizmos.color = new Color(1f, 1f, 1f, selected ? 0.9f : 0.5f);
                for (float h = 0.5f; h < doorHeight; h += 0.5f)
                {
                    bool isFullMeter = Mathf.Approximately(h % 1.0f, 0f);
                    float tickLen = isFullMeter ? 0.25f : 0.12f;

                    // Marcas no pilar esquerdo
                    Vector3 tickL = bLeft + transform.up * h;
                    Gizmos.DrawLine(tickL, tickL + transform.right * tickLen);

                    // Marcas no pilar direito
                    Vector3 tickR = bRight + transform.up * h;
                    Gizmos.DrawLine(tickR, tickR - transform.right * tickLen);
                }
            }

            // Se selecionado, desenha um sombreado de passagem no vão da porta
            if (selected)
            {
                Color portalColor = mainColor;
                portalColor.a = 0.15f;
                Handles.color = portalColor;
                Vector3[] verts = new Vector3[] { bLeft, tLeft, tRight, bRight };
                Handles.DrawSolidRectangleWithOutline(verts, portalColor, mainColor);

                // Label com a indicação métrica no topo
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = mainColor;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.fontSize = 12;
                labelStyle.alignment = TextAnchor.MiddleCenter;

                string typeName = (pointType == PointType.Entrada) ? "ENTRADA" : "SAÍDA";
                Handles.Label(arrowEnd + Vector3.up * 0.3f, $"[{typeName}]\n{doorWidth:F1}m x {doorHeight:F1}m", labelStyle);
            }
        }

        // 5. Linha de Detecção do Piso (Floor Offset)
        if (floorOffset != 0f)
        {
            Vector3 floorPos = transform.position - Vector3.up * floorOffset;
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.9f);
            Gizmos.DrawSphere(floorPos, 0.15f);
            Gizmos.DrawLine(transform.position, floorPos);
        }
    }
#endif
}
