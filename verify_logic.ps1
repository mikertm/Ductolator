$source = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class Verifier
{
    private const double InPerFt = 12.0;
    private const double SlopeQuarterInPerFt_FtPerFt = 0.25 / InPerFt;   // 1/4 in per ft
    private const double SlopeEighthInPerFt_FtPerFt = 0.125 / InPerFt;   // 1/8 in per ft
    private const double SlopeSixteenthInPerFt_FtPerFt = 0.0625 / InPerFt; // 1/16 in per ft

    // Using Tuple<double, double> instead of (double, double) for compatibility
    private static Dictionary<double, List<Tuple<double, double>>> SanitaryCapacity;

    static Verifier()
    {
        SanitaryCapacity = new Dictionary<double, List<Tuple<double, double>>>();
        
        var listQuarter = new List<Tuple<double, double>>();
        listQuarter.Add(new Tuple<double, double>(2.0, 21));
        listQuarter.Add(new Tuple<double, double>(2.5, 24));
        listQuarter.Add(new Tuple<double, double>(3.0, 42)); // Updated value
        listQuarter.Add(new Tuple<double, double>(4.0, 216));
        SanitaryCapacity.Add(SlopeQuarterInPerFt_FtPerFt, listQuarter);

        var listEighth = new List<Tuple<double, double>>();
        listEighth.Add(new Tuple<double, double>(2.0, 15));
        listEighth.Add(new Tuple<double, double>(2.5, 20));
        listEighth.Add(new Tuple<double, double>(3.0, 36));
        listEighth.Add(new Tuple<double, double>(4.0, 180));
        SanitaryCapacity.Add(SlopeEighthInPerFt_FtPerFt, listEighth);

        var listSixteenth = new List<Tuple<double, double>>();
        listSixteenth.Add(new Tuple<double, double>(2.0, 8));
        listSixteenth.Add(new Tuple<double, double>(2.5, 21));
        listSixteenth.Add(new Tuple<double, double>(3.0, 42));
        listSixteenth.Add(new Tuple<double, double>(4.0, 216));
        SanitaryCapacity.Add(SlopeSixteenthInPerFt_FtPerFt, listSixteenth);
    }

    public static double MinSanitaryDiameterFromDfu(double drainageFixtureUnits, double slopeFtPerFt)
    {
        if (drainageFixtureUnits <= 0 || slopeFtPerFt <= 0) return 0;

        double closestSlope = 0;
        double minDelta = double.MaxValue;
        foreach (var key in SanitaryCapacity.Keys)
        {
            double delta = Math.Abs(key - slopeFtPerFt);
            if (delta < minDelta)
            {
                minDelta = delta;
                closestSlope = key;
            }
        }

        if (closestSlope == 0) return 0;

        foreach (var entry in SanitaryCapacity[closestSlope])
        {
            // entry.Item1 is Diameter, entry.Item2 is MaxDfu
            if (drainageFixtureUnits <= entry.Item2)
                return entry.Item1;
        }

        return 0;
    }

    public static double GasFlow_Scfh(double diameterIn, double lengthFt, double pressureDropInWc,
                                      double specificGravity, double basePressurePsi)
    {
        if (diameterIn <= 0 || lengthFt <= 0 || pressureDropInWc <= 0 || specificGravity <= 0) return 0;

        // IFGC 2021 Equation 4-1
        // Q = 2313 * D^2.623 * (DeltaH / L)^0.541
        // Adjusted for SG: * sqrt(0.60 / SG)
        
        double baseFlow = 2313.0 * Math.Pow(diameterIn, 2.623) * Math.Pow(pressureDropInWc / lengthFt, 0.541);
        double gravityCorrection = Math.Sqrt(0.60 / specificGravity);

        return baseFlow * gravityCorrection;
    }

    public static void RunTests()
    {
        Console.WriteLine("Running Verification Tests...");
        bool allPassed = true;

        // Test 1: Sanitary Capacity
        // 3" pipe at 1/4" slope should handle 42 DFU (previously 35)
        double slope = 0.25 / 12.0;
        double dfu = 40; 
        double dia = MinSanitaryDiameterFromDfu(dfu, slope);
        
        if (dia == 3.0)
        {
            Console.WriteLine("[PASS] Sanitary: 40 DFU at 1/4 slope requires 3.0 inch pipe.");
        }
        else
        {
            Console.WriteLine("[FAIL] Sanitary: 40 DFU at 1/4 slope returned " + dia + " inch pipe. Expected 3.0.");
            allPassed = false;
        }

        // Test 2: Gas Sizing
        // Example: 1 inch pipe, 100 ft, 0.5 in w.c. drop, SG=0.60
        // Q = 2313 * 1^2.623 * (0.5/100)^0.541 * 1
        // Q = 2313 * 1 * (0.005)^0.541
        // 0.005^0.541 = 0.0568 (approx)
        // Q = 2313 * 0.0568 = 131.4 SCFH (approx)
        
        double gasFlow = GasFlow_Scfh(1.0, 100.0, 0.5, 0.60, 0.5);
        Console.WriteLine("[INFO] Gas Flow Result: " + gasFlow.ToString("F2") + " SCFH");

        if (gasFlow > 125 && gasFlow < 135)
        {
             Console.WriteLine("[PASS] Gas: Flow calculation is within expected range (approx 131 SCFH).");
        }
        else
        {
             Console.WriteLine("[FAIL] Gas: Flow calculation " + gasFlow + " is out of expected range.");
             allPassed = false;
        }

        if (allPassed) Console.WriteLine("ALL TESTS PASSED");
        else Console.WriteLine("SOME TESTS FAILED");
    }
}
"@

Add-Type -TypeDefinition $source -Language CSharp
[Verifier]::RunTests()
