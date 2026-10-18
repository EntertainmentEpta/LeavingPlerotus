using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PrimaryAttackKnife attackScript;
    private Animator anim;
    private Rigidbody playerRb;
    private Ultimate_Axe cachedAxeUlt;

    private void Awake()
    {
        FindAttackScript();
        anim = GetComponent<Animator>();
        playerRb = GetComponentInParent<Rigidbody>();
    }

    private void OnAnimatorMove()
    {
        // Durante a Ultimate, não interfere com o salto da habilidade
        PlayerUltimate ult = GetComponentInParent<PlayerUltimate>() ?? GetComponent<PlayerUltimate>();
        if (ult != null && ult.IsUltimateActive()) return;

        if (anim == null) anim = GetComponent<Animator>();
        if (playerRb == null) playerRb = GetComponentInParent<Rigidbody>() ?? GetComponent<Rigidbody>();

        if (anim != null && anim.applyRootMotion && playerRb != null)
        {
            // Repassa o deslocamento do Root Motion da animação para a física do Rigidbody pai
            if (anim.deltaPosition.sqrMagnitude > 0.000001f)
            {
                playerRb.MovePosition(playerRb.position + anim.deltaPosition);
            }
            if (anim.deltaRotation != Quaternion.identity)
            {
                playerRb.MoveRotation(playerRb.rotation * anim.deltaRotation);
            }
        }
    }

    private void FindAttackScript()
    {
        if (attackScript == null)
        {
            attackScript = GetComponentInParent<PrimaryAttackKnife>();
            if (attackScript == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    attackScript = playerObj.GetComponentInChildren<PrimaryAttackKnife>();
                }
            }
        }
    }

    /// <summary>
    /// Busca e faz cache da referência ao Ultimate_Axe na hierarquia do Player.
    /// </summary>
    private Ultimate_Axe FindUltimateAxe()
    {
        if (cachedAxeUlt != null) return cachedAxeUlt;

        cachedAxeUlt = GetComponentInParent<Ultimate_Axe>() ?? GetComponent<Ultimate_Axe>();
        if (cachedAxeUlt == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                cachedAxeUlt = player.GetComponentInChildren<Ultimate_Axe>() ?? player.GetComponent<Ultimate_Axe>();
        }

        return cachedAxeUlt;
    }

    public void EnableHitbox()
    {
        FindAttackScript();
        if (attackScript != null)
        {
            attackScript.EnableHitbox();
        }
    }

    public void DisableHitbox()
    {
        FindAttackScript();
        if (attackScript != null)
        {
            attackScript.DisableHitbox();
        }
    }

    public void OpenAttackWindow()
    {
        FindAttackScript();
        if (attackScript != null)
        {
            attackScript.OpenAttackWindow();
        }
    }

    // Esta função vai estar no objeto FILHO (astronauta), junto com o Animator
    public void HandleReviveCompletion()
    {
        PlayerHealth healthScript = GetComponentInParent<PlayerHealth>();
        if (healthScript == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                healthScript = playerObj.GetComponent<PlayerHealth>() ?? playerObj.GetComponentInChildren<PlayerHealth>();
            }
        }
        if (healthScript == null)
        {
            healthScript = Object.FindFirstObjectByType<PlayerHealth>();
        }
        
        if (healthScript != null)
        {
            healthScript.HandleReviveCompletion();
        }
        else
        {
            Debug.LogError("PlayerAnimationEvents: Não encontrei o PlayerHealth no pai!");
        }
    }

    /// <summary>
    /// Evento de animação para o impacto da onda de choque do Machado (Suporta maiúsculas e minúsculas).
    /// </summary>
    public void OnAxeSlam() => OnAxeSlamImpact();
    public void onaxeslam() => OnAxeSlamImpact();
    public void onaxeslamimpact() => OnAxeSlamImpact();

    public void OnAxeSlamImpact()
    {
        Debug.Log("[PlayerAnimationEvents] Repassando evento 'OnAxeSlamImpact' para o Ultimate_Axe...");
        Ultimate_Axe axeUlt = FindUltimateAxe();

        if (axeUlt != null)
        {
            axeUlt.TriggerAxeSlamImpact();
        }
        else
        {
            Debug.LogError("[PlayerAnimationEvents] ERRO: Ultimate_Axe não encontrado no Player!");
        }
    }

    // ── Ponte de SFX para Animation Events (Combo Primário do Machado) ──────
    public void PlayAxeSwing1() { FindAttackScript(); if (attackScript != null) attackScript.PlaySwing1(); }
    public void PlayAxeSwing2() { FindAttackScript(); if (attackScript != null) attackScript.PlaySwing2(); }
    public void PlayAxeSwing3() { FindAttackScript(); if (attackScript != null) attackScript.PlaySwing3(); }
    public void PlayAxeSwing4() { FindAttackScript(); if (attackScript != null) attackScript.PlaySwing4(); }

    // ── Ponte de SFX para Animation Events (Ultimate do Machado) ──────

    /// <summary>
    /// Ponte: Toca o som de carregamento da Ultimate do Machado. Chamado via Animation Event.
    /// </summary>
    public void PlayChargeSFX()
    {
        Ultimate_Axe axeUlt = FindUltimateAxe();
        if (axeUlt != null)
            axeUlt.PlayChargeSFX();
        else
            Debug.LogWarning("[PlayerAnimationEvents] PlayChargeSFX: Ultimate_Axe NÃO encontrado!");
    }

    /// <summary>
    /// Ponte: Toca o som de impacto no chão da Ultimate do Machado. Chamado via Animation Event.
    /// </summary>
    public void PlayGroundImpactSFX()
    {
        Ultimate_Axe axeUlt = FindUltimateAxe();
        if (axeUlt != null)
            axeUlt.PlayGroundImpactSFX();
        else
            Debug.LogWarning("[PlayerAnimationEvents] PlayGroundImpactSFX: Ultimate_Axe NÃO encontrado!");
    }

    /// <summary>
    /// Ponte: Toca o som de estilhaçamento de cristais da Ultimate do Machado. Chamado via Animation Event.
    /// </summary>
    public void PlayCrystalShatterSFX()
    {
        Ultimate_Axe axeUlt = FindUltimateAxe();
        if (axeUlt != null)
            axeUlt.PlayCrystalShatterSFX();
        else
            Debug.LogWarning("[PlayerAnimationEvents] PlayCrystalShatterSFX: Ultimate_Axe NÃO encontrado!");
    }

    /// <summary>
    /// Evento de animação para o fim da sequência do Ultimate (retorno ao Idle).
    /// </summary>
    public void EndUltimateSequence()
    {
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        PlayerUltimate ultManager = GetComponentInParent<PlayerUltimate>() ?? GetComponent<PlayerUltimate>();
        if (ultManager == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) ultManager = player.GetComponentInChildren<PlayerUltimate>();
        }

        if (ultManager != null)
        {
            ultManager.EndUltimateSequence();
        }
    }
}