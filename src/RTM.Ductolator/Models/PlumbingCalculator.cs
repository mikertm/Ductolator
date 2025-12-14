using System;
using System.Collections.Generic;
using System.Linq;
using static RTM.Ductolator.Models.DuctCalculator;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Plumbing (water) sizing helpers using U.S. practice.
    /// Implements Hazen-Williams head loss for domestic water at 60 °F
    /// and Darcy-Weisbach with Swamee-Jain friction for general fluids.
    /// </summary>
    public static class PlumbingCalculator
    {
        // === Water properties & constants (imperial) ===
        private const double WaterDensity_LbmPerFt3 = 62.4; // at ~60 °F
        private const double LbmPerSlug = 32.174;
        private const double GravitationalAcceleration_FtPerS2 = 32.174;
        private const double InPerFt = 12.0;
        private const double FtPer100Ft = 100.0;
        private const double GpmToCfs = 0.00222800926; // 1 gpm = 0.002228 ft³/s
        private const double CfsToGpm = 448.831;
        private const double BtuhPerGpmDeltaTF = 500.0; // water, 60 °F

        // Kinematic viscosity of water at 60 °F (ASHRAE/ASPE tables)
        private const double WaterNu60F_Ft2PerS = 1.13e-5;

        /// <summary>
        /// Supported fluids for plumbing calculations.
        /// </summary>
        public enum FluidType
        {
            Water,
            EthyleneGlycol,
            PropyleneGlycol
        }

        public readonly record struct FluidPropertyTableEntry(
            FluidType Fluid,
            double TemperatureF,
            double PercentGlycol,
            double DensityLbmPerFt3,
            double KinematicViscosityFt2PerS,
            double HazenWilliamsMultiplier,
            double RoughnessMultiplier);

        // Expanded temperature coverage (≈ASHRAE data) to reduce interpolation error and
        // allow more accurate correction across typical hydronic ranges.
        private static readonly IReadOnlyList<FluidPropertyTableEntry> FluidPropertyData = new List<FluidPropertyTableEntry>
        {
            // Water (reference)
            new(FluidType.Water, 60, 0, WaterDensity_LbmPerFt3, WaterNu60F_Ft2PerS, 1.0, 1.0),
            new(FluidType.Water, 100, 0, 62.0, 1.24e-5, 1.0, 1.0),

            // Ethylene glycol mixtures (approximate ASHRAE data)
            new(FluidType.EthyleneGlycol, 40, 30, 67.0, 3.80e-5, 0.84, 1.22),
            new(FluidType.EthyleneGlycol, 60, 30, 66.1, 2.30e-5, 0.85, 1.20),
            new(FluidType.EthyleneGlycol, 80, 30, 65.5, 1.75e-5, 0.86, 1.18),
            new(FluidType.EthyleneGlycol, 120, 30, 64.4, 1.40e-5, 0.88, 1.15),
            new(FluidType.EthyleneGlycol, 160, 30, 63.3, 1.15e-5, 0.89, 1.12),
            new(FluidType.EthyleneGlycol, 200, 30, 62.3, 1.00e-5, 0.90, 1.10),

            new(FluidType.EthyleneGlycol, 40, 40, 68.2, 4.80e-5, 0.80, 1.27),
            new(FluidType.EthyleneGlycol, 60, 40, 67.4, 3.20e-5, 0.82, 1.25),
            new(FluidType.EthyleneGlycol, 80, 40, 66.4, 2.20e-5, 0.83, 1.23),
            new(FluidType.EthyleneGlycol, 120, 40, 65.3, 1.60e-5, 0.84, 1.21),
            new(FluidType.EthyleneGlycol, 160, 40, 64.0, 1.30e-5, 0.86, 1.18),
            new(FluidType.EthyleneGlycol, 200, 40, 63.0, 1.15e-5, 0.87, 1.16),

            new(FluidType.EthyleneGlycol, 40, 50, 69.5, 6.80e-5, 0.74, 1.38),
            new(FluidType.EthyleneGlycol, 60, 50, 68.7, 4.10e-5, 0.75, 1.35),
            new(FluidType.EthyleneGlycol, 80, 50, 67.8, 3.00e-5, 0.76, 1.33),
            new(FluidType.EthyleneGlycol, 120, 50, 66.2, 2.10e-5, 0.78, 1.30),
            new(FluidType.EthyleneGlycol, 160, 50, 64.5, 1.60e-5, 0.79, 1.27),
            new(FluidType.EthyleneGlycol, 200, 50, 63.4, 1.35e-5, 0.80, 1.25),

            // Propylene glycol mixtures (approximate ASHRAE data)
            new(FluidType.PropyleneGlycol, 40, 30, 65.4, 3.60e-5, 0.84, 1.22),
            new(FluidType.PropyleneGlycol, 60, 30, 64.8, 2.70e-5, 0.85, 1.20),
            new(FluidType.PropyleneGlycol, 80, 30, 64.2, 2.10e-5, 0.86, 1.18),
            new(FluidType.PropyleneGlycol, 120, 30, 63.1, 1.60e-5, 0.88, 1.15),
            new(FluidType.PropyleneGlycol, 160, 30, 62.0, 1.25e-5, 0.89, 1.12),
            new(FluidType.PropyleneGlycol, 200, 30, 61.2, 1.05e-5, 0.90, 1.10),

            new(FluidType.PropyleneGlycol, 40, 40, 66.4, 4.80e-5, 0.80, 1.27),
            new(FluidType.PropyleneGlycol, 60, 40, 65.6, 3.40e-5, 0.82, 1.25),
            new(FluidType.PropyleneGlycol, 80, 40, 64.8, 2.60e-5, 0.83, 1.23),
            new(FluidType.PropyleneGlycol, 120, 40, 63.6, 1.90e-5, 0.84, 1.21),
            new(FluidType.PropyleneGlycol, 160, 40, 62.5, 1.55e-5, 0.86, 1.18),
            new(FluidType.PropyleneGlycol, 200, 40, 61.6, 1.35e-5, 0.87, 1.16),

            new(FluidType.PropyleneGlycol, 40, 50, 67.2, 6.80e-5, 0.74, 1.38),
            new(FluidType.PropyleneGlycol, 60, 50, 66.9, 5.00e-5, 0.75, 1.35),
            new(FluidType.PropyleneGlycol, 80, 50, 66.0, 3.40e-5, 0.76, 1.33),
            new(FluidType.PropyleneGlycol, 120, 50, 64.5, 2.50e-5, 0.78, 1.30),
            new(FluidType.PropyleneGlycol, 160, 50, 63.3, 1.90e-5, 0.79, 1.27),
            new(FluidType.PropyleneGlycol, 200, 50, 62.5, 1.60e-5, 0.80, 1.25)
        };

        public record FluidProperties(
            double DensityLbmPerFt3,
            double KinematicViscosityFt2PerS,
            double HazenWilliamsCFactorMultiplier,
            double RoughnessMultiplier);

        // Pipe materials are supplied at runtime via RuntimeCatalogs.

        public static double GetInnerDiameterIn(PipeMaterialProfile material, double nominalSizeIn)
        {
            if (material == null)
                return 0;

            return material.TryGetInnerDiameter(nominalSizeIn, out double id) ? id : 0;
        }

        public static IReadOnlyDictionary<double, double> GetAvailableNominalIds(PipeMaterialProfile material)
            => material?.NominalIdIn ?? new Dictionary<double, double>();

        public static MaterialHydraulics GetMaterialData(PipeMaterialProfile material) => material.Hydraulics;

        /// <summary>
        /// Convert a static head (ft) to psi for a given fluid density.
        /// </summary>
        public static double PsiPerFtHeadFromDensity(double densityLbmPerFt3) => densityLbmPerFt3 / 144.0;

        private static double Interpolate(double x0, double x1, double y0, double y1, double x)
        {
            if (Math.Abs(x1 - x0) < 1e-9) return y0;
            double t = (x - x0) / (x1 - x0);
            return y0 + t * (y1 - y0);
        }

        private static FluidPropertyTableEntry InterpolateAcrossTemperature(IEnumerable<FluidPropertyTableEntry> entries, double tempF)
        {
            var ordered = entries.OrderBy(e => e.TemperatureF).ToList();
            if (ordered.Count == 0) return new FluidPropertyTableEntry(FluidType.Water, tempF, 0, WaterDensity_LbmPerFt3, WaterNu60F_Ft2PerS, 1.0, 1.0);

            if (tempF <= ordered.First().TemperatureF) return ordered.First();
            if (tempF >= ordered.Last().TemperatureF) return ordered.Last();

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var lo = ordered[i];
                var hi = ordered[i + 1];
                if (tempF >= lo.TemperatureF && tempF <= hi.TemperatureF)
                {
                    return new FluidPropertyTableEntry(
                        lo.Fluid,
                        tempF,
                        lo.PercentGlycol,
                        Interpolate(lo.TemperatureF, hi.TemperatureF, lo.DensityLbmPerFt3, hi.DensityLbmPerFt3, tempF),
                        Interpolate(lo.TemperatureF, hi.TemperatureF, lo.KinematicViscosityFt2PerS, hi.KinematicViscosityFt2PerS, tempF),
                        Interpolate(lo.TemperatureF, hi.TemperatureF, lo.HazenWilliamsMultiplier, hi.HazenWilliamsMultiplier, tempF),
                        Interpolate(lo.TemperatureF, hi.TemperatureF, lo.RoughnessMultiplier, hi.RoughnessMultiplier, tempF));
                }
            }

            return ordered.Last();
        }

        /// <summary>
        /// Resolve fluid density/viscosity and roughness/C multipliers for the chosen fluid and glycol percentage.
        /// </summary>
        public static FluidProperties ResolveFluidProperties(FluidType fluid, double temperatureF, double percentGlycol)
        {
            double clampedPercent = Math.Max(0, Math.Min(60, percentGlycol));
            double clampedTemp = Math.Max(20, Math.Min(200, temperatureF <= 0 ? 60 : temperatureF));

            if (clampedPercent <= 0.0001)
            {
                var waterRows = FluidPropertyData.Where(e => e.Fluid == FluidType.Water);
                var water = InterpolateAcrossTemperature(waterRows, clampedTemp);
                return new FluidProperties(water.DensityLbmPerFt3, water.KinematicViscosityFt2PerS, 1.0, 1.0);
            }

            var table = FluidPropertyData.Where(e => e.Fluid == fluid).ToList();
            var percentLevels = table.Select(e => e.PercentGlycol).Distinct().OrderBy(p => p).ToList();

            if (percentLevels.Count == 0)
            {
                return new FluidProperties(WaterDensity_LbmPerFt3, WaterNu60F_Ft2PerS, 1.0, 1.0);
            }

            double lowerPercent = percentLevels.Where(p => p <= clampedPercent).DefaultIfEmpty(percentLevels.First()).Max();
            double upperPercent = percentLevels.Where(p => p >= clampedPercent).DefaultIfEmpty(percentLevels.Last()).Min();

            FluidPropertyTableEntry InterpForPercent(double percent)
            {
                var rows = table.Where(e => Math.Abs(e.PercentGlycol - percent) < 1e-6);
                return InterpolateAcrossTemperature(rows, clampedTemp);
            }

            var lower = InterpForPercent(lowerPercent);
            var upper = InterpForPercent(upperPercent);

            if (Math.Abs(upperPercent - lowerPercent) < 1e-9)
            {
                return new FluidProperties(lower.DensityLbmPerFt3, lower.KinematicViscosityFt2PerS, lower.HazenWilliamsMultiplier, lower.RoughnessMultiplier);
            }

            double density = Interpolate(lowerPercent, upperPercent, lower.DensityLbmPerFt3, upper.DensityLbmPerFt3, clampedPercent);
            double viscosity = Interpolate(lowerPercent, upperPercent, lower.KinematicViscosityFt2PerS, upper.KinematicViscosityFt2PerS, clampedPercent);
            double hazenMult = Interpolate(lowerPercent, upperPercent, lower.HazenWilliamsMultiplier, upper.HazenWilliamsMultiplier, clampedPercent);
            double roughMult = Interpolate(lowerPercent, upperPercent, lower.RoughnessMultiplier, upper.RoughnessMultiplier, clampedPercent);

            return new FluidProperties(density, viscosity, hazenMult, roughMult);
        }

        public static double AdjustedHazenWilliamsCFactor(double baseCFactor, FluidProperties? fluidProperties)
        {
            if (baseCFactor <= 0) return 0;
            double multiplier = fluidProperties?.HazenWilliamsCFactorMultiplier ?? 1.0;
            if (multiplier <= 0) multiplier = 1.0;
            return baseCFactor * multiplier;
        }

        public static double AdjustedRoughnessFt(double baseRoughnessFt, FluidProperties? fluidProperties)
        {
            if (baseRoughnessFt <= 0) return 0;
            double multiplier = fluidProperties?.RoughnessMultiplier ?? 1.0;
            if (multiplier <= 0) multiplier = 1.0;
            return baseRoughnessFt * multiplier;
        }

        /// <summary>
        /// Hybrid friction helper: uses Hazen-Williams when the Reynolds number is
        /// safely turbulent; falls back to Darcy-Weisbach when flow is laminar or transitional.
        /// Applies fluid-specific roughness/C multipliers so hot/cold glycol mixtures get
        /// appropriately derated.
        /// </summary>
        public static double HybridPsiPer100Ft(
            double gpm,
            double diameterIn,
            double baseHazenWilliamsCFactor,
            double baseAbsoluteRoughnessFt,
            FluidProperties? fluidProperties,
            out string governingMethod,
            double? psiPerFtHead = null,
            double laminarReCutoff = 2000)
        {
            governingMethod = "";
            if (gpm <= 0 || diameterIn <= 0 || baseHazenWilliamsCFactor <= 0 || baseAbsoluteRoughnessFt <= 0)
                return 0;

            var props = fluidProperties ?? new FluidProperties(WaterDensity_LbmPerFt3, WaterNu60F_Ft2PerS, 1.0, 1.0);
            double velocity = VelocityFpsFromGpm(gpm, diameterIn);
            double reynolds = Reynolds(velocity, diameterIn, props.KinematicViscosityFt2PerS);

            if (reynolds < laminarReCutoff)
            {
                governingMethod = "Darcy-Weisbach";
                double roughness = AdjustedRoughnessFt(baseAbsoluteRoughnessFt, props);
                return HeadLoss_Darcy_PsiPer100Ft(gpm, diameterIn, roughness, props.KinematicViscosityFt2PerS, psiPerFtHead);
            }

            governingMethod = "Hazen-Williams";
            double adjustedC = AdjustedHazenWilliamsCFactor(baseHazenWilliamsCFactor, props);
            return HazenWilliamsPsiPer100Ft(gpm, diameterIn, adjustedC, psiPerFtHead);
        }

        /// <summary>
        /// Fluid velocity (ft/s) from flow (gpm) and diameter (in).
        /// </summary>
        public static double VelocityFpsFromGpm(double gpm, double diameterIn)
        {
            if (diameterIn <= 0) return 0;

            double area = Area_Round_Ft2(diameterIn);
            double flowCfs = gpm * GpmToCfs;
            return area > 0 ? flowCfs / area : 0;
        }

        /// <summary>
        /// Flow in gpm from a target velocity (ft/s) and diameter (in).
        /// </summary>
        public static double GpmFromVelocityFps(double velocityFps, double diameterIn)
        {
            if (velocityFps <= 0 || diameterIn <= 0) return 0;

            double area = Area_Round_Ft2(diameterIn);
            return velocityFps * area / GpmToCfs;
        }

        /// <summary>
        /// Reynolds number for water based on velocity (ft/s) and diameter (in).
        /// Optional kinematic viscosity override allows temperature correction.
        /// </summary>
        public static double Reynolds(double velocityFps, double diameterIn, double? kinematicViscosityFt2PerS = null)
        {
            double dFt = diameterIn / InPerFt;
            if (velocityFps <= 0 || dFt <= 0) return 0;

            double nu = kinematicViscosityFt2PerS ?? WaterNu60F_Ft2PerS;
            if (nu <= 0) return 0;

            return velocityFps * dFt / nu;
        }

        // === Hazen-Williams (domestic water, 60 °F) ===

        /// <summary>
        /// Head loss (ft/100 ft) via Hazen-Williams with diameter in inches and flow in gpm.
        /// Uses standard form: h_f = 10.44 * Q^1.85 / (C^1.85 * d_in^4.87).
        /// This returns feet of head per 100 ft, which is then converted to PSI by HazenWilliamsPsiPer100Ft.
        /// </summary>
        public static double HazenWilliamsHeadLoss_FtPer100Ft(double gpm, double diameterIn, double cFactor)
        {
            if (gpm <= 0 || diameterIn <= 0 || cFactor <= 0) return 0;

            double numerator = 10.44 * Math.Pow(gpm, 1.85);
            double denominator = Math.Pow(cFactor, 1.85) * Math.Pow(diameterIn, 4.87);
            return numerator / denominator;
        }

        public static double HazenWilliamsPsiPer100Ft(double gpm, double diameterIn, double cFactor, double? psiPerFtHead = null)
        {
            double headFtPer100 = HazenWilliamsHeadLoss_FtPer100Ft(gpm, diameterIn, cFactor);
            double psiPerFt = psiPerFtHead ?? PsiPerFtHeadFromDensity(WaterDensity_LbmPerFt3);
            return headFtPer100 * psiPerFt;
        }

        /// <summary>
        /// Hazen-Williams head loss (psi) across a specific run length (ft).
        /// </summary>
        public static double HazenWilliamsPsi(double gpm, double diameterIn, double cFactor, double lengthFt, double? psiPerFtHead = null)
        {
            if (lengthFt <= 0) return 0;

            double psiPer100 = HazenWilliamsPsiPer100Ft(gpm, diameterIn, cFactor, psiPerFtHead);
            return psiPer100 * (lengthFt / FtPer100Ft);
        }

        /// <summary>
        /// Solve flow (gpm) for a target Hazen-Williams loss (psi/100 ft).
        /// </summary>
        public static double SolveFlowFromHazenWilliams(double diameterIn, double targetPsiPer100Ft, double cFactor, double? psiPerFtHead = null)
        {
            if (diameterIn <= 0 || targetPsiPer100Ft <= 0 || cFactor <= 0) return 0;

            double psiPerFt = psiPerFtHead ?? PsiPerFtHeadFromDensity(WaterDensity_LbmPerFt3);
            double targetHead = targetPsiPer100Ft / psiPerFt;
            double lo = 0.01;
            double hi = 5000.0;

            double Fn(double flowGpm)
            {
                double head = HazenWilliamsHeadLoss_FtPer100Ft(flowGpm, diameterIn, cFactor);
                return head - targetHead;
            }

            double fLo = Fn(lo);
            double fHi = Fn(hi);

            for (int i = 0; i < 40 && fLo * fHi > 0; i++)
            {
                lo = Math.Max(0.001, lo / 2.0);
                hi *= 2.0;
                fLo = Fn(lo);
                fHi = Fn(hi);
            }

            for (int i = 0; i < 100; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = Fn(mid);

                if (Math.Abs(fMid) < 1e-4)
                    return mid;

                if (fLo * fMid < 0)
                {
                    hi = mid;
                    fHi = fMid;
                }
                else
                {
                    lo = mid;
                    fLo = fMid;
                }
            }

            return 0.5 * (lo + hi);
        }

        /// <summary>
        /// Solve pipe diameter (in) for a target Hazen-Williams loss (psi/100 ft).
        /// </summary>
        public static double SolveDiameterFromHazenWilliams(double gpm, double targetPsiPer100Ft, double cFactor, double? psiPerFtHead = null)
        {
            if (gpm <= 0 || targetPsiPer100Ft <= 0 || cFactor <= 0) return 0;

            double psiPerFt = psiPerFtHead ?? PsiPerFtHeadFromDensity(WaterDensity_LbmPerFt3);
            double targetHead = targetPsiPer100Ft / psiPerFt;

            double lo = 0.25;  // in
            double hi = 48.0;  // in

            double Fn(double dIn)
            {
                double head = HazenWilliamsHeadLoss_FtPer100Ft(gpm, dIn, cFactor);
                return head - targetHead;
            }

            double fLo = Fn(lo);
            double fHi = Fn(hi);

            for (int i = 0; i < 40 && fLo * fHi > 0; i++)
            {
                lo = Math.Max(0.125, lo / 1.5);
                hi *= 1.5;
                fLo = Fn(lo);
                fHi = Fn(hi);
            }

            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = Fn(mid);

                if (Math.Abs(fMid) < 1e-4)
                    return mid;

                if (fLo * fMid < 0)
                {
                    hi = mid;
                    fHi = fMid;
                }
                else
                {
                    lo = mid;
                    fLo = fMid;
                }
            }

            return 0.5 * (lo + hi);
        }

        // === Darcy-Weisbach for general liquids ===

        /// <summary>
        /// Swamee-Jain friction factor (Darcy) for liquids using supplied roughness.
        /// </summary>
        public static double FrictionFactor(double reynolds, double diameterIn, double roughnessFt)
        {
            if (reynolds <= 0 || diameterIn <= 0 || roughnessFt < 0) return 0;

            double dFt = diameterIn / InPerFt;
            if (reynolds < 2000)
                return 64.0 / reynolds;

            double term = (roughnessFt / (3.7 * dFt)) + 5.74 / Math.Pow(reynolds, 0.9);
            return 0.25 / Math.Pow(Math.Log10(term), 2.0);
        }

        /// <summary>
        /// Darcy-Weisbach head loss (ft/100 ft) for a liquid stream.
        /// </summary>
        public static double HeadLoss_Darcy_FtPer100Ft(double gpm, double diameterIn, double roughnessFt, double? kinematicViscosityFt2PerS = null)
        {
            if (gpm <= 0 || diameterIn <= 0) return 0;

            double velocityFps = VelocityFpsFromGpm(gpm, diameterIn);
            double re = Reynolds(velocityFps, diameterIn, kinematicViscosityFt2PerS);
            if (re <= 0) return 0;

            double f = FrictionFactor(re, diameterIn, roughnessFt);
            if (f <= 0) return 0;

            double dFt = diameterIn / InPerFt;
            double headPer100Ft = f * (FtPer100Ft / dFt) * (velocityFps * velocityFps) / (2.0 * GravitationalAcceleration_FtPerS2);
            return headPer100Ft;
        }

        public static double HeadLoss_Darcy_PsiPer100Ft(double gpm, double diameterIn, double roughnessFt, double? kinematicViscosityFt2PerS = null, double? psiPerFtHead = null)
        {
            double headFt = HeadLoss_Darcy_FtPer100Ft(gpm, diameterIn, roughnessFt, kinematicViscosityFt2PerS);
            double psiPerFt = psiPerFtHead ?? PsiPerFtHeadFromDensity(WaterDensity_LbmPerFt3);
            return headFt * psiPerFt;
        }

        /// <summary>
        /// Darcy-Weisbach pressure drop (psi) over an arbitrary length (ft).
        /// </summary>
        public static double HeadLoss_Darcy_Psi(double gpm, double diameterIn, double roughnessFt, double lengthFt, double? kinematicViscosityFt2PerS = null, double? psiPerFtHead = null)
        {
            if (lengthFt <= 0) return 0;

            double psiPer100 = HeadLoss_Darcy_PsiPer100Ft(gpm, diameterIn, roughnessFt, kinematicViscosityFt2PerS, psiPerFtHead);
            return psiPer100 * (lengthFt / FtPer100Ft);
        }

        /// <summary>
        /// Minor (fitting) loss using loss coefficient K: ΔP = K * (ρ V² / 2) / 144 to psi.
        /// </summary>
        public static double MinorLossPsi(double velocityFps, double kCoefficient, double? fluidDensityLbmPerFt3 = null)
        {
            if (velocityFps <= 0 || kCoefficient < 0) return 0;

            double density = fluidDensityLbmPerFt3 ?? WaterDensity_LbmPerFt3;
            double velocityPressure_LbPerFt2 = density / LbmPerSlug * velocityFps * velocityFps / 2.0;
            double deltaP_LbPerFt2 = kCoefficient * velocityPressure_LbPerFt2;
            return deltaP_LbPerFt2 / 144.0; // 1 psi = 144 lb/ft²
        }

        /// <summary>
        /// Sum straight-run and fitting pressure losses (psi) using Hazen-Williams for friction and K for fittings.
        /// Straight segments use the supplied C factor; fittings compute minor loss from velocity per fitting.
        /// </summary>
        public static double TotalPressureDropPsi(
            IEnumerable<(double LengthFt, double DiameterIn, double FlowGpm)> straightSegments,
            double hazenWilliamsCFactor,
            IEnumerable<(double KCoefficient, double DiameterIn, double FlowGpm)> fittingLosses)
        {
            double totalPsi = 0;

            if (straightSegments != null)
            {
                foreach (var seg in straightSegments)
                {
                    if (seg.LengthFt <= 0 || seg.DiameterIn <= 0 || seg.FlowGpm <= 0) continue;
                    totalPsi += HazenWilliamsPsi(seg.FlowGpm, seg.DiameterIn, hazenWilliamsCFactor, seg.LengthFt);
                }
            }

            if (fittingLosses != null)
            {
                foreach (var fit in fittingLosses)
                {
                    if (fit.DiameterIn <= 0 || fit.FlowGpm <= 0 || fit.KCoefficient < 0) continue;
                    double velocity = VelocityFpsFromGpm(fit.FlowGpm, fit.DiameterIn);
                    totalPsi += MinorLossPsi(velocity, fit.KCoefficient);
                }
            }

            return totalPsi;
        }

        /// <summary>
        /// Convert a fitting K to equivalent length (ft) for use with Hazen-Williams/Darcy friction calculations.
        /// Leq = K * (D / 4f). Caller must supply friction factor f (Darcy) consistent with the governing method.
        /// </summary>
        public static double EquivalentLengthFromK(double kCoefficient, double diameterIn, double frictionFactor)
        {
            if (kCoefficient < 0 || diameterIn <= 0 || frictionFactor <= 0) return 0;

            double dFt = diameterIn / InPerFt;
            return kCoefficient * (dFt / (4.0 * frictionFactor));
        }

        /// <summary>
        /// Convenience helper: check if velocity is within common design guidance
        /// based on material and hot/cold service.
        /// </summary>
        public static bool IsVelocityWithinGuidance(double velocityFps, PipeMaterialProfile material, bool isHotWater)
        {
            if (velocityFps <= 0 || material == null) return false;
            var data = GetMaterialData(material);
            double limit = isHotWater ? data.MaxHotFps : data.MaxColdFps;
            return velocityFps <= limit;
        }

        // === Fixture unit / drainage helpers (Table Registries) ===

        private static readonly Dictionary<string, List<(double FixtureUnits, double DemandGpm)>> FixtureDemandCurves = new();

        private static readonly Dictionary<string, List<(double DiameterIn, double SlopeFtPerFt, double MaxDfu)>> SanitaryDfuTables = new();

        private static readonly Dictionary<string, bool> GasSizingMethods = new();

        public static void RegisterFixtureDemandCurve(string key, List<(double FixtureUnits, double DemandGpm)> points)
        {
            if (string.IsNullOrWhiteSpace(key) || points == null) return;
            FixtureDemandCurves[key] = points.OrderBy(p => p.FixtureUnits).ToList();
        }

        public static bool HasFixtureDemandCurve(string key) => !string.IsNullOrWhiteSpace(key) && FixtureDemandCurves.ContainsKey(key);

        public static void RegisterSanitaryDfuTable(string key, List<(double DiameterIn, double SlopeFtPerFt, double MaxDfu)> rows)
        {
            if (string.IsNullOrWhiteSpace(key) || rows == null) return;
            SanitaryDfuTables[key] = rows;
        }

        public static bool HasSanitaryDfuTable(string key) => !string.IsNullOrWhiteSpace(key) && SanitaryDfuTables.ContainsKey(key);

        public static void RegisterGasSizingMethod(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            GasSizingMethods[key] = true;
        }

        public static bool HasGasSizingMethod(string key) => !string.IsNullOrWhiteSpace(key) && GasSizingMethods.ContainsKey(key);

        /// <summary>
        /// Convert total fixture units to probable peak demand (gpm) using the registered Hunter curve.
        /// Interpolates log-log between points.
        /// </summary>
        public static bool TryHunterDemandGpm(double totalFixtureUnits, string key, out double gpm, out string warning)
        {
            gpm = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No fixture demand curve key provided.";
                return false;
            }

            if (!FixtureDemandCurves.TryGetValue(key, out var points) || points.Count == 0)
            {
                warning = $"Missing fixture demand curve: {key}";
                return false;
            }

            if (totalFixtureUnits <= 0)
            {
                gpm = 0;
                return true;
            }

            if (totalFixtureUnits <= points[0].FixtureUnits)
            {
                gpm = points[0].DemandGpm * totalFixtureUnits / points[0].FixtureUnits;
                return true;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                if (totalFixtureUnits >= a.FixtureUnits && totalFixtureUnits <= b.FixtureUnits)
                {
                    double logFu = Math.Log10(totalFixtureUnits);
                    double logA = Math.Log10(a.FixtureUnits);
                    double logB = Math.Log10(b.FixtureUnits);
                    double t = (logFu - logA) / (logB - logA);

                    double logQa = Math.Log10(a.DemandGpm);
                    double logQb = Math.Log10(b.DemandGpm);
                    double logQ = logQa + t * (logQb - logQa);
                    gpm = Math.Pow(10, logQ);
                    return true;
                }
            }

            // Extrapolate
            var last = points[^1];
            var prev = points[^2];
            double lastSlope = (Math.Log10(last.DemandGpm) - Math.Log10(prev.DemandGpm)) /
                               (Math.Log10(last.FixtureUnits) - Math.Log10(prev.FixtureUnits));
            double extrapolated = Math.Log10(last.DemandGpm) +
                                  lastSlope * (Math.Log10(totalFixtureUnits) - Math.Log10(last.FixtureUnits));
            gpm = Math.Pow(10, extrapolated);
            return true;
        }


        /// <summary>
        /// Minimum nominal diameter (in) to carry the given sanitary drainage fixture units.
        /// Uses the registered table keyed by 'key'.
        /// Matches slope if relevant (for Building Drain).
        /// If the table has rows with slope > 0, we treat it as Building Drain table.
        /// If rows have slope=0 (or ignored), we treat it as Horizontal Branch table (but this function is seemingly for building drain).
        /// The instruction says: "Sanitary drainage calculations must distinguish between 'Horizontal Fixture Branch' and 'Building Drain' capacities as per IPC Table 710.1(2)."
        ///
        /// This method `TryMinBuildingDrainNominalDia` implies Building Drain.
        /// </summary>
        public static bool TryMinBuildingDrainNominalDia(double dfu, double slopeFtPerFt, string key, out double diameter, out string warning)
        {
            diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No sanitary DFU table key provided.";
                return false;
            }

            if (!SanitaryDfuTables.TryGetValue(key, out var rows) || rows.Count == 0)
            {
                warning = $"Missing sanitary DFU table: {key}";
                return false;
            }

            if (dfu <= 0) return true;
            if (slopeFtPerFt <= 0)
            {
                // slope required for building drain sizing usually
                // but let's check if we can find anything with 0 slope (unlikely for building drain)
            }

            // Find nearest slope group in table
            // Group rows by slope
            var groups = rows.GroupBy(r => r.SlopeFtPerFt).ToList();

            // Filter to find best matching slope (closest?)
            // Usually we pick the specific slope or next lower capacity? No, slope determines capacity.
            // If we have 1/8 and 1/4, and user enters 0.02 (1/4), we pick that.
            // If user enters 0.015, we pick closest? Or maybe we should require match?
            // "nearest match" logic from previous implementation:

            var slopeGroups = groups.Select(g => g.Key).ToList();
            double closestSlope = 0;
            double minDelta = double.MaxValue;
            foreach (var s in slopeGroups)
            {
                double delta = Math.Abs(s - slopeFtPerFt);
                if (delta < minDelta)
                {
                    minDelta = delta;
                    closestSlope = s;
                }
            }

            // Check if closest is reasonable threshold? Say within 10%?
            // Existing logic was just nearest.

            var relevantRows = rows.Where(r => Math.Abs(r.SlopeFtPerFt - closestSlope) < 1e-6).OrderBy(r => r.DiameterIn).ToList();

            foreach (var entry in relevantRows)
            {
                if (dfu <= entry.MaxDfu)
                {
                    diameter = entry.DiameterIn;
                    return true;
                }
            }

            warning = "Demand exceeds capacity in table for closest slope.";
            return false;
        }

        // === Low-pressure natural gas sizing (IFGC/NFPA 54 style) ===

        private const double InWcPerPsi = 27.7076;
        private const double GasHeatingValue_BtuPerScf = 1000.0; // typical pipeline gas

        /// <summary>
        /// IFGC/NFPA 54 low-pressure gas formula (Equation 4-1).
        /// </summary>
        public static double GasFlow_Scfh(double diameterIn, double lengthFt, double pressureDropInWc,
                                          double specificGravity = 0.6, double basePressurePsi = 0.5)
        {
            if (diameterIn <= 0 || lengthFt <= 0 || pressureDropInWc <= 0 || specificGravity <= 0) return 0;

            // IFGC Equation 4-1: Q = 1.316 * sqrt(ΔH * D^5 / (Cr * L))
            // Cr for natural gas (SG=0.60) is 0.6094
            // For other specific gravities: Cr = 0.6094 * (SG / 0.60)
            double cr = 0.6094 * (specificGravity / 0.60);
            double d5 = Math.Pow(diameterIn, 5.0);
            double term = (pressureDropInWc * d5) / (cr * lengthFt);
            
            return 1.316 * Math.Sqrt(term);
        }

        public static double GasFlow_Mbh(double diameterIn, double lengthFt, double pressureDropInWc,
                                         double specificGravity = 0.6, double basePressurePsi = 0.5,
                                         double heatingValueBtuPerScf = GasHeatingValue_BtuPerScf)
        {
            double scfh = GasFlow_Scfh(diameterIn, lengthFt, pressureDropInWc, specificGravity, basePressurePsi);
            return scfh * heatingValueBtuPerScf / 1000.0;
        }

        /// <summary>
        /// Solve minimum diameter (in) for a target gas load (MBH) given run length and allowable ΔP (in.w.c.).
        /// Routes through method key check.
        /// </summary>
        public static bool TryGasDiameter(double loadMbh, double lengthFt, double pressureDropInWc,
                                                     string key,
                                                     out double diameter,
                                                     out string warning,
                                                     double specificGravity = 0.6, double basePressurePsi = 0.5,
                                                     double heatingValueBtuPerScf = GasHeatingValue_BtuPerScf,
                                                     double minDiameterIn = 0.5, double maxDiameterIn = 6.0)
        {
            diameter = 0;
            warning = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                warning = "No gas sizing method key provided.";
                return false;
            }

            if (!GasSizingMethods.ContainsKey(key))
            {
                warning = $"Unknown or missing gas sizing method: {key}";
                return false;
            }

            if (loadMbh <= 0 || lengthFt <= 0 || pressureDropInWc <= 0)
            {
                diameter = 0;
                return true;
            }

            double targetScfh = loadMbh * 1000.0 / heatingValueBtuPerScf;

            double Fn(double dIn) => GasFlow_Scfh(dIn, lengthFt, pressureDropInWc, specificGravity, basePressurePsi) - targetScfh;

            double lo = minDiameterIn;
            double hi = maxDiameterIn;
            double fLo = Fn(lo);
            double fHi = Fn(hi);

            for (int i = 0; i < 50; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = Fn(mid);

                if (Math.Abs(fMid) < 1e-3)
                {
                    diameter = mid;
                    return true;
                }

                if (fLo * fMid > 0)
                {
                    lo = mid;
                    fLo = fMid;
                }
                else
                {
                    hi = mid;
                    fHi = fMid;
                }
            }

            diameter = 0.5 * (lo + hi);
            return true;
        }

        /// <summary>
        /// Gas velocity (ft/s) from flow (scfh) and diameter (in) at standard conditions.
        /// </summary>
        public static double GasVelocityFps(double flowScfh, double diameterIn)
        {
            if (flowScfh <= 0 || diameterIn <= 0) return 0;
            double flowCfs = flowScfh / 3600.0;
            double area = Area_Round_Ft2(diameterIn);
            return area > 0 ? flowCfs / area : 0;
        }

        // === Domestic hot-water recirculation ===

        public static double RecirculationFlowFromVolume(double loopVolumeGallons, double turnoverMinutes)
        {
            if (loopVolumeGallons <= 0 || turnoverMinutes <= 0) return 0;
            return loopVolumeGallons / turnoverMinutes;
        }

        public static double RecirculationFlowFromHeatLoss(double totalHeatLossBtuh, double allowableDeltaTF)
        {
            if (totalHeatLossBtuh <= 0 || allowableDeltaTF <= 0) return 0;
            return totalHeatLossBtuh / (BtuhPerGpmDeltaTF * allowableDeltaTF);
        }

        public static double RecirculationHeadFt(double gpm, double diameterIn, double cFactor, double straightLengthFt, double fittingEquivalentLengthFt, double? psiPerFtHead = null)
        {
            double totalLength = Math.Max(0, straightLengthFt) + Math.Max(0, fittingEquivalentLengthFt);
            double psi = HazenWilliamsPsi(gpm, diameterIn, cFactor, totalLength, psiPerFtHead);
            double psiPerFt = psiPerFtHead ?? PsiPerFtHeadFromDensity(WaterDensity_LbmPerFt3);
            return psi / psiPerFt;
        }

        // === Water hammer ===

        public static double GetWaveSpeedFps(PipeMaterialProfile material)
        {
            return material?.WaveSpeedFps > 0 ? material.WaveSpeedFps : 4000.0;
        }

        public static double SurgePressurePsi(double velocityChangeFps, PipeMaterialProfile material)
        {
            if (velocityChangeFps <= 0) return 0;

            double a = GetWaveSpeedFps(material);
            double densitySlugPerFt3 = WaterDensity_LbmPerFt3 / LbmPerSlug;
            double deltaPLbPerFt2 = densitySlugPerFt3 * a * velocityChangeFps;
            return deltaPLbPerFt2 / 144.0;
        }

        public static double SurgePressureWithClosure(double flowVelocityFps, double pipeLengthFt, double closureTimeSeconds, PipeMaterialProfile material)
        {
            if (flowVelocityFps <= 0) return 0;

            double a = GetWaveSpeedFps(material);
            if (closureTimeSeconds > 0 && pipeLengthFt > 0)
            {
                double wavePeriod = 2.0 * pipeLengthFt / a;
                double scaling = Math.Min(1.0, wavePeriod / closureTimeSeconds);
                return SurgePressurePsi(flowVelocityFps * scaling, material);
            }

            return SurgePressurePsi(flowVelocityFps, material);
        }
    }
}
