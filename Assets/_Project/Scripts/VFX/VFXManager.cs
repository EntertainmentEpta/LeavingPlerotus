using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Identificador de tipo para todos os efeitos visuais (VFX) do jogo.
/// Permite disparar efeitos sem que os componentes (BossController, Player, etc.) precisem manter
/// dezenas de referências no Inspector.
/// </summary>
public enum VFXType
{
    CocoonLeavesBurst,      // Explosão de folhas na saída do Casulo (CFXR3 Hit Leaves A Lit)
    BossPunchImpact,        // Impacto do soco do Boss (CFXR Hit D 3D Yellow / Red)
    BossSwipeImpact,        // Impacto do corte/garra do Boss (CFXR4 Sword Hit Cross)
    BossStompShockwave,     // Onda de choque do pisão frontal
    BossJumpShockwave,      // Impacto radial do salto destruidor
    PlayerAxeGroundSlam,    // Impacto do Ultimate do Machado
    ImpactFrame,            // Easy Impact Frame oficial da Vefects (VFX_Impact_Frame_01)
    ImpactEyeCandy,         // Eye Candy / Faíscas estilizadas da Vefects (VFX_Extra_Eye_Candy_01)
    WWExplosionVariant1,    // CFXR2 WW Explosion 1 (Variação Semi-Estilizada)
    PlayerDash,             // VFX de partículas do Dash do Player (speedlines/burst)
    PlayerDashEnhanced,     // VFX de dash melhorado (efeito T4 SharpItem4 — trail enhanced)
    DashExplosion,          // Explosão AoE no final do dash (efeito T4 SharpItem4)
    Custom                  // VFX avulso/customizado
}

/// <summary>
/// Gerenciador Centralizado de VFX com Object Pooling integrado.
/// Mantém o Inspector de todos os scripts limpo e evita GC allocations em tempo real.
/// </summary>
[DisallowMultipleComponent]
public class VFXManager : MonoBehaviour
{
    private static VFXManager _instance;
    public static VFXManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<VFXManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[VFX_Manager]");
                    _instance = go.AddComponent<VFXManager>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class VFXEntry
    {
        public VFXType type;
        public GameObject prefab;
        [Range(1, 20)] public int poolSize = 3;
    }

    [Header("📋 Registro de Efeitos Visuais (VFX Registry)")]
    [Tooltip("Lista de mapeamentos de tipos de VFX para seus respectivos prefabs.")]
    [SerializeField] private List<VFXEntry> vfxRegistry = new List<VFXEntry>();

    [Header("⚡ Ajustes de Velocidade e Duração")]
    [Tooltip("Velocidade de reprodução do Impact Frame (valores maiores deixam o flash mais rápido e seco, estilo anime). Padrão recomendado: 2.5x")]
    [Range(0.5f, 6.0f)] public float impactFrameSpeed = 2.5f;

    private Dictionary<VFXType, Queue<GameObject>> poolDictionary = new Dictionary<VFXType, Queue<GameObject>>();
    private Dictionary<VFXType, GameObject> prefabMap = new Dictionary<VFXType, GameObject>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeDefaultEntries();
        InitializePools();
    }

    private void OnValidate()
    {
        InitializeDefaultEntries();
    }

    /// <summary>
    /// Auto-preenche as entradas padrão com os prefabs do Cartoon FX Remaster caso ainda não tenham sido configuradas.
    /// </summary>
    public void InitializeDefaultEntries()
    {
#if UNITY_EDITOR
        Dictionary<VFXType, string> defaultPaths = new Dictionary<VFXType, string>()
        {
            { VFXType.CocoonLeavesBurst, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Nature/CFXR3 Hit Leaves A (Lit).prefab" },
            { VFXType.BossPunchImpact, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab" },
            { VFXType.BossSwipeImpact, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Plain/CFXR4 Sword Hit PLAIN (Cross).prefab" },
            { VFXType.BossStompShockwave, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR2 Ground Hit.prefab" },
            { VFXType.BossJumpShockwave, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab" },
            { VFXType.PlayerAxeGroundSlam, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Impact Glowing HDR (Blue).prefab" },
            { VFXType.ImpactFrame, "Assets/Vefects/Easy Impact Frames/VFX/Impact Frames/Particles/VFX_Impact_Frame_01.prefab" },
            { VFXType.ImpactEyeCandy, "Assets/Vefects/Easy Impact Frames/VFX/Extra/Particles/VFX_Extra_Eye_Candy_01.prefab" },
            { VFXType.WWExplosionVariant1, "Assets/_Project/VFX/Texture Packs/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR2 WW Explosion 1.prefab" }
        };

        foreach (var kvp in defaultPaths)
        {
            VFXEntry existing = vfxRegistry.Find(e => e.type == kvp.Key);
            if (existing == null)
            {
                GameObject loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kvp.Value);
                if (loadedPrefab != null)
                {
                    vfxRegistry.Add(new VFXEntry { type = kvp.Key, prefab = loadedPrefab, poolSize = 3 });
                }
            }
            else if (existing.prefab == null)
            {
                existing.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kvp.Value);
            }
        }
#endif
    }

    private void InitializePools()
    {
        poolDictionary.Clear();
        prefabMap.Clear();

        foreach (VFXEntry entry in vfxRegistry)
        {
            if (entry.prefab == null) continue;

            prefabMap[entry.type] = entry.prefab;
            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < entry.poolSize; i++)
            {
                GameObject obj = Instantiate(entry.prefab, transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            poolDictionary[entry.type] = objectPool;
        }
    }

    /// <summary>
    /// Dispara um efeito visual registrado pelo seu Enum.
    /// </summary>
    public static GameObject Play(VFXType type, Vector3 position, Quaternion rotation = default, float scale = 1.0f, float customSimulationSpeed = -1f)
    {
        return Instance.PlayVFX(type, position, rotation == default ? Quaternion.identity : rotation, scale, customSimulationSpeed);
    }

    /// <summary>
    /// Dispara um efeito visual registrado na posição especificada.
    /// </summary>
    public GameObject PlayVFX(VFXType type, Vector3 position, Quaternion rotation, float scale = 1.0f, float customSimulationSpeed = -1f)
    {
        GameObject vfxInstance = GetFromPool(type);
        if (vfxInstance == null) return null;

        vfxInstance.transform.position = position;
        vfxInstance.transform.rotation = rotation;
        vfxInstance.transform.localScale = Vector3.one * (scale <= 0f ? 1.0f : scale);

        vfxInstance.SetActive(true);

        // Define a velocidade efetiva de reprodução (override específico ou global)
        float effectiveSpeed = (customSimulationSpeed > 0f)
            ? customSimulationSpeed
            : ((type == VFXType.ImpactFrame) ? impactFrameSpeed : 1.0f);

        // Reinicia sistemas de partículas e aplica velocidade configurada
        ParticleSystem[] particleSystems = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        float maxDuration = 1.5f;

        foreach (ParticleSystem ps in particleSystems)
        {
            var main = ps.main;
            if (type == VFXType.ImpactFrame || customSimulationSpeed > 0f)
            {
                main.simulationSpeed = effectiveSpeed;
            }

            ps.Clear();
            ps.Play();
            float speedDiv = effectiveSpeed > 0f ? effectiveSpeed : 1.0f;
            float dur = (main.duration + main.startLifetimeMultiplier) / speedDiv;
            if (dur > maxDuration) maxDuration = dur;
        }

        // Agenda retorno ao pool
        StartCoroutine(ReturnToPoolRoutine(type, vfxInstance, maxDuration + 0.2f));

        return vfxInstance;
    }

    private GameObject GetFromPool(VFXType type)
    {
        if (!poolDictionary.ContainsKey(type))
        {
            if (prefabMap.ContainsKey(type) && prefabMap[type] != null)
            {
                poolDictionary[type] = new Queue<GameObject>();
            }
            else
            {
                // Tenta resolver dinamicamente se o prefab existe no registro
                VFXEntry entry = vfxRegistry.Find(e => e.type == type);
                if (entry != null && entry.prefab != null)
                {
                    prefabMap[type] = entry.prefab;
                    poolDictionary[type] = new Queue<GameObject>();
                }
                else
                {
                    Debug.LogWarning($"[VFXManager] ⚠️ Efeito {type} não possui prefab atribuído no registro!");
                    return null;
                }
            }
        }

        Queue<GameObject> pool = poolDictionary[type];
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null) return obj;
        }

        // Se o pool esgotou, cria uma nova instância
        if (prefabMap.TryGetValue(type, out GameObject prefab) && prefab != null)
        {
            GameObject newObj = Instantiate(prefab, transform);
            return newObj;
        }

        return null;
    }

    private IEnumerator ReturnToPoolRoutine(VFXType type, GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (instance != null)
        {
            instance.SetActive(false);
            instance.transform.SetParent(transform);

            if (poolDictionary.TryGetValue(type, out Queue<GameObject> pool))
            {
                pool.Enqueue(instance);
            }
        }
    }

    /// <summary>
    /// Dispara o VFX para teste na frente da câmera principal ou do jogador.
    /// </summary>
    public void TestPlayVFX(VFXType type)
    {
        Vector3 spawnPos = Vector3.zero;
        Camera cam = Camera.main;
        if (cam != null)
        {
            spawnPos = cam.transform.position + cam.transform.forward * 4.0f;
        }
        else
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) spawnPos = player.transform.position + Vector3.up * 1.0f;
        }

        Play(type, spawnPos, cam != null ? cam.transform.rotation : Quaternion.identity);
        Debug.Log($"🎮 [VFXManager TEST] Disparou VFX '{type}' na posição {spawnPos}!");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VFXManager))]
public class VFXManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VFXManager manager = (VFXManager)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("🎮 Testador de VFX em Tempo Real", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Clique nos botões abaixo durante o Play Mode para testar cada efeito visual imediatamente na frente da câmera!", MessageType.Info);

        GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
        if (GUILayout.Button("⚡ TESTAR: Easy Impact Frame (Vefects)", GUILayout.Height(32)))
        {
            manager.TestPlayVFX(VFXType.ImpactFrame);
        }

        GUI.backgroundColor = new Color(1f, 0.55f, 0.2f);
        if (GUILayout.Button("💣 TESTAR: CFXR2 WW Explosion 1 (Semi-Estilizado)", GUILayout.Height(30)))
        {
            manager.TestPlayVFX(VFXType.WWExplosionVariant1);
        }

        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button("🍃 TESTAR: Explosão de Folhas do Casulo (Hit Leaves A)", GUILayout.Height(28)))
        {
            manager.TestPlayVFX(VFXType.CocoonLeavesBurst);
        }

        GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
        if (GUILayout.Button("🥊 TESTAR: Impacto do Soco do Boss (Hit D 3D)", GUILayout.Height(28)))
        {
            manager.TestPlayVFX(VFXType.BossPunchImpact);
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("💥 TESTAR: Salto Destruidor do Boss (Explosion 1)", GUILayout.Height(28)))
        {
            manager.TestPlayVFX(VFXType.BossJumpShockwave);
        }

        GUI.backgroundColor = new Color(0.7f, 0.5f, 1f);
        if (GUILayout.Button("🪓 TESTAR: Impacto do Machado do Player (Glowing HDR)", GUILayout.Height(28)))
        {
            manager.TestPlayVFX(VFXType.PlayerAxeGroundSlam);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif

