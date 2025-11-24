using System;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Storm drainage helpers using Manning's equation and IPC/UPC rainfall conversions.
    /// Supports rainfall-intensity flow, full-pipe sizing, partial-flow sizing, and
    /// vertical leader capacity checks.
    /// </summary>
    public static class StormDrainageCalculator
    {
        private const double InPerFt = 12.0;

        /// <summary>
        /// Storm flow in gpm from roof/area (ft²) and rainfall intensity (in/hr):
        /// Q = I * A / 96.23 per IPC/UPC rainfall method.
        /// </summary>
        public static double FlowFromRainfall(double areaFt2, double rainfallIntensityInPerHr)
        {
            if (areaFt2 <= 0 || rainfallIntensityInPerHr <= 0) return 0;
            return rainfallIntensityInPerHr * areaFt2 / 96.23;
        }

        /// <summary>
        /// Solve full-flow circular pipe diameter (in) for a storm flow using Manning's equation.
        /// Q (gpm) = 449 * (1/n) * A * R^(2/3) * S^(1/2) where A in ft², R in ft, S slope (ft/ft).
        /// </summary>
        public static double FullFlowDiameterFromGpm(double flowGpm, double slopeFtPerFt, double roughnessN = 0.012,
                                                     double minDiameterIn = 2.0, double maxDiameterIn = 60.0)
        {
            if (flowGpm <= 0 || slopeFtPerFt <= 0 || roughnessN <= 0) return 0;

            double FlowFromDiameter(double dIn)
            {
                double dFt = dIn / InPerFt;
                double area = Math.PI * dFt * dFt / 4.0;
                double radius = dFt / 4.0; // hydraulic radius for full pipe
                double qCfs = (1.49 / roughnessN) * area * Math.Pow(radius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return qCfs * 448.831; // to gpm
            }

            return SolveByBisection(flowGpm, minDiameterIn, maxDiameterIn, FlowFromDiameter);
        }

        /// <summary>
        /// Solve diameter (in) when the circular section runs partially full by depth ratio (y/D).
        /// Uses the standard circular segment area/perimeter relationships with Manning's equation.
        /// </summary>
        public static double PartialFlowDiameterFromGpm(double flowGpm, double slopeFtPerFt, double depthRatio,
                                                        double roughnessN = 0.012, double minDiameterIn = 2.0,
                                                        double maxDiameterIn = 60.0)
        {
            if (flowGpm <= 0 || slopeFtPerFt <= 0 || roughnessN <= 0) return 0;
            depthRatio = Math.Max(0.05, Math.Min(0.99, depthRatio));

            double FlowFromDiameter(double dIn)
            {
                double dFt = dIn / InPerFt;
                double area = PartiallyFullArea(dFt, depthRatio);
                double wettedPerimeter = PartiallyFullWettedPerimeter(dFt, depthRatio);
                if (wettedPerimeter <= 0 || area <= 0) return 0;
                double hydraulicRadius = area / wettedPerimeter;
                double qCfs = (1.49 / roughnessN) * area * Math.Pow(hydraulicRadius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return qCfs * 448.831; // to gpm
            }

            return SolveByBisection(flowGpm, minDiameterIn, maxDiameterIn, FlowFromDiameter);
        }

        /// <summary>
        /// Maximum gpm that can be carried by a vertical leader (slope ~ 1.0) at full flow.
        /// </summary>
        public static double VerticalLeaderMaxFlow(double diameterIn, double roughnessN = 0.012)
        {
            if (diameterIn <= 0 || roughnessN <= 0) return 0;
            return FullFlowFromDiameter(diameterIn, 1.0, roughnessN);
        }

        /// <summary>
        /// Minimum leader diameter (in) for a given flow assuming vertical orientation (slope ~1.0).
        /// </summary>
        public static double VerticalLeaderDiameter(double flowGpm, double roughnessN = 0.012,
                                                    double minDiameterIn = 2.0, double maxDiameterIn = 24.0)
        {
            if (flowGpm <= 0 || roughnessN <= 0) return 0;
            return FullFlowDiameterFromGpm(flowGpm, 1.0, roughnessN, minDiameterIn, maxDiameterIn);
        }

        private static double SolveByBisection(double targetFlow, double lo, double hi, Func<double, double> flowFromDiameter)
        {
            double fLo = flowFromDiameter(lo) - targetFlow;
            double fHi = flowFromDiameter(hi) - targetFlow;

            for (int i = 0; i < 50; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = flowFromDiameter(mid) - targetFlow;

                if (Math.Abs(fMid) < 1e-3)
                    return mid;

                if (fLo * fMid > 0)
                {
                    lo = mid;
                    fLo = fMid;
                }
                else
                {
                    hi = mid;
                    fHi = fMid;
                }
            }

            return 0.5 * (lo + hi);
        }

        private static double PartiallyFullArea(double diameterFt, double depthRatio)
        {
            double theta = 2 * Math.Acos(1 - 2 * depthRatio); // central angle in radians
            double radius = diameterFt / 2.0;
            return (radius * radius / 2.0) * (theta - Math.Sin(theta));
        }

        private static double PartiallyFullWettedPerimeter(double diameterFt, double depthRatio)
        {
            double theta = 2 * Math.Acos(1 - 2 * depthRatio);
            double radius = diameterFt / 2.0;
            return radius * theta;
        }

        private static double FullFlowFromDiameter(double diameterIn, double slopeFtPerFt, double roughnessN)
        {
            double dFt = diameterIn / InPerFt;
            double area = Math.PI * dFt * dFt / 4.0;
            double radius = dFt / 4.0;
            double qCfs = (1.49 / roughnessN) * area * Math.Pow(radius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
            return qCfs * 448.831;
        }
    }
}
