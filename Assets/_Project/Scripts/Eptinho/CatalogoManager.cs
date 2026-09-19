using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia o Catálogo de Itens do Eptinho — rastreia itens coletados durante a run.
///
/// FLUXO:
///   1. ItemCollectable detecta que o player coletou um item.
///   2. Chama CatalogoManager.instancia.Catalogar(interactable).
///   3. CatalogoManager adiciona o ItemData à lista e dispara o popup.
///   4. EptinhoMenuController exibe a lista ao abrir o menu (tecla I).
/// </summary>
public class CatalogoManager : MonoBehaviour
{
    public static CatalogoManager instancia;

    // Lista de ItemData já coletados/catalogados nesta run
    public List<ItemData> itensCatalogados = new();
    private HashSet<string> idsRegistrados = new();

    void Awake()
    {
        if (instancia == null)
        {
            instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Cataloga um item pela primeira vez que é coletado.
    /// Aceita um Interactable para compatibilidade com o sistema antigo.
    /// </summary>
    public void Catalogar(Interactable item)
    {
        if (item == null) return;
        if (item.foiCatalogado) return;

        // Se o ItemData está configurado no Interactable (novo padrão)
        if (item.itemData != null)
        {
            if (idsRegistrados.Contains(item.itemData.itemId)) return;

            item.foiCatalogado = true;
            itensCatalogados.Add(item.itemData);
            idsRegistrados.Add(item.itemData.itemId);

            if (EptinhoPopupController.instancia != null)
                EptinhoPopupController.instancia.MostrarPopup(item.itemData);

            // Integração com o catálogo de sinergias
            if (!string.IsNullOrEmpty(item.itemData.synergyNodeID))
                SynergyCatalogManager.Instance.DiscoverNode(item.itemData.synergyNodeID);

            Debug.Log($"[CATÁLOGO] Novo item registrado: {item.itemData.itemName}");
        }
        else
        {
            // Fallback para itens antigos sem ItemData, apenas marca como catalogado
            item.foiCatalogado = true;
            Debug.LogWarning($"[CATÁLOGO] Item {item.objetoNome} catalogado, mas não possui ItemData!");
        }
    }
}
