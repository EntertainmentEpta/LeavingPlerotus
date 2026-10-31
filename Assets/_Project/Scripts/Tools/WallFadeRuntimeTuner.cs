using UnityEngine;

/// <summary>
/// Permite calibrar o Dither Fade das paredes em tempo real (Play Mode).
/// Atualiza as propriedades do shader DitherWall_URP instantaneamente
/// através do Inspector ou de pequenos sliders na tela (GUI).
/// </summary>
public class WallFadeRuntimeTuner : MonoBehaviour
{
    [Header("Material da Parede")]
    [Tooltip("Material que usa o shader Custom/DitherWall_URP. Se vazio, o script busca automaticamente.")]
    public Material targetMaterial;

    [Header("1. Proximidade da Câmera")]
    [Tooltip("Distância mínima da câmera onde a parede fica 100% invisível.")]
    [Range(0.2f, 8f)]
    public float fadeNearDistance = 1.0f;

    [Tooltip("Distância da câmera onde a parede volta a ficar 100% sólida.")]
    [Range(0.5f, 15f)]
    public float fadeFarDistance = 2.5f;

    [Tooltip("Ajuste de Altura do Fade no Eixo Y (Subir ou Descer o corte na parede).")]
    [Range(-3f, 6f)]
    public float fadeOffsetY = 1.5f;

    [Tooltip("Peso vertical da parede (0 = corte reto vertical da base ao topo, 1 = corte diagonal).")]
    [Range(0f, 1f)]
    public float verticalDistanceWeight = 0.2f;

    [Header("2. Foco no Player (Recorte ao Redor do Personagem)")]
    [Tooltip("Ativar recorte cilíndrico ao redor do jogador.")]
    public bool enablePlayerFade = true;

    [Tooltip("Raio de visibilidade ao redor do player (revela o astronauta por inteiro).")]
    [Range(0.5f, 6f)]
    public float playerFadeRadius = 2.2f;

    [Tooltip("Suavidade da borda de transição ao redor do player.")]
    [Range(0.2f, 3f)]
    public float playerFadeFeather = 1.0f;

    [Tooltip("Deslocamento de altura do centro do player no eixo Y.")]
    [Range(-2f, 4f)]
    public float playerYOffset = 0.5f;

    [Header("Estilo do Dither")]
    [Tooltip("Tamanho dos pontinhos de dither (1 = fino, 3 = padrão, 4-6 = estilizado).")]
    [Range(1f, 6f)]
    public float ditherDotSize = 3.0f;

    [Header("Controles em Tela (Opcional)")]
    [Tooltip("Exibir painel discreto com sliders no canto da tela durante o jogo.")]
    public bool showGui = true;

    private static readonly int PropNear = Shader.PropertyToID("_FadeNearDistance");
    private static readonly int PropFar = Shader.PropertyToID("_FadeFarDistance");
    private static readonly int PropFadeOffsetY = Shader.PropertyToID("_FadeOffsetY");
    private static readonly int PropVertWeight = Shader.PropertyToID("_VerticalDistanceWeight");
    private static readonly int PropEnablePlayer = Shader.PropertyToID("_EnablePlayerFade");
    private static readonly int PropPlayerRadius = Shader.PropertyToID("_PlayerFadeRadius");
    private static readonly int PropPlayerFeather = Shader.PropertyToID("_PlayerFadeFeather");
    private static readonly int PropPlayerYOffset = Shader.PropertyToID("_PlayerYOffset");
    private static readonly int PropDotSize = Shader.PropertyToID("_DitherDotSize");

    private Transform playerTransform;

    private void Awake()
    {
        FindMaterialIfNeeded();
        ReadCurrentValues();
    }

    private void OnValidate()
    {
        ApplyValues();
    }

    private void Update()
    {
        // Tecla de atalho F9 para ligar/desligar painel em tela
        if (Input.GetKeyDown(KeyCode.F9))
        {
            showGui = !showGui;
        }

        // Garante que a posição do Player esteja sempre no shader (backup para MoveCam)
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerTransform != null)
        {
            Vector3 pPos = playerTransform.position;
            Shader.SetGlobalVector("_PlayerPos", new Vector4(pPos.x, pPos.y + 1.0f, pPos.z, 1.0f));
        }

        // Aplica valores no material
        ApplyValues();
    }

    private void FindMaterialIfNeeded()
    {
        if (targetMaterial != null) return;

        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.shader != null && m.shader.name == "Custom/DitherWall_URP")
                {
                    // Prioriza material que esteja com o fade ativo
                    if (m.HasProperty("_EnableDitherFade") && m.GetFloat("_EnableDitherFade") > 0.5f)
                    {
                        targetMaterial = m;
                        return;
                    }
                    if (targetMaterial == null) targetMaterial = m;
                }
            }
        }
    }

    private void ReadCurrentValues()
    {
        if (targetMaterial == null) return;

        if (targetMaterial.HasProperty(PropNear))
            fadeNearDistance = targetMaterial.GetFloat(PropNear);

        if (targetMaterial.HasProperty(PropFar))
            fadeFarDistance = targetMaterial.GetFloat(PropFar);

        if (targetMaterial.HasProperty(PropFadeOffsetY))
            fadeOffsetY = targetMaterial.GetFloat(PropFadeOffsetY);

        if (targetMaterial.HasProperty(PropVertWeight))
            verticalDistanceWeight = targetMaterial.GetFloat(PropVertWeight);

        if (targetMaterial.HasProperty(PropEnablePlayer))
            enablePlayerFade = targetMaterial.GetFloat(PropEnablePlayer) > 0.5f;

        if (targetMaterial.HasProperty(PropPlayerRadius))
            playerFadeRadius = targetMaterial.GetFloat(PropPlayerRadius);

        if (targetMaterial.HasProperty(PropPlayerFeather))
            playerFadeFeather = targetMaterial.GetFloat(PropPlayerFeather);

        if (targetMaterial.HasProperty(PropPlayerYOffset))
            playerYOffset = targetMaterial.GetFloat(PropPlayerYOffset);

        if (targetMaterial.HasProperty(PropDotSize))
            ditherDotSize = targetMaterial.GetFloat(PropDotSize);
    }

    public void ApplyValues()
    {
        if (targetMaterial == null) return;

        targetMaterial.SetFloat(PropNear, fadeNearDistance);
        targetMaterial.SetFloat(PropFar, Mathf.Max(fadeNearDistance + 0.1f, fadeFarDistance));
        targetMaterial.SetFloat(PropFadeOffsetY, fadeOffsetY);
        targetMaterial.SetFloat(PropVertWeight, verticalDistanceWeight);
        targetMaterial.SetFloat(PropEnablePlayer, enablePlayerFade ? 1.0f : 0.0f);
        targetMaterial.SetFloat(PropPlayerRadius, playerFadeRadius);
        targetMaterial.SetFloat(PropPlayerFeather, playerFadeFeather);
        targetMaterial.SetFloat(PropPlayerYOffset, playerYOffset);
        targetMaterial.SetFloat(PropDotSize, ditherDotSize);
    }

    private void OnGUI()
    {
        if (!showGui) return;

        GUILayout.BeginArea(new Rect(20, 20, 360, 420), "Calibração Dither Wall (F9 fechar)", GUI.skin.window);

        GUILayout.Space(8);
        GUILayout.Label($"<b>Subir Fade no Eixo Y (Altura):</b> {fadeOffsetY:F2}m");
        float newHeight = GUILayout.HorizontalSlider(fadeOffsetY, -3f, 6f);

        GUILayout.Label($"<b>Câmera Near (Invisível):</b> {fadeNearDistance:F2}m");
        float newNear = GUILayout.HorizontalSlider(fadeNearDistance, 0.2f, 6f);

        GUILayout.Label($"<b>Câmera Far (Sólida):</b> {fadeFarDistance:F2}m");
        float newFar = GUILayout.HorizontalSlider(fadeFarDistance, 0.5f, 10f);

        GUILayout.Label($"<b>Peso Vertical (0 = Reto, 1 = Diagonal):</b> {verticalDistanceWeight:F2}");
        float newWeight = GUILayout.HorizontalSlider(verticalDistanceWeight, 0f, 1f);

        GUILayout.Space(6);
        bool newEnablePlayer = GUILayout.Toggle(enablePlayerFade, " <b>Foco no Player (Recorte ao Redor)</b>");

        GUILayout.Label($"<b>Raio de Visão no Player:</b> {playerFadeRadius:F2}m");
        float newRadius = GUILayout.HorizontalSlider(playerFadeRadius, 0.5f, 6f);

        GUILayout.Label($"<b>Altura Player Y:</b> {playerYOffset:F2}m");
        float newPlayerY = GUILayout.HorizontalSlider(playerYOffset, -2f, 4f);

        GUILayout.Label($"<b>Suavidade do Player:</b> {playerFadeFeather:F2}m");
        float newFeather = GUILayout.HorizontalSlider(playerFadeFeather, 0.2f, 3f);

        GUILayout.Space(6);
        GUILayout.Label($"<b>Tamanho Pontinhos:</b> {ditherDotSize:F1}");
        float newDot = GUILayout.HorizontalSlider(ditherDotSize, 1f, 6f);

        if (newNear != fadeNearDistance || newFar != fadeFarDistance || newHeight != fadeOffsetY ||
            newWeight != verticalDistanceWeight || newEnablePlayer != enablePlayerFade ||
            newRadius != playerFadeRadius || newPlayerY != playerYOffset || newFeather != playerFadeFeather ||
            newDot != ditherDotSize)
        {
            fadeNearDistance = newNear;
            fadeFarDistance = Mathf.Max(fadeNearDistance + 0.1f, newFar);
            fadeOffsetY = newHeight;
            verticalDistanceWeight = newWeight;
            enablePlayerFade = newEnablePlayer;
            playerFadeRadius = newRadius;
            playerYOffset = newPlayerY;
            playerFadeFeather = newFeather;
            ditherDotSize = newDot;
            ApplyValues();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Copiar Valores para o Console"))
        {
            Debug.Log($"[WallFadeRuntimeTuner] FadeOffsetY={fadeOffsetY:F2} | Near={fadeNearDistance:F2} | Far={fadeFarDistance:F2} | VertWeight={verticalDistanceWeight:F2} | PlayerRadius={playerFadeRadius:F2} | PlayerY={playerYOffset:F2}");
        }

        GUILayout.EndArea();
    }
}
