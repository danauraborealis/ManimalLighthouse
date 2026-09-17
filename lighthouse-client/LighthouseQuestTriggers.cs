using System;
using System.IO;
using EFT.Interactive;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLinq;

namespace Manimal.Lighthouse.Client;

internal static class LighthouseQuestTriggers
{
    private sealed class Entry
    {
        public string Path = "";
        public string Id = "";
    }

    private static readonly Lazy<Entry[]> Entries = new(() =>
    {
        using var stream = typeof(LighthouseQuestTriggers).Assembly.GetManifestResourceStream("Manimal.Lighthouse.LegacyQuestTriggers.json")
            ?? throw new InvalidDataException("Missing Lighthouse quest trigger table");
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<Entry[]>(reader.ReadToEnd())
            ?? throw new InvalidDataException("Invalid Lighthouse quest trigger table");
    });

    internal static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!LighthouseSceneLoader.Owns(scene.name) || scene.name != "Lighthouse_DesignMain_ML") return;
        var roots = scene.GetRootGameObjects();
        var restored = 0;
        foreach (var entry in Entries.Value)
        {
            var separator = entry.Path.IndexOf('/');
            var rootName = entry.Path.Substring(0, separator);
            var root = roots.AsValueEnumerable().SingleOrDefault(candidate => candidate.name == rootName);
            var owner = root ? root!.transform.Find(entry.Path.Substring(separator + 1)) : null;
            if (!owner)
            {
                Plugin.Log.LogWarning("Legacy quest trigger object missing: " + entry.Path);
                continue;
            }
            if (!owner!.GetComponents<Collider>().AsValueEnumerable().Any(collider => collider && collider.isTrigger))
            {
                Plugin.Log.LogWarning("Legacy quest trigger collider missing: " + entry.Path);
                continue;
            }
            var trigger = owner.GetComponent<PlaceItemTrigger>();
            if (trigger && !string.IsNullOrEmpty(trigger.Id)) continue;
            // Keep the rework's geometry and active state; restore only the SPT
            // interaction component and its exact stock ID. Stock dummies are null.
            if (!trigger) trigger = owner.gameObject.AddComponent<PlaceItemTrigger>();
            trigger.SetId(entry.Id);
            restored++;
        }
        Plugin.Log.LogInfo("Lighthouse: restored " + restored + " legacy quest placement triggers.");
    }
}
