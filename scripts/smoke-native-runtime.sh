#!/usr/bin/env bash
set -euo pipefail

runtime_identifier="${1:-}"
port="${PORT:-5079}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

current_rid() {
    local os_part
    local arch_part

    case "$(uname -s)" in
        Darwin) os_part="osx" ;;
        Linux) os_part="linux" ;;
        *)
            echo "Unsupported operating system for native smoke testing." >&2
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
publish_root="${PUBLISHED_DIRECTORY:-${repo_root}/artifacts/publish/${rid}/VisualLLM.NET}"
model_path="${MODEL_PATH:-$(bash "${script_dir}/download-smoke-model.sh")}"
logs_root="${repo_root}/artifacts/logs"
mkdir -p "${logs_root}"
stdout_path="${logs_root}/smoke-${rid}.stdout.log"
stderr_path="${logs_root}/smoke-${rid}.stderr.log"

if [[ ! -d "${publish_root}" ]]; then
    echo "The published server directory '${publish_root}' was not found." >&2
    exit 1
fi

if [[ -x "${publish_root}/VisualLLM.Server" ]]; then
    server_command=("${publish_root}/VisualLLM.Server")
else
    server_command=(dotnet "${publish_root}/VisualLLM.Server.dll")
fi

cleanup() {
    if [[ -n "${server_pid:-}" ]] && kill -0 "${server_pid}" >/dev/null 2>&1; then
        kill "${server_pid}" >/dev/null 2>&1 || true
        wait "${server_pid}" >/dev/null 2>&1 || true
    fi
}

trap cleanup EXIT

"${server_command[@]}" \
    --model "${model_path}" \
    --listen-port "${port}" \
    --context 256 \
    --threads 2 \
    --gpu-layers 0 \
    --no-banner \
    >"${stdout_path}" 2>"${stderr_path}" &
server_pid=$!

health_url="http://127.0.0.1:${port}/healthz"
deadline=$((SECONDS + 90))
health_payload=""
while (( SECONDS < deadline )); do
    if health_payload="$(curl --fail --silent "${health_url}")"; then
        break
    fi
    sleep 1
done

if [[ -z "${health_payload}" ]]; then
    echo "Timed out waiting for '${health_url}'." >&2
    exit 1
fi

if ! grep -q '"backend":"llama.cpp-pinvoke"' <<<"${health_payload}"; then
    echo "The server did not report the P/Invoke backend." >&2
    echo "${health_payload}" >&2
    exit 1
fi

request_body='{"model":"default","messages":[{"role":"user","content":"Say hello from Visual Basic."}],"stream":false,"max_tokens":16,"temperature":0}'
response_payload="$(curl --fail --silent --show-error "http://127.0.0.1:${port}/v1/chat/completions" -H "Content-Type: application/json" -d "${request_body}")"

if ! grep -q '"object":"chat.completion"' <<<"${response_payload}"; then
    echo "The chat completion response did not have the expected object type." >&2
    echo "${response_payload}" >&2
    exit 1
fi

if ! grep -Eq '"content":"[^"]+' <<<"${response_payload}"; then
    echo "The native smoke response did not contain assistant content." >&2
    echo "${response_payload}" >&2
    exit 1
fi

echo "Native smoke test succeeded for ${rid}."
