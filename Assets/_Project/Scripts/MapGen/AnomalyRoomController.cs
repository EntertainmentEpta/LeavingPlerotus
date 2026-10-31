using UnityEngine;
using System.Collections;

/// <summary>
/// Controla a atmosfera serena e mística da Sala de Anomalia.
/// - Remove qualquer nódulo invasivo ou barreiras bloqueadoras.
/// - Contém exclusivamente a página/escritura ancestral do Caderno do Pesquisador.
/// - Garante o spawn da página milimetricamente acima do piso navegável (NavMesh + Raycast físico).
/// - Ativa a cura potencializada do traje (+30% HP ao invés de 15% e taxa 1.5x de síntese).
/// </summary>
public class AnomalyRoomController : MonoBehaviour
{
    [Header("Atmosfera e Áudio")]
    [Tooltip("Áudio ambiente de estática biológica / ressonância mística")]
    public AudioClip anomalyAtmosphereClip;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;

    [Header("Página do Caderno")]
    public Transform pageSpawnPoint;

    private AudioSource audioSource;
    private bool playerInside = false;

    void Awake()
    {
        // 1. Configura AudioSource próprio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.volume = ambientVolume;
        if (anomalyAtmosphereClip != null) audioSource.clip = anomalyAtmosphereClip;

        // 2. Garante um Trigger em objeto filho dedicado para não interferir na colisão do piso ou paredes
        EnsureTriggerZoneChild();

        // 3. Garante que a página do livro esteja presente na sala
        EnsureScripturePageSpawned();
    }

    private IEnumerator Start()
    {
        // Aguarda a geração de salas e bake de NavMesh completarem no LevelGenerator
        yield return null;
        yield return new WaitForSeconds(0.15f);
        RealignScripturePage();
    }

    private void EnsureTriggerZoneChild()
    {
        Transform existingZone = transform.Find("Anomaly_Trigger_Zone");
        if (existingZone != null) return;

        GameObject zoneObj = new GameObject("Anomaly_Trigger_Zone");
        zoneObj.transform.SetParent(transform, false);

        // Compensa escala do pai para manter tamanho em metros reais no mundo
        Vector3 parentLossy = transform.lossyScale;
        zoneObj.transform.localScale = new Vector3(
            parentLossy.x != 0f ? 1f / parentLossy.x : 1f,
            parentLossy.y != 0f ? 1f / parentLossy.y : 1f,
            parentLossy.z != 0f ? 1f / parentLossy.z : 1f
        );
        zoneObj.transform.localPosition = Vector3.zero;
        zoneObj.transform.position = transform.position + Vector3.up * 2.5f;

        BoxCollider zoneBox = zoneObj.AddComponent<BoxCollider>();
        zoneBox.isTrigger = true;
        zoneBox.size = new Vector3(26f, 8f, 26f);

        AnomalyZoneTriggerProxy proxy = zoneObj.AddComponent<AnomalyZoneTriggerProxy>();
        proxy.Init(this);
    }

    /// <summary>
    /// Calcula uma posição de referência confiável no centro da sala,
    /// usando o ConnectionPoint de entrada como âncora da porta e piso.
    /// </summary>
    private Vector3 GetRoomCenterReference()
    {
        ConnectionPoint cp = GetComponentInChildren<ConnectionPoint>();
        if (cp != null)
        {
            // O ConnectionPoint aponta para fora da sala (+forward).
            // Caminhamos 8.0m para dentro da sala (-forward)
            Vector3 center = cp.transform.position - (cp.transform.forward * 8.0f);
            center.y = cp.transform.position.y;
            return center;
        }

        MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
        if (mr != null)
        {
            return mr.bounds.center;
        }

        return transform.position;
    }

    /// <summary>
    /// Encontra o piso navegável sólido usando NavMesh (prioridade 1) e Raycast físico como fallback.
    /// Nunca deixa a página cair no abismo ou ficar sob o chão.
    /// </summary>
    private Vector3 FindSolidFloor(Vector3 referencePos)
    {
        // 1. Tenta NavMesh assado (prioridade máxima e precisão exata onde o player anda)
        if (UnityEngine.AI.NavMesh.SamplePosition(referencePos, out UnityEngine.AI.NavMeshHit navHit, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return navHit.position;
        }

        // 2. Raycast físico sólido vertical de cima para baixo
        Vector3 rayStart = referencePos + Vector3.up * 6.0f;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 20.0f, ~0, QueryTriggerInteraction.Ignore);
        float bestFloorY = float.MinValue;
        bool hitSolid = false;

        foreach (var h in hits)
        {
            if (h.collider != null && !h.collider.isTrigger && !h.collider.name.Contains("Anomaly_Trigger_Zone"))
            {
                if (h.point.y > bestFloorY && h.point.y < referencePos.y + 2.5f)
                {
                    bestFloorY = h.point.y;
                    hitSolid = true;
                }
            }
        }

        if (hitSolid)
        {
            return new Vector3(referencePos.x, bestFloorY, referencePos.z);
        }

        return referencePos;
    }

    private void EnsureScripturePageSpawned()
    {
        if (transform.Find("ScripturePickup_AnomalyPage") != null) return;

        Vector3 refPos = (pageSpawnPoint != null) ? pageSpawnPoint.position : GetRoomCenterReference();
        Vector3 floorPos = FindSolidFloor(refPos);
        Vector3 spawnPos = floorPos + Vector3.up * 0.45f;

        GameObject pageGo = new GameObject("ScripturePickup_AnomalyPage");
        pageGo.transform.position = spawnPos;
        pageGo.transform.SetParent(transform, true);

        // Compensa escala do pai para que a página, seu colisor e luz tenham dimensões métricas no mundo
        Vector3 pLossy = transform.lossyScale;
        pageGo.transform.localScale = new Vector3(
            pLossy.x != 0f ? 1f / pLossy.x : 1f,
            pLossy.y != 0f ? 1f / pLossy.y : 1f,
            pLossy.z != 0f ? 1f / pLossy.z : 1f
        );

        // Visual brilhante do manuscrito
        GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sheet.name = "Page_Mesh";
        sheet.transform.SetParent(pageGo.transform, false);
        sheet.transform.localPosition = Vector3.zero;
        sheet.transform.localScale = new Vector3(0.7f, 0.02f, 0.9f);
        sheet.transform.localRotation = Quaternion.Euler(0, 35f, 0);

        Collider sheetCol = sheet.GetComponent<Collider>();
        if (sheetCol != null) Destroy(sheetCol);

        Renderer rend = sheet.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material pageMat = new Material(litShader);
            pageMat.color = new Color(0.96f, 0.92f, 0.78f);
            if (pageMat.HasProperty("_BaseColor")) pageMat.SetColor("_BaseColor", new Color(0.96f, 0.92f, 0.78f, 1f));
            if (pageMat.HasProperty("_EmissionColor"))
            {
                pageMat.EnableKeyword("_EMISSION");
                pageMat.SetColor("_EmissionColor", new Color(1.0f, 0.75f, 0.3f, 1f) * 1.5f);
            }
            rend.material = pageMat;
        }

        // Luz dourada suave iluminando a página
        GameObject lightObj = new GameObject("Page_Glow");
        lightObj.transform.SetParent(pageGo.transform, false);
        lightObj.transform.localPosition = Vector3.up * 0.4f;
        Light pLight = lightObj.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(1.0f, 0.85f, 0.4f);
        pLight.intensity = 2.5f;
        pLight.range = 5.0f;

        // Trigger de coleta com raio generoso (2.8m)
        SphereCollider trig = pageGo.AddComponent<SphereCollider>();
        trig.isTrigger = true;
        trig.radius = 2.8f;

        AnomalyScripturePickup pickup = pageGo.AddComponent<AnomalyScripturePickup>();
        pickup.Init("scripture_blood_tragedy");
        pickup.SetBasePosition(spawnPos);
    }

    /// <summary>
    /// Re-alinha a página ao NavMesh após o LevelGenerator assar o mapa.
    /// </summary>
    public void RealignScripturePage()
    {
        Transform pageTransform = transform.Find("ScripturePickup_AnomalyPage");
        if (pageTransform == null) return;

        Vector3 refPos = (pageSpawnPoint != null) ? pageSpawnPoint.position : GetRoomCenterReference();
        Vector3 floorPos = FindSolidFloor(refPos);
        Vector3 targetPos = floorPos + Vector3.up * 0.45f;

        pageTransform.position = targetPos;

        // Garante escala normalizada no mundo
        Vector3 pLossy = transform.lossyScale;
        pageTransform.localScale = new Vector3(
            pLossy.x != 0f ? 1f / pLossy.x : 1f,
            pLossy.y != 0f ? 1f / pLossy.y : 1f,
            pLossy.z != 0f ? 1f / pLossy.z : 1f
        );

        AnomalyScripturePickup pickup = pageTransform.GetComponent<AnomalyScripturePickup>();
        if (pickup != null)
        {
            pickup.SetBasePosition(targetPos);
        }
    }

    public void OnPlayerEnterZone()
    {
        if (playerInside) return;
        playerInside = true;

        Debug.Log("🌫️ [ANOMALIA] O jogador entrou na Câmara de Anomalia. Cura amplificada para 30% e síntese acelerada.");

        // Ativa modo Anomalia no traje (+30% de cura ao invés de 15%, e taxa 1.5x)
        if (SuitHealingSystem.Instance != null)
        {
            SuitHealingSystem.Instance.isInAnomalyRoom = true;
        }

        // Atenua música de fundo suavemente
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            GlobalAudioManager.Instance.mainMixer.SetFloat("BGMVolume", -25f);
        }

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }

        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso("🌫️ <i>\"Câmara de Anomalia... Campo biológico estável detectado. Síntese do traje potencializada (+30% HP).\"</i>");
        }
    }

    public void OnPlayerExitZone()
    {
        if (!playerInside) return;
        playerInside = false;

        if (SuitHealingSystem.Instance != null)
        {
            SuitHealingSystem.Instance.isInAnomalyRoom = false;
        }

        // Restaura música com base no volume salvo
        if (GlobalAudioManager.Instance != null && GlobalAudioManager.Instance.mainMixer != null)
        {
            float bgmVal = PlayerPrefs.GetFloat("SavedBGM", 0.5f);
            GlobalAudioManager.Instance.mainMixer.SetFloat("BGMVolume", Mathf.Log10(Mathf.Clamp(bgmVal, 0.0001f, 1f)) * 20f);
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}

/// <summary>
/// Proxy de trigger para detectar entrada/saída do player na zona da sala de anomalia sem tocar no collider raiz da sala.
/// </summary>
public class AnomalyZoneTriggerProxy : MonoBehaviour
{
    private AnomalyRoomController controller;

    public void Init(AnomalyRoomController ctrl)
    {
        controller = ctrl;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (controller == null) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            controller.OnPlayerEnterZone();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller == null) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            controller.OnPlayerExitZone();
        }
    }
}

/// <summary>
/// Coletor animado da escritura ancestral na sala de anomalia.
/// Rotação mística, flutuação vertical contínua, suporte a coleta por toque ou tecla [E] / [F].
/// </summary>
public class AnomalyScripturePickup : MonoBehaviour
{
    private string scriptureId = "scripture_blood_tragedy";
    private bool collected = false;
    private Vector3 basePosition;
    private float floatPhase;

    public void Init(string id)
    {
        scriptureId = id;
    }

    public void SetBasePosition(Vector3 newBasePos)
    {
        basePosition = newBasePos;
        transform.position = newBasePos;
    }

    private void Start()
    {
        if (basePosition == Vector3.zero)
        {
            basePosition = transform.position;
        }
        floatPhase = Random.Range(0f, 6.28f);
    }

    private void Update()
    {
        if (collected) return;

        // Rotação suave no mundo
        transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);

        // Flutuação senoidal graciosa (bobbing)
        float yBob = Mathf.Sin((Time.time + floatPhase) * 2.2f) * 0.08f;
        transform.position = basePosition + (Vector3.up * yBob);

        // Suporte a pressionamento de tecla ao se aproximar
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && Vector3.Distance(player.transform.position, transform.position) <= 2.8f)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
            {
                Collect();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerHealth>() != null)
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;

        if (LoreManager.Instance != null)
        {
            LoreScriptureData data = LoreManager.Instance.GetScripture(scriptureId);
            if (data != null)
            {
                LoreManager.Instance.CollectScripture(data);
            }
            else
            {
                // Fallback para a próxima escritura não descoberta
                LoreScriptureData nextData = LoreManager.Instance.GetNextUndiscoveredScripture();
                if (nextData != null)
                {
                    LoreManager.Instance.CollectScripture(nextData);
                }
            }
        }

        // Som de coleta
        AudioClip sound = Resources.Load<AudioClip>("SFX/Item_Pickup") ?? 
                          Resources.Load<AudioClip>("confirmation_002");
        if (sound != null)
        {
            AudioSource.PlayClipAtPoint(sound, transform.position, 0.9f);
        }



        Destroy(gameObject);
    }
}
