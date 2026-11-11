using EFT.GameTriggers;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.PadlockAudio;

internal sealed class PlaySoundPatch : LighthouseOwnedPatch<PlaySoundPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(HandlerPlaySoundAdvanced), nameof(HandlerPlaySoundAdvanced.PlaySound), [typeof(HandlerPlaySoundAdvanced.PlaySoundConfig)
        ]);
    }

    [PatchPrefix]
    private static void Prefix(HandlerPlaySoundAdvanced __instance, ref HandlerPlaySoundAdvanced.PlaySoundConfig __0)
    {
        LighthousePadlockAudio.BeforePlay(__instance, ref __0);
    }
}
