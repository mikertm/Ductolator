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

        // Table Registries
        private static readonly Dictionary<string, List<(double DiameterIn, double MaxDfu)>> SanitaryBranchDfuTables = new();
        private static readonly Dictionary<string, (List<(double DiameterIn, double MaxDfu)> BranchRows, List<(double DiameterIn, double BaseMaxDfu)> StackRows)> VentDfuLengthTables = new();

        public static void RegisterSanitaryBranchDfuTable(string key, List<(double DiameterIn, double MaxDfu)> rows)
        {
            if (string.IsNullOrWhiteSpace(key) || rows == null) return;
            SanitaryBranchDfuTables[key] = rows.OrderBy(r => r.DiameterIn).ToList();
        }

        public static bool HasSanitaryBranchDfuTable(string key) => !string.IsNullOrWhiteSpace(key) && SanitaryBranchDfuTables.ContainsKey(key);

        public static void RegisterVentDfuLengthTable(string key, List<(double DiameterIn, double MaxDfu)> branchRows, List<(double DiameterIn, double BaseMaxDfu)> stackRows)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            VentDfuLengthTables[key] = (
                branchRows?.OrderBy(r => r.DiameterIn).ToList() ?? new List<(double, double)>(),
                stackRows?.OrderBy(r => r.DiameterIn).ToList() ?? new List<(double, double)>()
            );
        }

        public static bool HasVentDfuLengthTable(string key) => !string.IsNullOrWhiteSpace(key) && VentDfuLengthTables.ContainsKey(key);


        /// <summary>
        /// Minimum nominal diameter (in) to carry the given sanitary DFU on a horizontal fixture branch.
        /// Uses registered table.
        /// </summary>
        public static bool TryBranchMinDiameter(double dfu, double slopeFtPerFt, string key, out double diameter, out string warning)
        {
            diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary branch DFU table key provided.";
                return false;
            }

            if (!SanitaryBranchDfuTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing sanitary branch DFU table: {key}";
                return false;
            }

            if (dfu <= 0) return true;

            foreach (var entry in rows)
            {
                if (dfu <= entry.MaxDfu)
                {
                    diameter = entry.DiameterIn;
                    return true;
                }
            }

            warning = "Demand exceeds capacity in table.";
            return false;
        }

        /// <summary>
        /// Reports the estimated DFU capacity for a horizontal fixture branch.
        /// </summary>
        public static bool TryAllowableFixtureUnits(double diameterIn, double slopeFtPerFt, string key, out double allowableDfu, out string warning)
        {
            allowableDfu = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary branch DFU table key provided.";
                return false;
            }

            if (!SanitaryBranchDfuTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing sanitary branch DFU table: {key}";
                return false;
            }

            if (diameterIn <= 0) return true;

            var match = rows.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
            if (match != default)
            {
                allowableDfu = match.MaxDfu;
                return true;
            }

            warning = "Diameter not found in table.";
            return false;
        }


        /// <summary>
        /// Minimum vent branch diameter.
        /// </summary>
        public static bool TryVentBranchMinDiameter(double dfu, double developedLengthFt, string key, out double diameter, out string warning)
        {
            diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return false;
            }

            if (!VentDfuLengthTables.TryGetValue(key, out var tables) || tables.BranchRows.Count == 0)
            {
                warning = $"Missing vent branch DFU table: {key}";
                return false;
            }

            if (dfu <= 0) return true;

            // Branch rows in vent tables usually just mapping diameter to max Dfu (length independent often, or handled otherwise)
            // If the user provided VentBranchRows, we use them.

            foreach (var entry in tables.BranchRows)
            {
                if (dfu <= entry.MaxDfu)
                {
                    diameter = entry.DiameterIn;
                    return true;
                }
            }

            warning = "Demand exceeds vent branch capacity in table.";
            return false;
        }

        /// <summary>
        /// Minimum vent stack or stack vent diameter (in) for a given total vented DFU and developed length (ft).
        /// </summary>
        public static bool TryVentStackMinDiameter(double ventedDfu, double developedLengthFt, string key, out double diameter, out string warning)
        {
             diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return false;
            }

            if (!VentDfuLengthTables.TryGetValue(key, out var tables) || tables.StackRows.Count == 0)
            {
                warning = $"Missing vent stack DFU table: {key}";
                return false;
            }

            if (ventedDfu <= 0) return true;

            double lengthFactor = developedLengthFt <= 100 ? 1.0 : developedLengthFt <= 200 ? 0.9 : 0.8;

            foreach (var entry in tables.StackRows)
            {
                if (ventedDfu <= entry.BaseMaxDfu * lengthFactor)
                {
                    diameter = entry.DiameterIn;
                    return true;
                }
            }

            warning = "Demand exceeds vent stack capacity in table.";
            return false;
        }

        public static bool TryVentStackAllowableFixtureUnits(double diameterIn, double developedLengthFt, string key, out double allowableDfu, out string warning)
        {
            allowableDfu = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return false;
            }

            if (!VentDfuLengthTables.TryGetValue(key, out var tables) || tables.StackRows.Count == 0)
            {
                warning = $"Missing vent stack DFU table: {key}";
                return false;
            }

            if (diameterIn <= 0) return true;

            double lengthFactor = developedLengthFt <= 100 ? 1.0 : developedLengthFt <= 200 ? 0.9 : 0.8;
            var match = tables.StackRows.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
            if (match == default)
            {
                warning = "Diameter not found in vent stack table.";
                return false;
            }

            allowableDfu = match.BaseMaxDfu * lengthFactor;
            return true;
        }
    }
}
