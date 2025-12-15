using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System.Collections.Generic;

namespace RTM.Ductolator.Tests
{
    [TestClass]
    public class PlumbingCalculatorTests
    {
        [TestMethod]
        public void Verify_HazenWilliams_PsiPer100Ft()
        {
            // Scenario: 50 gpm, 2.067 inch ID, C=120
            // Formula: h_f (ft/100) = 0.2083 * (100/C)^1.85 * Q^1.85 / d^4.8655
            // With my constant 1044.0: h_f = 1044 * Q^1.85 / (C^1.85 * d^4.87)
            // Q=50, C=120, d=2.067
            // 50^1.85 = 1394.6
            // 120^1.85 = 7054.8
            // 2.067^4.87 = 34.25
            // h_f = 1044 * 1394.6 / (7054.8 * 34.25) = 1455962 / 241626 = 6.02 ft/100ft

            // Convert to PSI: 6.02 * 0.433 (std water) = 2.61 psi

            double gpm = 50;
            double id = 2.067;
            double c = 120;

            // Pass standard psi/ft head for water ~ 0.433
            double psiPerFt = PlumbingCalculator.PsiPerFtHeadFromDensity(62.4); // ~0.4333

            double psi = PlumbingCalculator.HazenWilliamsPsiPer100Ft(gpm, id, c, psiPerFt);

            // Hand calc target: ~2.6 psi/100ft.
            TestTolerance.AssertApproximatelyEqual(2.61, psi, 0.1, "Hazen-Williams calculation mismatch");
        }

        [TestMethod]
        public void Verify_Velocity_GpmAndId()
        {
            // 50 gpm in 2.067 inch ID
            // Area = 3.355 in2 = 0.0233 ft2
            // V = 50 gpm * 0.002228 / 0.0233 = 4.78 fps

            double v = PlumbingCalculator.VelocityFpsFromGpm(50, 2.067);
            TestTolerance.AssertApproximatelyEqual(4.78, v, 0.05, "Velocity calculation mismatch");
        }

        [TestMethod]
        public void Verify_DarcyPsi_StandardWater()
        {
            // 50 gpm, 2.067 inch ID.
            // V = 4.78 fps.
            // Re = 4.78 * (2.067/12) / 1.2e-5 (approx 60F)
            // Re ~ 68,000.
            // Smooth pipe (eps=0.000005 ft).
            // f ~ 0.02 (Churchill/Swamee).
            // h_f = f * (L/D) * v^2/2g
            // = 0.02 * (100 / 0.172) * 4.78^2 / 64.4
            // = 11.6 * 0.35 = 4.0 ft head.
            // psi = 4.0 * 0.433 = 1.7 psi.

            // Let's use the calculator methods.
            double psiPerFt = PlumbingCalculator.PsiPerFtHeadFromDensity(62.4);
            double psi = PlumbingCalculator.HeadLoss_Darcy_PsiPer100Ft(50, 2.067, 0.000005, 1.2e-5, psiPerFt);

            TestTolerance.AssertApproximatelyEqual(1.7, psi, 0.2, "Darcy calculation mismatch");
        }

        [TestMethod]
        public void Verify_DarcyFrictionFactor_Laminar()
        {
            // Re = 1000. f = 64/1000 = 0.064.
            double f = PlumbingCalculator.FrictionFactor(1000, 1.0, 0.0001);
            TestTolerance.AssertApproximatelyEqual(0.064, f, 1e-5, "Laminar friction factor mismatch");
        }

        [TestMethod]
        public void Verify_MinorLoss_Psi()
        {
            // V=10 fps. K=1.0.
            // h_m = 1.0 * 10^2 / 64.4 = 1.55 ft.
            // psi = 1.55 * 0.433 = 0.67 psi.

            double psi = PlumbingCalculator.MinorLossPsi(10, 1.0, 62.4);
            TestTolerance.AssertApproximatelyEqual(0.67, psi, 0.05, "Minor loss psi mismatch");
        }

        [TestMethod]
        public void Verify_GasSizing_NFPA54_LowPressure()
        {
            // Scenario: 100 ft of 1 inch Sched 40 (1.049" ID) @ 0.5" wc drop.
            // IFGC Eq 4-1: Q(kcfh) = 1.316 * sqrt(DH * D^5 / (Cr * L))
            // Q(scfh) = 1000 * 1.316 * sqrt(0.5 * 1.049^5 / (0.6094 * 100))
            // 1.049^5 = 1.27
            // Term = (0.5 * 1.27) / 60.94 = 0.01042
            // Sqrt = 0.102
            // Q = 1316 * 0.102 = 134 scfh.
            // Standard tables usually show ~130-150 range.

            double flow = PlumbingCalculator.GasFlow_Scfh(1.049, 100, 0.5);

            Assert.IsTrue(flow > 100 && flow < 200, $"Flow {flow} outside expected range (100-200 scfh)");
        }

        [TestMethod]
        public void Verify_FluidProperties_GlycolDensity()
        {
            // 30% Propylene Glycol at 60F.
            // Density should be > 62.4.
            var props = PlumbingCalculator.ResolveFluidProperties(PlumbingCalculator.FluidType.PropyleneGlycol, 60, 30);
            Assert.IsTrue(props.DensityLbmPerFt3 > 62.4);
            Assert.IsTrue(props.DensityLbmPerFt3 < 70.0);
        }
    }
}
