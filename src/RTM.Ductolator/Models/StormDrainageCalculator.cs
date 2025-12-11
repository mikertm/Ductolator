using System;
using System.Collections.Generic;
using System.Linq;

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
        private const double ManningCoefficient = 1.486; // US customary Manning constant
        private const double CfsToGpm = 448.831;
        private static double _defaultRoughnessN = 0.012;

        public static double DefaultRoughnessN
        {
            get => _defaultRoughnessN;
            set
            {
                if (value > 0)
                    _defaultRoughnessN = value;
            }
        }

        // IPC Table 1106.2 (2021) Vertical Conductors and Leaders
        // Diameter (in) -> Max Flow (gpm)
        private static readonly List<(double DiameterIn, double MaxGpm)> VerticalLeaderCapacity = new()
        {
            (2.0, 30),
            (3.0, 92),
            (4.0, 192),
            (5.0, 360),
            (6.0, 563),
            (8.0, 1208)
        };

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
        /// Q (gpm) = 448.831 * (1.486/n) * A * R^(2/3) * S^(1/2) where A in ft², R in ft, S slope (ft/ft).
        /// </summary>
        public static double FullFlowDiameterFromGpm(double flowGpm, double slopeFtPerFt, double? roughnessN = null,
                                                     double minDiameterIn = 2.0, double maxDiameterIn = 60.0)
        {
            double nValue = roughnessN ?? _defaultRoughnessN;
            if (flowGpm <= 0 || slopeFtPerFt <= 0 || nValue <= 0) return 0;

            double FlowFromDiameter(double dIn)
            {
                double dFt = dIn / InPerFt;
                double area = Math.PI * dFt * dFt / 4.0;
                double wettedPerimeter = Math.PI * dFt;
                double hydraulicRadius = wettedPerimeter > 0 ? area / wettedPerimeter : 0;
                double qCfs = (ManningCoefficient / nValue) * area * Math.Pow(hydraulicRadius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return qCfs * CfsToGpm; // to gpm
            }

            return SolveByBisection(flowGpm, minDiameterIn, maxDiameterIn, FlowFromDiameter);
        }

        /// <summary>
        /// Solve diameter (in) when the circular section runs partially full by depth ratio (y/D).
        /// Uses the standard circular segment area/perimeter relationships with Manning's equation.
        /// </summary>
        public static double PartialFlowDiameterFromGpm(double flowGpm, double slopeFtPerFt, double depthRatio,
                                                        double? roughnessN = null, double minDiameterIn = 2.0,
                                                        double maxDiameterIn = 60.0)
        {
            double nValue = roughnessN ?? _defaultRoughnessN;
            if (flowGpm <= 0 || slopeFtPerFt <= 0 || nValue <= 0) return 0;
            depthRatio = Math.Max(0.05, Math.Min(0.99, depthRatio));

            double FlowFromDiameter(double dIn)
            {
                double dFt = dIn / InPerFt;
                double area = PartiallyFullArea(dFt, depthRatio);
                double wettedPerimeter = PartiallyFullWettedPerimeter(dFt, depthRatio);
                if (wettedPerimeter <= 0 || area <= 0) return 0;
                double hydraulicRadius = area / wettedPerimeter;
                double qCfs = (ManningCoefficient / nValue) * area * Math.Pow(hydraulicRadius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return qCfs * CfsToGpm; // to gpm
            }

            return SolveByBisection(flowGpm, minDiameterIn, maxDiameterIn, FlowFromDiameter);
        }

        /// <summary>
        /// Maximum gpm that can be carried by a vertical leader (IPC Table 1106.2).
        /// </summary>
        public static double VerticalLeaderMaxFlow(double diameterIn)
        {
            if (diameterIn <= 0) return 0;
            var match = VerticalLeaderCapacity.FirstOrDefault(x => Math.Abs(x.DiameterIn - diameterIn) < 0.1);
            if (match != default)
                return match.MaxGpm;

            // Interpolate or extrapolate if not exact match found
            // Since this is a code table, usually we pick the next smaller size's capacity (safe) or next larger size?
            // Safer to return 0 or interpolate carefully.
            // For now, let's find the largest size smaller than diameterIn.

            var lower = VerticalLeaderCapacity.Where(x => x.DiameterIn <= diameterIn).OrderByDescending(x => x.DiameterIn).FirstOrDefault();

            // If it's larger than our largest, we might need to extrapolate, but IPC stops at 8".
            // Some extended tables go higher. Let's stick to the table logic.
            if (lower != default)
            {
                // If it's effectively equal, we returned it above.
                // If we are between sizes (e.g. 2.5"), strictly speaking code requires next larger pipe size for the flow.
                // So capacity of a 2.5" pipe is effectively the capacity of a 2" pipe? No, that's for sizing.
                // Capacity of 2.5" pipe is at least capacity of 2" pipe.
                return lower.MaxGpm;
            }

            return 0;
        }

        /// <summary>
        /// Minimum vertical leader diameter (in) for a given flow using IPC Table 1106.2.
        /// </summary>
        public static double VerticalLeaderDiameter(double flowGpm)
        {
            if (flowGpm <= 0) return 0;

            foreach (var entry in VerticalLeaderCapacity)
            {
                if (entry.MaxGpm >= flowGpm)
                    return entry.DiameterIn;
            }

            // If flow exceeds largest table entry (8" = 1208 GPM), return 0 or largest?
            // Return 0 to indicate "Too large for standard table"
            return 0;
        }

        private static double SolveByBisection(double targetFlow, double lo, double hi, Func<double, double> flowFromDiameter)
        {
            double fLo = flowFromDiameter(lo) - targetFlow;
            double fHi = flowFromDiameter(hi) - targetFlow;

            if (Math.Abs(fLo) < 1e-3) return lo;
            if (Math.Abs(fHi) < 1e-3) return hi;

            // Clamp when the target flow is already achievable at the bounds to avoid runaway iterations.
            if (fLo > 0 && fHi > 0) return lo; // target below minimum diameter capacity
            if (fLo < 0 && fHi < 0) return hi; // target above maximum diameter capacity

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
    }
}
