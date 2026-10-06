using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DeathScreenUI : MonoBehaviour
{
    public static DeathScreenUI Instance { get; private set; }

    [Header("Painéis & Textos")]
    public GameObject deathPanel;
    public TextMeshProUGUI txtTitle;
    public TextMeshProUGUI txtStatsSummary;

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
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }
    }

    public void ShowDeathScreen()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
            deathPanel.transform.SetAsLastSibling();
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (txtTitle != null)
        {
            txtTitle.text = "<size=46><color=#ff2233><b>VOCÊ MORREU</b></color></size>\n<size=16><color=#ffaa44><i>RESUMO DA RUN</i></color></size>";
        }

        if (txtStatsSummary != null && RunStatsManager.Instance != null)
        {
            RunStatsManager s = RunStatsManager.Instance;
            string timeStr = s.FormatTime(s.survivalTimer);
            string dmgDealtStr = s.FormatNumber(s.totalDamageDealt);
            string dmgTakenStr = s.FormatNumber(s.totalDamageTaken);

            txtStatsSummary.text =
                $"<color=#ffcc00><b>TEMPO SOBREVIDO:</b></color>  <color=#ffffff>{timeStr}</color>\n\n" +
                $"<color=#ffaa44><b>DANO TOTAL CAUSADO:</b></color>  <color=#ffffff>{dmgDealtStr}</color>\n\n" +
                $"<color=#ff4455><b>INIMIGOS DERROTADOS:</b></color>  <color=#ffffff>{s.totalMobsKilled}</color>\n\n" +
                $"<color=#00ff99><b>ESSÊNCIAS COLETADAS:</b></color>  <color=#ffffff>{s.totalEssenceCollected}</color>\n\n" +
                $"<color=#ffcc00><b>ESSÊNCIAS GASTAS:</b></color>  <color=#ffffff>{s.totalEssenceSpent}</color>\n\n" +
                $"<color=#ff6666><b>DANO TOTAL RECEBIDO:</b></color>  <color=#ffffff>{dmgTakenStr}</color>\n\n" +
                $"<color=#cc88ff><b>LOCAL DA MORTE:</b></color>  <color=#ffffff>{s.deathStage}</color>";
        }

        Time.timeScale = 0f; // Pausa a partida
    }

    // Chamado pelo botão no Inspector
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