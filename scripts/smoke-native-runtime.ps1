param(
    [string]$RuntimeIdentifier = "",
    [string]$PublishedDirectory = "",
    [string]$ModelPath = "",
    [int]$Port = 5079
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Get-CurrentRuntimeIdentifier {
    $osPart =
        if ($IsWindows) { "win" }
        elseif ($IsMacOS) { "osx" }
        else { "linux" }

    $architecturePart = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    return "$osPart-$architecturePart"
}

function Wait-ForHealth([string]$url, [int]$timeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            return Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 5
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "Timed out waiting for '$url'."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$rid = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { Get-CurrentRuntimeIdentifier } else { $RuntimeIdentifier.Trim() }
$publishRoot =
    if ([string]::IsNullOrWhiteSpace($PublishedDirectory)) {
        Join-Path $repoRoot "artifacts\publish\$rid\VisualLLM.NET"
    } else {
        $PublishedDirectory
    }

if (-not (Test-Path -LiteralPath $publishRoot)) {
    throw "The published server directory '$publishRoot' was not found. Run the release packaging script first."
}

$resolvedModelPath =
    if ([string]::IsNullOrWhiteSpace($ModelPath)) {
        (& (Join-Path $PSScriptRoot "download-smoke-model.ps1")).Trim()
    } else {
        $ModelPath
    }

if (-not (Test-Path -LiteralPath $resolvedModelPath)) {
    throw "The smoke-test model '$resolvedModelPath' does not exist."
}

$logsRoot = Join-Path $repoRoot "artifacts\logs"
$null = New-Item -ItemType Directory -Path $logsRoot -Force
$stdoutPath = Join-Path $logsRoot "smoke-$rid.stdout.log"
$stderrPath = Join-Path $logsRoot "smoke-$rid.stderr.log"

$serverExecutable =
    if ($IsWindows) {
        Join-Path $publishRoot "VisualLLM.Server.exe"
    } else {
        Join-Path $publishRoot "VisualLLM.Server"
    }

$command = if (Test-Path -LiteralPath $serverExecutable) { $serverExecutable } else { "dotnet" }
$arguments =
    if ($command -eq "dotnet") {
        @(
            (Join-Path $publishRoot "VisualLLM.Server.dll"),
            "--model", $resolvedModelPath,
            "--listen-port", $Port,
            "--context", "256",
            "--threads", "2",
            "--gpu-layers", "0",
            "--no-banner"
        )
    } else {
        @(
            "--model", $resolvedModelPath,
            "--listen-port", $Port,
            "--context", "256",
            "--threads", "2",
            "--gpu-layers", "0",
            "--no-banner"
        )
    }

$process = Start-Process -FilePath $command -ArgumentList $arguments -WorkingDirectory $publishRoot -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru

try {
    $health = Wait-ForHealth "http://127.0.0.1:$Port/healthz" 90
    if ($health.backend -ne "llama.cpp-pinvoke") {
        throw "Expected the P/Invoke backend, but the server reported '$($health.backend)'."
    }

    $requestBody = @{
        model = "default"
        messages = @(
            @{
                role = "user"
                content = "Say hello from Visual Basic."
            }
        )
        stream = $false
        max_tokens = 16
        temperature = 0
    } | ConvertTo-Json -Depth 6

    $response = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/v1/chat/completions" -Method Post -ContentType "application/json" -Body $requestBody -TimeoutSec 60

    if ($response.object -ne "chat.completion") {
        throw "The chat completion response object was '$($response.object)', not 'chat.completion'."
    }

    $content = $response.choices[0].message.content
    if ([string]::IsNullOrWhiteSpace($content)) {
        throw "The native smoke response did not contain assistant content."
    }

    Write-Host "Native smoke test succeeded for $rid."
} finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}
