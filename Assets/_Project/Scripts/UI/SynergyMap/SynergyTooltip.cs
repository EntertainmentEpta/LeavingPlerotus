using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton simples que gerencia o painel de Tooltip do Mapa de Sinergias.
/// Coloque este script no GameObject do painel Tooltip (que começa desativado).
/// Ele segue a posição do mouse a cada frame enquanto estiver visível.
/// </summary>
public class SynergyTooltip : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────

    public static SynergyTooltip Instance { get; private set; }

    // ── Referências ───────────────────────────────────────────

    [Header("Referências UI")]
    [Tooltip("TextMeshPro do título do tooltip.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("TextMeshPro da descrição do tooltip.")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Configuração")]
    [Tooltip("Offset em pixels entre o mouse e o tooltip.")]
    [SerializeField] private Vector2 offset = new Vector2(20f, -20f);

    // ── Cache ──────────────────────────────────────────────────

    private RectTransform _rect;
    private Canvas _parentCanvas;
    private bool _isShowing;

    // ── Lifecycle ──────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _rect = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();

        // Começa escondido
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // Sempre começa escondido quando reativado pelo toggle
        _isShowing = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isShowing) return;
        FollowMouse();
    }

    // ── API Pública ────────────────────────────────────────────

    /// <summary>
    /// Exibe o tooltip com o título e descrição fornecidos.
    /// </summary>
    public void Show(string title, string description)
    {
        titleText.text = title;
        descriptionText.text = description;

        gameObject.SetActive(true);
        _isShowing = true;

        // Posiciona imediatamente para não "piscar" no canto
        FollowMouse();
    }

    /// <summary>
    /// Esconde o tooltip.
    /// </summary>
    public void Hide()
    {
        _isShowing = false;
        gameObject.SetActive(false);
    }

    // ── Helpers ────────────────────────────────────────────────

    private void FollowMouse()
    {
        Vector2 mousePos = Input.mousePosition;

        // Converte a posição do mouse para o espaço do Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentCanvas.transform as RectTransform,
            mousePos,
            _parentCanvas.worldCamera,
            out Vector2 localPoint
        );

        _rect.localPosition = localPoint + offset;

        // Impede que o tooltip saia da tela
        ClampToScreen();
    }

    private void ClampToScreen()
    {
        Vector3[] corners = new Vector3[4];
        _rect.GetWorldCorners(corners);

        // corners: 0=bottomLeft, 1=topLeft, 2=topRight, 3=bottomRight
        float minX = corners[0].x;
        float maxX = corners[2].x;
        float minY = corners[0].y;
        float maxY = corners[2].y;

        Vector3 pos = _rect.localPosition;

        if (minX < 0) pos.x -= minX;
        if (maxX > Screen.width) pos.x -= (maxX - Screen.width);
        if (minY < 0) pos.y -= minY;
        if (maxY > Screen.height) pos.y -= (maxY - Screen.height);

        _rect.localPosition = pos;
    }
}
