using UnityEngine;
using System.Collections;

/// <summary>
/// Componente do Prefab Variant da Spider Elite.
/// Adicione este script ao Prefab Variant da Spider Elite.
///
/// MECANICA ELITE:
///   A Spider Elite faz 3 leaps (dashes) consecutivos em vez de 1.
///   Substitui o comportamento padrao do PerformLeapAttack original
///   ouvindo o evento do Spider_AI via campo spiderAI.
///
/// COMO FUNCIONA:
///   Este componente fica em modo "interceptacao": ao ser ativado,
///   ele desativa o Spider_AI e gerencia o proprio loop de combate.
///   Nao modifica Spider_AI.cs, mantendo 100% de compatibilidade com
///   o base prefab.
/// </summary>
[RequireComponent(typeof(Spider_AI))]
public class SpiderEliteOverride : MonoBehaviour
{
    [Header("Triple Dash Config")]
    [Tooltip("Numero de leaps consecutivos da Spider Elite.")]
    public int dashCount = 3;
    [Tooltip("Pausa curta entre cada dash da sequencia.")]
    public float pauseBetweenDashes = 0.25f;

    private Spider_AI baseAI;
    private Rigidbody rb;
    private DummyHealth health;
    private SpiderDashVFX dashVFX;
    private Transform playerTransform;

    private bool isDoingEliteSequence = false;
    private float eliteCooldownTimer = 0f;

    // Copiamos os valores do Spider_AI base para usar aqui
    private float leapForce;
    private float leapUpForce;
    private float leapRange;
    private float leapCooldown;
    private int   leapDamage;
    private float leapHitRadius;
    private float leapDuration;
    private float leapLandingDuration;

    void Start()
    {
        baseAI = GetComponent<Spider_AI>();
        rb     = GetComponent<Rigidbody>();
        health = GetComponent<DummyHealth>();
        dashVFX = GetComponent<SpiderDashVFX>();

        // Copia parametros do Spider_AI para usar nos leaps Elite
        leapForce          = baseAI.leapForce;
        leapUpForce        = baseAI.leapUpForce;
        leapRange          = baseAI.leapRange;
        leapCooldown       = baseAI.leapCooldown;
        leapDamage         = baseAI.leapDamage;
        leapHitRadius      = baseAI.leapHitRadius;
        leapDuration       = baseAI.leapDuration;
        leapLandingDuration = baseAI.leapLandingDuration;

        // Cooldown inicial identico ao base para nao atacar logo de cara
        eliteCooldownTimer = leapCooldown;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else return;
        }

        if (health != null && health.CurrentHealth <= 0) return;
        if (isDoingEliteSequence) return;

        // O Spider_AI base controla o movimento de aproximacao.
        // Nos monitoramos o cooldown e interceptamos o ataque.
        if (eliteCooldownTimer > 0f)
        {
            eliteCooldownTimer -= Time.deltaTime;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= leapRange)
        {
            StartCoroutine(EliteTripleDash());
        }
    }

    IEnumerator EliteTripleDash()
    {
        isDoingEliteSequence = true;
        eliteCooldownTimer   = leapCooldown;

        // Desativa o Spider_AI durante a sequencia para nao conflitar
        baseAI.enabled = false;

        for (int i = 0; i < dashCount; i++)
        {
            if (playerTransform == null) break;

            // Calcula direcao atualizada a cada dash
            Vector3 dir = (playerTransform.position - transform.position).normalized;

            if (dashVFX != null)
            {
                dashVFX.StartDashEffect();
                dashVFX.SpawnAfterImage();
            }

            rb.linearVelocity = Vector3.zero;
            rb.AddForce(dir * leapForce + Vector3.up * leapUpForce, ForceMode.VelocityChange);

            float elapsed = 0f;
            bool hitThisDash = false;

            while (elapsed < leapDuration)
            {
                elapsed += Time.deltaTime;

                if (!hitThisDash)
                {
                    Collider[] hits = Physics.OverlapSphere(transform.position, leapHitRadius);
                    foreach (Collider hit in hits)
                    {
                        if (hit.CompareTag("Player"))
                        {
                            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
                            if (ph != null)
                            {
                                ph.TakeDamage(leapDamage, gameObject);
                                hitThisDash = true;
                            }
                            break;
                        }
                    }
                }

                if (dashVFX != null && ((int)(elapsed * 10) % 2 == 0))
                    dashVFX.SpawnAfterImage();

                yield return null;
            }

            if (dashVFX != null) dashVFX.StopDashEffect();
            yield return new WaitForSeconds(leapLandingDuration);

            // Pausa entre dashes
            if (i < dashCount - 1)
                yield return new WaitForSeconds(pauseBetweenDashes);
        }

        // Reativa o Spider_AI base
        baseAI.enabled = true;
        isDoingEliteSequence = false;
    }
}
