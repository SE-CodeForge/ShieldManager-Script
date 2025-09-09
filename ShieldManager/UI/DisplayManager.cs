using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using Sandbox.ModAPI.Interfaces;

namespace IngameScript
{
    internal class DisplayManager
    {
        private readonly Program program;
        private readonly ConfigManager config;
        private ShieldController shieldController;
        private ThreatAnalyzer threatAnalyzer;

        // UI
        private StringBuilder display = new StringBuilder();
        private IMyTextSurface pbSurface;
        
        // Persistent Output System
        private bool showingActionList = false;
        private bool showingTestOutput = false;
        private string persistentOutput = "";
        private int persistentOutputTimer = 0;

        public DisplayManager(Program program, ConfigManager config)
        {
            this.program = program;
            this.config = config;
            pbSurface = program.Me.GetSurface(0);
            pbSurface.ContentType = ContentType.TEXT_AND_IMAGE;
            pbSurface.FontSize = 0.7f;
            pbSurface.Font = "Monospace";
        }

        public void SetControllers(ShieldController shieldController, ThreatAnalyzer threatAnalyzer)
        {
            this.shieldController = shieldController;
            this.threatAnalyzer = threatAnalyzer;
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
            if (shieldController.DSControl == null)
            {
                display.Clear();
                display.AppendLine("No Defense Shields controller found");
                pbSurface.WriteText(display.ToString());
                return;
            }
            
            display.Clear();
            display.AppendLine("=== DEFENSE SHIELDS ACTIONS ===");
            
            var actions = new List<ITerminalAction>();
            shieldController.DSControl.GetActions(actions);
            
            foreach (var action in actions)
            {
                display.AppendLine($"Action: '{action.Id}'");
            }
            
            display.AppendLine("");
            display.AppendLine("=== DEFENSE SHIELDS PROPERTIES ===");
            
            var properties = new List<ITerminalProperty>();
            shieldController.DSControl.GetProperties(properties);
            
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
                if (persistentOutputTimer <= 0 && !shieldController.CyclingShunts) // Keep cycling display active
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
                else if (showingTestOutput)
                {
                    output = persistentOutput + "\n\nRun 'clear' to return to normal display";
                }
                else
                {
                    return;
                }
                
                pbSurface.WriteText(output);
                
                foreach (var panel in shieldController.LcdPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(output);
                }
                return;
            }
            
            // Normal display update
            display.Clear();
            display.AppendLine("TEKKONIC SHIELD MANAGER v2.0");
            display.AppendLine("Directional Auto-Shunt Mode");
            display.AppendLine();
            
            display.AppendLine("SHIELD STATUS");
            if (shieldController.ApiCachedPercent >= 0)
                display.AppendLine("Level: " + (shieldController.ApiCachedPercent * 100).ToString("F1") + "% (" + shieldController.LastShieldSource + ")");
            else
            {
                display.AppendLine("Level: NO DATA");
                // Add debug information to see what's wrong
                if (shieldController.DSControl == null)
                    display.AppendLine("DEBUG: No Defense Shields Controller found");
                else
                {
                    display.AppendLine("DEBUG: DS Controller found: " + shieldController.DSControl.CustomName);
                    display.AppendLine("DEBUG: API Percent: " + shieldController.ApiCachedPercent);
                    display.AppendLine("DEBUG: Last Source: " + shieldController.LastShieldSource);
                    
                    // Check if the controller block has a shield percentage in its name
                    var name = shieldController.DSControl.CustomName;
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
            if (shieldController.CyclingShunts)
                display.AppendLine("CYCLING: " + shieldController.GetCyclingInfo());
            else if (shieldController.ForceShunt)
                display.AppendLine("Forced: " + shieldController.ForcedShuntMode.ToUpper());
            display.AppendLine();

            display.AppendLine("DIRECTIONAL THREAT ANALYSIS");
            
            // Make sure we analyze threats first for display purposes
            threatAnalyzer.AnalyzeDirectionalThreats();
            var total = threatAnalyzer.GetTotalDirectionalThreats();
            
            if (total == 0)
                display.AppendLine("No threats detected");
            else
            {
                display.AppendLine("=== THREAT DIRECTIONS ===");
                display.AppendLine("Front: " + threatAnalyzer.DirectionThreats["front"]);
                display.AppendLine("Back: " + threatAnalyzer.DirectionThreats["back"]);
                display.AppendLine("Left: " + threatAnalyzer.DirectionThreats["left"]);
                display.AppendLine("Right: " + threatAnalyzer.DirectionThreats["right"]);
                display.AppendLine("Top: " + threatAnalyzer.DirectionThreats["top"]);
                display.AppendLine("Bottom: " + threatAnalyzer.DirectionThreats["bottom"]);
                display.AppendLine("Primary: " + ThreatAnalyzer.DetermineOptimalShunt(threatAnalyzer.DirectionThreats).ToUpper());
                display.AppendLine("Total: " + total);
            }
            display.AppendLine();

            display.AppendLine("WEAPONCORE API: " + (threatAnalyzer.WcApiActive ? "ACTIVE" : "OFFLINE"));
            display.AppendLine("Recent Samples: " + threatAnalyzer.RecentThreatCount);
            
            var outText = display.ToString();
            foreach (var p in shieldController.LcdPanels)
            {
                p.ContentType = ContentType.TEXT_AND_IMAGE;
                p.WriteText(outText);
                p.FontSize = 0.8f;
            }

            pbSurface.WriteText(outText);
            program.Echo("Shunt: " + config.GetCurrentShunt().ToUpper() + " | Threats: " + total + " | WC: " + (threatAnalyzer.WcApiActive ? "OK" : "OFF"));
        }
    }
}