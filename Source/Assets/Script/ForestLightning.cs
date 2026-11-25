using UnityEngine;

public sealed class ForestLightning : MonoBehaviour
{
    private const int PointsPerArc = 8;
    private readonly Vector3[] path = new Vector3[PointsPerArc];
    private LineRenderer[] cores;
    private LineRenderer[] halos;
    private Material coreMaterial;
    private Material haloMaterial;
    private float radius;
    private float height;
    private float interval;
    private float nextFlash;
    private float expiresAt = float.PositiveInfinity;

    public void Configure(float ringRadius, float arcHeight, float refreshSeconds, int arcs = 4, int sortingOrder = 0)
    {
        radius = ringRadius;
        height = arcHeight;
        interval = refreshSeconds;
        Shader shader = Resources.Load<Shader>("Firelight/LightningGlow");
        if (shader == null)
            return;
        coreMaterial = new Material(shader);
        coreMaterial.SetColor("_BaseColor", new Color(.53f, .81f, 1f, .9f));
        haloMaterial = new Material(shader);
        haloMaterial.SetColor("_BaseColor", new Color(.19f, .33f, 1f, .24f));
        cores = new LineRenderer[arcs];
        halos = new LineRenderer[arcs];
        for (int i = 0; i < arcs; i++)
        {
            cores[i] = MakeLine("Electric arc", coreMaterial, Mathf.Max(.023f, ringRadius * .028f));
            halos[i] = MakeLine("Electric halo", haloMaterial, Mathf.Max(.071f, ringRadius * .081f));
            cores[i].sortingOrder = sortingOrder;
            halos[i].sortingOrder = sortingOrder;
        }
        DrawArcs();
    }

    public void ExpireAfter(float seconds) => expiresAt = Time.time + seconds;

    private LineRenderer MakeLine(string label, Material material, float width)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        line.positionCount = PointsPerArc;
        line.widthMultiplier = width;
        return line;
    }

    private void LateUpdate()
    {
        if (Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }
        if (cores != null && Time.time >= nextFlash)
        {
            DrawArcs();
            nextFlash = Time.time + interval;
        }
    }

    private void DrawArcs()
    {
        for (int i = 0; i < cores.Length; i++)
        {
            float angle = i * 2f * Mathf.PI / cores.Length + Time.time * 1.9f;
            for (int j = 0; j < PointsPerArc; j++)
            {
                float progress = j / (float)(PointsPerArc - 1);
                float pulse = Mathf.PerlinNoise(i * 2.1f + j * .8f, Time.time * 15f);
                float side = radius * (.58f + pulse * .67f);
                float yaw = angle + progress * 1.48f + (j % 2 == 0 ? .16f : -.16f);
                path[j] = new Vector3(Mathf.Cos(yaw) * side,
                    height * (progress - .5f + (pulse - .5f) * .15f), Mathf.Sin(yaw) * side);
            }
            cores[i].SetPositions(path);
            halos[i].SetPositions(path);
        }
    }

    private void OnDestroy()
    {
        Destroy(coreMaterial);
        Destroy(haloMaterial);
    }
}
