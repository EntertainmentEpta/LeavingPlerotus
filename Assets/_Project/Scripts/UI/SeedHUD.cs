using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Mostra a Seed ativa da run no canto inferior esquerdo (aparece no HUD, Pause e Fim de Run).
/// Cria a si mesmo por código, igual ao FpsCounterHUD: não precisa estar em nenhuma cena.
/// </summary>
public class SeedHUD : MonoBehaviour
{
    private TextMeshProUGUI seedText;

    // A Unity chama isso sozinha quando o jogo abre: cria o SeedHUD uma única vez.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        GameObject go = new GameObject("SeedHUD_AutoInit");
        DontDestroyOnLoad(go); // não é apagado ao trocar de cena
        go.AddComponent<SeedHUD>();
    }

    private void Start()
    {
        // Canvas próprio, desenhado por cima das outras telas
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        // Mantém o tamanho proporcional em qualquer resolução
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // O texto
        GameObject textGo = new GameObject("txtSeed");
        textGo.transform.SetParent(transform, false);

        RectTransform rt = textGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); // preso no canto inferior esquerdo
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(55f, 295f); // logo acima do escudo do HUD
        rt.sizeDelta = new Vector2(500f, 30f);

        seedText = textGo.AddComponent<TextMeshProUGUI>();
        seedText.fontSize = 18f;
        seedText.color = new Color(1f, 1f, 1f, 0.6f); // branco, 60% visível
        seedText.alignment = TextAlignmentOptions.BottomLeft;
        seedText.raycastTarget = false; // não bloqueia cliques
        seedText.text = "";
    }

    private void Update()
    {
        if (seedText == null) return;

        string scene = SceneManager.GetActiveScene().name;
        bool inRun = scene != "Base" && scene != "MainMenu" && scene != "Menu" && !scene.Contains("Loading");

        string seed = RunManager.instance != null ? RunManager.instance.ActiveSeedText : "";
        string newText = (inRun && !string.IsNullOrEmpty(seed)) ? $"SEED: {seed}" : "";

        // Só troca o texto quando ele muda (evita trabalho à toa a cada quadro)
        if (seedText.text != newText) seedText.text = newText;
    }
}