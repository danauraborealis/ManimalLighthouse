using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Manimal.Lighthouse.Client.Patches.SceneCaches;

internal sealed class AwakePatch : LighthouseOwnedPatch<AwakePatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocationScene), nameof(LocationScene.Awake));
    }

    [PatchPrefix]
    // Enable Labyrinth reads every scene's extract entries without null checks.
    // Remove missing backport references before its global scene prefix runs.
    [HarmonyBefore("LocationSceneAwakePatch")]
    private static void Prefix(LocationScene __instance)
    {
        LighthouseSceneCaches.BeforeAwake(__instance);
        LighthouseKeeperRestore.OnLocationSceneAwake(__instance);
    }
}
