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
    public float blackScreenDuration = 0.15f;

    // Static transition lock to prevent overlapping transitions
    private static bool isTransitioning = false;
    private static float transitionStartTime = 0f;
    private static float globalTransitionCooldownUntil = 0f;
    private const float MAX_TRANSITION_TIMEOUT = 3.0f;

    // Se o jogador nasceu dentro da caixa de colisão da porta, ele deve sair antes dela poder ser reativada
    [SerializeField] private bool requireTriggerExit = false;
    private float exitTimeout = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticStateDomain()
    {
        isTransitioning = false;
        transitionStartTime = 0f;
        globalTransitionCooldownUntil = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticStateScene()
    {
        isTransitioning = false;
        transitionStartTime = 0f;
        globalTransitionCooldownUntil = 0f;
    }

    private void Awake()
    {
        // Fail-safe: if transition was left stuck on scene load or past timeout, auto-recover
        if (isTransitioning && Time.unscaledTime - transitionStartTime > MAX_TRANSITION_TIMEOUT)
        {
            isTransitioning = false;
        }

        AutoResolveReferences();
    }

    private void OnEnable()
    {
        requireTriggerExit = false;
        exitTimeout = 0f;
    }

    /// <summary>
    /// Auto-resolve setores e spawn point caso alguma ligação de prefab tenha se perdido.
    /// </summary>
    public void AutoResolveReferences()
    {
        if (sectorToDeactivate == null)
        {
            // O setor que estamos deixando é o setor pai deste portal
            Transform p = transform.parent;
            while (p != null)
            {
                if (p.name.ToLower().StartsWith("sector_"))
                {
                    sectorToDeactivate = p.gameObject;
                    break;
                }
                p = p.parent;
            }
        }

        if (sectorToActivate == null)
        {
            string myName = gameObject.name.ToLower();
            if (myName.Contains("lobby"))
            {
                sectorToActivate = GameObject.Find("Sector_Lobby");
            }
            else if (myName.Contains("lab"))
            {
                sectorToActivate = GameObject.Find("Sector_Laboratory");
            }
            else if (myName.Contains("cockpit"))
            {
                sectorToActivate = GameObject.Find("Sector_Cockpit");
            }
        }

        if (spawnPoint == null && sectorToActivate != null)
        {
            foreach (Transform t in sectorToActivate.GetComponentsInChildren<Transform>(true))
            {
                string tName = t.name.ToLower();
                if (tName.Contains("spawnpoint") || tName.Contains("spawn_point") || tName.Contains("spawn"))
                {
                    spawnPoint = t;
                    break;
                }
            }
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
        if (other != null && (other.CompareTag("Player") || other.GetComponent<PlayerM>() != null || other.GetComponentInParent<PlayerM>() != null))
        {
            if (requireTriggerExit)
            {
                requireTriggerExit = false;
                Debug.Log($"[BaseSectorTransition] Jogador saiu da soleira do portal {gameObject.name}. Portal rearmado com sucesso.");
            }
        }
    }

    // Compatibilidade com chamadas legadas
    public void GrantArrivalImmunity(Collider playerCollider, Vector3 arrivalPoint, float duration = 2.5f, float minDistance = 3.5f)
    {
        Collider col = GetComponent<Collider>();
        if (col != null && col.bounds.Contains(arrivalPoint))
        {
            requireTriggerExit = true;
            exitTimeout = Time.unscaledTime + duration;
        }
        else
        {
            requireTriggerExit = false;
        }
    }

    public void GrantTemporaryImmunity(Collider playerCollider, float duration = 2.5f)
    {
        GrantArrivalImmunity(playerCollider, transform.position, duration, 3.5f);
    }

    private void TryStartTransition(Collider other)
    {
        // 1. Global Cooldown (trava após qualquer transição recente para evitar efeito bate-e-volta)
        if (Time.unscaledTime < globalTransitionCooldownUntil)
        {
            return;
        }

        // 2. Se o jogador nasceu dentro da caixa de colisão, ele deve sair antes de usar
        if (requireTriggerExit)
        {
            if (Time.unscaledTime < exitTimeout)
            {
                return;
            }
            // Timeout de segurança: libera após expirar
            requireTriggerExit = false;
        }

        // 3. Watchdog check: Auto-recover if a previous transition hung
        if (isTransitioning)
        {
            if (Time.unscaledTime - transitionStartTime > MAX_TRANSITION_TIMEOUT)
            {
                Debug.LogWarning("[BaseSectorTransition] Watchdog: Transição anterior expirou por timeout (3.0s). Resetando isTransitioning.");
                isTransitioning = false;
            }
            else
            {
                return; // Transition actively running
            }
        }

        // 4. Find Player component (check both component and parent)
        PlayerM playerMovement = other.GetComponent<PlayerM>() ?? other.GetComponentInParent<PlayerM>();
        if (playerMovement != null)
        {
            Debug.Log($"[BaseSectorTransition] 🚪 Iniciando transição do setor via {gameObject.name}...");
            playerMovement.StartCoroutine(DoTransition(playerMovement, other));
        }
    }

    private IEnumerator DoTransition(PlayerM player, Collider playerCollider)
    {
        isTransitioning = true;
        transitionStartTime = Time.unscaledTime;
        globalTransitionCooldownUntil = Time.unscaledTime + 999f; // Trava global durante todo o processo

        try
        {
            AutoResolveReferences();

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
                Debug.Log($"[BaseSectorTransition] Jogador posicionado no spawnPoint {spawnPoint.name} em {spawnPoint.position}.");
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

            // Força atualização da física para que os colliders do novo setor sejam atualizados imediatamente
            Physics.SyncTransforms();

            // Configura os portais do novo setor: se algum contiver o spawnPoint, exige saída antes de rearmar
            if (sectorToActivate != null && spawnPoint != null)
            {
                BaseSectorTransition[] destPortals = sectorToActivate.GetComponentsInChildren<BaseSectorTransition>(true);
                foreach (var destPortal in destPortals)
                {
                    if (destPortal != null)
                    {
                        Collider col = destPortal.GetComponent<Collider>();
                        if (col != null && col.bounds.Contains(spawnPoint.position))
                        {
                            destPortal.requireTriggerExit = true;
                            destPortal.exitTimeout = Time.unscaledTime + 3.0f;
                            Debug.Log($"[BaseSectorTransition] Portal de retorno {destPortal.name} contém o spawnPoint; requer saída do jogador.");
                        }
                        else
                        {
                            destPortal.requireTriggerExit = false;
                        }
                    }
                }
            }

            if (blackScreenDuration > 0f)
            {
                yield return new WaitForSeconds(blackScreenDuration);
            }

            // Permite que o jogador se mova imediatamente enquanto a tela clareia
            if (player != null)
            {
                player.enabled = true;
            }

            if (fader != null && player != null)
            {
                yield return player.StartCoroutine(fader.FadeIn());
            }

            if (EquipmentManager.Instance != null && EquipmentManager.Instance.IsEquipped("equip_aprimoramento_bota"))
            {
                PlayerAttributesDefensive defStats = player.GetComponent<PlayerAttributesDefensive>();
                if (defStats != null) defStats.temporarySpeedBoost = 0.5f;
            }
        }
        finally
        {
            // Cooldown de 1.0s após o término do fade-in antes de permitir nova transição
            globalTransitionCooldownUntil = Time.unscaledTime + 1.0f;
            isTransitioning = false;
            if (player != null)
            {
                player.enabled = true;
            }
        }
    }
}
