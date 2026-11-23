using UnityEngine;
using System.Collections;

/// <summary>
/// IA do Crystal Dragon (mob comum).
/// Voa a uma altura fixa, mantém distância ideal de combate (~3 m),
/// dispara spread shot, executa ataque de rabada e spin attack 360° melee.
/// Usa DummyHealth para vida e PlayerHealth para causar dano.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CrystalDragonCommon_AI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // Enum de estados
    // ─────────────────────────────────────────────────────────────

    public enum EstadoDragao
    {
        Idle,           // Recuperação / estado inicial após ações
        Chasing,        // Aproxima-se até entrar na zona ideal
        Orbitar,        // Kiting — gira ao redor do player mantendo ~3 m
        Fugir,          // Player chegou perto demais — recua
        RangedAttack,   // Para e dispara spread shot
        AtaqueTail,     // Rabada quando flanqueado por trás
        SpinAttack      // Giro 360° melee quando player entra em curta distância
    }

    // ─────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────

    [Header("Alvo")]
    [Tooltip("Referência ao Transform do Player. Se null, localiza via tag 'Player' no Start.")]
    public Transform player;

    [Header("Movimentação")]
    public float velocidadeVoo = 5f;
    [Tooltip("Altura mundial (Y) em que o dragão deve voar")]
    public float alturaFixa = 3f;
    [Tooltip("Distância ideal de combate (metros)")]
    public float distanciaIdeal = 3.0f;
    [Tooltip("Recua se o player ficar abaixo desta distância")]
    public float distanciaFuga = 2.8f;
    [Tooltip("Aproxima-se se o player ficar acima desta distância")]
    public float distanciaAproximacao = 3.5f;
    public float velocidadeRotacao = 6f;

    [Header("Órbita")]
    [Tooltip("Velocidade angular ao orbitar o player (graus/segundo)")]
    public float velocidadeOrbita = 60f;
    [Tooltip("Intervalo mínimo/máximo (segundos) para sortear nova direção de órbita")]
    public float intervaloMudancaDirecaoMin = 2f;
    public float intervaloMudancaDirecaoMax = 5f;

    [Header("Ataque de Projétil")]
    [Tooltip("Ponto de origem dos projéteis")]
    public Transform pointDisparo;
    public GameObject projectilePrefab;
    [Tooltip("Cooldown total entre rajadas (segundos)")]
    public float cooldownAtaque = 3f;
    [Tooltip("Delay entre cada disparo da rajada")]
    public float delayEntreDisparos = 0.2f;
    public float velocidadeProjétil = 10f;
    [Tooltip("Lifetime do projétil (segundos)")]
    public float lifetimeProjétil = 5f;
    [Tooltip("Ângulo horizontal de cada projétil lateral do spread (graus)")]
    public float anguloSpread = 15f;

    [Header("Buff (Crystal Tuner)")]
    [Tooltip("Projéteis extras adicionados à rajada quando buffado.")]
    public int buffProjeteisExtras = 1;
    [Tooltip("Multiplicador do tamanho do projétil quando buffado.")]
    public float buffProjectileSizeMultiplier = 1.25f;

    [Header("Ataque de Rabada")]
    [Tooltip("Raio do OverlapSphere do ataque de rabada")]
    public float raioTail = 2.0f;
    [Tooltip("Limiar do dot product: valores menores indicam que o player está atrás.\n" +
             "0 = hemisfério traseiro | -0.5 = cone traseiro de 120°")]
    public float dotLimiarTras = -0.2f;
    public int danoRabada = 20;
    [Tooltip("Nome do trigger no Animator para a animação de rabada")]
    public string animTriggerRabada = "Tail";
    [Tooltip("Delay após o SetTrigger antes de aplicar o dano (para sincronizar com a animação)")]
    public float delayDanoRabada = 0.3f;

    [Header("Spin Attack (Melee)")]
    [Tooltip("Distância para ativar o spin attack (metros)")]
    public float meleeAttackRange = 1.5f;
    [Tooltip("Duração total do giro (segundos)")]
    public float spinDuration = 1.2f;
    [Tooltip("Velocidade do giro (graus/segundo)")]
    public float spinSpeed = 720f;
    [Tooltip("Dano aplicado durante o giro")]
    public int spinDamage = 25;
    [Tooltip("Cooldown entre spin attacks (segundos)")]
    public float spinCooldown = 5f;
    [Tooltip("Trigger do Animator para o spin attack")]
    public string animTriggerSpin = "SpinAttack";
    public GameObject attackEffect;
    public Transform attackPoint;

    // ─────────────────────────────────────────────────────────────
    // Privado
    // ─────────────────────────────────────────────────────────────

    private Rigidbody rb;
    private Animator anim;

    private EstadoDragao estado = EstadoDragao.Idle;
    private float timerAtaque = 0f;

    // Flags: impedem que Update dispare múltiplas corrotinas em paralelo
    private bool estaAtacando = false;
    private bool estaExecutandoRabada = false;
    private bool estaExecutandoSpin = false;
    private float timerSpinCooldown = 0f;
    private bool isBuffed = false;
    private int projeteisExtrasAtivos = 0;
    private float projectileSizeMultiplierAtivo = 1f;

    // Órbita
    private float anguloOrbita = 0f;
    private int direcaoOrbita = 1;
    private float timerMudancaDirecao = 0f;

    // Debug
    private EstadoDragao estadoAnterior = EstadoDragao.Idle;
    private float timerLogPeriodico = 0f;
    private float timerLogTravado = 0f;

    // ─────────────────────────────────────────────────────────────
    // Unity callbacks
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        rb.freezeRotation = true;
        rb.useGravity = false; // dragão voa

        // Garante a tag "Enemy" e a Layer "Enemy" no root e em todos os filhos
        gameObject.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer != -1)
        {
            gameObject.layer = enemyLayer;
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = enemyLayer;
                child.gameObject.tag = "Enemy";
            }
        }

        // Garante que os colidores do dragão sejam Triggers para receber dano sem servir de rampa/plataforma para outros mobs (como Golem)
        Collider[] dragonColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider dc in dragonColliders)
        {
            dc.isTrigger = true;
        }

        // Ignora colisões físicas com todos os outros inimigos para ninguém surfá-lo
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider otherCol in allColliders)
        {
            if (otherCol != null && otherCol.transform.root != transform.root)
            {
                if (otherCol.gameObject.layer == enemyLayer || otherCol.CompareTag("Enemy"))
                {
                    foreach (Collider dc in dragonColliders)
                    {
                        Physics.IgnoreCollision(dc, otherCol, true);
                    }
                }
            }
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
                Debug.LogError("[CRYSTAL DRAGON COMMON] Player não encontrado! Verifique a tag 'Player'.");
        }

        // Inicia o ângulo de órbita a partir da posição atual em relação ao player
        if (player != null)
        {
            Vector3 offset = transform.position - player.position;
            offset.y = 0f;
            if (offset.magnitude > 0.01f)
                anguloOrbita = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

            // Impede que o Rigidbody do dragão empurre o player fisicamente.
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
            foreach (Collider dc in dragonColliders)
                foreach (Collider pc in playerColliders)
                    Physics.IgnoreCollision(dc, pc, true);
        }
        direcaoOrbita = Random.value > 0.5f ? 1 : -1;
        timerMudancaDirecao = Random.Range(intervaloMudancaDirecaoMin, intervaloMudancaDirecaoMax);
    }

    /// <summary>
    /// Crystal Tuner aumenta levemente a pressão do ranged sem alterar dano base por hit.
    /// </summary>
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            projeteisExtrasAtivos = Mathf.Max(0, buffProjeteisExtras);
            projectileSizeMultiplierAtivo = Mathf.Max(0.1f, buffProjectileSizeMultiplier);
            Debug.Log($"[DRAGON] Buff ativo: +{projeteisExtrasAtivos} projétil(eis), tamanho x{projectileSizeMultiplierAtivo:F2}");
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            projeteisExtrasAtivos = 0;
            projectileSizeMultiplierAtivo = 1f;
            Debug.Log("[DRAGON] Buff removido: rajada e tamanho do projétil voltaram ao normal.");
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Log periódico a cada 2s com distância e estado atual
        timerLogPeriodico -= Time.deltaTime;
        if (timerLogPeriodico <= 0f)
        {
            timerLogPeriodico = 2f;
            Debug.Log($"[DRAGON] Estado={estado} | Dist={DistanciaHorizontal():F2}m " +
                      $"| timerAtaque={timerAtaque:F1}s | spinCD={timerSpinCooldown:F1}s " +
                      $"| atk={estaAtacando} | rabada={estaExecutandoRabada} | spin={estaExecutandoSpin}");
        }

        // Se estiver travado (flags ativas), avisa a cada 1s
        if (estaAtacando || estaExecutandoRabada || estaExecutandoSpin)
        {
            timerLogTravado -= Time.deltaTime;
            if (timerLogTravado <= 0f)
            {
                timerLogTravado = 1f;
                Debug.LogWarning($"[DRAGON] BLOQUEADO — atk={estaAtacando} rabada={estaExecutandoRabada} spin={estaExecutandoSpin}");
            }
            return;
        }
        timerLogTravado = 0f;

        timerAtaque -= Time.deltaTime;
        timerSpinCooldown -= Time.deltaTime;

        // Timer para mudar direção de órbita aleatoriamente
        timerMudancaDirecao -= Time.deltaTime;
        if (timerMudancaDirecao <= 0f)
        {
            direcaoOrbita = Random.value > 0.5f ? 1 : -1;
            timerMudancaDirecao = Random.Range(intervaloMudancaDirecaoMin, intervaloMudancaDirecaoMax);
        }

        AvaliarEstado();

        // Log quando o estado muda
        if (estado != estadoAnterior)
        {
            Debug.Log($"[DRAGON] Estado mudou: {estadoAnterior} → {estado} | Dist={DistanciaHorizontal():F2}m");
            estadoAnterior = estado;
        }

        // Corrotinas são disparadas do Update (correto — apenas iniciam uma vez)
        if (estado == EstadoDragao.RangedAttack)
            StartCoroutine(RajadaDeProjeteis());
        else if (estado == EstadoDragao.AtaqueTail)
            StartCoroutine(ExecutarRabada());
        else if (estado == EstadoDragao.SpinAttack)
            StartCoroutine(ExecutarSpinAttack());

        RotacionarParaPlayer();
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        // Toda física (rb.MovePosition) deve ficar no FixedUpdate
        if (!estaAtacando && !estaExecutandoRabada && !estaExecutandoSpin)
        {
            switch (estado)
            {
                case EstadoDragao.Idle:
                case EstadoDragao.Chasing:
                    MoverParaDistanciaIdeal();
                    break;
                case EstadoDragao.Orbitar:
                    Orbitar();
                    break;
                case EstadoDragao.Fugir:
                    Recuar();
                    break;
                default:
                    MantenerAltura();
                    break;
            }
        }
        else if (!estaExecutandoSpin)
        {
            // Durante ataques de projétil/rabada: hover parado
            MantenerAltura();
        }
        // Durante SpinAttack: a corrotina controla a posição diretamente
    }

    // ─────────────────────────────────────────────────────────────
    // Máquina de estados
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Avalia a situação e seleciona o estado mais adequado.
    /// O flanco traseiro tem prioridade máxima sobre qualquer outra condição.
    /// </summary>
    private void AvaliarEstado()
    {
        float dist = DistanciaHorizontal();

        // ── Prioridade 0 (máxima): Spin Attack melee ─────────────────
        if (dist <= meleeAttackRange && timerSpinCooldown <= 0f)
        {
            estado = EstadoDragao.SpinAttack;
            return;
        }

        // ── Prioridade 1: flanqueamento traseiro ──────────────────
        if (dist <= raioTail && PlayerEstaTras())
        {
            estado = EstadoDragao.AtaqueTail;
            return;
        }

        // ── Prioridade 2: manutenção de distância ─────────────────
        if (dist > distanciaAproximacao)
        {
            estado = EstadoDragao.Chasing;
            return;
        }

        if (dist < distanciaFuga)
        {
            estado = EstadoDragao.Fugir;
            return;
        }

        // ── Prioridade 3: atacar ou orbitar na zona ideal ─────────────
        if (timerAtaque <= 0f)
            estado = EstadoDragao.RangedAttack;
        else
            estado = EstadoDragao.Orbitar;
    }

    // ─────────────────────────────────────────────────────────────
    // Movimento
    // ─────────────────────────────────────────────────────────────

    private void MoverParaDistanciaIdeal()
    {
        Vector3 direcao = DirecaoHorizontalParaPlayer();
        Vector3 alvo = player.position - direcao * distanciaIdeal;
        alvo.y = alturaFixa;

        rb.MovePosition(Vector3.MoveTowards(transform.position, alvo, velocidadeVoo * Time.fixedDeltaTime));
    }

    /// <summary>
    /// Move o dragão tangencialmente ao redor do player mantendo distanciaIdeal.
    /// O ângulo é sincronizado da posição real a cada frame para transições suaves.
    /// </summary>
    private void Orbitar()
    {
        // Sincroniza o ângulo com a posição real (elimina saltos ao entrar no estado)
        Vector3 offset = new Vector3(
            transform.position.x - player.position.x, 0f,
            transform.position.z - player.position.z);
        if (offset.magnitude > 0.05f)
            anguloOrbita = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

        // Avança o ângulo neste frame
        anguloOrbita += velocidadeOrbita * direcaoOrbita * Time.fixedDeltaTime;

        // Posição alvo na circunferência de órbita
        Vector3 alvo = player.position
            + Quaternion.Euler(0f, anguloOrbita, 0f) * Vector3.forward * distanciaIdeal;
        alvo.y = alturaFixa;

        rb.MovePosition(Vector3.MoveTowards(transform.position, alvo, velocidadeVoo * Time.fixedDeltaTime));
    }

    private void Recuar()
    {
        Vector3 direcaoFuga = -DirecaoHorizontalParaPlayer();
        Vector3 alvo = new Vector3(
            transform.position.x + direcaoFuga.x * velocidadeVoo * Time.fixedDeltaTime,
            alturaFixa,
            transform.position.z + direcaoFuga.z * velocidadeVoo * Time.fixedDeltaTime
        );
        rb.MovePosition(alvo);
    }

    /// <summary>Mantém a altura fixa sem movimento horizontal (hover).</summary>
    private void MantenerAltura()
    {
        Vector3 pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, alturaFixa, velocidadeVoo * Time.fixedDeltaTime);
        rb.MovePosition(pos);
    }

    private void RotacionarParaPlayer()
    {
        Vector3 direcao = DirecaoHorizontalParaPlayer();
        if (direcao == Vector3.zero) return;

        Quaternion rotAlvo = Quaternion.LookRotation(direcao);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, velocidadeRotacao * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────
    // Ataques
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispara 3 projéteis simultaneamente em formato de cone (spread shot).
    /// Ângulos: -anguloSpread | 0° | +anguloSpread
    /// O dragão para completamente enquanto atira.
    /// </summary>
    private IEnumerator RajadaDeProjeteis() =>
        DummyHealth.PauseAttackWhileStunned(this, RajadaDeProjeteisSteps());

    private IEnumerator RajadaDeProjeteisSteps()
    {
        estaAtacando = true;
        timerAtaque = cooldownAtaque;
        rb.linearVelocity = Vector3.zero;
        Debug.Log("[DRAGON] ATAQUE — Spread shot iniciado. pointDisparo=" +
                  (pointDisparo != null ? pointDisparo.name : "NULL") +
                  " | projectilePrefab=" + (projectilePrefab != null ? projectilePrefab.name : "NULL"));

        int totalProjeteis = 3 + projeteisExtrasAtivos;
        float centro = (totalProjeteis - 1) * 0.5f;
        for (int i = 0; i < totalProjeteis; i++)
        {
            float offset = (i - centro) * anguloSpread;
            DispararProjetil(Quaternion.Euler(0f, offset, 0f));
        }
        Debug.Log($"[DRAGON] Spread shot disparado ({totalProjeteis} projéteis).");

        // Pequena pausa antes de liberar o movimento (dá feel ao ataque)
        yield return new WaitForSeconds(delayEntreDisparos);

        Debug.Log("[DRAGON] Ataque concluído. Retomando movimento.");
        estaAtacando = false;
        estado = EstadoDragao.Idle;
    }

    /// <summary>Instancia e lança um projétil com a rotação horizontal dada.</summary>
    private void DispararProjetil(Quaternion rotacaoSpread)
    {
        if (projectilePrefab == null || pointDisparo == null)
        {
            Debug.LogWarning("[CRYSTAL DRAGON COMMON] projectilePrefab ou pointDisparo não atribuído.");
            return;
        }

        Vector3 origem = pointDisparo.position;

        // Zera o Y para disparar em linha reta horizontal, independente da altura do dragão
        Vector3 diff = player.position - origem;
        diff.y = 0f;
        Vector3 direcaoBase = diff.normalized;
        Vector3 direcao = rotacaoSpread * direcaoBase;

        GameObject proj = Instantiate(projectilePrefab, origem, Quaternion.LookRotation(direcao));
        proj.transform.localScale = proj.transform.localScale * projectileSizeMultiplierAtivo;

        // Compatibilidade com CrystalSpikeProjectile (sistema existente)
        CrystalSpikeProjectile spike = proj.GetComponent<CrystalSpikeProjectile>() ?? proj.AddComponent<CrystalSpikeProjectile>();
        spike.owner = gameObject;
        spike.Launch(direcao, velocidadeProjétil, lifetimeProjétil);
    }

    /// <summary>
    /// Rabada de emergência: aciona animação e aplica dano em área 360°
    /// ao redor do dragão via Physics.OverlapSphere.
    /// </summary>
    private IEnumerator ExecutarRabada() =>
        DummyHealth.PauseAttackWhileStunned(this, ExecutarRabadaSteps());

    private IEnumerator ExecutarRabadaSteps()
    {
        estaExecutandoRabada = true;
        Debug.Log("[DRAGON] RABADA iniciada.");

        if (anim != null)
            anim.SetTrigger(animTriggerRabada);

        yield return new WaitForSeconds(delayDanoRabada);

        Collider[] atingidos = Physics.OverlapSphere(transform.position, raioTail);
        Debug.Log($"[DRAGON] Rabada — {atingidos.Length} colliders detectados no raio {raioTail}m");
        foreach (Collider col in atingidos)
        {
            if (col.CompareTag("Player"))
            {
                PlayerHealth ph = col.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(danoRabada, gameObject);
                    Debug.Log($"[DRAGON] Rabada acertou o Player! Dano={danoRabada}");
                }
            }
        }

        yield return new WaitForSeconds(0.5f);

        estaExecutandoRabada = false;
        timerAtaque = cooldownAtaque * 0.5f;
        estado = EstadoDragao.Idle;
        Debug.Log("[DRAGON] Rabada concluída. Retomando movimento.");
    }

    /// <summary>
    /// Spin Attack 360° melee.
    /// Fases: wind-up (abaixa), giro com dano área, recovery (sobe).
    /// </summary>
    private IEnumerator ExecutarSpinAttack() =>
        DummyHealth.PauseAttackWhileStunned(this, ExecutarSpinAttackSteps());

    private IEnumerator ExecutarSpinAttackSteps()
    {
        estaExecutandoSpin = true;
        timerSpinCooldown = spinCooldown;
        rb.linearVelocity = Vector3.zero;
        Debug.Log("[DRAGON] SPIN ATTACK iniciado!");

        if (anim != null)
            anim.SetTrigger(animTriggerSpin);

        // ── Fase 1: Wind-up — abaixa levemente o corpo ────────────────
        float windUpTime = 0.25f;
        float alturaWindUp = alturaFixa - 0.6f;
        float t = 0f;
        float yInicial = transform.position.y;
        while (t < windUpTime)
        {
            t += Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(yInicial, alturaWindUp, t / windUpTime);
            rb.MovePosition(pos);
            yield return new WaitForFixedUpdate();
        }

        // ── Fase 2: Giro 360° com dano ────────────────────────
        // Dispara o efeito visual de slash no início do giro
        if (attackEffect != null && attackPoint != null)
            Attack();

        // HashSet evita aplicar dano múltiplas vezes no mesmo alvo
        var atingidos = new System.Collections.Generic.HashSet<GameObject>();
        float elapsed = 0f;

        // Libera rotação durante o giro
        rb.freezeRotation = false;

        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);

            // Aplica dano por área a cada frame (cada alvo só é atingido uma vez)
            Collider[] cols = Physics.OverlapSphere(transform.position, meleeAttackRange);
            foreach (Collider col in cols)
            {
                if (!col.CompareTag("Player")) continue;
                if (atingidos.Contains(col.gameObject)) continue;

                atingidos.Add(col.gameObject);
                PlayerHealth ph = col.GetComponent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(spinDamage, gameObject);
                    Debug.Log($"[DRAGON] Spin acertou Player! Dano={spinDamage}");
                }
            }
            yield return null;
        }

        // Restaura constraênt de rotação
        rb.freezeRotation = true;
        rb.angularVelocity = Vector3.zero;

        // ── Fase 3: Recovery — sobe de volta à altura normal ─────────
        float recoverTime = 0.3f;
        t = 0f;
        float yPos = transform.position.y;
        while (t < recoverTime)
        {
            t += Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(yPos, alturaFixa, t / recoverTime);
            rb.MovePosition(pos);
            yield return new WaitForFixedUpdate();
        }

        estaExecutandoSpin = false;
        estado = EstadoDragao.Idle;
        Debug.Log("[DRAGON] Spin Attack concluído. Retomando movimento.");
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Usa Vector3.Dot para detectar se o player está atrás do dragão.
    /// dot = 1  → player está na frente.
    /// dot = 0  → player está a 90° (lateral).
    /// dot = -1 → player está diretamente atrás.
    /// Retorna true se dot &lt; dotLimiarTras.
    /// </summary>
    private bool PlayerEstaTras()
    {
        Vector3 paraPlayer = (player.position - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, paraPlayer);
        return dot < dotLimiarTras;
    }

    /// <summary>Distância horizontal (ignora Y) entre o dragão e o player.</summary>
    private float DistanciaHorizontal()
    {
        Vector3 posA = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 posB = new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.Distance(posA, posB);
    }

    /// <summary>Direção normalizada do dragão → player, sem componente Y.</summary>
    private Vector3 DirecaoHorizontalParaPlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        return dir.normalized;
    }

    // ─────────────────────────────────────────────────────────────
    // Gizmos (editor only)
    // ─────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Zona de aproximação
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, distanciaAproximacao);

        // Distância ideal
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, distanciaIdeal);

        // Zona de fuga
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaFuga);

        // Raio da rabada
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioTail);

        // Raio do spin attack melee
        Gizmos.color = new Color(1f, 0.4f, 0f); // laranja
        Gizmos.DrawWireSphere(transform.position, meleeAttackRange);
    }
        public void Attack()
    {
        Instantiate(
            attackEffect,
            attackPoint.position,
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f)
        );
    }

}
