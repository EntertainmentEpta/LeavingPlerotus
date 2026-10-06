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

    [Header("InflaÃ§Ã£o de InfusÃ£o (GDD Â§1.3)")]
    [Tooltip("Î± = coeficiente de inflaÃ§Ã£o. GDD usa 0,1 conforme os exemplos da tabela.")]
    public float inflationAlpha = 0.1f;

    // Peso total acumulado de todos os itens jÃ¡ infundidos (Ptotal)
    private float totalInfusionWeight = 0f;
    
    // HistÃ³rico de itens infundidos
    [HideInInspector]
    public System.Collections.Generic.List<ItemData> infusedItems = new System.Collections.Generic.List<ItemData>();

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

        // Verifica se tem o item no inventÃ¡rio antes de destruir
        if (inventory.HasItem(itemId, 1))
        {
            // DÃ¡ essÃªncia
            if (essenceWallet != null)
                essenceWallet.AddEssence(data.recycleEssenceValue);
            
            // Remove 1 do inventÃ¡rio
            inventory.RemoveItem(itemId, 1);
            
            Debug.Log($"[INFUSÃƒO] Item Reciclado: {data.itemName} -> +{data.recycleEssenceValue} EssÃªncias");
            return true;
        }

        return false;
    }

    // =====================================================
    // SISTEMA DE INFLAÃ‡ÃƒO (GDD Â§1.3)
    // =====================================================

    /// <summary>
    /// Calcula o custo REAL de infusÃ£o com a inflaÃ§Ã£o acumulada.
    /// FÃ³rmula: C = B Ã— (1,0 + Î± Ã— Ptotal)
    /// </summary>
    public int GetInflatedCost(ItemData data)
    {
        if (data == null) return 0;
        float cost = data.infusionEssenceCost * (1f + inflationAlpha * totalInfusionWeight);
        return Mathf.RoundToInt(cost);
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
        Debug.Log("[INFUSION MANAGER] Peso de inflaÃ§Ã£o e histÃ³rico de infusÃµes resetados para nova Run.");
    }

    /// <summary>
    /// Infunde o item no corpo, ganhando TODOS os atributos permanentemente e consumindo o item do inventÃ¡rio.
    /// O custo Ã© calculado com inflaÃ§Ã£o: C = B Ã— (1 + Î± Ã— Ptotal).
    /// </summary>
    public bool InfuseItem(string itemId)
    {
        if (ItemDatabase.Instance == null) return false;
        
        ItemData data = ItemDatabase.Instance.GetItemData(itemId);
        if (data == null) return false;

        // Regra de Balanceamento (Brotato x Hades): Itens LendÃ¡rios (T4) tÃªm limite de 1 infusÃ£o por run (Max Stacks = 1)
        if (data.tier == ItemTier.Legendary && infusedItems.Contains(data))
        {
            Debug.LogWarning($"[INFUSÃƒO] {data.itemName} Ã© um item LendÃ¡rio (T4) e jÃ¡ foi infundido nesta run! (Limite = 1)");
            return false;
        }

        if (inventory.HasItem(itemId, 1))
        {
            // Calcula custo com inflaÃ§Ã£o acumulada
            int actualCost = GetInflatedCost(data);

            // TENTA PAGAR O CUSTO PRIMEIRO!
            if (essenceWallet != null)
            {
                if (!essenceWallet.SpendEssence(actualCost))
                {
                    Debug.Log($"[INFUSÃƒO] Bloqueado! Custo atual: {actualCost} EssÃªncias (base:{data.infusionEssenceCost} Ã— inflaÃ§Ã£o:{(1f + inflationAlpha * totalInfusionWeight):F2}). VocÃª tem: {essenceWallet.GetEssence()}");
                    return false;
                }
            }

            // Roda o loop em todos os buffs
            foreach (var buff in data.itemAttributes)
            {
                ApplyAttribute(buff);
            }

            // Acumula o peso desta infusÃ£o no Ptotal
            float addedWeight = data.GetTierWeight();
            totalInfusionWeight += addedWeight;
            
            // Registra a infusÃ£o para possÃ­vel cirurgia de remoÃ§Ã£o
            infusedItems.Add(data);

            // Ativa efeito especial T4 (se houver)
            if (data.tier4Effect != Tier4EffectType.None)
            {
                Tier4EffectManager t4Manager = GetComponent<Tier4EffectManager>();
                if (t4Manager != null)
                {
                    t4Manager.ActivateEffect(data.tier4Effect);
                }
                else
                {
                    Debug.LogWarning("[INFUSÃƒO] Item T4 com efeito especial, mas Tier4EffectManager nÃ£o encontrado no Player!");
                }
            }

            // Consome 1 item do inventÃ¡rio
            inventory.RemoveItem(itemId, 1);

            Debug.Log($"[INFUSÃƒO] Sucesso! {data.itemName} | Custo pago: {actualCost} | Ptotal agora: {totalInfusionWeight:F2}");
            return true;
        }
        
        return false;
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
        
        foreach(var id in itemIds)
        {
            ItemData data = ItemDatabase.Instance.GetItemData(id);
            if (data == null) continue;
            if (data.tier == ItemTier.Legendary && infusedItems.Contains(data)) continue; 
            if (!inventory.HasItem(id, 1)) continue;
            
            float costF = data.infusionEssenceCost * (1f + inflationAlpha * currentWeight);
            int cost = UnityEngine.Mathf.RoundToInt(costF);
            
            totalCost += cost;
            currentWeight += data.GetTierWeight();
            validItems.Add(data);
            itemIDsToInfuse.Add(data.itemId);
        }

        if (validItems.Count == 0) return false;

        // CÃ¡lculo da Sinergia
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
        foreach (var data in validItems)
        {
            // Aplica stats base
            foreach (var buff in data.itemAttributes)
            {
                ApplyAttribute(buff);
            }
            
            float addedWeight = data.GetTierWeight();
            totalInfusionWeight += addedWeight;
            infusedItems.Add(data);
            
            if (data.tier4Effect != Tier4EffectType.None)
            {
                Tier4EffectManager t4Manager = GetComponent<Tier4EffectManager>();
                if (t4Manager != null) t4Manager.ActivateEffect(data.tier4Effect);
            }
            
            inventory.RemoveItem(data.itemId, 1);
            success = true;
            Debug.Log($"[INFUSÃO] Sucesso! {data.itemName} infundido via mÃºltipla.");
        }

        // AplicaÃ§Ã£o dos Modificadores Extras
        if (result.extraModifiers != null)
        {
            foreach (var mod in result.extraModifiers)
            {
                ApplySynergyModifier(mod);
            }
        }

        return success;
    }

    private void ApplySynergyModifier(StatModifier mod)
    {
        if (System.Enum.TryParse(mod.statName, out AttributeType attrType))
        {
            if (mod.baseValue != 0f) ApplyAttribute(new ItemAttributeParam { attributeType = attrType, value = mod.baseValue, isMultiplier = false });
            if (mod.multiplierValue != 0f) ApplyAttribute(new ItemAttributeParam { attributeType = attrType, value = mod.multiplierValue, isMultiplier = true });
        }
        else
        {
            Debug.LogWarning($"[SYNERGY] Atributo {mod.statName} invÃ¡lido no StatModifier!");
        }
    }
    public bool InfuseSynergy(string item1Id, string item2Id)
    {
        if (ItemDatabase.Instance == null || inventory == null) return false;

        ItemData data1 = ItemDatabase.Instance.GetItemData(item1Id);
        ItemData data2 = ItemDatabase.Instance.GetItemData(item2Id);

        if (data1 == null || data2 == null) return false;

        int cost1 = GetInflatedCost(data1);
        int cost2 = GetInflatedCost(data2);
        int totalCost = cost1 + cost2;

        if (essenceWallet != null && essenceWallet.GetEssence() < totalCost)
        {
            Debug.Log("[INFUSÃO SINERGIA] Bloqueado! Sem essência suficiente.");
            return false;
        }

        bool s1 = InfuseItem(item1Id);
        bool s2 = InfuseItem(item2Id);

        return s1 && s2;
    }
    public bool RemoveInfusion(ItemData data)
    {
        if (data == null || !infusedItems.Contains(data)) return false;

        // Reverte todos os buffs (sinal negativo para somas, ou inversÃ£o para multiplicadores)
        foreach (var buff in data.itemAttributes)
        {
            RemoveAttribute(buff);
        }

        // Desativa efeito especial T4 (se houver)
        if (data.tier4Effect != Tier4EffectType.None)
        {
            Tier4EffectManager t4Manager = GetComponent<Tier4EffectManager>();
            if (t4Manager != null)
            {
                t4Manager.DeactivateEffect(data.tier4Effect);
            }
        }

        // Subtrai o peso de inflaÃ§Ã£o
        float removedWeight = data.GetTierWeight();
        totalInfusionWeight = Mathf.Max(0f, totalInfusionWeight - removedWeight);

        infusedItems.Remove(data);
        
        Debug.Log($"[REMOÃ‡ÃƒO] Item extraÃ­do: {data.itemName}. Ptotal reduzido para {totalInfusionWeight:F2}");
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
}

