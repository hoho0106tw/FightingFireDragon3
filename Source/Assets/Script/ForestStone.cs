using UnityEngine;

// ??????????????????????? Projectile ???
public static class ForestStone
{
    private static Mesh stoneMesh;
    private static Mesh arrowDiamond;

    public static Mesh SharedMesh => stoneMesh != null ? stoneMesh : stoneMesh = ForestProps.CreateStone();

    public static GameObject CreateVisual(Vector3 origin, Quaternion rotation, Material material, bool arrow)
    {
        var root = new GameObject(arrow ? "Flying arrow" : "Thrown stone");
        root.transform.SetPositionAndRotation(origin, rotation);
        var visual = new GameObject(arrow ? "Arrow shaft and fletching" : "Stone mesh").transform;
        visual.SetParent(root.transform, false);
        if (arrow)
        {
            visual.localPosition = Vector3.back * .96f;
            AddArrowVisual(visual, material);
        }
        else
        {
            visual.localScale = Vector3.one * .16f;
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = SharedMesh;
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        return root;
    }

    public static void AddArrowVisual(Transform parent, Material material)
    {
        var shaft = new GameObject("Wooden shaft").transform;
        shaft.SetParent(parent, false);
        LineRenderer line = shaft.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = material;
        line.generateLightingData = true;
        line.widthMultiplier = .026f;
        line.positionCount = 2;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, Vector3.forward * .87f);
        Tint(line, new Color(.53f, .33f, .16f));

        if (arrowDiamond == null)
            arrowDiamond = ForestProps.CreateDiamond();
        ArrowPart(parent, material, "Metal arrowhead", new Vector3(0f, 0f, .876f),
            new Vector3(.10f, .032f, .16f), new Color(.80f, .82f, .76f));
        ArrowPart(parent, material, "Arrow fletching", new Vector3(0f, 0f, .123f),
            new Vector3(.112f, .021f, .21f), new Color(.75f, .66f, .47f));
        Transform vane = ArrowPart(parent, material, "Arrow fletching", new Vector3(0f, 0f, .123f),
            new Vector3(.112f, .021f, .21f), new Color(.75f, .66f, .47f));
        vane.localRotation = Quaternion.Euler(0f, 0f, 90f) * vane.localRotation;
    }

    private static Transform ArrowPart(Transform parent, Material material, string label, Vector3 point, Vector3 scale, Color tint)
    {
        Transform part = ForestHunterDetail.AddShape(parent, label, arrowDiamond, material, point, scale);
        Tint(part.GetComponent<MeshRenderer>(), tint);
        return part;
    }

    private static void Tint(Renderer renderer, Color tint)
    {
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", tint);
        renderer.SetPropertyBlock(properties);
    }
}
