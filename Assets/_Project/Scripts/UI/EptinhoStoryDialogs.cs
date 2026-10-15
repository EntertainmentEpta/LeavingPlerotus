using System;
using UnityEngine;

/// <summary>
/// Centraliza os diálogos explicativos do Eptinho (com Time Freeze pelo DialogManager).
/// Todos utilizam IDs únicos persistentes (oneShotId) salvos em SaveManager.instance.CachedData.seenDialogIds,
/// garantindo que cada explicação toque exatamente uma vez por save.
/// </summary>
public static class EptinhoStoryDialogs
{
    public const string ID_INTRO_EPTINHO = "dialog_intro_eptinho";
    public const string ID_PORTA_CONSERTADA = "dialog_porta_consertada";
    public const string ID_FIRST_MONSTER_LOOT = "dialog_first_monster_loot";
    public const string ID_FIRST_RESOURCE_MINED = "dialog_first_resource_mined";
    public const string ID_SINTONIZADOR_ENCOUNTER = "dialog_sintonizador_encounter";
    public const string ID_MERCHANT_ENCOUNTER = "dialog_merchant_encounter";

    public static bool HasSeenDialog(string oneShotId)
    {
        if (string.IsNullOrEmpty(oneShotId)) return false;
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null) return false;
        return SaveManager.instance.CachedData.seenDialogIds != null &&
               SaveManager.instance.CachedData.seenDialogIds.Contains(oneShotId);
    }

    public static void MarkSeen(string oneShotId)
    {
        if (string.IsNullOrEmpty(oneShotId)) return;
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null) return;
        if (SaveManager.instance.CachedData.seenDialogIds == null)
            SaveManager.instance.CachedData.seenDialogIds = new System.Collections.Generic.List<string>();

        if (!SaveManager.instance.CachedData.seenDialogIds.Contains(oneShotId))
        {
            SaveManager.instance.CachedData.seenDialogIds.Add(oneShotId);
            SaveManager.instance.SavePersistentData();
        }
    }

    private static readonly System.Collections.Generic.HashSet<string> queuedOrActiveDialogs = new System.Collections.Generic.HashSet<string>();

    public static bool TryTriggerDialog(string oneShotId, string[] lines, Action onComplete = null)
    {
        if (HasSeenDialog(oneShotId) || queuedOrActiveDialogs.Contains(oneShotId)) return false;

        DialogManager dm = DialogManager.Instance;
        if (dm == null) return false;

        queuedOrActiveDialogs.Add(oneShotId);
        MarkSeen(oneShotId); // Bloqueia imediatamente chamadas em frames seguintes

        dm.StartDialog(lines, () =>
        {
            queuedOrActiveDialogs.Remove(oneShotId);
            onComplete?.Invoke();
        });
        return true;
    }

    /// <summary>
    /// Evento: Porta da Run consertada na Base.
    /// </summary>
    public static bool TriggerPortaConsertada(Action onComplete = null)
    {
        string[] lines = new string[]
        {
            "Eptinho: \"Excelente! A câmara de descompressão está online. Lembre-se: você não tem armas militares. Use suas ferramentas de engenharia para se defender lá fora. Se encontrar algo estranho, eu congelo seu visor para explicar.\""
        };
        return TryTriggerDialog(ID_PORTA_CONSERTADA, lines, onComplete);
    }

    /// <summary>
    /// Evento: Primeira Parte de Monstro coletada na Run (Explica Sinergias Alquímicas).
    /// </summary>
    public static bool TriggerFirstMonsterLoot(Action onComplete = null)
    {
        string[] lines = new string[]
        {
            "Eptinho: \"Opa, você pegou um pedaço de monstro! Você pode fundir isso na sua arma agora para ganhar atributos básicos... MAS, se tiver paciência para guardar no inventário e fundir peças específicas juntas (de 2 a 4 peças), você cria uma Sinergia Alquímica super poderosa e mais barata. Vale a pena arriscar!\""
        };
        return TryTriggerDialog(ID_FIRST_MONSTER_LOOT, lines, onComplete);
    }

    /// <summary>
    /// Evento: Primeiro Minério / Recurso Puro coletado (Explica a Bolsa Sintética e Recursos Persistentes).
    /// </summary>
    public static bool TriggerFirstResourceMined(Action onComplete = null)
    {
        string[] lines = new string[]
        {
            "Eptinho: \"Boa coleta! Preste atenção: se você morrer, o planeta vai digerir todas as partes de monstros do seu inventário. Mas, esses recursos minerais puros vão direto para uma bolsa sintética blindada. Eles você nunca perde, e servirão para consertar a base no futuro!\""
        };
        return TryTriggerDialog(ID_FIRST_RESOURCE_MINED, lines, onComplete);
    }

    /// <summary>
    /// Evento: Sintonizador avistado pela primeira vez (Explica o suporte inimigo).
    /// </summary>
    public static bool TriggerSintonizadorEncounter(Action onComplete = null)
    {
        string[] lines = new string[]
        {
            "Eptinho: \"Alerta vermelho! Está vendo aquele robozinho esquisito flutuando ali? Ele é um Sintonizador. Ele injeta energia nos monstros maiores, deixando eles mais rápidos e destrutivos. Dica: Destrua o robozinho primeiro, ou eles vão te esmagar!\""
        };
        return TryTriggerDialog(ID_SINTONIZADOR_ENCOUNTER, lines, onComplete);
    }

    /// <summary>
    /// Evento: Sala do Mercador / Encontro com o Mercador (Explica o Tarô Proibido e Pactos de Sangue).
    /// </summary>
    public static bool TriggerMerchantEncounter(Action onComplete = null)
    {
        string[] lines = new string[]
        {
            "Eptinho: \"Que calafrio... Tem um nativo muito estranho ali. O dialeto dele é bizarro, mas consegui traduzir o básico. O ponto principal dele são as Cartas de Maldição. Ele pode transformar completamente o seu estilo de jogo te dando poderes absurdos através dessas cartas... mas o preço não é dinheiro, é a sua Vida Máxima (Sangue). Negocie por sua conta e risco!\""
        };
        return TryTriggerDialog(ID_MERCHANT_ENCOUNTER, lines, onComplete);
    }
}
