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
    public float blackScreenDuration = 0.3f;

    private static bool isTransitioning = false;
    private static float nextAllowedTransitionTime = 0f;

    // NOVO: Lista de players que acabaram de nascer dentro deste trigger especfico.
    // Eles s podero ativar este trigger DEPOIS que sairem dele (OnTriggerExit).
    private HashSet<Collider> immunePlayers = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        // Se o player acabou de nascer DENTRO da porta, ignora ele at ele sair.
        if (immunePlayers.Contains(other))
        {
            Debug.Log("[BaseSectorTransition] " + gameObject.name + " ignorando " + other.name + " pois ele nasceu aqui dentro.");
            return;
        }

        // Check if transition is already running
        if (isTransitioning) 
        { 
            Debug.Log("[BaseSectorTransition] Blocked by isTransitioning"); 
            return; 
        }

        // (Opcional) Cooldown de segurana global de 0.5s para evitar bugs de mltiplos triggers no mesmo frame
        if (Time.time < nextAllowedTransitionTime)
        {
            Debug.Log("[BaseSectorTransition] Blocked by global cooldown");
            return;
        }

        PlayerM playerMovement = other.GetComponent<PlayerM>();
        if (playerMovement != null)
        {
            Debug.Log("[BaseSectorTransition] Iniciando transio de " + gameObject.name);
            playerMovement.StartCoroutine(DoTransition(playerMovement, other));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Quando o player sai da porta, ele perde a imunidade e pode usar a porta normalmente de novo.
        if (immunePlayers.Contains(other))
        {
            immunePlayers.Remove(other);
            Debug.Log("[BaseSectorTransition] " + other.name + " saiu de " + gameObject.name + " e perdeu a imunidade.");
        }
    }

    private IEnumerator DoTransition(PlayerM player, Collider playerCollider)
    {
        isTransitioning = true;

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

            // NOVO: Imunidade de Portal
            // Ao teleportar, vamos checar quais Portas (BaseSectorTransition) o player est tocando agora.
            // Avisamos a essas portas para IGNORAREM o player at que ele d um passo para fora!
            Collider[] hitColliders = Physics.OverlapSphere(spawnPoint.position, 1.5f);
            foreach (var hit in hitColliders)
            {
                BaseSectorTransition destTransition = hit.GetComponent<BaseSectorTransition>();
                if (destTransition != null)
                {
                    destTransition.immunePlayers.Add(playerCollider);
                    Debug.Log("[BaseSectorTransition] Dando imunidade ao player no portal de destino: " + destTransition.gameObject.name);
                }
            }
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

        yield return new WaitForSeconds(blackScreenDuration);

        // Permite que o jogador se mova IMEDIATAMENTE enquanto a tela clareia! (Game Feel rpido)
        player.enabled = true;
        
        if (fader != null)
        {
            player.StartCoroutine(fader.FadeIn());
        }

        if (EquipmentManager.Instance != null && EquipmentManager.Instance.IsEquipped("equip_aprimoramento_bota"))
        {
            PlayerAttributesDefensive defStats = player.GetComponent<PlayerAttributesDefensive>();
            if (defStats != null) defStats.temporarySpeedBoost = 0.5f;
        }

        // Cooldown global mnimo de 0.5s apenas para evitar bizarrices de fsica, mas a verdadeira trava  a Imunidade.
        nextAllowedTransitionTime = Time.time + 0.5f;
        isTransitioning = false;
    }
}
