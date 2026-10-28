# Export the Assets-embedded plugin as a UPM package.
# Usage: pwsh -File Tools/export-upm.ps1 [-Destination dist/com.inspiaaa.unityhfsm]
param(
    [string]$Destination = "dist/com.inspiaaa.unityhfsm"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "Assets/UnityHFSM"

if (-not (Test-Path $source)) {
    throw "Package source not found: $source"
}

$dest = if ([System.IO.Path]::IsPathRooted($Destination)) {
    $Destination
}
else {
    Join-Path $root $Destination
}

if (Test-Path $dest) {
    [System.IO.Directory]::Delete($dest, $true)
}
New-Item -ItemType Directory -Force $dest | Out-Null

Copy-Item (Join-Path $source "*") $dest -Recurse -Force

# UPM hides sample folders with the ~ suffix and imports them on demand.
$samples = Join-Path $dest "Samples"
if (-not (Test-Path $samples)) {
    throw "Samples folder not found in package source."
}
Rename-Item $samples "Samples~"

# Unity ignores ~ folders, so the exported package should not carry a root meta
# for the former visible Samples folder.
$samplesMeta = Join-Path $dest "Samples.meta"
if (Test-Path $samplesMeta) {
    [System.IO.File]::Delete($samplesMeta)
}

Copy-Item (Join-Path $root "README.md") $dest
Copy-Item (Join-Path $root "CHANGELOG.md") $dest
Copy-Item (Join-Path $root "LICENSE.md") $dest

# Validate that package.json points at folders that exist in the exported package.
$manifestPath = Join-Path $dest "package.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
foreach ($sample in $manifest.samples) {
    $samplePath = Join-Path $dest $sample.path
    if (-not (Test-Path $samplePath)) {
        throw "Sample path from package.json does not exist: $($sample.path)"
    }
}

Write-Host "Exported UPM package to: $dest"
