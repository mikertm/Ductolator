using System;

namespace RTM.Ductolator.Models
{
    public record AirState(
        double DryBulbF,
        double WetBulbF,
        double HumidityRatio,
        double EnthalpyBtuPerLb,
        double DewPointF,
        double DensityLbPerFt3,
        double SpecificVolumeFt3PerLb)
    {
        public double HumidityRatioGrainsPerLb => HumidityRatio * 7000.0;
    }

    public record MixedAirResult(
        double TotalCfm,
        double OutsideAirCfm,
        double ReturnAirCfm,
        double OutsideAirPercent,
        AirState OutdoorAir,
        AirState ReturnAir,
        AirState MixedAir,
        double MixedAirMassFlowLbPerHr);

    public static class MixedAirCalculator
    {
        public static MixedAirResult Calculate(
            double? totalCfm,
            double? outsideAirCfm,
            double? returnAirCfm,
            double? outsideAirPercent,
            double outdoorDbF,
            double outdoorWbF,
            double returnDbF,
            double returnWbF,
            double altitudeFt = 0)
        {
            ValidateTemperatures(outdoorDbF, outdoorWbF, nameof(outdoorDbF), nameof(outdoorWbF));
            ValidateTemperatures(returnDbF, returnWbF, nameof(returnDbF), nameof(returnWbF));

            if (outsideAirPercent.HasValue && (outsideAirPercent.Value < 0 || outsideAirPercent.Value > 100))
                throw new InvalidOperationException("Outside air percent must be between 0 and 100.");

            var flows = SolveAirflows(totalCfm, outsideAirCfm, returnAirCfm, outsideAirPercent);
            double pressureKpa = PressureAtAltitudeKpa(altitudeFt);

            var outdoor = BuildState(outdoorDbF, outdoorWbF, pressureKpa);
            var indoor = BuildState(returnDbF, returnWbF, pressureKpa);

            double outdoorMassLbPerS = MassFlowLbPerSecond(flows.OutsideAirCfm, outdoor.DensityLbPerFt3);
            double returnMassLbPerS = MassFlowLbPerSecond(flows.ReturnAirCfm, indoor.DensityLbPerFt3);
            double totalMassLbPerS = outdoorMassLbPerS + returnMassLbPerS;

            if (totalMassLbPerS <= 0)
                throw new InvalidOperationException("Calculated mass flow was zero; check airflow inputs.");

            double mixedHumidityRatio = WeightedAverage(outdoor.HumidityRatio, indoor.HumidityRatio, outdoorMassLbPerS, returnMassLbPerS);
            double mixedEnthalpy = WeightedAverage(outdoor.EnthalpyBtuPerLb, indoor.EnthalpyBtuPerLb, outdoorMassLbPerS, returnMassLbPerS);
            double mixedDryBulbC = DryBulbFromEnthalpyAndHumidityRatio(mixedEnthalpy, mixedHumidityRatio);
            double saturationHumidityRatio = SaturationHumidityRatio(mixedDryBulbC, pressureKpa);
            if (mixedHumidityRatio > saturationHumidityRatio)
            {
                throw new InvalidOperationException("Inputs imply supersaturated mixed air. Check DB/WB pairs and airflow fractions.");
            }
            double mixedWetBulbC = WetBulbFromRatio(mixedDryBulbC, mixedHumidityRatio, pressureKpa);
            double mixedDewPointC = DewPointFromHumidityRatio(mixedHumidityRatio, pressureKpa);
            double mixedDensity = DensityLbPerFt3(mixedDryBulbC, mixedHumidityRatio, pressureKpa);

            var mixedState = new AirState(
                DryBulbToF(mixedDryBulbC),
                DryBulbToF(mixedWetBulbC),
                mixedHumidityRatio,
                mixedEnthalpy,
                DryBulbToF(mixedDewPointC),
                mixedDensity,
                mixedDensity > 0 ? 1.0 / mixedDensity : 0);

            return new MixedAirResult(
                flows.TotalCfm,
                flows.OutsideAirCfm,
                flows.ReturnAirCfm,
                flows.OutsideAirPercent,
                outdoor,
                indoor,
                mixedState,
                totalMassLbPerS * 3600.0);
        }

        private static (double TotalCfm, double OutsideAirCfm, double ReturnAirCfm, double OutsideAirPercent) SolveAirflows(
            double? totalCfm,
            double? outsideAirCfm,
            double? returnAirCfm,
            double? outsideAirPercent)
        {
            double? total = totalCfm > 0 ? totalCfm : null;
            double? oa = outsideAirCfm >= 0 ? outsideAirCfm : null;
            double? ra = returnAirCfm >= 0 ? returnAirCfm : null;
            double? oaPct = outsideAirPercent >= 0 ? outsideAirPercent : null;

            int providedCount = 0;
            if (total.HasValue) providedCount++;
            if (oa.HasValue) providedCount++;
            if (ra.HasValue) providedCount++;
            if (oaPct.HasValue) providedCount++;

            if (providedCount < 2)
                throw new InvalidOperationException("Provide any two of total CFM, OA CFM, RA CFM, or OA percent to solve airflow.");

            double resolvedTotal;
            double resolvedOa;
            double resolvedRa;
            double resolvedPct;

            if (total.HasValue && oa.HasValue)
            {
                resolvedTotal = total.Value;
                resolvedOa = oa.Value;
                resolvedRa = resolvedTotal - resolvedOa;
                resolvedPct = resolvedTotal > 0 ? resolvedOa / resolvedTotal * 100.0 : 0;
            }
            else if (total.HasValue && ra.HasValue)
            {
                resolvedTotal = total.Value;
                resolvedRa = ra.Value;
                resolvedOa = resolvedTotal - resolvedRa;
                resolvedPct = resolvedTotal > 0 ? resolvedOa / resolvedTotal * 100.0 : 0;
            }
            else if (total.HasValue && oaPct.HasValue)
            {
                resolvedTotal = total.Value;
                resolvedPct = oaPct.Value;
                resolvedOa = resolvedTotal * resolvedPct / 100.0;
                resolvedRa = resolvedTotal - resolvedOa;
            }
            else if (oa.HasValue && ra.HasValue)
            {
                resolvedOa = oa.Value;
                resolvedRa = ra.Value;
                resolvedTotal = resolvedOa + resolvedRa;
                resolvedPct = resolvedTotal > 0 ? resolvedOa / resolvedTotal * 100.0 : 0;
            }
            else if (oa.HasValue && oaPct.HasValue)
            {
                resolvedOa = oa.Value;
                resolvedPct = oaPct.Value;
                resolvedTotal = resolvedPct > 0 ? resolvedOa / (resolvedPct / 100.0) : resolvedOa;
                resolvedRa = resolvedTotal - resolvedOa;
            }
            else if (ra.HasValue && oaPct.HasValue)
            {
                resolvedRa = ra.Value;
                resolvedPct = oaPct.Value;
                resolvedTotal = 100.0 - resolvedPct <= 0 ? resolvedRa : resolvedRa / ((100.0 - resolvedPct) / 100.0);
                resolvedOa = resolvedTotal - resolvedRa;
            }
            else
            {
                throw new InvalidOperationException("Unable to solve airflow combination.");
            }

            if (resolvedTotal <= 0)
                throw new InvalidOperationException("Total airflow must be greater than zero.");

            if (resolvedOa < 0 || resolvedRa < 0)
                throw new InvalidOperationException("Calculated outside or return air CFM was negative. Check the provided combination.");

            if (resolvedPct < 0 || resolvedPct > 100)
                throw new InvalidOperationException("Calculated outside air percent landed outside 0-100%. Verify airflow inputs.");

            return (resolvedTotal, resolvedOa, resolvedRa, resolvedPct);
        }

        private static AirState BuildState(double dryBulbF, double wetBulbF, double pressureKpa)
        {
            double dryBulbC = DryBulbToC(dryBulbF);
            double wetBulbC = DryBulbToC(wetBulbF);
            double humidityRatio = HumidityRatioFromWetBulb(dryBulbC, wetBulbC, pressureKpa);
            double dewPointC = DewPointFromHumidityRatio(humidityRatio, pressureKpa);
            double enthalpy = EnthalpyBtuPerLb(dryBulbC, humidityRatio);
            double density = DensityLbPerFt3(dryBulbC, humidityRatio, pressureKpa);

            return new AirState(
                dryBulbF,
                wetBulbF,
                humidityRatio,
                enthalpy,
                DryBulbToF(dewPointC),
                density,
                density > 0 ? 1.0 / density : 0);
        }

        private static double PressureAtAltitudeKpa(double altitudeFt)
        {
            double altitudeMeters = altitudeFt * 0.3048;
            return 101.325 * Math.Pow(1 - 2.25577e-5 * altitudeMeters, 5.2559);
        }

        private static double HumidityRatioFromWetBulb(double dryBulbC, double wetBulbC, double pressureKpa)
        {
            double pws = SaturationPressureKpa(wetBulbC);
            double ws = 0.621945 * pws / Math.Max(pressureKpa - pws, 0.0001);
            double numerator = (2501 - 2.381 * wetBulbC) * ws - 1.006 * (dryBulbC - wetBulbC);
            double denominator = 2501 + 1.86 * dryBulbC - 4.186 * wetBulbC;
            double w = numerator / Math.Max(denominator, 0.0001);
            return Math.Max(w, 0);
        }

        private static double SaturationPressureKpa(double tempC)
        {
            return 0.61078 * Math.Exp((17.2694 * tempC) / (tempC + 237.3));
        }

        private static double DewPointFromHumidityRatio(double humidityRatio, double pressureKpa)
        {
            double vaporPressure = pressureKpa * humidityRatio / (0.621945 + humidityRatio);
            vaporPressure = Math.Max(vaporPressure, 1e-6);
            double lnRatio = Math.Log(vaporPressure / 0.61078);
            double dewPointC = (237.3 * lnRatio) / (17.2694 - lnRatio);
            return dewPointC;
        }

        private static double EnthalpyBtuPerLb(double dryBulbC, double humidityRatio)
        {
            double enthalpyKjPerKg = 1.006 * dryBulbC + humidityRatio * (2501 + 1.86 * dryBulbC);
            return enthalpyKjPerKg * 0.429922614; // kJ/kg to Btu/lbm
        }

        private static double DryBulbFromEnthalpyAndHumidityRatio(double enthalpyBtuPerLb, double humidityRatio)
        {
            double enthalpyKjPerKg = enthalpyBtuPerLb / 0.429922614;
            double numerator = enthalpyKjPerKg - 2501 * humidityRatio;
            double denominator = 1.006 + 1.86 * humidityRatio;
            return numerator / Math.Max(denominator, 0.0001);
        }

        private static double DensityLbPerFt3(double dryBulbC, double humidityRatio, double pressureKpa)
        {
            double temperatureK = dryBulbC + 273.15;
            double pressurePa = pressureKpa * 1000.0;
            double densityKgPerM3 = pressurePa / (287.042 * temperatureK * (1 + 1.607858 * humidityRatio));
            return densityKgPerM3 * 0.062428;
        }

        private static double WetBulbFromRatio(double dryBulbC, double humidityRatio, double pressureKpa)
        {
            double upper = dryBulbC;
            double lower = dryBulbC - 50.0;

            for (int i = 0; i < 50; i++)
            {
                double mid = 0.5 * (upper + lower);
                double wAtMid = HumidityRatioFromWetBulb(dryBulbC, mid, pressureKpa);
                if (wAtMid > humidityRatio)
                {
                    upper = mid;
                }
                else
                {
                    lower = mid;
                }
            }

            return 0.5 * (upper + lower);
        }

        private static double WeightedAverage(double v1, double v2, double w1, double w2)
        {
            return (v1 * w1 + v2 * w2) / Math.Max(w1 + w2, 1e-6);
        }

        private static double MassFlowLbPerSecond(double cfm, double densityLbPerFt3)
        {
            return densityLbPerFt3 * cfm / 60.0;
        }

        private static double DryBulbToC(double dryBulbF) => (dryBulbF - 32.0) * (5.0 / 9.0);

        private static double DryBulbToF(double dryBulbC) => dryBulbC * 9.0 / 5.0 + 32.0;

        private static void ValidateTemperatures(double dryBulbF, double wetBulbF, string dryLabel, string wetLabel)
        {
            if (double.IsNaN(dryBulbF) || double.IsInfinity(dryBulbF) ||
                double.IsNaN(wetBulbF) || double.IsInfinity(wetBulbF))
            {
                throw new InvalidOperationException("Temperatures must be valid numbers.");
            }

            if (wetBulbF > dryBulbF + 0.01)
            {
                throw new InvalidOperationException($"{wetLabel} cannot exceed {dryLabel}. Provide a realistic DB/WB pair.");
            }
        }

        private static double SaturationHumidityRatio(double dryBulbC, double pressureKpa)
        {
            double pws = SaturationPressureKpa(dryBulbC);
            return 0.621945 * pws / Math.Max(pressureKpa - pws, 0.0001);
        }
    }
}
