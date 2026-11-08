using UnityEngine;

public class SonicCrystal : MonoBehaviour
{
    [Header("Configurações de Status")]
    public int damage = 12;
    public float slowAmount = 1f; 
    public float slowDuration = 2f;
    public float knockbackForce = 22f; // Reduzido de 70f para evitar catapultar o jogador
    public float lifeTime = 3.5f; 

    [Header("Efeitos Visuais")]
    [Tooltip("Prefab de Particle System")]
    public GameObject breakEffectPrefab;

    [Header("Áudio — Explosão / Quebra")]
    [Tooltip("Sons reproduzidos quando o cristal explode ou se quebra")]
    public AudioClip[] explodeSounds;
    [Tooltip("Volume do som de explosão do cristal")]
    [Range(0f, 1f)]
    public float explodeVolume = 0.25f;

    private bool isDestroyed = false;

    // Proteção Anti-Burst / Anti-Shotgun compartilhada: impede que múltiplos cristais explodindo no mesmo instante zerem a vida do player
    private static float lastGlobalPlayerHitTime = -10f;
    private const float DAMAGE_COALESCING_WINDOW = 0.40f;

    private void Start()
    {
        Invoke(nameof(SelfDestruct), lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;

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
        bool canDealFullBurst = (Time.time - lastGlobalPlayerHitTime >= DAMAGE_COALESCING_WINDOW);

        if (canDealFullBurst)
        {
            lastGlobalPlayerHitTime = Time.time;

            PlayerHealth ph = player.GetComponent<PlayerHealth>() ?? player.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damage, gameObject);
            }

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 direction = player.transform.position - transform.position;
                direction.y = 0; 
                if (direction.sqrMagnitude < 0.01f) direction = -transform.forward;
                rb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
            }
        }
        else
        {
            // Proteção contra stack: se outro cristal explodiu há menos de 0.4s, causa apenas um dano residual mínimo
            PlayerHealth ph = player.GetComponent<PlayerHealth>() ?? player.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * 0.25f)), gameObject);
            }
        }
    }

    public void SelfDestruct()
    {
        if (isDestroyed) return;
        isDestroyed = true;

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
            float effectiveVol = Mathf.Min(explodeVolume, 0.25f) * 0.45f * GlobalAudioManager.SFXVolumeMultiplier;
            if (effectiveVol <= 0.001f) return;

            GameObject audioObj = new GameObject("TempSonicCrystalAudio");
            audioObj.transform.position = transform.position;
            AudioSource aSource = audioObj.AddComponent<AudioSource>();
            aSource.clip = clip;
            aSource.pitch = Random.Range(0.9f, 1.1f);
            aSource.volume = effectiveVol;
            aSource.spatialBlend = 0.85f;
            aSource.minDistance = 3f;
            aSource.maxDistance = 35f;
            aSource.rolloffMode = AudioRolloffMode.Linear;
            if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                aSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
            aSource.Play();
            Destroy(audioObj, clip.length + 0.1f);
        }
    }
}
