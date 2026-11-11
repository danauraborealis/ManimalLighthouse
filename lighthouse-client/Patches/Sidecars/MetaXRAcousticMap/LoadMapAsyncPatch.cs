using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Manimal.Lighthouse.Client.Patches.Sidecars.MetaXRAcousticMap;

internal sealed class LoadMapAsyncPatch : LighthouseOwnedPatch<LoadMapAsyncPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(global::MetaXRAcousticMap), "LoadMapAsync", [typeof(string)]);
    }

    [PatchPrefix]
    private static void Prefix(Component __instance, ref string __0)
    {
        LighthouseSidecars.XrAsyncPath(__instance, ref __0);
    }
}
