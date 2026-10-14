using UnityEngine;
using System.Collections.Generic;

// Todos os passos do tutorial da Base e Onboarding em ordem
public enum TutorialStep
{
    PegarAdaga,             // 0: Pegar a Adaga de Plasma no chão do lobby
    FalarComRobozinho,      // 1: Interagir com o Robozinho (Mesa de Trabalho)
    AbrirAbaArmas,          // 2: Clicar na aba de ARMAS no Robozinho
    ForjarArma,             // 3: Forjar um armamento (Machado de Cristal ou Pistola)
    EquiparNoRack,          // 4: Ir até o Weapon Rack e empunhar a arma
    ColetarItemNaRun,       // 5: Entrar na run e coletar itens
    Concluido               // 6: Tutorial concluído
}

public class TutorialManager : MonoBehaviour
{
    private static TutorialManager _instance;
    public static TutorialManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TutorialManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TutorialManager_Auto");
                    _instance = go.AddComponent<TutorialManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Progresso Atual")]
    public TutorialStep currentStep = TutorialStep.PegarAdaga;

    // Um evento (aviso) disparado toda vez que o passo muda
    public delegate void OnStepChanged(TutorialStep newStep);
    public event OnStepChanged stepChangedEvent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInit()
    {
        if (_instance == null)
        {
            var inst = Instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            int saved = SaveManager.instance.CachedData.tutorialStep;
            currentStep = (TutorialStep)Mathf.Clamp(saved, 0, (int)TutorialStep.Concluido);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Avanço idempotente. Só avança se o tutorial estiver exatamente no passo esperado,
    /// prevenindo pulos acidentais caso múltiplos eventos disparem juntos.
    /// </summary>
    public bool CompleteStep(TutorialStep expected)
    {
        if (currentStep != expected || currentStep == TutorialStep.Concluido) return false;

        currentStep = expected + 1;
        if (SaveManager.instance != null)
        {
            SaveManager.instance.CachedData.tutorialStep = (int)currentStep;
            SaveManager.instance.SavePersistentData();
        }

        Debug.Log($"[TutorialManager] Passo concluído! Avançando para: {currentStep}");
        stepChangedEvent?.Invoke(currentStep);
        return true;
    }

    /// <summary>
    /// Compatibilidade retroativa. Avança o passo atual diretamente.
    /// </summary>
    public void CompleteCurrentStep() => CompleteStep(currentStep);
}
