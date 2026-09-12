using UnityEngine;

/// <summary>
/// Gerencia a porta do laboratorio na Base.
/// Bloqueia/desbloqueia o trigger e troca a cor da luz caso o jogador tenha craftado o reparo.
/// </summary>
public class LabDoorManager : MonoBehaviour
{
    [Header("Componentes da Porta")]
    [Tooltip("A luz que fica em cima/dentro da porta.")]
    public Light doorLight;
    
    [Tooltip("O GameObject que contem o trigger de transicao pro lab.")]
    public GameObject labTriggerObject;

    [Header("Cores")]
    public Color brokenColor = new Color(0.886f, 0.486f, 0.388f); // #E27C63
    public Color fixedColor = new Color(0.388f, 0.556f, 0.886f);  // #638EE2

    private bool isRepaired = false;

    void Start()
    {
        // Forca o estado inicial baseando-se no SaveManager
        UpdateDoorState();

        // Se inscreve para atualizar quando houver novo craft
        if (CraftingManager.Instance != null)
        {
            CraftingManager.OnCraftCompleted += CheckCraft;
        }
    }

    void OnDestroy()
    {
        if (CraftingManager.Instance != null)
        {
            CraftingManager.OnCraftCompleted -= CheckCraft;
        }
    }

    private void CheckCraft(CraftingRecipe recipe)
    {
        if (recipe != null && recipe.resultEquipment != null && recipe.resultEquipment.equipmentId == "eq_consertar_porta")
        {
            UpdateDoorState();
            
            // Efeito sonoro / Particula de conserto poderia ir aqui!
            Debug.Log("[LabDoor] Porta consertada com sucesso!");
        }
    }

    private void UpdateDoorState()
    {
        if (SaveManager.instance != null)
        {
            isRepaired = SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_consertar_porta");
        }

        if (doorLight != null)
        {
            doorLight.color = isRepaired ? fixedColor : brokenColor;
        }

        if (labTriggerObject != null)
        {
            labTriggerObject.SetActive(isRepaired);
        }
    }
}
