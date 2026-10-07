#!/usr/bin/env python3
"""检查已发布的 WebAssembly 站点能否在子路径下解析资源。"""

import json
import re
import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


class AssetParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets = []
        self.import_maps = []
        self.in_import_map = False
        self.base = None

    def handle_starttag(self, tag, attributes):
        attributes = dict(attributes)
        if tag == "base":
            self.base = attributes.get("href", "")
        if tag in {"script", "img", "source"} and attributes.get("src"):
            self.assets.append(attributes["src"])
        if tag == "link" and attributes.get("href"):
            self.assets.append(attributes["href"])
        if tag == "script" and attributes.get("type") == "importmap":
            self.in_import_map = True

    def handle_endtag(self, tag):
        if tag == "script":
            self.in_import_map = False

    def handle_data(self, data):
        if self.in_import_map and data.strip():
            self.import_maps.append(json.loads(data))


def check_site(site):
    site = site.resolve()
    failures = []
    index = site / "index.html"
    if not index.is_file():
        return ["Missing index.html"]
    parser = AssetParser()
    html = index.read_text(encoding="utf-8")
    parser.feed(html)
    if "#[.{fingerprint}]" in html:
        failures.append("Unresolved .NET static asset fingerprint in index.html")
    if parser.base not in {None, "", "./"}:
        failures.append("Use a relative base href so project Pages paths work")

    def check_reference(reference, directory):
        parsed = urlsplit(reference)
        if parsed.scheme or parsed.netloc or reference.startswith("#"):
            return
        if reference.startswith("/"):
            failures.append(f"Root-relative asset breaks project Pages: {reference}")
            return
        path = (directory / unquote(parsed.path)).resolve()
        if not path.is_relative_to(site):
            failures.append(f"Asset escapes site directory: {reference}")
        elif not path.is_file():
            failures.append(f"Missing local asset: {reference}")

    for reference in parser.assets:
        check_reference(reference, site)
    mapped_imports = {}
    for import_map in parser.import_maps:
        mapped_imports.update(import_map.get("imports", {}))
        for reference in import_map.get("imports", {}).values():
            check_reference(reference, site)
        for imports in import_map.get("scopes", {}).values():
            for reference in imports.values():
                check_reference(reference, site)

    # 自有模块按模块 URL 解析相对路径；运行时模块由 SDK 发布流程检查。
    imports = re.compile(r"(?:from\s*|import\s*\(\s*|import\s*|import\.meta\.resolve\s*\(\s*)['\"]([^'\"]+)['\"]")
    for module in site.glob("*.js"):
        for reference in imports.findall(module.read_text(encoding="utf-8")):
            if reference.startswith((".", "/")):
                check_reference(mapped_imports.get(reference, reference), module.parent)

    framework = site / "_framework"
    if not framework.is_dir() or not any(framework.glob("*.wasm")):
        failures.append("Missing _framework WebAssembly payload")
    if not framework.is_dir() or not any(framework.glob("dotnet*.js")):
        failures.append("Missing .NET JavaScript runtime loader")
    if not (site / ".nojekyll").is_file():
        failures.append("Missing .nojekyll")
    return failures


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python3 scripts/check-static-site.py <published-site>")
    errors = check_site(Path(sys.argv[1]))
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        raise SystemExit(1)
    print("Static asset checks passed (relative URLs, resolved assets, .NET WebAssembly payload).")
