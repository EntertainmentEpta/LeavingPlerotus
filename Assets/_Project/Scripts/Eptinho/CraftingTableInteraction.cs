using UnityEngine;

/// <summary>
/// Script de interação física com a Mesa de Trabalho na cena da Base.
/// Detecta proximidade do jogador e abre a UI de Crafting ao pressionar T.
///
/// SETUP NA CENA:
///   1. Adicione este script ao GameObject da mesa de trabalho
///   2. Adicione um BoxCollider com isTrigger = true ao objeto
///   3. Arraste o GameObject do prompt visual "T" para pressTUI
///   4. O CraftingUI é encontrado automaticamente (singleton)
///
/// DEPENDÊNCIAS:
///   - CraftingUI.Instance (tela de crafting — criada automaticamente)
///   - Player deve ter a tag "Player"
/// </summary>
public class CraftingTableInteraction : MonoBehaviour
{
    public static CraftingTableInteraction Instance { get; private set; }

    [Header("Áudio / Sons do Robozinho")]
    [Tooltip("Som reproduzido ao interagir/falar com o robô de síntese (tecla F ou T).")]
    public AudioClip talkSound;

    [Tooltip("Som reproduzido ao sintetizar/craftar um item com sucesso.")]
    public AudioClip synthesizeSuccessSound;

    [Tooltip("Som reproduzido quando não for possível sintetizar (falta de materiais ou requisitos).")]
    public AudioClip synthesizeFailSound;

    [Range(0f, 1f)]
    [Tooltip("Volume dos efeitos sonoros do robô.")]
    public float soundVolume = 1f;

    [Header("UI References")]
    [Tooltip("Prompt visual 'Pressione T/F' (world space, filho da crafting table)")]
    public GameObject pressTUI;

    [Header("Fallback")]
    [Tooltip("Painel legado da CraftingTableUI. Se CraftingUI.Instance existir, este é ignorado.")]
    public GameObject craftingTableUI;

    private bool playerPerto = false;
    private AudioSource audioSource;

    void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D som limpo de interface/NPC
        }

        if (pressTUI != null)
            pressTUI.SetActive(false);
        if (craftingTableUI != null)
            craftingTableUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerPerto = true;
        if (pressTUI != null)
            pressTUI.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerPerto = false;
        if (pressTUI != null)
            pressTUI.SetActive(false);

        // Fecha o crafting ao sair da área
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
            CraftingUI.Instance.CloseCrafting();
        else if (craftingTableUI != null)
            craftingTableUI.SetActive(false);
    }

    void Update()
    {
        if (!playerPerto) return;

        // Aceita tanto T quanto F para interagir com o robô
        if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.F))
        {
            PlayTalkSound();

            // Usa o novo CraftingUI se disponível
            if (CraftingUI.Instance != null)
            {
                if (CraftingUI.Instance.IsOpen())
                    CraftingUI.Instance.CloseCrafting();
                else
                    CraftingUI.Instance.OpenCrafting();
            }
            // Fallback para o painel legado
            else if (craftingTableUI != null)
            {
                craftingTableUI.SetActive(!craftingTableUI.activeSelf);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen())
                CraftingUI.Instance.CloseCrafting();
            else if (craftingTableUI != null && craftingTableUI.activeSelf)
                craftingTableUI.SetActive(false);
        }

        // Billboard: pressTUI sempre virado para a câmera
        if (pressTUI != null && pressTUI.activeSelf && Camera.main != null)
        {
            pressTUI.transform.forward = Camera.main.transform.forward;
        }
    }

    // ─── Métodos de Áudio ────────────────────────────────────────────────────

    public void PlayTalkSound()
    {
        PlayAudio(talkSound);
    }

    public void PlaySynthesizeSuccessSound()
    {
        PlayAudio(synthesizeSuccessSound);
    }

    public void PlaySynthesizeFailSound()
    {
        PlayAudio(synthesizeFailSound);
    }

    private void PlayAudio(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, soundVolume);
        }
    }
}
