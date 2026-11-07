using System.Collections;
using UnityEngine;
using TMPro;

public class DashM : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerAttributesDefensive playerAttributes;
    private PlayerHealth playerHealth;
    private PlayerM playerMovement;
    public bool isDashing = false;

    [Header("Dash Settings")]
    public float dashSpeed = 30f;
    public float dashDuration = 0.2f;

    [Header("Dash Charges (Estilo Hades)")]
    public int maxDashes = 2;
    private int dashesLeft;

    [Header("Cooldown Independente")]
    [Tooltip("Tempo em segundos para recarregar UMA carga individual de dash (Estilo Hades).")]
    public float dashCooldown = 0.9f;
    private float chargeRechargeTimer = 0f;

    [Header("Esquiva Perfeita (Perfect Dash)")]
    [Tooltip("Janela inicial em segundos para qualificar uma esquiva perfeita.")]
    public float perfectDodgeWindowDuration = 0.16f;
    private bool isInPerfectDodgeWindow = false;
    private bool isNextAttackGuaranteedCrit = false;
    private float critBuffEndTime = 0f;

    [Header("UI")]
    public TextMeshProUGUI dashCountText;

    [Header("Input de Ação")]
    public KeyCode dashKey = KeyCode.E;

    [Header("Input de Movimento (Direção do Dash)")]
    public KeyCode keyUp = KeyCode.W;
    public KeyCode keyDown = KeyCode.S;
    public KeyCode keyLeft = KeyCode.A;
    public KeyCode keyRight = KeyCode.D;

    [Header("Input Buffer & Dash-Canceling (Estilo Hades)")]
    public float inputBufferWindow = 0.20f;
    private float lastDashInputTime = -999f;
    private PrimaryAttackKnife attackScript;

    [Header("T4 Effect Override")]
    [Tooltip("Multiplicador de duração do dash. Modificado pelo ExplosiveDashEffect (T4).\n" +
             "1.0 = normal, valores maiores = dash mais longo.")]
    [HideInInspector]
    public float dashDurationMultiplier = 1.0f;

    // === Getters para DashUI e Sistemas Externos ===
    public float CurrentCooldownTimer => dashesLeft >= maxDashes ? 0f : Mathf.Max(0f, MaxCooldown - chargeRechargeTimer);
    public float MaxCooldown => (playerAttributes != null && playerAttributes.dashCooldownMultiplier > 0) ? dashCooldown * playerAttributes.dashCooldownMultiplier : dashCooldown;
    public float ChargeProgress => dashesLeft >= maxDashes ? 1f : Mathf.Clamp01(chargeRechargeTimer / Mathf.Max(0.01f, MaxCooldown));
    public bool IsRecharging => dashesLeft < maxDashes;
    public int DashesLeft => dashesLeft;
    public int MaxDashes => maxDashes;
    public bool IsInPerfectDodgeWindow => isDashing && isInPerfectDodgeWindow;
    public bool IsNextAttackGuaranteedCrit => isNextAttackGuaranteedCrit && Time.unscaledTime <= critBuffEndTime;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerM>();
        dashesLeft = maxDashes;
        attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>() ?? GetComponentInParent<PrimaryAttackKnife>();
        
        // Buscar PlayerAttributesDefensive
        playerAttributes = GetComponent<PlayerAttributesDefensive>();
        if (playerAttributes == null)
        {
            Debug.LogWarning("DashM: PlayerAttributesDefensive não encontrado! Atributos de dash não serão aplicados.");
        }
        else if (playerAttributes.dashCounts > 0)
        {
            maxDashes = playerAttributes.dashCounts;
            dashesLeft = maxDashes;
        }
        
        // Buscar PlayerHealth para invulnerabilidade
        playerHealth = GetComponent<PlayerHealth>();
        
        // Tenta encontrar UI no inicio
        FindUIReferences();

        // Garante que o DashUI existe no Canvas para exibir recarga e cargas
        if (GetComponent<DashUI>() == null && FindFirstObjectByType<DashUI>() == null)
        {
            gameObject.AddComponent<DashUI>();
        }
    }

    public void FindUIReferences()
    {
        GameObject textObj = GameObject.Find("DashText"); 
        if (textObj != null)
        {
            dashCountText = textObj.GetComponent<TextMeshProUGUI>();
            dashCountText.enabled = true;
            HandleDashUI();
        }
    }

    private void Update()
    {
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused) return;
        if (CheatConsole.IsOpen) return;
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) return;
        if (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen) return;

        // Trava de segurança: Proíbe Dash durante o Ultimate
        PlayerUltimate playerUlt = GetComponent<PlayerUltimate>() ?? GetComponentInParent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        if (playerUlt != null && playerUlt.IsUltimateActive()) return;

        bool plasmaActionLocked = playerMovement != null && playerMovement.IsPlasmaActionLocked;
        if (plasmaActionLocked)
        {
            lastDashInputTime = -999f;
        }
        else if (Input.GetKeyDown(dashKey) || Input.GetKeyDown(KeyCode.Space))
        {
            lastDashInputTime = Time.time;
        }

        // Inicia o dash se tiver pelo menos 1 carga (não precisa esperar tudo recarregar!)
        if ((Time.time - lastDashInputTime <= inputBufferWindow) && dashesLeft > 0 && !isDashing && !plasmaActionLocked)
        {
            if (attackScript != null && attackScript.isAttacking)
            {
                attackScript.CancelAttackForDash();
            }

            lastDashInputTime = -999f;
            StartCoroutine(PerformDash());
        }

        // Atualização da Recarga Independente de Cargas (Estilo Hades)
        float effectiveCooldown = MaxCooldown;
        if (dashesLeft < maxDashes)
        {
            chargeRechargeTimer += Time.deltaTime;
            if (chargeRechargeTimer >= effectiveCooldown)
            {
                dashesLeft++;
                chargeRechargeTimer = (dashesLeft < maxDashes) ? (chargeRechargeTimer - effectiveCooldown) : 0f;
            }
        }
        else
        {
            chargeRechargeTimer = 0f;
        }

        // Expira buff de crítico garantido do Perfect Dash
        if (isNextAttackGuaranteedCrit && Time.unscaledTime > critBuffEndTime)
        {
            isNextAttackGuaranteedCrit = false;
        }

        HandleDashUI();
    }

    private IEnumerator PerformDash()
    {
        isDashing = true;
        isInPerfectDodgeWindow = true;
        dashesLeft--; // Gasta 1 carga

        // Ativar I-Frames durante o dash
        if (playerHealth != null)
        {
            playerHealth.isInvulnerable = true;
        }
        
        // Direção do dash
        float horizontal = 0f;
        if (Input.GetKey(keyRight)) horizontal += 1f;
        if (Input.GetKey(keyLeft)) horizontal -= 1f;

        float vertical = 0f;
        if (Input.GetKey(keyUp)) vertical += 1f;
        if (Input.GetKey(keyDown)) vertical -= 1f;

        Vector3 dashDirection = new Vector3(horizontal, 0f, vertical).normalized;
        if (dashDirection == Vector3.zero)
        {
            dashDirection = transform.forward;
        }

        rb.linearVelocity = Vector3.zero; 
        rb.linearVelocity = dashDirection * dashSpeed;

        float totalDuration = dashDuration * dashDurationMultiplier;
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= perfectDodgeWindowDuration && isInPerfectDodgeWindow)
            {
                isInPerfectDodgeWindow = false;
            }
            yield return null;
        }

        isInPerfectDodgeWindow = false;

        // Conserva inércia suave na direção do dash para emendar na corrida
        float exitSpeed = playerMovement != null ? playerMovement.sprintSpeed : 11f;
        rb.linearVelocity = dashDirection * exitSpeed;
        
        if (playerHealth != null)
        {
            if (playerAttributes == null || playerAttributes.dashInvulnerability <= 0)
            {
                playerHealth.isInvulnerable = false;
            }
        }
        
        isDashing = false;
    }

    /// <summary>
    /// Disparado pelo PlayerHealth quando o jogador esquiva com sucesso dentro da janela inicial.
    /// Ativa hit-stop, restaura 1 dash e concede 100% crítico no próximo golpe.
    /// </summary>
    public void TriggerPerfectDodge(GameObject attacker = null)
    {
        isInPerfectDodgeWindow = false;
        StartCoroutine(DoPerfectDodgeRoutine(attacker));
    }

    private IEnumerator DoPerfectDodgeRoutine(GameObject attacker)
    {
        Debug.Log("⚡ [DashM] PERFECT DASH EXECUTADO COM SUCESSO!");

        // 1. Hit-Stop (Desaceleração do tempo / Witch Time)
        Time.timeScale = 0.15f;

        // 2. Recompensa de Mobilidade: Restaura 1 Dash imediatamente!
        if (dashesLeft < maxDashes)
        {
            dashesLeft++;
            chargeRechargeTimer = 0f;
        }

        // 3. Recompensa Ofensiva: 100% de Crítico no próximo ataque (3s)
        isNextAttackGuaranteedCrit = true;
        critBuffEndTime = Time.unscaledTime + 3.0f;

        // 4. Feedback Visual na UI
        if (DashUI.Instance != null)
        {
            DashUI.Instance.TriggerPerfectDodgeFlash();
        }

        // Duração do Hit-Stop em tempo real
        yield return new WaitForSecondsRealtime(0.18f);

        Time.timeScale = 1.0f;
    }

    /// <summary>
    /// Consome o bônus de crítico garantido ao desferir um golpe.
    /// </summary>
    public bool ConsumeGuaranteedCrit()
    {
        if (isNextAttackGuaranteedCrit && Time.unscaledTime <= critBuffEndTime)
        {
            isNextAttackGuaranteedCrit = false;
            return true;
        }
        return false;
    }

    private void HandleDashUI()
    {
        if (dashCountText != null)
        {
            if (dashesLeft < maxDashes)
            {
                float remaining = CurrentCooldownTimer;
                dashCountText.text = $"Dash: {dashesLeft}/{maxDashes} ({remaining:F1}s)";
            }
            else
            {
                dashCountText.text = $"Dash: {dashesLeft}/{maxDashes}";
            }
        }
    }

    public void RechargeDashes(int amount)
    {
        dashesLeft = Mathf.Min(maxDashes, dashesLeft + amount);
        chargeRechargeTimer = 0f;
        Debug.Log($"Dashes recarregados! Total agora: {dashesLeft}/{maxDashes}");
    }
}
