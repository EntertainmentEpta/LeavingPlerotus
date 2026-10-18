using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Gerencia toda a lógica de crafting do jogo.
/// Singleton — persiste entre cenas via DontDestroyOnLoad.
///
/// RESPONSABILIDADES:
///   • Verificar se o jogador tem materiais suficientes (CanCraft)
///   • Consumir materiais da Bolsa Sintética (baseResources no SaveManager)
///   • Entregar o resultado (item ou melhoria equipável)
///   • Disparar eventos para a UI se atualizar
///   • Salvar progresso após cada craft
///
/// DEPENDÊNCIAS:
///   - SaveManager.instance  (leitura/escrita de baseResources)
///   - ItemDatabase.Instance (lookup de ItemData para ícones/nomes)
///   - EquipmentManager      (para entregar melhorias craftadas)
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Receitas Disponíveis")]
    [Tooltip("Arraste todos os ScriptableObjects de CraftingRecipe aqui")]
    public List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

    // ─── EVENTOS ─────────────────────────────────────────────────────────────

    /// <summary>Disparado após um craft bem-sucedido. Passa a receita craftada.</summary>
    public static event Action<CraftingRecipe> OnCraftCompleted;

    /// <summary>Disparado quando as receitas disponíveis mudam (reload).</summary>
    public static event Action OnRecipesChanged;

    // ─── CICLO DE VIDA ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Garante que todas as receitas no Resources sejam carregadas e mescladas
        CraftingRecipe[] loaded = Resources.LoadAll<CraftingRecipe>("");
        if (loaded.Length > 0)
        {
            foreach (var recipe in loaded)
            {
                if (recipe != null && !allRecipes.Contains(recipe))
                {
                    allRecipes.Add(recipe);
                }
            }
            Debug.Log($"[CRAFTING] Mesclou {loaded.Length} receitas do Resources. Total: {allRecipes.Count}");
        }

        // Garante que a receita gratuita do Detector de Barreiras esteja disponível no Crafting
        if (!allRecipes.Exists(r => r != null && r.recipeId == "recipe_detector_barreiras"))
        {
            CraftingRecipe barrierRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            barrierRecipe.recipeId     = "recipe_detector_barreiras";
            barrierRecipe.recipeName   = "Detector de Barreiras";
            barrierRecipe.description  = "Mapeador portátil gratuito que exibe na tela o progresso das salas do nível.";
            barrierRecipe.ingredients  = new List<CraftingIngredient>(); // 0 Ingredientes = GRATUITO!
            barrierRecipe.resultType   = CraftingResultType.Equipment;
            barrierRecipe.resultEquipment = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetEquipmentData("equip_detector_barreiras") : null;

            allRecipes.Add(barrierRecipe);
        }

        // Garante que a receita do Machado de Cristal esteja disponível no Crafting
        if (!allRecipes.Exists(r => r != null && (r.recipeId == "recipe_crystal_axe" || r.recipeId == "recipe_machado_cristal")))
        {
            CraftingRecipe axeRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            axeRecipe.recipeId     = "recipe_crystal_axe";
            axeRecipe.recipeName   = "Machado de Cristal";
            axeRecipe.description  = "Armamento pesado de corte cristalino. Causa alto impacto, golpes em área e estilhaços.";
            axeRecipe.ingredients  = new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "shard_splinter_t1", quantity = 3 },
                new CraftingIngredient { itemId = "cristal_explosivo", quantity = 2 }
            };
            axeRecipe.resultType   = CraftingResultType.Equipment;
            axeRecipe.resultEquipment = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetEquipmentData("eq_crystal_axe") : null;

            allRecipes.Add(axeRecipe);
        }

        RegisterBlueprintRecipes();

        Debug.Log($"[CRAFTING] CraftingManager inicializado com {allRecipes.Count} receitas.");
    }

    public void RegisterBlueprintRecipes()
    {
        if (LoreManager.Instance == null) return;
        foreach (var sc in LoreManager.Instance.allScriptures)
        {
            if (sc == null || !sc.hasBlueprint || string.IsNullOrEmpty(sc.blueprintEquipmentId)) continue;
            string rId = "recipe_" + sc.blueprintEquipmentId;
            if (allRecipes.Exists(r => r != null && r.recipeId == rId)) continue;

            CraftingRecipe bpRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            bpRecipe.recipeId = rId;
            bpRecipe.recipeName = sc.blueprintTitle;
            bpRecipe.description = sc.blueprintDescription;
            bpRecipe.ingredients = sc.ingredients != null ? new List<CraftingIngredient>(sc.ingredients) : new List<CraftingIngredient>();
            bpRecipe.resultType = CraftingResultType.Equipment;
            bpRecipe.resultEquipment = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetEquipmentData(sc.blueprintEquipmentId) : null;

            allRecipes.Add(bpRecipe);
        }
    }

    // ─── HELPERS INTERNOS ─────────────────────────────────────────────────────

    private PlayerInventory GetPlayerInventory()
    {
        GameObject player = GameManager.instance?.currentPlayer;
        return player != null ? player.GetComponent<PlayerInventory>() : null;
    }

    /// <summary>Soma o total de um ingrediente no inventário de run + Bolsa Sintética.</summary>
    private int GetTotalIngredientCount(string itemId)
    {
        int fromBag = SaveManager.instance != null ? SaveManager.instance.GetBaseResourceCount(itemId) : 0;
        PlayerInventory inv = GetPlayerInventory();
        int fromRun = inv != null ? inv.GetItemCount(itemId) : 0;
        return fromBag + fromRun;
    }

    // ─── API PRINCIPAL ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se o jogador possui todos os ingredientes necessários
    /// somando inventário de run + Bolsa Sintética.
    /// </summary>
    public bool CanCraft(CraftingRecipe recipe)
    {
        if (recipe == null || SaveManager.instance == null) return false;

        foreach (var ingredient in recipe.ingredients)
        {
            int available = GetTotalIngredientCount(ingredient.itemId);
            if (available < ingredient.quantity)
                return false;
        }

        // Se o resultado é Equipment, verifica maxStack
        if (recipe.resultType == CraftingResultType.Equipment && recipe.resultEquipment != null)
        {
            EquipmentData equip = recipe.resultEquipment;
            if (equip.maxStack > 0)
            {
                int owned = SaveManager.instance.GetCraftedEquipmentCount(equip.equipmentId);
                if (owned >= equip.maxStack)
                {
                    Debug.Log($"[CRAFTING] Limite de stack atingido para {equip.equipmentName} ({owned}/{equip.maxStack})");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Executa o craft: consome materiais, entrega resultado, salva e dispara eventos.
    /// Retorna true se o craft foi bem-sucedido.
    /// </summary>
    public bool Craft(CraftingRecipe recipe)
    {
        if (!CanCraft(recipe))
        {
            Debug.LogWarning($"[CRAFTING] Não é possível craftar: {recipe?.recipeName ?? "null"}");
            return false;
        }

        // 1. Consome ingredientes — inventário de run primeiro, depois Bolsa Sintética
        PlayerInventory inv = GetPlayerInventory();
        foreach (var ingredient in recipe.ingredients)
        {
            int needed = ingredient.quantity;

            // Consome do inventário de run primeiro
            if (inv != null)
            {
                int inRun = inv.GetItemCount(ingredient.itemId);
                if (inRun > 0)
                {
                    int consumeFromRun = Mathf.Min(inRun, needed);
                    inv.RemoveItem(ingredient.itemId, consumeFromRun);
                    needed -= consumeFromRun;
                }
            }

            // Consome o restante da Bolsa Sintética
            if (needed > 0)
            {
                bool removed = SaveManager.instance.RemoveResourceFromBase(ingredient.itemId, needed);
                if (!removed)
                {
                    Debug.LogError($"[CRAFTING] Erro ao remover ingrediente {ingredient.itemId} da SyntBag! Craft abortado.");
                    return false;
                }
            }
        }

        // 2. Entrega o resultado
        switch (recipe.resultType)
        {
            case CraftingResultType.Item:
                // Adiciona o item resultado à Bolsa Sintética
                if (!string.IsNullOrEmpty(recipe.resultItemId))
                {
                    SaveManager.instance.AddResourceToBase(recipe.resultItemId, 1);
                    Debug.Log($"[CRAFTING] Item craftado: {recipe.resultItemId}");
                }
                break;

            case CraftingResultType.Equipment:
                EquipmentData equipData = recipe.resultEquipment;
                if (equipData == null && EquipmentManager.Instance != null)
                {
                    string targetEquipId = recipe.recipeId == "recipe_crystal_axe" ? "eq_crystal_axe" :
                        (recipe.recipeId.StartsWith("recipe_") ? recipe.recipeId.Replace("recipe_", "equip_") : recipe.recipeId);
                    equipData = EquipmentManager.Instance.GetEquipmentData(targetEquipId);
                    recipe.resultEquipment = equipData;
                }

                // Registra a melhoria craftada no save
                if (equipData != null)
                {
                    SaveManager.instance.AddCraftedEquipment(equipData.equipmentId);
                    Debug.Log($"[CRAFTING] Melhoria craftada: {equipData.equipmentName}");
                }
                else
                {
                    string fallbackId = recipe.recipeId == "recipe_crystal_axe" ? "eq_crystal_axe" : recipe.recipeId.Replace("recipe_", "equip_");
                    Debug.LogWarning($"[CRAFTING] AVISO: resultEquipment era nulo para a receita '{recipe.recipeName}'. Usando ID '{fallbackId}'.");
                    SaveManager.instance.AddCraftedEquipment(fallbackId);
                }
                break;
        }

        // 3. Salva progresso imediatamente
        SaveManager.instance.SavePersistentData();

        // 4. Dispara evento
        OnCraftCompleted?.Invoke(recipe);

        Debug.Log($"[CRAFTING] ✓ Craft concluído: {recipe.recipeName}");
        return true;
    }

    /// <summary>
    /// Retorna a quantidade disponível de um ingrediente na Bolsa Sintética.
    /// Útil para a UI mostrar "3/5" ao lado de cada ingrediente.
    /// </summary>
    /// <summary>Retorna o total disponível somando inventário de run + Bolsa Sintética.</summary>
    public int GetAvailableAmount(string itemId)
    {
        if (SaveManager.instance == null) return 0;
        return GetTotalIngredientCount(itemId);
    }

    /// <summary>
    /// Retorna todas as receitas cadastradas.
    /// </summary>
    public List<CraftingRecipe> GetAllRecipes()
    {
        return allRecipes;
    }
}
