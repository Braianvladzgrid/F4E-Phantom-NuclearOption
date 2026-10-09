using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class F4EVisualInspection
{
    static F4EVisualInspection()
    {
        if (!Application.isBatchMode) EditorApplication.update += OpenWhenReady;
    }

    private static void OpenWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SessionState.GetBool("F4E.VisualInspection.v6", false))
        {
            EditorApplication.update -= OpenWhenReady;
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(F4EImportedModelBuilder.PrefabPath);
        if (prefab == null) return;
        EditorApplication.update -= OpenWhenReady;
        SessionState.SetBool("F4E.VisualInspection.v6", true);
        AssetDatabase.OpenAsset(prefab);
        EditorApplication.delayCall += () =>
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.LookAt(Vector3.zero, Quaternion.Euler(20f, 150f, 0f), 11f);
            view.Repaint();
        };
    }
}
