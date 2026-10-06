using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Gerenciador Central de Ultimate do Jogador (Core Hub).
/// Captura a tecla U, verifica se a arma esta empunhada e dispara o gatilho da animacao.
///
/// INVARIANTE DE SEGURANCA DO NAVMESHAGENT:
///   Durante o salto aereo, NUNCA desativamos agent.enabled durante o gameplay.
///   Em vez disso, suspendemos agent.updatePosition e agent.updateRotation,
///   mantendo o agente vivo e ancorado na malha - sem erros "Agent must be on NavMesh".
///   Ao aterrissar, usamos NavMesh.SamplePosition + agent.Warp() para reposicionar
///   atomicamente o agente no ponto valido mais proximo antes de restaurar o controle.
///
///   Unica excecao: GameManager usa agent.enabled = false/true em trocas de cena,
///   o que e seguro porque o NavMesh e reconstruido antes de agent.enabled = true.
/// </summary>
public class PlayerUltimate : MonoBehaviour
{
    [Header("Ultimate Settings")]
    [Tooltip("Tecla de ativacao")]
    public KeyCode ultimateKey = KeyCode.U;

    [Tooltip("Tempo de recarga do Ultimate em segundos")]
    public float ultimateCooldown = 20f;

    [Tooltip("Impulso fisico para a frente durante o pulo do Ultimate")]
    public float forwardLeapImpulse = 6.0f;

    [Tooltip("Invencibilidade durante o pulo/slam do Ultimate?")]
    public bool grantInvulnerability = true;

    [Tooltip("Raio de busca (metros) para a posicao de aterrissagem valida no NavMesh.")]
    public float navMeshLandingSearchRadius = 4.0f;

    [Header("Status (Read Only)")]
    [SerializeField] private bool isUltimateReady = true;
    [SerializeField] private bool isUltimateActive = false;
    [SerializeField] private float currentCooldown = 0f;

    private PlayerHealth playerHealth;
    private Player_WeaponManager weaponManager;
    private PrimaryAttackKnife attackScript;
    private Rigidbody playerRb;
    private Animator animator;
    private NavMeshAgent navAgent;
    private Vector3 safeStartingPosition;

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreNavAgentControl();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        RebindReferences();
    }

    public void RebindReferences()
    {
        isUltimateActive = false;
        RestoreNavAgentControl();

        navAgent      = GetComponent<NavMeshAgent>()        ?? GetComponentInParent<NavMeshAgent>()        ?? GetComponentInChildren<NavMeshAgent>();
        playerHealth  = GetComponent<PlayerHealth>()         ?? GetComponentInParent<PlayerHealth>();
        playerRb      = GetComponent<Rigidbody>()            ?? GetComponentInParent<Rigidbody>();
        weaponManager = GetComponent<Player_WeaponManager>() ?? GetComponentInChildren<Player_WeaponManager>();
        attackScript  = GetComponent<PrimaryAttackKnife>()   ?? GetComponentInChildren<PrimaryAttackKnife>();

        if (weaponManager != null && weaponManager.playerAnimator != null && weaponManager.playerAnimator.isActiveAndEnabled)
            animator = weaponManager.playerAnimator;
        else
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            PlayerAnimationEvents animEvents = animator.GetComponent<PlayerAnimationEvents>();
            if (animEvents == null)
                animator.gameObject.AddComponent<PlayerAnimationEvents>();
        }

        Ultimate_Axe axeUlt = GetComponent<Ultimate_Axe>() ?? GetComponentInChildren<Ultimate_Axe>();
        if (axeUlt == null) gameObject.AddComponent<Ultimate_Axe>();

        UltimateUI ui = GetComponent<UltimateUI>() ?? GetComponentInChildren<UltimateUI>();
        if (ui == null) gameObject.AddComponent<UltimateUI>();
    }

    void Start()
    {
        RebindReferences();
    }

    void Update()
    {
        if (!isUltimateReady && !isUltimateActive)
        {
            currentCooldown -= Time.deltaTime;
            if (currentCooldown <= 0f)
            {
                isUltimateReady = true;
                currentCooldown = 0f;
                Debug.Log("[PlayerUltimate] ULTIMATE PRONTO! Pressione U para ativar.");
            }
        }

        if (!CheatConsole.IsOpen && (Input.GetKeyDown(ultimateKey) || Input.GetKeyDown(KeyCode.U)))
        {
            Debug.Log($"[PlayerUltimate] Tecla U pressionada! Ready={isUltimateReady}, Active={isUltimateActive}, CD={currentCooldown:F1}s");

            if (!isUltimateReady)
            {
                Debug.LogWarning($"[PlayerUltimate] Ultimate em recarga! Aguarde {currentCooldown:F1}s.");
                return;
            }
            if (isUltimateActive)
            {
                Debug.LogWarning("[PlayerUltimate] Ultimate ja esta em execucao!");
                return;
            }
            ActivateUltimate();
        }
    }

    public void ActivateUltimate()
    {
        Debug.Log("[PlayerUltimate] ActivateUltimate() iniciado!");
        RebindReferences();

        // PASSO 1: Grava posicao segura de decolagem
        safeStartingPosition = transform.position;

        // PASSO 2: Suspende o controle do NavMeshAgent SEM desativa-lo
        // updatePosition=false: agente permanece vivo ancorado no piso da NavMesh.
        // Elimina "Agent must be on NavMesh" e impede o agente de puxar o transform
        // para o chao enquanto a parabola eleva o jogador no eixo Y.
        SuspendNavAgentControl();

        PrimaryAttackKnife primaryAttack = GetComponent<PrimaryAttackKnife>()
            ?? GetComponentInChildren<PrimaryAttackKnife>()
            ?? GetComponentInParent<PrimaryAttackKnife>();
        if (primaryAttack != null && primaryAttack.isAttacking)
            primaryAttack.CancelAttackForDash();

        // PASSO 3: Verificacao e ativacao de arma automatica
        if (weaponManager != null)
        {
            if (weaponManager.currentWeapon != null && !weaponManager.currentWeapon.activeInHierarchy)
            {
                weaponManager.currentWeapon.SetActive(true);
                weaponManager.isWeaponDrawn = true;
            }
            if (!weaponManager.isWeaponDrawn && weaponManager.currentWeapon != null)
                weaponManager.isWeaponDrawn = true;
        }

        Debug.Log("[PlayerUltimate] ULTIMATE DISPARADO!");
        isUltimateActive = true;
        isUltimateReady  = false;
        currentCooldown  = ultimateCooldown;

        if (grantInvulnerability && playerHealth != null)
            playerHealth.isInvulnerable = true;

        if (weaponManager != null && weaponManager.playerAnimator != null && weaponManager.playerAnimator.isActiveAndEnabled)
            animator = weaponManager.playerAnimator;
        else if (animator == null || !animator.isActiveAndEnabled)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            Debug.Log($"[PlayerUltimate] Animator: '{animator.gameObject.name}'. Buscando parametros...");
            bool triggerFound = false;
            foreach (var param in animator.parameters)
            {
                if (param.name.Equals("Ult",      System.StringComparison.OrdinalIgnoreCase) ||
                    param.name.Equals("Ultimate", System.StringComparison.OrdinalIgnoreCase) ||
                    param.name.Equals("JumpAxe",  System.StringComparison.OrdinalIgnoreCase) ||
                    param.name.Equals("UltAxe",   System.StringComparison.OrdinalIgnoreCase))
                {
                    animator.ResetTrigger(param.name);
                    animator.SetTrigger(param.name);
                    triggerFound = true;
                    Debug.Log($"[PlayerUltimate] GATILHO '{param.name}' DISPARADO!");
                }
            }
            if (!triggerFound)
            {
                Debug.LogWarning("[PlayerUltimate] Parametro 'Ult' nao encontrado. Forcando...");
                animator.ResetTrigger("Ult");
                animator.SetTrigger("Ult");
            }
        }
        else
        {
            Debug.LogError("[PlayerUltimate] ERRO CRITICO: Nenhum Animator encontrado no Player!");
        }

        if (attackScript != null)
            attackScript.SetTrailsEmitting(true);

        StopAllCoroutines();
        ExecuteWeaponSpecificUltimate();
        StartCoroutine(AutomaticUnlockCoroutine());
        StartCoroutine(WatchdogSafetyCoroutine());
    }

    // ========================================================================
    // NavMeshAgent: Suspensao / Restauracao
    // ========================================================================

    /// <summary>
    /// Suspende a autoridade do NavMeshAgent sobre o transform SEM desativa-lo.
    /// O agente permanece vivo na NavMesh como ancora invisivel durante o voo.
    /// Idempotente.
    /// </summary>
    private void SuspendNavAgentControl()
    {
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>() ?? GetComponentInParent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>();

        if (navAgent != null && navAgent.enabled)
        {
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;

            if (navAgent.isOnNavMesh)
            {
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }

            Debug.Log("[PlayerUltimate] NavMeshAgent suspenso (updatePosition=false). Ancora na NavMesh mantida.");
        }
    }

    /// <summary>
    /// Restaura o controle do NavMeshAgent apos o aterrissamento.
    /// Usa NavMesh.SamplePosition + Warp() para garantir posicao 100% valida na malha.
    ///
    /// ORDEM OBRIGATORIA: Warp() ANTES de updatePosition=true.
    /// Idempotente.
    /// </summary>
    private void RestoreNavAgentControl()
    {
        if (navAgent == null) return;
        if (!navAgent.enabled) return;
        if (navAgent.updatePosition) return; // Ja em controle normal.

        bool navMeshExistsInScene = NavMesh.SamplePosition(
            safeStartingPosition, out _, navMeshLandingSearchRadius + 1.0f, NavMesh.AllAreas);

        if (!navMeshExistsInScene)
        {
            // Cena sem NavMesh (Base): restaura as flags sem Warp.
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;
            Debug.Log("[PlayerUltimate] Cena sem NavMesh. Controle do agente restaurado sem Warp.");
            return;
        }

        Vector3 landingPos = transform.position;

        if (NavMesh.SamplePosition(landingPos, out NavMeshHit landingHit, navMeshLandingSearchRadius, NavMesh.AllAreas))
        {
            // Ponto valido: Warp atomico - nunca gera "must be on NavMesh".
            navAgent.Warp(landingHit.position);

            Vector3 snappedPos = landingHit.position;
            transform.position = snappedPos;
            if (playerRb != null)
            {
                playerRb.position = snappedPos;
                playerRb.linearVelocity = Vector3.zero;
            }
            Debug.Log($"[PlayerUltimate] Aterrissagem clampeada para NavMesh em {snappedPos}. Controle restaurado.");
        }
        else
        {
            // SALVA-VIDAS: aterrissagem fora da NavMesh (abismo / atravessou parede).
            Debug.LogWarning($"[PlayerUltimate] Aterrissagem em {landingPos} fora da NavMesh! Executando salva-vidas.");

            Vector3 fallbackPos = safeStartingPosition;
            if (NavMesh.SamplePosition(safeStartingPosition, out NavMeshHit safeHit, 5.0f, NavMesh.AllAreas))
                fallbackPos = safeHit.position;

            transform.position = fallbackPos;
            if (playerRb != null)
            {
                playerRb.position = fallbackPos;
                playerRb.linearVelocity = Vector3.zero;
            }
            navAgent.Warp(fallbackPos);
            Debug.LogWarning($"[PlayerUltimate] Salva-vidas: jogador reposicionado para {fallbackPos}.");
        }

        // ORDEM CRITICA: updatePosition=true SOMENTE apos o Warp completo.
        navAgent.updatePosition = true;
        navAgent.updateRotation = true;
    }

    // ========================================================================
    // Corrotinas
    // ========================================================================

    private IEnumerator AutomaticUnlockCoroutine()
    {
        float waitStart = Time.time;
        while (animator != null && !animator.GetCurrentAnimatorStateInfo(0).IsName("Ult") && (Time.time - waitStart) < 0.3f)
            yield return null;

        float duration = 2.6f;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.length > 0.2f)
            {
                float speed = stateInfo.speed > 0.01f ? stateInfo.speed : 1f;
                duration = stateInfo.length / speed;
            }
        }

        yield return new WaitForSeconds(duration);

        if (isUltimateActive)
        {
            Debug.Log($"[PlayerUltimate] Fim da animacao ({duration:F2}s). Destravando controles!");
            EndUltimateSequence();
        }
    }

    /// <summary>
    /// Watchdog: libera os controles mesmo se a animacao sofrer lag ou o evento nao disparar.
    /// </summary>
    private IEnumerator WatchdogSafetyCoroutine()
    {
        yield return new WaitForSeconds(3.2f);
        if (isUltimateActive)
        {
            Debug.LogWarning("[PlayerUltimate] WATCHDOG: 3.2s atingidos! Forcando finalizacao.");
            EndUltimateSequence();
        }
    }

    // ========================================================================
    // Dispatch
    // ========================================================================

    private void ExecuteWeaponSpecificUltimate()
    {
        WeaponType activeType = GetEquippedWeaponType();

        if (activeType == WeaponType.Axe)
        {
            Ultimate_Axe axeUlt = GetComponent<Ultimate_Axe>() ?? GetComponentInChildren<Ultimate_Axe>() ?? GetComponentInParent<Ultimate_Axe>();
            if (axeUlt != null)
            {
                Debug.Log("[PlayerUltimate] Machado equipado! Chamando Ultimate_Axe...");
                axeUlt.ExecuteUltimate();
            }
            else Debug.LogError("[PlayerUltimate] Componente Ultimate_Axe NAO ENCONTRADO!");
        }
        else if (activeType == WeaponType.Dagger)
        {
            Ultimate_Dagger daggerUlt = GetComponent<Ultimate_Dagger>() ?? GetComponentInChildren<Ultimate_Dagger>() ?? GetComponentInParent<Ultimate_Dagger>();
            if (daggerUlt != null)
            {
                Debug.Log("[PlayerUltimate] Adaga equipada! Chamando Ultimate_Dagger...");
                daggerUlt.ExecuteUltimate();
            }
            else Debug.LogError("[PlayerUltimate] Componente Ultimate_Dagger NAO ENCONTRADO!");
        }
    }

    private WeaponType GetEquippedWeaponType()
    {
        if (weaponManager != null && weaponManager.currentWeapon != null)
        {
            WeaponOffset offset = weaponManager.currentWeapon.GetComponent<WeaponOffset>();
            if (offset != null) return offset.weaponType;
        }
        Debug.LogWarning("[PlayerUltimate] Arma nao identificada. Assumindo Axe.");
        return WeaponType.Axe;
    }

    private IEnumerator FallbackUnlockCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isUltimateActive) EndUltimateSequence();
    }

    // ========================================================================
    // End Sequence
    // ========================================================================

    /// <summary>
    /// Chamado pelo evento de animacao ou apos o impacto do slam.
    ///
    /// ORDEM OBRIGATORIA:
    ///   1. isUltimateActive = false     -> PlayerM.FixedUpdate pode processar novamente.
    ///   2. RestoreNavAgentControl()     -> Warp clampeado + updatePosition=true.
    ///   3. Limpeza de estado            -> invencibilidade, animacao, trail.
    /// </summary>
    public void EndUltimateSequence()
    {
        if (!isUltimateActive) return;

        Debug.Log("[PlayerUltimate] Finalizando Ultimate. Controle liberado!");
        isUltimateActive = false;

        RestoreNavAgentControl();

        if (grantInvulnerability && playerHealth != null)
            playerHealth.isInvulnerable = false;

        if (animator != null)
        {
            animator.ResetTrigger("Ult");
            animator.SetInteger("ComboStep", 0);
            animator.applyRootMotion = false;
        }

        if (attackScript != null)
        {
            attackScript.SetTrailsEmitting(false);
            attackScript.ResetCombo();
        }
    }

    // ========================================================================
    // API Publica
    // ========================================================================

    public bool  IsUltimateReady()      => isUltimateReady;
    public bool  IsUltimateActive()     => isUltimateActive;
    public float GetCooldownRemaining() => currentCooldown;
    public float GetCooldownProgress()  => isUltimateReady ? 1f : (1f - (currentCooldown / ultimateCooldown));
}
