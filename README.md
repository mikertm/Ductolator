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
