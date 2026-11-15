using System.Collections;
using UnityEngine;

/// <summary>
/// Simula o reflexo de um terminal de computador/holograma na Point Light do Eptinho.
/// Alterna organicamente entre cores de monitor (Branco, Tons de Azul/Ciano e toques esporádicos de Vermelho de alerta),
/// com micro-oscilações de brilho para dar a sensação de que ele está lendo dados, digitando e trocando de abas/páginas.
/// </summary>
[DisallowMultipleComponent]
public class EptinhoComputerTerminalLight : MonoBehaviour
{
    [Header("Componentes")]
    [Tooltip("Luz que receberá o efeito. Se vazio, pega a Light deste GameObject ou dos filhos.")]
    public Light targetLight;

    [Header("Intensidade da Luz")]
    [Tooltip("Intensidade mínima da tela")]
    [Range(0.1f, 10f)]
    public float minIntensity = 1.2f;

    [Tooltip("Intensidade máxima da tela")]
    [Range(0.1f, 10f)]
    public float maxIntensity = 2.6f;

    [Tooltip("Velocidade de transição suave entre níveis de brilho")]
    [Range(1f, 30f)]
    public float smoothSpeed = 8f;

    [Header("Cores do Terminal")]
    [Tooltip("Cor padrão de tela/documento branco suave")]
    public Color terminalWhite = new Color(0.92f, 0.96f, 1.0f); // Branco azulado

    [Tooltip("Cor de dados/interface azul ciano")]
    public Color terminalCyan = new Color(0.2f, 0.75f, 1.0f); // Azul holográfico

    [Tooltip("Cor de processamento azul profundo")]
    public Color terminalDeepBlue = new Color(0.1f, 0.45f, 0.95f); // Azul terminal

    [Tooltip("Cor de alerta/erro ocasional")]
    public Color terminalAlertRed = new Color(1.0f, 0.25f, 0.2f); // Vermelho aviso

    [Tooltip("Chance (0 a 1) de piscar em vermelho ao mudar de página")]
    [Range(0f, 0.5f)]
    public float redAlertChance = 0.08f; // ~8% de chance

    [Header("Comportamento de 'Troca de Páginas'")]
    [Tooltip("Tempo mínimo em uma mesma tela antes de trocar (segundos)")]
    [Range(0.2f, 3f)]
    public float pageStayMinTime = 0.5f;

    [Tooltip("Tempo máximo em uma mesma tela antes de trocar (segundos)")]
    [Range(0.5f, 5f)]
    public float pageStayMaxTime = 2.0f;

    [Tooltip("Ativar micro-flicker orgânico contínuo (digitação/render de tela)")]
    public bool enableMicroFlicker = true;

    [Tooltip("Força do tremor sutil de brilho")]
    [Range(0f, 0.5f)]
    public float flickerNoiseAmount = 0.15f;

    // Estado interno
    private Color currentColor;
    private Color targetColor;
    private float currentIntensity;
    private float targetIntensity;
    private float pageTimer = 0f;
    private float nextPageDuration = 1f;
    private float perlinSeed;

    private void Awake()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight == null)
            targetLight = GetComponentInChildren<Light>();

        perlinSeed = Random.Range(0f, 100f);

        if (targetLight != null)
        {
            currentColor = targetLight.color;
            targetColor = terminalCyan;
            currentIntensity = targetLight.intensity;
            targetIntensity = (minIntensity + maxIntensity) * 0.5f;
        }
        else
        {
            currentColor = terminalCyan;
            targetColor = terminalCyan;
            currentIntensity = 2f;
            targetIntensity = 2f;
        }

        ChooseNextPage();
    }

    private void Update()
    {
        if (targetLight == null) return;

        pageTimer += Time.deltaTime;
        if (pageTimer >= nextPageDuration)
        {
            ChooseNextPage();
        }

        // Micro-flicker contínuo usando ruído Perlin para dar vida à luz
        float noise = 0f;
        if (enableMicroFlicker)
        {
            noise = (Mathf.PerlinNoise(Time.time * 12f, perlinSeed) - 0.5f) * flickerNoiseAmount * targetIntensity;
        }

        // Interpolação suave para não agredir os olhos
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity + noise, Time.deltaTime * smoothSpeed);
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * (smoothSpeed * 0.8f));

        targetLight.intensity = Mathf.Max(0.05f, currentIntensity);
        targetLight.color = currentColor;
    }

    /// <summary>
    /// Simula a mudança de janela/documento no computador do Eptinho.
    /// </summary>
    public void ChooseNextPage()
    {
        pageTimer = 0f;
        nextPageDuration = Random.Range(pageStayMinTime, pageStayMaxTime);

        // Decide a nova cor
        float roll = Random.value;
        if (roll < redAlertChance)
        {
            // Página de aviso / alarme ocasional
            targetColor = terminalAlertRed;
            // Vermelho tende a ser ligeiramente mais dramático / contrastante
            targetIntensity = Random.Range(minIntensity * 0.9f, maxIntensity * 1.15f);
            nextPageDuration = Mathf.Min(nextPageDuration, 0.8f); // Alertas costumam ser rápidos
        }
        else
        {
            // Escolhe entre branco suave, ciano e azul
            int pick = Random.Range(0, 3);
            switch (pick)
            {
                case 0:
                    targetColor = terminalWhite;
                    targetIntensity = Random.Range(minIntensity * 1.1f, maxIntensity);
                    break;
                case 1:
                    targetColor = terminalCyan;
                    targetIntensity = Random.Range(minIntensity, maxIntensity);
                    break;
                case 2:
                    targetColor = terminalDeepBlue;
                    targetIntensity = Random.Range(minIntensity * 0.85f, maxIntensity * 0.95f);
                    break;
            }
        }

        // Mini pulo de intensidade simulando a transição de renderização da tela
        if (Random.value > 0.4f)
        {
            currentIntensity += (Random.value > 0.5f ? 0.2f : -0.2f) * targetIntensity;
        }
    }
}
