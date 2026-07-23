#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: bash Backend/database/scripts/test-c4c2e-process.sh <Unity worker executable>" >&2
    exit 2
fi

worker_executable="$1"
if [[ ! -f "${worker_executable}" || ! -x "${worker_executable}" ]]; then
    echo "Unity worker executable is missing or not executable: ${worker_executable}" >&2
    exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SPLICE_UNITY_WORKER_EXECUTABLE="${worker_executable}" \
    bash "${script_dir}/test-c2.sh"
