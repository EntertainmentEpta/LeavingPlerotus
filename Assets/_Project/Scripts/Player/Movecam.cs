using UnityEngine;

public class MoveCam : MonoBehaviour
{
    // Removi o "Singleton" complexo para evitar que a câmera se suicide.
    // Agora ela apenas existe e faz o trabalho dela na cena atual.

    [Header("Configurações do Alvo")]
    public Transform playerTransform;
    public float followSpeed = 5f;
    
    // Offset automático se você esquecer de configurar
    public Vector3 offset = new Vector3(0, 10, -8); 

    private void Start()
    {
        // Ao iniciar, tenta achar o player imediatamente
        FindPlayer();
        
        // Garante a rotação correta para vista isométrica (olhando para baixo)
        transform.rotation = Quaternion.Euler(50, 0, 0);

        // Garante a presença do sistema de tremor de câmera (CameraShakeFeedback) desacoplado
        if (GetComponent<CameraShakeFeedback>() == null)
        {
            gameObject.AddComponent<CameraShakeFeedback>();
        }
    }

    private void LateUpdate()
    {
        // Se por algum motivo perdemos o player (troca de cena/morte), procura de novo
        if (playerTransform == null)
        {
            FindPlayer();
        }

        // Se temos um player, seguimos ele
        if (playerTransform != null)
        {
            Vector3 desiredPosition = playerTransform.position + offset;
            
            // Lerp para movimento suave
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            // Atualiza a posição do Player globalmente para os shaders de Dither Fade das paredes
            Vector3 pPos = playerTransform.position;
            Shader.SetGlobalVector("_PlayerPos", new Vector4(pPos.x, pPos.y + 1.0f, pPos.z, 1.0f));
        }
    }

    public void FindPlayer()
    {
        // Tenta achar pelo script de vida (funciona melhor que Tag as vezes)
        PlayerHealth playerScript = Object.FindFirstObjectByType<PlayerHealth>();

        if (playerScript != null)
        {
            playerTransform = playerScript.transform;
        }
        else
        {
            // Se falhar, tenta pela Tag tradicional
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerTransform = taggedPlayer.transform;
            }
        }
    }
}
