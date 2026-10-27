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

    [Header("Dash Charges (Modular & Cargas Independentes)")]
    public int maxDashes = 2;
    private int dashesLeft;

    [Header("Cooldown por Carga")]
    [Tooltip("Tempo em segundos para recarregar cada carga individual de dash (padrão: 0.5s).")]
    public float dashCooldown = 0.5f;

    // Timers individuais por carga (cada uma carrega separadamente em paralelo)
    private float[] chargeSlotTimers = new float[0];

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
    public float MaxCooldown => (playerAttributes != null && playerAttributes.dashCooldownMultiplier > 0) ? dashCooldown * playerAttributes.dashCooldownMultiplier : dashCooldown;
    public int DashesLeft => dashesLeft;
    public int MaxDashes => maxDashes;
    public bool IsInPerfectDodgeWindow => isDashing && isInPerfectDodgeWindow;
    public bool IsNextAttackGuaranteedCrit => isNextAttackGuaranteedCrit && Time.unscaledTime <= critBuffEndTime;
    public bool IsRecharging => dashesLeft < maxDashes;

    public float CurrentCooldownTimer
    {
        get
        {
            float minRemaining = float.MaxValue;
            float cd = MaxCooldown;
            for (int i = 0; i < maxDashes; i++)
            {
                if (chargeSlotTimers != null && i < chargeSlotTimers.Length && chargeSlotTimers[i] < cd)
                {
                    float rem = cd - chargeSlotTimers[i];
                    if (rem < minRemaining) minRemaining = rem;
                }
            }
            return minRemaining == float.MaxValue ? 0f : minRemaining;
        }
    }

    public float ChargeProgress
    {
        get
        {
            if (dashesLeft >= maxDashes) return 1f;
            float maxProg = 0f;
            float cd = Mathf.Max(0.01f, MaxCooldown);
            for (int i = 0; i < maxDashes; i++)
            {
                if (chargeSlotTimers != null && i < chargeSlotTimers.Length && chargeSlotTimers[i] < cd)
                {
                    float prog = chargeSlotTimers[i] / cd;
                    if (prog > maxProg) maxProg = prog;
                }
            }
            return Mathf.Clamp01(maxProg);
        }
    }

    /// <summary>
    /// Retorna o progresso de recarga (0.0 a 1.0) de um slot específico de dash.
    /// </summary>
    public float GetSlotProgress(int index)
    {
        if (chargeSlotTimers == null || index < 0 || index >= chargeSlotTimers.Length) return 0f;
        float cd = Mathf.Max(0.01f, MaxCooldown);
        return Mathf.Clamp01(chargeSlotTimers[index] / cd);
    }

    /// <summary>
    /// Informa se o slot específico de dash está 100% carregado e pronto para uso.
    /// </summary>
    public bool IsSlotReady(int index)
    {
        if (chargeSlotTimers == null || index < 0 || index >= chargeSlotTimers.Length) return false;
        return chargeSlotTimers[index] >= MaxCooldown;
    }

    /// <summary>
    /// Retorna o tempo restante em segundos para a carga deste slot ficar pronta.
    /// </summary>
    public float GetSlotRemainingTime(int index)
    {
        if (chargeSlotTimers == null || index < 0 || index >= chargeSlotTimers.Length) return 0f;
        return Mathf.Max(0f, MaxCooldown - chargeSlotTimers[index]);
    }

    private void Awake()
    {
        InitSlotTimers();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<PlayerM>();
        attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>() ?? GetComponentInParent<PrimaryAttackKnife>();
        
        // Buscar PlayerAttributesDefensive
        playerAttributes = GetComponent<PlayerAttributesDefensive>();
        if (playerAttributes != null)
        {
            // Se houver upgrades/atributos com mais cargas, expande maxDashes
            if (playerAttributes.dashCounts > maxDashes)
            {
                maxDashes = playerAttributes.dashCounts;
            }
        }

        InitSlotTimers();
        dashesLeft = maxDashes;
        
        // Buscar PlayerHealth para invulnerabilidade
        playerHealth = GetComponent<PlayerHealth>();
        
        // Tenta encontrar UI no inicio
        FindUIReferences();

        // Garante que o DashUI existe no Canvas para exibir as barras modulares
        if (GetComponent<DashUI>() == null && FindFirstObjectByType<DashUI>() == null)
        {
            gameObject.AddComponent<DashUI>();
        }
    }

    private void InitSlotTimers()
    {
        if (maxDashes < 1) maxDashes = 1;
        float cd = MaxCooldown;

        if (chargeSlotTimers == null || chargeSlotTimers.Length != maxDashes)
        {
            chargeSlotTimers = new float[maxDashes];
            for (int i = 0; i < maxDashes; i++)
            {
                chargeSlotTimers[i] = cd; // Todos começam prontos
            }
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

        // Garante tamanho do array sincronizado caso maxDashes mude em runtime
        if (chargeSlotTimers == null || chargeSlotTimers.Length != maxDashes)
        {
            InitSlotTimers();
        }

        float effectiveCooldown = MaxCooldown;

        // 1. Cada carga carrega separadamente em paralelo
        int readyCount = 0;
        for (int i = 0; i < maxDashes; i++)
        {
            if (chargeSlotTimers[i] < effectiveCooldown)
            {
                chargeSlotTimers[i] += Time.deltaTime;
                if (chargeSlotTimers[i] >= effectiveCooldown)
                {
                    chargeSlotTimers[i] = effectiveCooldown;
                }
            }

            if (chargeSlotTimers[i] >= effectiveCooldown)
            {
                readyCount++;
            }
        }
        dashesLeft = readyCount;

        // 2. Inicia o dash se tiver pelo menos 1 carga pronta
        if ((Time.time - lastDashInputTime <= inputBufferWindow) && dashesLeft > 0 && !isDashing && !plasmaActionLocked)
        {
            if (attackScript != null && attackScript.isAttacking)
            {
                attackScript.CancelAttackForDash();
            }

            lastDashInputTime = -999f;
            StartCoroutine(PerformDash());
        }

        // 3. Expira buff de crítico garantido do Perfect Dash
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

        // Consome a carga mais alta disponível (assim o slot correspondente entra em recarga)
        float effectiveCooldown = MaxCooldown;
        int slotToUse = -1;
        for (int i = maxDashes - 1; i >= 0; i--)
        {
            if (chargeSlotTimers[i] >= effectiveCooldown)
            {
                slotToUse = i;
                break;
            }
        }
        if (slotToUse >= 0)
        {
            chargeSlotTimers[slotToUse] = 0f;
        }
        dashesLeft = Mathf.Max(0, dashesLeft - 1);

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
    [ContextMenu("Testar Esquiva Perfeita")]
    public void DebugTriggerPerfectDodge()
    {
        TriggerPerfectDodge(null);
    }

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

        // 2. Recompensa de Mobilidade: Restaura 1 Dash imediatamente (enche o slot com menor progresso)
        float effectiveCooldown = MaxCooldown;
        int slotToRestore = -1;
        float lowestTime = float.MaxValue;
        for (int i = 0; i < maxDashes; i++)
        {
            if (chargeSlotTimers != null && i < chargeSlotTimers.Length && chargeSlotTimers[i] < effectiveCooldown)
            {
                if (chargeSlotTimers[i] < lowestTime)
                {
                    lowestTime = chargeSlotTimers[i];
                    slotToRestore = i;
                }
            }
        }
        if (slotToRestore >= 0)
        {
            chargeSlotTimers[slotToRestore] = effectiveCooldown;
            dashesLeft = Mathf.Min(maxDashes, dashesLeft + 1);
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
        float cd = MaxCooldown;
        for (int count = 0; count < amount; count++)
        {
            int slotToRestore = -1;
            float lowestTime = float.MaxValue;
            for (int i = 0; i < maxDashes; i++)
            {
                if (chargeSlotTimers != null && i < chargeSlotTimers.Length && chargeSlotTimers[i] < cd)
                {
                    if (chargeSlotTimers[i] < lowestTime)
                    {
                        lowestTime = chargeSlotTimers[i];
                        slotToRestore = i;
                    }
                }
            }
            if (slotToRestore >= 0)
            {
                chargeSlotTimers[slotToRestore] = cd;
            }
        }
        dashesLeft = Mathf.Min(maxDashes, dashesLeft + amount);
        Debug.Log($"Dashes recarregados! Total agora: {dashesLeft}/{maxDashes}");
    }
}
