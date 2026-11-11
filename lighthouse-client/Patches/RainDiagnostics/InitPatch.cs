using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Manimal.Lighthouse.Client.Patches.RainDiagnostics;

internal sealed class InitPatch : LighthouseOwnedPatch<InitPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RainFallDrops), nameof(RainFallDrops.Init));
    }

    [PatchPostfix]
    private static void Postfix(RainFallDrops __instance, MeshRenderer ____rainRenderer, ref Material ____closeCopy)
    {
        LighthouseRainDiagnostics.AfterInit(__instance, ____rainRenderer, ref ____closeCopy);
    }
}
