using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RTM.Ductolator.Models;
using WinForms = System.Windows.Forms;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace RTM.Ductolator
{
    public partial class MainWindow : Window
    {
        public record FixtureType(string Name, double FixtureUnits)
        {
            public override string ToString() => $"{Name} ({FixtureUnits:0.###} FU)";
        }

        public class FixtureRow : INotifyPropertyChanged
        {
            private FixtureType _fixtureType;
            private string _quantityText = "1";
            private string _overrideText = string.Empty;
            private double _defaultFixtureUnits;

            public FixtureRow(FixtureType fixtureType)
            {
                _fixtureType = fixtureType;
                _defaultFixtureUnits = fixtureType.FixtureUnits;
            }

            public FixtureType FixtureType
            {
                get => _fixtureType;
                set
                {
                    if (_fixtureType == value || value == null) return;
                    _fixtureType = value;
                    DefaultFixtureUnits = value.FixtureUnits;
                    OnPropertyChanged(nameof(FixtureType));
                    OnPropertyChanged(nameof(ResolvedFixtureUnits));
                }
            }

            public string QuantityText
            {
                get => _quantityText;
                set
                {
                    if (_quantityText == value) return;
                    _quantityText = value;
                    OnPropertyChanged(nameof(QuantityText));
                    OnPropertyChanged(nameof(ResolvedFixtureUnits));
                }
            }

            public string OverrideText
            {
                get => _overrideText;
                set
                {
                    if (_overrideText == value) return;
                    _overrideText = value;
                    OnPropertyChanged(nameof(OverrideText));
                    OnPropertyChanged(nameof(ResolvedFixtureUnits));
                }
            }

            public double DefaultFixtureUnits
            {
                get => _defaultFixtureUnits;
                private set
                {
                    if (Math.Abs(_defaultFixtureUnits - value) < 0.0001) return;
                    _defaultFixtureUnits = value;
                    OnPropertyChanged(nameof(DefaultFixtureUnits));
                    OnPropertyChanged(nameof(ResolvedFixtureUnits));
                }
            }

            public double ResolvedFixtureUnits
            {
                get
                {
                    double quantity = ParseInvariantDouble(_quantityText);
                    if (quantity < 0) quantity = 0;
                    double overrideFu = ParseInvariantDouble(_overrideText);
                    double perFixture = overrideFu > 0 ? overrideFu : DefaultFixtureUnits;
                    return perFixture * quantity;
                }
            }

            private static double ParseInvariantDouble(string? text)
            {
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    ? value
                    : 0;
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private record UserSettings(bool NoviceMode);

        private record DuctExportRow(
            double FlowCfm,
            double VelocityFpm,
            double FrictionInWgPer100Ft,
            double VelocityPressureInWg,
            double TotalPressureDropInWg,
            double StraightLengthFt,
            double FittingEquivalentLengthFt,
            double TotalRunLengthFt,
            double SumK,
            double FittingLossInWg,
            double SupplyStaticInWg,
            double ReturnStaticInWg,
            double PressureClassInWg,
            double LeakageCfm,
            double FanBhp,
            double AirTempF,
            double AltitudeFt,
            double AirDensityLbmPerFt3,
            double AirKinematicNuFt2PerS,
            double RoundDiaIn,
            double RectSide1In,
            double RectSide2In,
            double RectAreaFt2,
            double RectPerimeterFt,
            double RectAspectRatio,
            double OvalMajorIn,
            double OvalMinorIn,
            double OvalAreaFt2,
            double OvalPerimeterFt,
            double OvalAspectRatio,
            double InsulationR,
            double HeatTransferBtuh,
            double SupplyDeltaTF,
            double RequiredInsulR,
            double InsulationThicknessIn,
            string FittingsList);

        private record PlumbingExportRow(
            double FlowGpm,
            double LengthFt,
            double EquivalentLengthFt,
            double TotalRunLengthFt,
            string Material,
            double NominalIn,
            double ResolvedIdIn,
            bool UsedAgedC,
            bool IsHotWater,
            string Fluid,
            double FluidTempF,
            double AntifreezePercent,
            double FluidDensity,
            double FluidNu,
            double VelocityFps,
            double VelocityLimitFps,
            double Reynolds,
            double DarcyFriction,
            double HazenPsiPer100Ft,
            double HazenPsiTotal,
            double DarcyPsiPer100Ft,
            double DarcyPsiTotal,
            double HazenCFactor,
            double RoughnessFt,
            double WaveSpeedFps,
            double SumK,
            double FittingPsi,
            string FittingsList);

        private DuctExportRow? _lastDuctExport;
        private PlumbingExportRow? _lastPlumbingExport;
        private readonly List<DuctFittingSelection> _ductFittings = new();
        private readonly List<PipeFittingSelection> _plumbingFittings = new();
        private List<DuctFittingSelection> _lastDuctFittingSnapshot = new();
        private List<PipeFittingSelection> _lastPlumbingFittingSnapshot = new();
        private CatalogLoadReport _catalogReport = RuntimeCatalogs.LastReport;
        private DateTime _lastCatalogLoadedAt = DateTime.Now;
        private readonly ObservableCollection<string> _catalogFiles = new();
        private readonly ObservableCollection<string> _catalogPreview = new();
        private readonly ObservableCollection<string> _catalogWarnings = new();
        private readonly ObservableCollection<string> _catalogErrors = new();
        private readonly List<FixtureType> _fixtureCatalog = new()
        {
            new FixtureType("Lavatory (private)", 1.0),
            new FixtureType("Lavatory (public)", 2.0),
            new FixtureType("Water closet (flush tank)", 2.5),
            new FixtureType("Water closet (flushometer valve)", 10.0),
            new FixtureType("Urinal (flush tank)", 5.0),
            new FixtureType("Urinal (flushometer valve)", 10.0),
            new FixtureType("Shower head", 2.0),
            new FixtureType("Bathtub or tub/shower", 4.0),
            new FixtureType("Kitchen sink (residential)", 2.0),
            new FixtureType("Dishwasher (residential)", 1.5),
            new FixtureType("Clothes washer (laundry)", 2.5),
            new FixtureType("Hose bibb / sillcock", 2.5),
            new FixtureType("Service or mop sink", 3.0)
        };

        private readonly ObservableCollection<FixtureRow> _fixtureRows = new();

        public IEnumerable<FixtureType> FixtureCatalog => _fixtureCatalog;

        public ObservableCollection<FixtureRow> FixtureRows => _fixtureRows;

        public ObservableCollection<string> CatalogFiles => _catalogFiles;

        public ObservableCollection<string> CatalogPreview => _catalogPreview;

        public ObservableCollection<string> CatalogWarnings => _catalogWarnings;

        public ObservableCollection<string> CatalogErrors => _catalogErrors;

        private bool _noviceMode;
        private readonly string _settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RTM.Ductolator");
        private const string SettingsFileName = "user-settings.json";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadUserSettings();
            ApplyNoviceMode(_noviceMode);
            var defaultFolder = ConfigurationManager.AppSettings["CustomCatalogFolder"] ?? string.Empty;
            if (CatalogFolderText != null)
                CatalogFolderText.Text = defaultFolder;

            LoadCatalogs(defaultFolder);
            PopulateFluids();
            PopulateCodeProfiles();
            InitializeFixtureRows();

            UpdateDuctFittingSummary(0, 0);
            UpdatePlumbingFittingSummary(0, 0);
        }

        private string GetSettingsPath() => Path.Combine(_settingsFolder, SettingsFileName);

        private void LoadUserSettings()
        {
            try
            {
                var path = GetSettingsPath();
                if (File.Exists(path))
                {
                    var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path));
                    _noviceMode = settings?.NoviceMode ?? false;
                    return;
                }
            }
            catch
            {
                _noviceMode = false;
            }

            _noviceMode = false;
        }

        private void SaveUserSettings()
        {
            try
            {
                Directory.CreateDirectory(_settingsFolder);
                var json = JsonSerializer.Serialize(new UserSettings(_noviceMode));
                File.WriteAllText(GetSettingsPath(), json);
            }
            catch
            {
                // Ignore persistence issues
            }
        }

        private void ApplyNoviceMode(bool isNovice)
        {
            _noviceMode = isNovice;

            if (NoviceModeToggle != null && NoviceModeToggle.IsChecked != isNovice)
                NoviceModeToggle.IsChecked = isNovice;

            if (QuickStartNoteText != null)
                QuickStartNoteText.Text = isNovice
                    ? "Enter any two of the airflow, air speed, or pressure drop inputs plus one shape. We'll fill in the rest for you."
                    : "Use this for typical sizing: enter any two of flow, velocity, or friction plus one shape (round or rectangular) to solve the rest.";

            if (HowToUseStep1 != null)
                HowToUseStep1.Text = isNovice
                    ? "1) Type any two of airflow, air speed, or pressure drop plus one shape (round or rectangular). We'll solve the rest."
                    : "1) Enter any two of flow, velocity, or friction plus one geometry (round or rectangular) to solve the rest.";

            if (HowToUseStep2 != null)
                HowToUseStep2.Text = isNovice
                    ? "2) Pick a code region to auto-fill common air speed and pressure drop guidance."
                    : "2) Pick a code region to load common velocity and friction guidance.";

            if (HowToUseStep3 != null)
                HowToUseStep3.Text = isNovice
                    ? "3) Click Calculate to get air properties, pressure class, and sizes. Export saves the latest result to CSV."
                    : "3) Click Calculate to fill air properties, pressure class, and equivalent shapes; Export saves the latest result to CSV.";

            if (FlowLabel != null)
                FlowLabel.Text = isNovice ? "Airflow (cfm)" : "Flow (CFM)";

            if (InCfm != null)
                InCfm.ToolTip = isNovice
                    ? "How much air moves through the duct each minute."
                    : "Cubic feet per minute (ft³/min)";

            if (FlowExample != null)
                FlowExample.Text = "Office branch: ~1,000 cfm";

            if (VelocityLabel != null)
                VelocityLabel.Text = isNovice ? "Air speed (feet/min)" : "Velocity (FPM)";

            if (InVel != null)
                InVel.ToolTip = isNovice
                    ? "Feet per minute of air speed; supply mains often 800–1800 ft/min, returns 700–1200 ft/min."
                    : "Feet per minute; common supply 800–1800 fpm, returns 700–1200 fpm";

            if (VelocityExample != null)
                VelocityExample.Text = "Typical supply: 800–1800 ft/min";

            if (FrictionLabel != null)
                FrictionLabel.Text = isNovice ? "Pressure drop per 100 ft" : "Friction (in. w.g. / 100 ft)";

            if (InDp100 != null)
                InDp100.ToolTip = isNovice
                    ? "Inches of water lost every 100 ft of duct; aim for roughly 0.05–0.08 for quiet runs."
                    : "Inches of water per 100 ft; equal-friction duct design often 0.05–0.1";

            if (FrictionExample != null)
                FrictionExample.Text = "Aim for 0.05–0.08 in. drop per 100 ft";

            if (AspectRatioLabel != null)
                AspectRatioLabel.Text = isNovice ? "Shape ratio (long ÷ short)" : "Aspect Ratio (long/short)";

            if (InAR != null)
                InAR.ToolTip = isNovice
                    ? "Rectangle long side divided by short side; keep it under about 4:1 for noise control."
                    : "Dimensionless; use ≥1 (e.g., 2 = 2:1 rectangle)";

            if (AspectRatioExample != null)
                AspectRatioExample.Text = "Keep it at 4:1 or lower for noise";

            if (RoundDiameterLabel != null)
                RoundDiameterLabel.Text = isNovice ? "Round diameter (inches)" : "Round Diameter (in)";

            if (InDia != null)
                InDia.ToolTip = isNovice
                    ? "Inside round duct diameter in inches."
                    : "Inside diameter in inches";

            if (RoundDiameterExample != null)
                RoundDiameterExample.Text = "Example: 18 in round main";

            if (RectSide1Label != null)
                RectSide1Label.Text = isNovice ? "Rectangle side A (inches)" : "Rect Side 1 (in)";

            if (InS1 != null)
                InS1.ToolTip = isNovice ? "Long side of the rectangle in inches." : "Long side in inches";

            if (RectSide1Example != null)
                RectSide1Example.Text = "Example: 24 in long side";

            if (RectSide2Label != null)
                RectSide2Label.Text = isNovice ? "Rectangle side B (inches)" : "Rect Side 2 (in)";

            if (InS2 != null)
                InS2.ToolTip = isNovice ? "Short side of the rectangle in inches." : "Short side in inches";

            if (RectSide2Example != null)
                RectSide2Example.Text = "Example: 12 in short side";

            if (InputTipsLine1 != null)
                InputTipsLine1.Text = isNovice
                    ? "Enter airflow (cfm), air speed (feet/min), or pressure drop per 100 ft. Sizes are in inches; lengths are in feet."
                    : "Enter CFM (ft³/min), velocity (ft/min), friction (in. w.g. per 100 ft), dimensions in inches, and lengths in feet. You can size with diameter, rectangle, or flow+velocity combinations.";

            if (InputTipsLine2 != null)
                InputTipsLine2.Text = isNovice
                    ? "Air temperature (°F) and altitude (ft) tweak density/viscosity. Straight length and ΣK add fitting pressure to the total."
                    : "Air temp (°F) and altitude (ft) tune density/viscosity for non-standard air. Straight length and ΣK add fittings to total pressure.";

            SaveUserSettings();
        }

        private void LoadCatalogs(string? folder)
        {
            var (validationErrors, validationWarnings) = ValidateCatalogFolder(folder);
            PopulateCatalogFileList(folder);
            UpdateCatalogWarningsAndErrors(validationWarnings, validationErrors);
            if (validationErrors.Any())
            {
                _catalogReport = new CatalogLoadReport(
                    string.IsNullOrWhiteSpace(folder) ? "(built-in)" : folder!,
                    RuntimeCatalogs.Materials.Count,
                    RuntimeCatalogs.DuctFittings.Count,
                    RuntimeCatalogs.PipeFittings.Count,
                    validationWarnings,
                    validationErrors);
                PopulateCatalogPreview();
                UpdateCatalogStatus();
                return;
            }

            _catalogReport = RuntimeCatalogs.ReloadFromFolder(folder);
            _lastCatalogLoadedAt = DateTime.Now;
            PopulatePlumbingMaterials();
            PopulateFittingLibraries();
            PopulateCatalogFileList(folder);
            PopulateCatalogPreview();
            UpdateCatalogWarningsAndErrors(_catalogReport.Warnings, _catalogReport.Errors);
            UpdateCatalogStatus();
        }

        private void PopulateCatalogFileList(string? folder)
        {
            _catalogFiles.Clear();
            if (string.IsNullOrWhiteSpace(folder))
            {
                _catalogFiles.Add("(built-in catalogs active)");
                return;
            }

            try
            {
                if (!Directory.Exists(folder))
                {
                    _catalogFiles.Add($"Folder not found: {folder}");
                    return;
                }

                var files = Directory.GetFiles(folder, "*.*")
                    .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f)
                    .Select(path =>
                    {
                        var info = new FileInfo(path);
                        var sizeKb = Math.Max(1, info.Length / 1024.0);
                        return $"{info.Name} — {sizeKb:F1} KB, modified {info.LastWriteTime:G}";
                    });

                foreach (var file in files)
                    _catalogFiles.Add(file);

                if (!_catalogFiles.Any())
                    _catalogFiles.Add("No JSON or CSV files detected in this folder.");
            }
            catch (Exception ex)
            {
                _catalogFiles.Add($"Unable to read folder: {ex.Message}");
            }
        }

        private void PopulateCatalogPreview()
        {
            _catalogPreview.Clear();
            foreach (var material in RuntimeCatalogs.Materials.Take(6))
                _catalogPreview.Add($"Material: {material.DisplayName} ({material.Key})");

            if (RuntimeCatalogs.Materials.Count > 6)
                _catalogPreview.Add($"…plus {RuntimeCatalogs.Materials.Count - 6} more materials");

            foreach (var fitting in RuntimeCatalogs.DuctFittings.Take(4))
                _catalogPreview.Add($"Duct fitting: {fitting.Type} – {fitting.Name}");

            if (RuntimeCatalogs.DuctFittings.Count > 4)
                _catalogPreview.Add($"…plus {RuntimeCatalogs.DuctFittings.Count - 4} more duct fittings");

            foreach (var fitting in RuntimeCatalogs.PipeFittings.Take(4))
                _catalogPreview.Add($"Pipe fitting: {fitting.Type} – {fitting.Name}");

            if (RuntimeCatalogs.PipeFittings.Count > 4)
                _catalogPreview.Add($"…plus {RuntimeCatalogs.PipeFittings.Count - 4} more pipe fittings");

            if (!_catalogPreview.Any())
                _catalogPreview.Add("No catalog data loaded yet.");
        }

        private void UpdateCatalogWarningsAndErrors(IReadOnlyList<string> warnings, IReadOnlyList<string> errors)
        {
            _catalogWarnings.Clear();
            foreach (var warning in warnings)
                _catalogWarnings.Add(warning);

            _catalogErrors.Clear();
            foreach (var error in errors)
                _catalogErrors.Add(error);
        }

        private void UpdateCatalogStatus()
        {
            if (CatalogStatusNote == null || CatalogHelperText == null ||
                CatalogStatusBadge == null || CatalogStatusIcon == null || CatalogStatusTitle == null)
                return;

            var parts = new List<string>();
            bool usingBuiltIn = string.IsNullOrWhiteSpace(_catalogReport.Folder) || _catalogReport.Folder == "(built-in)";
            string folderNote = usingBuiltIn
                ? "Using built-in catalogs."
                : $"Loaded from '{_catalogReport.Folder}'.";
            parts.Add(folderNote);
            parts.Add($"Materials: {_catalogReport.MaterialCount}, duct fittings: {_catalogReport.DuctFittingCount}, pipe fittings: {_catalogReport.PipeFittingCount}.");
            if (_catalogReport.Warnings.Any())
                parts.Add(string.Join(" ", _catalogReport.Warnings));
            if (_catalogReport.Errors.Any())
                parts.Add(string.Join(" ", _catalogReport.Errors));

            CatalogStatusNote.Text = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            CatalogStatusNote.Foreground = _catalogReport.Errors.Any()
                ? (System.Windows.Media.Brush)FindResource("Brush.TextDanger")
                : _catalogReport.Warnings.Any()
                    ? (System.Windows.Media.Brush)FindResource("Brush.TextWarning")
                    : (System.Windows.Media.Brush)FindResource("Brush.TextSuccess");

            var (badgeBackground, badgeBorder, badgeText, badgeIcon, badgeTitle) = _catalogReport.Errors.Any()
                ? ((System.Windows.Media.Brush)FindResource("Brush.ChipDangerBackground"), (System.Windows.Media.Brush)FindResource("Brush.ChipDangerBorder"), (System.Windows.Media.Brush)FindResource("Brush.TextDanger"), "⛔", "Catalog load failed")
                : _catalogReport.Warnings.Any()
                    ? ((System.Windows.Media.Brush)FindResource("Brush.ChipWarningBackground"), (System.Windows.Media.Brush)FindResource("Brush.ChipWarningBorder"), (System.Windows.Media.Brush)FindResource("Brush.TextWarning"), "⚠", "Loaded with warnings")
                    : ((System.Windows.Media.Brush)FindResource("Brush.ChipSuccessBackground"), (System.Windows.Media.Brush)FindResource("Brush.ChipSuccessBorder"), (System.Windows.Media.Brush)FindResource("Brush.TextSuccess"), "✔", "Catalog ready");

            CatalogStatusBadge.Background = badgeBackground;
            CatalogStatusBadge.BorderBrush = badgeBorder;
            CatalogStatusIcon.Text = badgeIcon;
            CatalogStatusIcon.Foreground = badgeText;
            CatalogStatusTitle.Text = badgeTitle;
            CatalogStatusTitle.Foreground = badgeText;

            if (CatalogStatusSummary != null)
                CatalogStatusSummary.Text = _catalogReport.Errors.Any()
                    ? "Catalog load failed; using built-ins."
                    : usingBuiltIn
                        ? "Using built-in catalogs."
                        : $"Loaded from '{_catalogReport.Folder}'";

            CatalogHelperText.Text = usingBuiltIn
                ? "Example: C:\\projects\\catalogs with materials.json (or materials.csv) and fittings.json (or fittings.csv)."
                : $"Last loaded {_lastCatalogLoadedAt:G}. Expect materials.json (or .csv) and fittings.json (or .csv) in the selected folder.";
        }

        private void OpenCatalogSettings_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabs != null && CatalogTab != null)
                MainTabs.SelectedItem = CatalogTab;
        }

        private void BrowseCatalogFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Select a folder that contains materials.json/materials.csv and fittings.json/fittings.csv"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                if (CatalogFolderText != null)
                    CatalogFolderText.Text = dialog.SelectedPath;
                LoadCatalogs(dialog.SelectedPath);
            }
        }

        private void ReloadCatalogs_Click(object sender, RoutedEventArgs e)
        {
            LoadCatalogs(CatalogFolderText?.Text);
        }

        private void ResetCatalogs_Click(object sender, RoutedEventArgs e)
        {
            if (CatalogFolderText != null)
                CatalogFolderText.Text = string.Empty;

            LoadCatalogs(string.Empty);
        }

        private void CreateCatalogTemplates_Click(object sender, RoutedEventArgs e)
        {
            var targetFolder = CatalogFolderText?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                MessageBox.Show("Pick a catalog folder first so templates know where to go.", "Select folder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Directory.CreateDirectory(targetFolder);
                string appBase = AppDomain.CurrentDomain.BaseDirectory;
                string templateRoot = Path.Combine(appBase, "CatalogTemplates");
                if (!Directory.Exists(templateRoot))
                {
                    MessageBox.Show($"Template folder not found at {templateRoot}.", "Catalog templates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var templates = new Dictionary<string, string>
                {
                    { Path.Combine(templateRoot, "materials-template.json"), Path.Combine(targetFolder, "materials.json") },
                    { Path.Combine(templateRoot, "materials-template.csv"), Path.Combine(targetFolder, "materials.csv") },
                    { Path.Combine(templateRoot, "fittings-template.json"), Path.Combine(targetFolder, "fittings.json") },
                    { Path.Combine(templateRoot, "fittings-template.csv"), Path.Combine(targetFolder, "fittings.csv") }
                };

                var copied = new List<string>();
                var skipped = new List<string>();

                foreach (var kvp in templates)
                {
                    if (!File.Exists(kvp.Key))
                        continue;

                    if (File.Exists(kvp.Value))
                    {
                        skipped.Add(Path.GetFileName(kvp.Value));
                        continue;
                    }

                    File.Copy(kvp.Key, kvp.Value, overwrite: false);
                    copied.Add(Path.GetFileName(kvp.Value));
                }

                LoadCatalogs(targetFolder);

                var message = new StringBuilder();
                if (copied.Count > 0)
                    message.Append($"Copied {string.Join(", ", copied)}. ");

                if (skipped.Count > 0)
                    message.Append($"Skipped existing {string.Join(", ", skipped)} to avoid overwriting. ");

                if (message.Length == 0)
                    message.Append("No template files were copied.");
                else
                    message.Append("Edit any new templates, then click Reload Catalogs.");

                MessageBox.Show(message.ToString(), "Templates created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to create templates: {ex.Message}", "Catalog templates", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCatalogFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = CatalogFolderText?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("Pick an existing catalog folder first.", "Catalog folder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open folder: {ex.Message}", "Catalog folder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateCatalogFolder(string? folder)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(folder))
                return (errors, warnings);

            try
            {
                if (!Directory.Exists(folder))
                {
                    errors.Add($"Folder not found: {folder}");
                    return (errors, warnings);
                }

                bool hasMaterials = File.Exists(Path.Combine(folder, "materials.json")) || File.Exists(Path.Combine(folder, "materials.csv"));
                bool hasMaterialTemplate = File.Exists(Path.Combine(folder, "materials-template.json")) || File.Exists(Path.Combine(folder, "materials-template.csv"));
                bool hasFittings = File.Exists(Path.Combine(folder, "fittings.json")) || File.Exists(Path.Combine(folder, "fittings.csv"));
                bool hasFittingTemplate = File.Exists(Path.Combine(folder, "fittings-template.json")) || File.Exists(Path.Combine(folder, "fittings-template.csv"));

                if (!hasMaterials)
                {
                    if (hasMaterialTemplate)
                        warnings.Add("materials.json/materials.csv not found; using materials-template instead. Rename it when you finalize your catalog.");
                    else
                        errors.Add("materials.json or materials.csv is missing in the selected folder.");
                }
                if (!hasFittings)
                {
                    if (hasFittingTemplate)
                        warnings.Add("fittings.json/fittings.csv not found; using fittings-template instead. Rename it when you finalize your catalog.");
                    else
                        errors.Add("fittings.json or fittings.csv is missing in the selected folder.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Unable to read folder: {ex.Message}");
            }

            return (errors, warnings);
        }

        private void NoviceModeToggle_Checked(object sender, RoutedEventArgs e) => ApplyNoviceMode(true);

        private void NoviceModeToggle_Unchecked(object sender, RoutedEventArgs e) => ApplyNoviceMode(false);

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveUserSettings();
        }

        private record DuctFittingSelection(DuctFitting Fitting, int Quantity)
        {
            public override string ToString() =>
                $"{Quantity}× {Fitting.Name} (K={Fitting.KCoefficient:0.###}, Leq={Fitting.EquivalentLengthFt:0.#} ft each)";
        }

        private record PipeFittingSelection(PipeFitting Fitting, int Quantity)
        {
            public override string ToString() =>
                $"{Quantity}× {Fitting.Name} (K={Fitting.KCoefficient:0.###}, Leq={Fitting.EquivalentLengthFt:0.#} ft each)";
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

        private static bool TryParseProvidedBox(TextBox tb, out double value, out bool provided)
        {
            value = 0;
            provided = false;
            if (tb == null)
                return true;

            string text = tb.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            provided = true;
            return double.TryParse(text,
                                   NumberStyles.Float,
                                   CultureInfo.InvariantCulture,
                                   out value);
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

        private static string CsvEscape(object value)
        {
            string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (s.Contains('"'))
                s = s.Replace("\"", "\"\"");

            if (s.Contains(',') || s.Contains('\n'))
                s = $"\"{s}\"";

            return s;
        }

        private static bool SaveCsvToPath(string defaultFileName, string content)
        {
            var dialog = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            bool? result = dialog.ShowDialog();
            if (result != true)
                return false;

            try
            {
                File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save CSV: {ex.Message}",
                    "Export failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void PopulatePlumbingMaterials()
        {
            if (PlMaterialCombo == null)
                return;

            PlMaterialCombo.ItemsSource = RuntimeCatalogs.Materials;
            PlMaterialCombo.SelectedIndex = 0;
        }

        private void PopulateFluids()
        {
            if (PlAntifreezeTypeCombo == null)
                return;

            PlAntifreezeTypeCombo.ItemsSource = Enum.GetValues(typeof(PlumbingCalculator.FluidType))
                                                    .Cast<PlumbingCalculator.FluidType>()
                                                    .ToList();
            PlAntifreezeTypeCombo.SelectedItem = PlumbingCalculator.FluidType.Water;
        }

        private void PopulateFittingLibraries()
        {
            if (DuctFittingCombo != null)
            {
                DuctFittingCombo.ItemsSource = RuntimeCatalogs.DuctFittings;
                DuctFittingCombo.SelectedIndex = 0;
            }

            if (PlFittingCombo != null)
            {
                PlFittingCombo.ItemsSource = RuntimeCatalogs.PipeFittings;
                PlFittingCombo.SelectedIndex = 0;
            }
        }

        private void RefreshDuctFittingList()
        {
            if (DuctFittingRunList == null) return;
            DuctFittingRunList.ItemsSource = null;
            DuctFittingRunList.ItemsSource = _ductFittings;
        }

        private void RefreshPlumbingFittingList()
        {
            if (PlFittingRunList == null) return;
            PlFittingRunList.ItemsSource = null;
            PlFittingRunList.ItemsSource = _plumbingFittings;
        }

        private (double sumK, double equivalentLength) CurrentDuctFittingTotals()
        {
            double sumK = _ductFittings.Sum(f => f.Fitting.KCoefficient * f.Quantity);
            double eqLen = _ductFittings.Sum(f => f.Fitting.EquivalentLengthFt * f.Quantity);
            return (sumK, eqLen);
        }

        private (double sumK, double equivalentLength) CurrentPlumbingFittingTotals()
        {
            double sumK = _plumbingFittings.Sum(f => f.Fitting.KCoefficient * f.Quantity);
            double eqLen = _plumbingFittings.Sum(f => f.Fitting.EquivalentLengthFt * f.Quantity);
            return (sumK, eqLen);
        }

        private static int ParseQuantity(TextBox? tb)
        {
            if (tb == null) return 0;
            return int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty) && qty > 0
                ? qty
                : 0;
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

        private void ApplyVelocityPreset_Click(object sender, RoutedEventArgs e)
        {
            var ductProfile = CodeGuidance.GetDuctProfile(SelectedDuctRegionKey());

            double? targetVel = null;
            string label = "Preset";

            if (sender is Button btn)
            {
                label = btn.Content?.ToString() ?? label;
                string? tag = btn.Tag as string;
                targetVel = tag switch
                {
                    "Supply" => ductProfile.MaxSupplyMainFpm,
                    "Branch" => ductProfile.MaxBranchFpm,
                    "Return" => ductProfile.MaxReturnFpm,
                    _ => targetVel
                };
            }

            if (targetVel.HasValue && InVel != null)
                SetBox(InVel, targetVel.Value, "0");

            if (InDp100 != null)
                SetBox(InDp100, ductProfile.DefaultFriction_InWgPer100Ft, "0.###");

            if (DuctStatusNote != null)
            {
                string velText = targetVel.HasValue ? $"{targetVel.Value:0} fpm" : "default velocity";
                DuctStatusNote.Text = $"{label} loaded from {ductProfile.Region}: {velText}, friction {ductProfile.DefaultFriction_InWgPer100Ft:0.###} in. w.g./100 ft.";
            }
        }

        private PipeMaterialProfile? SelectedMaterial()
        {
            return PlMaterialCombo?.SelectedItem as PipeMaterialProfile;
        }

        private PlumbingCalculator.FluidType SelectedFluid()
        {
            if (PlAntifreezeTypeCombo?.SelectedItem is PlumbingCalculator.FluidType fluid)
                return fluid;

            return PlumbingCalculator.FluidType.Water;
        }

        private string SelectedDuctRegionKey() => DuctRegionCombo?.SelectedItem as string ?? CodeGuidance.AllDuctRegions.First();
        private string SelectedPlumbingRegionKey() => PlRegionCombo?.SelectedItem as string ?? CodeGuidance.AllPlumbingRegions.First();

        private void AddDuctFitting_Click(object sender, RoutedEventArgs e)
        {
            if (DuctFittingCombo?.SelectedItem is not DuctFitting fit)
                return;

            int qty = ParseQuantity(DuctFittingQty);
            if (qty <= 0) return;

            var existing = _ductFittings.FirstOrDefault(f => f.Fitting.Name == fit.Name);
            if (existing != null)
            {
                int idx = _ductFittings.IndexOf(existing);
                _ductFittings[idx] = existing with { Quantity = existing.Quantity + qty };
            }
            else
            {
                _ductFittings.Add(new DuctFittingSelection(fit, qty));
            }

            RefreshDuctFittingList();
            UpdateDuctFittingTotals();
        }

        private void RemoveDuctFitting_Click(object sender, RoutedEventArgs e)
        {
            if (DuctFittingRunList?.SelectedItem is not DuctFittingSelection sel)
                return;

            _ductFittings.Remove(sel);
            RefreshDuctFittingList();
            UpdateDuctFittingTotals();
        }

        private void ClearDuctFittings_Click(object sender, RoutedEventArgs e)
        {
            _ductFittings.Clear();
            RefreshDuctFittingList();
            UpdateDuctFittingTotals();
        }

        private void AddPlumbingFitting_Click(object sender, RoutedEventArgs e)
        {
            if (PlFittingCombo?.SelectedItem is not PipeFitting fit)
                return;

            int qty = ParseQuantity(PlFittingQty);
            if (qty <= 0) return;

            var existing = _plumbingFittings.FirstOrDefault(f => f.Fitting.Name == fit.Name);
            if (existing != null)
            {
                int idx = _plumbingFittings.IndexOf(existing);
                _plumbingFittings[idx] = existing with { Quantity = existing.Quantity + qty };
            }
            else
            {
                _plumbingFittings.Add(new PipeFittingSelection(fit, qty));
            }

            RefreshPlumbingFittingList();
            UpdatePlumbingFittingTotals();
        }

        private void RemovePlumbingFitting_Click(object sender, RoutedEventArgs e)
        {
            if (PlFittingRunList?.SelectedItem is not PipeFittingSelection sel)
                return;

            _plumbingFittings.Remove(sel);
            RefreshPlumbingFittingList();
            UpdatePlumbingFittingTotals();
        }

        private void ClearPlumbingFittings_Click(object sender, RoutedEventArgs e)
        {
            _plumbingFittings.Clear();
            RefreshPlumbingFittingList();
            UpdatePlumbingFittingTotals();
        }

        private void InLength_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDuctFittingTotals();
        }

        private void PlLengthInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlumbingFittingTotals();
        }

        private void UpdateDuctFittingTotals()
        {
            var (sumK, eqLen) = CurrentDuctFittingTotals();
            SetBox(DuctFittingSumKOutput, sumK, "0.###");
            SetBox(DuctFittingEquivalentLengthOutput, eqLen, "0.#");
            if (_ductFittings.Any())
                SetBox(InLossCoeff, sumK, "0.###");

            double baseLength = ParseBox(InLength);
            double totalLength = baseLength + eqLen;
            SetBox(DuctTotalRunLengthOutput, totalLength, "0.#");

            UpdateDuctFittingSummary(sumK, eqLen);
        }

        private void UpdatePlumbingFittingTotals()
        {
            var (sumK, eqLen) = CurrentPlumbingFittingTotals();
            SetBox(PlFittingSumKOutput, sumK, "0.###");
            SetBox(PlFittingEquivalentLengthOutput, eqLen, "0.#");

            double baseLength = ParseBox(PlLengthInput);
            double totalLength = baseLength + eqLen;
            SetBox(PlTotalRunLengthOutput, totalLength, "0.#");

            UpdatePlumbingFittingSummary(sumK, eqLen);
        }

        private void UpdateDuctFittingSummary(double sumK, double eqLen)
        {
            int count = _ductFittings.Sum(f => f.Quantity);
            string badgeText = $"{count} fitting{(count == 1 ? string.Empty : "s")} · ΣK={sumK:0.###} · Eq. L={eqLen:0.#} ft";

            if (DuctFittingBadgeText != null)
                DuctFittingBadgeText.Text = badgeText;

            if (DuctFittingSummaryText == null) return;

            if (count == 0)
            {
                DuctFittingSummaryText.Text = "Add fittings to include fitting loss sum (K) and equivalent length in the duct pressure drop. K factors express loss coefficients; equivalent length converts each fitting into a length of straight duct—use K to track loss coefficients and Leq when you need a length-based friction estimate.";
                return;
            }

            DuctFittingSummaryText.Text = $"{count} fittings added (K {sumK:0.###}, Leq {eqLen:0.#} ft). K factors express loss coefficients, while equivalent length translates fittings into straight-run feet—use K with velocity pressure math and Leq for friction-per-100 ft checks.";
        }

        private void UpdatePlumbingFittingSummary(double sumK, double eqLen)
        {
            int count = _plumbingFittings.Sum(f => f.Quantity);
            string badgeText = $"{count} fitting{(count == 1 ? string.Empty : "s")} · ΣK={sumK:0.###} · Eq. L={eqLen:0.#} ft";

            if (PlFittingBadgeText != null)
                PlFittingBadgeText.Text = badgeText;

            if (PlFittingSummaryText == null) return;

            if (count == 0)
            {
                PlFittingSummaryText.Text = "Add fittings to include fitting loss sum (K) and equivalent length in the plumbing run. K factors describe minor loss coefficients; equivalent length converts each fitting to straight pipe—use K for coefficient-based headloss and Leq when comparing to friction tables.";
                return;
            }

            PlFittingSummaryText.Text = $"{count} fittings added (K {sumK:0.###}, Leq {eqLen:0.#} ft). K factors track minor losses, while equivalent length shows the straight-pipe distance to use with friction charts—pick K for coefficient calculations and Leq for length substitutions.";
        }

        private static string FittingListSummary(IEnumerable<DuctFittingSelection> run)
        {
            return string.Join("; ", run.Select(f =>
                $"{f.Quantity}× {f.Fitting.Name} (K={f.Fitting.KCoefficient:0.###}, Leq={f.Fitting.EquivalentLengthFt:0.#} ft)"));
        }

        private static string FittingListSummary(IEnumerable<PipeFittingSelection> run)
        {
            return string.Join("; ", run.Select(f =>
                $"{f.Quantity}× {f.Fitting.Name} (K={f.Fitting.KCoefficient:0.###}, Leq={f.Fitting.EquivalentLengthFt:0.#} ft)"));
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var ctrl in new TextBox[]
            {
                InCfm, InDp100, InVel, InDia, InS1, InS2, InAR,
                InAirTemp, InAltitude, InLength, InLossCoeff,
                InSupplyStatic, InReturnStatic, InLeakageClass, InLeakTestPressure,
                InFanEff, InAmbientTemp, InMaxDeltaT, InExistingInsulR,
                DuctFittingSumKOutput, DuctFittingEquivalentLengthOutput, DuctTotalRunLengthOutput,
                OutRe, OutF, OutCfm, OutVel, OutDp100, OutVp, OutVpEcho, OutTotalDp,
                OutPressureClass, OutLeakage, OutFanBhp,
                OutHeatTransfer, OutDeltaT, OutRequiredR, OutInsulThk,
                OutAirDensity, OutAirNu,
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

            _ductFittings.Clear();
            RefreshDuctFittingList();
            UpdateDuctFittingTotals();

            if (DuctCodeNote != null)
                DuctCodeNote.Text = string.Empty;

            if (DuctStatusNote != null)
                DuctStatusNote.Text = string.Empty;

            _lastDuctExport = null;
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
            double supplyStatic = ParseBox(InSupplyStatic);
            double returnStatic = ParseBox(InReturnStatic);
            double leakageClass = ParseBox(InLeakageClass);
            double leakTestPressure = ParseBox(InLeakTestPressure);
            double fanEfficiency = ParseBox(InFanEff);
            double ambientTempF = string.IsNullOrWhiteSpace(InAmbientTemp?.Text) ? 75.0 : ParseBox(InAmbientTemp);
            double maxDeltaTF = ParseBox(InMaxDeltaT);
            double existingInsulR = ParseBox(InExistingInsulR);

            var air = DuctCalculator.AirAt(airTempF, altitudeFt);

            var ductProfile = CodeGuidance.GetDuctProfile(SelectedDuctRegionKey());

            if (leakageClass <= 0) leakageClass = 6.0;
            if (leakTestPressure <= 0) leakTestPressure = Math.Max(1.0, Math.Max(supplyStatic, returnStatic));
            if (fanEfficiency <= 0) fanEfficiency = 0.65;

            if (DuctStatusNote != null)
                DuctStatusNote.Text = string.Empty;

            double targetAR = arInput > 0 ? arInput : 2.0;

            var (fittingSumK, fittingEquivalentLength) = CurrentDuctFittingTotals();
            if (fittingSumK > 0)
            {
                sumLossCoeff = fittingSumK;
                SetBox(InLossCoeff, sumLossCoeff, "0.###");
            }
            double totalRunLengthFt = straightLengthFt + fittingEquivalentLength;
            SetBox(DuctTotalRunLengthOutput, totalRunLengthFt, "0.#");

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
            double re = DuctCalculator.Reynolds(usedVelFpm, dhIn, air);
            double reRaw = re;
            double reForFriction = re < 2300 && re > 0 ? 2300 : re;
            double f = DuctCalculator.FrictionFactor(reForFriction, dhIn);
            double dpPer100 = DuctCalculator.DpPer100Ft_InWG(usedVelFpm, dhIn, f, air);
            double vp = DuctCalculator.VelocityPressure_InWG(usedVelFpm, air);
            // When fitting equivalent length is included in the total run length, avoid
            // also charging the K-based minor loss term to prevent double-counting.
            double totalDp = DuctCalculator.TotalPressureDrop_InWG(
                dpPer100,
                totalRunLengthFt,
                fittingEquivalentLength > 0 ? 0 : sumLossCoeff,
                usedVelFpm,
                air);

            SetBox(OutRe, re, "0");
            SetBox(OutF, f, "0.0000");
            SetBox(OutCfm, cfm, "0.##");
            SetBox(OutVel, usedVelFpm, "0.00");
            SetBox(OutDp100, dpPer100, "0.0000");
            SetBox(OutVp, vp, "0.0000");
            SetBox(OutVpEcho, vp, "0.0000");
            SetBox(OutTotalDp, totalDp, "0.0000");
            SetBox(OutAirDensity, air.DensityLbmPerFt3, "0.0000");
            SetBox(OutAirNu, air.KinematicViscosityFt2PerS, "0.000000");

            double pressureClass = DuctCalculator.SelectSmacnaPressureClass(Math.Max(supplyStatic, returnStatic));
            double ductSurfaceArea = DuctCalculator.SurfaceAreaFromPerimeter(perimFt, straightLengthFt);
            double leakageCfm = DuctCalculator.LeakageCfm(leakageClass, leakTestPressure, ductSurfaceArea);
            double fanBhp = DuctCalculator.FanBrakeHorsepower(cfm, totalDp, fanEfficiency);

            double supplyToAmbientDelta = airTempF - ambientTempF;
            double interiorFilmR = 0.61;
            double exteriorFilmR = 0.17;
            double uValue = existingInsulR > 0
                ? 1.0 / (existingInsulR + interiorFilmR + exteriorFilmR)
                : 1.0 / (interiorFilmR + exteriorFilmR);

            double heatTransfer = DuctCalculator.HeatTransfer_Btuh(uValue, ductSurfaceArea, supplyToAmbientDelta);
            double deltaTAirCalc = DuctCalculator.AirTemperatureChangeFromHeat(heatTransfer, cfm, air);
            double requiredRCalc = (maxDeltaTF > 0 && Math.Abs(supplyToAmbientDelta) > 0 && ductSurfaceArea > 0 && cfm > 0)
                ? DuctCalculator.RequiredInsulationR(maxDeltaTF, ductSurfaceArea, supplyToAmbientDelta, cfm, air)
                : 0;
            double insulThickness = DuctCalculator.InsulationThicknessInFromR(requiredRCalc);

            SetBox(OutPressureClass, pressureClass, "0.0#");
            SetBox(OutLeakage, leakageCfm, "0.##");
            SetBox(OutFanBhp, fanBhp, "0.00");
            SetBox(OutHeatTransfer, heatTransfer, "0");
            SetBox(OutDeltaT, deltaTAirCalc, "0.00");
            SetBox(OutRequiredR, requiredRCalc, "0.00");
            SetBox(OutInsulThk, insulThickness, "0.00");

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

                string regimeStatus;
                if (reRaw < 2300 && reRaw > 0)
                {
                    regimeStatus = "Laminar regime; ASHRAE charts assume turbulent flow—verify viscous losses separately.";
                }
                else if (reRaw < 4000)
                {
                    regimeStatus = "Transitional regime; pressure drops may deviate from chart values.";
                }
                else
                {
                    regimeStatus = "Turbulent regime consistent with ASHRAE/SMACNA friction charts.";
                }

                string totalStatus = totalDp > 0.0
                    ? $"Total drop (friction + fittings) over {totalRunLengthFt:0.#} ft: {totalDp:0.0000} in. w.g." : string.Empty;

                string pressureStatus = pressureClass > 0
                    ? $"Pressure class {pressureClass:0.0} in. w.g.; leakage class {leakageClass:0.#} ≈ {leakageCfm:0.#} cfm over {straightLengthFt:0.#} ft."
                    : string.Empty;

                string fanStatus = fanBhp > 0
                    ? $"Fan bhp at {fanEfficiency:0.00} eff: {fanBhp:0.00} bhp."
                    : string.Empty;

                DuctStatusNote.Text = string.Join(" ", new[] { velocityStatus, frictionStatus, regimeStatus, totalStatus, pressureStatus, fanStatus }.Where(s => !string.IsNullOrWhiteSpace(s)));
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
                    double rectAreaEq = rectGeom.AreaFt2;
                    double rectPerim = rectGeom.PerimeterFt;

                    SetBox(OutRS1, rectS1, "0.##");
                    SetBox(OutRS2, rectS2, "0.##");
                    SetBox(OutRAR, rectS1 / rectS2, "0.000");
                    SetBox(OutRArea, rectAreaEq, "0.000");
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

            double rectSide1 = ParseBox(OutRS1);
            double rectSide2 = ParseBox(OutRS2);
            double rectArea = ParseBox(OutRArea);
            double rectPerimeter = ParseBox(OutRPerim);
            double rectAr = ParseBox(OutRAR);

            double ovalMajorIn = ParseBox(OutOS1);
            double ovalMinorIn = ParseBox(OutOS2);
            double ovalArea = ParseBox(OutOArea);
            double ovalPerimeter = ParseBox(OutOPerim);
            double ovalAr = ParseBox(OutOAR);

            double roundDia = ParseBox(OutDia);
            double fittingDrop = sumLossCoeff > 0 ? sumLossCoeff * vp : 0;
            double heatTransferBtuh = ParseBox(OutHeatTransfer);
            double deltaTAir = ParseBox(OutDeltaT);
            double requiredR = ParseBox(OutRequiredR);
            double insulThk = ParseBox(OutInsulThk);

            _lastDuctFittingSnapshot = _ductFittings.Select(f => f with { }).ToList();

            _lastDuctExport = new DuctExportRow(
                FlowCfm: cfm,
                VelocityFpm: usedVelFpm,
                FrictionInWgPer100Ft: dpPer100,
                VelocityPressureInWg: vp,
                TotalPressureDropInWg: totalDp,
                StraightLengthFt: straightLengthFt,
                FittingEquivalentLengthFt: fittingEquivalentLength,
                TotalRunLengthFt: totalRunLengthFt,
                SumK: sumLossCoeff,
                FittingLossInWg: fittingDrop,
                SupplyStaticInWg: supplyStatic,
                ReturnStaticInWg: returnStatic,
                PressureClassInWg: pressureClass,
                LeakageCfm: leakageCfm,
                FanBhp: fanBhp,
                AirTempF: airTempF,
                AltitudeFt: altitudeFt,
                AirDensityLbmPerFt3: air.DensityLbmPerFt3,
                AirKinematicNuFt2PerS: air.KinematicViscosityFt2PerS,
                RoundDiaIn: roundDia,
                RectSide1In: rectSide1,
                RectSide2In: rectSide2,
                RectAreaFt2: rectArea,
                RectPerimeterFt: rectPerimeter,
                RectAspectRatio: rectAr,
                OvalMajorIn: ovalMajorIn,
                OvalMinorIn: ovalMinorIn,
                OvalAreaFt2: ovalArea,
                OvalPerimeterFt: ovalPerimeter,
                OvalAspectRatio: ovalAr,
                InsulationR: existingInsulR,
                HeatTransferBtuh: heatTransferBtuh,
                SupplyDeltaTF: deltaTAir,
                RequiredInsulR: requiredR,
                InsulationThicknessIn: insulThk,
                FittingsList: FittingListSummary(_lastDuctFittingSnapshot));
        }

        private void BtnExportDuct_Click(object sender, RoutedEventArgs e)
        {
            if (_lastDuctExport == null)
            {
                MessageBox.Show("Calculate a duct run before exporting.", "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var d = _lastDuctExport!;
            var sb = new StringBuilder();
            sb.AppendLine("Flow (cfm),Velocity (fpm),Friction (in.w.g./100 ft),Velocity Pressure (in.w.g.),Total Drop (in.w.g.),Straight Length (ft),Fitting Equivalent Length (ft),Total Run Length (ft),Sum K,Fitting Drop (in.w.g.),Supply Static (in.w.g.),Return Static (in.w.g.),Pressure Class (in.w.g.),Leakage (cfm),Fan BHP,Air Temp (F),Altitude (ft),Air Density (lbm/ft^3),Air Kinematic Nu (ft^2/s),Round Dia (in),Rect Side 1 (in),Rect Side 2 (in),Rect Area (ft^2),Rect Perimeter (ft),Rect AR,Oval Major (in),Oval Minor (in),Oval Area (ft^2),Oval Perimeter (ft),Oval AR,Existing Insulation R,Heat Transfer (Btuh),Supply DeltaT (F),Required Insulation R,Estimated Thickness (in),Fittings");
            sb.AppendLine(string.Join(",", new[]
            {
                CsvEscape(d.FlowCfm),
                CsvEscape(d.VelocityFpm),
                CsvEscape(d.FrictionInWgPer100Ft),
                CsvEscape(d.VelocityPressureInWg),
                CsvEscape(d.TotalPressureDropInWg),
                CsvEscape(d.StraightLengthFt),
                CsvEscape(d.FittingEquivalentLengthFt),
                CsvEscape(d.TotalRunLengthFt),
                CsvEscape(d.SumK),
                CsvEscape(d.FittingLossInWg),
                CsvEscape(d.SupplyStaticInWg),
                CsvEscape(d.ReturnStaticInWg),
                CsvEscape(d.PressureClassInWg),
                CsvEscape(d.LeakageCfm),
                CsvEscape(d.FanBhp),
                CsvEscape(d.AirTempF),
                CsvEscape(d.AltitudeFt),
                CsvEscape(d.AirDensityLbmPerFt3),
                CsvEscape(d.AirKinematicNuFt2PerS),
                CsvEscape(d.RoundDiaIn),
                CsvEscape(d.RectSide1In),
                CsvEscape(d.RectSide2In),
                CsvEscape(d.RectAreaFt2),
                CsvEscape(d.RectPerimeterFt),
                CsvEscape(d.RectAspectRatio),
                CsvEscape(d.OvalMajorIn),
                CsvEscape(d.OvalMinorIn),
                CsvEscape(d.OvalAreaFt2),
                CsvEscape(d.OvalPerimeterFt),
                CsvEscape(d.OvalAspectRatio),
                CsvEscape(d.InsulationR),
                CsvEscape(d.HeatTransferBtuh),
                CsvEscape(d.SupplyDeltaTF),
                CsvEscape(d.RequiredInsulR),
                CsvEscape(d.InsulationThicknessIn),
                CsvEscape(d.FittingsList)
            }));

            if (_lastDuctFittingSnapshot.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Fittings");
                sb.AppendLine("Category,Name,Quantity,K,Equivalent Length (ft)");
                foreach (var f in _lastDuctFittingSnapshot)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        CsvEscape(f.Fitting.Category),
                        CsvEscape(f.Fitting.Name),
                        CsvEscape(f.Quantity),
                        CsvEscape(f.Fitting.KCoefficient),
                        CsvEscape(f.Fitting.EquivalentLengthFt)
                    }));
                }
            }

            if (SaveCsvToPath("duct-export.csv", sb.ToString()))
            {
                MessageBox.Show("Duct results exported for Excel review.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
            double fluidTempF = ParseBox(PlFluidTempInput);
            double antifreezePercent = ParseBox(PlAntifreezePercentInput);
            var fluidType = SelectedFluid();

            if (!TryParseProvidedBox(PlAvailableHeadPsiInput, out double availableHeadPsiInput, out bool headPsiProvided))
            {
                MessageBox.Show("Available head in psi must be a number.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryParseProvidedBox(PlAvailableHeadFtInput, out double availableHeadFtInput, out bool headFtProvided))
            {
                MessageBox.Show("Available head in feet must be a number.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (gpm <= 0)
            {
                MessageBox.Show("Enter a flow rate in gpm before calculating plumbing losses.",
                                "Inputs Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            if (lengthFt <= 0)
            {
                MessageBox.Show("Enter a straight pipe length in feet to evaluate losses.",
                                "Inputs Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            UpdatePlumbingFittingTotals();
            var (plFittingSumK, plFittingEqLength) = CurrentPlumbingFittingTotals();
            double totalRunLengthFt = lengthFt + plFittingEqLength;

            double idIn = explicitId > 0
                ? explicitId
                : PlumbingCalculator.GetInnerDiameterIn(material, nominal);

            if (idIn <= 0)
            {
                MessageBox.Show("Provide a nominal size available for the selected material or enter an explicit inside diameter.",
                                "Inputs Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var matData = PlumbingCalculator.GetMaterialData(material);
            bool useAgedC = PlUseAgedC?.IsChecked ?? false;
            var fluidProps = PlumbingCalculator.ResolveFluidProperties(fluidType, fluidTempF, antifreezePercent);
            double psiPerFtHead = PlumbingCalculator.PsiPerFtHeadFromDensity(fluidProps.DensityLbmPerFt3);
            double cFactor = (useAgedC ? matData.C_Aged : matData.C_New) * fluidProps.HazenWilliamsCFactorMultiplier;
            double roughness = matData.RoughnessFt * fluidProps.RoughnessMultiplier;
            bool isHot = PlIsHotWater?.IsChecked ?? false;

            double velocityFps = gpm > 0 ? PlumbingCalculator.VelocityFpsFromGpm(gpm, idIn) : 0;
            double psiPer100Hw = (gpm > 0 && cFactor > 0) ? PlumbingCalculator.HazenWilliamsPsiPer100Ft(gpm, idIn, cFactor, psiPerFtHead) : 0;
            double fittingPsi = velocityFps > 0 && plFittingSumK > 0
                ? PlumbingCalculator.MinorLossPsi(velocityFps, plFittingSumK, fluidProps.DensityLbmPerFt3)
                : 0;
            double frictionPsiHw = psiPer100Hw * (lengthFt > 0 ? lengthFt / 100.0 : 0);
            double fittingPsiHw = fittingPsi > 0
                ? fittingPsi
                : psiPer100Hw * (plFittingEqLength > 0 ? plFittingEqLength / 100.0 : 0);
            double psiTotalHw = frictionPsiHw + fittingPsiHw;

            double reynolds = velocityFps > 0 ? PlumbingCalculator.Reynolds(velocityFps, idIn, fluidProps.KinematicViscosityFt2PerS) : 0;
            double darcyF = (reynolds > 0) ? PlumbingCalculator.FrictionFactor(reynolds, idIn, roughness) : 0;
            double psiPer100Darcy = (gpm > 0) ? PlumbingCalculator.HeadLoss_Darcy_PsiPer100Ft(gpm, idIn, roughness, fluidProps.KinematicViscosityFt2PerS, psiPerFtHead) : 0;
            double frictionPsiDarcy = psiPer100Darcy * (lengthFt > 0 ? lengthFt / 100.0 : 0);
            double fittingPsiDarcy = fittingPsi > 0
                ? fittingPsi
                : psiPer100Darcy * (plFittingEqLength > 0 ? plFittingEqLength / 100.0 : 0);
            double psiTotalDarcy = frictionPsiDarcy + fittingPsiDarcy;

            bool hasAvailableHeadInput = headPsiProvided || headFtProvided;
            bool hasAvailableHead = false;
            double availableHeadPsi = 0;
            if (availableHeadPsiInput > 0)
            {
                availableHeadPsi += availableHeadPsiInput;
                hasAvailableHead = true;
            }

            if (availableHeadFtInput > 0 && psiPerFtHead > 0)
            {
                availableHeadPsi += availableHeadFtInput * psiPerFtHead;
                hasAvailableHead = true;
            }

            if (hasAvailableHeadInput && !hasAvailableHead)
            {
                MessageBox.Show("Enter a positive available head value in psi or feet to compare against losses.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetBox(PlResolvedIdOutput, idIn, "0.###");
            SetBox(PlVelocityOutput, velocityFps, "0.00");
            SetBox(PlHazenPsi100Output, psiPer100Hw, "0.000");
            SetBox(PlHazenPsiTotalOutput, psiTotalHw, "0.000");
            SetBox(PlDarcyPsi100Output, psiPer100Darcy, "0.000");
            SetBox(PlDarcyPsiTotalOutput, psiTotalDarcy, "0.000");
            SetBox(PlReOutput, reynolds, "0");
            SetBox(PlFrictionOutput, darcyF, "0.0000");
            SetBox(PlFluidDensityOutput, fluidProps.DensityLbmPerFt3, "0.00");
            SetBox(PlFluidNuOutput, fluidProps.KinematicViscosityFt2PerS, "0.0000e+0");

            double materialCap = isHot ? matData.MaxHotFps : matData.MaxColdFps;
            double regionalCap = plumbingProfile.GetMaxVelocity(isHot);
            double limitingCap = Math.Min(materialCap, regionalCap);

            bool velocityOk = velocityFps <= 0 || velocityFps <= limitingCap;
            if (PlVelocityNote != null)
            {
                string capText = $"Limit: {limitingCap:0.0} fps ({plumbingProfile.Region}, {plumbingProfile.CodeBasis}).";
                bool capsDiffer = Math.Abs(matData.MaxColdFps - matData.MaxHotFps) > 0.001;
                string hotDiff = capsDiffer
                    ? $" Hot water cap for this material is {matData.MaxHotFps:0.0} fps."
                    : string.Empty;
                PlVelocityNote.Text = velocityOk
                    ? "Velocity is within common design guidance. " + capText + hotDiff
                    : "Velocity exceeds regional/material velocity limits. " + capText + hotDiff;
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

                double totalRunLossPsi = psiTotalHw > 0 ? psiTotalHw : psiTotalDarcy;
                double totalRunLossFt = psiPerFtHead > 0 ? totalRunLossPsi / psiPerFtHead : 0;
                double availableHeadFt = psiPerFtHead > 0 ? availableHeadPsi / psiPerFtHead : 0;
                string marginNote;

                if (!hasAvailableHead)
                {
                    marginNote = hasAvailableHeadInput
                        ? "Enter a positive available head value (psi or ft) to check remaining margin."
                        : "Add available static/boost pressure or pump duty (psi or ft) to check remaining margin.";
                }
                else if (totalRunLossPsi <= 0)
                {
                    marginNote = "Provide flow, length, and size to evaluate head margin.";
                }
                else
                {
                    double marginPsi = availableHeadPsi - totalRunLossPsi;
                    double marginFt = psiPerFtHead > 0 ? marginPsi / psiPerFtHead : 0;
                    string marginMagnitude = $"{Math.Abs(marginPsi):0.###} psi";
                    if (marginFt != 0)
                        marginMagnitude += $" ({Math.Abs(marginFt):0.#} ft head)";

                    string runLossDetail = $"Total run loss (friction + fittings): {totalRunLossPsi:0.###} psi";
                    if (totalRunLossFt > 0)
                        runLossDetail += $" ({totalRunLossFt:0.#} ft head).";
                    else
                        runLossDetail += ".";

                    string availableDetail = $"Available head: {availableHeadPsi:0.###} psi";
                    if (availableHeadFt > 0)
                        availableDetail += $" ({availableHeadFt:0.#} ft head).";
                    else
                        availableDetail += ".";

                    string marginDetail = marginPsi >= 0
                        ? $"Remaining margin: {marginMagnitude}."
                        : $"Exceeded by {marginMagnitude}. Consider a pump or larger pipe.";

                    marginNote = string.Join(" ", new[] { runLossDetail, availableDetail, marginDetail });
                }

                PlStatusNote.Text = string.Join(" ", new[] { frictionNote, reynoldsNote, marginNote }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            double waveSpeed = PlumbingCalculator.GetWaveSpeedFps(material);
            _lastPlumbingFittingSnapshot = _plumbingFittings.Select(f => f with { }).ToList();
            double fittingLossPsiUsed = fittingPsi > 0 ? fittingPsi : fittingPsiHw;
            _lastPlumbingExport = new PlumbingExportRow(
                FlowGpm: gpm,
                LengthFt: lengthFt,
                EquivalentLengthFt: plFittingEqLength,
                TotalRunLengthFt: totalRunLengthFt,
                Material: material.DisplayName,
                NominalIn: nominal,
                ResolvedIdIn: idIn,
                UsedAgedC: useAgedC,
                IsHotWater: isHot,
                Fluid: fluidType.ToString(),
                FluidTempF: fluidTempF,
                AntifreezePercent: antifreezePercent,
                FluidDensity: fluidProps.DensityLbmPerFt3,
                FluidNu: fluidProps.KinematicViscosityFt2PerS,
                VelocityFps: velocityFps,
                VelocityLimitFps: limitingCap,
                Reynolds: reynolds,
                DarcyFriction: darcyF,
                HazenPsiPer100Ft: psiPer100Hw,
                HazenPsiTotal: psiTotalHw,
                DarcyPsiPer100Ft: psiPer100Darcy,
                DarcyPsiTotal: psiTotalDarcy,
                HazenCFactor: cFactor,
                RoughnessFt: roughness,
                WaveSpeedFps: waveSpeed,
                SumK: plFittingSumK,
                FittingPsi: fittingLossPsiUsed,
                FittingsList: FittingListSummary(_lastPlumbingFittingSnapshot));
        }

        private void BtnExportPlumbing_Click(object sender, RoutedEventArgs e)
        {
            if (_lastPlumbingExport == null)
            {
                MessageBox.Show("Calculate a pipe run before exporting.", "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var p = _lastPlumbingExport!;
            var sb = new StringBuilder();
            sb.AppendLine("Flow (gpm),Length (ft),Fitting Equivalent Length (ft),Total Run Length (ft),Material,Nominal Size (in),Resolved ID (in),Used Aged C?,Hot Water?,Fluid,Fluid Temp (F),Antifreeze %,Fluid Density (lb/ft3),Fluid Kinematic Nu (ft2/s),Velocity (ft/s),Velocity Limit (ft/s),Reynolds,Darcy f,Hazen-Williams psi/100 ft,Hazen-Williams total psi,Darcy-Weisbach psi/100 ft,Darcy-Weisbach total psi,C-Factor,Roughness (ft),Wave Speed (ft/s),Sum K,Fitting Minor Loss (psi),Fittings");
            sb.AppendLine(string.Join(",", new[]
            {
                CsvEscape(p.FlowGpm),
                CsvEscape(p.LengthFt),
                CsvEscape(p.EquivalentLengthFt),
                CsvEscape(p.TotalRunLengthFt),
                CsvEscape(p.Material),
                CsvEscape(p.NominalIn),
                CsvEscape(p.ResolvedIdIn),
                CsvEscape(p.UsedAgedC),
                CsvEscape(p.IsHotWater),
                CsvEscape(p.Fluid),
                CsvEscape(p.FluidTempF),
                CsvEscape(p.AntifreezePercent),
                CsvEscape(p.FluidDensity),
                CsvEscape(p.FluidNu),
                CsvEscape(p.VelocityFps),
                CsvEscape(p.VelocityLimitFps),
                CsvEscape(p.Reynolds),
                CsvEscape(p.DarcyFriction),
                CsvEscape(p.HazenPsiPer100Ft),
                CsvEscape(p.HazenPsiTotal),
                CsvEscape(p.DarcyPsiPer100Ft),
                CsvEscape(p.DarcyPsiTotal),
                CsvEscape(p.HazenCFactor),
                CsvEscape(p.RoughnessFt),
                CsvEscape(p.WaveSpeedFps),
                CsvEscape(p.SumK),
                CsvEscape(p.FittingPsi),
                CsvEscape(p.FittingsList)
            }));

            if (_lastPlumbingFittingSnapshot.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Fittings");
                sb.AppendLine("Category,Name,Quantity,K,Equivalent Length (ft)");
                foreach (var f in _lastPlumbingFittingSnapshot)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        CsvEscape(f.Fitting.Category),
                        CsvEscape(f.Fitting.Name),
                        CsvEscape(f.Quantity),
                        CsvEscape(f.Fitting.KCoefficient),
                        CsvEscape(f.Fitting.EquivalentLengthFt)
                    }));
                }
            }

            if (SaveCsvToPath("plumbing-export.csv", sb.ToString()))
            {
                MessageBox.Show("Plumbing results exported for Excel review.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
            double fluidTempF = ParseBox(PlFluidTempInput);
            double antifreezePercent = ParseBox(PlAntifreezePercentInput);
            var fluidType = SelectedFluid();

            if (gpm <= 0 || targetPsi100 <= 0)
            {
                MessageBox.Show("Enter both flow (gpm) and target friction (psi/100 ft).",
                                "Inputs Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            var matData = PlumbingCalculator.GetMaterialData(material);
            bool useAgedC = PlUseAgedC?.IsChecked ?? false;
            var fluidProps = PlumbingCalculator.ResolveFluidProperties(fluidType, fluidTempF, antifreezePercent);
            double psiPerFtHead = PlumbingCalculator.PsiPerFtHeadFromDensity(fluidProps.DensityLbmPerFt3);
            double cFactor = (useAgedC ? matData.C_Aged : matData.C_New) * fluidProps.HazenWilliamsCFactorMultiplier;

            double solvedDiameterIn = PlumbingCalculator.SolveDiameterFromHazenWilliams(gpm, targetPsi100, cFactor, psiPerFtHead);
            SetBox(PlSizedDiameterOutput, solvedDiameterIn, "0.###");

            double nearestNominal = FindNearestNominal(material, solvedDiameterIn);
            SetBox(PlSizedNominalOutput, nearestNominal, nearestNominal >= 1 ? "0.##" : "0.###");

            if (PlSizeNote != null)
            {
                if (nearestNominal <= 0)
                {
                    PlSizeNote.Text = "No nominal size in the embedded tables meets or exceeds the solved ID.";
                }
                else
                {
                    double availableId = PlumbingCalculator.GetInnerDiameterIn(material, nearestNominal);
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

        private void InitializeFixtureRows()
        {
            _fixtureRows.CollectionChanged += FixtureRows_CollectionChanged;
            if (_fixtureCatalog.Any())
            {
                AddFixtureRow(_fixtureCatalog[0]);
            }
            else
            {
                AddFixtureRow(new FixtureType("Fixture", 1.0));
            }
        }

        private void FixtureRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (FixtureRow row in e.NewItems)
                {
                    row.PropertyChanged += FixtureRow_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (FixtureRow row in e.OldItems)
                {
                    row.PropertyChanged -= FixtureRow_PropertyChanged;
                }
            }

            UpdateFixtureTotals();
        }

        private void FixtureRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FixtureRow.ResolvedFixtureUnits) ||
                e.PropertyName == nameof(FixtureRow.QuantityText) ||
                e.PropertyName == nameof(FixtureRow.OverrideText) ||
                e.PropertyName == nameof(FixtureRow.FixtureType))
            {
                UpdateFixtureTotals();
            }
        }

        private void AddFixtureRow(FixtureType? type)
        {
            var fixtureType = type ?? _fixtureCatalog.FirstOrDefault() ?? new FixtureType("Fixture", 1.0);
            var row = new FixtureRow(fixtureType);
            _fixtureRows.Add(row);
        }

        private void UpdateFixtureTotals()
        {
            double totalFu = _fixtureRows.Sum(r => r.ResolvedFixtureUnits);
            SetBox(FixtureUnitsInput, totalFu, "0.##");
            double demand = PlumbingCalculator.HunterDemandGpm(totalFu);
            SetBox(FixtureDemandOutput, demand, "0.00");
        }

        private void AddFixtureRow_Click(object sender, RoutedEventArgs e)
        {
            AddFixtureRow(_fixtureCatalog.FirstOrDefault());
        }

        private void RemoveFixtureRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FixtureRow row)
            {
                _fixtureRows.Remove(row);
                if (!_fixtureRows.Any())
                {
                    AddFixtureRow(_fixtureCatalog.FirstOrDefault());
                }
            }
        }

        private void FixtureType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox cb && cb.DataContext is FixtureRow row && cb.SelectedItem is FixtureType fixtureType)
            {
                row.FixtureType = fixtureType;
                UpdateFixtureTotals();
            }
        }

        private void BtnSanitarySize_Click(object sender, RoutedEventArgs e)
        {
            double dfu = ParseBox(SanitaryDfuInput);
            double slope = ParseBox(SanitarySlopeInput);
            double diameter = SanitaryVentCalculator.MinBranchDiameterFromDfu(dfu, slope);

            SetBox(SanitaryDiameterOutput, diameter, "0.##");

            if (SanitaryNote != null)
            {
                if (diameter <= 0)
                {
                    SanitaryNote.Text = "Demand exceeds SanitaryVentCalculator.MinBranchDiameterFromDfu() IPC/UPC branch caps. " +
                                        "Example: 0.0208 = 1/4 in per ft.";
                }
                else
                {
                    SanitaryNote.Text = "Uses SanitaryVentCalculator.MinBranchDiameterFromDfu() IPC/UPC branch DFU capacities; verify stack and trap arm separately. " +
                                        "Enter slope in ft/ft (e.g., 0.0208 = 1/4 in per ft).";
                }
            }
        }

        private void BtnSanitaryAllowable_Click(object sender, RoutedEventArgs e)
        {
            double diameter = ParseBox(SanitaryCheckDiameterInput);
            double slope = ParseBox(SanitaryCheckSlopeInput);
            double allowable = SanitaryVentCalculator.AllowableFixtureUnits(diameter, slope);

            SetBox(SanitaryAllowableOutput, allowable, "0.#");

            if (SanitaryNote != null)
            {
                SanitaryNote.Text = allowable > 0
                    ? "Allowable DFU from SanitaryVentCalculator.AllowableFixtureUnits() using IPC/UPC branch tables."
                    : "Diameter/slope not found in IPC/UPC branch DFU tables.";
            }
        }

        private void BtnVentSize_Click(object sender, RoutedEventArgs e)
        {
            double ventDfu = ParseBox(VentDfuInput);
            double ventLength = ParseBox(VentLengthInput);
            double diameter = SanitaryVentCalculator.VentStackMinDiameter(ventDfu, ventLength);
            SetBox(VentDiameterOutput, diameter, "0.##");

            double allowableForCheck = SanitaryVentCalculator.VentStackAllowableFixtureUnits(diameter, ventLength);
            if (VentAllowableOutput != null)
                SetBox(VentAllowableOutput, allowableForCheck, "0.#");

            if (SanitaryNote != null)
            {
                SanitaryNote.Text = diameter > 0
                    ? "Vent stack sized with SanitaryVentCalculator.VentStackMinDiameter(); capacities per IPC/UPC vent stack tables."
                    : "Vented DFU exceeds SanitaryVentCalculator.VentStackMinDiameter() table.";
            }
        }

        private void BtnVentAllowable_Click(object sender, RoutedEventArgs e)
        {
            double diameter = ParseBox(VentDiameterCheckInput);
            double length = ParseBox(VentLengthCheckInput);
            double allowable = SanitaryVentCalculator.VentStackAllowableFixtureUnits(diameter, length);
            SetBox(VentAllowableOutput, allowable, "0.#");

            if (SanitaryNote != null)
            {
                SanitaryNote.Text = allowable > 0
                    ? "Vent stack capacity from SanitaryVentCalculator.VentStackAllowableFixtureUnits()."
                    : "No matching vent stack entry for that diameter.";
            }
        }

        private void BtnStormSize_Click(object sender, RoutedEventArgs e)
        {
            double area = ParseBox(StormAreaInput);
            double intensity = ParseBox(StormRainfallInput);
            double slope = ParseBox(StormSlopeInput);
            double n = ParseBox(StormRoughnessInput);

            if (n <= 0) n = 0.012; // smooth pipe default

            double flow = StormDrainageCalculator.FlowFromRainfall(area, intensity);
            double diameter = StormDrainageCalculator.FullFlowDiameterFromGpm(flow, slope > 0 ? slope : 0.01, n);

            SetBox(StormFlowOutput, flow, "0.0");
            SetBox(StormDiameterOutput, diameter, "0.##");

            if (StormNote != null)
            {
                StormNote.Text = "Full-pipe Manning sizing via StormDrainageCalculator.FullFlowDiameterFromGpm(); check ponding/leader capacity per local storm code.";
            }
        }

        private void BtnStormPartialSize_Click(object sender, RoutedEventArgs e)
        {
            double area = ParseBox(StormAreaInput);
            double intensity = ParseBox(StormRainfallInput);
            double slope = ParseBox(StormSlopeInput);
            double n = ParseBox(StormRoughnessInput);
            double depthRatio = ParseBox(StormDepthRatioInput);

            if (n <= 0) n = 0.012;
            if (depthRatio <= 0) depthRatio = 0.5;

            double flow = StormDrainageCalculator.FlowFromRainfall(area, intensity);
            double diameter = StormDrainageCalculator.PartialFlowDiameterFromGpm(flow, slope > 0 ? slope : 0.01, depthRatio, n);

            SetBox(StormFlowOutput, flow, "0.0");
            SetBox(StormPartialDiameterOutput, diameter, "0.##");

            if (StormNote != null)
            {
                StormNote.Text = "Partial flow sizing with StormDrainageCalculator.PartialFlowDiameterFromGpm(); depth ratio clamps to 0.05–0.99.";
            }
        }

        private void BtnStormLeaderCheck_Click(object sender, RoutedEventArgs e)
        {
            double leaderFlow = ParseBox(StormLeaderFlowInput);
            double leaderDiameter = ParseBox(StormLeaderDiameterInput);
            double n = ParseBox(StormRoughnessInput);

            if (n <= 0) n = 0.012;

            double capacity = StormDrainageCalculator.VerticalLeaderMaxFlow(leaderDiameter, n);
            double sizedDia = StormDrainageCalculator.VerticalLeaderDiameter(leaderFlow, n);

            SetBox(StormLeaderCapacityOutput, capacity, "0.0");
            SetBox(StormLeaderSizedOutput, sizedDia, "0.##");

            if (StormNote != null)
            {
                StormNote.Text = "Leader check uses StormDrainageCalculator.VerticalLeaderMaxFlow() and VerticalLeaderDiameter(); assumes slope ~1.0.";
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

        private void BtnRecircCalc_Click(object sender, RoutedEventArgs e)
        {
            var material = SelectedMaterial();
            if (material == null)
            {
                MessageBox.Show("Select a pipe material for recirculation calculations.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var matData = PlumbingCalculator.GetMaterialData(material);

            double volumeGal = ParseBox(RecircVolumeInput);
            double turnoverMin = ParseBox(RecircTurnoverInput);
            double heatLossBtuh = ParseBox(RecircHeatLossInput);
            double allowableDeltaT = ParseBox(RecircDeltaTInput);
            double recircDia = ParseBox(RecircDiaInput);
            double recircC = ParseBox(RecircCInput);
            double lengthFt = ParseBox(RecircLengthInput);
            double eqLengthFt = ParseBox(RecircEqLengthInput);

            if (recircC <= 0) recircC = matData.C_New;

            double gpmVol = PlumbingCalculator.RecirculationFlowFromVolume(volumeGal, turnoverMin);
            double gpmHeat = PlumbingCalculator.RecirculationFlowFromHeatLoss(heatLossBtuh, allowableDeltaT);
            double recircGpm = Math.Max(gpmVol, gpmHeat);
            if (recircGpm <= 0)
                recircGpm = gpmVol > 0 ? gpmVol : gpmHeat;

            if (recircDia <= 0)
            {
                MessageBox.Show("Enter a recirculation pipe diameter.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double headFt = recircGpm > 0 ? PlumbingCalculator.RecirculationHeadFt(recircGpm, recircDia, recircC, lengthFt, eqLengthFt) : 0;
            double headPsi = headFt * 0.4335275;

            SetBox(RecircFlowOutput, recircGpm, "0.00");
            SetBox(RecircHeadOutput, headFt, "0.00");
            SetBox(RecircHeadPsiOutput, headPsi, "0.000");

            if (RecircNote != null)
            {
                string basis = gpmVol > 0 && gpmHeat > 0
                    ? "Flow is the max of volume turnover and heat-loss criteria."
                    : gpmHeat > 0
                        ? "Flow based on heat-loss ΔT criterion."
                        : "Flow based on volume turnover criterion.";

                RecircNote.Text = basis + " Head via Hazen-Williams with fitting equivalent length.";
            }
        }

        private void BtnHammerCalc_Click(object sender, RoutedEventArgs e)
        {
            var material = SelectedMaterial();
            if (material == null)
            {
                MessageBox.Show("Select a pipe material to evaluate water hammer.", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double velocityFps = ParseBox(HammerVelocityInput);
            double lengthFt = ParseBox(HammerLengthInput);
            double closureS = ParseBox(HammerClosureInput);
            double staticPsi = ParseBox(HammerStaticInput);

            if (velocityFps <= 0)
            {
                MessageBox.Show("Enter the flowing line velocity (ft/s).", "Inputs Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double waveSpeed = PlumbingCalculator.GetWaveSpeedFps(material);
            double surgePsi = PlumbingCalculator.SurgePressureWithClosure(velocityFps, lengthFt, closureS, material);
            double totalPsi = staticPsi > 0 ? staticPsi + surgePsi : surgePsi;

            SetBox(HammerWaveSpeedOutput, waveSpeed, "0");
            SetBox(HammerSurgeOutput, surgePsi, "0.00");
            SetBox(HammerTotalOutput, totalPsi, "0.00");

            if (HammerNote != null)
            {
                string caution = totalPsi > 150
                    ? "Total pressure exceeds 150 psi—verify pipe ratings and add arrestors."
                    : "Compare total pressure to pipe/valve ratings; add arrestors if near limits.";
                string closureNote = closureS > 0 && lengthFt > 0
                    ? "Closure-time scaling applied when slower than wave travel."
                    : "Instantaneous closure assumed.";
                HammerNote.Text = string.Join(" ", new[] { closureNote, caution });
            }
        }

        private static double FindNearestNominal(PipeMaterialProfile material, double requiredIdIn)
        {
            if (requiredIdIn <= 0) return 0;

            var available = PlumbingCalculator.GetAvailableNominalIds(material)
                                               .OrderBy(kv => kv.Value)
                                               .FirstOrDefault(kv => kv.Value >= requiredIdIn);

            return available.Key;
        }

        private void NavigateToCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FrameworkElement target })
            {
                target.BringIntoView();
            }
        }

        private void BtnPlClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tb in new[]
            {
                PlGpmInput, PlLengthInput, PlNominalInput, PlExplicitIdInput,
                PlFluidTempInput, PlAntifreezePercentInput, PlAvailableHeadPsiInput, PlAvailableHeadFtInput,
                PlResolvedIdOutput, PlVelocityOutput, PlHazenPsi100Output, PlHazenPsiTotalOutput,
                PlDarcyPsi100Output, PlDarcyPsiTotalOutput, PlReOutput, PlFrictionOutput,
                PlFluidDensityOutput, PlFluidNuOutput,
                PlSizeGpmInput, PlTargetPsi100Input, PlSizedDiameterOutput, PlSizedNominalOutput,
                FixtureUnitsInput, FixtureDemandOutput,
                SanitaryDfuInput, SanitarySlopeInput, SanitaryDiameterOutput,
                SanitaryCheckDiameterInput, SanitaryCheckSlopeInput, SanitaryAllowableOutput,
                VentDfuInput, VentLengthInput, VentDiameterOutput, VentDiameterCheckInput, VentLengthCheckInput, VentAllowableOutput,
                StormAreaInput, StormRainfallInput, StormSlopeInput, StormRoughnessInput,
                StormFlowOutput, StormDiameterOutput, StormDepthRatioInput, StormPartialDiameterOutput,
                StormLeaderFlowInput, StormLeaderDiameterInput, StormLeaderCapacityOutput, StormLeaderSizedOutput,
                GasLoadInput, GasLengthInput, GasPressureDropInput, GasSpecificGravityInput,
                GasBasePressureInput, GasDiameterOutput, GasFlowOutput, GasVelocityOutput,
                RecircVolumeInput, RecircTurnoverInput, RecircHeatLossInput, RecircDeltaTInput,
                RecircDiaInput, RecircCInput, RecircLengthInput, RecircEqLengthInput,
                RecircFlowOutput, RecircHeadOutput, RecircHeadPsiOutput,
                HammerVelocityInput, HammerLengthInput, HammerClosureInput, HammerStaticInput,
                HammerWaveSpeedOutput, HammerSurgeOutput, HammerTotalOutput
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

            if (RecircNote != null)
                RecircNote.Text = string.Empty;

            if (HammerNote != null)
                HammerNote.Text = string.Empty;

            if (PlUseAgedC != null)
                PlUseAgedC.IsChecked = false;

            if (PlIsHotWater != null)
                PlIsHotWater.IsChecked = false;

            if (PlAntifreezeTypeCombo != null)
                PlAntifreezeTypeCombo.SelectedItem = PlumbingCalculator.FluidType.Water;

            _plumbingFittings.Clear();
            RefreshPlumbingFittingList();
            UpdatePlumbingFittingTotals();

            _lastPlumbingExport = null;
        }
    }
}
