using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System.Collections.Generic;

namespace RTM.Ductolator.Tests
{
    [TestClass]
    public class DuctCalculatorTests
    {
        [TestMethod]
        public void Verify_RoundArea_StandardCalculation()
        {
            // Scenario: 10 inch round duct
            double diameterIn = 10.0;
            double expectedAreaFt2 = 0.5454; // pi * (10/12)^2 / 4

            double actual = DuctCalculator.Area_Round_Ft2(diameterIn);

            TestTolerance.AssertApproximatelyEqual(expectedAreaFt2, actual, 1e-4, "Round area calculation mismatch");
        }

        [TestMethod]
        public void Verify_Velocity_FromCfmAndArea()
        {
            // Scenario: 1000 CFM in 1 sq ft
            double cfm = 1000;
            double area = 1.0;
            double expectedVel = 1000;

            double actual = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, area);

            TestTolerance.AssertApproximatelyEqual(expectedVel, actual, 1e-4, "Velocity calculation mismatch");
        }

        [TestMethod]
        public void Verify_VelocityPressure_StandardAir()
        {
            // Scenario: 4005 FPM at standard air (0.075 density) => VP = 1.0 in.wg
            double vel = 4005;
            var air = new DuctCalculator.AirProperties(0.075, 1.6e-4);
            double expectedVp = 1.0;

            double actual = DuctCalculator.VelocityPressure_InWG(vel, air);

            TestTolerance.AssertApproximatelyEqual(expectedVp, actual, 1e-3, "VP at standard air mismatch");
        }

        [TestMethod]
        public void Verify_ReynoldsNumber_WaterVsAir()
        {
            // Scenario: 1000 FPM in 12 inch duct with standard air viscosity
            // Re = (V_fps * D_ft) / nu
            // V_fps = 1000/60 = 16.666
            // D_ft = 1.0
            // nu = 1.62e-4 (approx standard)
            double vel = 1000;
            double dia = 12;
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4);

            double expectedRe = (1000.0 / 60.0 * 1.0) / 1.62e-4; // ~ 102880

            double actual = DuctCalculator.Reynolds(vel, dia, air);

            TestTolerance.AssertRelative(expectedRe, actual, 0.001, "Reynolds number mismatch");
        }

        [TestMethod]
        public void Verify_FrictionFactor_Churchill_Turbulent()
        {
            // Reference scenario: Re=100,000, e/D = 0.0003/1.0 = 0.0003
            // Explicit Churchill calc or Moody chart read ~ 0.019
            double re = 100000;
            double dia = 12;
            double rough = 0.0003;

            double f = DuctCalculator.FrictionFactor(re, dia, rough);

            // Expect roughly 0.019 for these parameters
            TestTolerance.AssertApproximatelyEqual(0.019, f, 0.002, "Friction factor turbulent sanity check");
        }

        [TestMethod]
        public void Verify_DpPer100Ft_StandardAir()
        {
            // Reference: SMACNA/ASHRAE 1000 CFM in 12" round
            // V = 1273 FPM
            // Re ~ 1.3e5
            // f ~ 0.018-0.019
            // VP ~ 0.10
            // dP_100 = f * (100/D) * VP = 0.019 * 100 * 0.1 ~ 0.19 in.wg
            // Let's use specific math:
            double cfm = 1000;
            double dia = 12;
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4);

            double area = DuctCalculator.Area_Round_Ft2(dia);
            double v = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, area);
            double re = DuctCalculator.Reynolds(v, dia, air);
            double f = DuctCalculator.FrictionFactor(re, dia, 0.0003);
            double actualDp = DuctCalculator.DpPer100Ft_InWG(v, dia, f, air);

            // Hand calc check:
            // Area = 0.7854 ft2. V = 1273.2 fpm.
            // VP = (1273.2/4005)^2 = 0.101 in.wg.
            // Re = (1273.2/60)*1 / 1.62e-4 = 131,000.
            // f (Churchill) ~ 0.0185 (approx)
            // dP = 0.0185 * (100/1) * 0.101 = 0.187 in.wg/100ft

            TestTolerance.AssertApproximatelyEqual(0.187, actualDp, 0.01, "DP/100ft calculation mismatch");
        }

        [TestMethod]
        public void Verify_TotalPressureDrop_LengthOnly()
        {
            // Scenario: 100 ft run, dP/100 = 0.1, sumK=0
            double dp100 = 0.1;
            double len = 50;
            double sumK = 0;
            double vel = 1000;
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4);

            double actual = DuctCalculator.TotalPressureDrop_InWG(dp100, len, sumK, vel, air);
            double expected = 0.05;

            TestTolerance.AssertApproximatelyEqual(expected, actual, 1e-5, "Total DP length component mismatch");
        }

        [TestMethod]
        public void Verify_MinorLoss_SumK()
        {
            // Scenario: K=1.5, VP=1.0
            // Loss = 1.5 * 1.0 = 1.5 in.wg
            double dp100 = 0;
            double len = 0;
            double sumK = 1.5;
            double vel = 4005; // VP=1
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4);

            double actual = DuctCalculator.TotalPressureDrop_InWG(dp100, len, sumK, vel, air);

            TestTolerance.AssertApproximatelyEqual(1.5, actual, 1e-4, "Minor loss calculation mismatch");
        }

        [TestMethod]
        public void Verify_EquivalentRound_Rect()
        {
            // Scenario: 12x12 rect => Equiv D = 1.3 * (144)^0.625 / (24)^0.25
            // 12x12 is hydraulically somewhat larger than 12 round? Or smaller?
            // Actually Huebscher for 12x12 is:
            // D_e = 1.3 * (144)^0.625 / (24)^0.25
            // 144^0.625 = 22.36
            // 24^0.25 = 2.213
            // D_e = 1.3 * 22.36 / 2.213 = 13.13 inches

            double d = DuctCalculator.EquivalentRound_Rect(12, 12);
            TestTolerance.AssertApproximatelyEqual(13.1, d, 0.1, "Equivalent round for square mismatch");
        }

        [TestMethod]
        public void Verify_EqualFriction_Rect_Sanity()
        {
            // Round 10 inch. AR=1.
            // Should be roughly 9-something square?
            // Let's check logic: (s*s)^0.625 / (2s)^0.25 = D_e/1.3
            // For square s: s^1.25 / (1.189 * s^0.25) = s / 1.189
            // s / 1.189 = D_e / 1.3 -> s = D_e * 1.189 / 1.3 = D_e * 0.91
            // For 10 inch round: s ~ 9.1 inches.

            var (s1, s2) = DuctCalculator.EqualFrictionRectangleForRound(10, 1.0);

            Assert.AreEqual(s1, s2, 0.001);
            TestTolerance.AssertApproximatelyEqual(9.15, s1, 0.2, "Equal friction square side mismatch");
        }

        [TestMethod]
        public void Verify_AirProperties_AltitudeCorrection()
        {
            // Scenario: 5000 ft altitude at 70F
            // Standard pressure 14.696 psia -> 12.23 psia approx
            // Density ratio approx 0.83

            var air = DuctCalculator.AirAt(70, 5000);

            // Expected density ~ 0.0624 lbm/ft3 (0.075 * 0.832)
            TestTolerance.AssertApproximatelyEqual(0.0624, air.DensityLbmPerFt3, 0.001, "Density at altitude mismatch");
        }
    }
}
