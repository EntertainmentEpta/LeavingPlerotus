using UnityEngine;
using System.Collections.Generic;

public class SynergyCraftingManager : MonoBehaviour
{
    public static SynergyCraftingManager Instance { get; private set; }

    [Header("Itens Selecionados na Bancada")]
    public List<ItemData> selectedItems = new List<ItemData>();
    private const int MAX_SLOTS = 2;

    public delegate void SelectionChangedAction();
    public event SelectionChangedAction OnSelectionChanged;

    private InfusionManager infusionManager;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Encontra o gerenciador de infusão do player para pegar os cálculos de inflação
        infusionManager = FindFirstObjectByType<InfusionManager>();
    }

    /// <summary>
    /// Alterna a seleção de um item. Se já estiver na bancada, tira. Se não estiver, coloca (se houver espaço).
    /// </summary>
    public void ToggleItemSelection(ItemData item)
    {
        if (selectedItems.Contains(item))
        {
            selectedItems.Remove(item);
        }
        else
        {
            if (selectedItems.Count < MAX_SLOTS)
            {
                selectedItems.Add(item);
            }
            else
            {
                Debug.LogWarning("[SYNERGY] A bancada já está cheia! Selecione no máximo 2 itens.");
                return; // Ignora se tentar colocar o terceiro
            }
        }

        // Avisa a UI que a seleção mudou para ela atualizar os textos
        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Limpa a bancada.
    /// </summary>
    public void ClearSelection()
    {
        selectedItems.Clear();
        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Calcula o custo SOMADO e INFLACIONADO dos itens que estão na bancada.
    /// </summary>
    public int GetCombinedInfusionCost()
    {
        if (infusionManager == null || selectedItems.Count == 0) return 0;

        int totalCost = 0;
        foreach (var item in selectedItems)
        {
            // Usa a matemática mágica de inflação que já existe no seu InfusionManager!
            totalCost += infusionManager.GetInflatedCost(item);
        }
        return totalCost;
    }

    /// <summary>
    /// Verifica se podemos criar a Sinergia (temos 2 itens na bancada e dinheiro suficiente).
    /// </summary>
    public bool CanAffordSynergy()
    {
        if (selectedItems.Count < 2) return false;

        PlayerEssence wallet = FindFirstObjectByType<PlayerEssence>();
        if (wallet == null) return false;

        return wallet.currentEssence >= GetCombinedInfusionCost();
    }
}
