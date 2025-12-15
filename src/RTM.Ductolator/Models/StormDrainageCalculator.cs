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
        private const double ManningCoefficient = 1.486; // US customary Manning constant
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

        // Table Registry
        private static readonly Dictionary<string, List<(double DiameterIn, double MaxGpm)>> StormLeaderTables = new();

        public static void RegisterStormLeaderTable(string key, List<(double DiameterIn, double MaxGpm)> rows)
        {
            if (string.IsNullOrWhiteSpace(key) || rows == null) return;
            StormLeaderTables[key] = rows.OrderBy(r => r.DiameterIn).ToList();
        }

        public static bool HasStormLeaderTable(string key) => !string.IsNullOrWhiteSpace(key) && StormLeaderTables.ContainsKey(key);

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
                double dFt = Units.FromInchesToFeet(dIn);
                double area = Math.PI * dFt * dFt / 4.0;
                double wettedPerimeter = Math.PI * dFt;
                double hydraulicRadius = wettedPerimeter > 0 ? area / wettedPerimeter : 0;
                double qCfs = (ManningCoefficient / nValue) * area * Math.Pow(hydraulicRadius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return Units.FromCfsToGpm(qCfs);
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
                double dFt = Units.FromInchesToFeet(dIn);
                double area = PartiallyFullArea(dFt, depthRatio);
                double wettedPerimeter = PartiallyFullWettedPerimeter(dFt, depthRatio);
                if (wettedPerimeter <= 0 || area <= 0) return 0;
                double hydraulicRadius = area / wettedPerimeter;
                double qCfs = (ManningCoefficient / nValue) * area * Math.Pow(hydraulicRadius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return Units.FromCfsToGpm(qCfs);
            }

            return SolveByBisection(flowGpm, minDiameterIn, maxDiameterIn, FlowFromDiameter);
        }

        /// <summary>
        /// Maximum gpm that can be carried by a vertical leader.
        /// </summary>
        public static double VerticalLeaderMaxFlow(double diameterIn, double n, string key, out string warning)
        {
            double maxGpm = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No storm leader table key provided.";
                return 0;
            }

            if (!StormLeaderTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (diameterIn <= 0) return 0;

            var match = rows.FirstOrDefault(x => Math.Abs(x.DiameterIn - diameterIn) < 0.1);
            if (match != default)
            {
                maxGpm = match.MaxGpm;
                return maxGpm;
            }

            // Find smaller size
            var lower = rows.Where(x => x.DiameterIn <= diameterIn).OrderByDescending(x => x.DiameterIn).FirstOrDefault();

            if (lower != default)
            {
                maxGpm = lower.MaxGpm;
                return maxGpm;
            }

            warning = "Diameter smaller than all entries in table.";
            return 0;
        }

        /// <summary>
        /// Minimum vertical leader diameter (in) for a given flow.
        /// </summary>
        public static double VerticalLeaderDiameter(double flowGpm, double n, string key, out string warning)
        {
            double diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No storm leader table key provided.";
                return 0;
            }

            if (!StormLeaderTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (flowGpm <= 0) return 0;

            foreach (var entry in rows)
            {
                if (entry.MaxGpm >= flowGpm)
                {
                    diameter = entry.DiameterIn;
                    return diameter;
                }
            }

            warning = "Flow exceeds capacity of largest leader in table.";
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
