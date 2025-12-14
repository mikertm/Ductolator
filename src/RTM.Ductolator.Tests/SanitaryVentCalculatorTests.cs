using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTM.Ductolator.Models;
using System.Collections.Generic;

namespace RTM.Ductolator.Tests
{
    [TestClass]
    public class SanitaryVentCalculatorTests
    {
        [TestMethod]
        public void Verify_Sanitary_Branch_Lookup_WithData()
        {
            // Register dummy table
            SanitaryVentCalculator.RegisterSanitaryBranchDfuTable("test_table", new List<(double, double)>
            {
                (2.0, 6.0),
                (3.0, 20.0),
                (4.0, 160.0)
            });

            // Scenario: 10 DFU -> should fit in 3 inch (cap 20)
            string warn;
            double d = SanitaryVentCalculator.MinBranchDiameterFromDfu(10, 0.02, "test_table", out warn);

            Assert.AreEqual(3.0, d);
            Assert.AreEqual("", warn);
        }

        [TestMethod]
        public void Verify_Sanitary_Branch_Lookup_MissingTable()
        {
            string warn;
            double d = SanitaryVentCalculator.MinBranchDiameterFromDfu(10, 0.02, "missing_table", out warn);

            Assert.AreEqual(0.0, d);
            Assert.IsTrue(warn.Contains("Missing table"));
        }
    }
}
