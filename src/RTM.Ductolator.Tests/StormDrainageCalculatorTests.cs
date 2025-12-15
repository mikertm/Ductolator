using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System.Collections.Generic;

namespace RTM.Ductolator.Tests
{
    [TestClass]
    public class StormDrainageCalculatorTests
    {
        [TestMethod]
        public void Verify_FlowFromRainfall_IPC()
        {
            // Scenario: 1000 sf roof, 3 in/hr rain.
            // Q = 1000 * 3 / 96.23 = 31.17 gpm.
            double q = StormDrainageCalculator.FlowFromRainfall(1000, 3);
            TestTolerance.AssertApproximatelyEqual(31.2, q, 0.1, "Rainfall flow calculation mismatch");
        }

        [TestMethod]
        public void Verify_Manning_FullFlow_Sizing()
        {
            // Hand-calc: Manning full-flow PVC n=0.010, S=0.02 (2%), Q=50 gpm.
            // Q = 448.8 * (1.486/0.010) * A * R^(2/3) * S^(1/2)
            // K = 448.8 * 148.6 * 0.1414 = 9435
            // Q = K * A * R^(2/3).
            // For 3 inch (0.25 ft): A=0.049, P=0.785, R=0.0625.
            // A*R^(2/3) = 0.049 * 0.157 = 0.0077.
            // Q_cap = 9435 * 0.0077 = 72 gpm.
            // So for 50 gpm, it should size to 3 inch (assuming 2.5 isn't available or min is 2).
            // Let's check precise diameter solve.

            double d = StormDrainageCalculator.FullFlowDiameterFromGpm(72, 0.02, 0.010);
            TestTolerance.AssertApproximatelyEqual(3.0, d, 0.1, "Manning full flow diameter mismatch");
        }
    }
}
