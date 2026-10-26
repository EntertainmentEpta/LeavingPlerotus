using UnityEngine;

/// <summary>
/// Nódulo de Rejeição Biológica de Plerotus.
/// Localizado no centro da Sala de Anomalia (Fissura de Necrose).
/// Ao interagir, entrega uma escritura ancestral rara e uma bênção/mutação instável para a run.
/// </summary>
public class AnomalyNodule : MonoBehaviour
{
    [Header("Escritura Rara Garantida")]
    public LoreScriptureData rareScriptureData;
    public string defaultScriptureId = "scripture_blood_tragedy";

    [Header("Interação")]
    public float interactionDistance = 3.0f;
    public KeyCode interactKey = KeyCode.F;

    [Header("Feedback")]
    public GameObject collectVfx;
    public AudioClip interactionSfx;

    private bool hasInteracted = false;
    private bool playerNearby = false;

    void Start()
    {
        if (rareScriptureData == null && LoreManager.Instance != null)
        {
            rareScriptureData = LoreManager.Instance.GetScripture(defaultScriptureId);
        }
    }

    void Update()
    {
        if (hasInteracted) return;

        if (playerNearby && (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E)))
        {
            Interact();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasInteracted) return;

        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerNearby = true;
            // Mostra aviso de interação
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso("🔬 <color=#4DEEEA>NÓDULO DE REJEIÇÃO BIOLÓGICA</color>\nPressione <color=#FFE500>[F]</color> para extrair amostras do tecido necrosado.");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerNearby = false;
        }
    }

    public void Interact()
    {
        if (hasInteracted) return;
        hasInteracted = true;

        if (rareScriptureData == null && LoreManager.Instance != null)
        {
            rareScriptureData = LoreManager.Instance.GetScripture(defaultScriptureId);
        }

        // 1. Cataloga a escritura rara
        if (rareScriptureData != null && LoreManager.Instance != null)
        {
            LoreManager.Instance.CollectScripture(rareScriptureData);
        }

        // 2. Concede bônus instável de run: +30 Essências e cura de armadura
        PlayerHealth pHealth = Object.FindFirstObjectByType<PlayerHealth>();
        if (pHealth != null)
        {
            pHealth.ModifyAttribute("MaxArmor", 25f, false);
            pHealth.RestoreFullArmor();
            Debug.Log("🧬 [ANOMALIA] O Nódulo de Rejeição infundiu matéria pura: +25 de Armadura Máxima!");
        }

        // 3. Efeitos visuais e sonoros
        if (collectVfx != null)
        {
            Instantiate(collectVfx, transform.position, Quaternion.identity);
        }

        if (interactionSfx != null)
        {
            AudioSource.PlayClipAtPoint(interactionSfx, transform.position, 1.0f);
        }

        // Desativa mesh do nódulo
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 1.0f);
    }
}
