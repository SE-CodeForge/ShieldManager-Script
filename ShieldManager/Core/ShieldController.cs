using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;

namespace IngameScript
{
    internal class ShieldController
    {
        private readonly Program program;
        private readonly ConfigManager config;
        private IMyTerminalBlock dsControl;
        private List<IMyTextPanel> lcdPanels = new List<IMyTextPanel>();
        private PbApiWrapper dsWrapper;
        private int lastApiPollTick = -999;
        private float apiCachedPercent = -1f;
        private string lastShieldSource = "unknown";
        private string lastAppliedShunt = "";
        private bool forceShunt;
        private string forcedShuntMode = "";

        private static readonly string[] Faces = { "Top", "Bottom", "Left", "Right", "Front", "Back" };

        private static readonly Dictionary<string, HashSet<string>> ShuntModeMappings = new Dictionary<string, HashSet<string>>
        {
            ["kinetic"] = new HashSet<string> { "Front", "Back" },
            ["energy"] = new HashSet<string> { "Top", "Bottom" },
            ["explosive"] = new HashSet<string> { "Front", "Left", "Right" },
            ["front"] = new HashSet<string> { "Front" },
            ["back"] = new HashSet<string> { "Back" },
            ["left"] = new HashSet<string> { "Left" },
            ["right"] = new HashSet<string> { "Right" },
            ["top"] = new HashSet<string> { "Top" },
            ["bottom"] = new HashSet<string> { "Bottom" },
            ["balanced"] = new HashSet<string> { "Top", "Bottom", "Left", "Right", "Front", "Back" } // ALL faces ON
        };

        public bool IsInitialized => dsControl != null;
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
                    
                    if (p >= 0f && p <= 1f)
                    {
                        apiCachedPercent = p;
                        lastShieldSource = "api-decimal";
                        return;
                    }

                    if (p > 1f && p <= 100f)
                    {
                        apiCachedPercent = p / 100f;
                        lastShieldSource = "api-percent";
                        return;
                    }

                    if (charge >= 0 && maxCharge > 0)
                    {
                        apiCachedPercent = charge / maxCharge;
                        lastShieldSource = "api-charge";
                        if (config.DEBUG) program.Echo($"Calculated from charge: {charge}/{maxCharge} = {apiCachedPercent:F2}");
                        return;
                    }
                    
                    if (isUp && !string.IsNullOrEmpty(status))
                    {
                        apiCachedPercent = 0.5f;
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
        public void ForceShuntMode(string mode)
        {
            forceShunt = true;
            forcedShuntMode = mode;
            
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
                    forceShunt = false;
                    if (config.DEBUG) program.Echo("Shunt applied via actions: " + mode.ToUpper());
                }
                else
                {
                    lastAppliedShunt = mode;
                    forceShunt = false;
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
                EnableShuntShields();

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
            var normalizedMode = mode.ToLower();
            var shouldBeOn = ShuntModeMappings.ContainsKey(normalizedMode) && ShuntModeMappings[normalizedMode].Contains(face);

            return shouldBeOn ? $"DS-C_{face}Shield_ShuntOn" : $"DS-C_{face}Shield_ShuntOff";
        }

        private void EnableShuntShields()
        {
            if (dsControl == null) return;
            try
            {
                foreach (var face in Faces)
                {
                    var faceProp = dsControl.GetProperty($"DS-C_{face}Shield") as ITerminalProperty<bool>;
                    if (faceProp != null && !faceProp.GetValue(dsControl))
                    {
                        faceProp.SetValue(dsControl, true);
                    }
                }
                
                var redirectProp = dsControl.GetProperty("DS-C_SideRedirect") as ITerminalProperty<bool>;
                if (redirectProp != null && !redirectProp.GetValue(dsControl))
                {
                    redirectProp.SetValue(dsControl, true);
                    if (!redirectProp.GetValue(dsControl))
                    {
                        dsControl.GetActionWithName("DS-C_SideRedirect_Toggle")?.Apply(dsControl);
                    }
                }
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("EnableShuntShields failed: " + ex.Message);
            }
        }

        private void SetAutoManagement(bool enabled)
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
                        return;
                    }
                }
                
                if (config.DEBUG) program.Echo("AutoManage property not found");
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("Set AutoManage failed: " + ex.Message);
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
        }
    }
}