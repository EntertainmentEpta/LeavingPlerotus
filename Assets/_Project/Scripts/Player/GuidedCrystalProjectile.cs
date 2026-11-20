using UnityEngine;

public sealed class GuidedCrystalProjectile : MonoBehaviour
{
    private const float Speed = 15f;
    private const float LaunchArcDuration = 0.22f;
    private const float ArmingDelay = 0.16f;
    private const float Lifetime = 4f;
    private const float DetectionRadius = 30f;

    private int damage;
    private Transform sourceRoot;
    private Transform fallbackTarget;
    private DummyHealth dummyTarget;
    private ShardSwarmHealth swarmTarget;
    private float nextTargetScanTime;
    private float elapsedTime;
    private Vector3 currentDirection;

    public static void Spawn(Vector3 position, Transform sourceRoot, int damage, Vector3 initialDirection = default, Transform preferredTarget = null)
    {
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = "GuidedCrystalProjectile";
        crystal.transform.position = position;
        crystal.transform.localScale = new Vector3(0.22f, 0.22f, 0.45f);

        Color crystalCyan = new Color(0.2f, 0.92f, 1f, 1f);
        Renderer renderer = crystal.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");
            if (shader != null)
            {
                renderer.material = new Material(shader);
            }
            renderer.material.color = crystalCyan;
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", crystalCyan);
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", crystalCyan * 2.2f);
            }
        }

        // Rastro luminoso para garantir alta visibilidade mesmo em curta distância
        TrailRenderer trail = crystal.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0.0f;
        trail.minVertexDistance = 0.04f;
        Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (trailShader != null)
            trail.material = new Material(trailShader);
        trail.startColor = crystalCyan;
        trail.endColor = new Color(0.2f, 0.92f, 1f, 0f);

        SphereCollider collider = crystal.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.65f;

        Rigidbody rigidbody = crystal.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        GuidedCrystalProjectile projectile = crystal.AddComponent<GuidedCrystalProjectile>();
        projectile.sourceRoot = sourceRoot;
        projectile.damage = Mathf.Max(1, damage);
        projectile.fallbackTarget = preferredTarget;
        projectile.currentDirection = initialDirection.sqrMagnitude > 0.001f
            ? initialDirection.normalized
            : (sourceRoot != null ? sourceRoot.forward : Vector3.forward);
        crystal.transform.forward = projectile.currentDirection;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time >= nextTargetScanTime)
        {
            FindTarget();
            nextTargetScanTime = Time.time + 0.08f;
        }

        Vector3 targetPosition = GetTargetPosition();
        if (targetPosition != Vector3.zero)
        {
            Vector3 desiredDir = (targetPosition - transform.position).normalized;
            float turnFactor = elapsedTime < LaunchArcDuration ? 4.5f : 18f;
            currentDirection = Vector3.Slerp(currentDirection, desiredDir, turnFactor * Time.deltaTime).normalized;
        }

        float currentSpeed = elapsedTime < LaunchArcDuration ? Speed * 0.7f : Speed;
        transform.position += currentDirection * currentSpeed * Time.deltaTime;
        if (currentDirection.sqrMagnitude > 0.001f)
            transform.forward = currentDirection;

        // Checagem por proximidade após armar (garante acerto mesmo se já estiver dentro do trigger do inimigo)
        if (elapsedTime >= ArmingDelay && targetPosition != Vector3.zero)
        {
            if ((targetPosition - transform.position).sqrMagnitude <= 0.45f * 0.45f)
            {
                if (dummyTarget != null && dummyTarget.CurrentHealth > 0)
                {
                    dummyTarget.TakeDamage(damage);
                    Destroy(gameObject);
                    return;
                }
                if (swarmTarget != null && swarmTarget.CurrentHealth > 0)
                {
                    swarmTarget.TakeDamage(damage);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    private void FindTarget()
    {
        dummyTarget = null;
        swarmTarget = null;
        float nearestDistanceSquared = DetectionRadius * DetectionRadius;

        foreach (DummyHealth candidate in FindObjectsByType<DummyHealth>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.CurrentHealth <= 0 || IsSource(candidate.transform)) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                dummyTarget = candidate;
            }
        }

        foreach (ShardSwarmHealth candidate in FindObjectsByType<ShardSwarmHealth>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.CurrentHealth <= 0 || IsSource(candidate.transform)) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                dummyTarget = null;
                swarmTarget = candidate;
            }
        }

        if (dummyTarget == null && swarmTarget == null && fallbackTarget != null)
        {
            DummyHealth fbDummy = fallbackTarget.GetComponent<DummyHealth>() ?? fallbackTarget.GetComponentInParent<DummyHealth>();
            if (fbDummy != null && fbDummy.CurrentHealth > 0) dummyTarget = fbDummy;
        }
    }

    private bool IsSource(Transform candidate)
    {
        return sourceRoot != null && candidate.root == sourceRoot;
    }

    private Vector3 GetTargetPosition()
    {
        if (dummyTarget != null) return dummyTarget.transform.position + Vector3.up * 0.8f;
        if (swarmTarget != null) return swarmTarget.transform.position + Vector3.up * 0.8f;
        return Vector3.zero;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (elapsedTime < ArmingDelay) return;
        if (other == null || IsSource(other.transform)) return;

        DummyHealth target = other.GetComponent<DummyHealth>() ?? other.GetComponentInParent<DummyHealth>();
        if (target != null && target.CurrentHealth > 0)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        ShardSwarmHealth swarm = other.GetComponent<ShardSwarmHealth>() ?? other.GetComponentInParent<ShardSwarmHealth>();
        if (swarm != null && swarm.CurrentHealth > 0)
        {
            swarm.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
