using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cria uma câmera de preview 3D focada no Robozinho de Crafting.
/// Renderiza a imagem do robô em um RawImage na interface.
/// </summary>
public class RobotPreviewManager : MonoBehaviour
{
    public static RobotPreviewManager Instance { get; private set; }

    [Header("Configurações da Câmera")]
    [Tooltip("Z é a distância na frente do robô. Y é a altura (para mirar no rosto/peito).")]
    public Vector3 cameraOffset = new Vector3(0f, 1.2f, 2.5f);
    public float fieldOfView = 40f;
    [Tooltip("Intensidade da luz para iluminar o robô no menu")]
    public float lightIntensity = 3.0f;
    [Tooltip("Layer temporária usada para impedir que o jogador e o cenário apareçam no preview")]
    [SerializeField] private int previewLayer = 31;
    [Tooltip("Cor neutra do fundo do preview")]
    [SerializeField] private Color previewBackgroundColor = new Color(0.075f, 0.10f, 0.115f, 1f);

    private Camera previewCamera;
    private RenderTexture renderTexture;
    private RawImage targetRawImage;
    private readonly System.Collections.Generic.List<Transform> previewLayerObjects = new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<int> originalLayers = new System.Collections.Generic.List<int>();

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
            int width = rectTrans != null ? Mathf.RoundToInt(rectTrans.rect.width * 2f) : 512;
            int height = rectTrans != null ? Mathf.RoundToInt(rectTrans.rect.height * 2f) : 1024;

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
        }
    }

    /// <summary>
    /// Ativa a câmera posicionada na frente do robô específico
    /// </summary>
/// <summary>
    /// Ativa a câmera posicionada na frente do robô específico
    /// </summary>
    public void Activate(Transform robotTransform)
    {
        if (robotTransform == null) return;

        if (previewCamera == null)
        {
            GameObject camObj = new GameObject("RobotPreviewCamera");

            previewCamera = camObj.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = previewBackgroundColor;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.cullingMask = 1 << previewLayer;

            // Luz dedicada
            GameObject lightObj = new GameObject("RobotPreviewLight");
            lightObj.transform.SetParent(camObj.transform, false);
            lightObj.transform.localPosition = new Vector3(-0.5f, 1f, 0f);
            
            Light lightComp = lightObj.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.range = 10f;
            lightComp.intensity = lightIntensity;
            lightComp.color = new Color(0.9f, 0.95f, 1.0f);
        }

        RestoreRobotLayers();
        StoreAndApplyPreviewLayer(robotTransform);

        // Calcula a posição ideal MUNDIAL
        Vector3 targetPos = robotTransform.position + (robotTransform.forward * cameraOffset.z) + (Vector3.up * cameraOffset.y);
        targetPos += robotTransform.right * cameraOffset.x;

        previewCamera.transform.position = targetPos;
        previewCamera.transform.LookAt(robotTransform.position + (Vector3.up * cameraOffset.y));

        previewCamera.targetTexture = renderTexture;
        previewCamera.enabled = true;
    }

    /// <summary>
    /// Desliga a câmera quando o menu fecha
    /// </summary>
    public void Deactivate()
    {
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
        }

        RestoreRobotLayers();
    }

    private void StoreAndApplyPreviewLayer(Transform root)
    {
        previewLayerObjects.Clear();
        originalLayers.Clear();

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            previewLayerObjects.Add(child);
            originalLayers.Add(child.gameObject.layer);
            child.gameObject.layer = previewLayer;
        }
    }

    private void RestoreRobotLayers()
    {
        if (previewLayerObjects.Count != originalLayers.Count) return;

        for (int index = 0; index < previewLayerObjects.Count; index++)
        {
            if (previewLayerObjects[index] != null)
            {
                previewLayerObjects[index].gameObject.layer = originalLayers[index];
            }
        }

        previewLayerObjects.Clear();
        originalLayers.Clear();
    }

    private void OnDestroy()
    {
        RestoreRobotLayers();

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}