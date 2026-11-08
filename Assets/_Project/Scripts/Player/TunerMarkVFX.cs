using UnityEngine;

public sealed class TunerMarkVFX : MonoBehaviour
{
    private const int PointCount = 20;
    private const float Height = 1.5f;
    private const float Radius = 0.35f;
    private LineRenderer line;

    public static TunerMarkVFX Attach(Transform target)
    {
        GameObject marker = new GameObject("TunerMarkTarget");
        marker.transform.SetParent(target, false);
        TunerMarkVFX vfx = marker.AddComponent<TunerMarkVFX>();
        vfx.CreateMarker();
        return vfx;
    }

    private void CreateMarker()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = PointCount;
        line.startWidth = 0.06f;
        line.endWidth = 0.06f;
        line.startColor = Color.red;
        line.endColor = Color.red;
        line.material = new Material(Shader.Find("Sprites/Default"));
        transform.localPosition = Vector3.up * Height;

        for (int i = 0; i < PointCount; i++)
        {
            float angle = i * Mathf.PI * 2f / PointCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius));
        }
    }
}
