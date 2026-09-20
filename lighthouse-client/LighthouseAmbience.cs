using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Audio.AmbientSubsystem;
using Audio.SpatialSystem;
using Comfort.Common;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manimal.Lighthouse.Client;

internal static class LighthouseAmbience
{
    private const string AmbientRoot = "AmbientAudioSystem";
    private const string ReplacementSound = "Lighthouse_Sound_ML";
    private static readonly string[] Lifecycle = ["Awake", "OnEnable", "Start", "OnDisable", "OnDestroy"];

    internal static void Install()
    {
        var patched = new HashSet<MethodInfo>();

        foreach (var type in typeof(AmbientAudioSystem).Assembly.GetTypes())
        {
            var ns = type.Namespace ?? "";

            if (!typeof(MonoBehaviour).IsAssignableFrom(type) || type.ContainsGenericParameters)
            {
                continue;
            }

            if (ns == "Audio.AutoPanner" || ns.StartsWith("Audio.AmbientSubsystem", StringComparison.Ordinal))
            {
                GuardLifecycle(type, patched);
            }
        }

        foreach (var name in new[] {
            "Audio.SpatialSystem.SpatialAudioSystem", "Audio.SpatialSystem.SpatialAudioPortal",
            "Audio.SpatialSystem.AudioTriggerArea", "Audio.SpatialSystem.MultiWindowPortal",
            "SpatialAudioRoom", "Audio.SpatialSystem.SpatialAudioCrossSceneGroup", "GuidComponent",
            "CommonAssets.Scripts.Audio.RadioSystem.RadioBroadcastController",
            "Audio.RadioSystem.ClientBroadcastPlayer", "MetaXRAcousticMap"
        })
        {
            var type = AccessTools.TypeByName(name) ?? throw new TypeLoadException(name);

            GuardLifecycle(type, patched);
        }
    }

    private static void GuardLifecycle(Type type, HashSet<MethodInfo> patched)
    {
        foreach (var name in Lifecycle)
        {
            MethodInfo? method = null;

            for (var declaring = type; declaring != null && method == null; declaring = declaring.BaseType)
            {
                method = declaring.GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
            }

            if (method == null || method.IsAbstract || method.IsStatic || method.DeclaringType!.IsGenericType || !patched.Add(method))
            {
                continue;
            }

            new Patches.Ambience.AudioLifecyclePatch(method).Enable();
        }
    }

    internal static bool BeforeLifecycle(MonoBehaviour __instance)
    {
        if (LighthouseHeadless.Active)
        {
            return true;
        }

        var scene = __instance.gameObject.scene.name;
        var donor = LighthouseSceneLoader.IsNativeAmbienceDonor(scene);
        var replacement = scene == ReplacementSound && LighthouseSceneLoader.Owns(scene);

        if (!donor && !replacement)
        {
            return true;
        }

        var root = __instance.transform.root.gameObject;

        if (donor ? root.name == AmbientRoot : root.name != AmbientRoot)
        {
            return true;
        }

        if (root.activeSelf)
        {
            root.SetActive(false);
        }

        return false;
    }

    internal static bool IsAmbientObject(object? value)
    {
        if (value is Component component && component)
        {
            return component.transform.root.name == AmbientRoot;
        }

        return value is GameObject go && go && go.transform.root.name == AmbientRoot;
    }

    internal static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (LighthouseHeadless.Active)
        {
            return;
        }

        var donor = LighthouseSceneLoader.IsNativeAmbienceDonor(scene.name);
        var replacement = scene.name == ReplacementSound && LighthouseSceneLoader.Owns(scene.name);

        if (!donor && !replacement)
        {
            return;
        }

        var systems = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            switch (donor)
            {
                case true when root.name == AmbientRoot:
                    {
                        systems += root.GetComponentsInChildren<AmbientAudioSystem>(true).Length;
                        continue;
                    }

                case true when root.GetComponent<LocationScene>():
                    {
                        continue;
                    }
            }

            if (donor || root.name == AmbientRoot)
            {
                root.SetActive(false);
            }
        }

        if (!donor)
        {
            return;
        }

        RestoreSpatialRegistration();

        if (systems != 1)
        {
            throw new InvalidDataException("Expected one native Lighthouse ambient system, found " + systems);
        }

        Plugin.Log.LogInfo("Lighthouse ambience: native sound hierarchy loaded with original clips; donor rooms, "
                           + "radio and acoustic map disabled. Replacement spatial audio retained.");
    }

    internal static void Initialized(AmbientAudioSystem __instance)
    {
        if (!LighthouseSceneLoader.IsNativeAmbienceDonor(__instance.gameObject.scene.name))
        {
            return;
        }

        if (!__instance.Initialized)
        {
            Plugin.Log.LogError("Lighthouse ambience: native initialization failed; see Player.log for its exception.");

            return;
        }

        var groups = __instance.GetComponentsInChildren<AmbientSoundPlayerGroup>(true).Length;
        var players = __instance.GetComponentsInChildren<BaseAmbientSoundPlayer>(true).Length;

        Plugin.Log.LogInfo("Lighthouse ambience: initialized " + groups + " native groups and " + players + " players for " + __instance.CurrentSeasonStatus + ".");
    }

    internal static void ValidateLoaded()
    {
        if (LighthouseHeadless.Active || !LighthouseSceneLoader.IsNativeAmbienceDonor("Lighthouse_Sound"))
        {
            return;
        }

        var count = 0;

        foreach (var system in Resources.FindObjectsOfTypeAll<AmbientAudioSystem>())
        {
            if (!system || !LighthouseSceneLoader.IsNativeAmbienceDonor(system.gameObject.scene.name))
            {
                continue;
            }

            count++;

            if (!system.gameObject.activeInHierarchy || !system._ambientData || !system.EffectsData)
            {
                throw new InvalidDataException("Native Lighthouse ambient system or audio data is unavailable.");
            }

            if (system.GetComponentsInChildren<AmbientSoundPlayerGroup>(true).Length != 12
                || system.GetComponentsInChildren<BaseAmbientSoundPlayer>(true).Length != 78)
            {
                throw new InvalidDataException("Native Lighthouse ambient players do not match the audited donor.");
            }
        }

        if (count != 1)
        {
            throw new InvalidDataException("Native Lighthouse ambient donor did not load.");
        }

        RestoreSpatialRegistration();
    }

    private static void RestoreSpatialRegistration()
    {
        var current = Singleton<SpatialAudioSystem>.Instance;

        if (!current || !LighthouseSceneLoader.IsNativeAmbienceDonor(current.gameObject.scene.name))
        {
            return;
        }

        Singleton<SpatialAudioSystem>.TryRelease(current);

        foreach (var system in Resources.FindObjectsOfTypeAll<SpatialAudioSystem>())
        {
            if (!system || !LighthouseSceneLoader.Owns(system.gameObject.scene.name))
            {
                continue;
            }

            Singleton<SpatialAudioSystem>.Create(system);
            break;
        }
    }
}
