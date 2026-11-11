using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.KeyCleanup;

internal sealed class SpawnPatch : LighthouseOwnedPatch<SpawnPatch>
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MovementContext), nameof(MovementContext.SpawnKeyInHands));

    [PatchPrefix]
    private static bool Prefix(MovementContext __instance) => LighthouseKeyCleanup.BeforeSpawn(__instance);
}
