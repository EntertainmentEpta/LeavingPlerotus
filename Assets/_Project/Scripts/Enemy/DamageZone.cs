using UnityEngine;
using System.Collections;

public class DamageZone : MonoBehaviour
{
    [Header("Configurações de Dano")]
    public int damageAmount = 20;
    public float damageInterval = 1.0f;

    // --- NOVA OPÇÃO ---
    [Tooltip("Se marcado, o objeto se destrói assim que encostar no jogador (estilo Kamikaze).")]
    public bool destroyOnImpact = false; 

    [Header("Referências Visuais")]
    public PulseVisualizer pulseVisualizer;

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            
            if (playerHealth != null)
            {
                // Causa o dano imediatamente
                playerHealth.TakeDamage(damageAmount, gameObject);

                // Ativa o visual do pulso/explosão
                if (pulseVisualizer != null)
                {
                    pulseVisualizer.TriggerPulse();
                    // Se for destruir, precisamos desanexar o visual para ele não sumir junto instantaneamente
                    if (destroyOnImpact)
                    {
                        pulseVisualizer.transform.SetParent(null); 
                        Destroy(pulseVisualizer.gameObject, 1f); // Destrói o visual depois de 1s
                    }
                }

                // --- LÓGICA DE KAMIKAZE ---
                if (destroyOnImpact)
                {
                    Debug.Log(gameObject.name + " explodiu no jogador!");
                    Destroy(gameObject); // Destrói a caveira
                }
                else
                {
                    // Se não for kamikaze, inicia o dano contínuo (comportamento antigo)
                    StartCoroutine(DealDamageOverTime(playerHealth));
                }
            }
        }
    }

    // O OnTriggerExit não é mais tão necessário se for destroyOnImpact, mas mantemos para compatibilidade
    private void OnTriggerExit(Collider other)
    {
        if (!enabled) return;

        if (!destroyOnImpact && other.CompareTag("Player"))
        {
            StopAllCoroutines();
        }
    }

    private IEnumerator DealDamageOverTime(PlayerHealth player)
    {
        // Espera o intervalo, pois o primeiro dano já foi dado no Enter
        yield return new WaitForSeconds(damageInterval);

        while (player != null && player.gameObject.activeSelf)
        {
            player.TakeDamage(damageAmount, gameObject);
            
            if (pulseVisualizer != null)
            {
                pulseVisualizer.TriggerPulse();
            }
            
            yield return new WaitForSeconds(damageInterval);
        }
    }
}