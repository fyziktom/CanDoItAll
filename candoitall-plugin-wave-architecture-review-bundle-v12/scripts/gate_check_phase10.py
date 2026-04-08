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

def repo_search(pattern: str, *, include_tests_only: bool = False, include_src_only: bool = False) -> list[str]:
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

MUTATION_PATTERNS = [
    (re.compile(r'\bSaveChangesAsync\s*\('), "SaveChangesAsync"),
    (re.compile(r'\bSaveChanges\s*\('), "SaveChanges"),
    (re.compile(r'dbContext\.RemoveRange\s*\('), "dbContext.RemoveRange"),
    (re.compile(r'dbContext\.Remove\s*\('), "dbContext.Remove"),
    (re.compile(r'dbContext\.Set<[^>]+>\(\)\.(?:RemoveRange|Remove)\s*\('), "dbContext.Set<T>().Remove*"),
    (re.compile(r'\bExecuteDeleteAsync\s*\('), "ExecuteDeleteAsync"),
    (re.compile(r'\bExecuteUpdateAsync\s*\('), "ExecuteUpdateAsync"),
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

assembly_rel = "src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs"
workbench_di_rel = "src/CanDoItAll.Modules.Workbench/WorkbenchModuleServiceCollectionExtensions.cs"
maintenance_rel = "src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs"
settings_razor_rel = "src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor"
settings_code_rel = "src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs"
resources_code_rel = "src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs"
field_editor_rel = "src/CanDoItAll.Modules.Workspace/Pages/Components/ConnectorConfigFieldEditor.razor"

assembly = read(assembly_rel)
workbench_di = read(workbench_di_rel)
settings_razor = read(settings_razor_rel)
settings_code = read(settings_code_rel)
resources_code = read(resources_code_rel)
field_editor = read(field_editor_rel)

for item in analyze_transitive_mutations(assembly, "LoadAsync"):
    issues.append(f"HG-10-01 FAIL: {item}")

load_body = collect_method_bodies(assembly).get("LoadAsync", "")
if re.search(r'\bRetireLegacyProjectionRowsAsync\s*\(', load_body):
    issues.append("HG-10-01 FAIL: LoadAsync still calls RetireLegacyProjectionRowsAsync from the read path.")
if re.search(r'layoutOverrides\.Values[\s\S]{0,500}?dbContext\.RemoveRange\s*\([\s\S]{0,200}?SaveChangesAsync', load_body):
    issues.append("HG-10-01 FAIL: LoadAsync still deletes stale projection layouts and saves during reads.")

if not (repo / maintenance_rel).exists():
    issues.append("HG-10-03 FAIL: ProjectStructureProjectionMaintenanceService.cs is missing.")
else:
    maintenance_text = read(maintenance_rel)
    if "RepairAsync" not in maintenance_text:
        issues.append("HG-10-03 FAIL: ProjectStructureProjectionMaintenanceService does not expose RepairAsync.")
if "AddScoped<ProjectStructureProjectionMaintenanceService>()" not in workbench_di:
    issues.append("HG-10-03 FAIL: Workbench DI no longer registers ProjectStructureProjectionMaintenanceService.")

for test_name in [
    "GetStructureAsync_does_not_delete_stale_system_managed_projection_rows",
    "GetStructureAsync_does_not_delete_stale_projection_layout_rows",
    "GetStructureAsync_does_not_write_when_legacy_marker_and_reference_fallback_is_used",
    "Explicit_projection_repair_removes_stale_system_managed_rows_and_orphan_layouts_idempotently",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-10-03 FAIL: required zero-write/repair test is missing: {test_name}")

if 'Secrets="secrets"' not in settings_razor:
    issues.append("HG-10-05 FAIL: SettingsPage no longer passes secret options into ConnectorConfigFieldEditor.")
if not re.search(r'InputCheckbox[\s\S]{0,200}?data-testid="@TestId"', field_editor):
    issues.append("HG-10-05 FAIL: ConnectorConfigFieldEditor boolean branch no longer exposes a shared data-testid hook.")
if "provider-config-" not in settings_code:
    issues.append("HG-10-05 FAIL: SettingsPage no longer assigns generic test ids for unknown provider manifest fields.")
if "resource-config-" not in resources_code:
    issues.append("HG-10-05 FAIL: ResourcesPage no longer assigns generic test ids for unknown resource manifest fields.")

for test_name in [
    "Settings_page_renders_unknown_provider_manifest_fields_through_shared_field_editor",
    "Resources_page_renders_unknown_resource_manifest_fields_through_shared_field_editor",
    "Unknown_connector_manifest_fields_round_trip_without_page_specific_code",
]:
    if not repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True):
        issues.append(f"HG-10-05 FAIL: required unknown-plugin proof test is missing: {test_name}")

for pattern, label in [
    (r'ProjectNodeLegacyMetadata\.ReadLegacyMarkers', "marker compatibility fallback from metadata is still active in active code"),
    (r'ProjectNodeLegacyMetadata\.ReadLegacyReferences', "reference compatibility fallback from metadata is still active in active code"),
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

print("=== Phase10 plugin-gate check ===")
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
