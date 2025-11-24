using System;
using System.Collections.Generic;
using System.Linq;

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

        public record FluidProperties(
            double DensityLbmPerFt3,
            double KinematicViscosityFt2PerS,
            double HazenWilliamsCFactorMultiplier,
            double RoughnessMultiplier);

        /// <summary>
        /// Nominal pipe families that share internal diameter tables and C/ε defaults.
        /// </summary>
        public enum PipeMaterial
        {
            CopperTypeL,
            CopperTypeK,
            CopperTypeM,
            SteelSchedule40,
            PvcSchedule40,
            CpvcSchedule80,
            PexTubing
        }

        /// <summary>
        /// Collates roughness, Hazen-Williams C (new/aged), and velocity guidance.
        /// Values follow ASHRAE/ASPE/IPC tables for common domestic water materials.
        /// </summary>
        public record MaterialHydraulics(double RoughnessFt, double C_New, double C_Aged, double MaxColdFps, double MaxHotFps);

        // Reference C factors and roughness for typical U.S. plumbing materials (ASPE/ASME B31).
        private static readonly Dictionary<PipeMaterial, MaterialHydraulics> MaterialData = new()
        {
            // C factors per ASHRAE/ASPE; max velocities from common design guidance (UPC/IPC, ASPE Data Book)
            { PipeMaterial.CopperTypeK, new MaterialHydraulics(1.5e-6, 150, 135, 8.0, 5.0) },
            { PipeMaterial.CopperTypeL, new MaterialHydraulics(1.5e-6, 150, 135, 8.0, 5.0) },
            { PipeMaterial.CopperTypeM, new MaterialHydraulics(1.5e-6, 150, 130, 8.0, 5.0) },
            { PipeMaterial.SteelSchedule40, new MaterialHydraulics(0.00015, 120, 100, 6.0, 5.0) },
            { PipeMaterial.PvcSchedule40, new MaterialHydraulics(0.000005, 150, 140, 10.0, 8.0) },
            { PipeMaterial.CpvcSchedule80, new MaterialHydraulics(0.000005, 150, 140, 8.0, 8.0) },
            { PipeMaterial.PexTubing, new MaterialHydraulics(0.0001, 150, 140, 8.0, 5.0) }
        };

        private static readonly Dictionary<PipeMaterial, double> WaveSpeed_FtPerS = new()
        {
            { PipeMaterial.CopperTypeK, 4000 },
            { PipeMaterial.CopperTypeL, 4000 },
            { PipeMaterial.CopperTypeM, 4000 },
            { PipeMaterial.SteelSchedule40, 4000 },
            { PipeMaterial.PvcSchedule40, 1400 },
            { PipeMaterial.CpvcSchedule80, 1500 },
            { PipeMaterial.PexTubing, 1500 }
        };

        /// <summary>
        /// Internal diameters (in) by nominal size for select materials.
        /// Values are typical manufacturers' data (ASTM B88, ASTM D1785/D2846, ASTM F876/877).
        /// </summary>
        private static readonly Dictionary<PipeMaterial, Dictionary<double, double>> NominalToIdIn = new()
        {
            {
                PipeMaterial.CopperTypeL,
                new Dictionary<double, double>
                {
                    { 0.5, 0.545 },
                    { 0.75, 0.785 },
                    { 1.0, 1.025 },
                    { 1.25, 1.265 },
                    { 1.5, 1.505 },
                    { 2.0, 1.949 },
                    { 2.5, 2.445 },
                    { 3.0, 2.907 },
                    { 4.0, 3.826 }
                }
            },
            {
                PipeMaterial.CopperTypeK,
                new Dictionary<double, double>
                {
                    { 0.5, 0.527 },
                    { 0.75, 0.745 },
                    { 1.0, 0.995 },
                    { 1.25, 1.235 },
                    { 1.5, 1.473 },
                    { 2.0, 1.913 },
                    { 2.5, 2.401 },
                    { 3.0, 2.889 },
                    { 4.0, 3.826 }
                }
            },
            {
                PipeMaterial.CopperTypeM,
                new Dictionary<double, double>
                {
                    { 0.5, 0.569 },
                    { 0.75, 0.811 },
                    { 1.0, 1.055 },
                    { 1.25, 1.291 },
                    { 1.5, 1.527 },
                    { 2.0, 1.959 },
                    { 2.5, 2.445 },
                    { 3.0, 2.907 },
                    { 4.0, 3.826 }
                }
            },
            {
                PipeMaterial.SteelSchedule40,
                new Dictionary<double, double>
                {
                    { 0.5, 0.622 },
                    { 0.75, 0.824 },
                    { 1.0, 1.049 },
                    { 1.25, 1.380 },
                    { 1.5, 1.610 },
                    { 2.0, 2.067 },
                    { 2.5, 2.469 },
                    { 3.0, 3.068 },
                    { 4.0, 4.026 }
                }
            },
            {
                PipeMaterial.PvcSchedule40,
                new Dictionary<double, double>
                {
                    { 0.5, 0.602 },
                    { 0.75, 0.804 },
                    { 1.0, 1.029 },
                    { 1.25, 1.360 },
                    { 1.5, 1.590 },
                    { 2.0, 2.047 },
                    { 2.5, 2.445 },
                    { 3.0, 3.042 },
                    { 4.0, 4.026 }
                }
            },
            {
                PipeMaterial.CpvcSchedule80,
                new Dictionary<double, double>
                {
                    { 0.5, 0.526 },
                    { 0.75, 0.722 },
                    { 1.0, 0.936 },
                    { 1.25, 1.255 },
                    { 1.5, 1.476 },
                    { 2.0, 1.913 },
                    { 2.5, 2.290 },
                    { 3.0, 2.864 },
                    { 4.0, 3.786 }
                }
            },
            {
                PipeMaterial.PexTubing,
                new Dictionary<double, double>
                {
                    { 0.5, 0.475 },
                    { 0.75, 0.671 },
                    { 1.0, 0.875 },
                    { 1.25, 1.054 },
                    { 1.5, 1.220 },
                    { 2.0, 1.572 }
                }
            }
        };

        // Typical absolute roughness (ft) values for U.S. plumbing materials
        public const double Roughness_Copper_Ft = 1.5e-6;     // Type L copper (new)
        public const double Roughness_Steel_Ft = 0.00015;     // Schedule 40 steel (aged)
        public const double Roughness_PVC_Ft = 0.000005;      // PVC / CPVC smooth

        private record FluidPropertyTableEntry(
            FluidType Fluid,
            double TemperatureF,
            double PercentGlycol,
            double DensityLbmPerFt3,
            double KinematicViscosityFt2PerS,
            double HazenWilliamsMultiplier,
            double RoughnessMultiplier);

        // Limited table of temperature- and mixture-dependent properties for water and common glycol blends (approximate)
        // Density and viscosity values compiled from ASHRAE/ASHRAE Fundamentals and manufacturer curves.
        private static readonly List<FluidPropertyTableEntry> FluidPropertyData = new()
        {
            // Water only
            new FluidPropertyTableEntry(FluidType.Water, 40, 0, 62.99, 1.62e-5, 1.0, 1.0),
            new FluidPropertyTableEntry(FluidType.Water, 60, 0, 62.37, 1.13e-5, 1.0, 1.0),
            new FluidPropertyTableEntry(FluidType.Water, 80, 0, 61.93, 8.59e-6, 1.0, 1.0),
            new FluidPropertyTableEntry(FluidType.Water, 120, 0, 60.74, 5.41e-6, 1.0, 1.0),

            // Ethylene glycol 30%
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 60, 30, 64.7, 2.20e-5, 0.95, 1.05),
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 100, 30, 63.6, 1.50e-5, 0.95, 1.05),
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 140, 30, 62.4, 1.10e-5, 0.95, 1.05),

            // Ethylene glycol 40%
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 60, 40, 65.3, 2.90e-5, 0.9, 1.08),
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 100, 40, 64.0, 1.90e-5, 0.9, 1.08),
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 140, 40, 62.7, 1.30e-5, 0.9, 1.08),

            // Ethylene glycol 50%
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 60, 50, 66.1, 3.90e-5, 0.85, 1.12),
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 100, 50, 64.6, 2.60e-5, 0.85, 1.12),
            new FluidPropertyTableEntry(FluidType.EthyleneGlycol, 140, 50, 63.1, 1.70e-5, 0.85, 1.12),

            // Propylene glycol 30%
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 60, 30, 64.0, 2.70e-5, 0.93, 1.05),
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 100, 30, 62.8, 1.70e-5, 0.93, 1.05),
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 140, 30, 61.5, 1.20e-5, 0.93, 1.05),

            // Propylene glycol 40%
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 60, 40, 64.6, 3.70e-5, 0.88, 1.08),
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 100, 40, 63.2, 2.40e-5, 0.88, 1.08),
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 140, 40, 61.8, 1.60e-5, 0.88, 1.08),

            // Propylene glycol 50%
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 60, 50, 65.4, 5.10e-5, 0.83, 1.12),
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 100, 50, 63.8, 3.20e-5, 0.83, 1.12),
            new FluidPropertyTableEntry(FluidType.PropyleneGlycol, 140, 50, 62.2, 2.10e-5, 0.83, 1.12)
        };

        // === Geometry ===

        /// <summary>
        /// Circular pipe cross-sectional area (ft²) from diameter (in).
        /// </summary>
        public static double Area_Round_Ft2(double diameterIn)
        {
            if (diameterIn <= 0) return 0;

            double dFt = diameterIn / InPerFt;
            return Math.PI * dFt * dFt / 4.0;
        }

        /// <summary>
        /// Returns the material data set (roughness, C values, velocity caps) for a given pipe material.
        /// </summary>
        public static MaterialHydraulics GetMaterialData(PipeMaterial material) => MaterialData[material];

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
            double clampedTemp = Math.Max(40, Math.Min(180, temperatureF <= 0 ? 60 : temperatureF));

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

        /// <summary>
        /// Returns the internal diameter (in) for a nominal size (in) and material family.
        /// Values are 0 if the nominal size is not available in the embedded table.
        /// </summary>
        public static double GetInnerDiameterIn(PipeMaterial material, double nominalSizeIn)
        {
            if (!NominalToIdIn.TryGetValue(material, out var table)) return 0;
            return table.TryGetValue(nominalSizeIn, out var id) ? id : 0;
        }

        /// <summary>
        /// Returns the available nominal sizes and inside diameters for a material.
        /// </summary>
        public static IReadOnlyDictionary<double, double> GetAvailableNominalIds(PipeMaterial material)
        {
            if (NominalToIdIn.TryGetValue(material, out var table))
            {
                return table;
            }

            return new Dictionary<double, double>();
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
        /// Uses standard form: h_f = 4.52 * Q^1.85 / (C^1.85 * d_in^4.87).
        /// </summary>
        public static double HazenWilliamsHeadLoss_FtPer100Ft(double gpm, double diameterIn, double cFactor)
        {
            if (gpm <= 0 || diameterIn <= 0 || cFactor <= 0) return 0;

            double numerator = 4.52 * Math.Pow(gpm, 1.85);
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
        public static bool IsVelocityWithinGuidance(double velocityFps, PipeMaterial material, bool isHotWater)
        {
            if (velocityFps <= 0) return false;
            var data = GetMaterialData(material);
            double limit = isHotWater ? data.MaxHotFps : data.MaxColdFps;
            return velocityFps <= limit;
        }

        // === Fixture unit / drainage helpers (IPC/UPC style tables) ===

        private static readonly List<(double FixtureUnits, double DemandGpm)> HunterCurvePoints = new()
        {
            // IPC/UPC Hunter curve anchor points (total fixture units -> probable demand gpm)
            (1, 1.0),
            (2, 1.4),
            (4, 2.0),
            (6, 2.4),
            (8, 2.8),
            (10, 3.1),
            (15, 3.8),
            (20, 4.4),
            (30, 5.8),
            (40, 7.0),
            (60, 8.9),
            (80, 10.7),
            (100, 12.3),
            (150, 15.6),
            (200, 18.2),
            (400, 26.9),
            (600, 34.0),
            (1000, 48.0)
        };

        /// <summary>
        /// Convert total fixture units to probable peak demand (gpm) using Hunter's curve
        /// with log-log interpolation between IPC/UPC anchor points.
        /// </summary>
        public static double HunterDemandGpm(double totalFixtureUnits)
        {
            if (totalFixtureUnits <= 0) return 0;

            if (totalFixtureUnits <= HunterCurvePoints[0].FixtureUnits)
                return HunterCurvePoints[0].DemandGpm * totalFixtureUnits / HunterCurvePoints[0].FixtureUnits;

            for (int i = 0; i < HunterCurvePoints.Count - 1; i++)
            {
                var a = HunterCurvePoints[i];
                var b = HunterCurvePoints[i + 1];
                if (totalFixtureUnits >= a.FixtureUnits && totalFixtureUnits <= b.FixtureUnits)
                {
                    double logFu = Math.Log10(totalFixtureUnits);
                    double logA = Math.Log10(a.FixtureUnits);
                    double logB = Math.Log10(b.FixtureUnits);
                    double t = (logFu - logA) / (logB - logA);

                    double logQa = Math.Log10(a.DemandGpm);
                    double logQb = Math.Log10(b.DemandGpm);
                    double logQ = logQa + t * (logQb - logQa);
                    return Math.Pow(10, logQ);
                }
            }

            // Extrapolate beyond the last point conservatively using the last slope
            var last = HunterCurvePoints[^1];
            var prev = HunterCurvePoints[^2];
            double lastSlope = (Math.Log10(last.DemandGpm) - Math.Log10(prev.DemandGpm)) /
                               (Math.Log10(last.FixtureUnits) - Math.Log10(prev.FixtureUnits));
            double extrapolated = Math.Log10(last.DemandGpm) +
                                  lastSlope * (Math.Log10(totalFixtureUnits) - Math.Log10(last.FixtureUnits));
            return Math.Pow(10, extrapolated);
        }

        // IPC Table 703.2 style DFU limits for horizontal drainage (dfu capacity at typical slopes)
        // Slopes are stored in ft/ft, converted from the customary in/ft presentation
        // (e.g., 1/4 in/ft → 0.0208 ft/ft) used by IPC/UPC branch sizing tables.
        private const double SlopeQuarterInPerFt_FtPerFt = 0.25 / InPerFt;   // 1/4 in per ft
        private const double SlopeEighthInPerFt_FtPerFt = 0.125 / InPerFt;   // 1/8 in per ft
        private const double SlopeSixteenthInPerFt_FtPerFt = 0.0625 / InPerFt; // 1/16 in per ft
        private static readonly Dictionary<double, List<(double DiameterIn, double MaxDfu)>> SanitaryCapacity = new()
        {
            { SlopeQuarterInPerFt_FtPerFt, new List<(double, double)> { (2.0, 21), (2.5, 24), (3.0, 35), (4.0, 216) } },
            { SlopeEighthInPerFt_FtPerFt, new List<(double, double)> { (2.0, 15), (2.5, 20), (3.0, 36), (4.0, 180) } },
            { SlopeSixteenthInPerFt_FtPerFt, new List<(double, double)> { (2.0, 8), (2.5, 21), (3.0, 42), (4.0, 216) } }
        };

        /// <summary>
        /// Minimum nominal diameter (in) to carry the given sanitary drainage fixture units
        /// for a horizontal branch at the provided slope (ft/ft). Uses embedded IPC-style DFU caps.
        /// Returns 0 when no table value satisfies the demand.
        /// </summary>
        public static double MinSanitaryDiameterFromDfu(double drainageFixtureUnits, double slopeFtPerFt)
        {
            if (drainageFixtureUnits <= 0 || slopeFtPerFt <= 0) return 0;

            // Find nearest slope in table (simple nearest match)
            double closestSlope = 0;
            double minDelta = double.MaxValue;
            foreach (var kvp in SanitaryCapacity)
            {
                double delta = Math.Abs(kvp.Key - slopeFtPerFt);
                if (delta < minDelta)
                {
                    minDelta = delta;
                    closestSlope = kvp.Key;
                }
            }

            if (closestSlope == 0) return 0;

            foreach (var entry in SanitaryCapacity[closestSlope])
            {
                if (drainageFixtureUnits <= entry.MaxDfu)
                    return entry.DiameterIn;
            }

            return 0;
        }

        // === Storm drainage (Manning full-pipe) ===

        /// <summary>
        /// Storm flow in gpm from roof/area (ft²) and rainfall intensity (in/hr):
        /// Q = I * A / 96.23 per IPC/UPC rainfall method.
        /// </summary>
        public static double StormFlowGpm(double areaFt2, double rainfallIntensityInPerHr)
        {
            if (areaFt2 <= 0 || rainfallIntensityInPerHr <= 0) return 0;
            return rainfallIntensityInPerHr * areaFt2 / 96.23;
        }

        /// <summary>
        /// Solve full-flow circular pipe diameter (in) for a storm flow using Manning's equation.
        /// Q (gpm) = 449 * (1/n) * A * R^(2/3) * S^(1/2) where A in ft², R in ft, S slope (ft/ft).
        /// </summary>
        public static double StormDiameterFromFlow(double flowGpm, double slopeFtPerFt, double roughnessN = 0.012,
                                                   double minDiameterIn = 2.0, double maxDiameterIn = 60.0)
        {
            if (flowGpm <= 0 || slopeFtPerFt <= 0 || roughnessN <= 0) return 0;

            double FlowFromDiameter(double dIn)
            {
                double dFt = dIn / InPerFt;
                double area = Math.PI * dFt * dFt / 4.0;
                double radius = dFt / 4.0; // hydraulic radius for full pipe
                double qCfs = (1.49 / roughnessN) * area * Math.Pow(radius, 2.0 / 3.0) * Math.Sqrt(slopeFtPerFt);
                return qCfs * 448.831; // to gpm
            }

            double lo = minDiameterIn;
            double hi = maxDiameterIn;
            double fLo = FlowFromDiameter(lo) - flowGpm;
            double fHi = FlowFromDiameter(hi) - flowGpm;

            for (int i = 0; i < 40; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = FlowFromDiameter(mid) - flowGpm;

                if (Math.Abs(fMid) < 1e-3)
                    return mid;

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

            return 0.5 * (lo + hi);
        }

        // === Low-pressure natural gas sizing (IFGC/NFPA 54 style) ===

        private const double InWcPerPsi = 27.7076;
        private const double GasHeatingValue_BtuPerScf = 1000.0; // typical pipeline gas

        /// <summary>
        /// IFGC/NFPA 54 empirical sizing equation (low pressure):
        /// Q_scfh = 3550 * (ΔP * P_base / (SG * L))^0.54 * d^2.63
        /// where ΔP and P_base in psi, L in ft, d in inches, SG relative to air.
        /// </summary>
        public static double GasFlow_Scfh(double diameterIn, double lengthFt, double pressureDropInWc,
                                          double specificGravity = 0.6, double basePressurePsi = 0.5)
        {
            if (diameterIn <= 0 || lengthFt <= 0 || pressureDropInWc <= 0 || specificGravity <= 0) return 0;

            double deltaPsi = pressureDropInWc / InWcPerPsi;
            double term = deltaPsi * basePressurePsi / (specificGravity * lengthFt);
            double multiplier = Math.Pow(term, 0.54);
            return 3550.0 * multiplier * Math.Pow(diameterIn, 2.63);
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
        /// </summary>
        public static double SolveGasDiameterForLoad(double loadMbh, double lengthFt, double pressureDropInWc,
                                                     double specificGravity = 0.6, double basePressurePsi = 0.5,
                                                     double heatingValueBtuPerScf = GasHeatingValue_BtuPerScf,
                                                     double minDiameterIn = 0.5, double maxDiameterIn = 6.0)
        {
            if (loadMbh <= 0 || lengthFt <= 0 || pressureDropInWc <= 0) return 0;

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
                    return mid;

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

            return 0.5 * (lo + hi);
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

        public static double GetWaveSpeedFps(PipeMaterial material)
        {
            return WaveSpeed_FtPerS.TryGetValue(material, out double a) ? a : 4000.0;
        }

        public static double SurgePressurePsi(double velocityChangeFps, PipeMaterial material)
        {
            if (velocityChangeFps <= 0) return 0;

            double a = GetWaveSpeedFps(material);
            double densitySlugPerFt3 = WaterDensity_LbmPerFt3 / LbmPerSlug;
            double deltaPLbPerFt2 = densitySlugPerFt3 * a * velocityChangeFps;
            return deltaPLbPerFt2 / 144.0;
        }

        public static double SurgePressureWithClosure(double flowVelocityFps, double pipeLengthFt, double closureTimeSeconds, PipeMaterial material)
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
