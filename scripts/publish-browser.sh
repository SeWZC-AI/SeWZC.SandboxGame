#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
worldbox_dotnet="${WORLDBOX_DOTNET:-dotnet}"
worldbox_publish="$PWD/artifacts/browser"
worldbox_site="$PWD/artifacts/site"

# Clear only this script's output folders so stale assets cannot conceal a bad publish.
rm -rf "$worldbox_publish" "$worldbox_site"
"$worldbox_dotnet" publish src/SeWZC.WorldBox.Browser/SeWZC.WorldBox.Browser.csproj \
  -c Release -o "$worldbox_publish" \
  -p:WasmEnableThreads=false -p:RunAOTCompilation=false

# Microsoft.NET.Sdk.WebAssembly emits the deployable static tree here.
if [[ ! -f "$worldbox_publish/wwwroot/index.html" ]]; then
  echo "Expected publish/wwwroot/index.html was not generated; inspect the SDK publish output." >&2
  exit 1
fi

mkdir -p "$worldbox_site"
cp -a "$worldbox_publish/wwwroot/." "$worldbox_site/"
mkdir -p "$worldbox_site/licenses"
cp src/SeWZC.WorldBox.UI/Assets/Fonts/LICENSE.txt "$worldbox_site/licenses/NotoSansSC.txt"
touch "$worldbox_site/.nojekyll"
python3 scripts/check-static-site.py "$worldbox_site"
echo "Static site is ready: $worldbox_site"
