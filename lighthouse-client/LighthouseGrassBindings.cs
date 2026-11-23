#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using GPUInstancer;
using UnityEngine;

namespace Manimal.Lighthouse.Client;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
internal static class LighthouseGrassBindings
{
#pragma warning disable CS0649
    [Serializable]
    public sealed class AssetBinding
    {
        public string Field, Asset;
    }
    
    [Serializable]
    public sealed class ObjectData
    {
        public string Id, Type, Name, Json; 
        public AssetBinding[] Textures;
    }
    
    [Serializable]
    public sealed class ManagerData
    {
        public string Terrain, Json, Settings; 
        public string[] Prototypes;
    }
    
    [Serializable]
    public sealed class Settings
    {
        public int Version; public ObjectData[] Objects; 
        public ManagerData[] Managers;
    }
#pragma warning restore CS0649
    
    private static Settings _settings;
    
    private static readonly Dictionary<string, ScriptableObject> Objects = new();
    private static readonly List<GPUInstancerDetailManager> Managers = [];
    private static readonly HashSet<Terrain> Restored = [];
    public static int ManagerCount => Managers.Count;
    internal static List<GPUInstancerDetailManager> ActiveManagers => Managers;
    internal static IEnumerable<ScriptableObject> Instances => Objects.Values;

    public static void Load(AssetBundle bundle, Action<Material> bindMaterial)
    {
        var json = bundle.LoadAsset<TextAsset>("grass.json");

        if (!json)
        {
            throw new InvalidOperationException("Lighthouse grass binding table is missing.");
        }

        _settings = JsonUtility.FromJson<Settings>(json.text);

        if (_settings.Version != 1 || _settings.Managers.Length != 12)
        {
            throw new InvalidOperationException("Unexpected grass schema.");
        }

        foreach (var data in _settings.Objects)
        {
            ScriptableObject instance = data.Type switch
            {
                nameof(GPUInstancerDetailPrototype) => ScriptableObject.CreateInstance<GPUInstancerDetailPrototype>(),
                nameof(GPUInstancerTerrainSettings) => ScriptableObject.CreateInstance<GPUInstancerTerrainSettings>(),
                _ => throw new InvalidOperationException("Unsupported grass object type: " + data.Type)
            };

            Objects.Add(data.Id, instance);
            JsonUtility.FromJsonOverwrite(data.Json, instance);
            instance.name = data.Name;

            foreach (var binding in data.Textures)
            {
                var field = instance.GetType().GetField(binding.Field, BindingFlags.Instance | BindingFlags.Public);

                if (field == null || !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    throw new InvalidOperationException("Invalid grass asset field: " + binding.Field);
                }

                var asset = bundle.LoadAsset(binding.Asset, field.FieldType);

                if (!asset)
                {
                    throw new InvalidOperationException("Missing grass asset: " + binding.Asset);
                }

                field.SetValue(instance, asset);

                switch (asset)
                {
                    case GameObject prefab:
                    {
                        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                        {
                            foreach (var material in renderer.sharedMaterials)
                            {
                                if (material)
                                {
                                    bindMaterial(material);
                                }
                            }
                        }

                        break;
                    }
                    case Material material:
                    {
                        bindMaterial(material);
                        break;
                    }
                }
            }
        }

        _settings.Objects = null;
        Resources.UnloadAsset(json);
    }

    public static int Restore(Terrain terrain, bool activate = true)
    {
        if (_settings == null || !terrain || Restored.Contains(terrain))
        {
            return 0;
        }

        var count = 0;

        foreach (var data in _settings.Managers)
        {
            if (data.Terrain != terrain.name)
            {
                continue;
            }

            if (!terrain.terrainData || terrain.terrainData.detailResolution == 0)
            {
                throw new InvalidOperationException("Terrain has no grass placement data: " + terrain.name);
            }

            var go = new GameObject("Manimal grass " + terrain.name);

            go.SetActive(false);
            go.transform.SetParent(terrain.transform, false);

            var manager = go.AddComponent<GPUInstancerDetailManager>();

            Managers.Add(manager);
            JsonUtility.FromJsonOverwrite(data.Json, manager);
            manager._terrain = terrain;
            manager.terrainSettings = (GPUInstancerTerrainSettings)Objects[data.Settings];
            manager.prototypeList = new List<GPUInstancerPrototype>(data.Prototypes.Length);

            foreach (var id in data.Prototypes)
            {
                var prototype = (GPUInstancerDetailPrototype)Objects[id];
                
                var density = prototype.cachedDensityMapForInstance;
                var side = density == null ? 0 : (int)Math.Sqrt(density.Length);

                if (side == 0 || side * side != density?.Length)
                {
                    throw new InvalidOperationException("Grass prototype has no valid cached placement map: " + prototype.name);
                }

                if (prototype.usePrototypeMesh ? !prototype.prefabObject : !prototype.prototypeTexture)
                {
                    throw new InvalidOperationException("Grass prototype has no render asset: " + prototype.name);
                }

                manager.prototypeList.Add(prototype);
            }

            manager.cameraData = new GPUInstancerCameraData();
            manager.isInitialized = false;
            go.name += manager.IsOptic ? " (optic)" : " (main)";

            var proxy = terrain.GetComponent<GPUInstancerTerrainProxy>();

            if (!proxy)
            {
                proxy = terrain.gameObject.AddComponent<GPUInstancerTerrainProxy>();
            }

            if (manager.IsOptic)
            {
                proxy.detailManagerOptic = manager;
            }
            else
            {
                proxy.detailManager = manager;
            }

            if (activate)
            {
                go.SetActive(true);
            }

            count++;
        }

        if (count != 0 && count != 2)
        {
            throw new InvalidOperationException("Incomplete grass camera pair for " + terrain.name);
        }

        if (count == 2)
        {
            Restored.Add(terrain);
        }

        return count;
    }

    public static void Validate()
    {
        if (_settings == null)
        {
            return;
        }

        if (Managers.Count != 12 || Restored.Count != 6)
        {
            throw new InvalidOperationException("Grass restoration is incomplete: " + Managers.Count + "/12 managers.");
        }

        foreach (var terrain in Restored)
        {
            var proxy = terrain.GetComponent<GPUInstancerTerrainProxy>();

            if (!proxy || !proxy.detailManager || !proxy.detailManagerOptic)
            {
                throw new InvalidOperationException("Missing grass proxy camera pair.");
            }
        }
    }

    public static string Diagnostics()
    {
        int initialized = 0, prototypes = 0, runtime = 0;

        foreach (var manager in Managers)
        {
            if (!manager)
            {
                continue;
            }

            if (manager.isInitialized)
            {
                initialized++;
            }

            prototypes += manager.prototypeList.Count;

            if (manager.runtimeDataList != null)
            {
                runtime += manager.runtimeDataList.Count;
            }
        }

        return "Lighthouse grass: " + Managers.Count + " managers, " + initialized + " initialized, " + prototypes + " prototypes, " + runtime + " runtime entries.";
    }

    public static void Clear()
    {
        Managers.Clear();
        Restored.Clear();

        foreach (var instance in Objects.Values)
        {
            if (instance)
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        Objects.Clear();
        _settings = null;
    }
}