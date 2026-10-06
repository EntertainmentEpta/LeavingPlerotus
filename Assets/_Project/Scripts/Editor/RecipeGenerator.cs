using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class RecipeGenerator
{
    [MenuItem("Tools/Generate New Meta Recipes")]
    public static void Generate()
    {
        // 1. Equipment Data: Mapa de Sinergias
        EquipmentData mapEq = ScriptableObject.CreateInstance<EquipmentData>();
        mapEq.equipmentId = "eq_mapa_sinergias";
        mapEq.equipmentName = "Mapa de Sinergias";
        mapEq.description = "Permite acessar o Mapa de Sinergias no Eptinho Oráculo, revelando combinações de itens.";
        mapEq.effectType = EquipmentEffectType.UnlockSynergyMap;
        mapEq.effectValue = 1;
        mapEq.maxStack = 1;
        AssetDatabase.CreateAsset(mapEq, "Assets/_Project/Items_and_Crafting/Resources/EquipmentData/eq_mapa_sinergias.asset");

        // 2. Equipment Data: Arma de Projétil
        EquipmentData gunEq = ScriptableObject.CreateInstance<EquipmentData>();
        gunEq.equipmentId = "eq_arma_projetil";
        gunEq.equipmentName = "Arquétipo: Arma de Projétil";
        gunEq.description = "Desbloqueia a Arma de Projétil (Rifle) para ser encontrada ou equipada nas runs.";
        gunEq.effectType = EquipmentEffectType.UnlockProjectileWeapon;
        gunEq.effectValue = 1;
        gunEq.maxStack = 1;
        AssetDatabase.CreateAsset(gunEq, "Assets/_Project/Items_and_Crafting/Resources/EquipmentData/eq_arma_projetil.asset");

        // 3. Recipe: Mapa de Sinergias
        CraftingRecipe mapRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        mapRecipe.recipeId = "recipe_mapa_sinergias";
        mapRecipe.recipeName = "Mapa de Sinergias";
        mapRecipe.description = "Habilita a interface visual de Sinergias da Alquimia no Eptinho.";
        mapRecipe.resultType = CraftingResultType.Equipment;
        mapRecipe.resultEquipment = mapEq;
        mapRecipe.ingredients = new List<CraftingIngredient>() {
            new CraftingIngredient { itemId = "vagalume_cristalizado", quantity = 3 },
            new CraftingIngredient { itemId = "prismalita", quantity = 2 }
        };
        AssetDatabase.CreateAsset(mapRecipe, "Assets/_Project/Items_and_Crafting/Resources/Recipes/recipe_mapa_sinergias.asset");

        // 4. Recipe: Arma de Projétil
        CraftingRecipe gunRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        gunRecipe.recipeId = "recipe_arma_projetil";
        gunRecipe.recipeName = "Protótipo: Arma de Fogo";
        gunRecipe.description = "Desenvolve o primeiro protótipo de arma de projéteis usando energia volátil.";
        gunRecipe.resultType = CraftingResultType.Equipment;
        gunRecipe.resultEquipment = gunEq;
        gunRecipe.ingredients = new List<CraftingIngredient>() {
            new CraftingIngredient { itemId = "resta_geobionte", quantity = 2 },
            new CraftingIngredient { itemId = "cristal_explosivo", quantity = 5 }
        };
        AssetDatabase.CreateAsset(gunRecipe, "Assets/_Project/Items_and_Crafting/Resources/Recipes/recipe_arma_projetil.asset");

        AssetDatabase.SaveAssets();
        Debug.Log("Generated new recipes successfully.");
    }
}
