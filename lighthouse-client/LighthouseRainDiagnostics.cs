using System;
using UnityEngine;

namespace Manimal.Lighthouse.Client;

internal static class LighthouseRainDiagnostics
{
    internal static void AfterInit(RainFallDrops __instance, MeshRenderer ____rainRenderer, ref Material ____closeCopy)
    {
        if (LighthouseHeadless.Active || !LighthouseSceneLoader.IsNativeDonor(__instance.gameObject.scene.name))
        {
            return;
        }

        var removed = LighthouseRainCopies.DisableSavedCopies(__instance.gameObject.scene, ____rainRenderer);

        Plugin.Log.LogInfo("Lighthouse rain: disabled " + removed + " saved rain renderers; preserved the live weather-controlled renderer.");

        try
        {
            if (!____rainRenderer)
            {
                throw new InvalidOperationException("Native rain renderer is missing.");
            }

            ____closeCopy = LighthouseRainRendering.CreateRuntimeMaterial(____closeCopy);
            ____rainRenderer.sharedMaterial = ____closeCopy;

            var liveMaterial = ____rainRenderer ? ____rainRenderer.sharedMaterial : null;

            if (!liveMaterial || !liveMaterial?.shader || liveMaterial?.shader.name != "Manimal/Lighthouse/RainDrops")
            {
                throw new InvalidOperationException("Live rain renderer did not receive the brightness override.");
            }

            Plugin.Log.LogInfo("Lighthouse rain: live renderer bound to Manimal/Lighthouse/RainDrops; native motion, density and roof mask preserved.");
        }
        catch (Exception error) { Plugin.Log.LogError("Lighthouse rain material repair failed: " + error); }

        if (!Plugin.CaptureRain.Value)
        {
            return;
        }

        if (__instance.GetComponent<LighthouseRainCapture>())
        {
            return;
        }

        var capture = __instance.gameObject.AddComponent<LighthouseRainCapture>();

        capture.Rain = __instance;
        capture.Renderer = ____rainRenderer;
        capture.Material = ____closeCopy;
        Plugin.Log.LogInfo("Lighthouse rain capture armed: up to 24 camera position/orientation samples during rain.");
    }
}