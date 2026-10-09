using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class F4EImportedModelBuilder
{
    private const string ModRoot = "Assets/Blueprinter/Mods/F4EPhantom";
    private const string ImportRoot = ModRoot + "/Art/Imported";
    // A versioned destination guarantees a newly downloaded source model cannot reuse stale meshes.
    internal const string PrefabPath = ModRoot + "/F4E_Real_Model_v7.prefab";
    private const string MeshRoot = ImportRoot + "/MeshesV7";
    private const string MaterialRoot = ImportRoot + "/MaterialsV7";

    internal static GameObject CreateIfReady()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
            return existing;

        var gltfPath = Path.GetFullPath(ImportRoot + "/scene.gltf");
        var binPath = Path.GetFullPath(ImportRoot + "/scene.bin");
        if (!File.Exists(gltfPath) || !File.Exists(binPath))
            return null;

        EnsureFolder(MeshRoot);
        EnsureFolder(MaterialRoot);
        var document = JsonUtility.FromJson<GltfDocument>(File.ReadAllText(gltfPath));
        var binary = File.ReadAllBytes(binPath);
        if (document == null || document.meshes == null || document.accessors == null || document.bufferViews == null)
        {
            Debug.LogError("[F4E] The downloaded glTF model is incomplete.");
            return null;
        }

        CalculateBounds(document, binary, out var min, out var max);
        var center = (min + max) * 0.5f;
        // Kenny's mesh coordinates: X span, Y length, negative Z up.
        // The retained flight prefab uses X right, Y up, Z forward.
        var scale = 19.2f / Mathf.Max(0.001f, max.y - min.y);
        var span = (max.x - min.x) * scale;
        var height = (max.z - min.z) * scale;
        if (span < 11f || span > 12.5f || height < 4f || height > 6f)
            throw new InvalidDataException("F-4E source dimensions do not match the configured coordinate conversion.");
        var materials = CreateMaterials(document);
        var root = new GameObject("F4E_Real_Model");

        for (var meshIndex = 0; meshIndex < document.meshes.Length; meshIndex++)
        {
            var meshDefinition = document.meshes[meshIndex];
            if (meshDefinition.primitives == null)
                continue;

            for (var primitiveIndex = 0; primitiveIndex < meshDefinition.primitives.Length; primitiveIndex++)
            {
                var primitive = meshDefinition.primitives[primitiveIndex];
                if (primitive.attributes == null || primitive.attributes.POSITION < 0 || primitive.indices < 0)
                    continue;

                var mesh = BuildMesh(document, binary, primitive, center, scale, $"F4E_Real_{meshIndex}_{primitiveIndex}");
                if (mesh == null)
                    continue;

                var parts = meshIndex == 1 ? F4EGearIntegration.Split(mesh, center, scale) : new[] { mesh };
                foreach (var partMesh in parts)
                {
                    var meshPath = MeshRoot + "/" + partMesh.name + ".asset";
                    AssetDatabase.CreateAsset(partMesh, meshPath);
                    var part = new GameObject(partMesh.name);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials.TryGetValue(primitive.material, out var material) ? material : materials[-1];
                }
                if (meshIndex == 1) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[F4E] Imported CC-BY F-4E exterior model with persistent meshes.");
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static Dictionary<int, Material> CreateMaterials(GltfDocument document)
    {
        var materials = new Dictionary<int, Material>();
        var fallback = CreateMaterial("F4E_Fallback", Color.gray, null);
        materials[-1] = fallback;
        if (document.materials == null)
            return materials;

        for (var i = 0; i < document.materials.Length; i++)
        {
            var definition = document.materials[i];
            var color = Color.white;
            Texture texture = null;
            if (definition.pbrMetallicRoughness != null)
            {
                var factor = definition.pbrMetallicRoughness.baseColorFactor;
                if (factor != null && factor.Length >= 3)
                    color = new Color(factor[0], factor[1], factor[2], factor.Length > 3 ? factor[3] : 1f);
                var textureIndex = definition.pbrMetallicRoughness.baseColorTexture != null
                    ? definition.pbrMetallicRoughness.baseColorTexture.index
                    : -1;
                texture = LoadTexture(document, textureIndex);
            }
            var material = CreateMaterial("F4E_" + i, color, texture);
            if (definition.alphaMode == "BLEND")
            {
                color.a = 0.15f;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            materials[i] = material;
        }
        return materials;
    }

    private static Texture LoadTexture(GltfDocument document, int textureIndex)
    {
        if (textureIndex < 0 || document.textures == null || textureIndex >= document.textures.Length)
            return null;
        var source = document.textures[textureIndex].source;
        if (source < 0 || document.images == null || source >= document.images.Length)
            return null;
        var uri = document.images[source].uri;
        if (string.IsNullOrEmpty(uri))
            return null;
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ImportRoot + "/" + uri);
    }

    private static Material CreateMaterial(string name, Color color, Texture texture)
    {
        var path = MaterialRoot + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh BuildMesh(GltfDocument document, byte[] binary, Primitive primitive, Vector3 center, float scale, string name)
    {
        var sourcePositions = ReadVec3(document, binary, primitive.attributes.POSITION);
        if (sourcePositions == null || sourcePositions.Length == 0)
            return null;
        var vertices = new Vector3[sourcePositions.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var source = sourcePositions[i] - center;
            vertices[i] = new Vector3(source.x, -source.z, source.y) * scale;
        }

        var mesh = new Mesh { name = name };
        mesh.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = ReadIndices(document, binary, primitive.indices);
        var sourceNormals = primitive.attributes.NORMAL >= 0 ? ReadVec3(document, binary, primitive.attributes.NORMAL) : null;
        if (sourceNormals != null && sourceNormals.Length == vertices.Length)
        {
            var normals = new Vector3[sourceNormals.Length];
            for (var i = 0; i < normals.Length; i++)
                normals[i] = new Vector3(sourceNormals[i].x, -sourceNormals[i].z, sourceNormals[i].y);
            mesh.normals = normals;
        }
        else
        {
            mesh.RecalculateNormals();
        }
        if (primitive.attributes.TEXCOORD_0 >= 0)
            mesh.uv = ReadVec2(document, binary, primitive.attributes.TEXCOORD_0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CalculateBounds(GltfDocument document, byte[] binary, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var mesh in document.meshes)
        {
            if (mesh.primitives == null) continue;
            foreach (var primitive in mesh.primitives)
            {
                if (primitive.attributes == null || primitive.attributes.POSITION < 0) continue;
                foreach (var point in ReadVec3(document, binary, primitive.attributes.POSITION))
                {
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
            }
        }
    }

    private static Vector3[] ReadVec3(GltfDocument document, byte[] binary, int accessorIndex)
    {
        var accessor = document.accessors[accessorIndex];
        var view = document.bufferViews[accessor.bufferView];
        var values = new Vector3[accessor.count];
        var stride = view.byteStride > 0 ? view.byteStride : 12;
        var offset = view.byteOffset + accessor.byteOffset;
        for (var i = 0; i < values.Length; i++)
        {
            var p = offset + i * stride;
            values[i] = new Vector3(ReadFloat(binary, p), ReadFloat(binary, p + 4), ReadFloat(binary, p + 8));
        }
        return values;
    }

    private static Vector2[] ReadVec2(GltfDocument document, byte[] binary, int accessorIndex)
    {
        var accessor = document.accessors[accessorIndex];
        var view = document.bufferViews[accessor.bufferView];
        var values = new Vector2[accessor.count];
        var stride = view.byteStride > 0 ? view.byteStride : 8;
        var offset = view.byteOffset + accessor.byteOffset;
        for (var i = 0; i < values.Length; i++)
        {
            var p = offset + i * stride;
            // glTF image coordinates start at the top; Unity textures start at the bottom.
            values[i] = new Vector2(ReadFloat(binary, p), 1f - ReadFloat(binary, p + 4));
        }
        return values;
    }

    private static int[] ReadIndices(GltfDocument document, byte[] binary, int accessorIndex)
    {
        var accessor = document.accessors[accessorIndex];
        var view = document.bufferViews[accessor.bufferView];
        var values = new int[accessor.count];
        var elementSize = accessor.componentType == 5125 ? 4 : 2;
        var stride = view.byteStride > 0 ? view.byteStride : elementSize;
        var offset = view.byteOffset + accessor.byteOffset;
        for (var i = 0; i < values.Length; i++)
        {
            var p = offset + i * stride;
            values[i] = accessor.componentType == 5125 ? (int)BitConverter.ToUInt32(binary, p) : BitConverter.ToUInt16(binary, p);
        }
        return values;
    }

    private static float ReadFloat(byte[] bytes, int offset) => BitConverter.ToSingle(bytes, offset);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    [Serializable] private class GltfDocument { public Accessor[] accessors; public BufferView[] bufferViews; public GltfMesh[] meshes; public GltfMaterial[] materials; public GltfTexture[] textures; public GltfImage[] images; }
    [Serializable] private class Accessor { public int bufferView; public int byteOffset; public int componentType; public int count; }
    [Serializable] private class BufferView { public int byteOffset; public int byteStride; }
    [Serializable] private class GltfMesh { public Primitive[] primitives; }
    [Serializable] private class Primitive { public Attributes attributes; public int indices = -1; public int material = -1; }
    [Serializable] private class Attributes { public int POSITION = -1; public int NORMAL = -1; public int TEXCOORD_0 = -1; }
    [Serializable] private class GltfMaterial { public Pbr pbrMetallicRoughness; public string alphaMode; }
    [Serializable] private class Pbr { public float[] baseColorFactor; public TextureInfo baseColorTexture; }
    [Serializable] private class TextureInfo { public int index = -1; }
    [Serializable] private class GltfTexture { public int source = -1; }
    [Serializable] private class GltfImage { public string uri; }
}
