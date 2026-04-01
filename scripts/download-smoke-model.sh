#!/usr/bin/env bash
set -euo pipefail

destination_path="${1:-}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
model_path="${destination_path:-${repo_root}/artifacts/models/smoke/tinyllamas-stories15M-q4_0.gguf}"
expected_sha256="66967fbece6dbe97886593fdbb73589584927e29119ec31f08090732d1861739"
download_url="https://huggingface.co/ggml-org/models/resolve/main/tinyllamas/stories15M-q4_0.gguf?download=true"

mkdir -p "$(dirname "${model_path}")"

sha256_file() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{ print $1 }'
    else
        shasum -a 256 "$1" | awk '{ print $1 }'
    fi
}

if [[ -f "${model_path}" ]]; then
    if [[ "$(sha256_file "${model_path}")" == "${expected_sha256}" ]]; then
        printf '%s\n' "${model_path}"
        exit 0
    fi

    rm -f "${model_path}"
fi

temporary_path="${model_path}.download"
curl --fail --location --silent --show-error "${download_url}" --output "${temporary_path}"

if [[ "$(sha256_file "${temporary_path}")" != "${expected_sha256}" ]]; then
    rm -f "${temporary_path}"
    echo "The downloaded smoke-test model hash did not match the expected SHA256." >&2
    exit 1
fi

mv -f "${temporary_path}" "${model_path}"
printf '%s\n' "${model_path}"
