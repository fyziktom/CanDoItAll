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

def count_repo_hits(pattern: str) -> int:
    rx = re.compile(pattern, re.MULTILINE)
    count = 0
    for path in repo.rglob("*"):
        if not path.is_file() or not should_scan(path):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except Exception:
            continue
        count += len(rx.findall(text))
    return count

issues: list[str] = []
warnings: list[str] = []

# HG-11-01: no default operational-node modeling + multi-source automation signals.
signal_agg_hits = repo_search(r'\bIEnumerable\s*<\s*IAutomationSignal(?:Source|Provider)\s*>')
composite_hits = repo_search(r'\bCompositeAutomationSignalProvider\b|\bIAutomationSignalSource\b')
singular_automation_workspace_hits = repo_search(r'AutomationWorkspaceService[\s\S]{0,250}IAutomationSignalProvider')
if not signal_agg_hits and not composite_hits:
    issues.append("HG-11-01 FAIL: no multi-source automation signal aggregation seam detected (expected IEnumerable<IAutomationSignalSource/Provider> or CompositeAutomationSignalProvider).")
if singular_automation_workspace_hits and not signal_agg_hits and not composite_hits:
    issues.append("HG-11-01 FAIL: automation workspace still appears to consume a singular IAutomationSignalProvider.")

node_materialization_tests = [
    "Operational_messages_do_not_materialize_workbench_nodes_by_default",
    "Explicit_materializer_can_turn_an_execution_result_into_a_domain_artifact",
    "AutomationWorkspaceService_aggregates_multiple_signal_sources_without_last_registration_wins",
]
for test_name in node_materialization_tests:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-11-01 FAIL: required test is missing: {test_name}")

# HG-11-02: canonical trigger registry + Quartz projection.
required_trigger_types = [
    "AutomationTriggerRecord",
    "IAutomationTriggerRegistry",
    "QuartzAutomationSchedulerBridge",
]
for symbol in required_trigger_types:
    if not repo_search(rf'\b{re.escape(symbol)}\b'):
        issues.append(f"HG-11-02 FAIL: required scheduler symbol is missing: {symbol}")

quartz_hits = repo_search(r'\bQuartz\b|\bAddQuartz\b|\bIJob\b|\bJobKey\b|\bTriggerKey\b')
if not quartz_hits:
    issues.append("HG-11-02 FAIL: no Quartz integration detected.")

for test_name in [
    "Automation_trigger_definition_round_trips_with_cron_timezone_and_misfire_policy",
    "Quartz_scheduler_bridge_rehydrates_canonical_triggers_on_startup",
    "Quartz_trigger_fire_publishes_durable_work_instead_of_running_plugin_logic_inline",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-11-02 FAIL: required test is missing: {test_name}")

# HG-11-03: durable internal message plane.
required_message_types = [
    "AutomationEnvelopeRecord",
    "IAutomationMessagePublisher",
    "IAutomationMessageDispatcher",
    "AutomationSubscriptionRegistry",
    "AutomationDeadLetterRecord",
]
for symbol in required_message_types:
    if not repo_search(rf'\b{re.escape(symbol)}\b'):
        issues.append(f"HG-11-03 FAIL: required durable message-plane symbol is missing: {symbol}")

for test_name in [
    "Internal_message_dispatch_retries_then_dead_letters_failed_handlers_idempotently",
    "Internal_message_publish_fans_out_to_multiple_subscribers",
    "Internal_message_delivery_survives_restart_boundary",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-11-03 FAIL: required test is missing: {test_name}")

# HG-11-04: hosted workers must drain queues/outboxes/triggers.
hosted_worker_hits = repo_search(r'\bIHostedService\b|\bBackgroundService\b|\bAddHostedService\b')
if not hosted_worker_hits:
    issues.append("HG-11-04 FAIL: no hosted worker/runtime service detected.")

dequeue_hits = repo_search(r'\bDequeueAsync\s*\(')
# Current repo has only the declaration and concrete queue implementation, so <=2 hits means no consumer.
if len(dequeue_hits) <= 2:
    issues.append("HG-11-04 FAIL: background work dequeue side still has no visible consumer.")

connector_pending_hits = repo_search(r'\bProcessPendingAsync\s*\(')
# Current repo has only the definition. Require at least one extra hit.
if len(connector_pending_hits) <= 1:
    issues.append("HG-11-04 FAIL: connector outbox pending processor still has no visible runtime caller.")

for test_name in [
    "Connector_outbox_pending_commands_are_processed_by_a_hosted_worker",
    "Queued_background_work_is_consumed_by_a_runtime_worker",
    "Due_triggers_are_dispatched_without_manual_invocation",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-11-04 FAIL: required test is missing: {test_name}")

# HG-11-05: ingress inbox boundary.
required_ingress_types = [
    "PluginIngressEnvelopeRecord",
    "PluginIngressCursorRecord",
    "IPluginIngressInbox",
    "IPluginIngressMaterializer",
]
for symbol in required_ingress_types:
    if not repo_search(rf'\b{re.escape(symbol)}\b'):
        issues.append(f"HG-11-05 FAIL: required plugin ingress symbol is missing: {symbol}")

for test_name in [
    "Plugin_ingress_inbox_deduplicates_external_envelopes",
    "Plugin_ingress_cursor_progress_is_persisted_across_runs",
    "Ingress_envelope_can_remain_unmaterialized_until_explicit_handler_runs",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-11-05 FAIL: required test is missing: {test_name}")

# HG-11-06: observability + optional MQTT.
required_observability_types = [
    "AutomationExecutionLogRecord",
    "AutomationDeliveryAttemptRecord",
    "IAutomationTelemetryPublisher",
]
for symbol in required_observability_types:
    if not repo_search(rf'\b{re.escape(symbol)}\b'):
        issues.append(f"HG-11-06 FAIL: required execution/telemetry symbol is missing: {symbol}")

mqtt_hits = repo_search(r'\bMQTTnet\b|\bMqttAutomationTelemetryBridge\b|\bIMqtt')
if not mqtt_hits:
    warnings.append("ADV WARNING: no optional MQTT telemetry bridge detected yet. This is acceptable only until HG-11-06 is fully implemented.")

for test_name in [
    "Execution_telemetry_preserves_correlation_and_causation_across_dispatch",
    "Dead_letter_items_are_visible_to_operators",
    "Core_runtime_still_functions_when_mqtt_bridge_is_disabled",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-11-06 FAIL: required test is missing: {test_name}")

# Advisory: remaining phase10 advisories.
for pattern, label in [
    (r'ProjectNodeLegacyMetadata\.ReadLegacyMarkers', "marker compatibility fallback from metadata is still active"),
    (r'ProjectNodeLegacyMetadata\.ReadLegacyReferences', "reference compatibility fallback from metadata is still active"),
]:
    hits = repo_search(pattern)
    if hits:
        warnings.append(f"ADV WARNING: {label}: {', '.join(hits)}")

for rel, threshold in [
    ("src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs", 4000),
    ("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs", 1000),
]:
    path = repo / rel
    if path.exists():
        lines = path.read_text(encoding="utf-8").count("\n") + 1
        if lines > threshold:
            warnings.append(f"ADV WARNING: hotspot '{rel}' is still large ({lines} lines > {threshold}).")

print("=== Phase11 plugin-runtime gate check ===")
print(f"Repo: {repo}")

if issues:
    print("\nHard-gate failures:")
    for item in issues:
        print(f"- {item}")
else:
    print("\nNo hard-gate failures detected.")

if warnings:
    print("\nWarnings:")
    for item in warnings:
        print(f"- {item}")

sys.exit(1 if issues else 0)
