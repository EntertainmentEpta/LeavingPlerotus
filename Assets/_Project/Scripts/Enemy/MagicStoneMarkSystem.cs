using UnityEngine;
using System.Collections;

/// <summary>
/// Componente do Prefab Variant da MagicStone Elite.
/// Adicione este script ao MagicStone Elite.
///
/// MECANICA:
///   1. Ser acertado 1 vez pelo raio -> MARCA por markDuration segundos (anel laranja)
///   2. Ser acertado pelo raio ja marcado -> CONSOME a marca e aplica STUN por stunDuration segundos
///
/// COMO USAR:
///   - Adicione este script ao Prefab Variant da MagicStone Elite.
///   - O AttackBeam elite precisa chamar MagicStoneMarkSystem.NotifyHit(player) ao inves de (ou alem de) aplicar dano.
///   - Configure isEliteBeam=true no AttackBeam do Prefab Variant para ativar o comportamento.
/// </summary>
public class MagicStoneMarkSystem : MonoBehaviour
{
    public static MagicStoneMarkSystem Instance { get; private set; }

    [Header("Mark Config")]
    public float markDuration = 3f;
    public float stunDuration = 0.75f;

    private bool playerIsMarked = false;
    private float markTimer = 0f;
    private GameObject markVFX;

    void Awake()
    {
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
        if (markTimer <= 0f) RemoveMark();
    }

    /// <summary>
    /// Chamado pelo AttackBeam elite ao acertar o player.
    /// </summary>
    public void OnLaserHit(GameObject player)
    {
        if (player == null) return;
        PlayerHealth ph = player.GetComponent<PlayerHealth>();
        if (ph == null) return;

        if (playerIsMarked)
        {
            RemoveMark();
            ph.ApplyStun(stunDuration);
            Debug.Log("[MAGICSTONE ELITE] Marca consumida! Player stunado por " + stunDuration + "s.");
        }
        else
        {
            ApplyMark(player);
            Debug.Log("[MAGICSTONE ELITE] Player MARCADO por " + markDuration + "s.");
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

    void SpawnMarkVFX(GameObject player)
    {
        DestroyMarkVFX();
        markVFX = new GameObject("LaserMarkVFX");
        markVFX.transform.SetParent(player.transform, false);
        markVFX.transform.localPosition = Vector3.up * 0.5f;

        var lr = markVFX.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.positionCount = 33;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.10f;
        lr.endWidth   = 0.10f;

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
        if (markVFX != null) { Destroy(markVFX); markVFX = null; }
    }

    IEnumerator AnimateMark(LineRenderer lr)
    {
        Color orange = new Color(1f, 0.45f, 0f, 1f);
        while (lr != null && playerIsMarked)
        {
            float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
            Color c = orange;
            c.a = Mathf.Lerp(0.5f, 1f, pulse);
            lr.material.color = c;
            lr.startColor = c;
            lr.endColor   = c;
            yield return null;
        }
    }
}
