using UnityEngine;
using System.Collections;

/// <summary>
/// Sistema completo de som 3D para o Boss Geobionte (todas as fases).
/// - Integra com GlobalAudioManager (SFX group e multiplicador de volume)
/// - Passos dinâmicos por intervalo de movimento (com variação de pitch)
/// - Sons para todos os ataques melee (swings e impactos de punho/garra)
/// - Sons de Stomp (impacto sísmico no chão) e Salto
/// - Sons de Magias (Casulo, Espinhos, Cuspe Ácido e Salva de Espinhos)
/// - Sons de Transição de Fase, Rugido/PowerUp, Dano e Derrota
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BossAudio : MonoBehaviour
{
    [Header("Componentes")]
    public AudioSource audioSource;
    private BossController bossController;
    private DummyHealth dummyHealth;

    [Header("Passos / Movimento")]
    [Tooltip("Sons de passos do Boss (Geobionte/Golem).")]
    public AudioClip[] footstepSounds;
    [Range(0f, 1f)] public float footstepVolume = 0.55f;
    public float walkStepInterval = 0.48f;
    public float sprintStepInterval = 0.30f;
    private float stepTimer = 0f;

    [Header("Ataques Melee (Swings no Ar)")]
    public AudioClip[] attackSwingSounds;
    [Range(0f, 1f)] public float swingVolume = 0.85f;

    [Header("Impacto no Chão / Stomp / Salto")]
    public AudioClip[] stompImpactSounds;
    [Range(0f, 1f)] public float stompVolume = 0.95f;

    [Header("Magias & Espinhos (Fases 1 e 2)")]
    public AudioClip[] spellCastSounds;
    public AudioClip[] crystalSpikeSounds;
    [Range(0f, 1f)] public float spellVolume = 0.85f;

    [Header("Habilidades da Fase 3 (Cuspe Ácido & Salva de Espinhos)")]
    public AudioClip[] acidSpitSounds;
    public AudioClip[] thornVolleySounds;
    [Range(0f, 1f)] public float phase3SkillsVolume = 0.90f;

    [Header("Rugido / Transição de Fase / PowerUP")]
    public AudioClip[] roarPowerUpSounds;
    [Range(0f, 1f)] public float roarVolume = 1.0f;

    [Header("Feedback de Dano (Hit)")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float hitVolume = 0.70f;
    private float lastHitSoundTime = -1f;

    [Header("Derrota / Morte (Die)")]
    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float deathVolume = 1.0f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.spatialBlend = 1.0f; // 3D total
            audioSource.minDistance = 4.0f;
            audioSource.maxDistance = 35.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.playOnAwake = false;

            if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
            {
                audioSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
            }
        }

        bossController = GetComponent<BossController>() ?? GetComponentInParent<BossController>();
        dummyHealth = GetComponent<DummyHealth>() ?? GetComponentInChildren<DummyHealth>();

        if (dummyHealth != null)
        {
            dummyHealth.onDamageTaken += OnBossDamageTaken;
            dummyHealth.onDeath += PlayDeathSound;
        }

        AutoPopulateClipsIfEmpty();
    }

    private void OnDestroy()
    {
        if (dummyHealth != null)
        {
            dummyHealth.onDamageTaken -= OnBossDamageTaken;
            dummyHealth.onDeath -= PlayDeathSound;
        }
    }

    private void Update()
    {
        HandleFootsteps();
    }

    // =========================================================================
    // PASSOS DO BOSS
    // =========================================================================
    private void HandleFootsteps()
    {
        if (bossController == null || bossController.IsDead || bossController.IsStunned) return;

        // Se estiver executando animação de ataque corpo a corpo, pausa os passos
        if (bossController.isAttacking) return;

        bool isMoving = false;
        bool isSprinting = false;

        var agent = bossController.agent;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            float speed = agent.velocity.magnitude;
            isMoving = speed > 0.2f;
            isSprinting = speed > 5.5f;
        }

        if (isMoving)
        {
            stepTimer -= Time.deltaTime;
            float interval = isSprinting ? sprintStepInterval : walkStepInterval;

            if (stepTimer <= 0f)
            {
                PlayFootstep();
                stepTimer = interval;
            }
        }
        else
        {
            stepTimer = 0.1f;
        }
    }

    public void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0) return;
        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        PlayClipRandomPitch(clip, footstepVolume, 0.85f, 1.15f);
    }

    // =========================================================================
    // ATAQUES MELEE & IMPACTOS
    // =========================================================================
    public void PlayAttackSwing(string attackTrigger = "")
    {
        if (attackSwingSounds == null || attackSwingSounds.Length == 0) return;
        AudioClip clip = attackSwingSounds[Random.Range(0, attackSwingSounds.Length)];
        PlayClipRandomPitch(clip, swingVolume, 0.90f, 1.10f);
    }

    public void PlayStompImpact()
    {
        if (stompImpactSounds == null || stompImpactSounds.Length == 0) return;
        AudioClip clip = stompImpactSounds[Random.Range(0, stompImpactSounds.Length)];
        PlayClipRandomPitch(clip, stompVolume, 0.85f, 1.05f);
    }

    public void PlayJumpImpact()
    {
        if (stompImpactSounds == null || stompImpactSounds.Length == 0) return;
        AudioClip clip = stompImpactSounds[Random.Range(0, stompImpactSounds.Length)];
        PlayClipRandomPitch(clip, stompVolume, 0.80f, 0.95f);
    }

    // =========================================================================
    // MAGIAS & HABILIDADES
    // =========================================================================
    public void PlaySpellCast()
    {
        if (spellCastSounds == null || spellCastSounds.Length == 0) return;
        AudioClip clip = spellCastSounds[Random.Range(0, spellCastSounds.Length)];
        PlayClipRandomPitch(clip, spellVolume, 0.95f, 1.15f);
    }

    public void PlayCrystalSpikes()
    {
        if (crystalSpikeSounds == null || crystalSpikeSounds.Length == 0) return;
        AudioClip clip = crystalSpikeSounds[Random.Range(0, crystalSpikeSounds.Length)];
        PlayClipRandomPitch(clip, spellVolume, 0.90f, 1.10f);
    }

    public void PlayAcidSpit()
    {
        if (acidSpitSounds != null && acidSpitSounds.Length > 0)
        {
            AudioClip clip = acidSpitSounds[Random.Range(0, acidSpitSounds.Length)];
            PlayClipRandomPitch(clip, phase3SkillsVolume, 0.90f, 1.15f);
        }
    }

    public void PlayThornVolley()
    {
        if (thornVolleySounds != null && thornVolleySounds.Length > 0)
        {
            AudioClip clip = thornVolleySounds[Random.Range(0, thornVolleySounds.Length)];
            PlayClipRandomPitch(clip, phase3SkillsVolume, 0.95f, 1.10f);
        }
        else
        {
            PlayCrystalSpikes();
        }
    }

    public void PlayRoarOrPhaseTransition()
    {
        if (roarPowerUpSounds == null || roarPowerUpSounds.Length == 0) return;
        AudioClip clip = roarPowerUpSounds[Random.Range(0, roarPowerUpSounds.Length)];
        PlayClipRandomPitch(clip, roarVolume, 0.85f, 1.05f);
    }

    // =========================================================================
    // DANO & MORTE
    // =========================================================================
    private void OnBossDamageTaken(int damage, GameObject source)
    {
        // Evita sobreposição de hits em caso de multi-ticks em menos de 0.15s
        if (Time.time - lastHitSoundTime < 0.15f) return;
        lastHitSoundTime = Time.time;

        if (hitSounds == null || hitSounds.Length == 0) return;
        AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
        PlayClipRandomPitch(clip, hitVolume, 0.90f, 1.10f);
    }

    public void PlayDeathSound()
    {
        if (deathSounds == null || deathSounds.Length == 0) return;
        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        PlayClipRandomPitch(clip, deathVolume, 0.85f, 1.0f);
    }

    // =========================================================================
    // UTILITÁRIOS DE TOCAR SOM
    // =========================================================================
    private void PlayClipRandomPitch(AudioClip clip, float volume, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        if (clip == null) return;

        float masterSFX = GlobalAudioManager.SFXVolumeMultiplier;
        float finalVolume = volume * masterSFX;
        if (finalVolume <= 0.001f) return;

        if (audioSource != null)
        {
            if (audioSource.outputAudioMixerGroup == null && GlobalAudioManager.Instance != null)
            {
                audioSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
            }
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(clip, finalVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, finalVolume);
        }
    }

    // =========================================================================
    // AUTO-POPULAÇÃO DE ÁUDIOS DO PROJETO CASO OS SLOTS ESTEJAM VAZIOS
    // =========================================================================
    public void AutoPopulateClipsIfEmpty()
    {
#if UNITY_EDITOR
        bool dirty = false;

        if (footstepSounds == null || footstepSounds.Length == 0)
        {
            footstepSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Geobionte/legs1.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Geobionte/legs2.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Geobionte/legs3.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Golem/golemWaking1.mp3"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Golem/golemWalking3.mp3")
            };
            dirty = true;
        }

        if (attackSwingSounds == null || attackSwingSounds.Length == 0)
        {
            attackSwingSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Weapons/Axe/Combo/Swing_01.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Weapons/Axe/Combo/Swing_02.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Weapons/Axe/Combo/Swing_03.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Weapons/Axe/Combo/Swing_04.wav")
            };
            dirty = true;
        }

        if (stompImpactSounds == null || stompImpactSounds.Length == 0)
        {
            stompImpactSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Golem/golemStomp.mp3"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Weapons/Axe/ULT/impact.wav")
            };
            dirty = true;
        }

        if (spellCastSounds == null || spellCastSounds.Length == 0)
        {
            spellCastSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/MagicStone/magicStoneZap.mp3"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/MagicStone/magicStoneTelegraphSound.mp3")
            };
            dirty = true;
        }

        if (crystalSpikeSounds == null || crystalSpikeSounds.Length == 0)
        {
            crystalSpikeSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Cristalus/CristalusCrystalSpawn.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Cristalus/CristalusCrystalExplosion.wav")
            };
            dirty = true;
        }

        if (acidSpitSounds == null || acidSpitSounds.Length == 0)
        {
            acidSpitSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Spider/spiderWalking2.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/MagicStone/magicStoneZap.mp3")
            };
            dirty = true;
        }

        if (thornVolleySounds == null || thornVolleySounds.Length == 0)
        {
            thornVolleySounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Cristalus/CristalusCrystalSpawn.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Cristalus/CristalusCrystalExplosion.wav")
            };
            dirty = true;
        }

        if (roarPowerUpSounds == null || roarPowerUpSounds.Length == 0)
        {
            roarPowerUpSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/CrystalWatcher/charging.mp3"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/Weapons/Axe/ULT/charge.wav")
            };
            dirty = true;
        }

        if (hitSounds == null || hitSounds.Length == 0)
        {
            hitSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/StoneEnemies/stoneHit.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/CrystalEnemies/crystalHit.mp3")
            };
            dirty = true;
        }

        if (deathSounds == null || deathSounds.Length == 0)
        {
            deathSounds = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/StoneEnemies/StoneDying.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SoundFX/CrystalEnemies/crystalDying.mp3")
            };
            dirty = true;
        }

        if (dirty)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
