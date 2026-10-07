#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."
worldbox_dotnet="${WORLDBOX_DOTNET:-dotnet}"
worldbox_publish="$PWD/artifacts/browser"
worldbox_site="$PWD/artifacts/site"

# --no-restore 复用 CI 的还原结果；本地发布默认执行还原。
worldbox_restore_args=()
if [[ "${1:-}" == "--no-restore" ]]; then
  worldbox_restore_args+=(--no-restore)
  shift
fi
if (( $# > 0 )); then
  echo "Usage: $0 [--no-restore]" >&2
  exit 2
fi

rm -rf "$worldbox_publish" "$worldbox_site"
"$worldbox_dotnet" publish src/SeWZC.WorldBox.Browser/SeWZC.WorldBox.Browser.csproj \
  -c Release -o "$worldbox_publish" \
  "${worldbox_restore_args[@]}"

# Microsoft.NET.Sdk.WebAssembly 在此目录生成静态站点。
if [[ ! -f "$worldbox_publish/wwwroot/index.html" ]]; then
  echo "Expected publish/wwwroot/index.html was not generated; inspect the SDK publish output." >&2
  exit 1
fi

mkdir -p "$worldbox_site"
cp -a "$worldbox_publish/wwwroot/." "$worldbox_site/"
mkdir -p "$worldbox_site/licenses"
cp src/SeWZC.WorldBox.UI/Assets/Fonts/LICENSE.txt "$worldbox_site/licenses/NotoSansSC.txt"
touch "$worldbox_site/.nojekyll"
# 在 HTML 中记录提交编号，供公网检查核对版本。
python3 - "$worldbox_site/index.html" "${GITHUB_SHA:-$(git rev-parse HEAD)}" <<'PY'
from pathlib import Path
import re
import sys

index = Path(sys.argv[1])
revision = sys.argv[2]
if not re.fullmatch(r"[0-9a-f]{40}", revision):
    raise SystemExit("Expected a full Git commit SHA for the published revision")
html = index.read_text(encoding="utf-8")
if "</head>" not in html:
    raise SystemExit("Missing HTML head for published revision")
index.write_text(html.replace("</head>", f'<meta name="worldbox-revision" content="{revision}">\n</head>', 1), encoding="utf-8")
PY
python3 scripts/check-static-site.py "$worldbox_site"
echo "Static site is ready: $worldbox_site"
