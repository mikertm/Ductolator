using System;
using System.Globalization;
using System.Linq;
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
            PopulatePlumbingMaterials();
            PopulateCodeProfiles();
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

        private void PopulatePlumbingMaterials()
        {
            if (PlMaterialCombo == null)
                return;

            PlMaterialCombo.ItemsSource = Enum.GetValues(typeof(PlumbingCalculator.PipeMaterial))
                                               .Cast<PlumbingCalculator.PipeMaterial>()
                                               .ToList();
            PlMaterialCombo.SelectedIndex = 0;
        }

        private void PopulateCodeProfiles()
        {
            if (DuctRegionCombo != null)
            {
                DuctRegionCombo.ItemsSource = CodeGuidance.AllDuctRegions;
                DuctRegionCombo.SelectedIndex = 0;
            }

            if (PlRegionCombo != null)
            {
                PlRegionCombo.ItemsSource = CodeGuidance.AllPlumbingRegions;
                PlRegionCombo.SelectedIndex = 0;
            }
        }

        private PlumbingCalculator.PipeMaterial? SelectedMaterial()
        {
            if (PlMaterialCombo?.SelectedItem is PlumbingCalculator.PipeMaterial mat)
                return mat;
            return null;
        }

        private string SelectedDuctRegionKey() => DuctRegionCombo?.SelectedItem as string ?? CodeGuidance.AllDuctRegions.First();
        private string SelectedPlumbingRegionKey() => PlRegionCombo?.SelectedItem as string ?? CodeGuidance.AllPlumbingRegions.First();

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var ctrl in new TextBox[]
            {
                InCfm, InDp100, InVel, InDia, InS1, InS2, InAR,
                InAirTemp, InAltitude, InLength, InLossCoeff,
                OutRe, OutF, OutCfm, OutVel, OutDp100, OutVp, OutTotalDp,
                OutAirDensity, OutAirNu,
                OutRe, OutF, OutCfm, OutVel, OutDp100,
                OutDia, OutAreaRound, OutCircRound,
                OutRS1, OutRS2, OutRAR, OutRArea, OutRPerim,
                OutOS1, OutOS2, OutOAR, OutOArea, OutOPerim
            })
            {
                ctrl.Text = string.Empty;
            }

            if (DuctCodeNote != null)
                DuctCodeNote.Text = string.Empty;

            if (DuctStatusNote != null)
                DuctStatusNote.Text = string.Empty;
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
            double airTempF = string.IsNullOrWhiteSpace(InAirTemp?.Text) ? 70.0 : ParseBox(InAirTemp);
            double altitudeFt = string.IsNullOrWhiteSpace(InAltitude?.Text) ? 0.0 : ParseBox(InAltitude);
            double straightLengthFt = string.IsNullOrWhiteSpace(InLength?.Text) ? 100.0 : ParseBox(InLength);
            double sumLossCoeff = ParseBox(InLossCoeff);

            var air = DuctCalculator.AirAt(airTempF, altitudeFt);

            var ductProfile = CodeGuidance.GetDuctProfile(SelectedDuctRegionKey());

            if (DuctStatusNote != null)
                DuctStatusNote.Text = string.Empty;

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

            if (dp100Input <= 0 && cfm > 0 && velInput <= 0 && diaIn <= 0 && s1In <= 0 && s2In <= 0)
            {
                // No velocity or friction provided; use regional default friction to size a round duct.
                dp100Input = ductProfile.DefaultFriction_InWgPer100Ft;
            }

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
                    usedVelFpm = DuctCalculator.SolveVelocityFpm_FromDp(dhIn, dp100Input, air);
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
                    usedVelFpm = DuctCalculator.SolveVelocityFpm_FromDp(dhIn, dp100Input, air);
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
                primaryRoundDiaIn = DuctCalculator.SolveRoundDiameter_FromCfmAndFriction(cfm, dp100Input, air);
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
            double reRaw = DuctCalculator.Reynolds(usedVelFpm, dhIn, air);
            double reForFriction = reRaw < 2300 && reRaw > 0 ? 2300 : reRaw;
            double f = DuctCalculator.FrictionFactor(reForFriction, dhIn);
            double dpPer100 = DuctCalculator.DpPer100Ft_InWG(usedVelFpm, dhIn, f, air);
            double vp = DuctCalculator.VelocityPressure_InWG(usedVelFpm, air);
            double totalDp = DuctCalculator.TotalPressureDrop_InWG(dpPer100, straightLengthFt, sumLossCoeff, usedVelFpm, air);

            SetBox(OutRe, reForFriction, "0");
            double re = DuctCalculator.Reynolds(usedVelFpm, dhIn);
            double f = DuctCalculator.FrictionFactor(re, dhIn);
            double dpPer100 = DuctCalculator.DpPer100Ft_InWG(usedVelFpm, dhIn, f);

            SetBox(OutRe, re, "0");
            SetBox(OutF, f, "0.0000");
            SetBox(OutCfm, cfm, "0.##");
            SetBox(OutVel, usedVelFpm, "0.00");
            SetBox(OutDp100, dpPer100, "0.0000");
            SetBox(OutVp, vp, "0.0000");
            SetBox(OutTotalDp, totalDp, "0.0000");
            SetBox(OutAirDensity, air.DensityLbmPerFt3, "0.0000");
            SetBox(OutAirNu, air.KinematicViscosityFt2PerS, "0.000000");

            if (DuctCodeNote != null)
            {
                bool withinSupply = usedVelFpm <= ductProfile.MaxSupplyMainFpm + 1e-6;
                string baseNote =
                    $"{ductProfile.Region}: supply mains ≤ {ductProfile.MaxSupplyMainFpm:0} fpm, branches ≤ {ductProfile.MaxBranchFpm:0} fpm, returns/exhaust ≤ {ductProfile.MaxReturnFpm:0} fpm. Default sizing friction {ductProfile.DefaultFriction_InWgPer100Ft:0.###} in.w.g./100 ft ({ductProfile.Basis}).";

                if (usedVelFpm > 0)
                {
                    string status = withinSupply
                        ? "Computed velocity falls within the regional supply-main cap."
                        : "Computed velocity exceeds the regional supply-main cap; consider upsizing.";
                    DuctCodeNote.Text = baseNote + " " + status;
                }
                else
                {
                    DuctCodeNote.Text = baseNote;
                }
            }

            if (DuctStatusNote != null)
            {
                string frictionStatus = dpPer100 > ductProfile.DefaultFriction_InWgPer100Ft
                    ? "Friction exceeds the default equal-friction target; expect higher noise/pressure." : "Friction at or below the default equal-friction target.";

                string velocityStatus;
                if (usedVelFpm > ductProfile.MaxSupplyMainFpm)
                    velocityStatus = "Velocity is above the regional supply-main cap; consider a larger duct or lower flow path.";
                else if (usedVelFpm > 0.9 * ductProfile.MaxSupplyMainFpm)
                    velocityStatus = "Velocity is near the regional supply-main cap; verify noise criteria.";
                else
                    velocityStatus = "Velocity is within regional supply-main guidance.";

                string totalStatus = totalDp > 0.0
                    ? $"Total drop (friction + fittings) over {straightLengthFt:0.#} ft: {totalDp:0.0000} in. w.g." : string.Empty;

                DuctStatusNote.Text = string.Join(" ", new[] { velocityStatus, frictionStatus, totalStatus }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

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

        // === Plumbing UI ===

        private void BtnPlCalc_Click(object sender, RoutedEventArgs e)
        {
            var material = SelectedMaterial();
            if (material == null)
            {
                MessageBox.Show("Select a pipe material before calculating.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var plumbingProfile = CodeGuidance.GetPlumbingProfile(SelectedPlumbingRegionKey());

            double gpm = ParseBox(PlGpmInput);
            double lengthFt = ParseBox(PlLengthInput);
            double nominal = ParseBox(PlNominalInput);
            double explicitId = ParseBox(PlExplicitIdInput);

            double idIn = explicitId > 0
                ? explicitId
                : PlumbingCalculator.GetInnerDiameterIn(material.Value, nominal);

            if (idIn <= 0)
            {
                MessageBox.Show("Provide a nominal size available for the selected material or enter an explicit inside diameter.",
                                "Inputs Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var matData = PlumbingCalculator.GetMaterialData(material.Value);
            bool useAgedC = PlUseAgedC?.IsChecked ?? false;
            double cFactor = useAgedC ? matData.C_Aged : matData.C_New;
            double roughness = matData.RoughnessFt;
            bool isHot = PlIsHotWater?.IsChecked ?? false;

            double velocityFps = gpm > 0 ? PlumbingCalculator.VelocityFpsFromGpm(gpm, idIn) : 0;
            double psiPer100Hw = (gpm > 0 && cFactor > 0) ? PlumbingCalculator.HazenWilliamsPsiPer100Ft(gpm, idIn, cFactor) : 0;
            double psiTotalHw = psiPer100Hw * (lengthFt > 0 ? lengthFt / 100.0 : 0);

            double reynolds = velocityFps > 0 ? PlumbingCalculator.Reynolds(velocityFps, idIn) : 0;
            double darcyF = (reynolds > 0) ? PlumbingCalculator.FrictionFactor(reynolds, idIn, roughness) : 0;
            double psiPer100Darcy = (gpm > 0) ? PlumbingCalculator.HeadLoss_Darcy_PsiPer100Ft(gpm, idIn, roughness) : 0;
            double psiTotalDarcy = psiPer100Darcy * (lengthFt > 0 ? lengthFt / 100.0 : 0);

            SetBox(PlResolvedIdOutput, idIn, "0.###");
            SetBox(PlVelocityOutput, velocityFps, "0.00");
            SetBox(PlHazenPsi100Output, psiPer100Hw, "0.000");
            SetBox(PlHazenPsiTotalOutput, psiTotalHw, "0.000");
            SetBox(PlDarcyPsi100Output, psiPer100Darcy, "0.000");
            SetBox(PlDarcyPsiTotalOutput, psiTotalDarcy, "0.000");
            SetBox(PlReOutput, reynolds, "0");
            SetBox(PlFrictionOutput, darcyF, "0.0000");

            double materialCap = isHot ? matData.MaxHotFps : matData.MaxColdFps;
            double regionalCap = plumbingProfile.GetMaxVelocity(isHot);
            double limitingCap = Math.Min(materialCap, regionalCap);

            bool velocityOk = velocityFps <= 0 || velocityFps <= limitingCap;
            if (PlVelocityNote != null)
            {
                string capText = $"Limit: {limitingCap:0.0} fps ({plumbingProfile.Region}, {plumbingProfile.CodeBasis}).";
                PlVelocityNote.Text = velocityOk
                    ? "Velocity is within common design guidance. " + capText
                    : "Velocity exceeds regional/material velocity limits. " + capText;
            }

            if (PlStatusNote != null)
            {
                string frictionNote;
                if (psiPer100Hw > 8.0)
                    frictionNote = "Hazen-Williams friction is above the common 2–8 psi/100 ft envelope; consider upsizing.";
                else if (psiPer100Hw > 0)
                    frictionNote = "Hazen-Williams friction is within typical domestic design limits (≈2–8 psi/100 ft).";
                else
                    frictionNote = "Enter flow and length to evaluate friction.";

                string reynoldsNote = reynolds > 0 && reynolds < 4000
                    ? "Reynolds number indicates transitional/laminar flow—verify minor loss methods." : string.Empty;

                PlStatusNote.Text = string.Join(" ", new[] { frictionNote, reynoldsNote }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }

        private void BtnPlSize_Click(object sender, RoutedEventArgs e)
        {
            var material = SelectedMaterial();
            if (material == null)
            {
                MessageBox.Show("Select a pipe material before sizing.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double gpm = ParseBox(PlSizeGpmInput);
            double targetPsi100 = ParseBox(PlTargetPsi100Input);

            if (gpm <= 0 || targetPsi100 <= 0)
            {
                MessageBox.Show("Enter both flow (gpm) and target friction (psi/100 ft).",
                                "Inputs Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var matData = PlumbingCalculator.GetMaterialData(material.Value);
            bool useAgedC = PlUseAgedC?.IsChecked ?? false;
            double cFactor = useAgedC ? matData.C_Aged : matData.C_New;

            double solvedDiameterIn = PlumbingCalculator.SolveDiameterFromHazenWilliams(gpm, targetPsi100, cFactor);
            SetBox(PlSizedDiameterOutput, solvedDiameterIn, "0.###");

            double nearestNominal = FindNearestNominal(material.Value, solvedDiameterIn);
            SetBox(PlSizedNominalOutput, nearestNominal, nearestNominal >= 1 ? "0.##" : "0.###");

            if (PlSizeNote != null)
            {
                if (nearestNominal <= 0)
                {
                    PlSizeNote.Text = "No nominal size in the embedded tables meets or exceeds the solved ID.";
                }
                else
                {
                    double availableId = PlumbingCalculator.GetInnerDiameterIn(material.Value, nearestNominal);
                    PlSizeNote.Text = $"Select nominal {nearestNominal}" + " in (ID " + availableId.ToString("0.###", CultureInfo.InvariantCulture) + " in).";
                }
            }
        }

        private void BtnFixtureDemand_Click(object sender, RoutedEventArgs e)
        {
            double fu = ParseBox(FixtureUnitsInput);
            double demand = PlumbingCalculator.HunterDemandGpm(fu);
            SetBox(FixtureDemandOutput, demand, "0.00");
        }

        private void BtnSanitarySize_Click(object sender, RoutedEventArgs e)
        {
            double dfu = ParseBox(SanitaryDfuInput);
            double slope = ParseBox(SanitarySlopeInput);
            double diameter = PlumbingCalculator.MinSanitaryDiameterFromDfu(dfu, slope);

            SetBox(SanitaryDiameterOutput, diameter, "0.##");

            if (SanitaryNote != null)
            {
                if (diameter <= 0)
                {
                    SanitaryNote.Text = "Demand exceeds the embedded IPC-style branch table for the given slope.";
                }
                else
                {
                    SanitaryNote.Text = "Uses IPC/UPC DFU branch capacities; verify stack and trap arm separately.";
                }
            }
        }

        private void BtnStormSize_Click(object sender, RoutedEventArgs e)
        {
            double area = ParseBox(StormAreaInput);
            double intensity = ParseBox(StormRainfallInput);
            double slope = ParseBox(StormSlopeInput);
            double n = ParseBox(StormRoughnessInput);

            if (n <= 0) n = 0.012; // smooth pipe default

            double flow = PlumbingCalculator.StormFlowGpm(area, intensity);
            double diameter = PlumbingCalculator.StormDiameterFromFlow(flow, slope > 0 ? slope : 0.01, n);

            SetBox(StormFlowOutput, flow, "0.0");
            SetBox(StormDiameterOutput, diameter, "0.##");

            if (StormNote != null)
            {
                StormNote.Text = "Full-pipe Manning sizing; check ponding/leader capacity per local storm code.";
            }
        }

        private void BtnGasSize_Click(object sender, RoutedEventArgs e)
        {
            double loadMbh = ParseBox(GasLoadInput);
            double lengthFt = ParseBox(GasLengthInput);
            double dpInWc = ParseBox(GasPressureDropInput);
            double sg = ParseBox(GasSpecificGravityInput);
            double basePsi = ParseBox(GasBasePressureInput);

            if (sg <= 0) sg = 0.6;
            if (basePsi <= 0) basePsi = 0.5;
            if (dpInWc <= 0) dpInWc = 0.3;

            double diameter = PlumbingCalculator.SolveGasDiameterForLoad(loadMbh, lengthFt, dpInWc, sg, basePsi);
            double scfh = PlumbingCalculator.GasFlow_Scfh(diameter, lengthFt, dpInWc, sg, basePsi);
            double velocity = PlumbingCalculator.GasVelocityFps(scfh, diameter);

            SetBox(GasDiameterOutput, diameter, "0.##");
            SetBox(GasFlowOutput, scfh, "0");
            SetBox(GasVelocityOutput, velocity, "0.0");

            if (GasNote != null)
            {
                GasNote.Text = "IFGC/NFPA 54 empirical sizing; validate against local tables for long runs or LP systems.";
            }
        }

        private static double FindNearestNominal(PlumbingCalculator.PipeMaterial material, double requiredIdIn)
        {
            if (requiredIdIn <= 0) return 0;

            var available = PlumbingCalculator.GetAvailableNominalIds(material)
                                               .OrderBy(kv => kv.Value)
                                               .FirstOrDefault(kv => kv.Value >= requiredIdIn);

            return available.Key;
        }

        private void BtnPlClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tb in new[]
            {
                PlGpmInput, PlLengthInput, PlNominalInput, PlExplicitIdInput,
                PlResolvedIdOutput, PlVelocityOutput, PlHazenPsi100Output, PlHazenPsiTotalOutput,
                PlDarcyPsi100Output, PlDarcyPsiTotalOutput, PlReOutput, PlFrictionOutput,
                PlSizeGpmInput, PlTargetPsi100Input, PlSizedDiameterOutput, PlSizedNominalOutput,
                FixtureUnitsInput, FixtureDemandOutput,
                SanitaryDfuInput, SanitarySlopeInput, SanitaryDiameterOutput,
                StormAreaInput, StormRainfallInput, StormSlopeInput, StormRoughnessInput,
                StormFlowOutput, StormDiameterOutput,
                GasLoadInput, GasLengthInput, GasPressureDropInput, GasSpecificGravityInput,
                GasBasePressureInput, GasDiameterOutput, GasFlowOutput, GasVelocityOutput
            })
            {
                if (tb != null) tb.Text = string.Empty;
            }

            if (PlVelocityNote != null)
                PlVelocityNote.Text = string.Empty;

            if (PlStatusNote != null)
                PlStatusNote.Text = string.Empty;

            if (PlSizeNote != null)
                PlSizeNote.Text = string.Empty;

            if (SanitaryNote != null)
                SanitaryNote.Text = string.Empty;

            if (StormNote != null)
                StormNote.Text = string.Empty;

            if (GasNote != null)
                GasNote.Text = string.Empty;

            if (PlUseAgedC != null)
                PlUseAgedC.IsChecked = false;

            if (PlIsHotWater != null)
                PlIsHotWater.IsChecked = false;
        }
    }
}
