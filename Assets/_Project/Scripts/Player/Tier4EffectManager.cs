using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerenciador central de efeitos especiais de itens Tier 4 (Lendários).
/// Fica no GameObject do Player (junto com InfusionManager, PlayerInventory, etc.)
/// 
/// Responsabilidades:
///   - Ativar/desativar efeitos T4 quando itens lendários são infundidos/removidos
///   - Manter registro de quais efeitos estão ativos
///   - Delegar para os scripts especializados de cada efeito
/// 
/// Uso:
///   1. Adicione este componente ao Player
///   2. O InfusionManager chama ActivateEffect/DeactivateEffect automaticamente
///   3. Cada efeito T4 tem seu próprio script (Ex: ExplosiveDashEffect) que é
///      ativado/desativado por este gerenciador
/// </summary>
public class Tier4EffectManager : MonoBehaviour
{
    [Header("Efeitos T4 Registrados")]
    [Tooltip("Referência ao ExplosiveDashEffect (pode estar no mesmo GameObject ou em filho).")]
    public ExplosiveDashEffect explosiveDashEffect;

    [Header("Poison (T4) - Config")]
    [Tooltip("Dano por tick do veneno aplicado nos inimigos atingidos.")]
    public int poisonDamagePerTick = 5;

    [Tooltip("Duração do veneno em segundos (reaplicar no mesmo inimigo restaura o timer).")]
    public float poisonDuration = 5f;

    [Tooltip("Intervalo entre ticks de dano do veneno (segundos).")]
    public float poisonTickInterval = 1f;

    [Header("SharpBlur T4 - Sangramento")]
    public int sharpBlurBleedDamagePerTick = 10;
    public float sharpBlurBleedDuration = 3f;

    [Header("Tiro Teleguiado")]
    [Min(0.1f)] public float homingDetectionRadius = 5f;
    [Min(1f)] public float homingTurnSpeed = 720f;
    [Min(0.02f)] public float homingTargetRefreshInterval = 0.1f;
    public Color homingBatteryOrbTint = new Color(126f / 255f, 61f / 255f, 202f / 255f, 1f);

    [Header("Crystal Tuner T4 - Marca")]
    [Tooltip("Prefab visual instanciado sobre o inimigo marcado. Se vazio, usa a marca procedural.")]
    public GameObject tunerMarkVFXPrefab;
    [Min(3f)]
    public float tunerMarkDuration = 4f;
    [Min(3f)]
    public float tunerBossMarkDuration = 5f;
    [Min(2f)]
    public float tunerCooldownAfterMark = 4f;
    [Min(6f)]
    public float tunerBossCooldownAfterMark = 8f;
    [Range(0f, 1f)]
    public float tunerTriggerDamagePercent = 0.30f;
    [Range(0f, 1f)]
    public float tunerBossTriggerDamagePercent = 0.10f;

    private PlayerHealth playerHealth;
    private float defaultShieldRegenDelay;
    private const float TotemDashShieldRestorePercent = 0.20f;
    private const float MagicCatalystRoomHealPercent = 0.20f;
    public float reflectedDamageStunDuration = 1f;
    private const int ShardSwarmCrystalDamage = 5;
    private const int ShardSwarmCrystalCount = 2;
    private const float SentinelLifestealPercent = 0.05f;
    private float pendingSentinelLifesteal;
    private const float BismutadoAreaDamagePercent = 0.20f;
    private const float BismutadoAreaRadius = 3.5f;
    private GameObject tunerMarkedTarget;
    private TunerMarkVFX tunerMarkVFX;
    private bool tunerMarkedTargetIsBoss;
    private float tunerMarkExpiresAt;
    private float tunerCooldownUntil;

    // Registro de efeitos ativos para consulta rápida
    private HashSet<Tier4EffectType> activeEffects = new HashSet<Tier4EffectType>();

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = GetComponentInChildren<PlayerHealth>();
        if (playerHealth != null)
            defaultShieldRegenDelay = playerHealth.shieldRegenDelay;

        // Auto-detectar referências se não foram atribuídas no Inspector
        if (explosiveDashEffect == null)
        {
            explosiveDashEffect = GetComponent<ExplosiveDashEffect>() 
                ?? GetComponentInChildren<ExplosiveDashEffect>();
        }
    }

    private void OnEnable()
    {
        RoomController.OnRoomCleared += OnRoomCleared;
    }

    private void OnDisable()
    {
        RoomController.OnRoomCleared -= OnRoomCleared;
        ClearTunerMark();
    }

    private void Update()
    {
        if (!activeEffects.Contains(Tier4EffectType.Tuner_T4Effect)) return;

        if (tunerMarkedTarget != null && Time.time >= tunerMarkExpiresAt)
        {
            bool wasBoss = tunerMarkedTargetIsBoss;
            ClearTunerMark();
            tunerCooldownUntil = Time.time + GetTunerCooldown(wasBoss);
        }

        if (tunerMarkedTarget == null && Time.time >= tunerCooldownUntil)
            MarkRandomEnemy();
    }

    /// <summary>
    /// Ativa um efeito especial T4. Chamado pelo InfusionManager ao infundir um item lendário.
    /// </summary>
    public void ActivateEffect(Tier4EffectType effectType)
    {
        if (effectType == Tier4EffectType.None) return;
        if (activeEffects.Contains(effectType))
        {
            Debug.LogWarning($"[Tier4EffectManager] Efeito {effectType} já está ativo!");
            return;
        }

        switch (effectType)
        {
            case Tier4EffectType.ExplosiveDash:
                if (explosiveDashEffect == null)
                {
                    explosiveDashEffect = GetComponent<ExplosiveDashEffect>() 
                        ?? GetComponentInChildren<ExplosiveDashEffect>()
                        ?? gameObject.AddComponent<ExplosiveDashEffect>();
                }
                if (explosiveDashEffect != null)
                {
                    explosiveDashEffect.ActivateEffect();
                    activeEffects.Add(effectType);
                    Debug.Log($"💎 [T4 EFFECT] {effectType} ATIVADO!");
                }
                break;

            case Tier4EffectType.Poison:
                // Efeito passivo: não precisa de componente próprio, os ataques consultam HasEffect()
                activeEffects.Add(effectType);
                Debug.Log($"💎 [T4 EFFECT] {effectType} ATIVADO!");
                break;

            case Tier4EffectType.HomingProjectiles:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.SharpBlurT4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.Cristalus_T4Effect:
                if (playerHealth == null)
                    playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInChildren<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.shieldRegenDelay = 0f;
                    activeEffects.Add(effectType);
                    Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                }
                else
                {
                    Debug.LogWarning("[Tier4EffectManager] Cristalus_T4Effect requer PlayerHealth no Player.");
                }
                break;

            case Tier4EffectType.Totem_T4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.MagicCatalyst_T4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.CrystalDragon_T4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.ShardSwarm_T4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.Sentinel_T4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.Bismutado_T4Effect:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.Tuner_T4Effect:
                activeEffects.Add(effectType);
                tunerCooldownUntil = Time.time;
                MarkRandomEnemy();
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            case Tier4EffectType.Golem_T4Effect:
            case Tier4EffectType.Boomerang_ElectricCrystal:
            case Tier4EffectType.Boomerang_TranslucentClones:
            case Tier4EffectType.Boomerang_BounceEcho:
            case Tier4EffectType.Boomerang_ShieldParalysis:
                activeEffects.Add(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} ATIVADO!");
                break;

            default:
                Debug.LogWarning($"[Tier4EffectManager] Efeito T4 '{effectType}' não possui handler implementado.");
                break;
        }
    }

    /// <summary>
    /// Desativa um efeito especial T4. Chamado pelo InfusionManager ao remover infusão de item lendário.
    /// </summary>
    public void DeactivateEffect(Tier4EffectType effectType)
    {
        if (effectType == Tier4EffectType.None) return;
        if (!activeEffects.Contains(effectType))
        {
            Debug.LogWarning($"[Tier4EffectManager] Tentativa de desativar efeito {effectType} que não está ativo.");
            return;
        }

        switch (effectType)
        {
            case Tier4EffectType.ExplosiveDash:
                if (explosiveDashEffect != null)
                {
                    explosiveDashEffect.DeactivateEffect();
                    activeEffects.Remove(effectType);
                    Debug.Log($"💎 [T4 EFFECT] {effectType} DESATIVADO!");
                }
                break;

            case Tier4EffectType.Poison:
                activeEffects.Remove(effectType);
                Debug.Log($"💎 [T4 EFFECT] {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.HomingProjectiles:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.SharpBlurT4Effect:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.Cristalus_T4Effect:
                if (playerHealth != null)
                    playerHealth.shieldRegenDelay = defaultShieldRegenDelay;
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.Totem_T4Effect:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.MagicCatalyst_T4Effect:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.CrystalDragon_T4Effect:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.ShardSwarm_T4Effect:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.Sentinel_T4Effect:
                pendingSentinelLifesteal = 0f;
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.Bismutado_T4Effect:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.Tuner_T4Effect:
                ClearTunerMark();
                tunerCooldownUntil = 0f;
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;

            case Tier4EffectType.Golem_T4Effect:
            case Tier4EffectType.Boomerang_ElectricCrystal:
            case Tier4EffectType.Boomerang_TranslucentClones:
            case Tier4EffectType.Boomerang_BounceEcho:
            case Tier4EffectType.Boomerang_ShieldParalysis:
                activeEffects.Remove(effectType);
                Debug.Log($"[Tier4EffectManager] Efeito {effectType} DESATIVADO!");
                break;
        }
    }

    private void OnRoomCleared(RoomController room)
    {
        if (!activeEffects.Contains(Tier4EffectType.MagicCatalyst_T4Effect)) return;
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInChildren<PlayerHealth>();
        if (playerHealth == null || playerHealth.maxHealth <= 0) return;

        int restoredHealth = Mathf.Max(1, Mathf.CeilToInt(playerHealth.maxHealth * MagicCatalystRoomHealPercent));
        playerHealth.Heal(restoredHealth);
        Debug.Log($"[Tier4EffectManager] {Tier4EffectType.MagicCatalyst_T4Effect}: +{restoredHealth} vida ao completar a sala.");
    }

    public void OnDashUsed()
    {
        if (!activeEffects.Contains(Tier4EffectType.Totem_T4Effect)) return;
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInChildren<PlayerHealth>();
        if (playerHealth == null || playerHealth.maxArmor <= 0) return;

        int restoredShield = Mathf.Max(1, Mathf.CeilToInt(playerHealth.maxArmor * TotemDashShieldRestorePercent));
        playerHealth.RestoreArmor(restoredShield);
        Debug.Log($"[Tier4EffectManager] {Tier4EffectType.Totem_T4Effect}: +{restoredShield} escudo ao usar Dash.");
    }

    public void OnDamageReflected(GameObject attacker)
    {
        if (!activeEffects.Contains(Tier4EffectType.CrystalDragon_T4Effect) || attacker == null) return;
        DummyHealth dummy = attacker.GetComponent<DummyHealth>() ?? attacker.GetComponentInParent<DummyHealth>();
        if (dummy != null && dummy.CurrentHealth > 0)
        {
            dummy.ApplyStun(reflectedDamageStunDuration);
            AreaDamageVFX.Spawn(dummy.transform.position, 1.4f);
            Debug.Log($"[Tier4EffectManager] {Tier4EffectType.CrystalDragon_T4Effect}: {dummy.name} atordoado por {reflectedDamageStunDuration}s ao refletir dano!");
        }
    }

    public void OnPerfectDodge()
    {
        if (!activeEffects.Contains(Tier4EffectType.Golem_T4Effect)) return;
        const float stunRadius = 4.5f;
        const float stunDuration = 1.5f;
        HashSet<DummyHealth> stunned = new HashSet<DummyHealth>();
        foreach (Collider col in Physics.OverlapSphere(transform.position, stunRadius))
        {
            DummyHealth dummy = col.GetComponent<DummyHealth>() ?? col.GetComponentInParent<DummyHealth>();
            if (dummy != null && dummy.CurrentHealth > 0 && stunned.Add(dummy))
            {
                dummy.ApplyStun(stunDuration);
            }
        }
        AreaDamageVFX.Spawn(transform.position, stunRadius);
        Debug.Log($"[Tier4EffectManager] {Tier4EffectType.Golem_T4Effect}: {stunned.Count} inimigos atordoados na Esquiva Perfeita!");
    }

    /// <summary>
    /// Consulta se um efeito T4 específico está ativo.
    /// </summary>
    public bool HasEffect(Tier4EffectType effectType)
    {
        return activeEffects.Contains(effectType);
    }

    /// <summary>
    /// Aplica os efeitos T4 "on-hit" ativos no inimigo atingido por um ataque do player.
    /// Chamado pelos scripts de ataque (PrimaryAttackKnife, GunProjectile...) após causar dano.
    /// </summary>
    public void ApplyOnHitEffects(GameObject enemy, bool isCritical = false)
    {
        if (enemy == null) return;

        if (activeEffects.Contains(Tier4EffectType.Poison))
        {
            PoisonedEnemyStatus.ApplyPoison(enemy, poisonDamagePerTick, poisonDuration, poisonTickInterval);
        }

        if (isCritical && activeEffects.Contains(Tier4EffectType.SharpBlurT4Effect))
        {
            BleedingEnemyStatus.ApplyBleed(
                enemy,
                sharpBlurBleedDamagePerTick,
                sharpBlurBleedDuration);
        }

        if (activeEffects.Contains(Tier4EffectType.ShardSwarm_T4Effect))
        {
            for (int i = 0; i < ShardSwarmCrystalCount; i++)
            {
                float side = (i == 0) ? -1f : 1f;
                Vector3 spawnPos = transform.position + Vector3.up * 1.15f + transform.right * (side * 0.45f);
                Vector3 initialArcDir = (transform.forward * 0.35f + transform.right * (side * 0.85f) + Vector3.up * 0.55f).normalized;
                GuidedCrystalProjectile.Spawn(
                    spawnPos,
                    transform.root,
                    ShardSwarmCrystalDamage,
                    initialArcDir,
                    enemy != null ? enemy.transform : null);
            }
        }
    }

    public void ApplyDamageDealtEffects(int damage)
    {
        if (damage <= 0 || !activeEffects.Contains(Tier4EffectType.Sentinel_T4Effect)) return;
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInChildren<PlayerHealth>();
        if (playerHealth == null) return;

        pendingSentinelLifesteal += damage * SentinelLifestealPercent;
        int restoredHealth = Mathf.FloorToInt(pendingSentinelLifesteal);
        if (restoredHealth <= 0) return;

        pendingSentinelLifesteal -= restoredHealth;
        playerHealth.Heal(restoredHealth);
        Debug.Log($"[Tier4EffectManager] {Tier4EffectType.Sentinel_T4Effect}: +{restoredHealth} vida por {damage} dano causado.");
    }

    public void ApplyAreaDamageEffects(GameObject primaryTarget, int primaryDamage)
    {
        if (!activeEffects.Contains(Tier4EffectType.Bismutado_T4Effect)) return;
        if (primaryTarget == null || primaryDamage <= 0) return;

        int areaDamage = Mathf.Max(1, Mathf.RoundToInt(primaryDamage * BismutadoAreaDamagePercent));
        Vector3 impactPosition = primaryTarget.transform.position;
        HashSet<DummyHealth> damagedDummies = new HashSet<DummyHealth>();
        HashSet<ShardSwarmHealth> damagedSwarms = new HashSet<ShardSwarmHealth>();

        foreach (Collider hit in Physics.OverlapSphere(impactPosition, BismutadoAreaRadius))
        {
            DummyHealth dummy = hit.GetComponent<DummyHealth>() ?? hit.GetComponentInParent<DummyHealth>();
            if (dummy != null && dummy.gameObject != primaryTarget && damagedDummies.Add(dummy))
                dummy.TakeDamage(areaDamage);

            ShardSwarmHealth swarm = hit.GetComponent<ShardSwarmHealth>() ?? hit.GetComponentInParent<ShardSwarmHealth>();
            if (swarm != null && swarm.gameObject != primaryTarget && damagedSwarms.Add(swarm))
                swarm.TakeDamage(areaDamage);
        }

        AreaDamageVFX.Spawn(impactPosition, BismutadoAreaRadius);
    }

    public int ModifyIncomingDamage(GameObject target, int damage)
    {
        if (damage <= 0 || !IsTunerMarked(target)) return damage;

        if (!TryGetTargetHealth(target, out int currentHealth, out int maxHealth))
            return damage;

        int halfHealth = Mathf.CeilToInt(maxHealth * 0.5f);
        bool crossesHalfHealth = currentHealth <= halfHealth ||
                                 currentHealth - damage <= halfHealth;
        if (!crossesHalfHealth) return damage;

        bool isBoss = IsBossTarget(target);
        float bonus = isBoss ? tunerBossTriggerDamagePercent : tunerTriggerDamagePercent;
        ConsumeTunerMark(isBoss);
        int amplifiedDamage = Mathf.RoundToInt(damage * (1f + bonus));
        Debug.Log($"[Tier4EffectManager] Marca do Tuner consumida em {target.name}: " +
                  $"+{bonus:P0} dano explosivo{(isBoss ? " de Boss" : "")}.");
        return Mathf.Max(1, amplifiedDamage);
    }

    public void NotifyEnemyDamaged(GameObject target, bool targetDied)
    {
        if (!IsTunerMarked(target) || !targetDied) return;

        bool wasBoss = tunerMarkedTargetIsBoss;
        ClearTunerMark(true);
        tunerCooldownUntil = Time.time + GetTunerCooldown(wasBoss);
    }

    private bool IsTunerMarked(GameObject target)
    {
        if (target == null || tunerMarkedTarget == null) return false;
        return target == tunerMarkedTarget || target.transform.root == tunerMarkedTarget.transform.root;
    }

    private void MarkRandomEnemy()
    {
        List<GameObject> candidates = new List<GameObject>();
        foreach (DummyHealth enemy in FindObjectsByType<DummyHealth>(FindObjectsSortMode.None))
        {
            if (enemy != null && enemy.CurrentHealth > 0)
                candidates.Add(enemy.gameObject);
        }

        foreach (ShardSwarmHealth enemy in FindObjectsByType<ShardSwarmHealth>(FindObjectsSortMode.None))
        {
            if (enemy != null && enemy.CurrentHealth > 0)
                candidates.Add(enemy.gameObject);
        }

        if (candidates.Count == 0) return;

        tunerMarkedTarget = candidates[Random.Range(0, candidates.Count)];
        tunerMarkedTargetIsBoss = IsBossTarget(tunerMarkedTarget);
        float markDuration = tunerMarkedTargetIsBoss ? tunerBossMarkDuration : tunerMarkDuration;
        tunerMarkExpiresAt = Time.time + markDuration;
        tunerMarkVFX = TunerMarkVFX.AttachMark(tunerMarkedTarget, markDuration, tunerMarkVFXPrefab);
        Debug.Log($"[Tier4EffectManager] Tuner marcou {tunerMarkedTarget.name} por {markDuration:0.0}s" +
                  $"{(tunerMarkedTargetIsBoss ? " (Boss)" : "")}.");
    }

    private void ClearTunerMark(bool triggerShatter = false)
    {
        if (tunerMarkVFX != null)
        {
            if (triggerShatter)
                tunerMarkVFX.TriggerShatter();
            else
                tunerMarkVFX.Expire();
        }
        tunerMarkVFX = null;
        tunerMarkedTarget = null;
        tunerMarkedTargetIsBoss = false;
        tunerMarkExpiresAt = 0f;
    }

    private void ConsumeTunerMark(bool isBoss)
    {
        ClearTunerMark(true);
        tunerCooldownUntil = Time.time + GetTunerCooldown(isBoss);
    }

    private float GetTunerCooldown(bool isBoss)
    {
        return isBoss ? tunerBossCooldownAfterMark : tunerCooldownAfterMark;
    }

    private bool IsBossTarget(GameObject target)
    {
        return target != null &&
               (target.GetComponent<BossController>() != null ||
                target.GetComponentInParent<BossController>() != null);
    }

    private bool TryGetTargetHealth(GameObject target, out int currentHealth, out int maxHealth)
    {
        DummyHealth dummyHealth = target.GetComponent<DummyHealth>() ??
                                  target.GetComponentInParent<DummyHealth>();
        if (dummyHealth != null)
        {
            currentHealth = dummyHealth.CurrentHealth;
            maxHealth = dummyHealth.maxHealth;
            return maxHealth > 0;
        }

        ShardSwarmHealth swarmHealth = target.GetComponent<ShardSwarmHealth>() ??
                                       target.GetComponentInParent<ShardSwarmHealth>();
        if (swarmHealth != null)
        {
            currentHealth = swarmHealth.CurrentHealth;
            maxHealth = swarmHealth.maxHealth;
            return maxHealth > 0;
        }

        currentHealth = 0;
        maxHealth = 0;
        return false;
    }

    /// <summary>
    /// Retorna todos os efeitos T4 ativos.
    /// </summary>
    public HashSet<Tier4EffectType> GetActiveEffects()
    {
        return activeEffects;
    }

    /// <summary>
    /// Desativa todos os efeitos T4 ativos. Útil para resetar no início de uma nova run.
    /// </summary>
    public void DeactivateAllEffects()
    {
        // Cria cópia para iterar sem modificar a coleção
        var effectsCopy = new List<Tier4EffectType>(activeEffects);
        foreach (var effect in effectsCopy)
        {
            DeactivateEffect(effect);
        }
        Debug.Log("[Tier4EffectManager] Todos os efeitos T4 desativados.");
    }
}
