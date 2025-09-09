// TEKKONIC Shield Manager v2.0 - Auto Shunt Focus
// Core: WeaponCore threat detection + Defense Shields shunt modulation

using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    internal class Program : MyGridProgram
    {
        // Core components
        private ShieldController shieldController;
        private ThreatAnalyzer threatAnalyzer;
        private DisplayManager displayManager;
        private CommandProcessor commandProcessor;
        private TestSuite testSuite;
        private ConfigManager configManager;

        public Program()
        {
            
            configManager = new ConfigManager(this);
            shieldController = new ShieldController(this, configManager);
            threatAnalyzer = new ThreatAnalyzer(this, configManager, shieldController);
            displayManager = new DisplayManager(this, configManager, shieldController, threatAnalyzer);
            commandProcessor = new CommandProcessor(this, shieldController, threatAnalyzer, displayManager);
            testSuite = new TestSuite(this, shieldController, displayManager);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Echo("TEKKONIC Shield Manager v2.0 - Auto Shunt Focus");
        }

        public void Main(string arg, UpdateType updateSource)
        {
            if (!shieldController.IsInitialized)
            {
                Echo("ERROR: No Defense Shields controller found!");
                return;
            }

            shieldController.PollShieldApi();
            commandProcessor.ProcessArguments(arg);
            
            if (configManager.ADAPTIVE_SHUNT) 
            {
                threatAnalyzer.AnalyzeDirectionalThreats();
            }
            
            displayManager.UpdateDisplays();
            configManager.IncrementTicks();
        }

        // Utility classes
        public sealed class DetectedEntityComparer : IEqualityComparer<MyDetectedEntityInfo>
        {
            public bool Equals(MyDetectedEntityInfo a, MyDetectedEntityInfo b) => a.EntityId == b.EntityId;
            public int GetHashCode(MyDetectedEntityInfo obj) => obj.EntityId.GetHashCode();
        }
    }
}