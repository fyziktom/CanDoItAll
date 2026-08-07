#!/usr/bin/env python3
"""Check source patterns that commonly indicate unsafe architecture cutovers."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


IGNORED_DIRECTORIES = {
    ".artifacts",
    "artifacts",
    ".git",
    "node_modules",
    "bin",
    "obj",
    "output",
    ".playwright-cli",
    ".playwright-mcp",
    ".mcp-state",
}


def iter_cs_files(root: Path):
    import os

    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in IGNORED_DIRECTORIES]
        for name in filenames:
            if name.endswith(".cs"):
                yield Path(dirpath) / name


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.repo_root.resolve()
    errors: list[str] = []

    source = root / "src"
    if not source.is_dir():
        print(f"Cutover guard failed:\n- Missing source directory: {source}")
        return 1

    allowed_legacy_runtime_files = {
        "LegacyAgentRuntimeCompatibilityFacade.cs",
        "IAgentRuntime.Legacy.cs",
    }

    for path in iter_cs_files(source):
        text = read(path)
        rel = path.relative_to(root).as_posix()
        if (
            re.search(r"\bIAgentRuntime\b", text)
            and "Runtime.Abstractions" not in rel
            and path.name not in allowed_legacy_runtime_files
        ):
            errors.append(f"Broad runtime reference: {rel}")
        if "TryResolveProjectId(" in text and "Workflow" in rel and "Llm" in rel:
            errors.append(f"Workflow LLM payload scope inference: {rel}")
        if "new AgentDefinition(" in text and ("Llm" in rel or "Lightweight" in rel):
            errors.append(f"Agent construction in lightweight LLM path: {rel}")
        if "new ChatSessionRecord(" in text and ("Llm" in rel or "Lightweight" in rel):
            errors.append(f"Agent session construction in lightweight LLM path: {rel}")
        if any(fragment in rel.casefold() for fragment in ("/llm/", ".llm.", "lightweightllm")):
            for token in (
                "new HttpClient(",
                "HttpClientHandler(",
                "Environment.GetEnvironmentVariable(",
                "ISecretRuntimeResolver",
            ):
                if token in text and "Provider" not in rel:
                    errors.append(
                        f"Review possible duplicated provider infrastructure in lightweight LLM path: {rel} ({token})"
                    )
        if "ILlmConversationService" in text and (
            "AgentDefinition" in text or "IAgentRuntime" in text or "IAgentExecutionRuntime" in text
        ):
            errors.append(f"Ordinary LLM conversation is coupled to agent execution: {rel}")
        if (
            "RespondToPendingApprovals" in text
            and "ContextRegistry" in text
            and re.search(r"\bCaptureAsync\s*\(", text)
        ):
            errors.append(
                f"Review possible current-context recapture during continuation: {rel}"
            )

    if errors:
        print("Cutover guard failed:")
        for error in sorted(set(errors)):
            print(f"- {error}")
        return 1
    print("Cutover guard passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
