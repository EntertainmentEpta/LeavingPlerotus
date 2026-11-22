using System.Collections.Generic;
using UnityEngine;

/// <summary>Somente no Prefab Variant da caveira do Totem Elite.</summary>
[RequireComponent(typeof(HomingHazard))]
[DefaultExecutionOrder(100)]
public class SkullEliteGravity : MonoBehaviour
{
    [Min(0.1f)] public float attractionRadius = 8f;
    [Min(0f)] public float attractionAcceleration = 70f;
    [Tooltip("Velocidade radial inicial em direcao a caveira, em m/s.")]
    [Min(0f)] public float initialPullSpeed = 14f;
    [Tooltip("Janela curta que sustenta o puxao contra a frenagem do PlayerM.")]
    [Min(0f)] public float initialPullDuration = 0.18f;

    // Tetos compartilhados: varias caveiras nao multiplicam a intensidade.
    public const float MaxCombinedAcceleration = 70f;
    public const float MaxCombinedVelocityChange = 27f;
    private static readonly List<SkullEliteGravity> active = new List<SkullEliteGravity>();
    private HomingHazard hazard;
    private Rigidbody playerBody;
    private PlayerHealth playerHealth;
    private DashM dash;
    private PlayerUltimate ultimate;
    private bool wasFusing;
    private float pullStartedAt;
    private bool initialPullActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { active.Clear(); }

    void Awake() { hazard = GetComponent<HomingHazard>(); }
    void OnEnable()
    {
        wasFusing = false;
        initialPullActive = false;
        if (!active.Contains(this)) active.Add(this);
    }
    void OnDisable()
    {
        initialPullActive = false;
        active.Remove(this);
    }

    void FixedUpdate()
    {
        // Executa depois de PlayerM/HomingHazard e agrega apenas uma vez por passo.
        if (active.Count == 0 || active[0] != this) return;
        if (playerBody == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>() ?? player.GetComponentInParent<PlayerHealth>();
                playerBody = player.GetComponent<Rigidbody>() ?? player.GetComponentInParent<Rigidbody>();
                dash = player.GetComponent<DashM>() ?? player.GetComponentInParent<DashM>();
                ultimate = player.GetComponent<PlayerUltimate>() ?? player.GetComponentInChildren<PlayerUltimate>();
            }
        }

        bool canPull = playerBody != null && !playerBody.isKinematic && playerHealth != null &&
            !playerHealth.isDead && !playerHealth.isKnockbackImmune && !playerHealth.isInvulnerable &&
            (dash == null || (!dash.isDashing && !dash.IsPerfectDodgeInvulnerable)) &&
            (ultimate == null || !ultimate.IsUltimateActive()) &&
            !DialogManager.BlocksGameplayInput && !GamePause.IsPaused &&
            (CraftingUI.Instance == null || !CraftingUI.Instance.IsOpen()) &&
            (JournalBookUI.Instance == null || !JournalBookUI.Instance.IsOpen);

        Vector3 acceleration = Vector3.zero;
        Vector3 velocityChange = Vector3.zero;
        foreach (SkullEliteGravity skull in active)
        {
            if (skull == null || !skull.isActiveAndEnabled) continue;
            bool fusing = skull.hazard != null && skull.hazard.IsFusing;
            bool fuseStarted = fusing && !skull.wasFusing;
            skull.wasFusing = fusing; // Observa mesmo durante dash/imunidade: nunca adia o impulso.

            Vector3 delta = playerBody != null ? skull.transform.position - playerBody.position : Vector3.zero;
            delta.y = 0f;
            float distance = delta.magnitude;
            bool inRange = distance > 0.05f && distance <= skull.attractionRadius;

            if (fuseStarted)
            {
                skull.pullStartedAt = Time.fixedTime;
                skull.initialPullActive = canPull && inRange;
            }
            if (!fusing || !canPull || !inRange)
            {
                skull.initialPullActive = false;
                continue;
            }

            Vector3 direction = delta / distance;
            acceleration += direction * Mathf.Max(0f, skull.attractionAcceleration);
            float elapsed = Time.fixedTime - skull.pullStartedAt;
            float duration = Mathf.Max(0f, skull.initialPullDuration);
            if (skull.initialPullActive && duration > 0f && elapsed < duration)
            {
                // Impulso imediato + sustentacao decrescente por 0.18s.
                // Corrige somente a componente radial: preserva Y e movimento tangencial.
                // Nao soma novamente o impulso inteiro nem altera transform/controles.
                float targetSpeed = Mathf.Max(0f, skull.initialPullSpeed) * (1f - elapsed / duration);
                float radialSpeed = Vector3.Dot(playerBody.linearVelocity, direction);
                velocityChange += direction * Mathf.Max(0f, targetSpeed - radialSpeed);
            }
            else
            {
                skull.initialPullActive = false;
            }
        }

        if (!canPull) return;
        // VelocityChange independe da massa e passa pela simulacao/colisoes do Rigidbody.
        playerBody.AddForce(Vector3.ClampMagnitude(velocityChange, MaxCombinedVelocityChange), ForceMode.VelocityChange);
        playerBody.AddForce(Vector3.ClampMagnitude(acceleration, MaxCombinedAcceleration), ForceMode.Acceleration);
    }
}
