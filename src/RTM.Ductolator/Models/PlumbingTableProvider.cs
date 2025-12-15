using System;
using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Singleton provider for plumbing code tables (Sanitary, Vent, Storm, etc.).
    /// Loaded from external JSON/CSV to avoid embedded copyrights.
    /// </summary>
    public class PlumbingTableProvider
    {
        private static readonly PlumbingTableProvider _instance = new();
        public static PlumbingTableProvider Instance => _instance;

        private readonly Dictionary<string, ISanitaryBranchDfuTable> _sanitaryBranchTables = new();
        private readonly Dictionary<string, IVentStackTable> _ventStackTables = new();

        private PlumbingTableProvider() { }

        public void Clear()
        {
            _sanitaryBranchTables.Clear();
            _ventStackTables.Clear();
        }

        public void RegisterSanitaryBranchTable(ISanitaryBranchDfuTable table)
        {
            if (table != null && !string.IsNullOrWhiteSpace(table.Key))
            {
                _sanitaryBranchTables[table.Key] = table;
            }
        }

        public void RegisterVentStackTable(IVentStackTable table)
        {
            if (table != null && !string.IsNullOrWhiteSpace(table.Key))
            {
                _ventStackTables[table.Key] = table;
            }
        }

        public ISanitaryBranchDfuTable? GetSanitaryBranchTable(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _sanitaryBranchTables.TryGetValue(key, out var table) ? table : null;
        }

        public IVentStackTable? GetVentStackTable(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _ventStackTables.TryGetValue(key, out var table) ? table : null;
        }
    }

    // --- Concrete Implementations ---

    public class SimpleSanitaryBranchTable : ISanitaryBranchDfuTable
    {
        public string Key { get; }
        private readonly List<(double DiameterIn, double MaxDfu)> _rows;

        public SimpleSanitaryBranchTable(string key, IEnumerable<(double, double)> rows)
        {
            Key = key;
            _rows = rows.OrderBy(r => r.DiameterIn).ToList();
        }

        public double GetMaxDfu(double diameterIn)
        {
            var match = _rows.FirstOrDefault(r => Math.Abs(r.DiameterIn - diameterIn) < 1e-6);
            return match != default ? match.MaxDfu : 0;
        }

        public double GetMinDiameter(double dfu)
        {
            if (dfu <= 0) return 0;
            foreach (var row in _rows)
            {
                if (dfu <= row.MaxDfu) return row.DiameterIn;
            }
            return 0; // Exceeds capacity
        }
    }

    public class SimpleVentStackTable : IVentStackTable
    {
        public string Key { get; }
        private readonly List<(double DiameterIn, double BaseMaxDfu)> _rows;
        private readonly List<(double MaxLength, double Factor)> _lengthFactors;

        public SimpleVentStackTable(string key, IEnumerable<(double, double)> rows, IEnumerable<(double, double)>? lengthAdjustments = null)
        {
            Key = key;
            _rows = rows.OrderBy(r => r.DiameterIn).ToList();

            if (lengthAdjustments != null && lengthAdjustments.Any())
            {
                _lengthFactors = lengthAdjustments.OrderBy(f => f.Item1).ToList();
            }
            else
            {
                // Default to IPC-style logic if none provided, or just 1.0?
                // The previous implementation used 1.0/0.9/0.8 logic hardcoded.
                // To support "no copyrighted tables", we should probably default to 1.0 unless logic is generic.
                // However, the calculator logic in previous turn used 1.0/0.9/0.8.
                // Let's implement that as the default "generic" fallback if user provides NO adjustments but provides stack rows.
                _lengthFactors = new List<(double, double)>
                {
                    (100, 1.0),
                    (200, 0.9),
                    (double.MaxValue, 0.8)
                };
            }
        }

        public double GetMaxDfu(double diameterIn, double developedLengthFt)
        {
            var match = _rows.FirstOrDefault(r => Math.Abs(r.DiameterIn - diameterIn) < 1e-6);
            if (match == default) return 0;

            double factor = GetLengthFactor(developedLengthFt);
            return match.BaseMaxDfu * factor;
        }

        public double GetMinDiameter(double dfu, double developedLengthFt)
        {
            if (dfu <= 0) return 0;
            double factor = GetLengthFactor(developedLengthFt);

            foreach (var row in _rows)
            {
                if (dfu <= row.BaseMaxDfu * factor) return row.DiameterIn;
            }
            return 0;
        }

        private double GetLengthFactor(double length)
        {
            foreach (var (maxLength, factor) in _lengthFactors)
            {
                if (length <= maxLength) return factor;
            }
            return _lengthFactors.LastOrDefault().Factor;
        }
    }
}
