#!/usr/bin/env python3
"""Check Simple Chat project and source dependency boundaries."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


FORBIDDEN_PROJECT_REFERENCE_MARKERS = (
    "CanDoItAll.Web",
    "CanDoItAll.AppComponents",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Components",
    "CanDoItAll.AgentFramework.Maf",
    "CanDoItAll.AgentFramework.Tools",
    "CanDoItAll.AgentFramework.Tooling",
    "CanDoItAll.AgentFramework.Skills",
    "CanDoItAll.AgentFramework.Mcp",
    "CanDoItAll.AgentFramework.Memory",
    "CanDoItAll.Modules.AgentFramework",
    "CanDoItAll.Modules.Workbench",
    "CanDoItAll.Modules.Projects",
    "CanDoItAll.Modules.Processes",
)

FORBIDDEN_CORE_USINGS = (
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore",
    "Microsoft.AspNetCore.Components",
    "CanDoItAll.Web",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Maf",
    "CanDoItAll.AgentFramework.Tools",
    "CanDoItAll.AgentFramework.Skills",
    "CanDoItAll.AgentFramework.Mcp",
    "CanDoItAll.AgentFramework.Memory",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.repo_root.resolve()
    product_root = root / "src/Modules/CanDoItAll.Modules.LlmChats"
    persistence_root = root / "src/Modules/CanDoItAll.Modules.LlmChats.Persistence"
    errors: list[str] = []

    if not product_root.is_dir() or not persistence_root.is_dir():
        print("ERROR: Simple Chat projects were not found.", file=sys.stderr)
        return 1

    product_project = product_root / "CanDoItAll.Modules.LlmChats.csproj"
    project_text = product_project.read_text(encoding="utf-8", errors="replace")
    for marker in FORBIDDEN_PROJECT_REFERENCE_MARKERS:
        if marker in project_text:
            errors.append(
                f"Product project has forbidden reference marker {marker}"
            )

    for source in product_root.rglob("*.cs"):
        text = source.read_text(encoding="utf-8", errors="replace")
        relative = source.relative_to(root)
        for marker in FORBIDDEN_CORE_USINGS:
            if marker in text:
                errors.append(f"{relative} contains forbidden dependency {marker}")
        if "IServiceProvider" in text:
            errors.append(f"{relative} uses IServiceProvider in product behavior")

    target_services = (
        "LlmChatOperationApplicationService",
        "LlmChatConversationApplicationService",
    )
    for type_name in target_services:
        declarations = []
        for source in product_root.rglob("*.cs"):
            text = source.read_text(encoding="utf-8", errors="replace")
            if re.search(
                rf"\bpartial\s+class\s+{re.escape(type_name)}\b", text
            ):
                declarations.append(str(source.relative_to(root)))
        if declarations:
            errors.append(
                f"{type_name} uses partial-class expansion: {declarations}"
            )

    for source in persistence_root.rglob("*.cs"):
        text = source.read_text(encoding="utf-8", errors="replace")
        relative = source.relative_to(root)
        if "CanDoItAll.Web" in text or "Microsoft.AspNetCore.Components" in text:
            errors.append(f"{relative} leaks Web/UI into persistence")

    provider_roots = (
        root / "src/MAF/Common/CanDoItAll.AgentFramework.Providers",
        root / "src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions",
    )
    for provider_root in provider_roots:
        if not provider_root.is_dir():
            continue
        for source in provider_root.rglob("*.cs"):
            text = source.read_text(encoding="utf-8", errors="replace")
            if "CanDoItAll.Modules.LlmChats" in text:
                errors.append(
                    f"{source.relative_to(root)} depends on Simple Chat product"
                )

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1

    print("Architecture boundary check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
