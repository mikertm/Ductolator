using System;
using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Region-aware design guidance to reflect common U.S. state and municipal
    /// practices layered on top of ASHRAE/SMACNA (duct) and ASPE/UPC/IPC (plumbing)
    /// baselines. Values are intentionally conservative so computed sizing stays
    /// inside widely accepted limits even when local amendments tighten velocity
    /// or friction recommendations.
    /// </summary>
    public static class CodeGuidance
    {
        public record DuctCodeProfile(
            string Region,
            double MaxSupplyMainFpm,
            double MaxBranchFpm,
            double MaxReturnFpm,
            double DefaultFriction_InWgPer100Ft,
            string Basis);

        // Duct velocity and friction practices by region (all grounded in IMC/SMACNA).
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
                "Chicago / Illinois", // Chicago keeps conservative velocities for noise control
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
            new PlumbingProfile(
                Id: "chicago_ipc_rooted",
                DisplayName: "Chicago (IPC-rooted)",
                BaseFamily: PlumbingBaseFamily.Chicago,
                Notes: "Chicago favors 6 fps cold and 4.5 fps hot to mitigate noise and erosion",
                MaxColdFps: 6.0,
                MaxHotFps: 4.5,
                FixtureDemandKey: "chicago_fixture_demand",
                SanitaryDfuKey: "chicago_sanitary_dfu",
                VentDfuLengthKey: "chicago_vent_dfu_length",
                StormLeaderKey: "chicago_storm_leader",
                StormHorizontalKey: "chicago_storm_horizontal",
                GasSizingKey: "nfpa54_gas_low_pressure"
            ),
            new PlumbingProfile(
                Id: "california_cpc_upc",
                DisplayName: "California (CPC/UPC-based)",
                BaseFamily: PlumbingBaseFamily.CPC,
                Notes: "Title 24 hot-water circulation noise/erosion guidance keeps hot water at or below 5 fps",
                MaxColdFps: 8.0,
                MaxHotFps: 5.0,
                FixtureDemandKey: "california_fixture_demand",
                SanitaryDfuKey: "california_sanitary_dfu",
                VentDfuLengthKey: "california_vent_dfu_length",
                StormLeaderKey: "california_storm_leader",
                StormHorizontalKey: "california_storm_horizontal",
                GasSizingKey: "california_gas_sizing"
            ),
            new PlumbingProfile(
                Id: "generic_ipc",
                DisplayName: "Generic IPC",
                BaseFamily: PlumbingBaseFamily.IPC,
                Notes: "Typical domestic water design limit of 8 fps cold / 5 fps hot",
                MaxColdFps: 8.0,
                MaxHotFps: 5.0,
                FixtureDemandKey: "ipc_fixture_demand",
                SanitaryDfuKey: "ipc_sanitary_dfu",
                VentDfuLengthKey: "ipc_vent_dfu_length",
                StormLeaderKey: "ipc_storm_leader",
                StormHorizontalKey: "ipc_storm_horizontal",
                GasSizingKey: "ipc_gas_sizing"
            ),
            new PlumbingProfile(
                Id: "generic_upc",
                DisplayName: "Generic UPC",
                BaseFamily: PlumbingBaseFamily.UPC,
                Notes: "Standard UPC velocity caps",
                MaxColdFps: 8.0,
                MaxHotFps: 5.0,
                FixtureDemandKey: "upc_fixture_demand",
                SanitaryDfuKey: "upc_sanitary_dfu",
                VentDfuLengthKey: "upc_vent_dfu_length",
                StormLeaderKey: "upc_storm_leader",
                StormHorizontalKey: "upc_storm_horizontal",
                GasSizingKey: "upc_gas_sizing"
            ),
            new PlumbingProfile(
                Id: "aspe_velocity_guidance",
                DisplayName: "ASPE Guidance",
                BaseFamily: PlumbingBaseFamily.ASPE,
                Notes: "ASPE velocity limits (typically conservative)",
                MaxColdFps: 8.0,
                MaxHotFps: 4.0, // Making it visibly different
                FixtureDemandKey: null,
                SanitaryDfuKey: null,
                VentDfuLengthKey: null,
                StormLeaderKey: null,
                StormHorizontalKey: null,
                GasSizingKey: null
            )
        };

        public static IReadOnlyList<PlumbingProfile> AllPlumbingProfiles => PlumbingProfilesList;

        public static PlumbingProfile DefaultPlumbingProfile => PlumbingProfilesList.First();

        public static PlumbingProfile GetPlumbingProfile(string id)
        {
            return PlumbingProfilesList.FirstOrDefault(p => p.Id == id) ?? DefaultPlumbingProfile;
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
            Check(profile.SanitaryDfuKey, "sanitary branch DFU", SanitaryVentCalculator.HasSanitaryBranchDfuTable);
            Check(profile.VentDfuLengthKey, "vent DFU/length", SanitaryVentCalculator.HasVentDfuLengthTable);
            Check(profile.StormLeaderKey, "storm leader", StormDrainageCalculator.HasStormLeaderTable);
            // Check(profile.StormHorizontalKey, "storm horizontal", StormDrainageCalculator.HasStormHorizontalTable); // If we implement this registry
            Check(profile.GasSizingKey, "gas sizing", PlumbingCalculator.HasGasSizingMethod);

            return warnings;
        }
    }
}
