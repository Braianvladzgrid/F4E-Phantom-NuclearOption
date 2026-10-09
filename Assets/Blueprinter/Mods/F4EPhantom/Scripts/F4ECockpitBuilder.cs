using System.Collections.Generic;
using UnityEngine;

namespace F4EPhantom
{
    [ExecuteAlways]
    public sealed class F4ECockpitBuilder : MonoBehaviour
    {
        [SerializeField] private Material cockpitMaterial;
        [SerializeField] private Material instrumentMaterial;
        [SerializeField] private Material glassMaterial;

        [ContextMenu("Build F-4E Cockpit")]
        public void Build()
        {
            ClearGeneratedParts();
            CreateSeat("PilotSeat", new Vector3(-1.55f, 0.74f, 0f));
            CreateSeat("WSOSeat", new Vector3(0.45f, 0.77f, 0f));
            CreatePanel("PilotPanel", new Vector3(-2.32f, 1.20f, 0f), new Vector3(0.10f, -18f, 0f), new Vector3(0.08f, 0.55f, 1.15f));
            CreatePanel("WSOPanel", new Vector3(0.98f, 1.17f, 0f), new Vector3(0.10f, 18f, 0f), new Vector3(0.08f, 0.62f, 1.08f));
            CreateHudCombiner();
            CreateCanopyFrames();
        }

        private void ClearGeneratedParts()
        {
            var children = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("F4E_Cockpit_"))
                    children.Add(child);
            }

            foreach (var child in children)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        private void CreateSeat(string name, Vector3 position)
        {
            var seat = CreatePart(name, position, Vector3.zero);
            CreateCube(seat.transform, "Cushion", new Vector3(0f, 0.08f, 0f), new Vector3(0.46f, 0.18f, 0.52f), cockpitMaterial);
            CreateCube(seat.transform, "Back", new Vector3(0.15f, 0.43f, 0f), new Vector3(0.22f, 0.62f, 0.48f), cockpitMaterial);
            CreateCube(seat.transform, "HeadBox", new Vector3(0.30f, 0.82f, 0f), new Vector3(0.18f, 0.22f, 0.34f), cockpitMaterial);
            CreateCube(seat.transform, "Harness", new Vector3(-0.10f, 0.24f, 0f), new Vector3(0.05f, 0.28f, 0.38f), instrumentMaterial);
        }

        private void CreatePanel(string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            var panel = CreatePart(name, position, rotation);
            CreateCube(panel.transform, "PanelBody", Vector3.zero, scale, cockpitMaterial);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var y = -0.17f + row * 0.17f;
                    var z = -0.35f + column * 0.35f;
                    CreateCylinder(panel.transform, "Gauge", new Vector3(-0.048f, y, z), new Vector3(90f, 0f, 0f), 0.075f, 0.018f, instrumentMaterial);
                }
            }
        }

        private void CreateHudCombiner()
        {
            var hud = CreatePart("HUD", new Vector3(-2.78f, 1.56f, 0f), new Vector3(0f, 0f, 90f));
            CreateCube(hud.transform, "Glass", Vector3.zero, new Vector3(0.34f, 0.46f, 0.014f), glassMaterial);
            CreateCube(hud.transform, "Projector", new Vector3(-0.13f, -0.18f, 0f), new Vector3(0.16f, 0.14f, 0.18f), cockpitMaterial);
        }

        private void CreateCanopyFrames()
        {
            var canopy = CreatePart("CanopyFrames", new Vector3(-0.55f, 1.42f, 0f), Vector3.zero);
            for (var i = 0; i < 5; i++)
            {
                CreateCube(canopy.transform, "Frame", new Vector3(-1.85f + i * 0.82f, 0f, 0f), new Vector3(0f, 0f, -22f), new Vector3(0.05f, 0.82f, 1.16f), cockpitMaterial);
            }
        }

        private GameObject CreatePart(string name, Vector3 position, Vector3 rotation)
        {
            var part = new GameObject("F4E_Cockpit_" + name);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation;
            return part;
        }

        private static void CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateCube(Transform parent, string name, Vector3 position, Vector3 rotation, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateCylinder(Transform parent, string name, Vector3 position, Vector3 rotation, float radius, float height, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation;
            part.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
