using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// =================================================================================
/// INTERAÇÕES VISUAIS E ANIMAÇÃO DE FLUTUAÇÃO DAS CARTAS DE TARÔ
/// =================================================================================
/// Desenvolvido por: Vicenzo (Branch: VicenzoWS)
/// 
/// Funcionalidades Principais Implementadas:
/// 1. Elevação Dinâmica de 22px (liftAmountY) no eixo Y ao passar o cursor do mouse (Hover).
/// 2. Tint de Cor Avermelhado Escuro (#FF6666) na borda e fundo do card no Hover.
/// 3. Oscilação de Flutuação Mística (Wave Float) com onda senoidal orgânica e deslocamento de fase aleatório por carta.
/// 4. Efeitos de Clique (Press/Release Feedback) com transições suaves usando Vector3.Lerp.
/// =================================================================================
/// </summary>
public class MerchantCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // SFX de hover injetado pelo MerchantUIController (não precisa configurar aqui)
    private AudioClip hoverClip;
    private float hoverVolume = 0.7f;
    private AudioSource audioSource;
    [Header(" Configurações de Elevação & Escala no Hover")]
    public float hoverScale = 1.08f;
    public float clickScale = 0.95f;
    [Tooltip("Distância que a carta sobe ao passar o mouse por cima")]
    public float liftAmountY = 22f; 
    public float transitionSpeed = 14f;

    [Header(" Flutuação Mística em Repouso (Wave Float)")]
    public bool enableFloating = true;
    [Tooltip("Altura da oscilação (subindo e descendo)")]
    public float floatAmplitude = 6.0f;
    [Tooltip("Velocidade da oscilação suave")]
    public float floatSpeed = 2.2f;
    private float floatPhase;

    [Header(" Tint Avermelhada no Hover")]
    public bool enableColorChange = true;
    public Color normalBorderColor = Color.white; // Preserva a cor original das cartas
    public Color hoverBorderColor = new Color(1.0f, 0.40f, 0.40f, 1.0f); // Leve tom avermelhado no hover

    [Header(" Efeito Glitch de Fonte (Apenas em Cartas de Pacto)")]
    public bool isPactCard = false; // Por padrão falso, apenas ativado em cartas de pacto
    public bool enableGlitchFont = true;

    private Vector3 initialScale;
    private Vector3 initialPosition;
    private Vector3 targetScale;
    private Vector3 targetPositionOffset;

    private Image borderImage;
    private TextMeshProUGUI cardTitleText;
    private Vector3 originalTitlePos;

    private bool isHovered = false;
    private bool isPressed = false;

    // Glitch Animation System (Menor frequência / intervalo maior)
    private float glitchTimer = 0f;
    private float glitchDurationTimer = 0f;
    private bool isGlitching = false;

    void Awake()
    {
        initialScale = transform.localScale;
        initialPosition = transform.localPosition;
        targetScale = initialScale;
        targetPositionOffset = Vector3.zero;

        // Fase aleatória para o temporizador de glitch
        floatPhase = Random.Range(0f, Mathf.PI * 2f);

        borderImage = GetComponent<Image>();
        cardTitleText = GetComponentInChildren<TextMeshProUGUI>();
        if (cardTitleText != null)
        {
            originalTitlePos = cardTitleText.transform.localPosition;
        }

        // Garante temporizador inicial desfasado para evitar disparos simultâneos
        glitchTimer = Random.Range(2.0f, 5.0f);
    }

    /// <summary>
    /// Chamado pelo MerchantUIController para injetar o som de hover nas cartas.
    /// </summary>
    public void SetHoverAudio(AudioClip clip, float volume, AudioSource source)
    {
        hoverClip = clip;
        hoverVolume = volume;
        audioSource = source;
    }

    void OnEnable()
    {
        ResetToNormal();
    }

    public void ResetToNormal()
    {
        isHovered = false;
        isPressed = false;
        targetScale = initialScale;
        targetPositionOffset = Vector3.zero;
        transform.localScale = initialScale;
        transform.localPosition = initialPosition;

        if (enableColorChange && borderImage != null)
        {
            borderImage.color = normalBorderColor;
        }
    }

    void Update()
    {
        // 1. Calcula a flutuação mística sinusoidal da CARTA (subindo e descendo suavemente)
        float floatOffsetY = (enableFloating && !isPressed) ? Mathf.Sin(Time.unscaledTime * floatSpeed + floatPhase) * floatAmplitude : 0f;

        // 2. Animação de lerp fluida para posição da carta (elevação + flutuação) e escala
        Vector3 targetPos = initialPosition + targetPositionOffset + new Vector3(0f, floatOffsetY, 0f);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.unscaledDeltaTime * transitionSpeed);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);

        // 3. Animação de cor da carta: 80% mais escura (quase preta), destacando tom carmesim no hover
        if (enableColorChange && borderImage != null)
        {
            Color currentTargetColor = isHovered ? hoverBorderColor : normalBorderColor;
            borderImage.color = Color.Lerp(borderImage.color, currentTargetColor, Time.unscaledDeltaTime * transitionSpeed);
        }

        // 4. Efeito Glitch Digital / Dark Fantasy na Fonte do Título (Apenas cartas de pacto com intervalo maior)
        UpdateGlitchFontAnimation();
    }

    private void UpdateGlitchFontAnimation()
    {
        if (!enableGlitchFont || !isPactCard) return;

        if (cardTitleText == null)
        {
            cardTitleText = GetComponentInChildren<TextMeshProUGUI>();
            if (cardTitleText != null)
            {
                originalTitlePos = cardTitleText.transform.localPosition;
            }
        }

        if (cardTitleText == null) return;

        if (!isGlitching && cardTitleText.transform.localPosition != Vector3.zero)
        {
            originalTitlePos = cardTitleText.transform.localPosition;
        }

        // Intervalo de disparo de Glitch (Subtil, menos recorrente)
        glitchTimer -= Time.unscaledDeltaTime;
        if (glitchTimer <= 0f)
        {
            isGlitching = true;
            glitchDurationTimer = Random.Range(0.03f, 0.06f); // Surto muito rápido de glitch (30-60ms)
            // Intervalo espaçado: 3.5s a 7.0s em repouso; 1.2s a 2.5s no hover
            float nextInterval = isHovered ? Random.Range(1.2f, 2.5f) : Random.Range(3.5f, 7.0f);
            glitchTimer = nextInterval;
        }

        if (isGlitching)
        {
            glitchDurationTimer -= Time.unscaledDeltaTime;
            if (glitchDurationTimer <= 0f)
            {
                isGlitching = false;
                cardTitleText.transform.localPosition = originalTitlePos;
                cardTitleText.transform.localScale = Vector3.one;
            }
            else
            {
                // Micro-deslocamento (Saltos de posição tipo Glitch)
                float offsetX = Random.Range(-2.2f, 2.2f);
                float offsetY = Random.Range(-1.2f, 1.2f);
                cardTitleText.transform.localPosition = originalTitlePos + new Vector3(offsetX, offsetY, 0f);

                // Distorção rápida de escala
                float scaleGlitch = Random.Range(0.94f, 1.14f);
                cardTitleText.transform.localScale = new Vector3(scaleGlitch, scaleGlitch, 1f);

                // Flashes de Cores Glitch (Ouro, Carmesim Dark Fantasy e Cianita)
                Color[] glitchColors = {
                    new Color(1.0f, 0.85f, 0.20f, 1.0f), // Ouro
                    new Color(1.0f, 0.15f, 0.25f, 1.0f), // Carmesim
                    new Color(0.0f, 0.90f, 1.0f, 1.0f)   // Cianita
                };
                cardTitleText.color = glitchColors[Random.Range(0, glitchColors.Length)];
            }
        }
        else
        {
            // Estado Firme Normal: Sem flutuacão senoidal na fonte, mantém posição limpa e tom dourado/carmesim
            cardTitleText.transform.localPosition = originalTitlePos;
            cardTitleText.transform.localScale = Vector3.one;
            cardTitleText.transform.localRotation = Quaternion.identity;

            Color baseGold = isHovered ? new Color(1.0f, 0.30f, 0.35f, 1.0f) : new Color(1.0f, 0.85f, 0.20f, 1.0f);
            cardTitleText.color = Color.Lerp(cardTitleText.color, baseGold, Time.unscaledDeltaTime * 12f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (!isPressed)
        {
            targetScale = initialScale * hoverScale;
            targetPositionOffset = new Vector3(0f, liftAmountY, 0f);
        }

        // SFX de carta trocando ao passar o mouse
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip, hoverVolume);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        targetScale = initialScale;
        targetPositionOffset = Vector3.zero;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        targetScale = initialScale * clickScale;
        targetPositionOffset = Vector3.zero;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (isHovered)
        {
            targetScale = initialScale * hoverScale;
            targetPositionOffset = new Vector3(0f, liftAmountY, 0f);
        }
        else
        {
            targetScale = initialScale;
            targetPositionOffset = Vector3.zero;
        }
    }
}
