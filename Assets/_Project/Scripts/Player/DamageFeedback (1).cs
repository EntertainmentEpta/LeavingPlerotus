using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// DAMAGE FEEDBACK - script único.
///  1) Indicador de direção: um efeito por DIREÇÃO de ataque. Inimigos na mesma direção
///     (dentro de mergeAngle) compartilham um indicador; direções diferentes geram indicadores separados.
///  2) Tudo fica mais forte quanto mais perto da morte (alfa, tamanho).
///  3) Gotinhas de sangue nas bordas da tela (sem pulsar), mais visíveis com vida baixa.
///  Texturas geradas por código (PNG opcional). Canvas criado sozinho.
///
/// USO: coloque num GameObject vazio, arraste o Player e, no seu código de dano, chame:
///   DamageFeedback.Instance.ReportHit(transformDoInimigo, dano / vidaMax);
///   DamageFeedback.Instance.SetHealth(vidaAtual, vidaMax);
///
/// TESTE: marque "Test Mode" e aperte Play:
///   1 cima | 2 direita | 3 baixo | 4 esquerda | 5 dois da mesma direção (1 efeito)
///   6 duas direções (2 efeitos) | 7 quatro em volta | H cura
/// </summary>
[DisallowMultipleComponent]
public class DamageFeedback : MonoBehaviour
{
    public static DamageFeedback Instance { get; private set; }

    public enum IndicatorPlacement { ScreenEdge, AroundPlayer }

    [Header("Referências")]
    [Tooltip("Transform do personagem (obrigatório).")]
    public Transform player;
    [Tooltip("Câmera do jogo. Vazio = Camera.main.")]
    public Camera cam;
    [Tooltip("Marque se o jogo for 2D (plano XY). Desmarcado = 3D com chão no plano XZ.")]
    public bool is2DGame = false;

    [Header("Texturas (opcional: vazio = gerado por código)")]
    public Texture indicatorTexture;
    public Texture vignetteTexture;

    [Header("Agrupamento por direção")]
    [Tooltip("Ataques dentro deste ângulo (graus) viram UM indicador só.")]
    [Range(5f, 120f)] public float mergeAngle = 40f;
    [Range(1, 16)] public int maxIndicators = 8;

    [Header("Indicador de direção")]
    public IndicatorPlacement placement = IndicatorPlacement.AroundPlayer;
    public float indicatorDuration = 1.35f;
    [Tooltip("Tamanho do ponteiro (em pixels de uma tela 1920x1080).")]
    public Vector2 indicatorSize = new Vector2(160f, 82f);
    [Tooltip("Distância da borda da tela (modo ScreenEdge).")]
    public float edgeInset = 70f;
    [Tooltip("Raio do círculo em volta da cabeça (modo AroundPlayer).")]
    public float ringRadius = 85f;
    [Tooltip("Altura da cabeça em relação ao pivô do personagem (unidades do mundo).")]
    public Vector3 headOffset = new Vector3(0f, 1.6f, 0f);
    public Color indicatorTint = Color.white;
    [Range(0f, 1f)] public float baseIndicatorAlpha = 1.0f;

    [Header("Perto da morte")]
    [Tooltip("Abaixo desta fração de vida os efeitos começam a crescer.")]
    [Range(0.05f, 1f)] public float lowHealthStart = 0.6f;
    public AnimationCurve lowHealthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Quanto o indicador cresce com vida no fim (0.6 = +60%).")]
    public float lowHealthExtraScale = 0.6f;

    [Header("Gotas de sangue nas bordas")]
    public Color vignetteTint = Color.white;
    [Range(0f, 1f)] public float maxVignetteAlpha = 0.9f;
    [Tooltip("Gotas aparecem rápido a cada golpe, mesmo com vida cheia.")]
    [Range(0f, 1f)] public float hitFlashAlpha = 0.40f;
    public float hitFlashFade = 2.2f;
    [Tooltip("Quantidade de gotas (só vale para a textura gerada por código).")]
    [Range(10, 300)] public int dropletCount = 75;
    [Tooltip("Sorting order do Canvas criado (maior = por cima do HUD).")]
    public int canvasSortingOrder = 500;

    [Header("Teste pelo teclado")]
    public bool testMode = false;
    public float testMaxHealth = 100f;
    public float testDamagePerHit = 10f;

    /// <summary>0 = vida tranquila, 1 = à beira da morte (já com a curva aplicada).</summary>
    public float LowHealth01 { get; private set; }

    class Indicator
    {
        public RectTransform rt;
        public RawImage img;
        public bool active;
        public float angle, age, stack, punch;
        public Transform source;
        public Vector3 sourcePos;
    }

    readonly List<Indicator> pool = new List<Indicator>();
    RectTransform root;
    RawImage vignette;
    float health01 = 1f, shownHealth01 = 1f, hitFlash, testHp;

    // =================================================================== API

    /// <summary>Golpe vindo de um inimigo. O indicador segue o inimigo enquanto estiver ativo.</summary>
    public void ReportHit(Transform attacker, float damage01 = 0.2f)
    {
        Vector3 pos = attacker ? attacker.position : (player ? player.position : Vector3.zero);
        Register(attacker, pos, damage01);
    }

    /// <summary>Golpe vindo de um ponto do mundo (projétil, explosão...).</summary>
    public void ReportHit(Vector3 sourcePosition, float damage01 = 0.2f)
    {
        Register(null, sourcePosition, damage01);
    }

    public void SetHealth(float current, float max)
    {
        health01 = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    public void ClearAll()
    {
        foreach (var i in pool) Deactivate(i);
        hitFlash = 0f;
    }

    /// <summary>Ponto no mundo a 'distance' do player numa direção de tela (0° = direita, 90° = cima).</summary>
    public Vector3 WorldPointAtScreenAngle(float angleDeg, float distance)
    {
        GetScreenAxes(out Vector3 right, out Vector3 up);
        float r = angleDeg * Mathf.Deg2Rad;
        return player.position + (right * Mathf.Cos(r) + up * Mathf.Sin(r)) * distance;
    }

    // ================================================================= Unity

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!cam) cam = Camera.main;
        BuildUI();
        testHp = testMaxHealth;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!player) return;
        if (!cam) cam = Camera.main;
        if (testMode) HandleTestInput();

        // unscaled: continua funcionando durante hit-stop / câmera lenta
        float dt = Time.unscaledDeltaTime;

        shownHealth01 = Mathf.MoveTowards(shownHealth01, health01, dt * 1.5f);
        float t = Mathf.Clamp01(1f - shownHealth01 / lowHealthStart);
        LowHealth01 = Mathf.Clamp01(lowHealthCurve.Evaluate(t));

        UpdateVignette(dt);
        UpdateIndicators(dt);
        MergeConvergingIndicators();
    }

    // ================================================================ Lógica

    void Register(Transform attacker, Vector3 pos, float damage01)
    {
        if (!player) { Debug.LogWarning("[DamageFeedback] Arraste o Player no campo 'player'."); return; }

        float ang = AngleTo(pos);

        // Já existe um indicador nessa direção? Reaproveita (um efeito por direção).
        Indicator best = null;
        float bestDiff = mergeAngle;
        foreach (var i in pool)
        {
            if (!i.active) continue;
            float d = Mathf.Abs(Mathf.DeltaAngle(i.angle, ang));
            if (d <= bestDiff) { bestDiff = d; best = i; }
        }

        if (best != null)
        {
            best.age = 0f;
            best.stack = Mathf.Min(best.stack + 1f, 4f);
            best.punch = 1f;
            best.source = attacker;
            best.sourcePos = pos;
        }
        else
        {
            var i = GetFreeIndicator();
            i.active = true;
            i.angle = ang;
            i.age = 0f;
            i.stack = 1f;
            i.punch = 1f;
            i.source = attacker;
            i.sourcePos = pos;
            i.rt.gameObject.SetActive(true);
        }

        hitFlash = Mathf.Clamp01(Mathf.Max(hitFlash, hitFlashAlpha * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(damage01))));
    }

    // Gotas paradas (sem pulsar): ficam mais visíveis quanto menor a vida.
    void UpdateVignette(float dt)
    {
        hitFlash *= Mathf.Exp(-hitFlashFade * dt);

        float alpha = LowHealth01 * maxVignetteAlpha + hitFlash;
        Color c = vignetteTint;
        c.a = Mathf.Clamp01(alpha) * vignetteTint.a;
        vignette.color = c;
    }

    void UpdateIndicators(float dt)
    {
        Vector2 half = root.rect.size * 0.5f;
        Vector2 center = Vector2.zero;
        if (placement == IndicatorPlacement.AroundPlayer && cam)
        {
            Vector3 sp = cam.WorldToScreenPoint(player.position + headOffset);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out center);
        }

        Vector2 size = indicatorSize;

        foreach (var i in pool)
        {
            if (!i.active) continue;

            i.age += dt;
            if (i.age >= indicatorDuration) { Deactivate(i); continue; }

            // Segue o inimigo se ele andar em volta do player.
            if (i.source) i.sourcePos = i.source.position;
            float target = AngleTo(i.sourcePos);
            i.angle = Mathf.LerpAngle(i.angle, target, 1f - Mathf.Exp(-15f * dt));
            i.punch = Mathf.MoveTowards(i.punch, 0f, dt * 5f);

            float r = i.angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            i.rt.anchoredPosition = placement == IndicatorPlacement.ScreenEdge
                ? new Vector2(dir.x * (half.x - edgeInset), dir.y * (half.y - edgeInset))
                : center + dir * ringRadius;
            i.rt.localRotation = Quaternion.Euler(0f, 0f, i.angle - 90f);

            float k = i.age / indicatorDuration;
            float fade = Mathf.Clamp01(i.age / 0.06f) * (1f - Smooth(0.50f, 1f, k));
            float intensity = Mathf.Clamp01(baseIndicatorAlpha + 0.15f * (i.stack - 1f) + 0.45f * LowHealth01);
            float scale = (1f + lowHealthExtraScale * LowHealth01 + 0.08f * (i.stack - 1f))
                        * (1f + 0.2f * i.punch * i.punch);

            i.rt.sizeDelta = size * scale;
            Color c = indicatorTint;
            c.a = intensity * fade * indicatorTint.a;
            i.img.color = c;
        }
    }

    // Se dois inimigos se moverem para a mesma direção, os indicadores viram um só.
    void MergeConvergingIndicators()
    {
        for (int a = 0; a < pool.Count; a++)
        {
            var A = pool[a];
            if (!A.active) continue;
            for (int b = a + 1; b < pool.Count; b++)
            {
                var B = pool[b];
                if (!B.active) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(A.angle, B.angle)) > mergeAngle * 0.5f) continue;

                var keep = A.age <= B.age ? A : B;
                var drop = keep == A ? B : A;
                keep.stack = Mathf.Min(4f, keep.stack + drop.stack * 0.5f);
                Deactivate(drop);
                if (drop == A) break;
            }
        }
    }

    float AngleTo(Vector3 world)
    {
        Vector3 d = world - player.position;
        GetScreenAxes(out Vector3 right, out Vector3 up);
        float x = Vector3.Dot(d, right);
        float y = Vector3.Dot(d, up);
        if (x * x + y * y < 1e-6f) return 90f;
        return Mathf.Atan2(y, x) * Mathf.Rad2Deg; // 0° = direita da tela, 90° = topo
    }

    // Eixos "direita" e "cima" da tela projetados no chão do jogo.
    void GetScreenAxes(out Vector3 right, out Vector3 up)
    {
        if (is2DGame)
        {
            right = cam ? cam.transform.right : Vector3.right;
            up    = cam ? cam.transform.up    : Vector3.up;
            return;
        }

        Vector3 groundNormal = Vector3.up;
        Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, groundNormal) : Vector3.forward;
        if (fwd.sqrMagnitude < 1e-4f && cam) // câmera olhando reto para baixo
            fwd = Vector3.ProjectOnPlane(cam.transform.up, groundNormal);
        up = fwd.normalized;
        right = Vector3.Cross(groundNormal, up);
    }

    // ==================================================================== UI

    void BuildUI()
    {
        var canvasGO = new GameObject("DamageFeedbackCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = (RectTransform)canvasGO.transform;

        // Vinheta (fica atrás dos indicadores)
        if (!vignetteTexture) vignetteTexture = CreateDropletsTexture(dropletCount);
        vignette = CreateImage("BloodVignette", root, vignetteTexture);
        var vrt = vignette.rectTransform;
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        vignette.color = new Color(1f, 1f, 1f, 0f);

        // Pool de indicadores
        if (!indicatorTexture) indicatorTexture = CreateHitArcTexture();
        for (int n = 0; n < maxIndicators; n++)
        {
            var img = CreateImage("HitIndicator_" + n, root, indicatorTexture);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = indicatorSize;
            rt.gameObject.SetActive(false);
            pool.Add(new Indicator { rt = rt, img = img });
        }
    }

    static RawImage CreateImage(string name, Transform parent, Texture tex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false; // não bloqueia cliques no HUD
        return img;
    }

    Indicator GetFreeIndicator()
    {
        Indicator oldest = pool[0];
        foreach (var i in pool)
        {
            if (!i.active) return i;
            if (i.age > oldest.age) oldest = i;
        }
        return oldest; // todos ocupados: recicla o mais antigo
    }

    static void Deactivate(Indicator i)
    {
        i.active = false;
        i.source = null;
        i.rt.gameObject.SetActive(false);
    }

    // ========================================================= Texturas (sem PNG)

    static readonly Color DarkBlood   = new Color(0.45f, 0.01f, 0.02f);
    static readonly Color BrightBlood = new Color(0.95f, 0.08f, 0.08f);

    // Gotinhas de sangue espalhadas perto das bordas (centro da tela fica limpo).
    // Proporção 16:9 para as gotas não ficarem esticadas na tela.
    static Texture2D CreateDropletsTexture(int count, int w = 1024, int h = 576, int seed = 7)
    {
        var tex = NewTexture(w, h, "BloodDroplets (procedural)");
        var alpha = new float[w * h];
        var shine = new float[w * h];
        var rng = new System.Random(seed);
        float Rand() => (float)rng.NextDouble();

        for (int n = 0; n < count; n++)
        {
            // Quanto mais perto da borda, mais gotas.
            float edge01 = Mathf.Pow(Rand(), 2.2f);
            float m = edge01 * 0.20f * h;
            float along = Rand();
            int side = rng.Next(4);

            float x, y; Vector2 inward;
            switch (side)
            {
                case 0:  x = along * w;     y = h - 1 - m; inward = Vector2.down;  break; // topo
                case 1:  x = along * w;     y = m;         inward = Vector2.up;    break; // baixo
                case 2:  x = m;             y = along * h; inward = Vector2.right; break; // esquerda
                default: x = w - 1 - m;     y = along * h; inward = Vector2.left;  break; // direita
            }

            // Gotas mais encorpadas nas bordas (tamanho reduzido em 20%).
            float r = Mathf.Lerp(3.6f, 11.2f, Mathf.Pow(Rand(), 2.0f)) * Mathf.Lerp(1.3f, 0.75f, edge01);
            StampDrop(alpha, shine, w, h, x, y, r);

            // Respingos menores em volta.
            int sats = rng.Next(0, 4);
            for (int s = 0; s < sats; s++)
            {
                float a = Rand() * Mathf.PI * 2f;
                float d = r * Mathf.Lerp(1.6f, 4f, Rand());
                StampDrop(alpha, shine, w, h, x + Mathf.Cos(a) * d, y + Mathf.Sin(a) * d, r * Mathf.Lerp(0.2f, 0.45f, Rand()));
            }

            // Algumas gotas com rastro curto apontando para o centro.
            if (Rand() < 0.25f)
                for (int k = 1; k <= 3; k++)
                    StampDrop(alpha, shine, w, h, x + inward.x * r * k * 0.9f, y + inward.y * r * k * 0.9f, r * (1f - 0.25f * k));
        }

        var px = new Color[w * h];
        Color highlight = new Color(0.95f, 0.30f, 0.30f);
        for (int i = 0; i < px.Length; i++)
        {
            Color c = Color.Lerp(DarkBlood, BrightBlood, 0.35f + 0.65f * shine[i]);
            if (shine[i] > 0.75f) c = Color.Lerp(c, highlight, (shine[i] - 0.75f) * 2f);
            c.a = alpha[i] * 0.95f;
            px[i] = c;
        }

        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    static void StampDrop(float[] alpha, float[] shine, int w, int h, float cx, float cy, float r)
    {
        if (r < 0.6f) return;
        int x0 = Mathf.Max(0, (int)(cx - r - 2)), x1 = Mathf.Min(w - 1, (int)(cx + r + 2));
        int y0 = Mathf.Max(0, (int)(cy - r - 2)), y1 = Mathf.Min(h - 1, (int)(cy + r + 2));
        float hx = cx - r * 0.35f, hy = cy + r * 0.35f; // brilho no canto de cima-esquerda

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            float a = 1f - Smooth(r - 1f, r + 0.6f, d);
            if (a <= 0f) continue;
            int i = y * w + x;
            alpha[i] = Mathf.Max(alpha[i], a);
            float dh = Mathf.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy));
            shine[i] = Mathf.Max(shine[i], (1f - Mathf.Clamp01(dh / (r * 0.6f))) * a);
        }
    }

    // Arco/meia-lua de sangue apontando para cima (= para fora da tela).
    static Texture2D CreateHitArcTexture(int width = 256, int height = 128, int seed = 3)
    {
        var tex = NewTexture(width, height, "BloodHitArc (procedural)");
        var px = new Color[width * height];
        float o = seed * 9.731f;

        Vector2 center = new Vector2(0f, -1.05f);
        const float R = 2.0f;
        Color arcDeep = new Color(0.85f, 0.06f, 0.06f);
        Color arcBright = new Color(1.00f, 0.28f, 0.20f);
        Color arcRim = new Color(1.00f, 0.78f, 0.70f);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (width - 1f) * 2f - 1f;
            float v = y / (height - 1f);
            var p = new Vector2(u, v);

            float dist  = Vector2.Distance(p, center) - R; // < 0 = lado de dentro do arco
            float taper = Mathf.Clamp01(1f - u * u);       // afina nas pontas
            float noise = Fbm(u * 4f + o, v * 4f, 3);
            float thick = (0.26f + 0.14f * noise) * taper;

            float band = dist <= 0f
                ? 1f - Smooth(0f, thick + 1e-4f, -dist)
                : 1f - Smooth(0f, 0.03f, dist);
            float radialFade = Mathf.Pow(band, 1.35f);
            float tipFade = Mathf.Pow(taper, 1.6f); // pontas e extremidades em degradê transparente
            float a = radialFade * tipFade;

            // Riscos de sangue apontando para dentro.
            float streak = Smooth(0.6f, 0.8f, Mathf.PerlinNoise(u * 22f + o, 1.7f));
            if (dist <= 0f)
            {
                float sLen = thick + streak * 0.22f * taper;
                a = Mathf.Max(a, (1f - Smooth(sLen * 0.5f, sLen + 1e-4f, -dist)) * streak * tipFade * 0.60f);
            }

            float core = 1f - Mathf.Clamp01(-dist / (thick + 0.001f));
            Color c = Color.Lerp(arcDeep, arcBright, Mathf.Clamp01(core * 0.85f + noise * 0.25f));
            if (core > 0.70f)
            {
                c = Color.Lerp(c, arcRim, ((core - 0.70f) / 0.30f) * 0.75f);
            }
            c.a = Mathf.Clamp01(a);
            px[y * width + x] = c;
        }

        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    static Texture2D NewTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
    }

    // ============================================================ Utilitários

    static float Smooth(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float Fbm(float x, float y, int octaves)
    {
        float amp = 0.5f, freq = 1f, sum = 0f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum  += amp * Mathf.PerlinNoise(x * freq, y * freq);
            norm += amp;
            amp  *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    // ================================================================= Teste

    void HandleTestInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        bool k1 = kb.digit1Key.wasPressedThisFrame, k2 = kb.digit2Key.wasPressedThisFrame;
        bool k3 = kb.digit3Key.wasPressedThisFrame, k4 = kb.digit4Key.wasPressedThisFrame;
        bool k5 = kb.digit5Key.wasPressedThisFrame, k6 = kb.digit6Key.wasPressedThisFrame;
        bool k7 = kb.digit7Key.wasPressedThisFrame, kh = kb.hKey.wasPressedThisFrame;
#else
        bool k1 = Input.GetKeyDown(KeyCode.Alpha1), k2 = Input.GetKeyDown(KeyCode.Alpha2);
        bool k3 = Input.GetKeyDown(KeyCode.Alpha3), k4 = Input.GetKeyDown(KeyCode.Alpha4);
        bool k5 = Input.GetKeyDown(KeyCode.Alpha5), k6 = Input.GetKeyDown(KeyCode.Alpha6);
        bool k7 = Input.GetKeyDown(KeyCode.Alpha7), kh = Input.GetKeyDown(KeyCode.H);
#endif
        if (k1) TestHit(90f);
        if (k2) TestHit(0f);
        if (k3) TestHit(270f);
        if (k4) TestHit(180f);
        if (k5) TestHit(90f, 100f);           // mesma direção -> 1 efeito
        if (k6) TestHit(45f, 225f);           // direções diferentes -> 2 efeitos
        if (k7) TestHit(0f, 90f, 180f, 270f); // 4 efeitos
        if (kh)
        {
            testHp = testMaxHealth;
            SetHealth(testHp, testMaxHealth);
            ClearAll();
        }
    }

    void TestHit(params float[] screenAngles)
    {
        foreach (float a in screenAngles)
        {
            testHp = Mathf.Max(0f, testHp - testDamagePerHit);
            ReportHit(WorldPointAtScreenAngle(a, 4f), testDamagePerHit / testMaxHealth);
        }
        SetHealth(testHp, testMaxHealth);
    }
}
