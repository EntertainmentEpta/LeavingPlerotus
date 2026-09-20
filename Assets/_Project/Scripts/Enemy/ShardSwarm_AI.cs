using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IA do Shard Swarm (Estrela Elite) — GDD v6.0.
/// 
/// Comportamentos:
/// 1. Forma Unida (VULNERÁVEL):
///    - Flutua a altura fixa (1.1m) acima do chão (rente ao torso do player).
///    - As 5 pontas flutuam visivelmente em 3D (para frente, trás, cima e lados em campo gravitacional).
///    - Virada para o jogador.
///    - Acumula carga elétrica progressiva; se não for destruída a tempo, descarrega Pulso Elétrico.
/// 
/// 2. Disparo em Formação de Área (INVULNERÁVEL):
///    - Expande as 5 pontas em anel/área ampla antes de disparar.
///    - Dispara as pontas em ordem e delays 100% aleatórios.
///    - Se acertar 3+ hits no player → aplica STUN + VFX de Choque Elétrico!
/// 
/// 3. Reagrupamento com Trilhas Elétricas (INVULNERÁVEL):
///    - Pontas retornam ao núcleo deixando Trilhas Elétricas visíveis no piso (ElectricTrailVFX).
///    - Dano leve por tick controlado (com cooldown para evitar mortes instantâneas).
/// </summary>

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShardSwarmHealth))]
public class ShardSwarm_AI : MonoBehaviour
{
    public enum SwarmState
    {
        FormaUnida,      // Vulnerável, acumulando carga
        Disparando,      // Invulnerável, pontas voando
        Reagrupando      // Invulnerável, pontas voltando + rastro no chão
    }

    [Header("--- Estado Atual ---")]
    public SwarmState currentState = SwarmState.FormaUnida;

    [Header("--- Referências dos Modelos ---")]
    [Tooltip("Objeto central do modelo (Cubo/Núcleo). Se nulo, procura filho chamado CORE.")]
    public Transform coreTransform;
    [Tooltip("Array das pontas/espinhos. Se vazio, encontra automaticamente todos os filhos.")]
    public Transform[] spikeTransforms;

    [Header("--- Modos & IA ---")]
    [Tooltip("Se falso, a Estrela não ataca automaticamente (ótimo para testar animações em paz).")]
    public bool autoAttackEnabled = true;

    [Header("--- Configurações de Flutuação & Giro ---")]
    public float coreRotateSpeed = 200f;
    [Tooltip("Altura fixa em relação ao chão.")]
    public float floatHeight = 1.8f;
    public float pulseFrequency = 20f;
    [Tooltip("Amplitude de flutuação 3D das pontas em Idle.")]
    public float pulseAmplitude = 0.35f;
    public float activationDistance = 30f;
    public float moveSpeed = 3.5f;

    [Header("--- Movimentação Orgânica 2D / Aleatória (Sem Circundar) ---")]
    [Tooltip("Frequência da onda de avanço e recuo (ex: 1.2).")]
    public float sineWaveFrequency = 1.2f;
    [Tooltip("Amplitude de aproximação e recuo para frente/trás (ex: 3.5).")]
    public float sineWaveAmplitude = 3.5f;
    [Tooltip("Desvio lateral orgânico e aleatório (esquerda/direita) sem formar círculo (ex: 3.0).")]
    public float sideDriftAmplitude = 3.0f;
    [Tooltip("Velocidade da variação aleatória de direção e oscilação (ex: 1.0).")]
    public float randomnessChangeSpeed = 1.0f;

    [Header("--- Carga Elétrica (Forma Unida) ---")]
    public float maxChargeTime = 4.5f;
    public float electricPulseRadius = 0.75f;
    public int electricPulseDamage = 0;
    public float electricPulseStunDuration = 0.8f;
    public Color normalGlowColor = Color.cyan;
    public Color maxChargeGlowColor = new Color(1f, 0.85f, 0.2f); // Amarelo Faísca

    [Header("--- Disparo em Formação de Área (Estilo Ioiô) ---")]
    public float projectileSpeed = 60f;
    public float maxProjectileDistance = 25f;
    public int projectileDamage = 0;
    public float multiHitStunDuration = 1.5f;
    [Tooltip("Pausa entre disparos para a Estrela mirar no player antes de lançar cada pino (segundos).")]
    public float timeBetweenShotsMin = 0.6f;
    public float timeBetweenShotsMax = 0.95f;
    [Tooltip("Multiplicador de expansão ampla do Wind-Up telegrafado antes de disparar (ex: 2.2).")]
    public float windUpExpansionMultiplier = 2.2f;
    [Tooltip("Duração da pausa (telegrafo visual) com a estrela bem aberta antes do primeiro disparo (ex: 0.85s).")]
    public float windUpHoldDuration = 0.85f;
    [Tooltip("Tempo de espera (em segundos) que os projéteis ficam pairando no ar congelados antes de iniciarem o retorno.")]
    public float spikeAirHoverTime = 1.2f;
    [Tooltip("Intervalo entre o retorno de cada pino individual (retorno sequencial aleatório).")]
    public float spikeReturnInterval = 0.35f;

    public enum SpinAxis { LocalZ, LocalX, LocalY }

    [Header("--- Animação Wheel Spin (Ajustes do Inspetor) ---")]
    [Tooltip("Eixo de rotação no Prefab (Padrão: LocalZ / Vector3.forward).")]
    public SpinAxis wheelSpinAxis = SpinAxis.LocalZ;
    [Tooltip("Multiplicador de expansão da estrela durante o Giro de Roda.")]
    public float wheelSpinExpansionMultiplier = 2.0f;
    [Tooltip("Duração do Giro de Roda (segundos).")]
    public float wheelSpinDuration = 1.5f;
    [Tooltip("Número de voltas completas de 360° durante o giro.")]
    public float wheelSpinRotations = 3.0f;
    [Tooltip("Velocidade de Rotação do Giro (graus por segundo). Ex: 360 = 1 volta/s, 1080 = 3 voltas/s, 1800 = ultra rápido.")]
    public float wheelSpinSpeedDegreesPerSec = 360f;
    [Tooltip("Intervalo em segundos para o giro acontecer periodicamente por estética (ex: 30s).")]
    public float wheelSpinPeriodicInterval = 30f;
    public float returnSpeed = 30f;
    public float trailDuration = 3.0f;
    public int trailDamagePerTick = 0;

    [Header("--- Escudo de Espinhos & Janela de Vulnerabilidade ---")]
    [Tooltip("Dano de retaliação ao jogador ao encostar na Estrela enquanto a formação de espinhos está fechada.")]
    public int spikeShieldContactDamage = 8;
    [Tooltip("Força de empurrão (knockback) no jogador ao encostar nos espinhos fechados.")]
    public float spikeShieldKnockbackForce = 6f;
    [Tooltip("Multiplicador de dano sofrido quando as pontas estão fechadas (escudo de espinhos ativo). Ex: 0.5 = 50% de redução.")]
    public float spikeShieldDamageMultiplier = 0.5f;
    [Tooltip("Multiplicador de dano crítico sofrido quando o núcleo (Core) está exposto sem pontas (Disparando/Reagrupando). Ex: 1.5 = +50% dano crítico.")]
    public float exposedCoreDamageMultiplier = 1.5f;
    [Tooltip("Raio da zona de contato do escudo de espinhos (metros).")]
    public float spikeShieldHazardRadius = 1.3f;
    [Tooltip("Objeto visual do Escudo (GameObject 'Escudo' com a esfera de holograma). Se vazio, busca automaticamente um filho chamado 'Escudo'.")]
    public GameObject shieldVisualObject;
    [Tooltip("Duração do brilho/pulsar do holograma do escudo ao receber dano no modo protegido.")]
    public float shieldFlashDuration = 0.35f;

    [Header("--- Áudio ---")]
    [Tooltip("Som do disparo das pontas/espinhos da Estrela")]
    public AudioClip shotSound;
    [Tooltip("Volume do som de disparo")]
    [Range(0f, 1f)]
    public float shotSoundVolume = 0.8f;

    [Tooltip("Som do tiro voltando/recolhimento (Se nulo, usa o shotSound com o pitch ajustado)")]
    public AudioClip returnShotSound;
    [Tooltip("Volume do som do tiro voltando")]
    [Range(0f, 1f)]
    public float returnShotSoundVolume = 0.8f;
    [Tooltip("Tom/Pitch do som do tiro voltando (Valores menores que 1.0 deixam mais GRAVE, acima de 1.0 deixam mais AGUDO)")]
    [Range(0.2f, 2.0f)]
    public float returnShotPitch = 0.65f;
    [Tooltip("Ativa o efeito de Reverb / Eco no som do tiro voltando")]
    public bool useReturnShotReverb = true;
    [Tooltip("Estilo / Preset do Reverb (Cave, Room, Hallway, Generic, etc.)")]
    public AudioReverbPreset returnShotReverbPreset = AudioReverbPreset.Cave;

    [Tooltip("Som do choque elétrico / zap (pulso elétrico / debuff)")]
    public AudioClip zapSound;
    [Tooltip("Volume do som do choque (zap)")]
    [Range(0f, 1f)]
    public float zapSoundVolume = 0.9f;

    // Privados internos
    private Transform playerTransform;
    private Rigidbody rb;
    private ShardSwarmHealth health;
    private EnemyDrops drops;
    private Renderer coreRenderer;

    private Vector3[] spikeLocalPositionsHome;
    private Quaternion[] spikeLocalRotationsHome;
    private Transform[] spikeHomeParents;
    private Vector3[] spikeLaunchDirections;
    private Vector3[] spikeLastTrailPositions;
    private bool[] spikeIsReturning;
    private bool[] spikeHasHitPlayer;

    private float chargeTimer = 0f;
    private float periodicWheelSpinTimer = 0f;
    private float randomSeedX;
    private float randomSeedY;
    private bool isActivated = false;
    private bool isDead = false;
    private int playerHitsInCurrentAttack = 0;
    private LayerMask groundMask;

    private void Reset()
    {
        ApplyDefaultValues();
    }

    [ContextMenu("► APLICAR VALORES PADRÃO (SCREENSHOT)")]
    public void ApplyDefaultValues()
    {
        coreRotateSpeed = 200f;
        floatHeight = 1.8f;
        pulseFrequency = 20f;
        pulseAmplitude = 0.35f;
        activationDistance = 30f;
        moveSpeed = 3.5f;

        sineWaveFrequency = 1.2f;
        sineWaveAmplitude = 3.5f;
        sideDriftAmplitude = 3.0f;
        randomnessChangeSpeed = 1.0f;

        maxChargeTime = 4.5f;
        electricPulseRadius = 0.75f;
        electricPulseDamage = 0;
        electricPulseStunDuration = 0.8f;
        normalGlowColor = Color.cyan;
        maxChargeGlowColor = new Color(1f, 0.85f, 0.2f);

        projectileSpeed = 60f;
        maxProjectileDistance = 25f;
        projectileDamage = 0;
        multiHitStunDuration = 1.5f;
        timeBetweenShotsMin = 0.6f;
        timeBetweenShotsMax = 0.95f;
        windUpExpansionMultiplier = 2.2f;
        windUpHoldDuration = 0.85f;
        spikeAirHoverTime = 1.2f;
        spikeReturnInterval = 0.35f;

        wheelSpinAxis = SpinAxis.LocalZ;
        wheelSpinExpansionMultiplier = 2.0f;
        wheelSpinDuration = 1.5f;
        wheelSpinRotations = 3.0f;
        wheelSpinSpeedDegreesPerSec = 360f;
        wheelSpinPeriodicInterval = 30f;
        returnSpeed = 30f;
        trailDuration = 3.0f;
        trailDamagePerTick = 0;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<ShardSwarmHealth>();
        drops = GetComponent<EnemyDrops>();

        // Garante Tag "Enemy" e Layer "Enemy" para detecção de acerto pelas armas do player
        if (!CompareTag("Enemy")) gameObject.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1) gameObject.layer = enemyLayer;

        // Garante Colisor Sólido para receber acertos de armas e projéteis do player
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.radius = 1.3f;
        col.isTrigger = false;

        groundMask = LayerMask.GetMask("Default", "Terrain", "NavMesh", "Ground");
        if (groundMask == 0) groundMask = ~0; // Fallback
    }

    public void DestroyAllSpikes()
    {
        if (spikeTransforms != null)
        {
            for (int i = 0; i < spikeTransforms.Length; i++)
            {
                if (spikeTransforms[i] != null && spikeTransforms[i].gameObject != null)
                {
                    Destroy(spikeTransforms[i].gameObject);
                }
            }
        }
    }

    private void OnDestroy()
    {
        DestroyAllSpikes();
    }

    void Start()
    {
        randomSeedX = Random.Range(0f, 1000f);
        randomSeedY = Random.Range(1000f, 2000f);

        FindPlayer();
        SetupReferences();

        if (shieldVisualObject == null)
        {
            Transform childShield = transform.Find("Escudo");
            if (childShield != null) shieldVisualObject = childShield.gameObject;
        }
        if (shieldVisualObject != null) shieldVisualObject.SetActive(false);

        rb.useGravity = false;
        rb.freezeRotation = true;

        SetState(SwarmState.FormaUnida);
    }

    private Coroutine shieldFlashCoroutine;

    public void FlashShieldVisual()
    {
        // Busca recursiva por qualquer filho chamado 'Escudo' (mesmo se aninhado dentro do Core)
        if (shieldVisualObject == null)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                if (t.name.Equals("Escudo", System.StringComparison.OrdinalIgnoreCase))
                {
                    shieldVisualObject = t.gameObject;
                    break;
                }
            }
        }

        if (shieldVisualObject != null)
        {
            if (shieldFlashCoroutine != null) StopCoroutine(shieldFlashCoroutine);
            shieldFlashCoroutine = StartCoroutine(ShieldFlashRoutine());
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] ⚠️ Objeto visual 'Escudo' não foi encontrado no Prefab!");
        }
    }

    private IEnumerator ShieldFlashRoutine()
    {
        if (shieldVisualObject == null) yield break;

        shieldVisualObject.SetActive(true);
        Transform shieldTr = shieldVisualObject.transform;
        Vector3 baseScale = shieldTr.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one * 42f;

        Renderer shieldRenderer = shieldVisualObject.GetComponent<Renderer>();
        Material shieldMat = (shieldRenderer != null) ? shieldRenderer.material : null;

        Color baseColor = Color.cyan;
        string colorProp = null;

        if (shieldMat != null)
        {
            if (shieldMat.HasProperty("_Color")) colorProp = "_Color";
            else if (shieldMat.HasProperty("_BaseColor")) colorProp = "_BaseColor";
            else if (shieldMat.HasProperty("_HologramColor")) colorProp = "_HologramColor";

            if (!string.IsNullOrEmpty(colorProp))
            {
                baseColor = shieldMat.GetColor(colorProp);
            }
        }

        float totalDuration = Mathf.Max(0.35f, shieldFlashDuration);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / totalDuration;

            // Opacidade instantânea 100% no hit (frame 1) -> Fade out gradual
            float alpha = 1.0f - t;

            // Animação de Escala: Micro-pulsar de holograma (1.0x -> 1.08x)
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.08f, t);
            shieldTr.localScale = baseScale * scaleMultiplier;

            // Transparência suave no Material
            if (shieldMat != null && !string.IsNullOrEmpty(colorProp))
            {
                Color c = baseColor;
                c.a = baseColor.a * alpha * 0.85f;
                shieldMat.SetColor(colorProp, c);
            }

            yield return null;
        }

        // Restaura escala original e desativa o mesh
        shieldTr.localScale = baseScale;
        if (shieldMat != null && !string.IsNullOrEmpty(colorProp))
        {
            shieldMat.SetColor(colorProp, baseColor);
        }

        shieldVisualObject.SetActive(false);
        shieldFlashCoroutine = null;
    }

    void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    void SetupReferences()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (coreTransform == null)
        {
            Transform foundCore = transform.Find("CORE");
            if (foundCore != null) coreTransform = foundCore;
            else if (transform.childCount > 0) coreTransform = transform.GetChild(0);
            else coreTransform = transform;
        }

        if (coreTransform != null)
            coreRenderer = coreTransform.GetComponent<Renderer>();

        if (spikeTransforms == null || spikeTransforms.Length == 0)
        {
            List<Transform> spikes = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child != coreTransform && (child.name.StartsWith("Ponta") || child.name.StartsWith("mesh")))
                {
                    spikes.Add(child);
                }
            }

            if (spikes.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    if (child != coreTransform) spikes.Add(child);
                }
            }
            spikeTransforms = spikes.ToArray();
        }

        int count = spikeTransforms.Length;
        spikeLocalPositionsHome = new Vector3[count];
        spikeLocalRotationsHome = new Quaternion[count];
        spikeHomeParents = new Transform[count];
        spikeLaunchDirections = new Vector3[count];
        spikeLastTrailPositions = new Vector3[count];
        spikeIsReturning = new bool[count];
        spikeHasHitPlayer = new bool[count];

        for (int i = 0; i < count; i++)
        {
            if (spikeTransforms[i] != null)
            {
                spikeHomeParents[i] = spikeTransforms[i].parent;
                spikeLocalPositionsHome[i] = spikeTransforms[i].localPosition;
                spikeLocalRotationsHome[i] = spikeTransforms[i].localRotation;

                // Garante que cada ponta também tenha a Tag "Enemy" para acertos de colisão
                if (!spikeTransforms[i].CompareTag("Enemy")) spikeTransforms[i].gameObject.tag = "Enemy";
                if (enemyLayer != -1) spikeTransforms[i].gameObject.layer = enemyLayer;
            }
        }
    }

    void Update()
    {
        if (playerTransform == null) { FindPlayer(); return; }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (!isActivated)
        {
            if (dist < activationDistance) isActivated = true;
            return;
        }

        UpdateHeightAboveGround();
        UpdateSpikeShieldHazard();

        switch (currentState)
        {
            case SwarmState.FormaUnida:
                UpdateFormaUnida();
                break;

            case SwarmState.Disparando:
                break;

            case SwarmState.Reagrupando:
                UpdateReagrupamento();
                break;
        }
    }

    private float spikeShieldDamageCooldown = 0f;

    void UpdateSpikeShieldHazard()
    {
        // O escudo de retaliação e repulsão física só fica ativo quando a formação de espinhos está fechada (FormaUnida)
        if (currentState != SwarmState.FormaUnida) return;
        if (playerTransform == null) return;

        spikeShieldDamageCooldown -= Time.deltaTime;

        Vector3 centerPos = (coreTransform != null) ? coreTransform.position : transform.position;
        float distToPlayer = Vector3.Distance(centerPos, playerTransform.position);

        if (distToPlayer <= spikeShieldHazardRadius)
        {
            // Aplica retaliação e empurrão se o player tentar avançar colado nos espinhos fechados
            if (spikeShieldDamageCooldown <= 0f)
            {
                spikeShieldDamageCooldown = 0.5f;

                PlayerHealth ph = playerTransform.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(spikeShieldContactDamage, gameObject);
                    ElectrocutedStatus.ApplyElectrocuted(playerTransform.gameObject, 5, 0.50f, 3.0f);
                }

                // Empurrão (Knockback) físico para afastar o player do escudo
                Vector3 pushDir = (playerTransform.position - centerPos);
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward;

                Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerRb.AddForce(pushDir.normalized * spikeShieldKnockbackForce, ForceMode.Impulse);
                }
            }
        }
    }

    public float GetCurrentDamageMultiplier()
    {
        // Se a estrela disparou as 5 pontas, o Núcleo (CORE) fica NÚ e 100% EXPOSTO -> Dano Crítico (+50%)
        if (currentState == SwarmState.Disparando || currentState == SwarmState.Reagrupando)
        {
            return exposedCoreDamageMultiplier; // 1.5x Dano Crítico
        }

        // Se a formação de espinhos está fechada ao redor do Core -> Escudo de Espinhos (50% de redução)
        return spikeShieldDamageMultiplier; // 0.5x Armadura
    }

    void UpdateHeightAboveGround()
    {
        float targetY = (playerTransform != null) ? playerTransform.position.y + floatHeight : transform.position.y;

        // RaycastAll ignorando colisores do próprio inimigo, pontas e triggers
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 2f, Vector3.down, 20f, groundMask, QueryTriggerInteraction.Ignore);

        float highestGroundY = float.MinValue;
        bool foundGround = false;

        foreach (RaycastHit hit in hits)
        {
            // Ignora se o colisor for do próprio inimigo ou do player
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.collider.gameObject == gameObject || hit.collider.CompareTag("Player") || hit.collider.CompareTag("Enemy"))
                continue;

            if (hit.point.y > highestGroundY)
            {
                highestGroundY = hit.point.y;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            targetY = highestGroundY + floatHeight;
        }

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * 5f);
        transform.position = pos;
    }

    void SetState(SwarmState newState)
    {
        currentState = newState;

        // Ao mudar de estado (ex: rearmar / disparar), interrompe a corrotina e desativa o escudo imediatamente!
        if (shieldFlashCoroutine != null)
        {
            StopCoroutine(shieldFlashCoroutine);
            shieldFlashCoroutine = null;
        }
        if (shieldVisualObject != null)
        {
            shieldVisualObject.SetActive(false);
        }

        if (health != null)
        {
            // O inimigo é vulnerável tanto de espinhos fechados (escudo) quanto de espinhos abertos (dano crítico)
            health.isInvulnerable = false;
        }

        if (newState == SwarmState.FormaUnida)
        {
            chargeTimer = 0f;
            playerHitsInCurrentAttack = 0;
            ResetSpikesToHome();
        }
    }

    public void ResetSpikesToHome()
    {
        if (spikeTransforms == null || spikeLocalPositionsHome == null) return;
        if (coreTransform != null) coreTransform.localRotation = Quaternion.identity;

        for (int i = 0; i < spikeTransforms.Length; i++)
        {
            if (spikeTransforms[i] != null && i < spikeLocalPositionsHome.Length)
            {
                if (spikeHomeParents != null && i < spikeHomeParents.Length && spikeHomeParents[i] != null)
                {
                    spikeTransforms[i].SetParent(spikeHomeParents[i]);
                }
                spikeTransforms[i].localPosition = spikeLocalPositionsHome[i];
                spikeTransforms[i].localRotation = spikeLocalRotationsHome[i];
            }
        }
    }

    // ------------------------------------------------------------------------
    // 1. FORMA UNIDA (Vulnerável, Flutuação 3D & Navegação Orgânica)
    // ------------------------------------------------------------------------

    void UpdateFormaUnida()
    {
        RotateTowardPlayer();

        if (coreTransform != null)
        {
            coreTransform.Rotate(Vector3.up * coreRotateSpeed * Time.deltaTime, Space.Self);
        }

        // Flutuação Tridimensional Evidente (para frente, trás, cima, lados)
        for (int i = 0; i < spikeTransforms.Length; i++)
        {
            if (spikeTransforms[i] != null)
            {
                Vector3 homePos = spikeLocalPositionsHome[i];

                float xOffset = Mathf.Sin(Time.time * pulseFrequency + i * 1.5f) * pulseAmplitude;
                float yOffset = Mathf.Cos(Time.time * (pulseFrequency * 0.9f) + i * 1.8f) * (pulseAmplitude * 1.3f);
                float zOffset = Mathf.Sin(Time.time * (pulseFrequency * 1.3f) + i * 2.2f) * pulseAmplitude;
                Vector3 floatOffset = new Vector3(xOffset, yOffset, zOffset);

                spikeTransforms[i].localPosition = Vector3.Lerp(
                    spikeTransforms[i].localPosition,
                    homePos + floatOffset,
                    Time.deltaTime * 6f);
            }
        }

        // --- MOVIMENTAÇÃO ORGÂNICA 2D ALEATÓRIA (Frente/Trás + Desvio Lateral Orgânico) ---
        Vector3 dirToPlayer = (playerTransform.position - transform.position);
        dirToPlayer.y = 0f;

        Vector3 forwardDir = dirToPlayer.sqrMagnitude > 0.01f ? dirToPlayer.normalized : transform.forward;
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir); // Eixo lateral perpendicular (Direita/Esquerda)

        // 1. Oscilação Orgânica para Frente e Trás (Mistura de Perlin Noise + Onda Senoidal)
        float noiseForward = (Mathf.PerlinNoise(randomSeedX, Time.time * randomnessChangeSpeed) - 0.5f) * 2.0f;
        float sineForward = Mathf.Sin(Time.time * sineWaveFrequency);
        float combinedForward = (sineForward * 0.6f + noiseForward * 0.4f) * (sineWaveAmplitude * 0.5f);

        float targetDistance = 8.5f + combinedForward;

        // 2. Oscilação Orgânica Lateral (Mistura de Perlin Noise + Cosseno suave — Sem formar círculo)
        float noiseSide = (Mathf.PerlinNoise(randomSeedY, Time.time * (randomnessChangeSpeed * 0.8f)) - 0.5f) * 2.0f;
        float cosSide = Mathf.Cos(Time.time * (sineWaveFrequency * 0.7f) + 1.2f);
        float combinedSide = (noiseSide * 0.65f + cosSide * 0.35f) * sideDriftAmplitude;

        // Posição alvo calculada no plano 2D ao redor do player
        Vector3 targetPos = playerTransform.position - (forwardDir * targetDistance) + (rightDir * combinedSide);
        targetPos.y = transform.position.y;

        Vector3 desiredVelocity = (targetPos - transform.position) * moveSpeed;
        if (desiredVelocity.magnitude > moveSpeed * 1.5f)
        {
            desiredVelocity = desiredVelocity.normalized * (moveSpeed * 1.5f);
        }

        // Aplica velocidade com inércia física suavizada (sensação de um organismo flutuante vivo)
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredVelocity, Time.deltaTime * 2.0f);

        // Giro de Shuriken periódico por estética (a cada ~14 segundos)
        periodicWheelSpinTimer += Time.deltaTime;
        if (periodicWheelSpinTimer >= wheelSpinPeriodicInterval && currentState == SwarmState.FormaUnida)
        {
            periodicWheelSpinTimer = 0f;
            StartCoroutine(WheelSpinRoutine());
            return;
        }

        // Carga Progressiva
        chargeTimer += Time.deltaTime;
        float chargeRatio = Mathf.Clamp01(chargeTimer / maxChargeTime);

        if (coreRenderer != null && coreRenderer.material.HasProperty("_Color"))
        {
            coreRenderer.material.color = Color.Lerp(normalGlowColor, maxChargeGlowColor, chargeRatio);
        }

        // Se o ataque automático estiver habilitado
        if (autoAttackEnabled)
        {
            if (chargeTimer >= maxChargeTime)
            {
                TriggerElectricPulse();
            }
            else if (dirToPlayer.magnitude <= Mathf.Max(18f, maxProjectileDistance) && chargeTimer >= 1.2f)
            {
                StartCoroutine(LaunchSpikesRoutine());
            }
        }
    }

    void RotateTowardPlayer()
    {
        if (playerTransform == null) return;
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            // Suavizado para parecer reativo e natural com inércia
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2.2f);
        }
    }

    public void TriggerElectricPulse()
    {
        Debug.Log("[ESTRELA] ⚡ Pulso Elétrico descarregado!");
        PlayZapSound(transform.position);

        Collider[] hits = Physics.OverlapSphere(transform.position, electricPulseRadius);
        foreach (Collider h in hits)
        {
            if (h.CompareTag("Player"))
            {
                PlayerHealth ph = h.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(electricPulseDamage, gameObject);
                    ph.ApplyStun(electricPulseStunDuration);
                    ElectricShockVFX.AttachToPlayer(h.gameObject, electricPulseStunDuration);
                }
            }
        }

        if (autoAttackEnabled)
        {
            SetState(SwarmState.Reagrupando);
        }
    }



    // ------------------------------------------------------------------------
    // ANIMAÇÕES DE ATAQUE E SHOWCASE (Ioiô, Disparo, Wheel Spin 360°, Wind-Up)
    // ------------------------------------------------------------------------

    public IEnumerator LaunchSpikesRoutine()
    {
        SetState(SwarmState.Disparando);
        rb.linearVelocity = Vector3.zero;

        int totalSpikes = spikeTransforms.Length;

        // FASE 0: WIDE WIND-UP TELEGRAPH (Abertura Ampla Telegrafada antes de disparar)
        float expandTimer = 0f;
        float expandDuration = 0.35f;
        float mult = Mathf.Max(1.2f, windUpExpansionMultiplier);

        while (expandTimer < expandDuration)
        {
            expandTimer += Time.deltaTime;
            float t = expandTimer / expandDuration;
            RotateTowardPlayer();

            for (int i = 0; i < totalSpikes; i++)
            {
                if (spikeTransforms[i] != null && i < spikeLocalPositionsHome.Length)
                {
                    spikeTransforms[i].localPosition = Vector3.Lerp(
                        spikeLocalPositionsHome[i],
                        spikeLocalPositionsHome[i] * mult,
                        t);
                    spikeTransforms[i].localRotation = spikeLocalRotationsHome[i];
                }
            }
            yield return null;
        }

        // Segura a Estrela BEM ABERTA encarando o player (Telegrafo visual claro para o jogador se esquivar)
        float holdTimer = 0f;
        while (holdTimer < windUpHoldDuration)
        {
            holdTimer += Time.deltaTime;
            RotateTowardPlayer();
            yield return null;
        }

        // FASE 1: DISPARO DOS PROJÉTEIS
        for (int i = 0; i < totalSpikes; i++)
        {
            spikeIsReturning[i] = false;
            spikeHasHitPlayer[i] = false;

            RotateTowardPlayer();

            Vector3 shootDir = (playerTransform.position - spikeTransforms[i].position);
            shootDir.y = 0f;
            if (shootDir == Vector3.zero) shootDir = transform.forward;
            spikeLaunchDirections[i] = shootDir.normalized;

            if (spikeTransforms[i] != null)
            {
                spikeTransforms[i].SetParent(null);
                spikeLastTrailPositions[i] = spikeTransforms[i].position;
                PlayShotSound(spikeTransforms[i].position);
            }
            StartCoroutine(FlySingleSpike(i));

            float waitTime = Random.Range(timeBetweenShotsMin, timeBetweenShotsMax);
            yield return new WaitForSeconds(waitTime);
        }

        // FASE DE PAUSA NO AR: Todos os projéteis ficam pairando/congelados no ar antes de iniciar o retorno!
        yield return new WaitForSeconds(Mathf.Max(0.1f, spikeAirHoverTime));

        // FASE DE RETORNO SEQUENCIAL ALEATÓRIO: Retorna 1 pino por vez em ordem aleatória!
        SetState(SwarmState.Reagrupando);

        List<int> randomOrder = new List<int>();
        for (int i = 0; i < totalSpikes; i++) randomOrder.Add(i);

        // Embaralhamento Aleatório (Fisher-Yates)
        for (int i = randomOrder.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            int temp = randomOrder[i];
            randomOrder[i] = randomOrder[rnd];
            randomOrder[rnd] = temp;
        }

        // Autoriza o retorno individual de cada pino com atraso sequencial
        foreach (int spikeIndex in randomOrder)
        {
            spikeIsReturning[spikeIndex] = true;
            if (spikeTransforms[spikeIndex] != null)
            {
                spikeLastTrailPositions[spikeIndex] = spikeTransforms[spikeIndex].position;
                PlayReturnShotSound(spikeTransforms[spikeIndex].position);
            }
            yield return new WaitForSeconds(Mathf.Max(0.05f, spikeReturnInterval));
        }
    }

    IEnumerator FlySingleSpike(int i)
    {
        float elapsed = 0f;
        float duration = maxProjectileDistance / projectileSpeed;

        while (elapsed < duration && !spikeIsReturning[i])
        {
            elapsed += Time.deltaTime;
            if (spikeTransforms[i] == null) yield break;

            Vector3 current = spikeTransforms[i].position;
            Vector3 next = current + (spikeLaunchDirections[i] * projectileSpeed * Time.deltaTime);
            spikeTransforms[i].position = next;
            spikeTransforms[i].rotation = Quaternion.LookRotation(spikeLaunchDirections[i]);

            // Gera o rastro elétrico 3D na traseira da ponta (apenas fora do perímetro seguro de 3.0m do Core)
            if (Vector3.Distance(spikeLastTrailPositions[i], next) >= 0.9f)
            {
                Vector3 rearOffset = -spikeLaunchDirections[i] * 0.7f;
                Vector3 trailStart = spikeLastTrailPositions[i] + rearOffset;
                Vector3 trailEnd = next + rearOffset;

                float distToCore = (coreTransform != null) ? Vector3.Distance(trailEnd, coreTransform.position) : Vector3.Distance(trailEnd, transform.position);
                if (distToCore >= 3.0f)
                {
                    ElectricTrailVFX.CreateTrailSegment(trailStart, trailEnd, trailDamagePerTick, trailDuration, zapSound);
                }
                spikeLastTrailPositions[i] = next;
            }

            Collider[] hits = Physics.OverlapSphere(next, 0.45f);
            foreach (Collider c in hits)
            {
                if (c.CompareTag("Player") && !spikeHasHitPlayer[i])
                {
                    spikeHasHitPlayer[i] = true;
                    playerHitsInCurrentAttack++;

                    PlayerHealth ph = c.GetComponent<PlayerHealth>();
                    if (ph != null)
                    {
                        // Dano Físico Direto do Projétil (Sem slow nem status)
                        int actualProjDamage = (projectileDamage > 0) ? projectileDamage : 10;
                        ph.TakeDamage(actualProjDamage, gameObject);
                    }
                }
            }

            yield return null;
        }
    }

    // ------------------------------------------------------------------------
    // ANIMAÇÃO DE SHURIKEN (WHEEL SPIN 360° — PIVÔ NO CENTRO EXATO DO CORE)
    // ------------------------------------------------------------------------

    public IEnumerator WheelSpinRoutine()
    {
        SetState(SwarmState.Disparando);
        rb.linearVelocity = Vector3.zero;
        ResetSpikesToHome();

        if (coreTransform != null) coreTransform.localRotation = Quaternion.identity;

        int totalSpikes = spikeTransforms.Length;
        float mult = Mathf.Max(1.0f, wheelSpinExpansionMultiplier);

        // Mede os vetores de cada ponta em relação ao centro do CORE em 3D
        Vector3 coreCenter = (coreTransform != null) ? coreTransform.position : transform.position;
        Vector3[] initialOffsetsFromCore = new Vector3[totalSpikes];
        Quaternion[] initialSpikeRotations = new Quaternion[totalSpikes];

        for (int i = 0; i < totalSpikes; i++)
        {
            if (spikeTransforms[i] != null)
            {
                initialOffsetsFromCore[i] = spikeTransforms[i].position - coreCenter;
                initialSpikeRotations[i] = spikeTransforms[i].rotation;
            }
        }

        // FASE 1: Expansão Radial ao redor do centro do CORE (1.0x -> mult)
        float expandTimer = 0f;
        float expandDuration = 0.35f;

        while (expandTimer < expandDuration)
        {
            expandTimer += Time.deltaTime;
            float t = expandTimer / expandDuration;

            if (coreTransform != null) coreTransform.localRotation = Quaternion.identity;
            Vector3 currentCorePos = (coreTransform != null) ? coreTransform.position : transform.position;

            for (int i = 0; i < totalSpikes; i++)
            {
                if (spikeTransforms[i] != null)
                {
                    float currentMult = Mathf.Lerp(1.0f, mult, t);
                    spikeTransforms[i].position = currentCorePos + (initialOffsetsFromCore[i] * currentMult);
                    spikeTransforms[i].rotation = initialSpikeRotations[i];
                }
            }
            yield return null;
        }

        // FASE 2: Giro Shuriken — AS 5 PONTAS GIRAM TENDO COMO PIVÔ O CENTRO DO CORE!
        float spinTimer = 0f;
        float duration = Mathf.Max(0.2f, wheelSpinDuration);
        float degreesPerSecond = wheelSpinSpeedDegreesPerSec > 0f ? wheelSpinSpeedDegreesPerSec : (360f * wheelSpinRotations) / duration;

        Vector3 axisVector = (coreTransform != null) ? coreTransform.forward : transform.forward;
        if (wheelSpinAxis == SpinAxis.LocalX) axisVector = (coreTransform != null) ? coreTransform.right : transform.right;
        else if (wheelSpinAxis == SpinAxis.LocalY) axisVector = (coreTransform != null) ? coreTransform.up : transform.up;

        float currentAngle = 0f;

        while (spinTimer < duration)
        {
            spinTimer += Time.deltaTime;
            currentAngle += degreesPerSecond * Time.deltaTime;

            if (coreTransform != null) coreTransform.localRotation = Quaternion.identity;
            Vector3 currentCorePos = (coreTransform != null) ? coreTransform.position : transform.position;

            Quaternion spinRotation = Quaternion.AngleAxis(currentAngle, axisVector);

            // Gira cada ponta tendo como referência o pivô 3D exato do CORE!
            for (int i = 0; i < totalSpikes; i++)
            {
                if (spikeTransforms[i] != null)
                {
                    Vector3 expandedOffset = initialOffsetsFromCore[i] * mult;
                    Vector3 rotatedOffset = spinRotation * expandedOffset;
                    spikeTransforms[i].position = currentCorePos + rotatedOffset;
                    spikeTransforms[i].rotation = spinRotation * initialSpikeRotations[i];
                }
            }

            yield return null;
        }

        // FASE 3: Retração limpa de volta para a pose original
        ResetSpikesToHome();
        SetState(SwarmState.FormaUnida);
        Debug.Log("[ESTRELA] 🥷 Wheel Spin Shuriken 360° em volta do pivô do CORE concluído!");
    }

    // Coroutine exclusiva do Wind-Up: Expansão Linear -> Pausa -> Retração Linear pelo MESMO Caminho
    public IEnumerator WindUpOnlyRoutine()
    {
        SetState(SwarmState.Disparando);
        rb.linearVelocity = Vector3.zero;
        int totalSpikes = spikeTransforms.Length;

        // FASE 1: Expansão Radial (1.0x -> 1.8x)
        float expandTimer = 0f;
        float expandDuration = 0.45f;

        while (expandTimer < expandDuration)
        {
            expandTimer += Time.deltaTime;
            float t = expandTimer / expandDuration;
            RotateTowardPlayer();

            for (int i = 0; i < totalSpikes; i++)
            {
                if (spikeTransforms[i] != null)
                {
                    spikeTransforms[i].localPosition = Vector3.Lerp(
                        spikeLocalPositionsHome[i],
                        spikeLocalPositionsHome[i] * 1.8f,
                        t);
                    spikeTransforms[i].localRotation = spikeLocalRotationsHome[i];
                }
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.35f);

        // FASE 2: Retração pelo MESMO Caminho (1.8x -> 1.0x)
        float contractTimer = 0f;
        float contractDuration = 0.45f;

        while (contractTimer < contractDuration)
        {
            contractTimer += Time.deltaTime;
            float t = contractTimer / contractDuration;
            RotateTowardPlayer();

            for (int i = 0; i < totalSpikes; i++)
            {
                if (spikeTransforms[i] != null)
                {
                    spikeTransforms[i].localPosition = Vector3.Lerp(
                        spikeLocalPositionsHome[i] * 1.8f,
                        spikeLocalPositionsHome[i],
                        t);
                    spikeTransforms[i].localRotation = spikeLocalRotationsHome[i];
                }
            }
            yield return null;
        }

        ResetSpikesToHome();
        SetState(SwarmState.FormaUnida);
    }

    private bool isRearmSpinning = false;

    void UpdateReagrupamento()
    {
        if (isRearmSpinning) return;

        int count = spikeTransforms.Length;
        int arrivedCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (spikeTransforms[i] == null) { arrivedCount++; continue; }

            // Se a ponta ainda não foi chamada de volta (retorno sequencial), ela paira no ar!
            if (!spikeIsReturning[i])
            {
                // Flutuação/vibração micro-elétrica no ar enquanto aguarda a sua vez
                Vector3 hoverMicroJitter = new Vector3(
                    Mathf.Sin(Time.time * 14f + i) * 0.04f,
                    Mathf.Cos(Time.time * 11f + i) * 0.04f,
                    0f);
                spikeTransforms[i].position += hoverMicroJitter * Time.deltaTime;
                continue;
            }

            // Posição expandida ao redor do CORE para a animação de giro de reencaixe
            Vector3 expandedHomeLocal = spikeLocalPositionsHome[i] * 1.6f;
            Vector3 targetWorldPos = coreTransform.TransformPoint(expandedHomeLocal);
            Quaternion targetWorldRot = coreTransform.rotation * spikeLocalRotationsHome[i];

            Vector3 currentPos = spikeTransforms[i].position;
            spikeTransforms[i].position = Vector3.MoveTowards(
                currentPos,
                targetWorldPos,
                returnSpeed * Time.deltaTime);

            spikeTransforms[i].rotation = Quaternion.Slerp(
                spikeTransforms[i].rotation,
                targetWorldRot,
                Time.deltaTime * 10f);

            // Gerar descarga elétrica 3D no ar diretamente atrás da ponta durante o voo de volta (apenas fora do perímetro seguro de 3.0m do Core)
            if (Vector3.Distance(spikeLastTrailPositions[i], spikeTransforms[i].position) >= 0.9f)
            {
                Vector3 returnDir = (targetWorldPos - currentPos).normalized;
                Vector3 rearOffset = -returnDir * 0.7f;
                Vector3 trailStart = spikeLastTrailPositions[i] + rearOffset;
                Vector3 trailEnd = spikeTransforms[i].position + rearOffset;

                float distToCore = (coreTransform != null) ? Vector3.Distance(trailEnd, coreTransform.position) : Vector3.Distance(trailEnd, transform.position);
                if (distToCore >= 3.0f)
                {
                    ElectricTrailVFX.CreateTrailSegment(trailStart, trailEnd, trailDamagePerTick, trailDuration);
                }
                spikeLastTrailPositions[i] = spikeTransforms[i].position;
            }

            if (Vector3.Distance(spikeTransforms[i].position, targetWorldPos) < 0.2f)
            {
                arrivedCount++;
            }
        }

        // Quando todas as 5 pontas chegam na órbita expandida -> Inicia o Giro de 360° e Encaixe!
        if (arrivedCount >= count && !isRearmSpinning)
        {
            StartCoroutine(RearmSpinRoutine());
        }
    }

    public IEnumerator RearmSpinRoutine()
    {
        isRearmSpinning = true;

        // Re-parenta todas as 5 pontas de volta ao CORE na posição expandida
        for (int i = 0; i < spikeTransforms.Length; i++)
        {
            if (spikeTransforms[i] != null)
            {
                spikeTransforms[i].SetParent(coreTransform);
                spikeTransforms[i].localPosition = spikeLocalPositionsHome[i] * 1.6f;
                spikeTransforms[i].localRotation = spikeLocalRotationsHome[i];
            }
        }

        // --- FASE 1: Rotação Vórtice 360° em torno do eixo X/Z do CORE ---
        float spinTimer = 0f;
        float spinDuration = 0.45f;
        Quaternion startCoreRot = coreTransform.localRotation;

        while (spinTimer < spinDuration)
        {
            spinTimer += Time.deltaTime;
            float progress = spinTimer / spinDuration;

            // Rotação dramática de 360° no eixo X/Z do CORE
            coreTransform.Rotate(Vector3.right * (360f / spinDuration) * Time.deltaTime, Space.Self);

            yield return null;
        }

        // --- FASE 2: Encaixe com Trava Magnética (Snap Back) ---
        float snapTimer = 0f;
        float snapDuration = 0.18f;

        while (snapTimer < snapDuration)
        {
            snapTimer += Time.deltaTime;
            float snapProgress = snapTimer / snapDuration;

            for (int i = 0; i < spikeTransforms.Length; i++)
            {
                if (spikeTransforms[i] != null)
                {
                    spikeTransforms[i].localPosition = Vector3.Lerp(
                        spikeLocalPositionsHome[i] * 1.6f,
                        spikeLocalPositionsHome[i],
                        snapProgress);
                }
            }
            yield return null;
        }

        // Trava final no lugar
        ResetSpikesToHome();
        isRearmSpinning = false;

        Debug.Log("[ESTRELA] ⚙️ Rearmagem concluída: Giro 360° + Trava Magnética!");
        SetState(SwarmState.FormaUnida);
    }

    Vector3 GetGroundPos(Vector3 pos)
    {
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out hit, 10f, groundMask))
        {
            return hit.point + Vector3.up * 0.05f; // Levemente acima do chão
        }
        return new Vector3(pos.x, (playerTransform != null ? playerTransform.position.y : pos.y), pos.z);
    }

    public void SetBuff(bool active)
    {
        if (active)
        {
            projectileSpeed *= 1.3f;
            maxChargeTime *= 0.7f;
            trailDuration += 1.0f;
        }
        else
        {
            projectileSpeed /= 1.3f;
            maxChargeTime /= 0.7f;
            trailDuration = Mathf.Max(2.5f, trailDuration - 1.0f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, electricPulseRadius);
    }

    // =============================================
    // SISTEMA DE ÁUDIO (Disparo & Choque / Zap)
    // =============================================

    private void PlayShotSound(Vector3 position)
    {
        if (shotSound == null) return;
        float pitch = Random.Range(0.95f, 1.05f);
        PlayClipAtPointWithPitch(shotSound, position, pitch, shotSoundVolume);
    }

    private void PlayReturnShotSound(Vector3 position)
    {
        AudioClip clipToPlay = (returnShotSound != null) ? returnShotSound : shotSound;
        if (clipToPlay == null)
        {
            Debug.LogWarning("[ESTRELA] Nenhum AudioClip atribuído em 'Return Shot Sound' nem em 'Shot Sound' no Inspector da Estrela!");
            return;
        }

        float pitchJitter = Random.Range(-0.04f, 0.04f);
        float finalPitch = Mathf.Clamp(returnShotPitch + pitchJitter, 0.1f, 3.0f);
        PlayClipAtPointWithPitch(clipToPlay, position, finalPitch, returnShotSoundVolume, useReturnShotReverb, returnShotReverbPreset);
    }

    private void PlayZapSound(Vector3 position)
    {
        if (zapSound == null) return;
        float pitch = Random.Range(0.9f, 1.1f);
        PlayClipAtPointWithPitch(zapSound, position, pitch, zapSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume, bool addReverb = false, AudioReverbPreset reverbPreset = AudioReverbPreset.Cave)
    {
        if (clip == null) return;
        GameObject audioObj = new GameObject("TempStarAudio");
        audioObj.transform.position = position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.pitch = pitch;
        aSource.volume = volume;
        aSource.spatialBlend = 0.5f; // Semi-3D para garantir audibilidade clara na cena
        aSource.minDistance = 8f;
        aSource.maxDistance = 50f;
        aSource.rolloffMode = AudioRolloffMode.Linear;

        if (addReverb)
        {
            AudioReverbFilter reverb = audioObj.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = reverbPreset;
        }

        aSource.Play();
        float safePitch = Mathf.Abs(pitch) > 0.01f ? Mathf.Abs(pitch) : 1f;
        float destroyDelay = (clip.length / safePitch) + (addReverb ? 1.5f : 0.1f);
        Destroy(audioObj, destroyDelay);
    }
}
