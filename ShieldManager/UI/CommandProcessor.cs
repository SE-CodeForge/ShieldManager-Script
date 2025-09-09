using System.Text;

namespace IngameScript
{
    public class CommandProcessor
    {
        private readonly Program program;
        private readonly ShieldController shieldController;
        private readonly ThreatAnalyzer threatAnalyzer;
        private readonly DisplayManager displayManager;

        public CommandProcessor(Program program, ShieldController shieldController, ThreatAnalyzer threatAnalyzer, DisplayManager displayManager)
        {
            this.program = program;
            this.shieldController = shieldController;
            this.threatAnalyzer = threatAnalyzer;
            this.displayManager = displayManager;
        }

        public void ProcessArguments(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return;
            arg = arg.ToLower().Trim();

            switch (arg)
            {
                case "kinetic":
                case "energy":
                case "explosive":
                case "balanced":
                    shieldController.ForceShuntMode(arg);
                    displayManager.ClearPersistentOutputs();
                    return;
                    
                case "enableshunt":
                    TestEnableShuntShields();
                    return;
                    
                case "testchanges":
                    TestShuntChanges();
                    return;
                    
                case "teststate":
                    TestShuntState();
                    return;
                    
                case "cycleshunt":
                    StartShuntCycling();
                    return;
                    
                case "stopcycle":
                    StopShuntCycling();
                    return;
                    
                case "reenauto":
                    shieldController.ReEnableAuto();
                    displayManager.ShowPersistentOutput("Automatic shunt management re-enabled");
                    return;
                    
                case "clearforceshunt":
                    shieldController.ClearForceShunt();
                    displayManager.ShowPersistentOutput("Force shunt cleared");
                    return;
                    
                case "listactions":
                    displayManager.ListAllShieldActions();
                    return;
                    
                case "testshunt":
                    TestShuntActions();
                    return;
                    
                case "shuntdebug":
                    DumpShuntDebug();
                    return;
                    
                case "debug":
                    program.Runtime.UpdateFrequency = program.Runtime.UpdateFrequency;
                    displayManager.ShowPersistentOutput("Debug: " + (!program.Me.CustomData.Contains("DEBUG=true") ? "ON" : "OFF"));
                    return;
                    
                case "clear":
                    displayManager.ClearPersistentOutputs();
                    return;
            }
        }

        private void StartShuntCycling()
        {
            shieldController.StartShuntCycling();
            
            var output = new StringBuilder();
            output.AppendLine("=== SHUNT CYCLING STARTED ===");
            output.AppendLine("Cycling through: BALANCED → KINETIC → ENERGY → EXPLOSIVE");
            output.AppendLine("Cycle interval: 3 seconds");
            output.AppendLine("");
            output.AppendLine("Watch Defense Shields terminal for changes!");
            output.AppendLine("Run 'stopcycle' to stop cycling");
            output.AppendLine("Current mode: BALANCED");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void StopShuntCycling()
        {
            shieldController.StopShuntCycling();
            
            var output = new StringBuilder();
            output.AppendLine("=== SHUNT CYCLING STOPPED ===");
            output.AppendLine("Final mode: " + shieldController.LastAppliedShunt.ToUpper());
            output.AppendLine("");
            output.AppendLine("Run 'reenauto' to re-enable automatic management");
            output.AppendLine("Or use manual commands: kinetic, energy, explosive, balanced");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void TestEnableShuntShields()
        {
            var output = new StringBuilder();
            output.AppendLine("=== TESTING SHUNT SHIELDS ENABLED ===");
            output.AppendLine("This test is now integrated into the shield controller.");
            output.AppendLine("Shield faces are automatically enabled when applying shunts.");
            output.AppendLine("");
            output.AppendLine("Check Defense Shields terminal - 'Shunt Shields' should be enabled");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void TestShuntChanges()
        {
            var output = new StringBuilder();
            output.AppendLine("=== TESTING SHUNT CHANGES ===");
            output.AppendLine("Applying ENERGY mode for testing...");
            
            shieldController.ForceShuntMode("energy");
            
            output.AppendLine("✓ Energy mode applied");
            output.AppendLine("Expected: Top & Bottom shields strengthened");
            output.AppendLine("Expected: Other shields at normal power");
            output.AppendLine("");
            output.AppendLine("Check Defense Shields terminal now!");
            output.AppendLine("Run 'reenauto' to re-enable automatic mode");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void TestShuntState()
        {
            var output = new StringBuilder();
            output.AppendLine("=== COMPLETE SHUNT STATE DEBUG ===");
            output.AppendLine($"Current Shunt: {shieldController.LastAppliedShunt}");
            output.AppendLine($"Force Mode: {(shieldController.ForceShunt ? shieldController.ForcedShuntMode : "none")}");
            output.AppendLine($"Cycling: {(shieldController.CyclingShunts ? "active" : "inactive")}");
            output.AppendLine("");
            output.AppendLine("Threat Analysis:");
            output.AppendLine($"Total Threats: {threatAnalyzer.GetTotalThreats()}");
            output.AppendLine($"Directional Threats: {threatAnalyzer.GetTotalDirectionalThreats()}");
            output.AppendLine($"WeaponCore API: {(threatAnalyzer.WcApiActive ? "active" : "inactive")}");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void TestShuntActions()
        {
            var output = new StringBuilder();
            output.AppendLine("=== TESTING SHUNT ACTIONS ===");
            output.AppendLine("Testing kinetic mode application...");
            
            shieldController.ForceShuntMode("kinetic");
            
            output.AppendLine("✓ Kinetic mode applied");
            output.AppendLine("Expected: Front & Back shields strengthened");
            output.AppendLine("Expected: Other shields at normal power");
            output.AppendLine("");
            output.AppendLine("Check Defense Shields terminal now!");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void DumpShuntDebug()
        {
            var output = new StringBuilder();
            output.AppendLine("=== SHUNT DEBUG ===");
            output.AppendLine("Current: " + shieldController.LastAppliedShunt);
            output.AppendLine("Forced: " + (shieldController.ForceShunt ? shieldController.ForcedShuntMode : "none"));
            output.AppendLine("Cycling: " + (shieldController.CyclingShunts ? "active" : "none"));
            output.AppendLine("Recent Threats: " + threatAnalyzer.RecentThreatCount);
            output.AppendLine("K/E/X: " + threatAnalyzer.CachedThreats["kinetic"] + "/" + 
                             threatAnalyzer.CachedThreats["energy"] + "/" + threatAnalyzer.CachedThreats["explosive"]);
            
            displayManager.ShowPersistentOutput(output.ToString());
        }
    }
}