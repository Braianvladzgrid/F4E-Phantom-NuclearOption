using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class F4EViewIntegration
{
    internal static void Configure(GameObject root, Transform imported)
    {
        var exterior = new List<Renderer>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.transform.IsChildOf(imported) || renderer.name.StartsWith("F4E_Gear_")) exterior.Add(renderer);
        if (exterior.Count != 14) throw new InvalidOperationException("Unexpected F-4E exterior renderer count.");
        foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.GetType().Name != "Aircraft") continue;
            var serialized = new SerializedObject(component);
            var internalRenderers = serialized.FindProperty("cockpitRenderers");
            var externalRenderers = serialized.FindProperty("exteriorRenderers");
            if (internalRenderers == null || externalRenderers == null || internalRenderers.arraySize == 0)
                throw new InvalidOperationException("Missing aircraft view renderer contract.");
            externalRenderers.arraySize = exterior.Count;
            for (var i = 0; i < exterior.Count; i++) externalRenderers.GetArrayElementAtIndex(i).objectReferenceValue = exterior[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Aircraft.SetCockpitRenderers switches these lists at runtime.
            // Start with the exterior preview; retain the native instrument references.
            for (var i = 0; i < internalRenderers.arraySize; i++)
            {
                var renderer = internalRenderers.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                if (renderer == null) throw new InvalidOperationException("Missing native cockpit renderer.");
                renderer.enabled = false;
            }
            foreach (var renderer in exterior) renderer.enabled = true;
            return;
        }
        throw new InvalidOperationException("Missing aircraft for view integration.");
    }
}
