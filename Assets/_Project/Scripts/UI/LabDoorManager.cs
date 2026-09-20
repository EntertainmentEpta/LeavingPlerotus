using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerencia a porta do laboratorio na Base.
/// </summary>
public class LabDoorManager : MonoBehaviour
{
    [Header("Componentes da Porta")]
    [Tooltip("A luz que fica em cima/dentro da porta.")]
    public Light doorLight;
    
    [Tooltip("Objetos para ATIVAR quando a porta for consertada (Ex: Trigger do Lab, Luz verde, etc).")]
    public List<GameObject> objectsToEnable = new List<GameObject>();

    [Tooltip("Objetos para DESATIVAR quando a porta for consertada (Ex: Faiscas, fogo, luz vermelha, etc).")]
    public List<GameObject> objectsToDisable = new List<GameObject>();

    [Header("Cores da Luz Principal")]
    public Color brokenColor = new Color(0.886f, 0.486f, 0.388f); // #E27C63
    public Color fixedColor = new Color(0.388f, 0.556f, 0.886f);  // #638EE2

    private bool isRepaired = false;

    void Start()
    {
        // Se a luz nao foi assinalada, tenta buscar nos filhos
        if (doorLight == null) doorLight = GetComponentInChildren<Light>();

        UpdateDoorState();

        if (CraftingManager.Instance != null)
        {
            CraftingManager.OnCraftCompleted += CheckCraft;
            Debug.Log("[LabDoor] Inscrito no evento OnCraftCompleted.");
        }
        else
        {
            Debug.LogWarning("[LabDoor] CraftingManager.Instance ta nulo no Start!");
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
        if (recipe != null && recipe.resultEquipment != null)
        {
            Debug.Log("[LabDoor] Craft detectado: " + recipe.resultEquipment.equipmentId);
            if (recipe.resultEquipment.equipmentId == "eq_consertar_porta")
            {
                UpdateDoorState();
                Debug.Log("[LabDoor] A Porta foi consertada COM SUCESSO via Evento de Craft!");
            }
        }
    }

    private void UpdateDoorState()
    {
        if (SaveManager.instance != null)
        {
            isRepaired = SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_consertar_porta");
            Debug.Log("[LabDoor] Verificando SaveManager. isRepaired = " + isRepaired);
        }

        // Atualiza a Luz
        if (doorLight != null)
        {
            doorLight.color = isRepaired ? fixedColor : brokenColor;
        }

        // Ativa os Triggers
        foreach (var obj in objectsToEnable)
        {
            if (obj != null) obj.SetActive(isRepaired);
        }

        // Desativa as Faiscas/Fogo
        foreach (var obj in objectsToDisable)
        {
            if (obj != null) obj.SetActive(!isRepaired);
        }
    }
}
