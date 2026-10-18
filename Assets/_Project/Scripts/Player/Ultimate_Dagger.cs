using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Habilidade Ultimate da Adaga.
/// Lança uma onda de corte (VFX FBX) para frente, causando dano aos inimigos no caminho.
/// </summary>
public class Ultimate_Dagger : MonoBehaviour
{
    [Header("Efeitos Visuais (VFX)")]
    [Tooltip("Arraste aqui o seu Prefab FBX do efeito da lâmina de corte.")]
    public GameObject bladeWaveVFX;
    [Tooltip("Deslocamento Y para garantir que o efeito fique rente ao chão.")]
    public float groundYOffset = 0f;
    [Tooltip("Distância à frente do player onde o efeito nasce.")]
    public float spawnForwardOffset = 1.0f;

    [Header("Combate e Dano")]
    [Tooltip("Quantidade de dano que a onda causa aos inimigos.")]
    public int waveDamage = 180;
    [Tooltip("Velocidade com que a onda de corte viaja para frente.")]
    public float waveSpeed = 15.0f;
    [Tooltip("Distância máxima que a onda viaja antes de sumir.")]
    public float waveMaxDistance = 10.0f;
    [Tooltip("Tamanho da Hitbox (Caixa de colisão) da onda. Ajuste nos Gizmos para casar com a largura do FBX.")]
    public Vector3 hitboxHalfExtents = new Vector3(1.5f, 1.0f, 0.5f);
    [Tooltip("Força com que o inimigo é empurrado para trás ao ser cortado.")]
    public float knockbackForce = 5.0f;

    // Controle interno para não dar dano múltiplo no mesmo inimigo no mesmo ataque
    private HashSet<GameObject> enemiesHitByCurrentWave = new HashSet<GameObject>();
    private bool isUltActive = false;

    /// <summary>
    /// Chamado pelo script PlayerUltimate.cs quando o jogador aperta 'U'.
    /// (A animação já está sendo disparada pelo próprio PlayerUltimate).
    /// </summary>
    public void ExecuteUltimate()
    {
        if (isUltActive) return;
        
        isUltActive = true;
        Debug.Log("[Ultimate_Dagger] Preparando para lançar a lâmina! Aguardando o Evento de Animação...");
    }

    // =========================================================================
    // EVENTOS DE ANIMAÇÃO (Animation Events)
    // O Unity vai procurar exatamente um desses nomes quando o marcador for ativado na animação!
    // =========================================================================
    public void OnDaggerUlt() => TriggerDaggerImpact();
    public void ondaggerult() => TriggerDaggerImpact();
    public void OnDaggerSlash() => TriggerDaggerImpact();
    public void PlayParticle() => TriggerDaggerImpact();

    /// <summary>
    /// Instancia o efeito visual (FBX) e inicia a viagem dele causando dano.
    /// </summary>
    public void TriggerDaggerImpact()
    {
        Debug.Log("[Ultimate_Dagger] Lançando onda de corte!");
        
        // Limpa a memória de hits para esta nova onda
        enemiesHitByCurrentWave.Clear();

        // Calcula o ponto de origem (na frente do player)
        Vector3 spawnPos = transform.position + (transform.forward * spawnForwardOffset);
        spawnPos.y += groundYOffset;

        // Instancia o VFX FBX da lâmina
        GameObject waveInstance = null;
        if (bladeWaveVFX != null)
        {
            waveInstance = Instantiate(bladeWaveVFX, spawnPos, transform.rotation);
        }
        else
        {
            Debug.LogWarning("[Ultimate_Dagger] Prefab da lâmina (bladeWaveVFX) não foi atribuído no Inspector!");
        }

        // Inicia a rotina que move a lâmina e checa o dano
        StartCoroutine(MoveAndDamageCoroutine(waveInstance, spawnPos, transform.forward));
        
        // Libera para poder ser ativado novamente no próximo Cooldown
        isUltActive = false; 
    }

    /// <summary>
    /// Rotina que move o VFX para frente frame a frame e checa as colisões.
    /// </summary>
    private IEnumerator MoveAndDamageCoroutine(GameObject waveObj, Vector3 startPos, Vector3 direction)
    {
        float distanceTraveled = 0f;
        Vector3 currentPos = startPos;

        // Enquanto não atingir a distância máxima...
        while (distanceTraveled < waveMaxDistance)
        {
            // Calcula o passo do movimento baseado no tempo real
            float moveStep = waveSpeed * Time.deltaTime;
            currentPos += direction * moveStep;
            distanceTraveled += moveStep;

            // Arrasta o objeto visual da lâmina (se ele existir)
            if (waveObj != null)
            {
                waveObj.transform.position = currentPos;
            }

            // Checa a colisão usando uma Caixa (Box) invisível
            Collider[] hitColliders = Physics.OverlapBox(currentPos, hitboxHalfExtents, transform.rotation);
            foreach (var hit in hitColliders)
            {
                // Ignora o próprio player
                if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

                // Garante que o inimigo primário não tome múltiplos danos enquanto a lâmina passa por dentro dele
                GameObject rootEnemy = hit.transform.root.gameObject;
                if (enemiesHitByCurrentWave.Contains(rootEnemy)) continue;

                bool didDamage = false;
                
                // Tenta dar dano em inimigos padrão
                DummyHealth dummy = hit.GetComponent<DummyHealth>() ?? hit.GetComponentInParent<DummyHealth>();
                if (dummy != null)
                {
                    dummy.TakeDamage(waveDamage);
                    didDamage = true;
                }

                // Tenta dar dano no enxame de cristais
                ShardSwarmHealth swarm = hit.GetComponent<ShardSwarmHealth>() ?? hit.GetComponentInParent<ShardSwarmHealth>();
                if (swarm != null)
                {
                    swarm.TakeDamage(waveDamage);
                    didDamage = true;
                }

                // Se machucou alguém, anota na lista e empurra!
                if (didDamage)
                {
                    enemiesHitByCurrentWave.Add(rootEnemy);
                    Debug.Log($"[Ultimate_Dagger] Onda cortou o inimigo: {rootEnemy.name}");

                    Rigidbody enemyRb = hit.GetComponent<Rigidbody>() ?? hit.GetComponentInParent<Rigidbody>();
                    if (enemyRb != null)
                    {
                        Vector3 pushDir = direction; // Empurra na mesma direção do corte
                        pushDir.y = 0.2f; // Leve empurrão para cima para dar impacto
                        enemyRb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
                    }
                }
            }

            yield return null; // Espera o próximo frame
        }

        // Destrói o VFX quando ele chega no final do trajeto
        if (waveObj != null)
        {
            Destroy(waveObj);
        }
    }

    // =========================================================================
    // GIZMOS PARA DEBUG NO INSPECTOR
    // =========================================================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 spawnPos = transform.position + (transform.forward * spawnForwardOffset);
        spawnPos.y += groundYOffset;
        
        // Desenha uma esfera indicando de onde a onda vai sair
        Gizmos.DrawWireSphere(spawnPos, 0.3f);

        // Desenha a linha do caminho e a Hitbox final vermelha
        Gizmos.color = Color.red;
        Vector3 endPos = spawnPos + (transform.forward * waveMaxDistance);
        Gizmos.DrawLine(spawnPos, endPos);
        
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(endPos, transform.rotation, Vector3.one);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawWireCube(Vector3.zero, hitboxHalfExtents * 2f); 
    }
}