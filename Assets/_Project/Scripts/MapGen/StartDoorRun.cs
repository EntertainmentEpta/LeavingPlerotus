using UnityEngine;
using System.Collections;

public class StartRunDoor : MonoBehaviour
{
    private bool isLoading = false; // Impede múltiplos triggers

    private void OnTriggerEnter(Collider other)
    {
        // Se o jogador (com a tag "Player") tocar o trigger
        if (other.CompareTag("Player") && !isLoading)
        {
            // Verificação se o jogador está armado e com a arma empunhada
            Player_WeaponManager weaponManager = other.GetComponent<Player_WeaponManager>();
            if (weaponManager != null)
            {
                if (weaponManager.currentWeapon == null || !weaponManager.isWeaponDrawn)
                {
                    if (EptinhoPopupController.instancia != null)
                    {
                        EptinhoPopupController.instancia.MostrarPopupAviso("Você não pode sair desarmado! Empunhe sua arma antes de partir.");
                    }
                    else
                    {
                        Debug.LogWarning("[StartRunDoor] Jogador tentou sair desarmado, mas EptinhoPopupController não foi encontrado.");
                    }
                    return; // Bloqueia a transição
                }
            }

            if (!EptinhoStoryDialogs.HasSeenDialog(EptinhoStoryDialogs.ID_PORTA_CONSERTADA))
            {
                EptinhoStoryDialogs.TriggerPortaConsertada();
                return; // Aguarda o jogador ler a orientação do Eptinho
            }

            isLoading = true;
            Debug.Log("Jogador tocou a porta! Iniciando transição da run com fade...");

            // Desativa o collider da porta para evitar problemas
            GetComponent<Collider>().enabled = false; 

            PlayerM playerMovement = other.GetComponent<PlayerM>();
            if (playerMovement != null)
            {
                playerMovement.StartCoroutine(DoStartRunTransition(playerMovement));
            }
            else
            {
                // Fallback direct load if PlayerM is missing
                if (GameManager.instance != null) GameManager.instance.LoadGameLevel();
            }
        }
    }

    private IEnumerator DoStartRunTransition(PlayerM player)
    {
        // 1. Disable player movement controls and reset velocity
        player.enabled = false;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (player.animator != null)
        {
            player.animator.SetFloat("Speed", 0f);
        }

        // 2. Find ScreenFader (on player or in the scene)
        ScreenFader fader = player.GetComponentInChildren<ScreenFader>();
        if (fader == null)
        {
            fader = Object.FindFirstObjectByType<ScreenFader>();
        }

        if (fader != null)
        {
            // Fade Out (screen goes black smoothly)
            yield return player.StartCoroutine(fader.FadeOut());
        }
        else
        {
            Debug.LogWarning("[StartRunDoor] ScreenFader not found. Transitioning instantly.");
        }

        // 3. Call GameManager to load the level asynchronously (which will show the loading canvas on black screen)
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadGameLevel();
        }
        else
        {
            Debug.LogError("GameManager.instance não encontrado! O jogo não pode começar.");
        }
    }
}