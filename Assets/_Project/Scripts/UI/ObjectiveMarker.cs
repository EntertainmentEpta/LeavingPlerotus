using UnityEngine;

public class ObjectiveMarker : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Em qual passo do tutorial esta exclamação deve aparecer?")]
    public TutorialStep activeOnStep;
    
    [Tooltip("Arraste aqui o objeto visual da exclamação (o FloatingIndicator ou botão da UI)")]
    public GameObject indicatorVisual;

    private void Start()
    {
        // Inscreve no evento do Manager para saber quando o tutorial avançou
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.stepChangedEvent += CheckMyTurn;
            CheckMyTurn(TutorialManager.Instance.currentStep);
        }
    }

    private void OnDestroy()
    {
        // Limpa o evento ao destruir o objeto para evitar memory leaks
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.stepChangedEvent -= CheckMyTurn;
        }
    }

    private void CheckMyTurn(TutorialStep currentGlobalStep)
    {
        if (indicatorVisual == null) return;

        // Se o passo atual do jogo for igual ao meu passo, liga a exclamação. Se não, desliga.
        if (currentGlobalStep == activeOnStep)
        {
            indicatorVisual.SetActive(true);
        }
        else
        {
            indicatorVisual.SetActive(false);
        }
    }
}
