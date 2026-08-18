from __future__ import annotations

import json
import sys
from pathlib import Path


REQUIRED_ROOT_FILES = [
    "README.md",
    "EXECUTIVE-SUMMARY-CS.md",
    "CODEX-EXECUTION-CONTRACT.md",
    "CHANGE-CONTROL.md",
    "EXECUTION-PROGRESS.md",
    "INSTALLATION.md",
    "manifest.json",
    "bundle-status.json",
    "requirements/requirements.json",
    "requirements/requirements.md",
    "analysis/findings-register.json",
    "traceability/requirements-to-subbundles.json",
]

REQUIRED_SUBBUNDLE_FILES = [
    "README.md",
    "AGENT-PROMPT.md",
    "SESSION-HANDOFF.md",
    "acceptance-evidence.md",
    "STATUS.json",
    "proof-manifest.json",
]

REQUIRED_README_SECTIONS = [
    "## Status",
    "## Proof tier",
    "## Dependency",
    "## Objective",
    "## Success criteria",
    "## Scope",
    "## Exact source anchors",
    "## Required deliverables",
    "## Entry gate",
    "## Implementation sequence",
    "## Architecture and dependency gate",
    "## Impacted-test protocol",
    "## Focused test intent",
    "## Browser/UI proof",
    "## Source and phase guards",
    "## Acceptance checklist",
    "## Do not do",
    "## Proof manifest",
    "## Progression",
]


def load_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        raise ValueError(f"Cannot read JSON {path}: {exc}") from exc


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    errors: list[str] = []

    for rel in REQUIRED_ROOT_FILES:
        path = root / rel
        if not path.is_file():
            errors.append(f"Missing required file: {rel}")
        elif path.stat().st_size == 0:
            errors.append(f"Required file is empty: {rel}")

    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1

    manifest = load_json(root / "manifest.json")
    status = load_json(root / "bundle-status.json")

    if manifest.get("stage") != "prepared":
        errors.append("manifest.json stage must be 'prepared'.")
    if status.get("stage") != "prepared":
        errors.append("bundle-status.json stage must be 'prepared'.")
    if status.get("executionState") != "not-started":
        errors.append("Prepared bundle executionState must be 'not-started'.")
    if manifest.get("terminalExecutionState") != "awaiting-user-agent-chat-regression":
        errors.append("Unexpected terminal execution state.")
    if status.get("simpleChatUiActivationAllowed") is not False:
        errors.append("Simple Chat UI activation must be false.")

    subbundles = manifest.get("subbundles")
    if not isinstance(subbundles, list) or len(subbundles) != 9:
        errors.append("Manifest must declare exactly 9 subbundles.")
        subbundles = []

    expected_ids = [f"SB{i:02d}" for i in range(1, 10)]
    actual_ids = [item.get("id") for item in subbundles]
    if actual_ids != expected_ids:
        errors.append(f"Subbundle order must be {expected_ids}, got {actual_ids}.")

    seen_dirs: set[Path] = set()
    for item in subbundles:
        sb_id = item.get("id")
        slug = item.get("slug")
        if not isinstance(sb_id, str) or not isinstance(slug, str):
            errors.append(f"Invalid subbundle entry: {item!r}")
            continue

        folder = root / "subbundles" / f"{sb_id}-{slug}"
        seen_dirs.add(folder)
        if not folder.is_dir():
            errors.append(f"Missing subbundle directory: {folder.relative_to(root)}")
            continue

        for rel in REQUIRED_SUBBUNDLE_FILES:
            path = folder / rel
            if not path.is_file():
                errors.append(f"Missing {folder.relative_to(root)}/{rel}")
            elif path.stat().st_size == 0:
                errors.append(f"Empty {folder.relative_to(root)}/{rel}")

        readme = (folder / "README.md").read_text(encoding="utf-8")
        for section in REQUIRED_README_SECTIONS:
            if section not in readme:
                errors.append(f"{folder.relative_to(root)}/README.md lacks section {section!r}")

        sb_status = load_json(folder / "STATUS.json")
        if sb_status.get("subbundleId") != sb_id:
            errors.append(f"{folder.relative_to(root)}/STATUS.json has wrong id.")
        if sb_status.get("status") != "pending":
            errors.append(f"{folder.relative_to(root)}/STATUS.json must be pending.")
        if sb_status.get("ownedRequirements") != item.get("requirements"):
            errors.append(f"{folder.relative_to(root)}/STATUS.json requirements differ from manifest.")

        proof = load_json(folder / "proof-manifest.json")
        if proof.get("subbundleId") != sb_id:
            errors.append(f"{folder.relative_to(root)}/proof-manifest.json has wrong id.")
        if proof.get("status") != "pending":
            errors.append(f"{folder.relative_to(root)}/proof-manifest.json must be pending.")

    actual_dirs = {
        path
        for path in (root / "subbundles").iterdir()
        if path.is_dir()
    }
    unexpected_dirs = actual_dirs - seen_dirs
    if unexpected_dirs:
        errors.append(
            "Unexpected subbundle directories: "
            + ", ".join(str(path.relative_to(root)) for path in sorted(unexpected_dirs))
        )

    for path in root.rglob("*"):
        if path.is_dir() and path.name == "__pycache__":
            errors.append(f"Compiled cache directory must not be bundled: {path.relative_to(root)}")
        if path.is_file() and path.suffix == ".pyc":
            errors.append(f"Compiled Python file must not be bundled: {path.relative_to(root)}")

    if manifest.get("requirementsCount") != len(load_json(root / "requirements/requirements.json").get("requirements", [])):
        errors.append("requirementsCount does not match requirements.json.")
    if manifest.get("findingsCount") != len(load_json(root / "analysis/findings-register.json").get("findings", [])):
        errors.append("findingsCount does not match findings-register.json.")

    readme = (root / "README.md").read_text(encoding="utf-8")
    required_phrases = [
        "does not implement Simple Chat UI",
        "awaiting-user-agent-chat-regression",
        "code_analytics_impacted_tests_get",
        "behaviorIntent=Unknown",
        "at most once",
    ]
    for phrase in required_phrases:
        if phrase not in readme:
            errors.append(f"Root README lacks required phrase: {phrase!r}")

    if errors:
        print("Bundle validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(
        f"Bundle validation passed: {len(subbundles)} subbundles, "
        f"{manifest.get('requirementsCount')} requirements, "
        f"{manifest.get('findingsCount')} findings, stage={manifest.get('stage')}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
