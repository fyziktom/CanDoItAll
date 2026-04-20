#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate AgentFramework bundle closure truthfulness and proof discipline."
    )
    parser.add_argument("bundle_root", type=Path, help="Path to agentframework-full-integration")
    parser.add_argument(
        "--agentframework-root",
        type=Path,
        default=Path("src/CanDoItAll.Modules.AgentFramework"),
        help="Path to the local AgentFramework module source root.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    bundle_root = args.bundle_root.resolve()
    repo_root = bundle_root.parent.resolve()
    agentframework_root = (
        args.agentframework_root.resolve()
        if args.agentframework_root.is_absolute()
        else (repo_root / args.agentframework_root).resolve()
    )

    readme_path = bundle_root / "README.md"
    report_path = bundle_root / "reviews" / "01-execution-report.md"
    browser_logs_root = bundle_root / "reviews" / "browser-logs"

    failures: list[str] = []

    if not readme_path.exists():
        failures.append(f"Missing bundle README: {readme_path}")
    if not report_path.exists():
        failures.append(f"Missing execution report: {report_path}")
    if failures:
        print_failures(failures)
        return 1

    readme_text = readme_path.read_text(encoding="utf-8")
    report_text = report_path.read_text(encoding="utf-8")
    combined_text = f"{readme_text}\n{report_text}"
    execution_state = resolve_execution_state(report_text)
    completion_claimed = execution_state == "Completed" or bool(
        re.search(r"Execution status:\s*`Completed`", readme_text, re.IGNORECASE)
    )

    if not completion_claimed:
        if "reopened" not in combined_text.lower():
            failures.append("Bundle docs do not state that the initiative was reopened after audit.")
        if "premature" not in combined_text.lower():
            failures.append("Bundle docs do not state that the earlier completion claim was premature.")

    closed_subbundles = resolve_closed_subbundle_ids(report_text)
    if not closed_subbundles:
        failures.append("Execution report does not list any closed subbundles.")

    for subbundle_id in closed_subbundles:
        matching_logs = sorted(browser_logs_root.glob(f"sb{subbundle_id}-*.md"))
        if not matching_logs:
            failures.append(
                f"Closed subbundle {subbundle_id} is missing a browser proof log under {browser_logs_root}."
            )
            continue

        for log_path in matching_logs:
            log_text = log_path.read_text(encoding="utf-8")
            for required_field in (
                "Timestamp:",
                "Route:",
                "Viewport:",
                "Steps executed",
                "Observed result",
                "Screenshot review",
                "Automated proof surface:",
            ):
                if required_field not in log_text:
                    failures.append(f"{log_path} is missing required field '{required_field}'.")

            artifact_matches = set(re.findall(r"reviews/artifacts/[A-Za-z0-9._/-]+\.png", log_text))
            if not artifact_matches:
                failures.append(f"{log_path} does not reference any bundle screenshot artifacts.")

            for relative_artifact in artifact_matches:
                artifact_path = bundle_root / relative_artifact
                if not artifact_path.exists():
                    failures.append(f"{log_path} references missing artifact {artifact_path}.")

    if completion_claimed:
        if re.search(r"Pending implementation|To be filled", combined_text, re.IGNORECASE):
            failures.append(
                "Completion is claimed while the bundle still contains 'Pending implementation' or 'To be filled'."
            )

        placeholder_patterns = (
            r"Integrated agent module foundation",
            r"Planned imports",
            r"Later subbundles",
            r"future integrated surfaces",
            r"deferred",
        )
        source_text = read_agentframework_source(agentframework_root)
        for pattern in placeholder_patterns:
            if re.search(pattern, source_text, re.IGNORECASE):
                failures.append(
                    f"Completion is claimed while AgentFramework source still contains placeholder text matching '{pattern}'."
                )

    if failures:
        print_failures(failures)
        return 1

    print("AgentFramework audit closure check passed.")
    print(f"- Execution state: {execution_state}")
    print(f"- Closed subbundles with logs: {', '.join(closed_subbundles)}")
    return 0


def resolve_execution_state(report_text: str) -> str:
    match = re.search(r"Execution state:\s*`([^`]+)`", report_text, re.IGNORECASE)
    if match:
        return match.group(1).strip()
    return "Unknown"


def resolve_closed_subbundle_ids(report_text: str) -> list[str]:
    in_table = False
    closed_ids: list[str] = []
    for raw_line in report_text.splitlines():
        line = raw_line.strip()
        if line == "## Subbundle Gate Results":
            in_table = True
            continue
        if in_table and line.startswith("## "):
            break
        if not in_table or not line.startswith("|"):
            continue
        if "Subbundle" in line or line.startswith("| ---"):
            continue

        cells = [cell.strip().strip("`") for cell in line.strip("|").split("|")]
        if len(cells) < 3:
            continue

        subbundle_name = cells[0]
        closure_gate = cells[2]
        if closure_gate != "Closed":
            continue

        match = re.match(r"(?P<id>\d{2})-", subbundle_name)
        if match:
            closed_ids.append(match.group("id"))

    return closed_ids


def read_agentframework_source(agentframework_root: Path) -> str:
    parts: list[str] = []
    if not agentframework_root.exists():
        return ""

    for path in sorted(agentframework_root.rglob("*")):
        if not path.is_file():
            continue
        if any(segment in {"bin", "obj"} for segment in path.parts):
            continue
        if path.suffix.lower() not in {".cs", ".razor", ".md"}:
            continue
        parts.append(path.read_text(encoding="utf-8"))

    return "\n".join(parts)


def print_failures(failures: list[str]) -> None:
    print("AgentFramework audit closure check failed:")
    for failure in failures:
        print(f"- {failure}")


if __name__ == "__main__":
    sys.exit(main())
