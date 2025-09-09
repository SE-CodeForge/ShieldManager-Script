using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    internal class ThreatAnalyzer
    {
        private readonly Program program;
        private readonly ConfigManager config;
        private readonly ShieldController shieldController;

        private WcPbApi wcApi;
        private bool wcApiActive;

        // Threat data buffers
        private Dictionary<MyDetectedEntityInfo, float> wcThreatBuffer =
            new Dictionary<MyDetectedEntityInfo, float>(16, new Program.DetectedEntityComparer());
        private int lastWcThreatTick = -999;

        public bool WcApiActive => wcApiActive;
        public MyDetectedEntityInfo? CurrentTarget { get; private set; }
        public int IncomingLocks { get; private set; }

        public ThreatAnalyzer(Program program, ConfigManager config, ShieldController shieldController)
        {
            this.program = program;
            this.config = config;
            this.shieldController = shieldController;
            InitWeaponCoreApi();
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

        public void AnalyzeDirectionalThreats()
        {
            if (!wcApiActive) return;

            // --- Get General Target and Lock-on Info ---
            CurrentTarget = wcApi.GetAiFocus(program.Me.CubeGrid.EntityId);
            var lockData = wcApi.GetProjectilesLockedOn(program.Me.CubeGrid.EntityId);
            IncomingLocks = lockData.Item2; // Item2 is the number of projectiles locked on

            // --- Adaptive Shunting based on CLOSEST threat ---
            if (!config.ADAPTIVE_SHUNT) return;
            
            if (config.ticks < lastWcThreatTick + ConfigManager.THREAT_REFRESH_INTERVAL) return;
            lastWcThreatTick = config.ticks;

            wcThreatBuffer.Clear();
            wcApi.GetSortedThreats(program.Me, wcThreatBuffer);

            if (wcThreatBuffer.Count == 0)
            {
                shieldController.ApplyShunt("balanced");
                return;
            }

            // Find the closest threat
            MyDetectedEntityInfo closestThreat = new MyDetectedEntityInfo();
            double closestDistSq = double.MaxValue;
            Vector3D myPos = program.Me.GetPosition();

            foreach (var entry in wcThreatBuffer)
            {
                var threatInfo = entry.Key;
                double distSq = Vector3D.DistanceSquared(myPos, threatInfo.Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestThreat = threatInfo;
                }
            }

            // Apply shunt based on the direction of the closest threat
            if (closestThreat.EntityId != 0)
            {
                string direction = GetThreatDirection(closestThreat);
                shieldController.ApplyShunt(direction);
            }
            else
            {
                shieldController.ApplyShunt("balanced");
            }
        }

        private string GetThreatDirection(MyDetectedEntityInfo threat)
        {
            // Use the Defense Shields controller's coordinate system instead of the PB's
            Vector3D threatPos = threat.Position;
            Vector3D controllerPos = shieldController.DSControl.GetPosition();
            
            // Transform the threat position into the controller's local coordinate system
            Vector3D worldOffset = threatPos - controllerPos;
            Vector3D localThreatVector = Vector3D.Transform(worldOffset, 
                MatrixD.Transpose(shieldController.DSControl.WorldMatrix));

            double absX = Math.Abs(localThreatVector.X);
            double absY = Math.Abs(localThreatVector.Y);
            double absZ = Math.Abs(localThreatVector.Z);

            // Determine the dominant direction in the controller's coordinate system
            if (absX >= absY && absX >= absZ)
                return localThreatVector.X > 0 ? "right" : "left";
            else if (absY >= absX && absY >= absZ)
                return localThreatVector.Y > 0 ? "top" : "bottom";
            else
                return localThreatVector.Z > 0 ? "back" : "front";
        }
    }
}