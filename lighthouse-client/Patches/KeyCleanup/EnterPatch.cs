using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeyCleanup;

internal sealed class EnterPatch : LighthouseOwnedPatch<EnterPatch>
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(DoorInteractState), nameof(DoorInteractState.Enter));

    [PatchPostfix]
    private static void Postfix(DoorInteractState __instance) => LighthouseKeyCleanup.AfterEnter(__instance);
}
