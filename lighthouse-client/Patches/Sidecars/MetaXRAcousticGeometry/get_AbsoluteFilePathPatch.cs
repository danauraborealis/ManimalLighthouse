using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Manimal.Lighthouse.Client.Patches.Sidecars.MetaXRAcousticGeometry;

internal sealed class get_AbsoluteFilePathPatch : LighthouseOwnedPatch<get_AbsoluteFilePathPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.PropertyGetter(typeof(global::MetaXRAcousticGeometry), "AbsoluteFilePath");
    }

    [PatchPostfix]
    private static void Postfix(Component __instance, ref string __result)
    {
        LighthouseSidecars.XrAbsolutePath(__instance, ref __result);
    }
}
