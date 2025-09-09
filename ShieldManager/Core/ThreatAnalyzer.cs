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

        private string GetThreatDirection(MyDetectedEntityInfo threat)
        {
            Vector3D threatPos = threat.Position;
            Vector3D myPos = program.Me.GetPosition();
            Vector3D worldDirection = threatPos - myPos;
            
            // Use the Programming Block's orientation vectors directly
            MatrixD worldMatrix = program.Me.WorldMatrix;
            Vector3D forward = worldMatrix.Forward;
            Vector3D left = worldMatrix.Left;
            Vector3D up = worldMatrix.Up;
            
            // Calculate dot products to determine which direction is dominant
            double dotForward = Vector3D.Dot(worldDirection, forward);
            double dotLeft = Vector3D.Dot(worldDirection, left);
            double dotUp = Vector3D.Dot(worldDirection, up);
            
            double absDotForward = Math.Abs(dotForward);
            double absDotLeft = Math.Abs(dotLeft);
            double absDotUp = Math.Abs(dotUp);

            // Add debug output
            program.Echo($"=== DIRECTION CALCULATION ===");
            program.Echo($"Dot Forward: {dotForward:F2}, Left: {dotLeft:F2}, Up: {dotUp:F2}");
            program.Echo($"Abs Forward: {absDotForward:F2}, Left: {absDotLeft:F2}, Up: {absDotUp:F2}");

            string result;
            if (absDotLeft >= absDotForward && absDotLeft >= absDotUp)
            {
                result = dotLeft > 0 ? "left" : "right";
                program.Echo($"Left/Right dominant ({dotLeft:F2}) -> {result.ToUpper()}");
            }
            else if (absDotUp >= absDotForward && absDotUp >= absDotLeft)
            {
                result = dotUp > 0 ? "top" : "bottom";
                program.Echo($"Up/Down dominant ({dotUp:F2}) -> {result.ToUpper()}");
            }
            else
            {
                result = dotForward > 0 ? "front" : "back";
                program.Echo($"Forward/Back dominant ({dotForward:F2}) -> {result.ToUpper()}");
            }

            return result;
        }

        public void AnalyzeDirectionalThreats()
        {
            if (!wcApiActive) return;

            // --- Get General Target and Lock-on Info ---
            CurrentTarget = wcApi.GetAiFocus(program.Me.CubeGrid.EntityId);
            var lockData = wcApi.GetProjectilesLockedOn(program.Me.CubeGrid.EntityId);
            IncomingLocks = lockData.Item2;

            // --- Adaptive Shunting based on CLOSEST threat ---
            if (!config.ADAPTIVE_SHUNT) return;
            
            if (config.ticks < lastWcThreatTick + ConfigManager.THREAT_REFRESH_INTERVAL) return;
            lastWcThreatTick = config.ticks;

            wcThreatBuffer.Clear();
            wcApi.GetSortedThreats(program.Me, wcThreatBuffer);

            // Add debug for threat detection
            program.Echo($"Threats detected: {wcThreatBuffer.Count}");

            if (wcThreatBuffer.Count == 0)
            {
                program.Echo("No threats - applying balanced shunt");
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
                program.Echo($"Applying shunt: {direction.ToUpper()}");
                shieldController.ApplyShunt(direction);
            }
            else
            {
                program.Echo("No valid threats - applying balanced shunt");
                shieldController.ApplyShunt("balanced");
            }
        }
    }
}