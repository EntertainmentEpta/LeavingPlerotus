using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteAlways]
public class HadesCameraSetup : MonoBehaviour
{
    [ContextMenu("Aplicar Estilo Hades (Câmera Isométrica)")]
    public void SetupCamera()
    {
        Camera cam = GetComponent<Camera>();
        
        // Hades usa cmera Ortogrfica (sem perspectiva, as coisas no diminuem com a distncia)
        cam.orthographic = true;
        cam.orthographicSize = 8f; // Isso controla o Zoom. Pode aumentar ou diminuir.

        // O ngulo matemtico perfeito de jogos isomtricos 2.5D
        transform.rotation = Quaternion.Euler(30f, 45f, 0f);
        
        // Afasta a cmera para ela enxergar o mapa no centro (0,0,0)
        transform.position = new Vector3(-10f, 10f, -10f);

        Debug.Log("Cmera configurada para o estilo Hades/Bastion! O Palco est pronto.");
    }
}
