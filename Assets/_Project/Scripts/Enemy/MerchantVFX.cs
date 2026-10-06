using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador Dedicado de VFX, Defesa e Reação Visual do Mercador das Sombras (Capítulo 4 - GDD).
/// Totalmente otimizado para zero lag e alta performance.
/// </summary>
public class MerchantVFX : MonoBehaviour
{
    [Header("1. Escudo Místico (Formato Elipsoide)")]
    [Tooltip("Objeto do escudo (ex: Magic shield blue ou pink).")]
    public GameObject shieldVisualObject;

    [Tooltip("Escala não-uniforme para cobrir a silhueta alta do Mercador (Padrão: 1.2, 2.3, 1.2).")]
    public Vector3 shieldElipsoidScale = new Vector3(1.2f, 2.3f, 1.2f);

    [Tooltip("Duração do flash do escudo em segundos.")]
    public float shieldDuration = 1.2f;

    [Tooltip("Velocidade de expansão do escudo no momento do golpe.")]
    public float shieldExpansionSpeed = 4.0f;

    [Tooltip("Multiplicador de pulsação de impacto.")]
    public float impactPulseMultiplier = 1.08f;

    [Header("2. Faíscas de Impacto (Hit Sparks)")]
    [Tooltip("Lista de objetos de faíscas pré-posicionados em locais variados do manto do Mercador.")]
    public GameObject[] hitSparkChildObjects;

    [Tooltip("Prefab de faísca dinâmico opcional caso prefira instanciar por código.")]
    public GameObject hitSparkPrefab;

    [Header("3. Tremor de Câmera (Camera Shake Forte)")]
    [Tooltip("Ativa o tremor pesado de tela ao golpear o Mercador.")]
    public bool enableStrongCameraShake = true;

    [Tooltip("Duração do tremor em segundos.")]
    public float shakeDuration = 0.35f;

    [Tooltip("Intensidade do tremor de tela (0.55 = repulsão forte e impactante).")]
    public float shakeIntensity = 0.55f;

    [Header("4. VFX dos Pactos do Mercador (Futuras Expansões)")]
    public GameObject bloodExchangeVFX;
    public GameObject surgicalRemovalVFX;
    public GameObject curseAuraVFX;

    [Header("5. 🔊 Áudio do Escudo (3 sons aleatórios)")]
    [Tooltip("AudioSource para tocar os sons do escudo. Se não atribuído, será criado automaticamente.")]
    public AudioSource audioSource;

    [Tooltip("Array de 3 AudioClips de deflexão do escudo. A cada hit, um será escolhido aleatoriamente.")]
    public AudioClip[] deflectSounds = new AudioClip[3];

    [Range(0f, 1f)]
    [Tooltip("Volume dos sons de deflexão do escudo.")]
    public float deflectVolume = 0.8f;

    [Tooltip("Se verdadeiro, aplica leve variação de pitch a cada hit para soar mais natural.")]
    public bool randomizePitch = true;

    [Range(0.85f, 1.15f)]
    [Tooltip("Pitch mínimo (variação para baixo).")]
    public float pitchMin = 0.9f;

    [Range(0.85f, 1.15f)]
    [Tooltip("Pitch máximo (variação para cima).")]
    public float pitchMax = 1.1f;

    [Header("6. Repulsão Leve de Knockback")]
    [Tooltip("Se verdadeiro, aplica um leve empurrão de repulsão no jogador ao atingir o escudo.")]
    public bool enableRepulsionKnockback = true;

    [Tooltip("Força do empurrão suave de repulsão (Padrão: 4.5 = leve e elegante).")]
    public float repulsionForce = 4.5f;

    [Tooltip("Duração da força de empurrão em segundos.")]
    public float knockbackDuration = 0.12f;

    [Header("7. Escala de Ira do Mercador (Progressão de Punição)")]
    [Tooltip("4º Hit: Ativa a distorção óptica de olho de peixe na câmera.")]
    public int hitsToTriggerHallucination = 4;
    [Tooltip("7º Hit: Deixa o ambiente/mapa escuro (-1.2 EV).")]
    public int hitsToTriggerDarkEnvironment = 7;
    [Tooltip("9º Hit: Inversão total de cores na câmera (Visão Negativa).")]
    public int hitsToTriggerColorInversion = 9;
    [Tooltip("10º Hit: Execução das Sombras (Morte imediata do jogador).")]
    public int hitsToTriggerDeath = 10;

    // Cache interno de partículas para evitar GetComponentsInChildren em tempo de execução
    private ParticleSystem[] cachedShieldParticles;
    private Dictionary<GameObject, ParticleSystem[]> cachedSparkParticles = new Dictionary<GameObject, ParticleSystem[]>();

    private Coroutine shieldCoroutine;
    private Coroutine repulsionCoroutine;
    private int consecutiveHitCount = 0;
    private float timeSinceLastHit = 0f;
    private float lastShakeTime = -1f;
    private int lastDeflectIndex = -1; // Evita repetir o mesmo som duas vezes seguidas

    private void Awake()
    {
        InitializeMerchantShield();
    }

    private void InitializeMerchantShield()
    {
        if (shieldVisualObject == null)
        {
            Transform childShield = transform.Find("Shield") ?? transform.Find("Escudo");
            if (childShield != null) shieldVisualObject = childShield.gameObject;
        }

        if (shieldVisualObject != null)
        {
            shieldVisualObject.transform.localScale = shieldElipsoidScale;
            shieldVisualObject.SetActive(false);
            cachedShieldParticles = shieldVisualObject.GetComponentsInChildren<ParticleSystem>(true);
        }

        if (hitSparkChildObjects != null)
        {
            foreach (var spark in hitSparkChildObjects)
            {
                if (spark != null)
                {
                    spark.SetActive(false);
                    cachedSparkParticles[spark] = spark.GetComponentsInChildren<ParticleSystem>(true);
                }
            }
        }

        // Auto-cria AudioSource se nenhum foi atribuído no Inspector
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }
    }

    void Update()
    {
        if (consecutiveHitCount > 0)
        {
            timeSinceLastHit += Time.deltaTime;
            if (timeSinceLastHit > 30.0f)
            {
                consecutiveHitCount = 0;
            }
        }
    }

    /// <summary>
    /// Método chamado quando qualquer ataque/faca atinge o Mercador.
    /// </summary>
    public void TriggerMerchantHitReaction(Vector3 hitPosition = default)
    {
        consecutiveHitCount++;
        timeSinceLastHit = 0f;

        // Progressão dos Estágios de Punição do Mercador:
        if (consecutiveHitCount >= hitsToTriggerDeath)
        {
            MerchantHallucinationEffect.TriggerMerchantExecution(gameObject);
        }
        else if (consecutiveHitCount >= hitsToTriggerColorInversion)
        {
            MerchantHallucinationEffect.TriggerColorInversion();
        }
        else if (consecutiveHitCount >= hitsToTriggerDarkEnvironment)
        {
            MerchantHallucinationEffect.TriggerDarkEnvironment();
        }
        else if (consecutiveHitCount >= hitsToTriggerHallucination)
        {
            MerchantHallucinationEffect.TriggerHallucination();
        }

        // 1. Tremor de Câmera (Com Throttle de 0.08s para evitar sobrecarga com facas rápidas)
        if (enableStrongCameraShake && Time.time - lastShakeTime > 0.08f)
        {
            lastShakeTime = Time.time;
            if (CameraShakeFeedback.Instance != null)
            {
                CameraShakeFeedback.TriggerShake(shakeDuration, shakeIntensity, 35f);
            }
            else
            {
                BossController.TriggerCameraShake(shakeDuration, shakeIntensity);
            }
        }

        // 2. Disparo de Faíscas
        TriggerHitSpark(hitPosition);

        // 3. Áudio de Repulsão (som aleatório)
        PlayRandomDeflectSound();

        // 4. Animação de Flash do Escudo Elipsoide
        if (shieldVisualObject != null)
        {
            if (shieldCoroutine != null) StopCoroutine(shieldCoroutine);
            shieldCoroutine = StartCoroutine(MerchantShieldRoutine());
        }

        // 5. Repulsão Leve de Knockback
        if (enableRepulsionKnockback)
        {
            ApplyPlayerRepulsion();
        }
    }

    private void ApplyPlayerRepulsion()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            PlayerM p = Object.FindFirstObjectByType<PlayerM>();
            if (p != null) playerObj = p.gameObject;
        }

        if (playerObj != null)
        {
            Rigidbody playerRb = playerObj.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 pushDir = (playerObj.transform.position - transform.position);
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.001f) pushDir = -transform.forward;
                else pushDir.Normalize();

                if (repulsionCoroutine != null) StopCoroutine(repulsionCoroutine);
                repulsionCoroutine = StartCoroutine(RepulsionRoutine(playerRb, pushDir));
            }
        }
    }

    private IEnumerator RepulsionRoutine(Rigidbody playerRb, Vector3 pushDir)
    {
        float elapsed = 0f;
        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            float currentForce = Mathf.Lerp(repulsionForce, 0f, elapsed / knockbackDuration);
            playerRb.linearVelocity = new Vector3(pushDir.x * currentForce, playerRb.linearVelocity.y, pushDir.z * currentForce);
            yield return null;
        }
        repulsionCoroutine = null;
    }

    /// <summary>
    /// Toca um som aleatório do array deflectSounds, evitando repetir o último tocado.
    /// Aplica variação de pitch opcional para soar mais natural.
    /// </summary>
    private void PlayRandomDeflectSound()
    {
        if (deflectSounds == null || deflectSounds.Length == 0) return;
        if (audioSource == null) return;

        // Filtra apenas os clips não-nulos
        int validCount = 0;
        for (int i = 0; i < deflectSounds.Length; i++)
        {
            if (deflectSounds[i] != null) validCount++;
        }
        if (validCount == 0) return;

        // Sorteia um índice diferente do último (se possível)
        int index;
        if (validCount > 1)
        {
            do
            {
                index = Random.Range(0, deflectSounds.Length);
            } while (deflectSounds[index] == null || index == lastDeflectIndex);
        }
        else
        {
            // Só tem 1 clip válido, encontra ele
            index = 0;
            for (int i = 0; i < deflectSounds.Length; i++)
            {
                if (deflectSounds[i] != null) { index = i; break; }
            }
        }

        lastDeflectIndex = index;

        // Variação de pitch para naturalidade
        if (randomizePitch)
        {
            audioSource.pitch = Random.Range(pitchMin, pitchMax);
        }
        else
        {
            audioSource.pitch = 1f;
        }

        audioSource.PlayOneShot(deflectSounds[index], deflectVolume);
    }

    public void TakeDamage(int damage, bool isCritical = false)
    {
        TriggerMerchantHitReaction();
    }

    private void TriggerHitSpark(Vector3 hitPosition)
    {
        if (hitSparkChildObjects != null && hitSparkChildObjects.Length > 0)
        {
            int index = Random.Range(0, hitSparkChildObjects.Length);
            GameObject sparkObj = hitSparkChildObjects[index];

            if (sparkObj != null)
            {
                sparkObj.SetActive(false);
                sparkObj.SetActive(true);

                if (cachedSparkParticles.TryGetValue(sparkObj, out var particles) && particles != null)
                {
                    foreach (var ps in particles)
                    {
                        if (ps != null)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                            ps.Play(true);
                        }
                    }
                }
            }
        }
        else if (hitSparkPrefab != null)
        {
            Vector3 sparkPos = (hitPosition != default) ? hitPosition : transform.position + Vector3.up * 1.2f;
            Quaternion sparkRot = (hitPosition != default && hitPosition != transform.position)
                ? Quaternion.LookRotation(hitPosition - transform.position)
                : Quaternion.identity;

            GameObject spark = Instantiate(hitSparkPrefab, sparkPos, sparkRot);
            Destroy(spark, 1.5f);
        }
    }

    private IEnumerator MerchantShieldRoutine()
    {
        shieldVisualObject.SetActive(true);
        Transform shieldTr = shieldVisualObject.transform;
        shieldTr.localScale = shieldElipsoidScale;

        if (cachedShieldParticles != null)
        {
            foreach (var ps in cachedShieldParticles)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(true);
                }
            }
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.2f, shieldDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (t < 0.15f)
            {
                float pulse = Mathf.Sin((t / 0.15f) * Mathf.PI * 0.5f);
                shieldTr.localScale = shieldElipsoidScale * Mathf.Lerp(1.0f, impactPulseMultiplier, pulse);
            }
            else
            {
                shieldTr.localScale = Vector3.Lerp(shieldTr.localScale, shieldElipsoidScale, Time.deltaTime * shieldExpansionSpeed);
            }

            yield return null;
        }

        shieldVisualObject.SetActive(false);
        shieldCoroutine = null;
    }

    private void OnDisable()
    {
        if (shieldCoroutine != null) { StopCoroutine(shieldCoroutine); shieldCoroutine = null; }
        if (repulsionCoroutine != null) { StopCoroutine(repulsionCoroutine); repulsionCoroutine = null; }
        if (shieldVisualObject != null) shieldVisualObject.SetActive(false);
    }
}
