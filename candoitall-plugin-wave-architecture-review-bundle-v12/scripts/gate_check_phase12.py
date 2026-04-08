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

def repo_search(
    pattern: str,
    *,
    include_tests_only: bool = False,
    include_src_only: bool = False,
    include_migrations_only: bool = False,
) -> list[str]:
    rx = re.compile(pattern, re.MULTILINE)
    hits: list[str] = []
    for path in repo.rglob("*"):
        if not path.is_file() or not should_scan(path):
            continue
        rel = path.relative_to(repo).as_posix()
        if include_tests_only and not rel.startswith("tests/"):
            continue
        if include_src_only and not rel.startswith("src/"):
            continue
        if include_migrations_only and not rel.startswith("src/CanDoItAll.Migrations."):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except Exception:
            continue
        if rx.search(text):
            hits.append(rel)
    return sorted(set(hits))

def count_repo_hits(
    pattern: str,
    *,
    include_src_only: bool = False,
) -> int:
    rx = re.compile(pattern, re.MULTILINE)
    count = 0
    for path in repo.rglob("*"):
        if not path.is_file() or not should_scan(path):
            continue
        rel = path.relative_to(repo).as_posix()
        if include_src_only and not rel.startswith("src/"):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except Exception:
            continue
        count += len(rx.findall(text))
    return count

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
        body = text[open_index + 1:close_index]
        methods[name] = body
    return methods

MUTATION_PATTERNS = [
    (re.compile(r'\bSaveChangesAsync\s*\('), "SaveChangesAsync"),
    (re.compile(r'\bSaveChanges\s*\('), "SaveChanges"),
    (re.compile(r'dbContext\.RemoveRange\s*\('), "dbContext.RemoveRange"),
    (re.compile(r'dbContext\.Remove\s*\('), "dbContext.Remove"),
    (re.compile(r'dbContext\.Set<[^>]+>\(\)\.(?:RemoveRange|Remove)\s*\('), "dbContext.Set<T>().Remove*"),
    (re.compile(r'dbContext\.Add(?:Range)?(?:Async)?\s*\('), "dbContext.Add*"),
    (re.compile(r'dbContext\.Set<[^>]+>\(\)\.(?:Add(?:Range)?(?:Async)?)\s*\('), "dbContext.Set<T>().Add*"),
    (re.compile(r'dbContext\.Update(?:Range)?\s*\('), "dbContext.Update*"),
    (re.compile(r'dbContext\.Set<[^>]+>\(\)\.(?:Update(?:Range)?)\s*\('), "dbContext.Set<T>().Update*"),
    (re.compile(r'\bExecuteDeleteAsync\s*\('), "ExecuteDeleteAsync"),
    (re.compile(r'\bExecuteUpdateAsync\s*\('), "ExecuteUpdateAsync"),
    (re.compile(r'Database\.ExecuteSql(?:Raw|Interpolated)Async\s*\('), "Database.ExecuteSql*Async"),
    (re.compile(r'EntityState\.(?:Added|Modified|Deleted)\b'), "EntityState.Added/Modified/Deleted"),
]

def analyze_transitive_mutations(file_text: str, root_method_name: str) -> list[str]:
    methods = collect_method_bodies(file_text)
    if root_method_name not in methods:
        return [f"Could not locate method body for {root_method_name}."]
    issues: list[str] = []
    visited: set[str] = set()
    local_method_names = set(methods.keys())

    def inspect(method_name: str, stack: list[str]) -> None:
        if method_name in visited:
            return
        visited.add(method_name)
        body = methods[method_name]
        for rx, label in MUTATION_PATTERNS:
            if rx.search(body):
                issues.append(f"transitive mutation detected in {' -> '.join(stack + [method_name])}: {label}")
        for candidate in sorted(local_method_names):
            if candidate == method_name:
                continue
            if re.search(rf'\b{re.escape(candidate)}\s*\(', body):
                inspect(candidate, stack + [method_name])

    inspect(root_method_name, [])
    return issues

issues: list[str] = []
warnings: list[str] = []

# HG-12-01: phase10 zero-write recovery must still be closed.
assembly_rel = "src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs"
workbench_di_rel = "src/CanDoItAll.Modules.Workbench/WorkbenchModuleServiceCollectionExtensions.cs"
maintenance_rel = "src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs"
assembly = read(assembly_rel)
workbench_di = read(workbench_di_rel)

load_mutation_issues = analyze_transitive_mutations(assembly, "LoadAsync")
for item in load_mutation_issues:
    issues.append(f"HG-12-01 FAIL: {item}")

load_body = collect_method_bodies(assembly).get("LoadAsync", "")
if re.search(r'\bRetireLegacyProjectionRowsAsync\s*\(', load_body):
    issues.append("HG-12-01 FAIL: LoadAsync still calls RetireLegacyProjectionRowsAsync from the read path.")

if re.search(r'layoutOverrides\.Values[\s\S]{0,500}?dbContext\.RemoveRange\s*\([\s\S]{0,200}?SaveChangesAsync', load_body):
    issues.append("HG-12-01 FAIL: LoadAsync still deletes stale projection layouts and saves during reads.")

if not (repo / maintenance_rel).exists():
    issues.append("HG-12-01 FAIL: ProjectStructureProjectionMaintenanceService.cs is missing.")
else:
    maintenance_text = read(maintenance_rel)
    if "RepairAsync" not in maintenance_text:
        issues.append("HG-12-01 FAIL: ProjectStructureProjectionMaintenanceService does not expose RepairAsync.")
if "AddScoped<ProjectStructureProjectionMaintenanceService>()" not in workbench_di:
    issues.append("HG-12-01 FAIL: Workbench DI no longer registers ProjectStructureProjectionMaintenanceService.")

for test_name in [
    "GetStructureAsync_does_not_delete_stale_system_managed_projection_rows",
    "GetStructureAsync_does_not_delete_stale_projection_layout_rows",
    "GetStructureAsync_does_not_write_when_legacy_marker_and_reference_fallback_is_used",
    "Explicit_projection_repair_removes_stale_system_managed_rows_and_orphan_layouts_idempotently",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-01 FAIL: required zero-write/repair test is missing: {test_name}")

# HG-12-02: phase10 unknown-manifest shared editor proof must still be closed.
settings_razor_rel = "src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor"
settings_code_rel = "src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs"
resources_code_rel = "src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs"
field_editor_rel = "src/CanDoItAll.Modules.Workspace/Pages/Components/ConnectorConfigFieldEditor.razor"

settings_razor = read(settings_razor_rel)
settings_code = read(settings_code_rel)
resources_code = read(resources_code_rel)
field_editor = read(field_editor_rel)

if 'Secrets="secrets"' not in settings_razor:
    issues.append("HG-12-02 FAIL: SettingsPage no longer passes secret options into ConnectorConfigFieldEditor.")
if not re.search(r'InputCheckbox[\s\S]{0,200}?data-testid="@TestId"', field_editor):
    issues.append("HG-12-02 FAIL: ConnectorConfigFieldEditor boolean branch no longer exposes a shared data-testid hook.")
if "provider-config-" not in settings_code:
    issues.append("HG-12-02 FAIL: SettingsPage no longer assigns generic test ids for unknown provider manifest fields.")
if "resource-config-" not in resources_code:
    issues.append("HG-12-02 FAIL: ResourcesPage no longer assigns generic test ids for unknown resource manifest fields.")

required_plugin_tests = [
    "Settings_page_renders_unknown_provider_manifest_fields_through_shared_field_editor",
    "Resources_page_renders_unknown_resource_manifest_fields_through_shared_field_editor",
    "Unknown_connector_manifest_fields_round_trip_without_page_specific_code",
]
for test_name in required_plugin_tests:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-02 FAIL: required unknown-plugin proof test is missing: {test_name}")

required_field_types = [
    "ConnectorConfigFieldType.Text",
    "ConnectorConfigFieldType.Url",
    "ConnectorConfigFieldType.Number",
    "ConnectorConfigFieldType.Boolean",
    "ConnectorConfigFieldType.Json",
    "ConnectorConfigFieldType.SecretReference",
]
plugin_test_files: set[str] = set()
for test_name in required_plugin_tests:
    plugin_test_files.update(repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True))
if plugin_test_files:
    missing_field_types: list[str] = []
    for token in required_field_types:
        token_found = False
        for rel in plugin_test_files:
            if token in read(rel):
                token_found = True
                break
        if not token_found:
            missing_field_types.append(token)
    if missing_field_types:
        issues.append(
            "HG-12-02 FAIL: the unknown-plugin proof tests still do not visibly exercise all shared connector field types: "
            + ", ".join(missing_field_types))

# HG-12-03: operational execution plane must be separate from nodes + signals must aggregate.
signal_agg_hits = repo_search(r'\bIEnumerable\s*<\s*IAutomationSignal(?:Source|Provider)\s*>', include_src_only=True)
composite_hits = repo_search(r'\bCompositeAutomationSignalProvider\b|\bIAutomationSignalSource\b', include_src_only=True)
singular_automation_workspace_hits = repo_search(r'AutomationWorkspaceService[\s\S]{0,250}IAutomationSignalProvider', include_src_only=True)
if not signal_agg_hits and not composite_hits:
    issues.append("HG-12-03 FAIL: no multi-source automation signal aggregation seam detected (expected IEnumerable<IAutomationSignalSource/Provider> or CompositeAutomationSignalProvider).")
if singular_automation_workspace_hits and not signal_agg_hits and not composite_hits:
    issues.append("HG-12-03 FAIL: automation workspace still appears to consume a singular IAutomationSignalProvider.")
for test_name in [
    "Operational_messages_do_not_materialize_workbench_nodes_by_default",
    "Explicit_materializer_can_turn_an_execution_result_into_a_domain_artifact",
    "AutomationWorkspaceService_aggregates_multiple_signal_sources_without_last_registration_wins",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-03 FAIL: required test is missing: {test_name}")

# HG-12-04: canonical trigger registry + Quartz-backed projection.
for symbol in ["AutomationTriggerRecord"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-04 FAIL: required scheduler symbol is missing from src: {symbol}")
    elif not repo_search(rf'\b{re.escape(symbol)}\b', include_migrations_only=True):
        issues.append(f"HG-12-04 FAIL: persistent scheduler symbol is not visible in EF migrations/snapshots: {symbol}")
for symbol in ["IAutomationTriggerRegistry", "QuartzAutomationSchedulerBridge", "AutomationTriggerFireRequest"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-04 FAIL: required scheduler symbol is missing from src: {symbol}")

quartz_hits = repo_search(r'\bQuartz\b|\bAddQuartz\b|\bIJob\b|\bJobKey\b|\bTriggerKey\b', include_src_only=True)
if not quartz_hits:
    issues.append("HG-12-04 FAIL: no Quartz integration detected.")
for test_name in [
    "Automation_trigger_definition_round_trips_with_cron_timezone_and_misfire_policy",
    "Quartz_scheduler_bridge_rehydrates_canonical_triggers_on_startup",
    "Quartz_trigger_fire_publishes_durable_work_instead_of_running_plugin_logic_inline",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-04 FAIL: required test is missing: {test_name}")

# HG-12-05: durable internal message plane.
for symbol in ["AutomationEnvelopeRecord", "AutomationDeadLetterRecord"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-05 FAIL: required durable message-plane symbol is missing from src: {symbol}")
    elif not repo_search(rf'\b{re.escape(symbol)}\b', include_migrations_only=True):
        issues.append(f"HG-12-05 FAIL: persistent message-plane symbol is not visible in EF migrations/snapshots: {symbol}")
for symbol in ["IAutomationMessagePublisher", "IAutomationMessageDispatcher", "IAutomationMessageHandler", "AutomationSubscriptionRegistry"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-05 FAIL: required durable message-plane symbol is missing from src: {symbol}")
for test_name in [
    "Internal_message_dispatch_retries_then_dead_letters_failed_handlers_idempotently",
    "Internal_message_publish_fans_out_to_multiple_subscribers",
    "Internal_message_delivery_survives_restart_boundary",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-05 FAIL: required test is missing: {test_name}")

# HG-12-06: hosted workers must drain queues/outboxes/triggers.
hosted_worker_hits = repo_search(r'\bIHostedService\b|\bBackgroundService\b|\bAddHostedService\b', include_src_only=True)
if not hosted_worker_hits:
    issues.append("HG-12-06 FAIL: no hosted worker/runtime service detected.")
dequeue_hits = count_repo_hits(r'\bDequeueAsync\s*\(', include_src_only=True)
if dequeue_hits <= 2:
    issues.append("HG-12-06 FAIL: background work dequeue side still has no visible consumer.")
connector_pending_hits = count_repo_hits(r'\bProcessPendingAsync\s*\(', include_src_only=True)
if connector_pending_hits <= 1:
    issues.append("HG-12-06 FAIL: connector outbox pending processor still has no visible runtime caller.")
for test_name in [
    "Connector_outbox_pending_commands_are_processed_by_a_hosted_worker",
    "Queued_background_work_is_consumed_by_a_runtime_worker",
    "Due_triggers_are_dispatched_without_manual_invocation",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-06 FAIL: required test is missing: {test_name}")

# HG-12-07: ingress inbox boundary.
for symbol in ["PluginIngressEnvelopeRecord", "PluginIngressCursorRecord"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-07 FAIL: required plugin ingress symbol is missing from src: {symbol}")
    elif not repo_search(rf'\b{re.escape(symbol)}\b', include_migrations_only=True):
        issues.append(f"HG-12-07 FAIL: persistent plugin ingress symbol is not visible in EF migrations/snapshots: {symbol}")
for symbol in ["IPluginIngressInbox", "IPluginIngressMaterializer"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-07 FAIL: required plugin ingress symbol is missing from src: {symbol}")
for test_name in [
    "Plugin_ingress_inbox_deduplicates_external_envelopes",
    "Plugin_ingress_cursor_progress_is_persisted_across_runs",
    "Ingress_envelope_can_remain_unmaterialized_until_explicit_handler_runs",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-07 FAIL: required test is missing: {test_name}")

# HG-12-08: observability + optional MQTT bridge.
for symbol in ["AutomationExecutionLogRecord", "AutomationDeliveryAttemptRecord"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-08 FAIL: required execution/telemetry symbol is missing from src: {symbol}")
    elif not repo_search(rf'\b{re.escape(symbol)}\b', include_migrations_only=True):
        issues.append(f"HG-12-08 FAIL: persistent execution/telemetry symbol is not visible in EF migrations/snapshots: {symbol}")
for symbol in ["IAutomationTelemetryPublisher"]:
    if not repo_search(rf'\b{re.escape(symbol)}\b', include_src_only=True):
        issues.append(f"HG-12-08 FAIL: required execution/telemetry symbol is missing from src: {symbol}")
mqtt_hits = repo_search(r'\bMQTTnet\b|\bMqttAutomationTelemetryBridge\b|\bIMqtt', include_src_only=True)
if not mqtt_hits:
    warnings.append("ADV WARNING: no optional MQTT telemetry bridge detected yet. This is acceptable only until HG-12-08 is fully implemented.")
for test_name in [
    "Execution_telemetry_preserves_correlation_and_causation_across_dispatch",
    "Dead_letter_items_are_visible_to_operators",
    "Core_runtime_still_functions_when_mqtt_bridge_is_disabled",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-12-08 FAIL: required test is missing: {test_name}")

# Advisories.
for pattern, label in [
    (r'ProjectNodeLegacyMetadata\.ReadLegacyMarkers', "marker compatibility fallback from metadata is still active"),
    (r'ProjectNodeLegacyMetadata\.ReadLegacyReferences', "reference compatibility fallback from metadata is still active"),
]:
    hits = repo_search(pattern, include_src_only=True)
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

print("=== Phase12 recovery + plugin-runtime gate check ===")
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
