using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cria uma câmera de preview 3D focada no Robozinho de Crafting no próprio cenário.
/// Renderiza a imagem do robô em um RawImage na interface com iluminação dedicada.
/// Não altera layers da cena nem instancia clones artificiais, mantendo a integridade da física.
/// </summary>
public class RobotPreviewManager : MonoBehaviour
{
    public static RobotPreviewManager Instance { get; private set; }

    [Header("Configurações da Câmera")]
    [Tooltip("Z é a distância na frente do robô. Y é a altura (para mirar no rosto/peito).")]
    public Vector3 cameraOffset = new Vector3(0f, 1.25f, 2.3f);
    public float fieldOfView = 38f;
    [Tooltip("Intensidade da luz suave de preenchimento para o robô no menu")]
    public float lightIntensity = 2.5f;
    [Tooltip("Cor de fundo para áreas fora do enquadramento")]
    [SerializeField] private Color previewBackgroundColor = new Color(0.06f, 0.08f, 0.10f, 1f);

    private Camera previewCamera;
    private RenderTexture renderTexture;
    private RawImage targetRawImage;
    private GameObject lightObject;

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
    /// Configura onde a imagem 3D será exibida na UI
    /// </summary>
    public void SetupPreview(RawImage rawImage)
    {
        targetRawImage = rawImage;

        if (targetRawImage != null)
        {
            RectTransform rectTrans = targetRawImage.GetComponent<RectTransform>();
            int width = rectTrans != null && rectTrans.rect.width > 10 ? Mathf.RoundToInt(rectTrans.rect.width * 2f) : 512;
            int height = rectTrans != null && rectTrans.rect.height > 10 ? Mathf.RoundToInt(rectTrans.rect.height * 2f) : 1024;

            if (width <= 0) width = 512;
            if (height <= 0) height = 1024;

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

            if (previewCamera != null)
            {
                previewCamera.targetTexture = renderTexture;
            }
        }
    }

    /// <summary>
    /// Ativa a visualização 3D focando diretamente no Robozinho no cenário.
    /// Exclui camadas de UI e Player para que nada obstrua o enquadramento.
    /// </summary>
    public void Activate(Transform robotTransform)
    {
        if (robotTransform == null) return;

        // 1. Garante o RenderTexture antes de ligar a câmera
        if (renderTexture == null)
        {
            int width = targetRawImage != null && targetRawImage.rectTransform.rect.width > 10 ? Mathf.RoundToInt(targetRawImage.rectTransform.rect.width * 2f) : 512;
            int height = targetRawImage != null && targetRawImage.rectTransform.rect.height > 10 ? Mathf.RoundToInt(targetRawImage.rectTransform.rect.height * 2f) : 1024;
            if (width <= 0) width = 512;
            if (height <= 0) height = 1024;

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = 4;
            renderTexture.Create();

            if (targetRawImage != null)
            {
                targetRawImage.texture = renderTexture;
                targetRawImage.enabled = true;
            }
        }

        // 2. Inicializa a câmera dedicada e luz se ainda não existirem
        if (previewCamera == null)
        {
            GameObject camObj = new GameObject("RobotPreviewCamera");
            camObj.transform.SetParent(transform, false);

            previewCamera = camObj.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = previewBackgroundColor;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 50f;
            previewCamera.depth = -10;

            // Filtra: renderiza tudo do cenário e robô, EXCETO UI e Player
            int uiLayer = LayerMask.NameToLayer("UI");
            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = ~0;
            if (uiLayer >= 0) mask &= ~(1 << uiLayer);
            if (playerLayer >= 0) mask &= ~(1 << playerLayer);
            previewCamera.cullingMask = mask;

            var camData = camObj.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null) camData = camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = false;
                camData.renderShadows = false;
            }

            // Luz suave frontal (Key Light para destacar a silhueta metálica)
            lightObject = new GameObject("RobotPreviewLight");
            lightObject.transform.SetParent(camObj.transform, false);
            lightObject.transform.localPosition = new Vector3(-0.35f, 0.7f, 0.5f);

            Light lightComp = lightObject.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.range = 8f;
            lightComp.intensity = lightIntensity;
            lightComp.color = new Color(0.92f, 0.96f, 1.0f);
        }

        // 3. Posiciona a câmera diretamente na frente do robô no mundo real
        Vector3 targetLookAt = robotTransform.position + (Vector3.up * cameraOffset.y);
        Vector3 camPos = robotTransform.position + (robotTransform.forward * cameraOffset.z) + (Vector3.up * cameraOffset.y) + (robotTransform.right * cameraOffset.x);

        previewCamera.transform.position = camPos;
        previewCamera.transform.LookAt(targetLookAt);

        previewCamera.targetTexture = renderTexture;
        previewCamera.enabled = true;

        if (targetRawImage != null)
        {
            targetRawImage.texture = renderTexture;
            targetRawImage.enabled = true;
        }
    }

    /// <summary>
    /// Desliga a câmera de preview quando o menu fecha
    /// </summary>
    public void Deactivate()
    {
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (previewCamera != null)
        {
            Destroy(previewCamera.gameObject);
            previewCamera = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }
}