
$source = @"
using System;
using System.Collections.Generic;

namespace RTM.Ductolator.Models
{
    public static class DuctCalculator
    {
        public struct AirProperties
        {
            public double DensityLbmPerFt3;
            public double DensitySlugPerFt3;
            public double KinematicViscosityFt2PerS;
            
            public AirProperties(double d1, double d2, double k)
            {
                DensityLbmPerFt3 = d1;
                DensitySlugPerFt3 = d2;
                KinematicViscosityFt2PerS = k;
            }

            public static AirProperties Standard 
            { 
                get { return new AirProperties(0.075, 0.075 / 32.174, 1.57e-4); } 
            }
        }

        private const double InPerFt = 12.0;
        private const double FtPer100Ft = 100.0;
        private const double Pi = Math.PI;
        private const double LbPerFt2_Per_InWG = 5.20233;
        private const double Roughness_Ft = 0.0003;

        public static AirProperties AirAt(double temperatureF, double altitudeFt)
        {
            return AirProperties.Standard; 
        }

        public class RectGeomResult 
        {
            public double AreaFt2;
            public double PerimeterFt;
            public double HydraulicDiameterFt;
        }

        public static RectGeomResult RectGeometry(double side1In, double side2In)
        {
            RectGeomResult res = new RectGeomResult();
            if (side1In <= 0 || side2In <= 0) return res;
            double a = side1In / InPerFt;
            double b = side2In / InPerFt;
            res.AreaFt2 = a * b;
            res.PerimeterFt = 2.0 * (a + b);
            res.HydraulicDiameterFt = 4.0 * res.AreaFt2 / res.PerimeterFt;
            return res;
        }

        public static double EquivalentRound_Rect(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return 0;
            double a = side1In;
            double b = side2In;
            double ab = a * b;
            return 1.30 * Math.Pow(ab, 0.625) / Math.Pow(a + b, 0.25);
        }

        public static double VelocityFpmFromCfmAndArea(double cfm, double areaFt2)
        {
            if (areaFt2 <= 0) return 0;
            return cfm / areaFt2;
        }

        public static double Reynolds(double velocityFpm, double hydraulicDiameterIn, AirProperties air)
        {
            double vFtPerS = velocityFpm / 60.0;
            double dFt = hydraulicDiameterIn / InPerFt;
            if (air.KinematicViscosityFt2PerS <= 0 || dFt <= 0 || vFtPerS <= 0) return 0;
            return (vFtPerS * dFt) / air.KinematicViscosityFt2PerS;
        }

        public static double FrictionFactor(double reynolds, double hydraulicDiameterIn)
        {
            double re = Math.Max(reynolds, 1.0);
            double dFt = hydraulicDiameterIn / InPerFt;
            if (dFt <= 0) return 0;
            double termLaminar = Math.Pow(8.0 / re, 12.0);
            double A = Math.Pow(2.457 * Math.Log(1.0 / (Math.Pow(7.0 / re, 0.9) + 0.27 * (Roughness_Ft / dFt))), 16.0);
            double B = Math.Pow(37530.0 / re, 16.0);
            double termMixed = Math.Pow(1.0 / (A + B), 1.5);
            return 8.0 * Math.Pow(termLaminar + termMixed, 1.0 / 12.0);
        }

        public static double DpPer100Ft_InWG(double velocityFpm, double hydraulicDiameterIn, double frictionFactor, AirProperties air)
        {
            double vFtPerS = velocityFpm / 60.0;
            double dFt = hydraulicDiameterIn / InPerFt;
            if (dFt <= 0 || frictionFactor <= 0 || vFtPerS <= 0) return 0;
            double dpPerFt_LbPerFt2 = frictionFactor * (air.DensitySlugPerFt3 * vFtPerS * vFtPerS / (2.0 * dFt));
            return (dpPerFt_LbPerFt2 * FtPer100Ft) / LbPerFt2_Per_InWG;
        }
    }

    public class Tester
    {
        public static void Run()
        {
            Console.WriteLine("Testing Rectangular Duct Logic...");
            
            // Test 1: CFM + Side 1 + Velocity -> Side 2
            double cfm = 1000;
            double velInput = 1200;
            double s1In = 24;
            double s2In = 0; 
            
            double knownSide = s1In > 0 ? s1In : s2In;
            double missingSide = 0;
            
            double usedVelFpm = velInput;
            double areaFt2 = cfm / usedVelFpm;
            double areaIn2 = areaFt2 * 144.0;
            missingSide = areaIn2 / knownSide;
            
            Console.WriteLine("Test 1 (Velocity): CFM=" + cfm + ", Vel=" + velInput + ", S1=" + s1In);
            Console.WriteLine("Result: S2 = " + missingSide.ToString("F2") + " inches");
            
            if (Math.Abs(missingSide - 5.0) < 0.1) 
                Console.WriteLine("[PASS] Calculated side is approx 5.0 inches");
            else 
                Console.WriteLine("[FAIL] Expected approx 5.0 inches");

            // Test 2: CFM + Side 1 + Friction -> Side 2
            cfm = 2000;
            double dp100Input = 0.1;
            s1In = 20;
            s2In = 0;
            knownSide = 20;
            missingSide = 0;
            
            DuctCalculator.AirProperties air = DuctCalculator.AirProperties.Standard;
            
            // Bisection logic
            double lo = 2.0;
            double hi = 120.0;
            
            double fLo = CalcDiff(lo, knownSide, cfm, dp100Input, air);
            double fHi = CalcDiff(hi, knownSide, cfm, dp100Input, air);
            
            for (int i = 0; i < 20 && fLo * fHi > 0; i++)
            {
                lo = Math.Max(1.0, lo / 1.5);
                hi *= 1.5;
                fLo = CalcDiff(lo, knownSide, cfm, dp100Input, air);
                fHi = CalcDiff(hi, knownSide, cfm, dp100Input, air);
            }

            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = CalcDiff(mid, knownSide, cfm, dp100Input, air);
                if (Math.Abs(fMid) < 1e-4) { missingSide = mid; break; }
                if (fLo * fMid < 0) { hi = mid; fHi = fMid; }
                else { lo = mid; fLo = fMid; }
            }
            if (missingSide <= 0) missingSide = 0.5 * (lo + hi);
            
            Console.WriteLine("Test 2 (Friction): CFM=" + cfm + ", dP=" + dp100Input + ", S1=" + s1In);
            Console.WriteLine("Result: S2 = " + missingSide.ToString("F2") + " inches");
            
            // Verification
            double finalS2 = missingSide;
            double finalLong = Math.Max(knownSide, finalS2);
            double finalShort = Math.Min(knownSide, finalS2);
            double finalEq = DuctCalculator.EquivalentRound_Rect(finalLong, finalShort);
            var finalGeom = DuctCalculator.RectGeometry(finalLong, finalShort);
            double finalVel = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, finalGeom.AreaFt2);
            double finalRe = DuctCalculator.Reynolds(finalVel, finalEq, air);
            double finalF = DuctCalculator.FrictionFactor(finalRe, finalEq);
            double finalDp = DuctCalculator.DpPer100Ft_InWG(finalVel, finalEq, finalF, air);
            
            Console.WriteLine("Verification dP: " + finalDp.ToString("F4") + " (Target: " + dp100Input + ")");
            
            if (Math.Abs(finalDp - dp100Input) < 0.005)
                Console.WriteLine("[PASS] Friction matches target.");
            else
                Console.WriteLine("[FAIL] Friction mismatch.");
        }

        private static double CalcDiff(double testSide, double knownSide, double cfm, double targetDp, DuctCalculator.AirProperties air)
        {
            double longSide = Math.Max(knownSide, testSide);
            double shortSide = Math.Min(knownSide, testSide);
            double eqRound = DuctCalculator.EquivalentRound_Rect(longSide, shortSide);
            var geom = DuctCalculator.RectGeometry(longSide, shortSide);
            double testVel = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, geom.AreaFt2);
            double re = DuctCalculator.Reynolds(testVel, eqRound, air);
            double f = DuctCalculator.FrictionFactor(re, eqRound);
            double dp = DuctCalculator.DpPer100Ft_InWG(testVel, eqRound, f, air);
            return dp - targetDp;
        }
    }
}
"@

Add-Type -TypeDefinition $source -Language CSharp
[RTM.Ductolator.Models.Tester]::Run()
