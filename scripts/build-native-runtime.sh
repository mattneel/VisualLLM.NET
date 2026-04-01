#!/usr/bin/env bash
set -euo pipefail

runtime_identifier="${1:-}"
configuration="${CONFIGURATION:-Release}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

current_rid() {
    local os_part
    local arch_part

    case "$(uname -s)" in
        Darwin) os_part="osx" ;;
        Linux) os_part="linux" ;;
        *)
            echo "Unsupported operating system for native runtime build." >&2
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
source_directory="${repo_root}/third_party/llama.cpp"
build_root="${BUILD_DIRECTORY:-${repo_root}/build/llama.cpp/${rid}}"
output_root="${OUTPUT_DIRECTORY:-${repo_root}/runtimes/${rid}/native}"

if [[ ! -d "${source_directory}" ]]; then
    echo "The llama.cpp submodule was not found at '${source_directory}'." >&2
    exit 1
fi

mkdir -p "${build_root}" "${output_root}"

cmake_args=(
    -S "${source_directory}"
    -B "${build_root}"
    -DBUILD_SHARED_LIBS=ON
    -DLLAMA_BUILD_COMMON=ON
    -DLLAMA_BUILD_SERVER=ON
    -DLLAMA_BUILD_TOOLS=ON
    -DLLAMA_BUILD_WEBUI=OFF
    -DLLAMA_BUILD_TESTS=OFF
    -DLLAMA_BUILD_EXAMPLES=OFF
    -DLLAMA_OPENSSL=OFF
    -DGGML_BACKEND_DL=OFF
    -DGGML_NATIVE=OFF
    "-DCMAKE_BUILD_TYPE=${configuration}"
)

if command -v ninja >/dev/null 2>&1; then
    cmake_args+=(-G Ninja)
fi

cmake "${cmake_args[@]}"
cmake --build "${build_root}" --config "${configuration}" --parallel --target llama llama-server

if [[ -d "${build_root}/bin/${configuration}" ]]; then
    build_bin_directory="${build_root}/bin/${configuration}"
elif [[ -d "${build_root}/bin" ]]; then
    build_bin_directory="${build_root}/bin"
else
    echo "Unable to locate the llama.cpp binary output under '${build_root}'." >&2
    exit 1
fi

case "$(uname -s)" in
    Darwin)
        patterns=("libllama*.dylib" "libggml*.dylib" "libmtmd*.dylib" "llama-server")
        ;;
    Linux)
        patterns=("libllama*.so*" "libggml*.so*" "libmtmd*.so*" "llama-server")
        ;;
esac

shopt -s nullglob
copied_count=0
for pattern in "${patterns[@]}"; do
    for file in "${build_bin_directory}"/${pattern}; do
        cp -f "${file}" "${output_root}/"
        copied_count=$((copied_count + 1))
    done
done
shopt -u nullglob

if [[ "${copied_count}" -eq 0 ]]; then
    echo "No native runtime files were copied from '${build_bin_directory}'." >&2
    exit 1
fi

echo "Native runtime staged for ${rid} at '${output_root}'."
printf '%s\n' "${output_root}"
