param(
    [string]$RuntimeIdentifier = "",
    [string]$Version = "0.1.1",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [string]$PublishDirectory = "",
    [string]$ArchiveDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-CurrentRuntimeIdentifier {
    $osPart =
        if ($IsWindows) { "win" }
        elseif ($IsMacOS) { "osx" }
        else { "linux" }

    $architecturePart = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    return "$osPart-$architecturePart"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$rid = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { Get-CurrentRuntimeIdentifier } else { $RuntimeIdentifier.Trim() }
$runtimeRoot = Join-Path $repoRoot "runtimes\$rid\native"
$publishRoot = if ([string]::IsNullOrWhiteSpace($PublishDirectory)) { Join-Path $repoRoot "artifacts\publish\$rid\VisualLLM.NET" } else { $PublishDirectory }
$releaseRoot = if ([string]::IsNullOrWhiteSpace($ArchiveDirectory)) { Join-Path $repoRoot "artifacts\release" } else { $ArchiveDirectory }

if (-not (Test-Path -LiteralPath $runtimeRoot)) {
    throw "No staged native runtime was found at '$runtimeRoot'. Run scripts/build-native-runtime.ps1 first."
}

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

$null = New-Item -ItemType Directory -Path $publishRoot -Force
$null = New-Item -ItemType Directory -Path $releaseRoot -Force

& dotnet publish (Join-Path $repoRoot "src\VisualLLM.Server\VisualLLM.Server.vbproj") `
    -c $Configuration `
    -f $Framework `
    -r $rid `
    --self-contained false `
    -p:UseAppHost=true `
    -o $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for runtime '$rid'."
}

Get-ChildItem -Path $publishRoot -Recurse -File -Include *.pdb, web.config | Remove-Item -Force

$publishNativeRoot = Join-Path $publishRoot "runtimes\$rid\native"
$null = New-Item -ItemType Directory -Path $publishNativeRoot -Force
Copy-Item -Path (Join-Path $runtimeRoot "*") -Destination $publishNativeRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $publishRoot "LICENSE") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "CONTRIBUTING.md") -Destination (Join-Path $publishRoot "CONTRIBUTING.md") -Force

$archivePath = Join-Path $releaseRoot "VisualLLM.NET-v$Version-$rid.zip"
if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $archivePath -Force
$checksumPath = "$archivePath.sha256"
$checksum = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $checksumPath -Value "$checksum *$(Split-Path -Leaf $archivePath)" -NoNewline
Write-Host "Release artifact created at '$archivePath'."
Write-Output $archivePath
