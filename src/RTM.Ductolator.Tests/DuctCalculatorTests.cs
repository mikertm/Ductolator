using System;
using RTM.Ductolator.Models;

public class DuctCalculatorTests
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Running DuctCalculator Tests...");
        Test_EquivalentRound_Rect();
        Test_FrictionFactor();
        Test_DpPer100Ft();
        Console.WriteLine("DuctCalculator Tests Complete.");
    }

    private static void Test_EquivalentRound_Rect()
    {
        // Example: 12x12 duct. Equivalent round should be ~13.0 inches by huebscher?
        // Actually, Huebscher equation: De = 1.30 * (ab)^0.625 / (a+b)^0.25
        // For 12x12: De = 1.30 * (144)^0.625 / (24)^0.25
        // 144^0.625 = 22.33
        // 24^0.25 = 2.213
        // De = 1.30 * 22.33 / 2.213 = 13.11 inches.

        double de = DuctCalculator.EquivalentRound_Rect(12, 12);
        Console.WriteLine($"EquivalentRound_Rect(12, 12): {de:F2} (Expected ~13.11)");
    }

    private static void Test_FrictionFactor()
    {
        // Check friction factor calculation.
        // Re = 100000, D = 12 inches.
        double f = DuctCalculator.FrictionFactor(100000, 12);
        Console.WriteLine($"FrictionFactor(100000, 12): {f:F5}");
    }

    private static void Test_DpPer100Ft()
    {
         // 12" round duct, 1000 CFM.
         // Area = PI * 0.5^2 / 4 = 0.7854 ft2
         // Velocity = 1000 / 0.7854 = 1273 FPM

         double area = DuctCalculator.Area_Round_Ft2(12);
         double vel = DuctCalculator.VelocityFpmFromCfmAndArea(1000, area);
         double re = DuctCalculator.Reynolds(vel, 12);
         double f = DuctCalculator.FrictionFactor(re, 12);
         double dp = DuctCalculator.DpPer100Ft_InWG(vel, 12, f);

         Console.WriteLine($"DpPer100Ft(1000 CFM, 12 in): {dp:F3} in.wg/100ft");
    }
}
