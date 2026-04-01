param(
    [string]$DestinationPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$modelPath =
    if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
        Join-Path $repoRoot "artifacts\models\smoke\tinyllamas-stories15M-q4_0.gguf"
    } else {
        $DestinationPath
    }

$expectedSha256 = "66967fbece6dbe97886593fdbb73589584927e29119ec31f08090732d1861739"
$downloadUrl = "https://huggingface.co/ggml-org/models/resolve/main/tinyllamas/stories15M-q4_0.gguf?download=true"

$modelDirectory = Split-Path -Parent $modelPath
$null = New-Item -ItemType Directory -Path $modelDirectory -Force

if (Test-Path -LiteralPath $modelPath) {
    $existingHash = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($existingHash -eq $expectedSha256) {
        Write-Output $modelPath
        return
    }

    Remove-Item -LiteralPath $modelPath -Force
}

$temporaryPath = "$modelPath.download"
Invoke-WebRequest -Uri $downloadUrl -OutFile $temporaryPath

$downloadedHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($downloadedHash -ne $expectedSha256) {
    Remove-Item -LiteralPath $temporaryPath -Force
    throw "The downloaded smoke-test model hash did not match the expected SHA256."
}

Move-Item -LiteralPath $temporaryPath -Destination $modelPath -Force
Write-Output $modelPath
