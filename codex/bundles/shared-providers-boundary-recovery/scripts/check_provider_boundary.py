#!/usr/bin/env python3
"""Inventory and validate the shared-provider module boundary.

The inventory mode is safe on the pre-refactor branch and records known coupling.
The final mode enforces the target boundary after BR07.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

SOURCE_SUFFIXES = {".cs", ".razor", ".csproj"}

EXPECTED_TABLE_NAMES = (
    "Workspace_ProviderProfiles",
    "Workspace_ProviderSharePublications",
    "Workspace_SharedProviderServiceIdentity",
    "Workspace_SharedProviderSources",
    "Workspace_SharedProviderInvocations",
    "Workspace_SharedProviderImports",
)

LEGACY_RUNTIME_DECLARATIONS = (
    "interface IProviderAdapter",
    "class ProviderRegistry",
    "class ProviderExecutionService",
    "record ProviderExecutionRequest",
    "class ProviderExecutionRequest",
    "record ProviderExecutionResponse",
    "class ProviderExecutionResponse",
    "class OpenAiProviderAdapter",
    "class OllamaProviderAdapter",
    "class ComfyUiProviderAdapter",
    "class LegacyProviderRuntimeGateway",
)

INVENTORY_PATTERNS = (
    "ProviderExecutionService",
    "IProviderRuntimeGateway",
    "IProviderAdapter",
    "ProviderRegistry",
    "WorkspaceBackedAgentProviderProfileRegistry",
    "WorkspaceAgentProviderProfileMapper",
    "SharedProvider",
    "ProviderSharePublication",
    "Workspace_ProviderProfiles",
)


@dataclass(frozen=True)
class Occurrence:
    path: str
    line: int
    text: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--mode", choices=("inventory", "final"), required=True)
    parser.add_argument("--output", type=Path)
    return parser.parse_args()


def run_git(repo: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", *args],
        cwd=repo,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        return f"ERROR: {completed.stderr.strip()}"
    return completed.stdout.strip()


def iter_source_files(root: Path) -> Iterable[Path]:
    if not root.exists():
        return []

    def walk() -> Iterable[Path]:
        for directory, child_directories, file_names in os.walk(root, onerror=lambda _: None):
            child_directories[:] = [
                name
                for name in child_directories
                if name not in {"artifacts", "bin", "obj"}
            ]
            directory_path = Path(directory)
            for file_name in file_names:
                path = directory_path / file_name
                if path.suffix in SOURCE_SUFFIXES:
                    yield path

    return walk()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def find_occurrences(repo: Path, pattern: str, roots: Iterable[Path]) -> list[Occurrence]:
    results: list[Occurrence] = []
    for root in roots:
        for path in iter_source_files(root):
            for line_number, line in enumerate(read_text(path).splitlines(), start=1):
                if pattern in line:
                    results.append(
                        Occurrence(
                            path=path.relative_to(repo).as_posix(),
                            line=line_number,
                            text=line.strip()[:240],
                        )
                    )
    return results


def add_violation(violations: list[str], condition: bool, message: str) -> None:
    if condition:
        violations.append(message)


def contains_any(path: Path, patterns: Iterable[str]) -> list[str]:
    if not path.exists():
        return []
    text = read_text(path)
    return [pattern for pattern in patterns if pattern in text]


def final_violations(repo: Path) -> list[str]:
    violations: list[str] = []

    modules = repo / "src" / "Modules"
    provider_project = modules / "CanDoItAll.Modules.AgentFramework.ProviderManagement"
    provider_csproj = provider_project / "CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj"
    workspace = modules / "CanDoItAll.Modules.Workspace"
    agent_framework = modules / "CanDoItAll.Modules.AgentFramework"
    workbench = modules / "CanDoItAll.Modules.Workbench"
    web_api = repo / "src" / "App" / "CanDoItAll.Web" / "Api"

    add_violation(violations, not provider_csproj.exists(), "ProviderManagement project is missing.")

    solution = repo / "CanDoItAll.slnx"
    add_violation(
        violations,
        provider_csproj.name not in read_text(solution),
        "ProviderManagement project is not included in the canonical solution.",
    )

    if provider_csproj.exists():
        csproj_text = read_text(provider_csproj)
        add_violation(
            violations,
            "CanDoItAll.Modules.Workspace" in csproj_text,
            "ProviderManagement project references Workspace.",
        )

    for path in iter_source_files(provider_project):
        text = read_text(path)
        if "CanDoItAll.Modules.Workspace" in text:
            violations.append(
                f"ProviderManagement source references Workspace: {path.relative_to(repo).as_posix()}"
            )

    add_violation(
        violations,
        (workspace / "SharedProviders").exists(),
        "Workspace/SharedProviders still exists.",
    )

    workspace_provider_dir = workspace / "Providers"
    allowed_workspace_provider_files = {"WorkspaceProviderCatalog.cs"}
    for path in iter_source_files(workspace_provider_dir):
        if path.name not in allowed_workspace_provider_files:
            violations.append(
                "Workspace/Providers contains provider ownership/runtime source: "
                f"{path.relative_to(repo).as_posix()}"
            )

    workspace_di = workspace / "Services" / "WorkspaceModuleServiceCollectionExtensions.cs"
    forbidden_workspace_di_tokens = (
        "IProviderAdapter",
        "ProviderRegistry",
        "ProviderExecutionService",
        "IProviderRuntimeGateway",
        "SharedProvider",
        "ProviderSharePublication",
        "AddAgentFrameworkProviderManagement",
    )
    for token in contains_any(workspace_di, forbidden_workspace_di_tokens):
        violations.append(f"Workspace DI still contains provider token: {token}")

    workspace_models = workspace / "Models" / "WorkspaceModels.cs"
    forbidden_workspace_model_tokens = (
        "class ProviderProfile",
        "ProviderProfileConfiguration",
        "ProviderProfileEditorModel",
        "SaveProviderAsync",
        "ListProviderProfilesAsync",
        "DeleteProviderAsync",
    )
    for token in contains_any(workspace_models, forbidden_workspace_model_tokens):
        violations.append(f"Workspace model/service source still owns provider behavior: {token}")

    allowed_workspace_shared_provider_files = {
        workspace / "ApiAccess" / "ApiAccessScopeNames.cs",
    }
    for path in iter_source_files(workspace):
        if path in allowed_workspace_shared_provider_files:
            continue
        text = read_text(path)
        if "SharedProvider" in text or "ProviderSharePublication" in text:
            violations.append(
                f"Workspace source still references shared-provider ownership: {path.relative_to(repo).as_posix()}"
            )

    provider_specific_af_roots = (
        agent_framework / "Providers",
        agent_framework / "Pages" / "Components",
    )
    for root in provider_specific_af_roots:
        for path in iter_source_files(root):
            text = read_text(path)
            is_provider_specific = (
                root == agent_framework / "Providers"
                or "Provider" in path.name
                or "SharedProvider" in path.name
            )
            if is_provider_specific and "CanDoItAll.Modules.Workspace" in text:
                violations.append(
                    f"Provider-specific AgentFramework source references Workspace: {path.relative_to(repo).as_posix()}"
                )

    for path in iter_source_files(web_api):
        if "SharedProvider" not in path.name:
            continue
        text = read_text(path)
        if "CanDoItAll.Modules.Workspace" in text:
            violations.append(
                f"Web provider API imports Workspace: {path.relative_to(repo).as_posix()}"
            )

    legacy_workbench_tokens = (
        "ProviderExecutionService",
        "ProviderExecutionRequest",
        "ProviderExecutionResponse",
    )
    for path in iter_source_files(workbench):
        text = read_text(path)
        for token in legacy_workbench_tokens:
            if token in text:
                violations.append(
                    f"Workbench still uses legacy provider runtime token {token}: "
                    f"{path.relative_to(repo).as_posix()}"
                )

    production_roots = (
        repo / "src" / "Modules",
        repo / "src" / "App",
        repo / "src" / "MAF",
    )
    for declaration in LEGACY_RUNTIME_DECLARATIONS:
        occurrences = find_occurrences(repo, declaration, production_roots)
        for occurrence in occurrences:
            violations.append(
                f"Legacy direct inference declaration remains ({declaration}): "
                f"{occurrence.path}:{occurrence.line}"
            )

    module_assemblies_candidates = [
        path
        for path in iter_source_files(repo / "src")
        if path.name == "ModuleAssemblies.cs"
    ]
    module_assemblies_text = "\n".join(read_text(path) for path in module_assemblies_candidates)
    add_violation(
        violations,
        "ProviderManagementModuleAssemblyMarker" not in module_assemblies_text,
        "ProviderManagement assembly marker is not registered in module discovery.",
    )

    provider_di_text = "\n".join(
        read_text(path)
        for path in iter_source_files(provider_project)
        if path.suffix == ".cs"
    ) if provider_project.exists() else ""
    add_violation(
        violations,
        "AddAgentFrameworkProviderManagement" not in provider_di_text,
        "ProviderManagement DI entry point AddAgentFrameworkProviderManagement is missing.",
    )

    production_source = "\n".join(
        read_text(path)
        for root in production_roots
        for path in iter_source_files(root)
        if path.suffix in {".cs", ".razor"}
    )
    provider_registration_count = production_source.count(
        "services.AddAgentFrameworkProviderManagement();"
    )
    add_violation(
        violations,
        provider_registration_count != 1,
        "ProviderManagement must be registered exactly once in production; "
        f"found {provider_registration_count} registrations.",
    )

    forbidden_user_facing_terms = (
        "workspace-backed provider",
        "workspace-owned provider",
        "workspace backed provider",
        "workspace owned provider",
    )
    user_facing_roots = (
        modules,
        web_api,
    )
    for root in user_facing_roots:
        for path in iter_source_files(root):
            if path.suffix not in {".cs", ".razor"}:
                continue
            text = read_text(path).lower()
            for term in forbidden_user_facing_terms:
                if term in text:
                    violations.append(
                        f"User-facing source contains forbidden provider ownership term '{term}': "
                        f"{path.relative_to(repo).as_posix()}"
                    )

    migration_root = repo / "src" / "Foundation"
    migration_text = "\n".join(
        read_text(path)
        for path in iter_source_files(migration_root)
        if path.suffix == ".cs"
    )
    for table_name in EXPECTED_TABLE_NAMES:
        add_violation(
            violations,
            table_name not in migration_text,
            f"Expected compatibility table name is not present in migration/model source: {table_name}",
        )

    forbidden_new_table_names = (
        "AgentFramework_ProviderProfiles",
        "ProviderManagement_ProviderProfiles",
        "AgentFramework_SharedProviderSources",
        "ProviderManagement_SharedProviderSources",
    )
    for table_name in forbidden_new_table_names:
        add_violation(
            violations,
            table_name in migration_text,
            f"A replacement physical table name was introduced: {table_name}",
        )

    return violations


def build_inventory(repo: Path) -> dict[str, object]:
    roots = (
        repo / "src" / "Modules",
        repo / "src" / "App",
        repo / "src" / "MAF",
        repo / "tests",
    )
    occurrences: dict[str, list[dict[str, object]]] = {}
    for pattern in INVENTORY_PATTERNS:
        found = find_occurrences(repo, pattern, roots)
        occurrences[pattern] = [
            {"path": item.path, "line": item.line, "text": item.text}
            for item in found[:500]
        ]

    return {
        "head": run_git(repo, "rev-parse", "HEAD"),
        "branch": run_git(repo, "branch", "--show-current"),
        "status": run_git(repo, "status", "--short"),
        "occurrences": occurrences,
    }


def main() -> int:
    args = parse_args()
    repo = args.repo.resolve()
    if not (repo / ".git").exists():
        print(f"Not a Git repository: {repo}", file=sys.stderr)
        return 2

    report: dict[str, object] = {
        "mode": args.mode,
        "repository": str(repo),
        "inventory": build_inventory(repo),
    }

    violations: list[str] = []
    if args.mode == "final":
        violations = final_violations(repo)
        report["violations"] = violations
        report["passed"] = not violations

    rendered = json.dumps(report, indent=2, ensure_ascii=False)
    if args.output:
        output = args.output
        if not output.is_absolute():
            output = repo / output
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered + "\n", encoding="utf-8")

    print(rendered)
    if violations:
        print("\nBoundary validation failed:", file=sys.stderr)
        for violation in violations:
            print(f"- {violation}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
