using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Configurações")]
    public int maxHealth = 100;
    public int maxArmor = 60;
    public int defaultMaxArmor = 60;
    public int currentHealth { get; private set; }
    private int currentArmor;
    public int CurrentArmor => currentArmor;
    private int cursedHealthLost = 0;

    [Header("Armor Regen")]
    public float armorRegenRate = 10f;
    [Tooltip("Tempo em segundos sem tomar dano para a armadura começar a se regenerar")]
    public float shieldRegenDelay = 3.5f;
    
    [Header("Invulnerability")]
    public bool isInvulnerable = false;
    
    [Header("Componentes")]
    public PlayerM playerMovement;
    public PrimaryAttackKnife playerAttack;
    public Animator playerAnimator;
    public ScreenFader screenFader;
    private Rigidbody rb;
    private PlayerAttributesDefensive playerAttributes;

    [Header("UI (Interface)")]
    public Image healthFillImage; // A barra verde (Filled)
    public Image gooBarImage;     // A barra de gosma (Filled)
    public Image armorBarImage;   // A barra de armadura (Filled)
    
    [Header("Modular Armor Plates (PlayerCanvasReal)")]
    [Tooltip("Capacidade de armadura por placa (padrão: 60 AP, 5 placas = 300 AP)")]
    public float armorPerPlate = 60f;
    [Tooltip("Outlines dos 5 escudos do PlayerCanvasReal (0 = eterno, 1..4 = extras)")]
    public GameObject[] armorOutlines = new GameObject[5];
    [Tooltip("Fills dos 5 escudos do PlayerCanvasReal")]
    public Image[] armorFills = new Image[5];
    
    public TextMeshProUGUI armorText;
    public TextMeshProUGUI percentageText; // O texto de %

    [Header("Mercador UI (Overlay & Sangue Escuro)")]
    public Image mercadorHealthFillImage; // Mercador_HealthFill (Filled)
    public GameObject mercadorBarObj;      // MercadorBar (Moldura com chapéu/sombras)
    private Coroutine mercadorAnimationCoroutine;
    private Vector3 mercadorBarOriginalScale = Vector3.one;
    private Vector3 healthBarOriginalPos = Vector3.zero;
    private Transform healthBarContainerTransform;

    /// <summary>
    /// Evento estático desacoplado disparado quando o jogador recebe dano.
    /// Parâmetros: (int danoRecebido, GameObject atacante)
    /// </summary>
    public static event System.Action<int, GameObject> OnPlayerDamaged;

    public bool hasPactCorrupted = false;
    public Color darkPactHealthColor = new Color(0.38f, 0.04f, 0.08f, 1.0f); // Sangue Escuro Obscuro (#610a14)
    private Color originalHealthFillColor = Color.white;
    private bool hasSavedOriginalColor = false;

    [HideInInspector] public float damageMultiplier = 1.0f;
    [HideInInspector] public float damageTakenMultiplier = 1.0f;
    [Header("Efeitos dos Pactos do Mercador")]
    [HideInInspector] public bool canHeal = true;
    [HideInInspector] public bool hasDoubleLoot = false;
    [HideInInspector] public bool hasGreedPact = false;
    [HideInInspector] public bool hasChaosSymphony = false;
    [HideInInspector] public bool hasVampirism = false;
    [HideInInspector] public bool hasNecrosis = false;
    [HideInInspector] public float lastKillTime = 0f;
    [HideInInspector] public bool hasSelfDamageOnAttack = false;
    [HideInInspector] public bool isKnockbackImmune = false;
    [HideInInspector] public float abilityCooldownMultiplier = 1.0f;
    [HideInInspector] public bool enemiesBuffed = false;

    [Header("Stun")]
    public bool isStunned { get; private set; } = false;
    private float stunImmunityTimer = 0f;
    private float stunImmunityDuration = 2f; // Imunidade após stun
    private StunVFX stunVFX;

    public bool isDead { get; private set; } = false;
    private int playerLayer;
    private bool diedFallingForward = false;
    private float sentinelLegCooldownEndTime = 0f;

    private float armorRegenAccumulator = 0f;
    private float healthRegenAccumulator = 0f;

    [Header("Heartbeat SFX (Sem Armadura)")]
    [Tooltip("Som de coração batendo quando o player está sem armadura. Arraste o AudioClip HeartBeat aqui.")]
    public AudioClip heartbeatClip;

    [Header("Áudio de Cura (Recuperação de Vida)")]
    [Tooltip("Som executado ao recuperar vida. Arraste confirmation_002.ogg ou seu áudio de cura aqui.")]
    public AudioClip healSound;
    [Range(0f, 1f)]
    public float healSoundVolume = 0.9f;

    [Tooltip("Volume do som de heartbeat (0.0 a 1.0)")]
    [Range(0f, 1f)]
    public float heartbeatVolume = 0.7f;

    private AudioSource heartbeatSource;
    private bool isHeartbeatPlaying = false;

    public static bool shouldPlayWakeUpAnimation = true;

    void Start()
    {
        isInvulnerable = false;
        playerLayer = gameObject.layer;
        rb = GetComponent<Rigidbody>();

        FindUIReferences();
        // Reseta essência da run ao renascer na base
        PlayerEssence essence = GetComponent<PlayerEssence>() ?? GetComponentInChildren<PlayerEssence>() ?? Object.FindFirstObjectByType<PlayerEssence>();
        if (essence != null)
        {
            essence.currentEssence = 0;
            essence.onEssenceChanged?.Invoke(0);
        }

        // Fecha e esconde texto de cochicho do Mercador
        if (MerchantPopupController.Instance != null)
        {
            MerchantPopupController.Instance.EsconderPopup();
        }

        FullHeal();
        
        // Buscar PlayerAttributesDefensive
        playerAttributes = GetComponent<PlayerAttributesDefensive>();
        if (playerAttributes == null)
        {
            Debug.LogWarning("PlayerHealth: PlayerAttributesDefensive não encontrado! Atributos defensivos não funcionarão.");
        }

        // Garante que o Sistema de Cura do Traje do Eptinho esteja ativo
        if (GetComponent<SuitHealingSystem>() == null && FindFirstObjectByType<SuitHealingSystem>() == null)
        {
            gameObject.AddComponent<SuitHealingSystem>();
        }

        // Setup do AudioSource para heartbeat (loop contínuo)
        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.clip = heartbeatClip;
        heartbeatSource.loop = true;
        heartbeatSource.playOnAwake = false;
        heartbeatSource.volume = heartbeatVolume;
        heartbeatSource.spatialBlend = 0f; // Som 2D (direto nos ouvidos do player)

        if (SceneManager.GetActiveScene().name == "Base")
        {
            TriggerBaseRespawn();
        }
    }

    public void TriggerBaseRespawn()
    {
        FindUIReferences();

        // Reseta Armadura acumulada durante a run de volta ao valor padrão da Base (1 placa de 60)
        maxArmor = defaultMaxArmor;
        currentArmor = defaultMaxArmor;

        // Limpa debuffs de solo e lentidão
        PlayerDebuffs debuffs = GetComponent<PlayerDebuffs>() ?? GetComponentInChildren<PlayerDebuffs>();
        if (debuffs != null) debuffs.ResetAllDebuffs();
        if (playerMovement != null) playerMovement.debuffSpeedMultiplier = 1.0f;

        // Reseta e destrava sistema de cura do traje
        SuitHealingSystem.Instance?.ResetOnDeathOrRespawn();

        // Reseta essência da run ao renascer na base
        PlayerEssence essence = GetComponent<PlayerEssence>() ?? GetComponentInChildren<PlayerEssence>() ?? Object.FindFirstObjectByType<PlayerEssence>();
        if (essence != null)
        {
            essence.currentEssence = 0;
            essence.onEssenceChanged?.Invoke(0);
        }

        // Fecha e esconde texto de cochicho do Mercador
        if (MerchantPopupController.Instance != null)
        {
            MerchantPopupController.Instance.EsconderPopup();
        }

        FullHeal();
        isDead = false;
        gameObject.layer = playerLayer;

        MerchantHallucinationEffect.ResetImmediate();

        // Reset rotação do pai para posição limpa e em pé
        transform.rotation = Quaternion.identity;

        // Garante que o player esteja perfeitamente alinhado na superfície do chão
        SnapToGround();

        // Unequip weapon ao renascer na base (player acorda com mãos livres e armas voltam ao expositor)
        Player_WeaponManager weaponManager = GetComponent<Player_WeaponManager>() ?? GetComponentInChildren<Player_WeaponManager>();
        if (weaponManager != null)
        {
            weaponManager.UnequipCurrentWeapon();
        }
        else if (playerAttack != null)
        {
            playerAttack.hasWeapon = false;
        }

        UpdateArmorBar();

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>();
        }

        // Restaura o Animator para estado Idle em pé e sem animações de acordar
        if (playerAnimator != null)
        {
            playerAnimator.ResetTrigger("Revive1");
            playerAnimator.ResetTrigger("Revive2");
            playerAnimator.ResetTrigger("DeathForward");
            playerAnimator.ResetTrigger("DeathBackward");
            playerAnimator.Play("Idle Default State", 0, 0f);
            playerAnimator.Update(0f);
        }

        if (shouldPlayWakeUpAnimation)
        {
            shouldPlayWakeUpAnimation = false;

            // Bloqueia movimentação durante o acordar (piscar de olhos)
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerAttack != null) playerAttack.enabled = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }

            // Inicia o efeito de abrir os olhos
            PlayerWakeUpEffect.TriggerWakeUp(this, () =>
            {
                UnlockPlayer();
            }, heartbeatClip);
        }
        else
        {
            UnlockPlayer();
        }
    }

    private void SnapToGround()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y;
            transform.position = pos;
        }
    }

    public void HandleReviveCompletion()
    {
        UnlockPlayer();
    }

    public void UnlockPlayer()
    {
        isDead = false;
        isInvulnerable = false;
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerAttack != null) playerAttack.enabled = true;
        gameObject.layer = playerLayer; 
        
        // Destrava a física para o jogo
        if (rb != null) 
        { 
            rb.isKinematic = false; 
            rb.detectCollisions = true; 
            rb.WakeUp(); 
        }
    }

    public void FindUIReferences()
    {
        try
        {
            GameObject healthObj = GameObject.Find("Health_Fill");
            if (healthObj != null) healthFillImage = healthObj.GetComponent<Image>();
            
            GameObject gooObj = GameObject.Find("Goo_Fill");
            if (gooObj != null) gooBarImage = gooObj.GetComponent<Image>();

            FindArmorSlotReferences();

            GameObject textArmor = GameObject.Find("ArmorText");
            if (textArmor != null) armorText = textArmor.GetComponent<TextMeshProUGUI>();
            
            GameObject textPercent = GameObject.Find("Text_Percentage");
            if (textPercent != null) percentageText = textPercent.GetComponent<TextMeshProUGUI>();

            // Busca os elementos do Mercador mesmo se estiverem inativos no HealthBar_Container
            GameObject healthContainer = GameObject.Find("HealthBar_Container");
            if (healthContainer != null)
            {
                healthBarContainerTransform = healthContainer.transform;
                healthBarOriginalPos = healthBarContainerTransform.localPosition;

                Transform mFill = null;
                Transform mBar = null;
                foreach (Transform t in healthContainer.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Mercador_HealthFill") mFill = t;
                    else if (t.name == "MercadorBar") mBar = t;
                }

                if (mFill != null) mercadorHealthFillImage = mFill.GetComponent<Image>();
                if (mBar != null)
                {
                    mercadorBarObj = mBar.gameObject;
                    mercadorBarOriginalScale = mBar.localScale;
                    if (mercadorBarOriginalScale == Vector3.zero) mercadorBarOriginalScale = Vector3.one;
                }
            }

            if (playerMovement == null) playerMovement = GetComponent<PlayerM>();
            if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
            if (playerAttack == null) playerAttack = GetComponent<PrimaryAttackKnife>();
        }
        catch (System.Exception) { }
        
        UpdateHealthBar();
        UpdateArmorBar();
    }

    public void ResetPlayerState()
    {
        isDead = false;
        isInvulnerable = false;
        // Reseta essência da run ao renascer na base
        PlayerEssence essence = GetComponent<PlayerEssence>() ?? GetComponentInChildren<PlayerEssence>() ?? Object.FindFirstObjectByType<PlayerEssence>();
        if (essence != null)
        {
            essence.currentEssence = 0;
            essence.onEssenceChanged?.Invoke(0);
        }

        // Fecha e esconde texto de cochicho do Mercador
        if (MerchantPopupController.Instance != null)
        {
            MerchantPopupController.Instance.EsconderPopup();
        }

        FullHeal(); 
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerAttack != null) playerAttack.enabled = true;
        gameObject.layer = playerLayer; 
        
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; }
        if (playerAnimator != null) { playerAnimator.Rebind(); playerAnimator.Update(0f); }
    }


    public void TakeCursedDamage(int amount)
    {
        if (isDead) return;
        int actualCost = Mathf.Min(amount, currentHealth);
        currentHealth -= actualCost;
        cursedHealthLost += actualCost;
        if (currentHealth <= 0) Die();
        UpdateHealthBar();
    }

    /// <summary>
    /// Aplica dano direto de Sacrifício à Vida Atual do jogador (sem alterar a Vida Máxima).
    /// </summary>
    public void TakeSacrificeDamage(int amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Max(1, currentHealth - amount);
        TriggerPlayerRedFlash();
        UpdateHealthBar();
        Debug.Log($"🩸 [SACRIFÍCIO] Dano de Pacto aplicado! Vida Atual: {currentHealth}/{maxHealth}");
    }
    
    void Die()
    {
        isDead = true;
        isStunned = false;
        if (stunVFX != null) stunVFX.HideStun();
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;
        gameObject.layer = LayerMask.NameToLayer("DeadBody");

        // Limpa o inventário de run (descarta itens comuns, mantém recursos de base)
        SaveManager.instance?.OnPlayerDied(this.gameObject);

        // Reseta todos os atributos acumulados durante a run ao morrer
        ResetAttributesOnDeath();
        
        // Trava física ao morrer também
        if (rb != null) 
        { 
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true; 
        }
        
        if (playerAnimator != null)
        {
            if (Random.value > 0.5f) { diedFallingForward = true; playerAnimator.SetTrigger("DeathForward"); }
            else { diedFallingForward = false; playerAnimator.SetTrigger("DeathBackward"); }
        }

        StartCoroutine(RespawnSequence());
    }

    private void ResetAttributesOnDeath()
    {
        // Reseta Armadura acumulada durante a run de volta ao valor padrão da Base
        maxArmor = defaultMaxArmor;
        currentArmor = defaultMaxArmor;

        // Limpa debuffs de solo e lentidão
        PlayerDebuffs debuffs = GetComponent<PlayerDebuffs>() ?? GetComponentInChildren<PlayerDebuffs>();
        if (debuffs != null) debuffs.ResetAllDebuffs();
        if (playerMovement != null) playerMovement.debuffSpeedMultiplier = 1.0f;

        // Unequip weapon para que as armas voltem ao expositor do rack
        Player_WeaponManager weaponManager = GetComponent<Player_WeaponManager>() ?? GetComponentInChildren<Player_WeaponManager>();
        if (weaponManager != null)
        {
            weaponManager.UnequipCurrentWeapon();
        }

        // Reseta sistema de cura do traje para não travar
        SuitHealingSystem.Instance?.ResetOnDeathOrRespawn();

        PlayerAttributesDefensive defStats = GetComponent<PlayerAttributesDefensive>() ?? GetComponentInChildren<PlayerAttributesDefensive>();
        if (defStats != null) defStats.ResetToDefaults();

        PlayerAttributesOffensive offStats = GetComponent<PlayerAttributesOffensive>() ?? GetComponentInChildren<PlayerAttributesOffensive>();
        if (offStats != null) offStats.ResetToDefaults();

        InfusionManager infusion = GetComponent<InfusionManager>() ?? GetComponentInChildren<InfusionManager>();
        if (infusion != null)
        {
            infusion.ResetRunInflation();
            if (infusion.infusedItems != null) infusion.infusedItems.Clear();
        }

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.ReapplyAllEquippedEffects();
        }

        if (MerchantUIController.HasInstance)
        {
            MerchantUIController.Instance.ResetPactState();
        }

        MerchantHallucinationEffect.ResetImmediate();

        // Reseta essência da run ao renascer na base
        PlayerEssence essence = GetComponent<PlayerEssence>() ?? GetComponentInChildren<PlayerEssence>() ?? Object.FindFirstObjectByType<PlayerEssence>();
        if (essence != null)
        {
            essence.currentEssence = 0;
            essence.onEssenceChanged?.Invoke(0);
        }

        // Fecha e esconde texto de cochicho do Mercador
        if (MerchantPopupController.Instance != null)
        {
            MerchantPopupController.Instance.EsconderPopup();
        }

        FullHeal();
        UpdateArmorBar();

        Debug.Log("[PLAYER HEALTH] Todos os atributos de run, infusões, maldições e armaduras foram resetados após a morte.");
    }

    IEnumerator RespawnSequence()
    {
        // 1. Aguarda a animação de morte completa do player caindo (2.0 segundos)
        yield return new WaitForSeconds(2.0f);

        // 2. Rastreamento do local da morte & Exibição da Tela de Morte ("VOCÊ MORREU")
        if (RunStatsManager.Instance != null)
        {
            string stageStr = RunManager.instance != null
                ? $"Nível {RunManager.instance.currentLevel} — Sala {RunManager.instance.currentRoomNumber}"
                : "Desconhecido";
            if (RunManager.instance != null && RunManager.instance.isEndlessMode)
            {
                stageStr += " (Endless)";
            }
            RunStatsManager.Instance.deathStage = stageStr;
            RunStatsManager.Instance.StopRunTracking();
        }

        shouldPlayWakeUpAnimation = true;

        if (DeathScreenUI.Instance != null)
        {
            DeathScreenUI.Instance.ShowDeathScreen();
        }
    }


    public void SetPactCorrupted(bool corrupted)
    {
        hasPactCorrupted = corrupted;
        if (corrupted)
        {
            TriggerMercadorPactAnimation();
        }
        else
        {
            ResetMercadorPactUI();
        }
        UpdateHealthBar();
        Debug.Log($"🩸 [BLOOD PACT] Barra de Vida alterada para Sangue Escuro Corrompido: {corrupted}");
    }

    /// <summary>
    /// Dispara a animação dramática das garras sombrias e sangue corrompido do Mercador na barra de vida.
    /// </summary>
    public void TriggerMercadorPactAnimation()
    {
        hasPactCorrupted = true;
        if (mercadorAnimationCoroutine != null) StopCoroutine(mercadorAnimationCoroutine);
        mercadorAnimationCoroutine = StartCoroutine(AnimateMercadorAppearance());
    }

    /// <summary>
    /// Reseta o visual do Mercador ao renascer ou curar.
    /// </summary>
    public void ResetMercadorPactUI()
    {
        if (mercadorAnimationCoroutine != null)
        {
            StopCoroutine(mercadorAnimationCoroutine);
            mercadorAnimationCoroutine = null;
        }

        if (mercadorBarObj != null)
        {
            mercadorBarObj.transform.localScale = mercadorBarOriginalScale;
            mercadorBarObj.SetActive(false);
        }

        if (mercadorHealthFillImage != null)
        {
            mercadorHealthFillImage.gameObject.SetActive(false);
        }

        if (healthBarContainerTransform != null && healthBarOriginalPos != Vector3.zero)
        {
            healthBarContainerTransform.localPosition = healthBarOriginalPos;
        }

        hasPactCorrupted = false;
        UpdateHealthBar();
    }

    private IEnumerator AnimateMercadorAppearance()
    {
        if (mercadorBarObj == null || mercadorHealthFillImage == null || healthBarContainerTransform == null)
        {
            FindUIReferences();
        }

        if (mercadorHealthFillImage != null)
        {
            mercadorHealthFillImage.gameObject.SetActive(true);
            mercadorHealthFillImage.fillAmount = (float)currentHealth / maxHealth;
        }

        if (mercadorBarObj != null)
        {
            mercadorBarObj.SetActive(true);
        }

        Image mercadorBarImg = mercadorBarObj != null ? mercadorBarObj.GetComponent<Image>() : null;
        CanvasGroup mercadorBarGroup = mercadorBarObj != null ? mercadorBarObj.GetComponent<CanvasGroup>() : null;

        // Efeito de "Garras se Fechando" / "Abocanhar":
        // Começa ligeiramente maior (1.30x) e translúcido, e fecha rápido com snap seco
        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 punchStartScale = mercadorBarOriginalScale * 1.30f;

        if (mercadorHealthFillImage != null)
        {
            Color c = mercadorHealthFillImage.color;
            c.a = 0f;
            mercadorHealthFillImage.color = c;
        }

        if (mercadorBarObj != null)
        {
            mercadorBarObj.transform.localScale = punchStartScale;
            if (mercadorBarImg != null)
            {
                Color c = mercadorBarImg.color;
                c.a = 0f;
                mercadorBarImg.color = c;
            }
            else if (mercadorBarGroup != null)
            {
                mercadorBarGroup.alpha = 0f;
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease Out Back suave (sensação elástica de garras estalando)
            float tScale = 1f - Mathf.Pow(1f - t, 3f);

            if (mercadorHealthFillImage != null)
            {
                Color c = mercadorHealthFillImage.color;
                c.a = Mathf.Lerp(0f, 1f, t * 1.5f);
                mercadorHealthFillImage.color = c;
                mercadorHealthFillImage.fillAmount = (float)currentHealth / maxHealth;
            }

            if (mercadorBarObj != null)
            {
                mercadorBarObj.transform.localScale = Vector3.LerpUnclamped(punchStartScale, mercadorBarOriginalScale, tScale);

                if (mercadorBarImg != null)
                {
                    Color c = mercadorBarImg.color;
                    c.a = Mathf.Lerp(0f, 1f, t * 2f);
                    mercadorBarImg.color = c;
                }
                else if (mercadorBarGroup != null)
                {
                    mercadorBarGroup.alpha = Mathf.Lerp(0f, 1f, t * 2f);
                }
            }

            yield return null;
        }

        // Garante valores finais
        if (mercadorBarObj != null)
        {
            mercadorBarObj.transform.localScale = mercadorBarOriginalScale;
            if (mercadorBarImg != null)
            {
                Color c = mercadorBarImg.color; c.a = 1f; mercadorBarImg.color = c;
            }
            else if (mercadorBarGroup != null)
            {
                mercadorBarGroup.alpha = 1f;
            }
        }
        if (mercadorHealthFillImage != null)
        {
            Color c = mercadorHealthFillImage.color; c.a = 1f; mercadorHealthFillImage.color = c;
        }

        // Micro-trepidação na barra (Screen Shake sutil na HealthBar para sentir o impacto)
        if (healthBarContainerTransform != null)
        {
            float shakeDuration = 0.18f;
            float shakeElapsed = 0f;
            float shakeMagnitude = 3.5f;

            while (shakeElapsed < shakeDuration)
            {
                shakeElapsed += Time.unscaledDeltaTime;
                float damping = 1f - (shakeElapsed / shakeDuration);
                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f) * shakeMagnitude * damping,
                    Random.Range(-1f, 1f) * shakeMagnitude * damping,
                    0f
                );
                healthBarContainerTransform.localPosition = healthBarOriginalPos + offset;
                yield return null;
            }
            healthBarContainerTransform.localPosition = healthBarOriginalPos;
        }

        mercadorAnimationCoroutine = null;
        Debug.Log("🩸 [PACTO MERCADOR] Animação de garras e sangue corrompido concluída com sucesso!");
    }

    public void FullHeal()
    {
        isInvulnerable = false;
        hasPactCorrupted = false;
        canRegenArmor = true;
        cursedHealthLost = 0;
        hasGreedPact = false;
        hasDoubleLoot = false;
        hasChaosSymphony = false;
        hasVampirism = false;
        hasNecrosis = false;
        hasSelfDamageOnAttack = false;
        isKnockbackImmune = false;
        canHeal = true;
        damageMultiplier = 1.0f;
        damageTakenMultiplier = 1.0f;
        currentHealth = maxHealth;
        currentArmor = maxArmor;
        ResetMercadorPactUI();
        UpdateHealthBar();
        UpdateArmorBar();
    }

    public void RestoreFullArmor()
    {
        if (isDead || maxArmor <= 0) return;
        currentArmor = maxArmor;
        UpdateArmorBar();
    }
    
    public void RestoreArmor(int amount)
    {
        if (isDead || maxArmor <= 0 || !canRegenArmor) return;
        currentArmor += amount;
        if (currentArmor > maxArmor) currentArmor = maxArmor;
        UpdateArmorBar();
    }

    private Coroutine adrenalineCoroutine = null;

    /// <summary>
    /// Bônus da Sinfonia do Caos: ao abater um inimigo Elite, o jogador ganha um surto de velocidade de movimento e ataque por alguns segundos.
    /// </summary>
    public void TriggerEliteKillAdrenaline(float duration = 5f)
    {
        if (adrenalineCoroutine != null)
        {
            StopCoroutine(adrenalineCoroutine);
        }
        adrenalineCoroutine = StartCoroutine(EliteAdrenalineRoutine(duration));
    }

    private IEnumerator EliteAdrenalineRoutine(float duration)
    {
        PlayerAttributesDefensive defStats = GetComponent<PlayerAttributesDefensive>() ?? GetComponentInChildren<PlayerAttributesDefensive>();
        PlayerAttributesOffensive offStats = GetComponent<PlayerAttributesOffensive>() ?? GetComponentInChildren<PlayerAttributesOffensive>();

        if (defStats != null) defStats.speedMultiplier *= 1.15f;
        if (offStats != null) offStats.attackSpeedMelee *= 1.20f;

        yield return new WaitForSeconds(duration);

        if (defStats != null) defStats.speedMultiplier /= 1.15f;
        if (offStats != null) offStats.attackSpeedMelee /= 1.20f;

        adrenalineCoroutine = null;
    }

    private void UpdateHealthBar()
    {
        if (healthFillImage != null)
        {
            if (!hasSavedOriginalColor)
            {
                originalHealthFillColor = healthFillImage.color;
                hasSavedOriginalColor = true;
            }

            healthFillImage.fillAmount = (float)currentHealth / maxHealth;
            healthFillImage.color = hasPactCorrupted ? darkPactHealthColor : originalHealthFillColor;
        }

        if (mercadorHealthFillImage != null && mercadorHealthFillImage.gameObject.activeSelf)
        {
            mercadorHealthFillImage.fillAmount = (float)currentHealth / maxHealth;
        }

        if (percentageText != null)
        {
            int percent = Mathf.RoundToInt(((float)currentHealth / maxHealth) * 100);
            percentageText.text = percent + "%";
        }

        if (gooBarImage != null)
        {
            if (cursedHealthLost > 0)
            {
                gooBarImage.enabled = true;
                gooBarImage.fillAmount = (float)(currentHealth + cursedHealthLost) / maxHealth;
            }
            else
            {
                gooBarImage.enabled = false;
            }
        }
    }

    public bool canRegenArmor = true;

    /// <summary>
    /// Localiza o ArmorBarContainer e mapeia os 5 pares de Outline e Fill do PlayerCanvasReal.
    /// </summary>
    public void FindArmorSlotReferences()
    {
        GameObject container = GameObject.Find("ArmorBarContainer");
        if (container == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                Transform[] transforms = c.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    if (t.name == "ArmorBarContainer")
                    {
                        container = t.gameObject;
                        break;
                    }
                }
                if (container != null) break;
            }
        }

        if (container != null)
        {
            Transform[] children = container.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < 5; i++)
            {
                string outlineName = (i == 0) ? "ArmorOutline" : $"ArmorOutline ({i})";
                string fillName = (i == 0) ? "ArmorBar_Fill" : $"ArmorBar_Fill ({i})";

                foreach (Transform t in children)
                {
                    if (t.name == outlineName)
                    {
                        armorOutlines[i] = t.gameObject;
                    }
                    else if (t.name == fillName)
                    {
                        armorFills[i] = t.GetComponent<Image>();
                    }
                }
            }

            if (armorBarImage == null && armorFills[0] != null)
            {
                armorBarImage = armorFills[0];
            }
        }
        else
        {
            GameObject armorObj = GameObject.Find("ArmorBar_Fill"); 
            if (armorObj != null) armorBarImage = armorObj.GetComponent<Image>();
        }
    }

    public void UpdateArmorBar()
    {
        // Garante que os slots do ArmorBarContainer estejam mapeados
        if (armorOutlines[0] == null || armorFills[0] == null)
        {
            FindArmorSlotReferences();
        }

        // Se a armadura estiver zerada ou regeneração bloqueada (ex: Pacto de Sangue)
        if (maxArmor <= 0 || !canRegenArmor)
        {
            for (int i = 0; i < 5; i++)
            {
                if (armorOutlines[i] != null)
                {
                    // O primeiro escudo (Slot 0) é eterno: sua carcaça permanece
                    armorOutlines[i].SetActive(i == 0);
                }
                if (armorFills[i] != null)
                {
                    armorFills[i].fillAmount = 0f;
                    armorFills[i].gameObject.SetActive(i == 0);
                }
            }
            if (armorBarImage != null) armorBarImage.fillAmount = 0f;
            if (armorText != null) armorText.text = "0/0";
            return;
        }

        float perPlate = Mathf.Max(1f, armorPerPlate);

        // Atualiza individualmente cada um dos 5 escudos do PlayerCanvasReal
        for (int i = 0; i < 5; i++)
        {
            float slotThreshold = i * perPlate;
            // Slot 0 é o eterno (sempre ativo). Slots 1..4 ativam quando maxArmor ultrapassa o threshold
            bool isSlotUnlocked = (i == 0) || (maxArmor > slotThreshold);

            if (armorOutlines[i] != null)
            {
                if (armorOutlines[i].activeSelf != isSlotUnlocked)
                {
                    armorOutlines[i].SetActive(isSlotUnlocked);
                }
            }

            if (armorFills[i] != null)
            {
                if (armorFills[i].gameObject.activeSelf != isSlotUnlocked)
                {
                    armorFills[i].gameObject.SetActive(isSlotUnlocked);
                }

                if (isSlotUnlocked)
                {
                    float slotCap = Mathf.Clamp(maxArmor - slotThreshold, 0f, perPlate);
                    float slotCurrent = Mathf.Clamp(currentArmor - slotThreshold, 0f, slotCap);

                    // Preenche verticalmente proporcional ao valor da placa
                    if (slotCap > 0f)
                    {
                        armorFills[i].fillAmount = slotCurrent / perPlate;
                    }
                    else
                    {
                        armorFills[i].fillAmount = 0f;
                    }
                }
                else
                {
                    armorFills[i].fillAmount = 0f;
                }
            }
        }

        // Compatibilidade com a referência legada armorBarImage
        if (armorBarImage != null && armorFills[0] == null)
        {
            armorBarImage.fillAmount = (float)currentArmor / maxArmor;
        }

        if (armorText != null)
        {
            armorText.text = currentArmor + "/" + maxArmor;
        }
    }

    public void SetArmorToZero()
    {
        maxArmor = 0;
        currentArmor = 0;
        canRegenArmor = false;
        UpdateArmorBar();
        Debug.Log("🛡️ [PACTO DE SANGUE] Armadura reduzida a ZERO! Regeneração de armadura bloqueada!");
    }
    
    public void SetCurrentSpawnPoint(Transform newSpawnPoint) { }

    // ========== UPGRADES (INFUSÃO DE ATRIBUTOS) ==========
    
    /// <summary>
    /// Modifica atributos de Vida e Armadura
    /// </summary>
    public void ModifyAttribute(string attributeName, float value, bool isMultiplier = false)
    {
        switch (attributeName.ToLower())
        {
            case "maxhealth":
            case "health":
                int oldHealth = maxHealth;
                maxHealth = Mathf.Max(1, isMultiplier ? Mathf.RoundToInt(maxHealth * value) : maxHealth + Mathf.RoundToInt(value));
                
                // Cura o player com o novo bônus e garante que currentHealth respeita o novo limite
                currentHealth = Mathf.Clamp(currentHealth + (maxHealth - oldHealth), 1, maxHealth);
                UpdateHealthBar();
                Debug.Log($"❤️ [PLAYER HEALTH] Vida Maxima aumentada: {oldHealth} -> {maxHealth} (+{maxHealth - oldHealth}) | Atual: {currentHealth}/{maxHealth}");
                break;

            case "maxarmor":
            case "armor":
                int oldArmor = maxArmor;
                maxArmor = Mathf.Max(0, isMultiplier ? Mathf.RoundToInt(maxArmor * value) : maxArmor + Mathf.RoundToInt(value));
                
                currentArmor = Mathf.Clamp(currentArmor + (maxArmor - oldArmor), 0, maxArmor);
                UpdateArmorBar();
                Debug.Log($"🛡️ [PLAYER HEALTH] Armadura Maxima aumentada: {oldArmor} -> {maxArmor} (+{maxArmor - oldArmor}) | Atual: {currentArmor}/{maxArmor}");
                break;

            default:
                Debug.LogWarning($"PlayerHealth: Atributo '{attributeName}' não encontrado!");
                break;
        }
    }

    // ========== SISTEMA DE STUN ==========
    
    /// <summary>
    /// Aplica stun ao jogador por uma duração específica.
    /// Chamado por inimigos como o Golem.
    /// </summary>
    public void ApplyStun(float duration)
    {
        // Não pode ser stunado se: já está stunado, morto, ou imune
        if (isStunned || isDead || stunImmunityTimer > 0) return;

        // Verifica se está em dash (não pode ser stunado durante dash)
        DashM dashScript = GetComponent<DashM>();
        if (dashScript != null && dashScript.isDashing) return;

        // Busca o StunVFX (lazy — garante compatibilidade mesmo sem o componente)
        if (stunVFX == null) stunVFX = GetComponent<StunVFX>();
        if (stunVFX == null) stunVFX = gameObject.AddComponent<StunVFX>();
        if (stunVFX != null) stunVFX.ShowStun(duration);

        StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;

        // Desabilita controles
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;

        // Para o movimento
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
        }

        // Animação de stun (opcional - usa o mesmo sistema de idle)
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat("Speed", 0f);
        }

        Debug.Log("Player STUNADO por " + duration + " segundos!");

        yield return new WaitForSeconds(duration);

        // Restaura controles
        isStunned = false;
        if (playerMovement != null) playerMovement.enabled = true;
        if (playerAttack != null) playerAttack.enabled = true;

        // Ativa imunidade temporária
        stunImmunityTimer = stunImmunityDuration;

        // Encerra imediatamente os efeitos visuais de stun (ícone e barra)
        if (stunVFX != null) stunVFX.HideStun();

        Debug.Log("Player recuperou do stun. Imunidade por " + stunImmunityDuration + "s");
    }
    
    // ========== SISTEMA DE ATRIBUTOS DEFENSIVOS ==========
    
    void Update()
    {
        // Gerenciar timer de imunidade a stun
        if (stunImmunityTimer > 0)
        {
            stunImmunityTimer -= Time.deltaTime;
        }
        
        // === REGENERAÇÃO DE ARMADURA (com Shield Delay pós-dano) ===
        if (!isDead && maxArmor > 0 && canRegenArmor && currentArmor < maxArmor && playerAttributes != null && (Time.time - lastDamageTime >= shieldRegenDelay))
        {
            armorRegenAccumulator += armorRegenRate * playerAttributes.armorRegen * Time.deltaTime;
            if (armorRegenAccumulator >= 1.0f)
            {
                int intRegen = Mathf.FloorToInt(armorRegenAccumulator);
                int previousArmor = currentArmor;
                currentArmor = Mathf.Min(maxArmor, currentArmor + intRegen);
                armorRegenAccumulator -= intRegen;
                
                // Log a cada segundo (aproximadamente)
                if (Time.frameCount % 60 == 0 && currentArmor != previousArmor)
                {
                    float regenPerSecond = armorRegenRate * playerAttributes.armorRegen;
                    Debug.Log($"🛡️ ARMOR REGEN! {previousArmor} → {currentArmor} | Taxa: {regenPerSecond:F1} pts/s");
                }
                    
                UpdateArmorBar();
            }
        }

        // === REGENERAÇÃO DE VIDA ===
        if (!isDead && currentHealth < maxHealth && playerAttributes != null && playerAttributes.healthRegen > 0f)
        {
            healthRegenAccumulator += playerAttributes.healthRegen * Time.deltaTime;
            if (healthRegenAccumulator >= 1.0f)
            {
                int intRegen = Mathf.FloorToInt(healthRegenAccumulator);
                currentHealth = Mathf.Min(maxHealth, currentHealth + intRegen);
                healthRegenAccumulator -= intRegen;
                
                UpdateHealthBar();
            }
        }

        // === NECROSE (Pacto do Parasita) ===
        if (hasNecrosis && !isDead)
        {
            if (Time.time - lastKillTime > 5f)
            {
                // Dano contínuo se ficar 5s sem matar
                int necroseDamage = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 0.02f * Time.deltaTime)); 
                currentHealth -= necroseDamage;
                if (currentHealth <= 0) Die();
                UpdateHealthBar();
            }
        }
        // === HEARTBEAT (Som de coração quando sem armadura) ===
        if (heartbeatSource != null && heartbeatClip != null)
        {
            bool shouldPlay = !isDead && maxArmor > 0 && canRegenArmor && currentArmor <= 0;

            if (shouldPlay && !isHeartbeatPlaying)
            {
                heartbeatSource.clip = heartbeatClip;
                heartbeatSource.volume = heartbeatVolume;
                heartbeatSource.Play();
                isHeartbeatPlaying = true;
                Debug.Log("💓 [HEARTBEAT] Coração batendo — armadura zerada!");
            }
            else if (!shouldPlay && isHeartbeatPlaying)
            {
                heartbeatSource.Stop();
                isHeartbeatPlaying = false;
                Debug.Log("💓 [HEARTBEAT] Coração parou — armadura regenerando.");
            }
        }
    }

    public void Heal(int amount)
    {
        if (isDead || !canHeal) return;
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateHealthBar();

        PlayHealSound();
    }
    
    [Header("Luz de Dano (DAMAGELIGHT)")]
    public GameObject damageLightObj;
    private Coroutine damageLightCoroutine;

    [Header("Damage Flash no Modelo do Player")]
    private Coroutine playerFlashCoroutine;
    private Renderer[] playerRenderers;
    private Color[] playerOriginalColors;

    private void TriggerDamageLight()
    {
        if (damageLightObj == null)
        {
            Transform found = transform.Find("DAMAGELIGHT");
            if (found == null)
            {
                Light[] lights = GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    if (l != null && (l.name == "DAMAGELIGHT" || l.name.ToUpper().Contains("DAMAGE")))
                    {
                        damageLightObj = l.gameObject;
                        break;
                    }
                }
            }
            else
            {
                damageLightObj = found.gameObject;
            }
        }

        if (damageLightObj != null)
        {
            if (damageLightCoroutine != null) StopCoroutine(damageLightCoroutine);
            damageLightCoroutine = StartCoroutine(AnimateDamageLight());
        }
    }

    private IEnumerator AnimateDamageLight()
    {
        damageLightObj.SetActive(true);
        yield return new WaitForSeconds(0.35f);
        if (damageLightObj != null) damageLightObj.SetActive(false);
    }

    private void TriggerPlayerRedFlash()
    {
        TriggerDamageLight();

        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            playerRenderers = GetComponentsInChildren<Renderer>();
            if (playerRenderers != null && playerRenderers.Length > 0)
            {
                playerOriginalColors = new Color[playerRenderers.Length];
                for (int i = 0; i < playerRenderers.Length; i++)
                {
                    if (playerRenderers[i] != null && playerRenderers[i].material != null && playerRenderers[i].material.HasProperty("_Color"))
                    {
                        playerOriginalColors[i] = playerRenderers[i].material.color;
                    }
                    else
                    {
                        playerOriginalColors[i] = Color.white;
                    }
                }
            }
        }

        if (playerRenderers != null && playerRenderers.Length > 0)
        {
            if (playerFlashCoroutine != null) StopCoroutine(playerFlashCoroutine);
            playerFlashCoroutine = StartCoroutine(AnimatePlayerRedFlash());
        }
    }

    private IEnumerator AnimatePlayerRedFlash()
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Color flashRed = new Color(1f, 0.25f, 0.25f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null && playerRenderers[i].material != null && playerRenderers[i].material.HasProperty("_Color"))
                {
                    playerRenderers[i].material.color = Color.Lerp(flashRed, playerOriginalColors[i], t);
                }
            }
            yield return null;
        }

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null && playerRenderers[i].material != null && playerRenderers[i].material.HasProperty("_Color"))
            {
                playerRenderers[i].material.color = playerOriginalColors[i];
            }
        }
    }

    private float lastDamageTime = -999f;
    public float damageIFrameCooldown = 0.25f; // Proteção contra empilhamento de multi-hits no mesmo frame

    /// <summary>
    /// Aplica dano ao jogador com todos os atributos defensivos.
    /// </summary>
    public void TakeDamage(int damage, GameObject attacker = null)
    {
        // 0. Checagem de Esquiva Perfeita (Perfect Dash): se estiver na janela inicial do dash, cancela o dano e ativa o contra-ataque
        DashM dash = GetComponent<DashM>() ?? GetComponentInParent<DashM>();
        if (dash != null && dash.IsInPerfectDodgeWindow)
        {
            dash.TriggerPerfectDodge(attacker);
            return;
        }

        // Se estiver nos I-Frames estendidos do Perfect Dash, bloqueia 100% de qualquer dano residual
        if (dash != null && dash.IsPerfectDodgeInvulnerable)
        {
            return;
        }

        if (isDead) return;

        if (isInvulnerable)
        {
            // Failsafe Duplo: se isInvulnerable estiver true mas o jogador NÃO estiver em dash ativo nem em ultimate ativo nem em Perfect Dash,
            // desativa a invulnerabilidade imediatamente e prossegue com o dano!
            bool isLegitInvulnerable = false;
            if (dash != null && (dash.isDashing || dash.IsPerfectDodgeInvulnerable)) isLegitInvulnerable = true;
            PlayerUltimate ult = GetComponent<PlayerUltimate>() ?? GetComponentInParent<PlayerUltimate>();
            if (ult != null && ult.IsUltimateActive()) isLegitInvulnerable = true;

            if (!isLegitInvulnerable)
            {
                isInvulnerable = false;
            }
            else
            {
                return;
            }
        }

        // Proteção Anti-Multi-Hit: Impede que múltiplos colisores no mesmo milissegundo zerem a vida do player instantaneamente!
        if (Time.time - lastDamageTime < damageIFrameCooldown) return;
        lastDamageTime = Time.time;

        // Dispara modelo ficando levemente vermelho
        TriggerPlayerRedFlash();

        // Escalonamento de Dano Inimigo (+3% por sala avançada conforme GDD / Hp_Dano_Inimigos.pdf)
        if (RunManager.instance != null && RunManager.instance.currentRoomNumber > 1 && attacker != null)
        {
            float roomDamageMultiplier = 1f + 0.03f * (RunManager.instance.currentRoomNumber - 1);
            damage = Mathf.RoundToInt(damage * roomDamageMultiplier);
        }

        // Se estivermos no modo Endless e o level for maior que 3, aumenta o dano recebido pelo jogador
        if (RunManager.instance != null && RunManager.instance.isEndlessMode && RunManager.instance.currentLevel > 3)
        {
            float endlessDamageMultiplier = 1f + (RunManager.instance.currentLevel - 3) * 0.15f; // +15% de dano por level acima do 3
            damage = Mathf.RoundToInt(damage * endlessDamageMultiplier);
        }

        // Resistência a Danos de Ácido / Poças / Veneno (Filtro Químico do Robozinho)
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.HasAcidPoisonResist && attacker == null)
        {
            damage = Mathf.RoundToInt(damage * 0.60f);
        }
        
        int finalDamage = Mathf.RoundToInt(damage * damageTakenMultiplier);
        
        // === DODGE (Esquiva) ===
        if (playerAttributes != null && playerAttributes.dodgeChance > 0)
        {
            float dodgeRoll = Random.Range(0f, 100f);
            if (dodgeRoll < playerAttributes.dodgeChance)
            {
                Debug.Log($"✨ ESQUIVOU! Dano anulado | Roll: {dodgeRoll:F1} < {playerAttributes.dodgeChance}% chance");
                return; // Anula completamente o dano
            }
        }
        
        // === DAMAGE NEGATION (Mitigação) ===
        if (playerAttributes != null && playerAttributes.damageNegation > 0)
        {
            int damageBeforeNegation = finalDamage;
            float reductionPercent = playerAttributes.damageNegation / 100f;
            finalDamage = Mathf.RoundToInt(finalDamage * (1f - reductionPercent));
            Debug.Log($"🛡️ DAMAGE NEGATION! Dano: {damageBeforeNegation} → {finalDamage} (-{playerAttributes.damageNegation}%)");
        }
        
        int totalDamageTakenThisHit = 0;

        // Aplicar dano em armor primeiro, depois health
        if (currentArmor > 0)
        {
            int damageToArmor = Mathf.Min(finalDamage, currentArmor);
            currentArmor -= damageToArmor;
            finalDamage -= damageToArmor;
            totalDamageTakenThisHit += damageToArmor;
            UpdateArmorBar();
        }
        
        if (finalDamage > 0)
        {
            // Checa se o dano seria letal
            if (currentHealth - finalDamage <= 0)
            {
                InfusionManager infusionManager = GetComponent<InfusionManager>();
                if (infusionManager != null && infusionManager.HasInfusion("sentinel_leg_t4") && Time.time >= sentinelLegCooldownEndTime)
                {
                    float currentHealthPercent = (float)currentHealth / maxHealth;
                    int previousHealth = currentHealth;
                    
                    if (currentHealthPercent < 0.30f)
                    {
                        // Se estiver com menos de 30% da vida, apenas ignora o dano (não altera a vida atual)
                        finalDamage = 0; 
                    }
                    else
                    {
                        // Se estiver com mais de 30% da vida, fica com 30% da vida
                        int targetHealth = Mathf.RoundToInt(maxHealth * 0.30f);
                        currentHealth = targetHealth;
                        finalDamage = 0; // O dano foi mitigado, já definimos a vida para 30%
                    }
                    
                    // Tempo de recarga de 10 minutos (600 segundos)
                    sentinelLegCooldownEndTime = Time.time + 600f;
                    
                    UpdateHealthBar();
                    Debug.Log($"🛡️ MITIGAÇÃO DE PERNA DE SENTINELA ATIVADA! Anterior: {previousHealth} HP -> Atual: {currentHealth} HP. Cooldown ativo por 10 minutos.");
                }
            }

            if (finalDamage > 0)
            {
                currentHealth -= finalDamage;
                totalDamageTakenThisHit += finalDamage;
                RunStatsManager.Instance?.RecordDamageTaken(finalDamage);
                UpdateHealthBar();
                
                Debug.Log($"❤️ Dano recebido: {finalDamage} | Health: {currentHealth}/{maxHealth} | Armor: {currentArmor}/{maxArmor}");
                
                if (currentHealth <= 0)
                {
                    Die();
                }
            }
        }

        // Dispara o evento desacoplado de dano e o indicador de dano recebido na tela via Eptinho Popup
        if (totalDamageTakenThisHit > 0)
        {
            OnPlayerDamaged?.Invoke(totalDamageTakenThisHit, attacker);

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso($"💥 Você recebeu -{totalDamageTakenThisHit} de Dano!");
            }
        }
        
        // === THORNS (Dano Refletido) ===
        if (attacker != null && playerAttributes != null && playerAttributes.thorns > 0)
        {
            DummyHealth enemyHealth = attacker.GetComponent<DummyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(playerAttributes.thorns);
                Debug.Log($"🌵 THORNS! Refletido {playerAttributes.thorns} de dano para {attacker.name}");
            }
        }
    }

    private void PlayHealSound()
    {
        AudioClip clipToPlay = healSound;

        #if UNITY_EDITOR
        if (clipToPlay == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip confirmation_002");
            if (guids.Length > 0)
            {
                clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        #endif

        if (clipToPlay != null)
        {
            GameObject sfxObj = new GameObject("Heal_SFX");
            sfxObj.transform.position = transform.position;
            AudioSource source = sfxObj.AddComponent<AudioSource>();
            source.clip = clipToPlay;
            source.volume = healSoundVolume;
            source.spatialBlend = 0f;
            source.pitch = Random.Range(0.98f, 1.02f);
            source.Play();
            Destroy(sfxObj, clipToPlay.length + 0.5f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (healSound == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip confirmation_002");
            if (guids.Length > 0)
            {
                healSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
    }
#endif
}
