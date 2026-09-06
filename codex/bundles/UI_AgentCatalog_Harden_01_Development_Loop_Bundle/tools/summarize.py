import argparse
import hashlib
import json
import statistics
from pathlib import Path

parser = argparse.ArgumentParser(description="Summarize the frozen direct local catalog measurements.")
parser.add_argument("runs", nargs=3, type=Path, help="Completed fullapp, parity and fast run directories, in order.")
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
plan_path = Path(__file__).resolve().parent.parent / "plan/frozen-direct-edits.json"
plan = json.loads(plan_path.read_text(encoding="utf-8"))
plan_hash = hashlib.sha256(plan_path.read_bytes()).hexdigest()
rows = []
receipts = []

def metrics(values):
    return {"min": min(values), "max": max(values), "range": max(values) - min(values), "median": statistics.median(values)}

for host, run in zip(plan["hosts"], args.runs, strict=True):
    entries = [json.loads(line) for line in (run / "ledger.jsonl").read_text(encoding="utf-8").splitlines()]
    protocol = next(row for row in entries if row["kind"] == "protocol")
    assert protocol["host"] == host
    assert protocol["planSha256"] == plan_hash
    assert protocol["harnessSha256"] == plan["harnessSha256"]
    assert any(row["kind"] == "complete" for row in entries)
    assert not any(row["kind"] == "failed" for row in entries)
    warm = [row for row in entries if row["kind"] == "warm"]
    assert len(warm) == len(plan["edits"]) * plan["successfulRepetitionsPerEdit"]
    receipts.append({"host": host, "runId": protocol["runId"], "planSha256": plan_hash, "harnessSha256": protocol["harnessSha256"]})
    for edit in plan["edits"]:
        trials = [row for row in warm if row["editId"] == edit["id"]]
        assert sorted(row["repetition"] for row in trials) == list(range(1, plan["successfulRepetitionsPerEdit"] + 1))
        for trial in trials:
            assert trial["success"] and trial["undoVisible"]
            assert trial["sourceSha256"] == trial["undoSha256"] == edit["sourceSha256"]
            assert len(trial["sdkEvents"]) == 1
            assert trial["ready"]["ownerId"] == trial["after"]["ownerId"] == protocol["runId"]
            assert trial["after"]["watchIteration"] >= trial["ready"]["watchIteration"] >= 1
        rows.append({"host": host, "edit": edit["category"], "successes": len(trials),
            "classifications": {kind: sum(row["classification"] == kind for row in trials) for kind in plan["classification"]},
            "sdkMs": metrics([row["sdkEvents"][0]["milliseconds"] for row in trials]),
            "visibleMs": metrics([row["elapsedMs"] for row in trials]),
            "firstVisibleMs": metrics([row["firstVisibleMs"] for row in trials]),
            "databaseConfirmations": sum(row["databaseConfirmations"] for row in trials),
            "sourceSha256": edit["sourceSha256"]})
    complete = next(row for row in entries if row["kind"] == "complete")
    assert complete["productionCssAfter"] == protocol["productionCssBefore"] == plan["environment"]["productionThemeSha256"]

args.output.mkdir(parents=True, exist_ok=True)
report = {"protocol": plan["protocol"], "runs": receipts, "rows": rows, "primarySuccesses": sum(row["successes"] for row in rows)}
(args.output / "direct-results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
lines = ["| Host | Edit | Successful | SDK ms min / max / range / median | Visible ms min / max / range / median | Hot reload / browser reload / restart / failed |", "| --- | --- | ---: | --- | --- | --- |"]
for row in rows:
    fmt = lambda value: " / ".join(f"{value[key]:.1f}" for key in ("min", "max", "range", "median"))
    c = row["classifications"]
    lines.append(f"| {row['host']} | {row['edit']} | {row['successes']} | {fmt(row['sdkMs'])} | {fmt(row['visibleMs'])} | {c['hot-reload']} / {c['browser-reload']} / {c['restart']} / 0 |")
(args.output / "direct-results-table.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
