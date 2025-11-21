using System;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Core duct sizing and air-data math (imperial).
    /// Equations follow ASHRAE/SMACNA friction-chart practice for
    /// galvanized steel ducts carrying standard air at 70 °F and 1 atm.
    /// </summary>
    public static class DuctCalculator
    {
        // === Air properties & constants ===

        // ASHRAE "standard air" density ~0.075 lbm/ft^3
        private const double AirDensity_LbmPerFt3 = 0.075;

        // 1 slug = 32.174 lbm
        private const double LbmPerSlug = 32.174;

        // Density in slug/ft^3 for use in Newton's second law
        private static readonly double AirDensity_SlugPerFt3 =
            AirDensity_LbmPerFt3 / LbmPerSlug;

        // Kinematic viscosity ν [ft^2/s] at room temperature (ASHRAE tables)
        private const double KinematicViscosity_Ft2PerS = 1.57e-4;

        // Absolute roughness for galvanized steel duct (ASHRAE Fundamentals):
        // ε ≈ 0.0003 ft (medium-smooth galvanized steel).
        private const double Roughness_Ft = 0.0003;

        private const double InPerFt = 12.0;
        private const double FtPer100Ft = 100.0;
        private const double Pi = Math.PI;

        // 1 in. water column ≈ 5.20233 lb/ft^2 (62.42796 lb/ft^3 / 12 in/ft)
        private const double LbPerFt2_Per_InWG = 5.20233;

        // === Basic geometry ===

        public static double Area_Round_Ft2(double diameterIn)
        {
            if (diameterIn <= 0) return 0;

            double dFt = diameterIn / InPerFt;
            return Pi * dFt * dFt / 4.0;
        }

        public static double Circumference_Round_Ft(double diameterIn)
        {
            if (diameterIn <= 0) return 0;

            double dFt = diameterIn / InPerFt;
            return Pi * dFt;
        }

        /// <summary>
        /// Rectangle geometry from sides (inches).
        /// Returns (area ft², perimeter ft, hydraulic diameter ft).
        /// </summary>
        public static (double AreaFt2, double PerimeterFt, double HydraulicDiameterFt)
            RectGeometry(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return (0, 0, 0);

            double a = side1In / InPerFt;
            double b = side2In / InPerFt;

            double area = a * b;
            double perimeter = 2.0 * (a + b);
            double dh = 4.0 * area / perimeter;

            return (area, perimeter, dh);
        }

        /// <summary>
        /// Flat-oval geometry using A and P relationships from SMACNA / ASHRAE.
        /// aminorIn = minor axis (in), amajorIn = major axis (in).
        /// Returns (area ft², perimeter ft, hydraulic diameter ft).
        /// </summary>
        public static (double AreaFt2, double PerimeterFt, double HydraulicDiameterFt)
            FlatOvalGeometry(double aminorIn, double amajorIn)
        {
            if (aminorIn <= 0 || amajorIn <= 0)
                return (0, 0, 0);

            // Area and perimeter in inches:
            // A = π a^2 / 4 + a (A - a)
            double A_in2 = Pi * aminorIn * aminorIn / 4.0 +
                           aminorIn * (amajorIn - aminorIn);

            // P = π a + 2 (A - a)
            double P_in = Pi * aminorIn + 2.0 * (amajorIn - aminorIn);

            double areaFt2 = A_in2 / (InPerFt * InPerFt);
            double perimeterFt = P_in / InPerFt;

            double dhFt = perimeterFt > 0 ? 4.0 * areaFt2 / perimeterFt : 0;

            return (areaFt2, perimeterFt, dhFt);
        }

        // === Equivalent round diameters ===

        /// <summary>
        /// Equivalent round diameter De (in) for rectangular duct.
        /// Huebscher equal-friction equation:
        /// De = 1.30 (ab)^0.625 / (a + b)^0.25 where a,b in inches.
        /// </summary>
        public static double EquivalentRound_Rect(double side1In, double side2In)
        {
            if (side1In <= 0 || side2In <= 0) return 0;

            double a = side1In;
            double b = side2In;
            double ab = a * b;

            return 1.30 * Math.Pow(ab, 0.625) / Math.Pow(a + b, 0.25);
        }

        /// <summary>
        /// Equivalent round diameter De (in) for flat-oval duct.
        /// ASHRAE / SMACNA form: De = 1.55 A^0.625 / P^0.25 with A [in^2], P [in].
        /// </summary>
        public static double EquivalentRound_FlatOval(double aminorIn, double amajorIn)
        {
            if (aminorIn <= 0 || amajorIn <= 0) return 0;

            double A = Pi * aminorIn * aminorIn / 4.0 +
                       aminorIn * (amajorIn - aminorIn);
            double P = Pi * aminorIn + 2.0 * (amajorIn - aminorIn);

            return 1.55 * Math.Pow(A, 0.625) / Math.Pow(P, 0.25);
        }

        /// <summary>
        /// Equal-friction rectangular equivalent for a given round diameter
        /// and target aspect ratio (long/short >= 1).
        /// Returns (side1In, side2In) with side1 >= side2.
        /// </summary>
        public static (double Side1In, double Side2In)
            EqualFrictionRectangleForRound(double roundDiaIn, double aspectRatio)
        {
            if (roundDiaIn <= 0 || aspectRatio <= 0)
                return (0, 0);

            double R = aspectRatio >= 1.0 ? aspectRatio : 1.0 / aspectRatio;
            double targetD = roundDiaIn;

            double Func(double bIn)
            {
                double aIn = R * bIn;
                double De = EquivalentRound_Rect(aIn, bIn);
                return De - targetD;
            }

            double lo = 0.5;
            double hi = 10.0 * roundDiaIn;

            double fLo = Func(lo);
            double fHi = Func(hi);

            // Try to bracket the root
            for (int i = 0; i < 100 && fLo * fHi > 0; i++)
            {
                lo /= 2.0;
                hi *= 2.0;
                fLo = Func(lo);
                fHi = Func(hi);
            }

            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = Func(mid);

                if (Math.Abs(fMid) < 1e-6)
                {
                    lo = hi = mid;
                    break;
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

            double side2 = 0.5 * (lo + hi);
            double side1 = R * side2;

            if (side1 < side2)
            {
                double t = side1;
                side1 = side2;
                side2 = t;
            }

            return (side1, side2);
        }

        /// <summary>
        /// Equal-friction flat-oval equivalent for a given round diameter
        /// and target aspect ratio amajor/aminor >= 1.
        /// Returns (amajorIn, aminorIn) with amajor >= aminor.
        /// </summary>
        public static (double MajorIn, double MinorIn)
            EqualFrictionFlatOvalForRound(double roundDiaIn, double aspectRatio)
        {
            if (roundDiaIn <= 0 || aspectRatio <= 0)
                return (0, 0);

            double R = aspectRatio >= 1.0 ? aspectRatio : 1.0 / aspectRatio;
            double targetD = roundDiaIn;

            double Func(double aminorIn)
            {
                double amajorIn = R * aminorIn;
                double De = EquivalentRound_FlatOval(aminorIn, amajorIn);
                return De - targetD;
            }

            double lo = 0.5;
            double hi = 10.0 * roundDiaIn;

            double fLo = Func(lo);
            double fHi = Func(hi);

            for (int i = 0; i < 100 && fLo * fHi > 0; i++)
            {
                lo /= 2.0;
                hi *= 2.0;
                fLo = Func(lo);
                fHi = Func(hi);
            }

            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fMid = Func(mid);

                if (Math.Abs(fMid) < 1e-6)
                {
                    lo = hi = mid;
                    break;
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

            double aminor = 0.5 * (lo + hi);
            double amajor = R * aminor;

            if (amajor < aminor)
            {
                double t = amajor;
                amajor = aminor;
                aminor = t;
            }

            return (amajor, aminor);
        }

        // === Flow properties & friction ===

        public static double VelocityFpmFromCfmAndArea(double cfm, double areaFt2)
        {
            if (areaFt2 <= 0) return 0;
            return cfm / areaFt2;
        }

        /// <summary>
        /// Reynolds number using hydraulic diameter (inches) and velocity (FPM).
        /// Re = V * D / ν, with V in ft/s, D in ft, ν in ft²/s.
        /// </summary>
        public static double Reynolds(double velocityFpm, double hydraulicDiameterIn)
        {
            double vFtPerS = velocityFpm / 60.0;
            double dFt = hydraulicDiameterIn / InPerFt;

            if (KinematicViscosity_Ft2PerS <= 0 || dFt <= 0 || vFtPerS <= 0)
                return 0;

            return (vFtPerS * dFt) / KinematicViscosity_Ft2PerS;
        }

        /// <summary>
        /// Friction factor using Churchill correlation to remain valid
        /// for laminar, transitional, and turbulent regimes.
        /// This avoids clamping Re and remains consistent with ASHRAE charts.
        /// </summary>
        public static double FrictionFactor(double reynolds, double hydraulicDiameterIn)
        {
            double re = Math.Max(reynolds, 1.0);
            double dFt = hydraulicDiameterIn / InPerFt;
            if (dFt <= 0) return 0;

            double term1 = Math.Pow(8.0 / re, 12.0);
            double A = Math.Pow(2.457 * Math.Log(1.0 / Math.Pow((Roughness_Ft / (3.7 * dFt)) + (7.0 / re), 0.9)), 16.0);
            double B = Math.Pow(37530.0 / re, 16.0);
            double term2 = Math.Pow(1.0 / (A + B), 1.5);

            double frictionDarcy = 8.0 * Math.Pow(term1 + term2, 1.0 / 12.0);
            return frictionDarcy;
        }

        /// <summary>
        /// Darcy–Weisbach: ΔP/L = f * (ρ V² / (2 D_h))  [lb/ft² per ft].
        /// Returns ΔP per 100 ft in inches of water column.
        /// </summary>
        public static double DpPer100Ft_InWG(double velocityFpm,
                                             double hydraulicDiameterIn,
                                             double frictionFactor)
        {
            double vFtPerS = velocityFpm / 60.0;
            double dFt = hydraulicDiameterIn / InPerFt;

            if (dFt <= 0 || frictionFactor <= 0 || vFtPerS <= 0) return 0;

            double dpPerFt_LbPerFt2 =
                frictionFactor * (AirDensity_SlugPerFt3 * vFtPerS * vFtPerS / (2.0 * dFt));

            double dpPer100Ft_LbPerFt2 = dpPerFt_LbPerFt2 * FtPer100Ft;

            double dpPer100Ft_InWG = dpPer100Ft_LbPerFt2 / LbPerFt2_Per_InWG;
            return dpPer100Ft_InWG;
        }

        /// <summary>
        /// Solve round diameter (in) for target CFM and friction rate (in.w.g./100 ft)
        /// using bisection on Darcy–Weisbach + Churchill friction factor.
        /// </summary>
        public static double SolveRoundDiameter_FromCfmAndFriction(double cfm,
                                                                   double targetDpPer100Ft_InWG)
        {
            if (cfm <= 0 || targetDpPer100Ft_InWG <= 0) return 0;

            double lo = 2.0;   // inches
            double hi = 120.0; // inches

            double Fn(double dIn)
            {
                double area = Area_Round_Ft2(dIn);
                double vel = VelocityFpmFromCfmAndArea(cfm, area);
                double re = Reynolds(vel, dIn);
                double f = FrictionFactor(re, dIn);
                double dp = DpPer100Ft_InWG(vel, dIn, f);
                return dp - targetDpPer100Ft_InWG;
            }

            double fLo = Fn(lo);
            double fHi = Fn(hi);

            // Try to bracket a root
            for (int i = 0; i < 20 && fLo * fHi > 0; i++)
            {
                lo = Math.Max(1.0, lo / 1.5);
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

        /// <summary>
        /// Solve velocity (FPM) for a given hydraulic diameter (in)
        /// and friction rate (in.w.g./100 ft).
        ///
        /// This is used for reverse-calculating airflow from known
        /// duct size and friction rate (no CFM or velocity given).
        /// </summary>
        public static double SolveVelocityFpm_FromDp(double hydraulicDiameterIn,
                                                     double targetDpPer100Ft_InWG)
        {
            if (hydraulicDiameterIn <= 0 || targetDpPer100Ft_InWG <= 0)
                return 0;

            double lo = 100.0;   // FPM
            double hi = 8000.0;  // FPM

            double Fn(double vFpm)
            {
                double re = Reynolds(vFpm, hydraulicDiameterIn);
                double f = FrictionFactor(re, hydraulicDiameterIn);
                double dp = DpPer100Ft_InWG(vFpm, hydraulicDiameterIn, f);
                return dp - targetDpPer100Ft_InWG;
            }

            double fLo = Fn(lo);
            double fHi = Fn(hi);

            // Try to bracket
            for (int i = 0; i < 20 && fLo * fHi > 0; i++)
            {
                lo = Math.Max(10.0, lo / 1.5);
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

        /// <summary>
        /// Compute rectangular sides from area ft² and aspect ratio AR = long/short.
        /// Returns (side1In, side2In) with side1 ≥ side2.
        /// </summary>
        public static (double s1In, double s2In) RectangleFromAreaAndAR(double areaFt2,
                                                                        double aspectRatio)
        {
            if (areaFt2 <= 0 || aspectRatio <= 0) return (0, 0);

            double R = aspectRatio >= 1.0 ? aspectRatio : 1.0 / aspectRatio;

            // Let s2 = x (ft), s1 = R * x (ft), area = R x² → x = sqrt(area/R)
            double xFt = Math.Sqrt(areaFt2 / R);
            double s2In = xFt * InPerFt;
            double s1In = R * xFt * InPerFt;

            return (s1In, s2In);
        }
    }
}
