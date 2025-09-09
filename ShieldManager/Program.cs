// TEKKONIC Shield Manager v2.0 - Auto Shunt Focus
// Core: WeaponCore threat detection + Defense Shields shunt modulation

using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRageMath;

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
            // Initialize all components
            configManager = new ConfigManager(this);
            shieldController = new ShieldController(this, configManager);
            threatAnalyzer = new ThreatAnalyzer(this, configManager);
            displayManager = new DisplayManager(this, configManager);
            commandProcessor = new CommandProcessor(this, shieldController, threatAnalyzer, displayManager);
            testSuite = new TestSuite(this, shieldController, displayManager);

            threatAnalyzer.SetShieldController(shieldController);
            displayManager.SetControllers(shieldController, threatAnalyzer);

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
            shieldController.UpdateShuntCycling();
            
            if (configManager.ADAPTIVE_SHUNT && !shieldController.CyclingShunts) 
            {
                threatAnalyzer.ManageShuntSystems();
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