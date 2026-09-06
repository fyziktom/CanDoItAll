import argparse
import collections
import csv
import gzip
import json
import statistics
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--ledger", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
bundle = Path(__file__).resolve().parents[1]
plan = json.loads((bundle / "plan/frozen-edits.json").read_text(encoding="utf-8"))
ledger_bytes = gzip.decompress(args.ledger.read_bytes()) if args.ledger.suffix == ".gz" else args.ledger.read_bytes()
rows = [json.loads(line) for line in ledger_bytes.decode("utf-8").splitlines() if line.strip()]
hosts = ("pre", "post", "sandbox")
edits = {edit["id"]: edit for edit in plan["edits"]}
problems = []
excluded = []
warm = []
cold = []
ids = collections.Counter(row["trialId"] for row in rows)
problems.extend("Duplicate trial identity: " + key for key, count in ids.items() if count != 1)

for row in rows:
    if row["host"] not in hosts:
        excluded.append({"trialId": row["trialId"], "reason": "Calibration, outside primary hosts"})
    elif row["kind"] == "warm" and row["category"] == "CSS" and row.get("cssProtocol") != "v2-static-completion":
        excluded.append({"trialId": row["trialId"], "reason": "Retained incompatible pre-CSS manager-parser protocol"})
    elif row["kind"] == "warm":
        warm.append(row)
    elif row["kind"] == "cold":
        cold.append(row)
    else:
        problems.append("Unknown sample kind: " + row["trialId"])

for host in hosts:
    for edit in edits:
        selected = [row for row in warm if row["host"] == host and row.get("editId") == edit]
        if sorted(row.get("repeat") for row in selected) != [1, 2, 3]:
            problems.append(f"{host}/{edit}: expected frozen repeats 1, 2, 3; found {len(selected)}")
    selected = [row for row in cold if row["host"] == host]
    if len(selected) != 3:
        problems.append(f"{host}: expected three process-cold samples; found {len(selected)}")

for row in warm:
    if not row.get("undoConfirmed"):
        problems.append("Visible undo was not confirmed: " + row["trialId"])
    if row.get("mechanism") not in ("hot reload", "browser reload", "process restart", "failure"):
        problems.append("Unknown mechanism: " + row["trialId"])
    if row.get("editId") not in edits:
        problems.append("Unfrozen edit: " + row["trialId"])
    if row.get("elapsedMs", -1) < 0:
        problems.append("Invalid elapsed time: " + row["trialId"])

for edit in edits:
    samples = [row for row in warm if row.get("editId") == edit]
    if len({row["patchSha256"] for row in samples}) > 1:
        problems.append("Patch differs across hosts: " + edit)
    after = [row for row in samples if row["host"] in ("post", "sandbox")]
    if len({row["sourceSha256"] for row in after}) > 1:
        problems.append("Post-extraction source differs across hosts: " + edit)

def metrics(selected):
    success = [row["elapsedMs"] for row in selected if row["outcome"] == "success"]
    return {
        "samples": len(selected),
        "success": len(success),
        "failures": len(selected) - len(success),
        "minimumMs": min(success) if success else None,
        "maximumMs": max(success) if success else None,
        "rangeMs": max(success) - min(success) if success else None,
        "medianMs": statistics.median(success) if success else None,
        "mechanisms": dict(collections.Counter(row["mechanism"] for row in selected)),
        "failedTrials": [row["trialId"] for row in selected if row["outcome"] != "success"],
    }

report = {
    "scope": "Observed edit-to-visible latency includes managed-tool and browser orchestration.",
    "coldDefinition": "Process-cold with restored, previously compiled caches; not clean build.",
    "completeMatrix": not problems,
    "validationProblems": problems,
    "excludedButRetained": excluded,
    "primaryWarm": len(warm),
    "primaryCold": len(cold),
    "byEdit": {
        edit: {host: metrics([r for r in warm if r["host"] == host and r.get("editId") == edit]) for host in hosts}
        for edit in edits
    },
    "byCategory": {
        category: {host: metrics([r for r in warm if r["host"] == host and r["category"] == category]) for host in hosts}
        for category in ("Razor", "CSharp", "CSS")
    },
    "cold": {host: metrics([r for r in cold if r["host"] == host]) for host in hosts},
}
args.output.mkdir(parents=True, exist_ok=True)
(args.output / "summary.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
for filename, selected in (("warm.csv", warm), ("cold.csv", cold)):
    with (args.output / filename).open("w", newline="", encoding="utf-8") as stream:
        columns = ["host", "trialId", "editId", "category", "outcome", "elapsedMs", "mechanism"]
        writer = csv.DictWriter(stream, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(selected)

def display(metric):
    if metric["medianMs"] is None:
        return "No successful sample"
    values = [metric[key] / 1000 for key in ("minimumMs", "maximumMs", "rangeMs", "medianMs")]
    return f"{values[0]:.3f} / {values[1]:.3f} / {values[2]:.3f} / **{values[3]:.3f}**"

lines = [
    "# Catalog measurement results",
    "",
    "Values are seconds: minimum / maximum / range / **median**. These are observed end-to-end timings, including tool transport; they are not isolated compiler benchmarks.",
    "",
    "| Edit | Pre-extraction full app | Post-extraction full app | Sandbox |",
    "|---|---|---|---|",
]
for edit, group in report["byEdit"].items():
    lines.append("| " + edit + " | " + " | ".join(display(group[host]) for host in hosts) + " |")
lines.extend(["", "Process-cold results use populated build/restore caches.", "", "| Host | Min / max / range / median (s) |", "|---|---|"])
for host, metric in report["cold"].items():
    lines.append(f"| {host} | {display(metric)} |")
lines.extend(["", f"Primary samples: {len(warm)} warm; {len(cold)} cold."])
lines.extend(f"- Retained outside comparison: {row['trialId']} - {row['reason']}." for row in excluded)
if problems:
    lines.extend(["", "INCOMPLETE OR INVALID COMPARISON:"] + ["- " + problem for problem in problems])
if any(row["outcome"] != "success" for row in warm + cold):
    lines.append("\nFailures remain in the CSV/ledger. Successful-sample statistics alone do not establish reliable iteration.")
(args.output / "results.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps({"completeMatrix": not problems, "warm": len(warm), "cold": len(cold), "problems": problems}, indent=2))
raise SystemExit(0 if not problems else 2)
