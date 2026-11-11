using EFT.Vehicle;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Btr;

internal sealed class InitBTRServerPatch : LighthouseOwnedPatch<InitBTRServerPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BtrController), nameof(BtrController.InitBTRServer));
    }

    [PatchPostfix]
    private static void Postfix(BtrController __instance)
    {
        LighthouseBtr.ServerInitialized(__instance);
    }
}
