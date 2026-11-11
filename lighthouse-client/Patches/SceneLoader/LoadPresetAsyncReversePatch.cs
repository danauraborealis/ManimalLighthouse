using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.Lighthouse.Client.Patches.SceneLoader;

internal sealed class LoadPresetAsyncReversePatch : LighthouseOwnedPatch<LoadPresetAsyncReversePatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LoadScenesFromPresetOperation), nameof(LoadScenesFromPresetOperation.LoadPresetAsync));
    }

    [PatchReverse]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Task LoadOriginal(LoadScenesFromPresetOperation instance, ScenesPreset preset)
    {
        throw new NotSupportedException("Scene-loader reverse patch is not installed.");
    }
}
