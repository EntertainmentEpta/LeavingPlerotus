using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Sistema de vida exclusivo do Shard Swarm.
/// Cópia do DummyHealth com método SetHealth() para permitir divisão de HP no Split.
/// </summary>
public class ShardSwarmHealth : MonoBehaviour
{
    [Header("Debuffs (Slow)")]
    [HideInInspector] public bool isSlowed = false;
    private float slowPercent = 0f;
    private float slowTimer = 0f;

    private Geobionte_AI geobionteAI;
    private Golem_AI golemAI;
    private Spider_AI spiderAI;
    private GoblinAI_Transform goblinAI;
    private ShardSwarm_AI shardSwarmAI;
    private MagicStone_AI magicStoneAI;
    private CrystalTuner crystalTunerAI;
    private Cristalus_AI cristalusAI;

    [Header("Vida")]
    public int maxHealth = 100;
    public int CurrentHealth { get; private set; }

    [Header("Referências UI")]
    public Slider healthBarSlider;
    private Image healthBarFill;

    [Header("Cores da Barra")]
    public Color normalColor = Color.red;
    [Tooltip("Cor da barra quando está sendo protegido pelo Sintonizador.")]
    public Color buffedColor = Color.cyan;

    [Header("Feedback de Dano")]
    public GameObject floatingDamageTextPrefab;
    public Vector3 textOffset = new Vector3(0, 2f, 0);
    public Color hitColor = Color.red;
    public float hitFlashTime = 0.2f;

    [HideInInspector] public bool isInvulnerable = false;
    [HideInInspector] public bool isBuffed = false;

    /// <summary>
    /// Se definido, substitui a lógica padrão de morte (drops + Destroy).
    /// Usado pelo ShardSwarm_AI para controlar o split de gerações.
    /// </summary>
    [HideInInspector] public System.Action onDeathOverride = null;

    private Color originalRenderColor;
    private Color originalBaseColor;
    private bool hasBaseColor = false;
    private Renderer dummyRenderer;

    private void Start()
    {
        CurrentHealth = maxHealth;

        dummyRenderer = GetComponentInChildren<Renderer>();
        if (dummyRenderer != null)
        {
            originalRenderColor = dummyRenderer.material.color;
            if (dummyRenderer.material.HasProperty("_BaseColor"))
            {
                hasBaseColor = true;
                originalBaseColor = dummyRenderer.material.GetColor("_BaseColor");
            }
        }

        if (healthBarSlider == null)
        {
            healthBarSlider = GetComponentInChildren<Slider>(true);

            if (healthBarSlider == null)
                Debug.LogWarning("[" + gameObject.name + "] Slider de vida não encontrado!");
            else
                Debug.Log("[" + gameObject.name + "] Slider encontrado: " + healthBarSlider.gameObject.name);
        }

        if (healthBarSlider != null)
        {
            if (healthBarSlider.fillRect != null)
            {
                healthBarFill = healthBarSlider.fillRect.GetComponent<Image>();
            }
            if (healthBarFill != null) healthBarFill.color = normalColor;
            healthBarSlider.gameObject.SetActive(false);

            // Garante que o Canvas da barra de vida encare a câmera fixo sem girar
            Canvas parentCanvas = healthBarSlider.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.GetComponent<FaceCamera>() == null)
            {
                parentCanvas.gameObject.AddComponent<FaceCamera>();
            }
        }

        UpdateHealthBar();
        DetectAIScripts();
    }

    private void DetectAIScripts()
    {
        geobionteAI = GetComponent<Geobionte_AI>();
        golemAI = GetComponent<Golem_AI>();
        spiderAI = GetComponent<Spider_AI>();
        goblinAI = GetComponent<GoblinAI_Transform>();
        shardSwarmAI = GetComponent<ShardSwarm_AI>();
        magicStoneAI = GetComponent<MagicStone_AI>();
        crystalTunerAI = GetComponent<CrystalTuner>();
        cristalusAI = GetComponent<Cristalus_AI>();
    }

    public void SetBuffedStatus(bool buffed)
    {
        isBuffed = buffed;
        if (healthBarFill != null)
        {
            healthBarFill.color = buffed ? buffedColor : normalColor;
        }
    }

    /// <summary>
    /// Define o HP diretamente (usado pelo Split do ShardSwarm).
    /// </summary>
    public void SetHealth(int hp)
    {
        CurrentHealth = Mathf.Clamp(hp, 0, maxHealth);
        UpdateHealthBar();

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(int damage, bool isCritical = false)
    {
        if (isInvulnerable) return;
        if (CurrentHealth <= 0) return;

        if (shardSwarmAI == null)
        {
            shardSwarmAI = GetComponent<ShardSwarm_AI>() ?? GetComponentInChildren<ShardSwarm_AI>() ?? GetComponentInParent<ShardSwarm_AI>();
        }

        if (shardSwarmAI != null)
        {
            float mult = shardSwarmAI.GetCurrentDamageMultiplier();
            damage = Mathf.RoundToInt(damage * mult);
            if (mult > 1.0f)
            {
                isCritical = true; // Exibe Dano Crítico ao acertar o Núcleo EXPOSTO!
            }
            else
            {
                // Dispara a ativação do holograma 'Escudo' ao receber dano no modo protegido!
                shardSwarmAI.FlashShieldVisual();
            }
        }
        else if (isBuffed)
        {
            damage = Mathf.RoundToInt(damage * 0.5f);
        }

        CurrentHealth -= damage;

        UpdateHealthBar();
        ShowHealthBar();

        if (floatingDamageTextPrefab != null)
        {
            GameObject textObject = Instantiate(floatingDamageTextPrefab, transform.position + textOffset, Quaternion.identity);
            FloatingDamageText dmgScript = textObject.GetComponent<FloatingDamageText>();
            if (dmgScript != null)
            {
                dmgScript.SetDamage(damage, isCritical);
            }
        }

        if (dummyRenderer != null)
        {
            StartCoroutine(FlashRed());
        }

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            Die();
        }
    }

    void ShowHealthBar()
    {
        if (healthBarSlider == null) return;
        healthBarSlider.gameObject.SetActive(true);
    }

    void UpdateHealthBar()
    {
        if (healthBarSlider != null)
        {
            float healthPercent = (float)CurrentHealth / maxHealth;
            healthBarSlider.value = healthPercent;
            if (CurrentHealth <= 0) healthBarSlider.gameObject.SetActive(false);
        }
    }

    IEnumerator FlashRed()
    {
        dummyRenderer.material.color = hitColor;
        if (hasBaseColor) dummyRenderer.material.SetColor("_BaseColor", hitColor);
        yield return new WaitForSeconds(hitFlashTime);
        dummyRenderer.material.color = originalRenderColor;
        if (hasBaseColor) dummyRenderer.material.SetColor("_BaseColor", originalBaseColor);
    }

    private void Die()
    {
        if (shardSwarmAI != null)
        {
            shardSwarmAI.DestroyAllSpikes();
        }

        // Se um override foi definido (ex: ShardSwarm_AI controla o split),
        // chama o callback e retorna sem executar a lógica padrão.
        if (onDeathOverride != null)
        {
            onDeathOverride.Invoke();
            return;
        }

        Debug.Log(gameObject.name + " foi destruído.");

        EnemyDrops drops = GetComponent<EnemyDrops>()
                        ?? GetComponentInChildren<EnemyDrops>()
                        ?? GetComponentInParent<EnemyDrops>();
        if (drops != null)
        {
            Debug.Log("[DROPS] EnemyDrops encontrado, chamando OnDeath()...");
            drops.OnDeath();
        }

        Destroy(gameObject);
    }

    private void Update()
    {
        if (isSlowed)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                RemoveSlow();
            }
        }
    }

    public void ApplySlow(float percent, float duration)
    {
        if (percent <= 0f) return;

        if (!isSlowed)
        {
            isSlowed = true;
            slowPercent = percent;
            slowTimer = duration;
            ApplySlowToAI(percent);
            Debug.Log($"[DEBUFF] Slow de {percent * 100}% aplicado a {gameObject.name} por {duration}s.");
        }
        else
        {
            // Atualiza tempo de duração (pega o maior)
            slowTimer = Mathf.Max(slowTimer, duration);
            
            // Se o novo slow for mais forte, aplica a diferença
            if (percent > slowPercent)
            {
                RemoveSlowFromAI(); // Reverte o antigo
                slowPercent = percent;
                ApplySlowToAI(percent); // Aplica o novo mais forte
            }
        }
    }

    public void RemoveSlow()
    {
        if (!isSlowed) return;

        RemoveSlowFromAI();
        isSlowed = false;
        slowPercent = 0f;
        slowTimer = 0f;
        Debug.Log($"[DEBUFF] Slow removido de {gameObject.name}. Velocidade restaurada.");
    }

    private void ApplySlowToAI(float percent)
    {
        float factor = 1f - percent;
        if (geobionteAI != null) { geobionteAI.chaseSpeed *= factor; geobionteAI.seekSpeed *= factor; geobionteAI.wanderSpeed *= factor; }
        if (golemAI != null) { golemAI.moveSpeed *= factor; }
        if (spiderAI != null) { spiderAI.moveSpeed *= factor; }
        if (goblinAI != null) { goblinAI.chaseSpeed *= factor; goblinAI.fleeSpeed *= factor; goblinAI.strafeSpeed *= factor; }
        if (shardSwarmAI != null) { shardSwarmAI.moveSpeed *= factor; }
        if (magicStoneAI != null) { magicStoneAI.moveSpeed *= factor; }
        if (crystalTunerAI != null) { crystalTunerAI.moveSpeed *= factor; }
        if (cristalusAI != null) { cristalusAI.moveSpeed *= factor; }
    }

    private void RemoveSlowFromAI()
    {
        if (slowPercent >= 1f) return; // Evita divisão por zero
        float factor = 1f / (1f - slowPercent);
        if (geobionteAI != null) { geobionteAI.chaseSpeed *= factor; geobionteAI.seekSpeed *= factor; geobionteAI.wanderSpeed *= factor; }
        if (golemAI != null) { golemAI.moveSpeed *= factor; }
        if (spiderAI != null) { spiderAI.moveSpeed *= factor; }
        if (goblinAI != null) { goblinAI.chaseSpeed *= factor; goblinAI.fleeSpeed *= factor; goblinAI.strafeSpeed *= factor; }
        if (shardSwarmAI != null) { shardSwarmAI.moveSpeed *= factor; }
        if (magicStoneAI != null) { magicStoneAI.moveSpeed *= factor; }
        if (crystalTunerAI != null) { crystalTunerAI.moveSpeed *= factor; }
        if (cristalusAI != null) { cristalusAI.moveSpeed *= factor; }
    }
}
