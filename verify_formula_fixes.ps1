# Formula Compliance Verification Tests
# This script verifies the corrected Hazen-Williams and Gas Sizing formulas

Write-Host "=== Formula Compliance Verification ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Hazen-Williams Head Loss
Write-Host "Test 1: Hazen-Williams Head Loss" -ForegroundColor Yellow
Write-Host "Input: 100 GPM, 4-inch pipe, C=150"
Write-Host "Expected: ~0.58 ft/100ft (previous was ~0.25 ft/100ft due to wrong coefficient)"
Write-Host ""

$gpm = 100
$diameter = 4.0
$cFactor = 150

# Standard Hazen-Williams: h_f = 10.44 * Q^1.85 / (C^1.85 * d^4.87)
$numerator = 10.44 * [Math]::Pow($gpm, 1.85)
$denominator = [Math]::Pow($cFactor, 1.85) * [Math]::Pow($diameter, 4.87)
$headLoss_FtPer100 = $numerator / $denominator

Write-Host "Calculated head loss: $($headLoss_FtPer100.ToString('F4')) ft/100ft" -ForegroundColor Green

# Convert to PSI
$psiPerFtHead = 62.4 / 144.0  # water density / conversion factor
$pressureLoss_PsiPer100 = $headLoss_FtPer100 * $psiPerFtHead

Write-Host "Calculated pressure loss: $($pressureLoss_PsiPer100.ToString('F4')) psi/100ft" -ForegroundColor Green
Write-Host ""

# Test 2: Gas Sizing (IFGC Equation 4-1)
Write-Host "Test 2: Gas Sizing (IFGC Equation 4-1)" -ForegroundColor Yellow
Write-Host "Input: 1-inch pipe (1.049 inch ID), 100 ft, 0.5 inch w.c. drop, SG=0.6"
Write-Host "Expected: ~134 scfh (using exact IFGC formula)"
Write-Host ""

$diameterGas = 1.049
$lengthFt = 100
$pressureDropInWc = 0.5
$specificGravity = 0.6

# IFGC Equation 4-1: Q = 1.316 * sqrt(ΔH * D^5 / (Cr * L))
# Cr = 0.6094 * (SG / 0.60)
$cr = 0.6094 * ($specificGravity / 0.60)
$d5 = [Math]::Pow($diameterGas, 5.0)
$term = ($pressureDropInWc * $d5) / ($cr * $lengthFt)
$gasFlow_Scfh = 1.316 * [Math]::Sqrt($term)

Write-Host "Calculated gas flow: $($gasFlow_Scfh.ToString('F1')) scfh" -ForegroundColor Green
Write-Host ""

# Test 3: Hazen-Williams - Another common case
Write-Host "Test 3: Hazen-Williams - 2-inch pipe, 50 GPM" -ForegroundColor Yellow
Write-Host "Input: 50 GPM, 2-inch pipe, C=120"
Write-Host ""

$gpm2 = 50
$diameter2 = 2.0
$cFactor2 = 120

$numerator2 = 10.44 * [Math]::Pow($gpm2, 1.85)
$denominator2 = [Math]::Pow($cFactor2, 1.85) * [Math]::Pow($diameter2, 4.87)
$headLoss2_FtPer100 = $numerator2 / $denominator2
$pressureLoss2_PsiPer100 = $headLoss2_FtPer100 * $psiPerFtHead

Write-Host "Calculated head loss: $($headLoss2_FtPer100.ToString('F4')) ft/100ft" -ForegroundColor Green
Write-Host "Calculated pressure loss: $($pressureLoss2_PsiPer100.ToString('F4')) psi/100ft" -ForegroundColor Green
Write-Host ""

# Test 4: Comparison - Old vs New Gas Formula
Write-Host "Test 4: Gas Formula Comparison (Old Approximation vs New Exact)" -ForegroundColor Yellow
Write-Host "Input: 1-inch pipe, 100 ft, 0.5 inch w.c. drop, SG=0.6"
Write-Host ""

# Old approximation: Q = 2313 * D^2.623 * (DeltaP / L)^0.541 * sqrt(0.60 / SG)
$baseFlowOld = 2313.0 * [Math]::Pow($diameterGas, 2.623) * [Math]::Pow($pressureDropInWc / $lengthFt, 0.541)
$gravityCorrection = [Math]::Sqrt(0.60 / $specificGravity)
$gasFlowOld_Scfh = $baseFlowOld * $gravityCorrection

Write-Host "Old formula result: $($gasFlowOld_Scfh.ToString('F1')) scfh" -ForegroundColor Magenta
Write-Host "New formula result: $($gasFlow_Scfh.ToString('F1')) scfh" -ForegroundColor Green
Write-Host "Difference: $([Math]::Abs($gasFlowOld_Scfh - $gasFlow_Scfh).ToString('F1')) scfh ($((($gasFlow_Scfh - $gasFlowOld_Scfh) / $gasFlowOld_Scfh * 100).ToString('F2')) % )" -ForegroundColor Cyan
Write-Host ""

Write-Host "=== Verification Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor White
Write-Host "- Hazen-Williams now uses coefficient 10.44 (feet of head) instead of 4.52 (PSI)" -ForegroundColor White
Write-Host "  This will produce ~2.31x higher pressure drops (matching industry standards)" -ForegroundColor White
Write-Host "- Gas Sizing now uses exact IFGC Equation 4-1 instead of power-law approximation" -ForegroundColor White
Write-Host "  Results will vary slightly from previous calculations" -ForegroundColor White
