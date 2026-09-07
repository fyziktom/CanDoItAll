from pathlib import Path
import argparse
import datetime
import gzip
import hashlib
import json
import os
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--playwright-module", type=Path, required=True)
parser.add_argument("--attempt", required=True)
args = parser.parse_args()
root = next(p for p in Path(__file__).resolve().parents if (p / "CanDoItAll.slnx").exists())
output = Path(__file__).resolve().parent
source = output / "browser-web.cjs"
snapshot = output / ("browser-web-" + args.attempt + ".cjs")
snapshot.write_bytes(source.read_bytes())
environment = os.environ.copy()
environment["OV_PLAYWRIGHT"] = str(args.playwright_module.resolve())
command = ["node", str(snapshot)]
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
process = subprocess.run(command, cwd=root, env=environment, capture_output=True)
raw = process.stdout + process.stderr
(output / ("browser-command-" + args.attempt + ".txt.gz")).write_bytes(gzip.compress(raw, mtime=0))
receipt = {
    "command": command,
    "scriptSha256": hashlib.sha256(snapshot.read_bytes()).hexdigest(),
    "playwrightModule": str(args.playwright_module),
    "startedUtc": started,
    "finishedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "exit": process.returncode
}
(output / ("browser-command-" + args.attempt + ".json")).write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
print(json.dumps(receipt), flush=True)
print(json.dumps(raw.decode("utf-8", "replace")[-6000:]), flush=True)
raise SystemExit(process.returncode)
