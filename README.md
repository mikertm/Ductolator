# Ductolator
Core duct sizing ASHRAE compliant engineering tool.

## Capabilities

- Air duct sizing and equal-friction conversions using ASHRAE/SMACNA practice,
  including altitude/temperature-adjusted air properties, velocity pressure, and
  fitting/straight-run total static drop outputs.
- Air duct sizing and equal-friction conversions using ASHRAE/SMACNA practice.
- Plumbing water sizing helpers (Hazen-Williams and Darcy–Weisbach) aligned with
  common U.S. standards (ASHRAE/ASPE/UPC design guidance).
- Embedded pipe material tables (Type K/L/M copper, Schedule 40 steel/PVC, CPVC
  80, PEX) with standard IDs, roughness, Hazen-Williams C factors, and velocity
  limits for hot and cold water services.
 - Runtime material and fitting catalogs: point the app at JSON/CSV libraries
   to extend the built-in ASHRAE/SMACNA/ASPE tables. Invalid files surface
   parse errors in the UI and automatically fall back to the defaults.
- Flow/velocity solvers, pressure-drop aggregation for straight runs and fitting
  losses, and equivalent-length helpers to mirror common sizing chart workflows.
- A single WPF window with tabbed duct and plumbing tools, inline guidance, and
  read-only outputs to prevent accidental edits during engineering checks.
- Unit-labelled inputs with tooltips, regional code callouts, and friction/velocity
  status notes to help engineers keep entries within IMC/SMACNA/IPC/UPC practices.
- Regional code profiles (California, Chicago/Illinois, Florida, Texas, national
  IMC/IPC/SMACNA baselines) that set conservative velocity and friction caps for
  both duct and plumbing calculations.
- Fixture-unit to demand conversion (Hunter curve), sanitary DFU sizing, storm
  drainage (rainfall intensity + Manning full-pipe solver), and low-pressure gas
  sizing helpers aligned with IPC/UPC/IFGC/NFPA practice so plumbers can size
  domestic, storm, and gas services alongside ducts.
- SMACNA pressure-class selection, leakage estimation, and fan brake horsepower
  outputs to pair friction sizing with casing and fan checks.
- Duct heat-gain/loss helper with insulation guidance so supply temperature drops
  can be bounded by target ΔT over known surface areas and lengths.
- Domestic hot-water recirculation flow/head calculator (volume-turnover or
  heat-loss driven) and water-hammer surge checker with material-specific wave
  speeds to flag transient overpressure against common pipe ratings.

## Custom materials and fittings

- Place `materials.json`/`materials.csv` and/or `fittings.json`/`fittings.csv`
  in a folder, then either set the `CustomCatalogFolder` key in
  `src/RTM.Ductolator/App.config` or use the **Custom catalog folder** browse
  control at the top of the UI. The app merges any valid entries with the
  built-in catalog and shows warnings/errors if parsing fails.
- Materials schema (JSON array):
  ```json
  {
    "key": "pvc_sch_40",
    "displayName": "PVC Sch 40",
    "serviceNote": "condensate / water",
    "roughnessFt": 0.000005,
    "cNew": 150,
    "cAged": 140,
    "maxColdFps": 10.0,
    "maxHotFps": 8.0,
    "waveSpeedFps": 1400,
    "nominalSizes": [ { "nominalIn": 2.0, "idIn": 2.067 } ]
  }
  ```
  CSV headers mirror the JSON fields with one row per nominal size: `Key,DisplayName,ServiceNote,RoughnessFt,CNew,CAged,MaxColdFps,MaxHotFps,WaveSpeedFps,NominalIn,IdIn`.
- Fittings schema (JSON array):
  ```json
  { "type": "pipe", "category": "Valve", "name": "Full-port ball valve", "k": 0.05, "equivalentLengthFt": 3 }
  ```
  CSV headers: `Type,Category,Name,K,EquivalentLengthFt`. `Type` must be `duct`
  or `pipe`; K values must be positive; equivalent length may be 0 for pure K
  libraries.
- Example template libraries seeded with common copper/steel/stainless/cast
  iron/ductile iron, PVC/CPVC/PEX/HDPE/PP-R/lined steel/aluminum materials and
  duct/pipe/gas fittings live in `CatalogTemplates/` for easy copy/extension.
