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

    // Rastreia colisores que acabaram de chegar neste setor através deste ou de outro portal próximo.
    // Eles permanecem imunes a re-transição enquanto permanecerem parados dentro do trigger,
    // sendo liberados assim que derem um passo para fora (OnTriggerExit) ou se afastarem mais de 3.0m.
    private readonly HashSet<Collider> occupantsWhoTransitioned = new HashSet<Collider>();
    private Vector3 lastArrivalSpawnPos = Vector3.zero;

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

    private void OnEnable()
    {
        // Limpa estado quando o setor é reativado para prevenir travas
        occupantsWhoTransitioned.Clear();
        lastArrivalSpawnPos = Vector3.zero;
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
        if (other != null && occupantsWhoTransitioned.Contains(other))
        {
            occupantsWhoTransitioned.Remove(other);
            Debug.Log($"[BaseSectorTransition] {other.name} saiu de {gameObject.name}; portal rearmado para transição.");
        }
    }

    private bool IsColliderImmune(Collider other)
    {
        if (other == null) return false;

        if (occupantsWhoTransitioned.Contains(other))
        {
            // Fail-safe de distância: se o jogador já se afastou mais de 3.0m do ponto de chegada/portal,
            // remove a imunidade mesmo se OnTriggerExit não tiver sido capturado pela física da Unity.
            if (lastArrivalSpawnPos != Vector3.zero && Vector3.Distance(other.transform.position, lastArrivalSpawnPos) > 3.0f)
            {
                occupantsWhoTransitioned.Remove(other);
                return false;
            }
            return true; // Ainda dentro da área de chegada/trigger: NÃO retransiciona
        }
        return false;
    }

    public void GrantArrivalImmunity(Collider playerCollider, Vector3 arrivalPoint)
    {
        if (playerCollider != null)
        {
            occupantsWhoTransitioned.Add(playerCollider);
            lastArrivalSpawnPos = arrivalPoint;
            Debug.Log($"[BaseSectorTransition] Concedendo imunidade de chegada a {playerCollider.name} no portal {gameObject.name} (posição {arrivalPoint}).");
        }
    }

    // Compatibilidade com chamadas legadas
    public void GrantTemporaryImmunity(Collider playerCollider, float duration = 1.0f)
    {
        GrantArrivalImmunity(playerCollider, transform.position);
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

            // Limpa o jogador da porta de origem para que ele possa retornar no futuro
            occupantsWhoTransitioned.Remove(playerCollider);

            if (spawnPoint != null)
            {
                // Concede imunidade de chegada aos portais próximos ao ponto de nascimento no novo setor.
                // O jogador não será re-teleportado enquanto permanecer parado no ponto de chegada.
                Collider[] hitColliders = Physics.OverlapSphere(spawnPoint.position, 2.5f);
                foreach (var hit in hitColliders)
                {
                    BaseSectorTransition destTransition = hit.GetComponent<BaseSectorTransition>() ?? hit.GetComponentInParent<BaseSectorTransition>();
                    if (destTransition != null)
                    {
                        destTransition.GrantArrivalImmunity(playerCollider, spawnPoint.position);
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

