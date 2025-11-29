$source = @"
using System;

public class DebugHW
{
    public static double HazenWilliamsHeadLoss_FtPer100Ft(double gpm, double diameterIn, double cFactor)
    {
        if (gpm <= 0 || diameterIn <= 0 || cFactor <= 0) return 0;

        // Current implementation in codebase
        double numerator = 4.52 * Math.Pow(gpm, 1.85);
        double denominator = Math.Pow(cFactor, 1.85) * Math.Pow(diameterIn, 4.87);
        return numerator / denominator;
    }

    public static void Test()
    {
        double gpm = 100;
        double dia = 3.0;
        double c = 150;

        double result = HazenWilliamsHeadLoss_FtPer100Ft(gpm, dia, c);
        Console.WriteLine("GPM: " + gpm + ", Dia: " + dia + ", C: " + c);
        Console.WriteLine("Result (labeled FtPer100Ft in code): " + result);
        
        // Manual check with 10.44 (Feet coefficient)
        double numFt = 10.44 * Math.Pow(gpm, 1.85);
        double den = Math.Pow(c, 1.85) * Math.Pow(dia, 4.87);
        double expectedFt = numFt / den;
        Console.WriteLine("Expected Ft (using 10.44): " + expectedFt);

        // Manual check with 4.52 (PSI coefficient)
        double numPsi = 4.52 * Math.Pow(gpm, 1.85);
        double expectedPsi = numPsi / den;
        Console.WriteLine("Expected PSI (using 4.52): " + expectedPsi);
    }
}
"@

Add-Type -TypeDefinition $source -Language CSharp
[DebugHW]::Test()
