[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$projectRoot = "c:\Users\wdsyzx\Documents\GitHub\community\Ink Canvas"
$resxPath = Join-Path $projectRoot "Properties\Strings.resx"
$enResxPath = Join-Path $projectRoot "Properties\Strings.en-US.resx"

# Parse Chinese resx
$zhXml = [xml](Get-Content $resxPath -Encoding UTF8)
$zhMap = @{}
foreach ($node in $zhXml.root.data) {
    $key = $node.name
    $val = $node.value
    if ($key -and $val -ne $null) {
        $zhMap[$key] = $val
    }
}

# Parse English resx
$enXml = [xml](Get-Content $enResxPath -Encoding UTF8)
$enMap = @{}
foreach ($node in $enXml.root.data) {
    $key = $node.name
    $val = $node.value
    if ($key -and $val -ne $null) {
        $enMap[$key] = $val
    }
}

# Find duplicate Chinese values with different keys
$zhReverse = @{}
foreach ($key in $zhMap.Keys) {
    $val = $zhMap[$key]
    if (-not $zhReverse.ContainsKey($val)) {
        $zhReverse[$val] = [System.Collections.ArrayList]::new()
    }
    [void]$zhReverse[$val].Add($key)
}

Write-Host "=== Chinese values with multiple keys (potential semantic conflicts) ===" -ForegroundColor Cyan
Write-Host ""

$conflicts = [System.Collections.ArrayList]::new()

foreach ($val in ($zhReverse.Keys | Sort-Object)) {
    $keys = $zhReverse[$val]
    if ($keys.Count -gt 1) {
        # Check if English translations differ
        $enValues = @{}
        foreach ($k in $keys) {
            $enVal = if ($enMap.ContainsKey($k)) { $enMap[$k] } else { "(no en-US entry)" }
            $enValues[$k] = $enVal
        }
        
        $uniqueEnVals = @($enValues.Values | Select-Object -Unique)
        
        if ($uniqueEnVals.Count -gt 1) {
            Write-Host "CONFLICT: Chinese='$val'" -ForegroundColor Red
            foreach ($k in $keys) {
                $enV = $enValues[$k]
                Write-Host "  Key=$k  EN=$enV" -ForegroundColor Yellow
            }
            Write-Host ""
            [void]$conflicts.Add(@{Chinese=$val; Keys=$keys; EnValues=$enValues})
        }
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Total Chinese values with multiple keys: $(($zhReverse.Values | Where-Object { $_.Count -gt 1 }).Count)"
Write-Host "Of those, keys with DIFFERENT English translations (real conflicts): $($conflicts.Count)"
