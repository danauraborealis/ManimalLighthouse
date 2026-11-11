using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Manimal.Lighthouse.Shared;
using UnityEngine.SceneManagement;

namespace Manimal.Lighthouse.Client;

[BepInPlugin(ModIdentity.Guid, ModIdentity.ClientName, ModIdentity.Version)]
[BepInDependency("com.arys.unitytoolkit")]
[BepInDependency("xyz.drakia.waypoints", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    
    internal static string Root = "";
    
    internal static ConfigEntry<bool> AllowProbe = null!;
    internal static ConfigEntry<bool> AllowTest = null!;
    internal static ConfigEntry<bool> CaptureRain = null!;

    private void Awake()
    {
        Log = Logger;
        Root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        AllowProbe = Config.Bind("Development", "AllowProbeContent", false, "Allow explicitly marked loader-test content. Leave disabled for normal raids.");
        AllowTest = Config.Bind("Development", "AllowTestContent", false, "Load the explicitly marked full-map test build, with native SPT environment managers and documented incomplete features.");
        CaptureRain = Config.Bind("Diagnostics", "CaptureRain", false, "Capture rain material/depth diagnostics. Expensive scene scans and GPU readback; enable only for an explicitly requested capture.");
        // BepInEx/cache is the conventional throwaway spot: deleting it just forces one full rehash.
        // fully qualified — an unqualified Paths resolves to some other referenced type here
        LighthouseVerifiedFiles.CacheFile = Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "manimal-lighthouse-verified-files.txt");

        LighthousePatchLifetime.InstallGlobal();

        SceneManager.sceneLoaded += LighthouseQuestTriggers.SceneLoaded;
        SceneManager.sceneLoaded += LighthouseAmbience.SceneLoaded;
        SceneManager.sceneLoaded += LighthouseShaderRebind.SceneLoaded;
        SceneManager.sceneLoaded += LighthouseTestEnvironment.SceneLoaded;
        SceneManager.sceneLoaded += LighthouseAudioRouting.SceneLoaded;
        SceneManager.sceneUnloaded += LighthouseSceneLoader.SceneUnloaded;

        Log.LogInfo("Lighthouse loader registered. Matching client/server content is required; development test content requires explicit enablement.");

        if (LighthouseBindingVerification.Requested())
        {
            LighthouseBindingVerification.RunAndQuit();
        }
    }

    private void OnDestroy()
    {
        // The loader keeps its static unload observer until any owned/donor
        // scenes finish draining; it also retires global interception once a
        // pending replacement operation is no longer using the reverse stub.
        LighthouseSceneLoader.BeginShutdown();
    }

    internal static void DetachSceneCallbacks()
    {
        SceneManager.sceneLoaded -= LighthouseQuestTriggers.SceneLoaded;
        SceneManager.sceneLoaded -= LighthouseAmbience.SceneLoaded;
        SceneManager.sceneLoaded -= LighthouseShaderRebind.SceneLoaded;
        SceneManager.sceneLoaded -= LighthouseTestEnvironment.SceneLoaded;
        SceneManager.sceneLoaded -= LighthouseAudioRouting.SceneLoaded;
    }
}
