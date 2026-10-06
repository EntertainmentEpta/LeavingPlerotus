using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class VictoryScreenUI : MonoBehaviour
{
    public static VictoryScreenUI Instance { get; private set; }

    [Header("Painel Principal")]
    public GameObject victoryPanel;

    [Header("Containers (Abas)")]
    public GameObject containerStats;
    public GameObject containerThanks;

    [Header("Botões das Abas")]
    public Button btnTabStats;
    public Button btnTabThanks;

    [Header("Textos")]
    public TextMeshProUGUI txtTitle;
    public TextMeshProUGUI txtStatsSummary;
    public TextMeshProUGUI txtThanksMessage; // Opcional, para o texto de agradecimento

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        // Liga as funções de trocar de aba automaticamente aos botões
        if (btnTabStats != null) btnTabStats.onClick.AddListener(ShowStatsTab);
        if (btnTabThanks != null) btnTabThanks.onClick.AddListener(ShowThanksTab);
    }

    public void ShowVictoryScreen()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            victoryPanel.transform.SetAsLastSibling();
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // === A MUDANÇA ESTÁ AQUI ===
        // Garante que a aba de Agradecimento seja a primeira a aparecer!
        ShowThanksTab();

        if (txtTitle != null)
        {
            txtTitle.text = "<size=46><color=#00ff99><b>ÁREA CONCLUÍDA!</b></color></size>\n<size=16><color=#ffaa44><i>RESUMO DA MISSÃO</i></color></size>";
        }

        if (txtStatsSummary != null && RunStatsManager.Instance != null)
        {
            RunStatsManager s = RunStatsManager.Instance;
            string timeStr = s.FormatTime(s.survivalTimer);
            string dmgDealtStr = s.FormatNumber(s.totalDamageDealt);
            string dmgTakenStr = s.FormatNumber(s.totalDamageTaken);

            txtStatsSummary.text =
                $"<color=#ffcc00><b>TEMPO DE CONCLUSÃO:</b></color>  <color=#ffffff>{timeStr}</color>\n\n" +
                $"<color=#ffaa44><b>DANO TOTAL CAUSADO:</b></color>  <color=#ffffff>{dmgDealtStr}</color>\n\n" +
                $"<color=#ff4455><b>INIMIGOS DERROTADOS:</b></color>  <color=#ffffff>{s.totalMobsKilled}</color>\n\n" +
                $"<color=#00ff99><b>ESSÊNCIAS COLETADAS:</b></color>  <color=#ffffff>{s.totalEssenceCollected}</color>\n\n" +
                $"<color=#ffcc00><b>ESSÊNCIAS GASTAS:</b></color>  <color=#ffffff>{s.totalEssenceSpent}</color>\n\n" +
                $"<color=#ff6666><b>DANO TOTAL RECEBIDO:</b></color>  <color=#ffffff>{dmgTakenStr}</color>";
        }

        // Você pode editar a mensagem de agradecimento direto pelo Inspector ou deixar este texto base
        if (txtThanksMessage != null)
        {
            txtThanksMessage.text = "Obrigado por jogar a nossa demo!\n\nSua jornada pelo subsolo apenas começou. Em breve, novos desafios e mistérios aguardam.\n\n<color=#00ff99><b>A equipe agradece!</b></color>";
        }

        Time.timeScale = 0f; 
    }

    public void ShowStatsTab()
    {
        if (containerStats != null) containerStats.SetActive(true);
        if (containerThanks != null) containerThanks.SetActive(false);
    }

    public void ShowThanksTab()
    {
        if (containerStats != null) containerStats.SetActive(false);
        if (containerThanks != null) containerThanks.SetActive(true);
    }

    // Chamado pelo botão "Voltar à Base"
    public void OnReturnBaseClicked()
    {
        Time.timeScale = 1f;
        if (RunStatsManager.Instance != null)
        {
            RunStatsManager.Instance.ResetStats();
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.ReturnToBase();
        }
        else
        {
            SceneManager.LoadScene("Base");
        }
    }
}