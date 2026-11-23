using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.Lighthouse.Client.Patches.NightBoss;

// the controller rolls this raid's dawn in its constructor, and boss waves don't run until after
// BotsController.Init — so this is the first point the game's own day/night answer exists
internal sealed class ConstructorPatch : LighthouseOwnedPatch<ConstructorPatch>
{
    protected override MethodBase GetTargetMethod()
    {
        var constructors = AccessTools.GetDeclaredConstructors(typeof(BotZonesLeaveController));

        if (constructors.Count != 1)
        {
            throw new MissingMethodException(nameof(BotZonesLeaveController), ".ctor");
        }

        return constructors[0];
    }

    [PatchPostfix]
    private static void Postfix(BotZonesLeaveController __instance)
    {
        try
        {
            LighthouseNightBoss.Apply(__instance);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("Lighthouse treatment plant boss swap failed: " + e);
        }
    }
}
