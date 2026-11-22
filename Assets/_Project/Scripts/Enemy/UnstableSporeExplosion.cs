using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Objeto temporário independente instanciado quando um inimigo morre sob a Mutação "Esporos Instáveis".
/// - Exibe aviso visual pulsante no chão por ~0.8s (raio configurável, padrão 3.5m).
/// - Após o delay, detona com Physics.OverlapSphere atingindo Player e outros inimigos vivos (permitindo reação em cadeia segura).
/// - Dispara um burst visual procedural e se autodestrói em seguida.
/// </summary>
public class UnstableSporeExplosion : MonoBehaviour
{
    private float delay = 0.8f;
    private float radius = 3.5f;
    private int enemyAreaDamage = 35;
    private int playerAreaDamage = 18;

    public static void Spawn(Vector3 deathPosition, float delaySeconds, float explosionRadius, int damageToEnemies, int damageToPlayer)
    {
        Vector3 groundPos = ResolveGroundPosition(deathPosition);

        GameObject go = new GameObject("UnstableSporeExplosion");
        go.transform.position = groundPos;
        go.transform.rotation = Quaternion.identity;

        UnstableSporeExplosion spore = go.AddComponent<UnstableSporeExplosion>();
        spore.delay = Mathf.Max(0.25f, delaySeconds);
        spore.radius = Mathf.Max(1.0f, explosionRadius);
        spore.enemyAreaDamage = Mathf.Max(1, damageToEnemies);
        spore.playerAreaDamage = Mathf.Max(1, damageToPlayer);
        spore.StartCoroutine(spore.ExplosionRoutine());
    }

    private static Vector3 ResolveGroundPosition(Vector3 origin)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin + Vector3.up * 1.5f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.MinValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.isTrigger) continue;
            if (col.CompareTag("Player") || col.CompareTag("Enemy")) continue;
            if (col.GetComponentInParent<DummyHealth>() != null || col.GetComponentInParent<PlayerHealth>() != null) continue;
            if (hits[i].normal.y < 0.5f) continue;

            if (hits[i].point.y <= origin.y + 1.0f && hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                found = true;
            }
        }

        return found ? new Vector3(origin.x, bestY + 0.06f, origin.z) : origin + Vector3.up * 0.06f;
    }

    private IEnumerator ExplosionRoutine()
    {
        // 1. Cria círculo externo (borda/área total) e disco interno que expande até preencher o raio
        Color outerColor = new Color(1.0f, 0.28f, 0.10f, 0.35f);
        Color innerColor = new Color(1.0f, 0.55f, 0.12f, 0.55f);

        GameObject outerDisc = CreateFlatDisc("SporeOuterRing", radius * 2f, 0.02f, outerColor, out Material outerMat);
        GameObject innerDisc = CreateFlatDisc("SporeInnerFill", 0.2f, 0.03f, innerColor, out Material innerMat);

        // Luz de aviso sutil
        GameObject lightGo = new GameObject("SporeWarningLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        Light warnLight = lightGo.AddComponent<Light>();
        warnLight.type = LightType.Point;
        warnLight.color = new Color(1.0f, 0.42f, 0.12f, 1f);
        warnLight.range = radius * 1.8f;
        warnLight.intensity = 2.0f;

        // 2. Fase de aviso visual (0.8s)
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / delay);

            // Disco interno cresce suavemente de 10% até 100% do raio
            float fillDiameter = Mathf.Lerp(radius * 0.2f, radius * 2f, t);
            if (innerDisc != null)
            {
                innerDisc.transform.localScale = new Vector3(fillDiameter, 0.03f, fillDiameter);
            }

            // Pulso cada vez mais rápido nos instantes finais
            float blinkFreq = Mathf.Lerp(8f, 26f, t);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * blinkFreq);

            if (outerMat != null)
            {
                Color c = new Color(1.0f, Mathf.Lerp(0.32f, 0.12f, t), 0.08f, Mathf.Lerp(0.25f, 0.62f, pulse));
                SetMatColor(outerMat, c);
            }
            if (innerMat != null)
            {
                Color c = new Color(1.0f, Mathf.Lerp(0.60f, 0.22f, t), 0.10f, Mathf.Lerp(0.35f, 0.72f, t));
                SetMatColor(innerMat, c);
            }
            if (warnLight != null)
            {
                warnLight.intensity = Mathf.Lerp(1.8f, 6.5f, t) * (0.75f + 0.25f * pulse);
            }

            yield return null;
        }

        // Remove discos de aviso antes do burst
        if (outerDisc != null) Destroy(outerDisc);
        if (innerDisc != null) Destroy(innerDisc);

        // 3. Detonação: Physics.OverlapSphere atingindo Player e outros inimigos vivos
        ApplyAreaDamage();

        // 4. Burst visual procedural curto (~0.32s)
        yield return StartCoroutine(PlayBurstVisual(warnLight));

        Destroy(gameObject);
    }

    private void ApplyAreaDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<GameObject> damagedRoots = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null) continue;

            // Atingiu o Player?
            PlayerHealth player = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
            if (player != null && !player.isDead)
            {
                if (damagedRoots.Add(player.gameObject))
                {
                    player.TakeDamage(playerAreaDamage, attacker: null);
                }
                continue;
            }

            // Atingiu outro inimigo vivo (DummyHealth)?
            DummyHealth enemy = col.GetComponent<DummyHealth>() ?? col.GetComponentInParent<DummyHealth>();
            if (enemy != null && enemy.CurrentHealth > 0)
            {
                if (damagedRoots.Add(enemy.gameObject))
                {
                    enemy.TakeDamage(enemyAreaDamage, isCritical: false, isPoison: false, isProjectile: false);
                }
                continue;
            }

            // Atingiu ShardSwarm vivo?
            ShardSwarmHealth swarm = col.GetComponent<ShardSwarmHealth>() ?? col.GetComponentInParent<ShardSwarmHealth>();
            if (swarm != null && swarm.CurrentHealth > 0)
            {
                if (damagedRoots.Add(swarm.gameObject))
                {
                    swarm.TakeDamage(enemyAreaDamage, isCritical: false, isPoison: false);
                }
            }
        }
    }

    private IEnumerator PlayBurstVisual(Light flashLight)
    {
        GameObject burstSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burstSphere.name = "SporeBurstSphere";
        burstSphere.transform.SetParent(transform, false);
        burstSphere.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        Collider col = burstSphere.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Color startColor = new Color(1.0f, 0.45f, 0.10f, 0.78f);
        Material burstMat = AimSettings.CreateGroundIndicatorMaterial(startColor, "SporeBurstMat", false);
        Renderer rend = burstSphere.GetComponent<Renderer>();
        if (rend != null && burstMat != null)
        {
            rend.material = burstMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        // Partículas rápidas de esporos
        SpawnBurstParticles();

        const float burstDuration = 0.30f;
        float elapsed = 0f;
        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / burstDuration);
            float diameter = Mathf.Lerp(radius * 0.45f, radius * 2.1f, 1f - Mathf.Pow(1f - t, 2f));
            if (burstSphere != null)
            {
                burstSphere.transform.localScale = Vector3.one * diameter;
            }
            if (burstMat != null)
            {
                SetMatColor(burstMat, new Color(1.0f, Mathf.Lerp(0.45f, 0.15f, t), 0.08f, (1f - t) * 0.75f));
            }
            if (flashLight != null)
            {
                flashLight.intensity = Mathf.Lerp(9f, 0f, t);
            }
            yield return null;
        }

        if (burstSphere != null) Destroy(burstSphere);
    }

    private void SpawnBurstParticles()
    {
        GameObject psGo = new GameObject("SporeBurstParticles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 7.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
        main.startColor = new Color(1.0f, 0.48f, 0.12f, 1f);
        main.gravityModifier = 0.4f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.4f;

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                Material pMat = new Material(shader) { name = "SporeParticleMat" };
                pMat.color = Color.white;
                if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", Color.white);
                psRenderer.material = pMat;
            }
        }

        ps.Play();
    }

    private GameObject CreateFlatDisc(string discName, float diameter, float height, Color color, out Material mat)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = discName;
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(diameter, height, diameter);

        Collider col = disc.GetComponent<Collider>();
        if (col != null) Destroy(col);

        mat = AimSettings.CreateGroundIndicatorMaterial(color, discName + "_Mat", false);
        Renderer r = disc.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return disc;
    }

    private static void SetMatColor(Material mat, Color color)
    {
        if (mat == null) return;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
