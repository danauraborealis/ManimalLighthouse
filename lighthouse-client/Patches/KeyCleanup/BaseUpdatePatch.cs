using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeyCleanup;

internal sealed class BaseUpdatePatch : LighthouseOwnedPatch<BaseUpdatePatch>
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(DoorInteractState), nameof(DoorInteractState.BaseUpdate));

    [PatchPrefix]
    private static bool Prefix(DoorInteractState __instance) => LighthouseKeyCleanup.BeforeBaseUpdate(__instance);
}
