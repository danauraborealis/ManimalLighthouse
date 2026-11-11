using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.FrameStats;

internal sealed class OnGameStartedPatch : LighthouseOwnedPatch<OnGameStartedPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    private static void Postfix(GameWorld __instance)
    {
        LighthouseFrameStats.StartTracking(__instance);
    }
}
