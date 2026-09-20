using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Ferramenta de Testes e Debug do Boss Cromático (Bioma 1).
/// 100% Controlado via Botões Personalizados no Inspector do Unity!
/// Separado por Fase 1, Fase 2 e Fase 3.
/// </summary>
public class BossAnimationTester : MonoBehaviour
{
    [Header("1. Controle de Modo de Teste")]
    [Tooltip("Se FALSO (padrão), o Boss desativa perseguição/ataques automáticos e fica em modo Sandbox 100% parado para testes. Se VERDADEIRO, o Boss luta normalmente.")]
    public bool enableBossAI = false;

    [Tooltip("Se ativado no modo Sandbox, o Boss vira suavemente para encarar o Player para você visualizar os ataques de frente.")]
    public bool alwaysFacePlayer = true;

    [Tooltip("Velocidade de rotação suave em direção ao Player.")]
    public float lookAtPlayerSpeed = 10.0f;

    [Header("2. Referências")]
    public BossController bossController;
    public Animator animator;

    private NavMeshAgent navAgent;
    private bool lastAIState = true;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    public void FindReferences()
    {
        if (bossController == null) bossController = GetComponent<BossController>() ?? GetComponentInParent<BossController>();
        if (bossController != null)
        {
            if (bossController.visualPhase3 != null && bossController.visualPhase3.activeInHierarchy)
            {
                animator = bossController.visualPhase3.GetComponentInChildren<Animator>(true);
            }
            else if (bossController.visualPhase2 != null && bossController.visualPhase2.activeInHierarchy)
            {
                animator = bossController.visualPhase2.GetComponentInChildren<Animator>(true);
            }
            else
            {
                animator = bossController.animator;
            }
        }
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>() ?? GetComponentInParent<NavMeshAgent>();
    }

    private void Start()
    {
        FindReferences();
        ApplyAIMode(true);
    }

    private void Update()
    {
        if (lastAIState != enableBossAI)
        {
            ApplyAIMode(false);
        }

        // No modo sandbox, garante que o NavMesh continue congelado e não ande
        if (!enableBossAI)
        {
            if (bossController != null) bossController.OverrideMovement = true;
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
            }

            // Gira o Boss suavemente para encarar o Player
            if (alwaysFacePlayer)
            {
                FacePlayerSmoothly();
            }
        }
    }

    public void FacePlayerSmoothly()
    {
        Transform bossTransform = (bossController != null) ? bossController.transform : transform;
        Transform player = null;

        if (bossController != null && bossController.playerTransform != null && bossController.playerTransform.gameObject.activeInHierarchy)
        {
            player = bossController.playerTransform;
        }
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else
            {
                PlayerHealth ph = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
                if (ph != null) player = ph.transform;
                else
                {
                    PlayerM pm = UnityEngine.Object.FindFirstObjectByType<PlayerM>();
                    if (pm != null) player = pm.transform;
                }
            }
        }

        if (player == null) return;

        Vector3 dir = (player.position - bossTransform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            bossTransform.rotation = Quaternion.Slerp(bossTransform.rotation, targetRot, Time.deltaTime * lookAtPlayerSpeed);
        }
    }

    /// <summary>
    /// Aplica o estado de IA (Pausa perseguição e magias automáticas no modo Sandbox)
    /// </summary>
    public void ApplyAIMode(bool force = false)
    {
        FindReferences();
        lastAIState = enableBossAI;

        if (bossController != null)
        {
            bossController.OverrideMovement = !enableBossAI;
        }

        // Desativa apenas os scripts autônomos de fases e magias automáticas no modo Sandbox
        var phase1 = GetComponent<BossPhase1_MestreDoSolo>() ?? GetComponentInChildren<BossPhase1_MestreDoSolo>();
        if (phase1 != null) { phase1.StopAllCoroutines(); }

        var spawner = GetComponent<BossPhase1_MobSpawner>() ?? GetComponentInChildren<BossPhase1_MobSpawner>();
        if (spawner != null) { spawner.StopAllCoroutines(); spawner.enabled = enableBossAI; }

        var phase2 = GetComponent<BossPhase2_Refraction>() ?? GetComponentInChildren<BossPhase2_Refraction>();
        if (phase2 != null) { phase2.StopAllCoroutines(); }

        var puddles = GetComponent<AcidPuddleSpawner>() ?? GetComponentInChildren<AcidPuddleSpawner>();
        if (puddles != null) { puddles.StopAllCoroutines(); puddles.enabled = enableBossAI; }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = !enableBossAI;
            if (!enableBossAI) navAgent.velocity = Vector3.zero;
        }

        if (animator != null)
        {
            animator.enabled = true;
            if (!enableBossAI)
            {
                animator.SetBool("IsWalking", false);
                animator.SetBool("isSprinting", false);
                animator.SetFloat("Speed", 0f);
            }
        }

        Debug.Log(enableBossAI 
            ? "[BossAnimationTester] ⚔️ IA do Boss ATIVADA (Combate Normal)." 
            : "[BossAnimationTester] ⏸️ IA do Boss PAUSADA (Modo Sandbox de Testes). Boss 100% parado.");
    }

    /// <summary>
    /// Força o Boss a parar imediatamente qualquer ação em andamento e ficar em repouso no lugar.
    /// </summary>
    public void StopAndResetBoss()
    {
        FindReferences();
        enableBossAI = false;
        ApplyAIMode(true);

        if (bossController != null)
        {
            bossController.OverrideMovement = true;
            bossController.StopAllCoroutines();
            bossController.DesativarTodosOsTrails();
            bossController.isCastingSpell = false;
            bossController.isTeleporting = false;
            bossController.isExecutingCombo = false;
        }

        StopAllCoroutines();

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        PlayIdle();

        Debug.Log("[BossAnimationTester] 🛑 Boss parado e resetado para Idle no lugar!");
    }

    // =====================================================
    // CONTROLE DE FASES (DISPARADO VIA INSPECTOR)
    // =====================================================

    public void SetPhase(BossController.BossState newPhase)
    {
        FindReferences();

        int phaseInt = 1;
        if (newPhase == BossController.BossState.Phase2) phaseInt = 2;
        else if (newPhase == BossController.BossState.Phase3) phaseInt = 3;

        Debug.Log($"[BossAnimationTester] 🔄 Alterando Fase para: {newPhase} (Fase {phaseInt})");
        
        if (bossController != null)
        {
            bossController.ForcePhase(phaseInt);
            bossController.OverrideMovement = !enableBossAI;
        }
        else
        {
            BossEvents.RaisePhaseChanged(phaseInt);
        }

        FindReferences();

        if (phaseInt == 3)
        {
            var stretch = GetComponent<BossArmStretch>() ?? GetComponentInChildren<BossArmStretch>() ?? (bossController != null ? bossController.GetComponent<BossArmStretch>() : null);
            if (stretch != null && bossController != null && bossController.visualPhase3 != null)
            {
                stretch.FindBones(bossController.visualPhase3);
            }
        }
    }

    // =====================================================
    // DISPARO DE ANIMAÇÕES
    // =====================================================

    public void PlayAnimationTrigger(string triggerName)
    {
        FindReferences();

        if (animator == null)
        {
            Debug.LogWarning("[BossAnimationTester] ⚠️ Animator não encontrado! Verifique a hierarquia do Boss.");
            return;
        }

        animator.enabled = true;
        animator.SetBool("IsWalking", false);
        animator.SetBool("isSprinting", false);
        animator.SetFloat("Speed", 0f);

        // Reseta triggers conhecidos para evitar colisões
        string[] allTriggers = new string[] { "bossSwipe", "bossPunch", "bossJumpAttack", "bossStomp", "bossSpell", "Spell", "PowerUp", "SimpleCast", "BossSpellWide", "Stunned", "Die", "Roar", "bossLowAttack", "bossUpAttack", "AcidSpit", "ThornVolley" };
        foreach (var t in allTriggers)
        {
            try { animator.ResetTrigger(t); } catch { }
        }

        // Dispara o Trigger solicitado (com suporte a Spell se for bossSpell)
        try { animator.SetTrigger(triggerName); } catch { }
        if (triggerName.Equals("bossSpell", System.StringComparison.OrdinalIgnoreCase))
        {
            try { animator.SetTrigger("Spell"); } catch { }
        }

        if (triggerName.Equals("PowerUp", System.StringComparison.OrdinalIgnoreCase) && bossController != null)
        {
            bossController.TriggerPowerUP(2.5f);
            return;
        }

        // Fallbacks de busca de State no Animator (com variações de caixa e prefixos)
        string[] candidates = new string[] 
        { 
            triggerName, 
            "Boss" + char.ToUpper(triggerName[0]) + triggerName.Substring(1),
            triggerName.Replace("boss", "").Replace("Boss", ""),
            char.ToUpper(triggerName[0]) + triggerName.Substring(1),
            triggerName == "bossLowAttack" ? "Attack_Low" : "",
            triggerName == "bossUpAttack" ? "Attack_Uppercut" : "",
            triggerName == "bossStomp" ? "STOMP" : "",
            triggerName == "bossJumpAttack" ? "JumpAttack" : "",
            triggerName == "SimpleCast" ? "SpellStunSimple" : "",
            triggerName == "BossSpellWide" ? "SpellWide" : "",
            triggerName == "Spell" ? "SpellGround" : "",
            triggerName == "Die" ? "DeathBoss" : "",
            triggerName == "DeathBoss" ? "Die" : ""
        };

        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (animator.HasState(0, Animator.StringToHash(name)))
            {
                animator.Play(name, 0, 0f);
                break;
            }
        }

        // Se for ataque da Fase 3 disparado pelo Tester, aciona o estiramento com a calibração do ataque!
        if (triggerName == "bossLowAttack" || triggerName == "bossUpAttack")
        {
            var stretch = GetComponent<BossArmStretch>() ?? GetComponentInChildren<BossArmStretch>() ?? (bossController != null ? bossController.GetComponent<BossArmStretch>() : null);
            if (stretch == null && bossController != null) stretch = bossController.gameObject.AddComponent<BossArmStretch>();
            if (stretch != null)
            {
                if (bossController != null && bossController.visualPhase3 != null)
                    stretch.FindBones(bossController.visualPhase3);
                stretch.TriggerAttackStretch(triggerName);
            }
        }

        Debug.Log($"[BossAnimationTester] 🎬 Animação disparada: {triggerName}");
    }

    public void PlayIdle()
    {
        FindReferences();
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("isSprinting", false);
            animator.SetFloat("Speed", 0f);
            if (animator.HasState(0, Animator.StringToHash("BossIdle"))) animator.Play("BossIdle", 0, 0f);
            else if (animator.HasState(0, Animator.StringToHash("Idle"))) animator.Play("Idle", 0, 0f);
            else if (animator.HasState(0, Animator.StringToHash("bossIdle"))) animator.Play("bossIdle", 0, 0f);
        }
    }

    // =====================================================
    // HABILIDADES DA FASE 2
    // =====================================================

    /// <summary>
    /// Dispara o Stun do Céu / Mímica do Golem (SimpleCast / SpellStunSimple).
    /// </summary>
    public void TriggerGolemStunCast()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.PerformGolemStunCast();
        }
        else
        {
            PlayAnimationTrigger("SimpleCast");
        }
        Debug.Log("[BossAnimationTester] ⚡ Disparou Stun do Céu (Mímica do Golem)!");
    }

    /// <summary>
    /// Dispara a Invocação de Pilares do Solo (SpellCast / SpellGround).
    /// Executa a animação Spell e ergue os pilares/espinhos do chão.
    /// </summary>
    public void TriggerSpellGroundPillars()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.TriggerPillarSummon();
        }
        else
        {
            BossPhase1_MestreDoSolo mestre = GetComponent<BossPhase1_MestreDoSolo>() ?? GetComponentInChildren<BossPhase1_MestreDoSolo>();
            if (mestre != null)
            {
                mestre.InvocarPrisaoForcado(bypassActionCheck: true, forceClearOld: true);
            }
            PlayAnimationTrigger("Spell");
        }
        Debug.Log("[BossAnimationTester] 🪄 Disparou Invocação de Pilares do Chão (SpellGround)!");
    }

    /// <summary>
    /// Dispara o Super Ataque de Espinhos 360° (BossSpellWide).
    /// </summary>
    public void TriggerSuperAtaqueEspinhos()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.PerformBossSpellWide();
        }
        else
        {
            PlayAnimationTrigger("BossSpellWide");
        }
        Debug.Log("[BossAnimationTester] 🌊 Disparou Super Ataque 360° (BossSpellWide)!");
    }

    /// <summary>
    /// Dispara o Teleporte com Holograma Estilo SharpBlur (Fase 2).
    /// </summary>
    public void TriggerSharpBlurTeleport()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.TriggerSharpBlurTeleport();
        }
        else
        {
            Debug.LogWarning("[BossAnimationTester] ⚠️ BossController não encontrado para o Teleporte!");
        }
    }

    /// <summary>
    /// Dispara a Energização PowerUp (Fase 2).
    /// </summary>
    public void TriggerPowerUp()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.TriggerPowerUP(2.5f);
        }
        else
        {
            PlayAnimationTrigger("PowerUp");
        }
    }

    /// <summary>
    /// Alterna a Refração / Invisibilidade da Fase 2 (ON / OFF).
    /// </summary>
    public void ToggleRefraction()
    {
        FindReferences();
        if (bossController != null)
        {
            bool newState = !bossController.IsInvisible;
            bossController.SetRefraction(newState);
            Debug.Log($"[BossAnimationTester] 👻 Refração / Invisibilidade: {(newState ? "LIGADA" : "DESLIGADA")}");
        }
    }

    /// <summary>
    /// Aplica Stun no próprio Boss.
    /// </summary>
    public void TriggerBossStun(float duration = 3.0f)
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.ApplyStun(duration);
        }
        else
        {
            PlayAnimationTrigger("Stunned");
        }
        Debug.Log($"[BossAnimationTester] 💫 Boss Atordoado por {duration}s!");
    }

    // =====================================================
    // HABILIDADES DA FASE 3
    // =====================================================

    public void TriggerAcidSpit()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.TriggerAcidSpit();
        }
        else
        {
            PlayAnimationTrigger("AcidSpit");
        }
        Debug.Log("[BossAnimationTester] 🧪 Disparou Cuspe Ácido (Fase 3)!");
    }

    public void TriggerThornVolley()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.TriggerThornVolley();
        }
        else
        {
            PlayAnimationTrigger("ThornVolley");
        }
        Debug.Log("[BossAnimationTester] 🌵 Disparou Salva de Espinhos (Fase 3)!");
    }

    // =====================================================
    // DISPARO DE VFX & CAMERA SHAKE
    // =====================================================

    public void TriggerVFXStomp()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.AnimEvent_StompImpact();
            Debug.Log("[BossAnimationTester] 💥 Disparou VFX e Onda de Choque de Pisada (Stomp)");
        }
    }

    public void TriggerVFXJumpAttack()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.AnimEvent_JumpImpact();
            Debug.Log("[BossAnimationTester] 💥 Disparou VFX e Onda de Choque de Salto (Jump)");
        }
    }

    public void TriggerCameraShake()
    {
        BossController.TriggerCameraShake(0.45f, 0.40f);
        Debug.Log("[BossAnimationTester] 📳 Disparou Camera Shake do Boss");
    }

    public void TriggerBossDeath()
    {
        FindReferences();
        if (bossController != null)
        {
            bossController.TriggerDeathSequence();
        }
        else
        {
            PlayAnimationTrigger("Die");
        }
        Debug.Log("[BossAnimationTester] 💀 Disparou Sequência Completa de Morte & Absorção pela Terra!");
    }
}

// =====================================================
// CUSTOM INSPECTOR EDITOR (DESENHA BOTÕES NO UNITY)
// =====================================================
#if UNITY_EDITOR
[CustomEditor(typeof(BossAnimationTester))]
public class BossAnimationTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BossAnimationTester tester = (BossAnimationTester)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🎮 CONTROLE DO BOSS & MODO SANDBOX", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            tester.enableBossAI 
                ? "⚠️ MODO COMBATE ATIVO: O Boss perseguirá e atacará o jogador normalmente."
                : "✅ MODO SANDBOX ATIVO: O Boss está 100% PARADO. Clique nos botões abaixo para testar cada golpe e feitiço!",
            tester.enableBossAI ? MessageType.Warning : MessageType.Info
        );

        EditorGUILayout.Space(5);

        // 1. ALTERNAR MODO DE IA
        GUI.backgroundColor = tester.enableBossAI ? new Color(1f, 0.45f, 0.45f) : new Color(0.45f, 0.95f, 0.55f);
        if (GUILayout.Button(tester.enableBossAI ? "⏸️ PARAR BOSS (MODO SANDBOX PASSIVO)" : "⚔️ ATIVAR IA (MODO COMBATE AGRESSIVO)", GUILayout.Height(36)))
        {
            tester.enableBossAI = !tester.enableBossAI;
            tester.ApplyAIMode();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("🛑 RESETAR / PARAR BOSS NO LUGAR", GUILayout.Height(28)))
        {
            tester.StopAndResetBoss();
        }

        GUI.backgroundColor = tester.alwaysFacePlayer ? new Color(0.35f, 0.85f, 1f) : new Color(0.85f, 0.85f, 0.85f);
        if (GUILayout.Button(tester.alwaysFacePlayer ? "👀 ENCARAR PLAYER: ATIVADO (Sempre Virado para Você)" : "👀 ENCARAR PLAYER: DESATIVADO (Rotação Fixa)", GUILayout.Height(30)))
        {
            tester.alwaysFacePlayer = !tester.alwaysFacePlayer;
            EditorUtility.SetDirty(tester);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("⚙️ SELEÇÃO DE FASES", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fase 1 (Solo/Casulo)", GUILayout.Height(30)))
        {
            tester.SetPhase(BossController.BossState.Phase1);
        }
        if (GUILayout.Button("Fase 2 (Orc Cromático)", GUILayout.Height(30)))
        {
            tester.SetPhase(BossController.BossState.Phase2);
        }
        GUI.backgroundColor = new Color(0.4f, 1f, 0.6f);
        if (GUILayout.Button("Fase 3 (Boss Flor / Raízes)", GUILayout.Height(30)))
        {
            tester.SetPhase(BossController.BossState.Phase3);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // -------------------------------------------------------------
        // SEÇÃO FASE 1 & 2: ATAQUES FÍSICOS (ORC)
        // -------------------------------------------------------------
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🪨 FASE 1 & 2 - ATAQUES FÍSICOS (Orc Cromático)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⚔️ Swipe (Varredura)", GUILayout.Height(28)))
        {
            tester.PlayAnimationTrigger("bossSwipe");
        }
        if (GUILayout.Button("👊 Punch (Soco)", GUILayout.Height(28)))
        {
            tester.PlayAnimationTrigger("bossPunch");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🦘 Jump Attack (Salto)", GUILayout.Height(28)))
        {
            tester.PlayAnimationTrigger("bossJumpAttack");
        }
        if (GUILayout.Button("🦶 Stomp (Pisada)", GUILayout.Height(28)))
        {
            tester.PlayAnimationTrigger("bossStomp");
        }
        EditorGUILayout.EndHorizontal();

        // -------------------------------------------------------------
        // SEÇÃO FASE 2: MAGIAS & HABILIDADES ESPECIAIS (ORC)
        // -------------------------------------------------------------
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🔮 FASE 2 - MAGIAS & PODERES ESPECIAIS", EditorStyles.boldLabel);

        // STUN DO GOLEM (SimpleCast)
        GUI.backgroundColor = new Color(1.0f, 0.85f, 0.3f);
        if (GUILayout.Button("⚡ Stun do Céu / Mímica do Golem (SimpleCast)", GUILayout.Height(32)))
        {
            tester.TriggerGolemStunCast();
        }
        GUI.backgroundColor = Color.white;

        // INVOCAÇÃO DE PILARES (SpellCast)
        GUI.backgroundColor = new Color(0.7f, 0.85f, 1.0f);
        if (GUILayout.Button("🪄 Invocação de Pilares do Chão (SpellCast)", GUILayout.Height(32)))
        {
            tester.TriggerSpellGroundPillars();
        }
        GUI.backgroundColor = Color.white;

        // SUPER ATAQUE 360°
        if (GUILayout.Button("🌊 Super Ataque: Espinhos 360° (BossSpellWide)", GUILayout.Height(30)))
        {
            tester.TriggerSuperAtaqueEspinhos();
        }

        // TELEPORTE SHARPBLUR
        GUI.backgroundColor = new Color(0.85f, 0.5f, 1f);
        if (GUILayout.Button("⚡ Teleporte Estilo SharpBlur (Holograma + Warp + Golpe)", GUILayout.Height(32)))
        {
            tester.TriggerSharpBlurTeleport();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🛡️ PowerUp (Energização)", GUILayout.Height(28)))
        {
            tester.TriggerPowerUp();
        }
        bool isInvis = tester.bossController != null && tester.bossController.IsInvisible;
        GUI.backgroundColor = isInvis ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.8f, 0.9f, 1f);
        if (GUILayout.Button(isInvis ? "👻 Refração (DESATIVAR)" : "👻 Refração (ATIVAR INVISÍVEL)", GUILayout.Height(28)))
        {
            tester.ToggleRefraction();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // -------------------------------------------------------------
        // SEÇÃO FASE 3: ATAQUES & PODERES (BOSS FLOR / RAÍZES)
        // -------------------------------------------------------------
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🌸 FASE 3 - ATAQUES (Boss Flor / Raízes & Serralha)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1f, 0.6f);
        if (GUILayout.Button("🌸 Ataque Básico Baixo", GUILayout.Height(30)))
        {
            tester.PlayAnimationTrigger("bossLowAttack");
        }
        if (GUILayout.Button("🌸 Ataque Baixo Uppercut", GUILayout.Height(30)))
        {
            tester.PlayAnimationTrigger("bossUpAttack");
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.95f, 0.95f);
        if (GUILayout.Button("🧪 Cuspe Ácido (Serralha)", GUILayout.Height(30)))
        {
            tester.TriggerAcidSpit();
        }
        if (GUILayout.Button("🌵 Salva de Espinhos (Serralha)", GUILayout.Height(30)))
        {
            tester.TriggerThornVolley();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("🦁 Rugido de Entrada (Roar)", GUILayout.Height(28)))
        {
            tester.PlayAnimationTrigger("Roar");
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🌿 TESTE DE BRAÇO ESTICANDO (CRESCIMENTO PROCEDURAL)", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1.0f, 0.5f);
        if (GUILayout.Button("🌿 Esticar Braço Direito (Fase 3)", GUILayout.Height(30)))
        {
            var stretch = tester.GetComponent<BossArmStretch>() ?? tester.GetComponentInChildren<BossArmStretch>() ?? (tester.bossController != null ? tester.bossController.GetComponent<BossArmStretch>() : null);
            if (stretch == null && tester.bossController != null) stretch = tester.bossController.gameObject.AddComponent<BossArmStretch>();
            if (stretch != null)
            {
                if (tester.bossController != null && tester.bossController.visualPhase3 != null)
                    stretch.FindBones(tester.bossController.visualPhase3);
                stretch.StretchRightArm();
            }
        }
        if (GUILayout.Button("🌿 Esticar Braço Esquerdo (Fase 3)", GUILayout.Height(30)))
        {
            var stretch = tester.GetComponent<BossArmStretch>() ?? tester.GetComponentInChildren<BossArmStretch>() ?? (tester.bossController != null ? tester.bossController.GetComponent<BossArmStretch>() : null);
            if (stretch == null && tester.bossController != null) stretch = tester.bossController.gameObject.AddComponent<BossArmStretch>();
            if (stretch != null)
            {
                if (tester.bossController != null && tester.bossController.visualPhase3 != null)
                    stretch.FindBones(tester.bossController.visualPhase3);
                stretch.StretchLeftArm();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // CALIBRAÇÃO AO VIVO DO BRAÇO ESTICANDO (Fase 3)
        var stretchComp = tester.GetComponent<BossArmStretch>() ?? tester.GetComponentInChildren<BossArmStretch>() ?? (tester.bossController != null ? tester.bossController.GetComponent<BossArmStretch>() : null);
        if (stretchComp != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("[CALIBRACAO DO BRACO ESTICANDO - FASE 3]", EditorStyles.boldLabel);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("1. Ataque Basico Baixo (Low Attack):", EditorStyles.miniBoldLabel);
            stretchComp.lowAttackStartDelay = EditorGUILayout.Slider("Delay Inicial (s)", stretchComp.lowAttackStartDelay, 0.0f, 1.2f);
            stretchComp.lowAttackDuration = EditorGUILayout.Slider("Duracao Ciclo (s)", stretchComp.lowAttackDuration, 0.2f, 1.8f);
            stretchComp.lowAttackMultiplier = EditorGUILayout.Slider("Tamanho / Alcance", stretchComp.lowAttackMultiplier, 1.5f, 5.0f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("2. Ataque Baixo Uppercut (Up Attack):", EditorStyles.miniBoldLabel);
            stretchComp.upAttackStartDelay = EditorGUILayout.Slider("Delay Inicial (s)", stretchComp.upAttackStartDelay, 0.0f, 1.2f);
            stretchComp.upAttackDuration = EditorGUILayout.Slider("Duracao Ciclo (s)", stretchComp.upAttackDuration, 0.2f, 1.8f);
            stretchComp.upAttackMultiplier = EditorGUILayout.Slider("Tamanho / Alcance", stretchComp.upAttackMultiplier, 1.5f, 5.0f);

            EditorGUILayout.EndVertical();
        }

        // -------------------------------------------------------------
        // SEÇÃO GERAL: VFX, IMPACTOS E ESTADOS
        // -------------------------------------------------------------
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("💥 VFX, TREMOR E ESTADOS GERAIS", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("💥 VFX Pisada (Stomp)", GUILayout.Height(28)))
        {
            tester.TriggerVFXStomp();
        }
        if (GUILayout.Button("💥 VFX Salto (Jump)", GUILayout.Height(28)))
        {
            tester.TriggerVFXJumpAttack();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("💫 Atordoar Boss (Stun - 3s)", GUILayout.Height(28)))
        {
            tester.TriggerBossStun(3.0f);
        }
        if (GUILayout.Button("📳 Camera Shake", GUILayout.Height(28)))
        {
            tester.TriggerCameraShake();
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
        if (GUILayout.Button("💀 Morrer (Morte Cinematográfica: Queda + Absorção pela Terra)", GUILayout.Height(32)))
        {
            tester.TriggerBossDeath();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
    }
}
#endif
