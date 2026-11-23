using UnityEngine;

/// <summary>
/// Configuração e gatilho do Stun Orbital do Golem (feixe de luz do céu + prefabs reais de impacto CFXR / Hovl em URP).
/// </summary>
public class StunBeam : MonoBehaviour
{
    [Header("Configurações")]
    public float radius = 4f;
    public float stunDuration = 1.5f;

    [Header("Cores & Altura do Feixe")]
    [Tooltip("Cor principal do Stun (telegraph limpo e feixe do céu).")]
    public Color beamColor = new Color(0.62f, 0.30f, 0.95f, 1f);
    [Tooltip("Cor secundária roxa profunda do halo externo do feixe.")]
    public Color accentColor = new Color(0.44f, 0.18f, 0.85f, 1f);
    [Tooltip("Cor do núcleo lavanda suave no impacto (evita estourar para branco).")]
    public Color flashColor = new Color(0.82f, 0.62f, 1.0f, 1f);
    [Tooltip("Altura do feixe vindo do céu (metros).")]
    public float skyBeamHeight = 22f;

    [Header("Prefabs de VFX (Partículas Feitas à Mão)")]
    public GameObject anticipationGlowPrefab;
    public GameObject groundImpactPrefab;
    public GameObject groundHitRingPrefab;
    public GameObject impactBurstPrefab;
    public GameObject electricStunPrefab;

    [Header("Materiais URP do Feixe & Telegraph Limpo")]
    public Material skyBeamCoreMaterial;
    public Material skyBeamOuterMaterial;
    public Material cleanTelegraphMaterial;

    // Campos de compatibilidade mantidos para outros scripts (ex.: BossController)
    [HideInInspector] public float ringWidth = 0.5f;
    [HideInInspector] public float pillarHeight = 8f;
    [HideInInspector] public float pillarDuration = 0.4f;
    [HideInInspector] public int particleCount = 30;

    private bool hasAppliedStun = false;
    private object stunSequence;

    public void Initialize(float newRadius, float newStunDuration, object sequence)
    {
        stunSequence = sequence;
        Initialize(newRadius, newStunDuration);
    }

    public void Initialize(float newRadius, float newStunDuration)
    {
        radius = newRadius;
        stunDuration = newStunDuration;
        if (stunSequence != null)
            ApplySequenceStunToPlayersInArea();
        else
            ApplyStunInArea(transform.position, radius, stunDuration);
        hasAppliedStun = true;
    }

    void Start()
    {
        if (!hasAppliedStun)
        {
            if (stunSequence != null)
                ApplySequenceStunToPlayersInArea();
            else
                ApplyStunInArea(transform.position, radius, stunDuration);
            hasAppliedStun = true;
        }

        GolemSkyStunVFX sky = SpawnSkyStunAnticipation(transform.position, radius, 0.25f);
        sky.TriggerImpact();
        Destroy(gameObject, 0.1f);
    }

    void ApplySequenceStunToPlayersInArea()
    {
        var affected = new System.Collections.Generic.HashSet<PlayerHealth>();
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || !affected.Add(playerHealth)) continue;
            if (playerHealth.TryApplySequenceStun(stunDuration, stunSequence))
                Debug.Log("Golem Elite: stun aplicado por " + stunDuration + "s.");
        }
    }

    public GolemSkyStunVFX SpawnSkyStunAnticipation(Vector3 groundPos, float targetRadius, float telegraphDuration, Color? overrideBeamColor = null)
    {
        Color mainCol = overrideBeamColor ?? beamColor;
        Color secCol = overrideBeamColor.HasValue ? Color.Lerp(overrideBeamColor.Value, Color.white, 0.25f) : accentColor;

        return GolemSkyStunVFX.SpawnAnticipation(
            groundPos,
            targetRadius,
            telegraphDuration,
            skyBeamHeight,
            mainCol,
            secCol,
            flashColor,
            skyBeamCoreMaterial,
            skyBeamOuterMaterial,
            cleanTelegraphMaterial,
            anticipationGlowPrefab,
            groundImpactPrefab,
            groundHitRingPrefab,
            impactBurstPrefab,
            electricStunPrefab
        );
    }

    public static void ApplyStunInArea(Vector3 center, float areaRadius, float duration)
    {
        Vector3 flatCenter = new Vector3(center.x, 0f, center.z);
        Collider[] hits = Physics.OverlapSphere(center + Vector3.up * 1.0f, areaRadius + 1.5f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (!hit.CompareTag("Player")) continue;

            Vector3 flatTarget = new Vector3(hit.transform.position.x, 0f, hit.transform.position.z);
            if (Vector3.Distance(flatCenter, flatTarget) > areaRadius) continue;

            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>() ?? hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ApplyStun(duration);
                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = beamColor;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
