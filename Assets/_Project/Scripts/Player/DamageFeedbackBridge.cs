using UnityEngine;

/// <summary>
/// Liga o DamageFeedback ao PlayerHealth SEM alterar o PlayerHealth.
///
/// - Escuta o evento estático PlayerHealth.OnPlayerDamaged (já disparado a cada golpe)
///   e mostra o ponteiro na direção do atacante.
/// - Lê a vida atual do PlayerHealth a cada frame e repassa ao DamageFeedback
///   (cobre dano, cura, regeneração, necrose, morte e respawn).
/// - Se o campo "Player" do DamageFeedback estiver vazio, preenche com o personagem.
///
/// Não precisa colocar em nenhum objeto: ele se cria sozinho quando o jogo começa
/// e continua vivo entre as cenas. Só precisa existir um DamageFeedback na cena.
/// </summary>
public class DamageFeedbackBridge : MonoBehaviour
{
    PlayerHealth playerHealth;
    float nextSearchTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("[DamageFeedbackBridge]");
        DontDestroyOnLoad(go);
        go.AddComponent<DamageFeedbackBridge>();
    }

    void OnEnable()  { PlayerHealth.OnPlayerDamaged += HandlePlayerDamaged; }
    void OnDisable() { PlayerHealth.OnPlayerDamaged -= HandlePlayerDamaged; }

    void HandlePlayerDamaged(int damage, GameObject attacker)
    {
        var fx = DamageFeedback.Instance;
        if (fx == null) return;

        FindPlayer();
        LinkPlayer(fx);

        // Dano sem atacante (ácido, poça, veneno) não tem direção: só a vida é atualizada.
        if (attacker != null)
        {
            float max = playerHealth != null ? Mathf.Max(1, playerHealth.maxHealth) : 100f;
            fx.ReportHit(attacker.transform, damage / max);
        }

        if (playerHealth != null)
            fx.SetHealth(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    void Update()
    {
        var fx = DamageFeedback.Instance;
        if (fx == null) return;

        if (playerHealth == null && Time.unscaledTime >= nextSearchTime)
        {
            nextSearchTime = Time.unscaledTime + 0.5f;
            FindPlayer();
        }
        if (playerHealth == null) return;

        LinkPlayer(fx);
        fx.SetHealth(playerHealth.currentHealth, playerHealth.maxHealth);
    }

    void FindPlayer()
    {
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    void LinkPlayer(DamageFeedback fx)
    {
        if (playerHealth != null && fx.player == null) fx.player = playerHealth.transform;
    }
}
