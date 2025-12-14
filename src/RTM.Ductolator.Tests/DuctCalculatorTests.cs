using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System;

namespace RTM.Ductolator.Tests
{
    [TestClass]
    public class DuctCalculatorTests
    {
        [TestMethod]
        public void Verify_DefaultRoughness()
        {
            Assert.AreEqual(0.0003, DuctCalculator.DefaultGalvanizedRoughnessFt, 1e-10, "Default roughness mismatch");
        }

        [TestMethod]
        public void Verify_Colebrook_Laminar()
        {
            // Re=1000 => f = 64/1000 = 0.064
            double re = 1000;
            double dia = 12;
            double f = DuctCalculator.FrictionFactor_ColebrookDarcy(re, dia);
            TestTolerance.AssertApproximatelyEqual(0.064, f, 1e-6, "Laminar friction mismatch");
        }

        [TestMethod]
        public void Verify_Colebrook_Transitional()
        {
            // Re=3500. Just check it runs and isn't crazy.
            // Haaland gives approx: 1/rt(f) = -1.8 log((0.0003/1.0/3.7)^1.11 + 6.9/3500)
            // 0.0003/3.7 = 8e-5. 6.9/3500 = 0.00197. Term ~ 0.002.
            // -1.8 * log(0.002) ~ -1.8 * -2.7 = 4.86. f ~ 1/23.6 ~ 0.042
            double re = 3500;
            double dia = 12;
            double f = DuctCalculator.FrictionFactor_ColebrookDarcy(re, dia);
            Assert.IsTrue(f > 0.03 && f < 0.06, $"Transitional f={f} seems out of range");
        }

        [TestMethod]
        public void Verify_Colebrook_Turbulent_Comparison()
        {
            double[] res = { 1e5, 5e5, 1e6, 5e6 };
            double dia = 12; // 1 ft
            double eps = 0.0003;

            foreach (var re in res)
            {
                double fCole = DuctCalculator.FrictionFactor_ColebrookDarcy(re, dia, eps);
                double fHaal = DuctCalculator.FrictionFactor_HaalandDarcy(re, dia, eps);

                // Haaland is approx, but usually within 2%.
                TestTolerance.AssertRelative(fCole, fHaal, 0.02, $"Colebrook vs Haaland mismatch at Re={re}");
            }
        }

        [TestMethod]
        public void Verify_DpPer100Ft_UnitConsistency()
        {
            // Reference scenario: 1000 cfm, 10 inch round, standard air.
            // Manual check inside test.
            double cfm = 1000;
            double diaIn = 10;
            var air = DuctCalculator.AirAt(70, 0); // Standard

            // 1. Geometry
            double areaFt2 = Math.PI * Math.Pow(diaIn / 12.0, 2) / 4.0;
            double vFpm = cfm / areaFt2; // ~ 1833

            // 2. VP in.wg.
            // VP = rho * (V_fps)^2 / (2 * 32.174) * (1/5.202)
            double vFps = vFpm / 60.0;
            double rho = air.DensityLbmPerFt3;
            double vpExpected = (rho * vFps * vFps) / (2.0 * 32.174) * (1.0 / 5.202);

            // 3. Re
            double dFt = diaIn / 12.0;
            double nu = air.KinematicViscosityFt2PerS;
            double reExpected = (vFps * dFt) / nu;

            // 4. Friction Factor (Colebrook) - solve here or trust helper for this step?
            // Let's use the helper but verify it matches manual call.
            double fExpected = DuctCalculator.FrictionFactor_ColebrookDarcy(reExpected, diaIn);

            // 5. dP per 100
            // dP = f * (L/D) * VP
            double dpExpected = fExpected * (100.0 / dFt) * vpExpected;

            // Actual
            double vpActual = DuctCalculator.VelocityPressure_InWG(vFpm, air);
            double reActual = DuctCalculator.Reynolds(vFpm, diaIn, air);
            double fActual = DuctCalculator.FrictionFactor(reActual, diaIn); // Uses Colebrook
            double dpActual = DuctCalculator.DpPer100Ft_InWG(vFpm, diaIn, fActual, air);

            TestTolerance.AssertApproximatelyEqual(vpExpected, vpActual, 1e-4, "VP calculation check");
            TestTolerance.AssertApproximatelyEqual(dpExpected, dpActual, 1e-4, "dP unit consistency check");
        }

        [TestMethod]
        public void Verify_EquivalentDiameter_Rect()
        {
            // ASHRAE Huebscher: De = 1.30 * ((a*b)^0.625) / ((a+b)^0.25)
            // Case: 24 x 12
            double a = 24;
            double b = 12;
            double num = Math.Pow(a * b, 0.625); // 288^0.625
            double den = Math.Pow(a + b, 0.25);  // 36^0.25
            double expected = 1.30 * num / den; // ~ 18.2 inches?

            // 288^0.625 = 34.33
            // 36^0.25 = 2.449
            // 1.3 * 34.33 / 2.449 = 18.22

            double actual = DuctCalculator.EquivalentRound_Rect(a, b);
            TestTolerance.AssertApproximatelyEqual(expected, actual, 1e-3, "Rect equivalent diameter mismatch");
        }

        [TestMethod]
        public void Verify_EquivalentDiameter_Rect_Square()
        {
            // Case: 10 x 10
            // De = 1.3 * (100)^0.625 / (20)^0.25
            // 100^0.625 = 17.78
            // 20^0.25 = 2.114
            // 1.3 * 17.78 / 2.114 = 10.93

            double actual = DuctCalculator.EquivalentRound_Rect(10, 10);
            TestTolerance.AssertApproximatelyEqual(10.93, actual, 0.01, "Square equivalent diameter mismatch");
        }

        [TestMethod]
        public void Verify_EquivalentDiameter_FlatOval()
        {
            // ASHRAE: De = 1.55 * A^0.625 / P^0.25
            // Case: Minor=10, Major=30
            // Rect part: 20 x 10. Circle part: 10 dia.
            // Area = 20*10 + pi*25 = 200 + 78.54 = 278.54
            // Perim = 2*20 + pi*10 = 40 + 31.416 = 71.416

            // Expected = 1.55 * 278.54^0.625 / 71.416^0.25
            // 278.54^0.625 = 33.63
            // 71.416^0.25 = 2.909
            // 1.55 * 33.63 / 2.909 = 17.92

            double actual = DuctCalculator.EquivalentRound_FlatOval(10, 30);
            TestTolerance.AssertApproximatelyEqual(17.92, actual, 0.05, "Flat oval equivalent diameter mismatch");
        }

        // --- Existing tests preserved/updated ---

        [TestMethod]
        public void Verify_RoundArea_StandardCalculation()
        {
            double diameterIn = 10.0;
            double expectedAreaFt2 = 0.5454;
            double actual = DuctCalculator.Area_Round_Ft2(diameterIn);
            TestTolerance.AssertApproximatelyEqual(expectedAreaFt2, actual, 1e-4, "Round area calculation mismatch");
        }

        [TestMethod]
        public void Verify_Velocity_FromCfmAndArea()
        {
            double cfm = 1000;
            double area = 1.0;
            double expectedVel = 1000;
            double actual = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, area);
            TestTolerance.AssertApproximatelyEqual(expectedVel, actual, 1e-4, "Velocity calculation mismatch");
        }

        [TestMethod]
        public void Verify_VelocityPressure_StandardAir()
        {
            double vel = 4005;
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4);
            // VP = 0.075 * (4005/60)^2 / (2 * 32.174 * 5.202)
            // = 0.075 * 4455.56 / 334.7 = 0.998 ~ 1.0

            double expectedVp = 1.0;
            double actual = DuctCalculator.VelocityPressure_InWG(vel, air);
            TestTolerance.AssertApproximatelyEqual(expectedVp, actual, 0.005, "VP at standard air mismatch");
        }

        [TestMethod]
        public void Verify_ReynoldsNumber_WaterVsAir()
        {
            double vel = 1000;
            double dia = 12;
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4);
            double expectedRe = (1000.0 / 60.0 * 1.0) / 1.62e-4;
            double actual = DuctCalculator.Reynolds(vel, dia, air);
            TestTolerance.AssertRelative(expectedRe, actual, 0.001, "Reynolds number mismatch");
        }

        [TestMethod]
        public void Verify_FrictionFactor_Churchill_Turbulent()
        {
            // Re=100000, e/D=0.0003. Colebrook.
            double re = 100000;
            double dia = 12;
            double rough = 0.0003;
            double f = DuctCalculator.FrictionFactor_ColebrookDarcy(re, dia, rough);
            // ~0.019
            TestTolerance.AssertApproximatelyEqual(0.019, f, 0.002, "Friction factor turbulent sanity check");
        }

        [TestMethod]
        public void Verify_TotalPressureDrop_LengthOnly()
        {
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
            double dp100 = 0;
            double len = 0;
            double sumK = 1.5;
            double vel = 4005;
            var air = new DuctCalculator.AirProperties(0.075, 1.62e-4); // VP approx 1.0
            double vp = DuctCalculator.VelocityPressure_InWG(vel, air); // ~1.0
            double expected = 1.5 * vp;
            double actual = DuctCalculator.TotalPressureDrop_InWG(dp100, len, sumK, vel, air);
            TestTolerance.AssertApproximatelyEqual(expected, actual, 1e-4, "Minor loss calculation mismatch");
        }

        [TestMethod]
        public void Verify_EqualFriction_Rect_Sanity()
        {
            var (s1, s2) = DuctCalculator.EqualFrictionRectangleForRound(10, 1.0);
            Assert.AreEqual(s1, s2, 0.001);
            // ~10.9
            TestTolerance.AssertApproximatelyEqual(10.93, s1, 0.2, "Equal friction square side mismatch");
        }

        [TestMethod]
        public void Verify_AirProperties_AltitudeCorrection()
        {
            var air = DuctCalculator.AirAt(70, 5000);
            TestTolerance.AssertApproximatelyEqual(0.0624, air.DensityLbmPerFt3, 0.001, "Density at altitude mismatch");
        }
    }
}
