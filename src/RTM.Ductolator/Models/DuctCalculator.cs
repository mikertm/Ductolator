using System;
using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    public static class DuctCalculator
    {
        // === Assumptions & Constants ===
        /// <summary>
        /// Default roughness for galvanized steel (medium smooth) in feet.
        /// Source: ASHRAE Fundamentals (typical value for friction charts).
        /// </summary>
        public const double DefaultGalvanizedRoughnessFt = 0.0003;

        public const string FrictionFactorMethodName = "Darcy-Weisbach (Colebrook-White)";
        public const string AirModelName = "Standard atmosphere (density) + Sutherland viscosity (ν)";

        // ASHRAE standard air properties at sea level (14.696 psia, 70°F)
        public static readonly double StandardDensity_LbmPerFt3 = 0.075;
        public static readonly double StandardViscosity_LbmPerFtS = 1.22e-5; // Dynamic viscosity µ
        public static readonly double StandardKinematicViscosity_Ft2PerS = 1.62e-4; // at ~70F

        public record AirProperties(double DensityLbmPerFt3, double KinematicViscosityFt2PerS);

        /// <summary>
        /// Calculate air density and kinematic viscosity given dry bulb temperature and altitude.
        /// </summary>
        public static AirProperties AirAt(double tempF, double altitudeFt)
        {
            double tempR = tempF + Units.RankineZeroF;
            // P_psia = 14.696 * (1 - 6.8754e-6 * Z_ft)^5.2559
            double pPsia = 14.696 * Math.Pow(1.0 - 6.8754e-6 * altitudeFt, 5.2559);

            // R_specific for dry air = 53.35 ft·lbf/(lbm·R)
            // rho = P / (R * T)
            // P in psf = pPsia * 144
            double rho = (pPsia * Units.SqInchesPerSqFoot) / (53.35 * tempR);

            double muRef = 1.22e-5; // lbm/(ft·s) at 70 F (529.67 R)
            double tRef = 529.67;
            double s = 198.72;

            double mu = muRef * Math.Pow(tempR / tRef, 1.5) * (tRef + s) / (tempR + s);
            double nu = mu / rho;

            return new AirProperties(rho, nu);
        }

        // === Geometry Helpers ===

        public static double Area_Round_Ft2(double diameterIn)
        {
            if (diameterIn <= 0) return 0;
            return Math.PI * Math.Pow(Units.FromInchesToFeet(diameterIn), 2) / 4.0;
        }

        public static double Circumference_Round_Ft(double diameterIn)
        {
            if (diameterIn <= 0) return 0;
            return Math.PI * Units.FromInchesToFeet(diameterIn);
        }

        public static (double AreaFt2, double PerimeterFt) RectGeometry(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return (0, 0);
            double w = Units.FromInchesToFeet(side1In);
            double h = Units.FromInchesToFeet(side2In);
            return (w * h, 2.0 * (w + h));
        }

        public static (double AreaFt2, double PerimeterFt) FlatOvalGeometry(double minorAxisIn, double majorAxisIn)
        {
            if (minorAxisIn <= 0 || majorAxisIn <= 0) return (0, 0);

            double a = Math.Max(minorAxisIn, majorAxisIn); // width
            double b = Math.Min(minorAxisIn, majorAxisIn); // height

            double straightLen = a - b;
            double circleArea = Math.PI * Math.Pow(b / 2.0, 2.0);
            double rectArea = straightLen * b;
            double areaIn2 = circleArea + rectArea;

            double perimeterIn = 2.0 * straightLen + Math.PI * b;

            return (Units.FromSqInchesToSqFeet(areaIn2), Units.FromInchesToFeet(perimeterIn));
        }

        /// <summary>
        /// Equivalent round diameter for a rectangular duct using Huebscher equation (ASHRAE Equal Friction).
        /// De = 1.30 * ((a * b)^0.625) / ((a + b)^0.25)
        /// Used for equal-friction matching (ASHRAE-style), not hydraulic diameter.
        /// </summary>
        public static double EquivalentRound_Rect(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return 0;
            double num = Math.Pow(side1In * side2In, 0.625);
            double den = Math.Pow(side1In + side2In, 0.25);
            return 1.30 * num / den;
        }

        /// <summary>
        /// Equivalent round diameter for a flat oval duct.
        /// Uses ASHRAE form based on cross-sectional area and perimeter.
        /// De = 1.55 * A^0.625 / P^0.25
        /// Used for equal-friction matching.
        /// </summary>
        public static double EquivalentRound_FlatOval(double minorIn, double majorIn)
        {
            if (minorIn <= 0 || majorIn <= 0) return 0;

            double a = Math.Max(minorIn, majorIn);
            double b = Math.Min(minorIn, majorIn);

            double straightLen = a - b;
            double circleArea = Math.PI * Math.Pow(b / 2.0, 2.0);
            double rectArea = straightLen * b;
            double areaIn2 = circleArea + rectArea;
            double perimeterIn = 2.0 * straightLen + Math.PI * b;

            if (areaIn2 <= 0 || perimeterIn <= 0) return 0;

            return 1.55 * Math.Pow(areaIn2, 0.625) / Math.Pow(perimeterIn, 0.25);
        }

        // === Flow / Velocity ===

        public static double VelocityFpmFromCfmAndArea(double cfm, double areaFt2)
        {
            if (areaFt2 <= 0) return 0;
            return cfm / areaFt2;
        }

        public static double CfmFromVelocityAndArea(double fpm, double areaFt2)
        {
            return fpm * areaFt2;
        }

        /// <summary>
        /// Reynolds number = (Velocity * Diameter) / KinematicViscosity.
        /// D is the diameter used for friction (typically Equivalent Round for non-circular).
        /// </summary>
        public static double Reynolds(double velocityFpm, double diameterIn, AirProperties air)
        {
            if (velocityFpm <= 0 || diameterIn <= 0 || air.KinematicViscosityFt2PerS <= 0) return 0;
            double velFps = Units.FromFpmToFps(velocityFpm);
            double dFt = Units.FromInchesToFeet(diameterIn);
            return (velFps * dFt) / air.KinematicViscosityFt2PerS;
        }

        // === Friction Factor ===

        /// <summary>
        /// Primary friction factor calculation using Colebrook-White.
        /// </summary>
        public static double FrictionFactor(double re, double diameterIn, double roughnessFt = DefaultGalvanizedRoughnessFt)
        {
            return FrictionFactor_ColebrookDarcy(re, diameterIn, roughnessFt);
        }

        /// <summary>
        /// Iterative Colebrook-White solver for Darcy friction factor.
        /// 1/sqrt(f) = -2 log10( (ε/D)/3.7 + 2.51/(Re*sqrt(f)) )
        /// </summary>
        public static double FrictionFactor_ColebrookDarcy(double reynolds, double diameterIn, double roughnessFt = DefaultGalvanizedRoughnessFt)
        {
            if (reynolds <= 0 || diameterIn <= 0) return 0;

            if (reynolds < 2300)
            {
                return 64.0 / reynolds;
            }

            double dFt = Units.FromInchesToFeet(diameterIn);
            double relRough = roughnessFt / dFt;

            // Initial guess using Haaland
            double f = FrictionFactor_HaalandDarcy(reynolds, diameterIn, roughnessFt);
            if (f <= 0) f = 0.02; // Fallback

            // Newton-Raphson or Fixed Point iteration
            // Let x = 1/sqrt(f)
            // x = -2 * log10( relRough/3.7 + 2.51 * x / Re )
            double x = 1.0 / Math.Sqrt(f);

            for (int i = 0; i < 50; i++) // Cap iterations
            {
                double term = relRough / 3.7 + (2.51 * x) / reynolds;
                double xNew = -2.0 * Math.Log10(term);

                if (Math.Abs(xNew - x) < 1e-8)
                {
                    x = xNew;
                    return 1.0 / (x * x);
                }
                x = xNew;
            }

            // If not converged, return approximation (and maybe log/warn in real app)
            return FrictionFactor_HaalandDarcy(reynolds, diameterIn, roughnessFt);
        }

        /// <summary>
        /// Explicit Haaland approximation for Darcy friction factor.
        /// 1/sqrt(f) = -1.8 log10( ((ε/D)/3.7)^1.11 + 6.9/Re )
        /// </summary>
        public static double FrictionFactor_HaalandDarcy(double reynolds, double diameterIn, double roughnessFt = DefaultGalvanizedRoughnessFt)
        {
            if (reynolds <= 0 || diameterIn <= 0) return 0;
            if (reynolds < 2300) return 64.0 / reynolds;

            double dFt = Units.FromInchesToFeet(diameterIn);
            double relRough = roughnessFt / dFt;

            double term = Math.Pow(relRough / 3.7, 1.11) + 6.9 / reynolds;
            double invSqrtF = -1.8 * Math.Log10(term);
            return 1.0 / (invSqrtF * invSqrtF);
        }

        // === Pressure Drop ===

        /// <summary>
        /// Calculate Velocity Pressure (VP) in in.w.g.
        /// VP = Density * (V_fps)^2 / (2 * g_c * 5.202)
        /// </summary>
        public static double VelocityPressure_InWG(double velocityFpm, AirProperties air)
        {
            if (velocityFpm <= 0) return 0;
            double vFps = Units.FromFpmToFps(velocityFpm);
            // Dynamic Pressure (psf) = rho_lbm * v^2 / (2 * g_c)
            double vpPsf = (air.DensityLbmPerFt3 * vFps * vFps) / (2.0 * Units.Gc);
            return vpPsf * Units.InWgPerPsf;
        }

        /// <summary>
        /// Calculate friction pressure drop per 100 ft (in.w.g./100ft).
        /// Darcy-Weisbach: ΔP = f * (L/D) * VP
        /// </summary>
        public static double DpPer100Ft_InWG(double velocityFpm, double diameterIn, double f, AirProperties air)
        {
            if (diameterIn <= 0) return 0;
            double dFt = Units.FromInchesToFeet(diameterIn);
            double vp = VelocityPressure_InWG(velocityFpm, air);

            // ΔP_100 = f * (100 / D_ft) * VP_inwg
            return f * (100.0 / dFt) * vp;
        }

        /// <summary>
        /// Total pressure drop over a run length with fittings.
        /// </summary>
        public static double TotalPressureDrop_InWG(double frictionPer100, double runLength, double sumLossCoeffs, double velocityFpm, AirProperties air)
        {
            double frictionTotal = frictionPer100 * (runLength / 100.0);
            double vp = VelocityPressure_InWG(velocityFpm, air);
            double dynamicLoss = sumLossCoeffs * vp;
            return frictionTotal + dynamicLoss;
        }

        // === Solvers ===

        /// <summary>
        /// Solve velocity (FPM) given a target pressure drop (in.wg/100ft) and diameter (in).
        /// </summary>
        public static double SolveVelocityFpm_FromDp(double diameterIn, double targetDpPer100, AirProperties air = null)
        {
            var props = air ?? AirAt(70, 0);
            if (diameterIn <= 0 || targetDpPer100 <= 0) return 0;

            double vGuess = 1000.0;

            for (int i = 0; i < 20; i++)
            {
                double re = Reynolds(vGuess, diameterIn, props);
                double f = FrictionFactor(re, diameterIn);
                double dp = DpPer100Ft_InWG(vGuess, diameterIn, f, props);

                if (Math.Abs(dp - targetDpPer100) < 1e-5) return vGuess;

                // Adjust guess
                vGuess = vGuess * Math.Pow(targetDpPer100 / dp, 0.55);
            }

            return vGuess;
        }

        /// <summary>
        /// Solve diameter (in) given CFM and target pressure drop.
        /// </summary>
        public static double SolveRoundDiameter_FromCfmAndFriction(double cfm, double targetDpPer100, AirProperties air = null)
        {
            var props = air ?? AirAt(70, 0);
            if (cfm <= 0 || targetDpPer100 <= 0) return 0;

            double lo = 2.0;
            double hi = 100.0;

            for (int i = 0; i < 50; i++)
            {
                double mid = 0.5 * (lo + hi);
                double area = Area_Round_Ft2(mid);
                double v = VelocityFpmFromCfmAndArea(cfm, area);
                double re = Reynolds(v, mid, props);
                double f = FrictionFactor(re, mid);
                double dp = DpPer100Ft_InWG(v, mid, f, props);

                if (Math.Abs(dp - targetDpPer100) < 1e-5) return mid;

                if (dp > targetDpPer100)
                    lo = mid;
                else
                    hi = mid;
            }

            return 0.5 * (lo + hi);
        }

        // === Helpers ===

        public static (double s1In, double s2In) RectangleFromAreaAndAR(double areaFt2, double ar)
        {
            if (areaFt2 <= 0 || ar <= 0) return (0, 0);
            double areaIn2 = Units.FromSqFeetToSqInches(areaFt2);
            double s2 = Math.Sqrt(areaIn2 / ar);
            double s1 = areaIn2 / s2;
            return (s1, s2);
        }

        public static double SelectSmacnaPressureClass(double staticPressureInWg)
        {
            double absP = Math.Abs(staticPressureInWg);
            if (absP <= 0.5) return 0.5;
            if (absP <= 1.0) return 1.0;
            if (absP <= 2.0) return 2.0;
            if (absP <= 3.0) return 3.0;
            if (absP <= 4.0) return 4.0;
            if (absP <= 6.0) return 6.0;
            if (absP <= 10.0) return 10.0;
            return Math.Ceiling(absP);
        }

        public static double LeakageCfm(double leakageClass, double staticPressureInWg, double surfaceAreaFt2)
        {
            if (leakageClass <= 0 || staticPressureInWg <= 0 || surfaceAreaFt2 <= 0) return 0;
            double flowPer100 = leakageClass * Math.Pow(staticPressureInWg, 0.65);
            return flowPer100 * (surfaceAreaFt2 / 100.0);
        }

        public static double FanBrakeHorsepower(double cfm, double totalPressureInWg, double fanEfficiency)
        {
            if (cfm <= 0 || totalPressureInWg <= 0 || fanEfficiency <= 0) return 0;
            // bhp = (CFM * TP) / (6356 * eff) where 6356 is unit conversion constant
            return (cfm * totalPressureInWg) / (6356.0 * fanEfficiency);
        }

        public static double HeatTransfer_Btuh(double uValue, double surfaceAreaFt2, double deltaTemp)
        {
            return uValue * surfaceAreaFt2 * deltaTemp;
        }

        public static double AirTemperatureChangeFromHeat(double qBtuh, double cfm, AirProperties air)
        {
            if (cfm <= 0) return 0;
            double mDot = cfm * 60.0 * air.DensityLbmPerFt3; // lb/hr
            double cp = 0.24;
            return qBtuh / (mDot * cp);
        }

        public static (double Side1In, double Side2In) EqualFrictionRectangleForRound(double diameterIn, double ar)
        {
            double targetD = diameterIn;
            double lo = 1.0;
            double hi = diameterIn * 2;

            for (int i=0; i<30; i++)
            {
                double s2 = 0.5 * (lo + hi);
                double s1 = s2 * ar;
                double de = EquivalentRound_Rect(s1, s2);
                if (Math.Abs(de - targetD) < 0.01) return (s1, s2);
                if (de < targetD) lo = s2; else hi = s2;
            }
            double finalS2 = 0.5 * (lo + hi);
            return (finalS2 * ar, finalS2);
        }

        public static (double MajorIn, double MinorIn) EqualFrictionFlatOvalForRound(double diameterIn, double ar)
        {
            double targetD = diameterIn;
            double lo = 1.0;
            double hi = diameterIn * 2;

            for (int i = 0; i < 30; i++)
            {
                double minor = 0.5 * (lo + hi);
                double major = minor * ar;
                double de = EquivalentRound_FlatOval(minor, major);
                if (Math.Abs(de - targetD) < 0.01) return (major, minor);
                if (de < targetD) lo = minor; else hi = minor;
            }
            double finalMin = 0.5 * (lo + hi);
            return (finalMin * ar, finalMin);
        }

        public static double RequiredInsulationR(double maxDeltaT, double surfaceAreaFt2, double ambientDelta, double cfm, AirProperties air)
        {
            double mDot = cfm * 60.0 * air.DensityLbmPerFt3;
            double cp = 0.24;
            double qLimit = Math.Abs(mDot * cp * maxDeltaT);

            if (qLimit <= 0 || surfaceAreaFt2 <= 0 || Math.Abs(ambientDelta) <= 0.1) return 0;

            double requiredU = qLimit / (surfaceAreaFt2 * Math.Abs(ambientDelta));
            if (requiredU <= 0) return 0;

            double rTotal = 1.0 / requiredU;
            double films = 0.61 + 0.17;
            double rInsul = rTotal - films;
            return rInsul > 0 ? rInsul : 0;
        }

        public static double InsulationThicknessInFromR(double rValue)
        {
            if (rValue <= 0) return 0;
            return rValue / 3.0;
        }
    }
}
