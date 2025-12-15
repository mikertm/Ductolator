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
        // Wrapper for legacy Register methods to populate the Provider
        public static void RegisterSanitaryBranchDfuTable(string key, List<(double DiameterIn, double MaxDfu)> rows)
        {
            if (string.IsNullOrWhiteSpace(key) || rows == null) return;
            var table = new SimpleSanitaryBranchTable(key, rows);
            PlumbingTableProvider.Instance.RegisterSanitaryBranchTable(table);
        }

        public static bool HasSanitaryBranchDfuTable(string key)
        {
            return PlumbingTableProvider.Instance.GetSanitaryBranchTable(key) != null;
        }

        public static void RegisterVentDfuLengthTable(
            string key,
            List<(double DiameterIn, double MaxDfu)>? branchRows,
            List<(double DiameterIn, double BaseMaxDfu)> stackRows,
            List<(double MaxLength, double Factor)>? lengthAdjustments = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (stackRows != null)
            {
                var table = new SimpleVentStackTable(key, stackRows, lengthAdjustments);
                PlumbingTableProvider.Instance.RegisterVentStackTable(table);
            }
        }

        public static bool HasVentDfuLengthTable(string key)
        {
            return PlumbingTableProvider.Instance.GetVentStackTable(key) != null;
        }

        /// <summary>
        /// Minimum nominal diameter (in) to carry the given sanitary DFU on a horizontal fixture branch.
        /// Uses registered table.
        /// </summary>
        public static double MinBranchDiameterFromDfu(double dfu, double slopeFtPerFt, string key, out string warning)
        {
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary branch DFU table key provided.";
                return 0;
            }

            var table = PlumbingTableProvider.Instance.GetSanitaryBranchTable(key);
            if (table == null)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (dfu <= 0) return 0;

            double dia = table.GetMinDiameter(dfu);
            if (dia <= 0)
            {
                warning = "Demand exceeds capacity in table.";
                return 0;
            }
            return dia;
        }

        /// <summary>
        /// Reports the estimated DFU capacity for a horizontal fixture branch.
        /// </summary>
        public static double AllowableFixtureUnits(double diameterIn, double slopeFtPerFt, string key, out string warning)
        {
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary branch DFU table key provided.";
                return 0;
            }

            var table = PlumbingTableProvider.Instance.GetSanitaryBranchTable(key);
            if (table == null)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (diameterIn <= 0) return 0;

            double maxDfu = table.GetMaxDfu(diameterIn);
            if (maxDfu <= 0)
            {
                warning = "Diameter not found in table.";
                return 0;
            }
            return maxDfu;
        }

        /// <summary>
        /// Minimum vent stack or stack vent diameter (in) for a given total vented DFU and developed length (ft).
        /// </summary>
        public static double VentStackMinDiameter(double ventedDfu, double developedLengthFt, string key, out string warning)
        {
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return 0;
            }

            var table = PlumbingTableProvider.Instance.GetVentStackTable(key);
            if (table == null)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (ventedDfu <= 0) return 0;

            double dia = table.GetMinDiameter(ventedDfu, developedLengthFt);
            if (dia <= 0)
            {
                warning = "Demand exceeds vent stack capacity in table.";
                return 0;
            }
            return dia;
        }

        public static double VentStackAllowableFixtureUnits(double diameterIn, double developedLengthFt, string key, out string warning)
        {
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No vent table key provided.";
                return 0;
            }

            var table = PlumbingTableProvider.Instance.GetVentStackTable(key);
            if (table == null)
            {
                warning = $"Missing table '{key}'. Load plumbing-code-tables.json in the catalog folder.";
                return 0;
            }

            if (diameterIn <= 0) return 0;

            double maxDfu = table.GetMaxDfu(diameterIn, developedLengthFt);
            if (maxDfu <= 0)
            {
                warning = "Diameter not found in vent stack table.";
                return 0;
            }
            return maxDfu;
        }
    }
}
