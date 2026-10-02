#!/usr/bin/env python3
"""Run built unit + headless UI checks and report their combined wall time."""
import os
from pathlib import Path
import subprocess
import sys
import time


def main():
    root = Path(__file__).resolve().parent.parent
    dotnet = os.environ.get("WORLDBOX_DOTNET", "dotnet")
    commands = []
    for project, args in (
        ("SeWZC.WorldBox.Core.Tests", ["--suite", "unit"]),
        ("SeWZC.WorldBox.UI.Tests", []),
    ):
        binary = root / "tests" / project / "bin/Release/net10.0" / f"{project}.dll"
        if not binary.is_file():
            print(f"Missing {binary}; build Release first.", file=sys.stderr)
            return 2
        commands.append([dotnet, str(binary), *args])

    started = time.monotonic()
    try:
        for command in commands:
            subprocess.run(command, cwd=root, check=True)
    except (subprocess.CalledProcessError, OSError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    elapsed = time.monotonic() - started
    print(f"Fast checks: {elapsed:.2f} s (includes both process startups)", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
