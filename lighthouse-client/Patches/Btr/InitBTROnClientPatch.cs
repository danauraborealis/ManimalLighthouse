using EFT.Vehicle;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Btr;

internal sealed class InitBTROnClientPatch : LighthouseOwnedPatch<InitBTROnClientPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BtrController), nameof(BtrController.InitBTROnClient));
    }

    [PatchPostfix]
    private static void Postfix(BtrController __instance)
    {
        LighthouseBtr.ViewInitialized(__instance);
    }
}
