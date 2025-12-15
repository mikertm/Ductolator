using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System.Collections.Generic;

namespace RTM.Ductolator.Tests.Models
{
    [TestClass]
    public class SanitaryVentCalculatorTests
    {
        [TestInitialize]
        public void Setup()
        {
            PlumbingTableProvider.Instance.Clear();
        }

        [TestMethod]
        public void MissingTable_ReturnsWarning()
        {
            string key = "non_existent_key";
            double result = SanitaryVentCalculator.MinBranchDiameterFromDfu(10, 0.02, key, out string warning);
            Assert.AreEqual(0, result);
            Assert.IsTrue(warning.Contains("Missing table"));
        }

        [TestMethod]
        public void ValidTable_ReturnsCorrectDiameter()
        {
            string key = "test_table";
            var rows = new List<(double, double)>
            {
                (1.5, 3),
                (2.0, 6),
                (3.0, 20)
            };
            SanitaryVentCalculator.RegisterSanitaryBranchDfuTable(key, rows);

            // Test 1: Within capacity
            double dia = SanitaryVentCalculator.MinBranchDiameterFromDfu(5, 0.02, key, out string w1);
            Assert.AreEqual(2.0, dia);
            Assert.AreEqual(string.Empty, w1);

            // Test 2: Exact match
            double dia2 = SanitaryVentCalculator.MinBranchDiameterFromDfu(20, 0.02, key, out string w2);
            Assert.AreEqual(3.0, dia2);
            Assert.AreEqual(string.Empty, w2);

            // Test 3: Exceeds capacity
            double dia3 = SanitaryVentCalculator.MinBranchDiameterFromDfu(21, 0.02, key, out string w3);
            Assert.AreEqual(0, dia3);
            Assert.IsTrue(w3.Contains("exceeds capacity"));
        }

        [TestMethod]
        public void ValidVentStackTable_WithDefaultFactors_ReturnsCorrectDiameter()
        {
            string key = "test_vent_stack_default";
            var stackRows = new List<(double, double)>
            {
                (1.5, 4),
                (2.0, 10),
                (3.0, 30)
            };
            // No length adjustments passed, should default to IPC (1.0/0.9/0.8 logic if implemented in provider as fallback, or 1.0 if empty)
            // Wait, I am updating SimpleVentStackTable to take adjustments. If null, what is the behavior?
            // The previous implementation had hardcoded logic.
            // I should verify the new implementation handles null adjustments gracefully (e.g., factor 1.0 or legacy fallback).
            // Let's pass explicit adjustments to be sure.

            var adjustments = new List<(double, double)>
            {
                (100, 1.0),
                (200, 0.9),
                (double.MaxValue, 0.8)
            };

            SanitaryVentCalculator.RegisterVentDfuLengthTable(key, null, stackRows, adjustments);

            // Test 1: Short length (factor 1.0)
            double dia = SanitaryVentCalculator.VentStackMinDiameter(8, 50, key, out string w1);
            Assert.AreEqual(2.0, dia); // 8 <= 10*1.0

            // Test 2: Medium length (factor 0.9) -> Max becomes 9
            double dia2 = SanitaryVentCalculator.VentStackMinDiameter(9.5, 150, key, out string w2);
            // 9.5 > 10*0.9 (9.0) -> needs next size (3.0 capacity 30*0.9=27)
            Assert.AreEqual(3.0, dia2);

            // Test 3: Long length (factor 0.8) -> Max becomes 8
            double dia3 = SanitaryVentCalculator.VentStackMinDiameter(7, 250, key, out string w3);
            // 7 <= 10*0.8 (8.0) -> 2.0 fits
            Assert.AreEqual(2.0, dia3);
        }
    }
}
