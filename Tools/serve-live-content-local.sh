#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd "${script_dir}/.." && pwd)"
server_data="${repo_dir}/Splice/ServerData"

if [[ ! -f "${server_data}/live-content-manifest.json" ]]; then
  echo "No local content build found. Run Splice/Live Content/3 or /4 in Unity first." >&2
  exit 1
fi

echo "Serving Splice live content at http://127.0.0.1:8081"
python3 -m http.server 8081 --bind 127.0.0.1 --directory "${server_data}"
