param(
    [string]$RuntimeIdentifier = "",
    [string]$Configuration = "Release",
    [string]$BuildDirectory = "",
    [string]$OutputDirectory = "",
    [switch]$Clean
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

function Get-VisualStudioArchitecture([string]$rid) {
    if ($rid.EndsWith("arm64", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "ARM64"
    }

    return "x64"
}

function Resolve-BuildBinDirectory([string]$buildRoot, [string]$configuration) {
    $candidates = @(
        (Join-Path $buildRoot "bin\$configuration"),
        (Join-Path $buildRoot "bin")
    )

    foreach ($candidate In $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Unable to locate the llama.cpp binary output under '$buildRoot'."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$rid = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { Get-CurrentRuntimeIdentifier } else { $RuntimeIdentifier.Trim() }
$sourceDirectory = Join-Path $repoRoot "third_party/llama.cpp"
$buildRoot = if ([string]::IsNullOrWhiteSpace($BuildDirectory)) { Join-Path $repoRoot "build\llama.cpp\$rid" } else { $BuildDirectory }
$outputRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $repoRoot "runtimes\$rid\native" } else { $OutputDirectory }

if (-not (Test-Path -LiteralPath $sourceDirectory)) {
    throw "The llama.cpp submodule was not found at '$sourceDirectory'. Run 'git submodule update --init --recursive'."
}

if ($Clean -and (Test-Path -LiteralPath $buildRoot)) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}

$null = New-Item -ItemType Directory -Path $buildRoot -Force
$null = New-Item -ItemType Directory -Path $outputRoot -Force

$configureArguments = @(
    "-S", $sourceDirectory,
    "-B", $buildRoot,
    "-DBUILD_SHARED_LIBS=ON",
    "-DLLAMA_BUILD_COMMON=ON",
    "-DLLAMA_BUILD_SERVER=ON",
    "-DLLAMA_BUILD_TOOLS=ON",
    "-DLLAMA_BUILD_WEBUI=OFF",
    "-DLLAMA_BUILD_TESTS=OFF",
    "-DLLAMA_BUILD_EXAMPLES=OFF",
    "-DLLAMA_OPENSSL=OFF",
    "-DGGML_BACKEND_DL=OFF",
    "-DGGML_NATIVE=OFF"
)

if ($IsWindows) {
    $configureArguments += @("-G", "Visual Studio 17 2022", "-A", (Get-VisualStudioArchitecture $rid))
} else {
    $ninja = Get-Command ninja -ErrorAction SilentlyContinue
    if ($null -ne $ninja) {
        $configureArguments += @("-G", "Ninja")
    }

    $configureArguments += "-DCMAKE_BUILD_TYPE=$Configuration"
}

& cmake @configureArguments
if ($LASTEXITCODE -ne 0) {
    throw "CMake configure failed for runtime '$rid'."
}

& cmake --build $buildRoot --config $Configuration --parallel --target llama llama-server
if ($LASTEXITCODE -ne 0) {
    throw "CMake build failed for runtime '$rid'."
}

$buildBinDirectory = Resolve-BuildBinDirectory $buildRoot $Configuration
$patterns =
    if ($IsWindows) {
        @("llama.dll", "ggml*.dll", "mtmd.dll", "llama-server.exe")
    } elseif ($IsMacOS) {
        @("libllama*.dylib", "libggml*.dylib", "libmtmd*.dylib", "llama-server")
    } else {
        @("libllama*.so*", "libggml*.so*", "libmtmd*.so*", "llama-server")
    }

$copiedFiles = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($pattern In $patterns) {
    foreach ($file In Get-ChildItem -LiteralPath $buildBinDirectory -File -Filter $pattern -ErrorAction SilentlyContinue) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $outputRoot $file.Name) -Force
        $null = $copiedFiles.Add($file.Name)
    }
}

if ($copiedFiles.Count -eq 0) {
    throw "No native runtime files were copied from '$buildBinDirectory'."
}

Write-Host "Native runtime staged for $rid at '$outputRoot'."
Write-Output $outputRoot
