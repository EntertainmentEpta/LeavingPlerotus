using System.Collections;
using UnityEngine;

/// <summary>Tres varreduras fixas apos a perseguicao normal, sem segundo controlador de dano.</summary>
[RequireComponent(typeof(CrystalWatcher_AI), typeof(DummyHealth))]
public class CrystalWatcherEliteOverride : MonoBehaviour
{
    [Min(0.1f)] public float trackingDuration = 2f;
    [Min(0.1f)] public float warningDuration = 0.5f;
    [Min(0.1f)] public float sweepDuration = 0.6f;
    [Range(1f, 180f)] public float sweepArc = 90f;
    [Min(0f)] public float sweepInterval = 0.25f;
    private CrystalWatcher_AI baseAI;
    private DummyHealth health;

    void Awake()
    {
        baseAI = GetComponent<CrystalWatcher_AI>();
        health = GetComponent<DummyHealth>();
    }
    void OnEnable() { health.onDeath += CancelSequence; }
    void OnDisable() { health.onDeath -= CancelSequence; CancelSequence(); }

    bool CanAttack(Transform player)
    {
        return isActiveAndEnabled && baseAI.isActiveAndEnabled && health.CurrentHealth > 0 && player != null;
    }

    public IEnumerator SweepSequence(Transform player)
    {
        baseAI.StopEliteLaser();
        for (int i = 0; i < 3; i++)
        {
            if (!CanAttack(player)) yield break;
            Vector3 direction = player.position - transform.position;
            float center = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float start = center - sweepArc * 0.5f;
            float duration = Mathf.Max(0.1f, sweepDuration);
            // Previa sem dano percorre o arco completo com o laser fino existente.
            baseAI.BeginEliteLaser(start, false);
            float warning = Mathf.Max(0.1f, warningDuration);
            for (float t = 0f; t < warning; t += Time.deltaTime)
            {
                if (!CanAttack(player)) { baseAI.StopEliteLaser(); yield break; }
                baseAI.TickEliteLaser(start + sweepArc * Mathf.Clamp01(t / warning), false);
                yield return null;
            }
            baseAI.BeginEliteLaser(start, true);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (!CanAttack(player)) { baseAI.StopEliteLaser(); yield break; }
                baseAI.TickEliteLaser(start + sweepArc * Mathf.Clamp01(t / duration), true);
                yield return null;
            }
            baseAI.StopEliteLaser();
            if (i < 2) yield return new WaitForSeconds(Mathf.Max(0f, sweepInterval));
        }
    }

    void CancelSequence()
    {
        if (baseAI == null) return;
        baseAI.CancelEliteAttack();
    }
}
