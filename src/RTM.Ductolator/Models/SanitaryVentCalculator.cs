using System;
using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Sanitary waste and vent sizing helpers following IPC/UPC style DFU tables.
    /// Provides DFU-to-diameter lookups for horizontal branches, vent stacks/stack vents,
    /// and helper methods to report allowable fixture units for a given slope and size.
    /// </summary>
    public static class SanitaryVentCalculator
    {
        private const double InPerFt = 12.0;
        private const double SlopeQuarterInPerFt_FtPerFt = 0.25 / InPerFt;   // 1/4 in per ft
        private const double SlopeEighthInPerFt_FtPerFt = 0.125 / InPerFt;   // 1/8 in per ft
        private const double SlopeSixteenthInPerFt_FtPerFt = 0.0625 / InPerFt; // 1/16 in per ft

        // IPC/UPC style DFU limits for horizontal branches (nominal diameter, max DFU)
        private static Dictionary<double, List<(double DiameterIn, double MaxDfu)>> HorizontalBranchCapacity = new()
        {
            { SlopeQuarterInPerFt_FtPerFt, new List<(double, double)> { (2.0, 21), (2.5, 24), (3.0, 35), (4.0, 216) } },
            { SlopeEighthInPerFt_FtPerFt, new List<(double, double)> { (2.0, 15), (2.5, 20), (3.0, 36), (4.0, 180) } },
            { SlopeSixteenthInPerFt_FtPerFt, new List<(double, double)> { (2.0, 8), (2.5, 21), (3.0, 42), (4.0, 216) } }
        };

        // IPC Table 906.1 / UPC vent stack-vent capacities (nominal diameter, max DFU at typical developed lengths)
        // The length reduction factor derates long vents for friction and load diversity.
        private static List<(double DiameterIn, double BaseMaxDfu)> VentStackBaseCapacity = new()
        {
            (1.25, 1),
            (1.5, 8),
            (2.0, 24),
            (2.5, 84),
            (3.0, 212),
            (4.0, 500)
        };

        public static void SetHorizontalBranchCapacity(Dictionary<double, List<(double DiameterIn, double MaxDfu)>> capacityTable)
        {
            if (capacityTable == null || capacityTable.Count == 0) return;

            var newTable = new Dictionary<double, List<(double DiameterIn, double MaxDfu)>>();
            foreach (var kvp in capacityTable)
            {
                if (kvp.Key <= 0 || kvp.Value == null) continue;
                var row = kvp.Value
                    .Where(v => v.DiameterIn > 0 && v.MaxDfu > 0)
                    .OrderBy(v => v.DiameterIn)
                    .ToList();

                if (row.Count > 0)
                    newTable[kvp.Key] = row;
            }

            if (newTable.Count > 0)
                HorizontalBranchCapacity = newTable;
        }

        public static void SetVentStackBaseCapacity(List<(double DiameterIn, double BaseMaxDfu)> capacityTable)
        {
            if (capacityTable == null || capacityTable.Count == 0) return;
            var filtered = capacityTable
                .Where(v => v.DiameterIn > 0 && v.BaseMaxDfu > 0)
                .OrderBy(v => v.DiameterIn)
                .ToList();

            if (filtered.Count > 0)
                VentStackBaseCapacity = filtered;
        }

        /// <summary>
        /// Minimum nominal diameter (in) to carry the given sanitary DFU on a horizontal branch at the given slope (ft/ft).
        /// Returns 0 when no table value satisfies the demand.
        /// </summary>
        public static double MinBranchDiameterFromDfu(double drainageFixtureUnits, double slopeFtPerFt)
        {
            if (drainageFixtureUnits <= 0 || slopeFtPerFt <= 0) return 0;

            double closestSlope = ClosestSlopeKey(HorizontalBranchCapacity.Keys, slopeFtPerFt);
            if (closestSlope == 0) return 0;

            foreach (var entry in HorizontalBranchCapacity[closestSlope])
            {
                if (drainageFixtureUnits <= entry.MaxDfu)
                    return entry.DiameterIn;
            }

            return 0;
        }

        /// <summary>
        /// Maximum allowable DFU for a given nominal diameter and slope using embedded IPC/UPC branch tables.
        /// Returns 0 if the diameter is not present in the nearest slope row.
        /// </summary>
        public static double AllowableFixtureUnits(double diameterIn, double slopeFtPerFt)
        {
            if (diameterIn <= 0 || slopeFtPerFt <= 0) return 0;

            double closestSlope = ClosestSlopeKey(HorizontalBranchCapacity.Keys, slopeFtPerFt);
            if (closestSlope == 0) return 0;

            var row = HorizontalBranchCapacity[closestSlope];
            var match = row.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
            return match == default ? 0 : match.MaxDfu;
        }

        /// <summary>
        /// Minimum vent stack or stack vent diameter (in) for a given total vented DFU and developed length (ft).
        /// Uses IPC/UPC style vent stack capacities with a simple derate beyond 100 ft.
        /// </summary>
        public static double VentStackMinDiameter(double ventedDfu, double developedLengthFt)
        {
            if (ventedDfu <= 0) return 0;

            double lengthFactor = developedLengthFt <= 100 ? 1.0 : developedLengthFt <= 200 ? 0.9 : 0.8;

            foreach (var entry in VentStackBaseCapacity)
            {
                if (ventedDfu <= entry.BaseMaxDfu * lengthFactor)
                    return entry.DiameterIn;
            }

            return 0;
        }

        /// <summary>
        /// Reports the estimated DFU capacity for a vent stack diameter and developed length using the embedded table and derate.
        /// </summary>
        public static double VentStackAllowableFixtureUnits(double diameterIn, double developedLengthFt)
        {
            if (diameterIn <= 0) return 0;

            double lengthFactor = developedLengthFt <= 100 ? 1.0 : developedLengthFt <= 200 ? 0.9 : 0.8;
            var match = VentStackBaseCapacity.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
            if (match == default) return 0;

            return match.BaseMaxDfu * lengthFactor;
        }

        private static double ClosestSlopeKey(IEnumerable<double> keys, double slope)
        {
            double closest = 0;
            double minDelta = double.MaxValue;
            foreach (var key in keys)
            {
                double delta = Math.Abs(key - slope);
                if (delta < minDelta)
                {
                    minDelta = delta;
                    closest = key;
                }
            }

            return closest;
        }
    }
}
