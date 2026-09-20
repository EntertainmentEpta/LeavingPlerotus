using UnityEngine;
using System.Collections;

/// <summary>
/// VFX de partículas para o Dash do Player.
/// Dispara um efeito registrado no VFXManager (speedlines, burst de faíscas, etc.)
/// quando o player inicia o dash.
///
/// USO:
///   1. Crie/importe seu prefab de partículas de dash (speedlines, burst, etc.)
///   2. No VFXManager, adicione uma entrada com Type = PlayerDash e arraste o prefab.
///   3. Coloque este script no mesmo GameObject do Player (junto ao DashM).
///   4. Ajuste os parâmetros no Inspector conforme desejado.
///
/// ROTAÇÃO:
///   O VFX é automaticamente rotacionado para apontar na direção do dash.
///   A direção é calculada a partir do INPUT do player (mesma lógica do DashM),
///   garantindo que a rotação seja sempre exata — sem interferência de velocity
///   residual ou rotação do corpo do player.
///   Use "rotationOffset" para ajuste fino caso o prefab tenha uma orientação diferente.
///
/// FOLLOW MODE (PADRÃO: ATIVO):
///   O VFX acompanha a POSIÇÃO do player via tracking manual (sem parenting),
///   o que garante que o efeito fica "grudado" no player sem herdar a rotação dele.
///   A rotação do VFX fica TRAVADA na direção do dash — nunca distorce.
///   Quando o dash termina, a emissão de partículas para suavemente,
///   e o VFX é liberado após um delay para que partículas já emitidas
///   terminem sua animação naturalmente.
///
/// O script se conecta automaticamente ao DashM — nenhuma modificação manual necessária.
/// </summary>
public class PlayerDashVFX : MonoBehaviour
{
    [Header("VFX Settings")]
    [Tooltip("Tipo de VFX registrado no VFXManager para o dash.")]
    public VFXType dashVFXType = VFXType.PlayerDash;

    [Tooltip("Escala do efeito de partículas. Aumente para efeitos mais grandiosos.")]
    [Range(0.1f, 5.0f)]
    public float vfxScale = 1.0f;

    [Tooltip("Offset vertical do spawn do VFX em relação ao player (0 = pés, 1 = centro).")]
    public float verticalOffset = 0.3f;

    [Tooltip("Se true, o VFX é spawnado na posição INICIAL do dash (burst de saída). Se false, na posição FINAL.")]
    public bool spawnAtStart = true;

    [Tooltip("Velocidade customizada de simulação das partículas. -1 usa o padrão do VFXManager.")]
    public float customSimulationSpeed = -1f;

    [Header("Rotação do VFX")]
    [Tooltip("Offset de rotação (em graus Euler) aplicado ao VFX DEPOIS de alinhar com a direção do dash.\n" +
             "Use para corrigir a orientação do prefab.\n" +
             "Ex: Se o trail sai pra cima em vez de pra frente, coloque X = 90.")]
    public Vector3 rotationOffset = Vector3.zero;

    public enum RotationMode
    {
        DashDirection,  // Alinha o VFX na direção do dash (baseado no INPUT, mesma lógica do DashM)
        PlayerForward,  // Usa o transform.forward do player
        WorldForward    // Sem rotação (aponta pra frente do mundo)
    }

    [Tooltip("Como calcular a rotação do VFX:\n" +
             "DashDirection = alinha na direção real do dash via INPUT (recomendado)\n" +
             "PlayerForward = usa a rotação do player\n" +
             "WorldForward = sem rotação")]
    public RotationMode rotationMode = RotationMode.DashDirection;

    [Header("Follow Mode (Trail)")]
    [Tooltip("Se true, o VFX acompanha a POSIÇÃO do player durante o dash (sem parenting).\n" +
             "O efeito fica 'grudado' no player mas a rotação fica TRAVADA na direção do dash.\n" +
             "Isso evita que a rotação do corpo do player distorça o efeito.\n" +
             "Ao terminar o dash, a emissão para e o VFX é liberado suavemente.")]
    public bool followPlayer = true;

    [Tooltip("Tempo (em segundos) para liberar o VFX após o dash terminar.\n" +
             "Permite que partículas já emitidas terminem sua animação naturalmente.")]
    [Range(0f, 2f)]
    public float detachDelay = 0.5f;

    [Header("Referência (auto-detectado)")]
    [Tooltip("Referência ao DashM. Se vazio, busca automaticamente no Awake.")]
    public DashM dashScript;

    private bool wasDashing = false;
    private Rigidbody playerRb;

    // VFX Type Override (usado pelo ExplosiveDashEffect para trocar o VFX do dash dinamicamente)
    private VFXType? overrideVFXType = null;

    /// <summary>
    /// Define um override para o tipo de VFX do dash. Enquanto ativo, o dash usará este VFX em vez do padrão.
    /// Chamado pelo ExplosiveDashEffect quando o efeito T4 é ativado.
    /// </summary>
    public void SetVFXOverride(VFXType newType)
    {
        overrideVFXType = newType;
        Debug.Log($"[PlayerDashVFX] VFX Override ativado: {newType}");
    }

    /// <summary>
    /// Remove o override de VFX, voltando ao tipo padrão configurado no Inspector.
    /// Chamado pelo ExplosiveDashEffect quando o efeito T4 é desativado.
    /// </summary>
    public void ClearVFXOverride()
    {
        Debug.Log($"[PlayerDashVFX] VFX Override removido. Voltando ao padrão: {dashVFXType}");
        overrideVFXType = null;
    }

    // VFX ativo que está acompanhando o player (tracking manual, SEM parenting)
    private GameObject activeFollowVFX;
    // Flag para saber se ainda devemos atualizar a posição do VFX
    private bool isTrackingPosition = false;
    private Coroutine detachCoroutine;

    private void Awake()
    {
        if (dashScript == null)
        {
            dashScript = GetComponent<DashM>();
            if (dashScript == null)
            {
                dashScript = GetComponentInChildren<DashM>();
            }
            if (dashScript == null)
            {
                dashScript = GetComponentInParent<DashM>();
            }
        }

        if (dashScript == null)
        {
            Debug.LogWarning("[PlayerDashVFX] DashM não encontrado! O VFX de dash não funcionará.");
            enabled = false;
            return;
        }

        // Pega o Rigidbody como fallback para rotação
        playerRb = GetComponent<Rigidbody>();
        if (playerRb == null)
        {
            playerRb = GetComponentInParent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (dashScript == null) return;

        // Detecta a transição: não estava dando dash → começou a dar dash
        if (dashScript.isDashing && !wasDashing)
        {
            if (spawnAtStart)
            {
                SpawnDashVFX();
            }
        }

        // Detecta a transição: estava dando dash → parou de dar dash
        if (!dashScript.isDashing && wasDashing)
        {
            if (!spawnAtStart)
            {
                SpawnDashVFX();
            }

            // Para a emissão e libera o VFX suavemente quando o dash termina
            if (followPlayer && activeFollowVFX != null)
            {
                StopEmissionAndRelease();
            }
        }

        wasDashing = dashScript.isDashing;
    }

    /// <summary>
    /// Atualiza a posição do VFX DEPOIS de toda a movimentação do player.
    /// Roda no LateUpdate para garantir que o player já se moveu neste frame.
    /// A rotação NÃO é atualizada — fica travada na direção do dash.
    /// </summary>
    private void LateUpdate()
    {
        if (isTrackingPosition && activeFollowVFX != null)
        {
            activeFollowVFX.transform.position = transform.position + Vector3.up * verticalOffset;
            // Rotação NÃO muda — fica travada na direção exata do dash
        }
    }

    /// <summary>
    /// Calcula a rotação correta do VFX baseado no modo escolhido + offset.
    /// No modo DashDirection, usa o INPUT do player (mesma lógica do DashM.PerformDash)
    /// em vez da velocity do Rigidbody, garantindo direção exata mesmo em mudanças bruscas.
    /// </summary>
    private Quaternion GetVFXRotation()
    {
        Quaternion baseRotation;

        switch (rotationMode)
        {
            case RotationMode.DashDirection:
                // Usa o INPUT do player (mesma lógica do DashM.PerformDash)
                // Isso garante que a direção é exatamente a que o player está pressionando,
                // sem interferência de velocity residual ou rotação do corpo.
                Vector3 dashDir = new Vector3(
                    Input.GetAxisRaw("Horizontal"),
                    0f,
                    Input.GetAxisRaw("Vertical")
                ).normalized;

                // Fallback: se não tem input, tenta velocity do Rigidbody
                if (dashDir.sqrMagnitude < 0.01f && playerRb != null)
                {
                    dashDir = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z).normalized;
                }

                // Fallback final: forward do player
                if (dashDir.sqrMagnitude < 0.01f)
                {
                    dashDir = transform.forward;
                }

                baseRotation = Quaternion.LookRotation(dashDir, Vector3.up);
                break;

            case RotationMode.PlayerForward:
                baseRotation = transform.rotation;
                break;

            case RotationMode.WorldForward:
            default:
                baseRotation = Quaternion.identity;
                break;
        }

        // Aplica o offset de rotação
        return baseRotation * Quaternion.Euler(rotationOffset);
    }

    /// <summary>
    /// Spawna o VFX de dash via VFXManager (com pooling).
    /// O VFX NÃO é parenteado ao player — a posição é atualizada manualmente no LateUpdate.
    /// Isso garante que a rotação fica travada na direção do dash, independente da rotação do player.
    /// </summary>
    private void SpawnDashVFX()
    {
        if (VFXManager.Instance == null)
        {
            Debug.LogWarning("[PlayerDashVFX] VFXManager não encontrado na cena!");
            return;
        }

        // Se já existe um VFX ativo de follow, libera o anterior antes de criar um novo
        if (followPlayer && activeFollowVFX != null)
        {
            // Cancela qualquer release pendente do VFX anterior
            if (detachCoroutine != null)
            {
                StopCoroutine(detachCoroutine);
                detachCoroutine = null;
            }
            ReleaseVFX(activeFollowVFX);
            activeFollowVFX = null;
            isTrackingPosition = false;
        }

        Vector3 spawnPos = transform.position + Vector3.up * verticalOffset;
        Quaternion spawnRot = GetVFXRotation();

        // Usa o VFX override (T4 effect) se disponível, senão usa o padrão
        VFXType activeVFXType = overrideVFXType ?? dashVFXType;
        GameObject vfxObj = VFXManager.Play(activeVFXType, spawnPos, spawnRot, vfxScale, customSimulationSpeed);

        // Se follow mode está ativo, inicia o tracking manual de posição (SEM parenting)
        if (followPlayer && vfxObj != null)
        {
            activeFollowVFX = vfxObj;
            isTrackingPosition = true;

            // Posição e rotação iniciais já foram definidas pelo VFXManager.Play
            // A posição será atualizada a cada frame no LateUpdate
            // A rotação fica TRAVADA nesta direção — nunca muda
        }
    }

    /// <summary>
    /// Para a emissão de todas as partículas do VFX ativo e agenda a liberação
    /// após um delay, permitindo que partículas já emitidas terminem naturalmente.
    /// </summary>
    private void StopEmissionAndRelease()
    {
        if (activeFollowVFX == null) return;

        // Para a emissão de novas partículas (mas as já emitidas continuam)
        ParticleSystem[] particleSystems = activeFollowVFX.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particleSystems)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        // Para de atualizar a posição — o VFX fica parado onde o dash terminou
        isTrackingPosition = false;

        // Guarda referência local para o coroutine usar
        GameObject vfxToRelease = activeFollowVFX;
        activeFollowVFX = null;

        // Agenda a liberação após o delay
        detachCoroutine = StartCoroutine(DelayedRelease(vfxToRelease));
    }

    /// <summary>
    /// Aguarda um tempo e então libera o VFX, devolvendo-o ao pool do VFXManager.
    /// </summary>
    private IEnumerator DelayedRelease(GameObject vfxObj)
    {
        yield return new WaitForSeconds(detachDelay);

        if (vfxObj != null)
        {
            ReleaseVFX(vfxObj);
        }

        detachCoroutine = null;
    }

    /// <summary>
    /// Reativa a emissão do VFX (para funcionar corretamente quando reutilizado no pool)
    /// e garante que ele não está parenteado a ninguém.
    /// </summary>
    private void ReleaseVFX(GameObject vfxObj)
    {
        if (vfxObj == null) return;

        // Reativa a emissão para que o prefab funcione corretamente quando reutilizado no pool
        ParticleSystem[] particleSystems = vfxObj.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particleSystems)
        {
            var emission = ps.emission;
            emission.enabled = true;
        }

        // Garante que o VFX está sob o VFXManager (o ReturnToPoolRoutine fará o cleanup final)
        if (VFXManager.Instance != null)
        {
            vfxObj.transform.SetParent(VFXManager.Instance.transform);
        }
        else
        {
            vfxObj.transform.SetParent(null);
        }
    }
}

