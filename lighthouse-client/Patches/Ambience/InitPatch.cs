using Audio.AmbientSubsystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Ambience;

internal sealed class InitPatch : LighthouseOwnedPatch<InitPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(AmbientAudioSystem), nameof(AmbientAudioSystem.Init));
    }

    [PatchPostfix]
    private static void Postfix(AmbientAudioSystem __instance)
    {
        LighthouseAmbience.Initialized(__instance);
    }
}
