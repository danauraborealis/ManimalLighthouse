using EFT.Interactive;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.TunnelDoor;

internal sealed class OnEnablePatch : LighthouseOwnedPatch<OnEnablePatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(WorldInteractiveObject), nameof(WorldInteractiveObject.OnEnable));
    }

    [PatchPrefix]
    private static void Prefix(WorldInteractiveObject __instance)
    {
        LighthouseTunnelDoor.BeforeEnable(__instance);
    }
}
