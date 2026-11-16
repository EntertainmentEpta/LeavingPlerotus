using UnityEngine;

/// <summary>
/// Componente auxiliar para testes e validação da marca TunerMarkVFX em PlayMode na cena EnemyTestScene.
/// Permite aplicar a marca, simular o shatter ou acompanhar a expiração via teclado ou Inspector.
/// </summary>
public class TunerMarkTester : MonoBehaviour
{
    [Header("Configurações de Teste")]
    [Tooltip("Prefab customizado da marca. Se nulo, utiliza a geração procedural.")]
    public GameObject markPrefab;

    [Tooltip("Duração da marca em segundos para o teste.")]
    public float testDuration = 5.0f;

    [Tooltip("Raio de busca para encontrar o monstro vivo mais próximo.")]
    public float searchRadius = 30.0f;

    [Header("Estado Atual do Teste")]
    [SerializeField] private TunerMarkVFX activeMark;
    [SerializeField] private GameObject currentMarkedEnemy;

    private void Update()
    {
        // Tecla T: Aplica a marca no inimigo vivo mais proximo
        if (Input.GetKeyDown(KeyCode.T))
        {
            MarkNearestEnemy();
        }

        // Tecla Y: Dispara o efeito de Shatter na marca atual
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (activeMark != null)
            {
                activeMark.TriggerShatter();
                activeMark = null;
                currentMarkedEnemy = null;
            }
        }

        // Tecla U: Força a expiração / fade-out da marca atual
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (activeMark != null)
            {
                activeMark.Expire();
                activeMark = null;
                currentMarkedEnemy = null;
            }
        }
    }

    [ContextMenu("Test - Mark Nearest Enemy")]
    public void MarkNearestEnemy()
    {
        GameObject nearestEnemy = FindNearestEnemy();
        if (nearestEnemy == null)
        {
            Debug.LogWarning("[TunerMarkTester] Nenhum inimigo vivo encontrado no raio de busca.");
            return;
        }

        currentMarkedEnemy = nearestEnemy;
        activeMark = TunerMarkVFX.AttachMark(nearestEnemy, testDuration, markPrefab);
    }

    [ContextMenu("Test - Shatter Current Mark")]
    public void ShatterCurrentMark()
    {
        if (activeMark != null)
        {
            activeMark.TriggerShatter();
            activeMark = null;
            currentMarkedEnemy = null;
        }
    }

    private GameObject FindNearestEnemy()
    {
        DummyHealth[] allDummies = FindObjectsByType<DummyHealth>(FindObjectsSortMode.None);
        GameObject bestTarget = null;
        float bestDist = searchRadius * searchRadius;
        Vector3 origin = transform.position;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) origin = player.transform.position;

        foreach (var dummy in allDummies)
        {
            if (dummy == null || dummy.CurrentHealth <= 0) continue;
            float sqrDist = (dummy.transform.position - origin).sqrMagnitude;
            if (sqrDist < bestDist)
            {
                bestDist = sqrDist;
                bestTarget = dummy.gameObject;
            }
        }

        return bestTarget;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) origin = player.transform.position;
        Gizmos.DrawWireSphere(origin, searchRadius);
    }
}
