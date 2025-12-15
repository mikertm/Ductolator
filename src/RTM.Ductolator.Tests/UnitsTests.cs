using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System;

namespace RTM.Ductolator.Tests
{
    [TestClass]
    public class UnitsTests
    {
        [TestMethod]
        public void Verify_Constants_Roundtrip()
        {
            // Verify internal consistency
            TestTolerance.AssertApproximatelyEqual(1.0, Units.InchesPerFoot * Units.FeetPerInch, 1e-9, "Length conversion inverse");
            TestTolerance.AssertApproximatelyEqual(1.0, Units.SqInchesPerSqFoot * Units.SqFeetPerSqInch, 1e-9, "Area conversion inverse");
            TestTolerance.AssertApproximatelyEqual(1.0, Units.PsfPerPsi * Units.PsiPerPsf, 1e-9, "Pressure conversion inverse");
            TestTolerance.AssertApproximatelyEqual(1.0, Units.GpmToCfs * Units.CfsToGpm, 1e-9, "Flow conversion inverse");
        }

        [TestMethod]
        public void Verify_SpecificConstants()
        {
            // Verify specific values against expected standards
            Assert.AreEqual(12.0, Units.InchesPerFoot);
            Assert.AreEqual(144.0, Units.SqInchesPerSqFoot);
            Assert.AreEqual(32.174, Units.Gc);
            Assert.AreEqual(459.67, Units.RankineZeroF);

            // 7.48052 gal/ft^3
            TestTolerance.AssertApproximatelyEqual(7.48052, Units.GallonsPerCubicFoot, 1e-5, "Gallons per cubic foot");

            // 1 in.w.g. = 5.2023 psf (approx)
            TestTolerance.AssertApproximatelyEqual(5.202, Units.PsfPerInWg, 0.001, "Psf per InWg");
        }

        [TestMethod]
        public void Verify_Formatting()
        {
            string s = Units.Format(1234.5678, "0.00");
            Assert.AreEqual("1234.57", s); // Rounding check + invariant
        }

        [TestMethod]
        public void Verify_Integration_DuctDpUnits()
        {
            // Verify that DuctCalculator produces in.w.g./100ft using Units constants
            // 1000 CFM, 10 inch round, standard air
            // V = 1833.46 fpm
            // D = 10 in
            // Re ~ 103,000
            // f ~ 0.019
            // VP = (1833/4005)^2 = 0.21 in.wg
            // dP = f * (100/D_ft) * VP
            // D_ft = 0.833
            // dP = 0.019 * 120 * 0.21 = 0.48 in.wg
            // Actual calc runs full Colebrook

            double cfm = 1000;
            double dia = 10;
            var air = DuctCalculator.AirAt(70, 0); // uses Units internal

            double area = DuctCalculator.Area_Round_Ft2(dia);
            double v = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, area);
            double re = DuctCalculator.Reynolds(v, dia, air);
            double f = DuctCalculator.FrictionFactor(re, dia);
            double dp = DuctCalculator.DpPer100Ft_InWG(v, dia, f, air);

            // Expect roughly 0.4-0.5 range
            Assert.IsTrue(dp > 0.4 && dp < 0.6, $"Duct dP {dp} out of expected range");
        }

        [TestMethod]
        public void Verify_Integration_PlumbingLossUnits()
        {
            // Hazen Williams
            // 50 gpm, 2.067 in ID, C=120
            // Returns PSI per 100 ft
            double loss = PlumbingCalculator.HazenWilliamsPsiPer100Ft(50, 2.067, 120, 0.433);

            // Expected ~ 2.6 psi
            TestTolerance.AssertApproximatelyEqual(2.6, loss, 0.2, "Plumbing loss units check");
        }

        [TestMethod]
        public void Verify_Integration_StormFlowUnits()
        {
            // Rainfall flow
            // 1000 sq ft, 3 in/hr
            // Q = 1000 * 3 / 96.23 = 31.17 gpm
            double q = StormDrainageCalculator.FlowFromRainfall(1000, 3);
            TestTolerance.AssertApproximatelyEqual(31.2, q, 0.1, "Storm rainfall flow units check");
        }
    }
}
