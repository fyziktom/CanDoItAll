#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
import re
import sys

repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()

TEXT_SUFFIXES = {".cs", ".razor", ".md", ".json", ".sql", ".txt", ".props", ".targets"}

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
    return hits

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
    # This is intentionally simple but good enough for the current review bundle.
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
                issues.append(
                    f"transitive mutation detected in {' -> '.join(stack + [method_name])}: {label}")
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
bindings_rel = "src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs"
phase9_gate_rel = "candoitall-plugin-wave-architecture-review-bundle-v9/scripts/gate_check_phase9.py"

assembly = read(assembly_rel)
bindings = read(bindings_rel)
phase9_gate = read(phase9_gate_rel)

# HG-10-01 and HG-10-02: read path must be zero-write.
load_mutation_issues = analyze_transitive_mutations(assembly, "LoadAsync")
for item in load_mutation_issues:
    issues.append(f"HG-10-01 FAIL: {item}")

if re.search(r'\bRetireLegacyProjectionRowsAsync\s*\(', collect_method_bodies(assembly).get("LoadAsync", "")):
    issues.append("HG-10-01 FAIL: LoadAsync still calls RetireLegacyProjectionRowsAsync from the read path.")

if re.search(r'layoutOverrides\.Values[\s\S]{0,500}?dbContext\.RemoveRange\s*\([\s\S]{0,200}?SaveChangesAsync', collect_method_bodies(assembly).get("LoadAsync", "")):
    issues.append("HG-10-01 FAIL: LoadAsync still deletes stale projection layouts and saves during reads.")

# HG-10-03: required zero-write and repair tests must exist.
required_tests = [
    "GetStructureAsync_does_not_delete_stale_system_managed_projection_rows",
    "GetStructureAsync_does_not_delete_stale_projection_layout_rows",
    "GetStructureAsync_does_not_write_when_legacy_marker_and_reference_fallback_is_used",
    "Explicit_projection_repair_removes_stale_system_managed_rows_and_orphan_layouts_idempotently",
]
for test_name in required_tests:
    hits = repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True)
    if not hits:
        issues.append(f"HG-10-03 FAIL: required zero-write/repair test is missing: {test_name}")

# HG-10-04: the phase10 gate must close the phase9 false green.
if "NormalizeAndHydrateAsync" in phase9_gate and "RetireLegacyProjectionRowsAsync" not in phase9_gate and "RemoveRange" not in phase9_gate:
    warnings.append("ADV WARNING: the phase9 gate still appears focused on the old normalization symbols and does not obviously scan the current stale-projection delete path.")

if not load_mutation_issues:
    # If the load mutation analysis finds nothing, make sure the new gate still proves something meaningful.
    warnings.append("ADV WARNING: transitive load mutation analysis found no issues. Review manually that the new gate still rejects the old false-green scenario.")

# HG-10-05: future plugin-wave editor proof must include unknown-manifest tests.
required_plugin_tests = [
    "Settings_page_renders_unknown_provider_manifest_fields_through_shared_field_editor",
    "Resources_page_renders_unknown_resource_manifest_fields_through_shared_field_editor",
    "Unknown_connector_manifest_fields_round_trip_without_page_specific_code",
]
for test_name in required_plugin_tests:
    hits = repo_search(rf'\b{re.escape(test_name)}\b', include_tests_only=True)
    if not hits:
        issues.append(f"HG-10-05 FAIL: required unknown-plugin proof test is missing: {test_name}")

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
            "HG-10-05 FAIL: the unknown-plugin proof tests still do not visibly exercise all shared connector field types: "
            + ", ".join(missing_field_types))

# Advisory warnings: remaining compatibility fallback is still active.
for pattern, label in [
    (r'ProjectNodeLegacyMetadata\.ReadLegacyMarkers', "marker compatibility fallback from metadata is still active"),
    (r'ProjectNodeLegacyMetadata\.ReadLegacyReferences', "reference compatibility fallback from metadata is still active"),
]:
    hits = repo_search(pattern)
    if hits:
        warnings.append(f"ADV WARNING: {label} in active code: {', '.join(sorted(set(hits)))}")

# Advisory warnings: hotspots.
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
