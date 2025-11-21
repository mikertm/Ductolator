using System;
using System.Collections.Generic;

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
        private const double PsiPerFtHead = 0.4335275; // 1 psi ≈ 2.31 ft head
        private const double InPerFt = 12.0;
        private const double FtPer100Ft = 100.0;
        private const double GpmToCfs = 0.00222800926; // 1 gpm = 0.002228 ft³/s

        // Kinematic viscosity of water at 60 °F (ASHRAE/ASPE tables)
        private const double WaterNu60F_Ft2PerS = 1.13e-5;

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
        /// Returns the internal diameter (in) for a nominal size (in) and material family.
        /// Values are 0 if the nominal size is not available in the embedded table.
        /// </summary>
        public static double GetInnerDiameterIn(PipeMaterial material, double nominalSizeIn)
        {
            if (!NominalToIdIn.TryGetValue(material, out var table)) return 0;
            return table.TryGetValue(nominalSizeIn, out var id) ? id : 0;
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

        public static double HazenWilliamsPsiPer100Ft(double gpm, double diameterIn, double cFactor)
        {
            double headFtPer100 = HazenWilliamsHeadLoss_FtPer100Ft(gpm, diameterIn, cFactor);
            return headFtPer100 * PsiPerFtHead;
        }

        /// <summary>
        /// Hazen-Williams head loss (psi) across a specific run length (ft).
        /// </summary>
        public static double HazenWilliamsPsi(double gpm, double diameterIn, double cFactor, double lengthFt)
        {
            if (lengthFt <= 0) return 0;

            double psiPer100 = HazenWilliamsPsiPer100Ft(gpm, diameterIn, cFactor);
            return psiPer100 * (lengthFt / FtPer100Ft);
        }

        /// <summary>
        /// Solve flow (gpm) for a target Hazen-Williams loss (psi/100 ft).
        /// </summary>
        public static double SolveFlowFromHazenWilliams(double diameterIn, double targetPsiPer100Ft, double cFactor)
        {
            if (diameterIn <= 0 || targetPsiPer100Ft <= 0 || cFactor <= 0) return 0;

            double targetHead = targetPsiPer100Ft / PsiPerFtHead;
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
        public static double SolveDiameterFromHazenWilliams(double gpm, double targetPsiPer100Ft, double cFactor)
        {
            if (gpm <= 0 || targetPsiPer100Ft <= 0 || cFactor <= 0) return 0;

            double targetHead = targetPsiPer100Ft / PsiPerFtHead;

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

        public static double HeadLoss_Darcy_PsiPer100Ft(double gpm, double diameterIn, double roughnessFt, double? kinematicViscosityFt2PerS = null)
        {
            double headFt = HeadLoss_Darcy_FtPer100Ft(gpm, diameterIn, roughnessFt, kinematicViscosityFt2PerS);
            return headFt * PsiPerFtHead;
        }

        /// <summary>
        /// Darcy-Weisbach pressure drop (psi) over an arbitrary length (ft).
        /// </summary>
        public static double HeadLoss_Darcy_Psi(double gpm, double diameterIn, double roughnessFt, double lengthFt, double? kinematicViscosityFt2PerS = null)
        {
            if (lengthFt <= 0) return 0;

            double psiPer100 = HeadLoss_Darcy_PsiPer100Ft(gpm, diameterIn, roughnessFt, kinematicViscosityFt2PerS);
            return psiPer100 * (lengthFt / FtPer100Ft);
        }

        /// <summary>
        /// Minor (fitting) loss using loss coefficient K: ΔP = K * (ρ V² / 2) / 144 to psi.
        /// </summary>
        public static double MinorLossPsi(double velocityFps, double kCoefficient)
        {
            if (velocityFps <= 0 || kCoefficient < 0) return 0;

            double velocityPressure_LbPerFt2 = WaterDensity_LbmPerFt3 / LbmPerSlug * velocityFps * velocityFps / 2.0;
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
    }
}
