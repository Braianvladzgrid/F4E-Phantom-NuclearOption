using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class F4ELiveryRepair
{
    internal static void ApplyAndValidate()
    {
        const string root = "Assets/Blueprinter/Mods/F4EPhantom";
        string path = root + "/F4E_StandardLivery.asset";
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("LiveryData")).FirstOrDefault(t => t != null);
        if (type == null) throw new InvalidOperationException("LiveryData type unavailable");
        var livery = AssetDatabase.LoadMainAssetAtPath(path);
        if (livery == null) { livery = ScriptableObject.CreateInstance(type); AssetDatabase.CreateAsset(livery, path); }
        var material = AssetDatabase.LoadAssetAtPath<Material>(root + "/Art/Imported/MaterialsV7/F4E_0.mat");
        if (material == null || material.mainTexture == null) throw new InvalidOperationException("Phantom texture missing");
        var data = new SerializedObject(livery);
        data.FindProperty("Texture").objectReferenceValue = material.mainTexture;
        data.FindProperty("Glossiness").floatValue = 0.35f;
        var colors = data.FindProperty("Colors");
        colors.arraySize = 1;
        colors.GetArrayElementAtIndex(0).FindPropertyRelative("Color").colorValue = new Color(0.55f,0.57f,0.58f,1);
        colors.GetArrayElementAtIndex(0).FindPropertyRelative("Count").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(livery);
        var parameters = AssetDatabase.LoadMainAssetAtPath(root + "/F4E_Parameters.asset");
        var serialized = new SerializedObject(parameters);
        var liveries = serialized.FindProperty("liveries");
        liveries.arraySize = 1;
        var entry = liveries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("name").stringValue = "F-4E Standard Gray";
        entry.FindPropertyRelative("faction").objectReferenceValue = null;
        var reference = entry.FindPropertyRelative("assetReference");
        reference.FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(path);
        reference.FindPropertyRelative("m_SubObjectName").stringValue = "";
        reference.FindPropertyRelative("m_SubObjectType").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(parameters);
        AssetDatabase.SaveAssets();
        if (AssetDatabase.GUIDToAssetPath(reference.FindPropertyRelative("m_AssetGUID").stringValue) != path)
            throw new InvalidOperationException("Invalid Phantom livery reference");
        Debug.Log("[F4E] Validated one neutral livery with an included texture and addressable asset.");
    }
}
