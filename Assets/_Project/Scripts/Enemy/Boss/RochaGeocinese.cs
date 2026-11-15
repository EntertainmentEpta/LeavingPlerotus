using System.Collections;
using UnityEngine;

/// <summary>
/// Rocha/Cristal de Geocinese que orbita o casulo do Boss na Fase 1 (a partir de 50% HP).
/// Regras de Acionamento:
///  1. É acionado ESTRITAMENTE quando recebe um golpe da ARMA do jogador (não por contato de corpo).
///  2. Ao receber o hit, aplica no jogador o MESMO knockback do casulo (zera linearVelocity, AddForce com direcao.y = 0.2f).
///  3. Aguarda EXATAMENTE 3.0 SEGUNDOS (carregando/faiscando no ar).
///  4. Exatos 3 segundos depois, lança-se em alta velocidade na direção do jogador, causando dano AoE no impacto!
/// </summary>
[RequireComponent(typeof(DummyHealth))]
public class RochaGeocinese : MonoBehaviour
{
    [Header("Knockback do Orbe ao Ser Atingido")]
    [Tooltip("Força do empurrão aplicado no player ao rebater/golpear o orbe. Ajustável no Inspector!")]
    public float forcaDoKnockback = 8.0f;
    public float tempoDeEsperaAntesDoDisparo = 2.0f;

    [Header("Disparo & Dano AoE")]
    public float velocidadeArremesso = 38f;
    public int danoExplosaoAoE = 22;
    public float raioExplosaoAoE = 3.6f;

    [Header("VFX")]
    public GameObject hitVFXPrefab;
    public GameObject vfxKnockbackPrefab;
    public GameObject destruicaoVFXPrefab;

    [Header("Configuração de Órbita")]
    public float alturaDoChao = 2.2f;
    public float raioOrbita = 4.2f;

    [Header("Rotação Coordenada Rítmica")]
    public bool usarRitmoCoordenado = true;
    public float velocidadeMin = 35f;   // Janela LENTA (Player ataca!)
    public float velocidadeMax = 220f;  // Janela RÁPIDA (Escudo fecha!)
    public float frequenciaRitmo = 1.2f;

    private float anguloOrbita = 0f;
    private float velocidadeAtual = 120f;
    private Transform centroCasulo;
    private CristalCasulo casuloPai;
    private DummyHealth dummyHealth;

    private bool estaOrbitando = true;
    private bool jaFoiAtingido = false;

    private void Awake()
    {
        GarantirComponentesDeColisao();
    }

    /// <summary>
    /// Método universal de dano chamado por qualquer arma ou ataque do jogador.
    /// </summary>
    public void TakeDamage(int damage, bool isCritical = false)
    {
        ProcessarHitDaArma(null);
    }

    public void ReceberDano(int damage)
    {
        ProcessarHitDaArma(null);
    }

    private void GarantirComponentesDeColisao()
    {
        Collider col = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
        if (col == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.85f;
            sc.isTrigger = true;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void ConfigurarOrbita(CristalCasulo casulo, float anguloInicial, float raio, float altura, float velMin = 35f, float velMax = 220f)
    {
        casuloPai = casulo;
        centroCasulo = casulo != null ? casulo.transform : null;
        anguloOrbita = anguloInicial;
        raioOrbita = raio > 0f ? raio : 4.2f;
        alturaDoChao = altura > 0f ? altura : 2.2f;
        velocidadeMin = velMin;
        velocidadeMax = velMax;
        estaOrbitando = true;
        jaFoiAtingido = false;
        AtualizarPosicaoOrbita();
    }

    private void Update()
    {
        if (!estaOrbitando || jaFoiAtingido) return;

        if (centroCasulo == null)
        {
            Destroy(gameObject);
            return;
        }

        // Rotação Coordenada Rítmica
        if (usarRitmoCoordenado)
        {
            float t = (Mathf.Sin(Time.time * frequenciaRitmo) + 1f) * 0.5f;
            velocidadeAtual = Mathf.Lerp(velocidadeMin, velocidadeMax, t);
        }

        anguloOrbita += velocidadeAtual * Time.deltaTime;
        AtualizarPosicaoOrbita();
    }

    private void AtualizarPosicaoOrbita()
    {
        if (centroCasulo == null) return;
        float rad = anguloOrbita * Mathf.Deg2Rad;
        Vector3 novaPos = centroCasulo.position + new Vector3(Mathf.Cos(rad) * raioOrbita, alturaDoChao, Mathf.Sin(rad) * raioOrbita);
        transform.position = novaPos;
        transform.Rotate(Vector3.up * 180f * Time.deltaTime);
    }



    private void OnTriggerEnter(Collider other)
    {
        if (other == null || jaFoiAtingido) return;

        // Detecta qualquer golpe ou arma do jogador
        bool ehArma = other.GetComponent<WeaponHitbox>() != null 
                   || other.CompareTag("Weapon")
                   || other.CompareTag("Player") 
                   || (other.transform.root != null && other.transform.root.CompareTag("Player"));

        if (ehArma)
        {
            ProcessarHitDaArma(other.gameObject);
        }
    }

    public void ProcessarHitDaArma(GameObject causa)
    {
        if (jaFoiAtingido) return;
        jaFoiAtingido = true;
        estaOrbitando = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Aplica repulsão e empurrão instantâneo (SEM CAUSAR DANO AO PLAYER)
            AplicarKnockbackRepulso(player);
        }

        // Inicia a contagem de tempo de carga antes de lançar o meteoro de cristal no player
        StartCoroutine(RotinaContagemEArremesso(player));
    }

    /// <summary>
    /// Repulsa / Knockback instantâneo ao bater no Orbe:
    /// Expulsa o player na hora com onda de choque visual (SEM DANO).
    /// </summary>
    private void AplicarKnockbackRepulso(GameObject player)
    {
        Vector3 direcaoEmpurrao = (player.transform.position - transform.position);
        direcaoEmpurrao.y = 0.25f;

        Rigidbody rbPlayer = player.GetComponent<Rigidbody>() ?? player.GetComponentInParent<Rigidbody>();
        if (rbPlayer != null && !rbPlayer.isKinematic)
        {
            rbPlayer.linearVelocity = new Vector3(rbPlayer.linearVelocity.x * 0.2f, 0f, rbPlayer.linearVelocity.z * 0.2f);
            rbPlayer.AddForce(direcaoEmpurrao.normalized * forcaDoKnockback, ForceMode.Impulse);

            // Explosão de repulsão visual imediata (Ciano/Místico)
            VFX_BossShockwave.CriarEfeitoOndaDeChoque(transform.position, 3.5f, new Color(0f, 0.9f, 1f, 1f), new Color(0.8f, 0.2f, 1f, 0f), 0.25f, 1.0f);
        }
    }

    /// <summary>
    /// Aguarda 2.2 segundos tremendo no ar e depois se lança velozmente como meteoro/estrela no jogador!
    /// Só quebra quando bate no chão ou no player!
    /// </summary>
    private IEnumerator RotinaContagemEArremesso(GameObject player)
    {
        Vector3 posCarga = transform.position;
        float elapsed = 0f;
        float tempoDeCarga = 0.80f; // Tempo de carga ágil e dinâmico

        // Carga tremeluzente no ar
        while (elapsed < tempoDeCarga)
        {
            elapsed += Time.deltaTime;
            transform.position = posCarga + Random.insideUnitSphere * 0.15f;
            yield return null;
        }

        // Posição de destino elevada a 1.0m de altura para NUNCA clipar no chão!
        Vector3 alvo = player != null ? player.transform.position + Vector3.up * 1.0f : transform.position + transform.forward * 10f;
        Vector3 direcaoDisparo = (alvo - transform.position).normalized;

        float tempoVoo = 0f;
        float velocidadeFinal = 38f;

        while (tempoVoo < 1.5f)
        {
            tempoVoo += Time.deltaTime;
            
            // Perseguição inteligente (Homing): curva em direção ao jogador em alta velocidade
            if (player != null)
            {
                alvo = player.transform.position + Vector3.up * 1.0f;
                Vector3 targetDir = (alvo - transform.position).normalized;
                direcaoDisparo = Vector3.RotateTowards(direcaoDisparo, targetDir, 3.5f * Time.deltaTime, 0f);
            }

            // Avança pelo ar apontando para a direção de voo sem afundar no solo
            transform.position += direcaoDisparo * velocidadeFinal * Time.deltaTime;
            if (direcaoDisparo != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direcaoDisparo);
            }

            // Checa impacto por proximidade ou choque com o solo
            if (Vector3.Distance(transform.position, alvo) <= 1.2f || transform.position.y <= 0.3f)
            {
                break;
            }
            yield return null;
        }

        ExplodirAoE();
    }

    private void ExplodirAoE()
    {
        Vector3 posExplosao = transform.position;

        // Onda de choque e estilhaço no solo estilo Star Mob
        VFX_BossShockwave.CriarEfeitoOndaDeChoque(posExplosao, raioExplosaoAoE * 1.5f, new Color(0.2f, 1f, 0.5f, 1f), new Color(1f, 0.1f, 0.8f, 0f), 0.35f, 1.2f);

        if (destruicaoVFXPrefab != null)
        {
            Instantiate(destruicaoVFXPrefab, posExplosao, Quaternion.identity);
        }

        // Dano AoE e repulsão APENAS no momento exato em que o meteoro quebra no chão!
        Collider[] hits = Physics.OverlapSphere(posExplosao, raioExplosaoAoE);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player") || (hit.transform.root != null && hit.transform.root.CompareTag("Player")))
            {
                PlayerHealth ph = hit.GetComponent<PlayerHealth>() ?? hit.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(danoExplosaoAoE, gameObject);
                }
            }
        }

        // Notifica o casulo para regenerar um novo orbe após 4 segundos
        if (casuloPai != null)
        {
            casuloPai.AgendarRegeneracaoDeOrbe(2.5f);
        }

        Destroy(gameObject);
    }
}
