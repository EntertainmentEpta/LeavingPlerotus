using UnityEngine;

/// <summary>
/// Componente colocado no Prefab Variant de elite.
/// Gera automaticamente a aura procedural laranja pulsante.
/// </summary>
public class EliteVariantConfig : MonoBehaviour
{
    [Header("Identificacao")]
    [Tooltip("Nome de exibicao desta variante (usado em logs).")]
    public string variantName = "Variante Elite";

    [Header("Aura VFX (opcional)")]
    [Tooltip("Se preenchido, instancia este prefab como filho. Deixe vazio para usar a aura procedural.")]
    public GameObject auraPrefab;

    [Header("Aura Procedural")]
    [Tooltip("Se auraPrefab for null, gera uma aura procedural simples.")]
    public bool useProceduralAura = true;
    public Color auraColor = new Color(1f, 0.45f, 0f, 1f); // laranja
    [Range(0.3f, 3f)]
    public float auraRadius = 0.6f;
    [Range(0.05f, 0.5f)]
    public float auraWidth  = 0.08f;

    private GameObject auraInstance;
    private LineRenderer auraLR;
    private const int SEGMENTS = 32;

    void Start()
    {
        SpawnAura();
    }

    void OnDestroy()
    {
        if (auraInstance != null) Destroy(auraInstance);
    }

    void SpawnAura()
    {
        if (auraPrefab != null)
        {
            auraInstance = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
            return;
        }

        if (!useProceduralAura) return;

        auraInstance = new GameObject("EliteAura");
        auraInstance.transform.SetParent(transform, false);
        auraInstance.transform.localPosition = Vector3.up * 0.05f;

        auraLR = auraInstance.AddComponent<LineRenderer>();
        auraLR.loop = true;
        auraLR.useWorldSpace = false;
        auraLR.positionCount = SEGMENTS + 1;
        auraLR.numCapVertices = 3;
        auraLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var mat = new Material(Shader.Find("Sprites/Default"));
        auraLR.material = mat;
        auraLR.startWidth = auraWidth;
        auraLR.endWidth   = auraWidth;

        Color c = auraColor;
        c.a = 0.85f;
        auraLR.material.color = c;
        auraLR.startColor = c;
        auraLR.endColor   = c;

        BuildCircle(auraRadius);
        StartCoroutine(AnimateAura());
    }

    void BuildCircle(float r)
    {
        if (auraLR == null) return;
        for (int i = 0; i <= SEGMENTS; i++)
        {
            float ang = (float)i / SEGMENTS * Mathf.PI * 2f;
            auraLR.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
        }
    }

    System.Collections.IEnumerator AnimateAura()
    {
        while (auraLR != null)
        {
            float alpha = Mathf.Lerp(0.5f, 0.95f, (Mathf.Sin(Time.time * 2.5f) + 1f) * 0.5f);
            Color c = auraColor;
            c.a = alpha;
            if (auraLR.material != null)
            {
                auraLR.material.color = c;
                auraLR.startColor = c;
                auraLR.endColor   = c;
            }
            yield return null;
        }
    }
}
