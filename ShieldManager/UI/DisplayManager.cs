using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    internal class DisplayManager
    {
        private readonly Program program;
        private readonly ConfigManager config;
        private ShieldController shieldController2;
        private ThreatAnalyzer threatAnalyzer2;

        // UI
        private StringBuilder display = new StringBuilder();
        private IMyTextSurface pbSurface;
        
        // Persistent Output System
        private bool showingActionList;
        private bool showingTestOutput;
        private string persistentOutput = "";
        private int persistentOutputTimer;

        public DisplayManager(Program program, ConfigManager config, ShieldController shieldController, ThreatAnalyzer threatAnalyzer)
        {
            this.program = program;
            this.config = config;
            this.shieldController2 = shieldController;
            this.threatAnalyzer2 = threatAnalyzer;
            
            pbSurface = program.Me.GetSurface(0);
            pbSurface.ContentType = ContentType.TEXT_AND_IMAGE;
            pbSurface.FontSize = 0.7f;
            pbSurface.Font = "Monospace";
        }

        public void ShowPersistentOutput(string output)
        {
            persistentOutput = output;
            showingTestOutput = true;
            showingActionList = false;
            persistentOutputTimer = ConfigManager.PERSISTENT_OUTPUT_DURATION;
        }

        public void ClearPersistentOutputs()
        {
            showingActionList = false;
            showingTestOutput = false;
            persistentOutput = "";
            persistentOutputTimer = 0;
        }

        public void ListAllShieldActions()
        {
            if (shieldController2.DSControl == null)
            {
                display.Clear();
                display.AppendLine("No Defense Shields controller found");
                pbSurface.WriteText(display.ToString());
                return;
            }
            
            display.Clear();
            display.AppendLine("=== DEFENSE SHIELDS ACTIONS ===");
            
            var actions = new List<ITerminalAction>();
            shieldController2.DSControl.GetActions(actions);
            
            foreach (var action in actions)
            {
                display.AppendLine($"Action: '{action.Id}'");
            }
            
            display.AppendLine("");
            display.AppendLine("=== DEFENSE SHIELDS PROPERTIES ===");
            
            var properties = new List<ITerminalProperty>();
            shieldController2.DSControl.GetProperties(properties);
            
            foreach (var prop in properties)
            {
                display.AppendLine($"Property: '{prop.Id}' ({prop.TypeName})");
            }
            
            display.AppendLine("");
            display.AppendLine("Run 'clear' to return to normal display");
            
            // Set the persistent output instead of writing directly
            var output = display.ToString();
            ShowPersistentOutput(output);
        }

        public void UpdateDisplays()
        {
            // Handle persistent output timer
            if (showingTestOutput && persistentOutputTimer > 0)
            {
                persistentOutputTimer--;
                if (persistentOutputTimer <= 0)
                {
                    showingTestOutput = false;
                    persistentOutput = "";
                }
            }
            
            // Show persistent outputs if active
            if (showingActionList || showingTestOutput)
            {
                string output;
                if (showingActionList)
                {
                    return; // Let ListAllShieldActions handle the display
                }

                if (showingTestOutput)
                {
                    output = persistentOutput + "\n\nRun 'clear' to return to normal display";
                }
                else
                {
                    return;
                }

                pbSurface.WriteText(output);
                
                foreach (var panel in shieldController2.LcdPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(output);
                }
                return;
            }
            
            // Normal display update
            display.Clear();
            display.AppendLine("TEKKONIC SHIELD MANAGER v2.0");
            display.AppendLine("Closest Threat Auto-Shunt Mode");
            display.AppendLine();
            
            display.AppendLine("SHIELD STATUS");
            if (shieldController2.ApiCachedPercent >= 0)
                display.AppendLine("Level: " + (shieldController2.ApiCachedPercent * 100).ToString("F1") + "% (" + shieldController2.LastShieldSource + ")");
            else
            {
                display.AppendLine("Level: NO DATA");
                if (shieldController2.DSControl == null)
                    display.AppendLine("DEBUG: No Defense Shields Controller found");
                else
                {
                    display.AppendLine("DEBUG: DS Controller found: " + shieldController2.DSControl.CustomName);
                    display.AppendLine("DEBUG: API Percent: " + shieldController2.ApiCachedPercent);
                    display.AppendLine("DEBUG: Last Source: " + shieldController2.LastShieldSource);
                    
                    var name = shieldController2.DSControl.CustomName;
                    if (name.Contains("(") && name.Contains("/") && name.Contains(")"))
                    {
                        display.AppendLine("DEBUG: Shield data found in block name");
                    }
                    else
                    {
                        display.AppendLine("DEBUG: No shield data in block name - shields may be offline");
                    }
                }
            }
            display.AppendLine();

            display.AppendLine("SHUNT SYSTEM");
            display.AppendLine("Current: " + config.GetCurrentShunt().ToUpper());
            display.AppendLine("Auto: " + (config.ADAPTIVE_SHUNT ? "ENABLED" : "DISABLED"));
            if (shieldController2.ForceShunt)
                display.AppendLine("Forced: " + shieldController2.ForcedShuntMode.ToUpper());
            display.AppendLine();

            display.AppendLine($"Shield HP: {shieldController2.ApiCachedPercent:P0} ({shieldController2.LastShieldSource})");
            display.AppendLine($"Shunt Mode: {shieldController2.LastAppliedShunt.ToUpper()}");

            var target = threatAnalyzer2.CurrentTarget;
            if (target.HasValue && target.Value.EntityId != 0)
            {
                display.AppendLine($"AI Target: {target.Value.Name}");
            }
            else
            {
                display.AppendLine("AI Target: None");
            }

            if (threatAnalyzer2.IncomingLocks > 0)
            {
                display.AppendLine($"!!! INCOMING LOCKS: {threatAnalyzer2.IncomingLocks} !!!");
            }

            if (config.DEBUG)
            {
                display.AppendLine("--- DEBUG INFO ---");
                display.AppendLine($"Shield Source: {shieldController2.LastShieldSource}");
                display.AppendLine($"Force Shunt: {shieldController2.ForceShunt}");
                display.AppendLine($"Ticks: {config.ticks}");
                
                // Add orientation debug info
                if (shieldController2.DSControl != null)
                {
                    var pbMatrix = program.Me.WorldMatrix;
                    var dsMatrix = shieldController2.DSControl.WorldMatrix;
                    var relativeForward = Vector3D.Transform(pbMatrix.Forward, MatrixD.Transpose(dsMatrix));
                    display.AppendLine($"PB->DS Orientation: {relativeForward.ToString("F2")}");
                }
            }

            display.AppendLine();
            display.AppendLine("WEAPONCORE API: " + (threatAnalyzer2.WcApiActive ? "ACTIVE" : "OFFLINE"));
            
            var outText = display.ToString();
            foreach (var p in shieldController2.LcdPanels)
            {
                p.ContentType = ContentType.TEXT_AND_IMAGE;
                p.WriteText(outText);
                p.FontSize = 0.8f;
            }

            pbSurface.WriteText(outText);
            program.Echo("Shunt: " + config.GetCurrentShunt().ToUpper() + " | WC: " + (threatAnalyzer2.WcApiActive ? "OK" : "OFF"));
        }
    }
}