using System.Reflection;
using EFT.Visual;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.Lighthouse.Client.Patches.BuoyFlicker;

internal sealed class AwakePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LightFlicker), nameof(LightFlicker.Awake));
    }

    [PatchPrefix]
    private static void Prefix(LightFlicker __instance)
    {
        LighthouseBuoyFlicker.BeforeAwake(__instance);
    }
}
