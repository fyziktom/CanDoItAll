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

def repo_search(pattern: str) -> list[str]:
    rx = re.compile(pattern, re.MULTILINE)
    hits: list[str] = []
    for path in repo.rglob("*"):
        if not path.is_file() or not should_scan(path):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except Exception:
            continue
        if rx.search(text):
            hits.append(path.relative_to(repo).as_posix())
    return hits

issues: list[str] = []
warnings: list[str] = []

pw_models = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs")
pw_schema = read("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs")
bindings = read("src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs")
markers = read("src/CanDoItAll.Modules.Workbench/ProjectNodeMarkerState.cs")
assembly = read("src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs")
resources_page = read("src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor")
resources_page_cs = read("src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs")
resource_models = read("src/CanDoItAll.Modules.Resources/ResourceModels.cs")
settings_page = read("src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor")
settings_page_cs = read("src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs")
workspace_models = read("src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs")
provider_exec = read("src/CanDoItAll.Modules.Workspace/ProviderExecution.cs")

# HG-01 legacy carrier retirement
legacy_carrier_file = repo / "src/CanDoItAll.Modules.Workbench/ProjectObjectRecord.LegacyCarrier.cs"
if legacy_carrier_file.exists():
    issues.append("HG-01 FAIL: legacy carrier file still exists: src/CanDoItAll.Modules.Workbench/ProjectObjectRecord.LegacyCarrier.cs")

legacy_carrier_columns = [
    "Route",
    "ExternalArtifactKind",
    "ExternalArtifactId",
    "MediaRelativePath",
    "MediaContentType",
    "MediaOriginalFileName",
    "StorageObjectReferenceJson",
]
for col in legacy_carrier_columns:
    if f'("{col}"' in pw_schema or f'"{col}"' in pw_schema:
        issues.append(f"HG-01 FAIL: Workbench_ProjectObjects/schema initializer still carries legacy carrier column '{col}'.")

for patt, msg in [
    (r'node\.(Route|ExternalArtifactKind|ExternalArtifactId|MediaRelativePath|MediaContentType|MediaOriginalFileName|StorageObjectReferenceJson)\s*=', "ProjectNodeBindingStorage still writes binding data into legacy carrier fields."),
    (r'ResolveText\(binding\.Route,\s*node\.Route\)', "ResolveBinding still falls back from binding state to node.Route."),
    (r'ResolveArtifactKind\(binding\.ExternalArtifactKind,\s*node\.ExternalArtifactKind', "ResolveBinding still falls back from binding state to node.ExternalArtifactKind."),
    (r'node\.Binding\s*=\s*new ProjectNodeBindingState\(', "Projection assembly still seeds binding state from node carrier fields."),
    (r'HasLegacyCarrierPayload', "Legacy carrier payload detection still exists in active binding code."),
]:
    if re.search(patt, bindings) or re.search(patt, assembly):
        issues.append(f"HG-01 FAIL: {msg}")

# HG-02 marker single truth
for patt, msg in [
    (r'public string MarkerIcon', "ProjectObjectRecord still persists MarkerIcon."),
    (r'public string MarkerTone', "ProjectObjectRecord still persists MarkerTone."),
    (r'public string MarkerLabel', "ProjectObjectRecord still persists MarkerLabel."),
]:
    if re.search(patt, pw_models):
        issues.append(f"HG-02 FAIL: {msg}")
for token in ["MarkerIcon", "MarkerTone", "MarkerLabel"]:
    if token in pw_schema:
        issues.append(f"HG-02 FAIL: schema initializer still carries scalar marker column '{token}'.")
for token, msg in [
    ("ResolveLegacyJson", "ProjectNodeMarkerState still exposes ResolveLegacyJson in active code."),
    ("HydrateLegacyFields", "ProjectNodeMarkerState still exposes HydrateLegacyFields in active code."),
]:
    if token in markers:
        issues.append(f"HG-02 FAIL: {msg}")
if "ProjectNodeMarkerState.NormalizeAndHydrateAsync" in assembly:
    issues.append("HG-02 FAIL: load path still calls marker normalization/hydration.")

# HG-03 plugin editors must be manifest-driven
for text, label in [(resources_page, "ResourcesPage.razor"), (settings_page, "SettingsPage.razor")]:
    if "@switch (field.Key)" in text:
        issues.append(f"HG-03 FAIL: {label} still renders connector fields via @switch(field.Key).")
# stronger anti-evasion: flag hardcoded plugin field bag still living in shared editor model
resource_editor_field_names = [
    "RepositoryUrl", "DefaultBranch", "RelativePath", "FolderPath", "FilePath", "WorkingDirectory",
    "WebUrl", "UrlTitleHint", "Host", "Port", "RemotePath", "UserName", "ScriptPath",
    "ScriptArguments", "ComposeFilePath", "ComposeService", "SecretPurpose", "PromptReference",
    "PromptTitleHint", "EndpointUrl", "HealthPath", "HttpMethod"
]
resource_editor_hits = [name for name in resource_editor_field_names if re.search(rf'public .* {name} \{{ get; set; \}}', resource_models)]
if len(resource_editor_hits) >= 5:
    issues.append("HG-03 FAIL: ResourceEditorModel is still a current-plugin hardcoded property bag instead of a generic manifest-driven config state.")

# HG-04 legacy enum identity must be compatibility-only
for token, msg in [
    ("EnsureLegacyResourceKind", "resource editor still synthesizes legacy ResourceKind."),
    ("ResolveLegacyResourceKind", "resource editor still resolves legacy ResourceKind from plugin key."),
]:
    if token in resources_page_cs:
        issues.append(f"HG-04 FAIL: {msg}")
for patt, msg in [
    (r'entity\.ResourceKind\s*=\s*connectorPlugin\.LegacyResourceKind\s*\?\?\s*model\.ResourceKind', "resource save flow still persists fallback legacy enum identity."),
    (r'entity\.ProviderKind\s*=\s*providerPlugin\.LegacyProviderKind\s*\?\?\s*model\.ProviderKind', "provider save flow still persists fallback legacy enum identity."),
]:
    if re.search(patt, resource_models) or re.search(patt, workspace_models):
        issues.append(f"HG-04 FAIL: {msg}")
for patt, msg in [
    (r'public ProviderKind ProviderKind \{ get; set; \}', "ProviderProfile/ProviderProfileEditorModel still exposes active ProviderKind surface."),
    (r'public ResourceKind ResourceKind \{ get; set; \}', "ProjectResource/ResourceEditorModel still exposes active ResourceKind surface."),
]:
    # warning, not hard fail by itself
    if re.search(patt, workspace_models) or re.search(patt, resource_models):
        warnings.append(f"ADV WARNING: {msg}")

# HG-05 node references must be open-world
for patt, msg in [
    (r'public enum ProjectNodeReferenceKind', "closed-world ProjectNodeReferenceKind enum still exists."),
    (r'public sealed class ProjectNodeReferenceSet', "closed-world ProjectNodeReferenceSet fixed property bag still exists."),
    (r'public Guid ReferenceId \{ get; set; \}', "ProjectNodeReferenceRecord.ReferenceId is still Guid-only."),
]:
    if re.search(patt, bindings):
        issues.append(f"HG-05 FAIL: {msg}")

# HG-06 read paths must be read-only
if "ProjectNodeBindingStorage.NormalizeAndHydrateAsync" in assembly:
    issues.append("HG-06 FAIL: load path still calls binding normalization/hydration.")
if "ProjectNodeMarkerState.NormalizeAndHydrateAsync" in assembly:
    issues.append("HG-06 FAIL: load path still calls marker normalization/hydration.")
# make sure the normalize methods themselves do not save
if re.search(r'NormalizeAndHydrateAsync[\s\S]{0,2000}?SaveChangesAsync', bindings):
    issues.append("HG-06 FAIL: ProjectNodeBindingStorage.NormalizeAndHydrateAsync still saves changes.")
if re.search(r'NormalizeAndHydrateAsync[\s\S]{0,1200}?SaveChangesAsync', markers):
    issues.append("HG-06 FAIL: ProjectNodeMarkerState.NormalizeAndHydrateAsync still saves changes.")

# MG-01 manual warning for durable connector command boundary
outbox_like = repo_search(r'ConnectorCommand|ConnectorOutbox|Outbox|ExternalOperationIntent|IdempotencyKey')
if not outbox_like:
    warnings.append("MG-01 WARNING: no obvious generic connector command/outbox/idempotency boundary was found. Manual plugin-wave sign-off must stay blocked.")

# hotspot warnings
for rel, threshold in [
    ("src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs", 4000),
    ("src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs", 1000),
]:
    path = repo / rel
    if path.exists():
        lines = path.read_text(encoding="utf-8").count("\n") + 1
        if lines > threshold:
            warnings.append(f"ADV WARNING: hotspot '{rel}' is still large ({lines} lines > {threshold}).")

print("=== Phase9 plugin-gate check ===")
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
