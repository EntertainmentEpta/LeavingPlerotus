using UnityEngine;

/// <summary>
/// Script ponte de Eventos de Animação.
/// Deve ser anexado no mesmo GameObject filho onde fica o componente ANIMATOR.
/// Permite que o Unity exiba todas as funções no menu dropdown do Animation Event Inspector!
/// </summary>
public class BossAnimationEvents : MonoBehaviour
{
    private BossController bossController;

    private void Awake()
    {
        bossController = GetComponentInParent<BossController>();
    }

    private BossController GetBoss()
    {
        if (bossController == null)
            bossController = GetComponentInParent<BossController>();
        return bossController;
    }

    // =========================================================================
    // FUNÇÕES EXPOSTAS NO DROPDOWN DO ANIMATOR DO UNITY
    // =========================================================================

    // TRAIL APENAS (No início do balanço)
    public void AnimEvent_EnableTrailOnlyRight()
    {
        var boss = GetBoss();
        if (boss != null && boss.rightHandHitbox != null) boss.rightHandHitbox.OnAnimationEvent_EnableTrailOnly();
    }

    public void AnimEvent_EnableTrailOnlyLeft()
    {
        var boss = GetBoss();
        if (boss != null && boss.leftHandHitbox != null) boss.leftHandHitbox.OnAnimationEvent_EnableTrailOnly();
    }

    public void AnimEvent_EnableTrailOnlyBoth()
    {
        var boss = GetBoss();
        if (boss != null)
        {
            if (boss.rightHandHitbox != null) boss.rightHandHitbox.OnAnimationEvent_EnableTrailOnly();
            if (boss.leftHandHitbox != null) boss.leftHandHitbox.OnAnimationEvent_EnableTrailOnly();
        }
    }

    // DESLIGAR TRAIL APENAS (Para apagar o rastro no frame exato)
    public void AnimEvent_DisableTrailOnlyRight()
    {
        var boss = GetBoss();
        if (boss != null && boss.rightHandHitbox != null) boss.rightHandHitbox.OnAnimationEvent_DisableTrailOnly();
    }

    public void AnimEvent_DisableTrailOnlyLeft()
    {
        var boss = GetBoss();
        if (boss != null && boss.leftHandHitbox != null) boss.leftHandHitbox.OnAnimationEvent_DisableTrailOnly();
    }

    public void AnimEvent_DisableTrailOnlyBoth()
    {
        var boss = GetBoss();
        if (boss != null)
        {
            if (boss.rightHandHitbox != null) boss.rightHandHitbox.OnAnimationEvent_DisableTrailOnly();
            if (boss.leftHandHitbox != null) boss.leftHandHitbox.OnAnimationEvent_DisableTrailOnly();
        }
    }

    // HITBOX + DANO (No momento do impacto)
    public void AnimEvent_EnableRightHand()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_EnableRightHand();
    }

    public void AnimEvent_DisableRightHand()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_DisableRightHand();
    }

    public void AnimEvent_EnableLeftHand()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_EnableLeftHand();
    }

    public void AnimEvent_DisableLeftHand()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_DisableLeftHand();
    }

    public void AnimEvent_EnableBothHands()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_EnableBothHands();
    }

    public void AnimEvent_DisableBothHands()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_DisableBothHands();
    }

    public void AnimEvent_GroundImpact()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_GroundImpact();
    }

    public void AnimEvent_StompImpact()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_StompImpact();
    }

    public void AnimEvent_JumpImpact()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_JumpImpact();
    }

    public void AnimEvent_SnapToGround()
    {
        var boss = GetBoss();
        if (boss != null) boss.SnapToGround();
    }

    // =========================================================================
    // 🪄 MAGIAS E BUFFS (SPELL CAST, WIDE SPELL, POWERUP)
    // =========================================================================
    public void AnimEvent_CastSpell()
    {
        var boss = GetBoss();
        if (boss != null) boss.CastSpell();
    }

    public void AnimEvent_SpellCast()
    {
        var boss = GetBoss();
        if (boss != null) boss.CastSpell();
    }

    public void AnimEvent_SpellGround()
    {
        var boss = GetBoss();
        if (boss != null) boss.AnimEvent_GroundImpact();
    }

    public void AnimEvent_PowerUp()
    {
        var boss = GetBoss();
        if (boss != null) boss.TriggerPowerUP(2.5f);
    }

    // =========================================================================
    // 🌱 FASE 3 - HOOKS PARA O SERRALHA (CUSPE ÁCIDO & SALVA DE ESPINHOS)
    // =========================================================================
    public void AnimEvent_AcidSpit()
    {
        var boss = GetBoss();
        if (boss != null) boss.TriggerAcidSpit();
    }

    public void AnimEvent_ThornVolley()
    {
        var boss = GetBoss();
        if (boss != null) boss.TriggerThornVolley();
    }

    // =========================================================================
    // 🌿 ESTIRAMENTO PROCEDURAL DE BRAÇOS (BOSS ARM STRETCH)
    // =========================================================================
    public void AnimEvent_StretchRightArm()
    {
        var boss = GetBoss();
        if (boss != null)
        {
            var stretch = boss.GetComponent<BossArmStretch>() ?? boss.GetComponentInChildren<BossArmStretch>();
            if (stretch != null) stretch.StretchRightArm();
        }
    }

    public void AnimEvent_StretchLeftArm()
    {
        var boss = GetBoss();
        if (boss != null)
        {
            var stretch = boss.GetComponent<BossArmStretch>() ?? boss.GetComponentInChildren<BossArmStretch>();
            if (stretch != null) stretch.StretchLeftArm();
        }
    }

    public void AnimEvent_StretchBothArms()
    {
        var boss = GetBoss();
        if (boss != null)
        {
            var stretch = boss.GetComponent<BossArmStretch>() ?? boss.GetComponentInChildren<BossArmStretch>();
            if (stretch != null) stretch.StretchBothArms();
        }
    }

    private void OnAnimatorMove()
    {
        var boss = GetBoss();
        if (boss != null)
        {
            boss.OnChildAnimatorMove(GetComponent<Animator>());
        }
    }
}
