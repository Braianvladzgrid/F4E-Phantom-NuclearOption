using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
internal static class F4ECockpitRepair
{
    internal static void ApplyAndValidate()
    {
        const string path = "Assets/Blueprinter/Mods/F4EPhantom/F4E_Phantom_II.prefab";
        var def = AssetDatabase.LoadMainAssetAtPath("Assets/Blueprinter/_donotship/MonoBehaviour/FS-12_PLACEHOLDER.asset");
        var donor = new SerializedObject(def).FindProperty("unitPrefab").objectReferenceValue as GameObject;
        if (donor == null) throw new InvalidOperationException("Native cockpit donor missing");
        var source = new Dictionary<string, Renderer>();
        foreach (var r in donor.GetComponentsInChildren<Renderer>(true)) source[AnimationUtility.CalculateTransformPath(r.transform,donor.transform)] = r;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int restored = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Renderer native;
                if (!source.TryGetValue(AnimationUtility.CalculateTransformPath(r.transform,root.transform),out native)) continue;
                r.sharedMaterials = native.sharedMaterials;
                restored++;
            }
            int verified = 0;
            foreach(var c in root.GetComponentsInChildren<Component>(true))
            {
                if(c == null || c.GetType().Name != "Aircraft") continue;
                var list = new SerializedObject(c).FindProperty("cockpitRenderers");
                for(int i=0;i<list.arraySize;i++)
                {
                    var r=list.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    Renderer native;
                    if(r==null || !source.TryGetValue(AnimationUtility.CalculateTransformPath(r.transform,root.transform),out native)) throw new InvalidOperationException("Cockpit renderer has no native match");
                    if(r.sharedMaterials.Length == 0 || r.sharedMaterials.Length != native.sharedMaterials.Length) throw new InvalidOperationException("Invalid cockpit materials");
                    for(int k=0;k<r.sharedMaterials.Length;k++) if(r.sharedMaterials[k] == null || r.sharedMaterials[k] != native.sharedMaterials[k]) throw new InvalidOperationException("Unrestored cockpit material");
                    verified++;
                    Debug.Log("[F4E] Native cockpit material restored: " + r.name + " -> " + r.sharedMaterial.name);
                }
            }
            if(verified != 6) throw new InvalidOperationException("Expected six verified native cockpit renderers");
            PrefabUtility.SaveAsPrefabAsset(root,path);
            Debug.Log("[F4E] Restored native materials on " + restored + " renderers; verified " + verified + " cockpit renderers.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
