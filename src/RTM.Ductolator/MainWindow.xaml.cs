using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RTM.Ductolator.Models;

namespace RTM.Ductolator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // --- Helpers ---

        private static double ParseBox(TextBox tb)
        {
            if (tb == null) return 0;

            return double.TryParse(tb.Text,
                                   NumberStyles.Float,
                                   CultureInfo.InvariantCulture,
                                   out double value)
                ? value
                : 0;
        }

        private static void SetBox(TextBox tb, double value, string format)
        {
            if (tb == null)
                return;

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                tb.Text = string.Empty;
                return;
            }

            tb.Text = value.ToString(format, CultureInfo.InvariantCulture);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var ctrl in new TextBox[]
            {
                InCfm, InDp100, InVel, InDia, InS1, InS2, InAR,
                OutRe, OutF, OutCfm, OutVel, OutDp100,
                OutDia, OutAreaRound, OutCircRound,
                OutRS1, OutRS2, OutRAR, OutRArea, OutRPerim,
                OutOS1, OutOS2, OutOAR, OutOArea, OutOPerim
            })
            {
                ctrl.Text = string.Empty;
            }
        }

        private void BtnCalc_Click(object sender, RoutedEventArgs e)
        {
            // --- Read inputs ---
            double cfm = ParseBox(InCfm);
            double dp100Input = ParseBox(InDp100); // in.w.g./100ft
            double velInput = ParseBox(InVel);     // FPM
            double diaIn = ParseBox(InDia);        // inches
            double s1In = ParseBox(InS1);
            double s2In = ParseBox(InS2);
            double arInput = ParseBox(InAR);

            double targetAR = arInput > 0 ? arInput : 2.0;

            // Working variables
            double areaFt2 = 0;
            double perimFt = 0;
            double dhIn = 0;
            double usedVelFpm = velInput;
            double primaryRoundDiaIn = 0;

            // Clear geometry outputs first
            OutDia.Text = OutAreaRound.Text = OutCircRound.Text = string.Empty;
            OutRS1.Text = OutRS2.Text = OutRAR.Text = OutRArea.Text = OutRPerim.Text = string.Empty;
            OutOS1.Text = OutOS2.Text = OutOAR.Text = OutOArea.Text = OutOPerim.Text = string.Empty;

            // === Determine primary geometry and primary flow inputs ===
            // Priority order:
            //  1) Diameter given (round primary).
            //  2) Rect sides given (rect primary).
            //  3) CFM + Vel + AR (synthetic rect).
            //  4) CFM + dP/100ft (size round).
            // For 1 & 2 we also support reverse-solve from friction.

            if (diaIn > 0)
            {
                // --- Case 1: Round primary ---
                primaryRoundDiaIn = diaIn;
                dhIn = diaIn;

                areaFt2 = DuctCalculator.Area_Round_Ft2(diaIn);
                perimFt = DuctCalculator.Circumference_Round_Ft(diaIn);

                if (velInput > 0)
                {
                    usedVelFpm = velInput;
                    if (cfm <= 0 && areaFt2 > 0)
                        cfm = areaFt2 * usedVelFpm;
                }
                else if (cfm > 0 && areaFt2 > 0)
                {
                    usedVelFpm = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, areaFt2);
                }
                else if (dp100Input > 0)
                {
                    // Reverse: diameter + dp/100 → velocity + CFM
                    usedVelFpm = DuctCalculator.SolveVelocityFpm_FromDp(dhIn, dp100Input);
                    if (areaFt2 > 0 && usedVelFpm > 0)
                        cfm = areaFt2 * usedVelFpm;
                }
                else
                {
                    MessageBox.Show(
                        "For round ducts, provide at least one of:\n" +
                        "  • Flow (CFM)\n" +
                        "  • Velocity (FPM)\n" +
                        "  • dP per 100 ft",
                        "Inputs Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Round outputs
                SetBox(OutDia, diaIn, "0.##");
                SetBox(OutAreaRound, areaFt2, "0.000");
                SetBox(OutCircRound, perimFt, "0.000");
            }
            else if (s1In > 0 && s2In > 0)
            {
                // --- Case 2: Rectangular primary ---
                double longSide = Math.Max(s1In, s2In);
                double shortSide = Math.Min(s1In, s2In);

                var rectGeom = DuctCalculator.RectGeometry(longSide, shortSide);
                areaFt2 = rectGeom.AreaFt2;
                perimFt = rectGeom.PerimeterFt;

                // Equivalent round for friction
                primaryRoundDiaIn = DuctCalculator.EquivalentRound_Rect(longSide, shortSide);
                dhIn = primaryRoundDiaIn;

                if (velInput > 0)
                {
                    usedVelFpm = velInput;
                    if (cfm <= 0 && areaFt2 > 0)
                        cfm = areaFt2 * usedVelFpm;
                }
                else if (cfm > 0 && areaFt2 > 0)
                {
                    usedVelFpm = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, areaFt2);
                }
                else if (dp100Input > 0)
                {
                    // Reverse: rectangle + dp/100 → velocity + CFM
                    usedVelFpm = DuctCalculator.SolveVelocityFpm_FromDp(dhIn, dp100Input);
                    if (areaFt2 > 0 && usedVelFpm > 0)
                        cfm = areaFt2 * usedVelFpm;
                }
                else
                {
                    MessageBox.Show(
                        "For rectangular ducts, provide at least one of:\n" +
                        "  • Flow (CFM)\n" +
                        "  • Velocity (FPM)\n" +
                        "  • dP per 100 ft",
                        "Inputs Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                double arActual = longSide / shortSide;

                // Round group shows equivalent round
                SetBox(OutDia, primaryRoundDiaIn, "0.##");
                SetBox(OutAreaRound, areaFt2, "0.000");
                SetBox(OutCircRound, perimFt, "0.000");

                // Rectangle outputs echo the input rectangle
                SetBox(OutRS1, longSide, "0.##");
                SetBox(OutRS2, shortSide, "0.##");
                SetBox(OutRAR, arActual, "0.000");
                SetBox(OutRArea, areaFt2, "0.000");
                SetBox(OutRPerim, perimFt, "0.000");
            }
            else if (cfm > 0 && velInput > 0 && targetAR > 0)
            {
                // --- Case 3: CFM + Velocity + AR → synthetic rectangle + equivalent round ---
                usedVelFpm = velInput;
                areaFt2 = cfm / usedVelFpm;

                var rectSides = DuctCalculator.RectangleFromAreaAndAR(areaFt2, targetAR);
                double rs1 = rectSides.s1In;
                double rs2 = rectSides.s2In;

                double longSide = Math.Max(rs1, rs2);
                double shortSide = Math.Min(rs1, rs2);

                var rectGeom = DuctCalculator.RectGeometry(longSide, shortSide);
                areaFt2 = rectGeom.AreaFt2;
                perimFt = rectGeom.PerimeterFt;

                primaryRoundDiaIn = DuctCalculator.EquivalentRound_Rect(longSide, shortSide);
                dhIn = primaryRoundDiaIn;

                // Round outputs
                SetBox(OutDia, primaryRoundDiaIn, "0.##");
                SetBox(OutAreaRound, areaFt2, "0.000");
                SetBox(OutCircRound, perimFt, "0.000");

                // Rectangle outputs
                SetBox(OutRS1, longSide, "0.##");
                SetBox(OutRS2, shortSide, "0.##");
                SetBox(OutRAR, longSide / shortSide, "0.000");
                SetBox(OutRArea, areaFt2, "0.000");
                SetBox(OutRPerim, perimFt, "0.000");
            }
            else if (cfm > 0 && dp100Input > 0)
            {
                // --- Case 4: CFM + dP/100ft → solve round diameter ---
                primaryRoundDiaIn = DuctCalculator.SolveRoundDiameter_FromCfmAndFriction(cfm, dp100Input);
                dhIn = primaryRoundDiaIn;
                areaFt2 = DuctCalculator.Area_Round_Ft2(primaryRoundDiaIn);
                perimFt = DuctCalculator.Circumference_Round_Ft(primaryRoundDiaIn);
                usedVelFpm = DuctCalculator.VelocityFpmFromCfmAndArea(cfm, areaFt2);

                SetBox(OutDia, primaryRoundDiaIn, "0.##");
                SetBox(OutAreaRound, areaFt2, "0.000");
                SetBox(OutCircRound, perimFt, "0.000");
            }
            else
            {
                MessageBox.Show(
                    "Not enough inputs.\n\nProvide one of the following combinations:\n" +
                    "  • Diameter + (CFM or Velocity or dP/100ft)\n" +
                    "  • Rect sides + (CFM or Velocity or dP/100ft)\n" +
                    "  • CFM + Velocity + Aspect Ratio\n" +
                    "  • CFM + dP per 100 ft (round sizing).",
                    "Inputs Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // If CFM is still missing but we have geometry + velocity, back-calc it
            if (cfm <= 0 && areaFt2 > 0 && usedVelFpm > 0)
                cfm = areaFt2 * usedVelFpm;

            // === Air data (Re, f, dP/100ft) ===
            double re = DuctCalculator.Reynolds(usedVelFpm, dhIn);
            double f = DuctCalculator.FrictionFactor(re, dhIn);
            double dpPer100 = DuctCalculator.DpPer100Ft_InWG(usedVelFpm, dhIn, f);

            SetBox(OutRe, re, "0");
            SetBox(OutF, f, "0.0000");
            SetBox(OutCfm, cfm, "0.##");
            SetBox(OutVel, usedVelFpm, "0.00");
            SetBox(OutDp100, dpPer100, "0.0000");

            // === Equal-friction rectangle & flat oval when we have a round Dh ===
            if (primaryRoundDiaIn > 0)
            {
                // Rectangle if not already fully defined
                if (string.IsNullOrWhiteSpace(OutRS1.Text) ||
                    string.IsNullOrWhiteSpace(OutRS2.Text))
                {
                    var rectEq = DuctCalculator.EqualFrictionRectangleForRound(primaryRoundDiaIn, targetAR);
                    double rectS1 = rectEq.Side1In;
                    double rectS2 = rectEq.Side2In;

                    var rectGeom = DuctCalculator.RectGeometry(rectS1, rectS2);
                    double rectArea = rectGeom.AreaFt2;
                    double rectPerim = rectGeom.PerimeterFt;

                    SetBox(OutRS1, rectS1, "0.##");
                    SetBox(OutRS2, rectS2, "0.##");
                    SetBox(OutRAR, rectS1 / rectS2, "0.000");
                    SetBox(OutRArea, rectArea, "0.000");
                    SetBox(OutRPerim, rectPerim, "0.000");
                }

                // Flat oval
                var ovalEq = DuctCalculator.EqualFrictionFlatOvalForRound(primaryRoundDiaIn, targetAR);
                double ovalMajor = ovalEq.MajorIn;
                double ovalMinor = ovalEq.MinorIn;

                var ovalGeom = DuctCalculator.FlatOvalGeometry(ovalMinor, ovalMajor);
                double ovalAreaFt2 = ovalGeom.AreaFt2;
                double ovalPerimFt = ovalGeom.PerimeterFt;

                SetBox(OutOS1, ovalMajor, "0.##");
                SetBox(OutOS2, ovalMinor, "0.##");
                SetBox(OutOAR, ovalMajor / ovalMinor, "0.000");
                SetBox(OutOArea, ovalAreaFt2, "0.000");
                SetBox(OutOPerim, ovalPerimFt, "0.000");
            }
        }
    }
}
