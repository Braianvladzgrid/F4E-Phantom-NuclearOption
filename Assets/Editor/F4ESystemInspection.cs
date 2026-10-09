using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class F4ESystemInspection
{
    static F4ESystemInspection() { EditorApplication.update += WhenReady; }

    private static void WhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WhenReady;
        if (SessionState.GetBool("F4E.SystemInspection.1", false)) return;
        SessionState.SetBool("F4E.SystemInspection.1", true);
        Run();
    }

    [MenuItem("F4E/Inspect Integration Systems")]
    internal static void Run()
    {
        var report = new StringBuilder();
        var root = PrefabUtility.LoadPrefabContents("Assets/Blueprinter/Mods/F4EPhantom/F4E_Phantom_II.prefab");
        try
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var name = t.name.ToLowerInvariant();
                if (!name.Contains("camera") && !name.Contains("view") && !name.Contains("cockpit")
                    && !name.Contains("hardpoint") && !name.Contains("pylon") && !name.Contains("weaponbay")
                    && !name.Contains("bumpstop") && !name.Contains("castpoint") && !name.Contains("unsprung")) continue;
                report.AppendLine(t.name + " parent=" + (t.parent == null ? "ROOT" : t.parent.name)
                    + " pos=" + root.transform.InverseTransformPoint(t.position).ToString("F4"));
                foreach (var c in t.GetComponents<Component>())
                    if (c != null && !(c is Transform)) report.AppendLine("  " + c.GetType().Name);
            }
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "LandingGear") continue;
                report.AppendLine("GEAR " + c.name);
                var so = new SerializedObject(c);
                foreach (var name in new[] { "bumpStop", "unsprung", "castPoint", "axle", "gearHinge", "strutRotationTransform", "attachedPart", "aircraft" })
                {
                    var value = so.FindProperty(name)?.objectReferenceValue;
                    report.AppendLine("  " + name + "=" + (value == null ? "NULL" : value.name + ":" + value.GetType().Name));
                }
                report.AppendLine("  fold=" + so.FindProperty("foldDegrees").floatValue + " travel=" + so.FindProperty("suspensionTravel").floatValue);
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                foreach (var type in types)
                {
                    if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        if (method.Name == "OnParticleUpdateJobScheduled")
                            report.AppendLine("PARTICLE_CALLBACK " + type.FullName + " assembly=" + assembly.GetName().Name + " signature=" + method);
                }
            }
            File.WriteAllText(Path.GetFullPath("../../../outputs/F4EPhantom/system-inspection.txt"), report.ToString());
            Debug.Log("[F4E] System inspection saved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
