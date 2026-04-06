#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import re
import sys
from typing import Iterable


def read_text(path: pathlib.Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="ignore")


def iter_src_files(repo: pathlib.Path) -> Iterable[pathlib.Path]:
    for path in repo.rglob("*.cs"):
        parts = set(path.parts)
        if "tests" in parts:
            continue
        if "candoitall-plugin-wave-architecture-review-bundle-v11" in parts:
            continue
        if "candoitall-plugin-wave-architecture-review-bundle-v12" in parts:
            continue
        yield path


def repo_search(repo: pathlib.Path, pattern: str) -> list[pathlib.Path]:
    rx = re.compile(pattern, re.MULTILINE)
    hits: list[pathlib.Path] = []
    for path in iter_src_files(repo):
        if rx.search(read_text(path)):
            hits.append(path)
    return hits


def file_contains(path: pathlib.Path, pattern: str) -> bool:
    return re.search(pattern, read_text(path), re.MULTILINE) is not None


def rel(repo: pathlib.Path, path: pathlib.Path) -> str:
    return str(path.relative_to(repo)).replace("\\", "/")


def main() -> int:
    repo = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    failures: list[str] = []
    warnings: list[str] = []

    print("=== Phase13 execution-grade runtime hardening gate ===")
    print(f"Repo: {repo}")
    print()

    automation_module = repo / "src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs"
    automation_hosted = repo / "src/CanDoItAll.Modules.Automation/AutomationHostedServices.cs"
    automation_messaging = repo / "src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs"
    automation_ingress = repo / "src/CanDoItAll.Modules.Automation/AutomationIngressService.cs"
    connector_outbox = repo / "src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs"
    prompt_factory = repo / "src/CanDoItAll.Modules.Factory/PromptFactoryService.cs"

    # P13-001: runtime options bound from production configuration.
    config_binding_hits = repo_search(
        repo,
        r"Configure\s*<\s*AutomationRuntimeOptions\s*>|AddOptions\s*<\s*AutomationRuntimeOptions\s*>\s*\(\)\s*\.\s*BindConfiguration|BindConfiguration\s*\(\s*\"Automation"
    )
    config_binding_hits = [path for path in config_binding_hits if "tests" not in path.parts]
    if not config_binding_hits:
        failures.append(
            "P13-001 FAIL: AutomationRuntimeOptions are not bound from production configuration. "
            f"Evidence: {rel(repo, automation_module)}"
        )

    # P13-002: atomic idempotency / conflict recovery.
    if automation_messaging.exists():
        content = read_text(automation_messaging)
        if "FirstOrDefaultAsync(item =>" in content and "AutomationEnvelopeRecord" in content and "DbUpdateException" not in content:
            failures.append(
                "P13-002 FAIL: Automation message publish still uses read-then-insert dedupe without uniqueness-conflict recovery. "
                f"Evidence: {rel(repo, automation_messaging)}"
            )
    if automation_ingress.exists():
        content = read_text(automation_ingress)
        if "FirstOrDefaultAsync(item =>" in content and "PluginIngressEnvelopeRecord" in content and "DbUpdateException" not in content:
            failures.append(
                "P13-002 FAIL: Plugin ingress accept still uses read-then-insert dedupe without uniqueness-conflict recovery. "
                f"Evidence: {rel(repo, automation_ingress)}"
            )
    if connector_outbox.exists():
        content = read_text(connector_outbox)
        if "FirstOrDefaultAsync(item =>" in content and "ConnectorCommandRecord" in content and "DbUpdateException" not in content:
            failures.append(
                "P13-002 FAIL: Connector outbox enqueue still uses read-then-insert idempotency without uniqueness-conflict recovery. "
                f"Evidence: {rel(repo, connector_outbox)}"
            )

    # P13-003: DB-side acquisition / no broad in-memory scans.
    if file_contains(
        automation_messaging,
        r"var\s+dueDeliveries\s*=\s*await\s+dbContext\.Set<AutomationEnvelopeDeliveryRecord>\(\)[\s\S]{0,400}?\.ToListAsync"
    ):
        failures.append(
            "P13-003 FAIL: Automation dispatcher still materializes delivery candidates in memory before due-work acquisition. "
            f"Evidence: {rel(repo, automation_messaging)}"
        )
    if file_contains(
        connector_outbox,
        r"var\s+pendingCommands\s*=\s*await\s+dbContext\.Set<ConnectorCommandRecord>\(\)[\s\S]{0,400}?\.ToListAsync"
    ):
        failures.append(
            "P13-003 FAIL: Connector outbox still materializes pending command candidates in memory before acquisition. "
            f"Evidence: {rel(repo, connector_outbox)}"
        )
    # Hint that locking fields are still not used as a real predicate/claim boundary.
    if automation_messaging.exists():
        content = read_text(automation_messaging)
        lock_predicates = re.findall(r"Where\([^\)]*(LockToken|LockedAtUtc)", content)
        if not lock_predicates and ("LockToken" in content or "LockedAtUtc" in content):
            warnings.append(
                f"ADV WARNING: delivery lock fields exist but are not used as an acquisition predicate: {rel(repo, automation_messaging)}"
            )

    # P13-004: worker-loop exception isolation.
    if automation_hosted.exists() and "catch" not in read_text(automation_hosted):
        failures.append(
            "P13-004 FAIL: Automation hosted worker loops have no iteration-level exception isolation. "
            f"Evidence: {rel(repo, automation_hosted)}"
        )

    # P13-005: legacy queue seam retired from production call sites.
    legacy_callsite_hits: list[str] = []
    for path in iter_src_files(repo):
        if path.name == "BackgroundJobs.cs":
            continue
        text = read_text(path)
        if re.search(r"\bEnqueueTrackedAsync\s*\(", text):
            legacy_callsite_hits.append(rel(repo, path))
    if legacy_callsite_hits:
        failures.append(
            "P13-005 FAIL: Production call sites still schedule work through the legacy background-job queue seam: "
            + ", ".join(sorted(legacy_callsite_hits))
        )
    if automation_hosted.exists() and "Observed legacy background job queue item" in read_text(automation_hosted):
        warnings.append(
            f"ADV WARNING: legacy background queue bridge still looks observational/log-only: {rel(repo, automation_hosted)}"
        )

    # Carry forward important bundle12 warnings.
    if file_contains(repo / "src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs", r"ProjectNodeLegacyMetadata\.ReadLegacyMarkers"):
        warnings.append(
            "ADV WARNING: marker compatibility fallback from metadata is still active in active code: "
            "src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs"
        )
    if file_contains(repo / "src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs", r"ProjectNodeLegacyMetadata\.ReadLegacyReferences"):
        warnings.append(
            "ADV WARNING: reference compatibility fallback from metadata is still active in active code: "
            "src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs"
        )

    for hotspot, limit in [
        (repo / "src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs", 4000),
        (repo / "src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs", 1000),
    ]:
        if hotspot.exists():
            line_count = read_text(hotspot).count("\n") + 1
            if line_count > limit:
                warnings.append(
                    f"ADV WARNING: hotspot '{rel(repo, hotspot)}' is still large ({line_count} lines > {limit})."
                )

    if failures:
        print("Hard-gate failures:")
        for item in failures:
            print(f"- {item}")
    else:
        print("No hard-gate failures detected.")

    if warnings:
        print()
        print("Warnings:")
        for item in warnings:
            print(f"- {item}")

    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
