using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Máquina de Estados do Bumerangue.
/// </summary>
public enum BoomerangState
{
    Stowed,         // Preso nas costas do astronauta (guardado)
    Charging,       // Na mão do astronauta carregando a força do arremesso
    Throw,          // Arremessado no ar em alta rotação (ida / ricochetes)
    Returning,      // Voltando para o jogador (DynamicHoming) com clones translúcidos
    SpinningShield  // Escudo giratório defensivo ao redor/frente do corpo do player (Parry 0.2s)
}

/// <summary>
/// Controlador principal do Bumerangue (Opção B - Boomerang).
///
/// 🔹 Arte / Animações / VFX:
///    - Encaixe visual nas costas (Stowed / WeaponRack), na mão direita do astronauta e girando em alta rotação no ar.
///    - Integração com as animações Astronaut_tirandodascostas_boomerang, Astronaut_guardando_boomerang e Astronaut_defense.
///    - Efeito de Escudo Giratório Defensivo (BoomerangShieldVFX) ao redor/frente do corpo do player.
///    - Variante 1: Cristal Desacoplante Elétrico (BoomerangElectricCrystalVFX — estilo Istar com LineRenderer).
///    - Variante 2: Clones Translúcidos na Volta & Ecos de Ricochete (BoomerangEchoVFX — estilo SharpBlur).
///
/// 🔹 Física / Combate (Estrutura de Estados e Hooks):
///    - Máquina de Estados completa (Stowed, Charging, Throw, Returning, SpinningShield).
///    - Propriedades e métodos marcados na região PHYSICS_AND_COMBAT_HOOKS para plugar a física vetorial
///      definitiva, cálculo de quiques (2 a 6) e janela de Parry de 0.2s.
/// </summary>
public class BoomerangWeapon : MonoBehaviour
{
    [Header("Estado Atual da Máquina de Estados")]
    [SerializeField] private BoomerangState currentState = BoomerangState.Charging;
    public BoomerangState CurrentState => currentState;

    public event Action<BoomerangState, BoomerangState> OnStateChanged;

    [Header("Encaixe Visual: Mão Direita vs Costas")]
    [Tooltip("Posição local quando empunhado na mão direita.")]
    public Vector3 handMountPosition = new Vector3(0.02f, 0.02f, 0.03f);

    [Tooltip("Rotação local quando empunhado na mão direita.")]
    public Vector3 handMountRotation = new Vector3(0f, 0f, 90f);

    [Tooltip("Posição local quando preso nas costas do astronauta (mixamorig:Spine2).")]
    public Vector3 backMountPosition = new Vector3(0f, -0.08f, -0.20f);

    [Tooltip("Rotação local quando preso nas costas do astronauta.")]
    public Vector3 backMountRotation = new Vector3(0f, 0f, 45f);

    [Tooltip("Escala global do modelo visual do bumerangue.")]
    public Vector3 equippedWorldScale = new Vector3(0.55f, 0.55f, 0.55f);

    [Header("Variantes e Efeitos Tier 4 (Ativados por Tier4EffectManager ou Teste no Inspector)")]
    [Tooltip("Tier 4: Pontas de cristal se desacoplam no ar soltando feixes de eletricidade (estilo Istar).")]
    public bool enableElectricCrystalVariant = false;

    [Tooltip("Tier 4: Gera clones translúcidos na volta (Returning).")]
    public bool enableTranslucentClonesOnReturn = false;

    [Tooltip("Tier 4: Spawna um eco fantasma autônomo a cada ricochete.")]
    public bool enableBounceEchoSpawning = false;

    [Tooltip("Tier 4 (Evasion): Escudo giratório aplica paralisia elétrica ao contato.")]
    public bool enableShieldMeleeParalysis = false;

    [Header("Parâmetros de Física / Máquina de Estados")]
    [Tooltip("Se verdadeiro, ativa os controles e física base para testar todas as animações e VFX imediatamente.")]
    public bool useBuiltInStateAndPhysics = true;

    [Tooltip("Tempo máximo segurando o botão de ataque após armar para atingir 100% de força.")]
    public float maxChargeTime = 0.85f;

    [Tooltip("Velocidade mínima de arremesso (sem carga).")]
    public float minThrowSpeed = 14f;

    [Tooltip("Velocidade máxima de arremesso (carga máxima).")]
    public float maxThrowSpeed = 26f;

    [Tooltip("Distância mínima de arremesso antes de começar a voltar.")]
    public float minThrowDistance = 5.5f;

    [Tooltip("Distância máxima de arremesso com carga total.")]
    public float maxThrowDistance = 13.5f;

    [Tooltip("Velocidade base de retorno (DynamicHoming) para a mão do player.")]
    public float returnHomingSpeed = 22f;

    [Tooltip("Aceleração extra na volta para alcançar o jogador mesmo se ele der Dash.")]
    public float returnHomingAcceleration = 28f;

    [Tooltip("Quantidade base mínima de quiques (sem carga).")]
    public int minBounces = 0;

    [Tooltip("Quantidade base máxima de quiques (carga máxima).")]
    public int maxBounces = 1;

    [Tooltip("Janela de Parry nos primeiros 0.2s do estado SpinningShield (reflete projéteis / atordoa melee).")]
    public float parryWindowDuration = 0.2f;

    [Tooltip("Trava o movimento do player durante o escudo giratório (igual recarga da Pistola no PDF Pág. 7)?")]
    public bool lockPlayerMovementDuringShield = false;

    [Tooltip("Tecla alternativa (além do Botão Direito do Mouse) para ativar o Escudo Giratório.")]
    public KeyCode shieldAlternateKey = KeyCode.R;

    [Header("Cursor")]
    [Tooltip("Mantém o cursor do mouse sempre visível e destravado enquanto a Glaive estiver equipada.")]
    public bool alwaysShowCursorWhenEquipped = true;

    [Header("Antecipação e Timing de Arremesso")]
    [Tooltip("Nome do estado de ataque/arremesso no Animator (para sincronizar o ponto de soltura).")]
    public string attackStateName = "Attack1";

    [Tooltip("Se verdadeiro, soltar o botão antes do evento BoomerangDrawReady cancela o ataque (estilo arco).")]
    public bool requireDrawReadyEvent = true;

    [Tooltip("Momento normalizado (0..1) do evento ThrowBoomerang (fallback caso o evento seja pulado em transições).")]
    [Range(0.1f, 0.9f)]
    public float releaseNormalizedTime = 0.352f;

    [Tooltip("Ponto normalizado (0..1) onde a antecipação segura a pose armada antes do evento ThrowBoomerang.")]
    [Range(0.1f, 0.85f)]
    public float holdAtApexNormalizedTime = 0.30f;

    [Tooltip("Tempo máximo (segundos) que o jogador pode segurar o carregamento antes do arremesso automático.")]
    public float maxHoldDuration = 1.5f;

    [Tooltip("Velocidade inicial antes de BoomerangDrawReady (1.0 garante que um clique rápido solte antes do evento e cancele).")]
    public float initialAnticipationSpeed = 1.0f;

    [Tooltip("Velocidade após soltar logo depois de BoomerangDrawReady para concluir rapidamente até ThrowBoomerang.")]
    public float quickTapAnticipationSpeed = 2.85f;

    [Tooltip("Velocidade mínima da antecipação enquanto o jogador segura para carregar o arremesso.")]
    public float chargeHoldAnticipationSpeed = 0.25f;

    [Tooltip("Velocidade após soltar um ataque carregado para cruzar o evento ThrowBoomerang e concluir a animação.")]
    public float throwFollowThroughSpeed = 1.65f;

    [Tooltip("Janela após BoomerangDrawReady em que a soltura ainda usa velocidade acelerada de tirinho rápido.")]
    public float quickReleaseAfterCommitWindow = 0.28f;

    [Tooltip("Multiplicador inicial da velocidade de movimento ao apertar/segurar o ataque (0.10 = redução de 90%).")]
    [Range(0f, 1f)]
    public float aimingMoveSpeedMultiplier = 0.10f;

    [Tooltip("Multiplicador mínimo da velocidade de movimento ao atingir carga máxima (0 = jogador praticamente parado).")]
    [Range(0f, 1f)]
    public float minChargeMoveSpeedMultiplier = 0f;

    [Header("Indicador de Alcance no Chão")]
    [Tooltip("Cor do indicador translúcido no chão mostrando direção e alcance do arremesso.")]
    public Color aimIndicatorColor = new Color(0.05f, 0.75f, 1f, 0.22f);
    [Min(0.1f)] public float aimIndicatorWidth = 0.9f;
    [Min(0f)] public float aimIndicatorGroundOffset = 0.04f;
    [Tooltip("Se verdadeiro, mostra o indicador no chão apenas enquanto estiver segurando/carregando o arremesso (como a Pistola).")]
    public bool showAimIndicatorOnlyWhileAttacking = true;

    // ─── Referências Internas da Hierarquia do Modelo (boomerang.fbx) ─────────
    [Header("Referências da Hierarquia do Modelo (Auto-Bind)")]
    public Transform visualPivot;
    public Transform modelRoot;
    public Transform anelCentral;
    public Transform baseLaminaSuperior;
    public Transform baseLaminaInferior;
    public Transform piramideSuperior;
    public Transform piramideBrancaSuperior;
    public Transform piramideInferior;
    public Transform piramideBrancaInferior;

    // ─── Módulos de VFX ───────────────────────────────────────────────────────
    [Header("Módulos de VFX (Auto-Configurados)")]
    public BoomerangAirSpinVFX airSpinVFX;
    public BoomerangElectricCrystalVFX electricCrystalVFX;
    public BoomerangEchoVFX echoVFX;
    public BoomerangShieldVFX shieldVFX;

    // ─── SFX do Bumerangue ────────────────────────────────────────────────────
    [Header("SFX do Bumerangue")]
    public AudioClip sfxArmando;
    public AudioClip sfxGuardando;
    public AudioClip sfxSegurandoParaLancar;
    public AudioClip sfxJogando;
    public AudioClip sfxGirandoCurto;
    public AudioClip sfxGirandoMedio;
    public AudioClip sfxGirandoLongo;
    public AudioClip sfxVoltandoParaMao;
    public AudioClip sfxRefletirInicio;
    public AudioClip sfxRefletirGiroLoop;
    public AudioClip[] sfxRefletiuVariacoes;
    [Range(0f, 1.5f)] public float sfxMasterVolume = 0.85f;

    // ─── Referências do Player ────────────────────────────────────────────────
    private AudioSource sfxOneShotSource;
    private AudioSource sfxLoopSource;
    private float currentLoopBaseVolume = 0.8f;
    private int lastReflectSfxIndex = -1;
    private Player_WeaponManager weaponManager;
    private PlayerM playerMovement;
    private PlayerAttributesOffensive offensiveStats;
    private Tier4EffectManager tier4EffectManager;
    private Animator playerAnimator;
    private Transform rightHandAnchor;
    private Transform backSpineAnchor;
    private WeaponOffset weaponOffset;
    private ItemFloat itemFloat;

    // ─── Variáveis de Execução da Máquina de Estados ─────────────────────────
    private float currentChargeTime;
    private float currentCharge01;
    private float chargeSpinAngle;
    private Vector3 flightVelocity;
    private Vector3 throwOriginPos;
    private float targetFlightDistance;
    private float travelledDistance;
    private float currentReturnSpeed;
    private int bouncesRemaining;
    private int piercingRemaining;
    private float stateElapsedTime;
    private bool isDrawnInHand;
    private bool isWindupThrowPending;
    private bool hasCommittedDraw;
    private float drawCommitTime;
    private float fullChargeReachedAt = -1f;
    private bool isPerfectChargeThrow;
    private bool isReleaseRequested;
    private bool isQuickTapRelease;
    private bool animEventReleaseSignaled;
    private bool passedEventWhileHolding;
    private float releaseWaitElapsed;
    private bool hasBlendedOutOfThrowAnim;
    private bool aimCursorOverrideActive;
    private GameObject aimIndicatorObject;
    private Transform aimIndicatorTransform;
    private Mesh aimIndicatorMesh;
    private Material aimIndicatorMaterial;
    private float cachedIndicatorRange = -1f;
    private float cachedIndicatorWidth = -1f;
    private float cachedIndicatorCharge = -1f;

    private bool IsElectricCrystalActive => enableElectricCrystalVariant || (tier4EffectManager != null && tier4EffectManager.HasEffect(Tier4EffectType.Boomerang_ElectricCrystal));
    private bool IsTranslucentClonesActive => enableTranslucentClonesOnReturn || (tier4EffectManager != null && tier4EffectManager.HasEffect(Tier4EffectType.Boomerang_TranslucentClones));
    private bool IsBounceEchoActive => enableBounceEchoSpawning || (tier4EffectManager != null && tier4EffectManager.HasEffect(Tier4EffectType.Boomerang_BounceEcho));
    private bool IsShieldParalysisActive => enableShieldMeleeParalysis || (tier4EffectManager != null && tier4EffectManager.HasEffect(Tier4EffectType.Boomerang_ShieldParalysis));
    private readonly HashSet<int> hitTargetsThisPhase = new HashSet<int>();
    private readonly Dictionary<int, float> hitCooldowns = new Dictionary<int, float>();

    // Propriedades públicas da Máquina de Estados / Física
    public float ChargeNormalized => Mathf.Clamp01(currentCharge01);
    public bool IsInParryWindow => currentState == BoomerangState.SpinningShield && stateElapsedTime <= parryWindowDuration;
    public int BouncesRemaining { get => bouncesRemaining; set => bouncesRemaining = value; }
    public Vector3 FlightVelocity { get => flightVelocity; set => flightVelocity = value; }
    public float StateElapsedTime => stateElapsedTime;

    private static readonly int AnimTriggerAttack = Animator.StringToHash("Attack");
    private static readonly int AnimTriggerShoot = Animator.StringToHash("Shoot");
    private static readonly int AnimTriggerDraw = Animator.StringToHash("DrawWeapon");
    private static readonly int AnimTriggerHolster = Animator.StringToHash("HolsterWeapon");
    private static readonly int AnimBoolIsDefending = Animator.StringToHash("IsDefending");
    private static readonly int AnimBoolIsReloading = Animator.StringToHash("IsReloading");
    private static readonly int AnimBoolIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int AnimFloatAttackSpeed = Animator.StringToHash("AttackSpeed");

    private void Awake()
    {
        weaponOffset = GetComponent<WeaponOffset>();
        itemFloat = GetComponent<ItemFloat>();

        BindAndNormalizeModelHierarchy();
        InitializeVFXModules();
    }

    private void Start()
    {
        if (IsInLobbyRack())
            return;

        TryBindPlayerReferences();

        if (IsEquippedByPlayer())
        {
            // Começa empunhado na mão por padrão (salvo se o WeaponManager já tiver mandado guardar nas costas)
            if (weaponManager == null || weaponManager.isWeaponDrawn)
            {
                EquipInHandImmediate();
            }
            else
            {
                currentState = BoomerangState.Stowed;
                isDrawnInHand = false;
                AttachToBackImmediate();
            }
        }
    }

    /// <summary>
    /// Localiza automaticamente as partes do boomerang.fbx (anel_central, base_lamina_superior/inferior,
    /// piramide_superior/inferior) e corrige o offset Y=150 nativo do Blockbench dentro de um VisualPivot.
    /// </summary>
    public void BindAndNormalizeModelHierarchy()
    {
        if (visualPivot == null || !visualPivot.IsChildOf(transform))
        {
            Transform existingPivot = transform.Find("VisualPivot");
            if (existingPivot != null)
            {
                visualPivot = existingPivot;
            }
        }

        if (modelRoot == null || !modelRoot.IsChildOf(transform))
        {
            modelRoot = FindChildRecursive(transform, "boomerang");
        }

        // Caso o prefab tenha sido criado sem o modelo filho, tenta carregar de Resources
        if (modelRoot == null)
        {
            GameObject fallbackModel = Resources.Load<GameObject>("BoomerangModel");
            if (fallbackModel != null)
            {
                GameObject inst = Instantiate(fallbackModel, transform);
                inst.name = "boomerang";
                modelRoot = inst.transform;
            }
        }

        if (visualPivot == null)
        {
            GameObject pivotGO = new GameObject("VisualPivot");
            pivotGO.transform.SetParent(transform, false);
            pivotGO.transform.localPosition = Vector3.zero;
            pivotGO.transform.localRotation = Quaternion.identity;
            pivotGO.transform.localScale = Vector3.one;
            visualPivot = pivotGO.transform;

            if (modelRoot != null && modelRoot.parent != visualPivot)
            {
                modelRoot.SetParent(visualPivot, false);
            }
        }

        // O boomerang.fbx exportado do Blockbench possui Lcl Translation = (0, 150, 0) no nó "boomerang".
        // Zeramos a posição local dele dentro do VisualPivot para centralizar o giro exatamente no anel_central!
        if (modelRoot != null)
        {
            if (modelRoot.parent != visualPivot)
            {
                modelRoot.SetParent(visualPivot, false);
            }
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
        }

        Transform searchRoot = (modelRoot != null && modelRoot.IsChildOf(transform)) ? modelRoot : transform;
        anelCentral = FindChildRecursive(searchRoot, "anel_central");
        baseLaminaSuperior = FindChildRecursive(searchRoot, "base_lamina_superior");
        baseLaminaInferior = FindChildRecursive(searchRoot, "base_lamina_inferior");
        piramideSuperior = FindChildRecursive(searchRoot, "piramide_superior");
        piramideBrancaSuperior = FindChildRecursive(searchRoot, "piramide_branca_superior");
        piramideInferior = FindChildRecursive(searchRoot, "piramide_inferior");
        piramideBrancaInferior = FindChildRecursive(searchRoot, "piramide_branca_inferior");
    }

    private void InitializeVFXModules()
    {
        if (airSpinVFX == null)
            airSpinVFX = GetComponent<BoomerangAirSpinVFX>() ?? gameObject.AddComponent<BoomerangAirSpinVFX>();

        if (electricCrystalVFX == null)
            electricCrystalVFX = GetComponent<BoomerangElectricCrystalVFX>() ?? gameObject.AddComponent<BoomerangElectricCrystalVFX>();

        if (echoVFX == null)
            echoVFX = GetComponent<BoomerangEchoVFX>() ?? gameObject.AddComponent<BoomerangEchoVFX>();

        if (shieldVFX == null)
            shieldVFX = GetComponent<BoomerangShieldVFX>() ?? gameObject.AddComponent<BoomerangShieldVFX>();

        Transform spinTarget = (visualPivot != null && visualPivot.IsChildOf(transform)) ? visualPivot : ((modelRoot != null && modelRoot.IsChildOf(transform)) ? modelRoot : transform);
        Transform upperTip = (piramideSuperior != null && piramideSuperior.IsChildOf(transform)) ? piramideSuperior : transform;
        Transform lowerTip = (piramideInferior != null && piramideInferior.IsChildOf(transform)) ? piramideInferior : transform;

        airSpinVFX.Initialize(spinTarget, upperTip, lowerTip);
        electricCrystalVFX.Initialize(piramideSuperior, piramideInferior, baseLaminaSuperior, baseLaminaInferior);
        echoVFX.Initialize(spinTarget);
        shieldVFX.Initialize(spinTarget);
        shieldVFX.enableMeleeParalysisUpgrade = IsShieldParalysisActive;
    }

    private void TryBindPlayerReferences()
    {
        if (weaponManager == null)
        {
            weaponManager = GetComponentInParent<Player_WeaponManager>();
            if (weaponManager == null)
            {
                GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null)
                    weaponManager = playerGO.GetComponent<Player_WeaponManager>() ?? playerGO.GetComponentInChildren<Player_WeaponManager>();
            }
        }

        if (weaponManager != null)
        {
            playerMovement = weaponManager.GetComponent<PlayerM>() ?? weaponManager.GetComponentInParent<PlayerM>();
            offensiveStats = weaponManager.GetComponent<PlayerAttributesOffensive>() ?? weaponManager.GetComponentInParent<PlayerAttributesOffensive>() ?? PlayerAttributesOffensive.Instance;
            tier4EffectManager = weaponManager.GetComponentInChildren<Tier4EffectManager>() ?? weaponManager.GetComponentInParent<Tier4EffectManager>();
            rightHandAnchor = weaponManager.rightHand;
            playerAnimator = weaponManager.playerAnimator;

            if (backSpineAnchor == null)
            {
                Transform searchRoot = weaponManager.transform;
                backSpineAnchor = FindChildRecursive(searchRoot, "mixamorig:Spine2")
                               ?? FindChildRecursive(searchRoot, "mixamorig:Spine1")
                               ?? FindChildRecursive(searchRoot, "Spine2");
            }
        }
    }

    private bool IsInLobbyRack()
    {
        return GetComponentInParent<WeaponRack>() != null;
    }

    private bool IsEquippedByPlayer()
    {
        if (IsInLobbyRack() || weaponManager == null) return false;
        if (weaponManager.currentWeapon != null)
            return weaponManager.currentWeapon == gameObject;
        return transform.parent != null && (transform.parent.name.Contains("RightHand") || transform.parent.name.Contains("Spine"));
    }

    private void OnDisable()
    {
        hasBlendedOutOfThrowAnim = false;
        StopLoopSfx();
        if (playerMovement != null)
            playerMovement.SetPlasmaFiring(false);
        RestoreAimCursor();
        if (aimIndicatorObject != null) aimIndicatorObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (aimIndicatorMesh != null) Destroy(aimIndicatorMesh);
        if (aimIndicatorMaterial != null) Destroy(aimIndicatorMaterial);
        if (aimIndicatorObject != null) Destroy(aimIndicatorObject);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !IsInLobbyRack() && IsEquippedByPlayer())
        {
            UpdateAimCursor();
        }
    }

    private void UpdateAimCursor()
    {
        if (AimSettings.IsGameplayUIOpen())
            return;

        if (alwaysShowCursorWhenEquipped || AimSettings.IsMouseAim)
        {
            aimCursorOverrideActive = true;
            AimSettings.ShowAimCursor();
        }
        else
        {
            RestoreAimCursor();
        }
    }

    private void RestoreAimCursor()
    {
        if (!aimCursorOverrideActive) return;

        AimSettings.HideAimCursorIfNoUI();
        aimCursorOverrideActive = false;
    }

    private void Update()
    {
        if (IsInLobbyRack())
            return;

        TryBindPlayerReferences();
        if (!IsEquippedByPlayer())
        {
            StopLoopSfx();
            RestoreAimCursor();
            if (aimIndicatorObject != null) aimIndicatorObject.SetActive(false);
            return;
        }

        UpdateAimCursor();
        UpdateLoopSfxVolume();

        if (itemFloat != null && itemFloat.enabled)
            itemFloat.enabled = false;

        float dt = Time.deltaTime;
        stateElapsedTime += dt;

        // Sincroniza flag de upgrade Tier 4 no módulo de escudo
        if (shieldVFX != null)
            shieldVFX.enableMeleeParalysisUpgrade = IsShieldParalysisActive;

        // Atalhos rápidos no Editor para testar variantes em tempo real (Ctrl + 1 / 2 / 3)
#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.LeftControl))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                enableElectricCrystalVariant = !enableElectricCrystalVariant;
                Debug.Log($"[BoomerangWeapon] Variante 1 (Cristal Desacoplante Elétrico): {enableElectricCrystalVariant}");
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                enableTranslucentClonesOnReturn = !enableTranslucentClonesOnReturn;
                Debug.Log($"[BoomerangWeapon] Variante 2 (Clones Translúcidos na Volta): {enableTranslucentClonesOnReturn}");
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                enableShieldMeleeParalysis = !enableShieldMeleeParalysis;
                Debug.Log($"[BoomerangWeapon] Upgrade Melee (Escudo Paralisia): {enableShieldMeleeParalysis}");
            }
        }
#endif

        if (useBuiltInStateAndPhysics)
        {
            HandleBuiltInInputs(dt);
        }

        UpdateCurrentStateVisualsAndLogic(dt);
        UpdatePostThrowRunBlend();
        UpdateGroundAimIndicator();

        if (echoVFX != null)
        {
            echoVFX.UpdateEchoes(dt);
        }
    }

    // =========================================================================
    // MÁQUINA DE ESTADOS — TRANSIÇÕES E VISUAIS
    // =========================================================================

    /// <summary>
    /// Muda o estado do Bumerangue na Máquina de Estados e configura automaticamente
    /// todos os encaixes visuais (Costas, Mão, Ar, Escudo), animações e VFX.
    /// Pode ser chamado diretamente pela lógica de física ou combate.
    /// </summary>
    public void SetState(BoomerangState newState)
    {
        if (currentState == newState && stateElapsedTime > 0.01f)
            return;

        BoomerangState previousState = currentState;
        ExitStateVisuals(previousState);

        currentState = newState;
        stateElapsedTime = 0f;

        EnterStateVisuals(newState, previousState);
        OnStateChanged?.Invoke(previousState, newState);
    }

    private void ExitStateVisuals(BoomerangState exitingState)
    {
        switch (exitingState)
        {
            case BoomerangState.SpinningShield:
                StopLoopSfx();
                if (shieldVFX != null) shieldVFX.SetShieldActive(false);
                SetAnimatorBoolSafe(AnimBoolIsDefending, false);
                SetAnimatorBoolSafe(AnimBoolIsReloading, false);
                if (lockPlayerMovementDuringShield && playerMovement != null)
                    playerMovement.SetPlasmaFiring(false);
                break;

            case BoomerangState.Charging:
                StopLoopSfx();
                SetAnimatorBoolSafe(AnimBoolIsCharging, false);
                if (newStateNeedsResetSpeed())
                {
                    SetAnimatorFloatSafe(AnimFloatAttackSpeed, 1f);
                }
                isWindupThrowPending = false;
                hasCommittedDraw = false;
                drawCommitTime = 0f;
                isReleaseRequested = false;
                isQuickTapRelease = false;
                animEventReleaseSignaled = false;
                passedEventWhileHolding = false;
                releaseWaitElapsed = 0f;
                if (playerMovement != null)
                    playerMovement.SetPlasmaFiring(false);
                if (airSpinVFX != null)
                    airSpinVFX.ResetChargeVisual();
                chargeSpinAngle = 0f;
                break;

                bool newStateNeedsResetSpeed() => currentState != BoomerangState.Throw;

            case BoomerangState.Throw:
            case BoomerangState.Returning:
                StopLoopSfx();
                if (airSpinVFX != null) airSpinVFX.SetAirSpinActive(false);
                if (echoVFX != null) echoVFX.SetEchoEmission(false, false);
                break;
        }
    }

    private void EnterStateVisuals(BoomerangState enteringState, BoomerangState previousState)
    {
        switch (enteringState)
        {
            case BoomerangState.Stowed:
                // Preso nas costas do astronauta (animação Astronaut_guardando_boomerang)
                ClearThrowMovementLock();
                StopLoopSfx();
                isDrawnInHand = false;
                if (electricCrystalVFX != null) electricCrystalVFX.SnapCoupledImmediate();
                if (airSpinVFX != null) airSpinVFX.SetAirSpinActive(false);
                if (shieldVFX != null) shieldVFX.SetShieldActive(false);
                if (echoVFX != null) echoVFX.SetEchoEmission(false, false);

                if (previousState != BoomerangState.Stowed)
                {
                    TriggerPlayerAnimation(AnimTriggerHolster);
                    PlayOneShotSfx(sfxGuardando, 0.9f);
                }
                AttachToBackImmediate();
                break;

            case BoomerangState.Charging:
                // Na mão direita do astronauta carregando o arremesso
                StopLoopSfx();
                if (!isDrawnInHand || previousState == BoomerangState.Stowed)
                {
                    TriggerPlayerAnimation(AnimTriggerDraw);
                    if (previousState == BoomerangState.Stowed)
                    {
                        PlayOneShotSfx(sfxArmando, 0.9f);
                    }
                }
                isDrawnInHand = true;
                AttachToHandImmediate();
                if (electricCrystalVFX != null) electricCrystalVFX.SnapCoupledImmediate();
                if (airSpinVFX != null) airSpinVFX.SetAirSpinActive(false);
                if (shieldVFX != null) shieldVFX.SetShieldActive(false);
                SetAnimatorBoolSafe(AnimBoolIsCharging, true);
                break;

            case BoomerangState.Throw:
                // Arremessado no ar em alta rotação (a animação Attack1 já começou na antecipação;
                // só dispara o trigger aqui caso LaunchThrow tenha sido chamado direto por script externo)
                hitTargetsThisPhase.Clear();
                hitCooldowns.Clear();
                hasBlendedOutOfThrowAnim = false;
                ClearThrowMovementLock();
                SetAnimatorFloatSafe(AnimFloatAttackSpeed, throwFollowThroughSpeed * GetAttackSpeedMultiplier());
                if (!IsInAttackAnimationState())
                {
                    TriggerPlayerAnimation(AnimTriggerAttack);
                    TriggerPlayerAnimation(AnimTriggerShoot);
                }

                DetachIntoWorldSpace();
                StartLoopSfx(GetSpinClipForCharge(currentCharge01), 0.78f, true);

                float areaMult = GetPlayerAreaMultiplier();
                if (airSpinVFX != null)
                {
                    airSpinVFX.SetRadiusMultiplier(IsElectricCrystalActive ? areaMult * 1.4f : areaMult);
                    airSpinVFX.SetAirSpinActive(true);
                }

                if (electricCrystalVFX != null)
                {
                    electricCrystalVFX.SetDecoupledState(IsElectricCrystalActive, areaMult);
                }

                if (echoVFX != null)
                {
                    echoVFX.SetEchoEmission(IsTranslucentClonesActive, false);
                }
                break;

            case BoomerangState.Returning:
                // Voltando para o jogador com DynamicHoming e Clones Translúcidos na Volta
                hitTargetsThisPhase.Clear();
                hitCooldowns.Clear();
                DetachIntoWorldSpace();
                StartLoopSfx(sfxVoltandoParaMao, 0.88f, true);

                if (airSpinVFX != null)
                    airSpinVFX.SetAirSpinActive(true);

                if (echoVFX != null)
                    echoVFX.SetEchoEmission(IsTranslucentClonesActive, true);

                currentReturnSpeed = Mathf.Min(Mathf.Max(returnHomingSpeed, flightVelocity.magnitude) * Mathf.Lerp(1f, GetAttackSpeedMultiplier(), 0.4f), 28f);

                // Inverte imediatamente qualquer velocidade residual para frente ao atingir a distância limite,
                // evitando ultrapassar a ponta da marcação no chão antes de iniciar o retorno
                if (weaponManager != null)
                {
                    Vector3 toPlayerAtTurn = (weaponManager.transform.position + Vector3.up * 1.1f) - transform.position;
                    toPlayerAtTurn.y = 0f;
                    if (toPlayerAtTurn.sqrMagnitude > 0.001f)
                    {
                        Vector3 backDir = toPlayerAtTurn.normalized;
                        if (Vector3.Dot(flightVelocity, backDir) <= 0.1f)
                        {
                            Vector3 sideDir = Vector3.Cross(Vector3.up, backDir).normalized;
                            flightVelocity = (backDir * 0.94f + sideDir * 0.34f).normalized * currentReturnSpeed;
                        }
                    }
                }
                break;

            case BoomerangState.SpinningShield:
                // Escudo Giratório Defensivo ao redor do corpo do player (Astronaut_defense)
                ClearThrowMovementLock();
                isDrawnInHand = true;
                DetachIntoWorldSpace();
                PlayOneShotSfx(sfxRefletirInicio, 0.95f);
                StartLoopSfx(sfxRefletirGiroLoop != null ? sfxRefletirGiroLoop : sfxRefletirInicio, 0.78f, true);

                if (electricCrystalVFX != null)
                    electricCrystalVFX.SnapCoupledImmediate();

                if (airSpinVFX != null)
                    airSpinVFX.SetAirSpinActive(false);

                if (echoVFX != null)
                    echoVFX.SetEchoEmission(false, false);

                if (shieldVFX != null)
                    shieldVFX.SetShieldActive(true, true);

                SetAnimatorBoolSafe(AnimBoolIsDefending, true);
                SetAnimatorBoolSafe(AnimBoolIsReloading, true);

                if (lockPlayerMovementDuringShield && playerMovement != null)
                    playerMovement.SetPlasmaFiring(true);
                break;
        }
    }

    // =========================================================================
    // ENCAIXES VISUAIS: MÃO DIREITA, COSTAS E MUNDO
    // =========================================================================

    /// <summary>
    /// Coloca a Glaive já pronta na mão do astronauta (estado Charging), evitando iniciar guardada nas costas.
    /// </summary>
    public void EquipInHandImmediate()
    {
        currentState = BoomerangState.Charging;
        stateElapsedTime = 0f;
        isDrawnInHand = true;
        TryBindPlayerReferences();
        AttachToHandImmediate();
        SetAnimatorBoolSafe(AnimBoolIsCharging, true);
    }

    /// <summary>
    /// Prende o bumerangue visualmente na mão direita do astronauta (mixamorig:RightHand / Hand_Hitbx).
    /// </summary>
    public void AttachToHandImmediate()
    {
        TryBindPlayerReferences();
        if (weaponOffset != null)
        {
            weaponOffset.isStowedOnBack = false;
            weaponOffset.disableEditorLiveHandSnap = false;
        }

        SnapToHandPose();

        if (visualPivot != null)
        {
            chargeSpinAngle = 0f;
            visualPivot.localPosition = Vector3.zero;
            visualPivot.localRotation = Quaternion.identity;
        }
    }

    private void SnapToHandPose()
    {
        if (rightHandAnchor == null) return;
        if (transform.parent != rightHandAnchor)
            transform.SetParent(rightHandAnchor, false);

        transform.localPosition = weaponOffset != null ? weaponOffset.equipPosition : handMountPosition;
        transform.localEulerAngles = weaponOffset != null ? weaponOffset.equipRotation : handMountRotation;
        ApplyCompensatedScale(rightHandAnchor, equippedWorldScale);
    }

    /// <summary>
    /// Prende o bumerangue visualmente nas costas do astronauta (mixamorig:Spine2).
    /// </summary>
    public void AttachToBackImmediate()
    {
        TryBindPlayerReferences();
        if (weaponOffset != null)
        {
            weaponOffset.isStowedOnBack = true;
            weaponOffset.disableEditorLiveHandSnap = false;
        }

        SnapToBackPose();

        if (visualPivot != null)
        {
            chargeSpinAngle = 0f;
            visualPivot.localPosition = Vector3.zero;
            visualPivot.localRotation = Quaternion.identity;
        }
    }

    private void SnapToBackPose()
    {
        Transform targetBack = backSpineAnchor != null ? backSpineAnchor : rightHandAnchor;
        if (targetBack == null) return;
        if (transform.parent != targetBack)
            transform.SetParent(targetBack, false);

        transform.localPosition = weaponOffset != null ? weaponOffset.stowedBackPosition : backMountPosition;
        transform.localEulerAngles = weaponOffset != null ? weaponOffset.stowedBackRotation : backMountRotation;
        ApplyCompensatedScale(targetBack, equippedWorldScale);
    }

    private void ApplyCompensatedScale(Transform parentAnchor, Vector3 desiredWorldScale)
    {
        Vector3 pLossy = parentAnchor.lossyScale;
        float sx = Mathf.Abs(pLossy.x) > 0.0001f ? desiredWorldScale.x / pLossy.x : desiredWorldScale.x;
        float sy = Mathf.Abs(pLossy.y) > 0.0001f ? desiredWorldScale.y / pLossy.y : desiredWorldScale.y;
        float sz = Mathf.Abs(pLossy.z) > 0.0001f ? desiredWorldScale.z / pLossy.z : desiredWorldScale.z;
        transform.localScale = new Vector3(sx, sy, sz);
    }

    private void LateUpdate()
    {
        if (IsInLobbyRack() || !IsEquippedByPlayer()) return;

        UpdateAimCursor();

        // Garante que a Glaive permaneça 100% colada na mão (ou nas costas) após o Animator atualizar os ossos
        if (currentState == BoomerangState.Charging)
        {
            SnapToHandPose();
        }
        else if (currentState == BoomerangState.Stowed)
        {
            SnapToBackPose();
        }
    }

    /// <summary>
    /// Desacopla o bumerangue do esqueleto do player para voar livremente no mundo ou girar como escudo.
    /// </summary>
    public void DetachIntoWorldSpace()
    {
        if (weaponOffset != null)
        {
            weaponOffset.disableEditorLiveHandSnap = true;
        }

        Vector3 worldPos = transform.position;
        if (weaponManager != null && Vector3.Distance(worldPos, weaponManager.transform.position) > 4f && currentState != BoomerangState.Returning)
        {
            worldPos = weaponManager.transform.position + Vector3.up * 1.1f + weaponManager.transform.forward * 0.6f;
        }

        Vector3 flyDir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : (weaponManager != null ? weaponManager.transform.forward : transform.forward);
        flyDir.y = 0f;
        if (flyDir.sqrMagnitude < 0.001f) flyDir = Vector3.forward;

        transform.SetParent(null, true);
        transform.position = worldPos;
        transform.rotation = Quaternion.LookRotation(flyDir.normalized, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
        transform.localScale = equippedWorldScale;
    }

    // =========================================================================
    // ATUALIZAÇÃO POR FRAME DOS VISUAIS E PROTÓTIPO DE FÍSICA
    // =========================================================================

    private void UpdateCurrentStateVisualsAndLogic(float dt)
    {
        switch (currentState)
        {
            case BoomerangState.Stowed:
                // Mantém encaixado nas costas
                break;

            case BoomerangState.Charging:
                // Na mão carregando o arremesso: giro acelerado da lâmina, inclinação e brilho progressivo
                bool isActivelyCharging = currentChargeTime > 0.01f;

                if (isActivelyCharging)
                {
                    // Acelera o giro de 360°/s (início) até 1800°/s (carga total)
                    float spinSpeed = Mathf.Lerp(360f, 1800f, currentCharge01);
                    chargeSpinAngle = (chargeSpinAngle + spinSpeed * dt) % 360f;

                    if (airSpinVFX != null)
                        airSpinVFX.UpdateChargeVisual(currentCharge01, dt);
                }
                else
                {
                    // Em repouso na mão (idle), desacelera suavemente para a orientação padrão
                    chargeSpinAngle = Mathf.MoveTowardsAngle(chargeSpinAngle, 0f, 720f * dt);
                    if (airSpinVFX != null)
                        airSpinVFX.ResetChargeVisual();
                }

                if (visualPivot != null)
                {
                    float pullBackAngle = Mathf.Lerp(0f, -24f, currentCharge01);
                    float shake = currentCharge01 > 0.7f ? Mathf.Sin(Time.time * 60f) * 2.2f * currentCharge01 : 0f;
                    Quaternion pullBack = Quaternion.Euler(shake, 0f, pullBackAngle);
                    Quaternion spin = Quaternion.AngleAxis(chargeSpinAngle, Vector3.forward);
                    visualPivot.localRotation = pullBack * spin;
                }

                if (useBuiltInStateAndPhysics)
                    OnChargingUpdate(dt);
                break;

            case BoomerangState.Throw:
                if (airSpinVFX != null)
                    airSpinVFX.UpdateAirSpin(dt, 1f + currentCharge01 * 0.35f);

                if (electricCrystalVFX != null)
                    electricCrystalVFX.UpdateElectricCrystals(dt);

                if (useBuiltInStateAndPhysics)
                    OnThrowPhysicsUpdate(dt);
                break;

            case BoomerangState.Returning:
                if (airSpinVFX != null)
                    airSpinVFX.UpdateAirSpin(dt, 1.2f);

                if (electricCrystalVFX != null)
                {
                    // Quando está quase chegando na mão do player, começa a reacoplar os cristais suavemente
                    Transform playerTr = weaponManager != null ? weaponManager.transform : null;
                    if (playerTr != null && Vector3.Distance(transform.position, playerTr.position + Vector3.up * 1.1f) < 2.2f)
                    {
                        electricCrystalVFX.SetDecoupledState(false, GetPlayerAreaMultiplier());
                    }
                    electricCrystalVFX.UpdateElectricCrystals(dt);
                }

                if (useBuiltInStateAndPhysics)
                    OnReturningPhysicsUpdate(dt);
                break;

            case BoomerangState.SpinningShield:
                Transform playerTransform = weaponManager != null ? weaponManager.transform : transform;
                if (shieldVFX != null)
                {
                    shieldVFX.UpdateShield(playerTransform, dt, IsInParryWindow);
                }

                if (useBuiltInStateAndPhysics)
                    OnSpinningShieldUpdate(dt);
                break;
        }
    }

    // =========================================================================
    // #region PHYSICS_AND_COMBAT_HOOKS
    // (Métodos expansíveis para física vetorial customizada ou desativar useBuiltInStateAndPhysics)
    // =========================================================================

    private void HandleBuiltInInputs(float dt)
    {
        if (AimSettings.IsGameplayUIOpen())
        {
            if (isWindupThrowPending && !isReleaseRequested)
            {
                CancelAttackAnticipation();
            }
            return;
        }

        // Tecla G: Alterna entre Stowed (preso nas costas) e empunhado na mão
        if (Input.GetKeyDown(KeyCode.G) && (currentState == BoomerangState.Stowed || (currentState == BoomerangState.Charging && currentChargeTime <= 0.01f)))
        {
            if (currentState == BoomerangState.Stowed)
            {
                currentChargeTime = 0f;
                currentCharge01 = 0f;
                SetState(BoomerangState.Charging);
            }
            else
            {
                SetState(BoomerangState.Stowed);
            }
            return;
        }

        // Botão Direito do Mouse ou Tecla R: Escudo Giratório Defensivo (SpinningShield)
        bool shieldHeld = Input.GetMouseButton(1) || Input.GetKey(shieldAlternateKey);
        if (shieldHeld && (currentState == BoomerangState.Stowed || currentState == BoomerangState.Charging || currentState == BoomerangState.SpinningShield))
        {
            if (currentState != BoomerangState.SpinningShield)
            {
                SetState(BoomerangState.SpinningShield);
            }
            return;
        }
        else if (!shieldHeld && currentState == BoomerangState.SpinningShield)
        {
            currentChargeTime = 0f;
            currentCharge01 = 0f;
            SetState(BoomerangState.Charging);
            return;
        }

        // Botão Esquerdo do Mouse: Inicia antecipação do ataque imediatamente ao clicar;
        // velocidade da antecipação desacelera se segurar para carregar, e solta a Glaive no timing exato ao largar.
        if (currentState == BoomerangState.Stowed || currentState == BoomerangState.Charging)
        {
            if (Input.GetMouseButtonDown(0) && !isWindupThrowPending)
            {
                BeginAttackAnticipation();
            }

            if (isWindupThrowPending && currentState == BoomerangState.Charging)
            {
                UpdateAttackAnticipationAndRelease(dt);
            }
        }
        else if (currentState == BoomerangState.Throw && Input.GetMouseButtonDown(0) && stateElapsedTime > 0.15f)
        {
            // Permite puxar o bumerangue de volta mais cedo clicando novamente
            SetState(BoomerangState.Returning);
        }
    }

    /// <summary>
    /// Chamado via Animation Event BoomerangDrawReady no início da puxada para confirmar o arremesso.
    /// Soltar o clique antes desse ponto cancela a animação sem jogar o bumerangue (comportamento estilo arco).
    /// </summary>
    public void AnimEvent_BoomerangDrawReady()
    {
        if (!isWindupThrowPending || hasCommittedDraw) return;

        hasCommittedDraw = true;
        drawCommitTime = currentChargeTime;
        fullChargeReachedAt = -1f;
    }

    /// <summary>
    /// Chamado via Animation Event ThrowBoomerang no clipe de arremesso no frame exato em que a Glaive deixa a mão.
    /// </summary>
    public void AnimEvent_BoomerangReleasePoint()
    {
        if (!isWindupThrowPending) return;

        // Se chegou direto no ponto de arremesso com o botão ainda pressionado, considera também já armado
        if (!hasCommittedDraw)
        {
            hasCommittedDraw = true;
            drawCommitTime = currentChargeTime;
        }

        if (isReleaseRequested)
        {
            animEventReleaseSignaled = true;
        }
        else
        {
            // Se o evento disparar antes da soltura (ex: ajuste no clipe), segura até o player soltar ou atingir o limite
            passedEventWhileHolding = true;
        }
    }

    private void BeginAttackAnticipation()
    {
        currentChargeTime = 0f;
        currentCharge01 = 0f;
        isWindupThrowPending = true;
        hasCommittedDraw = !requireDrawReadyEvent;
        drawCommitTime = 0f;
        fullChargeReachedAt = -1f;
        isPerfectChargeThrow = false;
        isReleaseRequested = false;
        isQuickTapRelease = false;
        animEventReleaseSignaled = false;
        passedEventWhileHolding = false;
        releaseWaitElapsed = 0f;
        hasBlendedOutOfThrowAnim = false;

        if (currentState != BoomerangState.Charging)
        {
            SetState(BoomerangState.Charging);
        }
        else
        {
            SnapToHandPose();
        }

        float atkMult = GetAttackSpeedMultiplier();
        SetAnimatorFloatSafe(AnimFloatAttackSpeed, initialAnticipationSpeed * Mathf.Lerp(1f, atkMult, 0.55f));
        TriggerPlayerAnimation(AnimTriggerAttack);

        if (playerMovement != null)
        {
            Vector3 aimDir = GetMouseAimDirection();
            RotatePlayerTowardsDirection(aimDir);
            playerMovement.SetPlasmaFiring(true, GetChargingMoveSpeedMultiplier());
            playerMovement.SetPlasmaAimDirection(aimDir);
        }

        StartLoopSfx(sfxSegurandoParaLancar, 0.85f, false);
    }

    private void UpdateAttackAnticipationAndRelease(float dt)
    {
        bool buttonHeld = Input.GetMouseButton(0);
        if (!isReleaseRequested)
        {
            if (buttonHeld)
            {
                currentChargeTime += dt;
                float atkMult = GetAttackSpeedMultiplier();
                float effectiveChargeDuration = Mathf.Max(0.12f, maxChargeTime / atkMult);
                float effectiveMaxHold = Mathf.Max(effectiveChargeDuration + 0.15f, maxHoldDuration / Mathf.Lerp(1f, atkMult, 0.5f));

                // Fallback caso o clipe atual no Animator não possua o evento BoomerangDrawReady
                if (!hasCommittedDraw && !requireDrawReadyEvent)
                {
                    hasCommittedDraw = true;
                    drawCommitTime = currentChargeTime;
                }

                // A carga real começa em 0 exatamente no evento BoomerangDrawReady e cresce de forma determinística
                if (hasCommittedDraw)
                {
                    float heldSinceCommit = Mathf.Max(0f, currentChargeTime - drawCommitTime);
                    currentCharge01 = Mathf.Clamp01(heldSinceCommit / effectiveChargeDuration);
                    if (currentCharge01 >= 0.999f && fullChargeReachedAt < 0f)
                    {
                        fullChargeReachedAt = currentChargeTime;
                    }
                }
                else
                {
                    currentCharge01 = 0f;
                }

                Vector3 aimDir = GetMouseAimDirection();
                RotatePlayerTowardsDirection(aimDir);

                if (playerMovement != null)
                {
                    playerMovement.SetPlasmaFiring(true, GetChargingMoveSpeedMultiplier());
                    playerMovement.SetPlasmaAimDirection(aimDir);
                }

                float speed = ComputeHoldAnticipationSpeed();
                SetAnimatorFloatSafe(AnimFloatAttackSpeed, speed);

                // Limite máximo segurando: não deixa ficar carregando para sempre e arremessa com força máxima
                if (currentChargeTime >= effectiveMaxHold)
                {
                    if (!hasCommittedDraw)
                    {
                        hasCommittedDraw = true;
                        drawCommitTime = currentChargeTime;
                    }
                    isPerfectChargeThrow = false;
                    TriggerReleaseSequence(isQuickTap: false);
                }
            }

            if (!isReleaseRequested && (Input.GetMouseButtonUp(0) || !buttonHeld))
            {
                // Se soltou ANTES de passar pelo evento BoomerangDrawReady, cancela igual um arco sem atirar
                if (!hasCommittedDraw)
                {
                    CancelAttackAnticipation();
                    return;
                }

                // Timing perfeito: soltou dentro de 0.30s após atingir carga total (antes do limite automático)
                isPerfectChargeThrow = fullChargeReachedAt >= 0f && (currentChargeTime - fullChargeReachedAt) <= 0.30f;

                float heldAfterCommit = Mathf.Max(0f, currentChargeTime - drawCommitTime);
                bool isFastPostCommit = heldAfterCommit <= quickReleaseAfterCommitWindow;
                TriggerReleaseSequence(isFastPostCommit);
            }
        }

        if (!isReleaseRequested) return;

        if (playerMovement != null)
        {
            Vector3 aimDir = GetMouseAimDirection();
            playerMovement.SetPlasmaFiring(true, GetChargingMoveSpeedMultiplier());
            playerMovement.SetPlasmaAimDirection(aimDir);
        }

        releaseWaitElapsed += dt;
        float attackNorm = GetAttackStateNormalizedTime();
        bool reachedReleaseFallback = IsInAttackAnimationState() && attackNorm >= releaseNormalizedTime && releaseWaitElapsed >= 0.08f;
        bool failsafeExpired = releaseWaitElapsed >= (isQuickTapRelease ? 0.28f : 0.30f);

        if (animEventReleaseSignaled || passedEventWhileHolding || reachedReleaseFallback || failsafeExpired)
        {
            // Mantém EXATAMENTE a carga que o indicador no chão exibiu (sem cortes artificiais!)
            float launchCharge = currentCharge01;
            isWindupThrowPending = false;
            hasCommittedDraw = false;
            isReleaseRequested = false;
            animEventReleaseSignaled = false;
            passedEventWhileHolding = false;

            // Destrava o movimento imediatamente assim que o bumerangue sai da mão
            ClearThrowMovementLock();

            SetAnimatorFloatSafe(AnimFloatAttackSpeed, throwFollowThroughSpeed * GetAttackSpeedMultiplier());
            LaunchThrow(launchCharge);
        }
    }

    private void CancelAttackAnticipation()
    {
        ClearThrowMovementLock();
        StopLoopSfx();
        isWindupThrowPending = false;
        hasCommittedDraw = false;
        drawCommitTime = 0f;
        isReleaseRequested = false;
        isQuickTapRelease = false;
        animEventReleaseSignaled = false;
        passedEventWhileHolding = false;
        releaseWaitElapsed = 0f;
        currentChargeTime = 0f;
        currentCharge01 = 0f;
        chargeSpinAngle = 0f;

        if (playerMovement != null)
        {
            playerMovement.SetPlasmaFiring(false);
        }

        if (airSpinVFX != null)
        {
            airSpinVFX.ResetChargeVisual();
        }

        if (visualPivot != null)
        {
            visualPivot.localRotation = Quaternion.identity;
        }

        SetAnimatorFloatSafe(AnimFloatAttackSpeed, 1f);

        if (playerAnimator != null && playerAnimator.isActiveAndEnabled)
        {
            playerAnimator.ResetTrigger(AnimTriggerAttack);
            if (IsInAttackAnimationState())
            {
                bool isMoving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.05f;
                playerAnimator.CrossFadeInFixedTime(isMoving ? "Running" : "Idle Default State", 0.14f, 0);
            }
        }
    }

    private float ComputeHoldAnticipationSpeed()
    {
        // Enquanto ainda não atingiu BoomerangDrawReady, mantém velocidade consistente para o mínimo de segurada estilo arco
        if (!hasCommittedDraw)
        {
            return initialAnticipationSpeed;
        }

        // Após armar em BoomerangDrawReady, desacelera matematicamente enquanto continua segurando
        float timeSinceCommit = Mathf.Max(0f, currentChargeTime - drawCommitTime);
        float slowBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timeSinceCommit / 0.18f));
        float baseSpeed = Mathf.Lerp(initialAnticipationSpeed, chargeHoldAnticipationSpeed, slowBlend);

        // Freia progressivamente até 0 ao se aproximar de holdAtApexNormalizedTime (justo antes do evento ThrowBoomerang)
        if (IsInAttackAnimationState())
        {
            float normTime = GetAttackStateNormalizedTime();
            if (normTime >= holdAtApexNormalizedTime)
            {
                return 0f;
            }

            float brakeStart = Mathf.Max(0.05f, holdAtApexNormalizedTime - 0.06f);
            if (normTime > brakeStart)
            {
                float brakeFactor = 1f - Mathf.InverseLerp(brakeStart, holdAtApexNormalizedTime, normTime);
                return baseSpeed * brakeFactor;
            }
        }

        return baseSpeed;
    }

    private void TriggerReleaseSequence(bool isQuickTap)
    {
        isReleaseRequested = true;
        isQuickTapRelease = isQuickTap;
        releaseWaitElapsed = 0f;

        float postReleaseSpeed = isQuickTap ? quickTapAnticipationSpeed : throwFollowThroughSpeed;
        SetAnimatorFloatSafe(AnimFloatAttackSpeed, postReleaseSpeed);
    }

    private bool IsInAttackAnimationState()
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return false;
        AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName(attackStateName)) return true;
        if (playerAnimator.IsInTransition(0))
        {
            AnimatorStateInfo next = playerAnimator.GetNextAnimatorStateInfo(0);
            if (next.IsName(attackStateName)) return true;
        }
        return false;
    }

    private float GetAttackStateNormalizedTime()
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return 1f;
        AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName(attackStateName)) return info.normalizedTime;
        if (playerAnimator.IsInTransition(0))
        {
            AnimatorStateInfo next = playerAnimator.GetNextAnimatorStateInfo(0);
            if (next.IsName(attackStateName)) return next.normalizedTime;
        }
        return 0f;
    }

    private void SetAnimatorFloatSafe(int floatHash, float value)
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return;
        foreach (AnimatorControllerParameter p in playerAnimator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float && p.nameHash == floatHash)
            {
                playerAnimator.SetFloat(floatHash, value);
                return;
            }
        }
    }

    private float GetChargingMoveSpeedMultiplier()
    {
        return Mathf.Lerp(aimingMoveSpeedMultiplier, minChargeMoveSpeedMultiplier, Mathf.Clamp01(currentCharge01));
    }

    private void ClearThrowMovementLock()
    {
        if (playerMovement != null && !isWindupThrowPending)
        {
            playerMovement.SetPlasmaFiring(false);
        }
    }

    private void UpdatePostThrowRunBlend()
    {
        if (hasBlendedOutOfThrowAnim || isWindupThrowPending) return;
        if (currentState != BoomerangState.Throw && currentState != BoomerangState.Returning) return;
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return;

        if (!IsInAttackAnimationState())
        {
            hasBlendedOutOfThrowAnim = true;
            return;
        }

        // Permite um leve snap visual da soltura da mão (~0.05s) enquanto o player já está 100% destravado fisicamente,
        // e então faz blend imediato para Running se houver input de movimento (evita deslizar em pose estática).
        if (currentState == BoomerangState.Throw && stateElapsedTime < 0.05f) return;

        bool hasMoveInput = (playerMovement != null && playerMovement.MoveDirection.sqrMagnitude > 0.01f)
                            || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.05f
                            || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.05f;
        if (!hasMoveInput) return;

        SetAnimatorFloatSafe(AnimFloatAttackSpeed, 1f);
        playerAnimator.CrossFadeInFixedTime("Running", 0.12f, 0);
        hasBlendedOutOfThrowAnim = true;
    }

    protected virtual void OnChargingUpdate(float dt)
    {
        // Hook para lógica extra durante o carregamento
    }

    /// <summary>
    /// Inicia o arremesso vetorial com base no nível de carga (0..1).
    /// </summary>
    public virtual void LaunchThrow(float charge01)
    {
        hasBlendedOutOfThrowAnim = false;
        ClearThrowMovementLock();
        StopLoopSfx();

        chargeSpinAngle = 0f;
        currentCharge01 = Mathf.Clamp01(charge01);
        PlayOneShotSfx(sfxJogando, 1.0f, Mathf.Lerp(0.96f, 1.08f, currentCharge01));
        Vector3 aimDir = GetMouseAimDirection();
        RotatePlayerTowardsDirection(aimDir);

        float atkMult = GetAttackSpeedMultiplier();
        float launchSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, currentCharge01) * Mathf.Lerp(1f, atkMult, 0.4f);
        targetFlightDistance = GetEffectiveThrowDistance(currentCharge01);

        int bonusBounces = offensiveStats != null ? Mathf.Max(0, offensiveStats.bounceCount) : 0;
        bouncesRemaining = Mathf.RoundToInt(Mathf.Lerp(minBounces, maxBounces, currentCharge01)) + bonusBounces;
        piercingRemaining = offensiveStats != null ? Mathf.Max(0, offensiveStats.piercing) : 0;

        Transform originTr = weaponManager != null ? weaponManager.transform : transform;
        throwOriginPos = originTr.position + Vector3.up * 1.1f;
        const float launchOffset = 0.65f;
        transform.position = throwOriginPos + aimDir * launchOffset;
        transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);

        // Começa contando a distância a partir do jogador (launchOffset) para coincidir 100% com a ponta do indicador no chão
        travelledDistance = launchOffset;
        flightVelocity = aimDir * launchSpeed;

        SetState(BoomerangState.Throw);

        // Multishot: spawna lâminas-eco espectrais em leque quando multiShotChance ativar
        SpawnMultishotSpectralClones(throwOriginPos + aimDir * launchOffset, aimDir, launchSpeed, targetFlightDistance, atkMult);
    }

    private void SpawnMultishotSpectralClones(Vector3 spawnPos, Vector3 aimDir, float speed, float range, float atkMult)
    {
        if (echoVFX == null || offensiveStats == null || offensiveStats.multiShotChance <= 0f) return;

        float chance = Mathf.Max(0f, offensiveStats.multiShotChance);
        int extraCount = Mathf.FloorToInt(chance / 100f);
        float remainder = (chance % 100f) / 100f;
        if (UnityEngine.Random.value < remainder) extraCount++;
        extraCount = Mathf.Clamp(extraCount, 0, 4);
        if (extraCount <= 0) return;

        float spreadAngle = offensiveStats.spread > 0.5f ? offensiveStats.spread : 14f;
        Transform ownerRoot = weaponManager != null ? weaponManager.transform.root : transform.root;

        for (int i = 0; i < extraCount; i++)
        {
            int pairIndex = (i / 2) + 1;
            float sign = (i % 2 == 0) ? 1f : -1f;
            float angle = sign * spreadAngle * pairIndex;
            Vector3 cloneDir = Quaternion.Euler(0f, angle, 0f) * aimDir;
            echoVFX.SpawnSpectralMultishotClone(
                spawnPos,
                cloneDir,
                speed * Mathf.Lerp(0.95f, 1.05f, atkMult - 1f),
                range,
                offensiveStats.piercing,
                ownerRoot,
                (hitEnemy, hitPt, dir) => ApplyBoomerangDamageToEnemy(hitEnemy, hitPt, dir, 0.55f));
        }
    }

    protected virtual void OnThrowPhysicsUpdate(float dt)
    {
        float stepDist = flightVelocity.magnitude * dt;
        float remainingDist = Mathf.Max(0f, targetFlightDistance - travelledDistance);
        float clampedStepDist = Mathf.Min(stepDist, remainingDist);
        Vector3 dir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : transform.forward;
        float hitRadius = (electricCrystalVFX != null && IsElectricCrystalActive)
            ? electricCrystalVFX.GetExpandedHitRadius(0.6f)
            : 0.6f;

        // Detecção de colisão / piercing / ricochete em paredes e inimigos
        if (Physics.SphereCast(transform.position, 0.28f, dir, out RaycastHit hit, clampedStepDist + 0.25f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!IsPlayerCollider(hit.collider))
            {
                DummyHealth enemy = hit.collider.GetComponentInParent<DummyHealth>();
                if (enemy != null)
                {
                    int enemyId = enemy.GetInstanceID();
                    bool onCooldown = hitCooldowns.TryGetValue(enemyId, out float cd) && Time.time < cd;
                    ApplyBoomerangDamageToEnemy(enemy, hit.point, dir, 1f);

                    // Piercing: atravessa N inimigos em linha reta sem gastar quique
                    if (piercingRemaining > 0 && !onCooldown)
                    {
                        piercingRemaining--;
                    }
                    else if (!onCooldown)
                    {
                        if (TryConsumeOrRollBounce())
                        {
                            Vector3 eNorm = hit.normal;
                            eNorm.y = 0f;
                            if (eNorm.sqrMagnitude < 0.001f) eNorm = -dir;
                            RegisterBounce(eNorm.normalized, hit.point, enemy);
                            return;
                        }
                        else
                        {
                            SetState(BoomerangState.Returning);
                            return;
                        }
                    }
                }
                else
                {
                    // Colidiu com parede/obstáculo
                    if (TryConsumeOrRollBounce())
                    {
                        Vector3 wNorm = hit.normal;
                        wNorm.y = 0f;
                        if (wNorm.sqrMagnitude < 0.001f) wNorm = -dir;
                        RegisterBounce(wNorm.normalized, hit.point, null);
                        return;
                    }
                    else
                    {
                        SetState(BoomerangState.Returning);
                        return;
                    }
                }
            }
        }

        // Verifica inimigos na área das pontas de cristal desacopladas
        CheckSpinningDiscEnemies(hitRadius);

        transform.position += dir * clampedStepDist;
        travelledDistance += clampedStepDist;

        if (flightVelocity.sqrMagnitude > 0.001f)
        {
            Vector3 flyDir = flightVelocity.normalized;
            flyDir.y = 0f;
            if (flyDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flyDir.normalized, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            }
        }

        if (travelledDistance >= targetFlightDistance - 0.01f)
        {
            SetState(BoomerangState.Returning);
        }
    }

    private bool TryConsumeOrRollBounce()
    {
        if (bouncesRemaining > 0)
        {
            bouncesRemaining--;
            return true;
        }

        if (offensiveStats != null && offensiveStats.bounceChance > 0f)
        {
            if (UnityEngine.Random.value < Mathf.Clamp01(offensiveStats.bounceChance / 100f))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void OnReturningPhysicsUpdate(float dt)
    {
        Transform playerTr = weaponManager != null ? weaponManager.transform : null;
        if (playerTr == null)
        {
            CatchBoomerang();
            return;
        }

        Vector3 targetPos = playerTr.position + Vector3.up * 1.1f;
        Vector3 toPlayer = targetPos - transform.position;
        float distToPlayer = toPlayer.magnitude;

        // Failsafe 1: Captura por proximidade direta (1.4m)
        if (distToPlayer <= 1.4f)
        {
            CatchBoomerang();
            return;
        }

        // Failsafe 2: Failsafe de tempo de retorno (evita loop orbital infinito)
        if (stateElapsedTime > 3.2f)
        {
            CatchBoomerang();
            return;
        }

        // Failsafe 3: Detecção de overshoot (passou pelo jogador a curta distância)
        if (distToPlayer < 3.0f && Vector3.Dot(flightVelocity, toPlayer) < 0f)
        {
            CatchBoomerang();
            return;
        }

        // Limita a velocidade de retorno máxima para 26m/s para NÃO abrir a curva orbital
        float maxReturnSpeed = Mathf.Max(returnHomingSpeed, 26f);
        currentReturnSpeed = Mathf.Min(currentReturnSpeed + returnHomingAcceleration * dt, maxReturnSpeed);

        // Curva firme via RotateTowards (nunca colapsa nem avança para frente quando oposto)
        float turnRate = Mathf.Lerp(38f, 16f, Mathf.Clamp01(distToPlayer / 10f));
        Vector3 desiredDir = toPlayer.normalized;
        Vector3 curDir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : desiredDir;
        flightVelocity = Vector3.RotateTowards(curDir, desiredDir, turnRate * dt, 0f).normalized * currentReturnSpeed;

        Vector3 step = flightVelocity * dt;
        if (step.magnitude >= distToPlayer)
        {
            CatchBoomerang();
            return;
        }

        transform.position += step;

        if (flightVelocity.sqrMagnitude > 0.001f)
        {
            Vector3 flyDir = flightVelocity.normalized;
            flyDir.y = 0f;
            if (flyDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flyDir.normalized, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            }
        }

        float hitRadius = (electricCrystalVFX != null && IsElectricCrystalActive)
            ? electricCrystalVFX.GetExpandedHitRadius(0.6f)
            : 0.6f;
        CheckSpinningDiscEnemies(hitRadius);
    }

    private void CatchBoomerang()
    {
        ClearThrowMovementLock();
        StopLoopSfx();
        currentChargeTime = 0f;
        currentCharge01 = 0f;
        chargeSpinAngle = 0f;
        isPerfectChargeThrow = false;
        SetState(BoomerangState.Charging);
    }

    protected virtual void OnSpinningShieldUpdate(float dt)
    {
        if (weaponManager == null) return;
        Vector3 shieldCenter = transform.position;
        float shieldHitRadius = shieldVFX != null ? shieldVFX.shieldRadius : 0.85f;

        Collider[] cols = Physics.OverlapSphere(shieldCenter, shieldHitRadius);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null || IsPlayerCollider(c)) continue;

            // 1. Janela de Parry (primeiros 0.2s): reflete projéteis inimigos de volta
            if (IsInParryWindow)
            {
                if (IsEnemyProjectileCollider(c))
                {
                    ReflectProjectileOnParry(c.gameObject, c.transform.position);
                    continue;
                }
            }

            // 2. Contato com inimigos (Parry atordoa nos primeiros 0.2s ou Paralisia do Tier 4 Evasion)
            DummyHealth enemy = c.GetComponentInParent<DummyHealth>();
            if (enemy != null)
            {
                int id = enemy.GetInstanceID();
                if (!hitTargetsThisPhase.Contains(id))
                {
                    hitTargetsThisPhase.Add(id);
                    if (IsInParryWindow && shieldVFX != null)
                    {
                        shieldVFX.TriggerParrySuccessVFX(enemy.transform.position, enemy.gameObject);
                        PlayReflectVariationSfx(0.85f);
                    }
                    else if (IsShieldParalysisActive && shieldVFX != null)
                    {
                        shieldVFX.TriggerShieldMeleeParalysis(enemy.gameObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Registra um ricochete na parede ou inimigo, calcula o próximo vetor (com mira inteligente em cadeia para outros inimigos)
    /// e spawna o Eco Fantasma (Tier 4).
    /// </summary>
    public virtual void RegisterBounce(Vector3 surfaceNormal, Vector3 bouncePoint, DummyHealth hitEnemy = null)
    {
        PlayReflectVariationSfx(0.95f);
        float speed = Mathf.Max(flightVelocity.magnitude, minThrowSpeed);

        // 1. Procura o próximo inimigo para ricochete encadeado inteligente (Smart Chain Bounce)
        DummyHealth nextTarget = FindNextBounceTarget(bouncePoint, hitEnemy, 14f);

        Vector3 newDir;
        if (nextTarget != null)
        {
            Vector3 targetCenter = nextTarget.transform.position + Vector3.up * 1.0f;
            newDir = (targetCenter - bouncePoint);
            newDir.y = 0f;
            if (newDir.sqrMagnitude < 0.001f) newDir = surfaceNormal;
            newDir.Normalize();

            // Ajusta o alcance do próximo trecho para alcançar o alvo
            targetFlightDistance = Vector3.Distance(bouncePoint, targetCenter) + 1.5f;
        }
        else
        {
            // Bateu em parede ou sem próximo inimigo: reflexão física
            Vector3 inDir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : transform.forward;
            Vector3 reflected = Vector3.Reflect(inDir, surfaceNormal.normalized);
            reflected.y = 0f;
            if (reflected.sqrMagnitude < 0.001f) reflected = -inDir;
            newDir = reflected.normalized;

            // Se bateu no único inimigo e não há mais ninguém no raio, encerra quiques restantes e volta
            if (hitEnemy != null)
            {
                bouncesRemaining = 0;
            }
            targetFlightDistance = GetEffectiveThrowDistance(currentCharge01);
        }

        // Reseta a distância percorrida para o novo trecho
        travelledDistance = 0f;
        flightVelocity = newDir * speed;

        // Desloca para fora da superfície de colisão para evitar multi-colisões no mesmo ponto
        transform.position = bouncePoint + surfaceNormal * 0.15f + newDir * 0.15f;
        if (newDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(newDir.normalized, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
        }

        // Spawna Eco Translúcido de Ricochete (Tier 4)
        if (echoVFX != null && IsBounceEchoActive)
        {
            Vector3 echoDir = Quaternion.Euler(0f, UnityEngine.Random.Range(-32f, 32f), 0f) * newDir;
            echoVFX.SpawnBounceEchoClone(bouncePoint, echoDir, speed * 0.9f);
        }
    }

    private DummyHealth FindNextBounceTarget(Vector3 origin, DummyHealth ignoreEnemy, float searchRadius = 14f)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, searchRadius);
        DummyHealth bestTarget = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || IsPlayerCollider(c)) continue;

            DummyHealth enemy = c.GetComponentInParent<DummyHealth>();
            if (enemy == null || enemy == ignoreEnemy || enemy.CurrentHealth <= 0) continue;

            Vector3 toEnemy = enemy.transform.position - origin;
            toEnemy.y = 0f;
            float distSqr = toEnemy.sqrMagnitude;
            if (distSqr < 0.2f) continue;

            int id = enemy.GetInstanceID();
            bool hasCooldown = hitCooldowns.TryGetValue(id, out float cd) && Time.time < cd;
            // Prioridade para inimigos que ainda não tomaram dano recente
            float score = distSqr * (hasCooldown ? 4.0f : 1.0f);

            // Verifica se não há parede entre a origem e o inimigo
            Vector3 rayOrigin = origin + Vector3.up * 0.5f;
            Vector3 rayDir = toEnemy.normalized;
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit wallHit, toEnemy.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (wallHit.collider != null && !wallHit.collider.transform.IsChildOf(enemy.transform) && !IsPlayerCollider(wallHit.collider))
                {
                    continue; // Parede ou obstáculo bloqueando
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Chamado quando o Escudo Giratório reflete um projétil na janela de Parry de 0.2s.
    /// </summary>
    public virtual void ReflectProjectileOnParry(GameObject projectileObj, Vector3 contactPoint)
    {
        if (projectileObj == null) return;

        PlayReflectVariationSfx(1.0f);

        if (shieldVFX != null)
            shieldVFX.TriggerParrySuccessVFX(contactPoint, projectileObj);

        Rigidbody projRb = projectileObj.GetComponent<Rigidbody>() ?? projectileObj.GetComponentInParent<Rigidbody>();
        if (projRb != null && !projRb.isKinematic)
        {
            projRb.linearVelocity = -projRb.linearVelocity;
        }
        projectileObj.transform.forward = -projectileObj.transform.forward;
    }

    private bool IsEnemyProjectileCollider(Collider c)
    {
        if (c == null) return false;
        if (c.GetComponentInParent<DummyHealth>() != null) return false;
        string lower = c.gameObject.name.ToLower();
        return lower.Contains("projectile") || lower.Contains("bullet") || lower.Contains("spit") || lower.Contains("shot") || lower.Contains("spike");
    }

    // =========================================================================
    // AUXILIARES INTERNOS
    // =========================================================================

    private void CheckSpinningDiscEnemies(float radius)
    {
        Vector3 moveDir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : transform.forward;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null || IsPlayerCollider(c)) continue;
            DummyHealth enemy = c.GetComponentInParent<DummyHealth>();
            if (enemy != null)
            {
                ApplyBoomerangDamageToEnemy(enemy, enemy.transform.position, moveDir, 1f);
            }
        }
    }

    private void ApplyBoomerangDamageToEnemy(DummyHealth enemy, Vector3 hitPoint, Vector3 hitDirection, float damageScale = 1f)
    {
        if (enemy == null) return;
        int id = enemy.GetInstanceID();
        if (damageScale >= 0.99f)
        {
            if (hitCooldowns.TryGetValue(id, out float cd) && Time.time < cd) return;
            hitCooldowns[id] = Time.time + 0.28f;
            hitTargetsThisPhase.Add(id);
        }

        float baseDmg = 15f * (offensiveStats != null ? offensiveStats.baseDamageMultiplier : 1f);
        float chargeMult = Mathf.Lerp(0.85f, 1.6f, currentCharge01);

        // Sinergias de Crítico: critChance + Timing Perfeito (+25%) + Retorno pelas costas (+15%)
        float critChance01 = offensiveStats != null ? Mathf.Clamp01(offensiveStats.critChance / 100f) : 0.05f;
        if (isPerfectChargeThrow) critChance01 += 0.25f;
        if (currentState == BoomerangState.Returning) critChance01 += 0.15f;
        critChance01 = Mathf.Clamp01(critChance01);

        bool isCritical = UnityEngine.Random.value < critChance01;
        float critMult = isCritical && offensiveStats != null ? Mathf.Max(1f, offensiveStats.critMultiplier) : (isCritical ? 1.5f : 1f);

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDmg * chargeMult * critMult * damageScale));
        enemy.TakeDamage(finalDamage, isCritical, false, true);

        // Slow on Hit e Knockback dos atributos do jogador
        if (offensiveStats != null)
        {
            if (offensiveStats.slowOnHit > 0f)
            {
                enemy.ApplySlow(Mathf.Clamp01(offensiveStats.slowOnHit), 2f);
            }

            if (offensiveStats.knockback > 0f)
            {
                ApplyBoomerangKnockback(enemy, hitDirection, offensiveStats.knockback);
            }
        }

        // Efeitos Tier 4 genéricos ao causar dano/acerto (Veneno, Sangramento, Sentinela, Bismutado, etc.)
        if (tier4EffectManager != null)
        {
            tier4EffectManager.ApplyDamageDealtEffects(finalDamage);
            tier4EffectManager.ApplyAreaDamageEffects(enemy.gameObject, finalDamage);
            if (enemy.CurrentHealth > 0)
            {
                tier4EffectManager.ApplyOnHitEffects(enemy.gameObject, isCritical);
            }
        }

        if (IsElectricCrystalActive && electricCrystalVFX != null)
        {
            electricCrystalVFX.TriggerElectricCrystalHit(enemy.gameObject, hitPoint);
        }
    }

    private void ApplyBoomerangKnockback(DummyHealth target, Vector3 direction, float knockbackStrength)
    {
        if (target == null || knockbackStrength <= 0f) return;
        Rigidbody targetRb = target.GetComponent<Rigidbody>() ?? target.GetComponentInParent<Rigidbody>();
        if (targetRb == null || targetRb.isKinematic) return;

        float effective = knockbackStrength * Mathf.Clamp01(1f - target.knockbackResistance);
        if (effective <= 0.001f) return;

        Vector3 pushDir = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (pushDir.sqrMagnitude < 0.001f) pushDir = target.transform.position - transform.position;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.001f) return;

        targetRb.AddForce(pushDir.normalized * (4.5f * effective), ForceMode.Impulse);
    }

    private bool IsPlayerCollider(Collider c)
    {
        if (c == null) return false;
        if (weaponManager != null && c.transform.IsChildOf(weaponManager.transform)) return true;
        return c.CompareTag("Player");
    }

    private float GetAttackSpeedMultiplier()
    {
        if (offensiveStats != null && offensiveStats.attackSpeedMelee > 0.05f)
            return Mathf.Clamp(offensiveStats.attackSpeedMelee, 0.5f, 2.0f);
        return 1f;
    }

    private float GetProjectileRangeMultiplier()
    {
        // Base padrão em PlayerAttributesOffensive é 10.0f
        if (offensiveStats != null && offensiveStats.weaponRangeProjectile > 0.1f)
            return Mathf.Clamp(offensiveStats.weaponRangeProjectile / 10f, 0.5f, 3.0f);
        return 1f;
    }

    private float GetEffectiveThrowDistance(float charge01)
    {
        float baseDist = Mathf.Lerp(minThrowDistance, maxThrowDistance, Mathf.Clamp01(charge01));
        return baseDist * GetProjectileRangeMultiplier();
    }

    private float GetPlayerAreaMultiplier()
    {
        // Glaive não depende de weaponRangeMelee; escala padrão de área
        return 1f;
    }

    private void CreateGroundAimIndicator()
    {
        if (aimIndicatorObject != null) return;
        aimIndicatorMaterial = AimSettings.CreateGroundIndicatorMaterial(aimIndicatorColor, "BoomerangAimRangeMaterial", useVertexColors: true);
        if (aimIndicatorMaterial == null) return;

        aimIndicatorObject = new GameObject("BoomerangAimRange");
        DontDestroyOnLoad(aimIndicatorObject);
        aimIndicatorTransform = aimIndicatorObject.transform;
        MeshFilter meshFilter = aimIndicatorObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = aimIndicatorObject.AddComponent<MeshRenderer>();
        aimIndicatorMesh = new Mesh { name = "BoomerangAimRangeMesh" };
        meshFilter.sharedMesh = aimIndicatorMesh;

        meshRenderer.sharedMaterial = aimIndicatorMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sortingOrder = 10;
        aimIndicatorObject.SetActive(false);
    }

    private void UpdateGroundAimIndicator()
    {
        bool shouldShow = IsEquippedByPlayer()
            && !AimSettings.IsGameplayUIOpen()
            && currentState == BoomerangState.Charging
            && (!showAimIndicatorOnlyWhileAttacking || isWindupThrowPending);

        if (aimIndicatorObject == null)
        {
            if (!shouldShow) return;
            CreateGroundAimIndicator();
        }
        if (aimIndicatorObject == null) return;

        if (aimIndicatorObject.activeSelf != shouldShow)
            aimIndicatorObject.SetActive(shouldShow);
        if (!shouldShow) return;

        Transform playerTr = weaponManager != null ? weaponManager.transform : transform;
        Vector3 aimDir = GetMouseAimDirection();
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude < 0.0001f) aimDir = playerTr.forward;
        aimDir.Normalize();

        Vector3 origin = playerTr.position;
        origin.y += aimIndicatorGroundOffset;
        aimIndicatorTransform.position = origin;
        aimIndicatorTransform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);

        float charge = isWindupThrowPending ? currentCharge01 : 0f;
        float range = GetEffectiveThrowDistance(charge);
        if (!Mathf.Approximately(range, cachedIndicatorRange)
            || !Mathf.Approximately(aimIndicatorWidth, cachedIndicatorWidth)
            || Mathf.Abs(charge - cachedIndicatorCharge) > 0.005f)
        {
            BuildGroundAimIndicatorMesh(range, aimIndicatorWidth, charge);
            cachedIndicatorRange = range;
            cachedIndicatorWidth = aimIndicatorWidth;
            cachedIndicatorCharge = charge;
        }
    }

    private void BuildGroundAimIndicatorMesh(float range, float width, float charge01)
    {
        float safeRange = Mathf.Max(0.8f, range);
        float halfW = Mathf.Max(0.15f, width * 0.5f);
        float railW = Mathf.Min(halfW * 0.25f, 0.075f);
        float innerW = Mathf.Max(0.04f, halfW - railW);
        float tipLen = Mathf.Clamp(safeRange * 0.14f, 0.45f, 0.95f);
        float bodyEnd = Mathf.Max(0.2f, safeRange - tipLen);
        float pulseZ = Mathf.Lerp(0.15f, bodyEnd, charge01);

        // Paleta dinâmica: azul cristalino -> ciano luminoso -> dourado-turquesa ao atingir 100%
        Color baseTint = Color.Lerp(new Color(0.06f, 0.72f, 0.96f, 1f), new Color(0.20f, 0.95f, 1.0f, 1f), charge01);
        Color tipTint = charge01 >= 0.98f
            ? new Color(1.0f, 0.94f, 0.48f, 1f)
            : Color.Lerp(new Color(0.28f, 0.95f, 1.0f, 1f), new Color(0.70f, 1.0f, 0.96f, 1f), charge01);

        Color bodyStart = new Color(baseTint.r, baseTint.g, baseTint.b, 0.04f);
        Color bodyMid = new Color(baseTint.r, baseTint.g, baseTint.b, Mathf.Lerp(0.16f, 0.28f, charge01));
        Color bodyEndCol = new Color(tipTint.r, tipTint.g, tipTint.b, Mathf.Lerp(0.24f, 0.38f, charge01));
        Color railStart = new Color(baseTint.r, baseTint.g, baseTint.b, 0.10f);
        Color railEnd = new Color(tipTint.r, tipTint.g, tipTint.b, Mathf.Lerp(0.55f, 0.88f, charge01));
        Color tipPeak = new Color(1f, tipTint.g, tipTint.b, Mathf.Lerp(0.75f, 0.96f, charge01));
        Color spineCol = new Color(1f, 1f, 1f, Mathf.Lerp(0.20f, 0.52f, charge01));
        float spineHalfW = 0.025f;

        List<Vector3> verts = new List<Vector3>(24);
        List<Color> cols = new List<Color>(24);
        List<int> tris = new List<int>(48);

        void AddQuad(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Color cbl, Color cbr, Color ctr, Color ctl)
        {
            int idx = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tr); verts.Add(tl);
            cols.Add(cbl); cols.Add(cbr); cols.Add(ctr); cols.Add(ctl);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
            tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
        }

        // 1. Preenchimento translúcido com degradê longitudinal (da base até a ponta)
        AddQuad(
            new Vector3(-innerW, 0f, 0f), new Vector3(innerW, 0f, 0f),
            new Vector3(innerW, 0f, pulseZ), new Vector3(-innerW, 0f, pulseZ),
            bodyStart, bodyStart, bodyMid, bodyMid);
        AddQuad(
            new Vector3(-innerW, 0f, pulseZ), new Vector3(innerW, 0f, pulseZ),
            new Vector3(innerW, 0f, bodyEnd), new Vector3(-innerW, 0f, bodyEnd),
            bodyMid, bodyMid, bodyEndCol, bodyEndCol);

        // 2. Trilhos laterais luminosos (esquerda e direita)
        AddQuad(
            new Vector3(-halfW, 0f, 0f), new Vector3(-innerW, 0f, 0f),
            new Vector3(-innerW, 0f, bodyEnd), new Vector3(-halfW, 0f, bodyEnd),
            railStart, railStart, railEnd, railEnd);
        AddQuad(
            new Vector3(innerW, 0f, 0f), new Vector3(halfW, 0f, 0f),
            new Vector3(halfW, 0f, bodyEnd), new Vector3(innerW, 0f, bodyEnd),
            railStart, railStart, railEnd, railEnd);

        // 3. Eixo central fino de mira
        AddQuad(
            new Vector3(-spineHalfW, 0f, 0.1f), new Vector3(spineHalfW, 0f, 0.1f),
            new Vector3(spineHalfW, 0f, bodyEnd), new Vector3(-spineHalfW, 0f, bodyEnd),
            new Color(spineCol.r, spineCol.g, spineCol.b, 0.04f),
            new Color(spineCol.r, spineCol.g, spineCol.b, 0.04f),
            spineCol, spineCol);

        // 4. Ponta em chevron brilhante na distância EXATA de retorno (safeRange)
        int tipStart = verts.Count;
        float flareW = halfW * 1.28f;
        verts.Add(new Vector3(-flareW, 0f, bodyEnd));
        verts.Add(new Vector3( flareW, 0f, bodyEnd));
        verts.Add(new Vector3(0f,      0f, safeRange));
        cols.Add(railEnd);
        cols.Add(railEnd);
        cols.Add(tipPeak);
        tris.Add(tipStart); tris.Add(tipStart + 2); tris.Add(tipStart + 1);

        aimIndicatorMesh.Clear();
        aimIndicatorMesh.SetVertices(verts);
        aimIndicatorMesh.SetColors(cols);
        aimIndicatorMesh.SetTriangles(tris, 0);
        aimIndicatorMesh.RecalculateNormals();
        aimIndicatorMesh.RecalculateBounds();
    }

    private Vector3 GetMouseAimDirection()
    {
        Transform playerTr = weaponManager != null ? weaponManager.transform : transform;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, playerTr.position.y + 1.0f, 0f));
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 dir = hitPoint - playerTr.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }
        }
        return playerTr.forward;
    }

    private void RotatePlayerTowardsMouse()
    {
        RotatePlayerTowardsDirection(GetMouseAimDirection());
    }

    private void RotatePlayerTowardsDirection(Vector3 dir)
    {
        if (weaponManager == null || dir.sqrMagnitude < 0.001f) return;
        dir.y = 0f;
        weaponManager.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void TriggerPlayerAnimation(int triggerHash)
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return;
        foreach (AnimatorControllerParameter p in playerAnimator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == triggerHash)
            {
                playerAnimator.ResetTrigger(triggerHash);
                playerAnimator.SetTrigger(triggerHash);
                return;
            }
        }
    }

    private void SetAnimatorBoolSafe(int boolHash, bool value)
    {
        if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) return;
        foreach (AnimatorControllerParameter p in playerAnimator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == boolHash)
            {
                playerAnimator.SetBool(boolHash, value);
                return;
            }
        }
    }

    // =========================================================================
    // SFX DO BUMERANGUE
    // =========================================================================

    private void EnsureSfxAudioSources()
    {
        if (sfxOneShotSource == null)
        {
            sfxOneShotSource = gameObject.AddComponent<AudioSource>();
            sfxOneShotSource.playOnAwake = false;
            sfxOneShotSource.loop = false;
            sfxOneShotSource.spatialBlend = 0f;
            if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                sfxOneShotSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
        }

        if (sfxLoopSource == null)
        {
            sfxLoopSource = gameObject.AddComponent<AudioSource>();
            sfxLoopSource.playOnAwake = false;
            sfxLoopSource.loop = true;
            sfxLoopSource.spatialBlend = 0f;
            if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.sfxGroup != null)
                sfxLoopSource.outputAudioMixerGroup = GlobalAudioManager.Instance.sfxGroup;
        }
    }

    private float GetEffectiveSfxVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume * sfxMasterVolume * GlobalAudioManager.SFXVolumeMultiplier);
    }

    private void PlayOneShotSfx(AudioClip clip, float baseVolume = 1f, float pitch = 1f)
    {
        if (clip == null || IsInLobbyRack()) return;
        EnsureSfxAudioSources();

        float vol = GetEffectiveSfxVolume(baseVolume);
        if (vol <= 0.001f) return;

        sfxOneShotSource.pitch = Mathf.Clamp(pitch, 0.7f, 1.4f);
        sfxOneShotSource.PlayOneShot(clip, vol);
    }

    private void StartLoopSfx(AudioClip clip, float baseVolume = 0.8f, bool loop = true, float pitch = 1f)
    {
        if (clip == null || IsInLobbyRack())
        {
            StopLoopSfx();
            return;
        }

        EnsureSfxAudioSources();
        currentLoopBaseVolume = baseVolume;
        sfxLoopSource.loop = loop;
        sfxLoopSource.pitch = Mathf.Clamp(pitch, 0.7f, 1.4f);
        sfxLoopSource.volume = GetEffectiveSfxVolume(baseVolume);

        if (sfxLoopSource.clip != clip || !sfxLoopSource.isPlaying)
        {
            sfxLoopSource.clip = clip;
            sfxLoopSource.Play();
        }
    }

    private void StopLoopSfx()
    {
        if (sfxLoopSource != null && sfxLoopSource.isPlaying)
        {
            sfxLoopSource.Stop();
        }
    }

    private void UpdateLoopSfxVolume()
    {
        if (sfxLoopSource == null || !sfxLoopSource.isPlaying) return;
        if (GamePause.IsPaused || Time.timeScale <= 0.001f)
        {
            sfxLoopSource.volume = 0f;
            return;
        }
        sfxLoopSource.volume = GetEffectiveSfxVolume(currentLoopBaseVolume);
    }

    private AudioClip GetSpinClipForCharge(float charge01)
    {
        if (charge01 < 0.35f)
            return sfxGirandoCurto ?? sfxGirandoMedio ?? sfxGirandoLongo;
        if (charge01 < 0.72f)
            return sfxGirandoMedio ?? sfxGirandoLongo ?? sfxGirandoCurto;
        return sfxGirandoLongo ?? sfxGirandoMedio ?? sfxGirandoCurto;
    }

    private void PlayReflectVariationSfx(float baseVolume = 0.95f)
    {
        if (sfxRefletiuVariacoes == null || sfxRefletiuVariacoes.Length == 0)
        {
            PlayOneShotSfx(sfxRefletirInicio, baseVolume);
            return;
        }

        int count = sfxRefletiuVariacoes.Length;
        int chosen = (count == 1) ? 0 : UnityEngine.Random.Range(0, count);
        if (count > 1 && chosen == lastReflectSfxIndex)
        {
            chosen = (chosen + 1) % count;
        }
        lastReflectSfxIndex = chosen;

        AudioClip clip = sfxRefletiuVariacoes[chosen];
        if (clip != null)
        {
            PlayOneShotSfx(clip, baseVolume, UnityEngine.Random.Range(0.95f, 1.06f));
        }
    }

    private static Transform FindChildRecursive(Transform parent, string exactName)
    {
        if (parent == null) return null;
        if (parent.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), exactName);
            if (found != null) return found;
        }
        return null;
    }
}
