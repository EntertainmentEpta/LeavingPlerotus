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

    [Header("Efeitos Visuais / Sonoros")]
    public GameObject collectVfxPrefab;
    public AudioClip collectSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    private bool isCollected = false;
    private bool playerInRange = false;

    void Start()
    {
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
            col.radius = interactionRadius;
        }
    }

    void Update()
    {
        if (isCollected || autoCollectOnTrigger || !playerInRange) return;

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
