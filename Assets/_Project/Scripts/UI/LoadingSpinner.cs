using UnityEngine;
using UnityEngine.UI;

public class LoadingSpinner : MonoBehaviour
{
    [Header("Animação Pixel Art (GIF / Frames do Eptinho)")]
    [Tooltip("Frames da animação do Eptinho andando (extraídos do GIF ou Sprite Sheet). Se preenchido, substitui a rotação.")]
    public Sprite[] walkingFrames;

    [Tooltip("Velocidade da animação em quadros por segundo (FPS).")]
    [Range(1f, 60f)]
    public float framesPerSecond = 10f;

    [Tooltip("Garante FilterMode.Point (sem borrado) e PreserveAspect para manter a Pixel Art nítida.")]
    public bool enforcePixelArtFilter = true;

    [Header("Movimento Opcional na Barra")]
    [Tooltip("Se ativado, o Eptinho caminha da esquerda para a direita acompanhando o preenchimento da barra de loading.")]
    public bool walkAlongLoadingBar = false;
    public Slider loadingBarToFollow;
    public float walkOffsetY = 95f;

    [Header("Fallback (Enquanto o GIF não for colocado)")]
    [Tooltip("Se verdadeiro e 'walkingFrames' estiver vazio, mantém o giro antigo até o GIF ser importado.")]
    public bool spinWhenNoFrames = true;
    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = -150f;

    private Image targetImage;
    private RectTransform rectTransform;
    private int currentFrameIndex = 0;
    private float frameTimer = 0f;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        TryAutoLoadFrames();
        ConfigureImageForAnimation();
    }

    private void OnEnable()
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        TryAutoLoadFrames();
        ConfigureImageForAnimation();

        currentFrameIndex = 0;
        frameTimer = 0f;

        if (HasAnimationFrames() && targetImage != null)
        {
            targetImage.sprite = walkingFrames[0];
        }
    }

    private void TryAutoLoadFrames()
    {
        if (HasAnimationFrames()) return;

        // Procura automaticamente em Resources/Loading/Frames ou Resources/Loading/EptinhoWalking
        Sprite[] loaded = Resources.LoadAll<Sprite>("Loading/Frames");
        if (loaded == null || loaded.Length == 0)
        {
            loaded = Resources.LoadAll<Sprite>("Loading/EptinhoWalking");
        }

        if (loaded != null && loaded.Length > 0)
        {
            walkingFrames = loaded;
        }
    }

    public bool HasAnimationFrames()
    {
        return walkingFrames != null && walkingFrames.Length > 0 && walkingFrames[0] != null;
    }

    public void ConfigureImageForAnimation()
    {
        if (targetImage == null) return;

        // Corrige o Image Type (que antes estava como Sliced) para Simple + PreserveAspect
        targetImage.type = Image.Type.Simple;
        targetImage.preserveAspect = true;

        if (HasAnimationFrames())
        {
            // Reseta a rotação para o Eptinho andar em pé (sem ficar de cabeça para baixo)
            transform.localRotation = Quaternion.identity;

            if (enforcePixelArtFilter)
            {
                foreach (Sprite s in walkingFrames)
                {
                    if (s != null && s.texture != null)
                    {
                        s.texture.filterMode = FilterMode.Point;
                    }
                }
            }
        }
        else if (targetImage.sprite == null)
        {
            Sprite[] fallbackSprites = Resources.LoadAll<Sprite>("Loading/EptinhoFallback");
            if (fallbackSprites != null && fallbackSprites.Length > 0)
            {
                targetImage.sprite = fallbackSprites[0];
            }
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (HasAnimationFrames())
        {
            // Garante que não gire quando estiver usando a animação de caminhada
            if (transform.localRotation != Quaternion.identity)
                transform.localRotation = Quaternion.identity;

            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            frameTimer += dt;

            if (frameTimer >= frameDuration)
            {
                int steps = Mathf.FloorToInt(frameTimer / frameDuration);
                frameTimer -= steps * frameDuration;
                currentFrameIndex = (currentFrameIndex + steps) % walkingFrames.Length;

                if (targetImage != null && walkingFrames[currentFrameIndex] != null)
                {
                    targetImage.sprite = walkingFrames[currentFrameIndex];
                }
            }

            if (walkAlongLoadingBar && loadingBarToFollow != null && rectTransform != null)
            {
                RectTransform barRect = loadingBarToFollow.GetComponent<RectTransform>();
                if (barRect != null)
                {
                    float halfWidth = barRect.sizeDelta.x * 0.5f;
                    float xPos = barRect.anchoredPosition.x + Mathf.Lerp(-halfWidth, halfWidth, loadingBarToFollow.value);
                    rectTransform.anchoredPosition = new Vector2(xPos, barRect.anchoredPosition.y + walkOffsetY);
                }
            }
        }
        else if (spinWhenNoFrames)
        {
            // Fallback: gira a imagem estática enquanto o GIF ainda não foi adicionado
            transform.Rotate(0f, 0f, rotationSpeed * dt);
        }
    }
}

