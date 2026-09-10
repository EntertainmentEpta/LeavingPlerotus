using System.Collections;
using UnityEngine;
using TMPro;

public class DashM : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerAttributesDefensive playerAttributes;
    private PlayerHealth playerHealth;
    public bool isDashing = false;

    [Header("Dash Settings")]
    public float dashSpeed = 30f;
    public float dashDuration = 0.2f;

    [Header("Dash Charges")]
    public int maxDashes = 2;
    private int dashesLeft;

    [Header("Cooldown")]
    public float dashCooldown = 2f; // Tempo em segundos para recarregar todos os dashes
    private float cooldownTimer;
    private bool isRecharging = false;

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

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        dashesLeft = maxDashes;
        attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>() ?? GetComponentInParent<PrimaryAttackKnife>();
        
        // Buscar PlayerAttributesDefensive
        playerAttributes = GetComponent<PlayerAttributesDefensive>();
        if (playerAttributes == null)
        {
            Debug.LogWarning("DashM: PlayerAttributesDefensive não encontrado! Atributos de dash não serão aplicados.");
        }
        
        // Buscar PlayerHealth para invulnerabilidade
        playerHealth = GetComponent<PlayerHealth>();
        
        // Tenta encontrar UI no inicio
        FindUIReferences();
    }

    public void FindUIReferences()
    {
        GameObject textObj = GameObject.Find("DashText"); 
        if (textObj != null)
        {
            dashCountText = textObj.GetComponent<TextMeshProUGUI>();
            dashCountText.enabled = true;
            HandleDashUI(); // Atualiza o texto imediatamente
        }
    }

    private void Update()
    {
        if (CheatConsole.IsOpen) return;

        // Trava de segurança: Proíbe Dash durante o Ultimate para evitar exploits de voo/cancelamento indevido
        PlayerUltimate playerUlt = GetComponent<PlayerUltimate>() ?? GetComponentInParent<PlayerUltimate>() ?? GetComponentInChildren<PlayerUltimate>();
        if (playerUlt != null && playerUlt.IsUltimateActive()) return;

        if (Input.GetKeyDown(dashKey))
        {
            lastDashInputTime = Time.time;
        }

        // Lógica para iniciar o dash com suporte a Input Buffer Temporal e Dash-Canceling de ataque (Estilo Hades)
        if ((Time.time - lastDashInputTime <= inputBufferWindow) && dashesLeft > 0 && !isDashing && !isRecharging)
        {
            // Se o player estiver atacando, cancela a recuperação do ataque imediatamente para realizar o Dash
            if (attackScript != null && attackScript.isAttacking)
            {
                attackScript.CancelAttackForDash();
            }

            lastDashInputTime = -999f; // Consome o comando
            StartCoroutine(PerformDash());
        }

        // Lógica do Cooldown
        if (isRecharging)
        {
            // Se a recarga está ativa, diminui o timer
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                // Quando o timer acaba, reseta os dashes e para a recarga
                isRecharging = false;
                
                // Aplicar Dash Counts do PlayerAttributesDefensive
                if (playerAttributes != null)
                {
                    dashesLeft = playerAttributes.dashCounts;
                    maxDashes = playerAttributes.dashCounts;
                }
                else
                {
                    dashesLeft = maxDashes;
                }
            }
        }

        // Atualiza a UI a cada frame
        HandleDashUI();
    }

    private IEnumerator PerformDash()
    {
        isDashing = true;
        dashesLeft--; // Gasta um dash
        
        // === NOVA LÓGICA MANUAL DE DIREÇÃO DO DASH ===
        float horizontal = 0f;
        if (Input.GetKey(keyRight)) horizontal += 1f;
        if (Input.GetKey(keyLeft)) horizontal -= 1f;

        float vertical = 0f;
        if (Input.GetKey(keyUp)) vertical += 1f;
        if (Input.GetKey(keyDown)) vertical -= 1f;

        Vector3 dashDirection = new Vector3(horizontal, 0f, vertical).normalized;

        // Se o cara apertar dash parado, esquiva para frente
        if (dashDirection == Vector3.zero)
        {
            dashDirection = transform.forward;
        }

        rb.linearVelocity = Vector3.zero; 
        rb.linearVelocity = dashDirection * dashSpeed;
        
        // Ativar invulnerabilidade durante o dash
        if (playerHealth != null && playerAttributes != null && playerAttributes.dashInvulnerability > 0)
        {
            playerHealth.isInvulnerable = true;
            Debug.Log($"🛡️ Dash Invulnerability ativada por {playerAttributes.dashInvulnerability}s");
        }

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector3.zero; // Para o movimento bruscamente no final do dash
        
        // Desativar invulnerabilidade se estava ativa
        if (playerHealth != null && playerAttributes != null && playerAttributes.dashInvulnerability > 0)
        {
            playerHealth.isInvulnerable = false;
        }
        
        isDashing = false;

        // Se acabaram os dashes, inicia o cooldown
        if (dashesLeft <= 0)
        {
            isRecharging = true;
            
            // Aplicar Dash Cooldown Multiplier
            if (playerAttributes != null)
            {
                cooldownTimer = dashCooldown * playerAttributes.dashCooldownMultiplier;
                Debug.Log($"⏱️ Dash Cooldown: {dashCooldown}s × {playerAttributes.dashCooldownMultiplier} = {cooldownTimer:F2}s");
            }
            else
            {
                cooldownTimer = dashCooldown;
            }
        }
    }

    private void HandleDashUI()
    {
        if (dashCountText != null)
        {
            if (isRecharging)
            {
                // Se estiver recarregando, mostra o timer
                dashCountText.text = "Dash: " + cooldownTimer.ToString("F1"); // "F1" mostra 1 casa decimal
            }
            else
            {
                // Senão, mostra a quantidade de dashes
                dashCountText.text = "Dash: " + dashesLeft;
            }
        }
    }

    // Função chamada pelo item de recarga (DashRecharge.cs)
    public void RechargeDashes(int amount)
    {
        // Se estiver em cooldown, a recarga o cancela e enche os dashes.
        if (isRecharging)
        {
            isRecharging = false;
        }
        
        dashesLeft += amount;
        
        // Garante que a quantidade de dashes não ultrapasse o máximo
        if (dashesLeft > maxDashes)
        {
            dashesLeft = maxDashes;
        }
        Debug.Log("Dashes recarregados! Total agora: " + dashesLeft);
    }
}