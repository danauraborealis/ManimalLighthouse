using System.Text.Json;
using Manimal.Lighthouse.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace Manimal.Lighthouse.Server;

public static class LighthouseLocationData
{
    private static readonly string[] Names = 
    [
        "base.json", 
        "looseLoot.json", 
        "staticLoot.json", 
        "staticContainers.json", 
        "staticAmmo.json", 
        "statics.json", 
        "allExtracts.json"
    ];

    // upstream 5.0 data names transits with tr_lighthouse_* locale keys that 4.1.5 doesnt ship,
    // so they show as raw strings; the same ids have vanilla LIG_TRANSIT_* keys in every language
    private static readonly Dictionary<int, string> VanillaTransitKeys = new()
    {
        [21] = "LIG_TRANSIT_21",
        [22] = "LIG_TRANSIT_22",
        [23] = "LIG_TRANSIT_23"
    };

    private static LocationBase UseVanillaTransitKeys(LocationBase location)
    {
        if (location.Transits is null)
        {
            return location;
        }

        foreach (var transit in location.Transits)
        {
            if (transit.Id is not int id || !VanillaTransitKeys.TryGetValue(id, out var key))
            {
                continue;
            }

            transit.Name = key;
            transit.Description = key + "_DESC";
            transit.Conditions = key + "_COND";
        }

        return location;
    }

    public static Location Read(string root, ContentManifest manifest, JsonUtil json, Dictionary<MongoId, TemplateItem> templates, LooseLoot? baseline = null)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in Names)
        {
            var relative = "db/locations/lighthouse/" + name;
            PayloadFile? declaration = null;

            foreach (var file in manifest.ServerFiles)
            {
                if (file.Path != relative) { continue; }

                declaration = file;
                break;
            }

            if (declaration is null)
            {
                throw new InvalidDataException("Missing Lighthouse database declaration: " + relative);
            }

            ManifestRules.VerifyFile(root, relative, declaration.Sha256);

            var text = File.ReadAllText(ManifestRules.Resolve(root, relative));
            using var document = JsonDocument.Parse(text);

            ValidateTemplates(document.RootElement, templates);
            texts.Add(name, text);
        }

        _ = ReadLooseLoot();
        _ = Parse<StaticContainerDetails>("staticContainers.json");

        var loot = Parse<Dictionary<MongoId, StaticLootDetails>>("staticLoot.json");

        foreach (var identity in loot.Keys)
        {
            if (!templates.ContainsKey(identity))
            {
                throw new InvalidDataException("Unknown static container template: " + identity);
            }
        }

        return new Location
        {
            Base = UseVanillaTransitKeys(Parse<LocationBase>("base.json")),
            LooseLoot = new LazyLoad<LooseLoot>(ReadLooseLoot, false),
            StaticContainers = new LazyLoad<StaticContainerDetails>(() => Parse<StaticContainerDetails>("staticContainers.json"), false),
            StaticLoot = new LazyLoad<Dictionary<MongoId, StaticLootDetails>>(() => Parse<Dictionary<MongoId, StaticLootDetails>>("staticLoot.json"), false),
            StaticAmmo = Parse<Dictionary<string, IEnumerable<StaticAmmoDetails>>>("staticAmmo.json"),
            Statics = Parse<StaticContainer>("statics.json"),
            AllExtracts = Parse<IEnumerable<AllExtractsExit>>("allExtracts.json")
        };

        T Parse<T>(string name) => json.Deserialize<T>(texts[name]) ?? throw new InvalidDataException("Null Lighthouse database: " + name);
        LooseLoot ReadLooseLoot()
        {
            var loot = Parse<LooseLoot>("looseLoot.json");
            return baseline is null ? loot : Manimal.MapBackport.LegacyLootCompatibility.Reconcile(loot, baseline, templates, json);
        }
    }

    private static void ValidateTemplates(JsonElement value, Dictionary<MongoId, TemplateItem> templates, LooseLoot? baseline = null)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
            {
                foreach (var field in value.EnumerateObject())
                {
                    if (field.Name is "_tpl" or "tpl" && field.Value.ValueKind == JsonValueKind.String)
                    {
                        var id = new MongoId(field.Value.GetString()!);

                        if (!templates.ContainsKey(id))
                        {
                            throw new InvalidDataException("Missing Lighthouse loot template: " + id);
                        }
                    }
                    else
                    {
                        ValidateTemplates(field.Value, templates);
                    }
                }

                break;
            }
            case JsonValueKind.Array:
            {
                foreach (var child in value.EnumerateArray())
                {
                    ValidateTemplates(child, templates);
                }

                break;
            }
        }
    }

    public static void Apply(Location target, Location replacement)
    {
        LighthouseLazyLoadCompatibility.PreserveTransformers(target.LooseLoot, replacement.LooseLoot);
        LighthouseLazyLoadCompatibility.PreserveTransformers(target.StaticLoot, replacement.StaticLoot);
        LighthouseLazyLoadCompatibility.PreserveTransformers(target.StaticContainers, replacement.StaticContainers);

        target.Base = replacement.Base;
        target.LooseLoot = replacement.LooseLoot;
        target.StaticLoot = replacement.StaticLoot;
        target.StaticContainers = replacement.StaticContainers;
        target.StaticAmmo = replacement.StaticAmmo;
        target.Statics = replacement.Statics;
        target.AllExtracts = replacement.AllExtracts;
    }
}
