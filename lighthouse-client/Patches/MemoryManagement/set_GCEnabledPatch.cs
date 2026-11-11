using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.MemoryManagement;

internal sealed class set_GCEnabledPatch : LighthouseOwnedPatch<set_GCEnabledPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.PropertySetter(typeof(InGameMemoryManagement), nameof(InGameMemoryManagement.GCEnabled));
    }

    [PatchPrefix]
    private static void Prefix(ref bool __0)
    {
        LighthouseMemoryManagement.BeforeSet(ref __0);
    }
}
