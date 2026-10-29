using UnityEngine;

/// <summary>
/// Controla a atmosfera serena e mística da Sala de Anomalia.
/// - Remove qualquer nódulo invasivo ou barreiras bloqueadoras.
/// - Contém exclusivamente a página/escritura ancestral do Caderno do Pesquisador.
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

    private void EnsureTriggerZoneChild()
    {
        Transform existingZone = transform.Find("Anomaly_Trigger_Zone");
        if (existingZone != null) return;

        GameObject zoneObj = new GameObject("Anomaly_Trigger_Zone");
        zoneObj.transform.SetParent(transform, false);
        zoneObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);

        BoxCollider zoneBox = zoneObj.AddComponent<BoxCollider>();
        zoneBox.isTrigger = true;
        zoneBox.size = new Vector3(26f, 8f, 26f);

        AnomalyZoneTriggerProxy proxy = zoneObj.AddComponent<AnomalyZoneTriggerProxy>();
        proxy.Init(this);
    }

    private void EnsureScripturePageSpawned()
    {
        if (transform.Find("ScripturePickup_AnomalyPage") != null) return;

        Vector3 basePos = (pageSpawnPoint != null) ? pageSpawnPoint.position : transform.position;
        // Raycast para assentar o item com precisão milimétrica no piso, ignorando estritamente triggers como AutoSpawnArea
        RaycastHit[] hits = Physics.RaycastAll(basePos + Vector3.up * 6f, Vector3.down, 15f, ~0, QueryTriggerInteraction.Ignore);
        float bestFloorY = float.MinValue;
        bool hitSolid = false;

        foreach (var h in hits)
        {
            if (h.collider != null && !h.collider.isTrigger)
            {
                if (h.point.y > bestFloorY && h.point.y < basePos.y + 2.0f)
                {
                    bestFloorY = h.point.y;
                    hitSolid = true;
                }
            }
        }

        Vector3 spawnPos = hitSolid ? new Vector3(basePos.x, bestFloorY + 0.35f, basePos.z) : (basePos + Vector3.up * 0.35f);

        GameObject pageGo = new GameObject("ScripturePickup_AnomalyPage");
        pageGo.transform.position = spawnPos;
        pageGo.transform.SetParent(transform, true);

        // Visual brilhante do manuscrito
        GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sheet.name = "Page_Mesh";
        sheet.transform.SetParent(pageGo.transform, false);
        sheet.transform.localScale = new Vector3(0.7f, 0.02f, 0.9f);
        sheet.transform.localRotation = Quaternion.Euler(0, 35f, 0);

        Collider sheetCol = sheet.GetComponent<Collider>();
        if (sheetCol != null) Destroy(sheetCol);

        Renderer rend = sheet.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material pageMat = new Material(litShader);
            pageMat.color = new Color(0.92f, 0.88f, 0.72f);
            rend.material = pageMat;
        }

        // Luz dourada suave iluminando a página
        GameObject lightObj = new GameObject("Page_Glow");
        lightObj.transform.SetParent(pageGo.transform, false);
        lightObj.transform.localPosition = Vector3.up * 0.5f;
        Light pLight = lightObj.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(1.0f, 0.85f, 0.4f);
        pLight.intensity = 2.0f;
        pLight.range = 5.0f;

        // Trigger de coleta
        BoxCollider box = pageGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2.5f, 2.0f, 2.5f);

        AnomalyScripturePickup pickup = pageGo.AddComponent<AnomalyScripturePickup>();
        pickup.Init("scripture_blood_tragedy");
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
/// Coletor da escritura ancestral na sala de anomalia.
/// </summary>
public class AnomalyScripturePickup : MonoBehaviour
{
    private string scriptureId = "scripture_blood_tragedy";
    private bool collected = false;

    public void Init(string id)
    {
        scriptureId = id;
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
        }

        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso("📖 <b>PÁGINA DO CADERNO RECOLHIDA!</b>\nO relato ancestral foi adicionado ao seu Caderno de Pesquisa.");
        }

        Destroy(gameObject);
    }
}
