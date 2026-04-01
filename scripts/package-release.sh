#!/usr/bin/env bash
set -euo pipefail

runtime_identifier="${1:-}"
version="${VERSION:-0.1.1}"
configuration="${CONFIGURATION:-Release}"
framework="${FRAMEWORK:-net10.0}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

current_rid() {
    local os_part
    local arch_part

    case "$(uname -s)" in
        Darwin) os_part="osx" ;;
        Linux) os_part="linux" ;;
        *)
            echo "Unsupported operating system for release packaging." >&2
            exit 1
            ;;
    esac

    case "$(uname -m)" in
        x86_64|amd64) arch_part="x64" ;;
        arm64|aarch64) arch_part="arm64" ;;
        *)
            echo "Unsupported architecture '$(uname -m)'." >&2
            exit 1
            ;;
    esac

    printf '%s-%s\n' "${os_part}" "${arch_part}"
}

rid="${runtime_identifier:-$(current_rid)}"
runtime_root="${repo_root}/runtimes/${rid}/native"
publish_root="${repo_root}/artifacts/publish/${rid}/VisualLLM.NET"
release_root="${repo_root}/artifacts/release"

if [[ ! -d "${runtime_root}" ]]; then
    echo "No staged native runtime was found at '${runtime_root}'." >&2
    exit 1
fi

rm -rf "${publish_root}"
mkdir -p "${publish_root}" "${release_root}"

dotnet publish "${repo_root}/src/VisualLLM.Server/VisualLLM.Server.vbproj" \
    -c "${configuration}" \
    -f "${framework}" \
    -r "${rid}" \
    --self-contained false \
    -p:UseAppHost=true \
    -o "${publish_root}"

find "${publish_root}" -type f \( -name '*.pdb' -o -name 'web.config' \) -delete

mkdir -p "${publish_root}/runtimes/${rid}/native"
cp -Rf "${runtime_root}/." "${publish_root}/runtimes/${rid}/native/"
cp -f "${repo_root}/LICENSE" "${publish_root}/LICENSE"
cp -f "${repo_root}/CONTRIBUTING.md" "${publish_root}/CONTRIBUTING.md"

archive_path="${release_root}/VisualLLM.NET-v${version}-${rid}.tar.gz"
rm -f "${archive_path}"
tar -czf "${archive_path}" -C "${publish_root}" .
checksum_path="${archive_path}.sha256"
if command -v sha256sum >/dev/null 2>&1; then
    checksum_value="$(sha256sum "${archive_path}" | awk '{ print $1 }')"
else
    checksum_value="$(shasum -a 256 "${archive_path}" | awk '{ print $1 }')"
fi
printf '%s *%s\n' "${checksum_value}" "$(basename "${archive_path}")" > "${checksum_path}"

echo "Release artifact created at '${archive_path}'."
printf '%s\n' "${archive_path}"
