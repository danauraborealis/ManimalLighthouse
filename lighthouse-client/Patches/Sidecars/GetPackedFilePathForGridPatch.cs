using HarmonyLib;
using Koenigz.PerfectCulling.EFT;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Sidecars;

internal sealed class GetPackedFilePathForGridPatch : LighthouseOwnedPatch<GetPackedFilePathForGridPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(PackedCullingGridData), nameof(PackedCullingGridData.GetPackedFilePathForGrid));
    }

    [PatchPostfix]
    private static void Postfix(PerfectCullingAdaptiveGrid grid, ref string __result)
    {
        LighthouseSidecars.PackedPath(grid, ref __result);
    }
}
