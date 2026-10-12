using UnityEngine;
using System.Collections.Generic;

// Todos os passos do seu tutorial em ordem
public enum TutorialStep
{
    FalarComRobozinho,
    ClicarNoBotaoDoCanvas,
    ColetarItemNaRun,
    Concluido
}

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Progresso Atual")]
    public TutorialStep currentStep = TutorialStep.FalarComRobozinho;

    // Um evento (aviso) disparado toda vez que o passo muda
    public delegate void OnStepChanged(TutorialStep newStep);
    public event OnStepChanged stepChangedEvent;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// Chama isso quando o jogador completar o que tinha que fazer (ex: após fechar o diálogo).
    /// O manager avança sozinho para o próximo passo.
    /// </summary>
    public void CompleteCurrentStep()
    {
        if (currentStep == TutorialStep.Concluido) return;

        currentStep++; // Pula pro próximo Enum
        Debug.Log($"[TutorialManager] Passo concluído! Avançando para: {currentStep}");
        
        // Avisa todos os indicadores do jogo que o passo mudou
        stepChangedEvent?.Invoke(currentStep);
    }
}
