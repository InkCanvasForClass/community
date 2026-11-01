param(
    [Parameter(Mandatory = $true)][string]$TouchSuitePath,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$pinnedCommit = '719f06e0d173e26b285de0693236b6561476f81f'
$actualCommit = & git -C $TouchSuitePath rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actualCommit.Trim() -ne $pinnedCommit) { throw "Expected TouchSuite $pinnedCommit" }
$repoRoot = Split-Path $PSScriptRoot -Parent
$referenceDir = Join-Path $repoRoot 'artifacts/tests/TouchSuiteParity'
New-Item -ItemType Directory -Force -Path $referenceDir | Out-Null
foreach ($name in @('EraserEngine.cs', 'ContactTypes.cs')) {
    $relative = "src/TouchSuite.App/Eraser/$name"
    # Read committed content, so an edited reference working tree cannot change the oracle.
    $sourceLines = & git -C $TouchSuitePath show "${pinnedCommit}:$relative"
    if ($LASTEXITCODE -ne 0) { throw "Cannot read $relative" }
    $sourceText = $sourceLines -join "`n"
    if ($name -eq 'EraserEngine.cs') {
        # The only behavioral substitution is a deterministic clock shared by both engines.
        $sourceText = $sourceText.Replace('Environment.TickCount64', 'global::ReferenceClock.Now')
    }
    [IO.File]::WriteAllText((Join-Path $referenceDir $name), $sourceText)
}
& $Dotnet run --project (Join-Path $PSScriptRoot 'TouchSuiteParity/TouchSuiteParity.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'TouchSuite parity verification failed' }
