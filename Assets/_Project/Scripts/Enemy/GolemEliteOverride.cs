using System.Collections;
using UnityEngine;

/// <summary>Componente do Prefab Variant: substitui apenas o cast de stun.</summary>
[RequireComponent(typeof(Golem_AI), typeof(DummyHealth))]
public class GolemEliteOverride : MonoBehaviour
{
    [Min(0.1f)] public float telegraphTime = 0.65f;
    [Min(0f)] public float impactInterval = 0.9f;
    [Min(0f)] public float earlyStunDuration = 0.25f;
    [Min(0f)] public float sequenceCooldown = 11f;

    private Golem_AI baseAI;
    private DummyHealth health;
    private GameObject marker;
    private Material markerMaterial;
    private bool casting;

    void Awake()
    {
        baseAI = GetComponent<Golem_AI>();
        health = GetComponent<DummyHealth>();
    }

    void OnEnable() { health.onDeath += CancelSequence; }
    void OnDisable() { health.onDeath -= CancelSequence; CancelSequence(); }

    public IEnumerator CastTripleStun(Transform player)
    {
        casting = true;
        // Outros Golems/casts nao podem ignorar a imunidade deste cast.
        object stunSequence = new object();
        for (int i = 0; i < 3; i++)
        {
            if (!CanCast(player)) break;
            Vector3 target = player.position;
            // Usa o piso sob o alvo; ignora triggers e corpos de combate.
            target.y = transform.position.y + 0.05f;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(player.position + Vector3.up * 2f,
                Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerHealth>() != null ||
                    hit.collider.GetComponentInParent<DummyHealth>() != null) continue;
                if (hit.distance < nearest) { nearest = hit.distance; target.y = hit.point.y + 0.05f; }
            }
            ShowMarker(target);
            yield return new WaitForSeconds(Mathf.Max(0.1f, telegraphTime));
            ClearMarker();
            if (!CanCast(player)) break;

            baseAI.PlayStompSound(target);
            float duration = i == 2 ? baseAI.stunDuration : earlyStunDuration;
            GameObject beam = baseAI.stunBeamPrefab != null
                ? Instantiate(baseAI.stunBeamPrefab, target, Quaternion.identity)
                : new GameObject("GolemEliteStun");
            beam.transform.position = target;
            StunBeam stun = beam.GetComponent<StunBeam>() ?? beam.AddComponent<StunBeam>();
            stun.Initialize(baseAI.stunRadius, duration, stunSequence);
            Destroy(beam, 0.4f);

            // Intervalo medido entre impactos; garante recuperacao do stun curto.
            if (i < 2)
                yield return new WaitForSeconds(Mathf.Max(earlyStunDuration + 0.1f,
                    impactInterval - telegraphTime));
        }
        casting = false;
        ClearMarker();
    }

    bool CanCast(Transform player)
    {
        return isActiveAndEnabled && baseAI.isActiveAndEnabled && health.CurrentHealth > 0 && player != null;
    }

    void ShowMarker(Vector3 position)
    {
        ClearMarker();
        if (baseAI.stunMarkerPrefab != null)
        {
            marker = Instantiate(baseAI.stunMarkerPrefab, position, Quaternion.Euler(90f, 0f, 0f));
            marker.transform.localScale = new Vector3(baseAI.stunRadius * 2f, baseAI.stunRadius * 2f, 1f);
            return;
        }
        // Mesmo anel procedural usado pelos Elites; mostra o raio real do impacto.
        marker = new GameObject("GolemEliteStunMarker");
        marker.transform.position = position;
        LineRenderer ring = marker.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 48;
        ring.startWidth = ring.endWidth = 0.10f;
        markerMaterial = new Material(Shader.Find("Sprites/Default"));
        markerMaterial.color = Color.cyan;
        ring.sharedMaterial = markerMaterial;
        for (int n = 0; n < ring.positionCount; n++)
        {
            float a = n * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(n, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * baseAI.stunRadius);
        }
    }

    void ClearMarker()
    {
        if (marker != null) Destroy(marker);
        if (markerMaterial != null) Destroy(markerMaterial);
        marker = null;
        markerMaterial = null;
    }

    void CancelSequence()
    {
        if (baseAI != null && (casting || health.CurrentHealth <= 0))
            baseAI.CancelEliteStun(sequenceCooldown);
        casting = false;
        ClearMarker();
    }
}
