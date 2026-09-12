using UnityEngine;

/// <summary>
/// Script de interacao fisica com a Mesa de Trabalho (Robozinho) na cena da Base.
/// Detecta proximidade do jogador e abre a UI de Crafting ao pressionar F.
/// </summary>
public class CraftingTableInteraction : MonoBehaviour
{
    [Header("UI do Robozinho")]
    [Tooltip("Arraste aqui o objeto que tem o '[ F ]' escrito (ex: um World Space Canvas filho do robo).")]
    public GameObject promptUI;

    [Header("Som (Opcional)")]
    public AudioClip roboSound;

    private bool playerPerto = false;

    void Start()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerPerto = true;
        
        if (promptUI != null)
        {
            promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerPerto = false;
        
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
        {
            CraftingUI.Instance.CloseCrafting();
        }
    }

    void Update()
    {
        if (!playerPerto) return;

        // Sempre faz o Prompt olhar para a camera (Billboard)
        if (promptUI != null && promptUI.activeSelf && Camera.main != null)
        {
            promptUI.transform.forward = Camera.main.transform.forward;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            // Tocar sonzinho do robo (se tiver)
            if (roboSound != null)
            {
                AudioSource src = GetComponent<AudioSource>();
                if (src == null) src = gameObject.AddComponent<AudioSource>();
                src.PlayOneShot(roboSound);
            }

            // Abre / Fecha a UI do Crafting
            if (CraftingUI.Instance != null)
            {
                if (CraftingUI.Instance.IsOpen())
                    CraftingUI.Instance.CloseCrafting();
                else
                    CraftingUI.Instance.OpenCrafting();
            }
            else
            {
                Debug.LogWarning("[Robozinho] Nao achei o CraftingUI.Instance na cena!");
            }
        }

        // Fechar com ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
                CraftingUI.Instance.CloseCrafting();
        }
    }
}
