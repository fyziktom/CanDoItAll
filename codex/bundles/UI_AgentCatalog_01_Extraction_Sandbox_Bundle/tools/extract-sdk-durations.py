import argparse
import csv
import gzip
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser(description="Correlate SDK-reported apply durations with frozen primary trial cursor windows.")
parser.add_argument("--ledger", type=Path, required=True)
parser.add_argument("--logs", type=Path, nargs="+", required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
entries = {}
for path in args.logs:
    payload = gzip.decompress(path.read_bytes()) if path.suffix == ".gz" else path.read_bytes()
    envelope = json.loads(payload.decode("utf-8-sig"))
    if not envelope["ok"] or envelope["data"]["truncated"]:
        raise SystemExit("Log archive is failed or truncated: " + path.name)
    data = envelope["data"]
    entries[data["sessionId"]] = data["entries"]
rows = [json.loads(line) for line in args.ledger.read_text(encoding="utf-8").splitlines() if line.strip()]
result = []
for row in rows:
    if row["kind"] != "warm" or row["host"] not in ("pre", "post", "sandbox"):
        continue
    if row["category"] == "CSS" and row.get("cssProtocol") != "v2-static-completion":
        continue
    session = row["watch"]["sessionId"]
    if session not in entries:
        continue
    matches = []
    for event in entries[session]:
        if row["ready"]["lastCursor"] < event["sequence"] <= row["watch"]["lastCursor"]:
            match = re.search(r"(C# and Razor|Static asset) changes applied in (\d+)ms\.", event["text"])
            if match:
                matches.append({"kind": match.group(1), "milliseconds": int(match.group(2)), "sequence": event["sequence"], "timestampUtc": event["timestampUtc"]})
    result.append({
        "trialId": row["trialId"], "host": row["host"], "editId": row["editId"], "category": row["category"],
        "observedEditToVisibleMs": row["elapsedMs"], "sdkApplyMs": matches[0]["milliseconds"] if len(matches) == 1 else None,
        "eventCount": len(matches), "events": matches
    })
args.output.mkdir(parents=True, exist_ok=True)
(args.output/"sdk-events.json").write_text(json.dumps({
    "meaning": "SDK-reported update-apply duration, not browser-visible latency or an isolated build benchmark. Zero milliseconds is the SDK's resolution/reporting, not zero user latency.",
    "samples": result
}, indent=2)+"\n", encoding="utf-8")
with (args.output/"sdk-durations.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.DictWriter(stream, fieldnames=["trialId","host","editId","category","observedEditToVisibleMs","sdkApplyMs","eventCount"], extrasaction="ignore")
    writer.writeheader()
    writer.writerows(result)
print(json.dumps({"mappedSamples": len(result), "singleEventSamples": sum(r["eventCount"] == 1 for r in result), "ambiguousOrMissing": [r["trialId"] for r in result if r["eventCount"] != 1]}))

