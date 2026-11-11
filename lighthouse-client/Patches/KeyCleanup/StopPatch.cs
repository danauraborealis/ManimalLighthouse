using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeyCleanup;

internal sealed class StopPatch : LighthouseOwnedPatch<StopPatch>
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(DoorInteractState), nameof(DoorInteractState.StopInteractionAnimation));

    [PatchPrefix]
    private static void Prefix(DoorInteractState __instance) => LighthouseKeyCleanup.BeforeStop(__instance);
}
