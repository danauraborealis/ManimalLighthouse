using EFT.BufferZone;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeeperRestore;

internal sealed class BufferZoneContainerAwakePatch : LighthouseOwnedPatch<BufferZoneContainerAwakePatch>
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BufferZoneContainer), nameof(BufferZoneContainer.Awake));

    [PatchPrefix]
    private static void Prefix(BufferZoneContainer __instance) => LighthouseKeeperRestore.BeforeAwake(__instance);
}
