using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cria um palco 3D escondido e renderiza a skin ativa do jogador
/// em tempo real para uma textura exibida na UI do Inventário.
/// </summary>
public class PlayerPreviewManager : MonoBehaviour
{
    public static PlayerPreviewManager Instance { get; private set; }

    [Header("Configurações do Palco")]
    [Tooltip("Posição onde o palco 3D do preview será criado. Longe do cenário.")]
    public Vector3 stagePosition = new Vector3(1000f, 1000f, 1000f);
    [Tooltip("Velocidade de rotação do personagem.")]
    public float rotationSpeed = 30f;
    [Tooltip("Resolução horizontal do render texture.")]
    public int textureWidth = 512;
    [Tooltip("Resolução vertical do render texture.")]
    public int textureHeight = 512;

    [Header("Ajustes da Câmera de Preview (Tempo Real)")]
    [Tooltip("Offset local (X, Y, Z) da câmera de preview em relação ao player (posição IDEAL sem paredes).")]
    public Vector3 previewCamOffset = new Vector3(0f, 1.5f, 5.0f);
    [Tooltip("Campo de visão da câmera (FOV).")]
    public float previewCamFOV = 35f;
    [Tooltip("Inclinação da câmera no eixo X.")]
    public float previewCamRotationX = 3f;
    [Tooltip("Intensidade da iluminação frontal.")]
    public float previewLightIntensity = 4.0f;

    [Header("Spring Arm — Colisão com Paredes")]
    [Tooltip("Ativa o Spring Arm: câmera se aproxima do player quando há uma parede no caminho.")]
    public bool enableSpringArm = true;
    [Tooltip("Raio da esfera de detecção de colisão (em metros). Valores maiores detectam antes.")]
    [Range(0.05f, 0.5f)]
    public float springArmProbeRadius = 0.15f;
    [Tooltip("Recuo mínimo da câmera em relação ao ponto de colisão (evita z-fighting).")]
    [Range(0.05f, 0.5f)]
    public float springArmWallOffset = 0.2f;
    [Tooltip("Velocidade de aproximação (quando parede é detectada). Menor = mais suave.")]
    [Range(1f, 30f)]
    public float springArmZoomSpeed = 12f;
    [Tooltip("Velocidade de recuo (quando câmera volta à posição ideal). Menor = mais suave.")]
    [Range(1f, 15f)]
    public float springArmReturnSpeed = 6f;
    [Tooltip("Layers que bloqueiam a câmera (paredes, chão, etc). Exclua Player, Enemy, Item.")]
    public LayerMask springArmCollisionMask = ~0; // tudo por padrão; ajuste no Inspector


    private Camera previewCamera;
    private RenderTexture renderTexture;
    private RawImage targetRawImage;

    // Spring Arm: distância atual da câmera (interpolada suavemente)
    private float _springArmCurrentDist;
    // Flag para saber se a distância foi inicializada (evita snap no primeiro frame)
    private bool _springArmInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Configura o RawImage da UI para exibir a textura de renderização da câmera 3D
    /// </summary>
    public void SetupPreview(RawImage rawImage)
    {
        targetRawImage = rawImage;

        if (targetRawImage != null)
        {
            // Pega as dimensões do RectTransform para evitar qualquer distorção de aspecto (anti-esticamento)
            RectTransform rectTrans = targetRawImage.GetComponent<RectTransform>();
            int width = 512;
            int height = 1024;
            if (rectTrans != null)
            {
                width = Mathf.RoundToInt(rectTrans.rect.width * 2f); // Supersampling x2 para nitidez premium
                height = Mathf.RoundToInt(rectTrans.rect.height * 2f);
            }

            // Garante dimensões válidas
            if (width <= 0) width = 512;
            if (height <= 0) height = 1024;

            // Recria a textura se as dimensões mudarem
            if (renderTexture != null && (renderTexture.width != width || renderTexture.height != height))
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }

            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = 4;
                renderTexture.Create();
            }

            targetRawImage.texture = renderTexture;
            targetRawImage.enabled = true;
        }
    }

    /// <summary>
    /// Ativa o preview 3D, acoplando a câmera ao jogador real da cena
    /// </summary>
    public void Activate()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            // Tenta localizar ou criar a câmera de preview acoplada ao player
            Transform camT = player.transform.Find("PlayerPreviewCamera");
            if (camT == null)
            {
                GameObject camObj = new GameObject("PlayerPreviewCamera");
                camObj.transform.SetParent(player.transform, false);
                
                // Posiciona com base no previewCamOffset
                camObj.transform.localPosition = previewCamOffset;
                camObj.transform.localRotation = Quaternion.Euler(previewCamRotationX, 180f, 0f);

                previewCamera = camObj.AddComponent<Camera>();
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                // Fundo cinza escuro/preto para destacar o personagem
                previewCamera.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
                previewCamera.fieldOfView = previewCamFOV;
                previewCamera.nearClipPlane = 0.1f;
                previewCamera.farClipPlane = previewCamOffset.z + 8f;

                // Adiciona uma luz frontal dedicada anexada ao player para iluminá-lo no preview
                GameObject lightObj = new GameObject("PreviewFrontLight");
                lightObj.transform.SetParent(camObj.transform, false);
                lightObj.transform.localPosition = new Vector3(-0.5f, 1f, -0.5f);
                lightObj.transform.localRotation = Quaternion.Euler(20f, 20f, 0f);

                Light lightComp = lightObj.AddComponent<Light>();
                lightComp.type = LightType.Spot;
                lightComp.range = previewCamOffset.z * 2f;
                lightComp.spotAngle = 60f;
                lightComp.intensity = previewLightIntensity;
                lightComp.color = new Color(0.9f, 0.92f, 1.0f); // Brilho frio
            }
            else
            {
                previewCamera = camT.GetComponent<Camera>();
            }

            if (previewCamera != null)
            {
                previewCamera.targetTexture = renderTexture;
                previewCamera.enabled = true;
            }
        }
    }

    /// <summary>
    /// Desliga a câmera e limpa a câmera temporária do player
    /// </summary>
    public void Deactivate()
    {
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Transform camT = player.transform.Find("PlayerPreviewCamera");
            if (camT != null)
            {
                Destroy(camT.gameObject);
            }
        }
        previewCamera = null;
        _springArmInitialized = false; // reseta o estado do Spring Arm para o próximo Activate()
    }


    private void Update()
    {
        if (previewCamera == null || !previewCamera.enabled) return;

        // ── Aplica FOV em tempo real ───────────────────────────────────────────
        previewCamera.fieldOfView = previewCamFOV;

        // ── Spring Arm: evita que a câmera atravesse paredes ──────────────────
        // A câmera olha para o player de trás (180° no Y local).
        // O offset define o ponto IDEAL sem obstáculos.
        // Se houver uma parede no caminho, a câmera recua suavemente em direção ao player.

        // Ponto de referência ("ombro") no espaço do player para o eixo XY do offset.
        // O player é o parent da câmera, então o pivot do spring arm é o próprio player.
        Transform playerT = previewCamera.transform.parent; // o GameObject do Player

        if (enableSpringArm && playerT != null)
        {
            // Direção mundial do braço da câmera (de onde o player "lança" a câmera).
            // A câmera está deslocada em localPosition = previewCamOffset e rotacionada 180°
            // no Y para olhar de volta ao player, então o braço aponta na direção -forward do player
            // mais o offset lateral/vertical.

            // Distância ideal (comprimento total do braço sem obstáculos)
            float idealDist = previewCamOffset.z; // componente Z do offset = distância do player

            // Pivot = posição do player + offset XY em espaço mundial
            // (centro de onde o raio começa — altura dos olhos do personagem)
            Vector3 pivotWorldPos = playerT.position
                + playerT.right   * previewCamOffset.x
                + playerT.up      * previewCamOffset.y;

            // Direção do braço: da câmera em direção ao player (a câmera está "atrás" do player).
            // Como a câmera tem rotação 180° no Y, ela olha para -forward do player.
            // O braço vai de pivotWorldPos até pivotWorldPos + (-playerForward * idealDist).
            Vector3 armDirection = -playerT.forward; // câmera fica atrás do player

            // Inicializa a distância atual na primeira vez (sem snap)
            if (!_springArmInitialized)
            {
                _springArmCurrentDist = idealDist;
                _springArmInitialized = true;
            }

            // SphereCast do pivot na direção do braço até a distância ideal.
            // SphereCast é melhor que Raycast pois detecta paredes que o ray fino passaria.
            float targetDist = idealDist;

            // Exclui o próprio layer do Player e outros objetos irrelevantes da detecção
            int mask = springArmCollisionMask;
            // Remove layers de Player, Enemy, Item, Weapon, DeadBody da máscara
            mask &= ~LayerMask.GetMask("Player", "Enemy", "Item", "Weapon", "DeadBody", "Ignore Raycast");

            if (Physics.SphereCast(pivotWorldPos, springArmProbeRadius, armDirection,
                out RaycastHit hit, idealDist, mask, QueryTriggerInteraction.Ignore))
            {
                // Parede detectada: posiciona a câmera antes do ponto de impacto
                targetDist = Mathf.Max(0.05f, hit.distance - springArmWallOffset);
            }

            // Interpolação suave:
            // - Aproxima rápido (parede surgiu de repente)
            // - Recua devagar (parede sumiu, câmera volta ao lugar)
            float lerpSpeed = targetDist < _springArmCurrentDist
                ? springArmZoomSpeed       // aproxima rápido
                : springArmReturnSpeed;    // recua devagar

            _springArmCurrentDist = Mathf.Lerp(_springArmCurrentDist, targetDist, lerpSpeed * Time.deltaTime);

            // Aplica a posição calculada em espaço local do player
            Vector3 clampedOffset = new Vector3(previewCamOffset.x, previewCamOffset.y, _springArmCurrentDist);
            previewCamera.transform.localPosition = clampedOffset;
        }
        else
        {
            // Spring Arm desativado: comportamento original
            previewCamera.transform.localPosition = previewCamOffset;
            _springArmInitialized = false;
        }

        // ── Rotação e luz em tempo real ────────────────────────────────────────
        previewCamera.transform.localRotation = Quaternion.Euler(previewCamRotationX, 180f, 0f);

        Light lightComp = previewCamera.GetComponentInChildren<Light>();
        if (lightComp != null)
        {
            lightComp.intensity = previewLightIntensity;
            lightComp.range = previewCamOffset.z * 2f;
        }
    }


    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
