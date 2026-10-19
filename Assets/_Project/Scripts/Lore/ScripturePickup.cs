using UnityEngine;

/// <summary>
/// Objeto interativo no mapa que representa um monólito, placa ou fóssil de escritura ancestral.
/// Ao aproximar-se ou interagir, cataloga a escritura no LoreManager e notifica o Eptinho.
/// </summary>
public class ScripturePickup : MonoBehaviour
{
    [Header("Escritura Associada")]
    [Tooltip("Arraste o asset de LoreScriptureData ou especifique o scriptureId")]
    public LoreScriptureData scriptureData;
    public string scriptureIdFallback = "scripture_acid_filter";

    [Header("Configurações de Interação")]
    [Tooltip("Coleta automática ao encostar no colisor (trigger)? Se false, exige tecla E")]
    public bool autoCollectOnTrigger = true;
    public KeyCode interactKey = KeyCode.E;
    public float interactionRadius = 2.5f;

    [Header("Efeitos & Áudio")]
    public GameObject collectVfxPrefab;
    public AudioClip collectSound;
    public float soundVolume = 0.85f;

    [Header("Animação")]
    public float bobSpeed = 2.4f;
    public float bobHeight = 0.12f;
    public float rotateSpeed = 40f;

    private bool isCollected = false;
    private bool playerInRange = false;
    private Vector3 basePosition;

    void Start()
    {
        basePosition = transform.position;

        // Se scriptureData não foi arrastado, busca via LoreManager
        if (scriptureData == null && LoreManager.Instance != null && !string.IsNullOrEmpty(scriptureIdFallback))
        {
            scriptureData = LoreManager.Instance.GetScripture(scriptureIdFallback);
        }

        // Garante a presença de um SphereCollider trigger
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = Mathf.Max(interactionRadius, 3.0f);
        }
        else
        {
            col.isTrigger = true;
            col.radius = Mathf.Max(col.radius, 3.0f);
        }
    }

    void Update()
    {
        if (isCollected) return;

        // Flutuação suave e rotação mística
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

        if (autoCollectOnTrigger || !playerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            Collect();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerInRange = true;

            if (autoCollectOnTrigger)
            {
                Collect();
            }
            else
            {
                if (EptinhoPopupController.instancia != null)
                {
                    string title = scriptureData != null ? scriptureData.title : "Escritura Ancestral";
                    EptinhoPopupController.instancia.MostrarPopupAviso($"📜 <color=#4DEEEA>{title.ToUpper()}</color>\nPressione <color=#FFE500>[E]</color> para catalogar a página do diário.");
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerInRange = false;
        }
    }

    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (scriptureData == null && LoreManager.Instance != null)
        {
            scriptureData = LoreManager.Instance.GetScripture(scriptureIdFallback);
        }

        if (scriptureData != null && LoreManager.Instance != null)
        {
            LoreManager.Instance.CollectScripture(scriptureData);
        }
        else
        {
            Debug.LogWarning($"[ScripturePickup] {gameObject.name} foi coletado mas scriptureData é nulo!");
        }

        // Spawn de VFX
        if (collectVfxPrefab != null)
        {
            Instantiate(collectVfxPrefab, transform.position, Quaternion.identity);
        }

        // Áudio
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position, soundVolume);
        }

        // Desativa renderers/colisores para feedback imediato
        Collider c = GetComponent<Collider>();
        if (c != null) c.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;

        Destroy(gameObject, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
