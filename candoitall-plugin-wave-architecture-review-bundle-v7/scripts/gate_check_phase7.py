#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")

def find_file(root: Path, pattern: str) -> list[Path]:
    return list(root.rglob(pattern))

def fail(failures: list[str], message: str) -> None:
    failures.append(message)

def warn(warnings: list[str], message: str) -> None:
    warnings.append(message)

def pass_msg(passes: list[str], message: str) -> None:
    passes.append(message)

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=".", help="Repository root to scan.")
    args = parser.parse_args()

    repo = Path(args.repo).resolve()
    src = repo / "src"
    tests = repo / "tests"

    failures: list[str] = []
    warnings: list[str] = []
    passes: list[str] = []

    workbench_models = src / "CanDoItAll.Modules.Workbench" / "ProjectWorkbenchModels.cs"
    metadata_file = src / "CanDoItAll.Modules.Workbench" / "ProjectWorkbenchMetadata.cs"
    page_party = src / "CanDoItAll.Modules.Workbench" / "Pages" / "ProjectStructurePage.PartyIntegration.cs"
    crmhr_services = src / "CanDoItAll.Modules.CrmHr" / "CrmHrServices.cs"
    workspace_models = src / "CanDoItAll.Modules.Workspace" / "WorkspaceModels.cs"
    provider_execution = src / "CanDoItAll.Modules.Workspace" / "ProviderExecution.cs"
    resource_models = src / "CanDoItAll.Modules.Resources" / "ResourceModels.cs"

    wb_text = read_text(workbench_models)
    metadata_text = read_text(metadata_file)
    page_text = read_text(page_party)
    crmhr_text = read_text(crmhr_services)
    workspace_text = read_text(workspace_models)
    provider_text = read_text(provider_execution)
    resource_text = read_text(resource_models)

    # G1: Parallel truth
    if "private async Task SyncGraphAsync" in wb_text or re.search(r"await\s+SyncGraphAsync\(", wb_text):
        fail(failures, "G1 FAIL - Workbench still contains SyncGraph-style persisted projection sync.")
    else:
        pass_msg(passes, "G1 PASS - No SyncGraph-style persisted projection sync was detected.")

    # G2: Overloaded carrier
    banned_carrier_fields = [
        "Route",
        "ExternalArtifactKind",
        "ExternalArtifactId",
        "MediaRelativePath",
        "MediaContentType",
        "MediaOriginalFileName",
        "StorageObjectReferenceJson",
    ]
    missing = []
    carrier_block = wb_text.split("public sealed class ProjectObjectRecord", 1)[1].split("internal sealed class ProjectObjectRecordConfiguration", 1)[0]
    remaining_banned = [field for field in banned_carrier_fields if re.search(rf"\b{re.escape(field)}\b", carrier_block)]
    if remaining_banned:
        fail(failures, "G2 FAIL - The node carrier still owns overloaded binding/projection fields: " + ", ".join(remaining_banned))
    else:
        pass_msg(passes, "G2 PASS - The carrier no longer exposes the overloaded binding/projection fields checked by this gate.")

    # G3: Node-kind registry and capability rules
    registry_files = find_file(src, "*ProjectNodeKindRegistry*.cs") + find_file(src, "*NodeKindRegistry*.cs")
    descriptor_hits = list(find_file(src, "*ProjectNodeKindDescriptor*.cs")) + list(find_file(src, "*NodeKindDescriptor*.cs"))
    hardcoded_role_helpers = ["ResolveNodeAssignmentRoles(", "ResolveParticipantRole("]
    hardcoded_crm_helpers = ["RequiresCanonicalNode(", "IsAllowedNodeType("]
    if not registry_files and not descriptor_hits:
        fail(failures, "G3 FAIL - No central ProjectNodeKindRegistry/descriptor implementation was found.")
    if any(token in page_text for token in hardcoded_role_helpers):
        fail(failures, "G3 FAIL - ProjectStructurePage still hardcodes node assignment role rules.")
    if any(token in crmhr_text for token in hardcoded_crm_helpers):
        fail(failures, "G3 FAIL - CRM/HR still hardcodes node-role capability checks.")

    # G4: Reclassification history
    transition_files = find_file(src, "*TransitionHistory*.cs") + find_file(src, "*NodeTransition*.cs")
    if re.search(r"ReclassifyObjectAsync[\s\S]{0,1200}node\.ObjectType\s*=\s*request\.TargetObjectType", wb_text):
        fail(failures, "G4 FAIL - Reclassification still mutates the active node kind in place.")
    if not transition_files:
        fail(failures, "G4 FAIL - No node transition history implementation was found.")

    # G5: Hierarchy dual-write
    if "ResolveHierarchyLinkKind" in wb_text:
        fail(failures, "G5 FAIL - Editable hierarchy still appears to derive link persistence through ResolveHierarchyLinkKind.")
    dual_write_patterns = [
        r"UpsertLinkAsync\([\s\S]{0,220}ProjectObjectLinkKind\.Contains",
        r"UpsertLinkAsync\([\s\S]{0,220}ProjectObjectLinkKind\.BelongsTo",
    ]
    if any(re.search(pattern, wb_text) for pattern in dual_write_patterns):
        fail(failures, "G5 FAIL - Editable hierarchy still appears to persist Contains/BelongsTo links directly.")

    # G6: Metadata foreign ids and dual marker truth
    banned_metadata_tokens = [
        "ParticipantIds",
        "MeetingNodeArtifactId",
        "TranscriptNodeArtifactId",
        "RecordingNodeArtifactId",
        "LastProviderProfileId",
        "ParentParticipantArtifactId",
        "AssigneeParticipantArtifactId",
        "RepositoryResourceId",
        "SecretReferenceArtifactId",
        "StorageCatalogId",
    ]
    leaked_tokens = [token for token in banned_metadata_tokens if token in metadata_text]
    if leaked_tokens:
        fail(failures, "G6 FAIL - Workbench metadata still exposes foreign-id helper fields: " + ", ".join(leaked_tokens))
    if "ResolveMarkers(" in metadata_text and "legacyMarker" in metadata_text:
        fail(failures, "G6 FAIL - Marker truth still falls back between metadata and legacy marker columns.")

    # G7: Closed enum/switch connector seam
    if re.search(r"\benum\s+ProviderKind\b", workspace_text):
        fail(failures, "G7 FAIL - ProviderKind enum still exists as the provider extensibility seam.")
    if re.search(r"\benum\s+ResourceKind\b", resource_text):
        fail(failures, "G7 FAIL - ResourceKind enum still exists as the resource extensibility seam.")
    connector_descriptor_files = find_file(src, "*ConnectorDescriptor*.cs") + find_file(src, "*ConnectorManifest*.cs")
    if not connector_descriptor_files:
        fail(failures, "G7 FAIL - No connector descriptor/manifest implementation was found.")

    # G8: Hard closure enforcement
    guardrail_tests = find_file(tests, "*ArchitectureGuardrail*.cs") + find_file(tests, "*CanonicalGuardrail*.cs") + find_file(tests, "*GuardrailTests.cs")
    if not guardrail_tests:
        fail(failures, "G8 FAIL - No dedicated architecture guardrail test suite was found.")
    if "gate_check_phase7.py" not in str(repo):
        pass_msg(passes, "G8 NOTE - This script is running from the bundle, not from the target repository.")
    # Watch-only checks
    if "assignment reconciliation" in wb_text:
        warn(warnings, "W1 WARN - Compensation-style assignment reconciliation strings are still present.")
    if len(wb_text.splitlines()) > 2500:
        warn(warnings, "W2 WARN - ProjectWorkbenchModels.cs is still a large hotspot.")
    if len(crmhr_text.splitlines()) > 4000:
        warn(warnings, "W3 WARN - CrmHrServices.cs is still a large hotspot.")

    print("PHASE7 HARD-GATE CHECK")
    print(f"Repository: {repo}")
    print("")
    for item in passes:
        print(item)
    if warnings:
        print("")
        for item in warnings:
            print(item)
    if failures:
        print("")
        for item in failures:
            print(item)
        print("")
        print(f"RESULT: FAIL ({len(failures)} hard-gate failure(s))")
        return 1

    print("")
    print("RESULT: PASS")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
