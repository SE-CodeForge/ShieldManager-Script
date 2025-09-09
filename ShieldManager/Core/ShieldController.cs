using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;

namespace IngameScript
{
    internal class ShieldController
    {
        private readonly Program program;
        private readonly ConfigManager config;

        // Core Blocks
        private IMyTerminalBlock dsControl;
        private List<IMyTextPanel> lcdPanels = new List<IMyTextPanel>();

        // Shield API
        private PbApiWrapper dsWrapper;
        private int lastApiPollTick = -999;
        private float apiCachedPercent = -1f;
        private string lastShieldSource = "unknown";

        // Cycling state
        private bool cyclingShunts = false;
        private int cycleTimer = 0;
        private int currentCycleIndex = 0;

        // Shunt state
        private string lastAppliedShunt = "";
        private bool forceShunt;
        private string forcedShuntMode = "";

        private static readonly string[] Faces = { "Top", "Bottom", "Left", "Right", "Front", "Back" };

        public bool IsInitialized => dsControl != null;
        public bool CyclingShunts => cyclingShunts;
        public IMyTerminalBlock DSControl => dsControl;
        public List<IMyTextPanel> LcdPanels => lcdPanels;
        public float ApiCachedPercent => apiCachedPercent;
        public string LastShieldSource => lastShieldSource;
        public bool ForceShunt => forceShunt;
        public string ForcedShuntMode => forcedShuntMode;
        public string LastAppliedShunt => lastAppliedShunt;

        public ShieldController(Program program, ConfigManager config)
        {
            this.program = program;
            this.config = config;
            InitializeBlocks();
            InitDefenseShieldsWrapper();
            config.SetShuntRecommendation("balanced");
        }

        private void InitializeBlocks()
        {
            var dsBlocks = new List<IMyTerminalBlock>();
            program.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(dsBlocks, block =>
                block.BlockDefinition.SubtypeName.Contains("DSControl"));
            dsControl = dsBlocks.FirstOrDefault();
            program.GridTerminalSystem.GetBlocksOfType(lcdPanels, b => b.CustomName.Contains("[ShieldLCD]"));
        }

        private void InitDefenseShieldsWrapper()
        {
            if (dsControl == null) return;
            try
            {
                dsWrapper = new PbApiWrapper(dsControl);
                if (config.DEBUG) program.Echo("DS Wrapper initialized");
            }
            catch
            {
                dsWrapper = null;
                if (config.DEBUG) program.Echo("DS Wrapper failed");
            }
        }

        public void PollShieldApi()
        {
            if (config.ticks - lastApiPollTick < 30) return;
            lastApiPollTick = config.ticks;
            apiCachedPercent = -1f;

            if (dsWrapper != null)
            {
                try
                {
                    var p = dsWrapper.GetShieldPercent();
                    var isUp = dsWrapper.IsShieldUp();
                    var status = dsWrapper.ShieldStatus();
                    var charge = dsWrapper.GetCharge();
                    var maxCharge = dsWrapper.GetMaxCharge();
                    
                    if (config.DEBUG)
                    {
                        program.Echo($"API Raw Values - Percent: {p}, IsUp: {isUp}, Status: '{status}'");
                        program.Echo($"API Raw Values - Charge: {charge}, MaxCharge: {maxCharge}");
                    }
                    
                    // Try different approaches to get shield percentage
                    if (p >= 0f && p <= 1f)
                    {
                        apiCachedPercent = p;
                        lastShieldSource = "api-decimal";
                        return;
                    }
                    else if (p > 1f && p <= 100f)
                    {
                        apiCachedPercent = p / 100f;
                        lastShieldSource = "api-percent";
                        return;
                    }
                    
                    // Try to calculate from charge values
                    if (charge >= 0 && maxCharge > 0)
                    {
                        apiCachedPercent = charge / maxCharge;
                        lastShieldSource = "api-charge";
                        if (config.DEBUG) program.Echo($"Calculated from charge: {charge}/{maxCharge} = {apiCachedPercent:F2}");
                        return;
                    }
                    
                    // Check if shield is just starting up
                    if (isUp && status != null && status.Length > 0)
                    {
                        apiCachedPercent = 0.5f; // Default to 50% if we know shields are up but can't get exact value
                        lastShieldSource = "api-status";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (config.DEBUG) program.Echo("API Exception: " + ex.Message);
                }
            }
            else if (config.DEBUG)
            {
                program.Echo("dsWrapper is null - attempting to reinitialize");
                InitDefenseShieldsWrapper();
            }

            // Enhanced name parsing as fallback - Fixed for C# 6.0
            var name = dsControl?.CustomName;
            if (!string.IsNullOrEmpty(name))
            {
                if (config.DEBUG) program.Echo($"Checking block name: '{name}'");
                
                var s = name.IndexOf('(');
                var slash = name.IndexOf('/');
                var e = name.IndexOf(')');
                if (s >= 0 && slash > s && e > slash)
                {
                    var curStr = name.Substring(s + 1, slash - s - 1);
                    var maxStr = name.Substring(slash + 1, e - slash - 1);
                    
                    if (config.DEBUG) program.Echo($"Found shield data in name: '{curStr}' / '{maxStr}'");

                    float cur, max;
                    if (float.TryParse(curStr, out cur) && float.TryParse(maxStr, out max) && max > 0)
                    {
                        apiCachedPercent = cur / max;
                        lastShieldSource = "name";
                        if (config.DEBUG) program.Echo($"Name parsing successful: {cur}/{max} = {apiCachedPercent:F2}");
                        return;
                    }
                }
            }
            
            if (config.DEBUG && dsControl != null)
            {
                program.Echo($"All shield data methods failed for block: {dsControl.CustomName}");
            }
        }

        public void StartShuntCycling()
        {
            cyclingShunts = true;
            cycleTimer = 0;
            currentCycleIndex = 0;
            config.ADAPTIVE_SHUNT = false;
            SetAutoManagement(false);
            EnableShuntShields(true);
            ApplyShunt(config.cycleOrder[currentCycleIndex]);
        }

        public void StopShuntCycling()
        {
            cyclingShunts = false;
            cycleTimer = 0;
        }

        public void UpdateShuntCycling()
        {
            if (!cyclingShunts) return;
            cycleTimer++;
            if (cycleTimer >= ConfigManager.CYCLE_INTERVAL)
            {
                currentCycleIndex = (currentCycleIndex + 1) % config.cycleOrder.Length;
                cycleTimer = 0;
                ApplyShunt(config.cycleOrder[currentCycleIndex]);
            }
        }

        public void ForceShuntMode(string mode)
        {
            forceShunt = true;
            forcedShuntMode = mode;
            cyclingShunts = false;
            
            var previousLastApplied = lastAppliedShunt;
            lastAppliedShunt = "";
            
            ApplyShunt(mode);
        }

        public void ApplyShunt(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;

            if (forceShunt || mode != lastAppliedShunt)
            {
                if (dsControl != null && ApplyShuntViaActions(mode))
                {
                    lastAppliedShunt = mode;
                    forceShunt = false; // Reset force flag
                    if (config.DEBUG) program.Echo("Shunt applied via actions: " + mode.ToUpper());
                }
                else
                {
                    lastAppliedShunt = mode;
                    forceShunt = false; // Also reset on failure
                    if (config.DEBUG) program.Echo("Shunt failed or applied via CustomData: " + mode.ToUpper());
                }
                
                config.SetShuntRecommendation(mode);
            }
        }

        private bool ApplyShuntViaActions(string mode)
        {
            if (dsControl == null) return false;
            try
            {
                SetAutoManagement(false);
                EnableShuntShields(true);

                var anyApplied = false;
                foreach (var face in Faces)
                {
                    var actionName = GetShuntActionName(face, mode);
                    if (!string.IsNullOrEmpty(actionName))
                    {
                        var action = dsControl.GetActionWithName(actionName);
                        if (action != null)
                        {
                            action.Apply(dsControl);
                            anyApplied = true;
                        }
                        else if (config.DEBUG)
                        {
                            program.Echo($"Action not found: {actionName}");
                        }
                    }
                }
                return anyApplied;
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("Action application failed: " + ex.Message);
                return false;
            }
        }

        private string GetShuntActionName(string face, string mode)
        {
            switch (mode.ToLower())
            {
                case "kinetic":
                    return (face == "Front" || face == "Back") ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "energy":
                    return (face == "Top" || face == "Bottom") ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "explosive":
                    return (face == "Front" || face == "Left" || face == "Right") ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "balanced":
                    return $"DS-C_{face}Shield_ShuntOff";
                case "front":
                    return face == "Front" ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "back":
                    return face == "Back" ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "left":
                    return face == "Left" ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "right":
                    return face == "Right" ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "top":
                    return face == "Top" ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                case "bottom":
                    return face == "Bottom" ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
                default:
                    return $"DS-C_{face}Shield_ShuntOff";
            }
        }

        private bool EnableShuntShields(bool enabled)
        {
            if (dsControl == null) return false;
            try
            {
                // Enable individual faces
                foreach (var face in Faces)
                {
                    var faceProp = dsControl.GetProperty($"DS-C_{face}Shield") as ITerminalProperty<bool>;
                    if (faceProp != null && !faceProp.GetValue(dsControl))
                    {
                        faceProp.SetValue(dsControl, true);
                    }
                }

                // Enable the main shunt toggle
                var redirectProp = dsControl.GetProperty("DS-C_SideRedirect") as ITerminalProperty<bool>;
                if (redirectProp != null && !redirectProp.GetValue(dsControl))
                {
                    // Prefer property setting, but fallback to action if it fails
                    redirectProp.SetValue(dsControl, true);
                    if (!redirectProp.GetValue(dsControl))
                    {
                        dsControl.GetActionWithName("DS-C_SideRedirect_Toggle")?.Apply(dsControl);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("EnableShuntShields failed: " + ex.Message);
                return false;
            }
        }

        private bool SetAutoManagement(bool enabled)
        {
            try
            {
                var property = dsControl.GetProperty("DS-C_AutoManage");
                if (property != null)
                {
                    var boolProp = property as ITerminalProperty<bool>;
                    if (boolProp != null)
                    {
                        boolProp.SetValue(dsControl, enabled);
                        if (config.DEBUG) program.Echo($"Set AutoManage to {enabled}");
                        return true;
                    }
                }
                
                if (config.DEBUG) program.Echo("AutoManage property not found");
                return false;
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("Set AutoManage failed: " + ex.Message);
                return false;
            }
        }

        public void ClearForceShunt()
        {
            forceShunt = false;
            forcedShuntMode = "";
        }

        public void ReEnableAuto()
        {
            config.ADAPTIVE_SHUNT = true;
            forceShunt = false;
            forcedShuntMode = "";
            cyclingShunts = false;
        }

        public string GetCyclingInfo()
        {
            if (!cyclingShunts) return "";
            return $"Current: {config.cycleOrder[currentCycleIndex]} | Next in: {((ConfigManager.CYCLE_INTERVAL - cycleTimer) / 60f):F1}s";
        }
    }
}