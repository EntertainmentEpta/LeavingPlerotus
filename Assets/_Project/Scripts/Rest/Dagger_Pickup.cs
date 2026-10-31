using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gerencia a coleta da Adaga no chão da base / lobby.
/// - Exibe o prompt visual padronizado [ F ] Pegar Adaga de Plasma com CanvasGroup fade.
/// - Suporta ObjectiveMarker automático com flutuação da exclamação.
/// - Ao pegar: equipa na mão do jogador e avança o Tutorial/Quest.
/// </summary>
public class Dagger_Pickup : MonoBehaviour
{
    [Header("Configuração de Interação")]
    public string actionText = "Empunhar Machado de Cristal";
    public float fadeSpeed = 6f;
    public Vector3 promptOffset = new Vector3(0f, 0.65f, 0f);
    [Tooltip("Tamanho da fonte aumentado em 2x")]
    public float promptFontSize = 42f;

    private bool playerIsNear = false;
    private GameObject playerObject;

    private GameObject promptUI;
    private TextMeshProUGUI promptTextMesh;
    private CanvasGroup promptCanvasGroup;
    private WeaponSelectionHighlight selectionHighlight;

    private void Start()
    {
        if (gameObject.name.ToLower().Contains("dagger") || gameObject.name.ToLower().Contains("adaga"))
        {
            gameObject.SetActive(false);
            return;
        }
        // Garante que a adaga tenha o ObjectiveMarker enquanto estiver no chão
        if (GetComponent<ObjectiveMarker>() == null)
        {
            ObjectiveMarker marker = gameObject.AddComponent<ObjectiveMarker>();
            marker.condition = ObjectiveMarker.TriggerCondition.AlwaysActive;
            marker.heightOffset = 0.85f;
        }

        BuildPromptUI();

        if (GetComponent<WeaponSelectionHighlight>() == null)
        {
            selectionHighlight = gameObject.AddComponent<WeaponSelectionHighlight>();
            selectionHighlight.autoProximity = false;
            selectionHighlight.outlineWidth = 0.018f;
        }
    }

    private KeyCode GetCurrentInteractKey()
    {
        string savedKey = PlayerPrefs.GetString("KeyInteract", "F");
        try { return (KeyCode)System.Enum.Parse(typeof(KeyCode), savedKey); }
        catch { return KeyCode.F; }
    }

    private void BuildPromptUI()
    {
        promptUI = new GameObject("Dagger_PromptUI");
        promptUI.transform.SetParent(transform, false);
        promptUI.transform.localPosition = promptOffset;

        Canvas canvas = promptUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;

        RectTransform rect = promptUI.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(720f, 120f);
        rect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
        promptCanvasGroup.alpha = 0f;

        // Fundo cinza bem sutil e transparente (sem bordas ciano)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(promptUI.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.07f, 0.35f);
        bgImg.raycastTarget = false;

        Shader uiShader = Shader.Find("UI/Default");
        if (uiShader != null)
        {
            Material bgMat = new Material(uiShader);
            bgMat.SetFloat(Shader.PropertyToID("unity_GUIZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            bgMat.renderQueue = 3999;
            bgImg.material = bgMat;
        }

        // Borda ciano removida completamente para visual minimalista e flutuante

        // Texto do prompt
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptUI.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        promptTextMesh = textObj.AddComponent<TextMeshProUGUI>();
        promptTextMesh.fontSize = promptFontSize;
        promptTextMesh.fontStyle = FontStyles.Bold;
        promptTextMesh.alignment = TextAlignmentOptions.Center;
        promptTextMesh.color = Color.white;
        promptTextMesh.raycastTarget = false;
        promptTextMesh.outlineWidth = 0.22f;
        promptTextMesh.outlineColor = new Color32(0, 0, 0, 220);
        promptTextMesh.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (font != null) promptTextMesh.font = font;

        if (promptTextMesh.fontSharedMaterial != null)
        {
            Material fontOverlayMat = new Material(promptTextMesh.fontSharedMaterial);
            Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay") ??
                                   Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            if (overlayShader != null) fontOverlayMat.shader = overlayShader;
            fontOverlayMat.SetFloat(Shader.PropertyToID("unity_GUIZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            fontOverlayMat.SetFloat(Shader.PropertyToID("_ZTestMode"), (float)UnityEngine.Rendering.CompareFunction.Always);
            fontOverlayMat.renderQueue = 4000;
            promptTextMesh.fontSharedMaterial = fontOverlayMat;
        }

        promptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = true;
            playerObject = other.gameObject;
            if (promptTextMesh != null)
            {
                promptTextMesh.text = $"[ <color=#FFE500>{GetCurrentInteractKey()}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = false;
            playerObject = null;
        }
    }

    private void Update()
    {
        if (promptUI != null && promptCanvasGroup != null)
        {
            float targetAlpha = playerIsNear ? 1f : 0f;

            if (playerIsNear && !promptUI.activeSelf)
            {
                promptUI.SetActive(true);
                if (promptTextMesh != null)
                {
                    promptTextMesh.text = $"[ <color=#FFE500>{GetCurrentInteractKey()}</color> ]  <color=#FFE500><b>{actionText}</b></color>";
                }
            }

            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);

            if (!playerIsNear && promptCanvasGroup.alpha <= 0.01f && promptUI.activeSelf)
            {
                promptUI.SetActive(false);
            }

            if (promptUI.activeSelf && Camera.main != null)
            {
                promptUI.transform.forward = Camera.main.transform.forward;
            }
        }

        if (selectionHighlight != null)
        {
            selectionHighlight.SetHighlighted(playerIsNear);
        }

        if (playerIsNear && Input.GetKeyDown(GetCurrentInteractKey()))
        {
            PickUpDagger();
        }
    }

    private void PickUpDagger()
    {
        if (playerObject == null) return;

        Player_WeaponManager wm = playerObject.GetComponent<Player_WeaponManager>();
        if (wm != null)
        {
            // Remove o highlight e UI antes de equipar
            if (selectionHighlight != null) Destroy(selectionHighlight);
            if (promptUI != null) Destroy(promptUI);

            ObjectiveMarker marker = GetComponent<ObjectiveMarker>();
            if (marker != null)
            {
                marker.CompleteMarker();
                Destroy(marker);
            }

            // Completa o tutorial e quests
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.CompleteStep(TutorialStep.PegarMachado);
                TutorialManager.Instance.CompleteStep(TutorialStep.EquiparNoRack);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AdvanceQuest(QuestType.Interact, "weapon_equipped", 1);
                QuestManager.Instance.AdvanceQuest(QuestType.Interact, "axe_pickup", 1);
                QuestManager.Instance.AdvanceQuest(QuestType.Interact, "dagger_pickup", 1);
            }

            wm.EquipDagger(this.gameObject);
            Debug.Log("[Dagger_Pickup] Adaga de Plasma coletada e empunhada pelo jogador!");
        }
    }
}