using UnityEngine;
using System.Collections;

/// <summary>
/// Componente de Barreira / Borda de Sala.
/// Controla a física sólida (BoxCollider), malha visual e a animação procedural
/// de emergir do chão (rise) e afundar na terra (lower) quando a sala tranca e destranca.
/// </summary>
[SelectionBase]
public class RoomBarrier : MonoBehaviour
{
    [Header("Colisor Sólido da Barreira")]
    public BoxCollider barrierCollider;

    [Header("Componentes Visuais da Barreira")]
    public GameObject visualRoot;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    [Header("Animação Procedural do Chão")]
    [Tooltip("Duração da animação de subida (emergindo do chão) em segundos.")]
    public float riseDuration = 1.2f;

    [Tooltip("Duração da animação de descida (afundando no chão) em segundos.")]
    public float lowerDuration = 0.8f;

    [Tooltip("Profundidade subterrânea inicial em metros.")]
    public float sinkDepth = 8.5f;

    [Tooltip("Som ao subir do chão (opcional).")]
    public AudioClip riseSound;

    // Estado interno
    private Vector3 restingPosition;
    private bool hasRecordedRestingPos = false;
    private Coroutine currentAnimRoutine;
    private bool isCurrentlyLocked = false;
    public bool IsLocked => isCurrentlyLocked;

    private void Awake()
    {
        RecordRestingPositionIfNeeded();

        if (barrierCollider == null)
            barrierCollider = GetComponent<BoxCollider>() ?? GetComponentInChildren<BoxCollider>();

        if (visualRoot == null)
        {
            Transform v = transform.Find("Barrier_Mesh") ?? transform.Find("Visual");
            if (v != null) visualRoot = v.gameObject;
        }

        if (meshFilter == null)
            meshFilter = GetComponentInChildren<MeshFilter>(true);

        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>(true);
    }

    private void OnEnable()
    {
        RecordRestingPositionIfNeeded();
        if (!isCurrentlyLocked)
        {
            SetAllColliders(isTrigger: true, enabled: false);
        }
    }

    private void OnDisable()
    {
        if (currentAnimRoutine != null)
        {
            StopCoroutine(currentAnimRoutine);
            currentAnimRoutine = null;
        }

        // Se o objeto for desativado, NUNCA deixa colisores sólidos ativos
        SetAllColliders(isTrigger: true, enabled: false);
    }

    /// <summary>
    /// Configura de uma só vez todos os colisores no objeto e em seus filhos.
    /// Evita qualquer colisão invisível residual deixada por colliders de filhos.
    /// </summary>
    /// <summary>
    /// Força o recolhimento imediato e desligamento dos colisores da barreira.
    /// </summary>
    public void ForceLowerImmediate()
    {
        SetLocked(false, animate: false);
        SetAllColliders(isTrigger: true, enabled: false);
    }

    public void SetAllColliders(bool isTrigger, bool enabled)
    {
        Collider[] allCols = GetComponentsInChildren<Collider>(true);
        if (allCols != null)
        {
            foreach (var col in allCols)
            {
                if (col != null)
                {
                    col.isTrigger = isTrigger;
                    col.enabled = enabled;
                }
            }
        }
        if (barrierCollider != null)
        {
            barrierCollider.isTrigger = isTrigger;
            barrierCollider.enabled = enabled;
        }
    }

    /// <summary>
    /// Registra a posição de repouso (nível do chão correto) da barreira.
    /// </summary>
    public void RecordRestingPositionIfNeeded()
    {
        if (!hasRecordedRestingPos)
        {
            restingPosition = transform.position;
            hasRecordedRestingPos = true;
        }
    }

    /// <summary>
    /// Ativa ou desativa a barreira física e visual com suporte a animação saindo do chão.
    /// </summary>
    public void SetLocked(bool locked, bool animate = true)
    {
        RecordRestingPositionIfNeeded();

        if (currentAnimRoutine != null)
        {
            StopCoroutine(currentAnimRoutine);
            currentAnimRoutine = null;
        }

        if (locked)
        {
            isCurrentlyLocked = true;
            gameObject.SetActive(true);
            SetVisualActive(true);

            if (animate && gameObject.activeInHierarchy)
            {
                currentAnimRoutine = StartCoroutine(AnimateRise());
            }
            else
            {
                transform.position = restingPosition;
                SetAllColliders(isTrigger: false, enabled: true);
            }
        }
        else
        {
            isCurrentlyLocked = false;

            // Libera passagem física IMEDIATAMENTE (sem colisão invisível residual)
            SetAllColliders(isTrigger: true, enabled: false);

            if (animate && gameObject.activeInHierarchy)
            {
                currentAnimRoutine = StartCoroutine(AnimateLower());
            }
            else
            {
                SetVisualActive(false);
                transform.position = restingPosition - Vector3.up * sinkDepth;
                gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator AnimateRise()
    {
        Vector3 sunkenPos = restingPosition - Vector3.up * sinkDepth;
        transform.position = sunkenPos;

        // Enquanto emerge do chão, os colisores ficam como trigger para não arremessar nem travar o player
        SetAllColliders(isTrigger: true, enabled: true);

        PlayRiseSound();

        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);

            // Ease-out cúbico suave: emerge com ímpeto da rocha e desacelera com firmeza no topo
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(sunkenPos, restingPosition, ease);

            // Aos 70% da subida, torna os colisores sólidos para barrar passagem
            if (t >= 0.70f)
            {
                SetAllColliders(isTrigger: false, enabled: true);
            }

            yield return null;
        }

        transform.position = restingPosition;
        SetAllColliders(isTrigger: false, enabled: true);
        currentAnimRoutine = null;
    }

    private IEnumerator AnimateLower()
    {
        // Ao destravar, libera a passagem física imediatamente (sem colisão invisível residual)
        SetAllColliders(isTrigger: true, enabled: false);

        Vector3 startPos = transform.position;
        Vector3 sunkenPos = restingPosition - Vector3.up * sinkDepth;

        float elapsed = 0f;
        while (elapsed < lowerDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lowerDuration);

            // Garante que os colisores NUNCA reativem durante a descida
            SetAllColliders(isTrigger: true, enabled: false);

            // Ease-in cúbico suave: afunda na terra
            float ease = t * t * t;
            transform.position = Vector3.Lerp(startPos, sunkenPos, ease);

            yield return null;
        }

        // Permanece afundada no chão (sunkenPos) - NUNCA teleporta de volta para cima!
        transform.position = sunkenPos;
        SetAllColliders(isTrigger: true, enabled: false);
        SetVisualActive(false);
        gameObject.SetActive(false);
        currentAnimRoutine = null;
    }

    private void SetVisualActive(bool active)
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(active);
        }
        else if (meshRenderer != null)
        {
            meshRenderer.enabled = active;
        }
    }

    private void PlayRiseSound()
    {
        if (riseSound != null)
        {
            AudioSource.PlayClipAtPoint(riseSound, transform.position, 0.75f);
        }
    }

    /// <summary>
    /// Atualiza o Mesh da barreira dinamicamente.
    /// </summary>
    public void AssignMesh(Mesh newMesh, Material newMaterial = null)
    {
        if (meshFilter != null && newMesh != null)
        {
            meshFilter.sharedMesh = newMesh;
        }
        if (meshRenderer != null && newMaterial != null)
        {
            meshRenderer.sharedMaterial = newMaterial;
        }
    }
}
