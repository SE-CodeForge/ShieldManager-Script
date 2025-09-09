using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    internal class ConfigManager
    {
        private readonly Program program;

        // Configuration
        public bool ADAPTIVE_SHUNT = true;
        public bool DEBUG = false;
        public int ticks = 0;
        public const int TICKS_PER_SECOND = 6;
        public const int THREAT_REFRESH_INTERVAL = 60;
        public const int SHUNT_TIMEOUT = 300;
        public const int POLL_MEMORY = 50;
        public const int CYCLE_INTERVAL = 180; // 3 seconds at 60 ticks per second
        public const int PERSISTENT_OUTPUT_DURATION = 300; // 5 seconds at 60 ticks per second

        public readonly string[] cycleOrder = { "balanced", "kinetic", "energy", "explosive" };

        public ConfigManager(Program program)
        {
            this.program = program;
        }

        public void IncrementTicks()
        {
            ticks++;
        }

        public void SetShuntRecommendation(string mode)
        {
            var lines = program.Me.CustomData.Split('\n').ToList();
            var found = false;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("CurrentShunt="))
                {
                    lines[i] = "CurrentShunt=" + mode;
                    found = true;
                    break;
                }
            }
            if (!found) lines.Add("CurrentShunt=" + mode);
            program.Me.CustomData = string.Join("\n", lines);
        }

        public string GetCurrentShunt()
        {
            var lines = program.Me.CustomData.Split('\n');
            for (var i = lines.Length - 1; i >= 0; i--)
                if (lines[i].StartsWith("CurrentShunt="))
                    return lines[i].Substring(13);
            return "balanced";
        }
    }
}