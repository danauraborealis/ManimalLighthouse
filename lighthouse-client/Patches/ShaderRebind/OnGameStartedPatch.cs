using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.ShaderRebind;

internal sealed class OnGameStartedPatch : LighthouseOwnedPatch<OnGameStartedPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    private static void Postfix()
    {
        LighthouseShaderRebind.RebindAll();
    }
}
