using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Manimal.Lighthouse.Client;

// the converted scenes route their audio sources to a bundled copy of retail's MasterMixer. the game only
// drives snapshots, occlusion and effect levels on the installed mixer, so reroute every source to the
// native group of the same name once a replacement scene has loaded.
internal static class LighthouseAudioRouting
{
    private static readonly Dictionary<AudioMixerGroup, AudioMixerGroup> Groups = new();
    private static AudioMixer? _native;

    internal static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (LighthouseHeadless.Active || !LighthouseSceneLoader.Owns(scene.name))
        {
            return;
        }

        if (!_native)
        {
            _native = LighthouseKeeperAssets.Get("keeper/mixer") as AudioMixer;

            if (!_native)
            {
                Plugin.Log.LogError("Lighthouse audio: native mixer unavailable; scene sources keep the bundled mixer copy.");
                return;
            }
        }

        var rerouted = 0;
        var unmatched = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            {
                var group = source.outputAudioMixerGroup;

                if (!group || group.audioMixer == _native)
                {
                    continue;
                }

                var replacement = Find(group);

                if (!replacement)
                {
                    unmatched++;
                    continue;
                }

                source.outputAudioMixerGroup = replacement;
                rerouted++;
            }
        }

        if (rerouted != 0 || unmatched != 0)
        {
            Plugin.Log.LogInfo("Lighthouse audio: " + scene.name + " rerouted " + rerouted + " sources to the native mixer" + (unmatched != 0 ? "; " + unmatched + " groups had no native match" : "") + ".");
        }
    }

    internal static void Clear()
    {
        Groups.Clear();
    }

    // FindMatchingGroups is a substring search and one name (Occlusion) repeats in this mixer, so pair
    // exact-name matches by their order within each mixer
    private static AudioMixerGroup? Find(AudioMixerGroup group)
    {
        if (Groups.TryGetValue(group, out var cached) && cached)
        {
            return cached;
        }

        var ordinal = Ordinal(group.audioMixer, group);
        var index = 0;

        foreach (var candidate in _native!.FindMatchingGroups(group.name))
        {
            if (!candidate || candidate.name != group.name)
            {
                continue;
            }

            if (index++ == ordinal)
            {
                Groups[group] = candidate;
                return candidate;
            }
        }

        return null;
    }

    private static int Ordinal(AudioMixer mixer, AudioMixerGroup group)
    {
        var index = 0;

        foreach (var candidate in mixer.FindMatchingGroups(group.name))
        {
            if (!candidate || candidate.name != group.name)
            {
                continue;
            }

            if (candidate == group)
            {
                return index;
            }

            index++;
        }

        return 0;
    }
}
