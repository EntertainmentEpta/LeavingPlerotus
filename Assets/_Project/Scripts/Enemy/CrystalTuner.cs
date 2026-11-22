using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CrystalTuner : MonoBehaviour
{
    [Header("Sintonia")]
    public float connectRange = 20f;
    public float disconnectRange = 30f;
    public LayerMask enemyLayer;
    public Color beamColor = Color.magenta;

    [Header("Múltiplos Alvos")]
    [Tooltip("Máximo de inimigos buffados simultaneamente")]
    public int maxTargets = 3;

    [Header("Movimento Inteligente")]
    public float moveSpeed = 4.5f;
    public float idealDistToTarget = 5f;
    public float fleeDistFromPlayer = 8f;

    [Header("Voo")]
    [Tooltip("Altura fixa do chão.")]
    public float flyHeight = 1.5f;
    public float heightCorrectionSpeed = 5.0f;

    [Header("Luz de Conexão")]
    [Tooltip("Point Light associada ao sintonizador.")]
    public Light connectionLight;
    public float minLightIntensity = 0.5f;
    public float maxLightIntensity = 3.0f;
    public float lightPulseSpeed = 5.0f;

    [Header("Rotação Visual")]
    [Tooltip("Objeto visual que irá girar. Se nulo, tentará achar um filho chamado 'Crystal Tunner', 'Mesh1' ou o primeiro filho válido.")]
    public Transform visualChild;
    public float idleSpinSpeed = 30f;
    public float activeSpinSpeed = 180f;

    // --- Privados ---
    private Transform playerTransform;
    private Transform beamOrigin;
    private Rigidbody rb;
    private float beamPulseTimer = 0f;
    private const int BEAM_SEGMENTS = 20;

    // Materiais compartilhados URP para o feixe duplo (destruídos em OnDestroy)
    private Material beamCoreMaterial;
    private Material beamOuterMaterial;
    private Material anchorGlowMaterial;

    // Roaming (quando sem alvos)
    private Vector3 roamAnchor;          // ponto base do roaming
    private float roamAngle = 0f;        // angulo atual na orbita
    private float roamRadius = 6f;       // raio da orbita atual
    private float roamRadiusTarget = 6f; // raio-alvo para transicao suave
    private float roamChangeTimer = 0f;  // tempo para trocar comportamento
    private bool roamInitialized = false;
    private bool sintonizadorDialogTriggered = false;

    // Lista de alvos ativos
    private List<TargetData> targets = new List<TargetData>();

    private struct TargetData
    {
        public GameObject obj;
        public Transform center;
        public Renderer renderer;
        public GameObject beamRoot;
        public LineRenderer coreBeam;
        public LineRenderer outerBeam;
        public Transform anchorSpark;
    }

    private static T GetTargetComponent<T>(GameObject obj) where T : Component
    {
        if (obj == null) return null;
        return obj.GetComponent<T>() ?? obj.GetComponentInParent<T>();
    }

    private GameObject ResolveTargetRoot(Collider hit)
    {
        if (hit == null) return null;

        DummyHealth health = hit.GetComponent<DummyHealth>() ?? hit.GetComponentInParent<DummyHealth>();
        if (health != null)
            return health.gameObject;

        if (hit.attachedRigidbody != null)
            return hit.attachedRigidbody.gameObject;

        return hit.gameObject;
    }

    private bool registradoNoBestiario = false;

    // ──────────────────────────────────────────────
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        Transform center = transform.Find("Center");
        beamOrigin = (center != null) ? center : transform;
        if (center == null)
            Debug.LogWarning("[CrystalTuner] Filho 'Center' não encontrado. Usando o pivô.");

        if (connectionLight == null)
            connectionLight = GetComponentInChildren<Light>(true);

        if (connectionLight != null)
            connectionLight.enabled = false;

        if (visualChild == null)
        {
            visualChild = transform.Find("Crystal Tunner") ?? transform.Find("Mesh1");
            if (visualChild == null && transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.name != "HealthBar_Canvas" && child.name != "Point Light" && child.name != "Center")
                    {
                        visualChild = child;
                        break;
                    }
                }
            }
        }
    }

    void Update()
    {
        if (targets != null)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i].obj == null)
                {
                    if (targets[i].beamRoot != null)
                    {
                        Destroy(targets[i].beamRoot);
                    }
                    targets.RemoveAt(i);
                }
            }
        }

        HandleBuffs();
        UpdateAllBeams();
        UpdateConnectionLight();
        UpdateVisualRotation();

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // Registro no bestiário por proximidade e diálogo do Eptinho
        if (playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer < fleeDistFromPlayer * 2f)
            {
                if (!registradoNoBestiario)
                {
                    registradoNoBestiario = true;
                    EnemyIdentity id = GetComponent<EnemyIdentity>() ?? GetComponentInChildren<EnemyIdentity>() ?? GetComponentInParent<EnemyIdentity>();
                    Debug.Log("[CRYSTAL] EnemyIdentity: " + (id != null ? id.nomeInimigo : "NULL") + " | BestiarioManager: " + (BestiarioManager.instancia != null));
                    if (id != null && BestiarioManager.instancia != null)
                        BestiarioManager.instancia.Registrar(id);
                }

                if (!sintonizadorDialogTriggered)
                {
                    sintonizadorDialogTriggered = true;
                    // Dispara o diálogo explicativo do Eptinho sobre o Sintonizador
                    EptinhoStoryDialogs.TriggerSintonizadorEncounter();
                }
            }
        }
    }

    void UpdateConnectionLight()
    {
        if (connectionLight == null) return;

        bool hasTargets = targets.Count > 0;
        
        if (connectionLight.enabled != hasTargets)
            connectionLight.enabled = hasTargets;

        if (hasTargets)
        {
            float t = Mathf.PingPong(Time.time * lightPulseSpeed, 1f);
            connectionLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, t);
        }
    }

    void UpdateVisualRotation()
    {
        if (visualChild == null) return;

        bool hasTargets = targets.Count > 0;
        float currentSpeed = hasTargets ? activeSpinSpeed : idleSpinSpeed;
        visualChild.Rotate(Vector3.up, currentSpeed * Time.deltaTime, Space.World);
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    // ──────────────────────────────────────────────
    void HandleMovement()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        Vector3 finalDirection = Vector3.zero;
        Vector3 myPosFlat = new Vector3(transform.position.x, 0, transform.position.z);

        // Força de proteção: fica perto do primeiro alvo
        if (targets.Count > 0 && targets[0].obj != null)
        {
            Vector3 targetPosFlat = new Vector3(targets[0].obj.transform.position.x, 0, targets[0].obj.transform.position.z);
            if (Vector3.Distance(myPosFlat, targetPosFlat) > idealDistToTarget)
                finalDirection += (targetPosFlat - myPosFlat).normalized * 1.5f;
        }

        // Força de medo: foge do player
        bool playerTooClose = false;
        if (playerTransform != null)
        {
            Vector3 playerPosFlat = new Vector3(playerTransform.position.x, 0, playerTransform.position.z);
            float distToPlayer = Vector3.Distance(myPosFlat, playerPosFlat);
            if (distToPlayer < fleeDistFromPlayer)
            {
                finalDirection += (myPosFlat - playerPosFlat).normalized * 3.0f;
                playerTooClose = true;
            }
        }

        // ── Roaming orgânico quando sem alvos ──────────────────────────
        // Quando não há inimigos para se conectar, o tuner patrulha em curvas
        // suaves, mudando de raio e velocidade angular, para parecer vivo.
        if (targets.Count == 0 && !playerTooClose)
        {
            // Inicializa a âncora na primeira vez sem alvos
            if (!roamInitialized)
            {
                roamAnchor = transform.position;
                roamAnchor.y = 0f;
                roamAngle = UnityEngine.Random.Range(0f, 360f);
                roamRadius = UnityEngine.Random.Range(4f, 8f);
                roamRadiusTarget = roamRadius;
                roamChangeTimer = UnityEngine.Random.Range(3f, 6f);
                roamInitialized = true;
            }

            // Se o player existir, usa a posição dele como âncora (mas a distância
            // de fuga já bloqueou a aproximação acima — aqui ele orbitará ao redor)
            if (playerTransform != null)
            {
                Vector3 anchor = new Vector3(playerTransform.position.x, 0, playerTransform.position.z);
                // Atualiza âncora suavemente para não teletransportar
                roamAnchor = Vector3.Lerp(roamAnchor, anchor + new Vector3(
                    Mathf.Sin(Time.time * 0.3f) * 5f,
                    0,
                    Mathf.Cos(Time.time * 0.2f) * 5f
                ), 0.02f);
            }

            // Muda raio e velocidade angular periodicamente
            roamChangeTimer -= Time.fixedDeltaTime;
            if (roamChangeTimer <= 0f)
            {
                roamChangeTimer = UnityEngine.Random.Range(3f, 7f);
                roamRadiusTarget = UnityEngine.Random.Range(4f, 10f);
            }

            // Suaviza o raio
            roamRadius = Mathf.Lerp(roamRadius, roamRadiusTarget, 0.02f);

            // Avança o ângulo com uma velocidade angular variável (Perlin noise)
            float angularSpeed = 45f + Mathf.PerlinNoise(Time.time * 0.4f, 0f) * 60f;
            roamAngle += angularSpeed * Time.fixedDeltaTime;

            // Calcula o ponto-alvo na órbita
            float rad = roamAngle * Mathf.Deg2Rad;
            Vector3 orbitPoint = roamAnchor + new Vector3(Mathf.Cos(rad) * roamRadius, 0, Mathf.Sin(rad) * roamRadius);

            // Direção para o ponto-alvo da órbita
            Vector3 orbitDir = (orbitPoint - myPosFlat);
            if (orbitDir.sqrMagnitude > 0.01f)
                finalDirection += orbitDir.normalized * 1.2f;
        }
        else if (targets.Count > 0)
        {
            // Quando encontrar alvos novamente, reinicia o roaming
            roamInitialized = false;
        }

        Vector3 targetPos = transform.position;
        if (finalDirection != Vector3.zero)
        {
            finalDirection.Normalize();
            targetPos += finalDirection * moveSpeed * Time.fixedDeltaTime;

            if (finalDirection.sqrMagnitude > 0.1f)
            {
                Vector3 lookDir = finalDirection;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.fixedDeltaTime);
                }
            }
        }

        float newY = Mathf.Lerp(transform.position.y, flyHeight, heightCorrectionSpeed * Time.fixedDeltaTime);
        targetPos.y = newY;
        rb.MovePosition(targetPos);
    }

    // ──────────────────────────────────────────────
    void HandleBuffs()
    {
        // 1. Remove alvos inválidos (mortos ou fora de range)
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            var td = targets[i];
            DummyHealth targetDh = td.obj != null ? GetTargetComponent<DummyHealth>(td.obj) : null;
            ShardSwarmHealth targetSsh = td.obj != null ? GetTargetComponent<ShardSwarmHealth>(td.obj) : null;
            bool isDead = (targetDh != null && targetDh.CurrentHealth <= 0) || (targetSsh != null && targetSsh.CurrentHealth <= 0);

            if (td.obj == null || !td.obj.activeSelf || isDead ||
                Vector3.Distance(transform.position, td.obj.transform.position) > disconnectRange)
            {
                RemoveBuffs(td.obj);
                if (td.beamRoot != null) Destroy(td.beamRoot);
                targets.RemoveAt(i);
            }
        }

        // 2. Tenta preencher slots vazios
        if (targets.Count < maxTargets)
            FindNewTargets();
    }

    void FindNewTargets()
    {
        // Busca em todas as camadas para garantir encontrar inimigos em qualquer camada (como a Aranha na camada Default)
        Collider[] hits = Physics.OverlapSphere(transform.position, connectRange);

        // Ordena por distância
        System.Array.Sort(hits, (a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

        foreach (Collider hit in hits)
        {
            if (targets.Count >= maxTargets) break;

            GameObject candidate = ResolveTargetRoot(hit);
            if (candidate == null) continue;
            if (candidate == gameObject) continue;
            if (GetTargetComponent<CrystalTuner>(candidate) != null) continue;
            if (GetTargetComponent<HomingHazard>(candidate) != null) continue;
            DummyHealth candDh = GetTargetComponent<DummyHealth>(candidate);

            ShardSwarmHealth candSsh = GetTargetComponent<ShardSwarmHealth>(candidate);

            if (candDh == null && candSsh == null) continue;

            if (candDh != null && candDh.CurrentHealth <= 0) continue;

            if (candSsh != null && candSsh.CurrentHealth <= 0) continue;

            // Já é alvo?
            bool alreadyTargeted = false;
            foreach (var td in targets)
                if (td.obj == candidate) { alreadyTargeted = true; break; }
            if (alreadyTargeted) continue;

            ConnectToTarget(candidate);
        }
    }

    void ConnectToTarget(GameObject target)
    {
        Transform centerPoint = target.transform.Find("CenterTarget");
        Renderer rend = target.GetComponentInChildren<Renderer>();

        CreateDualBeam(out GameObject root, out LineRenderer core, out LineRenderer outer, out Transform spark);
        targets.Add(new TargetData
        {
            obj = target,
            center = centerPoint,
            renderer = rend,
            beamRoot = root,
            coreBeam = core,
            outerBeam = outer,
            anchorSpark = spark
        });
        ApplyBuffs(target);
    }

    private void EnsureBeamMaterials()
    {
        if (beamCoreMaterial == null)
            beamCoreMaterial = CreateURPAdditiveLineMat(Color.white, 1.8f);
        if (beamOuterMaterial == null)
            beamOuterMaterial = CreateURPAdditiveLineMat(beamColor, 1.15f);
        if (anchorGlowMaterial == null)
            anchorGlowMaterial = CreateURPAdditiveLineMat(Color.Lerp(beamColor, Color.white, 0.35f), 1.4f);
    }

    private static Material CreateURPAdditiveLineMat(Color color, float emissionMult)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");
        Material mat = new Material(sh);

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3015;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emissionMult);
        }
        if (mat.HasProperty("_SrcBlend") && mat.HasProperty("_DstBlend"))
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        return mat;
    }

    void CreateDualBeam(out GameObject root, out LineRenderer coreLR, out LineRenderer outerLR, out Transform anchorSpark)
    {
        EnsureBeamMaterials();

        root = new GameObject("TunerBeam");
        root.transform.SetParent(transform, false);

        // 1. Halo externo suave em URP Additive
        GameObject outerObj = new GameObject("OuterGlow");
        outerObj.transform.SetParent(root.transform, false);
        outerLR = outerObj.AddComponent<LineRenderer>();
        outerLR.positionCount = BEAM_SEGMENTS + 2;
        outerLR.numCapVertices = 6;
        outerLR.numCornerVertices = 4;
        outerLR.useWorldSpace = true;
        outerLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outerLR.receiveShadows = false;
        outerLR.sharedMaterial = beamOuterMaterial;

        // 2. Núcleo fino e brilhante com pulso viajando do cristal até o inimigo
        GameObject coreObj = new GameObject("InnerCore");
        coreObj.transform.SetParent(root.transform, false);
        coreLR = coreObj.AddComponent<LineRenderer>();
        coreLR.positionCount = BEAM_SEGMENTS + 2;
        coreLR.numCapVertices = 6;
        coreLR.numCornerVertices = 4;
        coreLR.useWorldSpace = true;
        coreLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coreLR.receiveShadows = false;
        coreLR.sharedMaterial = beamCoreMaterial;

        // 3. Pequeno brilho/faísca no ponto de entrada no inimigo
        GameObject sparkObj = new GameObject("AnchorSpark");
        sparkObj.transform.SetParent(root.transform, false);
        ParticleSystem ps = sparkObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.40f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        Color spCol = Color.Lerp(beamColor, Color.white, 0.35f);
        spCol.a = 0.65f;
        main.startColor = spCol;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 14;

        var em = ps.emission;
        em.rateOverTime = 14f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = anchorGlowMaterial;
        ps.Play();

        anchorSpark = sparkObj.transform;
    }

    // ──────────────────────────────────────────────
    void UpdateAllBeams()
    {
        beamPulseTimer += Time.deltaTime;
        Vector3 origin = (beamOrigin != null && beamOrigin != transform) ? beamOrigin.position : (transform.position + Vector3.up * 0.6f);
        if (origin.y < transform.position.y + 0.3f) origin = transform.position + Vector3.up * 0.6f;

        for (int i = 0; i < targets.Count; i++)
        {
            var td = targets[i];
            if (td.coreBeam == null || td.outerBeam == null || td.obj == null) continue;

            Vector3 targetPos = td.obj.transform.position;
            if (td.center != null)
            {
                targetPos = td.center.position;
            }
            else if (td.renderer != null && td.renderer.bounds.center.y > td.obj.transform.position.y + 0.3f)
            {
                targetPos = td.renderer.bounds.center;
            }
            else
            {
                targetPos = td.obj.transform.position + Vector3.up * 1.0f;
            }

            UpdateDualBeam(td.coreBeam, td.outerBeam, td.anchorSpark, origin, targetPos, i);
        }
    }

    void UpdateDualBeam(LineRenderer coreLR, LineRenderer outerLR, Transform anchorSpark, Vector3 origin, Vector3 targetPos, int index)
    {
        float totalDist = Vector3.Distance(origin, targetPos);
        Vector3 dir = targetPos - origin;
        Vector3 perp = (dir.sqrMagnitude > 0.001f)
            ? Vector3.Cross(dir.normalized, Vector3.up)
            : Vector3.right;

        // Pulso contínuo viajando do Crystal Tuner (t=0) até o inimigo (t=1)
        float flowT = Mathf.Repeat(beamPulseTimer * 1.45f + index * 0.33f, 1f);
        float p0 = Mathf.Clamp(flowT - 0.14f, 0.02f, 0.96f);
        float p1 = Mathf.Clamp(flowT, 0.04f, 0.97f);
        float p2 = Mathf.Clamp(flowT + 0.14f, 0.05f, 0.98f);

        // Curva de espessura com onda energética viajando pelo feixe
        AnimationCurve coreWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0.045f),
            new Keyframe(p0, 0.038f),
            new Keyframe(p1, 0.095f),
            new Keyframe(p2, 0.038f),
            new Keyframe(1f, 0.035f)
        );
        coreLR.widthCurve = coreWidthCurve;
        coreLR.widthMultiplier = 1f;

        AnimationCurve outerWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0.16f),
            new Keyframe(p0, 0.13f),
            new Keyframe(p1, 0.28f),
            new Keyframe(p2, 0.13f),
            new Keyframe(1f, 0.12f)
        );
        outerLR.widthCurve = outerWidthCurve;
        outerLR.widthMultiplier = 1f;

        // Gradientes suaves com núcleo branco-ciano viajando no pulso
        Color brightCore = Color.Lerp(beamColor, Color.white, 0.72f);
        Gradient coreGrad = new Gradient();
        coreGrad.SetKeys(
            new[] {
                new GradientColorKey(beamColor, 0f),
                new GradientColorKey(brightCore, p1),
                new GradientColorKey(beamColor, 1f)
            },
            new[] {
                new GradientAlphaKey(0.15f, 0f),
                new GradientAlphaKey(0.88f, 0.12f),
                new GradientAlphaKey(1.0f, p1),
                new GradientAlphaKey(0.85f, 0.90f),
                new GradientAlphaKey(0.15f, 1f)
            }
        );
        coreLR.colorGradient = coreGrad;

        Gradient outerGrad = new Gradient();
        outerGrad.SetKeys(
            new[] {
                new GradientColorKey(beamColor, 0f),
                new GradientColorKey(Color.Lerp(beamColor, Color.white, 0.30f), p1),
                new GradientColorKey(beamColor, 1f)
            },
            new[] {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(0.32f, 0.14f),
                new GradientAlphaKey(0.55f, p1),
                new GradientAlphaKey(0.32f, 0.88f),
                new GradientAlphaKey(0.0f, 1f)
            }
        );
        outerLR.colorGradient = outerGrad;

        // Trajetória orgânica e suave (22 pontos com leve arco e ondulação harmônica)
        int total = BEAM_SEGMENTS + 2;
        float timeScroll = beamPulseTimer * 5.2f + index * 2.1f;
        float arcHeight = Mathf.Clamp(totalDist * 0.08f, 0.25f, 1.1f);

        for (int i = 0; i < total; i++)
        {
            float t = (float)i / (total - 1);
            Vector3 pt = Vector3.Lerp(origin, targetPos, t);

            if (i > 0 && i < total - 1)
            {
                float envelope = Mathf.Sin(t * Mathf.PI);
                float waveSide = Mathf.Sin(t * 7.5f - timeScroll) * 0.55f + Mathf.Sin(t * 14f - timeScroll * 1.4f) * 0.20f;
                float waveUp = Mathf.Cos(t * 6.0f - timeScroll * 0.85f) * 0.30f;

                float lateralAmp = Mathf.Clamp(totalDist * 0.032f, 0.08f, 0.35f) * envelope;
                pt += perp * (waveSide * lateralAmp);
                pt += Vector3.up * (envelope * arcHeight + waveUp * lateralAmp * 0.5f);
            }

            coreLR.SetPosition(i, pt);
            outerLR.SetPosition(i, pt);
        }

        if (anchorSpark != null)
            anchorSpark.position = targetPos;
    }

    // ──────────────────────────────────────────────
    void ApplyBuffs(GameObject target)
    {
        if (target == null) return;
        GetTargetComponent<TotemSpawner>(target)?.SetBuff(true);
        GetTargetComponent<MagicStone_AI>(target)?.SetBuff(true);
        GetTargetComponent<ShardSwarm_AI>(target)?.SetBuff(true);
        GetTargetComponent<GoblinAI_Transform>(target)?.SetBuff(true);
        GetTargetComponent<DummyHealth>(target)?.SetBuffedStatus(true);
        GetTargetComponent<ShardSwarmHealth>(target)?.SetBuffedStatus(true);
        GetTargetComponent<CrystalWatcher_AI>(target)?.SetBuff(true);
        GetTargetComponent<Cristalus_AI>(target)?.SetBuff(true);
        GetTargetComponent<Geobionte_AI>(target)?.SetBuff(true);
        GetTargetComponent<CrystalDragonCommon_AI>(target)?.SetBuff(true);
        GetTargetComponent<Golem_AI>(target)?.SetBuff(true);
        GetTargetComponent<Spider_AI>(target)?.SetBuff(true);
        GetTargetComponent<SharpBlur>(target)?.SetBuff(true);
    }

    void RemoveBuffs(GameObject target)
    {
        if (target == null) return;
        GetTargetComponent<TotemSpawner>(target)?.SetBuff(false);
        GetTargetComponent<MagicStone_AI>(target)?.SetBuff(false);
        GetTargetComponent<ShardSwarm_AI>(target)?.SetBuff(false);
        GetTargetComponent<GoblinAI_Transform>(target)?.SetBuff(false);
        GetTargetComponent<DummyHealth>(target)?.SetBuffedStatus(false);
        GetTargetComponent<ShardSwarmHealth>(target)?.SetBuffedStatus(false);
        GetTargetComponent<CrystalWatcher_AI>(target)?.SetBuff(false);
        GetTargetComponent<Cristalus_AI>(target)?.SetBuff(false);
        GetTargetComponent<Geobionte_AI>(target)?.SetBuff(false);
        GetTargetComponent<CrystalDragonCommon_AI>(target)?.SetBuff(false);
        GetTargetComponent<Golem_AI>(target)?.SetBuff(false);
        GetTargetComponent<Spider_AI>(target)?.SetBuff(false);
        GetTargetComponent<SharpBlur>(target)?.SetBuff(false);
    }

    void OnDisable()
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            var td = targets[i];
            if (td.obj != null)
                RemoveBuffs(td.obj);
            if (td.beamRoot != null)
                Destroy(td.beamRoot);
        }
        targets.Clear();
    }

    void OnDestroy()
    {
        foreach (var td in targets)
        {
            if (td.obj != null)
                RemoveBuffs(td.obj);
            if (td.beamRoot != null)
                Destroy(td.beamRoot);
        }
        targets.Clear();

        if (beamCoreMaterial != null) Destroy(beamCoreMaterial);
        if (beamOuterMaterial != null) Destroy(beamOuterMaterial);
        if (anchorGlowMaterial != null) Destroy(anchorGlowMaterial);
    }
}