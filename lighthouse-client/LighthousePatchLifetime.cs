using System;
using System.Collections.Generic;
using System.Reflection;
using Manimal.Lighthouse.Shared;
using SPT.Reflection.Patching;

namespace Manimal.Lighthouse.Client;

// ModulePatch defaults its Harmony id to the CLR type's short name. That is
// not unique when two patch classes share a name (and is especially unsafe
// for repeatable raid lifetimes), so all Lighthouse-owned patches use an id
// derived from the mod id and their complete patch type.
internal abstract class LighthouseOwnedPatch<TPatch> : ModulePatch
    where TPatch : LighthouseOwnedPatch<TPatch>
{
    protected LighthouseOwnedPatch()
        : base(BuildOwnerId())
    {
    }

    protected LighthouseOwnedPatch(MethodBase target)
        : base(BuildOwnerId(MethodIdentity(target)))
    {
    }

    private static string BuildOwnerId(string? method = null)
    {
        var typeName = typeof(TPatch).FullName ?? typeof(TPatch).Name;
        return method is null
            ? ModIdentity.Guid + "." + typeName
            : ModIdentity.Guid + "." + typeName + "." + method;
    }

    private static string MethodIdentity(MethodBase target)
    {
        var declaringType = target.DeclaringType?.FullName ?? "<global>";
        return declaringType + "::" + target.Name + "::" + target;
    }
}

// The scene-preset interception and its reverse stub stay installed for the
// lifetime of the plugin. Every other patch is acquired only while a
// Lighthouse replacement is loading/loaded and is released with its scenes.
internal static class LighthousePatchLifetime
{
    private static List<ModulePatch>? _patches;
    private static ModulePatch? _globalReversePatch;
    private static ModulePatch? _globalPresetPatch;

    internal static bool IsAcquired => _patches is not null;
    internal static bool IsGlobalInstalled => _globalReversePatch is not null || _globalPresetPatch is not null;

    internal static void InstallGlobal()
    {
        if (LighthouseSceneLoader.IsShuttingDown)
        {
            throw new OperationCanceledException("Lighthouse global patch installation was requested during shutdown.");
        }

        try
        {
            _globalReversePatch ??= new Patches.SceneLoader.LoadPresetAsyncReversePatch();
            if (!_globalReversePatch.IsActive)
            {
                _globalReversePatch.Enable();
            }

            _globalPresetPatch ??= new Patches.SceneLoader.LoadPresetAsyncPatch();
            if (!_globalPresetPatch.IsActive)
            {
                _globalPresetPatch.Enable();
            }
        }
        catch
        {
            DisableGlobal();
            throw;
        }
    }

    internal static bool DisableGlobal()
    {
        var success = true;

        // Stop interception first. The reverse stub is retained until the
        // active replacement operation has finished using it.
        if (_globalPresetPatch is not null)
        {
            try
            {
                _globalPresetPatch.Disable();
                _globalPresetPatch = null;
            }
            catch (Exception e)
            {
                success = false;
                Plugin.Log.LogError("Lighthouse global preset patch teardown failed: " + e);
            }
        }

        if (_globalReversePatch is not null)
        {
            try
            {
                _globalReversePatch.Disable();
                _globalReversePatch = null;
            }
            catch (Exception e)
            {
                success = false;
                Plugin.Log.LogError("Lighthouse global reverse patch teardown failed: " + e);
            }
        }

        return success;
    }

    internal static void Acquire()
    {
        if (LighthouseSceneLoader.IsShuttingDown)
        {
            throw new OperationCanceledException("Lighthouse patch acquisition was requested during shutdown.");
        }

        if (_patches is not null)
        {
            return;
        }

        _patches = new List<ModulePatch>();

        try
        {
            Add(new Patches.Sidecars.GetPackedFilePathForGridPatch());
            Add(new Patches.Sidecars.ConstructorPatch());
            Add(new Patches.Sidecars.MetaXRAcousticMap.get_AbsoluteFilePathPatch());
            Add(new Patches.Sidecars.MetaXRAcousticMap.LoadMapAsyncPatch());
            Add(new Patches.Sidecars.MetaXRAcousticGeometry.get_AbsoluteFilePathPatch());
            Add(new Patches.Sidecars.MetaXRAcousticGeometry.LoadGeometryAsyncPatch());
            Add(new Patches.ShaderRebind.InitPatch());
            Add(new Patches.ShaderRebind.OnGameStartedPatch());
            Add(new Patches.KeyCleanup.ExitPatch());
            Add(new Patches.KeyCleanup.EnterPatch());
            Add(new Patches.KeyCleanup.StopPatch());
            Add(new Patches.KeyCleanup.BaseUpdatePatch());
            Add(new Patches.KeyCleanup.SpawnPatch());
            Add(new Patches.TunnelDoor.OnEnablePatch());
            Add(new Patches.BuoyFlicker.AwakePatch());
            Add(new Patches.RainDiagnostics.InitPatch());
            Add(new Patches.PadlockAudio.PlaySoundPatch());
            Add(new Patches.BundledScenes.LoadScenePatch());
            Add(new Patches.SceneCaches.AwakePatch());
            Add(new Patches.KeeperRestore.BufferZoneContainerAwakePatch());
            Add(new Patches.KeeperRestore.KeeperZoneAwakePatch());
            Add(new Patches.MemoryManagement.set_GCEnabledPatch());
            Add(new Patches.Btr.LoadMapPathsConfigurationPatch());
            Add(new Patches.Btr.LoadBTRVehiclePatch());
            Add(new Patches.Btr.InitBTRServerPatch());
            Add(new Patches.Btr.InitBTROnClientPatch());
            Add(new Patches.FrameStats.OnGameStartedPatch());
            Add(new Patches.Culling.CrossSceneSamplerStartPatch());
            Add(new Patches.Ambience.InitPatch());
            Add(new Patches.Seasons.RunPatch());
        Add(new Patches.NightBoss.ConstructorPatch());

            // Both of these families are discovered at runtime and retain
            // their own patch instances so every owner can be disabled again.
            LighthouseAmbience.Install();
            LighthouseWaypointsCompatibility.EnableIfAvailable();
        }
        catch
        {
            // Add() records an instance before Enable(), which also removes
            // patches from a ModulePatch whose Enable() failed part-way.
            Release();
            throw;
        }
    }

    internal static void Release()
    {
        LighthouseWaypointsCompatibility.Disable();
        LighthouseAmbience.Release();

        var patches = _patches;
        _patches = null;

        if (patches is null)
        {
            return;
        }

        for (var i = patches.Count - 1; i >= 0; i--)
        {
            try
            {
                // Disable() unpatches by owner id even when Enable() failed
                // before ModulePatch could mark IsActive.
                patches[i].Disable();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Lighthouse patch teardown failed: " + e);
            }
        }

        patches.Clear();
    }

    private static void Add(ModulePatch patch)
    {
        _patches!.Add(patch);
        patch.Enable();
    }
}
