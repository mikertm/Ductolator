using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System;

namespace RTM.Ductolator.Tests.Models
{
    [TestClass]
    public class FittingLossTests
    {
        private static readonly DuctCalculator.AirProperties StandardAir = DuctCalculator.AirAt(70, 0);

        [TestMethod]
        public void Verify_Duct_UseSumK_IgnoresLeq()
        {
            // Setup
            double frictionPer100 = 0.1;
            double straightLength = 100.0; // 0.1 friction total
            double sumK = 1.0;
            double leq = 5000.0; // Huge, should be ignored
            double velocityFpm = 4005.0; // VP = 1.0 in.w.g. approx

            // Expected: Friction(100ft) + K(1.0)*VP(1.0) = 0.1 + 1.0 = 1.1
            // If Leq used: Friction(5100ft) = 5.1 -> 5.1 total.

            double vp = DuctCalculator.VelocityPressure_InWG(velocityFpm, StandardAir);
            // Verify VP is approx 1.0
            Assert.IsTrue(Math.Abs(vp - 1.0) < 0.05, $"VP {vp} not close to 1.0");

            double result = DuctCalculator.TotalPressureDrop_InWG(
                frictionPer100,
                straightLength,
                sumK,
                leq,
                FittingLossMode.UseSumK,
                velocityFpm,
                StandardAir);

            double expected = (frictionPer100 * straightLength / 100.0) + (sumK * vp);
            Assert.AreEqual(expected, result, 1e-4, "Should use SumK and ignore Leq");
        }

        [TestMethod]
        public void Verify_Duct_UseEquivalentLength_IgnoresSumK()
        {
            // Setup
            double frictionPer100 = 0.1;
            double straightLength = 100.0;
            double sumK = 10.0; // Huge K, should be ignored
            double leq = 50.0;
            double velocityFpm = 4005.0;

            // Expected: Friction(150ft) = 0.15. K ignored.

            double result = DuctCalculator.TotalPressureDrop_InWG(
                frictionPer100,
                straightLength,
                sumK,
                leq,
                FittingLossMode.UseEquivalentLength,
                velocityFpm,
                StandardAir);

            double expected = frictionPer100 * (straightLength + leq) / 100.0;
            Assert.AreEqual(expected, result, 1e-4, "Should use Leq and ignore SumK");
        }

        [TestMethod]
        public void Verify_Plumbing_HazenWilliams_UseSumK_IgnoresLeq()
        {
            // Hazen-Williams
            double gpm = 100;
            double id = 2.0;
            double c = 140;
            double length = 100;
            double sumK = 1.5;
            double leq = 1000; // Should ignore
            double psiPerFt = 0.433; // ~water

            double psi100 = PlumbingCalculator.HazenWilliamsPsiPer100Ft(gpm, id, c, psiPerFt);
            double velocity = PlumbingCalculator.VelocityFpsFromGpm(gpm, id);
            double velocityHeadPsi = PlumbingCalculator.MinorLossPsi(velocity, 1.0); // K=1

            double result = PlumbingCalculator.TotalPressureDropPsi_HazenWilliams(
                gpm, id, c, length, sumK, leq, FittingLossMode.UseSumK, psiPerFt);

            double expectedFriction = psi100 * (length / 100.0);
            double expectedDynamic = sumK * velocityHeadPsi;

            Assert.AreEqual(expectedFriction + expectedDynamic, result, 1e-4);
        }

        [TestMethod]
        public void Verify_Plumbing_HazenWilliams_UseEquivalentLength_IgnoresK()
        {
            double gpm = 100;
            double id = 2.0;
            double c = 140;
            double length = 100;
            double sumK = 50.0; // Should ignore
            double leq = 50;
            double psiPerFt = 0.433;

            double psi100 = PlumbingCalculator.HazenWilliamsPsiPer100Ft(gpm, id, c, psiPerFt);

            double result = PlumbingCalculator.TotalPressureDropPsi_HazenWilliams(
                gpm, id, c, length, sumK, leq, FittingLossMode.UseEquivalentLength, psiPerFt);

            double expectedFriction = psi100 * ((length + leq) / 100.0);

            Assert.AreEqual(expectedFriction, result, 1e-4);
        }

        [TestMethod]
        public void Verify_Plumbing_Darcy_UseSumK_IgnoresLeq()
        {
            double gpm = 100;
            double id = 2.0;
            double rough = 0.0005;
            double nu = 1.2e-5;
            double length = 100;
            double sumK = 2.0;
            double leq = 500;
            double psiPerFt = 0.433;

            double psi100 = PlumbingCalculator.HeadLoss_Darcy_PsiPer100Ft(gpm, id, rough, nu, psiPerFt);
            double velocity = PlumbingCalculator.VelocityFpsFromGpm(gpm, id);
            double velocityHeadPsi = PlumbingCalculator.MinorLossPsi(velocity, 1.0);

            double result = PlumbingCalculator.TotalPressureDropPsi_Darcy(
                gpm, id, rough, nu, length, sumK, leq, FittingLossMode.UseSumK, psiPerFt);

            double expectedFriction = psi100 * (length / 100.0);
            double expectedDynamic = sumK * velocityHeadPsi;

            Assert.AreEqual(expectedFriction + expectedDynamic, result, 1e-4);
        }
    }
}
