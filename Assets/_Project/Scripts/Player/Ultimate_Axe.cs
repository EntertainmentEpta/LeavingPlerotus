using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Habilidade Ultimate do Machado (Axe Ground Slam).
/// Suporta Sprite customizado de rachadura no chão com flash inicial de luz, permanência e desvanecimento lento (Slow Dissolve).
/// </summary>
public class Ultimate_Axe : MonoBehaviour
{
    public enum LeapMode { ParabolicCurve, InstantImpulse, SmoothSliding, SimpleForward }

    [Header("🎯 Ponto de Origem do Impacto")]
    [Tooltip("Arraste o Empty GameObject 'PontaDoMachado'. Se nulo, busca automaticamente.")]
    public Transform axeBladeTip;

    [Tooltip("Deslocamento Y fixo do VFX em relação ao piso (-0.225f por padrão).")]
    [Range(-0.5f, 0.5f)]
    public float groundYOffset = -0.225f;

    [Header("🚀 Mecânica de Root Motion e Salto")]
    [Tooltip("Quando ativado, usa o Root Motion nativo da animação. Se desativado (padrão), usa o salto parabólico dinâmico da habilidade.")]
    public bool useRootMotion = false;

    [Tooltip("Modo legado de deslocamento por script (usado apenas se useRootMotion for desmarcado).")]
    public LeapMode leapMode = LeapMode.ParabolicCurve;

    [Header("🏃 Transição Suave de Corrida (Ground Takeoff Transition)")]
    public bool enableRunningTransition = true;

    [Range(0.1f, 1.0f)]
    public float windupGlideFactor = 0.4f;

    [Header("📈 Configurações do Salto em Parábola (Legado)")]
    public float windupDelay = 0.15f;
    public float parabolaForwardDistance = 6.0f;
    public float parabolaPeakHeight = 2.0f;
    public float parabolaAirTime = 0.8f;

    public AnimationCurve customJumpCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3.5f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3.5f, 0f)
    );

    public AnimationCurve forwardMotionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.4f),
        new Keyframe(0.35f, 0.2f, 1f, 1f),
        new Keyframe(1f, 1f, 1f, 0f)
    );

    [Header("🌊 Configurações da Onda de Choque Sequencial (Ripple Waves)")]
    public int shockwaveRingCount = 4;
    public float delayBetweenRings = 0.06f;
    public float ringExpansionDuration = 0.7f;

    [Header("⏱️ Câmera Lenta para Testes (Slow Motion Testing)")]
    public bool enableSlowMotionOnUlt = false;
    [Range(0.05f, 1.0f)] public float slowMotionTimeScale = 0.25f;
    public float slowMotionDuration = 1.0f;

    [Header("💥 Impacto e Dano em Área (AoE)")]
    public int slamDamage = 1000;
    public float shockwaveRadius = 5.0f;
    public float knockbackForce = 12.0f;
    public float upwardKnockbackForce = 0.4f;

    [Header("⏱️ Sincronia e Tempo de Impacto")]
    [Tooltip("Disparar impacto EXCLUSIVAMENTE pelo evento OnAxeSlam da animação.")]
    public bool useAnimationEventForImpact = true;

    [Tooltip("Destravar controles do jogador no impacto? Se FALSE, aguarda o evento EndUltimateSequence no FIM da animação.")]
    public bool unlockPlayerOnImpact = false;

    [Header("✨ Configurações da Rachadura no Chão (Ground Crack Sprite)")]
    [Tooltip("Arraste aqui a sua imagem Sprite customizada para a onda/rachadura no chão!")]
    public Sprite customShockwaveSprite;

    [Tooltip("Altura / Deslocamento Y do Sprite no chão.")]
    [Range(-2.0f, 2.0f)]
    public float spriteHeightOffset = -0.95f; // Padrão exato: -0.95

    [Tooltip("Tamanho máximo / Escala do Sprite no chão.")]
    [Range(0.5f, 30.0f)]
    public float spriteMaxScale = 2.0f; // Padrão atualizado: 2.0

    [Tooltip("Tempo em segundos que a imagem leva para se formar abrindo do centro para fora.")]
    public float spriteFormDuration = 0.15f; // Padrão exato: 0.15s

    [Tooltip("Tempo que a rachadura permanece totalmente visível no chão antes de começar a sumir.")]
    public float crackStayDuration = 1.0f; // Padrão exato: 1.0s

    [Tooltip("Tempo de desvanecimento lento e suave da rachadura no chão.")]
    public float crackFadeDuration = 0.8f; // Padrão exato: 0.8s

    [Tooltip("Prefab de VFX em ParticleSystem / Visual Effect Graph (Opcional).")]
    public GameObject shockwaveVFX;

    public float vfxDurationOnField = 0.8f;

    [Range(0.05f, 1.0f)]
    public float maxShockwaveOpacity = 0.6f; // Padrão exato: 0.6 (60%)

    public Color shockwaveColor = new Color(1.0f, 0.5f, 0.1f, 1.0f); // Laranja Fogo / Ouro

    [Header("🌊 Onda de Choque 3D Sobreposta (3D Shockwave Ring Overlay)")]
    [Tooltip("Ativar anel de energia 3D expandindo simultaneamente por cima do Sprite?")]
    public bool enable3DShockwaveRingOverlay = true; // Padrão exato: true

    [Tooltip("Duração total da onda de choque 3D.")]
    public float shockwaveRingDuration = 1.0f; // Padrão exato: 1.0s

    [Tooltip("Escala extra de expansão da onda 3D em relação ao Sprite.")]
    public float shockwaveRingScaleMultiplier = 50.0f; // Padrão atualizado: 50.0

    [Tooltip("Opacidade máxima da onda de choque 3D sobreposta.")]
    [Range(0.05f, 1.0f)]
    public float shockwaveRingMaxOpacity = 0.15f; // Padrão exato: 0.15 (15%)

    [Tooltip("Gráfico de Expansão (Curva): Acelera no início e estabiliza no final.")]
    public AnimationCurve shockwaveRingExpansionCurve = new AnimationCurve(
        new Keyframe(0.0f, 0.0f, 2.5f, 2.5f),
        new Keyframe(0.5f, 0.75f, 0.8f, 0.8f),
        new Keyframe(1.0f, 1.0f, 0.0f, 0.0f)
    );

    [Tooltip("Gráfico de Opacidade (Curva): Decaimento suave do início ao fim.")]
    public AnimationCurve shockwaveRingOpacityCurve = new AnimationCurve(
        new Keyframe(0.0f, 1.0f, 0.0f, -0.8f),
        new Keyframe(0.5f, 0.5f, -1.2f, -1.2f),
        new Keyframe(1.0f, 0.0f, -0.4f, 0.0f)
    );

    [Header("🧪 Teste Manual de VFX (Inspector)")]
    [Tooltip("Marque esta caixinha no Inspector no Play Mode para disparar o VFX e Dano instantaneamente!")]
    public bool triggerVFXTestNow = false;

    // Componentes e Estado Interno
    private Rigidbody playerRb;
    private Animator playerAnimator;
    private bool slamImpactExecuted = false;
    private Quaternion lockedRotation;

    void Awake()
    {
        playerRb = GetComponentInParent<Rigidbody>() ?? GetComponent<Rigidbody>();
        FindAnimator();
    }

    private void FindAnimator()
    {
        if (playerAnimator == null)
        {
            Player_WeaponManager wm = GetComponentInParent<Player_WeaponManager>() ?? GetComponent<Player_WeaponManager>();
            if (wm != null && wm.playerAnimator != null && wm.playerAnimator.isActiveAndEnabled)
            {
                playerAnimator = wm.playerAnimator;
            }
            else
            {
                playerAnimator = GetComponentInChildren<Animator>() ?? GetComponentInParent<Animator>();
            }
        }
    }

    void Update()
    {
        if (triggerVFXTestNow)
        {
            triggerVFXTestNow = false;
            TestVFXDirectly();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleTestSlowMotion();
        }
    }

    [ContextMenu("💥 Disparar VFX e Dano Agora")]
    public void TestVFXDirectly()
    {
        slamImpactExecuted = false;
        Debug.Log("🧪 [TESTE MANUAL NO INSPECTOR] Disparando Impacto e VFX diretamente!");
        TriggerAxeSlamImpact();
    }

    public void ExecuteUltimate()
    {
        slamImpactExecuted = false;
        FindBladeTipIfMissing();
        playerRb = GetComponentInParent<Rigidbody>() ?? GetComponent<Rigidbody>();
        FindAnimator();

        Debug.Log("[Ultimate_Axe] ExecuteUltimate() iniciado!");

        Vector3 initialVelocity = (playerRb != null) ? playerRb.linearVelocity : Vector3.zero;
        Vector3 horizontalVel = new Vector3(initialVelocity.x, 0, initialVelocity.z);
        bool isRunning = enableRunningTransition && horizontalVel.magnitude > 0.8f;

        if (isRunning)
        {
            lockedRotation = Quaternion.LookRotation(horizontalVel.normalized, Vector3.up);
            transform.rotation = lockedRotation;
        }
        else
        {
            lockedRotation = transform.rotation;
        }

        if (enableSlowMotionOnUlt)
        {
            StartCoroutine(ApplySlowMotionCoroutine(slowMotionDuration));
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
        }

        // Garante que o applyRootMotion fique desligado durante o salto parabólico para não travar a posição
        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
        }

        // O NavMeshAgent é suspenso pelo PlayerUltimate (via updatePosition=false) ANTES desta
        // chamada. Ultimate_Axe NAO deve tocar no agent — apenas executa o salto parabólico.
        // Executa o salto guiado por física e parábola (salto original da Ultimate)
        if (playerRb != null)
        {
            switch (leapMode)
            {
                case LeapMode.ParabolicCurve:
                    StartCoroutine(ParabolicLeapCoroutine(isRunning, horizontalVel));
                    break;

                case LeapMode.InstantImpulse:
                    Vector3 impulse = (transform.forward * parabolaForwardDistance) + (Vector3.up * parabolaPeakHeight);
                    playerRb.AddForce(impulse, ForceMode.Impulse);
                    break;

                case LeapMode.SmoothSliding:
                    StartCoroutine(SmoothSlidingCoroutine());
                    break;

                case LeapMode.SimpleForward:
                    playerRb.linearVelocity = new Vector3(transform.forward.x * parabolaForwardDistance, parabolaPeakHeight, transform.forward.z * parabolaForwardDistance);
                    break;
            }
        }
    }

    private IEnumerator ParabolicLeapCoroutine(bool isRunning, Vector3 runVelocity)
    {
        if (playerRb == null) yield break;

        // SEGURANCA: O NavMeshAgent ja foi suspenso pelo PlayerUltimate antes desta corrotina.
        // NAO desativar nem tocar em agent.enabled aqui — o agente permanece vivo como ancora.

        // 2. ATIVA KINEMATIC NO RIGIDBODY DURANTE O VOO
        // Isso impede que a gravidade da Unity puxe o jogador para baixo enquanto a parábola eleva o Y
        bool wasKinematic = playerRb.isKinematic;
        playerRb.isKinematic = true;
        playerRb.linearVelocity = Vector3.zero;

        float runSpeed = runVelocity.magnitude;
        Vector3 runDir = isRunning ? runVelocity.normalized : transform.forward;

        if (windupDelay > 0f)
        {
            float wElapsed = 0f;
            float currentGlideSpeed = isRunning ? Mathf.Min(runSpeed, 5.0f) * windupGlideFactor : 0f;

            while (wElapsed < windupDelay && !slamImpactExecuted)
            {
                float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
                wElapsed += delta;
                playerRb.transform.rotation = lockedRotation;

                if (isRunning && currentGlideSpeed > 0.05f)
                {
                    Vector3 glidePos = playerRb.position + (runDir * currentGlideSpeed * delta);
                    playerRb.position = glidePos;
                    transform.position = glidePos;
                    currentGlideSpeed = Mathf.Lerp(currentGlideSpeed, 0f, delta * 12f);
                }

                yield return null;
            }
        }

        float elapsed = 0f;
        Vector3 startPos = playerRb.position;
        Vector3 leapDir = isRunning ? runDir : transform.forward;
        Vector3 targetXZPos = startPos + (leapDir * parabolaForwardDistance);

        // 3. RAYCAST ROBUSTO (Ignora triggers, o próprio player, inimigos e itens, focando apenas em paredes)
        int wallMask = LayerMask.GetMask("Default", "Ground");
        if (wallMask == 0) wallMask = ~LayerMask.GetMask("Player", "Enemy", "Item", "Weapon", "DeadBody", "Ignore Raycast");

        Vector3 rayOrigin = startPos + Vector3.up * 0.8f;
        Vector3 flatLeapDir = new Vector3(leapDir.x, 0f, leapDir.z).normalized;
        if (flatLeapDir.sqrMagnitude < 0.001f) flatLeapDir = transform.forward;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, flatLeapDir, parabolaForwardDistance, wallMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform.root)) continue;
            if (hit.collider.isTrigger) continue;

            targetXZPos = startPos + (flatLeapDir * Mathf.Max(0.5f, hit.distance - 0.5f));
            break;
        }

        while (elapsed < parabolaAirTime && !slamImpactExecuted)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / parabolaAirTime);

            playerRb.transform.rotation = lockedRotation;

            float tForward = (forwardMotionCurve != null && forwardMotionCurve.length > 0)
                ? forwardMotionCurve.Evaluate(t)
                : t;

            Vector3 currentXZ = Vector3.Lerp(startPos, targetXZPos, tForward);

            float heightFactor = (customJumpCurve != null && customJumpCurve.length > 0)
                ? customJumpCurve.Evaluate(t)
                : 4f * t * (1f - t);

            float currentY = Mathf.Max(startPos.y, startPos.y + (heightFactor * parabolaPeakHeight));

            Vector3 nextPos = new Vector3(currentXZ.x, currentY, currentXZ.z);

            // 4. ATUALIZA TANTO O RIGIDBODY QUANTO O TRANSFORM DIRETAMENTE
            playerRb.position = nextPos;
            transform.position = nextPos;

            yield return null;
        }

        // 5. RESTAURA KINEMATIC E VELOCIDADE
        playerRb.isKinematic = wasKinematic;
        playerRb.linearVelocity = Vector3.zero;
    }

    private IEnumerator SmoothSlidingCoroutine()
    {
        if (playerRb == null) yield break;

        float elapsed = 0f;
        Vector3 startPos = playerRb.position;
        Vector3 targetPos = startPos + (transform.forward * parabolaForwardDistance);

        while (elapsed < parabolaAirTime && !slamImpactExecuted)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = elapsed / parabolaAirTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            playerRb.transform.rotation = lockedRotation;
            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, smoothT);
            playerRb.MovePosition(nextPos);
            yield return null;
        }
    }

    public void ToggleTestSlowMotion()
    {
        if (Mathf.Approximately(Time.timeScale, 1.0f))
        {
            Time.timeScale = slowMotionTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            Debug.Log($"[CÂMERA LENTA ATIVADA] TimeScale = {Time.timeScale}");
        }
        else
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
            Debug.Log("[CÂMERA LENTA DESATIVADA] TimeScale = 1.0");
        }
    }

    private IEnumerator ApplySlowMotionCoroutine(float duration)
    {
        Time.timeScale = slowMotionTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void FindBladeTipIfMissing()
    {
        if (axeBladeTip != null) return;

        Transform foundTip = FindDeepChild(transform.root, "PontaDoMachado");
        if (foundTip != null)
        {
            axeBladeTip = foundTip;
            return;
        }

        Player_WeaponManager wm = GetComponentInParent<Player_WeaponManager>() ?? GetComponent<Player_WeaponManager>();
        if (wm != null && wm.rightHand != null && wm.rightHand.childCount > 0)
        {
            Transform weapon = wm.rightHand.GetChild(0);
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals("PontaDoMachado", System.StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals("BladeTip", System.StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals("AxeTip", System.StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals("Trail_Point", System.StringComparison.OrdinalIgnoreCase))
                {
                    axeBladeTip = child;
                    return;
                }
            }
            axeBladeTip = weapon;
        }
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    public void OnAxeSlam() => TriggerAxeSlamImpact();
    public void onaxeslam() => TriggerAxeSlamImpact();
    public void OnAxeSlamImpact() => TriggerAxeSlamImpact();
    public void onaxeslamimpact() => TriggerAxeSlamImpact();
    public void OnAxeSlamHit() => TriggerAxeSlamImpact();
    public void onaxeslamhit() => TriggerAxeSlamImpact();

    public void TriggerAxeSlamImpact()
    {
        if (slamImpactExecuted) return;
        slamImpactExecuted = true;

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            playerRb.linearVelocity = Vector3.zero;
        }

        FindBladeTipIfMissing();

        Vector3 tipPos = (axeBladeTip != null) ? axeBladeTip.position : transform.position;
        Vector3 impactPoint = tipPos;

        RaycastHit hit;
        if (Physics.Raycast(tipPos + Vector3.up * 3f, Vector3.down, out hit, 15f))
        {
            impactPoint = hit.point + Vector3.up * groundYOffset;
        }
        else
        {
            impactPoint = new Vector3(tipPos.x, transform.position.y + groundYOffset, tipPos.z);
        }

        Debug.Log($"💥 [Ultimate_Axe] SLAM IMPACT! Ponto de Origem: {impactPoint}");

        // Dano em Área e Knockback
        Collider[] hitColliders = Physics.OverlapSphere(impactPoint, shockwaveRadius);
        foreach (var hitObj in hitColliders)
        {
            if (hitObj.gameObject == gameObject || hitObj.transform.IsChildOf(transform)) continue;

            DummyHealth dummy = hitObj.GetComponent<DummyHealth>() ?? hitObj.GetComponentInParent<DummyHealth>();
            if (dummy != null) dummy.TakeDamage(slamDamage);

            ShardSwarmHealth swarm = hitObj.GetComponent<ShardSwarmHealth>() ?? hitObj.GetComponentInParent<ShardSwarmHealth>();
            if (swarm != null) swarm.TakeDamage(slamDamage);

            Rigidbody enemyRb = hitObj.GetComponent<Rigidbody>() ?? hitObj.GetComponentInParent<Rigidbody>();
            if (enemyRb != null)
            {
                Vector3 pushDir = (enemyRb.transform.position - impactPoint).normalized;
                pushDir.y = upwardKnockbackForce;
                enemyRb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
            }
        }

        // VFX do Cartoon FX e Impact Frame da Vefects no solo
        VFXManager.Play(VFXType.PlayerAxeGroundSlam, impactPoint, Quaternion.identity);
        VFXManager.Play(VFXType.ImpactFrame, impactPoint, Quaternion.identity);

        if (shockwaveVFX != null)
        {
            GameObject vfxInstance = Instantiate(shockwaveVFX, impactPoint, Quaternion.identity);
            Destroy(vfxInstance, vfxDurationOnField);
        }

        // Se houver um Sprite customizado, executa a sequência de Flash + Rachadura + Slow Dissolve
        if (customShockwaveSprite != null)
        {
            StartCoroutine(AnimateCustomSpriteCrackSequenceCoroutine(impactPoint));
        }
        else
        {
            StartCoroutine(CreateSequentialShockwaveRingsCoroutine(impactPoint));
        }

        if (unlockPlayerOnImpact)
        {
            PlayerUltimate ultManager = GetComponentInParent<PlayerUltimate>() ?? GetComponent<PlayerUltimate>();
            if (ultManager != null)
            {
                ultManager.EndUltimateSequence();
            }
        }
    }

    /// <summary>
    /// Sequência Cinematográfica para o Sprite da Rachadura: Flash Inicial -> Fixação no Chão -> Desvanecimento Lento (Slow Dissolve).
    /// </summary>
    private IEnumerator AnimateCustomSpriteCrackSequenceCoroutine(Vector3 position)
    {
        GameObject crackObj = new GameObject("AxeGroundCrackVFX");
        Vector3 finalSpritePos = new Vector3(position.x, position.y - groundYOffset + spriteHeightOffset, position.z);
        crackObj.transform.position = finalSpritePos;
        crackObj.transform.rotation = Quaternion.identity;

        GameObject spriteChild = new GameObject("SpriteFlatChild");
        spriteChild.transform.SetParent(crackObj.transform, false);
        spriteChild.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        SpriteRenderer sr = spriteChild.AddComponent<SpriteRenderer>();
        sr.sprite = customShockwaveSprite;

        // Flash de Luz Ponto Central
        GameObject lightObj = new GameObject("ImpactLight");
        lightObj.transform.SetParent(crackObj.transform, false);
        lightObj.transform.localPosition = Vector3.up * 0.3f;

        Light impactLight = lightObj.AddComponent<Light>();
        impactLight.type = LightType.Point;
        impactLight.color = shockwaveColor;
        impactLight.intensity = 30.0f;
        impactLight.range = shockwaveRadius * 1.5f;
        StartCoroutine(FadeLightCoroutine(impactLight));

        // Dispara o anel de choque 3D sobreposto expandindo velozmente por cima do Sprite
        if (enable3DShockwaveRingOverlay)
        {
            StartCoroutine(Animate3DShockwaveRingOverlayCoroutine(finalSpritePos));
        }

        // ETAPA 1: Formação Suave do Centro para Fora (Center-Out Expansion)
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero; // Começa do centro exato (escala 0)
        Vector3 targetScale = new Vector3(spriteMaxScale, spriteMaxScale, spriteMaxScale);

        while (elapsed < spriteFormDuration && crackObj != null)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / spriteFormDuration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f); // Curva de expansão orgânica e suave

            crackObj.transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            
            // Transiciona da luz inicial para a cor e opacidade máxima configuradas
            Color currentColor = Color.Lerp(Color.white, shockwaveColor, smoothT);
            currentColor.a = Mathf.Lerp(0f, maxShockwaveOpacity, smoothT);
            sr.color = currentColor;

            yield return null;
        }

        if (crackObj == null) yield break;
        crackObj.transform.localScale = targetScale;
        Color stayColor = shockwaveColor;
        stayColor.a = maxShockwaveOpacity;
        sr.color = stayColor;

        // ETAPA 2: Permanecer Fincado/Visível no Chão (crackStayDuration)
        yield return new WaitForSeconds(crackStayDuration);

        // ETAPA 3: Desvanecimento Lento e Suave do Alpha (crackFadeDuration)
        elapsed = 0f;
        Color startColor = shockwaveColor;
        startColor.a = maxShockwaveOpacity;

        while (elapsed < crackFadeDuration && crackObj != null)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / crackFadeDuration);

            // Dissolução suave exponencial do Alpha
            float alpha = Mathf.Pow(1.0f - t, 1.8f) * maxShockwaveOpacity;
            Color currentC = startColor;
            currentC.a = alpha;

            sr.color = currentC;
            yield return null;
        }

        if (crackObj != null)
        {
            Destroy(crackObj);
        }
    }

    /// <summary>
    /// Onda de choque 3D sobreposta que dispara velozmente por cima do Sprite e desvanece em 0.35s.
    /// </summary>
    private IEnumerator Animate3DShockwaveRingOverlayCoroutine(Vector3 position)
    {
        GameObject ringGroup = new GameObject("Axe3DShockwaveRingOverlay");
        ringGroup.transform.position = position + Vector3.up * 0.05f;
        ringGroup.transform.rotation = Quaternion.identity;

        GameObject quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObj.name = "ShockwaveQuadFlat";
        quadObj.transform.SetParent(ringGroup.transform, false);
        quadObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Destroy(quadObj.GetComponent<Collider>());

        MeshRenderer mr = quadObj.GetComponent<MeshRenderer>();

        Material ringMat = null;
#if UNITY_EDITOR
        ringMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Shockwave.mat");
        if (ringMat == null) ringMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Glow_01.mat");
#endif
        if (ringMat == null)
        {
            ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            ringMat.color = shockwaveColor;
        }

        mr.material = new Material(ringMat);
        if (mr.material.HasProperty("_Cull")) mr.material.SetInt("_Cull", 0);

        float elapsed = 0f;
        float targetScale = spriteMaxScale * shockwaveRingScaleMultiplier;
        Vector3 startScale = new Vector3(0.1f, 0.1f, 0.1f);
        Vector3 endScale = new Vector3(targetScale, targetScale, targetScale);

        while (elapsed < shockwaveRingDuration && ringGroup != null)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / shockwaveRingDuration);

            // Avalia a escala pelo Gráfico da Curva no Inspector (Acelera forte no início e desacelera)
            float curveExpansionT = (shockwaveRingExpansionCurve != null && shockwaveRingExpansionCurve.length > 0)
                ? shockwaveRingExpansionCurve.Evaluate(t)
                : (1.0f - Mathf.Pow(1.0f - t, 3.0f));

            ringGroup.transform.localScale = Vector3.Lerp(startScale, endScale, curveExpansionT);

            // Avalia a dissipação da transparência pela Curva de Opacidade
            float curveOpacityFactor = (shockwaveRingOpacityCurve != null && shockwaveRingOpacityCurve.length > 0)
                ? shockwaveRingOpacityCurve.Evaluate(t)
                : Mathf.Pow(1.0f - t, 2.0f);

            float alpha = Mathf.Clamp01(curveOpacityFactor) * shockwaveRingMaxOpacity;
            Color currentC = Color.Lerp(Color.white, shockwaveColor, t);
            currentC.a = alpha;

            if (mr != null && mr.material != null)
            {
                if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", currentC);
                if (mr.material.HasProperty("_Color")) mr.material.SetColor("_Color", currentC);
                if (mr.material.HasProperty("_TintColor")) mr.material.SetColor("_TintColor", currentC);
                mr.material.color = currentC;
            }

            yield return null;
        }

        if (ringGroup != null)
        {
            Destroy(ringGroup);
        }
    }

    private IEnumerator CreateSequentialShockwaveRingsCoroutine(Vector3 position)
    {
        GameObject parentVFX = new GameObject("AxeShockwaveVFX_Sequential");
        parentVFX.transform.position = position;
        parentVFX.transform.rotation = Quaternion.identity;

        GameObject lightObj = new GameObject("ImpactLight");
        lightObj.transform.SetParent(parentVFX.transform, false);
        lightObj.transform.localPosition = Vector3.up * 0.3f;

        Light impactLight = lightObj.AddComponent<Light>();
        impactLight.type = LightType.Point;
        impactLight.color = shockwaveColor;
        impactLight.intensity = 25.0f;
        impactLight.range = shockwaveRadius * 1.5f;
        StartCoroutine(FadeLightCoroutine(impactLight));

        Material shockwaveMat = null;
#if UNITY_EDITOR
        shockwaveMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Shockwave.mat");
        if (shockwaveMat == null) shockwaveMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Glow_01.mat");
#endif

        if (shockwaveMat == null)
        {
            shockwaveMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            shockwaveMat.color = shockwaveColor;
        }

        if (shockwaveMat.HasProperty("_Cull")) shockwaveMat.SetInt("_Cull", 0);

        for (int i = 0; i < shockwaveRingCount; i++)
        {
            StartCoroutine(AnimateSingleRingCoroutine(parentVFX.transform, position, shockwaveMat, ringExpansionDuration, i));
            yield return new WaitForSeconds(delayBetweenRings);
        }

        Destroy(parentVFX, ringExpansionDuration + 0.5f);
    }

    private IEnumerator AnimateSingleRingCoroutine(Transform parent, Vector3 centerPos, Material mat, float duration, int ringIndex)
    {
        GameObject ringObj = new GameObject($"ShockwaveRingGroup_{ringIndex}");
        ringObj.transform.SetParent(parent, false);
        ringObj.transform.position = centerPos + Vector3.up * (groundYOffset + ringIndex * 0.005f);
        ringObj.transform.rotation = Quaternion.identity;

        GameObject quadFlat = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadFlat.name = "QuadFlat";
        quadFlat.transform.SetParent(ringObj.transform, false);
        quadFlat.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Destroy(quadFlat.GetComponent<Collider>());
        MeshRenderer mrTop = quadFlat.GetComponent<MeshRenderer>();
        mrTop.material = mat;

        float elapsed = 0f;
        float maxScale = (shockwaveRadius * 4.5f) + (ringIndex * 1.5f);
        Vector3 startScale = new Vector3(0.2f, 0.2f, 0.2f);
        Vector3 targetScale = new Vector3(maxScale, maxScale, maxScale);

        Color ringColor = (ringIndex == 0) ? Color.white : shockwaveColor;

        while (elapsed < duration && ringObj != null)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = Mathf.Clamp01(elapsed / duration);

            ringObj.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            float alpha = Mathf.Pow(1.0f - t, 2.2f) * maxShockwaveOpacity;

            Color currentC = Color.Lerp(ringColor, shockwaveColor, t);
            currentC.a = alpha;

            if (mat.HasProperty("_Color")) mat.SetColor("_Color", currentC);
            else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", currentC);

            yield return null;
        }

        if (ringObj != null)
        {
            Destroy(ringObj);
        }
    }

    private IEnumerator FadeLightCoroutine(Light pointLight)
    {
        if (pointLight == null) yield break;
        float elapsed = 0f;
        float duration = 0.4f;
        float startIntensity = pointLight.intensity;

        while (elapsed < duration && pointLight != null)
        {
            float delta = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            elapsed += delta;
            float t = elapsed / duration;
            pointLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = (axeBladeTip != null) ? axeBladeTip.position : transform.position;
        Gizmos.DrawWireSphere(center, shockwaveRadius);

        if (axeBladeTip != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(axeBladeTip.position, 0.2f);
        }

        Gizmos.color = Color.cyan;
        Vector3 start = transform.position;
        Vector3 targetXZ = start + (transform.forward * parabolaForwardDistance);
        int steps = 20;
        Vector3 prevPoint = start;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            float tForward = (forwardMotionCurve != null && forwardMotionCurve.length > 0)
                ? forwardMotionCurve.Evaluate(t)
                : t;

            Vector3 currentXZ = Vector3.Lerp(start, targetXZ, tForward);
            float heightFactor = (customJumpCurve != null && customJumpCurve.length > 0)
                ? customJumpCurve.Evaluate(t)
                : 4f * t * (1f - t);
            float currentY = start.y + (heightFactor * parabolaPeakHeight);
            Vector3 point = new Vector3(currentXZ.x, currentY, currentXZ.z);

            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
}
