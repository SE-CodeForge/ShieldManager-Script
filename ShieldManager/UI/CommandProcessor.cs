using System;
using System.Text;
using VRageMath;

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
                case "front":
                case "back":
                case "left":
                case "right":
                case "top":
                case "bottom":
                case "balanced":
                    shieldController.ForceShuntMode(arg);
                    displayManager.ClearPersistentOutputs();
                    program.Echo($"Manual shunt: {arg.ToUpper()}");
                    return;

                case "auto":
                    shieldController.ClearForceShunt();
                    displayManager.ShowPersistentOutput("Auto shunt mode enabled - system will respond to closest threats");
                    return;

                case "clear":
                    displayManager.ClearPersistentOutputs();
                    return;
                
                case "shielddiag":
                    DiagnoseShieldConnection();
                    return;
                    
                case "listactions":
                    displayManager.ListAllShieldActions();
                    return;

                case "test-orientation":
                    TestOrientation();
                    return;
                    
                default:
                    if (arg.StartsWith("test-direction "))
                    {
                        var direction = arg.Substring("test-direction ".Length).Trim();
                        TestDirection(direction);
                    }
                    else
                    {
                        ShowHelp();
                    }
                    return;
            }
        }

        private void ShowHelp()
        {
            var output = new StringBuilder();
            output.AppendLine("=== SHIELD MANAGER COMMANDS ===");
            output.AppendLine("");
            output.AppendLine("MANUAL SHUNT CONTROL:");
            output.AppendLine("  front, back, left, right, top, bottom");
            output.AppendLine("  balanced - equal power to all faces");
            output.AppendLine("  auto - enable automatic threat response");
            output.AppendLine("");
            output.AppendLine("DIAGNOSTICS:");
            output.AppendLine("  shielddiag - check shield connection");
            output.AppendLine("  listactions - show available shield actions");
            output.AppendLine("  test-orientation - check block alignment");
            output.AppendLine("  test-direction [face] - test specific direction");
            output.AppendLine("");
            output.AppendLine("UTILITY:");
            output.AppendLine("  clear - return to main display");
            
            displayManager.ShowPersistentOutput(output.ToString());
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
                output.AppendLine("=== CURRENT STATUS ===");
                output.AppendLine($"  Shield Level: {shieldController.ApiCachedPercent:P1}");
                output.AppendLine($"  Current Shunt: {shieldController.LastAppliedShunt?.ToUpper() ?? "NONE"}");
                output.AppendLine($"  Auto Mode: {(!shieldController.ForceShunt ? "ENABLED" : "DISABLED")}");
                output.AppendLine($"  WeaponCore API: {(threatAnalyzer.WcApiActive ? "ACTIVE" : "OFFLINE")}");
                
                if (threatAnalyzer.WcApiActive)
                {
                    var target = threatAnalyzer.CurrentTarget;
                    if (target.HasValue && target.Value.EntityId != 0)
                    {
                        output.AppendLine($"  Current Target: {target.Value.Name}");
                    }
                    if (threatAnalyzer.IncomingLocks > 0)
                    {
                        output.AppendLine($"  Missile Locks: {threatAnalyzer.IncomingLocks}");
                    }
                }
            }
            
            displayManager.ShowPersistentOutput(output.ToString());
        }

        private void TestOrientation()
        {
            var output = new StringBuilder();
            output.AppendLine("=== ORIENTATION TEST ===");
            
            if (shieldController.DSControl == null)
            {
                output.AppendLine("ERROR: No Defense Shields controller found!");
            }
            else
            {
                var pbPos = program.Me.GetPosition();
                var dsPos = shieldController.DSControl.GetPosition();
                var pbMatrix = program.Me.WorldMatrix;
                var dsMatrix = shieldController.DSControl.WorldMatrix;
                
                output.AppendLine($"Programming Block: {program.Me.CustomName}");
                output.AppendLine($"DS Controller: {shieldController.DSControl.CustomName}");
                output.AppendLine($"Distance: {Vector3D.Distance(pbPos, dsPos):F1}m");
                output.AppendLine();
                
                // Show relative orientations
                var relativeForward = Vector3D.Transform(pbMatrix.Forward, MatrixD.Transpose(dsMatrix));
                var relativeUp = Vector3D.Transform(pbMatrix.Up, MatrixD.Transpose(dsMatrix));
                var relativeRight = Vector3D.Transform(pbMatrix.Right, MatrixD.Transpose(dsMatrix));
                
                output.AppendLine("PB orientation relative to DS Controller:");
                output.AppendLine($"Forward: {relativeForward.ToString("F2")}");
                output.AppendLine($"Up: {relativeUp.ToString("F2")}");
                output.AppendLine($"Right: {relativeRight.ToString("F2")}");
                output.AppendLine();
                output.AppendLine("Use 'test-direction front' to verify directions work correctly.");
            }
            
            displayManager.ShowPersistentOutput(output.ToString());
        }
        
        private void TestDirection(string direction)
        {
            var validDirections = new[] {"front", "back", "left", "right", "top", "bottom", "balanced"};
            if (!validDirections.Contains(direction))
            {
                program.Echo($"Invalid direction: {direction}");
                program.Echo("Valid directions: " + string.Join(", ", validDirections));
                return;
            }
            
            shieldController.ApplyShunt(direction);
            program.Echo($"Applied {direction.ToUpper()} shunt - watch your shields!");
            
            var output = new StringBuilder();
            output.AppendLine($"=== DIRECTION TEST: {direction.ToUpper()} ===");
            output.AppendLine("Watch your shield display to see which face is reinforced.");
            output.AppendLine("If it's the wrong face, there may be an orientation issue.");
            output.AppendLine();
            output.AppendLine("Run 'test-orientation' to check block alignments.");
            output.AppendLine("Run 'auto' to return to automatic mode.");
            
            displayManager.ShowPersistentOutput(output.ToString());
        }
    }
}