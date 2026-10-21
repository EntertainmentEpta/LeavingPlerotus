using UnityEngine;

/// <summary>
/// Controla a atmosfera velada e sutil da Sala de Anomalia (Fissura de Necrose de Plerotus).
/// Desativa música de combate, reduz iluminação vibrante para tons leitosos e foscos,
/// e instancia o Nódulo de Rejeição Biológica no centro da sala.
/// </summary>
public class AnomalyRoomController : MonoBehaviour
{
    [Header("Atmosfera e Áudio")]
    [Tooltip("Áudio ambiente de estática biológica / vácuo grave")]
    public AudioClip anomalyAtmosphereClip;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;

    [Header("Nódulo de Rejeição")]
    public GameObject nodulePrefab;
    public Transform noduleSpawnPoint;

    private AudioSource audioSource;
    private bool playerInside = false;

    void Awake()
    {
        // Configura AudioSource próprio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.volume = ambientVolume;
        if (anomalyAtmosphereClip != null) audioSource.clip = anomalyAtmosphereClip;

        // Garante um Trigger Collider para detecção de entrada/saída da câmara
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(20f, 6f, 20f);
            box.center = new Vector3(0, 3f, 0);
        }
        else
        {
            col.isTrigger = true;
        }

        // Garante a presença do Nódulo
        EnsureNoduleSpawned();
    }

    private void EnsureNoduleSpawned()
    {
        if (GetComponentInChildren<AnomalyNodule>() != null) return;

        Vector3 spawnPos = (noduleSpawnPoint != null) ? noduleSpawnPoint.position : (transform.position + Vector3.up * 0.8f);

        if (nodulePrefab != null)
        {
            Instantiate(nodulePrefab, spawnPos, Quaternion.identity, transform);
        }
        else
        {
            // Cria um nódulo procedural leitoso como fallback elegante
            GameObject noduleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            noduleObj.name = "Nodulo_Rejeicao_Procedural";
            noduleObj.transform.position = spawnPos;
            noduleObj.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            noduleObj.transform.SetParent(transform, true);

            Renderer rend = noduleObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.75f, 0.80f, 0.85f, 0.9f); // Tonalidade leitosa
            }

            SphereCollider col = noduleObj.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;

            noduleObj.AddComponent<AnomalyNodule>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (playerInside) return;

        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerInside = true;
            OnPlayerEnteredAnomalyZone();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!playerInside) return;

        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            playerInside = false;
            OnPlayerExitedAnomalyZone();
        }
    }

    private void OnPlayerEnteredAnomalyZone()
    {
        Debug.Log("🌫️ [ANOMALIA] O jogador entrou em uma Fissura de Necrose de Plerotus.");

        // Diminui música / atenua BGM para criar a atmosfera desolada e silenciosa
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            GlobalAudioManager.Instance.mainMixer.SetFloat("BGMVolume", -25f);
        }

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }

        // Notificação sutil do Eptinho
        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso("🌫️ <i>\"Silêncio estático... o micélio de Plerotus falhou em se estender a esta câmara. O solo aqui é inerte.\"</i>");
        }
    }

    private void OnPlayerExitedAnomalyZone()
    {
        // Restaura música com base no volume salvo
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            float bgmVal = PlayerPrefs.GetFloat("SavedBGM", 0.5f);
            GlobalAudioManager.Instance.mainMixer.SetFloat("BGMVolume", Mathf.Log10(Mathf.Clamp(bgmVal, 0.0001f, 1f)) * 20f);
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
