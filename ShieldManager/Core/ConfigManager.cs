using System.Linq;

namespace IngameScript
{
    internal class ConfigManager
    {
        private readonly Program program;

        // Configuration
        public bool ADAPTIVE_SHUNT = true;
        public readonly bool DEBUG = false;
        public int ticks = 0;
        public const int THREAT_REFRESH_INTERVAL = 60;
        public const int SHUNT_TIMEOUT = 300;
        public const int PERSISTENT_OUTPUT_DURATION = 300;

        public ConfigManager(Program program)
        {
            this.program = program;
            SetShuntRecommendation("balanced");
        }

        public void IncrementTicks()
        {
            ticks++;
        }

        public void SetShuntRecommendation(string mode)
        {
            const string key = "CurrentShunt=";
            var lines = program.Me.CustomData.Split('\n').ToList();
            var index = lines.FindIndex(line => line.StartsWith(key));
            var newEntry = key + mode;

            if (index != -1)
            {
                lines[index] = newEntry;
            }
            else
            {
                lines.Add(newEntry);
            }
            program.Me.CustomData = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        public string GetCurrentShunt()
        {
            const string key = "CurrentShunt=";
            var line = program.Me.CustomData.Split('\n').LastOrDefault(l => l.StartsWith(key));
            return line != null ? line.Substring(key.Length) : "balanced";
        }
    }
}