using System;
using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    public static class CodeGuidance
    {
        public record DuctCodeProfile(
            string Region,
            double MaxSupplyMainFpm,
            double MaxBranchFpm,
            double MaxReturnFpm,
            double DefaultFriction_InWgPer100Ft,
            string Basis);

        private static readonly Dictionary<string, DuctCodeProfile> DuctProfiles = new()
        {
            {
                "National (IMC/SMACNA)",
                new DuctCodeProfile(
                    Region: "National (IMC/SMACNA)",
                    MaxSupplyMainFpm: 1800,
                    MaxBranchFpm: 1400,
                    MaxReturnFpm: 1200,
                    DefaultFriction_InWgPer100Ft: 0.08,
                    Basis: "ASHRAE/SMACNA equal-friction practice used by IMC states"
                )
            },
            {
                "California (Title 24 / CMC)",
                new DuctCodeProfile(
                    Region: "California (Title 24 / CMC)",
                    MaxSupplyMainFpm: 1600,
                    MaxBranchFpm: 1200,
                    MaxReturnFpm: 1000,
                    DefaultFriction_InWgPer100Ft: 0.08,
                    Basis: "California Mechanical Code with Title 24 noise/efficiency emphasis"
                )
            },
            {
                "Chicago / Illinois",
                new DuctCodeProfile(
                    Region: "Chicago / Illinois",
                    MaxSupplyMainFpm: 1500,
                    MaxBranchFpm: 1200,
                    MaxReturnFpm: 900,
                    DefaultFriction_InWgPer100Ft: 0.08,
                    Basis: "Chicago Mechanical Code amendments favor lower velocities"
                )
            },
            {
                "Florida (IMC)",
                new DuctCodeProfile(
                    Region: "Florida (IMC)",
                    MaxSupplyMainFpm: 1800,
                    MaxBranchFpm: 1400,
                    MaxReturnFpm: 1200,
                    DefaultFriction_InWgPer100Ft: 0.1,
                    Basis: "Florida Building Code Mechanical (IMC with humidity-focused allowances)"
                )
            },
            {
                "Texas (IMC)",
                new DuctCodeProfile(
                    Region: "Texas (IMC)",
                    MaxSupplyMainFpm: 1800,
                    MaxBranchFpm: 1400,
                    MaxReturnFpm: 1200,
                    DefaultFriction_InWgPer100Ft: 0.08,
                    Basis: "Texas IMC adoption with SMACNA velocity guidance"
                )
            }
        };

        public static IReadOnlyList<string> AllDuctRegions => DuctProfiles.Keys.ToList();

        public static DuctCodeProfile GetDuctProfile(string regionKey)
        {
            if (string.IsNullOrWhiteSpace(regionKey) || !DuctProfiles.TryGetValue(regionKey, out var profile))
                return DuctProfiles.First().Value;
            return profile;
        }

        // --- Plumbing Profiles ---

        private static readonly List<PlumbingProfile> PlumbingProfilesList = new()
        {
            new PlumbingProfile
            {
                Id = "chicago_ipc_rooted",
                DisplayName = "Chicago (IPC-rooted)",
                BaseFamily = PlumbingProfileFamily.Chicago,
                Notes = "Chicago favors 6 fps cold and 4.5 fps hot to mitigate noise and erosion",
                MaxColdFps = 6.0,
                MaxHotFps = 4.5,
                FixtureDemandKey = "chicago_fixture_demand",
                SanitaryDfuKey = "chicago_sanitary_dfu",
                VentSizingKey = "chicago_vent_stack_table",
                StormSizingKey = "chicago_storm_leader_table",
                GasSizingKey = "nfpa54_gas_low_pressure"
            },
            new PlumbingProfile
            {
                Id = "california_cpc_upc",
                DisplayName = "California (CPC/UPC-based)",
                BaseFamily = PlumbingProfileFamily.CPC,
                Notes = "Title 24 hot-water circulation noise/erosion guidance keeps hot water at or below 5 fps",
                MaxColdFps = 8.0,
                MaxHotFps = 5.0,
                FixtureDemandKey = "california_fixture_demand",
                SanitaryDfuKey = "california_sanitary_dfu",
                VentSizingKey = "california_vent_stack_table",
                StormSizingKey = "california_storm_leader_table",
                GasSizingKey = "california_gas_sizing"
            },
            new PlumbingProfile
            {
                Id = "generic_ipc",
                DisplayName = "Generic IPC",
                BaseFamily = PlumbingProfileFamily.IPC,
                Notes = "Typical domestic water design limit of 8 fps cold / 5 fps hot",
                MaxColdFps = 8.0,
                MaxHotFps = 5.0,
                FixtureDemandKey = "ipc_fixture_demand",
                SanitaryDfuKey = "ipc_sanitary_dfu",
                VentSizingKey = "ipc_vent_stack_table",
                StormSizingKey = "ipc_roof_leader_table",
                GasSizingKey = "ipc_gas_sizing"
            },
            new PlumbingProfile
            {
                Id = "generic_upc",
                DisplayName = "Generic UPC",
                BaseFamily = PlumbingProfileFamily.UPC,
                Notes = "Standard UPC velocity caps",
                MaxColdFps = 8.0,
                MaxHotFps = 5.0,
                FixtureDemandKey = "upc_fixture_demand",
                SanitaryDfuKey = "upc_sanitary_dfu",
                VentSizingKey = "upc_vent_stack_table",
                StormSizingKey = "upc_roof_leader_table",
                GasSizingKey = "upc_gas_sizing"
            },
            new PlumbingProfile
            {
                Id = "aspe_velocity_guidance",
                DisplayName = "ASPE Guidance",
                BaseFamily = PlumbingProfileFamily.ASPE,
                Notes = "ASPE velocity limits (typically conservative)",
                MaxColdFps = 8.0,
                MaxHotFps = 4.0,
                FixtureDemandKey = "hunter_curve_v1",
                SanitaryDfuKey = "aspe_sanitary_dfu",
                VentSizingKey = "aspe_vent_stack_table",
                StormSizingKey = "aspe_storm_leader_table",
                GasSizingKey = "nfpa54_ifgc_equation_v1"
            }
        };

        public static IReadOnlyList<PlumbingProfile> AllPlumbingProfiles => PlumbingProfilesList;

        public static PlumbingProfile DefaultPlumbingProfile => PlumbingProfilesList.First();

        public static PlumbingProfile GetPlumbingProfileById(string id)
        {
            return PlumbingProfilesList.FirstOrDefault(p => p.Id == id) ?? PlumbingProfilesList.FirstOrDefault(p => p.BaseFamily == PlumbingProfileFamily.IPC) ?? DefaultPlumbingProfile;
        }

        public static IReadOnlyList<string> ValidateProfile(PlumbingProfile profile)
        {
            var warnings = new List<string>();

            if (profile == null) return warnings;

            void Check(string key, string name, Func<string, bool> checkFunc)
            {
                if (!string.IsNullOrEmpty(key) && !checkFunc(key))
                {
                    warnings.Add($"Missing {name} table: {key}");
                }
            }

            Check(profile.FixtureDemandKey, "fixture demand curve", PlumbingCalculator.HasFixtureDemandCurve);
            Check(profile.SanitaryDfuKey, "sanitary DFU", PlumbingCalculator.HasSanitaryDfuTable);
            Check(profile.VentSizingKey, "vent sizing", SanitaryVentCalculator.HasVentDfuLengthTable);
            Check(profile.StormSizingKey, "storm leader", StormDrainageCalculator.HasStormLeaderTable);
            Check(profile.GasSizingKey, "gas sizing", PlumbingCalculator.HasGasSizingMethod);

            return warnings;
        }
    }
}
