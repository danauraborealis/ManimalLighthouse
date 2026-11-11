using System.Reflection;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Manimal.Lighthouse.Client.Patches.Ambience;

internal sealed class AudioLifecyclePatch : LighthouseOwnedPatch<AudioLifecyclePatch>
{
    private readonly MethodInfo _targetMethod;

    internal AudioLifecyclePatch(MethodInfo targetMethod) : base(targetMethod)
    {
        _targetMethod = targetMethod;
    }

    protected override MethodBase GetTargetMethod()
    {
        return _targetMethod;
    }

    [PatchPrefix]
    private static bool Prefix(MonoBehaviour __instance)
    {
        return LighthouseAmbience.BeforeLifecycle(__instance);
    }
}
