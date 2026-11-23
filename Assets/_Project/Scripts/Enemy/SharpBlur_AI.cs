using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DummyHealth))]
public class SharpBlur : MonoBehaviour
{
    private Transform playerTransform;
    private Rigidbody playerRb;
    private DummyHealth health;
    private Rigidbody rb;
    private Animator anim;

    [Header("Ativação & Perseguição")]
    public float activationDistance = 25f;
    public float chaseSpeed = 7.48f;

    [Header("Gatilhos de Ataque (Distâncias)")]
    [Tooltip("Distância máxima para iniciar o Dash e impedir que o player fuja")]
    public float dashTriggerDistance = 15f;
    [Tooltip("Distância curta para iniciar ataque corpo a corpo (alinhada com a hitbox frontal)")]
    public float meleeTriggerDistance = 2.5f;
    [Tooltip("Se o player correr além dessa distância durante o combate, Sharp Blur dá prioridade ao Dash para cortar a fuga")]
    public float playerEscapeDistance = 5.5f;
    [Tooltip("Tempo de recarga entre sequências de teleporte/dash")]
    public float dashCooldown = 3.4f;

    [Header("Configurações do Dash")]
    public float dashDistance = 11f;
    public float dashSpeed = 31f;
    public float pauseBetweenDashes = 0.16f;
    public float endSequenceRest = 0.5f;
    [Tooltip("Janela vulnerável aberta para o player punir o Sharp Blur após gastar os Dashes.")]
    public float dashPunishWindow = 1.3f;
    [Range(1, 4)] public int minDashes = 2;
    [Range(1, 5)] public int maxDashes = 3;
    [Tooltip("Chance de dar um dash longo que atravessa o player em vez de parar na frente dele.")]
    [Range(0f, 1f)] public float pierceDashChance = 0.35f;

    [Header("Combo de 3 Ataques (Attack1 -> Attack2 -> Attack3 360°)")]
    [Tooltip("Dano do 1º golpe (Attack1).")]
    public int meleeDamage = 10;
    public float attack1HitDelay = 0.54f;
    public float attack1FollowUpDelay = 0.22f;

    [Space(4)]
    public bool enableAttack2 = true;
    public int attack2Damage = 12;
    public float attack2HitDelay = 0.36f;
    public float attack2FollowUpDelay = 0.44f;

    [Space(4)]
    public bool enableAttack3 = true;
    [Tooltip("Dano do 1º contato do 3º ataque (girada 360° no Attack3).")]
    public int attack3Damage = 10;
    [Tooltip("Dano do 2º contato do 3º ataque (final da girada 360° no Attack3).")]
    public int attack3SecondHitDamage = 14;
    [Tooltip("Delay até o 1º contato/VFX do Attack3 (pico do 1º giro em ~0.63s a 1.05x).")]
    public float attack3HitDelay = 0.63f;
    [Tooltip("Intervalo entre o 1º e o 2º contato/VFX do Attack3 (2º giro em ~1.16s a 1.05x).")]
    public float attack3SecondHitDelay = 0.53f;
    public float attack3RecoveryDelay = 0.38f;
    [Tooltip("Raio mínimo da área 360° atingida pela girada do Attack3 (cresce proporcionalmente à escala do VFX).")]
    public float attack3SpinRadius = 2.75f;

    [Space(4)]
    [Tooltip("Pausa curta entre Hit 1 e Hit 2 quando o combo encadeia (mantém o flow sem pausa morta).")]
    public float comboChain1To2Delay = 0.04f;
    [Tooltip("Pausa curta entre Hit 2 e Ataque 3 quando o combo encadeia.")]
    public float comboChain2To3Delay = 0.10f;
    [Tooltip("Segundos do agachamento inicial estático do Attack3 pulados ao encadear do combo/dash (VFX 3 e 4 mantêm o frame exato calibrado).")]
    [Range(0f, 0.55f)] public float attack3ComboIntroSkip = 0.26f;
    [Tooltip("Multiplicador de velocidade do Attack3 quando usado no combo comum (0.80 = 20% mais lento que o pós-dash).")]
    [Range(0.4f, 1.5f)] public float comboAttack3SpeedMultiplier = 0.80f;
    [Tooltip("Avanço curto entre os golpes do combo para colar no player se ele tentar desviar andando.")]
    public float comboStepSpeed = 6.12f;

    [Header("Hitbox do Ataque (Sincronizada com o VFX)")]
    [Tooltip("Distância mínima à frente do inimigo onde a hitbox de garra se forma")]
    public float meleeHitForwardOffset = 1.15f;
    [Tooltip("Altura da hitbox de ataque em relação ao pivô")]
    public float meleeHitHeightOffset = 1.0f;
    [Tooltip("Raio mínimo da hitbox frontal do golpe corpo a corpo (expande conforme a escala do VFX)")]
    public float meleeHitRadius = 1.55f;
    [Tooltip("Raio de colisão que causa dano ao atravessar durante o Dash")]
    public float dashHitRadius = 1.05f;
    [Tooltip("Margem radial do corpo usada para evitar atravessar paredes ao perseguir/dar dash")]
    public float bodyCollisionRadius = 0.45f;

    [Header("Refinamentos Inteligencia")]
    [Tooltip("Delay entre o aparecimento do Dash Hologram e o Sharp Blur de fato teleportar/dar o dash.")]
    public float anticipationTime = 0.26f;
    [Range(0f, 1f)] public float leadPrediction = 0.62f;
    [Tooltip("Se ativo, logo após o combo corpo a corpo ele emenda um Dash para reposicionar e pressionar.")]
    public bool followUpDashAfterMelee = true;

    [Header("Dano")]
    public int dashDamage = 15;

    [Header("Efeitos Visuais")]
    [Tooltip("Coloque o Prefab vazio com o script DashHologram aqui")]
    public GameObject hologramPrefab;
    [Tooltip("Coloque o Material transparente/fantasma aqui")]
    public Material hologramMaterial;
    [Range(0.1f, 1f)] public float hologramAlpha = 0.45f;

    [Header("Trails de Ataque (Garras)")]
    public bool enableAttackTrails = true;
    public Color trailStartColor = new Color(0.82f, 0.46f, 0.76f, 0.66f);
    public Color trailEndColor = new Color(0.50f, 0.22f, 0.56f, 0.0f);
    public float meleeTrailWidth = 0.36f;
    public float meleeTrailTime = 0.28f;
    [Tooltip("Deslocamento ao longo do osso da mão (+Y desce do punho em direção às garras/dedos).")]
    public float clawTrailHandOffset = 18f;

    [System.Serializable]
    public struct SlashVfxPose
    {
        [Tooltip("Posição em metros relativa ao Sharp Blur (X=lado, Y=altura, Z=frente).")]
        public Vector3 localOffset;
        [Tooltip("Rotação em graus relativa ao Sharp Blur (X=inclinação, Y=giro horizontal, Z=ângulo diagonal do corte).")]
        public Vector3 localEulerAngles;
        [Tooltip("Tamanho individual desse slash.")]
        public float scale;
        [Tooltip("Espelha horizontalmente para inverter o sentido do corte.")]
        public bool flipX;
        [Tooltip("Opcional: arraste um objeto filho aqui para usar a posição/rotação dele direto no Editor.")]
        public Transform customAnchor;
    }

    [Header("VFX de Slash nos Ataques")]
    [Tooltip("Prefab de VFX disparado a cada corte do combo corpo a corpo (ex: SharpBlur_Slash_VFX).")]
    public GameObject meleeSlashVfxPrefab;
    public Color slashVfxTint = new Color(0.76f, 0.42f, 0.72f, 0.45f);
    public float slashVfxLifetime = 1.1f;
    public SlashVfxPose hit1SlashPose = new SlashVfxPose
    {
        localOffset = new Vector3(0f, 1.45f, 0.15f),
        localEulerAngles = new Vector3(0f, -45f, 32f),
        scale = 0.90f,
        flipX = false
    };
    public SlashVfxPose hit2SlashPose = new SlashVfxPose
    {
        localOffset = new Vector3(0f, 1.45f, 0.15f),
        localEulerAngles = new Vector3(0f, 45f, -28f),
        scale = 0.90f,
        flipX = true
    };
    public SlashVfxPose dashSpinSlashPose = new SlashVfxPose
    {
        localOffset = new Vector3(0f, 1.35f, 0.15f),
        localEulerAngles = new Vector3(0f, -35f, 12f),
        scale = 0.95f,
        flipX = false
    };
    [Tooltip("Pose do 2º VFX de slash no 3º ataque (segundo contato da girada 360°).")]
    public SlashVfxPose dashSpinSlashPose2 = new SlashVfxPose
    {
        localOffset = new Vector3(0f, 1.25f, 0.25f),
        localEulerAngles = new Vector3(0f, 35f, -14f),
        scale = 1.05f,
        flipX = true
    };

    [Header("Áudio — Movimentação (Teleporte / Dash)")]
    [Tooltip("Som da distorção de teleporte / dash do SharpBlur")]
    public AudioClip teleportSound;
    [Tooltip("Volume do som de teleporte")]
    [Range(0f, 1f)]
    public float teleportSoundVolume = 0.8f;

    [Header("Áudio — Ataque (Golpe)")]
    [Tooltip("Som do ataque (swoosh / corte de cristal)")]
    public AudioClip meleeAttackSound;
    [Tooltip("Volume do som de ataque")]
    [Range(0f, 1f)]
    public float meleeAttackSoundVolume = 0.9f;

    [Header("Áudio — Dano Recebido (Hit)")]
    [Tooltip("Sons reproduzidos quando o SharpBlur leva dano (golpe do player). NÃO colocar sons de ataque aqui.")]
    public AudioClip[] hitSounds;
    [Tooltip("Volume dos sons de dano recebido (hit)")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 0.7f;

    [Header("Áudio — Morte (Die)")]
    [Tooltip("Sons reproduzidos quando o SharpBlur morre.")]
    public AudioClip[] dieSounds;
    [Tooltip("Volume dos sons de morte")]
    [Range(0f, 1f)]
    public float dieSoundVolume = 0.8f;

    [Header("Alinhamento do Modelo")]
    [Tooltip("Mantém os quadris (Hips) centralizados no pivô/colisor para impedir que o mesh salte para trás durante animações como Double_Combo_Attack.")]
    public bool lockMeshXZToRoot = true;

    [Header("Buff do Crystal Tuner")]
    [Tooltip("Multiplicador de velocidade de perseguição quando conectado ao Crystal Tuner.")]
    public float tunerChaseSpeedMultiplier = 1.12f;
    [Tooltip("Multiplicador de cooldown do dash quando conectado ao Crystal Tuner.")]
    public float tunerDashCooldownMultiplier = 0.80f;
    [Tooltip("Dashes extras adicionados à sequência quando conectado ao Crystal Tuner.")]
    public int tunerBonusDashes = 1;
    [Tooltip("Cor dos slashes quando potencializado pelo Crystal Tuner (ciano-violeta cristalino).")]
    public Color tunerSlashTint = new Color(0.36f, 0.78f, 0.96f, 0.48f);
    public Color tunerTrailStartColor = new Color(0.38f, 0.85f, 1.0f, 0.66f);
    public Color tunerTrailEndColor = new Color(0.45f, 0.35f, 0.90f, 0.0f);

    [Header("Melhorias do Sharp Blur Elite")]
    [Tooltip("Delay (s) do Corte Fantasma/Eco disparado pelo holograma deixado pelo Elite.")]
    public float eliteEchoDelay = 0.45f;
    [Tooltip("Dano do Corte Fantasma/Eco do holograma Elite.")]
    public int eliteEchoDamage = 12;
    [Tooltip("Cor translúcida fantasmagórica do Corte Eco Elite.")]
    public Color eliteEchoSlashTint = new Color(0.70f, 0.46f, 1.0f, 0.40f);
    [Tooltip("Chance de dash de atravessada (pierce) quando Elite.")]
    [Range(0f, 1f)] public float elitePierceDashChance = 0.65f;
    [Tooltip("Multiplicador do tempo de antecipação do dash quando Elite (< 1 = mais rápido).")]
    public float eliteAnticipationMultiplier = 0.85f;
    [Tooltip("Janela de punição pós-dash quando Elite (mais justa, porém ainda punível).")]
    public float eliteDashPunishWindow = 1.0f;
    [Tooltip("Multiplicador da escala de slash e hitbox quando Elite.")]
    public float eliteSlashScaleMultiplier = 1.15f;
    [Tooltip("Multiplicador de dano corpo a corpo e dash quando Elite.")]
    public float eliteDamageMultiplier = 1.20f;

    private enum State { Idle, Chasing, Dashing, MeleeAttacking, Resting }
    private State currentState = State.Idle;
    private float nextDashAllowedTime = 0f;
    private bool isBuffed = false;

    private bool IsElite => health != null && health.isElite;

    private Transform hipsTransform;
    private TrailRenderer leftClawTrail;
    private TrailRenderer rightClawTrail;
    private bool wantsChaseMove;
    private bool wantsComboStepMove;
    private Vector3 chaseTargetPos;
    private bool isDashingActive;
    private bool hitDealtThisDash;
    private bool isDead;
    private bool hasActivated;
    private bool registradoNoBestiario;
    private int obstacleLayerMask;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<DummyHealth>();
        anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.applyRootMotion = false;
        hipsTransform = FindChildByName(transform, "Hips");
        SetupAttackTrails();

        obstacleLayerMask = BuildObstacleLayerMask();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerRb = player.GetComponent<Rigidbody>();
        }

        // Snap de segurança no chão para evitar clipagem no piso da sala
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
        {
            Vector3 pos = transform.position;
            pos.y = navHit.position.y;
            transform.position = pos;
        }

        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.mass = 50f;
        rb.constraints |= RigidbodyConstraints.FreezePositionY;

        if (health != null)
        {
            health.onDamageTaken += HandleDamageTaken;
            health.onDeath += HandleDeath;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (meleeSlashVfxPrefab == null)
        {
            meleeSlashVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/vfx_0/Prefabs/Slash VFX.prefab");
        }
    }
#endif

    void OnEnable()
    {
        // Recupera o estado caso o script tenha sido pausado/reabilitado pelo HitStop do DummyHealth ao levar dano.
        if (!isDead && hasActivated)
        {
            wantsChaseMove = false;
            wantsComboStepMove = false;
            isDashingActive = false;
            currentState = State.Chasing;
            if (anim != null)
            {
                anim.SetInteger("comboStep", 0);
                anim.SetBool("isChasing", true);
            }
        }
    }

    void OnDisable()
    {
        wantsChaseMove = false;
        wantsComboStepMove = false;
        isDashingActive = false;
        if (anim != null) anim.SetInteger("comboStep", 0);
        StopAllTrails(clear: true);
        StopAllCoroutines();
        if (!isDead && hasActivated)
        {
            currentState = State.Chasing;
        }
    }

    private int BuildObstacleLayerMask()
    {
        int ignoreBits = (1 << gameObject.layer);
        string[] ignoredLayers = { "Player", "Enemy", "Weapon", "Item", "Ignore Raycast", "DeadBody", "TransparentFX", "UI" };
        foreach (string layerName in ignoredLayers)
        {
            int idx = LayerMask.NameToLayer(layerName);
            if (idx >= 0) ignoreBits |= (1 << idx);
        }
        return ~ignoreBits;
    }

    private void HandleDamageTaken(int damage, GameObject attacker)
    {
        if (!isDead && currentState == State.Idle)
        {
            hasActivated = true;
            currentState = State.Chasing;
            if (anim != null) anim.SetBool("isChasing", true);
        }
        PlayHitSound();
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        wantsChaseMove = false;
        wantsComboStepMove = false;
        isDashingActive = false;
        StopAllTrails(clear: true);
        StopAllCoroutines();
        PlayDieSound();
    }

    private void SetupAttackTrails()
    {
        if (!enableAttackTrails) return;

        Transform leftHand = FindChildByName(transform, "LeftHand");
        Transform rightHand = FindChildByName(transform, "RightHand");

        Vector3 handTipOffset = new Vector3(0f, clawTrailHandOffset, 0f);
        leftClawTrail = CreateTrailRenderer(leftHand != null ? leftHand : transform, "SharpBlur_LeftClawTrail",
            leftHand != null ? handTipOffset : new Vector3(-0.45f, 0.95f, 0.35f), meleeTrailWidth, meleeTrailTime);
        rightClawTrail = CreateTrailRenderer(rightHand != null ? rightHand : transform, "SharpBlur_RightClawTrail",
            rightHand != null ? handTipOffset : new Vector3(0.45f, 0.95f, 0.35f), meleeTrailWidth, meleeTrailTime);
    }

    private TrailRenderer CreateTrailRenderer(Transform parent, string trailName, Vector3 localOffset, float width, float time)
    {
        GameObject go = new GameObject(trailName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;

        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.time = time;
        tr.startWidth = width;
        tr.endWidth = 0.02f;
        tr.minVertexDistance = 0.04f;
        tr.autodestruct = false;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            tr.material = new Material(shader);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trailStartColor, 0f), new GradientColorKey(trailEndColor, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(trailStartColor.a, 0f),
                new GradientAlphaKey(trailStartColor.a * 0.78f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        tr.colorGradient = gradient;
        tr.emitting = false;
        return tr;
    }

    private void SetMeleeTrailsEmitting(bool emitting, bool clear = false)
    {
        SetSingleTrail(leftClawTrail, emitting, clear);
        SetSingleTrail(rightClawTrail, emitting, clear);
    }

    private void RefreshTrailColors()
    {
        Color startCol = isBuffed ? tunerTrailStartColor : trailStartColor;
        Color endCol = isBuffed ? tunerTrailEndColor : trailEndColor;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startCol, 0f), new GradientColorKey(endCol, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(startCol.a, 0f),
                new GradientAlphaKey(startCol.a * 0.78f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        if (leftClawTrail != null) leftClawTrail.colorGradient = gradient;
        if (rightClawTrail != null) rightClawTrail.colorGradient = gradient;
    }

    /// <summary>
    /// Buff aplicado pelo CrystalTuner: +12% velocidade, -20% cooldown de dash, +1 dash na sequência e VFX/trails ciano-violeta.
    /// </summary>
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            chaseSpeed *= tunerChaseSpeedMultiplier;
            dashCooldown *= tunerDashCooldownMultiplier;
            RefreshTrailColors();
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            chaseSpeed /= tunerChaseSpeedMultiplier;
            dashCooldown /= tunerDashCooldownMultiplier;
            RefreshTrailColors();
        }
    }

    private int ScaleDamageForElite(int baseDamage)
    {
        return IsElite ? Mathf.RoundToInt(baseDamage * eliteDamageMultiplier) : baseDamage;
    }

    private float GetEffectiveSlashScale(float baseScale)
    {
        float s = Mathf.Max(0.05f, baseScale);
        return IsElite ? s * eliteSlashScaleMultiplier : s;
    }

    private float GetEffectiveAnticipationTime()
    {
        return IsElite ? anticipationTime * Mathf.Clamp(eliteAnticipationMultiplier, 0.5f, 1f) : anticipationTime;
    }

    private float GetEffectivePierceChance()
    {
        return IsElite ? Mathf.Max(pierceDashChance, elitePierceDashChance) : pierceDashChance;
    }

    private Color GetActiveSlashTint()
    {
        return isBuffed ? tunerSlashTint : slashVfxTint;
    }

    private void StopAllTrails(bool clear)
    {
        SetMeleeTrailsEmitting(false, clear);
    }

    private static void SetSingleTrail(TrailRenderer tr, bool emitting, bool clear)
    {
        if (tr == null) return;
        if (clear) tr.Clear();
        tr.emitting = emitting;
    }

    private void PlayHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0) return;
        AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.9f, 1.1f), hitSoundVolume);
    }

    private void PlayDieSound()
    {
        if (dieSounds == null || dieSounds.Length == 0) return;
        AudioClip clip = dieSounds[Random.Range(0, dieSounds.Length)];
        if (clip != null)
            PlayClipAtPointWithPitch(clip, transform.position, Random.Range(0.9f, 1.1f), dieSoundVolume);
    }

    void Update()
    {
        if (isDead || (health != null && health.CurrentHealth <= 0))
        {
            wantsChaseMove = false;
            return;
        }

        if (playerTransform == null)
        {
            wantsChaseMove = false;
            return;
        }

        switch (currentState)
        {
            case State.Idle:
                wantsChaseMove = false;
                HandleIdle();
                break;
            case State.Chasing:
                HandleChasing();
                break;
            default:
                wantsChaseMove = false;
                break;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (!isDead && currentState == State.Chasing && wantsChaseMove)
        {
            Vector3 current = rb.position;
            Vector3 desired = Vector3.MoveTowards(current, chaseTargetPos, chaseSpeed * Time.fixedDeltaTime);
            desired.y = current.y;
            Vector3 safePos = ClampSafeStep(current, desired, allowSlide: true);
            rb.MovePosition(safePos);
        }
        else if (!isDead && wantsComboStepMove && playerTransform != null)
        {
            float dist = FlatDistance(rb.position, playerTransform.position);
            if (dist > 1.10f)
            {
                Vector3 current = rb.position;
                Vector3 desired = Vector3.MoveTowards(current, playerTransform.position, comboStepSpeed * Time.fixedDeltaTime);
                desired.y = current.y;
                Vector3 safePos = ClampSafeStep(current, desired, allowSlide: true);
                rb.MovePosition(safePos);
            }
        }
    }

    void LateUpdate()
    {
        LockHipsToRootXZ();
    }

    private void LockHipsToRootXZ()
    {
        if (!lockMeshXZToRoot || hipsTransform == null) return;
        Vector3 worldHips = hipsTransform.position;
        worldHips.x = transform.position.x;
        worldHips.z = transform.position.z;
        hipsTransform.position = worldHips;
    }

    private static Transform FindChildByName(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    void HandleIdle()
    {
        if (Vector3.Distance(transform.position, playerTransform.position) <= activationDistance)
        {
            hasActivated = true;
            currentState = State.Chasing;
            if (anim != null) anim.SetBool("isChasing", true);

            if (!registradoNoBestiario)
            {
                registradoNoBestiario = true;
                EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                if (id != null && BestiarioManager.instancia != null)
                    BestiarioManager.instancia.Registrar(id);
            }
        }
    }

    void HandleChasing()
    {
        float distToPlayer = FlatDistance(transform.position, playerTransform.position);
        LookAtPosition(playerTransform.position, 18f);

        bool canDash = Time.time >= nextDashAllowedTime;
        // Se o player tentar correr muito para longe, permite disparar o Dash mais cedo para não deixá-lo fugir fácil
        bool playerTryingToEscape = distToPlayer >= playerEscapeDistance && Time.time >= nextDashAllowedTime - (dashCooldown * 0.35f);

        // Perto do player: sempre golpeia imediatamente (nunca dá dash à queima-roupa).
        if (distToPlayer <= meleeTriggerDistance)
        {
            wantsChaseMove = false;
            StartCoroutine(MeleeAttackRoutine());
        }
        else if (distToPlayer <= dashTriggerDistance && (canDash || playerTryingToEscape))
        {
            wantsChaseMove = false;
            StartCoroutine(DashRoutine());
        }
        else
        {
            wantsChaseMove = true;
            Vector3 targetPos = playerTransform.position;
            if (playerRb != null && distToPlayer > meleeTriggerDistance * 1.15f)
            {
                Vector3 pVel = playerRb.linearVelocity;
                pVel.y = 0f;
                if (pVel.sqrMagnitude > 0.5f)
                {
                    float leadTime = Mathf.Clamp((distToPlayer / Mathf.Max(1f, chaseSpeed)) * 0.45f, 0.08f, 0.35f);
                    targetPos += pVel * leadTime;
                }
            }
            chaseTargetPos = targetPos;
        }
    }

    // ROTINA: Ataque de perto (Combo Duplo de Garras -> opcionalmente emenda Dash para continuar pressionando)
    IEnumerator MeleeAttackRoutine() =>
        DummyHealth.PauseAttackWhileStunned(this, MeleeAttackRoutineSteps());

    private IEnumerator MeleeAttackRoutineSteps()
    {
        currentState = State.MeleeAttacking;
        wantsChaseMove = false;

        if (anim != null) anim.SetBool("isChasing", false);

        yield return StartCoroutine(ExecuteMeleeCombo());

        // Se o Dash estiver pronto após os 2 ataques melee, dá 1–2 dashes de flanco/travessia para emendar novos combos
        if (!isDead && followUpDashAfterMelee && playerTransform != null && Time.time >= nextDashAllowedTime)
        {
            yield return StartCoroutine(DashRoutine(fromMeleeFollowUp: true));
            yield break;
        }

        if (!isDead)
        {
            currentState = State.Resting;
            yield return new WaitForSeconds(Mathf.Max(0.15f, endSequenceRest * 0.35f));
        }

        if (!isDead)
        {
            currentState = State.Chasing;
            if (anim != null) anim.SetBool("isChasing", true);
        }
    }

    // Executa combo escalonado de 3 ataques: Attack1 -> Attack2 -> Attack3 (girada 360°)
    IEnumerator ExecuteMeleeCombo()
    {
        // --- ATAQUE 1 ---
        if (anim != null)
        {
            anim.SetInteger("comboStep", 1);
            anim.CrossFadeInFixedTime("Attack1", 0.06f, 0, 0f);
        }

        float hit1Wait = Mathf.Max(0.08f, attack1HitDelay);
        float trailPreEnable = Mathf.Min(hit1Wait * 0.45f, 0.22f);

        wantsComboStepMove = playerTransform != null && FlatDistance(transform.position, playerTransform.position) <= meleeTriggerDistance + 2.2f;
        yield return StartCoroutine(FacePlayerFor(hit1Wait - trailPreEnable, 26f));
        if (enableAttackTrails && !isDead) SetMeleeTrailsEmitting(true, clear: true);
        yield return StartCoroutine(FacePlayerFor(trailPreEnable, 26f));
        wantsComboStepMove = false;

        if (!isDead)
        {
            PlayMeleeAttackSound();
            SpawnSlashVfx(hit1SlashPose);
            TryDealSlashDamage(hit1SlashPose, meleeDamage);
        }

        // Decide imediatamente após o impacto do Hit 1 se vai encadear o Hit 2 (sem pausa morta)
        bool continueCombo = !isDead && enableAttack2 &&
            playerTransform != null && FlatDistance(transform.position, playerTransform.position) <= meleeTriggerDistance + 2.2f;

        if (!continueCombo)
        {
            yield return StartCoroutine(FacePlayerFor(Mathf.Max(0.08f, attack1FollowUpDelay), 20f));
        }
        else
        {
            if (comboChain1To2Delay > 0.005f)
                yield return StartCoroutine(FacePlayerFor(comboChain1To2Delay, 26f));

            if (anim != null)
            {
                anim.SetInteger("comboStep", 2);
                anim.CrossFadeInFixedTime("Attack2", 0.06f, 0, 0f);
            }

            wantsComboStepMove = playerTransform != null && FlatDistance(transform.position, playerTransform.position) <= meleeTriggerDistance + 2.2f;
            yield return StartCoroutine(FacePlayerFor(Mathf.Max(0.08f, attack2HitDelay), 26f));
            wantsComboStepMove = false;

            if (!isDead)
            {
                PlayMeleeAttackSound();
                SpawnSlashVfx(hit2SlashPose);
                TryDealSlashDamage(hit2SlashPose, attack2Damage);
            }

            // Decide imediatamente após o impacto do Hit 2 se vai encadear o Ataque 3 (girada 360°)
            bool continueThird = !isDead && enableAttack3 &&
                playerTransform != null && FlatDistance(transform.position, playerTransform.position) <= attack3SpinRadius + 2.2f;

            if (!continueThird)
            {
                yield return StartCoroutine(FacePlayerFor(Mathf.Max(0.08f, attack2FollowUpDelay), 18f));
            }
            else
            {
                if (comboChain2To3Delay > 0.005f)
                    yield return StartCoroutine(FacePlayerFor(comboChain2To3Delay, 24f));

                yield return StartCoroutine(ExecuteAttack3Routine(dealDamage: true, useComboStepMove: true, skipIntroSeconds: attack3ComboIntroSkip, speedMultiplier: comboAttack3SpeedMultiplier));
            }
        }

        wantsComboStepMove = false;
        SetMeleeTrailsEmitting(false);
        if (anim != null) anim.SetInteger("comboStep", 0);
    }

    /// <summary>
    /// Executa a sequência completa do Attack3 (Skill_03 360°) com 2 janelas de contato, VFX e dano.
    /// Quando skipIntroSeconds > 0, pula o início agachado parado preservando o frame exato dos VFX 3 e 4.
    /// </summary>
    private IEnumerator ExecuteAttack3Routine(bool dealDamage, bool useComboStepMove, bool skipFirstHitDamage = false, float skipIntroSeconds = 0f, float speedMultiplier = 1f)
    {
        float safeMult = Mathf.Max(0.25f, speedMultiplier);
        float effectiveSkip = Mathf.Clamp(skipIntroSeconds, 0f, Mathf.Max(0f, attack3HitDelay - 0.12f));

        if (anim != null)
        {
            anim.SetFloat("attack3SpeedMult", safeMult);
            anim.SetInteger("comboStep", 3);
            float startOffset = effectiveSkip > 0f ? (effectiveSkip / safeMult) : 0f;
            anim.CrossFadeInFixedTime("Attack3", 0.08f, 0, startOffset);
        }

        bool canStepMove = useComboStepMove && playerTransform != null &&
            FlatDistance(transform.position, playerTransform.position) <= attack3SpinRadius + 2.2f;

        wantsComboStepMove = canStepMove;
        yield return StartCoroutine(FacePlayerFor(Mathf.Max(0.08f, (attack3HitDelay - effectiveSkip) / safeMult), 24f));
        wantsComboStepMove = false;

        if (!isDead)
        {
            PlayMeleeAttackSound();
            SpawnSlashVfx(dashSpinSlashPose);
            if (dealDamage && !skipFirstHitDamage)
                TryDealSpinDamage(dashSpinSlashPose, attack3Damage);
        }

        if (!isDead)
        {
            wantsComboStepMove = canStepMove && playerTransform != null &&
                FlatDistance(transform.position, playerTransform.position) <= attack3SpinRadius + 1.2f;
            yield return new WaitForSeconds(Mathf.Max(0.08f, attack3SecondHitDelay / safeMult));
            wantsComboStepMove = false;
        }

        if (!isDead)
        {
            PlayMeleeAttackSound();
            SpawnSlashVfx(dashSpinSlashPose2);
            if (dealDamage)
                TryDealSpinDamage(dashSpinSlashPose2, attack3SecondHitDamage);

            // Elite: deixa um holograma eco no ponto do Attack3 que dispara um corte fantasma atrasado
            if (IsElite && dealDamage)
            {
                ScheduleEliteEchoSlash(transform.position, transform.rotation, dashSpinSlashPose);
            }
        }

        if (!isDead)
        {
            yield return new WaitForSeconds(Mathf.Max(0.15f, attack3RecoveryDelay / safeMult));
        }

        if (anim != null) anim.SetFloat("attack3SpeedMult", 1f);
    }

    // Sequência de 1 a 3 Dashes híbridos (interceptação/travessia/aproximação) seguida de Combo e janela de punição
    IEnumerator DashRoutine(bool fromMeleeFollowUp = false) =>
        DummyHealth.PauseAttackWhileStunned(this, DashRoutineSteps(fromMeleeFollowUp));

    private IEnumerator DashRoutineSteps(bool fromMeleeFollowUp = false)
    {
        currentState = State.Dashing;
        wantsChaseMove = false;

        if (anim != null) anim.SetBool("isChasing", false);

        int bonusDashes = isBuffed ? Mathf.Max(0, tunerBonusDashes) : 0;
        int effMinDashes = minDashes + bonusDashes;
        int effMaxDashes = Mathf.Max(effMinDashes, maxDashes + bonusDashes);
        float effAnticipation = GetEffectiveAnticipationTime();

        int numberOfDashes = fromMeleeFollowUp
            ? Random.Range(1, Mathf.Max(2, effMaxDashes))
            : Random.Range(effMinDashes, effMaxDashes + 1);

        for (int i = 0; i < numberOfDashes; i++)
        {
            if (isDead || playerTransform == null) yield break;

            bool isLastDash = (i == numberOfDashes - 1);
            Vector3 dashOriginPos = transform.position;
            Quaternion dashOriginRot = transform.rotation;

            Vector3 targetDashPos = ComputeHybridDashTarget(isLastDash, out Vector3 dashDirection, forceFlankOrPierce: fromMeleeFollowUp && (!isLastDash || numberOfDashes == 1));
            Quaternion dashRot = Quaternion.LookRotation(dashDirection);

            if (hologramPrefab != null && hologramMaterial != null)
            {
                LockHipsToRootXZ();
                GameObject holo = Instantiate(hologramPrefab);
                DashHologram holoScript = holo.GetComponent<DashHologram>();
                if (holoScript != null)
                {
                    holoScript.Init(transform, targetDashPos, dashRot, effAnticipation + 0.05f, hologramMaterial, hologramAlpha);
                }
            }

            if (anim != null) anim.CrossFadeInFixedTime("Dash", 0.06f, 0, 0f);

            yield return StartCoroutine(FaceDirectionFor(dashDirection, effAnticipation, 22f));

            // Elite: no último dash da sequência, deixa um eco fantasma na origem que desfere um corte atrasado
            if (IsElite && isLastDash)
            {
                ScheduleEliteEchoSlash(dashOriginPos, dashOriginRot, hit1SlashPose);
            }

            yield return StartCoroutine(ExecuteSingleDash(dashDirection, targetDashPos, isLastDash));
            if (!isLastDash)
                yield return new WaitForSeconds(pauseBetweenDashes);
        }

        // Se Attack3 estiver desativado e finalizou os dashes perto do player, emenda no Combo Melee
        if (!isDead && !enableAttack3 && playerTransform != null && FlatDistance(transform.position, playerTransform.position) <= meleeTriggerDistance + 1.0f)
        {
            currentState = State.MeleeAttacking;
            yield return StartCoroutine(ExecuteMeleeCombo());
        }

        nextDashAllowedTime = Time.time + dashCooldown;

        // Janela clara para o player punir o Sharp Blur após uma sequência de Dash
        if (!isDead)
        {
            currentState = State.Resting;
            if (anim != null) anim.SetBool("isChasing", false);
            float punishWindow = IsElite ? Mathf.Min(dashPunishWindow, eliteDashPunishWindow) : dashPunishWindow;
            float restTime = Mathf.Max(0.25f, punishWindow);
            yield return new WaitForSeconds(restTime);
        }

        if (!isDead)
        {
            currentState = State.Chasing;
            if (anim != null) anim.SetBool("isChasing", true);
        }
    }

    private Vector3 ComputeHybridDashTarget(bool isLastDash, out Vector3 dashDirection, bool forceFlankOrPierce = false)
    {
        Vector3 playerPos = playerTransform.position;
        Vector3 playerVel = playerRb != null ? playerRb.linearVelocity : Vector3.zero;
        playerVel.y = 0f;
        bool playerMoving = playerVel.sqrMagnitude > 0.6f;

        Vector3 predictedPos = GetPredictedPlayerPosition();
        Vector3 toTarget = predictedPos - transform.position;
        toTarget.y = 0f;
        dashDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;

        Vector3 targetBase = predictedPos;
        targetBase.y = transform.position.y;

        Vector3 rawTargetPos;
        if (!isLastDash || forceFlankOrPierce)
        {
            // Dashes intermediários variam entre: cortar na frente do player em fuga, atravessar longo ou flanquear
            float roll = Random.value;
            if (playerMoving && roll < 0.40f)
            {
                // Intercepta à frente de onde o player está correndo
                Vector3 interceptAhead = playerVel.normalized * Random.Range(1.4f, 3.0f);
                rawTargetPos = targetBase + interceptAhead;
            }
            else if (roll < 0.72f)
            {
                // Dash longo atravessando o player
                float overshoot = Random.Range(2.4f, Mathf.Max(3.4f, dashDistance * 0.65f));
                rawTargetPos = targetBase + dashDirection * overshoot;
            }
            else
            {
                // Flanco diagonal com distância variável
                Vector3 side = Vector3.Cross(Vector3.up, dashDirection) * (Random.value < 0.5f ? 1f : -1f);
                float forwardDist = Mathf.Min(toTarget.magnitude * Random.Range(0.55f, 0.90f), dashDistance * 0.7f);
                rawTargetPos = transform.position + (dashDirection * forwardDist) + (side * Random.Range(1.8f, 3.2f));
            }
        }
        else
        {
            // Último dash: às vezes atravessa longo, na maioria fecha bem na frente (ou corta na frente se estiver correndo)
            if (Random.value < GetEffectivePierceChance() * 0.65f && toTarget.magnitude <= dashDistance)
            {
                float overshoot = Random.Range(2.0f, 3.8f);
                rawTargetPos = targetBase + dashDirection * overshoot;
            }
            else
            {
                float idealStopOffset = Random.Range(1.1f, Mathf.Max(1.35f, meleeTriggerDistance * 0.8f));
                Vector3 leadAhead = playerMoving ? playerVel.normalized * Random.Range(0.5f, 1.4f) : Vector3.zero;
                rawTargetPos = (targetBase + leadAhead) - dashDirection * idealStopOffset;
            }
        }

        // Limita o passo máximo deste dash a até dashDistance com variação orgânica
        Vector3 delta = rawTargetPos - transform.position;
        delta.y = 0f;
        float maxThisDash = dashDistance * Random.Range(0.85f, 1.15f);
        if (delta.magnitude > maxThisDash)
            rawTargetPos = transform.position + delta.normalized * maxThisDash;

        Vector3 dirAfter = rawTargetPos - transform.position;
        dirAfter.y = 0f;
        if (dirAfter.sqrMagnitude > 0.01f)
            dashDirection = dirAfter.normalized;

        return ClampSafePosition(transform.position, rawTargetPos);
    }

    IEnumerator ExecuteSingleDash(Vector3 direction, Vector3 targetDashPos, bool triggerEndSpinSlash) =>
        DummyHealth.PauseAttackWhileStunned(this, ExecuteSingleDashSteps(direction, targetDashPos, triggerEndSpinSlash));

    private IEnumerator ExecuteSingleDashSteps(Vector3 direction, Vector3 targetDashPos, bool triggerEndSpinSlash)
    {
        if (direction == Vector3.zero) direction = transform.forward;
        transform.rotation = Quaternion.LookRotation(direction);

        PlayTeleportSound();

        Vector3 startPos = rb != null ? rb.position : transform.position;
        targetDashPos = ClampSafePosition(startPos, targetDashPos);

        float distance = Vector3.Distance(startPos, targetDashPos);
        float duration = Mathf.Max(0.05f, distance / dashSpeed);
        float elapsed = 0f;

        isDashingActive = true;
        hitDealtThisDash = false;
        if (enableAttackTrails) SetMeleeTrailsEmitting(true, clear: true);

        while (elapsed < duration && !isDead)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPos = Vector3.Lerp(startPos, targetDashPos, t);

            if (rb != null) rb.MovePosition(nextPos);
            else transform.position = nextPos;

            TryDealDashDamage();
        }

        if (!isDead)
        {
            if (rb != null) rb.MovePosition(targetDashPos);
            else transform.position = targetDashPos;
            TryDealDashDamage();
        }

        SetMeleeTrailsEmitting(false);
        isDashingActive = false;

        // No final do último dash, sempre executa a girada 360° completa (Attack3)
        // com os 2 VFX calibrados (dashSpinSlashPose e dashSpinSlashPose2) e 2 hits.
        if (!isDead && triggerEndSpinSlash && enableAttack3 && playerTransform != null)
        {
            yield return StartCoroutine(FacePlayerFor(0.06f, 30f));
            if (!isDead)
            {
                if (enableAttackTrails) SetMeleeTrailsEmitting(true, clear: true);
                yield return StartCoroutine(ExecuteAttack3Routine(dealDamage: true, useComboStepMove: true, skipFirstHitDamage: false, skipIntroSeconds: attack3ComboIntroSkip));
                SetMeleeTrailsEmitting(false);
                if (anim != null) anim.SetInteger("comboStep", 0);
            }
        }
    }

    IEnumerator FacePlayerFor(float duration, float rotationSpeed)
    {
        float elapsed = 0f;
        while (elapsed < duration && !isDead)
        {
            if (playerTransform != null)
                LookAtPosition(playerTransform.position, rotationSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator FaceDirectionFor(Vector3 direction, float duration, float rotationSpeed)
    {
        float elapsed = 0f;
        direction.y = 0f;
        while (elapsed < duration && !isDead)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotationSpeed * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void GetSlashHitbox(SlashVfxPose pose, bool isSpin, out Vector3 center, out float radius)
    {
        float s = Mathf.Max(0.5f, GetEffectiveSlashScale(pose.scale));
        Vector3 flatOffset = isSpin
            ? new Vector3(pose.localOffset.x * 0.5f, 0f, pose.localOffset.z * 0.65f)
            : new Vector3(pose.localOffset.x * 0.45f, 0f, Mathf.Max(meleeHitForwardOffset, pose.localOffset.z));

        center = transform.position + (transform.rotation * flatOffset) + (Vector3.up * meleeHitHeightOffset);
        radius = isSpin
            ? Mathf.Max(attack3SpinRadius * (IsElite ? eliteSlashScaleMultiplier : 1f), 1.75f * s)
            : Mathf.Max(meleeHitRadius * (IsElite ? eliteSlashScaleMultiplier : 1f), 1.18f * s);
    }

    private void TryDealSlashDamage(SlashVfxPose pose, int damage)
    {
        GetSlashHitbox(pose, isSpin: false, out Vector3 center, out float radius);
        DealOverlapDamage(center, radius, ScaleDamageForElite(damage));
    }

    private void TryDealSpinDamage(SlashVfxPose pose, int damage)
    {
        GetSlashHitbox(pose, isSpin: true, out Vector3 center, out float radius);
        DealOverlapDamage(center, radius, ScaleDamageForElite(damage));
    }

    private void DealOverlapDamage(Vector3 center, float radius, int damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider col in hits)
        {
            if (col == null) continue;
            PlayerHealth ph = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damage, gameObject);
                return;
            }
        }
    }

    private static readonly Dictionary<(Material, Color32), Material> TintedSlashMaterialCache = new Dictionary<(Material, Color32), Material>();

    private void SpawnSlashVfx(SlashVfxPose pose)
    {
        SpawnSlashVfxAt(transform.position, transform.rotation, pose, GetActiveSlashTint(), GetEffectiveSlashScale(pose.scale));
    }

    private void SpawnSlashVfxAt(Vector3 originPos, Quaternion originRot, SlashVfxPose pose, Color tint, float scale)
    {
        if (meleeSlashVfxPrefab == null) return;

        Vector3 spawnPos;
        Quaternion spawnRot;
        if (pose.customAnchor != null && originPos == transform.position)
        {
            spawnPos = pose.customAnchor.position;
            spawnRot = pose.customAnchor.rotation * Quaternion.Euler(pose.localEulerAngles);
        }
        else
        {
            spawnPos = originPos + (originRot * pose.localOffset);
            spawnRot = originRot * Quaternion.Euler(pose.localEulerAngles);
        }

        GameObject vfx = Instantiate(meleeSlashVfxPrefab, spawnPos, spawnRot);
        float s = Mathf.Max(0.05f, scale);
        vfx.transform.localScale = new Vector3(pose.flipX ? -s : s, s, s);

        foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            Color currentStart = main.startColor.color;
            main.startColor = currentStart * tint;

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null && psr.sharedMaterial != null)
            {
                psr.sharedMaterial = GetOrCreateTintedMaterial(psr.sharedMaterial, tint);
            }

            ps.Play(true);
        }

        Destroy(vfx, Mathf.Max(0.3f, slashVfxLifetime));
    }

    private void ScheduleEliteEchoSlash(Vector3 echoPos, Quaternion echoRot, SlashVfxPose pose)
    {
        if (isDead) return;
        StartCoroutine(EliteEchoSlashRoutine(echoPos, echoRot, pose));
    }

    private IEnumerator EliteEchoSlashRoutine(Vector3 echoPos, Quaternion echoRot, SlashVfxPose pose)
    {
        float delay = Mathf.Max(0.20f, eliteEchoDelay);

        // Congela um holograma fantasma na posição para telegrafar o corte eco atrasado
        if (hologramPrefab != null && hologramMaterial != null)
        {
            LockHipsToRootXZ();
            GameObject holo = Instantiate(hologramPrefab);
            DashHologram holoScript = holo.GetComponent<DashHologram>();
            if (holoScript != null)
            {
                holoScript.Init(transform, echoPos, echoRot, delay + 0.08f, hologramMaterial, hologramAlpha * 0.9f);
            }
        }

        yield return new WaitForSeconds(delay);

        if (isDead) yield break;

        Color echoTint = isBuffed ? new Color(tunerSlashTint.r, tunerSlashTint.g, tunerSlashTint.b, 0.40f) : eliteEchoSlashTint;
        float echoScale = GetEffectiveSlashScale(pose.scale);
        SpawnSlashVfxAt(echoPos, echoRot, pose, echoTint, echoScale);

        if (meleeAttackSound != null)
            PlayMeleeAttackSound();

        Vector3 flatOffset = new Vector3(pose.localOffset.x * 0.45f, 0f, Mathf.Max(meleeHitForwardOffset, pose.localOffset.z));
        Vector3 hitCenter = echoPos + (echoRot * flatOffset) + (Vector3.up * meleeHitHeightOffset);
        float hitRadius = Mathf.Max(meleeHitRadius * eliteSlashScaleMultiplier, 1.25f * echoScale);
        DealOverlapDamage(hitCenter, hitRadius, eliteEchoDamage);
    }

    private static Material GetOrCreateTintedMaterial(Material template, Color tint)
    {
        if (template == null) return null;
        var key = (template, (Color32)tint);
        if (TintedSlashMaterialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Material mat = new Material(template);
        Color subtleHdr = new Color(tint.r * 1.15f, tint.g * 1.15f, tint.b * 1.15f, tint.a);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", subtleHdr);
        if (mat.HasProperty("_MainColor")) mat.SetColor("_MainColor", tint);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        TintedSlashMaterialCache[key] = mat;
        return mat;
    }

    private void TryDealDashDamage()
    {
        if (!isDashingActive || hitDealtThisDash) return;

        Vector3 center = transform.position + Vector3.up * meleeHitHeightOffset;
        Collider[] hits = Physics.OverlapSphere(center, dashHitRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider col in hits)
        {
            if (col == null) continue;
            if (TryApplyDashHit(col.gameObject)) return;
        }
    }

    private bool TryApplyDashHit(GameObject target)
    {
        if (!isDashingActive || hitDealtThisDash || target == null) return false;

        PlayerHealth ph = target.GetComponent<PlayerHealth>() ?? target.GetComponentInParent<PlayerHealth>();
        if (ph == null) return false;

        hitDealtThisDash = true;
        PlayMeleeAttackSound();
        ph.TakeDamage(ScaleDamageForElite(dashDamage), gameObject);
        return true;
    }

    Vector3 GetPredictedPlayerPosition()
    {
        Vector3 playerPos = playerTransform.position;
        if (playerRb != null && leadPrediction > 0f)
        {
            float distance = Vector3.Distance(transform.position, playerPos);
            float timeToTarget = distance / Mathf.Max(1f, dashSpeed);
            Vector3 lead = playerRb.linearVelocity * timeToTarget * leadPrediction;
            lead.y = 0f;
            playerPos += lead;
        }
        return playerPos;
    }

    private Vector3 ClampSafeStep(Vector3 from, Vector3 to, bool allowSlide)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist <= 0.0001f) return from;

        Vector3 dir = delta / dist;
        Vector3 origin = from + Vector3.up * 0.9f;

        if (Physics.SphereCast(origin, bodyCollisionRadius, dir, out RaycastHit hit, dist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
        {
            float allowed = Mathf.Max(0f, hit.distance - 0.05f);
            Vector3 clamped = from + dir * allowed;

            if (allowSlide)
            {
                Vector3 remaining = delta * (1f - allowed / dist);
                Vector3 slide = Vector3.ProjectOnPlane(remaining, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude > 0.00001f)
                {
                    Vector3 slideOrigin = clamped + Vector3.up * 0.9f;
                    float slideDist = slide.magnitude;
                    if (!Physics.SphereCast(slideOrigin, bodyCollisionRadius, slide / slideDist, out _, slideDist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        clamped += slide;
                    }
                }
            }

            clamped.y = from.y;
            return KeepOnNavMeshIfPresent(from, clamped);
        }

        Vector3 candidate = new Vector3(to.x, from.y, to.z);
        return KeepOnNavMeshIfPresent(from, candidate);
    }

    private Vector3 ClampSafePosition(Vector3 from, Vector3 target)
    {
        target.y = from.y;
        Vector3 delta = target - from;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist <= 0.001f) return from;

        Vector3 dir = delta / dist;
        Vector3 origin = from + Vector3.up * 0.9f;
        Vector3 clamped = target;

        if (Physics.SphereCast(origin, bodyCollisionRadius, dir, out RaycastHit hit, dist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Max(0f, hit.distance - 0.1f);
            clamped = from + dir * safeDist;
            clamped.y = from.y;
        }

        return KeepOnNavMeshIfPresent(from, clamped);
    }

    private Vector3 KeepOnNavMeshIfPresent(Vector3 from, Vector3 candidate)
    {
        if (NavMesh.Raycast(from, candidate, out NavMeshHit navHit, NavMesh.AllAreas))
        {
            // Só limita pelo NavMesh se o ponto de origem realmente estiver em uma NavMesh válida.
            if (NavMesh.SamplePosition(from, out _, 0.6f, NavMesh.AllAreas))
            {
                Vector3 res = navHit.position;
                res.y = from.y;
                return res;
            }
        }
        return candidate;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDashHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyDashHit(collision.gameObject);
    }

    void LookAtPosition(Vector3 target, float rotationSpeed)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotationSpeed * Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, dashTriggerDistance);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, meleeTriggerDistance);

        GetSlashHitbox(hit1SlashPose, isSpin: false, out Vector3 hit1Center, out float hit1Rad);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.22f);
        Gizmos.DrawSphere(hit1Center, hit1Rad);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(hit1Center, hit1Rad);

        GetSlashHitbox(hit2SlashPose, isSpin: false, out Vector3 hit2Center, out float hit2Rad);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(hit2Center, hit2Rad);

        Gizmos.color = new Color(0.85f, 0.2f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * meleeHitHeightOffset, dashHitRadius);

        GetSlashHitbox(dashSpinSlashPose, isSpin: true, out Vector3 spin1Center, out float spin1Rad);
        Gizmos.color = new Color(0.95f, 0.45f, 0.85f, 0.55f);
        Gizmos.DrawWireSphere(spin1Center, spin1Rad);

        GetSlashHitbox(dashSpinSlashPose2, isSpin: true, out Vector3 spin2Center, out float spin2Rad);
        Gizmos.color = new Color(1f, 0.55f, 0.75f, 0.55f);
        Gizmos.DrawWireSphere(spin2Center, spin2Rad);

        DrawSlashPoseGizmo(hit1SlashPose, Color.green);
        DrawSlashPoseGizmo(hit2SlashPose, Color.cyan);
        DrawSlashPoseGizmo(dashSpinSlashPose, Color.magenta);
        DrawSlashPoseGizmo(dashSpinSlashPose2, new Color(1f, 0.55f, 0.75f));
    }

    private void DrawSlashPoseGizmo(SlashVfxPose pose, Color col)
    {
        Vector3 pos = pose.customAnchor != null
            ? pose.customAnchor.position
            : transform.position + (transform.rotation * pose.localOffset);
        Quaternion rot = pose.customAnchor != null
            ? pose.customAnchor.rotation * Quaternion.Euler(pose.localEulerAngles)
            : transform.rotation * Quaternion.Euler(pose.localEulerAngles);

        Gizmos.color = col;
        Gizmos.DrawSphere(pos, 0.08f);
        Gizmos.DrawLine(pos, pos + rot * Vector3.forward * Mathf.Max(0.4f, pose.scale));
        Gizmos.DrawLine(pos - rot * Vector3.right * 0.35f, pos + rot * Vector3.right * 0.35f);
    }

    // =============================================
    // SISTEMA DE ÁUDIO (Teleporte/Dash & Ataques)
    // =============================================

    private void PlayTeleportSound()
    {
        if (teleportSound == null) return;
        float pitch = Random.Range(0.9f, 1.1f);
        PlayClipAtPointWithPitch(teleportSound, transform.position, pitch, teleportSoundVolume);
    }

    private void PlayMeleeAttackSound()
    {
        if (meleeAttackSound == null) return;
        float pitch = Random.Range(0.95f, 1.05f);
        PlayClipAtPointWithPitch(meleeAttackSound, transform.position, pitch, meleeAttackSoundVolume);
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;
        float effectiveVolume = volume * GlobalAudioManager.SFXVolumeMultiplier;
        if (effectiveVolume <= 0.001f) return;

        GameObject audioObj = new GameObject("TempSharpBlurAudio");
        audioObj.transform.position = position;
        AudioSource aSource = audioObj.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.pitch = pitch;
        aSource.volume = effectiveVolume;
        aSource.spatialBlend = 0.2f; // Mais presente na câmera top-down/isométrica
        aSource.minDistance = 12f;
        aSource.maxDistance = 60f;
        aSource.rolloffMode = AudioRolloffMode.Linear;
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
            aSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
        aSource.Play();
        float safePitch = Mathf.Abs(pitch) > 0.01f ? Mathf.Abs(pitch) : 1f;
        Destroy(audioObj, (clip.length / safePitch) + 0.1f);
    }
}
