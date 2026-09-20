using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DummyHealth : MonoBehaviour
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
    [Tooltip("Ativa logs detalhados de dano/morte para depuração em runtime.")]
    public bool debugLogDamage = false;
    public int CurrentHealth { get; private set; }

    [Header("Referências UI")]
    public Slider healthBarSlider;
    // A imagem que realmente tem a cor (dentro do Slider)
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
    /// Se > 0, cada hit causa exatamente este valor de dano, independente do dano da arma.
    /// Usado pelo Geobionte para que 7 hits = 7 HP (1 dano por hit).
    /// Valor padrão 0 = comportamento normal (usa o dano real da arma).
    /// </summary>
    [HideInInspector] public int fixedDamageOverride = 0;

    /// <summary>
    /// Se definido, chama este callback ao invés da lógica padrão de morte (drops + Destroy).
    /// Usado pelo Geobionte para substituir morte por fuga.
    /// Não afeta nenhum inimigo que não defina esse campo (null por padrão).
    /// </summary>
    [HideInInspector] public System.Action onDeathOverride = null;

    /// <summary>
    /// Evento disparado sempre que este objeto recebe dano de um ataque.
    /// </summary>
    [HideInInspector] public System.Action<int, GameObject> onDamageTaken = null;

    private Color originalRenderColor;
    private Color originalBaseColor;
    private bool hasBaseColor = false;
    private Renderer dummyRenderer;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        // Aplica escalonamento de HP por sala (+3% por sala avançada conforme Hp_Dano_Inimigos.pdf)
        if (RunManager.instance != null && RunManager.instance.currentLevel > 1)
        {
            int roomIndex = RunManager.instance.currentLevel;
            float hpScalingMultiplier = 1f + (0.03f * (roomIndex - 1));
            maxHealth = Mathf.RoundToInt(maxHealth * hpScalingMultiplier);
            CurrentHealth = maxHealth;
        }

        // Se estivermos no modo Endless e o level for maior que 3, aumenta a vida máxima do inimigo
        if (RunManager.instance != null && RunManager.instance.isEndlessMode && RunManager.instance.currentLevel > 3)
        {
            float hpMultiplier = 1f + (RunManager.instance.currentLevel - 3) * 0.15f; // +15% HP por level acima do 3
            maxHealth = Mathf.RoundToInt(maxHealth * hpMultiplier);
            CurrentHealth = maxHealth;
        }

        dummyRenderer = GetComponentInChildren<Renderer>();
        if (dummyRenderer != null)
        {
            originalRenderColor = dummyRenderer.material.color;
            // Suporte a shaders URP/HDRP que usam _BaseColor em vez de _Color
            if (dummyRenderer.material.HasProperty("_BaseColor"))
            {
                hasBaseColor = true;
                originalBaseColor = dummyRenderer.material.GetColor("_BaseColor");
            }
        }

        // Auto-busca o Slider nos filhos se não foi atribuído no Inspector
        if (healthBarSlider == null)
        {
            healthBarSlider = GetComponentInChildren<Slider>(true); // true = inclui desativados

            if (healthBarSlider == null)
                Debug.LogWarning("[" + gameObject.name + "] Slider de vida não encontrado! Crie um Canvas (World Space) com um Slider filho.");
            else
                Debug.Log("[" + gameObject.name + "] Slider encontrado automaticamente: " + healthBarSlider.gameObject.name);
        }

        // Busca a imagem Fill dentro do Slider para controlar a cor
        if (healthBarSlider != null)
        {
            if (healthBarSlider.fillRect != null)
            {
                healthBarFill = healthBarSlider.fillRect.GetComponent<Image>();
            }
            // Garante a cor inicial
            if (healthBarFill != null) healthBarFill.color = normalColor;

            // Começa escondida — só aparece após o primeiro hit
            healthBarSlider.gameObject.SetActive(false);
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

    // --- NOVA FUNÇÃO CHAMADA PELO SINTONIZADOR ---
    public void SetBuffedStatus(bool buffed)
    {
        isBuffed = buffed;

        // Muda a cor da barra
        if (healthBarFill != null)
        {
            healthBarFill.color = buffed ? buffedColor : normalColor;
        }
    }

    /// <summary>
    /// Reseta o HP para o valor máximo. Usado pelo Geobionte no respawn.
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void TakeDamage(int damage, bool isCritical = false)
    {
        if (isInvulnerable) return;
        if (CurrentHealth <= 0) return;

        Debug.Log($"💥 [DAMAGE LOG] {gameObject.name} recebeu {damage} de dano! (HP antes: {CurrentHealth}/{maxHealth} -> HP restante: {Mathf.Max(0, CurrentHealth - damage)})");

        // Se fixedDamageOverride está ativo, cada hit causa exatamente esse valor
        if (fixedDamageOverride > 0)
            damage = fixedDamageOverride;

        // Se estiver buffado, recebe dano reduzido (ex: metade)
        if (isBuffed) damage = Mathf.RoundToInt(damage * 0.5f);

        CurrentHealth -= damage;
        RunStatsManager.Instance?.RecordDamageDealt(damage);

        onDamageTaken?.Invoke(damage, null);

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

    public void SetHealth(int value)
    {
        CurrentHealth = Mathf.Clamp(value, 0, maxHealth);
        UpdateHealthBar();
    }

    public void ShowHealthBar()
    {
        if (healthBarSlider == null) return;
        healthBarSlider.gameObject.SetActive(true);
    }

    public void UpdateHealthBar()
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
        RunStatsManager.Instance?.RecordEnemyKilled();

        // Se um override foi definido (ex: Geobionte usa fuga ao invés de morte),
        // chama o callback e retorna sem executar a lógica padrão.
        if (onDeathOverride != null)
        {
            onDeathOverride.Invoke();
            return;
        }

        Debug.Log(gameObject.name + " foi destruído.");
        if (debugLogDamage)
        {
            Debug.Log("[DummyHealth] Death stack:\n" + System.Environment.StackTrace);
        }

        // --- SISTEMA DE PACTOS DO JOGADOR ---
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.lastKillTime = Time.time;        // Checa Vampirismo (GDD §4.1 - Carta O Parasita)
            if (playerHealth != null && playerHealth.currentHealth > 0 && playerHealth.hasVampirism)
            {
                Debug.Log("[DUMMY] Inimigo morreu! Curando Player (Vampirismo).");
                playerHealth.Heal(5); // Cura fixa de 5 (ajustável se quiser balancear)
                playerHealth.lastKillTime = Time.time; // Reseta a degeneração!
            }
        }
        // ------------------------------------

        // Chama o sistema de drops se existir
        // Busca em filhos e pais também — cobre hierarquias mais complexas de prefab
        EnemyDrops drops = GetComponent<EnemyDrops>()
                        ?? GetComponentInChildren<EnemyDrops>()
                        ?? GetComponentInParent<EnemyDrops>();
        if (drops != null)
        {
            Debug.Log("[DROPS] EnemyDrops encontrado, chamando OnDeath()...");
            drops.OnDeath();
        }
        else
        {
            Debug.LogWarning("[DROPS] EnemyDrops NÃO encontrado em " + gameObject.name + "! Adicione o componente EnemyDrops.");
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

    private void LateUpdate()
    {
        // Orientação Billboard: Garante que o Canvas/Slider de vida fique sempre virado de frente para a Câmera do Jogador
        if (healthBarSlider != null && healthBarSlider.gameObject.activeInHierarchy)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Canvas canvas = healthBarSlider.GetComponentInParent<Canvas>();
                Transform targetTransform = (canvas != null) ? canvas.transform : healthBarSlider.transform;
                targetTransform.rotation = mainCam.transform.rotation;
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