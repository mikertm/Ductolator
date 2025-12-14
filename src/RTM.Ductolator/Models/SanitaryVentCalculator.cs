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
        public static double MinBranchDiameterFromDfu(double dfu, double slopeFtPerFt, string key, out string warning)
        {
            double diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary branch DFU table key provided.";
                return 0;
            }

            if (!SanitaryBranchDfuTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (dfu <= 0) return 0;

            foreach (var entry in rows)
            {
                if (dfu <= entry.MaxDfu)
                {
                    diameter = entry.DiameterIn;
                    return diameter;
                }
            }

            warning = "Demand exceeds capacity in table.";
            return 0;
        }

        /// <summary>
        /// Reports the estimated DFU capacity for a horizontal fixture branch.
        /// </summary>
        public static double AllowableFixtureUnits(double diameterIn, double slopeFtPerFt, string key, out string warning)
        {
            double allowableDfu = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary branch DFU table key provided.";
                return 0;
            }

            if (!SanitaryBranchDfuTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (diameterIn <= 0) return 0;

            var match = rows.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
            if (match != default)
            {
                allowableDfu = match.MaxDfu;
                return allowableDfu;
            }

            warning = "Diameter not found in table.";
            return 0;
        }

        /// <summary>
        /// Minimum vent stack or stack vent diameter (in) for a given total vented DFU and developed length (ft).
        /// </summary>
        public static double VentStackMinDiameter(double ventedDfu, double developedLengthFt, string key, out string warning)
        {
            double diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return 0;
            }

            if (!VentDfuLengthTables.TryGetValue(key, out var tables) || tables.StackRows.Count == 0)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (ventedDfu <= 0) return 0;

            double lengthFactor = developedLengthFt <= 100 ? 1.0 : developedLengthFt <= 200 ? 0.9 : 0.8;

            foreach (var entry in tables.StackRows)
            {
                if (ventedDfu <= entry.BaseMaxDfu * lengthFactor)
                {
                    diameter = entry.DiameterIn;
                    return diameter;
                }
            }

            warning = "Demand exceeds vent stack capacity in table.";
            return 0;
        }

        public static double VentStackAllowableFixtureUnits(double diameterIn, double developedLengthFt, string key, out string warning)
        {
            double allowableDfu = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return 0;
            }

            if (!VentDfuLengthTables.TryGetValue(key, out var tables) || tables.StackRows.Count == 0)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (diameterIn <= 0) return 0;

            double lengthFactor = developedLengthFt <= 100 ? 1.0 : developedLengthFt <= 200 ? 0.9 : 0.8;
            var match = tables.StackRows.FirstOrDefault(e => Math.Abs(e.DiameterIn - diameterIn) < 1e-6);
            if (match == default)
            {
                warning = "Diameter not found in vent stack table.";
                return 0;
            }

            allowableDfu = match.BaseMaxDfu * lengthFactor;
            return allowableDfu;
        }
    }
}
