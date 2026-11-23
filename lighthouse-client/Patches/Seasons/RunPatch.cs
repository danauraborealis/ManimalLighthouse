using System;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.Weather;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.Lighthouse.Client.Patches.Seasons;

// every raid-start path (local and client) creates a SeasonsController and awaits Run
internal sealed class RunPatch : LighthouseOwnedPatch<RunPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SeasonsController), nameof(SeasonsController.Run), [typeof(ESeason), typeof(SeasonsSettings)])
            ?? throw new MissingMethodException(nameof(SeasonsController), nameof(SeasonsController.Run));
    }

    [PatchPostfix]
    private static void Postfix(ref Task __result, ESeason season)
    {
        __result = LighthouseSeasons.After(__result, season);
    }
}
