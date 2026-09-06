import argparse
import datetime
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("suites", nargs="*", default=["Unit", "Components", "Integration"])
parser.add_argument("--label", default="owning")
args = parser.parse_args()
bundle = Path(__file__).resolve().parents[1]
repo = bundle.parents[2]
output = repo / ".mcp-state" / "p02f"
output.mkdir(parents=True, exist_ok=True)
plan = json.loads((bundle / "plan/owning.json").read_text(encoding="utf-8"))
for suite in args.suites:
    selected = plan[suite]
    base = ["dotnet", "test", selected["project"], "--configuration", "Release", "--no-build", "--no-restore", "--filter", selected["filter"], "/m:1"]
    receipt = {"suite": suite, "expectedDiscovery": selected["expectedDiscovery"], "commands": []}
    def run(label, command):
        path = output / f"{args.label}-{label}-{suite}.txt"
        start = datetime.datetime.now(datetime.timezone.utc).isoformat()
        with path.open("wb") as stream:
            completed = subprocess.run(command, cwd=repo, stdout=stream, stderr=subprocess.STDOUT)
        receipt["commands"].append({"argv": command, "cwd": ".", "startUtc": start, "endUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "exitCode": completed.returncode, "artifact": path.name})
        return completed.returncode, path.read_text(encoding="utf-8", errors="replace")
    code, listing = run("list", base + ["--list-tests"])
    actual = sum(line.strip().startswith("CanDoItAll.Tests.") for line in listing.splitlines())
    receipt["actualDiscovery"] = actual
    if code or actual != selected["expectedDiscovery"]:
        (output / f"{args.label}-{suite}-receipt.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
        raise SystemExit(f"Invalid {suite} discovery: expected {selected['expectedDiscovery']}, actual {actual}, exit {code}")
    print(f"{suite}: discovered {actual}", flush=True)
    code, text = run("test", base + ["--logger", f"trx;LogFileName={args.label}-{suite}.trx", "--results-directory", str(output)])
    (output / f"{args.label}-{suite}-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"{suite}: exit {code}; " + "\n".join(text.splitlines()[-3:]), flush=True)
    if code:
        raise SystemExit(code)
