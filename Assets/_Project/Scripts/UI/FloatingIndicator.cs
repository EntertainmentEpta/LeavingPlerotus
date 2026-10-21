using UnityEngine;

public class FloatingIndicator : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    public float floatSpeed = 4f;     // Velocidade que sobe e desce
    public float floatHeight = 0.2f;  // Altura máxima do movimento

    [Header("Rotação")]
    public bool faceCamera = true;    // Se deve sempre olhar para a câmera

    private Vector3 startPos;
    private Camera mainCamera;

    void Start()
    {
        startPos = transform.localPosition;
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 1. Movimento de flutuar (sobe e desce usando Seno)
        float newY = startPos.y + (Mathf.Sin(Time.time * floatSpeed) * floatHeight);
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);

        // 2. Sempre virar para a câmera (Efeito Billboard) para nunca ficar de lado
        if (faceCamera && mainCamera != null)
        {
            transform.forward = mainCamera.transform.forward;
        }
    }
}
