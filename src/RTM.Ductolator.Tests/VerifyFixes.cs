using System;
using RTM.Ductolator.Models;
using System.Collections.Generic;

public class VerifyFixes
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Verifying Fixes...");
        VerifyStormDrainage();
        VerifyPlumbingCalculator();
        VerifySanitaryVentCalculator();
        Console.WriteLine("Verification Complete.");
    }

    private static void VerifyStormDrainage()
    {
        Console.WriteLine("\n--- Storm Drainage Verification ---");
        // IPC Table 1106.2 Check
        // 4" vertical leader should max out at 192 GPM (previously ~920 GPM)
        double maxFlow4 = StormDrainageCalculator.VerticalLeaderMaxFlow(4.0);
        Console.WriteLine($"VerticalLeaderMaxFlow(4.0): {maxFlow4} GPM (Expected 192)");
        if (maxFlow4 != 192) Console.WriteLine("FAIL: 4 inch leader capacity incorrect.");

        // Size for 300 GPM
        // Should be 5" (360 GPM capacity)
        double diameterFor300 = StormDrainageCalculator.VerticalLeaderDiameter(300);
        Console.WriteLine($"VerticalLeaderDiameter(300 GPM): {diameterFor300} inches (Expected 5)");
        if (diameterFor300 != 5) Console.WriteLine("FAIL: Diameter for 300 GPM incorrect.");
    }

    private static void VerifyPlumbingCalculator()
    {
        Console.WriteLine("\n--- Plumbing Calculator Verification ---");
        // Check Fixture Branch Sizing (IPC 710.1(2))
        // 18 DFU. Should be 3" (Max 20). 2.5" is max 12. 2" is max 6.
        double diaBranch = PlumbingCalculator.MinSanitaryDiameterFromDfu(18, 0.02, isBuildingDrain: false);
        Console.WriteLine($"MinSanitaryDiameterFromDfu(18 DFU, Branch): {diaBranch} inches (Expected 3)");
        if (diaBranch != 3) Console.WriteLine("FAIL: Branch sizing for 18 DFU incorrect.");

        // Check Building Drain Sizing (IPC 710.1(1))
        // 30 DFU at 1/4" slope (0.0208).
        // 3" Building Drain at 1/4" slope is Max 42 DFU.
        // 2.5" is Max 24.
        // So should be 3".
        double diaDrain = PlumbingCalculator.MinSanitaryDiameterFromDfu(30, 0.02083, isBuildingDrain: true);
        Console.WriteLine($"MinSanitaryDiameterFromDfu(30 DFU, Drain 1/4 slope): {diaDrain} inches (Expected 3)");

        // 30 DFU at 1/8" slope (0.0104).
        // 3" Building Drain at 1/8" slope is Max 36 DFU.
        // 2.5" is Max 20.
        // So should be 3".
        double diaDrainEighth = PlumbingCalculator.MinSanitaryDiameterFromDfu(30, 0.0104, isBuildingDrain: true);
        Console.WriteLine($"MinSanitaryDiameterFromDfu(30 DFU, Drain 1/8 slope): {diaDrainEighth} inches (Expected 3)");
    }

    private static void VerifySanitaryVentCalculator()
    {
        Console.WriteLine("\n--- Sanitary Vent Calculator Verification ---");
        // Check Horizontal Branch Capacity (updated to Fixture Branch table)
        // 5 DFU. Should be 2" (Max 6). 1.5" is Max 3.
        double dia = SanitaryVentCalculator.MinBranchDiameterFromDfu(5, 0.02);
        Console.WriteLine($"MinBranchDiameterFromDfu(5 DFU): {dia} inches (Expected 2)");
        if (dia != 2) Console.WriteLine("FAIL: Vent/Branch sizing for 5 DFU incorrect.");
    }
}
