using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MakeMeshFlat : MonoBehaviour
{
    [ContextMenu("Des-IAr Mapa (Aplicar Estilo Indie Flat-Shaded)")]
    public void ConvertToFlatShaded()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("Nenhuma malha encontrada neste objeto!");
            return;
        }

        // Criamos uma cpia da malha para no quebrar o arquivo original da IA
        Mesh originalMesh = mf.sharedMesh;
        Mesh flatMesh = new Mesh();
        flatMesh.name = originalMesh.name + "_Estilizado";

        Vector3[] oldVerts = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        Vector2[] oldUvs = originalMesh.uv;

        // Para ter a iluminao "Flat" (polgonos duros), cada tringulo precisa ter seus prprios vrtices exclusivos
        Vector3[] newVerts = new Vector3[triangles.Length];
        Vector2[] newUvs = new Vector2[triangles.Length];
        int[] newTris = new int[triangles.Length];

        for (int i = 0; i < triangles.Length; i++)
        {
            newVerts[i] = oldVerts[triangles[i]];
            if (oldUvs != null && oldUvs.Length > 0)
            {
                newUvs[i] = oldUvs[triangles[i]];
            }
            newTris[i] = i; // Agora os tringulos so sequenciais (0, 1, 2, 3...)
        }

        flatMesh.vertices = newVerts;
        flatMesh.uv = newUvs;
        flatMesh.triangles = newTris;

        // O segredo est aqui: como os vrtices no so mais compartilhados, as normais ficam duras!
        flatMesh.RecalculateNormals();
        flatMesh.RecalculateBounds();

        mf.mesh = flatMesh;
        
        Debug.Log("Sucesso! A malha agora est estilizada. Dica: Troque o Material dela para uma cor slida sem textura para ver o efeito real!");
    }
}
