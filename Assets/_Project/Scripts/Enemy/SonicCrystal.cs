using UnityEngine;

public class SonicCrystal : MonoBehaviour
{
    [Header("Configurações de Status")]

    public float slowAmount = 1f; 
    public float slowDuration = 2f;
    public float knockbackForce = 70f;
    public float lifeTime = 3f; 


    [Header("Efeitos Visuais")]
    [Tooltip("Prefab de Particle System")]
    public GameObject breakEffectPrefab;

    [Header("Áudio — Explosão / Quebra")]
    [Tooltip("Sons reproduzidos quando o cristal explode ou se quebra")]
    public AudioClip[] explodeSounds;
    [Tooltip("Volume do som de explosão do cristal")]
    [Range(0f, 1f)]
    public float explodeVolume = 0.8f;

    private void Start()
    {
 
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
     
        bool isPlayer = other.CompareTag("Player");
        bool isAttack = other.name.Contains("Attack") || other.name.Contains("Weapon") || other.gameObject.layer == LayerMask.NameToLayer("PlayerAttack");

        if (isPlayer || isAttack)
        {
            if (isPlayer)
            {
                ApplyEffects(other.gameObject);
            }

            SelfDestruct();
        }
    }

void ApplyEffects(GameObject player)
{
    Rigidbody rb = player.GetComponent<Rigidbody>();
    if (rb != null)
    {

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0; 
   
        rb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
    }


}





    public void SelfDestruct()
    {
        PlayExplodeSound();

        if (breakEffectPrefab != null)
        {
            GameObject fx = Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 2f); 
        }

        Destroy(gameObject);
    }

    private void PlayExplodeSound()
    {
        if (explodeSounds == null || explodeSounds.Length == 0) return;
        AudioClip clip = explodeSounds[Random.Range(0, explodeSounds.Length)];
        if (clip != null)
        {
            GameObject audioObj = new GameObject("TempSonicCrystalAudio");
            audioObj.transform.position = transform.position;
            AudioSource aSource = audioObj.AddComponent<AudioSource>();
            aSource.clip = clip;
            aSource.pitch = Random.Range(0.9f, 1.1f);
            aSource.volume = explodeVolume;
            aSource.spatialBlend = 0.85f;
            aSource.minDistance = 3f;
            aSource.maxDistance = 35f;
            aSource.rolloffMode = AudioRolloffMode.Linear;
            aSource.Play();
            Destroy(audioObj, clip.length + 0.1f);
        }
    }
}