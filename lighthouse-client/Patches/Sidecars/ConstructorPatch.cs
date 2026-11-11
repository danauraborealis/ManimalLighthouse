using Audio.SpatialSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace Manimal.Lighthouse.Client.Patches.Sidecars;

internal sealed class ConstructorPatch : LighthouseOwnedPatch<ConstructorPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Constructor(typeof(SpatialAudioDataLoader), [typeof(string), typeof(MonoBehaviour)]);
    }

    [PatchPrefix]
    private static void Prefix(ref string dataPath, MonoBehaviour runner)
    {
        LighthouseSidecars.AudioPath(ref dataPath, runner);
    }
}
