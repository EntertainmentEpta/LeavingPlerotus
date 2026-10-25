using UnityEngine;

public class NextLevelTrigger : MonoBehaviour
{
    private bool isLoading = false;

    private void OnTriggerEnter(Collider other)
    {
        // Se o jogador (que tem a tag "Player") tocar o trigger
        if (other.CompareTag("Player") && !isLoading)
        {
            isLoading = true;
            Debug.Log("Fim do nível alcançado! Gerando novo mapa...");

            // Desativa o collider para evitar cliques duplos
            GetComponent<Collider>().enabled = false; 

            // Avança o round e carrega o próximo nível (NÃO reseta a run)
            GameManager.EnsureInstance().LoadNextLevel();
        }
    }
}