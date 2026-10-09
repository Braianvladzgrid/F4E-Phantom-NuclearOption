using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
internal static class F4ENoseDoorRepair
{
    internal static void Apply(GameObject root)
    {
        const string art="Assets/Blueprinter/Mods/F4EPhantom/Art/Imported/";
        var original=AssetDatabase.LoadAssetAtPath<Mesh>(art+"MeshesV7/F4E_Gear_Nose.asset");
        var v=original.vertices;var tri=original.triangles;var p=new int[v.Length];
        var weld=new Dictionary<Vector3Int,int>();
        for(int i=0;i<v.Length;i++)p[i]=i;
        for(int i=0;i<v.Length;i++){var key=Vector3Int.RoundToInt(v[i]*100000f);if(weld.TryGetValue(key,out var j))p[Find(p,i)]=Find(p,j);else weld[key]=i;}
        for(int i=0;i<tri.Length;i+=3){p[Find(p,tri[i])]=Find(p,tri[i+1]);p[Find(p,tri[i])]=Find(p,tri[i+2]);}
        var boxes=new Dictionary<int,Bounds>();
        for(int i=0;i<v.Length;i++){var id=Find(p,i);if(!boxes.TryGetValue(id,out var b))b=new Bounds(v[i],Vector3.zero);b.Encapsulate(v[i]);boxes[id]=b;}
        var gearTriangles=new List<int>();var doorTriangles=new List<int>();
        for(int i=0;i<tri.Length;i+=3){
            // The long side door and its fittings are disconnected from the strut.
            // Split whole connected pieces, never individual intersecting triangles.
            var dest=boxes[Find(p,tri[i])].center.z<5.1f?doorTriangles:gearTriangles;
            dest.Add(tri[i]);dest.Add(tri[i+1]);dest.Add(tri[i+2]);
        }
        if(gearTriangles.Count==0||doorTriangles.Count==0||gearTriangles.Count+doorTriangles.Count!=tri.Length)throw new InvalidOperationException("Invalid nose door separation");
        var gearMesh=SaveMesh(original,gearTriangles,art+"F4E_NoseStrut_v8.asset");
        var doorMesh=SaveMesh(original,doorTriangles,art+"F4E_NoseDoor_v8.asset");
        var strut=F4EGearIntegration.FindRequired(root.transform,"F4E_Gear_Nose");
        strut.GetComponent<MeshFilter>().sharedMesh=gearMesh;
        var cockpit=root.transform.Find("fuselage/cockpit");
        var pivot=cockpit.Find("F4E_NoseDoorPivot");
        if(pivot!=null)UnityEngine.Object.DestroyImmediate(pivot.gameObject);
        pivot=new GameObject("F4E_NoseDoorPivot").transform;pivot.SetParent(cockpit,false);
        pivot.position=root.transform.TransformPoint(new Vector3(0.21f,-0.70f,4.16f));
        pivot.rotation=root.transform.rotation;
        var door=new GameObject("F4E_NoseDoor");door.transform.SetParent(root.transform,false);door.transform.SetParent(pivot,true);
        door.AddComponent<MeshFilter>().sharedMesh=doorMesh;
        var renderer=door.AddComponent<MeshRenderer>();renderer.sharedMaterials=strut.GetComponent<Renderer>().sharedMaterials;
        door.layer=strut.gameObject.layer;
        var gearBody=F4EGearIntegration.FindRequired(root.transform,"gear_F");
        foreach(var c in gearBody.GetComponents<Component>()){
            if(c==null||c.GetType().Name!="LandingGear")continue;
            var data=new SerializedObject(c);var doors=data.FindProperty("gearDoors");doors.arraySize=1;
            var item=doors.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("transform").objectReferenceValue=pivot;
            item.FindPropertyRelative("openAngle").vector3Value=Vector3.zero;
            item.FindPropertyRelative("closedAngle").vector3Value=new Vector3(0f,0f,-90f);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(var c in root.GetComponents<Component>()){
            if(c==null||c.GetType().Name!="Aircraft")continue;
            var data=new SerializedObject(c);var list=data.FindProperty("exteriorRenderers");
            // The factory reconstructs the fourteen imported references before repair.
            if(list.arraySize!=14)throw new InvalidOperationException("Unexpected exterior before door repair");
            list.arraySize=15;list.GetArrayElementAtIndex(14).objectReferenceValue=renderer;data.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log("[F4E] Nose door separated from strut and connected to native gear-door animation.");
    }
    static Mesh SaveMesh(Mesh original,List<int> triangles,string path){
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=UnityEngine.Object.Instantiate(original);mesh.name=System.IO.Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(mesh,path);}
        mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static int Find(int[] p,int i){while(p[i]!=i){p[i]=p[p[i]];i=p[i];}return i;}
}
