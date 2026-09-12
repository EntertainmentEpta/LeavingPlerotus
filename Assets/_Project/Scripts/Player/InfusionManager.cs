using UnityEngine;

/// <summary>
/// Motor central de Upgrades (Infusão e Reciclagem).
/// Fica no objeto Player (junto com o PlayerInventory e os Status base).
/// 
/// Implementa a fórmula de inflação do GDD (Economy.pdf §1.3):
///   C = B × (1,0 + α × Ptotal)
///   B     = custo base do tier (T1=60, T2=180, T3=300, T4=420)
///   α     = 0,1 (coeficiente de inflação - usando o valor dos exemplos do GDD)
///   Ptotal = soma dos pesos dos itens já infundidos (T1=1, T2=2.25, T3=4, T4=6)
/// </summary>
public class InfusionManager : MonoBehaviour
{
    private PlayerInventory inventory;
    private PlayerAttributesOffensive offensiveStats;
    private PlayerAttributesDefensive defensiveStats;
    private PlayerHealth healthStats;
    private PlayerEssence essenceWallet;

    [Header("Inflação de Infusão (GDD §1.3)")]
    [Tooltip("α = coeficiente de inflação. GDD usa 0,1 conforme os exemplos da tabela.")]
    public float inflationAlpha = 0.1f;

    // Peso total acumulado de todos os itens já infundidos (Ptotal)
    private float totalInfusionWeight = 0f;
    
    // Histórico de itens infundidos
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

        // Diagnóstico para garantir que não falta nada
        if (inventory == null || offensiveStats == null || defensiveStats == null || healthStats == null || essenceWallet == null)
        {
            Debug.LogWarning("[INFUSION MANAGER] Faltando componentes no Player! Verifique se todos os scripts de status estão adicionados no mesmo objeto.");
        }
    }

    /// <summary>
    /// Recicla o item, ganhando a essência configurada no ItemData e removendo o item da mochila.
    /// </summary>
    public bool RecycleItem(string itemId)
    {
        if (ItemDatabase.Instance == null) return false;
        
        ItemData data = ItemDatabase.Instance.GetItemData(itemId);
        if (data == null) return false;

        // Verifica se tem o item no inventário antes de destruir
        if (inventory.HasItem(itemId, 1))
        {
            // Dá essência
            if (essenceWallet != null)
                essenceWallet.AddEssence(data.recycleEssenceValue);
            
            // Remove 1 do inventário
            inventory.RemoveItem(itemId, 1);
            
            Debug.Log($"[INFUSÃO] Item Reciclado: {data.itemName} -> +{data.recycleEssenceValue} Essências");
            return true;
        }

        return false;
    }

    // =====================================================
    // SISTEMA DE INFLAÇÃO (GDD §1.3)
    // =====================================================

    /// <summary>
    /// Calcula o custo REAL de infusão com a inflação acumulada.
    /// Fórmula: C = B × (1,0 + α × Ptotal)
    /// </summary>
    public int GetInflatedCost(ItemData data)
    {
        if (data == null) return 0;
        float cost = data.infusionEssenceCost * (1f + inflationAlpha * totalInfusionWeight);
        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// Retorna o Ptotal atual (peso acumulado de infusões).
    /// Útil para exibir na UI info sobre o estado de inflação.
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
        Debug.Log("[INFUSION MANAGER] Peso de inflação e histórico de infusões resetados para nova Run.");
    }

    /// <summary>
    /// Infunde o item no corpo, ganhando TODOS os atributos permanentemente e consumindo o item do inventário.
    /// O custo é calculado com inflação: C = B × (1 + α × Ptotal).
    /// </summary>
    public bool InfuseItem(string itemId)
    {
        if (ItemDatabase.Instance == null) return false;
        
        ItemData data = ItemDatabase.Instance.GetItemData(itemId);
        if (data == null) return false;

        // Regra de Balanceamento (Brotato x Hades): Itens Lendários (T4) têm limite de 1 infusão por run (Max Stacks = 1)
        if (data.tier == ItemTier.Legendary && infusedItems.Contains(data))
        {
            Debug.LogWarning($"[INFUSÃO] {data.itemName} é um item Lendário (T4) e já foi infundido nesta run! (Limite = 1)");
            return false;
        }

        if (inventory.HasItem(itemId, 1))
        {
            // Calcula custo com inflação acumulada
            int actualCost = GetInflatedCost(data);

            // TENTA PAGAR O CUSTO PRIMEIRO!
            if (essenceWallet != null)
            {
                if (!essenceWallet.SpendEssence(actualCost))
                {
                    Debug.Log($"[INFUSÃO] Bloqueado! Custo atual: {actualCost} Essências (base:{data.infusionEssenceCost} × inflação:{(1f + inflationAlpha * totalInfusionWeight):F2}). Você tem: {essenceWallet.GetEssence()}");
                    return false;
                }
            }

            // Roda o loop em todos os buffs
            foreach (var buff in data.itemAttributes)
            {
                ApplyAttribute(buff);
            }

            // Acumula o peso desta infusão no Ptotal
            float addedWeight = data.GetTierWeight();
            totalInfusionWeight += addedWeight;
            
            // Registra a infusão para possível cirurgia de remoção
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
                    Debug.LogWarning("[INFUSÃO] Item T4 com efeito especial, mas Tier4EffectManager não encontrado no Player!");
                }
            }

            // Consome 1 item do inventário
            inventory.RemoveItem(itemId, 1);

            Debug.Log($"[INFUSÃO] Sucesso! {data.itemName} | Custo pago: {actualCost} | Ptotal agora: {totalInfusionWeight:F2}");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Usado pelo Mercador na "Cirurgia de Remoção".
    /// Remove permanentemente os efeitos de um item e reduz o peso de inflação.
    /// </summary>
    public bool RemoveInfusion(ItemData data)
    {
        if (data == null || !infusedItems.Contains(data)) return false;

        // Reverte todos os buffs (sinal negativo para somas, ou inversão para multiplicadores)
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

        // Subtrai o peso de inflação
        float removedWeight = data.GetTierWeight();
        totalInfusionWeight = Mathf.Max(0f, totalInfusionWeight - removedWeight);

        infusedItems.Remove(data);
        
        Debug.Log($"[REMOÇÃO] Item extraído: {data.itemName}. Ptotal reduzido para {totalInfusionWeight:F2}");
        return true;
    }

    /// <summary>
    /// Roteador: Descobre de quem é esse atributo e manda pro script correto
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
        // Para inverter o multiplicador, mandamos 1f / buff.value (com verificação contra divisão por zero)
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
