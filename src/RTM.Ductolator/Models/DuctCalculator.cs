using System;
using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    public static class DuctCalculator
    {
        public const string FrictionFactorMethodName = "Churchill (1977) explicit correlation";
        public const string AirModelName = "Standard atmosphere (density) + Sutherland viscosity (ν)";

        // ASHRAE standard air properties at sea level (14.696 psia, 70°F)
        // Note: ASHRAE uses 0.075 lbm/ft3 for "standard air" in many tables, but at 70F/sea level it is approx 0.0749.
        public static readonly double StandardDensity_LbmPerFt3 = 0.075;
        public static readonly double StandardViscosity_LbmPerFtS = 1.22e-5; // Dynamic viscosity µ
        // Kinematic viscosity ν = µ/ρ
        public static readonly double StandardKinematicViscosity_Ft2PerS = 1.62e-4; // at ~70F

        // Default roughness for galvanized steel (ASHRAE Fundamentals)
        public const double DefaultRoughnessFt = 0.0003; // ~0.09 mm (medium smooth) or 0.0005 ft (average)
        // 0.0003 ft is commonly used for new galvanized ducts.

        public record AirProperties(double DensityLbmPerFt3, double KinematicViscosityFt2PerS);

        /// <summary>
        /// Calculate air density and kinematic viscosity given dry bulb temperature and altitude.
        /// Uses ideal gas law and Sutherland's formula for viscosity.
        /// </summary>
        public static AirProperties AirAt(double tempF, double altitudeFt)
        {
            // Rankine temperature
            double tempR = tempF + 459.67;

            // Pressure at altitude (standard atmosphere model)
            // P = P0 * (1 - L*h / T0)^(g*M / (R*L))
            // Approx: P_psia = 14.696 * (1 - 6.8754e-6 * Z_ft)^5.2559
            double pPsia = 14.696 * Math.Pow(1.0 - 6.8754e-6 * altitudeFt, 5.2559);

            // Density: rho = P / (R_specific * T)
            // R_specific for dry air = 53.35 ft·lbf/(lbm·R)
            // P in psf = P_psia * 144
            double rho = (pPsia * 144.0) / (53.35 * tempR);

            // Dynamic viscosity (Sutherland's Law)
            // mu = C1 * T^1.5 / (T + S)
            // For air (Rankine): C1 ~ 2.27e-8 lbf·s/(ft²·R^0.5) ?? No, let's use standard relation relative to ref.
            // mu_ref = 1.22e-5 lbm/(ft·s) at 529.67 R (70 F)
            // S (Sutherland constant) ~ 198.72 R (110.4 K)
            double muRef = 1.22e-5; // lbm/(ft·s) at 70 F
            double tRef = 70.0 + 459.67;
            double s = 198.72;

            double mu = muRef * Math.Pow(tempR / tRef, 1.5) * (tRef + s) / (tempR + s);

            // Kinematic viscosity nu = mu / rho
            double nu = mu / rho;

            return new AirProperties(rho, nu);
        }

        public static double Area_Round_Ft2(double diameterIn)
        {
            if (diameterIn <= 0) return 0;
            return Math.PI * Math.Pow(diameterIn / 12.0, 2) / 4.0;
        }

        public static double Circumference_Round_Ft(double diameterIn)
        {
            if (diameterIn <= 0) return 0;
            return Math.PI * (diameterIn / 12.0);
        }

        public static (double AreaFt2, double PerimeterFt) RectGeometry(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return (0, 0);
            double w = side1In / 12.0;
            double h = side2In / 12.0;
            return (w * h, 2.0 * (w + h));
        }

        public static (double AreaFt2, double PerimeterFt) FlatOvalGeometry(double minorAxisIn, double majorAxisIn)
        {
            // Flat oval: a rectangle with semicircles on the ends
            // Major axis = width (overall), Minor axis = height
            // Straight section length = Major - Minor
            // Area = RectArea + CircleArea = (Major - Minor)*Minor + pi*(Minor/2)^2
            // Perimeter = 2*(Major - Minor) + pi*Minor
            if (minorAxisIn <= 0 || majorAxisIn <= 0) return (0, 0);

            // Ensure major >= minor
            double a = Math.Max(minorAxisIn, majorAxisIn);
            double b = Math.Min(minorAxisIn, majorAxisIn);

            double straightLen = a - b;
            double circleArea = Math.PI * Math.Pow(b / 2.0, 2.0);
            double rectArea = straightLen * b;
            double areaIn2 = circleArea + rectArea;

            double perimeterIn = 2.0 * straightLen + Math.PI * b;

            return (areaIn2 / 144.0, perimeterIn / 12.0);
        }

        /// <summary>
        /// Convert CFM and Area (ft²) to velocity in FPM.
        /// </summary>
        public static double VelocityFpmFromCfmAndArea(double cfm, double areaFt2)
        {
            if (areaFt2 <= 0) return 0;
            return cfm / areaFt2;
        }

        /// <summary>
        /// Convert Velocity (FPM) and Area (ft²) to CFM.
        /// </summary>
        public static double CfmFromVelocityAndArea(double fpm, double areaFt2)
        {
            return fpm * areaFt2;
        }

        /// <summary>
        /// Reynolds number = (Velocity * HydraulicDiameter) / KinematicViscosity
        /// </summary>
        public static double Reynolds(double velocityFpm, double diameterIn, AirProperties air)
        {
            if (velocityFpm <= 0 || diameterIn <= 0 || air.KinematicViscosityFt2PerS <= 0) return 0;
            double velFps = velocityFpm / 60.0;
            double dFt = diameterIn / 12.0;
            return (velFps * dFt) / air.KinematicViscosityFt2PerS;
        }

        /// <summary>
        /// Calculate Darcy friction factor 'f' using Churchill (1977) equation.
        /// Valid for laminar, transitional, and turbulent flow.
        /// </summary>
        public static double FrictionFactor(double re, double diameterIn, double roughnessFt = DefaultRoughnessFt)
        {
            if (re <= 0 || diameterIn <= 0) return 0;

            double dFt = diameterIn / 12.0;
            // Avoid divide by zero if D is huge or roughness is zero (though usually non-zero).
            double relRough = roughnessFt / dFt;

            // Churchill equation term A
            // A = (-2.457 * ln( (7/Re)^0.9 + 0.27 * (e/D) ))^16
            double termA_inner = Math.Pow(7.0 / re, 0.9) + 0.27 * relRough;
            double termA = Math.Pow(-2.457 * Math.Log(termA_inner), 16.0);

            // Churchill equation term B
            // B = (37530 / Re)^16
            double termB = Math.Pow(37530.0 / re, 16.0);

            // f = 8 * ( (8/Re)^12 + 1 / (A + B)^1.5 )^(1/12)
            double term8Re = Math.Pow(8.0 / re, 12.0);
            double f = 8.0 * Math.Pow(term8Re + 1.0 / Math.Pow(termA + termB, 1.5), 1.0 / 12.0);

            return f;
        }

        /// <summary>
        /// Velocity Pressure (VP) = (Velocity / 4005)^2 * DensityRatio
        /// Standard: VP = (V / 4005)^2
        /// Generalized: VP = rho * (V_fps)^2 / (2 * g_c * 5.193...) -> In WG
        /// Simplified: VP_inWg = Density * (V_fpm / 1097)^2
        /// </summary>
        public static double VelocityPressure_InWG(double velocityFpm, AirProperties air)
        {
            if (velocityFpm <= 0) return 0;
            // 1097 is the constant conversion factor for V in fpm, P in in.wg, rho in lb/ft3
            // VP = rho * (V / 1097)^2
            return air.DensityLbmPerFt3 * Math.Pow(velocityFpm / 1097.0, 2.0);
        }

        /// <summary>
        /// Darcy-Weisbach pressure drop per 100 ft.
        /// dP_100 = f * (100 / D) * VP
        /// </summary>
        public static double DpPer100Ft_InWG(double velocityFpm, double diameterIn, double f, AirProperties air)
        {
            if (diameterIn <= 0) return 0;
            double vp = VelocityPressure_InWG(velocityFpm, air);
            double dFt = diameterIn / 12.0;
            return f * (100.0 / dFt) * vp;
        }

        /// <summary>
        /// Equivalent round diameter for a rectangular duct (Huebscher equation).
        /// De = 1.30 * ((a * b)^0.625) / ((a + b)^0.25)
        /// </summary>
        public static double EquivalentRound_Rect(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return 0;
            double num = Math.Pow(side1In * side2In, 0.625);
            double den = Math.Pow(side1In + side2In, 0.25);
            return 1.30 * num / den;
        }

        /// <summary>
        /// Equivalent round diameter for a flat oval duct (Heyt & Diaz).
        /// De = 1.55 * A^0.625 / P^0.25
        /// Use geometric area and perimeter.
        /// </summary>
        public static double EquivalentRound_FlatOval(double minorIn, double majorIn)
        {
            // Ensure major >= minor
            double a = Math.Max(minorIn, majorIn);
            double b = Math.Min(minorIn, majorIn);

            // Area in sq inches
            double rectArea = (a - b) * b;
            double circArea = Math.PI * b * b / 4.0;
            double area = rectArea + circArea;

            // Perimeter in inches
            double perim = 2.0 * (a - b) + Math.PI * b;

            if (area <= 0 || perim <= 0) return 0;

            // Formula typically uses area and perimeter
            return 1.55 * Math.Pow(area, 0.625) / Math.Pow(perim, 0.25);
        }

        /// <summary>
        /// Solve velocity (FPM) given a target pressure drop (in.wg/100ft) and diameter (in).
        /// Iterative solution since f depends on Re which depends on V.
        /// </summary>
        public static double SolveVelocityFpm_FromDp(double diameterIn, double targetDpPer100, AirProperties air = null)
        {
            var props = air ?? AirAt(70, 0); // Standard air default
            if (diameterIn <= 0 || targetDpPer100 <= 0) return 0;

            // Initial guess: Assume rough turbulent (f constant approx 0.02)
            // dP = f * (L/D) * VP
            // dP = f * (100 / (D_in/12)) * rho * (V/1097)^2
            // V approx sqrt( dP / ( f * (1200/D_in) * rho ) ) * 1097
            // let f=0.02
            double vGuess = 1000.0; // Start somewhere

            for (int i = 0; i < 20; i++)
            {
                double re = Reynolds(vGuess, diameterIn, props);
                double f = FrictionFactor(re, diameterIn);
                double dp = DpPer100Ft_InWG(vGuess, diameterIn, f, props);

                if (Math.Abs(dp - targetDpPer100) < 1e-5) return vGuess;

                // Adjust guess - rudimentary secant or just Newton-like step?
                // Power law relationship is roughly dP ~ V^2 (turbulent)
                // So V_new = V_old * (target / current)^0.55 (approx 1/1.8 to 1/2)
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

            // Iterate on diameter
            double lo = 2.0;
            double hi = 100.0;

            // Bisection
            for (int i = 0; i < 50; i++)
            {
                double mid = 0.5 * (lo + hi);
                double area = Area_Round_Ft2(mid);
                double v = VelocityFpmFromCfmAndArea(cfm, area);
                double re = Reynolds(v, mid, props);
                double f = FrictionFactor(re, mid);
                double dp = DpPer100Ft_InWG(v, mid, f, props);

                if (Math.Abs(dp - targetDpPer100) < 1e-5) return mid;

                // If calc dp > target, we need larger duct to reduce velocity/friction
                if (dp > targetDpPer100)
                    lo = mid;
                else
                    hi = mid;
            }

            return 0.5 * (lo + hi);
        }

        // --- Helpers for Geometry Solving ---

        public static (double s1In, double s2In) RectangleFromAreaAndAR(double areaFt2, double ar)
        {
            if (areaFt2 <= 0 || ar <= 0) return (0, 0);
            // Area_in2 = s1 * s2 = s1 * (s1 / ar) = s1^2 / ar  (assuming s1 is long side)
            // or Area = w * h. Let s1/s2 = ar. s1 = ar*s2. Area = ar*s2^2. s2 = sqrt(Area/ar).
            double areaIn2 = areaFt2 * 144.0;
            double s2 = Math.Sqrt(areaIn2 / ar);
            double s1 = areaIn2 / s2;
            return (s1, s2);
        }

        // --- Pressure Class & Leakage ---

        public static double SelectSmacnaPressureClass(double staticPressureInWg)
        {
            // Standard classes: 0.5, 1, 2, 3, 4, 6, 10
            double absP = Math.Abs(staticPressureInWg);
            if (absP <= 0.5) return 0.5;
            if (absP <= 1.0) return 1.0;
            if (absP <= 2.0) return 2.0;
            if (absP <= 3.0) return 3.0;
            if (absP <= 4.0) return 4.0;
            if (absP <= 6.0) return 6.0;
            if (absP <= 10.0) return 10.0;
            return Math.Ceiling(absP); // Custom beyond 10?
        }

        public static double LeakageCfm(double leakageClass, double staticPressureInWg, double surfaceAreaFt2)
        {
            // Leakage Class (Cl) = F / P^0.65  where F is flow per 100ft2
            // F = Cl * P^0.65
            // Q_leak = F * (Area / 100)
            if (leakageClass <= 0 || staticPressureInWg <= 0 || surfaceAreaFt2 <= 0) return 0;
            double flowPer100 = leakageClass * Math.Pow(staticPressureInWg, 0.65);
            return flowPer100 * (surfaceAreaFt2 / 100.0);
        }

        public static double SurfaceAreaFromPerimeter(double perimeterFt, double lengthFt)
        {
            return perimeterFt * lengthFt;
        }

        // --- Heat Transfer & Fan BHP ---

        public static double FanBrakeHorsepower(double cfm, double totalPressureInWg, double fanEfficiency)
        {
            if (cfm <= 0 || totalPressureInWg <= 0 || fanEfficiency <= 0) return 0;
            // bhp = (CFM * TP) / (6356 * eff)
            return (cfm * totalPressureInWg) / (6356.0 * fanEfficiency);
        }

        public static double TotalPressureDrop_InWG(double frictionPer100, double runLength, double sumLossCoeffs, double velocityFpm, AirProperties air)
        {
            double frictionTotal = frictionPer100 * (runLength / 100.0);
            double vp = VelocityPressure_InWG(velocityFpm, air);
            double dynamicLoss = sumLossCoeffs * vp;
            return frictionTotal + dynamicLoss;
        }

        public static double HeatTransfer_Btuh(double uValue, double surfaceAreaFt2, double deltaTemp)
        {
            // Q = U * A * dT
            return uValue * surfaceAreaFt2 * deltaTemp;
        }

        public static double AirTemperatureChangeFromHeat(double qBtuh, double cfm, AirProperties air)
        {
            // Q = 1.08 * CFM * dT (standard air)
            // Generalized: Q = 60 * rho * CFM * Cp * dT
            // Cp air ~ 0.24 Btu/lb·F
            if (cfm <= 0) return 0;
            double mDot = cfm * 60.0 * air.DensityLbmPerFt3; // lb/hr
            double cp = 0.24;
            return qBtuh / (mDot * cp);
        }

        /// <summary>
        /// Equal-friction rectangle for a given round diameter.
        /// </summary>
        public static (double Side1In, double Side2In) EqualFrictionRectangleForRound(double diameterIn, double ar)
        {
            // Huebscher inverse is hard. Use iterative approach matching Area/Perim effective D.
            // Or simpler: iterate side2, calc side1=ar*side2, calc De, match D.

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
            // q = m * cp * maxDeltaT
            // q = U * A * ambientDelta
            // U = m * cp * maxDeltaT / (A * ambientDelta)
            // R_total = 1/U
            // R_insul = R_total - films

            double mDot = cfm * 60.0 * air.DensityLbmPerFt3;
            double cp = 0.24;
            double qLimit = Math.Abs(mDot * cp * maxDeltaT);

            if (qLimit <= 0 || surfaceAreaFt2 <= 0 || Math.Abs(ambientDelta) <= 0.1) return 0;

            // If ambientDelta is smaller than maxDeltaT, heat transfer is low anyway?
            // Check logic: if ambient is 80, supply 55, delta 25.

            double requiredU = qLimit / (surfaceAreaFt2 * Math.Abs(ambientDelta));
            if (requiredU <= 0) return 0;

            double rTotal = 1.0 / requiredU;
            double films = 0.61 + 0.17;
            double rInsul = rTotal - films;
            return rInsul > 0 ? rInsul : 0;
        }

        public static double InsulationThicknessInFromR(double rValue)
        {
            // Approx k=0.25 to 0.30 for duct wrap. Say 0.27 Btu·in/(hr·ft²·°F) -> R per inch ~ 3.7
            // R = thick / k_eff  => thick = R * k_eff ?? No R = thick/k.
            // R-value usually given per inch is like R-4.2 for 1.5"? No typical is R-6 for 2".
            // R ~ 3.0 per inch.
            if (rValue <= 0) return 0;
            return rValue / 3.0; // Rough estimate
        }
    }
}
