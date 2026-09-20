using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EFT;
using Manimal.Lighthouse.Shared;
using Newtonsoft.Json;
using SPT.Common.Http;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLinq;

namespace Manimal.Lighthouse.Client;

internal static class LighthouseSceneLoader
{
    private static readonly HashSet<string> PresentationBundles = new(StringComparer.Ordinal)
    {
        "bundles/manimal_lighthouse_rendering.bundle",
        "bundles/manimal_lighthouse_grass.bundle",
        "bundles/manimal_lighthouse_water.bundle",
    };

    private static readonly List<AssetBundle> Bundles = [];
    private static readonly HashSet<string> SceneNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NativeDonorScenes = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loading;
    private static ScenesPreset? _ownedPreset;
    internal static bool HasReplacement => _ownedPreset;

    // EFT's callback consumes increments in scene units, not an absolute percentage.
    private const int PreparationSceneUnits = 3;
    private sealed class PreparationProgress(IProgress<float>? target) : IProgress<float>
    {
        private float _reported;
        private bool _closed;

        public void Report(float value)
        {
            if (_closed || float.IsNaN(value)) return;
            value = Math.Max(_reported, Math.Min(1f, value));
            var delta = value - _reported;
            _reported = value;
            if (delta > 0f) target?.Report(delta * PreparationSceneUnits);
        }

        internal void Close() => _closed = true;
    }

    public static bool Prefix(LoadScenesFromPresetOperation __instance, ScenesPreset preset, ref Task __result)
    {
        if (!preset || !string.Equals(preset.ServerName, "Lighthouse", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!File.Exists(Path.Combine(Plugin.Root, ManifestRules.FileName)))
        {
            return true;
        }

        __result = LoadReplacement(__instance, preset);

        return false;
    }
    private static async Task LoadReplacement(LoadScenesFromPresetOperation operation, ScenesPreset native)
    {
        if (_loading)
        {
            operation.SetFailed("Lighthouse replacement is already loading.");

            return;
        }

        ReleaseIfUnused();
        _loading = true;

        var succeeded = false;
        var preparation = new PreparationProgress(operation._progress);
        var timer = Stopwatch.StartNew();

        try
        {
            operation._cancellationToken.ThrowIfCancellationRequested();

            var path = Path.Combine(Plugin.Root, ManifestRules.FileName);
            var manifest = JsonConvert.DeserializeObject<ContentManifest>(await File.ReadAllTextAsync(path))!;

            ManifestRules.Validate(manifest);
            var donorCount = ManifestRules.UsesNativeEnvironment(manifest)
                ? native._scenesResourceKeys.AsValueEnumerable().Count(original =>
                    native.ShouldLoadScene(original) &&
                    (ReferenceEquals(original, native._scenesResourceKeys[0]) ||
                     Path.GetFileNameWithoutExtension(original.path) == "Lighthouse_Sound"))
                : 0;
            operation._totalScenesToLoad = native.GetTotalSceneCount() + donorCount + PreparationSceneUnits;

            if (!manifest.Ready && manifest.Mode != "test")
            {
                throw new InvalidDataException("Lighthouse content conversion has not passed its release gates.");
            }

            switch (manifest.Mode)
            {
                case "test" when !Plugin.AllowTest.Value:
                {
                    throw new InvalidDataException("Full-map test content is disabled for this client.");
                }
                case "probe" when !Plugin.AllowProbe.Value:
                {
                    throw new InvalidDataException("Loader probe content is disabled for this client.");
                }
            }

            var request = RequestHandler.GetJsonAsync("/manimal/lighthouse/capability");
            var timeout = Task.Delay(TimeSpan.FromSeconds(15), operation._cancellationToken);

            if (await Task.WhenAny(request, timeout) != request)
            {
                operation._cancellationToken.ThrowIfCancellationRequested();

                throw new TimeoutException("Lighthouse server compatibility check timed out.");
            }

            var response = JsonConvert.DeserializeObject<ServerCapability>(await request)!;

            ManifestRules.CheckCapability(manifest, ManifestRules.Hash(path), response);
            Plugin.Log.LogInfo($"Lighthouse preparation: server check completed in {timer.Elapsed.TotalSeconds:F1}s; verifying files.");
            preparation.Report(0.02f);
            // Capture Unity's synchronization context before starting disk work.
            IProgress<float> verificationProgress = new System.Progress<float>(value => preparation.Report(0.02f + value * 0.68f));
            var nativeDataPath = Application.dataPath;
            await Task.Run(() =>
            {
                LighthouseNativeAssets.Verify(nativeDataPath, manifest, operation._cancellationToken,
                    value => verificationProgress.Report(value * 0.75f));
                var payloadCount = manifest.Bundles.Count + manifest.Sidecars.Count;
                var payloadIndex = 0;
                foreach (var bundle in manifest.Bundles)
                {
                    operation._cancellationToken.ThrowIfCancellationRequested();
                    LighthouseVerifiedFiles.Verify(Plugin.Root, bundle.Path, bundle.Sha256, operation._cancellationToken,
                        value => verificationProgress.Report(0.75f + 0.25f * (payloadIndex + value) / payloadCount));
                    payloadIndex++;
                }

                foreach (var sidecar in manifest.Sidecars)
                {
                    operation._cancellationToken.ThrowIfCancellationRequested();
                    LighthouseVerifiedFiles.Verify(Plugin.Root, sidecar.Path, sidecar.Sha256, operation._cancellationToken,
                        value => verificationProgress.Report(0.75f + 0.25f * (payloadIndex + value) / payloadCount));
                    payloadIndex++;
                }
            }, operation._cancellationToken);
            operation._cancellationToken.ThrowIfCancellationRequested();
            preparation.Report(0.70f);
            Plugin.Log.LogInfo($"Lighthouse preparation: file verification finished at {timer.Elapsed.TotalSeconds:F1}s; opening bundles asynchronously.");

            if (Bundles.Count != 0)
            {
                throw new InvalidOperationException("Previous Lighthouse scenes are still loaded.");
            }

            if (!LighthouseHeadless.Active)
            {
                LighthouseShaderRebind.CaptureNativeShaders();
            }

            var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bundleIndex = 0;

            foreach (var entry in manifest.Bundles)
            {
                operation._cancellationToken.ThrowIfCancellationRequested();

                if (LighthouseHeadless.Active && PresentationBundles.Contains(entry.Path))
                {
                    bundleIndex++;
                    preparation.Report(0.70f + 0.30f * bundleIndex / manifest.Bundles.Count);
                    continue;
                }

                var bundleProgress = Cysharp.Threading.Tasks.Progress.Create<float>(value =>
                    preparation.Report(0.70f + 0.30f * (bundleIndex + value) / manifest.Bundles.Count));
                // Unity cannot cancel this request. Own its result before honoring cancellation,
                // so failure cleanup can unload it instead of leaking the bundle.
                var bundle = await AssetBundle.LoadFromFileAsync(ManifestRules.Resolve(Plugin.Root, entry.Path))
                    .ToUniTask(progress: bundleProgress);

                if (!bundle)
                {
                    throw new InvalidDataException("Unity could not load " + entry.Path);
                }

                Bundles.Add(bundle);
                operation._cancellationToken.ThrowIfCancellationRequested();

                switch (entry.Path)
                {
                    case "bundles/manimal_lighthouse_rendering.bundle":
                    {
                        LighthouseShaderRebind.RegisterRenderingBundle(bundle);
                        break;
                    }
                    case "bundles/manimal_lighthouse_grass.bundle":
                    {
                        LighthouseGrassBindings.Load(bundle, LighthouseShaderRebind.Rebind);
                        break;
                    }
                    case "bundles/manimal_lighthouse_water.bundle":
                    {
                        LighthouseWaterBindings.Load(bundle);
                        break;
                    }
                }

                foreach (var scene in bundle.GetAllScenePaths())
                {
                    if (!available.Add(scene))
                    {
                        throw new InvalidDataException("Duplicate bundled scene path: " + scene);
                    }
                }
                bundleIndex++;
                preparation.Report(0.70f + 0.30f * bundleIndex / manifest.Bundles.Count);
            }

            preparation.Report(1f);
            preparation.Close();
            Plugin.Log.LogInfo($"Lighthouse preparation completed in {timer.Elapsed.TotalSeconds:F1}s; starting scenes.");

            if (native._scenesResourceKeys.Count != manifest.Scenes.Count)
            {
                throw new InvalidDataException("Native Lighthouse preset scene count changed.");
            }

            var replacementKeys = new List<SceneResourceKey>();

            for (var i = 0; i < manifest.Scenes.Count; i++)
            {
                var mapping = manifest.Scenes[i];
                var original = native._scenesResourceKeys[i];

                if (original.path != mapping.OriginalPath || original.onlyOffline != mapping.OnlyOffline)
                {
                    throw new InvalidDataException("Lighthouse preset order/flags do not match the audited build.");
                }

                if (!available.Contains(mapping.ReplacementPath))
                {
                    throw new InvalidDataException("Bundled replacement scene missing: " + mapping.ReplacementPath);
                }

                if (ManifestRules.UsesNativeEnvironment(manifest) &&
                    (i == 0 || Path.GetFileNameWithoutExtension(original.path) == "Lighthouse_Sound"))
                {
                    replacementKeys.Add(original);
                    NativeDonorScenes.Add(Path.GetFileNameWithoutExtension(original.path));
                }

                replacementKeys.Add(new SceneResourceKey
                {
                    path = mapping.ReplacementPath, rcid = mapping.ReplacementPath, onlyOffline = mapping.OnlyOffline
                });
                SceneNames.Add(Path.GetFileNameWithoutExtension(mapping.ReplacementPath));
            }

            _ownedPreset = UnityEngine.Object.Instantiate(native);
            InGameMemoryManagement.GCEnabled = true;
            _ownedPreset._scenesResourceKeys = replacementKeys;
            operation._totalScenesToLoad = _ownedPreset.GetTotalSceneCount() + donorCount + PreparationSceneUnits;
            LighthouseSidecars.Activate(manifest);
            Plugin.Log.LogInfo(
                "Loading coherent Lighthouse content " + manifest.ContentId + " (29 replacement scenes).");
            await Patches.SceneLoader.LoadPresetAsyncReversePatch.LoadOriginal(operation, _ownedPreset);

            if (operation.Failed)
            {
                throw new InvalidOperationException(operation.Error);
            }

            var sceneError = operation.GetLoadError();

            if (!string.IsNullOrEmpty(sceneError))
            {
                throw new InvalidOperationException(sceneError);
            }

            LighthouseAmbience.ValidateLoaded();

            if (!LighthouseHeadless.Active)
            {
                LighthouseShaderRebind.RebindAll();
                LighthouseShaderRebind.ValidateTerrain();
                LighthouseGrassBindings.Validate();
                LighthouseWaterBindings.ValidateRenderer();

                if (LighthouseWaterBindings.GroupCount != 0)
                {
                    Plugin.Log.LogInfo("Lighthouse water: 2 groups, 7 surfaces registered with the native SPT renderer.");
                }

                if (LighthouseGrassBindings.ManagerCount != 0)
                {
                    Plugin.Log.LogInfo(LighthouseGrassBindings.Diagnostics());
                }
            }

            operation._cancellationToken.ThrowIfCancellationRequested();
            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            operation.SetCancelled();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e); operation.SetFailed("Manimal Lighthouse: " + e.Message);
        }
        finally
        {
            preparation.Close();
            try
            {
                if (!succeeded)
                {
                    await UnloadPartialReplacement();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Lighthouse cleanup failed: " + e);
            }
            finally
            {
                _loading = false; ReleaseIfUnused();
            }
        }
    }
    private static async Task UnloadPartialReplacement()
    {
        var scenes = new List<Scene>();

        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);

            if (SceneNames.Contains(scene.name) || NativeDonorScenes.Contains(scene.name))
            {
                scenes.Add(scene);
            }
        }

        foreach (var scene in scenes)
        {
            var unloading = SceneManager.UnloadSceneAsync(scene);

            if (unloading == null || unloading.isDone)
            {
                continue;
            }

            var completion = new TaskCompletionSource<bool>();

            unloading.completed += _ => completion.TrySetResult(true);
            await completion.Task;
        }
    }
    public static void SceneUnloaded(Scene scene)
    {
        if (SceneNames.Contains(scene.name) || NativeDonorScenes.Contains(scene.name))
        {
            ReleaseIfUnused();
        }
    }
    internal static bool Owns(string sceneName) => SceneNames.Contains(sceneName);
    internal static bool IsNativeDonor(string sceneName) => NativeDonorScenes.Contains(sceneName);
    internal static bool IsNativeAmbienceDonor(string sceneName) => IsNativeDonor(sceneName) && sceneName == "Lighthouse_Sound";
    public static void ReleaseIfUnused()
    {
        if (_loading)
        {
            return;
        }

        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneNames.Contains(SceneManager.GetSceneAt(i).name) || NativeDonorScenes.Contains(SceneManager.GetSceneAt(i).name))
            {
                return;
            }
        }

        LighthouseTerrainBindings.Clear();
        LighthouseGrassBindings.Clear();
        LighthouseWaterBindings.Clear();
        LighthouseRainRendering.Clear();

        foreach (var bundle in Bundles)
        {
            if (bundle)
            {
                bundle.Unload(true);
            }
        }

        Bundles.Clear();
        SceneNames.Clear();
        NativeDonorScenes.Clear();
        LighthouseSidecars.Clear();
        LighthouseShaderRebind.Clear();
        LighthouseKeeperRestore.Clear();
        LighthouseAudioRouting.Clear();

        if (_ownedPreset)
        {
            UnityEngine.Object.Destroy(_ownedPreset);
        }

        _ownedPreset = null;
    }
}
