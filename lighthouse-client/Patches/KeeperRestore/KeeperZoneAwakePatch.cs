using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeeperRestore;

internal sealed class KeeperZoneAwakePatch : LighthouseOwnedPatch<KeeperZoneAwakePatch>
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(LighthouseKeeperZone), nameof(LighthouseKeeperZone.Awake));

    [PatchPrefix]
    private static void Prefix(LighthouseKeeperZone __instance) => LighthouseKeeperRestore.BeforeAwake(__instance);
}
