using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public GameObject pressFUI;

    public KeyCode interactKey = KeyCode.F;

    private Interactable itemAtual;

    void Start()
    {
        if (pressFUI != null) pressFUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        // Itens com ItemPickup se gerenciam sozinhos (glow + F-key) — ignorar aqui
        if (other.GetComponent<ItemPickup>() != null) return;

        Interactable item = other.GetComponent<Interactable>();
        if (item != null && !item.foiCatalogado)
        {
            itemAtual = item;
            if (pressFUI != null) pressFUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<Interactable>() == itemAtual)
        {
            itemAtual = null;
            if (pressFUI != null) pressFUI.SetActive(false);
        }
    }

    void Update()
    {
        if (itemAtual != null && Input.GetKeyDown(interactKey))
        {
            if (itemAtual.gameObject.name.Contains("Eptinho") || itemAtual.gameObject.name.Contains("Eptin"))
            {
                EptinhoController eptinhoCtrl = FindFirstObjectByType<EptinhoController>();
                if (eptinhoCtrl != null)
                {
                    eptinhoCtrl.AbrirMenuDoObjeto(itemAtual);
                }
            }
            else
            {
                CatalogoManager.instancia?.Catalogar(itemAtual);
            }
            
            if (pressFUI != null) pressFUI.SetActive(false);
            itemAtual = null;
        }
    }
}


//using UnityEngine;

//public class PlayerInteraction : MonoBehaviour
//{
//    public GameObject popupUI; // Pop-up do Eptinho
//    public EptinhoController eptinhoController;

//    private Interactable objetoAtual;

//    void Start()
//    {   
//        popupUI.SetActive(false);
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        Debug.Log("ENTROU NO TRIGGER COM: " + other.name);
//        Interactable interactable = other.GetComponent<Interactable>();

//        if (interactable != null)
//        {
//            objetoAtual = interactable;
//            popupUI.SetActive(true);
//            Debug.Log("Tentou ativar popup. popupUI ativo? " + popupUI.activeSelf);

//        }
//    }

//    void OnTriggerExit(Collider other)
//    {
//        if (other.GetComponent<Interactable>())
//        {
//            popupUI.SetActive(false);
//            objetoAtual = null;
//        }
//    }

//    // Chamada pelo bot�o do pop up (no UI Button)
//    public void AbrirMenu()
//    {
//        if (objetoAtual != null)
//        {
//            eptinhoController.AbrirMenuDoObjeto(objetoAtual);
//            popupUI.SetActive(false);
//        }
//    }
//}
