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

    private float timer;
    private Color startColor;
    private float baseFontSize;
    private bool isCriticalHit = false;
    private string pendingText = null;

    void Awake()
    {
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
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

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

    public void SetText(string text)
    {
        pendingText = text;

        if (textMesh != null)
        {
            textMesh.text = text;
        }

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

        if (isCritical)
        {
            lifetime *= criticalLifetimeMultiplier;
            timer = lifetime;

            if (textMesh != null)
            {
                textMesh.color = criticalColor;
                startColor = criticalColor;
                textMesh.fontSize = baseFontSize * criticalSizeMultiplier;
            }

            if (particleText != null)
            {
                ApplyParticleText();
            }
        }
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