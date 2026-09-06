import argparse
import copy
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser(description="Export only the frozen catalog rendering fixture from an isolated test snapshot.")
parser.add_argument("--input", type=Path, required=True)
parser.add_argument("--output", type=Path)
parser.add_argument("--verify", type=Path)
args = parser.parse_args()
if (args.output is None) == (args.verify is None):
    parser.error("Choose exactly one of --output or --verify.")
original = args.input.read_bytes()
snapshot = json.loads(original.decode("utf-8-sig"))
if set(snapshot) != {"agents", "teams", "privateProviderById"}:
    raise SystemExit("Unexpected snapshot contract.")
fixture = copy.deepcopy(snapshot)
for agent in fixture["agents"]:
    agent["instructions"] = ""
    agent["configurationJson"] = ""
    agent["permissions"]["allowedSecrets"] = []
    for capability in agent["capabilities"]:
        capability["proofNotes"] = ""
payload = (json.dumps(fixture, indent=2, ensure_ascii=True) + "\n").replace("\n", "\r\n").encode("utf-8")
if args.verify:
    existing = args.verify.read_bytes()
    if fixture != json.loads(existing.decode("utf-8-sig")):
        raise SystemExit("The sandbox fixture differs from the sanitized isolated snapshot.")
    identical_bytes = payload == existing
else:
    if args.output.exists():
        raise SystemExit("Refusing to overwrite an existing fixture; export to a new path for review.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(payload)
    identical_bytes = True
print(json.dumps({
    "state": "Verified" if args.verify else "Exported",
    "inputSha256": hashlib.sha256(original).hexdigest(),
    "outputSha256": hashlib.sha256(payload).hexdigest(),
    "agents": len(fixture["agents"]),
    "teams": len(fixture["teams"]),
    "bytes": len(payload),
    "byteIdentical": identical_bytes,
    "secretReferences": sum(len(a["permissions"]["allowedSecrets"]) for a in fixture["agents"])
}, indent=2))

