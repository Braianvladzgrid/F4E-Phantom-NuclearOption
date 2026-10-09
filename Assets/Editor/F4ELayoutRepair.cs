using System;
using UnityEditor;
using UnityEngine;
internal static class F4ELayoutRepair
{
    internal static void ApplyAndValidate()
    {
        const string path="Assets/Blueprinter/Mods/F4EPhantom/F4E_Phantom_II.prefab";
        var definition=AssetDatabase.LoadMainAssetAtPath("Assets/Blueprinter/_donotship/MonoBehaviour/FS-12_PLACEHOLDER.asset");
        var donor=new SerializedObject(definition).FindProperty("unitPrefab").objectReferenceValue as GameObject;
        var root=PrefabUtility.LoadPrefabContents(path);
        try {
            // Move only the crew and interior, keeping the original flight parts,
            // suspension, damage hierarchy and camera-to-instrument offsets intact.
            var pilot=root.transform.Find("fuselage/cockpit/pilot");
            var sourcePilot=donor.transform.Find("fuselage/cockpit/pilot");
            if(pilot==null||sourcePilot==null)throw new InvalidOperationException("Missing crew assembly");
            var delta=new Vector3(0f,0.010f,4.650f)-donor.transform.InverseTransformPoint(sourcePilot.position);
            foreach(var name in new[]{"pilot","cockpit_interior","cockpit_interior_simple","targetScreen","joystick","throttle","canopyHingea"}) {
                var relative="fuselage/cockpit/"+name;
                var target=root.transform.Find(relative);var original=donor.transform.Find(relative);
                if(target==null||original==null)throw new InvalidOperationException("Missing cockpit part: "+relative);
                target.position=root.transform.TransformPoint(donor.transform.InverseTransformPoint(original.position)+delta);
            }
            foreach(var c in root.GetComponents<Component>()) {
                if(c==null||c.GetType().Name!="Aircraft")continue;
                var view=new SerializedObject(c).FindProperty("cockpitViewPoint").objectReferenceValue as Transform;
                if(view==null||!view.IsChildOf(pilot))throw new InvalidOperationException("View is not attached to the repositioned crew");
                var position=root.transform.InverseTransformPoint(view.position);
                if(position.z<4f||position.z>5.3f||position.y<0.3f||position.y>1.2f)throw new InvalidOperationException("Camera outside front canopy: "+position);
                Debug.Log("[F4E] Crew/cockpit aligned. Camera "+position.ToString("F3"));
            }
            F4ENoseDoorRepair.Apply(root);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }
}
