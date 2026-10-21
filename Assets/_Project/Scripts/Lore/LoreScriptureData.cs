using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define um fragmento de escritura ancestral ou amostra biológica encontrada no planeta Plerotus.
/// Criado via: Assets > Create > Lore > Scripture Data
/// </summary>
[CreateAssetMenu(fileName = "NewScripture", menuName = "Lore/Scripture Data")]
public class LoreScriptureData : ScriptableObject
{
    [Header("Identificação da Escritura")]
    [Tooltip("ID único da escritura (persistido no save)")]
    public string scriptureId;

    [Tooltip("Título do fragmento / placa")]
    public string title;

    [TextArea(4, 10)]
    [Tooltip("Trecho do diário de campo ou quebra-cabeça ancestral deixado pela expedição")]
    public string excerptLore;

    [Header("Blueprint do Robozinho de Síntese")]
    [Tooltip("Esta escritura desbloqueia uma receita técnica para o Robozinho na base?")]
    public bool hasBlueprint = false;

    [Tooltip("ID do equipamento a ser liberado na mesa de trabalho")]
    public string blueprintEquipmentId;

    [Tooltip("Título da melhoria criada pelo Robozinho")]
    public string blueprintTitle;

    [TextArea(2, 4)]
    [Tooltip("Descrição do benefício prático no gameplay")]
    public string blueprintDescription;

    [Tooltip("Ingredientes de base necessários para o Robozinho forjar")]
    public List<CraftingIngredient> ingredients = new List<CraftingIngredient>();

    [Header("Anotações de Campo Complementares (Exibidas na Página Direita)")]
    [Tooltip("Título da anotação técnica/geológica na página direita (quando não tiver blueprint)")]
    public string fieldAnalysisHeader = "ANÁLISE DE CAMPO";

    [TextArea(3, 8)]
    [Tooltip("Conteúdo descritivo complementar da anotação de campo")]
    public string fieldAnalysisNotes = "";

    [TextArea(2, 4)]
    [Tooltip("Dica tática ou recomendação de sobrevivência")]
    public string tacticalSurvivalTip = "";
}
