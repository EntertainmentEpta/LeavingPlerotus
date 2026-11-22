using UnityEngine;
using System.Collections;

public class MagicStone_AI : MonoBehaviour
{
    [Header("Referências")]
    public Transform playerTransform;
    public GameObject attackMarkerPrefab;
    public GameObject attackBeamPrefab;

    [Header("Comportamento de Movimento")]
    public float moveSpeed = 4f;
    public float minOrbitDistance = 12f;
    public float maxOrbitDistance = 15f;
    public int orbitDirection = 1;

    [Header("Comportamento de Flutuação")]
    public float floatHeight = 0.2f;
    public float floatSpeed = 1f;
    private float startY;

    [Header("Ataque")]
    public float attackInterval = 5f;
    public float attackTelegraphTime = 1.5f;

    [Header("Teleporte")]
    public float teleportCooldown = 30f;
    public float teleportRange = 4f;
    // --- NOVA VARIÁVEL AQUI ---
    [Tooltip("Fator de distância do teleporte. 1.0 = lado oposto do mapa. 0.5 = metade do caminho.")]
    [Range(0.1f, 1.0f)]
    public float teleportDistanceFactor = 0.7f;

    private float teleportTimer;
    private float attackTimer;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        startY = transform.position.y;
        teleportTimer = 0;
        attackTimer = attackInterval / 2;

        if (Random.value > 0.5f)
        {
            orbitDirection = -1;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        teleportTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        HandleMovement();
        HandleTeleport();
        HandleAttack();
        HandleFloating();
    }

    void HandleMovement()
    {
        Vector3 playerPositionOnPlane = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        Vector3 directionToPlayer = (playerPositionOnPlane - transform.position).normalized;
        float distance = Vector3.Distance(playerPositionOnPlane, transform.position);
        
        Vector3 orbitDirectionVector = Vector3.Cross(directionToPlayer, Vector3.up) * orbitDirection;
        Vector3 finalMoveDirection = Vector3.zero;

        if (distance < minOrbitDistance)
        {
            finalMoveDirection = (-directionToPlayer + orbitDirectionVector).normalized;
        }
        else if (distance > maxOrbitDistance)
        {
            finalMoveDirection = (directionToPlayer + orbitDirectionVector).normalized;
        }
        else
        {
            finalMoveDirection = orbitDirectionVector;
        }
        
        rb.linearVelocity = new Vector3(finalMoveDirection.x * moveSpeed, rb.linearVelocity.y, finalMoveDirection.z * moveSpeed);
    }
    
    void HandleFloating()
    {
        float newY = startY + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void HandleTeleport()
    {
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance < teleportRange && teleportTimer <= 0)
        {
            Teleport();
        }
    }

    void HandleAttack()
    {
        if (attackTimer <= 0)
        {
            StartCoroutine(SkybeamAttack());
            attackTimer = attackInterval;
        }
    }

    void Teleport()
    {
        Debug.Log("MagicStone teleportou!");
        
        // --- LÓGICA DO TELEPORTE MODIFICADA ---
        // Agora multiplica a posição oposta pelo fator de distância
        Vector3 newPosition = -playerTransform.position * teleportDistanceFactor;
        newPosition.y = transform.position.y; // Mantém a mesma altura
        transform.position = newPosition;

        teleportTimer = teleportCooldown;
    }

    IEnumerator SkybeamAttack()
    {
        Vector3 targetPosition = new Vector3(playerTransform.position.x, 0.01f, playerTransform.position.z);
        GameObject marker = Instantiate(attackMarkerPrefab, targetPosition, Quaternion.Euler(90, 0, 0));
        yield return new WaitForSeconds(attackTelegraphTime);
        Destroy(marker);
        Instantiate(attackBeamPrefab, targetPosition, Quaternion.identity);
    }
}