#!/usr/bin/env python3
"""生成通过付费研究发展的世界，供浏览器回归检查使用。"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

root = Path(__file__).resolve().parent.parent
output = Path(sys.argv[1] if len(sys.argv) > 1 else root / 'artifacts/save-fixture/empires').resolve()
output.mkdir(parents=True, exist_ok=True)
binary = root / 'tests/SeWZC.WorldBox.Core.Tests/bin/Release/net10.0/SeWZC.WorldBox.Core.Tests.dll'
runs = []
for route, focus in (('technology', '--technology'), ('magic', '--arcane-industry')):
    folder = output / route
    subprocess.run([os.environ.get('WORLDBOX_DOTNET', 'dotnet'), str(binary), '--simulate-development', str(folder),
                    '42', '128', '24000', '--peaceful', focus, '--require-empire', '--until-empire', '--complete-agenda'],
                   cwd=root, check=True)
    report = json.loads((folder / 'report.json').read_text())
    filename = f'{route}-empire.worldbox.json'
    shutil.copyfile(folder / 'final.worldbox.json', output / filename)
    raw = (output / filename).read_bytes()
    complete = [t['Id'] for t in report['samples'][-1]['towns'] if t['civilization']['Achieved']]
    if not complete:
        raise RuntimeError(f'{route} did not complete its research route')
    runs.append({'route': route, 'completeTowns': complete, 'saveSha256': hashlib.sha256(raw).hexdigest()})
(output / 'simulation-results.json').write_text(json.dumps({'runs': runs}, indent=2) + '\n')
