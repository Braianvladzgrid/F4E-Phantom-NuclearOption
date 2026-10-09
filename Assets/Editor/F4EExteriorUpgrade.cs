using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class F4EExteriorUpgrade
{
    const string Mod="Assets/Blueprinter/Mods/F4EPhantom";
    const string Art=Mod+"/Art/PhantomDetails";
    internal static void ApplyAndValidate()
    {
        if(!AssetDatabase.IsValidFolder(Art))AssetDatabase.CreateFolder(Mod+"/Art","PhantomDetails");
        var definition=AssetDatabase.LoadMainAssetAtPath("Assets/Blueprinter/_donotship/MonoBehaviour/FS-12_PLACEHOLDER.asset");
        var donor=new SerializedObject(definition).FindProperty("unitPrefab").objectReferenceValue as GameObject;
        var root=PrefabUtility.LoadPrefabContents(Mod+"/F4E_Phantom_II.prefab");
        try {
            Remove(root.transform.Find("F4E_PhantomDetails"));
            var details=new GameObject("F4E_PhantomDetails").transform;details.SetParent(root.transform,false);
            var gray=Material("F4E_PylonGray",new Color(.52f,.55f,.57f),.34f);
            var metal=Material("F4E_NozzleMetal",new Color(.20f,.19f,.17f),.78f);
            var dark=Material("F4E_NozzleInterior",new Color(.045f,.038f,.03f),.15f);
            var exterior=new List<Renderer>();
            var stations=new[]{"hardpoint_wingtip_L","hardpoint_wingtip_R","pylon_wing_L","pylon_wing_R","weaponbayMount_L","weaponbayMount_R"};
            for(int i=0;i<stations.Length;i++) {
                var station=F4EGearIntegration.FindRequired(root.transform,stations[i]);
                var pos=root.transform.InverseTransformPoint(station.position);
                pos.y=i<2?-1.35f:i<4?-1.15f:-1.10f;
                station.position=root.transform.TransformPoint(pos);station.rotation=root.transform.rotation;
                var length=i<2?1.55f:i<4?1.30f:1.3f;
                var height=i<2?.65f:i<4?.46f:.32f;
                var mesh=SaveMesh("F4E_Pylon_"+i,PylonMesh(length,height,i>=4?.12f:.15f));
                // Parent to the station's damage assembly, so pylon and weapon stay together.
                var r=Renderer("F4E_Pylon_"+stations[i],station.parent,mesh,gray);
                r.transform.position=station.position;r.transform.rotation=root.transform.rotation;
                exterior.Add(r);
                BindHardpoint(root,station,r);
            }
            UpgradeAfterburners(root,donor,details,metal,dark,exterior);
            foreach(var c in root.GetComponents<Component>()) {
                if(c==null||c.GetType().Name!="Aircraft")continue;
                var data=new SerializedObject(c);var list=data.FindProperty("exteriorRenderers");
                if(list.arraySize!=15)throw new InvalidOperationException("Expected repaired exterior before upgrade");
                var start=list.arraySize;list.arraySize+=exterior.Count;
                for(int i=0;i<exterior.Count;i++)list.GetArrayElementAtIndex(start+i).objectReferenceValue=exterior[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root,Mod+"/F4E_Phantom_II.prefab");
            AssetDatabase.SaveAssets();
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void BindHardpoint(GameObject root,Transform station,Renderer renderer)
    {
        foreach(var c in root.GetComponentsInChildren<Component>(true)) {
            if(c==null||c.GetType().Name!="WeaponManager")continue;
            var data=new SerializedObject(c);var sets=data.FindProperty("hardpointSets");
            for(int i=0;i<sets.arraySize;i++) {
                var set=sets.GetArrayElementAtIndex(i);
                if(i==2)set.FindPropertyRelative("name").stringValue="Inner Wing Pylons";
                if(i==3)set.FindPropertyRelative("name").stringValue="Outer Wing Pylons";
                var points=set.FindPropertyRelative("hardpoints");
                for(int j=0;j<points.arraySize;j++) {
                    var point=points.GetArrayElementAtIndex(j);
                    if(point.FindPropertyRelative("transform").objectReferenceValue!=station)continue;
                    // Native pylon options would reveal geometry from the donor aircraft.
                    var options=point.FindPropertyRelative("pylonOptions");options.arraySize=1;
                    var option=options.GetArrayElementAtIndex(0);
                    option.FindPropertyRelative("cargo").boolValue=false;
                    option.FindPropertyRelative("mount").objectReferenceValue=null;
                    option.FindPropertyRelative("renderer").objectReferenceValue=renderer;
                    point.FindPropertyRelative("Pylon").objectReferenceValue=renderer;
                    point.FindPropertyRelative("Plug").objectReferenceValue=null;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    static void UpgradeAfterburners(GameObject root,GameObject donor,Transform details,Material metal,Material dark,List<Renderer> exterior)
    {
        Remove(root.transform.Find("tail/F4E_Exhaust_Left"));
        Remove(root.transform.Find("tail/F4E_Exhaust_Right"));
        var original=FindComponent(donor,"JetNozzle");var nozzle=FindComponent(root,"JetNozzle");
        var source=new SerializedObject(original);var data=new SerializedObject(nozzle);
        var baseline=source.FindProperty("afterburners").GetArrayElementAtIndex(0);
        var flame=baseline.FindPropertyRelative("flameRenderer").objectReferenceValue as Renderer;
        var glow=baseline.FindPropertyRelative("nozzleGlowRenderer").objectReferenceValue as Renderer;
        if(flame==null||glow==null)throw new InvalidOperationException("Native afterburner visuals missing");
        // Reference native meshes through Blueprinter's runtime asset resolver.
        // Normalize with transforms, without distributing copied game mesh data.
        var flameMesh=flame.GetComponent<MeshFilter>().sharedMesh;
        var glowMesh=glow.GetComponent<MeshFilter>().sharedMesh;
        var rimMesh=SaveMesh("F4E_NozzleRim",RingMesh(.46f,.49f,.38f,-.02f,.34f));
        var linerMesh=SaveMesh("F4E_NozzleLiner",RingMesh(.38f,.38f,.26f,.025f,.32f));
        var entries=data.FindProperty("afterburners");entries.arraySize=2;
        // Preserve the already-remapped native source; both entries share one audio emitter.
        var existingSource=entries.GetArrayElementAtIndex(0).FindPropertyRelative("source").objectReferenceValue;
        for(int i=0;i<2;i++) {
            var frame=new GameObject(i==0?"F4E_Exhaust_Left":"F4E_Exhaust_Right").transform;
            frame.SetParent(root.transform.Find("tail"),false);
            frame.position=root.transform.TransformPoint(new Vector3(i==0?-.64f:.64f,-.64f,-5.76f));frame.rotation=root.transform.rotation;
            var rim=Renderer("F4E_NozzleRim_"+i,frame,rimMesh,metal);
            var liner=Renderer("F4E_NozzleLiner_"+i,frame,linerMesh,dark);exterior.Add(rim);exterior.Add(liner);
            var scaled=new GameObject("F4E_FlameScale").transform;scaled.SetParent(frame,false);scaled.localScale=new Vector3(.70f,.62f,.58f);
            var f=Renderer("F4E_AfterburnerFlame_"+i,scaled,flameMesh,flame.sharedMaterial);f.enabled=false;
            var flameBounds=flameMesh.bounds;
            f.transform.localPosition=-new Vector3(flameBounds.center.x,flameBounds.center.y,flameBounds.max.z);
            var g=Renderer("F4E_AfterburnerGlow_"+i,frame,glowMesh,glow.sharedMaterial);
            g.transform.localScale=Vector3.one*.70f;g.transform.localPosition=new Vector3(0f,0f,.06f)-glowMesh.bounds.center*.70f;g.enabled=true;
            var entry=entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("flameRenderer").objectReferenceValue=f;
            entry.FindPropertyRelative("nozzleGlowRenderer").objectReferenceValue=g;
            entry.FindPropertyRelative("thrustDirection").objectReferenceValue=data.FindProperty("thrustTransform").objectReferenceValue;
            entry.FindPropertyRelative("source").objectReferenceValue=existingSource;
            foreach(var name in new[]{"smoothing","throttleStart","throttleEnd","flameBrightness","nozzleGlowBrightness","temperature"})
                entry.FindPropertyRelative(name).floatValue=baseline.FindPropertyRelative(name).floatValue;
            foreach(var name in new[]{"thrust","fuelConsumption","IRIntensity"})
                entry.FindPropertyRelative(name).floatValue=baseline.FindPropertyRelative(name).floatValue*.5f;
            entry.FindPropertyRelative("afterburnerAmount").floatValue=0f;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach(var r in root.transform.Find("tail/nozzle").GetComponentsInChildren<Renderer>(true))r.enabled=false;
        var haze=data.FindProperty("heatHaze").FindPropertyRelative("system").objectReferenceValue as ParticleSystem;
        if(haze!=null)haze.transform.position=root.transform.TransformPoint(new Vector3(0f,-.64f,-5.76f));
        foreach(var name in new[]{"thrust","fuelConsumption","IRIntensity"}) {
            var sum=entries.GetArrayElementAtIndex(0).FindPropertyRelative(name).floatValue+entries.GetArrayElementAtIndex(1).FindPropertyRelative(name).floatValue;
            if(Mathf.Abs(sum-baseline.FindPropertyRelative(name).floatValue)>.001f)throw new InvalidOperationException("Afterburner total changed: "+name);
        }
        if(new SerializedObject(FindComponent(root,"Turbojet")).FindProperty("nozzles").arraySize!=1)throw new InvalidOperationException("Engine force controller was duplicated");
        File.WriteAllText(Path.GetFullPath("../../../outputs/F4EPhantom/exterior-upgrade-audit.txt"),
            "Six weapon stations with visible supports; two exhaust outlets at x +/-0.64, y -0.64, z -5.76.\n"+
            "One original engine/nozzle force controller. Two native throttle-controlled afterburner entries.\n"+
            "Total afterburner thrust=50000 N; fuelConsumption=5; IRIntensity=4.5, preserved from donor.\n"+
            "In-game mounting clearance, release and afterburner appearance require flight verification.");
    }
    static Component FindComponent(GameObject root,string type){foreach(var c in root.GetComponentsInChildren<Component>(true))if(c!=null&&c.GetType().Name==type)return c;throw new InvalidOperationException(type+" missing");}
    static void Remove(Transform t){if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);}
    static Renderer Renderer(string name,Transform parent,Mesh mesh,Material material){
        // Remove previous detail geometry before an idempotent rebuild.
        Remove(parent.Find(name));var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.layer=14;
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=material;return r;
    }
    static Material Material(string name,Color color,float metal){
        var path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetColor("_Color",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.35f);EditorUtility.SetDirty(m);return m;
    }
    static Mesh SaveMesh(string name,Mesh candidate){
        var path=Art+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){candidate.name=name;AssetDatabase.CreateAsset(candidate,path);return candidate;}
        EditorUtility.CopySerialized(candidate,mesh);mesh.name=name;UnityEngine.Object.DestroyImmediate(candidate);EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh PylonMesh(float length,float height,float width){
        var side=new[]{new Vector2(-length*.50f,height),new Vector2(length*.50f,height),new Vector2(length*.36f,0f),new Vector2(-length*.34f,0f)};
        var v=new List<Vector3>();var t=new List<int>();
        // Separate face vertices retain sharp edges and a swept leading/trailing profile.
        for(int face=0;face<6;face++){
            int[] ids=face==0?new[]{0,3,2,1}:face==1?new[]{4,5,6,7}:new[]{face-2,(face-1)%4,(face-1)%4+4,face-2+4};
            int start=v.Count;foreach(int id in ids){var p=side[id%4];v.Add(new Vector3(id<4?-width*.5f:width*.5f,p.y,p.x));}
            t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Mesh RingMesh(float radius,float forwardRadius,float inner,float rearZ,float frontZ){
        var v=new List<Vector3>();var t=new List<int>();const int segments=32;
        for(int i=0;i<segments;i++){
            float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
            var rings=new[]{new Vector2(radius,rearZ),new Vector2(forwardRadius,frontZ),new Vector2(inner,frontZ),new Vector2(inner,rearZ)};
            for(int k=0;k<4;k++){
                var p=rings[k];var q=rings[(k+1)%4];int n=v.Count;
                v.Add(new Vector3(Mathf.Cos(a)*p.x,Mathf.Sin(a)*p.x,p.y));v.Add(new Vector3(Mathf.Cos(b)*p.x,Mathf.Sin(b)*p.x,p.y));
                v.Add(new Vector3(Mathf.Cos(b)*q.x,Mathf.Sin(b)*q.x,q.y));v.Add(new Vector3(Mathf.Cos(a)*q.x,Mathf.Sin(a)*q.x,q.y));
                t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
        }
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
}
