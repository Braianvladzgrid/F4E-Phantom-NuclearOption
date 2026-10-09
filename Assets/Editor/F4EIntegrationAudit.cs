using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class F4EIntegrationAudit
{
    private const string AuditSessionKey = "F4E.GeometryAudit.v7.systems1";

    static F4EIntegrationAudit()
    {
        if (!Application.isBatchMode)
            EditorApplication.update += RunWhenReady;
    }

    private static void RunWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EditorApplication.update -= RunWhenReady;
        if (SessionState.GetBool(AuditSessionKey, false)) return;
        SessionState.SetBool(AuditSessionKey, true);
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    [MenuItem("F4E/Validate Geometry")]
    // This check deliberately stops before packaging: gameplay requires a separate in-game test.
    public static void Run()
    {
        var path = Path.GetFullPath("../../../outputs/F4EPhantom/integration-audit.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, AuditSessionKey + " started; NOT validated until a passed report replaces this text.");
        F4EPrototypeFactory.CreateGameplayMod();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(F4EImportedModelBuilder.PrefabPath);
        if (model == null) throw new InvalidOperationException("Missing imported model.");
        var filters = model.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 14) throw new InvalidOperationException("Expected fourteen meshes including independent tires.");
        var bounds = filters[0].sharedMesh.bounds;
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) throw new InvalidOperationException("Missing mesh.");
            bounds.Encapsulate(filter.sharedMesh.bounds);
        }
        if (Mathf.Abs(bounds.size.z - 19.2f) > 0.05f || bounds.size.x < 11f || bounds.size.x > 12.5f)
            throw new InvalidOperationException("Invalid airframe axes or scale: " + bounds.size);
        AssetDatabase.SaveAssets();
        var gearReport = CheckGear();
        File.WriteAllText(path, AuditSessionKey + "\nGeometry checks passed. Bounds: " + bounds.size +
            "\n" + gearReport + "\nFlight, camera clearance, gear animation and weapon release remain unverified in game.");
        Debug.Log("[F4E] Geometry audit passed: " + bounds.size);
    }

    private static string CheckGear()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Blueprinter/Mods/F4EPhantom/F4E_Phantom_II.prefab");
        var report = new StringBuilder();
        try
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) throw new InvalidOperationException("Missing script in gameplay prefab.");
                if (c.GetType().Name != "Aircraft") continue;
                var aircraft = new SerializedObject(c);
                var exterior = aircraft.FindProperty("exteriorRenderers");
                if (exterior.arraySize != 14) throw new InvalidOperationException("Wrong exterior view renderer count.");
                for (var i = 0; i < exterior.arraySize; i++)
                {
                    var renderer = exterior.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    if (renderer == null || !renderer.name.StartsWith("F4E_"))
                        throw new InvalidOperationException("Native exterior would reappear when switching cameras.");
                }
                var definition = new SerializedObject(aircraft.FindProperty("definition").objectReferenceValue);
                if (Mathf.Abs(definition.FindProperty("value").floatValue - 40f) > 0.001f)
                    throw new InvalidOperationException("Aircraft price mismatch.");
                var parameters = new SerializedObject(definition.FindProperty("aircraftParameters").objectReferenceValue);
                if (parameters.FindProperty("rankRequired").intValue != 3)
                    throw new InvalidOperationException("Aircraft rank mismatch.");
                report.AppendLine("View references contain only F-4E exterior; rank 3 and price 40 verified.");
            }
            for (var i = 0; i < F4EGearIntegration.Wheels.Length; i++)
            {
                var wheel = F4EGearIntegration.FindRequired(root.transform, F4EGearIntegration.Wheels[i]);
                var position = root.transform.InverseTransformPoint(wheel.position);
                if (Vector3.Distance(position, F4EGearIntegration.TireCenters[i]) > 0.001f)
                    throw new InvalidOperationException("Tire center alignment failed: " + wheel.name);
                report.AppendLine("Aligned tire center: " + wheel.name + " " + position.ToString("F4"));
            }
            // A second alignment must not cause drift across repeated generation.
            var positions = new Vector3[all.Length];
            for (var i = 0; i < all.Length; i++) positions[i] = all[i].position;
            F4EGearIntegration.AlignContacts(root.transform);
            for (var i = 0; i < all.Length; i++)
                if (Vector3.Distance(positions[i], all[i].position) > 0.0001f)
                    throw new InvalidOperationException("Contact alignment is not idempotent.");
            var names = new[] { "F4E_Gear_Nose", "F4E_Gear_Left", "F4E_Gear_Right", "F4E_Gear_Tire_Nose", "F4E_Gear_Tire_Left", "F4E_Gear_Tire_Right" };
            var parents = new[] { "gear_F", "gear_L_sprung", "gear_R_sprung", "wheel_F", "wheel_L", "wheel_R" };
            for (var i = 0; i < names.Length; i++)
            {
                Transform part = null;
                var count = 0;
                foreach (var t in all) if (t.name == names[i]) { part = t; count++; }
                if (count != 1 || part.parent.name != parents[i])
                    throw new InvalidOperationException("Incorrect or duplicate gear binding: " + names[i]);
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled)
                    throw new InvalidOperationException("Gear renderer disabled: " + names[i]);
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                foreach (var vertex in mesh.vertices)
                {
                    var actual = root.transform.InverseTransformPoint(part.TransformPoint(vertex));
                    if (Vector3.Distance(actual, vertex) > 0.001f)
                        throw new InvalidOperationException("Gear binding moved or mirrored source geometry: " + names[i]);
                }
                var before = part.TransformPoint(mesh.bounds.max);
                var parent = part.parent;
                var rotation = parent.localRotation;
                parent.localRotation = rotation * Quaternion.Euler(20f, 0f, 0f);
                var displacement = Vector3.Distance(before, part.TransformPoint(mesh.bounds.max));
                parent.localRotation = rotation;
                if (displacement < 0.01f)
                    throw new InvalidOperationException("Gear does not follow its parent: " + names[i]);
                report.AppendLine(names[i] + " -> " + parents[i] + "; follows parent rotation; probe=" + root.transform.InverseTransformPoint(before).ToString("F3"));
            }
            for (var i = 0; i < 3; i++)
            {
                var wheel = F4EGearIntegration.FindRequired(root.transform, F4EGearIntegration.Wheels[i]);
                var tire = F4EGearIntegration.FindRequired(root.transform, F4EGearIntegration.Names[i + 4]);
                var strut = F4EGearIntegration.FindRequired(root.transform, F4EGearIntegration.Names[i + 1]);
                var suspension = wheel.parent;
                var saved = suspension.position;
                var tireBefore = tire.position;
                var strutBefore = strut.position;
                var delta = root.transform.up * 0.1f;
                suspension.position += delta;
                var tireDelta = tire.position - tireBefore;
                var strutDelta = strut.position - strutBefore;
                suspension.position = saved;
                if (Vector3.Distance(tireDelta, delta) > 0.001f || strutDelta.magnitude > 0.001f)
                    throw new InvalidOperationException("Independent tire suspension test failed: " + wheel.name);
                report.AppendLine(wheel.name + ": tire follows 0.1m suspension displacement; fixed strut stays in place.");
            }
            foreach (var t in all)
            {
                if (!t.name.StartsWith("gear") && !t.name.StartsWith("wheel")) continue;
                report.AppendLine(t.name + " parent=" + t.parent.name + " position=" + root.transform.InverseTransformPoint(t.position).ToString("F3") + " localRotation=" + t.localEulerAngles.ToString("F2"));
                foreach (var component in t.GetComponents<Component>())
                {
                    if (component == null) throw new InvalidOperationException("Missing gear script: " + t.name);
                    if (component.GetType().Name != "LandingGear") continue;
                    var serialized = new SerializedObject(component);
                    var bump = (serialized.FindProperty("bumpStop").objectReferenceValue as GameObject).transform;
                    var moving = (serialized.FindProperty("unsprung").objectReferenceValue as GameObject).transform;
                    var radius = serialized.FindProperty("wheelRadius").floatValue;
                    var travel = serialized.FindProperty("suspensionTravel").floatValue;
                    var unloadedPosition = bump.position - moving.up * (travel - radius);
                    if (Vector3.Distance(unloadedPosition, moving.position) > 0.001f)
                        throw new InvalidOperationException("Runtime suspension would jump on load: " + t.name);
                    report.AppendLine("  Unloaded suspension equation matches editor pose within 1mm.");
                    report.AppendLine("  LandingGear radius=" + serialized.FindProperty("wheelRadius").floatValue
                        + "; travel=" + serialized.FindProperty("suspensionTravel").floatValue);
                }
            }
            return report.ToString();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
