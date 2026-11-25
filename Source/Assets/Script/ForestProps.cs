using UnityEngine;

public static class ForestProps
{
    public static Mesh CreateStone()
    {
        const int sides = 7;
        var rim = new Vector3[2 * sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * 2f * Mathf.PI / sides;
            float radius = 0.84f + Mathf.Sin(i * 8.3f) * 0.15f;
            rim[i] = new Vector3(Mathf.Sin(angle) * radius, 0.4f + Mathf.Sin(i * 3.8f) * 0.22f, Mathf.Cos(angle) * radius);
            angle += 0.23f;
            rim[sides + i] = new Vector3(Mathf.Sin(angle) * radius, -0.36f + Mathf.Sin(i * 6.7f) * 0.17f, Mathf.Cos(angle) * radius);
        }
        var vertices = new Vector3[sides * 12];
        var triangles = new int[vertices.Length];
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int index = i * 12;
            Vector3 a = rim[i], b = rim[next], c = rim[sides + i], d = rim[sides + next];
            vertices[index] = new Vector3(0.15f, 0.73f, -0.12f);
            vertices[index + 1] = a;
            vertices[index + 2] = b;
            vertices[index + 3] = a;
            vertices[index + 4] = c;
            vertices[index + 5] = b;
            vertices[index + 6] = b;
            vertices[index + 7] = c;
            vertices[index + 8] = d;
            vertices[index + 9] = new Vector3(-0.09f, -0.65f, 0.05f);
            vertices[index + 10] = d;
            vertices[index + 11] = c;
        }
        for (int i = 0; i < triangles.Length; i++)
            triangles[i] = i;
        var mesh = new Mesh { name = "Chipped river stone", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        return mesh;
    }

    public static Mesh CreateDiamond()
    {
        Vector3[] rim = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        var vertices = new Vector3[24];
        var triangles = new int[24];
        for (int side = 0; side < rim.Length; side++)
        {
            Vector3 a = rim[side];
            Vector3 b = rim[(side + 1) % rim.Length];
            int index = side * 6;
            vertices[index] = Vector3.up * 0.8f;
            vertices[index + 1] = a;
            vertices[index + 2] = b;
            vertices[index + 3] = Vector3.down * 1.1f;
            vertices[index + 4] = b;
            vertices[index + 5] = a;
        }
        for (int i = 0; i < triangles.Length; i++)
            triangles[i] = i;
        var mesh = new Mesh { name = "Faceted diamond", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        return mesh;
    }
}
