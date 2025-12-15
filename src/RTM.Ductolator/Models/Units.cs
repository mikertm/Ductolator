using System;
using System.Globalization;

namespace RTM.Ductolator.Models
{
    /// <summary>
    /// Central authority for unit conversions and formatting.
    /// Internal Base System: Imperial (IP) -> ft, s, lbm, °F.
    /// All calculations should attempt to normalize to this base before computing,
    /// then convert back to display units (in, gpm, cfm, etc.) for output.
    /// </summary>
    public static class Units
    {
        // === Length ===
        public const double InchesPerFoot = 12.0;
        public const double FeetPerInch = 1.0 / 12.0;

        // === Area ===
        public const double SqInchesPerSqFoot = 144.0;
        public const double SqFeetPerSqInch = 1.0 / 144.0;

        // === Volume / Flow ===
        public const double GallonsPerCubicFoot = 7.48052;
        public const double CubicFeetPerGallon = 1.0 / 7.48052;
        public const double GpmToCfs = CubicFeetPerGallon / 60.0; // ~ 0.002228
        public const double CfsToGpm = 1.0 / GpmToCfs;            // ~ 448.831

        // === Pressure ===
        // Standard water density at 60F is approx 62.37 lbm/ft^3, often simplified to 62.4.
        // 1 psi = 144 psf.
        public const double PsfPerPsi = 144.0;
        public const double PsiPerPsf = 1.0 / 144.0;

        // 1 in.w.g. (60F) = 5.2023 lbf/ft^2 (psf)
        // Historically 5.202 or 5.2.
        public const double PsfPerInWg = 5.2023; // Adjusted to standard 60F value
        public const double InWgPerPsf = 1.0 / PsfPerInWg;

        // Consistent conversion: InWgPerPsi = InWgPerPsf / PsiPerPsf = (1/5.2023) / (1/144) = 144 / 5.2023
        public const double InWgPerPsi = PsfPerPsi / PsfPerInWg; // ~27.679

        // === Mass / Force ===
        // Standard Gravity g_c
        public const double Gc = 32.174; // lbm·ft / (lbf·s²)

        // === Temperature ===
        public const double RankineZeroF = 459.67;

        // === Time ===
        public const double SecondsPerMinute = 60.0;
        public const double MinutesPerHour = 60.0;

        // === Helpers ===

        public static double FromInchesToFeet(double inches) => inches / InchesPerFoot;
        public static double FromFeetToInches(double feet) => feet * InchesPerFoot;

        public static double FromSqInchesToSqFeet(double sqInches) => sqInches / SqInchesPerSqFoot;
        public static double FromSqFeetToSqInches(double sqFeet) => sqFeet * SqInchesPerSqFoot;

        public static double FromGpmToCfs(double gpm) => gpm * GpmToCfs;
        public static double FromCfsToGpm(double cfs) => cfs * CfsToGpm;

        public static double FromCfmToCfs(double cfm) => cfm / SecondsPerMinute;
        public static double FromCfsToCfm(double cfs) => cfs * SecondsPerMinute;

        public static double FromFpmToFps(double fpm) => fpm / SecondsPerMinute;
        public static double FromFpsToFpm(double fps) => fps * SecondsPerMinute;

        /// <summary>
        /// Formats a double using InvariantCulture.
        /// </summary>
        public static string Format(double value, string format = "0.##")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
