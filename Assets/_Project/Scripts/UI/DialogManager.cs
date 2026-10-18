using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogManager : MonoBehaviour
{
    private static DialogManager _instance;
    public static DialogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DialogManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("DialogManager_Auto");
                    _instance = go.AddComponent<DialogManager>();
                    _instance.EnsureUI();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    /// <summary>Verdadeiro enquanto um diálogo estiver ativo na tela.</summary>
    public static bool IsOpen => Instance != null && Instance.isOpen;

    /// <summary>
    /// Verdadeiro enquanto aberto E no exato frame em que fechou.
    /// A jogabilidade (ataque, esquiva, interação) deve checar isso para não consumir
    /// o clique/espaço/E usado para fechar o diálogo.
    /// </summary>
    public static bool BlocksGameplayInput =>
        Instance != null && (Instance.isOpen || Instance.lastCloseFrame == Time.frameCount);

    [Header("UI Elements")]
    public GameObject dialogPanel;       // O painel escuro no fundo
    public Image portraitImage;          // A imagem do Eptinho
    public TextMeshProUGUI dialogText;   // O texto do diálogo

    [Header("Configurações")]
    public float typeSpeed = 0.02f;      // Velocidade da máquina de escrever (segundos por caractere, tempo real)

    private struct DialogRequest { public string[] lines; public Action onComplete; }
    private readonly Queue<DialogRequest> pending = new Queue<DialogRequest>();
    private readonly List<string> sentences = new List<string>();

    private int currentIndex;
    private bool isTyping;
    private bool isOpen;
    private bool isWaitingForWakeUp;
    private int openedFrame = -1;
    private int lastCloseFrame = -1;
    private Action onCompleteCallback;
    private Coroutine typingRoutine;
    private Coroutine wakeUpWaitRoutine;
    private CanvasGroup panelCanvasGroup;

    private const int RevealAll = 99999;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsureCanvasGroup();
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        }
    }

    public void EnsureUI()
    {
        if (dialogPanel != null && dialogText != null) return;

        DialogManager existing = FindFirstObjectByType<DialogManager>();
        if (existing != null && existing != this && existing.dialogPanel != null)
        {
            dialogPanel = existing.dialogPanel;
            portraitImage = existing.portraitImage;
            dialogText = existing.dialogText;
            EnsureCanvasGroup();
            return;
        }

        // Constrói UI fallback em runtime
        GameObject canvasGo = new GameObject("DialogManager_FallbackCanvas");
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Painel Principal
        dialogPanel = new GameObject("DialogPanel");
        dialogPanel.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRect = dialogPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 40f);
        panelRect.sizeDelta = new Vector2(1100f, 210f);

        Image panelBg = dialogPanel.AddComponent<Image>();
        panelBg.color = new Color(0.04f, 0.045f, 0.075f, 0.94f);

        Outline outline = dialogPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.55f, 0.80f, 0.50f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Retrato
        GameObject portraitGo = new GameObject("PortraitImage");
        portraitGo.transform.SetParent(dialogPanel.transform, false);
        RectTransform portraitRect = portraitGo.AddComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(30f, 0f);
        portraitRect.sizeDelta = new Vector2(140f, 140f);

        portraitImage = portraitGo.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        Sprite eptSprite = Resources.Load<Sprite>("DialogueImage") ?? Resources.Load<Sprite>("EPTONHO");
        if (eptSprite != null) portraitImage.sprite = eptSprite;

        // Texto
        GameObject textGo = new GameObject("DialogText");
        textGo.transform.SetParent(dialogPanel.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(200f, 35f);
        textRect.offsetMax = new Vector2(-40f, -20f);

        dialogText = textGo.AddComponent<TextMeshProUGUI>();
        dialogText.fontSize = 26f;
        dialogText.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (font != null) dialogText.font = font;

        // Dica
        GameObject hintGo = new GameObject("HintText");
        hintGo.transform.SetParent(dialogPanel.transform, false);
        RectTransform hintRect = hintGo.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(1f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-25f, 10f);
        hintRect.sizeDelta = new Vector2(400f, 25f);

        TextMeshProUGUI hintText = hintGo.AddComponent<TextMeshProUGUI>();
        hintText.text = "[ Espaço / Clique / E para avançar ]";
        hintText.fontSize = 16f;
        hintText.alignment = TextAlignmentOptions.Right;
        hintText.color = new Color(0.5f, 0.6f, 0.7f, 0.8f);
        if (font != null) hintText.font = font;

        EnsureCanvasGroup();
        dialogPanel.SetActive(false);
    }

    private void EnsureCanvasGroup()
    {
        if (dialogPanel != null && panelCanvasGroup == null)
        {
            panelCanvasGroup = dialogPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = dialogPanel.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnDestroy()
    {
        StopWakeUpWaiting();
        if (isOpen) GamePause.Release(this);
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!isOpen) return;
        if (Time.frameCount == openedFrame) return;
        if (GamePause.TopOwner != this) return;

        // Se o jogador estiver na animação de acordar, bloqueia qualquer avanço/interação de diálogo
        if (isWaitingForWakeUp || PlayerWakeUpEffect.IsWakingUp) return;

        // VOLTAR — Seta Esquerda ou Backspace (tecla A removida para não conflitar com movimento)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace))
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                DisplaySentence();
            }
            return;
        }

        // AVANÇAR — Mouse, Espaço, Enter, Seta Direita ou E (tecla D removida para não conflitar com movimento)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
            {
                RevealInstantly();
            }
            else if (++currentIndex < sentences.Count)
            {
                DisplaySentence();
            }
            else
            {
                EndDialog();
            }
        }
    }

    public void StartDialog(string[] lines, Action onComplete = null)
    {
        EnsureUI();
        if (dialogPanel != null)
        {
            Canvas c = dialogPanel.GetComponentInParent<Canvas>();
            if (c != null && c.sortingOrder < 1500) c.sortingOrder = 1500;
        }

        if (lines == null || lines.Length == 0 || dialogPanel == null || dialogText == null)
        {
            Debug.LogWarning("[DialogManager] Diálogo vazio ou UI não configurada — executando callback direto.", this);
            SafeInvoke(onComplete);
            return;
        }

        if (isOpen)
        {
            // Proteção contra duplicação: ignora se o mesmo diálogo já estiver rodando ou na fila
            if (sentences.Count > 0 && sentences[0] == lines[0])
            {
                return;
            }
            foreach (var req in pending)
            {
                if (req.lines != null && req.lines.Length > 0 && req.lines[0] == lines[0])
                {
                    return;
                }
            }

            pending.Enqueue(new DialogRequest { lines = lines, onComplete = onComplete });
            return;
        }

        Open(lines, onComplete);
    }

    public void ForceClose(bool invokeCallbacks = false)
    {
        StopWakeUpWaiting();
        StopTyping();
        if (!isOpen) return;
        Action cb = onCompleteCallback;
        onCompleteCallback = null;
        DialogRequest[] queued = pending.ToArray();
        pending.Clear();
        Close();
        if (!invokeCallbacks) return;
        SafeInvoke(cb);
        foreach (DialogRequest r in queued) SafeInvoke(r.onComplete);
    }

    private void Open(string[] lines, Action onComplete)
    {
        onCompleteCallback = onComplete;
        sentences.Clear();
        sentences.AddRange(lines);
        currentIndex = 0;
        isOpen = true;
        openedFrame = Time.frameCount;

        GamePause.Acquire(this);
        EnsureCanvasGroup();

        if (PlayerWakeUpEffect.IsWakingUp)
        {
            if (dialogPanel != null) dialogPanel.SetActive(false);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
            if (dialogText != null) dialogText.text = "";
            StopWakeUpWaiting();
            wakeUpWaitRoutine = StartCoroutine(WaitForWakeUpAndDisplay());
        }
        else
        {
            if (dialogPanel != null) dialogPanel.SetActive(true);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
            DisplaySentence();
        }
    }

    private void Close()
    {
        StopTyping();
        StopWakeUpWaiting();
        isTyping = false;
        isOpen = false;
        lastCloseFrame = Time.frameCount;
        if (dialogPanel != null) dialogPanel.SetActive(false);
        GamePause.Release(this);
    }

    private IEnumerator WaitForWakeUpAndDisplay()
    {
        isWaitingForWakeUp = true;
        StopTyping();
        if (dialogPanel != null) dialogPanel.SetActive(false);
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        if (dialogText != null) dialogText.text = "";

        // Enquanto o efeito de abrir os olhos estiver rodando, o painel fica 100% oculto
        float safetyTimer = 0f;
        while (PlayerWakeUpEffect.IsWakingUp && safetyTimer < 10f)
        {
            safetyTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        isWaitingForWakeUp = false;
        wakeUpWaitRoutine = null;

        // Pausa dramática com a visão 100% nítida e os olhos abertos antes de surgir a caixa e começar a fala
        yield return new WaitForSecondsRealtime(0.4f);

        if (!isOpen) yield break;

        if (dialogPanel != null) dialogPanel.SetActive(true);
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;

        DisplaySentence();
    }

    private void StopWakeUpWaiting()
    {
        if (wakeUpWaitRoutine != null)
        {
            StopCoroutine(wakeUpWaitRoutine);
            wakeUpWaitRoutine = null;
        }
        isWaitingForWakeUp = false;
    }

    private void EndDialog()
    {
        Action cb = onCompleteCallback;
        onCompleteCallback = null;

        Close();
        SafeInvoke(cb);

        if (!isOpen && pending.Count > 0)
        {
            DialogRequest next = pending.Dequeue();
            Open(next.lines, next.onComplete);
        }
    }

    private void DisplaySentence()
    {
        StopTyping();
        typingRoutine = StartCoroutine(TypeSentence(sentences[currentIndex]));
    }

    private IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        dialogText.text = sentence;
        dialogText.maxVisibleCharacters = 0;
        dialogText.ForceMeshUpdate();
        int total = dialogText.textInfo.characterCount;

        float effectiveSpeed = (typeSpeed > 0.005f) ? typeSpeed : 0.02f;
        float elapsed = 0f;
        int visible = 0;
        while (visible < total && effectiveSpeed > 0f)
        {
            elapsed += Time.unscaledDeltaTime;
            visible = Mathf.Min(total, Mathf.FloorToInt(elapsed / effectiveSpeed));
            dialogText.maxVisibleCharacters = visible;
            yield return null;
        }

        typingRoutine = null;
        RevealInstantly();
    }

    private void RevealInstantly()
    {
        StopTyping();
        dialogText.maxVisibleCharacters = RevealAll;
        isTyping = false;
    }

    private void StopTyping()
    {
        if (typingRoutine == null) return;
        StopCoroutine(typingRoutine);
        typingRoutine = null;
    }

    private static void SafeInvoke(Action cb)
    {
        if (cb == null) return;
        try { cb(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
