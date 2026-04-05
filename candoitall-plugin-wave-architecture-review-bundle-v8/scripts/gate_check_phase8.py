#!/usr/bin/env python3
from __future__ import annotations
from pathlib import Path
import re
import sys

repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()

def read(rel: str) -> str:
    path = repo / rel
    if not path.exists():
        raise SystemExit(f"Missing expected file: {path}")
    return path.read_text(encoding="utf-8")

issues: list[str] = []
warnings: list[str] = []

pw_models = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs")
lifecycle = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchLifecycleService.cs")
command = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCommandService.cs")
cross = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutationService.cs")
relation = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchRelationService.cs")
metadata = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs")
party_page = read("src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs")
crmhr = read("src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs")
workspace_models = read("src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs")
provider_exec = read("src/CanDoItAll.Modules.Workspace/ProviderExecution.cs")
resources_page = read("src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor")
settings_page = read("src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor")
resource_models = read("src/CanDoItAll.Modules.Resources/ResourceModels.cs")

# HG-01: node core still persists binding columns / direct writes / leaky metadata
binding_columns = [
    "Route", "ExternalArtifactKind", "ExternalArtifactId",
    "MediaRelativePath", "MediaContentType", "MediaOriginalFileName", "StorageObjectReferenceJson"
]
mapped = [name for name in binding_columns if f"builder.Property(item => item.{name})" in pw_models]
if mapped:
    issues.append("HG-01 FAIL: ProjectObjectRecord still maps binding columns: " + ", ".join(mapped))

direct_binding_write_patterns = [
    (lifecycle, "ProjectWorkbenchLifecycleService"),
    (command, "ProjectWorkbenchCommandService"),
    (cross, "ProjectWorkbenchCrossModuleMutationService"),
]
for text, label in direct_binding_write_patterns:
    hits = []
    for name in binding_columns:
        if re.search(rf"\.{name}\s*=", text):
            hits.append(name)
    if hits:
        issues.append(f"HG-01 FAIL: {label} still mutates binding fields directly: " + ", ".join(sorted(set(hits))))

foreign_reference_markers = [
    "ParticipantIds", "MeetingNodeArtifactId", "TranscriptNodeArtifactId", "RecordingNodeArtifactId",
    "LastProviderProfileId", "ParentParticipantArtifactId", "AssigneeParticipantArtifactId",
    "RepositoryResourceId", "SecretReferenceArtifactId", "StorageCatalogId"
]
leaky = [name for name in foreign_reference_markers if name in metadata]
if leaky:
    issues.append("HG-01 FAIL: writable metadata envelope still exposes foreign-owner IDs: " + ", ".join(leaky))

# HG-02: dual hierarchy persistence
if "ResolveHierarchyLinkKind" in relation or "ResolveHierarchyLinkKind" in pw_models:
    issues.append("HG-02 FAIL: editable-node mutation paths still use ResolveHierarchyLinkKind / persisted hierarchy links.")
if "ProjectWorkbenchGraphConventions.UpsertLinkAsync" in relation:
    issues.append("HG-02 FAIL: reparent path still persists hierarchy links.")
if "ProjectWorkbenchGraphConventions.UpsertLinkAsync" in pw_models:
    issues.append("HG-02 FAIL: create/seed path still persists hierarchy links.")

# HG-03: hardcoded capability rules outside registry
for forbidden in ["ResolveNodeAssignmentRoles", "ResolveParticipantRole"]:
    if forbidden in party_page:
        issues.append(f"HG-03 FAIL: workbench page still owns hardcoded capability rule '{forbidden}'.")
for forbidden in ["RequiresCanonicalNode", "IsAllowedNodeType"]:
    if forbidden in crmhr:
        issues.append(f"HG-03 FAIL: CRM/HR service still owns hardcoded node-role rule '{forbidden}'.")

# Advisory marker dual truth
if "MarkerIcon" in pw_models and "ResolveMarkers(" in metadata:
    warnings.append("ADV WARNING: marker truth still appears dual (legacy scalar marker fields + marker set fallback).")

# HG-04: plugin-first connector platform not yet achieved
provider_ui_patterns = [
    "Enum.GetValues<ProviderKind>()",
    "providerModel.ProviderKind",
]
for pattern in provider_ui_patterns:
    if pattern in settings_page:
        issues.append(f"HG-04 FAIL: provider UI still depends on legacy enum pattern '{pattern}'.")
resource_ui_patterns = [
    "Enum.GetValues<ResourceKind>()",
    "@switch (editor.ResourceKind)",
    "editor.ResourceKind switch",
]
for pattern in resource_ui_patterns:
    if pattern in resources_page:
        issues.append(f"HG-04 FAIL: resources UI still depends on legacy enum pattern '{pattern}'.")
if "TryResolve(ProviderKind providerKind" in provider_exec:
    issues.append("HG-04 FAIL: provider resolution still requires ProviderKind in the active adapter registry API.")
if "public enum ProviderKind" in workspace_models:
    warnings.append("ADV WARNING: ProviderKind still exists. This may be acceptable only as a compatibility alias, not as the active control surface.")
if "public enum ResourceKind" in resource_models:
    warnings.append("ADV WARNING: ResourceKind still exists. This may be acceptable only as a compatibility alias, not as the active control surface.")

# HG-05: durable side-effect boundary absent / compensation still primary
for pattern in ["RestoreDeletedSubtreeAsync", "RestoreMovedDescendantsAsync", "MarkMutationCompensatedAsync"]:
    if pattern in cross:
        issues.append(f"HG-05 FAIL: compensation pattern '{pattern}' is still present in the active cross-module mutation path.")
if "projectPartyIntegrationBridge" in cross:
    issues.append("HG-05 FAIL: cross-module mutation path still performs direct bridge-side work instead of durable intent execution.")

# Advisory hotspot warnings
def line_count(rel: str) -> int:
    return read(rel).count("\n") + 1

for rel, threshold in [
    ("src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs", 4000),
    ("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs", 1000),
    ("src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor", 450),
    ("src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor", 450),
]:
    lines = line_count(rel)
    if lines > threshold:
        warnings.append(f"ADV WARNING: hotspot '{rel}' is still large ({lines} lines > {threshold}).")

print("=== Phase8 plugin-gate check ===")
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
