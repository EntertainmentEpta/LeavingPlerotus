using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Motor central de Upgrades (InfusÃ£o e Reciclagem).
/// Fica no objeto Player (junto com o PlayerInventory e os Status base).
/// 
/// Implementa a fÃ³rmula de inflaÃ§Ã£o do GDD (Economy.pdf Â§1.3):
///   C = B Ã— (1,0 + Î± Ã— Ptotal)
///   B     = custo base do tier (T1=60, T2=180, T3=300, T4=420)
///   Î±     = 0,1 (coeficiente de inflaÃ§Ã£o - usando o valor dos exemplos do GDD)
///   Ptotal = soma dos pesos dos itens jÃ¡ infundidos (T1=1, T2=2.25, T3=4, T4=6)
/// </summary>
public class InfusionManager : MonoBehaviour
{
    private PlayerInventory inventory;
    private PlayerAttributesOffensive offensiveStats;
    private PlayerAttributesDefensive defensiveStats;
    private PlayerHealth healthStats;
    private PlayerEssence essenceWallet;

    [Header("Inflação de Infusão")]
    [Tooltip("α = coeficiente de inflação balanceado para permitir progressão fluida sem travar a run.")]
    public float inflationAlpha = 0.04f;

    [Header("Fluxo Acumulado (2 itens iguais)")]
    [Tooltip("Multiplicador progressivo (M) aplicado aos buffs ao fundir 2 itens iguais. Padrão: ×2.")]
    public float fusionStackMultiplier = 2f;

    // Quantidade de unidades consumidas no Fluxo Acumulado
    private const int FUSION_ITEM_COUNT = 2;

    // Fator mínimo de um buff multiplicativo escalado (evita fator ≤ 0 e divisão por zero na remoção)
    private const float MIN_MULTIPLIER_FACTOR = 0.05f;

    [Header("Cirurgia de Remoção")]
    [Tooltip("Custo fixo de Essência cobrado para realizar a cirurgia de remoção.")]
    public int surgeryEssenceCost = 150;

    [Header("Limites de Câmara e Capacidade do Traje")]
    [Tooltip("Limite padrão de itens na câmara de infusão ao iniciar a run (sem upgrades).")]
    public int baseChamberLimit = 2;

    [Tooltip("Capacidade máxima padrão de infusões ativas no traje por run (sem upgrades).")]
    public int baseCapacityLimit = 6;

    /// <summary>
    /// Retorna o limite atual da câmara de infusão (2 base, 3 com Expansão I, 4 com Expansão II).
    /// </summary>
    public int GetMaxChamberItems()
    {
        int limit = baseChamberLimit;
        if (EquipmentManager.Instance != null)
        {
            if (EquipmentManager.Instance.IsOwned("equip_camara_infusao_2") || EquipmentManager.Instance.IsEquipped("equip_camara_infusao_2"))
                limit = 4;
            else if (EquipmentManager.Instance.IsOwned("equip_camara_infusao_1") || EquipmentManager.Instance.IsEquipped("equip_camara_infusao_1"))
                limit = 3;
        }
        return limit;
    }

    /// <summary>
    /// Retorna a capacidade máxima de infusões ativas no traje por run (6 base, 8 com Chassi I, 10 com Chassi II).
    /// </summary>
    public int GetMaxInfusionCapacity()
    {
        int limit = baseCapacityLimit;
        if (EquipmentManager.Instance != null)
        {
            if (EquipmentManager.Instance.IsOwned("equip_capacidade_infusao_2") || EquipmentManager.Instance.IsEquipped("equip_capacidade_infusao_2"))
                limit = 10;
            else if (EquipmentManager.Instance.IsOwned("equip_capacidade_infusao_1") || EquipmentManager.Instance.IsEquipped("equip_capacidade_infusao_1"))
                limit = 8;
        }
        return limit;
    }

    /// <summary>Quantidade de infusões ativas no momento.</summary>
    public int CurrentInfusionCount => infusedItems != null ? infusedItems.Count : 0;

    /// <summary>Verifica se há capacidade no traje para mais infusões.</summary>
    public bool HasInfusionCapacity(int incomingCount = 1)
    {
        return (CurrentInfusionCount + incomingCount) <= GetMaxInfusionCapacity();
    }

    // Peso total acumulado de todos os itens já infundidos (Ptotal)
    private float totalInfusionWeight = 0f;
    
    // Histórico de itens infundidos
    [HideInInspector]
    public System.Collections.Generic.List<ItemData> infusedItems = new System.Collections.Generic.List<ItemData>();

    // Multiplicador M usado em cada infusão (alinhado por índice com infusedItems).
    // Permite que a Cirurgia de Remoção reverta exatamente o que foi aplicado (×1 ou ×2).
    private readonly List<float> infusedStackMultipliers = new List<float>();
    // Registra o nome da sinergia ativada para cada item (se fez parte de uma sinergia)
    private readonly List<string> infusedSynergyNames = new List<string>();

    void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        
        // PegaInChildren pois as vezes esses scripts ficam no modelo 3D do player ("Astronaut")
        offensiveStats = GetComponentInChildren<PlayerAttributesOffensive>();
        defensiveStats = GetComponentInChildren<PlayerAttributesDefensive>();
        
        healthStats = GetComponent<PlayerHealth>();
        essenceWallet = GetComponent<PlayerEssence>();

        // DiagnÃ³stico para garantir que nÃ£o falta nada
        if (inventory == null || offensiveStats == null || defensiveStats == null || healthStats == null || essenceWallet == null)
        {
            Debug.LogWarning("[INFUSION MANAGER] Faltando componentes no Player! Verifique se todos os scripts de status estÃ£o adicionados no mesmo objeto.");
        }
    }

    /// <summary>
    /// Recicla o item, ganhando a essÃªncia configurada no ItemData e removendo o item da mochila.
    /// </summary>
    public bool RecycleItem(string itemId)
    {
        if (ItemDatabase.Instance == null) return false;
        
        ItemData data = ItemDatabase.Instance.GetItemData(itemId);
        if (data == null) return false;

        // Verifica se tem o item no inventário antes de destruir
        if (inventory != null && inventory.HasItem(itemId, 1))
        {
            // Remove 1 do inventário SEM dar essência (apenas joga fora conforme solicitado)
            inventory.RemoveItem(itemId, 1);
            
            Debug.Log($"[INFUSÃO] Item Descartado (Jogado fora): {data.itemName}");
            return true;
        }

        return false;
    }

    // =====================================================
    // SISTEMA DE INFLAÃ‡ÃƒO (GDD Â§1.3)
    // =====================================================

    /// <summary>
    /// Tabela base de custo por raridade/tier: T1=50, T2=100, T3=180, T4=280.
    /// </summary>
    public static int GetBaseTierCost(ItemTier tier) => tier switch
    {
        ItemTier.Common    => 50,
        ItemTier.Uncommon  => 100,
        ItemTier.Rare      => 180,
        ItemTier.Legendary => 280,
        _                  => 50
    };

    public int GetBaseCost(ItemData data)
    {
        if (data == null) return 0;
        int tierCost = GetBaseTierCost(data.tier);
        // Corrige assets com custo 0 ou custo descalibrado
        if (data.infusionEssenceCost <= 20 || (data.tier == ItemTier.Legendary && data.infusionEssenceCost > 320) || (data.tier == ItemTier.Common && data.infusionEssenceCost > 70))
        {
            return tierCost;
        }
        return data.infusionEssenceCost;
    }

    /// <summary>
    /// Calcula o custo REAL de infusão com a inflação acumulada e teto de balanceamento (+50% máx).
    /// </summary>
    public int GetInflatedCost(ItemData data)
    {
        if (data == null) return 0;
        int baseCost = GetBaseCost(data);
        float factor = Mathf.Min(1.5f, 1f + inflationAlpha * totalInfusionWeight);
        return Mathf.RoundToInt(baseCost * factor);
    }

    /// <summary>
    /// Retorna o Ptotal atual (peso acumulado de infusÃµes).
    /// Ãštil para exibir na UI info sobre o estado de inflaÃ§Ã£o.
    /// </summary>
    public float GetTotalWeight() => totalInfusionWeight;

    /// <summary>
    /// Reseta o peso acumulado ao iniciar uma nova Run.
    /// Chamado pelo GameManager via LoadGameLevel().
    /// </summary>
    public void ResetRunInflation()
    {
        totalInfusionWeight = 0f;
        if (infusedItems != null) infusedItems.Clear();
        infusedStackMultipliers.Clear();
        infusedSynergyNames.Clear();
        Debug.Log("[INFUSION MANAGER] Peso de inflaÃ§Ã£o e histÃ³rico de infusÃµes resetados para nova Run.");
    }

    // =====================================================
    // FLUXO DE INFUSÃO: SIMPLES (1 item) / ACUMULADO (2 iguais)
    // =====================================================

    /// <summary>
    /// Ponto de entrada da infusão padrão. Segue o fluxo:
    ///   1. Resolve o fluxo (Simples ×1 / Acumulado ×M) a partir da seleção.
    ///   2. Verifica a Essence. Sem saldo → aborta (nada é consumido).
    ///   3. Deduz a Essence.
    ///   4. Aplica os buffs escalados por M.
    ///   5. Soma a contribuição de inflação (P_tier × M) ao Ptotal.
    ///   6. Remove do inventário as unidades consumidas.
    /// </summary>
    /// <param name="itemIds">[id] (funde automaticamente se houver 2+ unidades) ou [id, id] (fusão explícita).</param>
    public InfusionResult TryInfuse(IReadOnlyList<string> itemIds)
    {
        if (inventory == null || essenceWallet == null)
        {
            Debug.LogWarning("[INFUSÃO] Abortado: PlayerInventory ou PlayerEssence ausente no Player.");
            return InfusionResult.MissingComponents;
        }

        InfusionRequest request = BuildInfusionRequest(itemIds);
        if (!request.IsValid)
        {
            Debug.LogWarning("[INFUSÃO] Abortado: seleção inválida (use 1 item ou 2 itens iguais).");
            return InfusionResult.InvalidSelection;
        }

        ItemData data = request.data;

        // Regra de Balanceamento (Brotato x Hades): Itens Lendários (T4) têm limite de 1 infusão por run (Max Stacks = 1)
        if (data.tier == ItemTier.Legendary && infusedItems.Contains(data))
        {
            Debug.LogWarning($"[INFUSÃO] {data.itemName} é Lendário (T4) e já foi infundido nesta run! (Limite = 1)");
            return InfusionResult.LegendaryLimit;
        }

        // Regra de Capacidade do Traje / Limite de Sobreaquecimento
        if (!HasInfusionCapacity(1))
        {
            Debug.LogWarning($"[INFUSÃO] Bloqueado! Capacidade do traje atingida ({CurrentInfusionCount}/{GetMaxInfusionCapacity()}).");
            return InfusionResult.CapacityLimit;
        }

        if (!inventory.HasItem(data.itemId, request.itemCount))
        {
            Debug.LogWarning($"[INFUSÃO] Abortado: precisa de {request.itemCount}x {data.itemName}, tem {inventory.GetItemCount(data.itemId)}.");
            return InfusionResult.MissingItems;
        }

        // --- 1. Verificação de Essence ---
        int cost = GetInfusionCost(request);
        if (essenceWallet.GetEssence() < cost)
        {
            Debug.Log($"[INFUSÃO] Bloqueado! Custo: {cost} Essence (base {data.infusionEssenceCost} × inflação {GetInflationFactor():F2}). Você tem: {essenceWallet.GetEssence()}");
            return InfusionResult.NotEnoughEssence;
        }

        // --- 2. Dedução ---
        if (!essenceWallet.SpendEssence(cost)) return InfusionResult.NotEnoughEssence;

        // --- 3. Aplicação dos buffs (×1 no Simples, ×M no Acumulado) ---
        ApplyScaledBuffs(data, request.stackMultiplier);

        // --- 4. Equação de inflação: Ptotal += P_tier × M ---
        float inflationDelta = GetInflationContribution(data, request.stackMultiplier);
        totalInfusionWeight += inflationDelta;

        RegisterInfusion(data, request.stackMultiplier);
        ActivateTier4Effect(data);

        // --- 5. Consumo dos itens ---
        inventory.RemoveItem(data.itemId, request.itemCount);

        string flow = request.mode == InfusionMode.Fused ? $"ACUMULADO ×{request.stackMultiplier:0.##}" : "SIMPLES";
        Debug.Log($"[INFUSÃO] {flow} | {data.itemName} (-{request.itemCount} un.) | Custo: {cost} | ΔPtotal: +{inflationDelta:F2} → {totalInfusionWeight:F2}");

        return request.mode == InfusionMode.Fused ? InfusionResult.Fused : InfusionResult.Success;
    }

    /// <summary>
    /// Resolve QUAL fluxo será executado, sem alterar estado (seguro para preview na UI).
    ///   • [id] com 1 unidade no inventário  → Simples   (consome 1, M = 1)
    ///   • [id] com 2+ unidades no inventário → Acumulado (consome 2, M = fusionStackMultiplier)
    ///   • [id, id]                           → Acumulado (fusão explícita)
    ///   • Qualquer outra combinação          → Inválido (sinergias usam InfuseMultipleItems)
    /// Lendários (T4) nunca fundem: Max Stacks = 1.
    /// </summary>
    public InfusionRequest BuildInfusionRequest(IReadOnlyList<string> itemIds)
    {
        InfusionRequest request = new InfusionRequest { mode = InfusionMode.Invalid, itemCount = 0, stackMultiplier = 1f };

        if (itemIds == null || itemIds.Count == 0 || itemIds.Count > FUSION_ITEM_COUNT) return request;
        if (ItemDatabase.Instance == null || inventory == null) return request;

        string itemId = itemIds[0];
        bool explicitFusion = itemIds.Count == FUSION_ITEM_COUNT;
        if (explicitFusion && itemIds[1] != itemId) return request; // 2 itens diferentes → não é fluxo padrão

        ItemData data = ItemDatabase.Instance.GetItemData(itemId);
        if (data == null) return request;

        // Infusão padrão sempre infunde exatamente 1 item (sem x2 automático)
        request.data = data;
        request.mode = InfusionMode.Simple;
        request.itemCount = 1;
        request.stackMultiplier = 1f;

        return request;
    }

    /// <summary>Atalho para a UI decidir entre o fluxo padrão e o fluxo de sinergia.</summary>
    public bool IsStandardInfusion(IReadOnlyList<string> itemIds) => BuildInfusionRequest(itemIds).IsValid;

    /// <summary>
    /// Custo do pedido. Ambos os fluxos custam 1× o custo inflacionado do item
    /// (a fusão é recompensada: 2 unidades pelo preço de 1 infusão).
    /// </summary>
    public int GetInfusionCost(InfusionRequest request) => request.IsValid ? GetInflatedCost(request.data) : 0;

    public int GetInfusionCost(IReadOnlyList<string> itemIds) => GetInfusionCost(BuildInfusionRequest(itemIds));

    /// <summary>Fator atual da equação de inflação: (1 + α × Ptotal), limitado a 1.5x (teto de +50%).</summary>
    public float GetInflationFactor() => Mathf.Min(1.5f, 1f + inflationAlpha * totalInfusionWeight);

    // ---------- Matemática modular do acúmulo ----------

    /// <summary>
    /// Escala UM buff pelo multiplicador do fluxo (M).
    ///   Aditivo:        v' = v × M             (+50 HP → +100 HP)
    ///   Multiplicativo: f' = 1 + (f − 1) × M   (×1.10 → ×1.20 | ×0.80 → ×0.60)
    /// No multiplicativo só o BÔNUS (f − 1) é escalado: dobrar o fator inteiro
    /// (1.1 × 2 = 2.2) daria +120% em vez de +20%.
    /// </summary>
    public static ItemAttributeParam ScaleBuff(ItemAttributeParam buff, float stackMultiplier)
    {
        float scaledValue = buff.isMultiplier
            ? Mathf.Max(MIN_MULTIPLIER_FACTOR, 1f + (buff.value - 1f) * stackMultiplier)
            : buff.value * stackMultiplier;

        return new ItemAttributeParam
        {
            attributeType = buff.attributeType,
            value = scaledValue,
            isMultiplier = buff.isMultiplier
        };
    }

    /// <summary>
    /// Quanto esta infusão soma ao Ptotal: ΔP = P_tier × M.
    /// Simples (M = 1) → peso normal do tier. Acumulado (M = 2) → peso de 2 itens.
    /// </summary>
    public static float GetInflationContribution(ItemData data, float stackMultiplier)
    {
        return data != null ? data.GetTierWeight() * stackMultiplier : 0f;
    }

    private void ApplyScaledBuffs(ItemData data, float stackMultiplier)
    {
        if (data.itemAttributes == null) return;
        foreach (var buff in data.itemAttributes)
        {
            ApplyAttribute(ScaleBuff(buff, stackMultiplier));
        }
    }

    private void RegisterInfusion(ItemData data, float stackMultiplier, string synergyName = "")
    {
        // Re-sincroniza caso infusedItems tenha sido limpo externamente (ex: PlayerHealth no respawn)
        while (infusedStackMultipliers.Count > infusedItems.Count) infusedStackMultipliers.RemoveAt(infusedStackMultipliers.Count - 1);
        while (infusedStackMultipliers.Count < infusedItems.Count) infusedStackMultipliers.Add(1f);
        while (infusedSynergyNames.Count > infusedItems.Count) infusedSynergyNames.RemoveAt(infusedSynergyNames.Count - 1);
        while (infusedSynergyNames.Count < infusedItems.Count) infusedSynergyNames.Add("");

        infusedItems.Add(data);
        infusedStackMultipliers.Add(stackMultiplier);
        infusedSynergyNames.Add(synergyName ?? "");

        // Integração de Descoberta com o Catálogo do Eptinho (Fog of War de Atributos)
        if (CatalogoManager.instancia != null && data != null)
        {
            CatalogoManager.instancia.RegistrarInfusao(data.itemId);
        }
    }

    private void ActivateTier4Effect(ItemData data)
    {
        if (data.tier4Effect == Tier4EffectType.None) return;

        Tier4EffectManager t4Manager = GetComponent<Tier4EffectManager>();
        if (t4Manager != null)
            t4Manager.ActivateEffect(data.tier4Effect);
        else
            Debug.LogWarning("[INFUSÃO] Item T4 com efeito especial, mas Tier4EffectManager não encontrado no Player!");
    }

    /// <summary>
    /// Infunde o item no corpo, ganhando TODOS os atributos permanentemente e consumindo o item do inventário.
    /// Mantido por compatibilidade: delega para o fluxo padrão (TryInfuse).
    /// </summary>
    public bool InfuseItem(string itemId)
    {
        InfusionResult result = TryInfuse(new[] { itemId });
        return result == InfusionResult.Success || result == InfusionResult.Fused;
    }

    /// <summary>
    /// Usado pelo Mercador na "Cirurgia de RemoÃ§Ã£o".
    /// Remove permanentemente os efeitos de um item e reduz o peso de inflaÃ§Ã£o.
    /// </summary>
    public bool InfuseMultipleItems(System.Collections.Generic.List<string> itemIds)
    {
        if (ItemDatabase.Instance == null || inventory == null || essenceWallet == null) return false;

        int totalCost = 0;
        float currentWeight = totalInfusionWeight; 
        System.Collections.Generic.List<ItemData> validItems = new System.Collections.Generic.List<ItemData>();
        System.Collections.Generic.List<string> itemIDsToInfuse = new System.Collections.Generic.List<string>();
        
        Dictionary<string, int> requiredCounts = new Dictionary<string, int>();
        foreach(var id in itemIds)
        {
            ItemData data = ItemDatabase.Instance.GetItemData(id);
            if (data == null) continue;
            if (data.tier == ItemTier.Legendary && infusedItems.Contains(data)) continue; 

            int currentReq = requiredCounts.ContainsKey(id) ? requiredCounts[id] + 1 : 1;
            if (inventory.GetItemCount(id) < currentReq) continue;
            requiredCounts[id] = currentReq;
            
            float costF = data.infusionEssenceCost * (1f + inflationAlpha * currentWeight);
            int cost = UnityEngine.Mathf.RoundToInt(costF);
            
            totalCost += cost;
            currentWeight += data.GetTierWeight();
            validItems.Add(data);
            itemIDsToInfuse.Add(data.itemId);
        }

        if (validItems.Count == 0) return false;

        // Regra de Capacidade do Traje / Limite de Sobreaquecimento
        if (!HasInfusionCapacity(validItems.Count))
        {
            Debug.LogWarning($"[INFUSÃO MULTIPLA] Bloqueado! Capacidade insuficiente no traje para +{validItems.Count} infusões ({CurrentInfusionCount}/{GetMaxInfusionCapacity()}).");
            return false;
        }

        // Cálculo da Sinergia
        SynergyResult result = SynergyInfusionCalculator.Instance.EvaluateInfusion(itemIDsToInfuse);

        // AplicaÃ§Ã£o do Desconto
        int discountedCost = UnityEngine.Mathf.RoundToInt(totalCost * (1f - result.totalEssenceDiscount));

        if (essenceWallet.GetEssence() < discountedCost)
        {
            UnityEngine.Debug.Log("[INFUSÃO MULTIPLA] Bloqueado! Sem essência suficiente.");
            return false;
        }

        // Paga o custo descontado de uma vez
        essenceWallet.SpendEssence(discountedCost);

        bool success = false;
        string recipesMsg = (result.hasSynergy && result.triggeredRecipeNames != null && result.triggeredRecipeNames.Count > 0)
            ? string.Join(", ", result.triggeredRecipeNames)
            : "";

        foreach (var data in validItems)
        {
            // Aplica stats base
            foreach (var buff in data.itemAttributes)
            {
                ApplyAttribute(buff);
            }
            
            float addedWeight = data.GetTierWeight();
            totalInfusionWeight += addedWeight;
            bool wasUsedInSynergy = result.hasSynergy && result.usedItemIDs != null && result.usedItemIDs.Contains(data.itemId);
            RegisterInfusion(data, 1f, wasUsedInSynergy ? recipesMsg : "");
            ActivateTier4Effect(data);
            
            inventory.RemoveItem(data.itemId, 1);
            success = true;
            Debug.Log($"[INFUSÃO] Sucesso! {data.itemName} infundido via múltipla. (Sinergia: {wasUsedInSynergy})");
        }

        // Aplicação dos Modificadores Extras
        if (result.extraModifiers != null)
        {
            foreach (var mod in result.extraModifiers)
            {
                ApplySynergyModifier(mod);
            }
        }

        // Se uma ou mais sinergias foram ativadas:
        if (result.hasSynergy && result.triggeredRecipeIDs != null && result.triggeredRecipeIDs.Count > 0)
        {
            foreach (var recId in result.triggeredRecipeIDs)
            {
                if (SynergyCatalogManager.Instance != null)
                {
                    SynergyCatalogManager.Instance.DiscoverNode(recId);
                }
            }

            if (SynergyMapPrototype.Instance != null && SynergyMapPrototype.Instance.IsOpen)
            {
                SynergyMapPrototype.Instance.RefreshConstellation();
            }

            Debug.Log($"[INFUSÃO] ★ Sinergia(s) Alquímica(s) Catalogada(s): {recipesMsg}!");

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(
                    $"★ SINERGIA ALQUÍMICA ATIVADA!\n'{recipesMsg}' registrada no Mapa Cósmico (M)!"
                );
            }
        }

        return success;
    }

    private void ApplySynergyModifier(StatModifier mod)
    {
        if (System.Enum.TryParse(mod.statName, out AttributeType attrType))
        {
            if (mod.baseValue != 0f)
            {
                float baseVal = mod.baseValue;
                // Chance / Percentuais expressos em 0..1 (ex: 0.10f para 10%) convertidos para escala 0..100
                if (attrType == AttributeType.CritChance || attrType == AttributeType.DodgeChance ||
                    attrType == AttributeType.MultiShotChance || attrType == AttributeType.BounceChance ||
                    attrType == AttributeType.DamageNegation)
                {
                    if (Mathf.Abs(baseVal) <= 1.0f)
                    {
                        baseVal *= 100f;
                    }
                }
                ApplyAttribute(new ItemAttributeParam { attributeType = attrType, value = baseVal, isMultiplier = false });
            }

            if (mod.multiplierValue != 0f)
            {
                // No sistema do jogo, isMultiplier espera o multiplicador completo (ex: 1.15 para +15%, 0.80 para -20% cooldown)
                float multFactor = mod.multiplierValue;
                if (multFactor > -1f && multFactor < 1f)
                {
                    multFactor = 1f + multFactor;
                }
                ApplyAttribute(new ItemAttributeParam { attributeType = attrType, value = multFactor, isMultiplier = true });
            }

            Debug.Log($"✨ [SYNERGY] Modificador aplicado: {mod.statName} (Base: {mod.baseValue}, Mult: {mod.multiplierValue})");
        }
        else
        {
            Debug.LogWarning($"[SYNERGY] Atributo {mod.statName} inválido no StatModifier!");
        }
    }

    public bool InfuseSynergy(string item1Id, string item2Id)
    {
        return InfuseMultipleItems(new System.Collections.Generic.List<string> { item1Id, item2Id });
    }
    /// <summary>
    /// Retorna a lista de infusões ativas da arma e arsenal como uma lista de InfusedItem.
    /// Atalho direto utilizado pela interface do Mercador (Aba Surgery) e pelo inventário.
    /// </summary>
    public System.Collections.Generic.List<InfusedItem> GetActiveInfusions()
    {
        return GetInfusionHistory();
    }

    /// <summary>
    /// Lógica da Cirurgia de Remoção:
    /// 1. Validação: Checa se o jogador possui o custo da cirurgia (uma taxa fixa de Essência). Se não tiver, cancela.
    /// 2. Cobrança: Deduza a taxa da cirurgia do jogador.
    /// 3. Remoção sem Reembolso: Remove a peça/buff do arsenal da arma sem devolver Essência original gasta na infusão.
    /// 4. Recálculo da Inflação: Subtrai o peso desse item da equação global de inflação (tornando futuras infusões mais baratas).
    /// </summary>
    public bool RequestSurgery(InfusedItem item)
    {
        if (item.data == null || !infusedItems.Contains(item.data))
        {
            Debug.LogWarning("[INFUSION MANAGER] Item inválido ou ausente no arsenal para cirurgia.");
            return false;
        }

        if (essenceWallet == null)
            essenceWallet = GetComponent<PlayerEssence>();

        // 1. Validação: Verifique se o jogador possui o custo da cirurgia
        if (essenceWallet == null || essenceWallet.GetEssence() < surgeryEssenceCost)
        {
            int current = essenceWallet != null ? essenceWallet.GetEssence() : 0;
            Debug.LogWarning($"[CIRURGIA] Cancelado! Essência insuficiente para cirurgia. Custo: {surgeryEssenceCost}, Disponível: {current}");
            return false;
        }

        // 2. Cobrança: Deduza a taxa da cirurgia do jogador
        if (!essenceWallet.SpendEssence(surgeryEssenceCost))
        {
            return false;
        }

        // 3 & 4. Remoção sem Reembolso e Recálculo da Inflação
        bool removed = RemoveInfusion(item);
        if (removed)
        {
            Debug.Log($"[CIRURGIA] Peça {item.data.itemName} (x{item.stacks}) extraída com sucesso. Taxa: {surgeryEssenceCost} Essências. Novo Ptotal: {totalInfusionWeight:F2}");

            // 5. Atualização: Notifica o Mercador se o painel estiver aberto
            if (MerchantUIController.HasInstance && MerchantUIController.Instance.IsUiOpen())
            {
                MerchantUIController.Instance.PopulateSurgeryList();
            }
        }

        return removed;
    }

    /// <summary>
    /// Remove a infusão especificada através da struct InfusedItem (respeitando o multiplicador exato de stack).
    /// </summary>
    public bool RemoveInfusion(InfusedItem item)
    {
        if (item.data == null || infusedItems.Count == 0) return false;

        int targetIndex = -1;
        for (int i = 0; i < infusedItems.Count; i++)
        {
            if (infusedItems[i] == item.data)
            {
                float mult = i < infusedStackMultipliers.Count ? infusedStackMultipliers[i] : 1f;
                int stacks = Mathf.RoundToInt(mult);
                if (stacks == item.stacks)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        if (targetIndex == -1)
        {
            targetIndex = infusedItems.IndexOf(item.data);
        }

        if (targetIndex == -1) return false;

        return RemoveInfusionAtIndex(targetIndex);
    }

    /// <summary>
    /// Remove permanentemente os efeitos de um item e reduz o peso de inflação.
    /// Mantido por compatibilidade.
    /// </summary>
    public bool RemoveInfusion(ItemData data)
    {
        if (data == null || !infusedItems.Contains(data)) return false;
        int index = infusedItems.IndexOf(data);
        return RemoveInfusionAtIndex(index);
    }

    /// <summary>
    /// Remove a infusão no índice indicado, revertendo buffs escalados,
    /// desativando efeito especial T4 (se não houver outra instância) e subtraindo o peso de inflação.
    /// NÃO devolve a Essência original de infusão.
    /// </summary>
    private bool RemoveInfusionAtIndex(int index)
    {
        if (index < 0 || index >= infusedItems.Count) return false;

        ItemData data = infusedItems[index];
        float stackMultiplier = index < infusedStackMultipliers.Count ? infusedStackMultipliers[index] : 1f;

        // Reverte todos os buffs escalados no arsenal da arma/player
        if (data.itemAttributes != null)
        {
            foreach (var buff in data.itemAttributes)
            {
                RemoveAttribute(ScaleBuff(buff, stackMultiplier));
            }
        }

        // Desativa efeito especial T4 (se houver e não houver outra cópia infundida)
        if (data.tier4Effect != Tier4EffectType.None)
        {
            bool hasOtherInstance = false;
            for (int i = 0; i < infusedItems.Count; i++)
            {
                if (i != index && infusedItems[i] != null && infusedItems[i].tier4Effect == data.tier4Effect)
                {
                    hasOtherInstance = true;
                    break;
                }
            }

            if (!hasOtherInstance)
            {
                Tier4EffectManager t4Manager = GetComponent<Tier4EffectManager>();
                if (t4Manager != null)
                {
                    t4Manager.DeactivateEffect(data.tier4Effect);
                }
            }
        }

        // Subtrai o peso de inflação (ΔP = P_tier × M) da equação global
        float removedWeight = GetInflationContribution(data, stackMultiplier);
        totalInfusionWeight = Mathf.Max(0f, totalInfusionWeight - removedWeight);

        infusedItems.RemoveAt(index);
        if (index < infusedStackMultipliers.Count) infusedStackMultipliers.RemoveAt(index);

        Debug.Log($"[REMOÇÃO/CIRURGIA] Peça extraída: {data.itemName} (x{Mathf.RoundToInt(stackMultiplier)}). Peso subtraído: -{removedWeight:F2}. Novo Ptotal: {totalInfusionWeight:F2}");
        return true;
    }

    /// <summary>
    /// Roteador: Descobre de quem Ã© esse atributo e manda pro script correto
    /// </summary>
    private void ApplyAttribute(ItemAttributeParam buff)
    {
        // Pega o nome exato do Enum em formato de Texto (String) para casar perfeitamente com os seus ModifyAttributes
        string attrName = buff.attributeType.ToString();

        switch (buff.attributeType)
        {
            // ======= OFENSIVOS =======
            case AttributeType.BaseDamageMultiplier:
            case AttributeType.AttackSpeedMelee:
            case AttributeType.CritChance:
            case AttributeType.CritMultiplier:
            case AttributeType.Knockback:
            case AttributeType.WeaponRangeMelee:
            case AttributeType.WeaponRangeProjectile:
            case AttributeType.Piercing:
            case AttributeType.BounceChance:
            case AttributeType.BounceCount:
            case AttributeType.MultiShotChance:
            case AttributeType.Spread:
            case AttributeType.SlowOnHit:
                if (offensiveStats != null)
                    offensiveStats.ModifyAttribute(attrName, buff.value, buff.isMultiplier);
                break;
            
            // ======= DEFENSIVOS & MOBILIDADE =======
            case AttributeType.ArmorRegen:
            case AttributeType.DodgeChance:
            case AttributeType.DamageNegation:
            case AttributeType.Thorns:
            case AttributeType.SpeedMultiplier:
            case AttributeType.DashCooldownMultiplier:
            case AttributeType.DashCounts:
            case AttributeType.DashInvulnerability:
                if (defensiveStats != null)
                    defensiveStats.ModifyAttribute(attrName, buff.value, buff.isMultiplier);
                break;

            // ======= VIDA & ARMADURA =======
            case AttributeType.MaxHealth:
            case AttributeType.MaxArmor:
                if (healthStats != null)
                    healthStats.ModifyAttribute(attrName, buff.value, buff.isMultiplier);
                break;
        }
    }

    private void RemoveAttribute(ItemAttributeParam buff)
    {
        string attrName = buff.attributeType.ToString();

        // Para inverter a soma, mandamos -buff.value
        // Para inverter o multiplicador, mandamos 1f / buff.value (com verificaÃ§Ã£o contra divisÃ£o por zero)
        float invertedValue = buff.isMultiplier ? (Mathf.Abs(buff.value) > 0.0001f ? (1f / buff.value) : 1f) : (-buff.value);

        switch (buff.attributeType)
        {
            // ======= OFENSIVOS =======
            case AttributeType.BaseDamageMultiplier:
            case AttributeType.AttackSpeedMelee:
            case AttributeType.CritChance:
            case AttributeType.CritMultiplier:
            case AttributeType.Knockback:
            case AttributeType.WeaponRangeMelee:
            case AttributeType.WeaponRangeProjectile:
            case AttributeType.Piercing:
            case AttributeType.BounceChance:
            case AttributeType.BounceCount:
            case AttributeType.MultiShotChance:
            case AttributeType.Spread:
            case AttributeType.SlowOnHit:
                if (offensiveStats != null)
                    offensiveStats.ModifyAttribute(attrName, invertedValue, buff.isMultiplier);
                break;
            
            // ======= DEFENSIVOS & MOBILIDADE =======
            case AttributeType.ArmorRegen:
            case AttributeType.DodgeChance:
            case AttributeType.DamageNegation:
            case AttributeType.Thorns:
            case AttributeType.SpeedMultiplier:
            case AttributeType.DashCooldownMultiplier:
            case AttributeType.DashCounts:
            case AttributeType.DashInvulnerability:
                if (defensiveStats != null)
                    defensiveStats.ModifyAttribute(attrName, invertedValue, buff.isMultiplier);
                break;

            // ======= VIDA & ARMADURA =======
            case AttributeType.MaxHealth:
            case AttributeType.MaxArmor:
                if (healthStats != null)
                    healthStats.ModifyAttribute(attrName, invertedValue, buff.isMultiplier);
                break;
        }
    }

    public bool HasInfusion(string itemId)
    {
        foreach (var item in infusedItems)
        {
            if (item.itemId == itemId) return true;
        }
        return false;
    }

    public System.Collections.Generic.List<ItemData> GetInfusedItems()
    {
        return infusedItems;
    }

    /// <summary>
    /// Retorna o histórico de infusões como lista tipada de InfusedItem,
    /// combinando ItemData com o multiplicador de stack (×1 Simples / ×2 Acumulado).
    /// Usado pelo módulo visual de histórico na InventoryUI.
    /// </summary>
    public System.Collections.Generic.List<InfusedItem> GetInfusionHistory()
    {
        var history = new System.Collections.Generic.List<InfusedItem>();
        for (int i = 0; i < infusedItems.Count; i++)
        {
            string syn = (i < infusedSynergyNames.Count) ? infusedSynergyNames[i] : "";
            history.Add(new InfusedItem
            {
                data = infusedItems[i],
                stacks = 1,
                isSynergy = !string.IsNullOrEmpty(syn),
                synergyName = syn
            });
        }
        return history;
    }
}

/// <summary>Fluxo de infusão resolvido a partir da seleção.</summary>
public enum InfusionMode
{
    Invalid,
    Simple, // 1 item  → buffs base (×1)
    Fused   // 2 iguais → buffs ×M (fusionStackMultiplier)
}

/// <summary>Resultado de TryInfuse, usado pela UI para feedback visual.</summary>
public enum InfusionResult
{
    Success,
    Fused,
    InvalidSelection,
    MissingItems,
    NotEnoughEssence,
    LegendaryLimit,
    MissingComponents,
    CapacityLimit
}

/// <summary>Pedido de infusão já resolvido (item, fluxo, unidades consumidas e multiplicador).</summary>
public struct InfusionRequest
{
    public ItemData data;
    public InfusionMode mode;
    public int itemCount;
    public float stackMultiplier;

    public bool IsValid => data != null && mode != InfusionMode.Invalid;
}

/// <summary>
/// Entrada no histórico de infusão: o ItemData infundido e a quantidade de stacks.
/// stacks = 1 para infusão simples, 2 para fusão acumulada (×2).
/// </summary>
[System.Serializable]
public struct InfusedItem
{
    public ItemData data;
    public int stacks;
    public bool isSynergy;
    public string synergyName;
}

