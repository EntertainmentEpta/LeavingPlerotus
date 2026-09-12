using UnityEngine;
using System.Collections;

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
    public float blackScreenDuration = 0.3f;

    private static bool isTransitioning = false;
    private static float nextAllowedTransitionTime = 0f;

    private void OnTriggerEnter(Collider other)
    {
        // Check if transition is already running or if we are in transition cooldown
        if (isTransitioning && Time.time > nextAllowedTransitionTime + 3f) { isTransitioning = false; Debug.Log("Reset stuck transition"); }
        // if (isTransitioning || Time.time < nextAllowedTransitionTime) { Debug.Log("Transition blocked. isTransitioning:" + isTransitioning); return; }

        // Check if it is the Player
        Debug.Log("[BaseSectorTransition] OnTriggerEnter by: " + other.name);
        PlayerM playerMovement = other.GetComponent<PlayerM>();
        if (playerMovement != null)
        {
            // Start the coroutine on the player to prevent it from being killed
            // when the trigger's parent sector is deactivated.
            playerMovement.StartCoroutine(DoTransition(playerMovement));
        }
    }

    private IEnumerator DoTransition(PlayerM player)
    {
        isTransitioning = true;

        // 1. Disable player movement controls and reset velocity
        player.enabled = false;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Make the animator play idle animation
        if (player.animator != null)
        {
            player.animator.SetFloat("Speed", 0f);
        }

        // Safety check: Unparent the player if they are a child of the deactivated sector
        if (sectorToDeactivate != null && player.transform.parent == sectorToDeactivate.transform)
        {
            player.transform.SetParent(null);
        }

        // 2. Find ScreenFader (on player or in the scene)
        ScreenFader fader = player.GetComponentInChildren<ScreenFader>();
        if (fader == null)
        {
            fader = Object.FindFirstObjectByType<ScreenFader>();
        }

        if (fader != null)
        {
            // Fade Out (screen goes black)
            yield return player.StartCoroutine(fader.FadeOut());
        }
        else
        {
            Debug.LogWarning("[BaseSectorTransition] ScreenFader not found. Transitioning instantly.");
        }

        // 3. Teleport Player to the new sector's spawn point
        if (spawnPoint != null)
        {
            player.transform.position = spawnPoint.position;
            
            // Align player rotation to spawn point rotation if set
            player.transform.rotation = spawnPoint.rotation;
        }
        else
        {
            Debug.LogError("[BaseSectorTransition] Spawn Point is not assigned!");
        }

        // 4. Snap Camera to avoid smooth slide through space
        MoveCam cameraController = Object.FindFirstObjectByType<MoveCam>();
        if (cameraController != null)
        {
            cameraController.playerTransform = player.transform; // Re-bind just in case
            cameraController.transform.position = player.transform.position + cameraController.offset;
        }

        // 5. Toggle Sector GameObjects (show/hide rooms)
        if (sectorToActivate != null)
        {
            sectorToActivate.SetActive(true);
        }
        if (sectorToDeactivate != null)
        {
            sectorToDeactivate.SetActive(false);
        }

        // 6. Brief pause while screen is black
        yield return new WaitForSeconds(blackScreenDuration);

        // 7. Fade In (screen becomes visible)
        if (fader != null)
        {
            yield return player.StartCoroutine(fader.FadeIn());
        }

        // 8. Re-enable player movement
        player.enabled = true;

        // Apply temporary speed boost if boots are equipped
        if (EquipmentManager.Instance != null && EquipmentManager.Instance.IsEquipped("equip_aprimoramento_bota"))
        {
            PlayerAttributesDefensive defStats = player.GetComponent<PlayerAttributesDefensive>();
            if (defStats != null)
            {
                defStats.temporarySpeedBoost = 0.5f; // +50% speed boost that decays over time
            }
        }

        // Set cooldown (1.5 seconds) to prevent instant back-and-forth triggering
        nextAllowedTransitionTime = Time.time + 1.5f;

        isTransitioning = false;
    }
}



