using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicador de Objetivo Universal (Objective Marker).
/// Suporta tanto Objetos 3D no Mundo (Adaga no chão, Robozinho, Portas) com Billboard e flutuação senoidal em coordenadas de mundo,
/// quanto Elementos 2D de UI/Canvas (Botões, Abas) com pulso dinâmico.
/// </summary>
public class ObjectiveMarker : MonoBehaviour
{
    public enum MarkerMode { AutoDetect, World3D, CanvasUI }
    public enum TriggerCondition { ByTutorialStep, ByQuestId, AlwaysActive, Custom }

    [Header("Gatilho de Ativação")]
    public TriggerCondition condition = TriggerCondition.ByTutorialStep;
    [Tooltip("Passo do Tutorial em que este marcador deve aparecer.")]
    public TutorialStep activeOnStep = TutorialStep.PegarAdaga;
    [Tooltip("ID da Quest necessária para exibir (se condition == ByQuestId).")]
    public string activeOnQuestId = "";

    [Header("Comportamento Visual")]
    public MarkerMode mode = MarkerMode.AutoDetect;
    [Tooltip("Altura em metros acima do objeto no mundo (ex: 0.85 para adaga no chão, 1.8 para robozinho).")]
    public float heightOffset = 0.85f;
    [Tooltip("Tamanho do ícone no mundo ou na UI.")]
    public Vector2 markerSize = new Vector2(48f, 48f);
    public Sprite markerSprite;

    [Header("Animação")]
    public float floatAmplitude = 0.08f;
    public float floatFrequency = 3.0f;
    public float pulseAmplitude = 0.08f;
    public float pulseFrequency = 3.5f;

    [Header("Visual Customizado (Opcional)")]
    [Tooltip("Arraste um objeto visual manual se não quiser usar o indicador automático gerado.")]
    public GameObject indicatorVisual;

    /// <summary>
    /// Condição customizada em código (ex: usada pelo Robozinho para checar conserto de porta + tutorial).
    /// </summary>
    public System.Func<bool> customVisibilityCondition;

    private TutorialManager boundTutorial;
    private bool isWorldMode = true;
    private Button boundButton;

    private void Awake()
    {
        EnsureMarkerSprite();
    }

    private void Start()
    {
        EnsureMarkerSprite();
        DetermineMode();
        BuildVisualIfNeeded();
        TryBind();

        if (!isWorldMode)
        {
            boundButton = GetComponent<Button>();
            if (boundButton != null)
            {
                boundButton.onClick.AddListener(OnButtonClicked);
            }
        }
    }

    private void OnEnable()
    {
        CheckVisibility();
    }

    private void OnDestroy()
    {
        if (boundTutorial != null)
        {
            boundTutorial.stepChangedEvent -= OnStepChanged;
            boundTutorial = null;
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StateChanged -= CheckVisibility;
        }

        if (boundButton != null)
        {
            boundButton.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void EnsureMarkerSprite()
    {
        if (markerSprite != null) return;

        // Prioridade máxima para a nova imagem 'exclamação'
        string[] candidateNames = new string[]
        {
            "exclamação",
            "exclamacao",
            "exclamação_0",
            "exclamação_1",
            "exclamacao_0",
            "exclamacao_1",
            "Exclamação",
            "Exclamacao",
            "Exclamation",
            "exclamation-128",
            "exclamation-256"
        };

        foreach (string cName in candidateNames)
        {
            // Tenta como Sprite direto
            markerSprite = Resources.Load<Sprite>(cName);
            if (markerSprite != null) return;

            // Se for Sprite Sheet / Multiple, pega a primeira fatia
            Sprite[] sprites = Resources.LoadAll<Sprite>(cName);
            if (sprites != null && sprites.Length > 0)
            {
                markerSprite = sprites[0];
                return;
            }

            // Tenta como Texture2D
            Texture2D tex = Resources.Load<Texture2D>(cName);
            if (tex != null)
            {
                markerSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return;
            }
        }

        if (markerSprite == null)
        {
            markerSprite = GenerateProceduralExclamation();
        }
    }

    private static Sprite GenerateProceduralExclamation()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.clear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.46f;
        Color bgCol = new Color(1.0f, 0.82f, 0.15f, 1f); // Amarelo Dourado
        Color fgCol = new Color(0.12f, 0.12f, 0.12f, 1f); // Grafite Escuro

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    cols[y * size + x] = bgCol;

                    // Barra vertical da exclamação
                    if (x >= 29 && x <= 34 && y >= 25 && y <= 47)
                    {
                        cols[y * size + x] = fgCol;
                    }
                    // Ponto inferior da exclamação
                    if (x >= 29 && x <= 34 && y >= 15 && y <= 20)
                    {
                        cols[y * size + x] = fgCol;
                    }
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void DetermineMode()
    {
        if (mode == MarkerMode.World3D)
        {
            isWorldMode = true;
        }
        else if (mode == MarkerMode.CanvasUI)
        {
            isWorldMode = false;
        }
        else
        {
            // AutoDetect: se tem RectTransform e está dentro de um Canvas, é CanvasUI
            isWorldMode = (GetComponent<RectTransform>() == null || GetComponentInParent<Canvas>() == null);
        }
    }

    private void BuildVisualIfNeeded()
    {
        if (indicatorVisual != null) return;

        EnsureMarkerSprite();

        if (isWorldMode)
        {
            // Constrói Canvas WorldSpace com Billboard e flutuação em coordenadas globais
            GameObject markerRoot = new GameObject("ObjectiveMarker_World");
            markerRoot.transform.SetParent(transform, false);

            Canvas canvas = markerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3000; // Sempre renderiza à frente

            RectTransform rect = markerRoot.GetComponent<RectTransform>();
            rect.sizeDelta = markerSize;
            rect.localScale = Vector3.one * 0.02f;

            GameObject imgObj = new GameObject("Icon");
            imgObj.transform.SetParent(markerRoot.transform, false);
            RectTransform imgRect = imgObj.AddComponent<RectTransform>();
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.sizeDelta = Vector2.zero;

            Image img = imgObj.AddComponent<Image>();
            img.sprite = markerSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            indicatorVisual = markerRoot;
        }
        else
        {
            // Constrói ícone 2D ancorado no canto superior do elemento da UI
            GameObject uiMarker = new GameObject("ObjectiveMarker_UI");
            uiMarker.transform.SetParent(transform, false);

            RectTransform rect = uiMarker.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = markerSize;

            Image img = uiMarker.AddComponent<Image>();
            img.sprite = markerSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            indicatorVisual = uiMarker;
        }

        indicatorVisual.SetActive(false);
    }

    private void Update()
    {
        if (indicatorVisual == null || !indicatorVisual.activeSelf) return;

        if (isWorldMode)
        {
            // 1. Posição no MUNDO: sempre diretamente acima da posição mundial do objeto (imune à rotação do objeto caído no chão!)
            float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            Vector3 baseWorldPos = transform.position + Vector3.up * (heightOffset + yOffset);

            // 2. Billboard e leve aproximação para a câmera para garantir renderização frontal
            if (Camera.main != null)
            {
                Vector3 toCam = (Camera.main.transform.position - baseWorldPos).normalized;
                indicatorVisual.transform.position = baseWorldPos + toCam * 0.25f;
                indicatorVisual.transform.forward = Camera.main.transform.forward;
            }
            else
            {
                indicatorVisual.transform.position = baseWorldPos;
            }

            // 3. Escala no MUNDO: compensa a escala local do pai para que a exclamação mantenha tamanho uniforme
            float scaleMul = 1f + Mathf.Sin(Time.time * pulseFrequency) * pulseAmplitude;
            float desiredWorldScale = 0.022f * scaleMul;
            Vector3 lossy = transform.lossyScale;
            indicatorVisual.transform.localScale = new Vector3(
                Mathf.Abs(lossy.x) > 0.0001f ? desiredWorldScale / Mathf.Abs(lossy.x) : desiredWorldScale,
                Mathf.Abs(lossy.y) > 0.0001f ? desiredWorldScale / Mathf.Abs(lossy.y) : desiredWorldScale,
                Mathf.Abs(lossy.z) > 0.0001f ? desiredWorldScale / Mathf.Abs(lossy.z) : desiredWorldScale
            );
        }
        else
        {
            // Animação de pulso na UI
            float scaleMul = 1f + Mathf.Sin(Time.unscaledTime * pulseFrequency) * pulseAmplitude;
            indicatorVisual.transform.localScale = Vector3.one * scaleMul;
        }
    }

    private void TryBind()
    {
        boundTutorial = TutorialManager.Instance;
        if (boundTutorial != null)
        {
            boundTutorial.stepChangedEvent += OnStepChanged;
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StateChanged += CheckVisibility;
        }

        CheckVisibility();
    }

    private void OnStepChanged(TutorialStep newStep) => CheckVisibility();

    public void CheckVisibility()
    {
        if (this == null) return;

        bool shouldShow = false;

        if (customVisibilityCondition != null)
        {
            shouldShow = customVisibilityCondition();
        }
        else
        {
            switch (condition)
            {
                case TriggerCondition.AlwaysActive:
                    shouldShow = true;
                    break;

                case TriggerCondition.ByTutorialStep:
                    if (TutorialManager.Instance != null)
                    {
                        shouldShow = (TutorialManager.Instance.currentStep == activeOnStep);
                    }
                    break;

                case TriggerCondition.ByQuestId:
                    if (!string.IsNullOrEmpty(activeOnQuestId))
                    {
                        bool isCurrent = QuestManager.Instance != null && QuestManager.Instance.currentQuest != null && QuestManager.Instance.currentQuest.questId == activeOnQuestId;
                        bool isCompleted = SaveManager.instance != null && SaveManager.instance.CachedData != null && SaveManager.instance.CachedData.completedQuestIds.Contains(activeOnQuestId);
                        shouldShow = isCurrent && !isCompleted;
                    }
                    break;
            }
        }

        SetVisual(shouldShow && isActiveAndEnabled);
    }

    private void SetVisual(bool on)
    {
        if (indicatorVisual == null && on)
        {
            BuildVisualIfNeeded();
        }

        if (indicatorVisual != null && indicatorVisual.activeSelf != on)
        {
            indicatorVisual.SetActive(on);
        }
    }

    private void OnButtonClicked()
    {
        if (condition == TriggerCondition.ByTutorialStep && TutorialManager.Instance != null)
        {
            if (TutorialManager.Instance.currentStep == activeOnStep)
            {
                TutorialManager.Instance.CompleteStep(activeOnStep);
            }
        }
    }

    /// <summary>
    /// Conclui manualmente este marcador (ex: chamado pelo Dagger_Pickup ao pegar a arma).
    /// </summary>
    public void CompleteMarker()
    {
        if (condition == TriggerCondition.ByTutorialStep && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.CompleteStep(activeOnStep);
        }
        SetVisual(false);
    }
}
