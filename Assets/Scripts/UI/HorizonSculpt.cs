using System.Collections.Generic;
using UnityEngine;

namespace Horizon.UI
{
    // Small authored profiles produce reusable meshes rather than scaled default primitives.
    public static class HorizonSculpt
    {
        public static Mesh Profile(string name, Vector2[] profile, int sides = 32, bool squared = false)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++)
            {
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    if (squared)
                    {
                        x = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 0.28f);
                        z = Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z), 0.28f);
                    }
                    vertices.Add(new Vector3(x * profile[row].x, profile[row].y, z * profile[row].x));
                    uv.Add(new Vector2(side / (float)sides, row / (float)(profile.Length - 1)));
                    if (row == 0 || side == sides) continue;
                    int a = row * (sides + 1) + side, b = a - sides - 1;
                    triangles.Add(b); triangles.Add(a); triangles.Add(a + 1);
                    triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh SoftBlock()
        {
            return Profile("HORIZON soft edged block", new[] {
                new Vector2(0, -0.5f), new Vector2(0.42f, -0.5f),
                new Vector2(0.5f, -0.42f), new Vector2(0.5f, 0.42f),
                new Vector2(0.42f, 0.5f), new Vector2(0, 0.5f) }, 32, true);
        }

        public static Mesh Leaf()
        {
            // A closed, curved leaf with a raised vein; visible from both sides.
            var mesh = new Mesh { name = "HORIZON curved leaf" };
            Vector3[] vertices = {
                new Vector3(0, 0, 0), new Vector3(-0.24f, 0.28f, 0.02f),
                new Vector3(0, 0.36f, -0.1f), new Vector3(0.24f, 0.28f, 0.02f),
                new Vector3(-0.22f, 0.65f, 0.16f), new Vector3(0, 0.7f, 0.04f),
                new Vector3(0.22f, 0.65f, 0.16f), new Vector3(0, 1, 0.36f),
                new Vector3(0, 0.38f, 0.045f), new Vector3(0, 0.7f, 0.17f)
            };
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0,2,1, 0,3,2, 1,2,5, 1,5,4, 2,3,6, 2,6,5,
                4,5,7, 5,6,7, 0,1,8, 0,8,3, 1,4,9, 1,9,8, 3,8,9, 3,9,6, 4,7,9, 6,9,7 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Torus(float radius, float thickness)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            const int rings = 64, sides = 8;
            for (int ring = 0; ring <= rings; ring++)
            {
                float a = ring * Mathf.PI * 2 / rings;
                for (int side = 0; side <= sides; side++)
                {
                    float b = side * Mathf.PI * 2 / sides;
                    float r = radius + Mathf.Cos(b) * thickness;
                    vertices.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(b) * thickness));
                    if (ring == 0 || side == sides) continue;
                    int i = ring * (sides + 1) + side, j = i - sides - 1;
                    triangles.Add(j); triangles.Add(i); triangles.Add(i + 1);
                    triangles.Add(j); triangles.Add(i + 1); triangles.Add(j + 1);
                }
            }
            var mesh = new Mesh { name = "HORIZON portal rim" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
