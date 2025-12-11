using System;
using RTM.Ductolator.Models;

public class PlumbingCalculatorTests
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Running PlumbingCalculator Tests...");
        Test_HazenWilliams();
        Console.WriteLine("PlumbingCalculator Tests Complete.");
    }

    private static void Test_HazenWilliams()
    {
        // 100 GPM, 4 inch pipe, C=100
        // hf = 10.44 * Q^1.85 / (C^1.85 * d^4.87)
        // hf = 10.44 * 100^1.85 / (100^1.85 * 4^4.87)
        // hf = 10.44 * (100/100)^1.85 / 4^4.87
        // hf = 10.44 * 1 / 856.7
        // hf = 0.0121 ft/100ft ?? Wait
        // Let's recheck formula

        // C=150, 4" pipe, 200 GPM
        double headLoss = PlumbingCalculator.HazenWilliamsHeadLoss_FtPer100Ft(200, 4, 150);
        Console.WriteLine($"HazenWilliamsHeadLoss(200 GPM, 4 in, C=150): {headLoss:F4} ft/100ft");
    }
}
