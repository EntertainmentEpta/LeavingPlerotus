using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla apenas o efeito de barra amarela de dano atrasado (estilo LoL) no Slider já configurado no Prefab.
/// Não altera tamanho, moldura, sprites nem cores configuradas no Slider.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthBarVisual : MonoBehaviour
{
    [Header("Dano Atrasado (Estilo LoL)")]
    [Tooltip("Cor da barra de dano atrasado (usada apenas caso o Slider tenha sido criado via código sem o filho DelayedDamageFill).")]
    public Color fallbackDelayedColor = new Color(128f / 255f, 128f / 255f, 128f / 255f, 1f);
    [Tooltip("Tempo que a barra amarela segura parada após um hit para mostrar o dano causado.")]
    public float damageHoldDuration = 0.36f;
    [Tooltip("Velocidade mínima de recuo da barra amarela (fração da barra por segundo).")]
    public float minDrainSpeed = 0.55f;
    [Tooltip("Suavização exponencial do recuo da barra amarela.")]
    public float drainLerpSpeed = 5.5f;
    [Tooltip("Breve tempo visível após golpe letal para mostrar o último bloco amarelo drenar.")]
    public float deathDrainGrace = 0.25f;

    private Slider slider;
    private RectTransform delayedFillRect;
    private Image healthFillImage;
    private CanvasGroup canvasGroup;

    private bool initialized;
    private float currentPercent = 1f;
    private float delayedPercent = 1f;
    private float holdTimer;
    private float deathTimer = -1f;

    public static EnemyHealthBarVisual AttachOrGet(Slider targetSlider, int maxHealth, Color initialFillColor)
    {
        if (targetSlider == null) return null;

        EnemyHealthBarVisual visual = targetSlider.GetComponent<EnemyHealthBarVisual>();
        if (visual == null)
            visual = targetSlider.gameObject.AddComponent<EnemyHealthBarVisual>();

        visual.Initialize(targetSlider, initialFillColor);
        return visual;
    }

    public void Initialize(Slider targetSlider, Color initialFillColor)
    {
        if (targetSlider == null) return;
        slider = targetSlider;

        if (initialized) return;
        initialized = true;

        if (slider.fillRect != null)
            healthFillImage = slider.fillRect.GetComponent<Image>();
        if (healthFillImage != null)
            healthFillImage.color = initialFillColor;

        canvasGroup = slider.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = slider.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Procura o objeto DelayedDamageFill configurado diretamente no Slider do prefab
        Transform fillParent = slider.fillRect != null ? slider.fillRect.parent : slider.transform;
        Transform existingDelayed = fillParent != null ? fillParent.Find("DelayedDamageFill") : null;
        if (existingDelayed != null)
        {
            delayedFillRect = existingDelayed as RectTransform;
        }
        else if (fillParent != null)
        {
            // Fallback apenas para sliders criados 100% em runtime (ex.: Bismutado/Geobionte)
            GameObject go = new GameObject("DelayedDamageFill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(fillParent, false);
            delayedFillRect = go.GetComponent<RectTransform>();
            delayedFillRect.anchorMin = Vector2.zero;
            delayedFillRect.anchorMax = Vector2.one;
            delayedFillRect.offsetMin = Vector2.zero;
            delayedFillRect.offsetMax = Vector2.zero;

            Image img = go.GetComponent<Image>();
            img.color = fallbackDelayedColor;
            img.raycastTarget = false;

            if (slider.fillRect != null)
                delayedFillRect.SetSiblingIndex(slider.fillRect.GetSiblingIndex());
        }

        currentPercent = Mathf.Clamp01(slider.value);
        delayedPercent = currentPercent;
        ApplyDelayedRect();
    }

    public void SetHealthColor(Color color)
    {
        if (healthFillImage != null)
            healthFillImage.color = color;
    }

    public void SetHealthInstant(float current, float max)
    {
        float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        currentPercent = pct;
        delayedPercent = pct;
        holdTimer = 0f;
        deathTimer = -1f;

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (slider != null) slider.value = pct;
        ApplyDelayedRect();
    }

    public void SetHealth(float current, float max)
    {
        float newPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (newPct < currentPercent - 0.0005f)
        {
            if (delayedPercent < currentPercent)
                delayedPercent = currentPercent;

            currentPercent = newPct;
            holdTimer = damageHoldDuration;

            if (newPct <= 0f)
            {
                deathTimer = deathDrainGrace;
                holdTimer = Mathf.Min(holdTimer, 0.08f);
            }
            else
            {
                deathTimer = -1f;
                if (canvasGroup != null) canvasGroup.alpha = 1f;
            }
        }
        else if (newPct > currentPercent + 0.0005f)
        {
            currentPercent = newPct;
            if (delayedPercent < newPct)
                delayedPercent = newPct;
            holdTimer = 0f;
            deathTimer = -1f;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }
        else
        {
            currentPercent = newPct;
        }

        if (slider != null)
            slider.value = currentPercent;

        ApplyDelayedRect();
    }

    private void Update()
    {
        if (!initialized) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (delayedPercent > currentPercent)
        {
            if (holdTimer > 0f)
            {
                holdTimer -= dt;
            }
            else
            {
                float diff = delayedPercent - currentPercent;
                float speed = Mathf.Max(minDrainSpeed, diff * drainLerpSpeed);
                delayedPercent = Mathf.MoveTowards(delayedPercent, currentPercent, speed * dt);
                ApplyDelayedRect();
            }
        }
        else if (delayedPercent < currentPercent)
        {
            delayedPercent = currentPercent;
            ApplyDelayedRect();
        }

        if (deathTimer >= 0f)
        {
            deathTimer -= dt;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(deathTimer / Mathf.Max(0.05f, deathDrainGrace));
            if (deathTimer <= 0f)
            {
                deathTimer = -1f;
                gameObject.SetActive(false);
            }
        }
    }

    private void ApplyDelayedRect()
    {
        if (delayedFillRect == null) return;
        delayedFillRect.anchorMin = Vector2.zero;
        delayedFillRect.anchorMax = new Vector2(Mathf.Clamp01(delayedPercent), 1f);
        delayedFillRect.offsetMin = Vector2.zero;
        delayedFillRect.offsetMax = Vector2.zero;
    }
}
