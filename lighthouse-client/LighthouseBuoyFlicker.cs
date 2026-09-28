using System;
using EFT.Visual;
using UnityEngine;

namespace Manimal.Lighthouse.Client;

internal static class LighthouseBuoyFlicker
{
    private const string SceneName = "Lighthouse_Light_ML";
    private const string BuoyPrefix = "Sea_buoy";
    private const string FloatingObjectName = "FloatingObject";
    private const string PointLightName = "Point light";

    internal static void BeforeAwake(LightFlicker instance)
    {
        if (!instance)
        {
            return;
        }

        var sceneName = instance.gameObject.scene.name;

        if (!string.Equals(sceneName, SceneName, StringComparison.Ordinal) ||
            !LighthouseSceneLoader.Owns(sceneName) ||
            !IsBuoyPointLight(instance.transform))
        {
            return;
        }

        if (RepairCurve(instance.Curve))
        {
            Plugin.Log.LogInfo("Lighthouse buoy flicker: repaired NaN AnimationCurve tangents on " +
                               instance.transform.parent!.parent!.name + "/" + FloatingObjectName + "/" +
                               PointLightName + ".");
        }
    }

    internal static bool RepairCurve(AnimationCurve? curve)
    {
        if (curve == null)
        {
            return false;
        }

        var keys = curve.keys;
        var changed = false;

        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var keyChanged = false;

            if (float.IsNaN(key.inTangent))
            {
                key.inTangent = 0f;
                keyChanged = true;
                changed = true;
            }

            if (float.IsNaN(key.outTangent))
            {
                key.outTangent = 0f;
                keyChanged = true;
                changed = true;
            }

            if (keyChanged)
            {
                keys[i] = key;
            }
        }

        if (changed)
        {
            curve.keys = keys;
        }

        return changed;
    }

    private static bool IsBuoyPointLight(Transform transform)
    {
        if (!transform || !string.Equals(transform.name, PointLightName, StringComparison.Ordinal))
        {
            return false;
        }

        var floatingObject = transform.parent;

        if (!floatingObject || !string.Equals(floatingObject.name, FloatingObjectName, StringComparison.Ordinal))
        {
            return false;
        }

        var buoy = floatingObject.parent;
        return buoy && buoy.name.StartsWith(BuoyPrefix, StringComparison.Ordinal);
    }
}
