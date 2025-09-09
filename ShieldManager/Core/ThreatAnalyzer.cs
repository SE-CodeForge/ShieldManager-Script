using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    public class ThreatAnalyzer
    {
        private readonly Program program;
        private readonly ConfigManager config;
        private ShieldController shieldController;

        // Threat System
        private Dictionary<string, int> cachedThreats = new Dictionary<string, int> 
            { { "kinetic", 0 }, { "energy", 0 }, { "explosive", 0 } };
        private Dictionary<string, int> directionThreats = new Dictionary<string, int> 
            { { "front", 0 }, { "back", 0 }, { "left", 0 }, { "right", 0 }, { "top", 0 }, { "bottom", 0 } };
        
        private int lastThreatRefresh = -999;
        private Queue<string> recentThreats = new Queue<string>();

        // WeaponCore Integration
        private WcPbApi wcApi;
        private bool wcApiActive;
        private Dictionary<MyDetectedEntityInfo, float> wcThreatBuffer =
            new Dictionary<MyDetectedEntityInfo, float>(16, new Program.DetectedEntityComparer());
        private int lastWcThreatTick = -999;

        public Dictionary<string, int> CachedThreats => cachedThreats;
        public Dictionary<string, int> DirectionThreats => directionThreats;
        public bool WcApiActive => wcApiActive;
        public int RecentThreatCount => recentThreats.Count;

        public ThreatAnalyzer(Program program, ConfigManager config)
        {
            this.program = program;
            this.config = config;
            InitWeaponCoreApi();
        }

        public void SetShieldController(ShieldController controller)
        {
            this.shieldController = controller;
        }

        private void InitWeaponCoreApi()
        {
            try
            {
                wcApi = new WcPbApi();
                wcApiActive = wcApi.Activate(program.Me);
                if (config.DEBUG) program.Echo("WeaponCore API: " + (wcApiActive ? "OK" : "FAILED"));
            }
            catch
            {
                wcApiActive = false;
                wcApi = null;
            }
        }

        public void ManageShuntSystems()
        {
            if (shieldController.ForceShunt)
            {
                shieldController.ApplyShunt(shieldController.ForcedShuntMode);
                return;
            }

            var threats = AnalyzeThreats();
            var refreshed = (config.ticks == lastThreatRefresh);

            if (refreshed)
            {
                var optimal = DetermineOptimalShunt(directionThreats);
                if (recentThreats.Count >= ConfigManager.POLL_MEMORY) recentThreats.Dequeue();
                recentThreats.Enqueue(optimal);
            }

            var timeTrigger = config.ticks % ConfigManager.SHUNT_TIMEOUT == 0;
            var threatTrigger = refreshed && GetTotalThreats() > 0;

            if (timeTrigger || threatTrigger)
            {
                var mode = GetMostFrequentShunt();
                ApplyDirectionalShunt(mode);
            }
        }

        private void WeaponCoreThreatScan()
        {
            if (!wcApiActive) return;
            if (config.ticks - lastWcThreatTick < ConfigManager.THREAT_REFRESH_INTERVAL) return;
            
            wcThreatBuffer.Clear();
            try { wcApi.GetSortedThreats(program.Me, wcThreatBuffer); }
            catch { return; }

            // Reset direction counters
            directionThreats["front"] = 0;
            directionThreats["back"] = 0;
            directionThreats["left"] = 0;
            directionThreats["right"] = 0;
            directionThreats["top"] = 0;
            directionThreats["bottom"] = 0;

            // Also reset weapon type counters for display
            cachedThreats["kinetic"] = 0;
            cachedThreats["energy"] = 0;
            cachedThreats["explosive"] = 0;

            foreach (var kv in wcThreatBuffer)
            {
                var ent = kv.Key;
                if (ent.IsEmpty()) continue;
                if (ent.Relationship == MyRelationsBetweenPlayerAndBlock.Owner ||
                    ent.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    continue;

                // Calculate direction from our position to threat
                var myPos = program.Me.CubeGrid.GetPosition();
                var threatPos = ent.Position;
                var direction = Vector3D.Normalize(threatPos - myPos);
                
                // Transform direction to grid local coordinates
                var gridMatrix = program.Me.CubeGrid.WorldMatrix;
                var localDirection = Vector3D.Transform(direction, MatrixD.Transpose(gridMatrix));

                // Determine which face the threat is closest to
                var maxAxis = Math.Max(Math.Max(Math.Abs(localDirection.X), Math.Abs(localDirection.Y)), Math.Abs(localDirection.Z));
                
                if (Math.Abs(localDirection.X) == maxAxis)
                {
                    if (localDirection.X > 0)
                        directionThreats["right"]++;
                    else
                        directionThreats["left"]++;
                }
                else if (Math.Abs(localDirection.Y) == maxAxis)
                {
                    if (localDirection.Y > 0)
                        directionThreats["top"]++;
                    else
                        directionThreats["bottom"]++;
                }
                else if (Math.Abs(localDirection.Z) == maxAxis)
                {
                    if (localDirection.Z > 0)
                        directionThreats["back"]++;
                    else
                        directionThreats["front"]++;
                }

                // Still categorize by weapon type for display purposes
                var lname = ent.Name.ToLower();
                if (lname.Contains("missile") || lname.Contains("rocket") || lname.Contains("torpedo"))
                    cachedThreats["explosive"]++;
                else if (lname.Contains("laser") || lname.Contains("beam") || lname.Contains("plasma"))
                    cachedThreats["energy"]++;
                else
                    cachedThreats["kinetic"]++;
            }
            lastThreatRefresh = config.ticks;
            lastWcThreatTick = config.ticks;
        }

        public Dictionary<string, int> AnalyzeThreats()
        {
            if (wcApiActive) WeaponCoreThreatScan();
            return cachedThreats;
        }

        public static string DetermineOptimalShunt(Dictionary<string, int> dirThreats)
        {
            var maxThreat = 0;
            var primaryDirection = "balanced";
            
            foreach (var kvp in dirThreats)
            {
                if (kvp.Value > maxThreat)
                {
                    maxThreat = kvp.Value;
                    primaryDirection = kvp.Key;
                }
            }
            
            return maxThreat > 0 ? primaryDirection : "balanced";
        }

        private string GetMostFrequentShunt()
        {
            // Count directional threats from recent queue
            int front = 0, back = 0, left = 0, right = 0, top = 0, bottom = 0, balanced = 0;
            
            foreach (var t in recentThreats)
            {
                switch (t)
                {
                    case "front": front++; break;
                    case "back": back++; break;
                    case "left": left++; break;
                    case "right": right++; break;
                    case "top": top++; break;
                    case "bottom": bottom++; break;
                    default: balanced++; break;
                }
            }
            
            var mode = "balanced";
            var best = balanced;
            
            if (front > best) { best = front; mode = "front"; }
            if (back > best) { best = back; mode = "back"; }
            if (left > best) { best = left; mode = "left"; }
            if (right > best) { best = right; mode = "right"; }
            if (top > best) { best = top; mode = "top"; }
            if (bottom > best) { best = bottom; mode = "bottom"; }
            
            return mode;
        }

        private void ApplyDirectionalShunt(string primaryDirection)
        {
            if (string.IsNullOrEmpty(primaryDirection)) return;
            
            if (primaryDirection != shieldController.LastAppliedShunt)
            {
                if (shieldController.DSControl != null && ApplyDirectionalShuntViaActions())
                {
                    if (config.DEBUG) program.Echo("Directional shunt applied: " + primaryDirection.ToUpper());
                }
                
                config.SetShuntRecommendation(primaryDirection);
            }
        }

        private bool ApplyDirectionalShuntViaActions()
        {
            try
            {
                // Get the primary threat direction
                var primaryDirection = DetermineOptimalShunt(directionThreats);
                
                // Use the shield controller's methods to apply the shunt
                shieldController.ApplyShunt(primaryDirection);
                
                return true;
            }
            catch (Exception ex)
            {
                if (config.DEBUG) program.Echo("Directional shunt application failed: " + ex.Message);
                return false;
            }
        }

        public int GetTotalThreats()
        {
            return cachedThreats["kinetic"] + cachedThreats["energy"] + cachedThreats["explosive"];
        }

        public int GetTotalDirectionalThreats()
        {
            return directionThreats["front"] + directionThreats["back"] + directionThreats["left"] + 
                   directionThreats["right"] + directionThreats["top"] + directionThreats["bottom"];
        }
    }
}