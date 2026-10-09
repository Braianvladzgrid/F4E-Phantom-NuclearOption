using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class F4EGearIntegration
{
    internal static readonly string[] Names = { "F4E_Real_1_0", "F4E_Gear_Nose", "F4E_Gear_Left", "F4E_Gear_Right",
        "F4E_Gear_Tire_Nose", "F4E_Gear_Tire_Left", "F4E_Gear_Tire_Right" };
    // Tire bounds measured from the connected components of Kenny's source mesh 1,
    // converted with the same center and scale as F4EImportedModelBuilder.
    internal static readonly Vector3[] TireCenters = {
        new Vector3(0f, -2.357592f, 5.299778f),
        new Vector3(-2.679232f, -2.209827f, -1.792681f),
        new Vector3(2.679232f, -2.209827f, -1.792629f)
    };
    internal static readonly float[] TireRadii = { 0.215621f, 0.350732f, 0.350732f };
    internal static readonly string[] Wheels = { "wheel_F", "wheel_L", "wheel_R" };
    internal static readonly string[] GearBodies = { "gear_F", "gear_L_sprung", "gear_R_sprung" };

    internal static void AlignContacts(Transform root)
    {
        var mounts = new[] { "gearHinge_F", "gearMount_L", "gearMount_R" };
        for (var i = 0; i < Wheels.Length; i++)
        {
            var wheel = FindRequired(root, Wheels[i]);
            var mount = FindRequired(root, mounts[i]);
            if (!wheel.IsChildOf(mount)) throw new InvalidOperationException("Wheel is outside its gear assembly.");
            // Move the complete mechanism before binding the imported visual.
            // Delta alignment is idempotent and keeps native suspension offsets intact.
            mount.position += root.TransformPoint(TireCenters[i]) - wheel.position;
            var body = FindRequired(root, GearBodies[i]);
            Component gear = null;
            foreach (var component in body.GetComponents<Component>())
                if (component != null && component.GetType().Name == "LandingGear") gear = component;
            if (gear == null) throw new InvalidOperationException("Missing LandingGear: " + body.name);
            var serialized = new SerializedObject(gear);
            var radius = serialized.FindProperty("wheelRadius");
            if (radius == null) throw new InvalidOperationException("Missing wheelRadius field.");
            radius.floatValue = TireRadii[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var bumpStop = (serialized.FindProperty("bumpStop").objectReferenceValue as GameObject)?.transform;
            var unsprung = (serialized.FindProperty("unsprung").objectReferenceValue as GameObject)?.transform;
            var castPoint = serialized.FindProperty("castPoint").objectReferenceValue as Transform;
            if (bumpStop == null || unsprung == null || !wheel.IsChildOf(unsprung))
                throw new InvalidOperationException("Invalid suspension references: " + body.name);
            var travel = serialized.FindProperty("suspensionTravel").floatValue;
            if (travel <= TireRadii[i]) throw new InvalidOperationException("Suspension travel is shorter than the tire radius.");
            // LandingGear resets unsprung.position from bumpStop every frame.
            // Keep the exported wheel center equal to that unloaded runtime pose.
            var tireCenter = root.TransformPoint(TireCenters[i]);
            bumpStop.position = tireCenter + unsprung.up * (travel - TireRadii[i]);
            unsprung.position = tireCenter;
            wheel.position = tireCenter;
            if (castPoint != null) castPoint.position = bumpStop.position;
        }
    }

    internal static Transform FindRequired(Transform root, string name)
    {
        Transform result = null;
        foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name != name) continue;
            if (result != null) throw new InvalidOperationException("Duplicate gear transform: " + name);
            result = candidate;
        }
        if (result == null) throw new InvalidOperationException("Missing gear transform: " + name);
        return result;
    }

    internal static Mesh[] Split(Mesh source, Vector3 center, float scale)
    {
        var vertices = source.vertices;
        var normals = source.normals;
        var uv = source.uv;
        var indices = source.triangles;
        var tireGroups = IdentifyTires(vertices, indices);
        var groups = new List<int>[Names.Length];
        for (var i = 0; i < groups.Length; i++) groups[i] = new List<int>();
        for (var i = 0; i < indices.Length; i += 3)
        {
            var group = tireGroups[indices[i]];
            if (group == 0) group = Classify(vertices[indices[i]], center, scale);
            if (group < 4 && (group != Classify(vertices[indices[i + 1]], center, scale)
                || group != Classify(vertices[indices[i + 2]], center, scale)))
                throw new InvalidOperationException("Gear separation would cut a triangle. Source model changed.");
            groups[group].Add(indices[i]);
            groups[group].Add(indices[i + 1]);
            groups[group].Add(indices[i + 2]);
        }

        var result = new Mesh[Names.Length];
        for (var group = 0; group < groups.Length; group++)
        {
            if (groups[group].Count == 0) throw new InvalidOperationException("Missing gear mesh group.");
            var map = new Dictionary<int, int>();
            var positions = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUv = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var index in groups[group])
            {
                if (!map.TryGetValue(index, out var mapped))
                {
                    mapped = positions.Count;
                    map.Add(index, mapped);
                    positions.Add(vertices[index]);
                    newNormals.Add(normals[index]);
                    newUv.Add(uv[index]);
                }
                triangles.Add(mapped);
            }
            var mesh = new Mesh { name = Names[group] };
            mesh.SetVertices(positions);
            mesh.SetNormals(newNormals);
            mesh.SetUVs(0, newUv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            result[group] = mesh;
        }
        return result;
    }

    private static int[] IdentifyTires(Vector3[] vertices, int[] triangles)
    {
        // Weld only for connectivity analysis; the output retains the original
        // vertices, UV seams, normals and triangle winding.
        var parent = new int[vertices.Length];
        var welded = new Dictionary<Vector3Int, int>();
        for (var i = 0; i < vertices.Length; i++) parent[i] = i;
        for (var i = 0; i < vertices.Length; i++)
        {
            var key = Vector3Int.RoundToInt(vertices[i] * 100000f);
            if (welded.TryGetValue(key, out var previous)) parent[Find(parent, i)] = Find(parent, previous);
            else welded.Add(key, i);
        }
        for (var i = 0; i < triangles.Length; i += 3)
        {
            parent[Find(parent, triangles[i])] = Find(parent, triangles[i + 1]);
            parent[Find(parent, triangles[i])] = Find(parent, triangles[i + 2]);
        }
        var bounds = new Dictionary<int, Bounds>();
        for (var i = 0; i < vertices.Length; i++)
        {
            var id = Find(parent, i);
            if (!bounds.TryGetValue(id, out var box)) box = new Bounds(vertices[i], Vector3.zero);
            box.Encapsulate(vertices[i]);
            bounds[id] = box;
        }
        var assigned = new Dictionary<int, int>();
        var counts = new int[3];
        foreach (var entry in bounds)
        {
            var box = entry.Value;
            for (var tire = 0; tire < 3; tire++)
            {
                var delta = box.center - TireCenters[tire];
                var diameter = TireRadii[tire] * 2f;
                // Nose has two distinct tires; mains have one each.
                if (Mathf.Abs(delta.x) > 0.2f || Mathf.Abs(delta.y) > 0.01f || Mathf.Abs(delta.z) > 0.01f
                    || Mathf.Abs(box.size.y - diameter) > 0.01f || Mathf.Abs(box.size.z - diameter) > 0.01f) continue;
                assigned.Add(entry.Key, tire + 4);
                counts[tire]++;
            }
        }
        if (counts[0] != 2 || counts[1] != 1 || counts[2] != 1)
            throw new InvalidOperationException("Expected two nose tires and one tire per main gear; source topology changed.");
        var groups = new int[vertices.Length];
        for (var i = 0; i < vertices.Length; i++) assigned.TryGetValue(Find(parent, i), out groups[i]);
        return groups;
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }
        return index;
    }

    private static int Classify(Vector3 vertex, Vector3 center, float scale)
    {
        // Source mesh 1 contains spatially disjoint assemblies. These boundaries
        // were checked against all its triangles; reject a changed model above.
        var sourceX = vertex.x / scale + center.x;
        var sourceY = vertex.z / scale + center.y;
        return sourceY > 350f ? 1 : sourceX < -200f ? 2 : sourceX > 200f ? 3 : 0;
    }

    internal static void ClearPrevious(Transform root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child != null && child.name.StartsWith("F4E_Gear_"))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    internal static void Bind(Transform aircraftRoot, GameObject visual)
    {
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        BindOne(aircraftRoot, visual.transform, "F4E_Gear_Nose", "gear_F");
        BindOne(aircraftRoot, visual.transform, "F4E_Gear_Left", "gear_L_sprung");
        BindOne(aircraftRoot, visual.transform, "F4E_Gear_Right", "gear_R_sprung");
        for (var i = 0; i < Wheels.Length; i++)
            BindOne(aircraftRoot, visual.transform, Names[i + 4], Wheels[i]);
    }

    private static void BindOne(Transform root, Transform visual, string name, string targetName)
    {
        var part = visual.Find(name);
        Transform target = null;
        foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            if (candidate.name == targetName) target = candidate;
        if (part == null || target == null) throw new InvalidOperationException("Missing gear binding: " + name);
        part.SetParent(target, true);
        part.gameObject.layer = target.gameObject.layer;
    }
}
