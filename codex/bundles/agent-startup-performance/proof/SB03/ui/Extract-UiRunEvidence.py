from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import stat
import sys
from typing import Any
from uuid import UUID


EXECUTION_ROOTS = {
    "native": Path(r"C:/Users/lucys/AppData/Local/CanDoItAll/workspace/runtime-overrides/ff24611dad478ec960349d9ad11d1017/data/scopes/organization/e5df9ad633dbc6974a0678a74976013c/execution"),
    "client": Path(r"C:/repositories/CanDoItAll/.artifacts/shared-providers-e2e/client-a/data/workspace/data/scopes/organization/3dfd771ef0fef5ef9ff8845e3efa2580/execution"),
}
STATES = ("Idle", "Preparing", "Running", "WaitingOnTool", "Persisting", "Completed", "Failed")
OUTCOMES = ("Succeeded", "Failed", "Cancelled")
APPROVAL_STATES = ("Pending", "Approved", "Rejected")
USAGE_STATES = ("Observed", "MissingAfterProviderActivity", "UsageUnavailable", "EstimatedFromMetric", "ObservedFromMetric")
SIDE_EFFECTS = ("Unspecified", "NoMutation", "ManagedProcessArtifacts", "ExternalArtifactDestination", "ProductMutation")
PHASES = frozenset(("Planning", "Framework", "Approval", "Context contributors", "Skills", "Workspace tools", "Runtime tool providers", "Capability", "Compaction", "Execution authority", "Model parameters", "Session", "Run", "Streaming", "Completed", "Failed", "Cancelled"))
COLLECTIONS = ("logs", "metrics", "usage", "approvals", "audit/receipts")
MAX_FILE_BYTES = 8 * 1024 * 1024
MAX_TOTAL_BYTES = 32 * 1024 * 1024
MAX_RECORD_FILES = 1000
IDENTIFIER = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.:/-]{0,127}\Z")


class EvidenceError(Exception):
    pass


def guid(value: Any, nullable: bool = False) -> str | None:
    if nullable and value is None:
        return None
    if not isinstance(value, str):
        raise EvidenceError("Expected a record GUID.")
    return str(UUID(value))


def identifier(value: Any) -> str:
    if not isinstance(value, str) or not IDENTIFIER.fullmatch(value):
        raise EvidenceError("A structured identifier is absent or outside the bounded schema.")
    return value


def timestamp(value: Any, nullable: bool = False) -> str | None:
    if nullable and value is None:
        return None
    if not isinstance(value, str):
        raise EvidenceError("Expected a timestamp.")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.utcoffset() is None:
        raise EvidenceError("A timestamp lacks an explicit UTC offset.")
    return parsed.astimezone(timezone.utc).isoformat()


def number(value: Any) -> int:
    if type(value) is not int or value < 0:
        raise EvidenceError("A count or duration is not a nonnegative integer.")
    return value


def boolean(value: Any) -> bool:
    if type(value) is not bool:
        raise EvidenceError("Expected a boolean.")
    return value


def enum_name(value: Any, names: tuple[str, ...], nullable: bool = False) -> str | None:
    if nullable and value is None:
        return None
    if type(value) is int and 0 <= value < len(names):
        return names[value]
    if isinstance(value, str) and value in names:
        return value
    raise EvidenceError("A persisted enum value is outside the reviewed schema.")


def objects(value: Any) -> list[dict[str, Any]]:
    if not isinstance(value, list) or len(value) > MAX_RECORD_FILES or not all(isinstance(item, dict) for item in value):
        raise EvidenceError("A record collection is outside the bounded schema.")
    return value


def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise EvidenceError("Duplicate JSON keys prevent reliable evidence extraction.")
        result[key] = value
    return result


class Snapshot:
    def __init__(self, root: Path):
        self.root = root
        self.files: dict[Path, str] = {}
        self.directories: dict[Path, tuple[str, ...] | None] = {}
        self.total_bytes = 0

    def safe_path(self, path: Path) -> None:
        if not path.is_relative_to(self.root):
            raise EvidenceError("A source path escaped the frozen execution root.")
        for ancestor in (*reversed(path.parents), path):
            try:
                info = ancestor.lstat()
            except FileNotFoundError:
                continue
            if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                raise EvidenceError("A source path contains a symlink or reparse point.")
        if not path.resolve().is_relative_to(self.root.resolve()):
            raise EvidenceError("A resolved source path escaped the frozen execution root.")

    def directory_names(self, path: Path) -> tuple[str, ...] | None:
        self.safe_path(path)
        if not path.exists():
            return None
        names = []
        for entry in path.iterdir():
            names.append(entry.name)
            if len(names) > MAX_RECORD_FILES:
                raise EvidenceError("A source directory exceeds the record limit.")
        return tuple(sorted(names))

    def read_bytes(self, path: Path) -> bytes:
        self.safe_path(path)
        before = path.stat()
        if not stat.S_ISREG(before.st_mode) or before.st_size > MAX_FILE_BYTES:
            raise EvidenceError("A source file exceeds the bounded regular-file schema.")
        with path.open("rb") as stream:
            content = stream.read(MAX_FILE_BYTES + 1)
        after = path.stat()
        if len(content) > MAX_FILE_BYTES or (before.st_ino, before.st_size, before.st_mtime_ns) != (after.st_ino, after.st_size, after.st_mtime_ns):
            raise EvidenceError("A source file changed during capture or exceeded its limit.")
        return content

    def read(self, path: Path) -> dict[str, Any]:
        content = self.read_bytes(path)
        if path not in self.files:
            self.total_bytes += len(content)
        if self.total_bytes > MAX_TOTAL_BYTES or len(self.files) >= MAX_RECORD_FILES:
            raise EvidenceError("The requested observation exceeds the aggregate evidence limit.")
        digest = hashlib.sha256(content).hexdigest()
        if path in self.files and self.files[path] != digest:
            raise EvidenceError("A previously observed source file changed.")
        self.files[path] = digest
        value = json.loads(content.decode("utf-8-sig"), object_pairs_hook=reject_duplicate_keys)
        if not isinstance(value, dict):
            raise EvidenceError("Expected a JSON record object.")
        return value

    def records(self, path: Path) -> list[dict[str, Any]]:
        names = self.directory_names(path)
        self.directories[path] = names
        return [self.read(path / name) for name in names or () if name.endswith(".json")]

    def assert_no_journal(self) -> None:
        self.safe_path(self.root)
        if any(self.root.glob("pending-*.json")):
            raise EvidenceError("A pending journal requires a later quiescent observation; no recovery was invoked.")

    def verify_unchanged(self) -> None:
        self.assert_no_journal()
        for path, names in self.directories.items():
            if self.directory_names(path) != names:
                raise EvidenceError("A record collection changed during capture.")
        for path, digest in self.files.items():
            if hashlib.sha256(self.read_bytes(path)).hexdigest() != digest:
                raise EvidenceError("A source record changed during capture.")
        self.assert_no_journal()


def approval_fields(record: dict[str, Any]) -> dict[str, Any]:
    return {"approvalId": identifier(record["approvalId"]), "callId": identifier(record["callId"]), "toolName": identifier(record["toolName"])}


def receipt_outcome(value: Any) -> str:
    if not isinstance(value, str):
        raise EvidenceError("Expected a tool outcome string.")
    if value in ("Succeeded", "Failed", "Denied", "Rejected", "Cancelled"):
        return value
    if value.startswith("Failed: "):
        return "Failed"
    return "UnclassifiedRedacted"


def selected_run(snapshot: Snapshot, run_id: str) -> dict[str, Any]:
    run_root = snapshot.root / "runs" / UUID(run_id).hex
    run = snapshot.read(run_root / "run.json")
    if guid(run["id"]) != run_id:
        raise EvidenceError("The requested run identity does not match its record.")
    agent_id = guid(run["agentId"])
    session_id = guid(run["chatSessionId"], nullable=True)
    state = enum_name(run["state"], STATES)
    result: dict[str, Any] = {
        "runId": run_id,
        "agentId": agent_id,
        "chatSessionId": session_id,
        "state": state,
        "outcome": enum_name(run["outcome"], OUTCOMES, nullable=True),
        "isTerminal": state in ("Completed", "Failed"),
        "revision": number(run["revision"]),
        "autoApprovePendingToolCalls": boolean(run["autoApprovePendingToolCalls"]),
        "pendingApprovals": [approval_fields(item) for item in objects(run["pendingApprovals"])],
    }
    for field in ("createdAtUtc", "updatedAtUtc", "startedAtUtc", "completedAtUtc"):
        result[field] = timestamp(run[field], nullable=field in ("startedAtUtc", "completedAtUtc"))
    result["pendingApprovalCount"] = len(result["pendingApprovals"])
    for collection in COLLECTIONS:
        entries = []
        for record in snapshot.records(run_root / collection):
            if guid(record.get("executionRunId"), nullable=collection == "usage") not in (run_id, None):
                raise EvidenceError("A collection record belongs to another run.")
            if "agentId" in record and record["agentId"] is not None and guid(record["agentId"]) != agent_id:
                raise EvidenceError("A collection record belongs to another agent.")
            if "chatSessionId" in record and record["chatSessionId"] is not None and guid(record["chatSessionId"]) != session_id:
                raise EvidenceError("A collection record belongs to another session.")
            entry: dict[str, Any] = {"id": guid(record["id"])} if collection != "approvals" else approval_fields(record)
            if collection in ("logs", "metrics", "usage"):
                entry["createdAtUtc"] = timestamp(record["createdAtUtc"])
            if collection == "logs":
                entry.update(state=enum_name(record["state"], STATES), phase=record["phase"] if record["phase"] in PHASES else "OtherRedacted")
            elif collection == "metrics":
                entry["outcome"] = enum_name(record["outcome"], OUTCOMES)
                for field in ("durationMs", "inputTokens", "outputTokens", "toolCalls", "cachedInputTokens", "cacheWriteTokens"):
                    entry[field] = number(record[field])
            elif collection == "usage":
                entry["usageStatus"] = enum_name(record["usageStatus"], USAGE_STATES)
                entry["explicitRunIdentityPresent"] = record.get("executionRunId") is not None
                for field in ("inputTokens", "cachedInputTokens", "outputTokens", "reasoningTokens", "totalTokens", "toolCallCount", "cacheWriteTokens"):
                    entry[field] = number(record[field])
            elif collection == "approvals":
                entry.update(status=enum_name(record["status"], APPROVAL_STATES), requestedAtUtc=timestamp(record["requestedAtUtc"]), decidedAtUtc=timestamp(record["decidedAtUtc"], nullable=True))
            else:
                started = timestamp(record["startedAtUtc"])
                completed = timestamp(record["completedAtUtc"])
                duration = (datetime.fromisoformat(completed) - datetime.fromisoformat(started)).total_seconds()
                if duration < 0:
                    raise EvidenceError("A receipt has an inverted time interval.")
                entry.update(toolName=identifier(record["toolName"]), startedAtUtc=started, completedAtUtc=completed, durationMilliseconds=round(duration * 1000, 3), outcome=receipt_outcome(record["exitSummary"]), sideEffectMode=enum_name(record["declaredSideEffectMode"], SIDE_EFFECTS))
            entries.append(entry)
        result[collection.replace("audit/", "")] = entries
    if session_id is not None:
        session = snapshot.read(snapshot.root / "sessions" / (UUID(session_id).hex + ".json"))
        if guid(session["id"]) != session_id or guid(session["agentId"]) != agent_id:
            raise EvidenceError("The session record identity does not match the selected run.")
        messages = objects(session["messages"])
        roles = Counter(enum_name(item["role"], ("System", "User", "Assistant")) for item in messages)
        result["sessionSnapshot"] = {
            "id": session_id,
            "latestExecutionRunId": guid(session["latestExecutionRunId"], nullable=True),
            "createdAtUtc": timestamp(session["createdAtUtc"]),
            "updatedAtUtc": timestamp(session["updatedAtUtc"]),
            "messageCount": len(messages),
            "messageCountsByRole": dict(roles),
            "messageTokenEstimateTotal": sum(number(item["tokenEstimate"]) for item in messages),
            "scope": "Current whole-session counts; messages are not attributed to an individual run.",
        }
    result["recordCounts"] = {name: len(result[name]) for name in ("logs", "metrics", "usage", "approvals", "receipts")}
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description="Read selected frozen-host run files without app calls; emit only allowlisted IDs, states, timing, counts, decisions and redacted tool outcomes to stdout.")
    parser.add_argument("--host", choices=tuple(EXECUTION_ROOTS), required=True)
    parser.add_argument("--run-id", action="append", required=True, help="Exact run GUID supplied by the UI test owner; repeat for at most 12 distinct runs.")
    args = parser.parse_args()
    if not 1 <= len(args.run_id) <= 12:
        raise EvidenceError("Request between one and twelve run IDs.")
    run_ids = [guid(value) for value in args.run_id]
    if len(set(run_ids)) != len(run_ids):
        raise EvidenceError("Run IDs must be distinct.")
    started = datetime.now(timezone.utc).isoformat()
    snapshot = Snapshot(EXECUTION_ROOTS[args.host])
    snapshot.assert_no_journal()
    runs = [selected_run(snapshot, run_id) for run_id in run_ids]
    snapshot.verify_unchanged()
    result = {
        "schemaVersion": 1,
        "host": args.host,
        "captureStartedUtc": started,
        "captureCompletedUtc": datetime.now(timezone.utc).isoformat(),
        "snapshotFileCount": len(snapshot.files),
        "snapshotSourceBytes": snapshot.total_bytes,
        "sourceRecordsUnchangedAtRecheck": True,
        "requiresLaterTerminalObservation": any(not run["isTerminal"] for run in runs),
        "limits": "Supporting persisted evidence only. No app calls, locks, recovery or source writes. Hash/list rechecks detect ordinary concurrent changes but are not an atomic snapshot or a guarantee against noncooperating races. Tool text, arguments, message content, provider/model data, environments and serialized contexts are never emitted. Unknown freeform tool outcomes remain UnclassifiedRedacted. Metrics and usage observations are separate overlapping evidence, not additive totals.",
        "runs": runs,
    }
    print(json.dumps(result, indent=2, allow_nan=False))


if __name__ == "__main__":
    try:
        main()
    except EvidenceError as error:
        print(f"Evidence extraction refused: {error}", file=sys.stderr)
        sys.exit(1)
    except (OSError, ValueError, KeyError, TypeError, RecursionError):
        print("Evidence extraction refused: a source file or requested value is missing, changed, unreadable or outside the reviewed schema; no source content was printed.", file=sys.stderr)
        sys.exit(1)
