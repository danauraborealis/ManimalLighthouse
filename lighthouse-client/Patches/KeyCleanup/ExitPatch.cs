using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeyCleanup;

internal sealed class ExitPatch : LighthouseOwnedPatch<ExitPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(DoorInteractState), nameof(DoorInteractState.Exit));
    }

    [PatchPrefix]
    private static void Prefix(DoorInteractState __instance)
    {
        LighthouseKeyCleanup.BeforeExit(__instance);
    }
}
