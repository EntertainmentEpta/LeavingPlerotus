using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia o Catálogo de Itens do Eptinho — rastreia itens coletados durante a run
/// e o estado de descoberta de atributos (via infusão).
///
/// FLUXO DE DESCOBERTA (FOG OF WAR DE ATRIBUTOS):
///   1. Coleta inicial: O item é catalogado (aparece no Catálogo do Eptinho e na Infusão),
///      porém seus atributos permanecem DESCONHECIDOS ("? ? ? Propriedade Oculta").
///   2. Primeira Infusão: O jogador infunde o item no traje na câmara de upgrades.
///      O Eptinho decodifica os poderes da matéria e os registra permanentemente no Catálogo!
///   3. Próximas vezes: Sempre que o jogador encontrar o item, os atributos aparecem
///      totalmente revelados e detalhados.
/// </summary>
public class CatalogoManager : MonoBehaviour
{
    private static CatalogoManager _instancia;
    public static CatalogoManager instancia
    {
        get
        {
            if (_instancia == null)
            {
                _instancia = FindFirstObjectByType<CatalogoManager>();
                if (_instancia == null)
                {
                    GameObject go = new GameObject("CatalogoManager_Auto");
                    _instancia = go.AddComponent<CatalogoManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instancia;
        }
        private set { _instancia = value; }
    }

    // Lista de ItemData já coletados/catalogados
    public List<ItemData> itensCatalogados = new List<ItemData>();
    private HashSet<string> idsRegistrados = new HashSet<string>();

    // Conjunto de IDs de itens cujos atributos já foram DECODIFICADOS via Infusão
    public HashSet<string> itensInfundidos = new HashSet<string>();

    void Awake()
    {
        if (_instancia == null)
        {
            _instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instancia != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        CarregarDoSave();
    }

    private bool saveCarregado = false;

    /// <summary>
    /// Carrega itens catalogados e infundidos das sessões anteriores via SaveManager.
    /// </summary>
    public void CarregarDoSave()
    {
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null) return;
        saveCarregado = true;

        var cached = SaveManager.instance.CachedData;

        // 1. Carrega itens com infusão decodificada
        if (cached.itensInfundidos != null)
        {
            foreach (string id in cached.itensInfundidos)
            {
                if (!string.IsNullOrEmpty(id))
                    itensInfundidos.Add(id);
            }
        }

        // 2. Carrega itens catalogados
        if (cached.itensCatalogados != null)
        {
            foreach (string id in cached.itensCatalogados)
            {
                if (string.IsNullOrEmpty(id) || idsRegistrados.Contains(id)) continue;

                ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(id) : null;
                if (data == null) data = Resources.Load<ItemData>("ItemData/" + id);

                if (data != null)
                {
                    idsRegistrados.Add(id);
                    if (!itensCatalogados.Contains(data))
                        itensCatalogados.Add(data);
                }
            }
        }

        Debug.Log($"[CATÁLOGO] Carregados {itensCatalogados.Count} item(ns) catalogados e {itensInfundidos.Count} com atributos decodificados.");
    }

    /// <summary>
    /// Retorna se o item já foi infundido pelo menos uma vez (atributos revelados).
    /// </summary>
    public bool IsItemInfused(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!saveCarregado) CarregarDoSave();
        return itensInfundidos.Contains(itemId);
    }

    /// <summary>
    /// Retorna se o item já foi encontrado/catalogado.
    /// </summary>
    public bool IsItemCataloged(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!saveCarregado) CarregarDoSave();
        return idsRegistrados.Contains(itemId);
    }

    /// <summary>
    /// Cataloga um item a partir de um ItemData diretamente.
    /// </summary>
    public void Catalogar(ItemData data)
    {
        if (data == null || string.IsNullOrEmpty(data.itemId)) return;
        if (idsRegistrados.Contains(data.itemId)) return;

        idsRegistrados.Add(data.itemId);
        if (!itensCatalogados.Contains(data))
            itensCatalogados.Add(data);

        // Persiste no save
        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            var cached = SaveManager.instance.CachedData;
            if (cached.itensCatalogados == null) cached.itensCatalogados = new List<string>();
            if (!cached.itensCatalogados.Contains(data.itemId))
            {
                cached.itensCatalogados.Add(data.itemId);
                SaveManager.instance.SavePersistentData();
            }
        }

        if (EptinhoPopupController.instancia != null)
            EptinhoPopupController.instancia.MostrarPopup(data);

        // Integração com o catálogo de sinergias
        if (!string.IsNullOrEmpty(data.synergyNodeID))
            SynergyCatalogManager.Instance?.DiscoverNode(data.synergyNodeID);

        Debug.Log($"[CATÁLOGO] Novo item registrado: {data.itemName}");
    }

    /// <summary>
    /// Cataloga um item pela primeira vez que é coletado no mundo.
    /// </summary>
    public void Catalogar(Interactable item)
    {
        if (item == null) return;
        if (item.foiCatalogado) return;

        if (item.itemData != null)
        {
            item.foiCatalogado = true;
            Catalogar(item.itemData);
        }
        else
        {
            item.foiCatalogado = true;
            Debug.LogWarning($"[CATÁLOGO] Item {item.objetoNome} catalogado, mas não possui ItemData!");
        }
    }

    /// <summary>
    /// Registra que um item foi infundido no traje, decodificando seus atributos permanentemente!
    /// </summary>
    public void RegistrarInfusao(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        bool wasAlreadyDecoded = itensInfundidos.Contains(itemId);
        itensInfundidos.Add(itemId);

        ItemData data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemId) : null;
        if (data == null) data = Resources.Load<ItemData>("ItemData/" + itemId);

        if (data != null && !idsRegistrados.Contains(itemId))
        {
            idsRegistrados.Add(itemId);
            if (!itensCatalogados.Contains(data))
                itensCatalogados.Add(data);
        }

        // Persiste a descoberta no save
        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            var cached = SaveManager.instance.CachedData;
            if (cached.itensInfundidos == null) cached.itensInfundidos = new List<string>();
            if (!cached.itensInfundidos.Contains(itemId)) cached.itensInfundidos.Add(itemId);

            if (cached.itensCatalogados == null) cached.itensCatalogados = new List<string>();
            if (!cached.itensCatalogados.Contains(itemId)) cached.itensCatalogados.Add(itemId);

            SaveManager.instance.SavePersistentData();
        }

        // Se foi decodificado pela primeira vez, notifica com entusiasmo pelo Eptinho!
        if (!wasAlreadyDecoded)
        {
            string nomeDisplay = data != null ? data.itemName : itemId;
            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(
                    $"Eptinho decodificou os atributos de {nomeDisplay}! Registrado no Catálogo."
                );
            }
            Debug.Log($"[CATÁLOGO] ✨ ATRIBUTOS DECODIFICADOS! {nomeDisplay} agora possui dados completos revelados!");
        }
    }
}
