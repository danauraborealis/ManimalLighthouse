using System;
using Comfort.Common;
using EFT;

namespace Manimal.Lighthouse.Client;

// the cultist harbinger wave and glukhar share the water treatment plant: one or the other per raid.
// night is the game's own cultist window (23:00 until a dawn rolled per raid between 05:30 and 06:30),
// read from the controller that enforces it
internal static class LighthouseNightBoss
{
    internal static void Apply(BotZonesLeaveController controller)
    {
        var botGame = Singleton<IBotGame>.Instance;
        var scenario = botGame?.BossSpawnScenario;
        var waves = scenario?.BossSpawnWaves;
        var gameWorld = Singleton<GameWorld>.Instance;

        if (waves is null || !gameWorld || !string.Equals(gameWorld.LocationId, "Lighthouse", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // a server without the cultist wave keeps glukhar around the clock
        var hasCult = false;

        foreach (var wave in waves)
        {
            if (wave is not null && wave.BossType == WildSpawnType.sectantPredvestnik)
            {
                hasCult = true;
            }
        }

        if (!hasCult)
        {
            return;
        }

        var night = !controller.IsDay();
        var dropped = WildSpawnType.marksman;
        var sectants = false;

        foreach (var wave in waves)
        {
            if (wave is null || !wave.ShallSpawn)
            {
                continue;
            }

            if ((night && wave.BossType == WildSpawnType.bossGluhar) || (!night && wave.BossType == WildSpawnType.sectantPredvestnik))
            {
                wave.ShallSpawn = false;
                dropped = wave.BossType;
                continue;
            }

            if (wave.BossType.IsSectant())
            {
                sectants = true;
            }
        }

        // Activate reads this to set up the day/night cultist zone blocks
        scenario!.HaveSectants = sectants;
        controller._haveSectants = sectants;

        Plugin.Log.LogInfo("Lighthouse treatment plant boss: " + botGame!.GameDateTime.Calculate().ToString("HH:mm")
            + (night ? " night" : " day") + " (dawn " + controller._startDayHour.ToString("00") + ":" + controller._startDayMin.ToString("00") + ")"
            + (dropped == WildSpawnType.marksman ? "" : ", dropped " + dropped) + ", cultists " + (sectants ? "on" : "off"));
    }
}
