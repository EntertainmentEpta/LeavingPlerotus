using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Controla o estado dos focos de fogo e brasas residuais da nave (Ship Embers).
/// No início, a nave apresenta chamas/brasas no Lobby e no Laboratório (sensação de avaria).
/// Ao comprar a melhoria "Sistema de Supressão de Incêndio" (eq_extintor_nave) na Mesa de Crafting,
/// o fogo é extinto permanentemente e o estado persiste via SaveManager / PlayerProgress.
/// </summary>
public class BaseFireExtinguisherManager : MonoBehaviour
{
    public const string UPGRADE_ID = "eq_extintor_nave";
    public const string RECIPE_ID = "recipe_extintor_nave";

    public static BaseFireExtinguisherManager Instance { get; private set; }

    [Header("Configurações")]
    [Tooltip("Se marcado, força o fogo a ficar extinto no Editor para testes.")]
    [SerializeField] private bool debugForceExtinguished = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("[BaseFireExtinguisherManager]");
            Instance = go.AddComponent<BaseFireExtinguisherManager>();
            DontDestroyOnLoad(go);
        }
        ApplyFireState();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CraftingManager.OnCraftCompleted += OnCraftCompleted;
        BaseSectorTransition.OnSectorActivated += OnSectorActivated;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CraftingManager.OnCraftCompleted -= OnCraftCompleted;
        BaseSectorTransition.OnSectorActivated -= OnSectorActivated;
    }

    void Start()
    {
        ApplyFireState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyFireState();
    }

    private void OnSectorActivated(GameObject sector)
    {
        ApplyFireState();
    }

    private void OnCraftCompleted(CraftingRecipe recipe)
    {
        if (recipe == null) return;

        bool matchesRecipe = recipe.recipeId == RECIPE_ID ||
            (recipe.resultEquipment != null && recipe.resultEquipment.equipmentId == UPGRADE_ID);

        if (matchesRecipe)
        {
            Debug.Log("[BaseFireExtinguisher] ✓ Sistema de Supressão de Incêndio adquirido! Extinguindo chamas...");
            ApplyFireState();

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso(
                    "🔥 SISTEMA TÉRMICO ATIVADO!\nFogo e brasas residuais da nave foram completamente extintos!"
                );
            }
        }
    }

    /// <summary>
    /// Retorna verdadeiro se o jogador já adquiriu a melhoria de extinção de fogo.
    /// </summary>
    public static bool IsFireExtinguished()
    {
        if (Instance != null && Instance.debugForceExtinguished) return true;

        if (SaveManager.instance == null) return false;
        return SaveManager.instance.GetAllCraftedEquipmentIds().Contains(UPGRADE_ID) ||
               SaveManager.instance.GetCraftedEquipmentCount(UPGRADE_ID) > 0;
    }

    /// <summary>
    /// Localiza todos os objetos 'Ship Embers' na cena e atualiza o estado de ativação conforme a melhoria.
    /// </summary>
    public static void ApplyFireState()
    {
        bool extinguished = IsFireExtinguished();

        // Encontra todos os Transform da cena (mesmo desativados) em cenas carregadas
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        int count = 0;

        foreach (var t in allTransforms)
        {
            if (t == null || t.gameObject == null) continue;
            // Apenas objetos pertencentes a uma cena carregada (ignora prefabs do disco)
            if (!t.gameObject.scene.isLoaded) continue;

            if (t.name == "Ship Embers")
            {
                bool shouldBeActive = !extinguished;
                if (t.gameObject.activeSelf != shouldBeActive)
                {
                    t.gameObject.SetActive(shouldBeActive);
                }

                // Se extinto, para o particle system imediatamente
                if (extinguished)
                {
                    var particles = t.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in particles)
                    {
                        if (ps != null)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }

                count++;
            }
        }

        if (count > 0)
        {
            Debug.Log($"[BaseFireExtinguisher] {count} emissor(es) 'Ship Embers' processados. Estado Extinto = {extinguished}");
        }
    }

    [ContextMenu("Testar Extinguir Fogo")]
    public void TestExtinguish()
    {
        debugForceExtinguished = true;
        ApplyFireState();
    }

    [ContextMenu("Testar Reacender Fogo")]
    public void TestReignite()
    {
        debugForceExtinguished = false;
        ApplyFireState();
    }
}
