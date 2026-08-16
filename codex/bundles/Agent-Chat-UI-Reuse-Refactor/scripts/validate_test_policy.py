from __future__ import annotations

import re
import sys
from pathlib import Path


FORBIDDEN_COMMAND_PATTERNS = [
    re.compile(r"^\s*(?:>\s*)?dotnet\s+test\s+CanDoItAll(?:\.sln|\.slnx)?\s*$", re.IGNORECASE),
    re.compile(r"^\s*(?:>\s*)?dotnet\s+test\s+tests/Solutions/CanDoItAll\.Tests\.Stable\.slnx\s*$", re.IGNORECASE),
    re.compile(r"^\s*(?:>\s*)?dotnet\s+test\s+tests/Solutions/CanDoItAll\.Tests\.Components\.slnx\s*$", re.IGNORECASE),
    re.compile(r"^\s*(?:>\s*)?dotnet\s+test\s+tests/Solutions/CanDoItAll\.Tests\.Playwright\.slnx\s*$", re.IGNORECASE),
]

REQUIRED_IMPACT_PHRASES = [
    "code_analytics_impacted_tests_get",
    "behaviorIntent=Unknown",
    "contextOnlyPaths",
    "nonzero",
    "required selector",
]


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    errors: list[str] = []

    for path in root.rglob("*.md"):
        relative = path.relative_to(root)
        text = path.read_text(encoding="utf-8")
        for line_no, line in enumerate(text.splitlines(), start=1):
            for pattern in FORBIDDEN_COMMAND_PATTERNS:
                if pattern.match(line):
                    errors.append(
                        f"Forbidden habitual broad test command at {relative}:{line_no}: {line.strip()}"
                    )

    manifest_text = (root / "manifest.json").read_text(encoding="utf-8")
    if '"SB09"' not in manifest_text:
        errors.append("SB09 final proof stage is missing.")

    for folder in sorted((root / "subbundles").iterdir()):
        if not folder.is_dir():
            continue
        sb_id = folder.name.split("-", 1)[0]
        if sb_id == "SB01":
            continue
        readme = (folder / "README.md").read_text(encoding="utf-8")
        for phrase in REQUIRED_IMPACT_PHRASES:
            if phrase not in readme:
                errors.append(f"{folder.name}/README.md lacks impacted-test rule {phrase!r}.")

    protocol = (root / "shared-prompts/01-impacted-test-protocol.md").read_text(encoding="utf-8")
    protocol_phrases = [
        "one-based inclusive changed line ranges",
        "behaviorIntent=Unknown",
        "BehaviorPreservingImplementation",
        "AllSuppliedSuites",
        "zero or unexpected discovery",
        "promotion",
    ]
    for phrase in protocol_phrases:
        if phrase not in protocol:
            errors.append(f"Impacted-test protocol lacks {phrase!r}.")

    budget = (root / "plan/03-test-budget-and-gates.md").read_text(encoding="utf-8")
    if "at most once" not in budget or "SB09" not in budget or "trigger" not in budget:
        errors.append("Test budget does not constrain the broad gate to one triggered SB09 run.")

    if errors:
        print("Test-policy validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print("Test-policy validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
