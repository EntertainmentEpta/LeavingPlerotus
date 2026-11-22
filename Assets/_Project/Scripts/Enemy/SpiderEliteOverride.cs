using UnityEngine;

/// <summary>
/// Wrapper legado mantido para compatibilidade com prefabs/cenas antigas da Spider_Elite.
/// A mecânica de salto triplo do Elite agora está unificada diretamente dentro de Spider_AI
/// (garantindo som, pose de salto/garras, antecipação, NavMesh e suporte ao Crystal Tuner).
/// </summary>
[RequireComponent(typeof(Spider_AI))]
public class SpiderEliteOverride : MonoBehaviour
{
    [Header("Triple Dash Config (Repassado para Spider_AI)")]
    public int dashCount = 3;
    public float pauseBetweenDashes = 0.24f;

    void Awake()
    {
        Spider_AI ai = GetComponent<Spider_AI>();
        if (ai != null)
        {
            ai.eliteLeapCount = Mathf.Max(2, dashCount);
            ai.elitePauseBetweenLeaps = Mathf.Max(0.16f, pauseBetweenDashes);
        }

        DummyHealth dh = GetComponent<DummyHealth>();
        if (dh != null)
            dh.isElite = true;

        // Desativa este componente após sincronizar para não conflitar com Spider_AI
        enabled = false;
    }
}
