using EFT.Vehicle;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Btr;

internal sealed class LoadMapPathsConfigurationPatch : LighthouseOwnedPatch<LoadMapPathsConfigurationPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BtrController), nameof(BtrController.LoadMapPathsConfiguration));
    }

    [PatchPrefix]
    private static bool Prefix(BtrController __instance, string locationID)
    {
        return LighthouseBtr.BindMapPaths(__instance, locationID);
    }
}
