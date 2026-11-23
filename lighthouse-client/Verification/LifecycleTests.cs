using System.Reflection;
using Manimal.Lighthouse.Client.Patches.ShaderRebind;
using SPT.Reflection.Patching;

namespace Manimal.Lighthouse.Client;

internal static class LifecycleTests
{
    internal static void Run()
    {
        LighthouseSceneLoader.IsShuttingDown = false;
        LighthousePatchLifetime.Release();
        LighthousePatchLifetime.DisableGlobal();
        ModulePatch.ResetCounters();

        VerifyOwnerIds();
        VerifyGlobalAndScopedIdempotency();
        VerifyReleaseAndReentry();
        VerifyPartialEnableRollback();
        VerifyShutdownBlocksAcquisition();

        LighthousePatchLifetime.Release();
        Assert(LighthousePatchLifetime.DisableGlobal(), "global owners disable cleanly during shutdown teardown");
        Assert(!LighthousePatchLifetime.IsGlobalInstalled, "global owners are retired after shutdown teardown");
        Assert(ModulePatch.ActiveCount == 0, "all mock owners are inactive after lifecycle tests");
        Console.WriteLine("PASS scoped patch lifecycle behavior checks");
    }

    private static void VerifyOwnerIds()
    {
        var init = new InitPatch();
        var started = new OnGameStartedPatch();
        Assert(init.HarmonyId.StartsWith("com.manimal.lighthouse.Manimal.Lighthouse.Client.Patches.ShaderRebind.InitPatch", StringComparison.Ordinal),
            "owner id includes mod guid and full patch type");
        Assert(init.HarmonyId != started.HarmonyId, "same short names in different namespaces get distinct owner ids");

        var methodA = new MethodScopedPatch(typeof(LifecycleTests).GetMethod(nameof(MethodA), BindingFlags.NonPublic | BindingFlags.Static)!);
        var methodB = new MethodScopedPatch(typeof(LifecycleTests).GetMethod(nameof(MethodB), BindingFlags.NonPublic | BindingFlags.Static)!);
        Assert(methodA.HarmonyId != methodB.HarmonyId, "dynamic lifecycle owners include method identity");
        Assert(methodA.HarmonyId.Contains(nameof(MethodA), StringComparison.Ordinal), "dynamic owner identifies target method");
    }

    private static void VerifyGlobalAndScopedIdempotency()
    {
        LighthousePatchLifetime.InstallGlobal();
        Assert(LighthousePatchLifetime.IsGlobalInstalled, "global scene-loader owners are installed");
        Assert(ModulePatch.ActiveCount == 2, "only two global owners are active before a raid");
        var globalEnableCalls = ModulePatch.EnableCalls;

        LighthousePatchLifetime.InstallGlobal();
        Assert(ModulePatch.EnableCalls == globalEnableCalls, "global installation is idempotent");

        LighthousePatchLifetime.Acquire();
        Assert(LighthousePatchLifetime.IsAcquired, "scoped owners are acquired");
        Assert(ModulePatch.ActiveCount == 31, "29 scoped owners plus two global owners are active");
        Assert(LighthouseAmbience.InstallCalls == 1 && LighthouseWaypointsCompatibility.EnableCalls == 1,
            "dynamic ambience and optional Waypoints owners install once");
    }

    private static void VerifyReleaseAndReentry()
    {
        var enableCalls = ModulePatch.EnableCalls;
        LighthousePatchLifetime.Acquire();
        Assert(ModulePatch.EnableCalls == enableCalls, "scoped acquisition is idempotent while active");

        LighthousePatchLifetime.Release();
        Assert(!LighthousePatchLifetime.IsAcquired, "scoped owners release as a group");
        Assert(ModulePatch.ActiveCount == 2, "global owners survive scoped release");
        Assert(LighthouseAmbience.ReleaseCalls == 1 && LighthouseWaypointsCompatibility.DisableCalls == 1,
            "dynamic ambience and optional Waypoints owners release with the scope");

        LighthousePatchLifetime.Acquire();
        Assert(ModulePatch.ActiveCount == 31, "scoped owners can be acquired again for re-entry");
        Assert(LighthouseAmbience.InstallCalls == 2 && LighthouseWaypointsCompatibility.EnableCalls == 2,
            "dynamic ambience and optional Waypoints owners re-enter");
        LighthousePatchLifetime.Release();
        Assert(ModulePatch.ActiveCount == 2, "re-entry release leaves only global owners");
    }

    private static void VerifyPartialEnableRollback()
    {
        var failedType = typeof(Patches.BundledScenes.LoadScenePatch).FullName!;
        ModulePatch.FailOnEnableType = failedType;
        var disableCalls = ModulePatch.DisableCalls;
        var threw = false;

        try
        {
            LighthousePatchLifetime.Acquire();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        ModulePatch.FailOnEnableType = null;
        Assert(threw, "partial enable failure is surfaced");
        Assert(!LighthousePatchLifetime.IsAcquired, "partial enable clears the scoped lifetime");
        Assert(ModulePatch.ActiveCount == 2, "partial enable rollback preserves only global owners");
        Assert(ModulePatch.DisableCalls > disableCalls, "rollback disables owners recorded before Enable returned");
    }

    private static void VerifyShutdownBlocksAcquisition()
    {
        LighthouseSceneLoader.IsShuttingDown = true;
        var enableCalls = ModulePatch.EnableCalls;
        var threw = false;

        try
        {
            LighthousePatchLifetime.Acquire();
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert(threw, "shutdown rejects a new scoped acquisition");
        Assert(!LighthousePatchLifetime.IsAcquired, "shutdown does not retain scoped owners");
        Assert(ModulePatch.EnableCalls == enableCalls, "shutdown rejection does not enable a patch");

        threw = false;
        try
        {
            LighthousePatchLifetime.InstallGlobal();
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert(threw, "shutdown rejects global patch installation");
        LighthouseSceneLoader.IsShuttingDown = false;
    }

    private static void MethodA()
    {
    }

    private static void MethodB()
    {
    }

    private sealed class MethodScopedPatch : LighthouseOwnedPatch<MethodScopedPatch>
    {
        private readonly MethodBase _target;

        internal MethodScopedPatch(MethodBase target) : base(target)
        {
            _target = target;
        }

        protected override MethodBase GetTargetMethod() => _target;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("FAIL " + message);
        }

        Console.WriteLine("PASS " + message);
    }
}
