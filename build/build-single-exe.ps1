# Build script to restore packages and build a single EXE (Release|x64)
# Usage: Open Developer PowerShell for VS (or ensure MSBuild is on PATH), then:
#   .\build\build-single-exe.ps1

param(
    [switch]$Pack
)

Write-Host "Checking for MSBuild..." -ForegroundColor Cyan
$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
if (-not $msbuild) {
    Write-Host "MSBuild not found on PATH. Please install 'Build Tools for Visual Studio' or open 'Developer Command Prompt for VS'." -ForegroundColor Yellow
    Write-Host "Instructions: https://learn.microsoft.com/visualstudio/install/install-visual-studio?view=vs-2022#install-build-tools" -ForegroundColor Yellow
    exit 1
}

Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
nuget restore "Ink Canvas.sln" -Verbosity minimal

Write-Host "Building Release|x64..." -ForegroundColor Cyan
msbuild "Ink Canvas.sln" /p:Configuration=Release /p:Platform=x64 /m /t:Rebuild

# Copy outputs to release folder
$releaseDir = Join-Path -Path (Get-Location) -ChildPath "release"
if (!(Test-Path $releaseDir)) { New-Item -Path $releaseDir -ItemType Directory | Out-Null }

$builtPaths = @(
    "Ink Canvas\bin\x64\Release\*",
    "Ink Canvas\bin\Release\net472\*"
)

foreach ($p in $builtPaths) {
    if (Test-Path $p) {
        Write-Host "Copying $p to release..." -ForegroundColor Green
        Copy-Item $p $releaseDir -Recurse -Force
    }
}

Write-Host "Release folder contents:" -ForegroundColor Cyan
Get-ChildItem -Path $releaseDir -Recurse | Sort-Object FullName | Format-Table Name, Length, FullName -AutoSize

if ($Pack) {
    $zipName = "InkCanvasForClass.singleexe.zip"
    if (Test-Path $zipName) { Remove-Item $zipName -Force }
    Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zipName -Force
    Write-Host "Packed release into $zipName" -ForegroundColor Green
}

Write-Host "Done." -ForegroundColor Cyan
