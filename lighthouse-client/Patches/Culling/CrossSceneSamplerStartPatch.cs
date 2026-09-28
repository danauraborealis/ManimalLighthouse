using HarmonyLib;
using Koenigz.PerfectCulling.EFT;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.Culling;

// the sampler needs a PerfectCullingCamera, which fika destroys on a headless.
internal sealed class CrossSceneSamplerStartPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(PerfectCullingCrossSceneSampler), nameof(PerfectCullingCrossSceneSampler.Start));
    }

    [PatchPrefix]
    private static bool Prefix(PerfectCullingCrossSceneSampler __instance)
    {
        if (!LighthouseHeadless.Active || !LighthouseSceneLoader.HasReplacement)
        {
            return true;
        }

        __instance.enabled = false;
        Plugin.Log.LogInfo("Lighthouse: Perfect Culling sampler stood down on the headless (scene " + __instance.gameObject.scene.name + ").");

        return false;
    }
}
