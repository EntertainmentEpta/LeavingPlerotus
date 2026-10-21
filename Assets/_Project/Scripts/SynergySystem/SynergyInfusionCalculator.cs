using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resultado da avaliação de combos de sinergia na infusão.
/// </summary>
public class SynergyResult
{
    public float totalEssenceDiscount = 0f;
    public List<StatModifier> extraModifiers = new List<StatModifier>();
    public List<string> triggeredRecipeNames = new List<string>();
    public List<string> triggeredRecipeIDs = new List<string>();
    public HashSet<string> usedItemIDs = new HashSet<string>();
    public bool hasSynergy => (triggeredRecipeNames != null && triggeredRecipeNames.Count > 0) || totalEssenceDiscount > 0f || (extraModifiers != null && extraModifiers.Count > 0);
}

/// <summary>
/// Calculador que valida receitas de sinergia com base nos itens selecionados para infusão.
/// </summary>
public class SynergyInfusionCalculator : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    private static SynergyInfusionCalculator _instance;
    public static SynergyInfusionCalculator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SynergyInfusionCalculator>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SynergyInfusionCalculator_Auto");
                    _instance = go.AddComponent<SynergyInfusionCalculator>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // ── Receitas em Cache ──────────────────────────────────────
    private List<SynergyNodeData> availableRecipes = new List<SynergyNodeData>();
    private List<SynergyNodeData> allLoadedNodes = new List<SynergyNodeData>();

    // ── Lifecycle ──────────────────────────────────────────────
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadRecipes();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void LoadRecipes()
    {
        // Carrega todos os nós de sinergia da pasta Resources/SynergyNodes
        SynergyNodeData[] allNodes = Resources.LoadAll<SynergyNodeData>("SynergyNodes");
        
        allLoadedNodes.Clear();
        allLoadedNodes.AddRange(allNodes);

        // Filtra apenas os nós que são receitas de combo
        foreach (var node in allNodes)
        {
            if (node != null && node.NodeType == NodeType.SynergyRecipe)
            {
                availableRecipes.Add(node);
            }
        }

        Debug.Log($"[SYNERGY CALCULATOR] Carregadas {availableRecipes.Count} receitas adicionais de sinergia.");
    }

    // ── API Pública ────────────────────────────────────────────

    /// <summary>
    /// Retorna todos os ScriptableObjects carregados (do tipo EnemyOrigin, InfusionPart e SynergyRecipe).
    /// </summary>
    public List<SynergyNodeData> GetAllNodes()
    {
        return allLoadedNodes;
    }

    /// <summary>
    /// Avalia a lista de IDs selecionados e retorna o desconto na essência e modificadores extras gerados pelos combos válidos.
    /// </summary>
    public SynergyResult EvaluateInfusion(List<string> selectedItemIDs)
    {
        SynergyResult result = new SynergyResult();
        
        if (selectedItemIDs == null || selectedItemIDs.Count == 0)
            return result;

        // 1. Avalia contra as 10 Sinergias Oficiais do SynergyDatabase
        var dbSynergies = SynergyDatabase.EvaluateSynergies(selectedItemIDs, out HashSet<string> usedDbIds);
        foreach (var recipe in dbSynergies)
        {
            if (!result.triggeredRecipeIDs.Contains(recipe.id))
            {
                result.triggeredRecipeIDs.Add(recipe.id);
            }
            if (!string.IsNullOrEmpty(recipe.name) && !result.triggeredRecipeNames.Contains(recipe.name))
            {
                result.triggeredRecipeNames.Add(recipe.name);
            }
            result.totalEssenceDiscount += recipe.essenceDiscount;
            if (recipe.modifiers != null)
            {
                result.extraModifiers.AddRange(recipe.modifiers);
            }
            Debug.Log($"[SYNERGY CALCULATOR] Sinergia Oficial Ativada: {recipe.name} (-{recipe.essenceDiscount * 100f:F0}% Essência)!");
        }

        foreach (var uId in usedDbIds)
        {
            result.usedItemIDs.Add(uId);
        }

        // 2. Avalia nós customizados do ScriptableObject (caso existam adicionais na pasta Resources)
        HashSet<string> selectedSet = new HashSet<string>(selectedItemIDs);
        foreach (var recipe in availableRecipes)
        {
            if (recipe == null) continue;
            if (result.triggeredRecipeIDs.Contains(recipe.NodeID)) continue;

            bool hasAllRequirements = true;
            if (recipe.SynergyRequirements != null && recipe.SynergyRequirements.Count > 0)
            {
                foreach (var req in recipe.SynergyRequirements)
                {
                    if (req == null || !selectedSet.Contains(req.NodeID))
                    {
                        hasAllRequirements = false;
                        break;
                    }
                }
            }
            else
            {
                hasAllRequirements = false;
            }

            if (hasAllRequirements)
            {
                result.totalEssenceDiscount += recipe.EssenceDiscountPercentage;
                if (!string.IsNullOrEmpty(recipe.NodeID) && !result.triggeredRecipeIDs.Contains(recipe.NodeID))
                {
                    result.triggeredRecipeIDs.Add(recipe.NodeID);
                }
                if (!string.IsNullOrEmpty(recipe.DisplayName) && !result.triggeredRecipeNames.Contains(recipe.DisplayName))
                {
                    result.triggeredRecipeNames.Add(recipe.DisplayName);
                }

                if (recipe.SynergyRequirements != null)
                {
                    foreach (var req in recipe.SynergyRequirements)
                    {
                        if (req != null && !string.IsNullOrEmpty(req.NodeID))
                        {
                            result.usedItemIDs.Add(req.NodeID);
                        }
                    }
                }
                
                if (recipe.StatModifiers != null)
                {
                    result.extraModifiers.AddRange(recipe.StatModifiers);
                }
                
                Debug.Log($"[SYNERGY CALCULATOR] Combo ScriptableObject ativado: {recipe.DisplayName}!");
            }
        }

        // Garante que o desconto nunca seja menor que 0 ou maior que 1 (100%)
        result.totalEssenceDiscount = Mathf.Clamp01(result.totalEssenceDiscount);

        return result;
    }

    /// <summary>
    /// Retorna todos os itens da lista informada que compõem ao menos uma sinergia com outro item da lista.
    /// </summary>
    public HashSet<string> GetSynergisticItemsInList(IEnumerable<string> inventoryItemIds, out Dictionary<string, string> itemToRecipeName)
    {
        return SynergyDatabase.GetSynergisticItemsInList(inventoryItemIds, out itemToRecipeName);
    }
}
