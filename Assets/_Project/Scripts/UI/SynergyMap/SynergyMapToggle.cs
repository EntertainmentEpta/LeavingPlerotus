using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador simples para abrir/fechar o Mapa de Sinergias com uma tecla.
/// Coloque este script no próprio SynergyMapCanvas.
/// </summary>
public class SynergyMapToggle : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Tecla para abrir/fechar o mapa de sinergias.")]
    [SerializeField] private Key toggleKey = Key.M;

    [Tooltip("O GameObject raiz do mapa (geralmente este mesmo objeto). " +
             "Se deixar vazio, usa o próprio GameObject.")]
    [SerializeField] private GameObject mapRoot;

    private bool _isOpen;

    private void Awake()
    {
        Debug.Log("[SYNERGY MAP] SynergyMapToggle AWAKE - script está rodando!");

        if (mapRoot == null)
            mapRoot = gameObject;

        // Começa fechado
        SetMapActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            Debug.LogWarning("[SYNERGY MAP] Keyboard.current é NULL!");
            return;
        }

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            _isOpen = !_isOpen;
            Debug.Log($"[SYNERGY MAP] Tecla pressionada! Mapa agora: {(_isOpen ? "ABERTO" : "FECHADO")}");
            SetMapActive(_isOpen);
        }
    }

    /// <summary>
    /// Abre ou fecha o mapa. Pode ser chamado por botão de UI também.
    /// </summary>
    public void Toggle()
    {
        _isOpen = !_isOpen;
        SetMapActive(_isOpen);
    }

    private void SetMapActive(bool active)
    {
        _isOpen = active;

        int childCount = mapRoot.transform.childCount;
        Debug.Log($"[SYNERGY MAP] SetMapActive({active}) — {childCount} filhos encontrados.");

        for (int i = 0; i < childCount; i++)
        {
            mapRoot.transform.GetChild(i).gameObject.SetActive(active);
        }
    }
}
