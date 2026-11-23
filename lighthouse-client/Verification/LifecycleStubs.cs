using System.Reflection;
using Manimal.Lighthouse.Client;

namespace SPT.Reflection.Patching
{

// Test-only stand-in for SPT's ModulePatch. It records owner ids and active
// instances while preserving the public Enable/Disable/IsActive contract used
// by LighthousePatchLifetime.
public class ModulePatch
{
    private static readonly List<ModulePatch> Active = [];

    public static string? FailOnEnableType { get; set; }
    public static int EnableCalls { get; private set; }
    public static int DisableCalls { get; private set; }
    public static int ActiveCount => Active.Count;

    public string HarmonyId { get; }
    public bool IsActive { get; private set; }

    protected ModulePatch() : this(null)
    {
    }

    protected ModulePatch(string? name)
    {
        HarmonyId = name ?? GetType().Name;
    }

    public virtual void Enable()
    {
        EnableCalls++;

        if (FailOnEnableType == GetType().FullName)
        {
            throw new InvalidOperationException("mock enable failure for " + GetType().FullName);
        }

        IsActive = true;
        if (!Active.Contains(this))
        {
            Active.Add(this);
        }
    }

    public virtual void Disable()
    {
        DisableCalls++;
        IsActive = false;
        Active.Remove(this);
    }

    protected virtual MethodBase? GetTargetMethod() => null;

    public static void ResetCounters()
    {
        FailOnEnableType = null;
        EnableCalls = 0;
        DisableCalls = 0;
    }
}

}

namespace Manimal.Lighthouse.Client
{

internal static class Plugin
{
    internal static readonly MockLog Log = new();
}

internal sealed class MockLog
{
    internal readonly List<string> Errors = [];

    internal void LogError(object message) => Errors.Add(message.ToString() ?? "");
}

internal static class LighthouseSceneLoader
{
    internal static bool IsShuttingDown { get; set; }
}

internal static class LighthouseAmbience
{
    private static bool _installed;
    internal static int InstallCalls { get; private set; }
    internal static int ReleaseCalls { get; private set; }

    internal static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        InstallCalls++;
    }

    internal static void Release()
    {
        if (!_installed)
        {
            return;
        }

        _installed = false;
        ReleaseCalls++;
    }
}

internal static class LighthouseWaypointsCompatibility
{
    private static bool _enabled;
    internal static int EnableCalls { get; private set; }
    internal static int DisableCalls { get; private set; }

    internal static void EnableIfAvailable()
    {
        if (_enabled)
        {
            return;
        }

        _enabled = true;
        EnableCalls++;
    }

    internal static void Disable()
    {
        if (!_enabled)
        {
            return;
        }

        _enabled = false;
        DisableCalls++;
    }
}

}

namespace Manimal.Lighthouse.Client.Patches.Ambience
{

internal sealed class InitPatch : LighthouseOwnedPatch<InitPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.Btr
{

internal sealed class InitBTROnClientPatch : LighthouseOwnedPatch<InitBTROnClientPatch>
{
}

internal sealed class InitBTRServerPatch : LighthouseOwnedPatch<InitBTRServerPatch>
{
}

internal sealed class LoadBTRVehiclePatch : LighthouseOwnedPatch<LoadBTRVehiclePatch>
{
}

internal sealed class LoadMapPathsConfigurationPatch : LighthouseOwnedPatch<LoadMapPathsConfigurationPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.BundledScenes
{

internal sealed class LoadScenePatch : LighthouseOwnedPatch<LoadScenePatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.BuoyFlicker
{

internal sealed class AwakePatch : LighthouseOwnedPatch<AwakePatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.Culling
{

internal sealed class CrossSceneSamplerStartPatch : LighthouseOwnedPatch<CrossSceneSamplerStartPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.FrameStats
{

internal sealed class OnGameStartedPatch : LighthouseOwnedPatch<OnGameStartedPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.KeeperRestore
{

internal sealed class BufferZoneContainerAwakePatch : LighthouseOwnedPatch<BufferZoneContainerAwakePatch>
{
}

internal sealed class KeeperZoneAwakePatch : LighthouseOwnedPatch<KeeperZoneAwakePatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.KeyCleanup
{

internal sealed class BaseUpdatePatch : LighthouseOwnedPatch<BaseUpdatePatch>
{
}

internal sealed class EnterPatch : LighthouseOwnedPatch<EnterPatch>
{
}

internal sealed class ExitPatch : LighthouseOwnedPatch<ExitPatch>
{
}

internal sealed class SpawnPatch : LighthouseOwnedPatch<SpawnPatch>
{
}

internal sealed class StopPatch : LighthouseOwnedPatch<StopPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.MemoryManagement
{

internal sealed class set_GCEnabledPatch : LighthouseOwnedPatch<set_GCEnabledPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.PadlockAudio
{

internal sealed class PlaySoundPatch : LighthouseOwnedPatch<PlaySoundPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.RainDiagnostics
{

internal sealed class InitPatch : LighthouseOwnedPatch<InitPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.SceneCaches
{

internal sealed class AwakePatch : LighthouseOwnedPatch<AwakePatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.SceneLoader
{

internal sealed class LoadPresetAsyncPatch : LighthouseOwnedPatch<LoadPresetAsyncPatch>
{
}

internal sealed class LoadPresetAsyncReversePatch : LighthouseOwnedPatch<LoadPresetAsyncReversePatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.ShaderRebind
{

internal sealed class InitPatch : LighthouseOwnedPatch<InitPatch>
{
}

internal sealed class OnGameStartedPatch : LighthouseOwnedPatch<OnGameStartedPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.Sidecars
{

internal sealed class ConstructorPatch : LighthouseOwnedPatch<ConstructorPatch>
{
}

internal sealed class GetPackedFilePathForGridPatch : LighthouseOwnedPatch<GetPackedFilePathForGridPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.Sidecars.MetaXRAcousticGeometry
{

internal sealed class get_AbsoluteFilePathPatch : LighthouseOwnedPatch<get_AbsoluteFilePathPatch>
{
}

internal sealed class LoadGeometryAsyncPatch : LighthouseOwnedPatch<LoadGeometryAsyncPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.Sidecars.MetaXRAcousticMap
{

internal sealed class get_AbsoluteFilePathPatch : LighthouseOwnedPatch<get_AbsoluteFilePathPatch>
{
}

internal sealed class LoadMapAsyncPatch : LighthouseOwnedPatch<LoadMapAsyncPatch>
{
}

}

namespace Manimal.Lighthouse.Client.Patches.TunnelDoor
{

internal sealed class OnEnablePatch : LighthouseOwnedPatch<OnEnablePatch>
{
}

}
