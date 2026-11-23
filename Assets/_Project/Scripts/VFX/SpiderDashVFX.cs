using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VFX URP de rastro e after-image para o salto/recuo da Aranha.
/// Usa shaders URP aditivos/translúcidos, fade suave ao aterrissar e suporte a cor ciano do Crystal Tuner.
/// </summary>
public class SpiderDashVFX : MonoBehaviour
{
    [Header("Trail Settings")]
    [Tooltip("Cor principal do rastro normal.")]
    public Color trailColor = new Color(0.68f, 0.24f, 0.94f, 0.72f);
    [Tooltip("Cor da ponta do rastro (fade).")]
    public Color trailEndColor = new Color(0.32f, 0.08f, 0.55f, 0f);
    [Tooltip("Cor principal do rastro quando conectada ao Crystal Tuner.")]
    public Color buffedTrailColor = new Color(0.22f, 0.90f, 1.0f, 0.78f);
    [Tooltip("Cor final do rastro quando bufada pelo Crystal Tuner.")]
    public Color buffedTrailEndColor = new Color(0.08f, 0.52f, 0.92f, 0f);
    [Tooltip("Largura do rastro.")]
    public float trailWidth = 0.45f;
    [Tooltip("Duração do rastro em segundos.")]
    public float trailDuration = 0.28f;

    [Header("Ghost After-Image")]
    [Tooltip("Ativar efeito de after-image (cópias fantasma).")]
    public bool useAfterImage = true;
    [Tooltip("Quantidade de after-images durante o dash.")]
    public int afterImageCount = 3;
    [Tooltip("Cor da after-image normal.")]
    public Color afterImageColor = new Color(0.60f, 0.24f, 0.88f, 0.42f);
    [Tooltip("Cor da after-image quando conectada ao Crystal Tuner.")]
    public Color buffedAfterImageColor = new Color(0.22f, 0.88f, 1.0f, 0.46f);
    [Tooltip("Duração de cada after-image em segundos.")]
    public float afterImageDuration = 0.24f;
    [Tooltip("Multiplicador de escala das after-images.")]
    public float ghostScaleMultiplier = 1.0f;

    public enum GhostMeshMode
    {
        SpiderMesh,
        Circle
    }

    [Header("Ghost Mesh")]
    public GhostMeshMode ghostMeshMode = GhostMeshMode.SpiderMesh;
    public SkinnedMeshRenderer targetRenderer;
    public float circleRadius = 0.6f;
    [Range(8, 64)]
    public int circleSegments = 24;

    private TrailRenderer trailRenderer;
    private Material trailMaterial;
    private bool isBuffed = false;
    private readonly List<GameObject> activeGhosts = new List<GameObject>();

    private Color ActiveTrailMain => isBuffed ? buffedTrailColor : trailColor;
    private Color ActiveTrailEnd => isBuffed ? buffedTrailEndColor : trailEndColor;
    private Color ActiveGhostColor => isBuffed ? buffedAfterImageColor : afterImageColor;

    void Start()
    {
        SetupTrailRenderer();
    }

    private void OnDisable()
    {
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < activeGhosts.Count; i++)
        {
            if (activeGhosts[i] != null) Destroy(activeGhosts[i]);
        }
        activeGhosts.Clear();

        if (trailMaterial != null) Destroy(trailMaterial);
    }

    private static Material CreateUrpTranslucentMaterial(Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        return mat;
    }

    void SetupTrailRenderer()
    {
        GameObject trailObj = new GameObject("SpiderDashTrail");
        trailObj.transform.SetParent(transform, false);
        trailObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        NeutralizeScale(trailObj.transform);

        trailRenderer = trailObj.AddComponent<TrailRenderer>();
        trailRenderer.time = trailDuration;
        trailRenderer.startWidth = trailWidth;
        trailRenderer.endWidth = trailWidth * 0.12f;
        trailRenderer.minVertexDistance = 0.08f;
        trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;

        trailMaterial = CreateUrpTranslucentMaterial(Color.white, additive: true);
        trailRenderer.sharedMaterial = trailMaterial;
        ApplyTrailGradient();
        trailRenderer.emitting = false;
    }

    private void NeutralizeScale(Transform t)
    {
        Vector3 parentScale = transform.lossyScale;
        t.localScale = new Vector3(
            parentScale.x > 0.0001f ? 1f / parentScale.x : 1f,
            parentScale.y > 0.0001f ? 1f / parentScale.y : 1f,
            parentScale.z > 0.0001f ? 1f / parentScale.z : 1f
        );
    }

    private void ApplyTrailGradient()
    {
        if (trailRenderer == null) return;
        Color cMain = ActiveTrailMain;
        Color cEnd = ActiveTrailEnd;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(cMain, 0f),
                new GradientColorKey(cEnd, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(cMain.a, 0f),
                new GradientAlphaKey(cMain.a * 0.45f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trailRenderer.colorGradient = gradient;
    }

    public void SetBuffTint(bool buffed)
    {
        isBuffed = buffed;
        ApplyTrailGradient();
    }

    public void StartDashEffect()
    {
        if (trailRenderer != null)
        {
            NeutralizeScale(trailRenderer.transform);
            ApplyTrailGradient();
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }
    }

    /// <summary>Para de emitir deixando o rastro desaparecer suavemente no ar (sem corte seco).</summary>
    public void StopDashEffect()
    {
        if (trailRenderer != null)
            trailRenderer.emitting = false;
    }

    public void ForceClearTrail()
    {
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }
    }

    public void SpawnAfterImage()
    {
        if (!useAfterImage) return;

        if (ghostMeshMode == GhostMeshMode.Circle)
            SpawnCircleGhost();
        else
            SpawnSpiderMeshGhost();
    }

    private void SpawnSpiderMeshGhost()
    {
        SkinnedMeshRenderer source = targetRenderer;

        if (source == null)
        {
            SkinnedMeshRenderer[] all = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in all)
            {
                string meshName = smr.sharedMesh != null ? smr.sharedMesh.name.ToLower() : "";
                if (meshName.Contains("capsule") || meshName.Contains("cylinder") || meshName.Contains("cube"))
                    continue;
                if (smr.transform == transform)
                    continue;
                source = smr;
                break;
            }
        }

        if (source != null)
        {
            Mesh bakedMesh = new Mesh();
            source.BakeMesh(bakedMesh);

            if (bakedMesh.vertexCount == 0)
            {
                Destroy(bakedMesh);
                return;
            }

            GameObject ghost = new GameObject("SpiderGhost_Mesh");
            ghost.transform.position = source.transform.position;
            ghost.transform.rotation = source.transform.rotation;
            ghost.transform.localScale = source.transform.lossyScale * ghostScaleMultiplier;

            MeshFilter mf = ghost.AddComponent<MeshFilter>();
            mf.sharedMesh = bakedMesh;

            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Material ghostMat = CreateUrpTranslucentMaterial(ActiveGhostColor, additive: true);
            mr.sharedMaterial = ghostMat;

            activeGhosts.Add(ghost);
            StartCoroutine(FadeOutGhost(ghost, ghostMat, bakedMesh, afterImageDuration));
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend == trailRenderer || rend is TrailRenderer) continue;
            if (rend.transform == transform) continue;

            MeshFilter mf2 = rend.GetComponent<MeshFilter>();
            if (mf2 == null || mf2.sharedMesh == null) continue;

            string mName = mf2.sharedMesh.name.ToLower();
            if (mName.Contains("capsule") || mName.Contains("cylinder") || mName.Contains("cube")) continue;

            GameObject ghost = new GameObject("SpiderGhost_Static");
            ghost.transform.position = rend.transform.position;
            ghost.transform.rotation = rend.transform.rotation;
            ghost.transform.localScale = rend.transform.lossyScale * ghostScaleMultiplier;

            MeshFilter ghostMf = ghost.AddComponent<MeshFilter>();
            ghostMf.sharedMesh = mf2.sharedMesh;

            MeshRenderer ghostMr = ghost.AddComponent<MeshRenderer>();
            ghostMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostMr.receiveShadows = false;
            Material ghostMat = CreateUrpTranslucentMaterial(ActiveGhostColor, additive: true);
            ghostMr.sharedMaterial = ghostMat;

            activeGhosts.Add(ghost);
            StartCoroutine(FadeOutGhost(ghost, ghostMat, null, afterImageDuration));
            return;
        }
    }

    private void SpawnCircleGhost()
    {
        GameObject ghost = new GameObject("SpiderGhost_Circle");

        Vector3 spawnPos = transform.position;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, (1 << 0) | (1 << 3), QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * 0.03f;

        ghost.transform.position = spawnPos;
        ghost.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);

        Mesh circleMesh = CreateCircleMesh(circleRadius, circleSegments);
        MeshFilter mf = ghost.AddComponent<MeshFilter>();
        mf.sharedMesh = circleMesh;

        MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Material ghostMat = CreateUrpTranslucentMaterial(ActiveGhostColor, additive: true);
        mr.sharedMaterial = ghostMat;

        activeGhosts.Add(ghost);
        StartCoroutine(FadeOutGhost(ghost, ghostMat, circleMesh, afterImageDuration));
    }

    private static Mesh CreateCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 2 > segments) ? 1 : i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private System.Collections.IEnumerator FadeOutGhost(GameObject ghost, Material mat, Mesh meshToDispose, float duration)
    {
        float elapsed = 0f;
        Color startColor = ActiveGhostColor;
        Vector3 startScale = ghost != null ? ghost.transform.localScale : Vector3.one;
        float safeDur = Mathf.Max(0.05f, duration);

        while (elapsed < safeDur && ghost != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDur);
            Color c = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            ghost.transform.localScale = Vector3.Lerp(startScale, startScale * 0.86f, t);
            yield return null;
        }

        activeGhosts.Remove(ghost);
        if (mat != null) Destroy(mat);
        if (meshToDispose != null) Destroy(meshToDispose);
        if (ghost != null) Destroy(ghost);
    }

    public void SetTrailColor(Color mainColor, Color fadeColor)
    {
        trailColor = mainColor;
        trailEndColor = fadeColor;
        ApplyTrailGradient();
    }
}
