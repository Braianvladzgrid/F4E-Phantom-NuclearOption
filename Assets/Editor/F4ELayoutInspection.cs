using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
[InitializeOnLoad]
internal static class F4ELayoutInspection
{
    static F4ELayoutInspection() { EditorApplication.delayCall += Inspect; }
    static void Inspect()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Blueprinter/Mods/F4EPhantom/F4E_Phantom_II.prefab");
        try {
            var report = new StringBuilder();
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) {
                var r=t.GetComponent<Renderer>();
                report.AppendLine(AnimationUtility.CalculateTransformPath(t,root.transform)+" pos="+root.transform.InverseTransformPoint(t.position).ToString("F3")+" rot="+t.localEulerAngles+" scale="+t.localScale+
                    (r==null?"":" renderer="+r.enabled+" bounds="+r.bounds+" mat="+(r.sharedMaterial==null?"null":r.sharedMaterial.name)));
            }
            File.WriteAllText(Path.GetFullPath("../../../outputs/F4EPhantom/layout-inspection.txt"),report.ToString());
            var filter=F4EGearIntegration.FindRequired(root.transform,"F4E_Gear_Nose").GetComponent<MeshFilter>();
            var mesh=filter.sharedMesh; var v=mesh.vertices; var tri=mesh.triangles;
            var p=new int[v.Length]; var weld=new System.Collections.Generic.Dictionary<Vector3Int,int>();
            for(int i=0;i<v.Length;i++)p[i]=i;
            for(int i=0;i<v.Length;i++){ var key=Vector3Int.RoundToInt(v[i]*100000f); if(weld.TryGetValue(key,out var j))p[Find(p,i)]=Find(p,j);else weld[key]=i; }
            for(int i=0;i<tri.Length;i+=3){p[Find(p,tri[i])]=Find(p,tri[i+1]);p[Find(p,tri[i])]=Find(p,tri[i+2]);}
            var boxes=new System.Collections.Generic.Dictionary<int,Bounds>();
            for(int i=0;i<v.Length;i++){var id=Find(p,i);if(!boxes.TryGetValue(id,out var b))b=new Bounds(v[i],Vector3.zero);b.Encapsulate(v[i]);boxes[id]=b;}
            var parts=new StringBuilder();foreach(var kv in boxes)parts.AppendLine(kv.Key+" "+kv.Value);
            File.WriteAllText(Path.GetFullPath("../../../outputs/F4EPhantom/nose-parts.txt"),parts.ToString());
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        F4EPreviewCapture.Capture();
    }
    static int Find(int[] p,int i){while(p[i]!=i){p[i]=p[p[i]];i=p[i];}return i;}
}
