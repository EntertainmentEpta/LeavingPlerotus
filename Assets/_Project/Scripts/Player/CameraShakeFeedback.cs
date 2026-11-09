using UnityEngine;

/// <summary>
/// Senior Game Feel & VFX Component: Camera Shake Feedback System.
///  • 100% Decoupled: Listens to static event 'PlayerHealth.OnPlayerDamaged'.
///  • Scalable Shake Profiles: Adapts duration & intensity based on damage severity.
///  • Smooth Damped Impulse: Uses Perlin noise + exponential impulse damping.
///  • Zero Disorientation: Auto-clears within 0.10s - 0.25s and resets cleanly on room transitions.
/// </summary>
public class CameraShakeFeedback : MonoBehaviour
{
    private static CameraShakeFeedback _instance;
    public static CameraShakeFeedback Instance => _instance;

    [System.Serializable]
    public struct ShakeProfile
    {
        public float duration;
        public float intensity;
        public float frequency;
    }

    [Header("🎯 Perfis de Impacto por Severidade de Dano")]
    [Tooltip("Dano Leve (< 20 HP - ex: aranhas, projéteis pequenos)")]
    public ShakeProfile lightHitProfile = new ShakeProfile { duration = 0.12f, intensity = 0.12f, frequency = 25f };

    [Tooltip("Dano Médio (20 - 45 HP - ex: goblins, golems)")]
    public ShakeProfile mediumHitProfile = new ShakeProfile { duration = 0.18f, intensity = 0.25f, frequency = 30f };

    [Tooltip("Dano Pesado (> 45 HP - ex: Boss, explosões, impactos em área)")]
    public ShakeProfile heavyHitProfile = new ShakeProfile { duration = 0.25f, intensity = 0.45f, frequency = 35f };

    private Vector3 shakeOffset = Vector3.zero;
    private float currentDuration = 0f;
    private float currentTimer = 0f;
    private float currentIntensity = 0f;
    private float currentFrequency = 25f;
    private float noiseSeedX;
    private float noiseSeedY;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
            return;
        }

        noiseSeedX = Random.Range(0f, 100f);
        noiseSeedY = Random.Range(0f, 100f);
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerDamaged += HandlePlayerDamaged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearShake();
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ClearShake();
    }

    /// <summary>
    /// Handler desacoplado: Escuta automaticamente os eventos de dano do PlayerHealth.
    /// </summary>
    private void HandlePlayerDamaged(int damageAmount, GameObject attacker)
    {
        TriggerDamageShake(damageAmount);
    }

    /// <summary>
    /// Seleciona o perfil de trepidação escalável com base na quantidade de dano.
    /// </summary>
    public static void TriggerDamageShake(int damageAmount)
    {
        if (_instance == null) return;

        ShakeProfile profile;
        if (damageAmount < 20)
        {
            profile = _instance.lightHitProfile;
        }
        else if (damageAmount <= 45)
        {
            profile = _instance.mediumHitProfile;
        }
        else
        {
            profile = _instance.heavyHitProfile;
        }

        _instance.StartShake(profile.duration, profile.intensity, profile.frequency);
    }

    /// <summary>
    /// Dispara um tremor customizado por duração e intensidade.
    /// </summary>
    public static void TriggerShake(float duration, float intensity, float frequency = 30f)
    {
        if (_instance == null) return;
        _instance.StartShake(duration, intensity, frequency);
    }

    private void StartShake(float duration, float intensity, float frequency)
    {
        if (Time.timeScale <= 0.0001f) return;

        // Se já estiver tremendo com mais intensidade, mantém o maior impacto
        if (currentTimer > 0f && currentIntensity > intensity)
        {
            currentDuration = Mathf.Max(currentDuration, duration);
        }
        else
        {
            currentDuration = duration;
            currentTimer = duration;
            currentIntensity = intensity;
            currentFrequency = frequency;
        }
    }

    /// <summary>
    /// Limpa instantaneamente o tremor de câmera.
    /// </summary>
    public void ClearShake()
    {
        currentTimer = 0f;
        shakeOffset = Vector3.zero;
    }

    void LateUpdate()
    {
        // Impede que o shake desloque a câmera infinitamente enquanto o jogo estiver pausado (ex: diálogo do Eptinho com Time.timeScale = 0)
        if (Time.timeScale <= 0.0001f || Time.deltaTime <= 0.0001f)
        {
            if (currentTimer > 0f)
            {
                ClearShake();
            }
            return;
        }

        if (currentTimer > 0f)
        {
            currentTimer -= Time.deltaTime;
            float progress = Mathf.Clamp01(currentTimer / currentDuration);

            // Damping suave (decaimento quadrático exponencial)
            float damping = progress * progress;

            float timeVal = Time.time * currentFrequency;
            float offsetX = (Mathf.PerlinNoise(noiseSeedX + timeVal, 0f) - 0.5f) * 2f * currentIntensity * damping;
            float offsetY = (Mathf.PerlinNoise(0f, noiseSeedY + timeVal) - 0.5f) * 2f * currentIntensity * damping;
            float offsetZ = (Mathf.PerlinNoise(noiseSeedX + timeVal, noiseSeedY + timeVal) - 0.5f) * 1.5f * currentIntensity * damping;

            shakeOffset = new Vector3(offsetX, offsetY, offsetZ);

            transform.position += shakeOffset;

            if (currentTimer <= 0f)
            {
                ClearShake();
            }
        }
    }
}
