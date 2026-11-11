using EFT.Vehicle;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Btr;

internal sealed class LoadBTRVehiclePatch : LighthouseOwnedPatch<LoadBTRVehiclePatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BtrController), nameof(BtrController.LoadBTRVehicle));
    }

    [PatchPostfix]
    private static void Postfix(BtrController __instance)
    {
        LighthouseBtr.VehicleLoaded(__instance);
    }
}
