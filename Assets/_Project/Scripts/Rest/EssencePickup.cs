using UnityEngine;

/// <summary>
/// Pickup de Essência - A "moeda XP" do jogo
/// Player coleta ao encostar e ganha pontos de essência
/// </summary>
public class EssencePickup : MonoBehaviour
{
    [Header("Valor")]
    [Tooltip("Quantidade de essência que este pickup dá")]
    public int essenceValue = 10;

    [Header("Movimento")]
    [Tooltip("Velocidade de atração quando perto do player")]
    public float attractSpeed = 16f;
    [Tooltip("Distância para começar a atrair")]
    public float attractDistance = 11f;
    [Tooltip("Offset vertical do ponto de atração (0 = pés, 1.2 = peito)")]
    public float attractYOffset = 1.2f;
    [Tooltip("Velocidade de rotação visual")]
    public float rotateSpeed = 90f;

    [Header("Flutuação")]
    [Tooltip("Altura da flutuação (pra cima e pra baixo)")]
    public float bobHeight = 0.15f;
    [Tooltip("Velocidade da flutuação")]
    public float bobSpeed = 2f;

    [Header("Configurações")]
    [Tooltip("Tempo antes de poder ser coletada (evita coleta instantânea)")]
    public float pickupDelay = 0.1f;
    [Tooltip("Tempo de vida antes de desaparecer (0 = infinito)")]
    public float lifetime = 30f;

    private Transform playerTransform;
    private bool canBePickedUp = false;
    private float spawnTime;
    private float baseY;
    private bool isBeingAttracted = false;

    [HideInInspector] public Transform customBossTarget;
    [HideInInspector] public bool isBossAbsorb = false;

    public void FlyToBoss(Transform bossTransform)
    {
        if (bossTransform == null) return;
        customBossTarget = bossTransform;
        isBossAbsorb = true;
        isBeingAttracted = true;
        canBePickedUp = true;
        isPopping = false;
        currentAttractSpeed = 18f;

        EssenceVFX vfx = GetComponent<EssenceVFX>();
        if (vfx != null) vfx.StartFlyingTrail();
    }

    [Header("Efeito Pop (Juice de Spawn)")]
    public bool enablePopArc = true;
    private Vector3 popVelocity;
    private bool isPopping = true;
    private float popTime = 0.35f;
    private float currentAttractSpeed;
    private Vector3 initialScale;

    void Start()
    {
        spawnTime = Time.time;
        baseY = transform.position.y;
        initialScale = transform.localScale;
        currentAttractSpeed = attractSpeed;

        // Configura impulso de explosão física no nascimento (Pop Arc)
        if (enablePopArc)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(1.5f, 2.8f);
            popVelocity = new Vector3(randomCircle.x, Random.Range(2.5f, 4.0f), randomCircle.y);
        }
        else
        {
            isPopping = false;
        }

        // Encontra o player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Força a cor amarelada/dourada no material, luz e trail do pickup (substitui qualquer material rosa)
        Color yellowColor = new Color(1.00f, 0.85f, 0.10f, 1.00f);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Shader uShader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Unlit/Color");

        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                Material m = new Material(uShader);
                m.color = yellowColor;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", yellowColor);
                if (m.HasProperty("_Color")) m.SetColor("_Color", yellowColor);
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", yellowColor * 2.5f);
                r.material = m;
            }
        }

        Light l = GetComponentInChildren<Light>();
        if (l != null)
        {
            l.color = yellowColor;
        }

        TrailRenderer tr = GetComponentInChildren<TrailRenderer>();
        if (tr != null)
        {
            tr.startColor = new Color(1.00f, 0.90f, 0.20f, 1.00f);
            tr.endColor = new Color(1.00f, 0.70f, 0.00f, 0.00f);
        }
        if (lifetime > 0)
        {
            Destroy(gameObject, lifetime);
        }
    }

    void Update()
    {
        // 1. Fase de Pop (Arco de nascimento no ar)
        if (isPopping)
        {
            popVelocity.y -= 14f * Time.deltaTime; // Gravidade do arco
            transform.position += popVelocity * Time.deltaTime;

            if (Time.time - spawnTime >= popTime || popVelocity.y < 0)
            {
                isPopping = false;

                // Snap de segurança no chão real para evitar que a essência fique flutuando alto
                if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit groundHit, 15f))
                {
                    baseY = groundHit.point.y + 0.5f;
                    Vector3 p = transform.position;
                    if (p.y > baseY) p.y = baseY;
                    transform.position = p;
                }
                else
                {
                    baseY = transform.position.y;
                }
            }
            return;
        }

        // Delay antes de poder coletar
        if (!canBePickedUp)
        {
            if (Time.time - spawnTime >= pickupDelay)
            {
                canBePickedUp = true;
            }
        }

        // Rotação suave no ar em múltiplos eixos
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        transform.Rotate(Vector3.right, (rotateSpeed * 0.4f) * Time.deltaTime, Space.Self);

        // Checa se o player está perto pra atrair
        if (!isBeingAttracted && playerTransform != null && canBePickedUp)
        {
            float currentAttractDistance = attractDistance;
            PlayerAttributesDefensive defStats = playerTransform.GetComponent<PlayerAttributesDefensive>();
            if (defStats != null)
            {
                currentAttractDistance *= defStats.magnetRangeMultiplier;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist < currentAttractDistance)
            {
                isBeingAttracted = true;

                EssenceVFX vfx = GetComponent<EssenceVFX>();
                if (vfx != null)
                {
                    vfx.StartFlyingTrail();
                }
            }
        }

        // 2. Fase de Atração Magnética Fluida e Acelerada
        if (isBossAbsorb && customBossTarget != null)
        {
            currentAttractSpeed = Mathf.Min(currentAttractSpeed + 35f * Time.deltaTime, 30f);
            Vector3 targetPos = customBossTarget.position + Vector3.up * 1.5f;

            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentAttractSpeed * Time.deltaTime);
            float distToBoss = Vector3.Distance(transform.position, targetPos);

            if (distToBoss < 1.2f)
            {
                float tScale = Mathf.Clamp01(distToBoss / 1.2f);
                transform.localScale = Vector3.Lerp(Vector3.zero, initialScale, tScale);
            }

            if (distToBoss < 0.4f)
            {
                Destroy(gameObject);
            }
        }
        else if (isBeingAttracted && playerTransform != null)
        {
            currentAttractSpeed = Mathf.Min(currentAttractSpeed + 28f * Time.deltaTime, 26f);
            Vector3 targetPos = playerTransform.position + Vector3.up * attractYOffset;

            // Movimento curvado e acelerado
            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentAttractSpeed * Time.deltaTime);

            float distToChest = Vector3.Distance(transform.position, targetPos);

            // Encolhe suavemente ao se aproximar do peito para dar efeito de absorção física
            if (distToChest < 1.2f)
            {
                float tScale = Mathf.Clamp01(distToChest / 1.2f);
                transform.localScale = Vector3.Lerp(Vector3.zero, initialScale, tScale);
            }

            // Absorve ao encostar
            if (distToChest < 0.35f)
            {
                CollectEssence(playerTransform.gameObject);
            }
        }
        else
        {
            // Flutuação mística (bobbing)
            float newY = baseY + Mathf.Sin((Time.time - spawnTime) * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canBePickedUp) return;

        if (other.CompareTag("Player"))
        {
            CollectEssence(other.gameObject);
        }
    }

    void CollectEssence(GameObject player)
    {
        // Tenta encontrar o componente de essência do player no objeto e nos pais
        PlayerEssence playerEssence = player.GetComponentInParent<PlayerEssence>();
        
        // Se ainda não achar (ex: o script está num objeto irmão do colisor), procura globalmente
        if (playerEssence == null)
        {
            playerEssence = Object.FindFirstObjectByType<PlayerEssence>();
        }
        
        if (playerEssence != null)
        {
            playerEssence.AddEssence(essenceValue);
            Debug.Log("[ESSENCE] Coletou " + essenceValue + " de essência!");
        }
        else
        {
            Debug.Log("[ESSENCE] Coletou " + essenceValue + " de essência! (PlayerEssence não encontrado, apenas log)");
        }

        // VFX de coleta
        EssenceVFX vfx = GetComponent<EssenceVFX>();
        if (vfx != null)
        {
            vfx.PlayCollectEffect();
        }

        Destroy(gameObject);
    }
}
