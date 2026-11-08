using UnityEngine;

public sealed class AreaDamageVFX : MonoBehaviour
{
    private float elapsed;
    private float duration;
    private LineRenderer line;
    private Vector3 center;
    private float radius;

    public static void Spawn(Vector3 position, float effectRadius)
    {
        GameObject effect = new GameObject("BismutadoAreaDamageVFX");
        AreaDamageVFX vfx = effect.AddComponent<AreaDamageVFX>();
        vfx.center = position;
        vfx.radius = effectRadius;
        vfx.duration = 0.3f;
        vfx.CreateRing();
    }

    private void CreateRing()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 32;
        line.startWidth = 0.08f;
        line.endWidth = 0.02f;
        line.startColor = new Color(1f, 0.55f, 0.05f, 1f);
        line.endColor = new Color(1f, 0.1f, 0.02f, 0f);
        line.material = new Material(Shader.Find("Sprites/Default"));
        UpdateRing(0.1f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        UpdateRing(Mathf.Lerp(0.1f, 1f, progress));
        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void UpdateRing(float scale)
    {
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * radius * scale);
        }
    }
}
