using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Variante 2 (Longa Distância — PDF Página 6): Clones Translúcidos na Volta & Ecos Fantasmas no Bounce.
/// Inspirado diretamente no DashHologram.cs (SharpBlur).
/// Renderiza:
/// 1. Clones translúcidos ("afterimages" holográficos) deixados pelo bumerangue na volta (Returning) e no voo.
/// 2. Ecos fantasmas autônomos gerados nos quiques (Bounces) que voam e giram com anéis translúcidos.
/// </summary>
public class BoomerangEchoVFX : MonoBehaviour
{
    [Header("Clones Translúcidos na Volta (Afterimages estilo SharpBlur)")]
    [Tooltip("Intervalo em segundos entre cada clone translúcido deixado na volta.")]
    public float returnCloneSpawnInterval = 0.045f;

    [Tooltip("Intervalo em segundos entre clones durante o arremesso de ida (quando a variante Eco está ativa).")]
    public float throwCloneSpawnInterval = 0.07f;

    [Tooltip("Duração de cada clone translúcido antes de desaparecer.")]
    public float cloneLifetime = 0.38f;

    [Tooltip("Opacidade inicial do clone translúcido (0 a 1).")]
    [Range(0.05f, 0.9f)]
    public float initialCloneAlpha = 0.52f;

    [Tooltip("Cor holográfica ciano/azul dos clones translúcidos.")]
    public Color echoCloneColor = new Color(0.25f, 0.92f, 1.0f, 0.52f);

    [Tooltip("Os clones translúcidos continuam girando suavemente enquanto desaparecem?")]
    public bool spinAfterimageWhileFading = true;

    [Header("Ecos Fantasmas de Ricochete (PDF Página 6)")]
    [Tooltip("Duração do eco fantasma que sai ricocheteando ao bater em parede/inimigo.")]
    public float bounceEchoDuration = 0.85f;

    [Tooltip("Velocidade de voo do eco fantasma de ricochete.")]
    public float bounceEchoSpeed = 14f;

    private struct MeshPartSnapshot
    {
        public Mesh mesh;
        public Matrix4x4 localToRootMatrix;
    }

    private class AfterimageSnapshot
    {
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 worldScale;
        public float elapsed;
        public float duration;
        public float extraSpinAngle;
        public List<MeshPartSnapshot> parts;
    }

    private class AutonomousBounceEcho
    {
        public Vector3 position;
        public Vector3 velocity;
        public float spinAngle;
        public float elapsed;
        public float duration;
        public LineRenderer ringLR;
        public GameObject ringGO;
    }

    private Transform visualRoot;
    private MeshFilter[] cachedMeshFilters;
    private readonly List<AfterimageSnapshot> activeAfterimages = new List<AfterimageSnapshot>();
    private readonly List<AutonomousBounceEcho> activeBounceEchoes = new List<AutonomousBounceEcho>();

    private Material baseEchoMaterial;
    private readonly List<Material> materialPool = new List<Material>();
    private int poolCursor;

    private float spawnTimer;
    private bool emitClonesActive;
    private bool isReturningMode;

    public void Initialize(Transform boomerangVisualRoot)
    {
        visualRoot = (boomerangVisualRoot != null && boomerangVisualRoot.IsChildOf(transform)) ? boomerangVisualRoot : transform;
        CacheMeshes();
        EnsureEchoMaterial();
    }

    private void CacheMeshes()
    {
        if (visualRoot == null) visualRoot = transform;
        cachedMeshFilters = visualRoot.GetComponentsInChildren<MeshFilter>(true);
    }

    private void EnsureEchoMaterial()
    {
        if (baseEchoMaterial != null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        baseEchoMaterial = new Material(shader);
        ConfigureTransparentMaterial(baseEchoMaterial, echoCloneColor);
    }

    private void ConfigureTransparentMaterial(Material mat, Color color)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
        }
        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 0);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.enableInstancing = true;
    }

    private Material GetPooledMaterial(float alpha)
    {
        EnsureEchoMaterial();
        if (poolCursor >= materialPool.Count)
        {
            Material m = new Material(baseEchoMaterial);
            materialPool.Add(m);
        }

        Material mat = materialPool[poolCursor++];
        Color c = echoCloneColor;
        c.a = alpha;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        return mat;
    }

    public void SetEchoEmission(bool active, bool isReturning)
    {
        emitClonesActive = active;
        isReturningMode = isReturning;
        if (!active)
            spawnTimer = 0f;
    }

    /// <summary>
    /// Captura instantaneamente um clone translúcido do bumerangue na posição/rotação atual.
    /// </summary>
    public void SpawnAfterimageSnapshot()
    {
        if (cachedMeshFilters == null || cachedMeshFilters.Length == 0)
            CacheMeshes();

        if (cachedMeshFilters == null || cachedMeshFilters.Length == 0)
            return;

        Transform root = visualRoot != null ? visualRoot : transform;
        Matrix4x4 worldToRoot = root.worldToLocalMatrix;

        List<MeshPartSnapshot> parts = new List<MeshPartSnapshot>(cachedMeshFilters.Length);
        for (int i = 0; i < cachedMeshFilters.Length; i++)
        {
            MeshFilter mf = cachedMeshFilters[i];
            if (mf == null || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy)
                continue;

            parts.Add(new MeshPartSnapshot
            {
                mesh = mf.sharedMesh,
                localToRootMatrix = worldToRoot * mf.transform.localToWorldMatrix
            });
        }

        if (parts.Count == 0) return;

        activeAfterimages.Add(new AfterimageSnapshot
        {
            worldPosition = root.position,
            worldRotation = root.rotation,
            worldScale = root.lossyScale,
            elapsed = 0f,
            duration = Mathf.Max(0.05f, cloneLifetime),
            extraSpinAngle = 0f,
            parts = parts
        });
    }

    /// <summary>
    /// Spawna um Eco Fantasma autônomo (PDF Página 6 — Bounce + Multishot) que sai voando e girando
    /// com anéis translúcidos de velocidade. Pronto para ser chamado nos ricochetes!
    /// </summary>
    public void SpawnBounceEchoClone(Vector3 origin, Vector3 flightDirection, float customSpeed = -1f, float customDuration = -1f)
    {
        if (cachedMeshFilters == null || cachedMeshFilters.Length == 0)
            CacheMeshes();

        Vector3 dir = flightDirection.sqrMagnitude > 0.001f ? flightDirection.normalized : transform.forward;
        float spd = customSpeed > 0f ? customSpeed : bounceEchoSpeed;
        float dur = customDuration > 0f ? customDuration : bounceEchoDuration;

        GameObject ringGO = new GameObject("BoomerangBounceEcho_Ring");
        LineRenderer lr = ringGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 28;
        lr.startWidth = 0.045f;
        lr.endWidth = 0.045f;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Shader s = Shader.Find("Sprites/Default");
        if (s != null) lr.material = new Material(s);

        // Desenha o anel circular translúcido ao redor do eco fantasma
        float radius = 0.65f;
        for (int i = 0; i < lr.positionCount; i++)
        {
            float angle = (float)i / lr.positionCount * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        activeBounceEchoes.Add(new AutonomousBounceEcho
        {
            position = origin,
            velocity = dir * spd,
            spinAngle = Random.Range(0f, 360f),
            elapsed = 0f,
            duration = dur,
            ringLR = lr,
            ringGO = ringGO
        });
    }

    public void UpdateEchoes(float deltaTime)
    {
        poolCursor = 0;

        if (emitClonesActive)
        {
            float interval = isReturningMode ? returnCloneSpawnInterval : throwCloneSpawnInterval;
            spawnTimer += deltaTime;
            if (spawnTimer >= interval)
            {
                spawnTimer = 0f;
                SpawnAfterimageSnapshot();
            }
        }

        // 1. Atualiza e desenha os Clones Translúcidos (Afterimages na volta/voo)
        for (int i = activeAfterimages.Count - 1; i >= 0; i--)
        {
            AfterimageSnapshot snap = activeAfterimages[i];
            snap.elapsed += deltaTime;
            if (snap.elapsed >= snap.duration)
            {
                activeAfterimages.RemoveAt(i);
                continue;
            }

            if (spinAfterimageWhileFading)
            {
                snap.extraSpinAngle += 720f * deltaTime;
            }

            float norm = 1f - (snap.elapsed / snap.duration);
            float alpha = initialCloneAlpha * norm * norm;
            Material mat = GetPooledMaterial(alpha);

            Quaternion rot = snap.worldRotation * Quaternion.Euler(0f, 0f, snap.extraSpinAngle);
            Matrix4x4 rootMatrix = Matrix4x4.TRS(snap.worldPosition, rot, snap.worldScale);

            for (int p = 0; p < snap.parts.Count; p++)
            {
                MeshPartSnapshot part = snap.parts[p];
                if (part.mesh == null) continue;
                Graphics.DrawMesh(part.mesh, rootMatrix * part.localToRootMatrix, mat, gameObject.layer);
            }
        }

        // 2. Atualiza e desenha os Ecos Fantasmas de Ricochete (PDF Página 6)
        Transform root = visualRoot != null ? visualRoot : transform;
        Matrix4x4 currentWorldToRoot = root.worldToLocalMatrix;

        for (int i = activeBounceEchoes.Count - 1; i >= 0; i--)
        {
            AutonomousBounceEcho echo = activeBounceEchoes[i];
            echo.elapsed += deltaTime;
            if (echo.elapsed >= echo.duration)
            {
                if (echo.ringGO != null) Destroy(echo.ringGO);
                activeBounceEchoes.RemoveAt(i);
                continue;
            }

            echo.position += echo.velocity * deltaTime;
            echo.spinAngle = (echo.spinAngle + 1600f * deltaTime) % 360f;

            float norm = 1f - (echo.elapsed / echo.duration);
            float alpha = (initialCloneAlpha * 1.15f) * norm;
            Material mat = GetPooledMaterial(alpha);

            Quaternion echoRot = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, echo.spinAngle);
            Matrix4x4 echoRootMatrix = Matrix4x4.TRS(echo.position, echoRot, root.lossyScale * 0.92f);

            if (echo.ringGO != null)
            {
                echo.ringGO.transform.position = echo.position;
                echo.ringGO.transform.rotation = Quaternion.Euler(0f, echo.spinAngle * 0.5f, 0f);
                Color ringColor = new Color(echoCloneColor.r, echoCloneColor.g, 1f, alpha * 0.8f);
                echo.ringLR.startColor = ringColor;
                echo.ringLR.endColor = ringColor;
            }

            if (cachedMeshFilters != null)
            {
                for (int m = 0; m < cachedMeshFilters.Length; m++)
                {
                    MeshFilter mf = cachedMeshFilters[m];
                    if (mf == null || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy)
                        continue;

                    Matrix4x4 localMat = currentWorldToRoot * mf.transform.localToWorldMatrix;
                    Graphics.DrawMesh(mf.sharedMesh, echoRootMatrix * localMat, mat, gameObject.layer);
                }
            }
        }
    }

    public void ClearAllEchoes()
    {
        activeAfterimages.Clear();
        for (int i = 0; i < activeBounceEchoes.Count; i++)
        {
            if (activeBounceEchoes[i].ringGO != null)
                Destroy(activeBounceEchoes[i].ringGO);
        }
        activeBounceEchoes.Clear();
    }

    private void OnDestroy()
    {
        ClearAllEchoes();
        if (baseEchoMaterial != null) Destroy(baseEchoMaterial);
        for (int i = 0; i < materialPool.Count; i++)
        {
            if (materialPool[i] != null) Destroy(materialPool[i]);
        }
    }
}
