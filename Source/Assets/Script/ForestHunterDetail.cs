using UnityEngine;

public static class ForestHunterDetail
{
    private static Mesh rounded;
    private static Material leather;
    private static Material bronze;
    private static Material bone;
    private static Material jade;

    public static void Apply(Transform model, Transform arm)
    {
        Material source = model.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        if (rounded == null)
        {
            rounded = CreateRoundedMesh();
            leather = Finish(source, "Dyed saddle leather", new Color(0.2f, 0.1f, 0.073f), 0.29f, 0f);
            bronze = Finish(source, "Hand-polished bronze", new Color(0.73f, 0.44f, 0.22f), 0.56f, 0.72f);
            bone = Finish(source, "Carved bone", new Color(0.86f, 0.76f, 0.58f), 0.4f, 0f);
            jade = Finish(source, "Jade inlay", new Color(0.12f, 0.54f, 0.44f), 0.65f, 0.15f);
        }

        foreach (MeshRenderer detail in arm.GetComponentsInChildren<MeshRenderer>())
        {
            if (detail.name == "Knapped slate" || detail.name == "Weathered hide" ||
                detail.name == "Oiled leather" || detail.name.EndsWith("woven accents", System.StringComparison.Ordinal))
                detail.enabled = false;
        }

        // The small inset pieces sit on the existing clothing and move with its rig.
        AddShape(arm, "Layered wrist guard", rounded, leather, new Vector3(0.18f, -0.43f, 0.248f), new Vector3(0.15f, 0.17f, 0.104f));
        AddShape(arm, "Wrist guard inlay", rounded, bronze, new Vector3(0.19f, -0.43f, 0.324f), new Vector3(0.103f, 0.115f, 0.045f));
        AddShape(arm, "Wrist stone", rounded, jade, new Vector3(0.19f, -0.43f, 0.361f), new Vector3(0.048f, 0.061f, 0.025f));

        for (int side = -1; side <= 1; side += 2)
        {
            float x = 0.31f * side;
            Transform pad = AddShape(model, "Layered shoulder guard", rounded, leather,
                new Vector3(x, 1.74f, 0.11f), new Vector3(0.23f, 0.134f, 0.252f));
            pad.localRotation = Quaternion.Euler(0f, -side * 13f, -side * 24f);
            for (int stud = 0; stud < 4; stud++)
                AddShape(model, "Shoulder rivet", rounded, bronze,
                    new Vector3(x + side * (stud - 1.5f) * 0.069f, 1.75f - (stud - 1.5f) * 0.025f, 0.329f),
                    Vector3.one * 0.026f);
        }

        AddShape(model, "Bronze clasp", rounded, bronze, new Vector3(-0.1f, 1.51f, 0.321f), new Vector3(0.137f, 0.137f, 0.064f));
        AddShape(model, "Carved clasp inset", rounded, bone, new Vector3(-0.1f, 1.51f, 0.374f), new Vector3(0.091f, 0.091f, 0.026f));
        AddShape(model, "Clasp gemstone", rounded, jade, new Vector3(-0.1f, 1.51f, 0.396f), new Vector3(0.054f, 0.067f, 0.031f));

        Transform head = model.Find("Face and headdress");
        if (head == null)
            return;
        for (int side = -1; side <= 1; side += 2)
        {
            AddShape(head, "Bronze earring", rounded, bronze, new Vector3(side * 0.278f, -0.101f, 0.084f), new Vector3(0.041f, 0.064f, 0.043f));
            AddShape(head, "Earring bead", rounded, jade, new Vector3(side * 0.277f, -0.175f, 0.098f), Vector3.one * 0.026f);
        }
    }

    public static Transform AddShape(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var shape = new GameObject(name);
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        shape.AddComponent<MeshFilter>().sharedMesh = mesh;
        shape.AddComponent<MeshRenderer>().sharedMaterial = material;
        return shape.transform;
    }

    private static Material Finish(Material source, string name, Color color, float smoothness, float metallic)
    {
        var material = new Material(source) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        return material;
    }

    private static Mesh CreateRoundedMesh()
    {
        const int rings = 12, sectors = 20;
        var vertices = new Vector3[(rings + 1) * sectors];
        var indices = new int[rings * sectors * 6];
        for (int row = 0; row <= rings; row++)
        {
            float latitude = Mathf.PI * row / rings;
            float radius = Mathf.Sin(latitude);
            for (int col = 0; col < sectors; ++col)
            {
                float angle = 2f * Mathf.PI * col / sectors;
                vertices[row * sectors + col] = new Vector3(radius * Mathf.Sin(angle), Mathf.Cos(latitude), radius * Mathf.Cos(angle));
                if (row == rings)
                    continue;
                int current = row * sectors + col;
                int next = row * sectors + (col + 1) % sectors;
                int i = current * 6;
                indices[i] = current;
                indices[i + 1] = current + sectors;
                indices[i + 2] = next;
                indices[i + 3] = next;
                indices[i + 4] = current + sectors;
                indices[i + 5] = next + sectors;
            }
        }
        return new Mesh { name = "Smooth crafted details", vertices = vertices, normals = vertices, triangles = indices };
    }
}
