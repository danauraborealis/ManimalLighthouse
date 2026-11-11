using EFT;
using EFT.AssetsManager;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace Manimal.Lighthouse.Client.Patches.BundledScenes;

internal sealed class LoadScenePatch : LighthouseOwnedPatch<LoadScenePatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(AssetsManagerExtension), nameof(AssetsManagerExtension.LoadScene));
    }

    [PatchPrefix]
    private static bool Prefix(ResourceKey resourceKey, LoadSceneMode loadSceneMode, bool allowSceneActivation, Action<float> progressCallback, ref LoadSceneOperation __result)
    {
        return LighthouseBundledScenes.BeforeLoad(resourceKey, loadSceneMode, allowSceneActivation, progressCallback, ref __result);
    }
}
