using UnityEngine;
using TMPro;
using System.Collections;
using CartoonFX;

public class FloatingDamageText : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Arraste o objeto de texto (filho) que tem o TextMeshProUGUI aqui (para versão Canvas).")]
    public TextMeshProUGUI textMesh;

    [Tooltip("Arraste o componente CFXR_ParticleText aqui (para versão VFX de partículas).")]
    public CFXR_ParticleText particleText;

    [Header("Animação")]
    [Tooltip("Tempo de vida total do objeto na cena")]
    public float lifetime = 1.35f;

    [Tooltip("Velocidade com que o número flutua para cima")]
    public float floatSpeed = 1.1f;
    
    [Header("Critical Hit Settings")]
    [Tooltip("Cor do texto para dano crítico (modo Canvas TMP)")]
    public Color criticalColor = new Color(0.92f, 0.12f, 0.15f, 1f);
    
    [Tooltip("Multiplicador de tamanho para críticos")]
    [Range(1f, 3f)]
    public float criticalSizeMultiplier = 1.4f;
    
    [Tooltip("Multiplicador de duração para críticos")]
    [Range(1f, 3f)]
    public float criticalLifetimeMultiplier = 1.25f;

    [Header("CFXR Particle Settings")]
    [Tooltip("Tamanho base da fonte nas partículas")]
    public float particleBaseSize = 0.32f;

    [Tooltip("Multiplicador de duração das partículas")]
    public float particleLifetimeMultiplier = 1.0f;

    [Tooltip("Cor 1 (superior) para dano normal - Branco")]
    public Color particleColorNormal1 = Color.white;

    [Tooltip("Cor 2 (inferior) para dano normal - Leve gradiente cinza")]
    public Color particleColorNormal2 = new Color(0.74f, 0.74f, 0.78f, 1f);

    [Tooltip("Cor 1 (superior) para dano crítico - Vermelho")]
    public Color particleColorCrit1 = new Color(0.92f, 0.12f, 0.15f, 1f);

    [Tooltip("Cor 2 (inferior) para dano crítico - Vinho profundo")]
    public Color particleColorCrit2 = new Color(0.42f, 0.02f, 0.08f, 1f);

    [Header("Endless & Strength Scaling")]
    [Tooltip("Permite que o tamanho da fonte e do número de dano escale com a força base do jogador e com o dano causado.")]
    public bool enableInfiniteStrengthScaling = true;

    [Tooltip("Sensibilidade do crescimento da fonte com o atributo de força base (1.0 = base). Ex: 0.5 significa que a cada +1x de força, o texto ganha +50% de tamanho.")]
    public float strengthScaleSensitivity = 0.5f;

    [Tooltip("Sensibilidade adicional de crescimento com base no valor numérico de dano.")]
    public float damageScaleSensitivity = 0.01f;

    [Tooltip("Dano base de referência para início do crescimento por dano numérico.")]
    public float baseDamageReference = 35f;

    [Tooltip("Escala mínima para evitar números invisíveis ou zerados.")]
    public float minScaleMultiplier = 0.6f;

    private float timer;
    private Color startColor;
    private float baseFontSize;
    private bool isCriticalHit = false;
    private string pendingText = null;

    private Vector3 initialLocalScale = Vector3.one;
    private float baseFloatSpeed = 1.1f;
    private float baseLifetime = 1.35f;
    private int currentDamageValue = -1;
    private float customStrengthMultiplier = -1f;
    private bool hasAppliedHeightLift = false;
    private Vector3 floatDirection = Vector3.up;

    void Awake()
    {
        initialLocalScale = transform.localScale;
        baseFloatSpeed = floatSpeed;
        baseLifetime = lifetime;

        // Leve dispersão horizontal para evitar que números consecutivos fiquem sobrepostos
        float randomX = Random.Range(-0.15f, 0.15f);
        float randomZ = Random.Range(-0.15f, 0.15f);
        floatDirection = (Vector3.up + new Vector3(randomX, 0f, randomZ)).normalized;

        // Auto-detecta os componentes se não foram atribuídos no Inspector
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (particleText == null)
        {
            particleText = GetComponent<CFXR_ParticleText>();
            if (particleText == null)
            {
                particleText = GetComponentInChildren<CFXR_ParticleText>();
            }
        }

        if (textMesh == null && particleText == null)
        {
            Debug.LogError("FloatingDamageText: Nenhuma referência de 'textMesh' ou 'particleText' encontrada!", this.gameObject);
            return;
        }

        if (textMesh != null)
        {
            startColor = textMesh.color;
            baseFontSize = textMesh.fontSize;
        }

        timer = lifetime;
    }

    void Update()
    {
        transform.position += floatDirection * floatSpeed * Time.deltaTime;

        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.forward);
        }

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            if (textMesh != null)
            {
                textMesh.color = new Color(startColor.r, startColor.g, startColor.b, timer / lifetime);
            }
        }
    }

    /// <summary>
    /// Define o dano numérico e calcula o escalonamento infinito da fonte/escala com base no dano e na força do jogador.
    /// </summary>
    public void SetDamage(int damage, bool isCritical = false, float strengthMult = -1f)
    {
        currentDamageValue = damage;
        isCriticalHit = isCritical;
        if (strengthMult > 0f)
        {
            customStrengthMultiplier = strengthMult;
        }

        pendingText = damage.ToString();

        if (textMesh != null)
        {
            textMesh.text = pendingText;
            if (isCritical)
            {
                textMesh.color = criticalColor;
                startColor = criticalColor;
            }
        }

        UpdateScaleAndVisuals();

        if (particleText != null)
        {
            ApplyParticleText();
        }
    }

    public void SetText(string text)
    {
        pendingText = text;

        if (textMesh != null)
        {
            textMesh.text = text;
        }

        if (int.TryParse(text, out int parsedDamage))
        {
            currentDamageValue = parsedDamage;
        }

        UpdateScaleAndVisuals();

        if (particleText != null)
        {
            ApplyParticleText();
        }
    }
    
    /// <summary>
    /// Configura o texto como dano crítico com visual aprimorado.
    /// </summary>
    public void SetCritical(bool isCritical)
    {
        isCriticalHit = isCritical;

        if (isCritical && textMesh != null)
        {
            textMesh.color = criticalColor;
            startColor = criticalColor;
        }

        UpdateScaleAndVisuals();

        if (particleText != null)
        {
            ApplyParticleText();
        }
    }

    private void UpdateScaleAndVisuals()
    {
        float strength = customStrengthMultiplier > 0f ? customStrengthMultiplier : GetPlayerStrength();

        float scaleMultiplier = 1.0f;

        if (enableInfiniteStrengthScaling)
        {
            // Bônus pela Força Base do jogador (SEM TETO MÁXIMO / INFINITO)
            float strengthBonus = Mathf.Max(0f, strength - 1.0f) * strengthScaleSensitivity;

            // Bônus adicional pelo valor numérico do dano causado (SEM TETO MÁXIMO / INFINITO)
            float damageBonus = 0f;
            if (currentDamageValue > 0)
            {
                damageBonus = Mathf.Max(0f, currentDamageValue - baseDamageReference) * damageScaleSensitivity;
            }

            scaleMultiplier = 1.0f + strengthBonus + damageBonus;
        }

        // Multiplicador se for acerto crítico
        if (isCriticalHit)
        {
            scaleMultiplier *= criticalSizeMultiplier;
        }

        // Evita escala nula ou invisível caso haja debuffs extremos
        scaleMultiplier = Mathf.Max(minScaleMultiplier, scaleMultiplier);

        // Aplica no transform.localScale de forma infinita (escala todo o canvas e texto em 3D)
        transform.localScale = initialLocalScale * scaleMultiplier;

        // Se o número for muito grande, ergue ligeiramente o ponto de origem para não cortar no chão ou no mob
        if (!hasAppliedHeightLift && scaleMultiplier > 1.25f)
        {
            float lift = Mathf.Min((scaleMultiplier - 1.0f) * 0.12f, 8f);
            transform.position += Vector3.up * lift;
            hasAppliedHeightLift = true;
        }

        // Velocidade com que o número flutua para cima cresce suavemente para acompanhar a imponência
        floatSpeed = baseFloatSpeed * Mathf.Pow(scaleMultiplier, 0.35f);

        // Ajusta duração proporcionalmente (com limite suave para não poluir o cenário indefinidamente)
        float critLtMult = isCriticalHit ? criticalLifetimeMultiplier : 1.0f;
        lifetime = baseLifetime * critLtMult * Mathf.Clamp(Mathf.Pow(scaleMultiplier, 0.15f), 1.0f, 2.5f);
        timer = lifetime;
    }

    private float GetPlayerStrength()
    {
        if (PlayerAttributesOffensive.Instance != null)
        {
            return PlayerAttributesOffensive.Instance.baseDamageMultiplier;
        }

        PlayerAttributesOffensive found = FindFirstObjectByType<PlayerAttributesOffensive>();
        if (found != null)
        {
            return found.baseDamageMultiplier;
        }

        return 1.0f;
    }

    private void ApplyParticleText()
    {
        if (particleText == null) return;

        Color c1 = isCriticalHit ? particleColorCrit1 : particleColorNormal1;
        Color c2 = isCriticalHit ? particleColorCrit2 : particleColorNormal2;
        float s = isCriticalHit ? (particleBaseSize * criticalSizeMultiplier) : particleBaseSize;
        float lt = isCriticalHit ? (particleLifetimeMultiplier * criticalLifetimeMultiplier) : particleLifetimeMultiplier;

        particleText.UpdateText(
            newText: pendingText,
            newSize: s,
            newColor1: c1,
            newColor2: c2,
            newLifetimeMultiplier: lt
        );
    }
}