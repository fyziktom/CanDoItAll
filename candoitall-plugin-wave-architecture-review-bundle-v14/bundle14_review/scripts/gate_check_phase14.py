#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import re
import sys

repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()

TEXT_SUFFIXES = {".cs", ".razor", ".md", ".json", ".sql", ".txt", ".props", ".targets", ".csproj"}


def should_scan(path: Path) -> bool:
    parts = set(path.parts)
    if "bin" in parts or "obj" in parts or ".git" in parts:
        return False
    return path.suffix.lower() in TEXT_SUFFIXES


def read(rel: str) -> str:
    path = repo / rel
    if not path.exists():
        raise SystemExit(f"Missing expected file: {path}")
    return path.read_text(encoding="utf-8")


def repo_search(pattern: str, include_tests_only: bool = False) -> list[str]:
    rx = re.compile(pattern, re.MULTILINE)
    hits: list[str] = []
    for path in repo.rglob("*"):
        if not path.is_file() or not should_scan(path):
            continue
        rel = path.relative_to(repo).as_posix()
        if include_tests_only and not rel.startswith("tests/"):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except Exception:
            continue
        if rx.search(text):
            hits.append(rel)
    return sorted(set(hits))


def find_matching_brace(text: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index
    raise ValueError("Could not find matching brace.")


def collect_method_bodies(text: str) -> dict[str, str]:
    signature_rx = re.compile(
        r'(?m)^\s*(?:public|private|internal|protected)\s+'
        r'(?:static\s+)?(?:async\s+)?'
        r'[\w<>\[\],\.\?\(\)\s]+\s+'
        r'(?P<name>\w+)\s*\(')
    methods: dict[str, str] = {}
    for match in signature_rx.finditer(text):
        name = match.group("name")
        search_index = match.end()
        open_index = text.find("{", search_index)
        if open_index < 0:
            continue
        try:
            close_index = find_matching_brace(text, open_index)
        except ValueError:
            continue
        methods[name] = text[open_index + 1:close_index]
    return methods


issues: list[str] = []
warnings: list[str] = []

triggering_rel = "src/CanDoItAll.Modules.Automation/AutomationTriggering.cs"
ingress_rel = "src/CanDoItAll.Modules.Automation/AutomationIngressService.cs"
outbox_rel = "src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs"
automation_messaging_rel = "src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs"

triggering = read(triggering_rel)
ingress = read(ingress_rel)
outbox = read(outbox_rel)
automation_messaging = read(automation_messaging_rel)
trigger_methods = collect_method_bodies(triggering)
ingress_methods = collect_method_bodies(ingress)
outbox_methods = collect_method_bodies(outbox)

# P14-001: once-like triggers must retire after firing and stay retired across restart projection.
execute_body = trigger_methods.get("Execute", "")
sync_body = trigger_methods.get("SynchronizeTriggerAsync", "")
if "AutomationTriggerKind.Once" in triggering and not re.search(r"IsEnabled\s*=\s*false", execute_body):
    issues.append(
        "P14-001 FAIL: AutomationTriggerQuartzJob.Execute does not retire/disable once-like triggers after they fire. "
        f"Evidence: {triggering_rel}"
    )
if re.search(r"case\s+AutomationTriggerKind\.Once:|case\s+AutomationTriggerKind\.Relative:|case\s+AutomationTriggerKind\.DueDateProjection:", triggering) and not re.search(
    r"LastFiredAtUtc|NextPlannedFireAtUtc|IsEnabled", sync_body
):
    issues.append(
        "P14-001 FAIL: Quartz scheduler projection has no guard that skips previously-consumed one-shot triggers during restart rehydration. "
        f"Evidence: {triggering_rel}"
    )
for test_name in [
    "One_shot_trigger_is_not_rehydrated_after_it_has_already_fired",
    "Once_like_trigger_is_retired_after_first_fire",
]:
    if not repo_search(rf"\b{re.escape(test_name)}\b", include_tests_only=True):
        issues.append(f"P14-001 FAIL: required one-shot trigger safety test is missing: {test_name}")

# P14-002: save must return the reloaded canonical trigger after Quartz projection updated the record.
save_body = trigger_methods.get("SaveAsync", "")
if "await schedulerBridge.SynchronizeAsync" in save_body and re.search(r"return\s+Map\(record\)\s*;", save_body):
    issues.append(
        "P14-002 FAIL: AutomationTriggerRegistry.SaveAsync still returns the pre-synchronization tracked entity instead of reloading the canonical record after Quartz projection. "
        f"Evidence: {triggering_rel}"
    )
if not repo_search(r"\bTrigger_registry_save_returns_reloaded_next_fire_time_after_quartz_projection\b", include_tests_only=True):
    issues.append("P14-002 FAIL: required trigger-save canonical round-trip test is missing: Trigger_registry_save_returns_reloaded_next_fire_time_after_quartz_projection")

# P14-003: cursor lookups must normalize keys and first-write races must recover from uniqueness conflicts.
get_cursor_body = ingress_methods.get("GetCursorAsync", "")
save_cursor_body = ingress_methods.get("SaveCursorAsync", "")
if re.search(r"item\.SourceKind\s*==\s*sourceKind", get_cursor_body) or re.search(r"item\.SourceKey\s*==\s*sourceKey", get_cursor_body):
    issues.append(
        "P14-003 FAIL: PluginIngressInbox.GetCursorAsync still queries with raw sourceKind/sourceKey instead of normalized trimmed keys. "
        f"Evidence: {ingress_rel}"
    )
if re.search(r"FirstOrDefaultAsync\(item\s*=>\s*item\.SourceKind\s*==\s*sourceKind\s*&&\s*item\.SourceKey\s*==\s*sourceKey", save_cursor_body) and "DbUpdateException" not in save_cursor_body:
    issues.append(
        "P14-003 FAIL: PluginIngressInbox.SaveCursorAsync still uses read-then-insert without uniqueness-conflict recovery for concurrent first writes. "
        f"Evidence: {ingress_rel}"
    )
for test_name in [
    "Plugin_ingress_cursor_save_trims_keys_before_lookup",
    "Concurrent_first_cursor_save_reuses_the_same_cursor_row",
]:
    if not repo_search(rf"\b{re.escape(test_name)}\b", include_tests_only=True):
        issues.append(f"P14-003 FAIL: required cursor normalization/upsert test is missing: {test_name}")

# P14-004: materialization must claim a single-executor boundary before plugin code runs.
materialize_body = ingress_methods.get("MaterializeAsync", "")
materializer_call_index = materialize_body.find("materializer.MaterializeAsync")
if materializer_call_index >= 0:
    prefix = materialize_body[:materializer_call_index]
    if not any(token in prefix for token in ["SaveChangesAsync", "ExecuteUpdateAsync", "BeginTransaction", "Lease", "Lock", "Materializing"]):
        issues.append(
            "P14-004 FAIL: PluginIngressInbox.MaterializeAsync calls plugin materializer code before any persisted claim/state transition, so concurrent callers can materialize the same envelope twice. "
            f"Evidence: {ingress_rel}"
        )
for test_name in [
    "Concurrent_materialize_calls_only_run_the_materializer_once",
    "Already_materialized_envelope_returns_existing_snapshot_without_reinvoking_plugin_code",
]:
    if not repo_search(rf"\b{re.escape(test_name)}\b", include_tests_only=True):
        issues.append(f"P14-004 FAIL: required materialization single-executor test is missing: {test_name}")

# P14-005: direct/manual connector processing must go through the same lease acquisition boundary as the worker.
public_process_body = outbox_methods.get("ProcessAsync", "")
if re.search(r"return\s+commandProcessor\.ProcessAsync\(commandId,\s*cancellationToken:\s*cancellationToken\)\s*;", public_process_body):
    issues.append(
        "P14-005 FAIL: ConnectorOutboxService.ProcessAsync still bypasses lease acquisition and can execute a pending command without claiming the durable single-executor boundary first. "
        f"Evidence: {outbox_rel}"
    )
for test_name in [
    "Direct_process_async_claims_a_lease_before_execution",
    "Concurrent_direct_process_calls_do_not_execute_the_same_command_twice",
]:
    if not repo_search(rf"\b{re.escape(test_name)}\b", include_tests_only=True):
        issues.append(f"P14-005 FAIL: required direct-process lease test is missing: {test_name}")

# Advisories.
if re.search(r"catch\s*\(Exception\s+ex\)", automation_messaging) and "OperationCanceledException" not in automation_messaging:
    warnings.append(
        f"ADV WARNING: Automation message dispatch still catches Exception broadly around handler execution; verify cancellation is propagated distinctly: {automation_messaging_rel}"
    )
if re.search(r"catch\s*\(Exception\s+ex\)", outbox) and "OperationCanceledException" not in outbox:
    warnings.append(
        f"ADV WARNING: Connector command execution still catches Exception broadly around handler execution; verify cancellation is propagated distinctly: {outbox_rel}"
    )

print("=== Phase14 hidden runtime semantics gate ===")
print(f"Repo: {repo}")
print()

if issues:
    print("Hard-gate failures:")
    for item in issues:
        print(f"- {item}")
else:
    print("No hard-gate failures detected.")

if warnings:
    print()
    print("Warnings:")
    for item in warnings:
        print(f"- {item}")

sys.exit(1 if issues else 0)
