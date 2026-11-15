using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador do Casulo do Boss (Fase 1 - Mestre do Solo).
///  • 100% a 50% HP: Casulo limpo e fixo no solo.
///  • 50% HP (Mini-Fase dos Orbes): Invoca os Orbes de Geocinese.
///  • Se a arma do player acerta um Orbe:
///      1. O Orbe repele o player imediatamente com Knockback.
///      2. O Orbe se desacopla e é ARREMMASSADO em alta velocidade contra o jogador!
///      3. Explode em Área (AoE) exigindo Dash rápido para esquiva.
///      4. Um novo Orbe é regenerado do solo após 4 segundos.
/// </summary>
[RequireComponent(typeof(DummyHealth))]
public class CristalCasulo : MonoBehaviour
{
    [Header("Estágios Visuais (Malhas 3D)")]
    [Tooltip("Arraste todos os modelos 3D (Meshes) em ordem de estágio (do mais intacto ao mais destruído) aqui!")]
    public Mesh[] malhasVisuais;

    [Header("Configuração de Colisão")]
    [Tooltip("Tamanho/raio do colisor físico para impedir que o player atravesse por dentro do casulo.")]
    public float raioColisor = 0.45f;
    public float alturaColisor = 2.8f;

    [Header("Visual Interno do Boss (Fake Agachado)")]
    [Tooltip("Objeto visual/modelo do Boss agachado visível dentro do casulo transparente.")]
    public GameObject bossInternoModel;
    [Tooltip("Animator do modelo do Boss agachado dentro do casulo.")]
    public Animator bossInternoAnimator;
    [Tooltip("Nome da animação/estado agachado (ex: Crouch, Agachado, Idle).")]
    public string animacaoAgachadaState = "Crouch";

    [Header("Escudo de Geocinese (Ativado a 50% HP)")]
    [Tooltip("Prefab da rocha/cristal que orbita em volta do casulo.")]
    public GameObject rochaOrbitantePrefab;
    public int quantidadeRochasGeocinese = 4;
    [Tooltip("Raio da órbita das rochas ao redor do casulo.")]
    public float raioOrbitaRochas = 4.2f;
    [Tooltip("Altura em que as rochas flutuam em relação ao solo.")]
    public float alturaDoChaoRochas = 2.2f;
    [Tooltip("Velocidade mínima de rotação (Janela LENTA de ataque do player).")]
    public float velocidadeOrbitaMinima = 35f;
    [Tooltip("Velocidade máxima de rotação (Janela RÁPIDA de escudo).")]
    public float velocidadeOrbitaMaxima = 220f;

    [Header("Efeitos Visuais (VFX)")]
    [Tooltip("Prefab do efeito de impacto ao receber dano.")]
    public GameObject vfxKnockbackPrefab;
    [Tooltip("Prefab do efeito de luz/estilhaçamento no chão durante a destruição")]
    public GameObject vfxMortePrefab;

    private DummyHealth meuHealth; 
    private DummyHealth bossHealth; 
    
    private MeshFilter meuMeshFilter; 
    private MeshCollider meuMeshCollider;
    private Collider meuColisorPrincipal;

    private int indiceAtual = -1;
    private int vidaAnterior;
    private GameObject playerNaCena;
    private bool estaMorto = false;

    // Threshold de Vida (50% HP)
    private bool reacao50Ativada = false;
    private List<RochaGeocinese> rochasGeocineseInstanciadas = new List<RochaGeocinese>();

    private void Awake()
    {
        meuHealth = GetComponent<DummyHealth>();
        meuMeshFilter = GetComponentInChildren<MeshFilter>();
        if (meuMeshFilter != null)
        {
            meuMeshCollider = meuMeshFilter.GetComponent<MeshCollider>();
        }
        if (meuMeshCollider == null)
        {
            meuMeshCollider = GetComponent<MeshCollider>();
        }

        GarantirColisorFisico();
    }

    private void Start()
    {
        if (meuHealth != null)
        {
            vidaAnterior = meuHealth.CurrentHealth;
            meuHealth.onDeathOverride += IniciarMorte; 
        }

        playerNaCena = GameObject.FindGameObjectWithTag("Player");
        GarantirColisorFisico();

        // Busca automática do modelo visual fake do Boss agachado se não estiver arrastado no Inspector
        if (bossInternoModel == null)
        {
            Transform foundFake = transform.Find("BossInterno") ?? transform.Find("BossFake") ?? transform.Find("Boss_Crouch");
            if (foundFake != null) bossInternoModel = foundFake.gameObject;
        }
        if (bossInternoAnimator == null && bossInternoModel != null)
        {
            bossInternoAnimator = bossInternoModel.GetComponent<Animator>() ?? bossInternoModel.GetComponentInChildren<Animator>();
        }
        if (bossInternoAnimator != null && !string.IsNullOrEmpty(animacaoAgachadaState))
        {
            bossInternoAnimator.applyRootMotion = false;
            bossInternoAnimator.Play(animacaoAgachadaState);
        }

        AtualizarVisual();
    }

    private void Update()
    {
        if (estaMorto) return; 

        AtualizarVisual();
        VerificarDano();
    }

    private void VerificarDano()
    {
        if (meuHealth == null) return;

        // Auto-sincroniza vidaAnterior para evitar desincronização ao resetar a vida do casulo
        if (vidaAnterior <= 0 || vidaAnterior > meuHealth.maxHealth || meuHealth.CurrentHealth > vidaAnterior)
        {
            vidaAnterior = meuHealth.CurrentHealth;
        }

        if (meuHealth.CurrentHealth < vidaAnterior)
        {
            int danoTomado = vidaAnterior - meuHealth.CurrentHealth;
            
            if (bossHealth == null)
            {
                BossController bossCtrl = GetComponentInParent<BossController>() ?? FindObjectOfType<BossController>();
                if (bossCtrl != null) bossHealth = bossCtrl.GetComponent<DummyHealth>();
            }

            if (bossHealth != null)
            {
                bossHealth.TakeDamage(danoTomado); 
            }

            if (vfxKnockbackPrefab != null)
            {
                Instantiate(vfxKnockbackPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
            }

            vidaAnterior = meuHealth.CurrentHealth;

            // Verifica threshold de 50% de vida para ativar a mini-fase dos Orbes
            VerificarThresholdsDeVida();
        }
    }

    private void VerificarThresholdsDeVida()
    {
        if (meuHealth == null || meuHealth.maxHealth <= 0) return;

        float pct = (float)meuHealth.CurrentHealth / meuHealth.maxHealth;

        // 50% HP -> Ativa o Escudo de Geocinese (Orbes Orbitantes de Contra-Ataque)
        if (pct <= 0.75f && !reacao50Ativada)
        {
            reacao50Ativada = true;
            Debug.Log("🪨 [CASULO BOSS] Threshold 75% de vida: Mini-Fase dos Orbes de Geocinese Ativada!");
            AtivarEscudoGeocinese();
        }
    }

    /// <summary>
    /// Instancia rochas de cristal do solo que giram como escudo orbital em volta do casulo.
    /// </summary>
    public void AtivarEscudoGeocinese()
    {
        for (int i = 0; i < quantidadeRochasGeocinese; i++)
        {
            float angulo = i * (360f / quantidadeRochasGeocinese);
            CriarOuSubstituirOrbe(angulo);
        }
    }

    private void CriarOuSubstituirOrbe(float angulo)
    {
        GameObject objRocha = null;

        if (rochaOrbitantePrefab != null)
        {
            objRocha = Instantiate(rochaOrbitantePrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // Fallback Procedural de Rocha de Geocinese
            objRocha = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objRocha.name = $"RochaGeocinese_Procedural_{Random.Range(100, 999)}";
            objRocha.transform.localScale = new Vector3(0.8f, 1.4f, 0.8f);

            Renderer r = objRocha.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.1f, 0.6f, 0.3f, 1.0f);
            }
        }

        RochaGeocinese rochaScript = objRocha.GetComponent<RochaGeocinese>();
        if (rochaScript == null) rochaScript = objRocha.AddComponent<RochaGeocinese>();

        rochaScript.ConfigurarOrbita(this, angulo, raioOrbitaRochas, alturaDoChaoRochas, velocidadeOrbitaMinima, velocidadeOrbitaMaxima);
        rochasGeocineseInstanciadas.Add(rochaScript);
    }

    /// <summary>
    /// Chamado pelo Orbe quando ele é arremessado e destruído para recriar um novo orbe do solo após alguns segundos.
    /// </summary>
    public void AgendarRegeneracaoDeOrbe(float delaySegundos)
    {
        if (estaMorto || !reacao50Ativada) return;
        StartCoroutine(RotinaRegenerarOrbe(delaySegundos));
    }

    private IEnumerator RotinaRegenerarOrbe(float delaySegundos)
    {
        yield return new WaitForSeconds(delaySegundos);
        if (!estaMorto && reacao50Ativada)
        {
            float anguloAleatorio = Random.Range(0f, 360f);
            CriarOuSubstituirOrbe(anguloAleatorio);
        }
    }

    public void DestruirEscudoGeocinese()
    {
        foreach (RochaGeocinese r in rochasGeocineseInstanciadas)
        {
            if (r != null && r.gameObject != null) Destroy(r.gameObject);
        }
        rochasGeocineseInstanciadas.Clear();
    }

    private void GarantirColisorFisico()
    {
        // Respeita e preserva o colisor original configurado no prefab Teste_casulo (ex: BoxCollider fixed)
        Collider existingCol = GetComponent<Collider>();
        if (existingCol != null)
        {
            meuColisorPrincipal = existingCol;
            Debug.Log($"[CristalCasulo] 🛡️ Preservado colisor original do prefab Teste_casulo: {existingCol.GetType().Name}");
            return;
        }

        // Fallback: Se não houver nenhum colisor no prefab, adiciona um CapsuleCollider padrão
        CapsuleCollider rootCapsule = gameObject.AddComponent<CapsuleCollider>();
        rootCapsule.radius = 1.8f;
        rootCapsule.height = 3.5f;
        rootCapsule.center = new Vector3(0f, 1.75f, 0f);
        rootCapsule.isTrigger = false;
        meuColisorPrincipal = rootCapsule;
    }

    public void Setup(DummyHealth healthDoBoss)
    {
        bossHealth = healthDoBoss;
        if (meuHealth != null)
        {
            vidaAnterior = meuHealth.CurrentHealth;
        }
    }

    private void AtualizarVisual()
    {
        if (meuHealth == null || malhasVisuais == null || malhasVisuais.Length == 0 || meuMeshFilter == null) return;
        if (meuHealth.maxHealth <= 0) return;

        float porcentagemVida = (float)meuHealth.CurrentHealth / meuHealth.maxHealth;
        float danoTomado = 1f - porcentagemVida;
        
        int novoIndice = Mathf.FloorToInt(danoTomado * malhasVisuais.Length);
        novoIndice = Mathf.Clamp(novoIndice, 0, malhasVisuais.Length - 1);

        if (novoIndice != indiceAtual)
        {
            if (malhasVisuais[novoIndice] != null)
            {
                meuMeshFilter.mesh = malhasVisuais[novoIndice];

                if (meuMeshCollider != null)
                {
                    meuMeshCollider.sharedMesh = malhasVisuais[novoIndice];
                    meuMeshCollider.convex = true;
                }
            }
            indiceAtual = novoIndice;
        }
    }

    private void IniciarMorte()
    {
        if (estaMorto) return;
        estaMorto = true;

        DestruirEscudoGeocinese();

        Collider col = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
        if (col != null) col.enabled = false;

        if (bossInternoModel != null)
        {
            Destroy(bossInternoModel);
        }

        int danoFatal = 0;
        if (meuHealth != null)
        {
            danoFatal = vidaAnterior - meuHealth.CurrentHealth;
            vidaAnterior = meuHealth.CurrentHealth; 
        }
        if (bossHealth != null && danoFatal > 0)
        {
            bossHealth.TakeDamage(danoFatal);
        }

        if (vfxMortePrefab != null)
        {
            Instantiate(vfxMortePrefab, transform.position + Vector3.up * 1.0f, Quaternion.identity);
        }

        // 🍃 Dispara a explosão de folhas e Impact Frame da Vefects na quebra do casulo
        VFXManager.Play(VFXType.CocoonLeavesBurst, transform.position + Vector3.up * 1.5f, Quaternion.identity);
        VFXManager.Play(VFXType.ImpactFrame, transform.position + Vector3.up * 1.5f, Quaternion.identity);

        if (meuMeshFilter != null && meuMeshFilter.gameObject != null)
        {
            Destroy(meuMeshFilter.gameObject, 0.2f);
        }
        Destroy(gameObject, 0.3f);
    }
} 