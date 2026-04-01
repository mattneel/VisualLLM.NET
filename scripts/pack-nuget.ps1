param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\VisualLLM.Inference\VisualLLM.Inference.vbproj"
$outputRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $repoRoot "artifacts\nuget" } else { $OutputDirectory }

$null = New-Item -ItemType Directory -Path $outputRoot -Force

$arguments = @(
    "pack",
    $projectPath,
    "-c", $Configuration,
    "-o", $outputRoot
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $arguments += "-p:PackageVersion=$Version"
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed for '$projectPath'."
}

Write-Host "NuGet package created under '$outputRoot'."
Write-Output $outputRoot
