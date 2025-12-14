using System.Collections.Generic;

namespace RTM.Ductolator.Models
{
    public class PlumbingTablesRoot
    {
        public List<FixtureDemandCurveDto>? FixtureDemandCurves { get; set; }
        public List<SanitaryDfuTableDto>? SanitaryDfuTables { get; set; }
        public List<SanitaryBranchTableDto>? SanitaryBranchTables { get; set; }
        public List<VentTableDto>? VentTables { get; set; }
        public List<StormLeaderTableDto>? StormLeaderTables { get; set; }
        public List<GasMethodDto>? GasMethods { get; set; }
    }

    public class FixtureDemandCurveDto
    {
        public string? Key { get; set; }
        public List<DemandPointDto>? Points { get; set; }
    }

    public class DemandPointDto
    {
        public double Wsfu { get; set; }
        public double Gpm { get; set; }
    }

    public class SanitaryDfuTableDto
    {
        public string? Key { get; set; }
        public List<SanitaryRowDto>? Rows { get; set; }
    }

    public class SanitaryRowDto
    {
        public double DiameterIn { get; set; }
        public double SlopeFtPerFt { get; set; }
        public double MaxDfu { get; set; }
    }

    public class SanitaryBranchTableDto
    {
        public string? Key { get; set; }
        public List<SanitaryBranchRowDto>? Rows { get; set; }
    }

    public class SanitaryBranchRowDto
    {
        public double DiameterIn { get; set; }
        public double MaxDfu { get; set; }
    }

    public class VentTableDto
    {
        public string? Key { get; set; }
        public List<VentBranchRowDto>? BranchRows { get; set; }
        public List<VentStackRowDto>? StackRows { get; set; }
    }

    public class VentBranchRowDto
    {
        public double DiameterIn { get; set; }
        public double MaxDfu { get; set; }
    }

    public class VentStackRowDto
    {
        public double DiameterIn { get; set; }
        public double BaseMaxDfu { get; set; }
    }

    public class StormLeaderTableDto
    {
        public string? Key { get; set; }
        public List<StormLeaderRowDto>? Rows { get; set; }
    }

    public class StormLeaderRowDto
    {
        public double DiameterIn { get; set; }
        public double MaxGpm { get; set; }
    }

    public class GasMethodDto
    {
        public string? Key { get; set; }
        public string? Method { get; set; }
    }
}
