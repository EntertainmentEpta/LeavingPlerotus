using UnityEngine;

[ExecuteAlways]
public class Billboard : MonoBehaviour
{
    [Tooltip("Deixe marcado para que o objeto fique sempre em pé (não deite quando a câmera olhar de cima).")]
    public bool lockYAxis = true;

    private Camera mainCam;

    void LateUpdate()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return; // Se no tiver cmera, no faz nada
        }

        // Calcula a posio para onde a imagem deve olhar
        Vector3 targetPos = mainCam.transform.position;

        // Se travar o eixo Y, a imagem s gira para os lados, como uma parede ou rvore de papel
        if (lockYAxis)
        {
            targetPos.y = transform.position.y;
        }

        // Calcula a direo da cmera para o objeto
        Vector3 directionToCamera = transform.position - targetPos;

        if (directionToCamera != Vector3.zero)
        {
            // O Quaternion.LookRotation faz o objeto olhar na direo do vetor
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }
}
