using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

public class DashM : MonoBehaviour
{
    private Rigidbody rb;
    private PlayerAttributesDefensive playerAttributes;
    private PlayerHealth playerHealth;
    private PlayerM playerMovement;
    public bool isDashing = false;

    [Header("Dash Settings")]
    public float dashSpeed = 38f;
    public float dashDuration = 0.22f;

    [Header("Dash Charges (Modular & Cargas Independentes)")]
    public int maxDashes = 2;
    private int bonusDashes = 0;
    private int dashesLeft;

    [Header("Cooldown por Carga")]
    [Tooltip("Tempo em segundos para recarregar cada carga individual de dash (padrão: 0.9s conforme game design).")]
    public float dashCooldown = 0.9f;

    // Timers individuais por carga (cada uma carrega separadamente em paralelo)
    private float[] chargeSlotTimers = new float[0];

    public static DashM Instance { get; private set; }

    [Header("Esquiva Perfeita (Perfect Dash)")]
    [Tooltip("Janela inicial em segundos para qualificar uma esquiva perfeita.")]
    public float perfectDodgeWindowDuration = 0.14f;
    private bool isInPerfectDodgeWindow = false;
    private bool isNextAttackGuaranteedCrit = false;
    private float critBuffEndTime = 0f;

    [Header("Animação / Inclinação Procedural")]
    [Tooltip("Transform do modelo visual do jogador para inclinação procedural.")]
    public Transform visualModelTransform;
    private Quaternion originalVisualLocalRot = Quaternion.identity;
    public float dashForwardLeanAngle = 22f;
    private Coroutine leanCoroutine = null;
    private Coroutine invulEndCoroutine = null;

    [Header("Layer Phasing (Atravessar Inimigos no Dash — Estilo Hades)")]
    [Tooltip("Permite ao jogador atravessar corpos de monstros livremente durante o dash, como no Hades.")]
    public bool enableLayerPhasing = true;
    private Collider playerCollider;
    private List<Collider> phasedEnemyColliders = new List<Collider>();
    private Coroutine restoreCollisionCoroutine = null;

    [Header("Sobrecarga de Esquiva Perfeita (Hyper Charge)")]
    [Tooltip("Duração em segundos que a barra de sobrecarga do Perfect Dash permanece ativa.")]
    public float perfectDodgeChargeDuration = 6.0f;
    private bool hasPerfectDodgeCharge = false;
    private float perfectDodgeChargeTimer = 0f;

    public bool HasPerfectDodgeCharge => hasPerfectDodgeCharge && perfectDodgeChargeTimer > 0f;
    public float PerfectDodgeChargeProgress => Mathf.Clamp01(perfectDodgeChargeTimer / Mathf.Max(0.1f, perfectDodgeChargeDuration));
    public float PerfectDodgeChargeRemainingTime => Mathf.Max(0f, perfectDodgeChargeTimer);

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
    public int MaxDashes => Mathf.Max(1, maxDashes + bonusDashes);
    public bool IsInPerfectDodgeWindow => isDashing && isInPerfectDodgeWindow;
    public bool IsNextAttackGuaranteedCrit => isNextAttackGuaranteedCrit && Time.unscaledTime <= critBuffEndTime;
    public bool IsRecharging => dashesLeft < MaxDashes;

    public void SetBonusDashes(int bonus)
    {
        bonusDashes = bonus;
        InitSlotTimers();
    }

    public float CurrentCooldownTimer
    {
        get
        {
            float minRemaining = float.MaxValue;
            float cd = MaxCooldown;
            int total = MaxDashes;
            for (int i = 0; i < total; i++)
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
            int total = MaxDashes;
            if (dashesLeft >= total) return 1f;
            float maxProg = 0f;
            float cd = Mathf.Max(0.01f, MaxCooldown);
            for (int i = 0; i < total; i++)
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

    private Animator FindAnimator()
    {
        Player_WeaponManager wm = GetComponent<Player_WeaponManager>() ?? GetComponentInParent<Player_WeaponManager>();
        if (wm != null && wm.playerAnimator != null && wm.playerAnimator.isActiveAndEnabled)
        {
            return wm.playerAnimator;
        }
        return GetComponentInChildren<Animator>(false) ?? GetComponentInParent<Animator>();
    }

    private void Awake()
    {
        Instance = this;
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
            if (playerAttributes.dashCounts > maxDashes)
            {
                maxDashes = playerAttributes.dashCounts;
            }
        }

        InitSlotTimers();
        dashesLeft = MaxDashes;
        
        // Buscar PlayerHealth para invulnerabilidade
        playerHealth = GetComponent<PlayerHealth>();
        playerCollider = GetComponent<Collider>();
        
        // Configura modelo visual para inclinação procedural
        if (visualModelTransform == null)
        {
            Transform found = transform.Find("astronaut");
            if (found != null)
            {
                visualModelTransform = found;
            }
            else
            {
                Animator a = FindAnimator();
                if (a != null) visualModelTransform = a.transform;
            }
        }
        if (visualModelTransform != null)
        {
            originalVisualLocalRot = visualModelTransform.localRotation;
        }
        
        // Tenta encontrar UI no inicio
        FindUIReferences();

        // Garante que o DashUI existe no Canvas para exibir as barras modulares
        if (GetComponent<DashUI>() == null && FindFirstObjectByType<DashUI>() == null)
        {
            gameObject.AddComponent<DashUI>();
        }

        // Garante que o Sistema de Cura do Traje do Eptinho esteja ativo
        if (GetComponent<SuitHealingSystem>() == null && FindFirstObjectByType<SuitHealingSystem>() == null)
        {
            gameObject.AddComponent<SuitHealingSystem>();
        }

        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    private void InitSlotTimers()
    {
        int targetMax = MaxDashes;
        float cd = MaxCooldown;

        if (chargeSlotTimers == null || chargeSlotTimers.Length != targetMax)
        {
            float[] oldTimers = chargeSlotTimers;
            chargeSlotTimers = new float[targetMax];
            for (int i = 0; i < targetMax; i++)
            {
                if (oldTimers != null && i < oldTimers.Length)
                {
                    chargeSlotTimers[i] = oldTimers[i];
                }
                else
                {
                    chargeSlotTimers[i] = cd; // Novo slot começa pronto
                }
            }
        }
    }

    private void OnDisable()
    {
        RestoreVisualLeanImmediate();
        RestorePhasingCollisionImmediate();
        if (invulEndCoroutine != null)
        {
            StopCoroutine(invulEndCoroutine);
            invulEndCoroutine = null;
        }
        if (playerHealth != null)
        {
            playerHealth.isInvulnerable = false;
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
            if (!EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_DASH_TUTORIAL))
            {
                EptinhoStoryDialogs.TriggerDashTutorial();
                return;
            }
            lastDashInputTime = Time.time;
        }

        int targetMax = MaxDashes;
        if (chargeSlotTimers == null || chargeSlotTimers.Length != targetMax)
        {
            InitSlotTimers();
        }

        float effectiveCooldown = MaxCooldown;

        // 1. Cada carga carrega separadamente em paralelo
        int readyCount = 0;
        for (int i = 0; i < targetMax; i++)
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

        // 3. Expira a Sobrecarga do Perfect Dash com o tempo (se não for consumida em 6s)
        if (hasPerfectDodgeCharge)
        {
            perfectDodgeChargeTimer -= Time.unscaledDeltaTime;
            if (perfectDodgeChargeTimer <= 0f)
            {
                hasPerfectDodgeCharge = false;
                perfectDodgeChargeTimer = 0f;
                isNextAttackGuaranteedCrit = false;
            }
        }
        else if (isNextAttackGuaranteedCrit && Time.unscaledTime > critBuffEndTime)
        {
            isNextAttackGuaranteedCrit = false;
        }

        HandleDashUI();
    }

    private IEnumerator PerformDash()
    {
        isDashing = true;
        isInPerfectDodgeWindow = true;

        if (invulEndCoroutine != null)
        {
            StopCoroutine(invulEndCoroutine);
            invulEndCoroutine = null;
        }

        // Checagem proativa de Esquiva Perfeita no instante exato da ativação
        bool isPerfectDodgeTriggered = false;
        if (IsAnyImminentAttackNearby(out GameObject imminentAttacker))
        {
            isPerfectDodgeTriggered = true;
            TriggerPerfectDodge(imminentAttacker);
        }

        // Layer Phasing (Atravessar inimigos como fantasma) APENAS no Perfect Dash!
        if (isPerfectDodgeTriggered && enableLayerPhasing)
        {
            if (restoreCollisionCoroutine != null)
            {
                StopCoroutine(restoreCollisionCoroutine);
                restoreCollisionCoroutine = null;
            }
            EnablePhasingCollision();
        }

        // Invulnerabilidade (I-Frames) APENAS no Perfect Dash! No dash normal, o jogador pode tomar dano se for descuidado!
        if (isPerfectDodgeTriggered && playerHealth != null)
        {
            playerHealth.isInvulnerable = true;
        }

        // Se o jogador estiver com a Sobrecarga do Perfect Dash ativa, gasta a sobrecarga como um DASH EXTRA LIVRE!
        if (HasPerfectDodgeCharge)
        {
            ConsumePerfectDodgeCharge();
            Debug.Log("⚡ [DashM] Dash Livre Extra executado usando a Sobrecarga do Perfect Dash!");
        }
        else
        {
            // Consome a carga normal do dash
            float effectiveCooldown = MaxCooldown;
            int targetMax = MaxDashes;
            int slotToUse = -1;
            for (int i = targetMax - 1; i >= 0; i--)
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

        // Alinha a rotação na direção do dash imediatamente
        if (dashDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dashDirection);
        }

        float totalDuration = dashDuration * dashDurationMultiplier;

        // Ativa animação de corrida para movimentar membros
        Animator anim = FindAnimator();
        if (anim != null && anim.isActiveAndEnabled)
        {
            anim.SetFloat("Speed", 1.5f);
        }

        // Inicia inclinação procedural aerodinâmica para a frente
        if (visualModelTransform != null)
        {
            if (leanCoroutine != null) StopCoroutine(leanCoroutine);
            leanCoroutine = StartCoroutine(AnimateDashLean(totalDuration));
        }

        rb.linearVelocity = Vector3.zero; 
        rb.linearVelocity = dashDirection * dashSpeed;
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= perfectDodgeWindowDuration && isInPerfectDodgeWindow)
            {
                isInPerfectDodgeWindow = false;
            }

            // No Dash Normal (sem Perfect Dash): aplica repulsão radial suave lateral para contornar inimigos fluidamente
            if (!isPerfectDodgeTriggered)
            {
                ApplyRadialSoftRepulsion(dashDirection);
            }

            if (anim != null && anim.isActiveAndEnabled)
            {
                anim.SetFloat("Speed", 1.5f);
            }
            yield return null;
        }

        isInPerfectDodgeWindow = false;

        // Restaura inclinação caso a corrotina ainda não tenha finalizado
        RestoreVisualLeanImmediate();

        // Restaura colisões com inimigos se estava em Phasing
        if (isPerfectDodgeTriggered && enableLayerPhasing)
        {
            if (restoreCollisionCoroutine != null) StopCoroutine(restoreCollisionCoroutine);
            restoreCollisionCoroutine = StartCoroutine(RestoreEnemyCollisionDelayed());
        }

        // Finaliza I-Frames do Perfect Dash
        if (isPerfectDodgeTriggered && playerHealth != null)
        {
            playerHealth.isInvulnerable = false;
        }

        // Verifica se o jogador está segurando teclas de movimento WASD ao sair do dash
        bool hasMovementInput = (Input.GetKey(keyRight) || Input.GetKey(keyLeft) || 
                                 Input.GetKey(keyUp) || Input.GetKey(keyDown));

        if (hasMovementInput)
        {
            // Conserva inércia suave na direção do movimento para emendar perfeitamente na corrida contínua
            float exitSpeed = playerMovement != null ? playerMovement.sprintSpeed : 11f;
            rb.linearVelocity = dashDirection * exitSpeed;
        }
        else
        {
            // Se o jogador estava parado (sem segurar WASD), freia imediatamente no chão!
            // Evita o efeito "sabão / patinando no gelo" em pose Idle.
            rb.linearVelocity = Vector3.zero;
            if (anim != null && anim.isActiveAndEnabled)
            {
                anim.SetFloat("Speed", 0f);
            }
        }
        
        // Failsafe garantido de invulnerabilidade ao sair do dash
        if (playerHealth != null)
        {
            float extraInvul = (playerAttributes != null) ? Mathf.Max(0f, playerAttributes.dashInvulnerability) : 0f;
            if (extraInvul > 0f)
            {
                if (invulEndCoroutine != null) StopCoroutine(invulEndCoroutine);
                invulEndCoroutine = StartCoroutine(EndInvulnerabilityAfterDelay(Mathf.Min(extraInvul, 0.25f)));
            }
            else
            {
                playerHealth.isInvulnerable = false;
            }
        }
        
        isDashing = false;
    }

    /// <summary>
    /// Disparado pelo PlayerHealth quando o jogador esquiva com sucesso dentro da janela inicial.
    /// Ativa hit-stop, after-image, restaura 1 dash, acelera cura do Eptinho e concede 100% crítico no próximo golpe.
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

    private void EnablePhasingCollision()
    {
        int playerLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayer >= 0 && enemyLayer >= 0)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }

        if (playerCollider != null)
        {
            Collider[] nearbyCols = Physics.OverlapSphere(transform.position, 16f);
            for (int i = 0; i < nearbyCols.Length; i++)
            {
                Collider col = nearbyCols[i];
                if (col == null || col == playerCollider || col.isTrigger) continue;

                bool isEnemy = (enemyLayer >= 0 && col.gameObject.layer == enemyLayer) ||
                               col.CompareTag("Enemy") ||
                               col.GetComponentInParent<DummyHealth>() != null;

                if (isEnemy)
                {
                    Physics.IgnoreCollision(playerCollider, col, true);
                    phasedEnemyColliders.Add(col);
                }
            }
        }
    }

    private IEnumerator RestoreEnemyCollisionDelayed()
    {
        yield return new WaitForFixedUpdate();

        int playerLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayer >= 0 && enemyLayer >= 0)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }

        if (playerCollider != null && phasedEnemyColliders.Count > 0)
        {
            for (int i = 0; i < phasedEnemyColliders.Count; i++)
            {
                Collider enemyCol = phasedEnemyColliders[i];
                if (enemyCol != null)
                {
                    Physics.IgnoreCollision(playerCollider, enemyCol, false);
                }
            }
            phasedEnemyColliders.Clear();
        }
        restoreCollisionCoroutine = null;
    }

    private void RestorePhasingCollisionImmediate()
    {
        if (restoreCollisionCoroutine != null)
        {
            StopCoroutine(restoreCollisionCoroutine);
            restoreCollisionCoroutine = null;
        }

        int playerLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayer >= 0 && enemyLayer >= 0)
        {
            Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }

        if (playerCollider != null && phasedEnemyColliders.Count > 0)
        {
            for (int i = 0; i < phasedEnemyColliders.Count; i++)
            {
                Collider enemyCol = phasedEnemyColliders[i];
                if (enemyCol != null)
                {
                    Physics.IgnoreCollision(playerCollider, enemyCol, false);
                }
            }
            phasedEnemyColliders.Clear();
        }
    }

    private IEnumerator EndInvulnerabilityAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerHealth != null && !isDashing)
        {
            playerHealth.isInvulnerable = false;
        }
        invulEndCoroutine = null;
    }

    private IEnumerator AnimateDashLean(float duration)
    {
        if (visualModelTransform == null) yield break;
        Quaternion targetRot = originalVisualLocalRot * Quaternion.Euler(dashForwardLeanAngle, 0f, 0f);

        // Interpola rapidamente para a frente (0.04s)
        float leanInTime = 0.04f;
        float elapsed = 0f;
        while (elapsed < leanInTime)
        {
            elapsed += Time.deltaTime;
            visualModelTransform.localRotation = Quaternion.Slerp(originalVisualLocalRot, targetRot, elapsed / leanInTime);
            yield return null;
        }
        visualModelTransform.localRotation = targetRot;

        // Mantém a inclinação durante o corpo do dash
        float holdTime = Mathf.Max(0f, duration - leanInTime - 0.08f);
        if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }

        // Retorna suavemente à rotação original (0.08s)
        float leanOutTime = 0.08f;
        elapsed = 0f;
        while (elapsed < leanOutTime)
        {
            elapsed += Time.deltaTime;
            visualModelTransform.localRotation = Quaternion.Slerp(targetRot, originalVisualLocalRot, elapsed / leanOutTime);
            yield return null;
        }
        visualModelTransform.localRotation = originalVisualLocalRot;
        leanCoroutine = null;
    }

    public void RestoreVisualLeanImmediate()
    {
        if (leanCoroutine != null)
        {
            StopCoroutine(leanCoroutine);
            leanCoroutine = null;
        }
        if (visualModelTransform != null)
        {
            visualModelTransform.localRotation = originalVisualLocalRot;
        }
    }

    /// <summary>
    /// Detecta se existe algum perigo iminente real perto do jogador (projétil inimigo em aproximação ou mob ATACANDO de frente).
    /// Ignora totens pacíficos (como Crystal Tuner) e não dispara se o player estiver nas costas/fora do cone de ataque do inimigo.
    /// </summary>
    public bool IsAnyImminentAttackNearby(out GameObject attacker)
    {
        attacker = null;
        Vector3 playerPos = transform.position;

        // 1. Checa projéteis hostis em aproximação (raio de 5.5m)
        Collider[] hits = Physics.OverlapSphere(playerPos, 5.5f);
        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || hit.transform.IsChildOf(transform)) continue;

                CrystalSpikeProjectile spike = hit.GetComponent<CrystalSpikeProjectile>() ?? hit.GetComponentInParent<CrystalSpikeProjectile>();
                if (spike != null)
                {
                    attacker = spike.gameObject;
                    return true;
                }

                BombaExplosiva bomb = hit.GetComponent<BombaExplosiva>() ?? hit.GetComponentInParent<BombaExplosiva>();
                if (bomb != null)
                {
                    attacker = bomb.gameObject;
                    return true;
                }

                string lower = hit.gameObject.name.ToLower();
                if (lower.Contains("projectile") || lower.Contains("bullet") || lower.Contains("hazard"))
                {
                    attacker = hit.gameObject;
                    return true;
                }
            }
        }

        // 2. Checa inimigos vivos em alcance estrito e atacando/virados para o jogador
        DummyHealth[] enemies = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                DummyHealth enemy = enemies[i];
                if (enemy == null || enemy.CurrentHealth <= 0) continue;

                // Ignora totens e suportes pacíficos que não golpeiam (como Crystal Tuner)
                string eName = enemy.gameObject.name.ToLower();
                if (eName.Contains("tuner") || eName.Contains("totem") || eName.Contains("sintonizador")) continue;

                Vector3 toPlayer = playerPos - enemy.transform.position;
                float dist = toPlayer.magnitude;
                if (dist > 2.4f) continue; // Janela de proximidade mais estrita

                // Checa cone frontal: deve estar encarando diretamente o jogador
                Vector3 toPlayerDir = toPlayer.normalized;
                float facingDot = Vector3.Dot(enemy.transform.forward, toPlayerDir);
                if (facingDot < 0.65f)
                {
                    continue; // Inimigo está de costas ou em ângulo lateral
                }

                attacker = enemy.gameObject;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Separa monstros de forma suave e contida durante o Dash Normal sem travar o jogador nem arremessar inimigos no ar.
    /// </summary>
    private void ApplyRadialSoftRepulsion(Vector3 dashDirection)
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, 1.4f);
        for (int i = 0; i < nearby.Length; i++)
        {
            Collider col = nearby[i];
            if (col == null || col.isTrigger) continue;
            if (col.CompareTag("Enemy") || col.GetComponent<DummyHealth>() != null)
            {
                Rigidbody enemyRb = col.GetComponent<Rigidbody>();
                if (enemyRb != null && !enemyRb.isKinematic)
                {
                    Vector3 diff = col.transform.position - transform.position;
                    diff.y = 0;
                    if (diff.sqrMagnitude > 0.01f)
                    {
                        Vector3 lateral = Vector3.ProjectOnPlane(diff.normalized, dashDirection).normalized;
                        if (lateral == Vector3.zero) lateral = Vector3.Cross(dashDirection, Vector3.up).normalized;

                        // Redução estrita do knockback: aranha ou inimigos saltando nunca são arremessados
                        bool isSpider = col.GetComponent<Spider_AI>() != null;
                        float nudgeForce = isSpider ? 0.35f : 1.2f;

                        enemyRb.AddForce(lateral * nudgeForce, ForceMode.VelocityChange);

                        // Trava a velocidade horizontal máxima para suavizar a separação
                        Vector3 horizontalVel = new Vector3(enemyRb.linearVelocity.x, 0, enemyRb.linearVelocity.z);
                        if (horizontalVel.magnitude > 2.5f)
                        {
                            horizontalVel = horizontalVel.normalized * 2.5f;
                            enemyRb.linearVelocity = new Vector3(horizontalVel.x, enemyRb.linearVelocity.y, horizontalVel.z);
                        }
                    }
                }
            }
        }
    }

    private Volume perfectDodgeBlurVolume;

    private void SetupPerfectDodgeMotionBlurVolume()
    {
        if (perfectDodgeBlurVolume != null) return;
        try
        {
            GameObject volObj = new GameObject("PerfectDodge_MotionBlurVolume");
            DontDestroyOnLoad(volObj);
            perfectDodgeBlurVolume = volObj.AddComponent<Volume>();
            perfectDodgeBlurVolume.isGlobal = true;
            perfectDodgeBlurVolume.priority = 10000f;
            perfectDodgeBlurVolume.weight = 0f;

            VolumeProfile prof = ScriptableObject.CreateInstance<VolumeProfile>();
            prof.name = "PerfectDodge_BlurProfile";
            MotionBlur mb = prof.Add<MotionBlur>(true);
            mb.mode.Override(MotionBlurMode.CameraAndObjects);
            mb.intensity.Override(0.95f);
            mb.quality.Override(MotionBlurQuality.High);
            mb.clamp.Override(0.08f);
            perfectDodgeBlurVolume.profile = prof;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[DashM] Volume URP MotionBlur não pôde ser instanciado: " + ex.Message);
        }
    }

    private IEnumerator DoPerfectDodgeRoutine(GameObject attacker)
    {
        Debug.Log("⚡ [DashM] PERFECT DASH EXECUTADO COM SUCESSO!");

        // 1. Spawna sequência de 5 After-Images translúcidas brancas ao longo do dash (sem ciano)
        StartCoroutine(SpawnAfterImageSequence(5, 0.04f));

        // 2. Hit-Stop / Witch Time: desacelera o tempo suavemente (0.35f)
        Time.timeScale = 0.35f;

        // 3. Motion Blur removido conforme solicitado pelo usuário (sem distorções na tela)
        if (perfectDodgeBlurVolume != null)
        {
            perfectDodgeBlurVolume.weight = 0f;
        }

        // 4. Ativa a Sobrecarga do Perfect Dash (Barra Especial de 3 Escolhas: Crítico / Dash Extra / Cura Q)
        hasPerfectDodgeCharge = true;
        perfectDodgeChargeTimer = perfectDodgeChargeDuration;
        isNextAttackGuaranteedCrit = true;
        critBuffEndTime = Time.unscaledTime + perfectDodgeChargeDuration;

        // 5. Feedback Visual na UI (Banner central de confirmação)
        if (DashUI.Instance != null)
        {
            DashUI.Instance.TriggerPerfectDodgeFlash();
        }

        // 6. Duração de desaceleração reduzida em 10% (de 1.0s para 0.90s)
        yield return new WaitForSecondsRealtime(0.90f);

        // 7. Transição suave de volta para a velocidade normal (0.15s)
        float restoreDuration = 0.15f;
        float restoreElapsed = 0f;
        while (restoreElapsed < restoreDuration)
        {
            restoreElapsed += Time.unscaledDeltaTime;
            float t = restoreElapsed / restoreDuration;
            Time.timeScale = Mathf.Lerp(0.35f, 1.0f, t);
            yield return null;
        }

        Time.timeScale = 1.0f;
        if (perfectDodgeBlurVolume != null)
        {
            perfectDodgeBlurVolume.weight = 0f;
        }
    }

    private IEnumerator SpawnAfterImageSequence(int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnAfterImage();
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    /// <summary>
    /// Spawna uma silhueta translúcida branca etérea (After-Image) do jogador no ponto da esquiva perfeita.
    /// Utiliza shaders compatíveis com URP para máxima visibilidade e persistência sem qualquer tom ciano.
    /// </summary>
    private void SpawnAfterImage()
    {
        SkinnedMeshRenderer[] smrs = GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smrs == null || smrs.Length == 0) return;

        GameObject ghostObj = new GameObject("DashGhost_AfterImage");
        ghostObj.transform.position = Vector3.zero;
        ghostObj.transform.rotation = Quaternion.identity;
        ghostObj.transform.localScale = Vector3.one;

        // Tenta usar URP Unlit para silhueta translúcida limpa; fallback para Sprites/Default
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material ghostMat = new Material(shader);
        Color ghostColor = new Color(1.0f, 1.0f, 1.0f, 0.55f); // Translúcido monocromático neutro (sem ciano)
        ghostMat.color = ghostColor;
        if (ghostMat.HasProperty("_BaseColor")) ghostMat.SetColor("_BaseColor", ghostColor);
        if (ghostMat.HasProperty("_Color")) ghostMat.SetColor("_Color", ghostColor);
        ghostMat.SetFloat("_Surface", 1); // Transparent
        ghostMat.SetFloat("_Blend", 0);
        ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghostMat.SetInt("_ZWrite", 0);
        ghostMat.renderQueue = 3100;

        foreach (var smr in smrs)
        {
            if (smr == null || !smr.gameObject.activeInHierarchy || !smr.enabled) continue;

            Mesh baked = new Mesh();
            smr.BakeMesh(baked);

            GameObject part = new GameObject($"GhostPart_{smr.gameObject.name}");
            part.transform.SetParent(ghostObj.transform, false);
            part.transform.position = smr.transform.position;
            part.transform.rotation = smr.transform.rotation;
            part.transform.localScale = smr.transform.lossyScale;

            MeshFilter mf = part.AddComponent<MeshFilter>();
            mf.mesh = baked;

            MeshRenderer mr = part.AddComponent<MeshRenderer>();
            mr.material = ghostMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        StartCoroutine(FadeAndDestroyGhost(ghostObj, ghostMat, 0.90f));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, Material mat, float duration)
    {
        float elapsed = 0f;
        Color initialColor = mat != null ? mat.color : new Color(1f, 1f, 1f, 0.55f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(initialColor.a, 0f, t);
            if (mat != null)
            {
                Color c = initialColor;
                c.a = alpha;
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
            yield return null;
        }

        if (ghost != null) Destroy(ghost);
    }

    /// <summary>
    /// Consome o bônus de crítico garantido ao desferir um golpe.
    /// </summary>
    /// <summary>
    /// Consome a Sobrecarga do Perfect Dash (quando gasta em Dash Livre, Ataque Crítico ou Síntese Q).
    /// </summary>
    public bool ConsumePerfectDodgeCharge()
    {
        if (hasPerfectDodgeCharge && perfectDodgeChargeTimer > 0f)
        {
            hasPerfectDodgeCharge = false;
            perfectDodgeChargeTimer = 0f;
            isNextAttackGuaranteedCrit = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consome o bônus de crítico garantido ao desferir um golpe (com Adaga, Machado ou Tiro).
    /// </summary>
    public bool ConsumeGuaranteedCrit()
    {
        if (HasPerfectDodgeCharge || (isNextAttackGuaranteedCrit && Time.unscaledTime <= critBuffEndTime))
        {
            ConsumePerfectDodgeCharge();
            return true;
        }
        return false;
    }

    private void HandleDashUI()
    {
        if (dashCountText != null)
        {
            int targetMax = MaxDashes;
            if (dashesLeft < targetMax)
            {
                float remaining = CurrentCooldownTimer;
                dashCountText.text = $"Dash: {dashesLeft}/{targetMax} ({remaining:F1}s)";
            }
            else
            {
                dashCountText.text = $"Dash: {dashesLeft}/{targetMax}";
            }
        }
    }

    public void RechargeDashes(int amount)
    {
        float cd = MaxCooldown;
        int targetMax = MaxDashes;
        for (int count = 0; count < amount; count++)
        {
            int slotToRestore = -1;
            float lowestTime = float.MaxValue;
            for (int i = 0; i < targetMax; i++)
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
        dashesLeft = Mathf.Min(targetMax, dashesLeft + amount);
        Debug.Log($"Dashes recarregados! Total agora: {dashesLeft}/{targetMax}");
    }
}
