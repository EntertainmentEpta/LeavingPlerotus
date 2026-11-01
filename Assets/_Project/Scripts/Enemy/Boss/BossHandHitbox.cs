using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum HandSide
{
    Left,
    Right
}

/// <summary>
/// Script de Hitbox & Trail exclusivo de UMA mão específica do Boss.
///  • SEM TELEPORTES: Mantém o movimento contínuo sem solavancos.
///  • SINCRONIA IMEDIATA: Ativa a janela de impacto no frame exato do golpe, sem atraso artificial.
///  • TRAVA ANTI-MORTE INSTANTÂNEA GLOBAL: Garante exatamente 1 hit por ataque por golpe do Boss (elimina multi-hit das duas mãos).
///  • VARREDURA DE ALCANCE CALIBRADA: Alcance de 4.8m para acompanhar a escala e o porte do Boss novo.
/// </summary>
public class BossHandHitbox : MonoBehaviour
{
    [Header("Identificação da Mão")]
    public HandSide handSide = HandSide.Right;

    [Header("GameObject da Mão para o Trail (Inspector)")]
    [Tooltip("Arraste aqui o GameObject 'hand' (coordenada/ponto na mão) para o Trail seguir exatamente ele.")]
    public Transform handTransform;

    [Header("Configurações de Dano & Hitbox desta Mão")]
    public Collider handCollider;
    public int damage = 35;
    public float knockbackForce = 6.0f;
    [Tooltip("Raio de varredura frontal para garantir que o golpe acerte mesmo com diferenças de altura.")]
    public float raioVarreduraArco = 4.8f;

    [Header("Sincronização com Animação")]
    public float duracaoJanelaImpacto = 0.35f;

    [Header("VFX & Trail Exclusivo desta Mão")]
    public BossMeleeTrail meleeTrail;
    public GameObject vfxImpactoChaoPrefab;
    public GameObject vfxAtaqueMeleePrefab;

    private bool isActive = false;
    private bool hasHitPlayerThisAttack = false;
    private BossController bossController;
    private Coroutine attackRoutine;
    private Transform playerTransformCached;

    void Awake()
    {
        if (handCollider == null)
            handCollider = GetComponent<Collider>();

        if (handCollider != null)
        {
            handCollider.isTrigger = true;
            handCollider.enabled = false;
        }

        bossController = GetComponentInParent<BossController>();

        // Garante que o Trail esteja posicionado no GameObject hand da mão
        Transform targetTrailTransform = handTransform != null ? handTransform : transform;
        if (meleeTrail == null)
            meleeTrail = targetTrailTransform.GetComponent<BossMeleeTrail>() ?? targetTrailTransform.gameObject.AddComponent<BossMeleeTrail>();
    }

    private Transform GetPlayerTransform()
    {
        if (playerTransformCached == null)
        {
            if (bossController != null && bossController.playerTransform != null)
            {
                playerTransformCached = bossController.playerTransform;
            }
            else
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransformCached = p.transform;
            }
        }
        return playerTransformCached;
    }

    /// <summary>
    /// Ativa a hitbox imediatamente no momento do impacto disparado pelo BossController.
    /// </summary>
    public void EnableHitbox(float totalDuration, int attackDamage = 35, float pushForce = 15f)
    {
        damage = attackDamage;
        knockbackForce = pushForce;
        hasHitPlayerThisAttack = false;

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(RotinaAtivacaoImediata(totalDuration > 0.05f ? totalDuration : duracaoJanelaImpacto));
    }

    private IEnumerator RotinaAtivacaoImediata(float activeWindow)
    {
        OnAnimationEvent_EnableHitbox();

        yield return new WaitForSeconds(activeWindow);

        DisableHitbox();
    }

    // =========================================================================
    // ANIMATION EVENTS (Controlados pelos marcadores da timeline ou pelo controller)
    // =========================================================================

    public void OnAnimationEvent_EnableHitbox()
    {
        isActive = true;
        hasHitPlayerThisAttack = false;
        if (handCollider != null) handCollider.enabled = true;
    }

    public void OnAnimationEvent_EnableTrailOnly()
    {
        if (meleeTrail != null) meleeTrail.AtivarTrail(0.45f);
    }

    public void OnAnimationEvent_DisableTrailOnly()
    {
        if (meleeTrail != null) meleeTrail.DesativarTrail();
    }

    public void OnAnimationEvent_DisableHitbox()
    {
        DisableHitbox();
    }

    public void OnAnimationEvent_GroundImpact()
    {
        Transform player = GetPlayerTransform();
        if (vfxImpactoChaoPrefab != null && player != null)
        {
            Vector3 posImpacto = player.position + Vector3.up * 0.1f;
            Instantiate(vfxImpactoChaoPrefab, posImpacto, Quaternion.identity);
        }
        BossController.TriggerCameraShake(0.3f, 0.2f);
    }

    public void DisableHitbox()
    {
        isActive = false;
        hasHitPlayerThisAttack = false;
        if (handCollider != null) handCollider.enabled = false;
        if (meleeTrail != null) meleeTrail.DesativarTrail();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || hasHitPlayerThisAttack) return;
        if (other.CompareTag("Player"))
        {
            AplicarDanoEKnockbackNoPlayer(other.gameObject);
        }
    }

    private void Update()
    {
        // Varredura por área enquanto ativa (para garantir acerto limpo em 4.8m)
        if (!isActive || hasHitPlayerThisAttack) return;

        Transform player = GetPlayerTransform();
        if (player == null) return;

        Transform rootBoss = bossController != null ? bossController.transform : transform.root;
        Vector3 playerPos = player.position + Vector3.up * 1.0f;

        // Testa distância do centro da mão (se houver) ou de um ponto projetado à frente do Boss
        Vector3 originHand = handTransform != null ? handTransform.position : transform.position;
        Vector3 originFront = rootBoss != null ? (rootBoss.position + rootBoss.forward * 1.8f + Vector3.up * 1.0f) : originHand;

        float distHand = Vector3.Distance(originHand, playerPos);
        float distFront = Vector3.Distance(originFront, playerPos);

        if (distHand <= (raioVarreduraArco * 0.7f) || distFront <= raioVarreduraArco)
        {
            Vector3 bossForward = rootBoss != null ? rootBoss.forward : transform.forward;
            Vector3 dirToPlayer = (playerPos - (rootBoss != null ? rootBoss.position : transform.position)).normalized;
            float angle = Vector3.Angle(bossForward, dirToPlayer);

            if (angle <= 75f)
            {
                AplicarDanoEKnockbackNoPlayer(player.gameObject);
            }
        }
    }

    private void AplicarDanoEKnockbackNoPlayer(GameObject player)
    {
        if (player == null || hasHitPlayerThisAttack) return;

        // Trava compartilhada anti-multi-hit: se a outra mão já acertou neste golpe há menos de 0.35s, ignora
        if (bossController != null)
        {
            if (Time.time - bossController.lastMeleeHitPlayerTime < 0.35f)
            {
                hasHitPlayerThisAttack = true;
                return;
            }
            bossController.lastMeleeHitPlayerTime = Time.time;
        }

        hasHitPlayerThisAttack = true;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>() ?? player.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, gameObject);
        }

        Rigidbody playerRb = player.GetComponent<Rigidbody>() ?? player.GetComponentInParent<Rigidbody>();
        if (playerRb != null && !playerRb.isKinematic)
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            dir.y = 0.25f;
            playerRb.AddForce(dir * knockbackForce, ForceMode.Impulse);
        }

        if (vfxAtaqueMeleePrefab != null)
        {
            Instantiate(vfxAtaqueMeleePrefab, player.transform.position + Vector3.up * 1.0f, Quaternion.identity);
        }
        else
        {
            VFXManager.Play(VFXType.BossPunchImpact, player.transform.position + Vector3.up * 1.0f, Quaternion.identity);
        }

        VFXManager.Play(VFXType.ImpactFrame, player.transform.position + Vector3.up * 1.0f, Quaternion.identity);
        BossController.TriggerCameraShake(0.35f, 0.2f);

        Debug.Log($"[BossHandHitbox - {handSide}] Golpe acertou o player com precisão! Dano: {damage}");
    }
}
