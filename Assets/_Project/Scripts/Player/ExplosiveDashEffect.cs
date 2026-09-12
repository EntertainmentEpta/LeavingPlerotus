using UnityEngine;
using System.Collections;

/// <summary>
/// Efeito Especial T4: Dash Explosivo (SharpItem4)
/// 
/// Quando ativo, modifica o dash do player:
///   1. Dash fica MAIS LONGO (multiplica a duração)
///   2. No FINAL do dash, dispara uma EXPLOSÃO AoE com dano e knockback
///   3. O VFX do dash muda para um prefab enhanced
/// 
/// TODOS os parâmetros são expostos no Inspector para tuning em playtesting.
/// 
/// USO:
///   1. Adicione este componente ao Player (junto com DashM)
///   2. Configure os valores no Inspector
///   3. O Tier4EffectManager ativa/desativa automaticamente ao infundir/remover o SharpItem4
/// 
/// DANO:
///   Usa OverlapSphere no final do dash (mesmo padrão do Ultimate_Axe)
///   Aplica dano em DummyHealth e ShardSwarmHealth
///   Knockback via Rigidbody.AddForce (ForceMode.Impulse)
/// </summary>
public class ExplosiveDashEffect : MonoBehaviour
{
    [Header("═══ Estado ═══")]
    [Tooltip("Se o efeito está ativo (ativado via Tier4EffectManager ao infundir SharpItem4).")]
    [SerializeField] private bool isActive = false;

    [Header("═══ Extensão do Dash ═══")]
    [Tooltip("Multiplicador de duração do dash. 1.0 = normal, 1.5 = 50% mais longo, 2.0 = dobro.\n" +
             "Valores recomendados: 1.3 ~ 2.0")]
    [Range(1.0f, 3.0f)]
    public float dashDurationMultiplier = 1.5f;

    [Header("═══ Explosão AoE ═══")]
    [Tooltip("Raio da explosão AoE no ponto final do dash (em unidades Unity).\n" +
             "Para referência: o raio do Ultimate_Axe shockwave é ~5.0")]
    [Range(1.0f, 10.0f)]
    public float explosionRadius = 4.0f;

    [Tooltip("Dano base da explosão aplicado a cada inimigo na área.\n" +
             "Para referência: dano do combo da Adaga é 30-40 por hit.")]
    public int explosionDamage = 50;

    [Tooltip("Força de knockback da explosão empurrando inimigos para longe.\n" +
             "Para referência: knockback do Ultimate_Axe é 12.0")]
    [Range(1.0f, 30.0f)]
    public float explosionKnockback = 12.0f;

    [Tooltip("Componente vertical do knockback (força para cima, arremessa inimigos).\n" +
             "0 = empurra só horizontalmente. 0.3 = leve arremesso. 1.0 = arremesso forte.")]
    [Range(0f, 2.0f)]
    public float upwardKnockback = 0.3f;

    [Header("═══ VFX da Explosão ═══")]
    [Tooltip("Tipo de VFX para a explosão final do dash. Registrado no VFXManager.\n" +
             "Use DashExplosion para VFX dedicado, ou WWExplosionVariant1 como placeholder.")]
    public VFXType explosionVFXType = VFXType.DashExplosion;

    [Tooltip("Escala do VFX da explosão. Aumente para efeitos mais grandiosos.")]
    [Range(0.5f, 5.0f)]
    public float explosionVFXScale = 1.5f;

    [Header("═══ VFX do Dash Enhanced ═══")]
    [Tooltip("Tipo de VFX do trail do dash melhorado (substitui o VFX padrão do PlayerDashVFX).\n" +
             "Registre o prefab no VFXManager com este tipo.")]
    public VFXType enhancedDashVFXType = VFXType.PlayerDashEnhanced;

    [Header("═══ Debug ═══")]
    [Tooltip("Se true, desenha o raio da explosão como Gizmo na Scene view para tuning visual.")]
    public bool showExplosionGizmo = true;

    [Tooltip("Cor do Gizmo da explosão na Scene view.")]
    public Color gizmoColor = new Color(1f, 0.3f, 0f, 0.3f);

    // Referências internas (auto-detectadas)
    private DashM dashScript;
    private PlayerDashVFX dashVFX;
    private bool wasDashing = false;
    private Vector3 lastDashEndPosition;

    private void Awake()
    {
        // Auto-detectar referências
        dashScript = GetComponent<DashM>() ?? GetComponentInChildren<DashM>() ?? GetComponentInParent<DashM>();
        dashVFX = GetComponent<PlayerDashVFX>() ?? GetComponentInChildren<PlayerDashVFX>() ?? GetComponentInParent<PlayerDashVFX>();

        if (dashScript == null)
        {
            Debug.LogError("[ExplosiveDashEffect] DashM não encontrado no Player! O efeito não funcionará.");
        }
    }

    private void Update()
    {
        if (!isActive || dashScript == null) return;

        // Detecta a transição: estava dando dash → parou de dar dash
        if (!dashScript.isDashing && wasDashing)
        {
            // O dash acabou! Dispara a explosão no ponto final
            OnDashEnded();
        }

        wasDashing = dashScript.isDashing;
    }

    /// <summary>
    /// Ativa o efeito Explosive Dash. Chamado pelo Tier4EffectManager.
    /// Modifica o DashM e PlayerDashVFX para usar os valores enhanced.
    /// </summary>
    public void ActivateEffect()
    {
        isActive = true;

        // Aplicar multiplicador de duração no DashM
        if (dashScript != null)
        {
            dashScript.dashDurationMultiplier = dashDurationMultiplier;
            Debug.Log($"💎 [ExplosiveDash] Duração do dash: {dashScript.dashDuration}s × {dashDurationMultiplier} = {dashScript.dashDuration * dashDurationMultiplier:F2}s");
        }

        // Trocar o VFX do dash para o enhanced
        if (dashVFX != null)
        {
            dashVFX.SetVFXOverride(enhancedDashVFXType);
            Debug.Log($"💎 [ExplosiveDash] VFX do dash trocado para {enhancedDashVFXType}");
        }

        Debug.Log("💎 [ExplosiveDash] Efeito ATIVADO! Dash mais longo + explosão no final.");
    }

    /// <summary>
    /// Desativa o efeito Explosive Dash. Chamado pelo Tier4EffectManager.
    /// Restaura o DashM e PlayerDashVFX para valores originais.
    /// </summary>
    public void DeactivateEffect()
    {
        isActive = false;

        // Restaurar duração original do dash
        if (dashScript != null)
        {
            dashScript.dashDurationMultiplier = 1.0f;
        }

        // Restaurar VFX original do dash
        if (dashVFX != null)
        {
            dashVFX.ClearVFXOverride();
        }

        Debug.Log("💎 [ExplosiveDash] Efeito DESATIVADO. Dash restaurado ao normal.");
    }

    /// <summary>
    /// Chamado quando o dash termina. Spawna a explosão AoE no ponto final.
    /// </summary>
    private void OnDashEnded()
    {
        lastDashEndPosition = transform.position;

        // 1. Dano AoE via OverlapSphere (mesmo padrão do Ultimate_Axe e BossAoEShockwave)
        Collider[] hitColliders = Physics.OverlapSphere(lastDashEndPosition, explosionRadius);

        int enemiesHit = 0;
        foreach (var hitObj in hitColliders)
        {
            // Ignora o próprio player e seus filhos
            if (hitObj.gameObject == gameObject || hitObj.transform.IsChildOf(transform)) continue;
            // Ignora se o collider está no mesmo root do player
            if (hitObj.transform.root == transform.root) continue;

            bool hitEnemy = false;

            // Tenta aplicar dano no DummyHealth (inimigos comuns)
            DummyHealth dummy = hitObj.GetComponent<DummyHealth>() ?? hitObj.GetComponentInParent<DummyHealth>();
            if (dummy != null)
            {
                dummy.TakeDamage(explosionDamage);
                hitEnemy = true;
            }

            // Tenta aplicar dano no ShardSwarmHealth (shard swarm)
            ShardSwarmHealth swarm = hitObj.GetComponent<ShardSwarmHealth>() ?? hitObj.GetComponentInParent<ShardSwarmHealth>();
            if (swarm != null)
            {
                swarm.TakeDamage(explosionDamage);
                hitEnemy = true;
            }

            // Aplica knockback via Rigidbody (mesmo padrão do Ultimate_Axe)
            if (hitEnemy)
            {
                Rigidbody enemyRb = hitObj.GetComponent<Rigidbody>() ?? hitObj.GetComponentInParent<Rigidbody>();
                if (enemyRb != null)
                {
                    Vector3 pushDir = (enemyRb.transform.position - lastDashEndPosition).normalized;
                    pushDir.y = upwardKnockback;
                    enemyRb.AddForce(pushDir * explosionKnockback, ForceMode.Impulse);
                }

                enemiesHit++;
            }
        }

        // 2. VFX da explosão
        if (VFXManager.Instance != null)
        {
            VFXManager.Play(explosionVFXType, lastDashEndPosition, Quaternion.identity, explosionVFXScale);
        }

        if (enemiesHit > 0)
        {
            Debug.Log($"💥 [ExplosiveDash] EXPLOSÃO! {enemiesHit} inimigo(s) atingido(s) | Dano: {explosionDamage} | Raio: {explosionRadius}");
        }
    }

    /// <summary>
    /// Consulta se o efeito está ativo.
    /// </summary>
    public bool IsActive() => isActive;

    /// <summary>
    /// Desenha o raio da explosão na Scene view para facilitar o tuning visual.
    /// Só aparece se showExplosionGizmo = true e o efeito está ativo.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showExplosionGizmo) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
        // Esfera sólida semi-transparente para visualizar melhor a área
        Color solidColor = gizmoColor;
        solidColor.a = 0.1f;
        Gizmos.color = solidColor;
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}
