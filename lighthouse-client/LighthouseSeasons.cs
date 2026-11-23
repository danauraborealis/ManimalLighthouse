#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.DependencyManager;
using Diz.Resources;
using EFT;
using GPUInstancer;
using HarmonyLib;
using UnityEngine;
using JBooth.MicroSplat;

namespace Manimal.Lighthouse.Client;

// SPT's season fixer, its tree/plant map and its grass slices all lived in the replaced Terrain scene,
// and the rework renders with bundle copies of the materials anyway. this applies SPT's map to its own
// materials, copies the result onto the rework copies by name and reseasons the GPUI grass.
internal static class LighthouseSeasons
{
#pragma warning disable CS0649
    [Serializable]
    private sealed class SeasonData
    {
        public string Texture, TextureRcid, Bump, BumpRcid;
        public Color Healthy, Dry, WindTint;
    }

    [Serializable]
    private sealed class GrassEntry
    {
        public string Key;
        public SeasonData Summer, Autumn, AutumnLate, Spring, SpringEarly, Winter;
    }

    [Serializable]
    private sealed class Table
    {
        public int Version;
        public GrassEntry[] Grass;
    }
#pragma warning restore CS0649

    private static readonly string[] TextureProperties =
    [
        "_MainTex", "_BumpMap", "_DetailTex", "_SpecMap", "_MainTexArray", "_NormalMapArray",
        "_MainTex0", "_MainTex1", "_MainTex2", "_BumpMap0", "_BumpMap1", "_BumpMap2"
    ];

    private static readonly string[] ColorProperties =
    [
        "_Color", "_HueVariation", "_HueVariationColor", "_BaseTintColor", "_DryColor", "_HealthyColor",
        "_WindWaveTintColor", "_Color0", "_Color1", "_Color2"
    ];

    private static readonly string[] SeasonSuffixes = ["_autumn_late", "_autumn", "_spring_early", "_spring", "_winter"];

    private static readonly AccessTools.FieldRef<SeasonsMaterial, ISeasonMaterial> LoadedMaterial =
        AccessTools.FieldRefAccess<SeasonsMaterial, ISeasonMaterial>("_loadedMaterial");

    private static readonly Dictionary<Type, FieldInfo> MaterialFields = new();
    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private static Table _table;
    private static AssetBundle _bundle;
    private static SeasonsMaterialsMap _map;
    private static DependencyGraph<IEasyBundle>.Token _token;

    // chained onto SeasonsController.Run so the raid start waits for the grass bundles like vanilla does
    internal static async Task After(Task original, ESeason season)
    {
        await original;

        try
        {
            await Apply(season);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Lighthouse seasons: update failed: " + e);
        }
    }

    private static async Task Apply(ESeason season)
    {
        if (LighthouseHeadless.Active || LighthouseGrassBindings.ManagerCount == 0)
        {
            return;
        }

        // vanilla sets this from SeasonsMaterialsTerrain, which only runs through the missing fixer
        MicroSplatObject.currentSeason = season switch
        {
            ESeason.Winter => TextureArrayConfig.Season.Winter,
            ESeason.Autumn => TextureArrayConfig.Season.Autumn,
            ESeason.AutumnLate => TextureArrayConfig.Season.AutumnLate,
            ESeason.Spring => TextureArrayConfig.Season.Spring,
            ESeason.SpringEarly => TextureArrayConfig.Season.SpringEarly,
            _ => TextureArrayConfig.Season.Summer
        };

        try
        {
            LighthouseTerrainBindings.Refresh();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Lighthouse seasons: terrain refresh failed: " + e);
        }

        // the rework already ships its summer look; SPT's summer data would only regress it
        if (season == ESeason.Summer)
        {
            Plugin.Log.LogInfo("Lighthouse seasons: Summer; rework vegetation left as authored.");
            return;
        }

        var report = new StringBuilder("Lighthouse seasons: " + season);

        try
        {
            var map = LoadMap();
            await Load(map, season);
            map.Fix();
            FixMaterials(report, map);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Lighthouse seasons: vegetation material update failed: " + e);
        }

        try
        {
            await LoadGrass(season);
            FixGrass(report, season);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Lighthouse seasons: grass update failed: " + e);
        }

        Plugin.Log.LogInfo(report.ToString());
    }

    // SPT's Terrain scene held the only reference to this map, so it is reached through a one-entry
    // bundle pointing into the verified native sharedassets200
    private static SeasonsMaterialsMap LoadMap()
    {
        if (_map)
        {
            return _map;
        }

        if (!_bundle)
        {
            using var stream = typeof(LighthouseSeasons).Assembly.GetManifestResourceStream("Manimal.Lighthouse.Seasons.bundle")
                ?? throw new InvalidOperationException("Lighthouse season bundle is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _bundle = AssetBundle.LoadFromMemory(memory.ToArray());

            if (!_bundle)
            {
                throw new InvalidOperationException("Lighthouse season bundle failed to load.");
            }
        }

        var map = _bundle.LoadAsset<SeasonsMaterialsMap>("seasons");

        if (!map || map.name != "SeasonsMaterials_Lighthouse" || map.Groups == null)
        {
            throw new InvalidOperationException("Native Lighthouse season map did not resolve.");
        }

        _map = map;
        return map;
    }

    private static Task Load(SeasonsMaterialsMap map, ESeason season) => season switch
    {
        ESeason.Winter => map.LoadWinter(),
        ESeason.Autumn => map.LoadAutumn(),
        ESeason.AutumnLate => map.LoadAutumnLate(),
        ESeason.Spring => map.LoadSpring(),
        ESeason.SpringEarly => map.LoadSpringEarly(),
        _ => map.LoadSummer()
    };

    private static async Task LoadGrass(ESeason season)
    {
        var keys = new List<string>();

        foreach (var entry in LoadTable().Grass)
        {
            var data = Select(entry, season);

            if (!string.IsNullOrEmpty(data.Texture)) { keys.Add(data.Texture); }
            if (!string.IsNullOrEmpty(data.Bump)) { keys.Add(data.Bump); }
        }

        var assets = Singleton<IEasyAssets>.Instance;
        var previous = _token;
        _token = assets.Retain(keys);
        Textures.Clear();
        await EasyAssetsExtensions.LoadBundles(_token);
        previous?.Release();

        foreach (var entry in LoadTable().Grass)
        {
            var data = Select(entry, season);
            Fetch(assets, data.Texture, data.TextureRcid);
            Fetch(assets, data.Bump, data.BumpRcid);
        }
    }

    private static void Fetch(IEasyAssets assets, string path, string rcid)
    {
        if (string.IsNullOrEmpty(path) || Textures.ContainsKey(path))
        {
            return;
        }

        var texture = assets.GetAsset<Texture2D>(new ResourceKey { path = path, rcid = rcid });

        if (texture)
        {
            Textures[path] = texture;
        }
    }

    // the grass prototypes are destroyed with the scenes first, so the retained textures can go;
    // Unload(false) leaves the borrowed native season objects alone
    internal static void Clear()
    {
        Textures.Clear();
        _map = null;

        if (_token != null)
        {
            _token.Release();
            _token = null;
        }

        if (_bundle)
        {
            _bundle.Unload(false);
            _bundle = null;
        }
    }

    private static void FixMaterials(StringBuilder report, SeasonsMaterialsMap map)
    {
        // speedtree reuses generic names (Branches_0, Fronds_1, Leaves_1) in every species, so one name
        // maps to several SPT materials and the base texture picks the species
        var natives = new Dictionary<string, List<Material>>(StringComparer.Ordinal);
        Collect(map, natives, new HashSet<object>());

        var nativeSet = new HashSet<Material>();
        var nativeCount = 0;

        foreach (var list in natives.Values)
        {
            foreach (var native in list)
            {
                nativeSet.Add(native);
                nativeCount++;
            }
        }
        int updated = 0, skipped = 0;
        var skippedNames = new List<string>();
        var unmatched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (!material || nativeSet.Contains(material) || !material.shader)
            {
                continue;
            }

            var name = BaseName(material.name);

            Material native = null;
            string reason = null;
            int best = -1, candidates = 0, viable = 0;

            if (natives.TryGetValue(name, out var list))
            {
                foreach (var candidate in list)
                {
                    if (!candidate.shader || candidate.shader.name != material.shader.name)
                    {
                        continue;
                    }

                    candidates++;
                    var score = Score(candidate, material, out var why);

                    if (score < 0)
                    {
                        reason ??= why;
                        continue;
                    }

                    viable++;

                    if (score > best)
                    {
                        best = score;
                        native = candidate;
                    }
                }
            }

            if (candidates == 0)
            {
                if (material.shader.name.IndexOf("SpeedTree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    material.shader.name.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    unmatched.Add(name);
                }

                continue;
            }

            // no texture evidence and more than one species to choose from: don't guess
            if (native && best == 0 && viable > 1)
            {
                native = null;
                reason = "ambiguous between " + viable + " species";
            }

            if (!native)
            {
                skipped++;

                if (skippedNames.Count < 12)
                {
                    skippedNames.Add(name + " (" + reason + ")");
                }

                continue;
            }

            foreach (var property in TextureProperties)
            {
                if (native.HasProperty(property) && material.HasProperty(property) && native.GetTexture(property))
                {
                    material.SetTexture(property, native.GetTexture(property));
                }
            }

            foreach (var property in ColorProperties)
            {
                if (native.HasProperty(property) && material.HasProperty(property))
                {
                    material.SetColor(property, native.GetColor(property));
                }
            }

            updated++;
        }

        report.Append("; vegetation materials: ").Append(nativeCount).Append(" native seasonal, ")
            .Append(updated).Append(" rework copies updated, ").Append(skipped).Append(" skipped");

        if (skippedNames.Count != 0)
        {
            report.Append(" [").Append(string.Join(", ", skippedNames)).Append(']');
        }

        if (unmatched.Count != 0)
        {
            var names = new List<string>(unmatched);
            names.Sort(StringComparer.Ordinal);

            if (names.Count > 20)
            {
                names.RemoveRange(20, names.Count - 20);
            }

            report.Append("; foliage without native season data: ").Append(unmatched.Count).Append(" [").Append(string.Join(", ", names)).Append(']');
        }
    }

    // -1 when a seasonal texture would land on a different base texture (wrong species, or retail
    // retextured it); otherwise how many base textures positively match
    private static int Score(Material native, Material material, out string reason)
    {
        var matched = 0;

        foreach (var property in TextureProperties)
        {
            if (!native.HasProperty(property) || !material.HasProperty(property))
            {
                continue;
            }

            var seasonal = native.GetTexture(property);

            if (!seasonal)
            {
                continue;
            }

            var current = material.GetTexture(property);

            if (current && (current == seasonal || current.name == seasonal.name || current.name == StripSeason(seasonal.name)))
            {
                matched++;
                continue;
            }

            reason = property + " " + (current ? current.name : "none") + " vs " + seasonal.name;
            return -1;
        }

        reason = null;
        return matched;
    }

    private static void Collect(object node, Dictionary<string, List<Material>> natives, HashSet<object> seen)
    {
        if (node == null || !seen.Add(node))
        {
            return;
        }

        switch (node)
        {
            case SeasonsMaterialsMap map when map.Groups != null:
            {
                foreach (var group in map.Groups)
                {
                    Collect(group, natives, seen);
                }

                break;
            }
            case SeasonsMaterialsGroup group when group.Materials != null:
            {
                foreach (var material in group.Materials)
                {
                    Collect(material, natives, seen);
                }

                break;
            }
            case SeasonsMaterial seasons:
            {
                var loaded = LoadedMaterial(seasons);

                if (loaded == null)
                {
                    break;
                }

                var type = loaded.GetType();

                if (!MaterialFields.TryGetValue(type, out var field))
                {
                    field = type.GetField("Material", BindingFlags.Instance | BindingFlags.Public);
                    MaterialFields[type] = field != null && field.FieldType == typeof(Material) ? field : null;
                    field = MaterialFields[type];
                }

                if (field?.GetValue(loaded) is Material material && material)
                {
                    var name = BaseName(material.name);

                    if (!natives.TryGetValue(name, out var list))
                    {
                        natives[name] = list = new List<Material>();
                    }

                    if (!list.Contains(material))
                    {
                        list.Add(material);
                    }
                }

                break;
            }
        }
    }

    private static void FixGrass(StringBuilder report, ESeason season)
    {
        var entries = new Dictionary<string, SeasonData>(StringComparer.Ordinal);

        foreach (var entry in LoadTable().Grass)
        {
            entries[entry.Key] = Select(entry, season);
        }

        int total = 0, changed = 0, textured = 0;
        var missing = new List<string>();

        foreach (var instance in LighthouseGrassBindings.Instances)
        {
            if (instance is not GPUInstancerDetailPrototype prototype)
            {
                continue;
            }

            // mesh details (the rocks) have no season data in SPT or retail
            if (prototype.usePrototypeMesh)
            {
                continue;
            }

            total++;

            if (!entries.TryGetValue(PrototypeKey(prototype.name), out var data))
            {
                missing.Add(prototype.name);
                continue;
            }

            // same rules as SeasonsGrass.Fix: wind tint only moves in winter
            prototype.detailDryColor = data.Dry;
            prototype.detailHealthyColor = data.Healthy;

            if (season == ESeason.Winter)
            {
                prototype.windWaveTintColor = data.WindTint;
            }

            if (!string.IsNullOrEmpty(data.Texture) && Textures.TryGetValue(data.Texture, out var texture))
            {
                prototype.prototypeTexture = texture;
                textured++;
            }

            if (!string.IsNullOrEmpty(data.Bump) && Textures.TryGetValue(data.Bump, out var bump))
            {
                prototype.bumpMap = bump;
            }

            changed++;
        }

        var managers = 0;

        foreach (var manager in LighthouseGrassBindings.ActiveManagers)
        {
            if (!manager)
            {
                continue;
            }

            // mirrors SeasonsMaterialsSlice.Fix; without it the managers keep their scene-load buffers
            GPUInstancerAPI.UpdateDetailInstances(manager, true);
            GPUInstancerAPI.UpdateTerrainNormalMapDetailInstance(manager);
            manager.InitializeRuntimeDataAndBuffers();
            managers++;
        }

        report.Append("; grass: ").Append(changed).Append('/').Append(total).Append(" prototypes, ")
            .Append(textured).Append(" retextured, ").Append(Textures.Count).Append(" textures loaded, ")
            .Append(managers).Append(" managers refreshed");

        if (missing.Count != 0)
        {
            Plugin.Log.LogError("Lighthouse seasons: grass prototypes without season data: " + string.Join(", ", missing));
        }
    }

    private static Table LoadTable()
    {
        if (_table != null)
        {
            return _table;
        }

        using var stream = typeof(LighthouseSeasons).Assembly.GetManifestResourceStream("Manimal.Lighthouse.GrassSeasons.json")
            ?? throw new InvalidOperationException("Lighthouse grass season table is missing.");
        using var reader = new StreamReader(stream);
        var table = JsonUtility.FromJson<Table>(reader.ReadToEnd());

        if (table.Version != 1 || table.Grass == null || table.Grass.Length == 0)
        {
            throw new InvalidOperationException("Unexpected Lighthouse grass season schema.");
        }

        _table = table;
        return table;
    }

    private static SeasonData Select(GrassEntry entry, ESeason season) => season switch
    {
        ESeason.Winter => entry.Winter,
        ESeason.Autumn => entry.Autumn,
        ESeason.AutumnLate => entry.AutumnLate,
        ESeason.Spring => entry.Spring,
        ESeason.SpringEarly => entry.SpringEarly,
        _ => entry.Summer
    };

    // "Detail_12_Grass_new_3_D_27bbce" -> "Grass_new_3_D_27bbce"
    private static string PrototypeKey(string name)
    {
        if (!name.StartsWith("Detail_", StringComparison.Ordinal))
        {
            return name;
        }

        var split = name.IndexOf('_', 7);
        return split < 0 ? name : name.Substring(split + 1);
    }

    private static string BaseName(string name)
    {
        const string suffix = " (Instance)";

        while (name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - suffix.Length);
        }

        return name;
    }

    private static string StripSeason(string name)
    {
        foreach (var suffix in SeasonSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }
        }

        return name;
    }
}
