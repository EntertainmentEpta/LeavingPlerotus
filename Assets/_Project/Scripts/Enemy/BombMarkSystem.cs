using UnityEngine;
using System.Collections;

/// <summary>
/// Componente de Marca de Bomba — adicionado ao Prefab Variant do Goblin Elite.
///
/// MECANICA:
///   1. Ser acertado pela bomba sem marca  →  aplica MARCA por markDuration segundos
///      (efeito visual: anel laranja pulsante ao redor do player)
///   2. Ser acertado pela bomba JA marcado →  CONSOME a marca e aplica STUN por stunDuration segundos
///
/// COMO FUNCIONA:
///   Este componente ouve o evento onDamageTaken do PlayerHealth.
///   A BombaExplosiva, quando explode com marksOnHit=true, chama BombMarkSystem.Instance.OnBombHit(player).
/// </summary>
public class BombMarkSystem : MonoBehaviour
{
    public static BombMarkSystem Instance { get; private set; }

    [Header("Mark Config")]
    [Tooltip("Duracao da marca em segundos.")]
    public float markDuration = 3f;
    [Tooltip("Duracao do stun quando a marca e consumida.")]
    public float stunDuration = 1f;

    private bool playerIsMarked = false;
    private float markTimer = 0f;
    private GameObject markVFX;

    void Awake()
    {
        // Singleton simples — apenas um GoblinElite deve estar ativo por sala,
        // mas mesmo com varios, o ultimo registrado e usado (safe).
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyMarkVFX();
    }

    void Update()
    {
        if (!playerIsMarked) return;

        markTimer -= Time.deltaTime;
        if (markTimer <= 0f)
        {
            RemoveMark();
        }
    }

    /// <summary>
    /// Chamado pela BombaExplosiva elite ao acertar o player.
    /// </summary>
    public void OnBombHit(GameObject player)
    {
        if (player == null) return;
        PlayerHealth ph = player.GetComponent<PlayerHealth>();
        if (ph == null) return;

        if (playerIsMarked)
        {
            // Consome a marca -> aplica stun
            RemoveMark();
            ph.ApplyStun(stunDuration);
            Debug.Log("[GOBLIN ELITE] Marca consumida! Player stunado por " + stunDuration + "s.");
        }
        else
        {
            // Aplica marca
            ApplyMark(player);
            Debug.Log("[GOBLIN ELITE] Player MARCADO por " + markDuration + "s.");
        }
    }

    void ApplyMark(GameObject player)
    {
        playerIsMarked = true;
        markTimer = markDuration;
        SpawnMarkVFX(player);
    }

    void RemoveMark()
    {
        playerIsMarked = false;
        markTimer = 0f;
        DestroyMarkVFX();
    }

    // ── VFX da marca (anel laranja ao redor do player) ──────────────────
    void SpawnMarkVFX(GameObject player)
    {
        DestroyMarkVFX();

        markVFX = new GameObject("BombMarkVFX");
        markVFX.transform.SetParent(player.transform, false);
        markVFX.transform.localPosition = Vector3.up * 0.05f;

        var lr = markVFX.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.positionCount = 33;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.12f;
        lr.endWidth   = 0.12f;

        float r = 0.8f;
        for (int i = 0; i <= 32; i++)
        {
            float ang = (float)i / 32 * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
        }

        StartCoroutine(AnimateMark(lr));
    }

    void DestroyMarkVFX()
    {
        if (markVFX != null)
        {
            Destroy(markVFX);
            markVFX = null;
        }
    }

    IEnumerator AnimateMark(LineRenderer lr)
    {
        Color orange = new Color(1f, 0.45f, 0f, 1f);
        while (lr != null && playerIsMarked)
        {
            float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            Color c = orange;
            c.a = Mathf.Lerp(0.6f, 1f, pulse);
            lr.material.color = c;
            lr.startColor = c;
            lr.endColor   = c;
            yield return null;
        }
    }
}
