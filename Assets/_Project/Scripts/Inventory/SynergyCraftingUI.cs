using UnityEngine;
using TMPro;

public class SynergyCraftingUI : MonoBehaviour
{
    [Header("Textos da Interface")]
    public TextMeshProUGUI costText;
    public TextMeshProUGUI statusText;

    void Start()
    {
        if (SynergyCraftingManager.Instance != null)
        {
            // Se inscreve no evento para atualizar a tela automaticamente sempre que clicar num item!
            SynergyCraftingManager.Instance.OnSelectionChanged += UpdateUI;
        }
        UpdateUI();
    }

    void OnDestroy()
    {
        if (SynergyCraftingManager.Instance != null)
        {
            SynergyCraftingManager.Instance.OnSelectionChanged -= UpdateUI;
        }
    }

    private void UpdateUI()
    {
        if (SynergyCraftingManager.Instance == null) return;

        int selectedCount = SynergyCraftingManager.Instance.selectedItems.Count;
        int totalCost = SynergyCraftingManager.Instance.GetCombinedInfusionCost();

        if (selectedCount == 0)
        {
            if (costText != null) costText.text = "Custo: 0 Essências";
            if (statusText != null) statusText.text = "Selecione 2 itens para fundir.";
        }
        else if (selectedCount == 1)
        {
            if (costText != null) costText.text = $"Custo Parcial: {totalCost} Essências";
            if (statusText != null) statusText.text = "Falta mais 1 item...";
        }
        else if (selectedCount == 2)
        {
            if (costText != null) costText.text = $"Custo Total Inflacionado: {totalCost} Essências";
            
            if (SynergyCraftingManager.Instance.CanAffordSynergy())
            {
                if (statusText != null) statusText.text = "<color=green>Pronto para Sintetizar!</color>";
            }
            else
            {
                if (statusText != null) statusText.text = "<color=red>Essências Insuficientes!</color>";
            }
        }
    }
}
