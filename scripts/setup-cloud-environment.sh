#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
worldbox_root="$PWD"
worldbox_sdk_root="$worldbox_root/.dotnet"
worldbox_cloud="$worldbox_root/artifacts/cloud"

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "Cloud setup requires Linux; use the README for other development platforms." >&2
  exit 1
fi

for worldbox_command in curl python3 tar sha512sum getconf node npm; do
  if ! command -v "$worldbox_command" >/dev/null 2>&1; then
    echo "Missing prerequisite: $worldbox_command. Configure it in the cloud runtime." >&2
    exit 1
  fi
done
if ! getconf GNU_LIBC_VERSION >/dev/null 2>&1; then
  echo "Cloud setup requires a glibc-based Linux runtime." >&2
  exit 1
fi
python3 -c 'import sys; sys.exit(0 if sys.version_info >= (3, 10) else "Python 3.10 or newer is required.")'
node -e 'if (Number(process.versions.node.split(".")[0]) < 22) { console.error("Node.js 22 or newer is required."); process.exit(1); }'

worldbox_sdk_version="$(python3 -c 'import json; print(json.load(open("global.json", encoding="utf-8"))["sdk"]["version"])')"
worldbox_sdk_channel="${worldbox_sdk_version%.*}"
case "$(uname -m)" in
  x86_64) worldbox_sdk_rid=linux-x64 ;;
  aarch64|arm64) worldbox_sdk_rid=linux-arm64 ;;
  *) echo "Unsupported cloud CPU architecture: $(uname -m)" >&2; exit 1 ;;
esac

mkdir -p "$worldbox_sdk_root" "$worldbox_cloud"

if ! [[ -x "$worldbox_sdk_root/dotnet" ]] ||
   ! "$worldbox_sdk_root/dotnet" --list-sdks | awk -v version="$worldbox_sdk_version" '$1 == version { found = 1 } END { exit !found }'; then
  worldbox_download="$(mktemp -d "$worldbox_cloud/sdk-download.XXXXXX")"
  trap 'rm -rf "$worldbox_download"' EXIT
  curl --fail --silent --show-error --location --retry 3 \
    "https://builds.dotnet.microsoft.com/dotnet/release-metadata/$worldbox_sdk_channel/releases.json" \
    --output "$worldbox_download/releases.json"
  python3 - "$worldbox_download/releases.json" "$worldbox_sdk_version" "$worldbox_sdk_rid" > "$worldbox_download/download.txt" <<'PY'
import json
import re
import sys
from urllib.parse import urlparse

manifest_path, version, rid = sys.argv[1:]
with open(manifest_path, encoding="utf-8") as manifest:
    releases = json.load(manifest)["releases"]
for release in releases:
    candidates = release.get("sdks", []) + [release.get("sdk", {})]
    for sdk in candidates:
        if sdk.get("version") != version:
            continue
        for item in sdk.get("files", []):
            if item.get("rid") != rid or not item.get("name", "").endswith(".tar.gz"):
                continue
            url, checksum = item["url"], item["hash"]
            parsed = urlparse(url)
            if parsed.scheme != "https" or parsed.hostname != "builds.dotnet.microsoft.com":
                raise SystemExit("SDK download host changed; review the network configuration.")
            if not re.fullmatch(r"[0-9a-fA-F]{128}", checksum):
                raise SystemExit("Invalid SDK SHA-512 in the release manifest.")
            print(url)
            print(checksum.lower())
            sys.exit(0)
raise SystemExit(f"SDK {version} for {rid} was not found in the release manifest.")
PY
  mapfile -t worldbox_download_info < "$worldbox_download/download.txt"
  curl --fail --silent --show-error --location --retry 3 "${worldbox_download_info[0]}" \
    --output "$worldbox_download/sdk.tar.gz"
  printf '%s  %s\n' "${worldbox_download_info[1]}" "$worldbox_download/sdk.tar.gz" | sha512sum --check --status
  tar -xzf "$worldbox_download/sdk.tar.gz" -C "$worldbox_sdk_root"
fi

export DOTNET_ROOT="$worldbox_sdk_root"
export PATH="$DOTNET_ROOT:$PATH"
export WORLDBOX_DOTNET="$DOTNET_ROOT/dotnet"
export DOTNET_CLI_HOME="$worldbox_cloud/dotnet-cli"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=true
export NUGET_PACKAGES="$worldbox_cloud/nuget"
export PLAYWRIGHT_BROWSERS_PATH="$worldbox_cloud/playwright"
export npm_config_cache="$worldbox_cloud/npm-cache"

{
  for worldbox_variable in DOTNET_ROOT WORLDBOX_DOTNET DOTNET_CLI_HOME DOTNET_CLI_TELEMETRY_OPTOUT DOTNET_NOLOGO NUGET_PACKAGES PLAYWRIGHT_BROWSERS_PATH npm_config_cache; do
    printf 'export %s=%q\n' "$worldbox_variable" "${!worldbox_variable}"
  done
  printf 'export PATH=%q:"$PATH"\n' "$DOTNET_ROOT"
} > "$worldbox_cloud/environment.sh"

# GitHub Actions uses the same setup; its later steps need these non-secret paths.
if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
  printf '%s\n' "$DOTNET_ROOT" >> "$GITHUB_PATH"
  for worldbox_variable in DOTNET_ROOT WORLDBOX_DOTNET DOTNET_CLI_HOME DOTNET_CLI_TELEMETRY_OPTOUT DOTNET_NOLOGO NUGET_PACKAGES PLAYWRIGHT_BROWSERS_PATH npm_config_cache; do
    printf '%s=%s\n' "$worldbox_variable" "${!worldbox_variable}" >> "$GITHUB_ENV"
  done
fi

dotnet --version
dotnet workload install wasm-tools --skip-manifest-update
dotnet restore
npm ci --prefix tests/browser
(cd tests/browser && npx --no-install playwright install --with-deps chromium)

echo "Cloud dependencies are ready. In each new shell, run: source artifacts/cloud/environment.sh"
