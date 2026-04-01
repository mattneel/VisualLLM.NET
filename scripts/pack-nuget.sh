#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
configuration="${CONFIGURATION:-Release}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
project_path="${repo_root}/src/VisualLLM.Inference/VisualLLM.Inference.vbproj"
output_root="${OUTPUT_DIRECTORY:-${repo_root}/artifacts/nuget}"

mkdir -p "${output_root}"

dotnet_args=(
    pack "${project_path}"
    -c "${configuration}"
    -o "${output_root}"
)

if [[ -n "${version}" ]]; then
    dotnet_args+=("-p:PackageVersion=${version}")
fi

dotnet "${dotnet_args[@]}"

echo "NuGet package created under '${output_root}'."
printf '%s\n' "${output_root}"
