using F4EPhantom;
using Blueprinter;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class F4EPrototypeFactory
{
    private const string ModRoot = "Assets/Blueprinter/Mods/F4EPhantom";
    private const string ArtRoot = ModRoot + "/Art";
    private const string PrefabPath = ModRoot + "/F4E_Phantom_II_Visual.prefab";
    private const string CamouflagePath = ArtRoot + "/F4E_SEA_Camouflage.png";
    private const string IconPath = ArtRoot + "/F4E_Phantom_Icon.png";
    private const string GeometryRoot = ArtRoot + "/Geometry";

    [InitializeOnLoadMethod]
    private static void ScheduleCreation()
    {
        EditorApplication.delayCall += CreateIfReady;
    }

    [MenuItem("F4E/Create Visual Prototype")]
    internal static void CreateIfReady()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            EnsurePersistentVisualMeshes();
            return;
        }

        if (EditorApplication.isCompiling || AssetDatabase.LoadAssetAtPath<Texture2D>(CamouflagePath) == null)
            return;

        ConfigureTexture();
        ConfigureIconTexture();
        var airframe = GetOrCreateMaterial("F4E_SEA_Airframe.mat", new Color(1f, 1f, 1f), AssetDatabase.LoadAssetAtPath<Texture2D>(CamouflagePath), 0.42f);
        var canopy = GetOrCreateMaterial("F4E_Canopy.mat", new Color(0.12f, 0.26f, 0.29f, 0.72f), null, 0.78f);
        var cockpit = GetOrCreateMaterial("F4E_Cockpit.mat", new Color(0.055f, 0.065f, 0.06f), null, 0.28f);
        var instrument = GetOrCreateMaterial("F4E_Instruments.mat", new Color(0.08f, 0.34f, 0.12f), null, 0.38f);

        var root = new GameObject("F4E_Phantom_II");
        var blockout = root.AddComponent<F4EBlockoutBuilder>();
        SetReference(blockout, "airframeMaterial", airframe);
        SetReference(blockout, "canopyMaterial", canopy);
        blockout.Build();

        var cockpitBuilder = root.AddComponent<F4ECockpitBuilder>();
        SetReference(cockpitBuilder, "cockpitMaterial", cockpit);
        SetReference(cockpitBuilder, "instrumentMaterial", instrument);
        SetReference(cockpitBuilder, "glassMaterial", canopy);
        cockpitBuilder.Build();

        root.AddComponent<F4EFlightVisualController>();
        root.AddComponent<F4EJetAudioSynth>();
        root.AddComponent<F4EHeadsUpDisplay>();
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.3f, 0f);
        collider.size = new Vector3(19.2f, 4.5f, 11.8f);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnsurePersistentVisualMeshes();
        Debug.Log("[F4E] Created original F-4E visual prototype.");
    }

    private static void EnsurePersistentVisualMeshes()
    {
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (visualPrefab == null || AssetDatabase.LoadAssetAtPath<Mesh>(GeometryRoot + "/F4E_Fuselage.asset") != null)
            return;

        if (!AssetDatabase.IsValidFolder(GeometryRoot))
            AssetDatabase.CreateFolder(ArtRoot, "Geometry");

        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var blockout = contents.GetComponent<F4EBlockoutBuilder>();
            var cockpit = contents.GetComponent<F4ECockpitBuilder>();
            if (blockout == null || cockpit == null)
            {
                Debug.LogError("[F4E] Visual prefab is missing its geometry builders.");
                return;
            }

            blockout.Build();
            cockpit.Build();
            foreach (var filter in contents.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh) || !filter.name.StartsWith("F4E_"))
                    continue;

                var path = GeometryRoot + "/" + filter.name + ".asset";
                AssetDatabase.CreateAsset(mesh, path);
                filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[F4E] Saved persistent F-4E geometry assets for runtime loading.");
    }

    [MenuItem("F4E/Build Test Package")]
    private static void BuildCompleteMod()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Blueprinter/_donotship"))
        {
            Debug.LogError("[F4E] Import game assets through Blueprinter Project Setup before building the mod.");
            return;
        }

        F4ETestBuild.Build();
    }

    internal static void CreateGameplayMod()
    {
        var baseDefinition = FindBaseDefinition();
        var realModelPrefab = F4EImportedModelBuilder.CreateIfReady();
        if (baseDefinition == null || realModelPrefab == null)
            throw new System.InvalidOperationException("Missing base aircraft or imported F-4E model; generation aborted.");

        var gameplayPrefabPath = ModRoot + "/F4E_Phantom_II.prefab";
        var gameplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gameplayPrefabPath);
        if (gameplayPrefab == null)
        {
            var serializedBase = new SerializedObject(baseDefinition);
            var basePrefab = serializedBase.FindProperty("unitPrefab").objectReferenceValue as GameObject;
            if (basePrefab == null)
            {
                throw new System.InvalidOperationException("The FS-12 base definition has no usable flight prefab.");
            }

            var instance = Object.Instantiate(basePrefab);
            if (instance == null)
            {
                throw new System.InvalidOperationException("The FS-12 flight prefab could not be instantiated.");
            }
            instance.name = "F4E_Phantom_II";
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            var visual = PrefabUtility.InstantiatePrefab(realModelPrefab) as GameObject;
            if (visual == null)
            {
                Object.DestroyImmediate(instance);
                throw new System.InvalidOperationException("The imported F-4E exterior could not be instantiated.");
            }
            visual.transform.SetParent(instance.transform, false);
            visual.name = "F4E_Imported_Visual";
            RemoveMissingScripts(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, gameplayPrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.ImportAsset(gameplayPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            gameplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gameplayPrefabPath);
        }

        if (gameplayPrefab == null)
        {
            throw new System.InvalidOperationException("Unable to create the F-4E gameplay prefab; build aborted.");
        }

        var definitionPath = ModRoot + "/F4E_Phantom_II.asset";
        var definition = AssetDatabase.LoadMainAssetAtPath(definitionPath);
        if (definition == null)
        {
            definition = Object.Instantiate(baseDefinition);
            definition.name = "F4E_Phantom_II";
            AssetDatabase.CreateAsset(definition, definitionPath);
        }

        ConfigureDefinition(definition, gameplayPrefab);
        ConfigureGameplayAircraft(gameplayPrefabPath, definition);
        RegisterAtHangars();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        OpReferenceIndex.Refresh();
    }

    private static void ConfigureTexture()
    {
        var importer = AssetImporter.GetAtPath(CamouflagePath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    private static void ConfigureIconTexture()
    {
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static Material GetOrCreateMaterial(string fileName, Color color, Texture texture, float smoothness)
    {
        var path = ArtRoot + "/" + fileName;
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemoveMissingScripts(GameObject root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
    }

    private static void SetReference(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Object FindBaseDefinition()
    {
        var knownFs12 = AssetDatabase.LoadMainAssetAtPath("Assets/Blueprinter/_donotship/MonoBehaviour/FS-12_PLACEHOLDER.asset");
        if (knownFs12 != null)
            return knownFs12;

        foreach (var guid in AssetDatabase.FindAssets("FS-12", new[] { "Assets/Blueprinter/_donotship" }))
        {
            var candidate = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate != null && candidate.GetType().Name == "AircraftDefinition")
                return candidate;
        }

        return null;
    }

    private static void ConfigureDefinition(Object definition, GameObject gameplayPrefab)
    {
        var serialized = new SerializedObject(definition);
        SetString(serialized, "jsonKey", "F4EPhantom");
        SetString(serialized, "unitName", "F-4E Phantom II");
        SetString(serialized, "code", "F-4E");
        SetString(serialized, "description", "F-4E Phantom II. Experimental integration with an imported exterior and adapted native flight systems.");
        SetFloat(serialized, "length", 19.2f);
        SetFloat(serialized, "width", 11.78f);
        SetFloat(serialized, "height", 5.15f);
        SetFloat(serialized, "mass", 13750f);
        SetFloat(serialized, "value", 40f);
        var parametersProperty = serialized.FindProperty("aircraftParameters");
        var parametersPath = ModRoot + "/F4E_Parameters.asset";
        var parameters = AssetDatabase.LoadMainAssetAtPath(parametersPath);
        if (parameters == null)
        {
            if (parametersProperty == null || parametersProperty.objectReferenceValue == null)
                throw new System.InvalidOperationException("Missing base aircraft parameters.");
            parameters = Object.Instantiate(parametersProperty.objectReferenceValue);
            parameters.name = "F4E_Parameters";
            AssetDatabase.CreateAsset(parameters, parametersPath);
        }
        var parametersData = new SerializedObject(parameters);
        parametersData.FindProperty("aircraftName").stringValue = "F-4E Phantom II";
        parametersData.FindProperty("rankRequired").intValue = 3;
        parametersData.ApplyModifiedPropertiesWithoutUndo();
        parametersProperty.objectReferenceValue = parameters;
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        SetObject(serialized, "friendlyIcon", icon);
        SetObject(serialized, "hostileIcon", icon);
        SetObject(serialized, "mapIcon", icon);
        var prefab = serialized.FindProperty("unitPrefab");
        if (prefab != null) prefab.objectReferenceValue = gameplayPrefab;
        var info = serialized.FindProperty("aircraftInfo");
        if (info != null)
        {
            SetFloat(info, "emptyWeight", 13750f);
            SetFloat(info, "maxSpeed", 1240f);
            SetFloat(info, "stallSpeed", 250f);
            SetFloat(info, "maneuverability", 7f);
            SetFloat(info, "maxWeight", 23000f);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureGameplayAircraft(string gameplayPrefabPath, Object definition)
    {
        var contents = PrefabUtility.LoadPrefabContents(gameplayPrefabPath);
        try
        {
            var updatedDefinition = false;
            foreach (var component in contents.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "Aircraft")
                    continue;

                var serializedAircraft = new SerializedObject(component);
                var definitionProperty = serializedAircraft.FindProperty("definition");
                if (definitionProperty == null)
                    continue;

                definitionProperty.objectReferenceValue = definition;
                serializedAircraft.ApplyModifiedPropertiesWithoutUndo();
                updatedDefinition = true;
            }

            var realModelPrefab = F4EImportedModelBuilder.CreateIfReady();
            if (realModelPrefab == null)
            {
                throw new System.InvalidOperationException("[F4E] Imported F-4E exterior model is unavailable.");
            }

            var prototypeVisual = contents.transform.Find("F4E_Original_Visual");
            if (prototypeVisual != null)
                prototypeVisual.gameObject.SetActive(false);

            F4EGearIntegration.ClearPrevious(contents.transform);
            var importedVisual = contents.transform.Find("F4E_Imported_Visual");
            if (importedVisual != null)
                Object.DestroyImmediate(importedVisual.gameObject);

            var instance = PrefabUtility.InstantiatePrefab(realModelPrefab) as GameObject;
            if (instance == null)
            {
                throw new System.InvalidOperationException("[F4E] Failed to instantiate the imported F-4E exterior model.");
            }
            instance.transform.SetParent(contents.transform, false);
            instance.name = "F4E_Imported_Visual";
            importedVisual = instance.transform;

            ConfigureF4WeaponStations(contents.transform);
            ConfigureF4LandingGear(contents.transform);
            F4EGearIntegration.Bind(contents.transform, instance);

            foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = renderer.transform.IsChildOf(importedVisual)
                    || renderer.gameObject.layer == 3
                    || renderer.name.StartsWith("F4E_Gear_");

            if (!updatedDefinition)
                throw new System.InvalidOperationException("[F4E] Gameplay prefab has no serializable Aircraft definition reference.");

            F4EViewIntegration.Configure(contents, importedVisual);
            PrefabUtility.SaveAsPrefabAsset(contents, gameplayPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void AlignWeaponStations(Transform aircraftRoot)
    {
        // The retained FS-12 flight prefab is slightly shorter and narrower than an F-4E.
        // Move only the weapon stations, once, while retaining their game-provided logic.
        if (aircraftRoot.Find("F4E_AttachmentLayoutV2") != null)
            return;

        const float lengthRatio = 19.2f / 15.8f;
        const float spanRatio = 11.7f / 11f;
        foreach (var station in aircraftRoot.GetComponentsInChildren<Transform>(true))
        {
            var name = station.name.ToLowerInvariant();
            if (!name.Contains("hardpoint") && !name.Contains("pylon"))
                continue;

            var position = aircraftRoot.InverseTransformPoint(station.position);
            position.x *= lengthRatio;
            position.z *= spanRatio;
            station.position = aircraftRoot.TransformPoint(position);
        }

        var marker = new GameObject("F4E_AttachmentLayoutV2");
        marker.transform.SetParent(aircraftRoot, false);
    }

    private static void ConfigureF4WeaponStations(Transform aircraftRoot)
    {
        // Stations are specified in the F-4E's X-length / Y-height / Z-span space.
        // The inherited weapon logic remains unchanged; only its physical mount positions move.
        // Candidate layout fitted to the imported underwing rails. Weapon-specific
        // rack offsets and release clearance still require an in-game loadout test.
        SetStationPosition(aircraftRoot, "hardpoint_wingtip_L", new Vector3(-0.55f, -1.20f, 2.34f));
        SetStationPosition(aircraftRoot, "hardpoint_wingtip_R", new Vector3(-0.55f, -1.20f, -2.34f));
        SetStationPosition(aircraftRoot, "pylon_wing_L", new Vector3(1.35f, -0.95f, 3.80f));
        SetStationPosition(aircraftRoot, "pylon_wing_R", new Vector3(1.35f, -0.95f, -3.80f));
        SetStationPosition(aircraftRoot, "weaponbayMount_L", new Vector3(-0.70f, -1.40f, 0.55f));
        SetStationPosition(aircraftRoot, "weaponbayMount_R", new Vector3(-0.70f, -1.40f, -0.55f));
        foreach (var component in aircraftRoot.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.GetType().Name != "WeaponManager") continue;
            var serialized = new SerializedObject(component);
            var sets = serialized.FindProperty("hardpointSets");
            if (sets == null || sets.arraySize != 4) throw new System.InvalidOperationException("Unexpected native hardpoint layout.");
            var labels = new[] { "Internal Cannon", "Fuselage Stations", "Outer Wing Pylons", "Inner Wing Rails" };
            for (var i = 0; i < sets.arraySize; i++)
            {
                var set = sets.GetArrayElementAtIndex(i);
                set.FindPropertyRelative("name").stringValue = labels[i];
                if (i != 1) continue;
                var points = set.FindPropertyRelative("hardpoints");
                for (var j = 0; j < points.arraySize; j++)
                {
                    var point = points.GetArrayElementAtIndex(j);
                    point.FindPropertyRelative("bayDoors").arraySize = 0;
                    point.FindPropertyRelative("doorOpenDuration").floatValue = 0f;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetStationPosition(Transform aircraftRoot, string stationName, Vector3 localPosition)
    {
        foreach (var candidate in aircraftRoot.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name != stationName)
                continue;

            // Legacy layout constants use negative X forward and positive Z left.
            // The actual flight prefab uses positive Z forward and negative X left.
            candidate.position = aircraftRoot.TransformPoint(new Vector3(-localPosition.z, localPosition.y, -localPosition.x));
            candidate.rotation = aircraftRoot.rotation;
            return;
        }

        throw new System.InvalidOperationException("[F4E] Missing expected weapon station: " + stationName);
    }

    private static void ConfigureF4LandingGear(Transform aircraftRoot)
    {
        F4EGearIntegration.AlignContacts(aircraftRoot);
    }

    private static void RegisterAtHangars()
    {
        var opPath = ModRoot + "/Ops/OpAddF4EToHangars.asset";
        var op = AssetDatabase.LoadAssetAtPath<OpAddAircraftToHangars>(opPath);
        if (op == null)
        {
            if (!AssetDatabase.IsValidFolder(ModRoot + "/Ops"))
                AssetDatabase.CreateFolder(ModRoot, "Ops");
            op = ScriptableObject.CreateInstance<OpAddAircraftToHangars>();
            AssetDatabase.CreateAsset(op, opPath);
        }

        op.aircraftJsonKey = "F4EPhantom";
        op.hangars = new List<OpAddAircraftToHangars.HangarTarget>();
        var index = OpReferenceIndex.Load();
        if (index != null)
        {
            foreach (var hangar in index.HangarUnits)
            {
                op.hangars.Add(new OpAddAircraftToHangars.HangarTarget
                {
                    hangarUnitJsonKey = hangar.jsonKey,
                    hangarNames = new List<string>(hangar.hangarNames)
                });
            }
        }

        EditorUtility.SetDirty(op);
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null) property.stringValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedProperty parent, string propertyName, float value)
    {
        var property = parent.FindPropertyRelative(propertyName);
        if (property != null) property.floatValue = value;
    }
}
