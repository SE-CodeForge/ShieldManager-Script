using System;
using System.Text;

namespace IngameScript
{
    internal class CommandProcessor
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
                // === DIRECTIONAL COMMANDS ===
                case "front":
                case "back":
                case "left":
                case "right":
                case "top":
                case "bottom":
                case "balanced":
                    shieldController.ForceShuntMode(arg);
                    displayManager.ClearPersistentOutputs();
                    return;

                case "clearforceshunt":
                    shieldController.ClearForceShunt();
                    displayManager.ShowPersistentOutput("Force shunt cleared");
                    return;

                // === SYSTEM CONTROL ===
                case "reenauto":
                    shieldController.ReEnableAuto();
                    displayManager.ShowPersistentOutput("Automatic directional shunt management re-enabled");
                    return;
                    
                case "clear":
                    displayManager.ClearPersistentOutputs();
                    return;
                    
                // === TESTING & DIAGNOSTICS ===
                case "shielddiag":
                    DiagnoseShieldConnection();
                    return;
                    
                case "teststate":
                    TestSystemState();
                    return;
                    
                case "cycleshunt":
                    StartDirectionalCycling();
                    return;
                    
                case "stopcycle":
                    StopShuntCycling();
                    return;
                    
                case "listactions":
                    displayManager.ListAllShieldActions();
                    return;
                    
                case "debug":
                    ToggleDebugMode();
                    return;
                    
                case "testapi":
                    TestShieldApi();
                    return;
                    
                case "fixshunt":
                    ForceRecalibrateShunt();
                    return;
            }
        }

        private void StartDirectionalCycling()
        {
            shieldController.StartShuntCycling();
            
            var output = new StringBuilder();
            output.AppendLine("=== DIRECTIONAL SHUNT CYCLING STARTED ===");
            output.AppendLine("Cycling through all shield directions...");
            output.AppendLine("Cycle interval: 3 seconds");
            output.AppendLine("");
            output.AppendLine("Watch Defense Shields terminal for changes!");
            output.AppendLine("Run 'stopcycle' to stop cycling");
            
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
            output.AppendLine("Or use directional commands: front, back, left, right, top, bottom, balanced");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void TestSystemState()
        {
            var output = new StringBuilder();
            string current = GetRecommendedShunt();
            if (string.IsNullOrEmpty(current)) current = "balanced";
            bool autoMode = !shieldController.ForceShunt && !shieldController.CyclingShunts; // approximation

            output.AppendLine("=== SYSTEM STATE DEBUG ===");
            output.AppendLine($"Current Shunt: {current}");
            output.AppendLine($"Last Applied: {(string.IsNullOrEmpty(shieldController.LastAppliedShunt) ? "(none)" : shieldController.LastAppliedShunt)}");
            output.AppendLine($"Force Mode: {(shieldController.ForceShunt ? shieldController.ForcedShuntMode : "none")}");
            output.AppendLine($"Cycling: {(shieldController.CyclingShunts ? "active" : "inactive")}");
            output.AppendLine($"Auto Management (derived): {(autoMode ? "enabled" : "disabled")}");
            output.AppendLine("");
            output.AppendLine("Directional Threat Analysis:");
            output.AppendLine($"Total Directional Threats: {threatAnalyzer.GetTotalDirectionalThreats()}");
            output.AppendLine($"Front: {threatAnalyzer.DirectionThreats["front"]}");
            output.AppendLine($"Back: {threatAnalyzer.DirectionThreats["back"]}");
            output.AppendLine($"Left: {threatAnalyzer.DirectionThreats["left"]}");
            output.AppendLine($"Right: {threatAnalyzer.DirectionThreats["right"]}");
            output.AppendLine($"Top: {threatAnalyzer.DirectionThreats["top"]}");
            output.AppendLine($"Bottom: {threatAnalyzer.DirectionThreats["bottom"]}");
            output.AppendLine($"WeaponCore API: {(threatAnalyzer.WcApiActive ? "active" : "inactive")}");
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private string GetRecommendedShunt()
        {
            var data = program.Me.CustomData;
            if (string.IsNullOrEmpty(data)) return "balanced";
            var lines = data.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].StartsWith("CurrentShunt="))
                    return lines[i].Substring("CurrentShunt=".Length).Trim();
            }
            return "balanced";
        }

        private void DiagnoseShieldConnection()
        {
            var output = new StringBuilder();
            output.AppendLine("=== SHIELD CONNECTION DIAGNOSTIC ===");
            
            if (shieldController.DSControl == null)
            {
                output.AppendLine("✗ No Defense Shields Controller found");
                output.AppendLine("  • Is Defense Shields mod loaded?");
                output.AppendLine("  • Is there a DS Controller block on grid?");
                output.AppendLine("  • Is the DS Controller powered?");
            }
            else
            {
                output.AppendLine("✓ Defense Shields Controller found");
                output.AppendLine("  Block: " + shieldController.DSControl.CustomName);
                output.AppendLine("  Working: " + (shieldController.DSControl.IsWorking ? "YES" : "NO"));
                output.AppendLine("  Functional: " + (shieldController.DSControl.IsFunctional ? "YES" : "NO"));
                
                output.AppendLine("");
                output.AppendLine("=== API STATUS ===");
                
                // Test the API directly
                try
                {
                    var testWrapper = new PbApiWrapper(shieldController.DSControl);
                    var percent = testWrapper.GetShieldPercent();
                    var isUp = testWrapper.IsShieldUp();
                    var status = testWrapper.ShieldStatus();
                    var charge = testWrapper.GetCharge();
                    var maxCharge = testWrapper.GetMaxCharge();
                    
                    output.AppendLine("  API Connection: SUCCESS");
                    output.AppendLine($"  Shield Percent: {percent}");
                    output.AppendLine($"  Shield Up: {isUp}");
                    output.AppendLine($"  Status: '{status}'");
                    output.AppendLine($"  Charge: {charge:F0} / {maxCharge:F0}");
                    
                    if (charge > 0 && maxCharge > 0)
                    {
                        var calculatedPercent = (charge / maxCharge) * 100f;
                        output.AppendLine($"  Calculated: {calculatedPercent:F1}%");
                    }
                }
                catch
                {
                    output.AppendLine("  API Connection: FAILED");
                    output.AppendLine("  Error: " + "Could not communicate with Defense Shields API");
                }
                
                output.AppendLine("");
                output.AppendLine("=== TROUBLESHOOTING ===");
                output.AppendLine("• Place Shield Generators on your ship");
                output.AppendLine("• Ensure sufficient power (50-200MW+)");
                output.AppendLine("• Turn ON shields in DS Controller terminal");
                output.AppendLine("• Enable shield faces in DS settings");
                output.AppendLine("• Look for 'Shield Online' status");
            }
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void ToggleDebugMode()
        {
            var customData = program.Me.CustomData;
            bool debugEnabled = customData.Contains("DEBUG=true");
            
            if (debugEnabled)
            {
                customData = customData.Replace("DEBUG=true", "DEBUG=false");
                displayManager.ShowPersistentOutput("Debug mode: DISABLED");
            }
            else
            {
                if (customData.Contains("DEBUG=false"))
                    customData = customData.Replace("DEBUG=false", "DEBUG=true");
                else
                    customData += "\nDEBUG=true";
                displayManager.ShowPersistentOutput("Debug mode: ENABLED");
            }
            
            program.Me.CustomData = customData;
        }

        private void TestShieldApi()
        {
            var output = new StringBuilder();
            output.AppendLine("=== SHIELD API LIVE TEST ===");
            
            if (shieldController.DSControl == null)
            {
                output.AppendLine("✗ No Defense Shields Controller found");
            }
            else
            {
                output.AppendLine($"Testing API on: {shieldController.DSControl.CustomName}");
                output.AppendLine("");
                
                try
                {
                    var wrapper = new PbApiWrapper(shieldController.DSControl);
                    
                    // Test all API methods
                    var percent = wrapper.GetShieldPercent();
                    var isUp = wrapper.IsShieldUp();
                    var status = wrapper.ShieldStatus();
                    var charge = wrapper.GetCharge();
                    var maxCharge = wrapper.GetMaxCharge();
                    var maxHpCap = wrapper.GetMaxHpCap();
                    var hpRegenPerSecond = wrapper.HpToChargeRatio();
                    var shieldHeat = wrapper.GetShieldHeat();
                    
                    output.AppendLine("=== API RESULTS ===");
                    output.AppendLine($"GetShieldPercent(): {percent}");
                    output.AppendLine($"IsShieldUp(): {isUp}");
                    output.AppendLine($"ShieldStatus(): '{status}'");
                    output.AppendLine($"GetCharge(): {charge}");
                    output.AppendLine($"GetMaxCharge(): {maxCharge}");
                    output.AppendLine($"GetMaxHpCap(): {maxHpCap}");
                    output.AppendLine($"GetHpRegenPerSecond(): {hpRegenPerSecond}");
                    output.AppendLine($"GetShieldHeat(): {shieldHeat}");
                    
                    output.AppendLine("");
                    output.AppendLine("=== CALCULATIONS ===");
                    if (charge >= 0 && maxCharge > 0)
                    {
                        var calcPercent = (charge / maxCharge) * 100f;
                        output.AppendLine($"Calculated %: {calcPercent:F1}%");
                    }
                    if (percent > 0)
                    {
                        if (percent <= 1f)
                            output.AppendLine($"Percent as decimal: {(percent * 100):F1}%");
                        else
                            output.AppendLine($"Percent direct: {percent:F1}%");
                    }
                }
                catch (Exception ex)
                {
                    output.AppendLine("✗ API Test Failed");
                    output.AppendLine($"Error: {ex.Message}");
                }
                
                // Also test block name parsing
                output.AppendLine("");
                output.AppendLine("=== BLOCK NAME ANALYSIS ===");
                var name = shieldController.DSControl.CustomName;
                output.AppendLine($"Full Name: '{name}'");
                
                if (name.Contains("(") && name.Contains("/") && name.Contains(")"))
                {
                    var s = name.IndexOf('(');
                    var slash = name.IndexOf('/');
                    var e = name.IndexOf(')');
                    var shieldPart = name.Substring(s, e - s + 1);
                    output.AppendLine($"Shield Part: '{shieldPart}'");
                }
                else
                {
                    output.AppendLine("No shield data pattern found in name");
                }
            }
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void ForceRecalibrateShunt()
        {
            var last = shieldController.LastAppliedShunt;
            if (string.IsNullOrEmpty(last)) last = "balanced";
            shieldController.ApplyShunt(last); // re-apply with new clean logic
            displayManager.ShowPersistentOutput("Shunt recalibrated: " + last.ToUpper());
        }
    }
}