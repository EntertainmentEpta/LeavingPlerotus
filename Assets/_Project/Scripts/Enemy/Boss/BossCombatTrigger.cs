using UnityEngine;
using System.Collections;

/// <summary>
/// Trigger de início do combate com o Boss.
///
/// FLUXO:
///   1. Player entra na arena passando pela entrada.
///   2. Após sealDelay, a barreira gigante sela a entrada nas costas do player.
///   3. O boss começa a lutar (StartFight).
///   4. Boss morre → a barreira é destruída/aberta pelo BossController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossCombatTrigger : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("O BossController do boss nesta arena.")]
    public BossController bossController;

    [Tooltip("O selo/barreira que tranca a arena durante o combate.\n" +
             "Começa DESATIVADO e é ativado quando o player entra.")]
    public GameObject arenaSeal;

    [Tooltip("GameObjects de portas extras que trancam durante o combate (opcional).")]
    public GameObject[] entranceDoors;

    [Header("Barreira Customizada (Prefab)")]
    [Tooltip("Prefab do modelo 3D da barreira (ex: Barreira.prefab). Se vazio, carrega automaticamente.")]
    public GameObject barrierPrefab;

    [Tooltip("Escala monumental para a barreira cobrir a entrada do Boss.")]
    public Vector3 barrierScale = new Vector3(6f, 5.5f, 3.5f);

    [Tooltip("Ajuste vertical de altura (eixo Y) da barreira.")]
    public float barrierYOffset = 1.0f;

    [Header("Configuração")]
    [Tooltip("Delay em segundos após o player entrar antes de ativar a barreira.\n" +
             "Dá tempo do player entrar completamente na arena.")]
    public float sealDelay = 1.0f;

    [Tooltip("Delay adicional após o selo antes de iniciar a luta.\n" +
             "Momento dramático antes do boss atacar.")]
    public float fightStartDelay = 1.5f;

    [Header("Debug")]
    public bool showDebugLog = true;

    private bool hasTriggered = false;

    void Start()
    {
        EnsureBossArenaBarrier();

        // Garante que o selo começa desativado
        if (arenaSeal != null)
            arenaSeal.SetActive(false);

        // Garante que o collider é trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[BossCombatTrigger] Collider não era trigger — corrigido automaticamente.");
        }
    }

    private void EnsureBossArenaBarrier()
    {
        if (barrierPrefab == null)
        {
            barrierPrefab = Resources.Load<GameObject>("Barreira");
#if UNITY_EDITOR
            if (barrierPrefab == null)
                barrierPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enviroment/Map/New map/BossRoom/Barreira.prefab");
#endif
        }

        ConnectionPoint cp = GetComponentInChildren<ConnectionPoint>();
        if (cp == null) return;

        // Se o arenaSeal antigo for aquele objeto genérico primitivo, desativa
        if (arenaSeal != null && arenaSeal.name == "ArenaSeal")
        {
            arenaSeal.SetActive(false);
        }

        Transform existing = transform.Find("[Boss_ArenaBarrier]");
        if (existing == null && barrierPrefab != null)
        {
            // Posiciona a barreira exatamente no ConnectionPoint da entrada
            Vector3 barrierPos = cp.transform.position + Vector3.up * barrierYOffset;

            // Direção horizontal perpendicular da porta
            Vector3 horizontalFwd = Vector3.ProjectOnPlane(cp.transform.forward, Vector3.up);
            if (horizontalFwd.sqrMagnitude < 0.01f)
                horizontalFwd = Vector3.ProjectOnPlane(cp.transform.up, Vector3.up);
            if (horizontalFwd.sqrMagnitude < 0.01f)
                horizontalFwd = Vector3.forward;
            horizontalFwd.Normalize();

            Quaternion rot = Quaternion.LookRotation(horizontalFwd, Vector3.up);

            GameObject wall = Instantiate(barrierPrefab, barrierPos, rot);
            wall.name = "[Boss_ArenaBarrier]";
            wall.transform.localScale = barrierScale;
            wall.transform.SetParent(this.transform, true);

            // Garante um BoxCollider sólido e largo para vedar 100% da passagem
            BoxCollider blocker = wall.GetComponent<BoxCollider>();
            if (blocker == null)
            {
                blocker = wall.AddComponent<BoxCollider>();
            }
            blocker.isTrigger = false;
            blocker.size = new Vector3(16f, 12f, 3f);
            blocker.center = new Vector3(0, 5f, 0);

            arenaSeal = wall;
            if (bossController != null)
            {
                bossController.arenaSeal = wall;
            }

            wall.SetActive(false);
        }
        else if (existing != null)
        {
            arenaSeal = existing.gameObject;
            if (bossController != null)
            {
                bossController.arenaSeal = existing.gameObject;
            }
            existing.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered) return;

        hasTriggered = true;

        if (showDebugLog) Debug.Log("[BossCombatTrigger] ⚔️ Player entrou na arena do boss!");

        StartCoroutine(StartBossFightSequence());
    }

    private IEnumerator StartBossFightSequence()
    {
        // Espera o player entrar completamente na arena
        yield return new WaitForSeconds(sealDelay);

        // Ativa o selo/barreira (tranca a arena)
        if (arenaSeal != null)
        {
            arenaSeal.SetActive(true);
            if (showDebugLog) Debug.Log("[BossCombatTrigger] 🔒 Barreira ativada — arena do Boss trancada!");
        }

        // Tranca portas extras se houver
        if (entranceDoors != null)
        {
            foreach (GameObject door in entranceDoors)
            {
                if (door != null) door.SetActive(true);
            }
        }

        // Momento dramático antes do boss atacar
        yield return new WaitForSeconds(fightStartDelay);

        // Inicia a luta
        if (bossController != null)
        {
            bossController.StartFight();
        }
        else
        {
            Debug.LogError("[BossCombatTrigger] ❌ BossController não atribuído! Arraste o boss no Inspector.");
        }

        // Desativa o trigger (não precisa mais)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
