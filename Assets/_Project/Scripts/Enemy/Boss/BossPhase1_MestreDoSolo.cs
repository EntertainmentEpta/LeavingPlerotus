using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controlador da Fase 1 - Mestre do Solo
/// O Boss fica escondido em um Casulo.
/// Ele prende o jogador em Círculos (Pilares) ou Quadrados (Espinhos) e invoca mobs.
/// </summary>
[RequireComponent(typeof(BossController))]
public class BossPhase1_MestreDoSolo : MonoBehaviour
{
    [Header("Referências")]
    private BossController bossController;
    private BossPhase1_MobSpawner mobSpawner;
    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;
    private Rigidbody rb;
    private Collider bossCollider; 
    private List<Renderer> renderersDoBoss = new List<Renderer>();

    [Header("Prefabs dos Ataques")]
    public GameObject pilarPrefab;
    public GameObject espinhoPrefab;

    [Header("Visuais do Cristal (Fase 1)")]
    [Tooltip("Arraste o prefab do cristal")]
    public GameObject cristalPrefab;
    private GameObject cristalInstanciado;

    [Header("Configurações de Combate")]
    public float offsetAlturaFinal = -1.5f;
    public float tempoEntreAtaques = 1f;
    public float raioDaPrisao = 10f;
    public float profundidadeSpawn = 4f;
    public float tempoEmergindo = 0.5f;
    public float tempoVidaPrisao = 6f;
    public float distanciaRecuo = 5f;

    private bool phase1Ativa = false;
    private bool atacando = false;
    public bool Atacando => atacando;
    private List<GameObject> activePrisonObjects = new List<GameObject>();

    /// <summary>
    /// Verifica se já existem pilares de cristal ou espinhos ativos na cena.
    /// Se houver pelo menos um pilar/espinho, impede novas invocações sobrepostas.
    /// </summary>
    public bool ExistemPilaresNaCena()
    {
        // 1. Limpa e verifica a lista interna de instâncias criadas
        activePrisonObjects.RemoveAll(item => item == null);
        if (activePrisonObjects.Count > 0) return true;

        // 2. Busca componentes CrystalPillar ativos na cena
        CrystalPillar[] pilares = Object.FindObjectsByType<CrystalPillar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (pilares != null && pilares.Length > 0)
        {
            for (int i = 0; i < pilares.Length; i++)
            {
                if (pilares[i] != null && pilares[i].gameObject.activeInHierarchy)
                    return true;
            }
        }

        // 3. Busca componentes SpikeDamageDealer ativos na cena
        SpikeDamageDealer[] espinhos = Object.FindObjectsByType<SpikeDamageDealer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (espinhos != null && espinhos.Length > 0)
        {
            for (int i = 0; i < espinhos.Length; i++)
            {
                if (espinhos[i] != null && espinhos[i].gameObject.activeInHierarchy)
                    return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        bossController = GetComponent<BossController>();
        mobSpawner = GetComponent<BossPhase1_MobSpawner>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
        bossCollider = GetComponent<Collider>();

#if UNITY_EDITOR
        if (cristalPrefab == null)
        {
            cristalPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Boss/Teste_casulo.prefab");
        }
        if (pilarPrefab == null)
        {
            pilarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Boss/Pilar_Cristal_Base.prefab");
        }
        if (espinhoPrefab == null)
        {
            espinhoPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Boss/Espinhos_Cristal.prefab");
        }
#endif
        if (cristalPrefab == null)
        {
            cristalPrefab = Resources.Load<GameObject>("Teste_casulo") 
                         ?? Resources.Load<GameObject>("Enemies/Boss/Teste_casulo");
        }
        if (pilarPrefab == null)
        {
            pilarPrefab = Resources.Load<GameObject>("Pilar_Cristal_Base") 
                       ?? Resources.Load<GameObject>("Enemies/Boss/Pilar_Cristal_Base");
        }
        if (espinhoPrefab == null)
        {
            espinhoPrefab = Resources.Load<GameObject>("Espinhos_Cristal") 
                         ?? Resources.Load<GameObject>("Enemies/Boss/Espinhos_Cristal");
        }
        
        Renderer[] todosRenderers = GetComponentsInChildren<Renderer>(true);
        foreach(Renderer r in todosRenderers)
        {
            if (r != null && r.gameObject != gameObject && !(r is ParticleSystemRenderer)) 
            {
                renderersDoBoss.Add(r);
            }
        }
    }

    private void OnEnable()
    {
        BossEvents.OnPhaseChanged += ControlarFase;
        BossEvents.OnBossFightStarted += BuscarPlayer;
    }

    private void OnDisable()
    {
        BossEvents.OnPhaseChanged -= ControlarFase;
        BossEvents.OnBossFightStarted -= BuscarPlayer;
    }

    private void BuscarPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    public void ControlarFase(int novaFase)
    {
        if (novaFase == 1)
        {
            phase1Ativa = true;
            
            // 1. TÉCNICA DO FANTASMA: Esconde o modelo do Boss para que apenas o Casulo fique visível
            if (agent != null) agent.enabled = false; 
            if (rb != null) rb.isKinematic = true;
            if (bossCollider != null) bossCollider.enabled = false;
            foreach (Renderer r in renderersDoBoss) { if (r != null) r.enabled = false; }

            // Destroi casulo anterior se existir
            if (cristalInstanciado != null)
            {
                Destroy(cristalInstanciado);
                cristalInstanciado = null;
            }

            // Garante auto-carregamento do prefab do casulo
#if UNITY_EDITOR
            if (cristalPrefab == null)
            {
                cristalPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Boss/Teste_casulo.prefab");
            }
#endif
            if (cristalPrefab == null)
            {
                cristalPrefab = Resources.Load<GameObject>("Teste_casulo") ?? Resources.Load<GameObject>("Enemies/Boss/Teste_casulo");
            }

            // 2. Instancia o Casulo (Objeto separado com seu próprio colisor dedicado de 1.8m de raio)
            if (cristalPrefab != null)
            {
                cristalInstanciado = Instantiate(cristalPrefab, transform.position, transform.rotation);
                cristalInstanciado.name = "Casulo_Boss_Fase1";
                
                CapsuleCollider casuloCol = cristalInstanciado.GetComponent<CapsuleCollider>();
                if (casuloCol == null) casuloCol = cristalInstanciado.AddComponent<CapsuleCollider>();
                casuloCol.radius = 1.8f;
                casuloCol.height = 3.5f;
                casuloCol.center = new Vector3(0f, 1.75f, 0f);
                casuloCol.isTrigger = false; // SÓLIDO durante a Fase 1!

                Collider[] allCasuloCols = cristalInstanciado.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in allCasuloCols)
                {
                    if (c != casuloCol)
                    {
                        c.isTrigger = true;
                    }
                }

                // 3. PREPARA O CASULO E O GATILHO DE MORTE
                DummyHealth casuloHealth = cristalInstanciado.GetComponent<DummyHealth>();
                if (casuloHealth != null)
                {
                    if (bossController == null) bossController = GetComponent<BossController>() ?? GetComponentInParent<BossController>();
                    DummyHealth vidaDoBoss = (bossController != null) ? bossController.GetComponent<DummyHealth>() : GetComponent<DummyHealth>();
                    float limiteFase2 = (bossController != null && bossController.phaseConfig != null) ? bossController.phaseConfig.phase2Threshold : 0.7f;
                    
                    int maxHpBoss = (vidaDoBoss != null) ? vidaDoBoss.maxHealth : 500;
                    int danoNecessario = Mathf.RoundToInt(maxHpBoss * (1f - limiteFase2));
                    casuloHealth.maxHealth = Mathf.Max(50, danoNecessario);
                    casuloHealth.ResetHealth();

                    // Conecta com o script CristalCasulo se houver
                    CristalCasulo casuloScript = cristalInstanciado.GetComponent<CristalCasulo>();
                    if (casuloScript != null)
                    {
                        casuloScript.Setup(vidaDoBoss);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[BossPhase1_MestreDoSolo] ⚠️ cristalPrefab (Teste_casulo) não encontrado para instanciar!");
            }

            // Inicia rotina de ataques se a IA estiver ativa
            StopAllCoroutines();
            if (bossController == null || !bossController.OverrideMovement)
            {
                StartCoroutine(RotinaDeAtaquesFase1());
            }
            Debug.Log("[Fase 1] Mestre do Solo: Casulo Ativado!");
        }
        else
        {
            // FIM DA FASE 1
            phase1Ativa = false; 
            StopAllCoroutines();
            atacando = false;

            // DESTROI O CASULO CASO AINDA EXISTA NA CENA
            if (cristalInstanciado != null)
            {
                Destroy(cristalInstanciado);
                cristalInstanciado = null;
            }

            // Devolve o visual sólido para o Boss e ativa sua inteligência
            foreach (Renderer r in renderersDoBoss) { if (r != null) r.enabled = true; }
            if (bossCollider != null) bossCollider.enabled = true;
            if (rb != null) 
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }
            if (agent != null) 
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                if (agent.isOnNavMesh) agent.isStopped = false;
            }
            
            Debug.Log($"[Fase 1] Fim da Fase 1! Boss revelado para a Fase {novaFase}.");
        }
    }

    // =====================================================
    // MÁQUINA DE ATAQUES
    // =====================================================
    private IEnumerator RotinaDeAtaquesFase1()
    {
        yield return new WaitForSeconds(tempoEntreAtaques);

        while (phase1Ativa)
        {
            if (bossController != null && bossController.CanInitiateAction && !atacando)
            {
                int ataqueSorteado = Random.Range(0, 4);
                
                if (ataqueSorteado == 0)
                {
                    if (ExistemPilaresNaCena() || pilarPrefab == null)
                    {
                        yield return StartCoroutine(RecuarDoPlayer());
                    }
                    else
                    {
                        yield return StartCoroutine(Ataque_Prisao(pilarPrefab, false));
                        if (mobSpawner != null) mobSpawner.SpawnWave(BossPhase1_MobSpawner.WaveType.PostPrison_Pillar);
                    }
                }
                else if (ataqueSorteado == 1)
                {
                    if (ExistemPilaresNaCena() || espinhoPrefab == null)
                    {
                        yield return StartCoroutine(RecuarDoPlayer());
                    }
                    else
                    {
                        yield return StartCoroutine(Ataque_Prisao(espinhoPrefab, true));
                        if (mobSpawner != null) mobSpawner.SpawnWave(BossPhase1_MobSpawner.WaveType.PostPrison_Spike);
                    }
                }
                else if (ataqueSorteado == 2)
                {
                    // Combo Tático Inteligente: Prisão Esmagadora (Trap & Stomp)
                    bossController.ExecuteTrapAndStompCombo();
                    while (bossController.isExecutingCombo || !bossController.CanInitiateAction)
                    {
                        yield return null;
                    }
                }
                else
                {
                    yield return StartCoroutine(RecuarDoPlayer());
                }
            }
            yield return new WaitForSeconds(tempoEntreAtaques);
        }
    }

    // =====================================================
    // LÓGICA DE GERAR AS PRISÕES (CÍRCULO OU QUADRADO)
    // =====================================================
    private IEnumerator Ataque_Prisao(GameObject prefabObstaculo, bool formatoQuadrado, bool bypassPillarsCheck = false)
    {
        // Se o prefab for nulo ou se já houver pilares/espinhos ativos na arena (sem bypass), cancela
        if (prefabObstaculo == null || (!bypassPillarsCheck && ExistemPilaresNaCena()))
        {
            yield break;
        }

        if (bypassPillarsCheck)
        {
            activePrisonObjects.RemoveAll(item => item == null);
            foreach (var old in activePrisonObjects)
            {
                if (old != null) Destroy(old);
            }
            activePrisonObjects.Clear();
        }

        atacando = true;

        // Congela 100% a movimentação do Boss durante a conjuração
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (animator != null)
        {
            animator.ResetTrigger("Spell");
            animator.SetTrigger("Spell");
            animator.Play("SpellGround", 0, 0f);
        }

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
        
        if (playerTransform != null)
        {
            Vector3 direcaoOlhar = (playerTransform.position - transform.position).normalized;
            direcaoOlhar.y = 0;
            if (direcaoOlhar.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direcaoOlhar);
        }

        yield return new WaitForSeconds(0.25f);

        List<Vector3> posicoesFinais = new List<Vector3>();
        Vector3 centro = playerTransform != null ? playerTransform.position : (transform.position + transform.forward * 4f);

        if (!formatoQuadrado)
        {
            int qtdPilares = 8;
            for (int i = 0; i < qtdPilares; i++)
            {
                float angulo = i * Mathf.PI * 2 / qtdPilares;
                Vector3 pos = centro + new Vector3(Mathf.Cos(angulo), 0, Mathf.Sin(angulo)) * raioDaPrisao;
                posicoesFinais.Add(pos);
            }
        }
        else
        {
            Vector3[] direcoesQuadrado = new Vector3[] {
                new Vector3(-1, 0,  1), new Vector3(0, 0,  1), new Vector3(1, 0,  1), 
                new Vector3(-1, 0,  0),                        new Vector3(1, 0,  0), 
                new Vector3(-1, 0, -1), new Vector3(0, 0, -1), new Vector3(1, 0, -1)  
            };

            foreach (Vector3 dir in direcoesQuadrado)
            {
                posicoesFinais.Add(centro + (dir * (raioDaPrisao * 0.8f))); 
            }
        }

        List<Transform> objetosCriados = new List<Transform>();
        for (int i = 0; i < posicoesFinais.Count; i++)
        {
            Vector3 posFinal = posicoesFinais[i];
            posFinal.y = transform.position.y + offsetAlturaFinal; 

            Vector3 posSubsolo = posFinal + (Vector3.down * profundidadeSpawn);
            
            if (prefabObstaculo != null)
            {
                Quaternion rotacao = Quaternion.LookRotation(centro - posFinal);
                GameObject obj = Instantiate(prefabObstaculo, posSubsolo, rotacao);
                objetosCriados.Add(obj.transform);
                activePrisonObjects.Add(obj);
                
                Destroy(obj, tempoVidaPrisao);
            }
        }

        yield return StartCoroutine(ErguerObjetosDoChao(objetosCriados, posicoesFinais));

        // Mantém o Boss congelado até a animação de conjuração SpellGround finalizar completamente (~1.80s total)
        yield return new WaitForSeconds(0.85f);

        if (agent != null && agent.enabled && agent.isOnNavMesh && bossController != null && bossController.CanInitiateAction && !bossController.OverrideMovement)
            agent.isStopped = false;

        atacando = false;
    }

    private IEnumerator ErguerObjetosDoChao(List<Transform> objetos, List<Vector3> posicoesFinais)
    {
        float tempoDecorrido = 0f;
        List<Vector3> posicoesIniciais = new List<Vector3>();
        
        foreach (var obj in objetos)
        {
            if (obj != null) posicoesIniciais.Add(obj.position);
            else posicoesIniciais.Add(Vector3.zero);
        }

        while (tempoDecorrido < tempoEmergindo)
        {
            tempoDecorrido += Time.deltaTime;
            
            float t = Mathf.Clamp01(tempoDecorrido / tempoEmergindo);
            float curvaSobeSutil = Mathf.Sin(t * Mathf.PI * 0.5f);

            for (int i = 0; i < objetos.Count; i++)
            {
                if (objetos[i] != null) 
                {
                    Vector3 alvoFinal = posicoesFinais[i];
                    alvoFinal.y = transform.position.y + offsetAlturaFinal;
                    objetos[i].position = Vector3.Lerp(posicoesIniciais[i], alvoFinal, curvaSobeSutil);
                }
            }
            yield return null;
        }
    }

    // =====================================================
    // IA DE FUGA (KITING)
    // =====================================================
    private IEnumerator RecuarDoPlayer()
    {
        if (bossController != null && (bossController.IsStunned || bossController.IsDead)) yield break;

        if (bossController != null) bossController.OverrideMovement = true;

        try
        {
            atacando = true;

            Vector3 direcaoOposta = (transform.position - (playerTransform != null ? playerTransform.position : transform.position)).normalized;
            direcaoOposta.y = 0;

            if (direcaoOposta.sqrMagnitude < 0.1f) direcaoOposta = transform.forward;

            Vector3 pontoDeFuga = transform.position + (direcaoOposta * distanciaRecuo);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(pontoDeFuga, out hit, 10f, NavMesh.AllAreas))
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                }

                yield return null;

                float tempoCorrendo = 0f;
                
                while (agent != null && agent.enabled && agent.isOnNavMesh && (agent.pathPending || agent.remainingDistance > 1.5f))
                {
                    tempoCorrendo += Time.deltaTime;

                    if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude);

                    if (tempoCorrendo > 2.5f || (tempoCorrendo > 0.5f && agent.velocity.sqrMagnitude < 0.1f)) 
                    {
                        Debug.Log("[Fase 1] Boss desistiu de fugir (travou ou demorou).");
                        break; 
                    }
                    
                    yield return null;
                }
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;
            if (animator != null) animator.SetFloat("Speed", 0f);
            
            if (playerTransform != null)
            {
                Vector3 olharPlayer = (playerTransform.position - transform.position).normalized;
                olharPlayer.y = 0;
                if (olharPlayer.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(olharPlayer);
            }
        }
        finally 
        {
            if (bossController != null) bossController.OverrideMovement = false;
            atacando = false; 
        }
    }

    public void RevidarAtaque()
    {
        if (phase1Ativa && !atacando && bossController != null && !bossController.IsStunned && !bossController.IsDead)
        {
            if (ExistemPilaresNaCena()) return;

            if (Random.Range(0, 2) == 0)
                StartCoroutine(Ataque_Prisao(pilarPrefab, false));
            else
                StartCoroutine(Ataque_Prisao(espinhoPrefab, true));
        }
    }

    /// <summary>
    /// Invoca uma prisão de pilares forçadamente, independente da fase ativa.
    /// Chamado pela BossPhase2_Refraction ou combos da IA.
    /// </summary>
    public void InvocarPrisaoForado()
    {
        InvocarPrisaoForcado(false);
    }

    public bool InvocarPrisaoForcado(bool bypassActionCheck = false, bool forceClearOld = false)
    {
        if (atacando && !forceClearOld) return false;
        if (!bypassActionCheck && (bossController == null || !bossController.CanInitiateAction)) return false;
        if (!forceClearOld && ExistemPilaresNaCena()) return false;

#if UNITY_EDITOR
        if (pilarPrefab == null)
            pilarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Boss/Pilar_Cristal_Base.prefab");
        if (espinhoPrefab == null)
            espinhoPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Enemies/Boss/Espinhos_Cristal.prefab");
#endif
        if (pilarPrefab == null) pilarPrefab = Resources.Load<GameObject>("Pilar_Cristal_Base") ?? Resources.Load<GameObject>("Enemies/Boss/Pilar_Cristal_Base");
        if (espinhoPrefab == null) espinhoPrefab = Resources.Load<GameObject>("Espinhos_Cristal") ?? Resources.Load<GameObject>("Enemies/Boss/Espinhos_Cristal");

        // Sorteia entre pilares ou espinhos
        if (pilarPrefab != null || espinhoPrefab != null)
        {
            GameObject prefab = (pilarPrefab != null && espinhoPrefab != null)
                ? (Random.Range(0, 2) == 0 ? pilarPrefab : espinhoPrefab)
                : (pilarPrefab != null ? pilarPrefab : espinhoPrefab);

            if (prefab == null) return false;

            bool quadrado = (prefab == espinhoPrefab);
            StartCoroutine(Ataque_Prisao(prefab, quadrado, bypassPillarsCheck: forceClearOld));
            return true;
        }
        return false;
    }
}