using UnityEngine;

/// <summary>
/// Efeito Visual de Descarga Elétrica Orgânica no Ar (Trail de Projétil 3D).
/// Conforme os conceitos de design do jogo:
///  • Trilhas permanecem ativas por ~2.5s (2-3s) antes de se dissipar.
///  • Pisar/passar exatamente sobre a trilha aplica o status Eletrocutado (50% slow + dano/s por 3s).
///  • Colisor preciso e fino (0.35m) alinhado exatamente com o rastro elétrico visual.
/// </summary>
public class ElectricTrailVFX : MonoBehaviour
{
    public int damagePerTick = 5;
    public float lifetime = 2.5f;     // Permanece ativa por ~2-3s
    public Color trailColor = new Color(0.2f, 0.9f, 1.0f, 0.9f); // Ciano Elétrico Neon

    private LineRenderer lineRenderer;
    private Vector3[] basePositions;
    private float timer = 0f;
    private float flickerTimer = 0f;

    public AudioClip zapSoundClip;

    public static void CreateTrailSegment(Vector3 startPos, Vector3 endPos, int damage, float trailLifetime, AudioClip zapClip = null)
    {
        GameObject trailObj = new GameObject("ElectricAirTrail");
        Vector3 midPoint = (startPos + endPos) * 0.5f;
        trailObj.transform.position = midPoint;

        ElectricTrailVFX trail = trailObj.AddComponent<ElectricTrailVFX>();
        trail.damagePerTick = (damage > 0) ? damage : 5;
        trail.lifetime = (trailLifetime > 0f) ? trailLifetime : 2.5f;
        trail.zapSoundClip = zapClip;
        trail.SetupAirLine(startPos, endPos);
    }

    public void SetupAirLine(Vector3 start, Vector3 end)
    {
        // Adiciona Rigidbody Kinematic para garantir que a Física da Unity processe colisões
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;

        // Curva de largura cônica reduzida e elegante (filamento fino e afiado)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0.0f, 0.03f);
        widthCurve.AddKey(0.3f, 0.12f);
        widthCurve.AddKey(0.7f, 0.09f);
        widthCurve.AddKey(1.0f, 0.01f);
        lineRenderer.widthCurve = widthCurve;

        // 7 Vértices para criar o arco elétrico orgânico serrilhado no ar
        int points = 7;
        lineRenderer.positionCount = points;
        basePositions = new Vector3[points];

        for (int i = 0; i < points; i++)
        {
            float t = (float)i / (points - 1);
            Vector3 basePoint = Vector3.Lerp(start, end, t);

            // Serrilhado elétrico 3D sutil nas posições intermediárias
            if (i > 0 && i < points - 1)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 0.08f;
                basePoint += randomOffset;
            }

            basePositions[i] = basePoint;
            lineRenderer.SetPosition(i, basePoint);
        }

        // Shader Additive para brilho elétrico neon
        Shader addShader = Shader.Find("Mobile/Particles/Additive");
        if (addShader == null) addShader = Shader.Find("Particles/Additive");
        if (addShader == null) addShader = Shader.Find("Sprites/Default");

        Material mat = new Material(addShader);
        mat.color = trailColor;
        lineRenderer.material = mat;

        // BoxCollider fino e preciso (0.35m) alinhado exatamente ao filamento do rastro
        Vector3 segmentVec = end - start;
        float segLength = segmentVec.magnitude;
        if (segLength > 0.01f)
        {
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.35f, 2.2f, segLength);
            col.center = new Vector3(0f, -0.5f, 0f);
            trailObjRotation(start, end);
        }

        Destroy(gameObject, lifetime);
    }

    private void trailObjRotation(Vector3 start, Vector3 end)
    {
        Vector3 dir = (end - start);
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        flickerTimer += Time.deltaTime;

        // Efeito de centelha / oscilação rápida da descarga no ar (Flicker)
        if (lineRenderer != null && basePositions != null)
        {
            if (flickerTimer >= 0.05f) // Atualiza ruído a cada 50ms
            {
                flickerTimer = 0f;
                for (int i = 1; i < basePositions.Length - 1; i++)
                {
                    Vector3 jitter = Random.insideUnitSphere * 0.03f;
                    lineRenderer.SetPosition(i, basePositions[i] + jitter);
                }
            }

            // Desaparecimento suave com fade-out da descarga elétrica
            float alpha = Mathf.Clamp01(1f - (timer / lifetime)) * 0.85f;
            Color c = trailColor;
            c.a = alpha;
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TriggerStatusOnPlayer(other);
    }

    void OnTriggerStay(Collider other)
    {
        TriggerStatusOnPlayer(other);
    }

    private bool hasPlayedSoundThisContact = false;

    private void TriggerStatusOnPlayer(Collider other)
    {
        if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
        {
            int actualDamage = (damagePerTick > 0) ? damagePerTick : 5;
            ElectrocutedStatus.ApplyElectrocuted(other.gameObject, actualDamage, 0.50f, 3.0f);

            if (zapSoundClip != null && !hasPlayedSoundThisContact)
            {
                hasPlayedSoundThisContact = true;
                AudioSource.PlayClipAtPoint(zapSoundClip, transform.position, 0.8f);
            }
        }
    }
}
