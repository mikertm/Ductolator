using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RTM.Ductolator.Models
{
    public record MaterialHydraulics(double RoughnessFt, double C_New, double C_Aged, double MaxColdFps, double MaxHotFps)
    {
        public double GetMaxVelocity(bool isHotWater) => isHotWater ? MaxHotFps : MaxColdFps;
    }

    public record PipeMaterialProfile(
        string Key,
        string DisplayName,
        string? ServiceNote,
        MaterialHydraulics Hydraulics,
        double WaveSpeedFps,
        IReadOnlyDictionary<double, double> NominalIdIn)
    {
        public override string ToString() => string.IsNullOrWhiteSpace(ServiceNote)
            ? DisplayName
            : $"{DisplayName} — {ServiceNote}";

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrWhiteSpace(Key)) yield return "Key is required.";
            if (string.IsNullOrWhiteSpace(DisplayName)) yield return "Display name is required.";
            if (Hydraulics.RoughnessFt <= 0) yield return "Roughness must be positive (ft).";
            if (Hydraulics.C_New <= 0 || Hydraulics.C_Aged <= 0) yield return "Hazen-Williams C values must be positive.";
            if (Hydraulics.MaxColdFps <= 0 || Hydraulics.MaxHotFps <= 0) yield return "Velocity caps must be positive.";
            if (WaveSpeedFps <= 0) yield return "Wave speed must be positive (ft/s).";
            if (!NominalIdIn.Any()) yield return "At least one nominal/ID pair is required.";
            foreach (var kvp in NominalIdIn)
            {
                if (kvp.Key <= 0 || kvp.Value <= 0)
                    yield return $"Nominal {kvp.Key} in has non-positive ID {kvp.Value} in.";
            }
        }

        public bool TryGetInnerDiameter(double nominalIn, out double idIn)
        {
            return NominalIdIn.TryGetValue(nominalIn, out idIn);
        }
    }

    public record CatalogLoadReport(
        string Folder,
        int MaterialCount,
        int DuctFittingCount,
        int PipeFittingCount,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Errors)
    {
        public bool IsHealthy => !Errors.Any();
    }

    public record CatalogSet(
        IReadOnlyList<PipeMaterialProfile> Materials,
        IReadOnlyList<DuctFitting> DuctFittings,
        IReadOnlyList<PipeFitting> PipeFittings,
        CatalogLoadReport Report);

    public static class RuntimeCatalogs
    {
        public static IReadOnlyList<PipeMaterialProfile> Materials { get; private set; } = BuiltInMaterials();
        public static IReadOnlyList<DuctFitting> DuctFittings { get; private set; } = BuiltInDuctFittings();
        public static IReadOnlyList<PipeFitting> PipeFittings { get; private set; } = BuiltInPipeFittings();
        public static CatalogLoadReport LastReport { get; private set; } = new("(built-in)", Materials.Count, DuctFittings.Count, PipeFittings.Count, Array.Empty<string>(), Array.Empty<string>());

        public static CatalogLoadReport ReloadFromFolder(string? folder)
        {
            var result = CatalogLoader.Load(folder, BuiltInMaterials(), BuiltInDuctFittings(), BuiltInPipeFittings());
            Materials = result.Materials;
            DuctFittings = result.DuctFittings;
            PipeFittings = result.PipeFittings;
            LastReport = result.Report;
            return result.Report;
        }

        private static IReadOnlyList<PipeMaterialProfile> BuiltInMaterials()
        {
            return new List<PipeMaterialProfile>
            {
                new(
                    Key: "copper_type_k",
                    DisplayName: "Copper Type K",
                    ServiceNote: "domestic water / heating",
                    Hydraulics: new MaterialHydraulics(1.5e-6, 150, 135, 8.0, 5.0),
                    WaveSpeedFps: 4000,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.527 },
                        { 0.75, 0.745 },
                        { 1.0, 0.995 },
                        { 1.25, 1.245 },
                        { 1.5, 1.481 },
                        { 2.0, 1.939 },
                        { 2.5, 2.353 },
                        { 3.0, 2.889 },
                        { 3.5, 3.389 },
                        { 4.0, 3.826 }
                    }),
                new(
                    Key: "copper_type_l",
                    DisplayName: "Copper Type L",
                    ServiceNote: "domestic water / heating",
                    Hydraulics: new MaterialHydraulics(1.5e-6, 150, 135, 8.0, 5.0),
                    WaveSpeedFps: 4000,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.545 },
                        { 0.75, 0.785 },
                        { 1.0, 1.025 },
                        { 1.25, 1.265 },
                        { 1.5, 1.481 },
                        { 2.0, 1.939 },
                        { 2.5, 2.347 },
                        { 3.0, 2.889 },
                        { 3.5, 3.389 },
                        { 4.0, 3.826 },
                        { 5.0, 4.813 },
                        { 6.0, 5.761 }
                    }),
                new(
                    Key: "copper_type_m",
                    DisplayName: "Copper Type M",
                    ServiceNote: "low-pressure water",
                    Hydraulics: new MaterialHydraulics(1.5e-6, 150, 130, 8.0, 5.0),
                    WaveSpeedFps: 4000,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.569 },
                        { 0.75, 0.811 },
                        { 1.0, 1.055 },
                        { 1.25, 1.291 },
                        { 1.5, 1.503 },
                        { 2.0, 1.947 },
                        { 2.5, 2.371 },
                        { 3.0, 2.907 },
                        { 3.5, 3.407 },
                        { 4.0, 3.834 }
                    }),
                new(
                    Key: "steel_sch_40",
                    DisplayName: "Steel Schedule 40",
                    ServiceNote: "process / hydronic / fire",
                    Hydraulics: new MaterialHydraulics(0.00015, 120, 100, 6.0, 5.0),
                    WaveSpeedFps: 4000,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.622 },
                        { 0.75, 0.824 },
                        { 1.0, 1.049 },
                        { 1.25, 1.38 },
                        { 1.5, 1.61 },
                        { 2.0, 2.067 },
                        { 2.5, 2.469 },
                        { 3.0, 3.068 },
                        { 3.5, 3.548 },
                        { 4.0, 4.026 },
                        { 5.0, 5.047 },
                        { 6.0, 6.065 },
                        { 8.0, 7.981 },
                        { 10.0, 10.02 },
                        { 12.0, 11.938 }
                    }),
                new(
                    Key: "stainless_sch_10s",
                    DisplayName: "Stainless Steel Sch 10S",
                    ServiceNote: "process / RO / gray water",
                    Hydraulics: new MaterialHydraulics(0.000006, 145, 140, 8.0, 6.0),
                    WaveSpeedFps: 3700,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 1.0, 1.097 },
                        { 1.5, 1.61 },
                        { 2.0, 2.067 },
                        { 2.5, 2.469 },
                        { 3.0, 3.068 },
                        { 4.0, 4.026 },
                        { 5.0, 5.047 },
                        { 6.0, 6.065 }
                    }),
                new(
                    Key: "cpvc_sch_80",
                    DisplayName: "CPVC Sch 80",
                    ServiceNote: "hot water / corrosive compatible",
                    Hydraulics: new MaterialHydraulics(0.000005, 150, 140, 8.0, 8.0),
                    WaveSpeedFps: 1500,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.526 },
                        { 0.75, 0.742 },
                        { 1.0, 0.957 },
                        { 1.25, 1.278 },
                        { 1.5, 1.470 },
                        { 2.0, 1.913 },
                        { 2.5, 2.290 },
                        { 3.0, 2.864 },
                        { 4.0, 3.786 },
                        { 6.0, 5.709 }
                    }),
                new(
                    Key: "pvc_sch_40",
                    DisplayName: "PVC Sch 40",
                    ServiceNote: "cold water / condensate",
                    Hydraulics: new MaterialHydraulics(0.000005, 150, 140, 10.0, 8.0),
                    WaveSpeedFps: 1400,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.622 },
                        { 0.75, 0.824 },
                        { 1.0, 1.049 },
                        { 1.25, 1.38 },
                        { 1.5, 1.61 },
                        { 2.0, 2.067 },
                        { 2.5, 2.469 },
                        { 3.0, 3.068 },
                        { 4.0, 4.026 },
                        { 6.0, 6.065 }
                    }),
                new(
                    Key: "pvc_sdr26",
                    DisplayName: "PVC SDR-26",
                    ServiceNote: "reclaimed / utility / effluent",
                    Hydraulics: new MaterialHydraulics(0.000005, 150, 140, 10.0, 8.0),
                    WaveSpeedFps: 1400,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 3.0, 3.26 },
                        { 4.0, 4.3 },
                        { 6.0, 6.31 },
                        { 8.0, 8.32 }
                    }),
                new(
                    Key: "pvc_sdr35",
                    DisplayName: "PVC SDR-35",
                    ServiceNote: "sanitary / storm sewer",
                    Hydraulics: new MaterialHydraulics(0.000005, 150, 140, 10.0, 8.0),
                    WaveSpeedFps: 1400,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 3.0, 3.26 },
                        { 4.0, 4.3 },
                        { 6.0, 6.31 },
                        { 8.0, 8.28 },
                        { 10.0, 10.34 },
                        { 12.0, 12.32 }
                    }),
                new(
                    Key: "pex_tubing",
                    DisplayName: "PEX Tubing",
                    ServiceNote: "hot/cold distribution",
                    Hydraulics: new MaterialHydraulics(0.0001, 150, 140, 8.0, 5.0),
                    WaveSpeedFps: 1500,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 0.5, 0.475 },
                        { 0.75, 0.671 },
                        { 1.0, 0.875 },
                        { 1.25, 1.055 },
                        { 1.5, 1.265 },
                        { 2.0, 1.635 }
                    }),
                new(
                    Key: "cast_iron_no_hub",
                    DisplayName: "Cast Iron (NH) DWV",
                    ServiceNote: "sanitary / storm drainage",
                    Hydraulics: new MaterialHydraulics(0.00085, 110, 90, 10.0, 8.0),
                    WaveSpeedFps: 3000,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 2.0, 2.125 },
                        { 3.0, 3.141 },
                        { 4.0, 4.090 },
                        { 5.0, 5.151 },
                        { 6.0, 6.186 },
                        { 8.0, 8.250 }
                    }),
                new(
                    Key: "ductile_iron_cl",
                    DisplayName: "Ductile Iron CL",
                    ServiceNote: "process / gray water / fire",
                    Hydraulics: new MaterialHydraulics(0.00085, 140, 125, 12.0, 10.0),
                    WaveSpeedFps: 3500,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 4.0, 4.26 },
                        { 6.0, 6.28 },
                        { 8.0, 8.32 },
                        { 10.0, 10.34 },
                        { 12.0, 12.36 }
                    }),
                new(
                    Key: "hdpe_sdr11",
                    DisplayName: "HDPE SDR-11",
                    ServiceNote: "water / industrial / gas",
                    Hydraulics: new MaterialHydraulics(0.000006, 150, 140, 10.0, 8.0),
                    WaveSpeedFps: 1200,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 1.0, 0.995 },
                        { 2.0, 1.939 },
                        { 3.0, 2.889 },
                        { 4.0, 3.826 },
                        { 6.0, 5.708 }
                    }),
                new(
                    Key: "ppr_sdr7_4",
                    DisplayName: "PP-R SDR-7.4",
                    ServiceNote: "hot water / heating",
                    Hydraulics: new MaterialHydraulics(0.000006, 150, 140, 8.0, 5.0),
                    WaveSpeedFps: 1200,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 1.0, 0.866 },
                        { 1.5, 1.299 },
                        { 2.0, 1.732 },
                        { 3.0, 2.598 },
                        { 4.0, 3.464 }
                    }),
                new(
                    Key: "lined_steel_cement",
                    DisplayName: "Cement-lined steel",
                    ServiceNote: "lined industrial",
                    Hydraulics: new MaterialHydraulics(0.0001, 140, 125, 8.0, 6.0),
                    WaveSpeedFps: 3600,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 2.0, 2.067 },
                        { 4.0, 4.026 },
                        { 6.0, 6.065 }
                    }),
                new(
                    Key: "aluminum",
                    DisplayName: "Aluminum tube",
                    ServiceNote: "lightweight / specialty",
                    Hydraulics: new MaterialHydraulics(0.00004, 140, 130, 8.0, 6.0),
                    WaveSpeedFps: 4200,
                    NominalIdIn: new Dictionary<double, double>
                    {
                        { 1.0, 0.995 },
                        { 2.0, 1.939 },
                        { 3.0, 2.889 },
                        { 4.0, 3.826 }
                    })
            };
        }

        private static IReadOnlyList<DuctFitting> BuiltInDuctFittings() => new List<DuctFitting>
        {
            new("Elbow", "Smooth radius elbow (R/D=1.5)", 0.15, 10),
            new("Elbow", "Medium radius elbow (R/D=1.0)", 0.25, 15),
            new("Elbow", "Mitered/square elbow with vanes", 0.45, 25),
            new("Elbow", "Sharp mitered elbow without vanes", 1.50, 55),
            new("Elbow", "45° elbow", 0.08, 6),
            new("Branch/Tee", "Straight-through tee", 0.60, 25),
            new("Branch/Tee", "45° side takeoff", 0.40, 18),
            new("Branch/Tee", "90° branch takeoff", 1.00, 40),
            new("Transition", "5° conical transition", 0.05, 5),
            new("Transition", "15° conical transition", 0.10, 8),
            new("Transition", "Square-to-round bell mouth", 0.04, 4)
        };

        private static IReadOnlyList<PipeFitting> BuiltInPipeFittings() => new List<PipeFitting>
        {
            new("Elbow", "Long-radius 90° elbow", 0.75, 30),
            new("Elbow", "Standard 90° elbow", 1.50, 50),
            new("Elbow", "45° elbow", 0.40, 15),
            new("Branch/Tee", "Straight-through tee", 0.60, 20),
            new("Branch/Tee", "Branch side of tee", 1.80, 60),
            new("Branch/Tee", "Wye (45°)", 0.75, 25),
            new("Valve", "Gate valve (open)", 0.19, 8),
            new("Valve", "Ball valve (open)", 0.05, 3),
            new("Valve", "Globe valve (open)", 10.0, 340),
            new("Coupling", "Coupling / union", 0.04, 1),
            new("Coupling", "Check valve (swing)", 2.5, 85)
        };
    }

    internal static class CatalogLoader
    {
        private static readonly JsonSerializerOptions CatalogJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static CatalogSet Load(string? folder,
                                      IReadOnlyList<PipeMaterialProfile> defaults,
                                      IReadOnlyList<DuctFitting> defaultDuct,
                                      IReadOnlyList<PipeFitting> defaultPipe)
        {
            var materials = defaults.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
            var ductFittings = defaultDuct.ToList();
            var pipeFittings = defaultPipe.ToList();
            var warnings = new List<string>();
            var errors = new List<string>();
            string folderPath = string.IsNullOrWhiteSpace(folder) ? "(built-in)" : folder.Trim();

            if (folderPath == "(built-in)")
            {
                return new CatalogSet(materials.Values.ToList(), ductFittings, pipeFittings,
                    new CatalogLoadReport(folderPath, materials.Count, ductFittings.Count, pipeFittings.Count, warnings, errors));
            }

            if (!Directory.Exists(folderPath))
            {
                errors.Add($"Folder not found: {folderPath}");
                warnings.Add("Using built-in catalogs because the folder was not found.");
                return new CatalogSet(materials.Values.ToList(), ductFittings, pipeFittings,
                    new CatalogLoadReport(folderPath, materials.Count, ductFittings.Count, pipeFittings.Count, warnings, errors));
            }

            try
            {
                LoadMaterials(folderPath, materials, warnings, errors);
                LoadFittings(folderPath, ductFittings, pipeFittings, warnings, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to load catalogs: {ex.Message}");
                materials = defaults.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
                ductFittings = defaultDuct.ToList();
                pipeFittings = defaultPipe.ToList();
            }

            if (errors.Any())
            {
                warnings.Add("Using built-in catalogs because custom files contained errors.");
                materials = defaults.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
                ductFittings = defaultDuct.ToList();
                pipeFittings = defaultPipe.ToList();
            }

            return new CatalogSet(materials.Values.ToList(), ductFittings, pipeFittings,
                new CatalogLoadReport(folderPath, materials.Count, ductFittings.Count, pipeFittings.Count, warnings, errors));
        }

        private static void LoadMaterials(string folder,
                                          Dictionary<string, PipeMaterialProfile> materials,
                                          List<string> warnings,
                                          List<string> errors)
        {
            string jsonPath = Path.Combine(folder, "materials.json");
            string csvPath = Path.Combine(folder, "materials.csv");

            if (File.Exists(jsonPath))
            {
                try
                {
                    var jsonMaterials = JsonSerializer.Deserialize<List<MaterialJsonModel>>(File.ReadAllText(jsonPath), CatalogJsonOptions);
                    if (jsonMaterials != null)
                    {
                        foreach (var mat in jsonMaterials)
                        {
                            var profile = mat.ToProfile();
                            ApplyMaterial(profile, materials, warnings, errors, jsonPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"materials.json parse error: {ex.Message}");
                }
            }

            if (File.Exists(csvPath))
            {
                try
                {
                    var grouped = new Dictionary<string, CsvMaterialAccumulator>(StringComparer.OrdinalIgnoreCase);
                    foreach (var line in File.ReadAllLines(csvPath).Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var cols = SplitCsv(line);
                        if (cols.Length < 11)
                        {
                            warnings.Add($"materials.csv row skipped: expected 11 columns, got {cols.Length}.");
                            continue;
                        }

                        string key = cols[0].Trim();
                        if (!grouped.TryGetValue(key, out var acc))
                        {
                            acc = new CsvMaterialAccumulator
                            {
                                Key = key,
                                DisplayName = cols[1].Trim(),
                                ServiceNote = cols[2].Trim(),
                                Hydraulics = ParseHydraulics(cols[3], cols[4], cols[5], cols[6], cols[7]),
                                WaveSpeedFps = ParseDouble(cols[8]),
                                NominalIdIn = new()
                            };
                            grouped[key] = acc;
                        }

                        double nominal = ParseDouble(cols[9]);
                        double id = ParseDouble(cols[10]);
                        if (nominal > 0 && id > 0)
                            acc.NominalIdIn[nominal] = id;
                    }

                    foreach (var acc in grouped.Values)
                    {
                        var profile = acc.ToProfile();
                        ApplyMaterial(profile, materials, warnings, errors, csvPath);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"materials.csv parse error: {ex.Message}");
                }
            }
        }

        private static void ApplyMaterial(PipeMaterialProfile profile,
                                          Dictionary<string, PipeMaterialProfile> materials,
                                          List<string> warnings,
                                          List<string> errors,
                                          string source)
        {
            var validation = profile.Validate().ToList();
            if (validation.Any())
            {
                errors.Add($"Material '{profile.DisplayName}' invalid from {source}: {string.Join("; ", validation)}");
                return;
            }

            materials[profile.Key] = profile;
        }

        private static void LoadFittings(string folder,
                                         List<DuctFitting> ductFittings,
                                         List<PipeFitting> pipeFittings,
                                         List<string> warnings,
                                         List<string> errors)
        {
            string jsonPath = Path.Combine(folder, "fittings.json");
            string csvPath = Path.Combine(folder, "fittings.csv");

            if (File.Exists(jsonPath))
            {
                try
                {
                    var fittingModels = JsonSerializer.Deserialize<List<FittingJsonModel>>(File.ReadAllText(jsonPath), CatalogJsonOptions);
                    if (fittingModels != null)
                    {
                        foreach (var model in fittingModels)
                        {
                            AddFitting(model.Type, model.Category, model.Name, model.K, model.EquivalentLengthFt, ductFittings, pipeFittings, warnings, errors, jsonPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"fittings.json parse error: {ex.Message}");
                }
            }

            if (File.Exists(csvPath))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(csvPath).Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var cols = SplitCsv(line);
                        if (cols.Length < 5)
                        {
                            warnings.Add($"fittings.csv row skipped: expected 5 columns, got {cols.Length}.");
                            continue;
                        }
                        AddFitting(cols[0], cols[1], cols[2], ParseDouble(cols[3]), ParseDouble(cols[4]), ductFittings, pipeFittings, warnings, errors, csvPath);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"fittings.csv parse error: {ex.Message}");
                }
            }
        }

        private static void AddFitting(string? type,
                                       string? category,
                                       string? name,
                                       double k,
                                       double eqLen,
                                       List<DuctFitting> ductFittings,
                                       List<PipeFitting> pipeFittings,
                                       List<string> warnings,
                                       List<string> errors,
                                       string source)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(name))
            {
                warnings.Add($"Fitting skipped from {source}: missing type/category/name.");
                return;
            }

            if (k <= 0 || eqLen < 0)
            {
                warnings.Add($"Fitting '{name}' skipped from {source}: K and equivalent length must be positive.");
                return;
            }

            if (type.Equals("duct", StringComparison.OrdinalIgnoreCase))
            {
                ductFittings.Add(new DuctFitting(category.Trim(), name.Trim(), k, eqLen));
            }
            else if (type.Equals("pipe", StringComparison.OrdinalIgnoreCase))
            {
                pipeFittings.Add(new PipeFitting(category.Trim(), name.Trim(), k, eqLen));
            }
            else
            {
                warnings.Add($"Fitting '{name}' skipped from {source}: type must be 'duct' or 'pipe'.");
            }
        }

        private static MaterialHydraulics ParseHydraulics(string roughness,
                                                           string cNew,
                                                           string cAged,
                                                           string maxCold,
                                                           string maxHot)
        {
            return new MaterialHydraulics(ParseDouble(roughness), ParseDouble(cNew), ParseDouble(cAged), ParseDouble(maxCold), ParseDouble(maxHot));
        }

        private static double ParseDouble(string value)
        {
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result);
            return result;
        }

        private static string[] SplitCsv(string line)
        {
            var values = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            values.Add(current.ToString());
            return values.ToArray();
        }

        private class MaterialJsonModel
        {
            public string Key { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
            public string? ServiceNote { get; set; }
            public double RoughnessFt { get; set; }
            public double CNew { get; set; }
            public double CAged { get; set; }
            public double MaxColdFps { get; set; }
            public double MaxHotFps { get; set; }
            public double WaveSpeedFps { get; set; }
            public List<MaterialSize> NominalSizes { get; set; } = new();

            public PipeMaterialProfile ToProfile()
            {
                var hydraulics = new MaterialHydraulics(RoughnessFt, CNew, CAged, MaxColdFps, MaxHotFps);
                var map = NominalSizes.ToDictionary(s => s.NominalIn, s => s.IdIn);
                return new PipeMaterialProfile(Key, DisplayName ?? Key, ServiceNote, hydraulics, WaveSpeedFps, map);
            }
        }

        private class MaterialSize
        {
            public double NominalIn { get; set; }
            public double IdIn { get; set; }
        }

        private class FittingJsonModel
        {
            public string Type { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public double K { get; set; }
            public double EquivalentLengthFt { get; set; }
        }

        private class CsvMaterialAccumulator
        {
            public string Key { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string ServiceNote { get; set; } = string.Empty;
            public MaterialHydraulics Hydraulics { get; set; } = new(0, 0, 0, 0, 0);
            public double WaveSpeedFps { get; set; }
            public Dictionary<double, double> NominalIdIn { get; set; } = new();

            public PipeMaterialProfile ToProfile() => new(Key, DisplayName, ServiceNote, Hydraulics, WaveSpeedFps, NominalIdIn);
        }
    }
}
