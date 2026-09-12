using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Script de interacao com a Mesa de Trabalho (Robozinho) na cena da Base.
/// Detecta proximidade do jogador e abre a UI de Crafting ao pressionar F.
/// Gera uma UI de tela (prompt) automaticamente.
/// </summary>
public class CraftingTableInteraction : MonoBehaviour
{
    private bool playerPerto = false;

    // Prompt estatico gerado em runtime
    private static GameObject s_promptCanvas;
    private static TextMeshProUGUI s_promptText;
    private static int s_activeCount = 0;

    [Header("Som (Opcional)")]
    public AudioClip roboSound;

    void Start()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;

        if (s_promptCanvas == null)
            CriarPromptUI();
    }

    void OnDestroy()
    {
        if (playerPerto)
        {
            playerPerto = false;
            s_activeCount = Mathf.Max(0, s_activeCount - 1);
            AtualizarPrompt();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (playerPerto) return;

        playerPerto = true;
        s_activeCount++;
        AtualizarPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerPerto) return;

        playerPerto = false;
        s_activeCount = Mathf.Max(0, s_activeCount - 1);
        AtualizarPrompt();

        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
            CraftingUI.Instance.CloseCrafting();
    }

    void Update()
    {
        if (!playerPerto) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (roboSound != null)
            {
                AudioSource src = GetComponent<AudioSource>();
                if (src == null) src = gameObject.AddComponent<AudioSource>();
                src.PlayOneShot(roboSound);
            }

            if (CraftingUI.Instance != null)
            {
                if (CraftingUI.Instance.IsOpen())
                    CraftingUI.Instance.CloseCrafting();
                else
                    CraftingUI.Instance.OpenCrafting();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
                CraftingUI.Instance.CloseCrafting();
        }
    }

    private static void CriarPromptUI()
    {
        s_promptCanvas = new GameObject("RobozinhoPrompt_Canvas");
        DontDestroyOnLoad(s_promptCanvas);

        Canvas canvas = s_promptCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = s_promptCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        s_promptCanvas.AddComponent<GraphicRaycaster>();

        GameObject painelGO = new GameObject("PromptPanel");
        painelGO.transform.SetParent(s_promptCanvas.transform, false);
        Image painelImg = painelGO.AddComponent<Image>();
        painelImg.color = new Color(0.10f, 0.08f, 0.04f, 0.85f);
        RectTransform painelRect = painelGO.GetComponent<RectTransform>();
        painelRect.anchorMin = new Vector2(0.5f, 0f);
        painelRect.anchorMax = new Vector2(0.5f, 0f);
        painelRect.pivot     = new Vector2(0.5f, 0f);
        painelRect.sizeDelta = new Vector2(340f, 50f);
        painelRect.anchoredPosition = new Vector2(0f, 60f);

        GameObject textoGO = new GameObject("PromptText");
        textoGO.transform.SetParent(painelGO.transform, false);
        s_promptText = textoGO.AddComponent<TextMeshProUGUI>();
        s_promptText.text = "<color=#FFD1C0>[ F ]</color>  Falar com Robozinho";
        s_promptText.fontSize = 20;
        s_promptText.alignment = TextAlignmentOptions.Center;
        s_promptText.color = new Color(1f, 0.95f, 0.9f, 1f);
        RectTransform txtRect = textoGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(10f, 5f);
        txtRect.offsetMax = new Vector2(-10f, -5f);

        s_promptCanvas.SetActive(false);
    }

    private static void AtualizarPrompt()
    {
        if (s_promptCanvas == null) return;
        s_promptCanvas.SetActive(s_activeCount > 0);
    }
}
