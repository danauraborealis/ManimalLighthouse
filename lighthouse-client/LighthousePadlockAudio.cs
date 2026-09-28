using System;
using EFT.GameTriggers;

namespace Manimal.Lighthouse.Client;

internal static class LighthousePadlockAudio
{
    internal static void BeforePlay(HandlerPlaySoundAdvanced __instance, ref HandlerPlaySoundAdvanced.PlaySoundConfig __0)
    {
        if (LighthouseHeadless.Active || !LighthouseSceneLoader.Owns(__instance.gameObject.scene.name))
        {
            return;
        }

        var trigger = __instance.GetComponentInParent<TriggerBallistic>();

        if (!trigger || !trigger.gameObject.name.StartsWith("INTERACTIVE_Shootable_Padlock_set", StringComparison.Ordinal)
            || trigger._triggerId != __instance._playTriggerId || !trigger._targetCollider)
        {
            return;
        }

        var audio = MonoBehaviourSingleton<BetterAudio>.Instance;

        if (audio && audio.EnvTechnicalSoundsGroup)
        {
            __0.MixerGroup = audio.EnvTechnicalSoundsGroup;
        }
    }
}
