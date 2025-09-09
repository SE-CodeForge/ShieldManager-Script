using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;

namespace IngameScript
{
    public class ShieldController
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
                    if (p > 1f && p <= 100f) p /= 100f;
                    if (p >= 0f && p <= 1.2f)
                    {
                        apiCachedPercent = p;
                        lastShieldSource = "api";
                        return;
                    }
                }
                catch { }
            }

            var name = dsControl?.CustomName;
            if (!string.IsNullOrEmpty(name))
            {
                var s = name.IndexOf('(');
                var slash = name.IndexOf('/');
                var e = name.IndexOf(')');
                if (s >= 0 && slash > s && e > slash)
                {
                    ulong cur, max;
                    if (ulong.TryParse(name.Substring(s + 1, slash - s - 1), out cur) &&
                        ulong.TryParse(name.Substring(slash + 1, e - slash - 1), out max) && max > 0)
                    {
                        apiCachedPercent = (float)cur / max;
                        lastShieldSource = "name";
                    }
                }
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
            
            ApplyShuntMode(config.cycleOrder[currentCycleIndex]);
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
                ApplyShuntMode(config.cycleOrder[currentCycleIndex]);
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
                    if (config.DEBUG) program.Echo("Shunt applied via actions: " + mode.ToUpper());
                }
                else
                {
                    lastAppliedShunt = mode;
                    if (config.DEBUG) program.Echo("Shunt applied via CustomData: " + mode.ToUpper());
                }
                
                config.SetShuntRecommendation(mode);
            }
        }

        private void ApplyShuntMode(string mode)
        {
            SetAutoManagement(false);
            EnableShuntShields(true);
            
            string[] faces = { "Top", "Bottom", "Left", "Right", "Front", "Back" };
            
            foreach (var face in faces)
            {
                var actionName = GetShuntActionName(face, mode);
                if (!string.IsNullOrEmpty(actionName))
                {
                    var action = dsControl.GetActionWithName(actionName);
                    if (action != null)
                    {
                        action.Apply(dsControl);
                        if (config.DEBUG) program.Echo($"Cycle: Applied {face}: {actionName}");
                    }
                }
            }
            
            config.SetShuntRecommendation(mode);
        }

        private bool ApplyShuntViaActions(string mode)
        {
            try
            {
                if (!SetAutoManagement(false))
                {
                    if (config.DEBUG) program.Echo("Failed to disable AutoManage");
                    return false;
                }
                
                if (!EnableShuntShields(true))
                {
                    if (config.DEBUG) program.Echo("Failed to enable shunt shields");
                    return false;
                }
                
                string[] faces = { "Top", "Bottom", "Left", "Right", "Front", "Back" };
                foreach (var face in faces)
                {
                    var faceProp = dsControl.GetProperty($"DS-C_{face}Shield");
                    if (faceProp != null)
                    {
                        var boolProp = faceProp as ITerminalProperty<bool>;
                        if (boolProp != null && !boolProp.GetValue(dsControl))
                        {
                            boolProp.SetValue(dsControl, true);
                            if (config.DEBUG) program.Echo($"Force enabled DS-C_{face}Shield");
                        }
                    }
                }
                
                var anyApplied = false;
                
                foreach (var face in faces)
                {
                    var actionName = GetShuntActionName(face, mode);
                    if (!string.IsNullOrEmpty(actionName))
                    {
                        var action = dsControl.GetActionWithName(actionName);
                        if (action != null)
                        {
                            action.Apply(dsControl);
                            anyApplied = true;
                            if (config.DEBUG) program.Echo($"Applied: {actionName}");
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
                    if (face == "Front" || face == "Back")
                        return $"DS-C_{face}Shield_ShuntOn";
                    else
                        return $"DS-C_{face}Shield_ShuntOff";
                        
                case "energy":
                    if (face == "Top" || face == "Bottom")
                        return $"DS-C_{face}Shield_ShuntOn";
                    else
                        return $"DS-C_{face}Shield_ShuntOff";
                        
                case "explosive":
                    if (face == "Front" || face == "Left" || face == "Right")
                        return $"DS-C_{face}Shield_ShuntOn";
                    else
                        return $"DS-C_{face}Shield_ShuntOff";
                        
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
            try
            {
                var success = false;
                string[] faces = { "Top", "Bottom", "Left", "Right", "Front", "Back" };
                foreach (var face in faces)
                {
                    var faceProp = dsControl.GetProperty($"DS-C_{face}Shield");
                    if (faceProp != null)
                    {
                        var boolProp = faceProp as ITerminalProperty<bool>;
                        if (boolProp != null)
                        {
                            boolProp.SetValue(dsControl, true);
                            if (config.DEBUG) program.Echo($"Set DS-C_{face}Shield to true");
                            success = true;
                        }
                    }
                }

                var redirectProp = dsControl.GetProperty("DS-C_SideRedirect");
                if (redirectProp != null)
                {
                    var boolProp = redirectProp as ITerminalProperty<bool>;
                    if (boolProp != null)
                    {
                        boolProp.SetValue(dsControl, true);
                        if (config.DEBUG) program.Echo($"Set DS-C_SideRedirect to true");
                        success = true;
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("Enable Shunt Shields failed: " + ex.Message);
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