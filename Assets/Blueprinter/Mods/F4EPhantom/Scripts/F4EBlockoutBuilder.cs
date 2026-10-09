using System.Collections.Generic;
using UnityEngine;

namespace F4EPhantom
{
    /// <summary>Creates an original, editable F-4E silhouette from simple meshes.</summary>
    [ExecuteAlways]
    public sealed class F4EBlockoutBuilder : MonoBehaviour
    {
        [SerializeField] private Material airframeMaterial;
        [SerializeField] private Material canopyMaterial;

        [ContextMenu("Build F-4E Blockout")]
        public void Build()
        {
            ClearGeneratedParts();

            CreateTube("Fuselage", new[]
            {
                new Ring(-9.60f, 0.00f, 0.06f), new Ring(-8.80f, 0.00f, 0.62f),
                new Ring(-6.80f, 0.00f, 1.08f), new Ring(-1.00f, 0.00f, 1.28f),
                new Ring(4.90f, 0.00f, 1.12f), new Ring(8.10f, 0.00f, 0.72f),
                new Ring(9.60f, 0.00f, 0.20f)
            }, 12, airframeMaterial);

            CreatePrism("Wing_L", new[]
            {
                new Vector3(-3.5f, 0.05f, 0.15f), new Vector3(1.8f, 0.05f, 0.22f),
                new Vector3(3.5f, 0.05f, 5.85f), new Vector3(0.9f, 0.05f, 5.85f)
            }, 0.10f, airframeMaterial);
            CreatePrism("Wing_R", new[]
            {
                new Vector3(-3.5f, 0.05f, -0.15f), new Vector3(1.8f, 0.05f, -0.22f),
                new Vector3(3.5f, 0.05f, -5.85f), new Vector3(0.9f, 0.05f, -5.85f)
            }, 0.10f, airframeMaterial);

            CreatePrism("Stabilator_L", new[]
            {
                new Vector3(5.00f, 0.42f, 0.12f), new Vector3(7.65f, 0.42f, 0.10f),
                new Vector3(7.10f, 0.42f, 2.25f), new Vector3(5.60f, 0.42f, 2.10f)
            }, 0.06f, airframeMaterial);
            CreatePrism("Stabilator_R", new[]
            {
                new Vector3(5.00f, 0.42f, -0.12f), new Vector3(7.65f, 0.42f, -0.10f),
                new Vector3(7.10f, 0.42f, -2.25f), new Vector3(5.60f, 0.42f, -2.10f)
            }, 0.06f, airframeMaterial);

            CreatePrism("VerticalTail", new[]
            {
                new Vector3(5.70f, 0.75f, 0.00f), new Vector3(8.30f, 0.88f, 0.00f),
                new Vector3(7.45f, 4.25f, 0.00f), new Vector3(6.05f, 4.20f, 0.00f)
            }, 0.10f, airframeMaterial);

            CreateTube("Intake_L", new[]
            {
                new Ring(-2.25f, -0.10f, 0.48f), new Ring(-1.20f, -0.12f, 0.72f),
                new Ring(0.90f, -0.10f, 0.62f)
            }, 8, airframeMaterial, new Vector3(0f, 0f, 1.12f));
            CreateTube("Intake_R", new[]
            {
                new Ring(-2.25f, -0.10f, 0.48f), new Ring(-1.20f, -0.12f, 0.72f),
                new Ring(0.90f, -0.10f, 0.62f)
            }, 8, airframeMaterial, new Vector3(0f, 0f, -1.12f));

            CreateTube("Canopy", new[]
            {
                new Ring(-3.40f, 0.72f, 0.50f), new Ring(-1.20f, 1.08f, 0.65f),
                new Ring(1.10f, 0.96f, 0.54f), new Ring(2.15f, 0.66f, 0.32f)
            }, 10, canopyMaterial);

            CreateTube("Nozzle_L", new[]
            {
                new Ring(7.75f, -0.35f, 0.38f), new Ring(9.70f, -0.35f, 0.56f)
            }, 10, airframeMaterial, new Vector3(0f, 0f, 0.48f));
            CreateTube("Nozzle_R", new[]
            {
                new Ring(7.75f, -0.35f, 0.38f), new Ring(9.70f, -0.35f, 0.56f)
            }, 10, airframeMaterial, new Vector3(0f, 0f, -0.48f));
        }

        private void ClearGeneratedParts()
        {
            var generated = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("F4E_")) generated.Add(child);
            }

            foreach (var child in generated)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private void CreateTube(string partName, Ring[] rings, int segments, Material material, Vector3 offset = default)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                for (var segment = 0; segment < segments; segment++)
                {
                    var angle = segment * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(rings[ringIndex].x, rings[ringIndex].y + Mathf.Sin(angle) * rings[ringIndex].radius,
                        Mathf.Cos(angle) * rings[ringIndex].radius) + offset);
                }
            }

            for (var ring = 0; ring < rings.Length - 1; ring++)
            {
                for (var segment = 0; segment < segments; segment++)
                {
                    var next = (segment + 1) % segments;
                    var a = ring * segments + segment;
                    var b = ring * segments + next;
                    var c = (ring + 1) * segments + segment;
                    var d = (ring + 1) * segments + next;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            CreateMeshPart(partName, vertices, triangles, material);
        }

        private void CreatePrism(string partName, Vector3[] outline, float thickness, Material material)
        {
            var vertices = new List<Vector3>();
            for (var side = -1; side <= 1; side += 2)
            {
                foreach (var point in outline) vertices.Add(point + Vector3.up * thickness * side);
            }

            var triangles = new List<int> { 0, 1, 2, 0, 2, 3, 7, 6, 5, 7, 5, 4 };
            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                triangles.AddRange(new[] { i, 4 + i, next, next, 4 + i, 4 + next });
            }

            CreateMeshPart(partName, vertices, triangles, material);
        }

        private void CreateMeshPart(string partName, List<Vector3> vertices, List<int> triangles, Material material)
        {
            var part = new GameObject("F4E_" + partName);
            part.transform.SetParent(transform, false);
            var mesh = new Mesh { name = part.name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private readonly struct Ring
        {
            public readonly float x;
            public readonly float y;
            public readonly float radius;

            public Ring(float x, float y, float radius)
            {
                this.x = x;
                this.y = y;
                this.radius = radius;
            }
        }
    }
}
