using System;
using System.Collections.Generic;

/// <summary>
/// Entrada única de item salvo: ID + quantidade.
/// </summary>
[Serializable]
public class ItemSaveEntry
{
    public string itemId;
    public int quantity;

    public ItemSaveEntry() { }
    public ItemSaveEntry(string id, int qty) { itemId = id; quantity = qty; }
}

/// <summary>
/// Dados permanentes do jogador que sobrevivem entre sessões.
/// Salvos em disco ao fechar o jogo e carregados na inicialização.
///
/// Conteúdo:
///   inventoryMaxSlots        — capacidade de slots comprada na base
///   baseResources            — itens com returnsToBase=true acumulados ao longo das runs
///   purchasedUpgradeIndices  — índices dos upgrades de PlayerUpgrades já comprados
/// </summary>
[Serializable]
public class PersistentSaveData
{
    public int inventoryMaxSlots = 10;
    public List<ItemSaveEntry> baseResources = new List<ItemSaveEntry>();
    public List<int> purchasedUpgradeIndices = new List<int>();
    public string selectedSkinID = "astronaut";
    public List<string> craftedEquipmentIds = new List<string>();
    public List<string> equippedEquipmentIds = new List<string>();
    public List<string> inimigosDescobertos = new List<string>();

    // --- Sistema de Quests Nível AAA ---
    public string activeQuestId = "";
    public int activeQuestProgress = 0;
    public List<string> completedQuestIds = new List<string>();

    // --- Meta Suprema do Jogo (Núcleos da Nave) ---
    public int shipPartsRecovered = 0;

    // --- Onboarding & Tutoriais ---
    public int tutorialStep = 0;
    public List<string> seenDialogIds = new List<string>();
}

