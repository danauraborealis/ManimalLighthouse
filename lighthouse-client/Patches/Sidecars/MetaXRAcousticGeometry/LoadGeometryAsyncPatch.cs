using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Manimal.Lighthouse.Client.Patches.Sidecars.MetaXRAcousticGeometry;

internal sealed class LoadGeometryAsyncPatch : LighthouseOwnedPatch<LoadGeometryAsyncPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(global::MetaXRAcousticGeometry), "LoadGeometryAsync", [typeof(string)]);
    }

    [PatchPrefix]
    private static void Prefix(Component __instance, ref string __0)
    {
        LighthouseSidecars.XrAsyncPath(__instance, ref __0);
    }
}
