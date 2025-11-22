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

        public record PlumbingCodeProfile(
            string Region,
            double MaxColdFps,
            double MaxHotFps,
            string CodeBasis,
            string Notes)
        {
            public double GetMaxVelocity(bool isHot) => isHot ? MaxHotFps : MaxColdFps;
        }

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

        // Plumbing velocity practices by region (all rooted in UPC/IPC & ASPE limits).
        private static readonly Dictionary<string, PlumbingCodeProfile> PlumbingProfiles = new()
        {
            {
                "National (IPC/UPC)",
                new PlumbingCodeProfile(
                    Region: "National (IPC/UPC)",
                    MaxColdFps: 8.0,
                    MaxHotFps: 5.0,
                    CodeBasis: "IPC/UPC with ASPE velocity limits",
                    Notes: "Typical domestic water design limit of 8 fps cold / 5 fps hot"
                )
            },
            {
                "California (CPC)",
                new PlumbingCodeProfile(
                    Region: "California (CPC)",
                    MaxColdFps: 8.0,
                    MaxHotFps: 5.0,
                    CodeBasis: "California Plumbing Code (UPC derivative) and Title 24 hot-water limits",
                    Notes: "Title 24 hot-water circulation noise/erosion guidance keeps hot water at or below 5 fps"
                )
            },
            {
                "Chicago / Illinois", // Chicago Plumbing Code is conservative on velocities
                new PlumbingCodeProfile(
                    Region: "Chicago / Illinois",
                    MaxColdFps: 6.0,
                    MaxHotFps: 4.5,
                    CodeBasis: "Chicago Plumbing Code with reduced velocities for noise control",
                    Notes: "Chicago favors 6 fps cold and 4.5 fps hot to mitigate noise and erosion"
                )
            },
            {
                "Florida (IPC)",
                new PlumbingCodeProfile(
                    Region: "Florida (IPC)",
                    MaxColdFps: 8.0,
                    MaxHotFps: 5.0,
                    CodeBasis: "Florida Plumbing Code (IPC-based)",
                    Notes: "IPC velocity limits widely used across Florida jurisdictions"
                )
            },
            {
                "Texas (IPC)",
                new PlumbingCodeProfile(
                    Region: "Texas (IPC)",
                    MaxColdFps: 8.0,
                    MaxHotFps: 5.0,
                    CodeBasis: "Texas statewide IPC adoption",
                    Notes: "Standard IPC velocity caps unless local amendments are stricter"
                )
            }
        };

        public static IReadOnlyList<string> AllDuctRegions => DuctProfiles.Keys.ToList();
        public static IReadOnlyList<string> AllPlumbingRegions => PlumbingProfiles.Keys.ToList();

        public static DuctCodeProfile GetDuctProfile(string regionKey)
        {
            if (string.IsNullOrWhiteSpace(regionKey) || !DuctProfiles.TryGetValue(regionKey, out var profile))
                return DuctProfiles.First().Value;
            return profile;
        }

        public static PlumbingCodeProfile GetPlumbingProfile(string regionKey)
        {
            if (string.IsNullOrWhiteSpace(regionKey) || !PlumbingProfiles.TryGetValue(regionKey, out var profile))
                return PlumbingProfiles.First().Value;
            return profile;
        }
    }
}
