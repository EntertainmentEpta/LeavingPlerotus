using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resultado da avaliação de combos de sinergia na infusão.
/// </summary>
public class SynergyResult
{
    public float totalEssenceDiscount = 0f;
    public List<StatModifier> extraModifiers = new List<StatModifier>();
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

        Debug.Log($"[SYNERGY CALCULATOR] Carregadas {availableRecipes.Count} receitas de sinergia.");
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

        HashSet<string> selectedSet = new HashSet<string>(selectedItemIDs);

        foreach (var recipe in availableRecipes)
        {
            bool hasAllRequirements = true;

            // Verifica se a receita possui requisitos para validar e se todos eles estão nos itens selecionados
            if (recipe.SynergyRequirements != null && recipe.SynergyRequirements.Count > 0)
            {
                foreach (var req in recipe.SynergyRequirements)
                {
                    // Se o requisito for nulo ou o ID do requisito não estiver na seleção, falha
                    if (req == null || !selectedSet.Contains(req.NodeID))
                    {
                        hasAllRequirements = false;
                        break;
                    }
                }
            }
            else
            {
                // Uma receita vazia (sem requisitos) não deve ativar automaticamente
                hasAllRequirements = false;
            }

            if (hasAllRequirements)
            {
                // Combo validado! Adiciona os bônus da receita ao resultado final
                result.totalEssenceDiscount += recipe.EssenceDiscountPercentage;
                
                if (recipe.StatModifiers != null)
                {
                    result.extraModifiers.AddRange(recipe.StatModifiers);
                }
                
                Debug.Log($"[SYNERGY CALCULATOR] Combo ativado: {recipe.DisplayName}!");
            }
        }

        // Garante que o desconto nunca seja menor que 0 ou maior que 1 (100%)
        result.totalEssenceDiscount = Mathf.Clamp01(result.totalEssenceDiscount);

        return result;
    }
}
