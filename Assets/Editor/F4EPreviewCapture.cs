using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal static class F4EPreviewCapture
{
    static F4EPreviewCapture() { EditorApplication.update += WhenReady; }
    private static void WhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WhenReady;
        if (SessionState.GetBool("F4E.PreviewCapture.3", false)) return;
        SessionState.SetBool("F4E.PreviewCapture.3", true);
        try { Capture(); } catch (Exception e) { Debug.LogException(e); }
    }

    [MenuItem("F4E/Capture Inspection Views")]
    internal static void Capture()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Blueprinter/Mods/F4EPhantom/F4E_Phantom_II.prefab");
        GameObject cameraObject = null, lightObject = null;
        try
        {
            cameraObject = new GameObject("InspectionCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Game;
            camera.scene = root.scene;
            EditorSceneManager.SetSceneCullingMask(root.scene, 1UL << 60);
            camera.overrideSceneCullingMask = 1UL << 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.24f, 0.29f, 0.33f);
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 150f;
            camera.fieldOfView = 45f;
            camera.transform.position = root.transform.TransformPoint(new Vector3(18f, 7f, 23f));
            camera.transform.LookAt(root.transform.position);
            lightObject = new GameObject("InspectionLight");
            SceneManager.MoveGameObjectToScene(lightObject, root.scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Debug.Log("[F4E] Render diagnostics: active=" + root.activeInHierarchy + " renderers=" + root.GetComponentsInChildren<Renderer>(true).Length + " sceneMask=" + camera.overrideSceneCullingMask);
            // Runtime shows the pilot in external view. Include that in inspection.
            foreach(var r in root.transform.Find("fuselage/cockpit/pilot").GetComponentsInChildren<SkinnedMeshRenderer>(true))r.enabled=true;
            Save(camera, "inspection-extended.png");
            camera.transform.position = root.transform.TransformPoint(new Vector3(8f, 2.5f, 7f));
            camera.transform.LookAt(root.transform.TransformPoint(new Vector3(0f, 0.4f, 4.4f)));
            Save(camera, "inspection-cabin-exterior.png");
            camera.transform.position = root.transform.TransformPoint(new Vector3(18f, 7f, 23f));
            camera.transform.LookAt(root.transform.position);
            camera.transform.position = root.transform.TransformPoint(new Vector3(9f, -4f, 5f));
            camera.transform.LookAt(root.transform.TransformPoint(new Vector3(0f,-.5f,0f)));
            Save(camera,"inspection-pylons.png");
            camera.transform.position=root.transform.TransformPoint(new Vector3(3.5f,1.2f,-11.5f));
            camera.transform.LookAt(root.transform.TransformPoint(new Vector3(0f,-.4f,-5.5f)));
            Save(camera,"inspection-twin-exhaust.png");
            camera.transform.position = root.transform.TransformPoint(new Vector3(18f, 7f, 23f));
            camera.transform.LookAt(root.transform.position);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "LandingGear") continue;
                var gear = new SerializedObject(component);
                var hinge = gear.FindProperty("gearHinge").objectReferenceValue as Transform;
                if (hinge == null) throw new InvalidOperationException("Missing retract hinge.");
                hinge.localEulerAngles += new Vector3(gear.FindProperty("foldDegrees").floatValue, 0f, 0f);
                hinge.localPosition += gear.FindProperty("hingeFoldMotion").vector3Value;
                var strut = gear.FindProperty("strutRotationTransform").objectReferenceValue as Transform;
                if (strut == null) strut = (gear.FindProperty("unsprung").objectReferenceValue as GameObject).transform;
                strut.localEulerAngles = new Vector3(0f, gear.FindProperty("strutRotation").floatValue, 0f);
                var doors=gear.FindProperty("gearDoors");
                for(int i=0;i<doors.arraySize;i++){
                    var door=doors.GetArrayElementAtIndex(i);
                    var hingeTransform=door.FindPropertyRelative("transform").objectReferenceValue as Transform;
                    if(hingeTransform!=null)hingeTransform.localEulerAngles=door.FindPropertyRelative("closedAngle").vector3Value;
                }
            }
            Save(camera, "inspection-folded-probe.png");
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "Aircraft") continue;
                var aircraft = new SerializedObject(component);
                SetVisibility(aircraft.FindProperty("exteriorRenderers"), false);
                SetVisibility(aircraft.FindProperty("cockpitRenderers"), true);
                foreach(var r in root.transform.Find("fuselage/cockpit/pilot").GetComponentsInChildren<SkinnedMeshRenderer>(true))r.enabled=false;
                var view = aircraft.FindProperty("cockpitViewPoint").objectReferenceValue as Transform;
                if (view == null) throw new InvalidOperationException("Missing cockpit view point.");
                camera.transform.SetPositionAndRotation(view.position, view.rotation);
                camera.fieldOfView = 75f;
                Save(camera, "inspection-cockpit.png");
            }
            Debug.Log("[F4E] Inspection images saved. Folded probe is not an in-game animation test.");
        }
        finally
        {
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetVisibility(SerializedProperty array, bool value)
    {
        for (var i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue is Renderer renderer) renderer.enabled = value;
    }

    private static void Save(Camera camera, string name)
    {
        var previous = RenderTexture.active;
        var texture = RenderTexture.GetTemporary(1200, 800, 24);
        Texture2D image = null;
        try
        {
            camera.targetTexture = texture;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = texture };
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else camera.Render();
            RenderTexture.active = texture;
            image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
            image.Apply();
            var pixels = image.GetPixels32();
            var background = pixels[0];
            var different = 0;
            foreach (var pixel in pixels)
                if (Math.Abs(pixel.r - background.r) + Math.Abs(pixel.g - background.g) + Math.Abs(pixel.b - background.b) > 24)
                    different++;
            if (different < pixels.Length / 100)
                throw new InvalidOperationException("Inspection render is blank or insufficiently framed: " + name);
            File.WriteAllBytes(Path.GetFullPath("../../../outputs/F4EPhantom/" + name), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(texture);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}


