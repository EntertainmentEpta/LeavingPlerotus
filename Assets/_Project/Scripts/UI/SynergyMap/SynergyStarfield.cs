using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gera um fundo estrelado procedural para o mapa de sinergias.
/// Coloque este script no GameObject "Background" dentro do Content.
/// Ele cria uma textura preta com pontos brancos aleatórios no Awake.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SynergyStarfield : MonoBehaviour
{
    [Header("Configuração das Estrelas")]
    [Tooltip("Largura da textura gerada (em pixels).")]
    [SerializeField] private int textureWidth = 512;

    [Tooltip("Altura da textura gerada (em pixels).")]
    [SerializeField] private int textureHeight = 512;

    [Tooltip("Número de estrelas a serem geradas.")]
    [SerializeField] private int starCount = 200;

    [Tooltip("Seed para reproduzir o mesmo padrão de estrelas.")]
    [SerializeField] private int seed = 42;

    private void Awake()
    {
        GenerateStarfield();
    }

    private void GenerateStarfield()
    {
        Texture2D tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;

        // Preenche tudo de preto
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.black;

        // Planta as estrelas
        Random.State oldState = Random.state;
        Random.InitState(seed);

        for (int i = 0; i < starCount; i++)
        {
            int x = Random.Range(0, textureWidth);
            int y = Random.Range(0, textureHeight);
            float brightness = Random.Range(0.4f, 1.0f);
            pixels[y * textureWidth + x] = new Color(brightness, brightness, brightness, 1f);
        }

        Random.state = oldState;

        tex.SetPixels(pixels);
        tex.Apply();

        RawImage rawImg = GetComponent<RawImage>();
        rawImg.texture = tex;
        rawImg.color = Color.white;
    }
}
