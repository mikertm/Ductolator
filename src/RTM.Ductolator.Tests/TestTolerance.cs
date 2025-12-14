using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RTM.Ductolator.Tests
{
    public static class TestTolerance
    {
        public static void AssertApproximatelyEqual(double expected, double actual, double absTol, double relTol, string message)
        {
            double diff = Math.Abs(expected - actual);
            double maxAbs = Math.Max(Math.Abs(expected), 1e-9); // Avoid divide by zero
            double relDiff = diff / maxAbs;

            if (diff <= absTol || relDiff <= relTol)
            {
                return; // Pass
            }

            Assert.Fail($"{message}\nExpected: {expected}\nActual:   {actual}\nDiff:     {diff} (Rel: {relDiff:P4})\nTol:      Abs={absTol}, Rel={relTol}");
        }

        public static void AssertApproximatelyEqual(double expected, double actual, double tolerance, string message)
        {
            // Treat single tolerance as absolute if small, or maybe hybrid?
            // The prompt says "overloads for percent tolerance only and absolute only".
            // Let's implement specific ones.
            AssertApproximatelyEqual(expected, actual, tolerance, 0.0, message);
        }

        public static void AssertRelative(double expected, double actual, double relTol, string message)
        {
            AssertApproximatelyEqual(expected, actual, 0.0, relTol, message);
        }
    }
}
