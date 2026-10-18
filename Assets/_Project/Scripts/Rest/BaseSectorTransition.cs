using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BaseSectorTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("The spawn point where the player will be positioned in the target sector.")]
    public Transform spawnPoint;

    [Tooltip("The GameObject representing the sector to activate (e.g. Room the player is entering).")]
    public GameObject sectorToActivate;

    [Tooltip("The GameObject representing the sector to deactivate (e.g. Room the player is leaving).")]
    public GameObject sectorToDeactivate;

    [Tooltip("How long to wait while screen is completely black (useful to allow assets to load/settle).")]
    public float blackScreenDuration = 0.2f;

    // Static transition lock to prevent overlapping transitions
    private static bool isTransitioning = false;
    private static float transitionStartTime = 0f;
    private static float nextAllowedTransitionTime = 0f;
    private const float MAX_TRANSITION_TIMEOUT = 2.5f;

    // Temporary immunity: maps player collider to timestamp when immunity expires.
    // Replaces the old indefinite HashSet<Collider> which permanently locked doors if OnTriggerExit missed.
    private readonly Dictionary<Collider, float> temporaryImmunity = new Dictionary<Collider, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticStateDomain()
    {
        isTransitioning = false;
        transitionStartTime = 0f;
        nextAllowedTransitionTime = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticStateScene()
    {
        isTransitioning = false;
        transitionStartTime = 0f;
        nextAllowedTransitionTime = 0f;
    }

    private void Awake()
    {
        // Fail-safe: if transition was left stuck on scene load or past timeout, auto-recover
        if (isTransitioning && Time.unscaledTime - transitionStartTime > MAX_TRANSITION_TIMEOUT)
        {
            isTransitioning = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryStartTransition(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryStartTransition(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && temporaryImmunity.ContainsKey(other))
        {
            temporaryImmunity.Remove(other);
            Debug.Log($"[BaseSectorTransition] {other.name} saiu de {gameObject.name}; imunidade liberada.");
        }
    }

    private bool IsColliderImmune(Collider other)
    {
        if (other == null) return false;

        if (temporaryImmunity.TryGetValue(other, out float expiryTime))
        {
            if (Time.time < expiryTime)
            {
                return true; // Still within grace period
            }
            // Immunity expired automatically
            temporaryImmunity.Remove(other);
        }
        return false;
    }

    public void GrantTemporaryImmunity(Collider playerCollider, float duration = 1.0f)
    {
        if (playerCollider != null)
        {
            temporaryImmunity[playerCollider] = Time.time + duration;
            Debug.Log($"[BaseSectorTransition] Concedendo imunidade temporária ({duration:F1}s) a {playerCollider.name} no portal {gameObject.name}");
        }
    }

    private void TryStartTransition(Collider other)
    {
        // 1. Check if temporary immunity is active (just teleported here)
        if (IsColliderImmune(other))
        {
            return;
        }

        // 2. Watchdog check: Auto-recover if a previous transition hung
        if (isTransitioning)
        {
            if (Time.unscaledTime - transitionStartTime > MAX_TRANSITION_TIMEOUT)
            {
                Debug.LogWarning("[BaseSectorTransition] Watchdog: Transição anterior expirou por timeout (2.5s). Resetando isTransitioning.");
                isTransitioning = false;
            }
            else
            {
                return; // Transition actively running
            }
        }

        // 3. Safety cooldown check to prevent double-fires on the same physics tick
        if (Time.time < nextAllowedTransitionTime)
        {
            return;
        }

        // 4. Find Player component (check both component and parent)
        PlayerM playerMovement = other.GetComponent<PlayerM>() ?? other.GetComponentInParent<PlayerM>();
        if (playerMovement != null)
        {
            Debug.Log($"[BaseSectorTransition] Iniciando transição do setor via {gameObject.name}");
            playerMovement.StartCoroutine(DoTransition(playerMovement, other));
        }
    }

    private IEnumerator DoTransition(PlayerM player, Collider playerCollider)
    {
        isTransitioning = true;
        transitionStartTime = Time.unscaledTime;

        try
        {
            player.enabled = false;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (player.animator != null)
            {
                player.animator.SetFloat("Speed", 0f);
            }

            if (sectorToDeactivate != null && player.transform.parent == sectorToDeactivate.transform)
            {
                player.transform.SetParent(null);
            }

            ScreenFader fader = player.GetComponentInChildren<ScreenFader>();
            if (fader == null) fader = Object.FindFirstObjectByType<ScreenFader>();

            if (fader != null)
            {
                yield return player.StartCoroutine(fader.FadeOut());
            }

            if (spawnPoint != null)
            {
                player.transform.position = spawnPoint.position;
                player.transform.rotation = spawnPoint.rotation;
            }
            else
            {
                Debug.LogError("[BaseSectorTransition] Spawn Point is not assigned on " + gameObject.name);
            }

            MoveCam cameraController = Object.FindFirstObjectByType<MoveCam>();
            if (cameraController != null)
            {
                cameraController.playerTransform = player.transform;
                cameraController.transform.position = player.transform.position + cameraController.offset;
            }

            if (sectorToActivate != null) sectorToActivate.SetActive(true);
            if (sectorToDeactivate != null) sectorToDeactivate.SetActive(false);

            // Força atualização da física para que as portas recém ativadas sejam detectadas
            Physics.SyncTransforms();

            if (spawnPoint != null)
            {
                // Concede imunidade temporária de 1 segundo nos portais próximos ao ponto de nascimento
                Collider[] hitColliders = Physics.OverlapSphere(spawnPoint.position, 1.5f);
                foreach (var hit in hitColliders)
                {
                    BaseSectorTransition destTransition = hit.GetComponent<BaseSectorTransition>() ?? hit.GetComponentInParent<BaseSectorTransition>();
                    if (destTransition != null && destTransition != this)
                    {
                        destTransition.GrantTemporaryImmunity(playerCollider, 1.0f);
                    }
                }
            }

            if (blackScreenDuration > 0f)
            {
                yield return new WaitForSeconds(blackScreenDuration);
            }

            // Permite que o jogador se mova imediatamente enquanto a tela clareia (Game Feel rápido)
            if (player != null)
            {
                player.enabled = true;
            }

            if (fader != null && player != null)
            {
                player.StartCoroutine(fader.FadeIn());
            }

            if (EquipmentManager.Instance != null && EquipmentManager.Instance.IsEquipped("equip_aprimoramento_bota"))
            {
                PlayerAttributesDefensive defStats = player.GetComponent<PlayerAttributesDefensive>();
                if (defStats != null) defStats.temporarySpeedBoost = 0.5f;
            }
        }
        finally
        {
            // Cooldown de segurança curto (0.35s) e garantia absoluta de limpeza de estado
            nextAllowedTransitionTime = Time.time + 0.35f;
            isTransitioning = false;
            if (player != null)
            {
                player.enabled = true;
            }
        }
    }
}

