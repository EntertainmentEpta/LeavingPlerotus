using UnityEngine;
using System.Collections;

/// <summary>
/// IA do Crystal Watcher (Vigia de Cristal) — Inimigo estático que dispara um laser giratório
/// COMO FUNCIONA:
/// 1. O inimigo fica parado no chão
/// 2. Quando o player se aproxima, ele começa a carregar (efeito visual)
/// 3. Após o carregamento, um laser sai do cristal e gira seguindo o player
/// 4. O laser sempre gira pelo caminho mais curto até o player
/// 5. Se o player dashar pro outro lado, o laser inverte a rotação
/// 6. O laser causa dano contínuo se tocar no player
/// 7. Após alguns segundos, o laser desliga e o ciclo recomeça
/// 
/// COMPONENTES NECESSÁRIOS no GameObject:
/// DummyHealth (vida, barra de HP, morte e drops)
/// EnemyDrops (sistema de loot)
/// Collider (para o player poder atacar)
/// Rigidbody (kinematic)
/// </summary>

[RequireComponent(typeof(DummyHealth))]
public class CrystalWatcher_AI : MonoBehaviour
{

    private Transform playerTransform;   // Referência ao player
    private DummyHealth health;          // Componente de vida
    private CrystalWatcherVFX vfx;       // Efeitos visuais do laser

    // ATIVAÇÃO

    [Header("Ativação")]
    [Tooltip("Distância em que o inimigo detecta o player e começa a atacar")]
    public float activationDistance = 20f;
    private bool isActivated = false;    // Guardamos se já foi ativado


    // CICLO DE ATAQUE
    
    [Header("Ciclo de Ataque")]
    [Tooltip("Tempo de carregamento antes do laser disparar (em segundos)")]
    public float chargeTime = 0.5f;

    [Tooltip("Duração do laser ativo (em segundos)")]
    public float fireDuration = 4.0f;

    [Tooltip("Pausa entre ciclos de ataque (em segundos)")]
    public float cooldownTime = 2.0f;

    // LASER
    
    [Header("Laser")]
    [Tooltip("Dano causado por cada tick do laser")]
    public int laserDamage = 15;

    [Tooltip("Intervalo entre ticks de dano (em segundos). Menor = mais dano por segundo")]
    public float damageTickRate = 0.3f;

    [Tooltip("Velocidade de rotação do laser em graus por segundo")]
    public float rotationSpeed = 30f;

    [Tooltip("Alcance máximo do laser")]
    public float laserRange = 20f;

    [Tooltip("Largura do laser para detecção de colisão")]
    public float laserWidth = 0.5f;

    // ÁUDIO

    [Header("Áudio")]
    [Tooltip("Som do hover/flutuação (loop constante do inimigo)")]
    public AudioClip hoverSound;
    [Tooltip("Volume do som de hover")]
    [Range(0f, 1f)]
    public float hoverSoundVolume = 0.4f;

    [Tooltip("Som do carregamento antes do disparo")]
    public AudioClip chargeSound;
    [Tooltip("Volume do som de carregamento")]
    [Range(0f, 1f)]
    public float chargeSoundVolume = 0.8f;

    [Tooltip("Som do feixe de laser disparando (loop durante o disparo)")]
    public AudioClip firingSound;
    [Tooltip("Volume do som de disparo do laser")]
    [Range(0f, 1f)]
    public float firingSoundVolume = 0.8f;

    [Tooltip("Som do impacto do laser no player (loop enquanto atinge o player)")]
    public AudioClip impactSound;
    [Tooltip("Volume do som de impacto no player")]
    [Range(0f, 1f)]
    public float impactSoundVolume = 0.8f;

    private AudioSource hoverAudioSource;
    private AudioSource firingAudioSource;
    private AudioSource impactAudioSource;

    private AudioSource baseAudioSource;

    // BUFF (quando Crystal Tuner está conectado)
    
    [Header("Buff")]
    private bool isBuffed = false;
    private float originalChargeTime;
    private float originalRotationSpeed;

    // ESTADO INTERNO — controle do ciclo
    
    // Enum = lista de estados possíveis
    private enum State
    {
        Idle,       // Dormindo, esperando o player se aproximar
        Charging,   // Carregando energia antes de disparar
        Firing,     // Laser ativo, girando e causando dano
        Cooldown    // Descansando entre ataques
    }

    private State currentState = State.Idle;

    // Controle do laser visual
    private LineRenderer laserLine;           // O componente que desenha a linha do laser
    private float currentLaserAngle = 0f;     // Ângulo atual do laser (em graus, no plano XZ)
    private float damageTimer = 0f;           // Timer para controlar ticks de dano
    private Renderer modelRenderer;            // Renderer do modelo 3D (para achar o centro)

    // START — Inicialização (roda uma vez quando o jogo começa)
    
    void Start()
    {
        // Sobrescreve o default antigo curto para o novo longo que cruza o mapa
        if (laserRange == 20f)
        {
            laserRange = 120f;
        }

        // Pega o componente de vida que está no mesmo GameObject
   
        health = GetComponent<DummyHealth>();

        // Encontra o player na cena pela tag "Player"
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("[WATCHER] Player não encontrado! Verifique a tag 'Player'.");
        }

        // Cria o LineRenderer para o laser (o componente que desenha linhas na tela)
        SetupLaserVisual();

        // Busca o Renderer do modelo 3D filho para encontrar o centro exato
        modelRenderer = GetComponentInChildren<Renderer>();

        // Configura VFX de partículas 
        vfx = GetComponent<CrystalWatcherVFX>();
        if (vfx == null)
        {
            vfx = gameObject.AddComponent<CrystalWatcherVFX>();
        }
        baseAudioSource = GetComponent<AudioSource>();
        // Configura AudioSources de hover, disparo e impacto
        SetupAudioSources();
    }

    // UPDATE — Roda todo frame
    
    void Update()
    {
        // Se não achou o player, não faz nada
        if (playerTransform == null) return;

        // Se o inimigo morreu, desliga visual e áudios
        if (health != null && health.CurrentHealth <= 0)
        {
            if (laserLine != null) laserLine.enabled = false;
            StopAllAudioSources();
            return;
        }

        // MÁQUINA DE ESTADOS 
        // Dependendo do estado atual, executa uma lógica diferente
        switch (currentState)
        {
            case State.Idle:
                HandleIdle();
                break;

            case State.Charging:
                // Charging é controlado pela Coroutine, não faz nada aqui
                break;

            case State.Firing:
                HandleFiring();
                break;

            case State.Cooldown:
                // Cooldown é controlado pela Coroutine, não faz nada aqui
                break;
        }
    }


    // ESTADO: IDLE

    void HandleIdle()
    {
        // Calcula a distância entre o inimigo e o player
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Se o player está perto o suficiente, ativa
        if (distToPlayer < activationDistance)
        {
            isActivated = true;
            Debug.Log("[WATCHER] Ativado! Player detectado a " + distToPlayer.ToString("F1") + "m");

            // Inicia o ciclo de ataque (Coroutine = função que pode "pausar" e esperar)
            StartCoroutine(AttackCycle());
        }
    }

    // CICLO DE ATAQUE (Coroutine)
    // "Faça X, espere 2 segundos, faça Y, espere mais 3 segundos..."

    IEnumerator AttackCycle()
    {
        // Loop infinito — o ciclo repete até o inimigo morrer
        while (health != null && health.CurrentHealth > 0)
        {
            // FASE 1: CARREGAMENTO 
            currentState = State.Charging;
            StopFiringAudioLoop();
            StopImpactAudioLoop();
            PlayChargeSound();
            Debug.Log("[WATCHER] Carregando laser...");

            // Aponta o laser na direção do player ANTES de carregar
            Vector3 dirToPlayer = (playerTransform.position - transform.position);
            dirToPlayer.y = 0; // Ignora altura (laser é horizontal)
            currentLaserAngle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;

            // Gira o modelo para encarar o player durante o carregamento
            transform.rotation = Quaternion.Euler(0, currentLaserAngle, 0);

            // Mostra o laser fraquinho durante o carregamento 
            ShowLaserCharging(true);

            // Ativa partículas de carregamento
            if (vfx != null)
            {
                vfx.StartChargeEffect();
                vfx.SetAmbientIntensity(3f); // Aura mais intensa
            }

            // Espera o tempo de carregamento
            yield return new WaitForSeconds(chargeTime);

            // FASE 2: DISPARANDO 
            currentState = State.Firing;
            damageTimer = 0f;
            StartFiringAudioLoop();
            Debug.Log("[WATCHER] LASER ATIVO!");

            // Para carregamento e ativa partículas do laser
            if (vfx != null)
            {
                vfx.StopChargeEffect();
                vfx.StartLaserEffect(AngleToDirection(currentLaserAngle));
                vfx.SetAmbientIntensity(5f); // Aura bem intensa durante disparo
            }

            // Mostra o laser com força total
            ShowLaserFiring(true);

            // O laser fica ativo por 'fireDuration' segundos
            // A lógica de rotação e dano roda no Update() > HandleFiring()
            yield return new WaitForSeconds(fireDuration);

            // FASE 3: DESLIGANDO 
            ShowLaserFiring(false);
            StopFiringAudioLoop();
            StopImpactAudioLoop();

            if (vfx != null)
            {
                vfx.StopLaserEffect();
                vfx.SetAmbientIntensity(1f); // Aura volta ao normal
            }
            Debug.Log("[WATCHER] Laser desligado. Descansando...");

            // FASE 4: COOLDOWN 
            currentState = State.Cooldown;
            yield return new WaitForSeconds(cooldownTime);

            // Verifica se o player ainda está no range
            // Se saiu, volta pro Idle
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist > activationDistance * 1.5f)
            {
                currentState = State.Idle;
                isActivated = false;
                Debug.Log("[WATCHER] Player saiu do range. Voltando a dormir.");
                yield break; // Sai da Coroutine
            }
        }
    }

    // ESTADO: FIRING (laser ativo)

    void HandleFiring()
    {
        // 1. CALCULA PARA ONDE O LASER DEVERIA APONTAR (direção do player)
        Vector3 dirToPlayer = (playerTransform.position - transform.position);
        dirToPlayer.y = 0; // Laser no plano horizontal

        // Converte a direção em ângulo (0-360 graus)
        float targetAngle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;

        // 2. CALCULA A DIFERENÇA DE ÂNGULO (caminho mais curto)
        // Mathf.DeltaAngle retorna um valor entre -180 e 180
        // Positivo = girar horário, Negativo = girar anti-horário
        // Isso é o que faz o laser inverter quando o player dá dash pro outro lado!
        float angleDifference = Mathf.DeltaAngle(currentLaserAngle, targetAngle);

        // 3. GIRA O LASER na velocidade configurada
        // Mathf.Sign retorna +1 ou -1 dependendo do sinal
        // Isso garante que o laser gira pelo caminho mais curto
        float maxRotation = rotationSpeed * Time.deltaTime;
        if (Mathf.Abs(angleDifference) > maxRotation)
        {
            // Se falta girar mais do que a velocidade permite, gira o máximo possível
            currentLaserAngle += Mathf.Sign(angleDifference) * maxRotation;
        }
        else
        {
            // Se falta pouco, vai direto pro ângulo do player
            currentLaserAngle = targetAngle;
        }

        // 4. GIRA O MODELO DO CRYSTAL WATCHER junto com o laser
        // Quaternion.Euler cria uma rotação a partir de ângulos
        // Só rotacionamos no eixo Y (horizontal), mantendo X e Z em 0
        transform.rotation = Quaternion.Euler(0, currentLaserAngle, 0);

        // 5. ATUALIZA DIREÇÃO DAS PARTÍCULAS DO LASER
        if (vfx != null)
        {
            vfx.UpdateLaserDirection(AngleToDirection(currentLaserAngle));
        }

        // 6. ATUALIZA A POSIÇÃO VISUAL DO LASER
        UpdateLaserVisual();

        // 6. VERIFICA SE O LASER ESTÁ ACERTANDO O PLAYER
        CheckLaserHit();
    }

    // DETECÇÃO DE COLISÃO DO LASER

    void CheckLaserHit()
    {
        damageTimer += Time.deltaTime;

        Vector3 origin = GetLaserOrigin(); // Um pouco acima do chão
        
        // Garante que o laser comece a pelo menos 1.2m do chão para nunca raspar no piso
        float minHeight = transform.position.y + 1.2f;
        if (origin.y < minHeight)
        {
            origin.y = minHeight;
        }

        // Calcula a direção do laser baseada no ângulo horizontal (sem desvio vertical)
        Vector3 laserDirection = AngleToDirection(currentLaserAngle);

        // Encontra a distância da parede mais próxima para não dar dano através dela
        float finalLaserDist = laserRange;
        RaycastHit[] wallHits = Physics.RaycastAll(origin, laserDirection, laserRange);
        foreach (RaycastHit wh in wallHits)
        {
            if (!wh.collider.CompareTag("Player") && !wh.collider.CompareTag("Enemy") && wh.collider.GetComponent<DummyHealth>() == null)
            {
                if (wh.distance < finalLaserDist)
                {
                    finalLaserDist = wh.distance;
                }
            }
        }

        // SphereCast = como um Raycast mas com "espessura"
        RaycastHit[] hits = Physics.SphereCastAll(origin, laserWidth, laserDirection, laserRange);
        
        bool hitPlayerThisFrame = false;

        foreach (RaycastHit h in hits)
        {
            // Se algum desses alvos for o Player, causa dano!
            if (h.collider.CompareTag("Player"))
            {
                // Só causa dano se o player estiver antes/rente à parede
                if (h.distance <= finalLaserDist + 0.5f) // pequena margem de tolerância
                {
                    hitPlayerThisFrame = true;

                    if (damageTimer >= damageTickRate)
                    {
                        PlayerHealth playerHealth = h.collider.GetComponent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            int finalDamage = laserDamage;
                            
                            if (isBuffed) finalDamage = Mathf.RoundToInt(finalDamage * 1.5f);
                            
                            playerHealth.TakeDamage(finalDamage, gameObject);
                            damageTimer = 0f; // Reseta o timer pra não dar multi-hit insano no mesmo frame
                            Debug.Log("[WATCHER] Laser acertou o player! Dano: " + finalDamage);
                        }
                    }
                    break; // Já achou o player, pode parar de procurar nos hits.
                }
            }
        }

        // Controla o loop de som de impacto enquanto o laser atinge o player
        SetImpactAudioHitting(hitPlayerThisFrame);
    }

    // =============================================
    // SISTEMA DE ÁUDIO (Hover, Charge, Firing, Impact)
    // =============================================

    private void SetupAudioSources()
        {
            // Hover AudioSource (Loop constante)
            if (hoverSound != null)
            {
                GameObject hoverObj = new GameObject("Audio_Hover");
                hoverObj.transform.SetParent(transform, false);
                hoverAudioSource = hoverObj.AddComponent<AudioSource>();
                hoverAudioSource.clip = hoverSound;
                hoverAudioSource.volume = hoverSoundVolume;
                hoverAudioSource.loop = true;
                hoverAudioSource.spatialBlend = 1f; // 3D Audio
                hoverAudioSource.minDistance = 3f;
                hoverAudioSource.maxDistance = 25f;
                if (baseAudioSource != null) hoverAudioSource.outputAudioMixerGroup = baseAudioSource.outputAudioMixerGroup;
                hoverAudioSource.Play();
            }

            // Firing AudioSource (Loop do feixe de laser)
            if (firingSound != null)
            {
                GameObject firingObj = new GameObject("Audio_Firing");
                firingObj.transform.SetParent(transform, false);
                firingAudioSource = firingObj.AddComponent<AudioSource>();
                firingAudioSource.clip = firingSound;
                firingAudioSource.volume = firingSoundVolume;
                firingAudioSource.loop = true;
                firingAudioSource.spatialBlend = 1f;
                firingAudioSource.minDistance = 3f;
                firingAudioSource.maxDistance = 30f;
                if (baseAudioSource != null) firingAudioSource.outputAudioMixerGroup = baseAudioSource.outputAudioMixerGroup;
            }

            // Impact AudioSource (Loop de impacto no player)
            if (impactSound != null)
            {
                GameObject impactObj = new GameObject("Audio_Impact");
                impactObj.transform.SetParent(transform, false);
                impactAudioSource = impactObj.AddComponent<AudioSource>();
                impactAudioSource.clip = impactSound;
                impactAudioSource.volume = impactSoundVolume;
                impactAudioSource.loop = true;
                impactAudioSource.spatialBlend = 1f;
                impactAudioSource.minDistance = 3f;
                impactAudioSource.maxDistance = 30f;
                if (baseAudioSource != null) impactAudioSource.outputAudioMixerGroup = baseAudioSource.outputAudioMixerGroup;
            }
        }
    private void PlayChargeSound()
    {
        if (chargeSound != null)
        {
            PlayClipAtPointWithPitch(chargeSound, transform.position, 1.0f, chargeSoundVolume);
        }
    }

    private void StartFiringAudioLoop()
    {
        if (firingAudioSource != null && firingSound != null)
        {
            firingAudioSource.volume = firingSoundVolume;
            if (!firingAudioSource.isPlaying)
                firingAudioSource.Play();
        }
    }

    private void StopFiringAudioLoop()
    {
        if (firingAudioSource != null && firingAudioSource.isPlaying)
        {
            firingAudioSource.Stop();
        }
    }

    private void SetImpactAudioHitting(bool hitting)
    {
        if (impactAudioSource != null && impactSound != null)
        {
            impactAudioSource.volume = impactSoundVolume;
            if (hitting)
            {
                if (!impactAudioSource.isPlaying)
                    impactAudioSource.Play();
            }
            else
            {
                if (impactAudioSource.isPlaying)
                    impactAudioSource.Stop();
            }
        }
    }

    private void StopImpactAudioLoop()
    {
        SetImpactAudioHitting(false);
    }

    private void StopAllAudioSources()
    {
        if (hoverAudioSource != null && hoverAudioSource.isPlaying) hoverAudioSource.Stop();
        StopFiringAudioLoop();
        StopImpactAudioLoop();
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float pitch, float volume)
        {
            if (baseAudioSource != null)
            {
                baseAudioSource.pitch = pitch;
                baseAudioSource.PlayOneShot(clip, volume);
            }
        }
    // VISUAL DO LASER (LineRenderer)
    
    // Duas camadas de laser: núcleo interno + brilho externo
    private LineRenderer laserGlow;  // Camada externa (brilho roxo suave)

    /// <summary>
    /// Configura os LineRenderers que desenham o laser na tela
    /// Usamos DUAS linhas sobrepostas para criar efeito de brilho:
    /// - laserLine = núcleo interno (fino, branco-roxo, brilhante)
    /// - laserGlow = brilho externo (grosso, roxo transparente)
    /// </summary>
    
    void SetupLaserVisual()
    {
        // === CAMADA 1: BRILHO EXTERNO (glow) ===
        GameObject glowObj = new GameObject("LaserGlow");
        glowObj.transform.SetParent(transform);
        glowObj.transform.localPosition = Vector3.zero;

        laserGlow = glowObj.AddComponent<LineRenderer>();
        laserGlow.positionCount = 2;
        laserGlow.startWidth = 0.8f;      // Mais grosso que o núcleo
        laserGlow.endWidth = 0.4f;
        laserGlow.useWorldSpace = true;
        laserGlow.numCapVertices = 8;
        laserGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Shader glowShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (glowShader == null) glowShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (glowShader == null) glowShader = Shader.Find("Sprites/Default");
        Material glowMat = new Material(glowShader);
        laserGlow.material = glowMat;

        // Gradiente roxo suave e transparente
        Gradient glowGradient = new Gradient();
        glowGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.6f, 0.1f, 0.9f), 0.0f),   // Roxo vibrante
                new GradientColorKey(new Color(0.4f, 0.0f, 0.7f), 0.5f),   // Roxo escuro
                new GradientColorKey(new Color(0.6f, 0.1f, 0.9f), 1.0f),   // Roxo vibrante
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.4f, 0.0f),   // Semi-transparente
                new GradientAlphaKey(0.25f, 0.5f),
                new GradientAlphaKey(0.05f, 1.0f),   // Quase invisível na ponta
            }
        );
        laserGlow.colorGradient = glowGradient;
        laserGlow.enabled = false;

        // CAMADA 2: NÚCLEO INTERNO (core)
        GameObject laserObj = new GameObject("LaserCore");
        laserObj.transform.SetParent(transform);
        laserObj.transform.localPosition = Vector3.zero;

        laserLine = laserObj.AddComponent<LineRenderer>();
        laserLine.positionCount = 2;
        laserLine.startWidth = 0.15f;     // Fino e concentrado
        laserLine.endWidth = 0.08f;
        laserLine.useWorldSpace = true;
        laserLine.numCapVertices = 8;
        laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Shader coreShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (coreShader == null) coreShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (coreShader == null) coreShader = Shader.Find("Sprites/Default");
        Material coreMat = new Material(coreShader);
        laserLine.material = coreMat;

        // Gradiente do núcleo: branco → roxo claro → roxo
        Gradient coreGradient = new Gradient();
        coreGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.9f, 0.7f, 1.0f), 0.0f),   // Branco-roxo (brilhante)
                new GradientColorKey(new Color(0.8f, 0.4f, 1.0f), 0.3f),   // Roxo claro
                new GradientColorKey(new Color(0.6f, 0.1f, 0.9f), 1.0f),   // Roxo puro
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0.5f, 1.0f),
            }
        );
        laserLine.colorGradient = coreGradient;
        laserLine.enabled = false;
    }

    /// <summary>
    /// Mostra o laser fraquinho durante o carregamento 
    /// </summary>
    void ShowLaserCharging(bool show)
    {
        if (laserLine == null) return;
        laserLine.enabled = show;
        if (laserGlow != null) laserGlow.enabled = false; // Sem glow durante carregamento
        if (show)
        {
            // Laser bem fininho e transparente — só um "fio" roxo de aviso
            laserLine.startWidth = 0.04f;
            laserLine.endWidth = 0.02f;
            UpdateLaserVisual();
        }
    }

    /// <summary>
    /// Mostra/esconde o laser com força total (núcleo + glow)
    /// </summary>
    void ShowLaserFiring(bool show)
    {
        if (laserLine == null) return;
        laserLine.enabled = show;
        if (laserGlow != null) laserGlow.enabled = show;
        if (show)
        {
            // Tamanhos iniciais — vão pulsar no UpdateLaserVisual
            laserLine.startWidth = 0.15f;
            laserLine.endWidth = 0.08f;
            laserGlow.startWidth = 0.8f;
            laserGlow.endWidth = 0.4f;
        }
    }

    /// <summary>
    /// Atualiza as posições e efeito pulsante do laser todo frame
    /// </summary>
    void UpdateLaserVisual()
    {
        if (laserLine == null || !laserLine.enabled) return;

        Vector3 origin = GetLaserOrigin();
        
        // Garante que o laser comece a pelo menos 1.2m do chão para nunca raspar no piso
        float minHeight = transform.position.y + 1.2f;
        if (origin.y < minHeight)
        {
            origin.y = minHeight;
        }

        Vector3 direction = AngleToDirection(currentLaserAngle);
        Vector3 endPoint = origin + direction * laserRange;

        float finalLaserDist = laserRange;

        // Pega TODOS os objetos que o laser cruzar
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, laserRange);

        foreach (RaycastHit h in hits)
        {
            // Nós queremos que o laser IGNORE ("atravesse") o Player e outros Inimigos.
            // Qualquer outra coisa sem essas Tags (como Paredes e Chão) vai parar o laser curto.
            if (!h.collider.CompareTag("Player") && !h.collider.CompareTag("Enemy") && h.collider.GetComponent<DummyHealth>() == null)
            {
                // Se a parede estiver mais perto que o alcance máximo, o laser para nela.
                if (h.distance < finalLaserDist)
                {
                    finalLaserDist = h.distance;
                }
            }
        }
        
        endPoint = origin + direction * finalLaserDist;

        // Posiciona ambas as linhas
        laserLine.SetPosition(0, origin);
        laserLine.SetPosition(1, endPoint);

        if (vfx != null)
        {
            // Encontra a normal da parede atingida para gerar faíscas ricocheteando corretamente
            Vector3 hitNormal = -direction; // fallback caso não ache a colisão exata
            foreach (RaycastHit h in hits)
            {
                if (h.distance == finalLaserDist)
                {
                    hitNormal = h.normal;
                    break;
                }
            }
            vfx.UpdateLaserImpact(endPoint, hitNormal);
        }

        if (laserGlow != null && laserGlow.enabled)
        {
            laserGlow.SetPosition(0, origin);
            laserGlow.SetPosition(1, endPoint);

            // === EFEITO DE PULSAÇÃO ===
            // Faz o laser "respirar" — oscila a largura suavemente
            float pulse = Mathf.Sin(Time.time * 8f) * 0.15f; // Oscila ±0.15
            laserLine.startWidth = 0.15f + pulse * 0.3f;
            laserGlow.startWidth = 0.8f + pulse;
            laserGlow.endWidth = 0.4f + pulse * 0.5f;
        }
    }

    // FUNÇÕES AUXILIARES

    /// <summary>
    /// Retorna o centro exato do modelo 3D.
    /// Usa o bounds.center do Renderer, que dá o ponto central da malha visível.
    /// </summary>
    Vector3 GetLaserOrigin()
    {
        if (modelRenderer != null)
            return modelRenderer.bounds.center;
        return transform.position;
    }

    /// <summary>
    /// Converte um ângulo em graus para uma direção no plano XZ
    /// Exemplo: 0° = frente (Z+), 90° = direita (X+), 180° = trás (Z-)
    /// </summary>
    Vector3 AngleToDirection(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
    }

    // =============================================
    // SISTEMA DE BUFF (Crystal Tuner)
    // =============================================
    
    /// <summary>
    /// Chamado pelo Crystal Tuner quando conecta/desconecta
    /// Quando buffado: carrega mais rápido e laser gira mais rápido
    /// </summary>
    public void SetBuff(bool active)
    {
        if (active && !isBuffed)
        {
            isBuffed = true;
            originalChargeTime = chargeTime;
            originalRotationSpeed = rotationSpeed;

            chargeTime *= 0.5f;         // Carrega 2x mais rápido
            rotationSpeed *= 1.5f;      // Laser gira 50% mais rápido

            Debug.Log("[WATCHER] BUFFADO pelo Crystal Tuner!");
        }
        else if (!active && isBuffed)
        {
            isBuffed = false;
            chargeTime = originalChargeTime;
            rotationSpeed = originalRotationSpeed;

            Debug.Log("[WATCHER] Buff removido.");
        }
    }

    // GIZMOS — Visualização no Editor (só aparece no Scene view, não no jogo)
    
    void OnDrawGizmosSelected()
    {
        // Círculo amarelo = range de ativação
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);

        // Linha vermelha = direção atual do laser
        Gizmos.color = Color.red;
        Vector3 dir = AngleToDirection(currentLaserAngle);
        Gizmos.DrawRay(GetLaserOrigin(), dir * laserRange);

        // Círculo do alcance do laser
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, laserRange);
    }
}
