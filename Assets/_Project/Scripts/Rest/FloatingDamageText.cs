using UnityEngine;
using TMPro;

public class FloatingDamageText : MonoBehaviour
{
    [Header("Animação")]
    public float lifetime = 1f;       // Tempo que o texto fica na tela
    public float floatSpeed = 1.5f;   // Velocidade com que o texto sobe

    private TextMeshProUGUI textMesh;
    private float timer;
    private Color startColor;

    void Awake()
    {
        // Pega a referência do componente de texto que é filho deste objeto
        textMesh = GetComponentInChildren<TextMeshProUGUI>();
        startColor = textMesh.color;
        timer = lifetime;
    }

    void Update()
    {
        // Move o texto para cima
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Faz o texto sempre encarar a câmera
        transform.LookAt(transform.position + Camera.main.transform.forward);

        // Lógica para desaparecer (fade out)
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            // Diminui a opacidade do texto ao longo do tempo
            textMesh.color = new Color(startColor.r, startColor.g, startColor.b, timer / lifetime);
        }
    }

    // Função pública para que o inimigo possa definir qual número mostrar
    public void SetText(string text)
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshProUGUI>();
        }
        textMesh.text = text;
    }
}