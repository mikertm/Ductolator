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
        // IPC Table 710.1(2) Horizontal Fixture Branches and Stacks.
        // NOTE: This table is independent of slope (unlike Building Drain).
        // The original code used Building Drain capacities here which was incorrect for "Branches".
        // Now updated to use Fixture Branch limits (more conservative).
        private static List<(double DiameterIn, double MaxDfu)> HorizontalBranchCapacity = new()
        {
            (1.5, 3),
            (2.0, 6),
            (2.5, 12),
            (3.0, 20),
            (4.0, 160),
            (5.0, 360),
            (6.0, 620),
            (8.0, 1400)
        };

        // If we want to support the old structure for Building Drains just in case, we can keep it,
        // but given the class name "SanitaryVentCalculator" and method "MinBranchDiameter",
        // it strongly implies fixture branches.
        // However, the signature uses 'slopeFtPerFt', which implies building drain logic.
        // If the user passes a slope, they might expect slope-dependent sizing (Building Drain).
        // But "Branch" usually means fixture branch.
        // To be safe and correct per the name "Horizontal Branch", we should use the Branch table.
        // We will ignore the slope for capacity lookup but check it for minimum velocity/code compliance if needed (1/4" usually min for <=2.5").

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

        public static void SetHorizontalBranchCapacity(List<(double DiameterIn, double MaxDfu)> capacityTable)
        {
            if (capacityTable == null || capacityTable.Count == 0) return;

             var row = capacityTable
                .Where(v => v.DiameterIn > 0 && v.MaxDfu > 0)
                .OrderBy(v => v.DiameterIn)
                .ToList();

            if (row.Count > 0)
                HorizontalBranchCapacity = row;
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
        /// Minimum nominal diameter (in) to carry the given sanitary DFU on a horizontal fixture branch.
        /// Uses IPC Table 710.1(2). Slope is ignored for capacity but must meet code minimums (usually 1/4" per ft).
        /// Returns 0 when no table value satisfies the demand.
        /// </summary>
        public static double MinBranchDiameterFromDfu(double drainageFixtureUnits, double slopeFtPerFt)
        {
            if (drainageFixtureUnits <= 0) return 0;

            // Note: Slope is not used for capacity lookup of Horizontal Fixture Branches in IPC.
            // Capacity is fixed by diameter.

            foreach (var entry in HorizontalBranchCapacity)
            {
                if (drainageFixtureUnits <= entry.MaxDfu)
                    return entry.DiameterIn;
            }

            return 0;
        }

        /// <summary>
        /// Maximum allowable DFU for a given nominal diameter for a horizontal fixture branch.
        /// Uses IPC Table 710.1(2).
        /// Returns 0 if the diameter is not present.
        /// </summary>
        public static double AllowableFixtureUnits(double diameterIn, double slopeFtPerFt)
        {
            if (diameterIn <= 0) return 0;

            var match = HorizontalBranchCapacity.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
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
    }
}
