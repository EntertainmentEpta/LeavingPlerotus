using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private BoomerangState currentState = BoomerangState.Stowed;
    public BoomerangState CurrentState => currentState;

    public event Action<BoomerangState, BoomerangState> OnStateChanged;

    [Header("Encaixe Visual: Mão Direita vs Costas")]
    [Tooltip("Posição local quando empunhado na mão direita (mixamorig:RightHand).")]
    public Vector3 handMountPosition = new Vector3(-0.14f, 0.08f, 0.05f);

    [Tooltip("Rotação local quando empunhado na mão direita.")]
    public Vector3 handMountRotation = new Vector3(0f, 0f, 90f);

    [Tooltip("Posição local quando preso nas costas do astronauta (mixamorig:Spine2).")]
    public Vector3 backMountPosition = new Vector3(0f, -0.08f, -0.20f);

    [Tooltip("Rotação local quando preso nas costas do astronauta.")]
    public Vector3 backMountRotation = new Vector3(0f, 0f, 45f);

    [Tooltip("Escala global do modelo visual do bumerangue.")]
    public Vector3 equippedWorldScale = new Vector3(0.55f, 0.55f, 0.55f);

    [Header("Variantes e VFX (Ativáveis no Inspector)")]
    [Tooltip("Variante 1 (PDF Pág. 5): Pontas de cristal se desacoplam no ar soltando feixes de eletricidade (estilo Istar).")]
    public bool enableElectricCrystalVariant = true;

    [Tooltip("Variante 2 (PDF Pág. 6): Gera clones translúcidos na volta (Returning) e ecos nos ricochetes.")]
    public bool enableTranslucentClonesOnReturn = true;

    [Tooltip("Variante 2 (PDF Pág. 6): Spawna um eco fantasma autônomo a cada ricochete.")]
    public bool enableBounceEchoSpawning = true;

    [Tooltip("Upgrade Melee (PDF Pág. 8): Escudo giratório aplica paralisia elétrica ao contato.")]
    public bool enableShieldMeleeParalysis = true;

    [Header("Parâmetros de Física / Máquina de Estados")]
    [Tooltip("Se verdadeiro, ativa os controles e física base para testar todas as animações e VFX imediatamente.")]
    public bool useBuiltInStateAndPhysics = true;

    [Tooltip("Tempo máximo segurando o botão de ataque para atingir 100% de força (Charging).")]
    public float maxChargeTime = 0.85f;

    [Tooltip("Velocidade mínima de arremesso (clique rápido).")]
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

    [Tooltip("Quantidade mínima de quiques (sem carga).")]
    public int minBounces = 2;

    [Tooltip("Quantidade máxima de quiques (carga máxima).")]
    public int maxBounces = 6;

    [Tooltip("Janela de Parry nos primeiros 0.2s do estado SpinningShield (reflete projéteis / atordoa melee).")]
    public float parryWindowDuration = 0.2f;

    [Tooltip("Trava o movimento do player durante o escudo giratório (igual recarga da Pistola no PDF Pág. 7)?")]
    public bool lockPlayerMovementDuringShield = false;

    [Tooltip("Tecla alternativa (além do Botão Direito do Mouse) para ativar o Escudo Giratório.")]
    public KeyCode shieldAlternateKey = KeyCode.R;

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

    // ─── Referências do Player ────────────────────────────────────────────────
    private Player_WeaponManager weaponManager;
    private PlayerM playerMovement;
    private PlayerAttributesOffensive offensiveStats;
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
    private float stateElapsedTime;
    private bool isDrawnInHand;
    private readonly HashSet<int> hitTargetsThisPhase = new HashSet<int>();

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
            // Quando equipado pelo Player_WeaponManager, começa na mão pronto para uso
            AttachToHandImmediate();
            isDrawnInHand = true;
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
        shieldVFX.enableMeleeParalysisUpgrade = enableShieldMeleeParalysis;
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
            offensiveStats = weaponManager.GetComponent<PlayerAttributesOffensive>() ?? weaponManager.GetComponentInParent<PlayerAttributesOffensive>();
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
        if (IsInLobbyRack()) return false;
        if (weaponManager != null && weaponManager.currentWeapon == gameObject) return true;
        if (transform.parent != null && (transform.parent.name.Contains("RightHand") || transform.parent.name.Contains("Spine")))
            return true;
        return weaponManager != null;
    }

    private void Update()
    {
        if (IsInLobbyRack())
            return;

        TryBindPlayerReferences();
        if (!IsEquippedByPlayer())
            return;

        if (itemFloat != null && itemFloat.enabled)
            itemFloat.enabled = false;

        float dt = Time.deltaTime;
        stateElapsedTime += dt;

        // Sincroniza flag de upgrade no módulo de escudo
        if (shieldVFX != null)
            shieldVFX.enableMeleeParalysisUpgrade = enableShieldMeleeParalysis;

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
                if (shieldVFX != null) shieldVFX.SetShieldActive(false);
                SetAnimatorBoolSafe(AnimBoolIsDefending, false);
                SetAnimatorBoolSafe(AnimBoolIsReloading, false);
                if (lockPlayerMovementDuringShield && playerMovement != null)
                    playerMovement.SetPlasmaFiring(false);
                break;

            case BoomerangState.Charging:
                SetAnimatorBoolSafe(AnimBoolIsCharging, false);
                if (playerMovement != null)
                    playerMovement.SetPlasmaFiring(false);
                if (airSpinVFX != null)
                    airSpinVFX.ResetChargeVisual();
                chargeSpinAngle = 0f;
                break;

            case BoomerangState.Throw:
            case BoomerangState.Returning:
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
                isDrawnInHand = false;
                if (electricCrystalVFX != null) electricCrystalVFX.SnapCoupledImmediate();
                if (airSpinVFX != null) airSpinVFX.SetAirSpinActive(false);
                if (shieldVFX != null) shieldVFX.SetShieldActive(false);
                if (echoVFX != null) echoVFX.SetEchoEmission(false, false);

                if (previousState != BoomerangState.Stowed)
                {
                    TriggerPlayerAnimation(AnimTriggerHolster);
                }
                AttachToBackImmediate();
                break;

            case BoomerangState.Charging:
                // Na mão direita do astronauta carregando o arremesso
                if (!isDrawnInHand || previousState == BoomerangState.Stowed)
                {
                    TriggerPlayerAnimation(AnimTriggerDraw);
                }
                isDrawnInHand = true;
                AttachToHandImmediate();
                if (electricCrystalVFX != null) electricCrystalVFX.SnapCoupledImmediate();
                if (airSpinVFX != null) airSpinVFX.SetAirSpinActive(false);
                if (shieldVFX != null) shieldVFX.SetShieldActive(false);
                SetAnimatorBoolSafe(AnimBoolIsCharging, true);
                break;

            case BoomerangState.Throw:
                // Arremessado no ar em alta rotação
                hitTargetsThisPhase.Clear();
                TriggerPlayerAnimation(AnimTriggerAttack);
                TriggerPlayerAnimation(AnimTriggerShoot);

                DetachIntoWorldSpace();

                float areaMult = GetPlayerAreaMultiplier();
                if (airSpinVFX != null)
                {
                    airSpinVFX.SetRadiusMultiplier(enableElectricCrystalVariant ? areaMult * 1.4f : areaMult);
                    airSpinVFX.SetAirSpinActive(true);
                }

                if (electricCrystalVFX != null)
                {
                    electricCrystalVFX.SetDecoupledState(enableElectricCrystalVariant, areaMult);
                }

                if (echoVFX != null)
                {
                    echoVFX.SetEchoEmission(enableTranslucentClonesOnReturn, false);
                }
                break;

            case BoomerangState.Returning:
                // Voltando para o jogador com DynamicHoming e Clones Translúcidos na Volta
                hitTargetsThisPhase.Clear();
                DetachIntoWorldSpace();

                if (airSpinVFX != null)
                    airSpinVFX.SetAirSpinActive(true);

                if (echoVFX != null)
                    echoVFX.SetEchoEmission(enableTranslucentClonesOnReturn, true);

                currentReturnSpeed = Mathf.Max(returnHomingSpeed, flightVelocity.magnitude);
                break;

            case BoomerangState.SpinningShield:
                // Escudo Giratório Defensivo ao redor do corpo do player (Astronaut_defense)
                isDrawnInHand = true;
                DetachIntoWorldSpace();

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
    /// Prende o bumerangue visualmente na mão direita do astronauta (mixamorig:RightHand).
    /// </summary>
    public void AttachToHandImmediate()
    {
        TryBindPlayerReferences();
        if (weaponOffset != null)
        {
            weaponOffset.isStowedOnBack = false;
            weaponOffset.disableEditorLiveHandSnap = false;
        }

        if (rightHandAnchor != null)
        {
            transform.SetParent(rightHandAnchor, false);
            transform.localPosition = weaponOffset != null ? weaponOffset.equipPosition : handMountPosition;
            transform.localEulerAngles = weaponOffset != null ? weaponOffset.equipRotation : handMountRotation;
            transform.localScale = equippedWorldScale;
        }

        if (visualPivot != null)
        {
            chargeSpinAngle = 0f;
            visualPivot.localPosition = Vector3.zero;
            visualPivot.localRotation = Quaternion.identity;
        }
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

        Transform targetBack = backSpineAnchor != null ? backSpineAnchor : rightHandAnchor;
        if (targetBack != null)
        {
            transform.SetParent(targetBack, false);
            transform.localPosition = weaponOffset != null ? weaponOffset.stowedBackPosition : backMountPosition;
            transform.localEulerAngles = weaponOffset != null ? weaponOffset.stowedBackRotation : backMountRotation;
            transform.localScale = equippedWorldScale;
        }

        if (visualPivot != null)
        {
            chargeSpinAngle = 0f;
            visualPivot.localPosition = Vector3.zero;
            visualPivot.localRotation = Quaternion.identity;
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

        transform.SetParent(null, true);
        transform.position = worldPos;
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
        if (CheatConsole.IsOpen) return;

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

        // Botão Esquerdo do Mouse: Carregar (Charging) -> Soltar para Arremessar (Throw)
        if (currentState == BoomerangState.Stowed || currentState == BoomerangState.Charging)
        {
            if (Input.GetMouseButtonDown(0))
            {
                currentChargeTime = 0f;
                currentCharge01 = 0f;
                if (currentState != BoomerangState.Charging)
                    SetState(BoomerangState.Charging);
            }

            if (Input.GetMouseButton(0) && currentState == BoomerangState.Charging)
            {
                currentChargeTime += dt;
                currentCharge01 = Mathf.Clamp01(currentChargeTime / Mathf.Max(0.05f, maxChargeTime));
                Vector3 aimDir = GetMouseAimDirection();
                RotatePlayerTowardsDirection(aimDir);

                // Permite movimentação strafing fluida enquanto carrega (mira fixa no mouse, pernas correndo)
                if (playerMovement != null)
                {
                    playerMovement.SetPlasmaFiring(true);
                    playerMovement.SetPlasmaAimDirection(aimDir);
                }
            }

            if (Input.GetMouseButtonUp(0) && currentState == BoomerangState.Charging && currentChargeTime > 0.01f)
            {
                if (playerMovement != null)
                {
                    playerMovement.SetPlasmaFiring(false);
                }
                LaunchThrow(currentCharge01);
            }
        }
        else if (currentState == BoomerangState.Throw && Input.GetMouseButtonDown(0) && stateElapsedTime > 0.15f)
        {
            // Permite puxar o bumerangue de volta mais cedo clicando novamente
            SetState(BoomerangState.Returning);
        }
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
        if (playerMovement != null)
            playerMovement.SetPlasmaFiring(false);

        chargeSpinAngle = 0f;
        currentCharge01 = Mathf.Clamp01(charge01);
        Vector3 aimDir = GetMouseAimDirection();
        RotatePlayerTowardsDirection(aimDir);

        float launchSpeed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, currentCharge01);
        targetFlightDistance = Mathf.Lerp(minThrowDistance, maxThrowDistance, currentCharge01);
        bouncesRemaining = Mathf.RoundToInt(Mathf.Lerp(minBounces, maxBounces, currentCharge01));

        Transform originTr = weaponManager != null ? weaponManager.transform : transform;
        throwOriginPos = originTr.position + Vector3.up * 1.1f;
        transform.position = throwOriginPos + aimDir * 0.65f;
        travelledDistance = 0f;
        flightVelocity = aimDir * launchSpeed;

        SetState(BoomerangState.Throw);
    }

    protected virtual void OnThrowPhysicsUpdate(float dt)
    {
        float stepDist = flightVelocity.magnitude * dt;
        Vector3 dir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : transform.forward;
        float hitRadius = (electricCrystalVFX != null && enableElectricCrystalVariant)
            ? electricCrystalVFX.GetExpandedHitRadius(0.6f)
            : 0.6f;

        // Detecção de colisão / ricochete em paredes e inimigos
        if (Physics.SphereCast(transform.position, 0.28f, dir, out RaycastHit hit, stepDist + 0.25f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!IsPlayerCollider(hit.collider))
            {
                DummyHealth enemy = hit.collider.GetComponentInParent<DummyHealth>();
                if (enemy != null)
                {
                    ApplyBoomerangDamageToEnemy(enemy, hit.point);
                }

                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.001f) normal = -dir;
                normal.Normalize();

                RegisterBounce(normal, hit.point);

                if (bouncesRemaining <= 0)
                {
                    SetState(BoomerangState.Returning);
                    return;
                }
            }
        }

        // Verifica inimigos na área das pontas de cristal desacopladas
        CheckSpinningDiscEnemies(hitRadius);

        transform.position += flightVelocity * dt;
        travelledDistance += stepDist;

        if (travelledDistance >= targetFlightDistance)
        {
            SetState(BoomerangState.Returning);
        }
    }

    protected virtual void OnReturningPhysicsUpdate(float dt)
    {
        Transform playerTr = weaponManager != null ? weaponManager.transform : null;
        if (playerTr == null)
        {
            SetState(BoomerangState.Charging);
            return;
        }

        // DynamicHoming estilo Boomerang Fu: recalcula a rota para seguir o jogador mesmo se ele der Dash
        Vector3 targetPos = playerTr.position + Vector3.up * 1.1f;
        Vector3 toPlayer = targetPos - transform.position;
        float distToPlayer = toPlayer.magnitude;

        if (distToPlayer <= 0.75f)
        {
            // Bumerangue voltou para a mão do astronauta!
            currentChargeTime = 0f;
            currentCharge01 = 0f;
            SetState(BoomerangState.Charging);
            return;
        }

        currentReturnSpeed += returnHomingAcceleration * dt;
        Vector3 desiredDir = toPlayer.normalized;
        flightVelocity = Vector3.Lerp(flightVelocity.normalized, desiredDir, 14f * dt).normalized * currentReturnSpeed;
        transform.position += flightVelocity * dt;

        float hitRadius = (electricCrystalVFX != null && enableElectricCrystalVariant)
            ? electricCrystalVFX.GetExpandedHitRadius(0.6f)
            : 0.6f;
        CheckSpinningDiscEnemies(hitRadius);
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

            // 2. Contato com inimigos (Parry atordoa nos primeiros 0.2s ou Paralisia do Upgrade Melee)
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
                    }
                    else if (enableShieldMeleeParalysis && shieldVFX != null)
                    {
                        shieldVFX.TriggerShieldMeleeParalysis(enemy.gameObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Registra um ricochete na parede ou inimigo, atualiza o vetor de velocidade e spawna o Eco Fantasma (Variante 2).
    /// Pode ser chamado externamente quando detectar colisão.
    /// </summary>
    public virtual void RegisterBounce(Vector3 surfaceNormal, Vector3 bouncePoint)
    {
        Vector3 inDir = flightVelocity.sqrMagnitude > 0.001f ? flightVelocity.normalized : transform.forward;
        Vector3 reflectedDir = Vector3.Reflect(inDir, surfaceNormal.normalized);
        reflectedDir.y = 0f;
        if (reflectedDir.sqrMagnitude < 0.001f) reflectedDir = -inDir;
        reflectedDir.Normalize();

        float speed = flightVelocity.magnitude;
        flightVelocity = reflectedDir * speed;
        bouncesRemaining--;

        // Spawna Eco Translúcido de Ricochete (PDF Página 6)
        if (echoVFX != null && enableBounceEchoSpawning)
        {
            Vector3 echoDir = Quaternion.Euler(0f, UnityEngine.Random.Range(-32f, 32f), 0f) * reflectedDir;
            echoVFX.SpawnBounceEchoClone(bouncePoint, echoDir, speed * 0.9f);
        }
    }

    /// <summary>
    /// Chamado quando o Escudo Giratório reflete um projétil na janela de Parry de 0.2s.
    /// </summary>
    public virtual void ReflectProjectileOnParry(GameObject projectileObj, Vector3 contactPoint)
    {
        if (projectileObj == null) return;

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
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (c == null || IsPlayerCollider(c)) continue;
            DummyHealth enemy = c.GetComponentInParent<DummyHealth>();
            if (enemy != null)
            {
                ApplyBoomerangDamageToEnemy(enemy, enemy.transform.position);
            }
        }
    }

    private void ApplyBoomerangDamageToEnemy(DummyHealth enemy, Vector3 hitPoint)
    {
        if (enemy == null) return;
        int id = enemy.GetInstanceID();
        if (hitTargetsThisPhase.Contains(id)) return;
        hitTargetsThisPhase.Add(id);

        float baseDmg = 15f * (offensiveStats != null ? offensiveStats.baseDamageMultiplier : 1f);
        float mult = Mathf.Lerp(0.85f, 1.6f, currentCharge01);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDmg * mult));
        enemy.TakeDamage(finalDamage, false, false, true);

        if (enableElectricCrystalVariant && electricCrystalVFX != null)
        {
            electricCrystalVFX.TriggerElectricCrystalHit(enemy.gameObject, hitPoint);
        }
    }

    private bool IsPlayerCollider(Collider c)
    {
        if (c == null) return false;
        if (weaponManager != null && c.transform.IsChildOf(weaponManager.transform)) return true;
        return c.CompareTag("Player");
    }

    private float GetPlayerAreaMultiplier()
    {
        if (offensiveStats != null && offensiveStats.weaponRangeMelee > 0.05f)
            return Mathf.Clamp(offensiveStats.weaponRangeMelee, 0.75f, 2.5f);
        return 1f;
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
