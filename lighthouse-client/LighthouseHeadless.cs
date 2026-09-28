using System;
using System.Reflection;

namespace Manimal.Lighthouse.Client;

// on a fika headless Shader.isSupported is false for every shader, so nothing here can bind.
internal static class LighthouseHeadless
{
    private const string BackendUtilsTypeName = "FikaBackendUtils";
    private const string HeadlessPropertyName = "IsHeadless";
    private const string HeadlessAssemblyName = "Fika.Headless";

    private static bool _resolved;
    private static bool _active;

    // lazy: BepInEx may load Fika.Headless after this plugin.
    internal static bool Active
    {
        get
        {
            if (_resolved)
            {
                return _active;
            }

            _resolved = true;
            _active = Detect();

            if (_active)
            {
                Plugin.Log.LogInfo("Lighthouse: Fika headless host detected; rendering and audio stand down for this process.");
            }

            return _active;
        }
    }

    private static bool Detect()
    {
        var property = FindHeadlessProperty();

        if (property is not null)
        {
            return property.GetValue(null) is true;
        }

        if (!IsAssemblyLoaded(HeadlessAssemblyName))
        {
            return false;
        }

        Plugin.Log.LogWarning(
            "Lighthouse: Fika's " + BackendUtilsTypeName + "." + HeadlessPropertyName +
            " was not recognized; falling back to the presence of " + HeadlessAssemblyName + ".");

        return true;
    }

    // simple name: fika moves this type's namespace between versions.
    private static PropertyInfo? FindHeadlessProperty()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type.Name != BackendUtilsTypeName)
                {
                    continue;
                }

                var property = type.GetProperty(HeadlessPropertyName, BindingFlags.Public | BindingFlags.Static);

                if (property is not null && property.PropertyType == typeof(bool) && property.CanRead)
                {
                    return property;
                }
            }
        }

        return null;
    }

    private static bool IsAssemblyLoaded(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
