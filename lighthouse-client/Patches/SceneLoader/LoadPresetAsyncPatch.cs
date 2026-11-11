using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using System.Threading.Tasks;

namespace Manimal.Lighthouse.Client.Patches.SceneLoader;

internal sealed class LoadPresetAsyncPatch : LighthouseOwnedPatch<LoadPresetAsyncPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LoadScenesFromPresetOperation), nameof(LoadScenesFromPresetOperation.LoadPresetAsync));
    }

    [PatchPrefix]
    private static bool Prefix(LoadScenesFromPresetOperation __instance, ScenesPreset preset, ref Task __result)
    {
        return LighthouseSceneLoader.Prefix(__instance, preset, ref __result);
    }
}
