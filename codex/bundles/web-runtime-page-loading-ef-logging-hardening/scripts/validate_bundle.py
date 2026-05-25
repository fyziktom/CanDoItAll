#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

COMMON_DIRECTORIES = [
    "inputs",
    "analysis",
    "requirements",
    "architecture",
    "plan",
    "traceability",
    "shared-prompts",
    "subbundles",
    "reviews",
]

PROFILE_DIRECTORIES = {
    "feedback": [],
    "initiative": ["inventories", "templates"],
}

REQUIRED_FILES = [
    "README.md",
    "inputs/00-original-request.md",
    "inputs/01-source-artifacts.md",
    "inputs/02-structured-input.md",
    "analysis/01-current-state.md",
    "requirements/01-normalized-requirements.md",
    "architecture/01-target-solution.md",
    "plan/01-phase-plan.md",
    "traceability/01-requirement-traceability.md",
    "shared-prompts/implementation-prompt.md",
    "shared-prompts/qa-prompt.md",
    "reviews/00-bundle-self-review.md",
    "reviews/01-execution-report.md",
]

ROOT_SUMMARY_LABEL_GROUPS = [
    ("Bundle preparation status:", "Bundle readiness gate:"),
    ("Execution status:",),
    ("Subbundle gate review:",),
    ("Final closure gate:",),
    ("Browser validation analytics:",),
]

PHASE_PLAN_HEADING_GROUPS = [
    ("## Execution Order", "## Phase Sequence"),
    ("## Subbundle Dependency Map",),
    ("## Critical Subbundles",),
    ("## Phase Gates",),
]

ASSUMPTIONS_AND_RISKS_HEADING_GROUPS = [
    ("## Working Assumptions", "## Assumptions"),
    ("## Critical Path Risks",),
    ("## Validation Risks",),
    ("## Reopen Triggers",),
]

SUBBUNDLE_HEADING_GROUPS = [
    ("## Status",),
    ("## Objective",),
    ("## Covered Inputs", "## Covered Notes"),
    ("## Prerequisites",),
    ("## Exact Source References",),
    ("## Deliverables", "## Scope"),
    ("## Dependency Impact",),
    ("## Validation Depth",),
    ("## Implementation Steps",),
    ("## Do Not Do",),
    ("## Acceptance Checklist",),
    ("## Proof Required",),
    ("## Browser Validation Logging",),
    ("## Progression Gate",),
    ("## Suggested Agent Prompt",),
]

SUBBUNDLE_REQUIRED_BULLET_GROUPS = [
    ("## Covered Inputs", "## Covered Notes"),
    ("## Prerequisites",),
    ("## Deliverables", "## Scope"),
    ("## Dependency Impact",),
    ("## Validation Depth",),
    ("## Acceptance Checklist",),
    ("## Proof Required",),
    ("## Browser Validation Logging",),
    ("## Progression Gate",),
]

EXECUTION_REPORT_HEADINGS = [
    "## Status",
    "## Subbundle Gate Results",
    "## Browser Validation Analytics",
    "## Analytics Review",
    "## Raw Note Closure",
]

FINAL_ALLOWED_SUBBUNDLE_STATUSES = {
    "Completed",
    "Blocked",
}

PENDING_VALUES = {
    "Draft",
    "In progress",
    "Not started",
    "Pending",
    "Pending implementation",
    "Ready",
}

ROOT_PREPARED_FORBIDDEN_LINES = (
    "Bundle preparation status: `Draft`",
    "Bundle readiness gate: `Not run`",
)

ROOT_COMPLETED_FORBIDDEN_LINES = (
    "Bundle preparation status: `Draft`",
    "Bundle readiness gate: `Not run`",
    "Execution status: `Not started`",
    "Subbundle gate review: `Not started`",
    "Final closure gate: `Not started`",
    "Final closure gate: `Not run`",
    "Browser validation analytics: `Not started`",
)

SUBBUNDLE_GATE_RESULTS_HEADER = "| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |"
BROWSER_ANALYTICS_HEADER = "| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |"
RAW_NOTE_CLOSURE_HEADER = "| Raw note | Status | Proof |"

SEMANTIC_PROOF_LABELS = [
    "Raw note owned",
    "Shipped behavior",
    "Source proof",
    "Test proof",
    "Shallow-pass trap",
    "Adversarial negative proof",
    "Semantic positive proof",
    "Anti-stub audit",
]

WEAK_PROOF_VALUES = {
    "",
    "n/a",
    "none",
    "done",
    "completed",
    "passed",
    "fixed",
    "see above",
    "see report",
    "manual",
    "tested",
}

PROOF_TOKEN_PATTERN = re.compile(
    r"(`[^`]+`|[A-Za-z]:[\\/]|[/\\]|\.md\b|\.cs\b|\.py\b|dotnet\b|python\b|"
    r"Select-String\b|Copy-Item\b|test\b|command\b|proof\b|SB\d{2}\b)",
    re.IGNORECASE,
)

SHA256_PATTERN = re.compile(r"\b[A-Fa-f0-9]{64}\b")
WINDOWS_ABSOLUTE_PATTERN = re.compile(r"^[A-Za-z]:[\\/]")
POSIX_ABSOLUTE_PATTERN = re.compile(r"^/")
ARTIFACT_PATH_PATTERN = re.compile(
    r"(?<![A-Za-z0-9_])(?:repo://[^`<>()\s|]+|bundle://[^`<>()\s|]+|[A-Za-z]:[\\/][^`<>()\s|]+|/[^`<>()\s|]+|proof[\\/][^`<>()\s|]+)",
    re.IGNORECASE,
)
PORTABLE_REFERENCE_PATTERN = re.compile(r"^(repo|bundle)://(.+)$", re.IGNORECASE)
COMMAND_FIELD_PATTERN = re.compile(r'["\']?Command["\']?\s*:', re.IGNORECASE)
EXIT_CODE_PATTERN = re.compile(r'["\']?Exit(?:Code| code)["\']?\s*:\s*(-?\d+)\b', re.IGNORECASE)
TEST_NAME_PATTERN = re.compile(r"^\s*-\s*Test name\s*:\s*`?([^`]+?)`?\s*$", re.IGNORECASE | re.MULTILINE)
INVARIANT_ID_PATTERN = re.compile(r"^\s*-\s*Invariant ID\s*:\s*`?([^`\r\n]+?)`?\s*$", re.IGNORECASE | re.MULTILINE)
SEMANTIC_INVARIANT_REQUIRED_LABELS = [
    "Invariant ID",
    "Source raw note",
    "Expected behavior",
    "Disallowed shallow implementation",
    "Failing-first test",
    "Passing test",
    "Changed source files",
    "Production assertions",
    "Red-team negative case",
    "Downstream dependency check",
]

PRODUCTION_ARTIFACT_KIND_PATTERN = re.compile(
    r"\b(?:new|added|introduce[sd]?|production-only|domain|lifecycle)\s+"
    r"(?:signal|state|record|event)\b|\b(?:SignalKind|SourceKind)\b",
    re.IGNORECASE,
)
PRODUCTION_ARTIFACT_IDENTIFIER_PATTERN = re.compile(
    r"\b[A-Z][A-Za-z0-9]*(?:Signal|State|Record|Event|AcceptedUse|LifecycleTransition)\b"
)
PRODUCTION_ARTIFACT_MATRIX_HEADING = "## Production Behavior Artifact Matrix"
PRODUCTION_ARTIFACT_MATRIX_COLUMNS = ("artifact", "producer", "consumer", "lifecycle", "negative")
PRODUCTION_ARTIFACT_MATRIX_PROOF_COLUMNS = ("producer", "consumer", "lifecycle", "negative")
DREAM_META_TEXT_PATTERN = re.compile(
    r"\bConclusion:\s*[^`\r\n|]*\bsupported by\s+(?:N|\d+)\s+source-backed observation",
    re.IGNORECASE,
)
DREAM_META_TEXT_PROHIBITED_LABELS = {
    "expected behavior",
    "shipped behavior",
    "semantic positive proof",
    "production assertions",
}
PROOF_CLAIM_TO_CODE_MATRIX_HEADING = "## Proof Claim To Code Matrix"
PROOF_CLAIM_TO_CODE_MATRIX_COLUMNS = (
    "capability claim",
    "required production source proof",
    "required test proof",
    "required negative fixture",
    "result",
)
MACHINE_SPECIFIC_PATH_ALLOW_MARKERS = (
    "non-artifact working-directory context",
    "non-artifact local context",
    "local context only",
    "working directory context only",
)
CAPABILITY_CLAIM_PATTERNS: dict[str, tuple[re.Pattern[str], ...]] = {
    "embedding-backed": (
        re.compile(r"\bembedding[- ]backed\b", re.IGNORECASE),
        re.compile(r"\bembedding/ranker\b", re.IGNORECASE),
    ),
    "Czech/diacritic": (
        re.compile(r"\bczech/diacritic\b", re.IGNORECASE),
        re.compile(r"\bczech\b[^.\n|]{0,80}\bdiacritic\b", re.IGNORECASE),
        re.compile(r"\bdiacritic\b[^.\n|]{0,80}\bczech\b", re.IGNORECASE),
    ),
    "provider-backed": (
        re.compile(r"\bprovider[- ]backed\b", re.IGNORECASE),
    ),
    "automatic": (
        re.compile(r"\bautomatic accepted[- ]use\b", re.IGNORECASE),
        re.compile(r"\bautomatic outcome\b", re.IGNORECASE),
    ),
    "scheduled": (
        re.compile(r"\bscheduled assimilation\b", re.IGNORECASE),
        re.compile(r"\bscheduled maintenance\b", re.IGNORECASE),
    ),
    "claim-specific": (
        re.compile(r"\bclaim[- ]specific\b", re.IGNORECASE),
    ),
    "line-level": (
        re.compile(r"\bline[- ]level\b", re.IGNORECASE),
        re.compile(r"\bstatement[- ]level lineage\b", re.IGNORECASE),
    ),
    "domain synthesis": (
        re.compile(r"\bdomain synthesis\b", re.IGNORECASE),
        re.compile(r"\bdomain[- ]useful\b", re.IGNORECASE),
    ),
    "portable proof": (
        re.compile(r"\bportable proof\b", re.IGNORECASE),
    ),
}
CAPABILITY_SOURCE_REQUIREMENTS: dict[str, tuple[tuple[str, tuple[str, ...]], ...]] = {
    "embedding-backed": (
        ("embedding provider call", ("ICognitiveMemoryEmbeddingProvider", "IEmbeddingProvider", "EmbedAsync", "EmbeddingProvider")),
        ("vector or ranker scoring", ("Vector", "Cosine", "Ranker", "SemanticSimilarity")),
        ("honest lexical fallback", ("Lexical", "Fallback")),
    ),
    "Czech/diacritic": (
        ("Czech signal model", ("Czech", "Cesky", "Cestina", "cs")),
        ("diacritic folding", ("Diacritic", "RemoveDiacritics", "NonSpacingMark", "FormD")),
        ("original text preservation", ("Original", "SourceUtterance", "Preserve")),
    ),
    "provider-backed": (
        ("provider abstraction", ("Provider", "I", "interface")),
        ("provider call or injection", ("GetRequiredService", "AddScoped", "AddSingleton", "Inject", "Async")),
    ),
    "automatic": (
        ("outcome or feedback event", ("Outcome", "Feedback", "Event", "Handler")),
        ("producer invokes workflow path", ("Handle", "Publish", "Emit", "AcceptedUse")),
    ),
    "scheduled": (
        ("scheduler lifecycle", ("Scheduled", "Scheduler", "Maintenance", "Runner")),
        ("assimilation scan invocation", ("ScanAssimilation", "Assimilation")),
    ),
    "claim-specific": (
        ("claim evidence link", ("CognitiveMemoryClaimEvidenceLinkRecord", "ClaimEvidence", "ClaimId")),
        ("unrelated evidence exclusion", ("Exclude", "Filter", "EvidenceAnchorId")),
    ),
    "line-level": (
        ("statement lineage", ("Statement", "Lineage", "SourceMap")),
        ("on-demand reference support", ("Reference", "Resolve", "Source")),
    ),
    "domain synthesis": (
        ("domain claim construction", ("Domain", "Canonical", "Subject", "Predicate", "Object")),
        ("diagnostics separated from text", ("Diagnostic", "Metadata", "Source")),
    ),
    "portable proof": (
        ("portable references", ("repo://", "bundle://")),
        ("moved checkout validation", ("moved", "checkout", "copy")),
    ),
}


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate a CanDoItAll bundle structure.")
    parser.add_argument("bundle_path", help="Path to the bundle root.")
    parser.add_argument("--profile", choices=("feedback", "initiative"), default="feedback")
    parser.add_argument("--stage", choices=("prepared", "completed"), default="prepared")
    parser.add_argument("--repo-root", help="Repository root used to resolve repo:// references.")
    parser.add_argument("--bundle-root", help="Bundle root used to resolve bundle:// references. Defaults to bundle_path.")
    return parser.parse_args()


def collect_missing_paths(bundle_path: Path, profile: str) -> list[str]:
    missing: list[str] = []

    for directory in [*COMMON_DIRECTORIES, *PROFILE_DIRECTORIES[profile]]:
        if not (bundle_path / directory).is_dir():
            missing.append(directory)

    for relative_file in REQUIRED_FILES:
        if not (bundle_path / relative_file).is_file():
            missing.append(relative_file)

    return missing


def find_present_heading(content: str, heading_group: tuple[str, ...]) -> str | None:
    for heading in heading_group:
        if heading in content:
            return heading

    return None


def validate_heading_groups(path: Path, content: str, heading_groups: list[tuple[str, ...]]) -> list[str]:
    issues: list[str] = []

    for heading_group in heading_groups:
        if find_present_heading(content, heading_group) is not None:
            continue

        issues.append(f"{path}: missing one of {', '.join(heading_group)}")

    return issues


def extract_markdown_section(content: str, heading: str) -> str | None:
    lines = content.splitlines()
    start_index: int | None = None

    for index, line in enumerate(lines):
        if line.strip() == heading:
            start_index = index + 1
            break

    if start_index is None:
        return None

    end_index = len(lines)
    for index in range(start_index, len(lines)):
        if lines[index].startswith("## "):
            end_index = index
            break

    return "\n".join(lines[start_index:end_index])


def normalize_markdown_value(value: str) -> str:
    exact_match = re.fullmatch(r"`([^`]+)`", value.strip())
    if exact_match is not None:
        return exact_match.group(1).strip()

    return value.strip()


def discover_repo_root(start: Path) -> Path:
    for candidate in (start, *start.parents):
        if (candidate / ".git").exists():
            return candidate

        if (candidate / "src").is_dir() and (candidate / "codex").is_dir():
            return candidate

    return Path.cwd().resolve()


def is_portable_reference(path_value: str) -> bool:
    return PORTABLE_REFERENCE_PATTERN.match(path_value.strip()) is not None


def has_portable_reference(content: str) -> bool:
    return "repo://" in content.lower() or "bundle://" in content.lower()


def is_absolute_reference_path(path_value: str) -> bool:
    return WINDOWS_ABSOLUTE_PATTERN.match(path_value) is not None or POSIX_ABSOLUTE_PATTERN.match(path_value) is not None


def resolve_under_root(root: Path, relative_value: str) -> Path:
    normalized = relative_value.replace("\\", "/").lstrip("/")
    if not normalized or any(part in {"", ".", ".."} for part in normalized.split("/")):
        raise ValueError(f"portable reference escapes its root: {relative_value}")

    resolved_root = root.resolve()
    resolved_path = (resolved_root / normalized).resolve()
    if resolved_path != resolved_root and resolved_root not in resolved_path.parents:
        raise ValueError(f"portable reference escapes its root: {relative_value}")

    return resolved_path


def resolve_reference_path(path_value: str, bundle_root: Path, repo_root: Path) -> Path:
    normalized = normalize_artifact_path_token(path_value)
    match = PORTABLE_REFERENCE_PATTERN.match(normalized)
    if match is not None:
        root = repo_root if match.group(1).lower() == "repo" else bundle_root
        return resolve_under_root(root, match.group(2))

    if is_absolute_reference_path(normalized):
        return Path(normalized)

    return bundle_root / normalized


def extract_bullet_values(section_content: str) -> list[str]:
    values: list[str] = []
    for line in section_content.splitlines():
        stripped = line.strip()
        if not stripped.startswith("- "):
            continue

        values.append(normalize_markdown_value(stripped[2:].strip()))

    return values


def validate_required_bullets_for_group(path: Path, content: str, heading_group: tuple[str, ...]) -> list[str]:
    heading = find_present_heading(content, heading_group)
    if heading is None:
        return []

    section_content = extract_markdown_section(content, heading)
    if section_content is None:
        return []

    if extract_bullet_values(section_content):
        return []

    return [f"{path}: {heading} must include at least one markdown bullet"]


def validate_exact_source_references(path: Path, content: str, bundle_root: Path, repo_root: Path) -> list[str]:
    section_content = extract_markdown_section(content, "## Exact Source References")
    if section_content is None:
        return []

    references = extract_bullet_values(section_content)
    if not references:
        return [f"{path}: ## Exact Source References must include at least one markdown bullet path"]

    issues: list[str] = []
    for reference in references:
        normalized = normalize_artifact_path_token(reference)
        if not is_portable_reference(normalized) and not is_absolute_reference_path(normalized):
            issues.append(f"{path}: source reference is not absolute or portable: {reference}")
            continue

        try:
            reference_path = resolve_reference_path(normalized, bundle_root, repo_root)
        except ValueError as exception:
            issues.append(f"{path}: {exception}")
            continue

        if not reference_path.exists():
            issues.append(f"{path}: source reference does not exist: {reference}")

    return issues


def contains_pending_marker(value: str | None) -> bool:
    if value is None:
        return False

    normalized = normalize_markdown_value(value)
    return normalized in PENDING_VALUES


def validate_root_readme(path: Path, stage: str) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    if "## Validation Summary" not in content:
        issues.append(f"{path}: missing required heading ## Validation Summary")
        return issues

    validation_summary = extract_markdown_section(content, "## Validation Summary")
    if validation_summary is None:
        return issues

    for label_group in ROOT_SUMMARY_LABEL_GROUPS:
        if any(label in validation_summary for label in label_group):
            continue

        issues.append(f"{path}: ## Validation Summary must include one of {', '.join(label_group)}")

    forbidden_lines = ROOT_PREPARED_FORBIDDEN_LINES if stage == "prepared" else ROOT_COMPLETED_FORBIDDEN_LINES
    for forbidden_line in forbidden_lines:
        if forbidden_line in content:
            issues.append(f"{path}: {stage}-stage validation does not allow '{forbidden_line}'")

    return issues


def validate_subbundle_readme(path: Path, stage: str, bundle_root: Path, repo_root: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues = validate_heading_groups(path, content, SUBBUNDLE_HEADING_GROUPS)

    issues.extend(validate_exact_source_references(path, content, bundle_root, repo_root))

    for heading_group in SUBBUNDLE_REQUIRED_BULLET_GROUPS:
        issues.extend(validate_required_bullets_for_group(path, content, heading_group))

    if stage == "completed":
        status = extract_first_status_value(content)
        if status in {"Ready", "In progress"}:
            issues.append(f"{path}: completed-stage validation does not allow subbundle status `{status}`")

    return issues


def validate_execution_report(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    for heading in EXECUTION_REPORT_HEADINGS:
        if heading not in content:
            issues.append(f"{path}: missing required heading {heading}")

    gate_section = extract_markdown_section(content, "## Subbundle Gate Results")
    if gate_section is not None and SUBBUNDLE_GATE_RESULTS_HEADER not in gate_section:
        issues.append(f"{path}: ## Subbundle Gate Results must include the '{SUBBUNDLE_GATE_RESULTS_HEADER}' table header")

    browser_section = extract_markdown_section(content, "## Browser Validation Analytics")
    if browser_section is not None and BROWSER_ANALYTICS_HEADER not in browser_section:
        issues.append(f"{path}: ## Browser Validation Analytics must include the '{BROWSER_ANALYTICS_HEADER}' table header")

    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is not None and RAW_NOTE_CLOSURE_HEADER not in raw_note_section:
        issues.append(f"{path}: ## Raw Note Closure must include the '{RAW_NOTE_CLOSURE_HEADER}' table header")

    return issues


def extract_table_rows(section_content: str) -> list[list[str]]:
    rows: list[list[str]] = []

    for line in section_content.splitlines():
        stripped = line.strip()
        if not stripped.startswith("|"):
            continue

        columns = [column.strip() for column in stripped.strip("|").split("|")]
        if not columns:
            continue

        if all(re.fullmatch(r"[:\- ]+", column) for column in columns):
            continue

        rows.append(columns)

    return rows


def data_table_rows(section_content: str) -> list[list[str]]:
    rows = extract_table_rows(section_content)
    if len(rows) <= 1:
        return []

    return rows[1:]


def has_bullets_or_data_rows(section_content: str) -> bool:
    if extract_bullet_values(section_content):
        return True

    return bool(data_table_rows(section_content))


def validate_phase_plan(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues = validate_heading_groups(path, content, PHASE_PLAN_HEADING_GROUPS)

    dependency_map = extract_markdown_section(content, "## Subbundle Dependency Map")
    if dependency_map is not None and "```mermaid" not in dependency_map:
        issues.append(f"{path}: ## Subbundle Dependency Map must include a mermaid diagram")

    for heading in ("## Critical Subbundles", "## Phase Gates"):
        section_content = extract_markdown_section(content, heading)
        if section_content is None:
            continue

        if has_bullets_or_data_rows(section_content):
            continue

        issues.append(f"{path}: {heading} must include at least one markdown bullet or populated markdown table")

    return issues


def validate_assumptions_and_risks(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues = validate_heading_groups(path, content, ASSUMPTIONS_AND_RISKS_HEADING_GROUPS)

    for heading_group in ASSUMPTIONS_AND_RISKS_HEADING_GROUPS:
        heading = find_present_heading(content, heading_group)
        if heading is None:
            continue

        section_content = extract_markdown_section(content, heading)
        if section_content is None:
            continue

        if extract_bullet_values(section_content):
            continue

        issues.append(f"{path}: {heading} must include at least one markdown bullet")

    return issues


def extract_first_status_value(content: str) -> str | None:
    status_section = extract_markdown_section(content, "## Status")
    if status_section is None:
        return None

    values = extract_bullet_values(status_section)
    if not values:
        return None

    first_value = values[0]
    if ":" in first_value:
        _, first_value = first_value.split(":", 1)

    return normalize_markdown_value(first_value)


def validate_completed_root_readme(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues: list[str] = []

    if "- Bundle preparation status: `Draft`" in content:
        issues.append(f"{path}: final closure cannot leave bundle preparation status as `Draft`")

    if "- Bundle readiness gate: `Not run`" in content:
        issues.append(f"{path}: final closure cannot leave bundle readiness gate as `Not run`")

    if "- Execution status: `Not started`" in content:
        issues.append(f"{path}: final closure cannot leave execution status as `Not started`")

    if "- Subbundle gate review: `Not started`" in content:
        issues.append(f"{path}: final closure cannot leave subbundle gate review as `Not started`")

    if "- Final closure gate: `Not started`" in content:
        issues.append(f"{path}: final closure cannot leave final closure gate as `Not started`")

    if "- Final closure gate: `Not run`" in content:
        issues.append(f"{path}: final closure cannot leave final closure gate as `Not run`")

    if "- Browser validation analytics: `Not started`" in content:
        issues.append(f"{path}: final closure cannot leave browser validation analytics as `Not started`")

    return issues


def validate_completed_subbundles(subbundle_directories: list[Path]) -> list[str]:
    issues: list[str] = []

    for subbundle_directory in subbundle_directories:
        readme_path = subbundle_directory / "README.md"
        if not readme_path.is_file():
            continue

        content = readme_path.read_text(encoding="utf-8")
        status = extract_first_status_value(content)
        if status is None:
            issues.append(f"{readme_path}: final closure requires an explicit subbundle status bullet")
            continue

        if status in FINAL_ALLOWED_SUBBUNDLE_STATUSES:
            continue

        issues.append(f"{readme_path}: final closure requires status `Completed` or `Blocked`, found `{status}`")

    return issues


def validate_completed_execution_report(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    issues = validate_no_machine_specific_artifact_paths(path, content)

    report_status = extract_first_status_value(content)
    if contains_pending_marker(report_status):
        issues.append(f"{path}: final closure cannot leave execution report status as `{report_status}`")

    gate_section = extract_markdown_section(content, "## Subbundle Gate Results")
    if gate_section is not None:
        gate_rows = data_table_rows(gate_section)
        if not gate_rows:
            issues.append(f"{path}: final closure requires at least one populated subbundle gate result row")
        else:
            for row in gate_rows:
                if len(row) < 6:
                    issues.append(f"{path}: subbundle gate result row is incomplete: {' | '.join(row)}")
                    continue

                for index in (1, 2, 3, 4):
                    if contains_pending_marker(row[index]):
                        issues.append(f"{path}: subbundle gate result cannot remain pending: {' | '.join(row)}")
                        break

    browser_section = extract_markdown_section(content, "## Browser Validation Analytics")
    if browser_section is not None:
        browser_rows = data_table_rows(browser_section)
        if not browser_rows:
            issues.append(f"{path}: final closure requires at least one populated browser validation analytics row")
        else:
            for row in browser_rows:
                if len(row) < 6:
                    issues.append(f"{path}: browser validation row is incomplete: {' | '.join(row)}")
                    continue

                if contains_pending_marker(row[5]):
                    issues.append(f"{path}: browser validation result cannot remain pending: {' | '.join(row)}")

    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is not None:
        raw_note_rows = data_table_rows(raw_note_section)
        if not raw_note_rows:
            issues.append(f"{path}: final closure requires at least one populated raw note closure row")
        else:
            for row in raw_note_rows:
                if len(row) < 3:
                    issues.append(f"{path}: raw note closure row is incomplete: {' | '.join(row)}")
                    continue

                if contains_pending_marker(row[1]) or contains_pending_marker(row[2]):
                    issues.append(f"{path}: raw note cannot remain pending at final closure: {' | '.join(row)}")

    return issues


def extract_critical_subbundle_numbers(phase_plan_path: Path) -> set[str]:
    content = phase_plan_path.read_text(encoding="utf-8")
    critical_section = extract_markdown_section(content, "## Critical Subbundles")
    if critical_section is None:
        return set()

    return {
        f"{int(match.group(1)):02d}"
        for match in re.finditer(r"\bSB\s*0*(\d+)\b", critical_section, re.IGNORECASE)
    }


def extract_subbundle_number(subbundle_directory: Path) -> str | None:
    match = re.match(r"^(\d{2})-", subbundle_directory.name)
    if match is None:
        return None

    return match.group(1)


def extract_semantic_evidence_section(content: str, subbundle_number: str) -> str | None:
    heading_candidates = [
        f"## SB{subbundle_number} Semantic Adequacy Evidence",
        f"## Subbundle {subbundle_number} Semantic Adequacy Evidence",
        f"## {subbundle_number} Semantic Adequacy Evidence",
    ]

    for heading in heading_candidates:
        section = extract_markdown_section(content, heading)
        if section is not None:
            return section

    return None


def extract_labeled_bullet_values(section_content: str) -> dict[str, str]:
    values: dict[str, str] = {}

    for line in section_content.splitlines():
        stripped = line.strip()
        if not stripped.startswith("- "):
            continue

        bullet = stripped[2:].strip()
        if ":" not in bullet:
            continue

        label, value = bullet.split(":", 1)
        values[label.strip().lower()] = value.strip()

    return values


def is_meaningful_proof_value(value: str) -> bool:
    normalized = normalize_markdown_value(value).strip()
    if contains_pending_marker(normalized):
        return False

    lowered = normalized.lower().strip(". ")
    if lowered in WEAK_PROOF_VALUES:
        return False

    return True


def requires_production_behavior_matrix(content: str) -> bool:
    if PRODUCTION_ARTIFACT_KIND_PATTERN.search(content) is not None:
        return True

    return PRODUCTION_ARTIFACT_IDENTIFIER_PATTERN.search(content) is not None


def validate_production_behavior_matrix(path: Path, content: str) -> list[str]:
    section = extract_markdown_section(content, PRODUCTION_ARTIFACT_MATRIX_HEADING)
    if section is None:
        return [f"{path}: production behavior artifacts require {PRODUCTION_ARTIFACT_MATRIX_HEADING}"]

    rows = extract_table_rows(section)
    if len(rows) < 2:
        return [f"{path}: {PRODUCTION_ARTIFACT_MATRIX_HEADING} must include a populated markdown table"]

    headers = [normalize_production_matrix_header(header) for header in rows[0]]
    issues: list[str] = []
    missing_columns = [column for column in PRODUCTION_ARTIFACT_MATRIX_COLUMNS if column not in headers]
    if missing_columns:
        issues.append(
            f"{path}: {PRODUCTION_ARTIFACT_MATRIX_HEADING} is missing columns: {', '.join(missing_columns)}"
        )
        return issues

    for row in rows[1:]:
        if len(row) < len(headers):
            issues.append(f"{path}: production behavior artifact matrix row is incomplete: {' | '.join(row)}")
            continue

        row_by_header = {
            header: normalize_markdown_value(row[index]).strip()
            for index, header in enumerate(headers)
            if index < len(row)
        }
        artifact = row_by_header.get("artifact", "")
        if not is_meaningful_proof_value(artifact):
            issues.append(f"{path}: production behavior artifact matrix row is missing an artifact name")

        for column in PRODUCTION_ARTIFACT_MATRIX_PROOF_COLUMNS:
            value = row_by_header.get(column, "")
            if not is_meaningful_proof_value(value):
                issues.append(f"{path}: production behavior artifact matrix has weak {column} proof for `{artifact}`")
                continue

            if PROOF_TOKEN_PATTERN.search(value) is None:
                issues.append(
                    f"{path}: production behavior artifact matrix {column} proof for `{artifact}` "
                    "must cite a command, test, file, or proof artifact"
                )

    return issues


def normalize_production_matrix_header(value: str) -> str:
    normalized = value.strip().lower()
    for suffix in (" proof", " citation", " citations", " path", " paths"):
        if normalized.endswith(suffix):
            return normalized[: -len(suffix)]

    return normalized


def validate_dream_meta_text_claims(path: Path, values: dict[str, str]) -> list[str]:
    issues: list[str] = []
    for label in DREAM_META_TEXT_PROHIBITED_LABELS:
        value = values.get(label, "")
        if DREAM_META_TEXT_PATTERN.search(value) is not None:
            issues.append(
                f"{path}: '{label}' treats dream evidence-count template text as shipped synthesis"
            )

    return issues


def extract_capability_claims(content: str) -> set[str]:
    claims: set[str] = set()
    for claim, patterns in CAPABILITY_CLAIM_PATTERNS.items():
        if any(pattern.search(content) is not None for pattern in patterns):
            claims.add(claim)

    return claims


def normalize_capability_claim(value: str) -> str | None:
    normalized = normalize_markdown_value(value).strip().lower()
    normalized = normalized.strip("`* ")

    for claim, patterns in CAPABILITY_CLAIM_PATTERNS.items():
        if normalized == claim.lower():
            return claim

        if any(pattern.search(normalized) is not None for pattern in patterns):
            return claim

    return None


def normalize_claim_matrix_header(value: str) -> str:
    normalized = value.strip().lower()
    for suffix in (" citation", " citations", " path", " paths"):
        if normalized.endswith(suffix):
            return normalized[: -len(suffix)]

    return normalized


def read_resolved_artifact_text(
    path: Path,
    bundle_path: Path,
    repo_root: Path,
    token: str,
) -> tuple[list[str], str]:
    try:
        artifact_path = resolve_artifact_path(bundle_path, repo_root, token)
    except ValueError as exception:
        return [f"{path}: claim-to-code source proof has invalid artifact reference `{token}`: {exception}"], ""

    if not artifact_path.is_file():
        return [f"{path}: claim-to-code source proof references missing file: {token}"], ""

    try:
        return [], artifact_path.read_text(encoding="utf-8", errors="replace")
    except OSError as exception:
        return [f"{path}: claim-to-code source proof cannot read `{token}`: {exception}"], ""


def validate_capability_source_requirements(
    path: Path,
    bundle_path: Path,
    repo_root: Path,
    claim: str,
    source_proof: str,
) -> list[str]:
    issues: list[str] = []
    source_tokens = extract_artifact_path_tokens(source_proof)
    if not source_tokens:
        return [f"{path}: `{claim}` source proof must cite at least one production source file or proof artifact"]

    source_text_parts: list[str] = []
    for source_token in source_tokens:
        token_issues, source_text = read_resolved_artifact_text(path, bundle_path, repo_root, source_token)
        issues.extend(token_issues)
        if source_text:
            source_text_parts.append(source_text)

    source_text = "\n".join(source_text_parts)
    for requirement, tokens in CAPABILITY_SOURCE_REQUIREMENTS.get(claim, ()):
        if any(token in source_text for token in tokens):
            continue

        issues.append(
            f"{path}: `{claim}` source proof does not show {requirement}; "
            f"expected one of {', '.join(tokens)}"
        )

    return issues


def validate_proof_claim_to_code_matrix(
    path: Path,
    content: str,
    required_claims: set[str],
    bundle_path: Path,
    repo_root: Path,
) -> list[str]:
    if not required_claims:
        return []

    section = extract_markdown_section(content, PROOF_CLAIM_TO_CODE_MATRIX_HEADING)
    if section is None:
        return [
            f"{path}: semantic capability claims require {PROOF_CLAIM_TO_CODE_MATRIX_HEADING} "
            f"for {', '.join(sorted(required_claims))}"
        ]

    rows = extract_table_rows(section)
    if len(rows) < 2:
        return [f"{path}: {PROOF_CLAIM_TO_CODE_MATRIX_HEADING} must include a populated markdown table"]

    headers = [normalize_claim_matrix_header(header) for header in rows[0]]
    issues: list[str] = []
    missing_columns = [column for column in PROOF_CLAIM_TO_CODE_MATRIX_COLUMNS if column not in headers]
    if missing_columns:
        issues.append(
            f"{path}: {PROOF_CLAIM_TO_CODE_MATRIX_HEADING} is missing columns: {', '.join(missing_columns)}"
        )
        return issues

    rows_by_claim: dict[str, dict[str, str]] = {}
    for row in rows[1:]:
        if len(row) < len(headers):
            issues.append(f"{path}: proof claim-to-code matrix row is incomplete: {' | '.join(row)}")
            continue

        row_by_header = {
            header: normalize_markdown_value(row[index]).strip()
            for index, header in enumerate(headers)
            if index < len(row)
        }
        raw_claim = row_by_header.get("capability claim", "")
        claim = normalize_capability_claim(raw_claim)
        if claim is None:
            issues.append(f"{path}: proof claim-to-code matrix has unknown capability claim `{raw_claim}`")
            continue

        rows_by_claim[claim] = row_by_header

    for claim in sorted(required_claims):
        row = rows_by_claim.get(claim)
        if row is None:
            issues.append(f"{path}: proof claim-to-code matrix is missing `{claim}`")
            continue

        source_proof = row.get("required production source proof", "")
        test_proof = row.get("required test proof", "")
        negative_fixture = row.get("required negative fixture", "")
        result = row.get("result", "")

        for label, value in (
            ("required production source proof", source_proof),
            ("required test proof", test_proof),
            ("required negative fixture", negative_fixture),
            ("result", result),
        ):
            if is_meaningful_proof_value(value):
                continue

            issues.append(f"{path}: `{claim}` proof claim-to-code matrix has weak {label}: {value}")

        if source_proof and PROOF_TOKEN_PATTERN.search(source_proof) is None:
            issues.append(f"{path}: `{claim}` source proof must cite a command, test, file, or proof artifact")

        if test_proof and PROOF_TOKEN_PATTERN.search(test_proof) is None:
            issues.append(f"{path}: `{claim}` test proof must cite a command, test, file, or proof artifact")

        if negative_fixture and PROOF_TOKEN_PATTERN.search(negative_fixture) is None:
            issues.append(f"{path}: `{claim}` negative fixture must cite a command, test, file, or proof artifact")

        if negative_fixture and not re.search(r"\b(fail|reject|negative|adversarial)\b", negative_fixture, re.IGNORECASE):
            issues.append(f"{path}: `{claim}` negative fixture must prove a failing or rejected shallow case")

        if result and not re.search(r"\b(pass|verified|satisfied|closed|complete)\b", result, re.IGNORECASE):
            issues.append(f"{path}: `{claim}` result must state a passing or verified outcome")

        issues.extend(validate_capability_source_requirements(path, bundle_path, repo_root, claim, source_proof))

    return issues


def validate_no_machine_specific_artifact_paths(path: Path, content: str) -> list[str]:
    issues: list[str] = []
    for line_number, line in enumerate(content.splitlines(), start=1):
        lowered = line.lower()
        if any(marker in lowered for marker in MACHINE_SPECIFIC_PATH_ALLOW_MARKERS):
            continue

        for token in extract_artifact_path_tokens(line):
            if not is_absolute_artifact_path(token):
                continue

            issues.append(
                f"{path}:{line_number}: artifact proof uses machine-specific absolute path "
                f"instead of repo:// or bundle://: {token}"
            )

    return issues


def validate_semantic_evidence_block(path: Path, subbundle_number: str, section_content: str) -> list[str]:
    issues: list[str] = []
    values = extract_labeled_bullet_values(section_content)

    for label in SEMANTIC_PROOF_LABELS:
        key = label.lower()
        value = values.get(key)
        if value is None:
            issues.append(f"{path}: SB{subbundle_number} semantic proof is missing '{label}'")
            continue

        if not is_meaningful_proof_value(value):
            issues.append(f"{path}: SB{subbundle_number} semantic proof has weak '{label}': {value}")

    anti_stub_value = values.get("anti-stub audit", "")
    lowered_anti_stub = anti_stub_value.lower()
    if anti_stub_value and not any(token in lowered_anti_stub for token in ("no", "none", "not ")):
        issues.append(f"{path}: SB{subbundle_number} anti-stub audit must explicitly state no stubs or name a blocker")

    test_proof_value = values.get("test proof", "")
    if test_proof_value and PROOF_TOKEN_PATTERN.search(test_proof_value) is None:
        issues.append(f"{path}: SB{subbundle_number} test proof must cite a command, test, file, or proof artifact")

    issues.extend(validate_dream_meta_text_claims(path, values))

    return issues


def validate_completed_semantic_proof(bundle_path: Path, subbundle_directories: list[Path]) -> list[str]:
    issues: list[str] = []
    phase_plan_path = bundle_path / "plan" / "01-phase-plan.md"
    execution_report_path = bundle_path / "reviews" / "01-execution-report.md"

    if not phase_plan_path.is_file() or not execution_report_path.is_file():
        return issues

    critical_subbundle_numbers = extract_critical_subbundle_numbers(phase_plan_path)
    if not critical_subbundle_numbers:
        issues.append(f"{phase_plan_path}: completed-stage semantic validation could not identify critical SBxx subbundles")
        return issues

    report_content = execution_report_path.read_text(encoding="utf-8")
    for subbundle_directory in subbundle_directories:
        subbundle_number = extract_subbundle_number(subbundle_directory)
        if subbundle_number is None or subbundle_number not in critical_subbundle_numbers:
            continue

        readme_path = subbundle_directory / "README.md"
        if not readme_path.is_file():
            continue

        status = extract_first_status_value(readme_path.read_text(encoding="utf-8"))
        if status != "Completed":
            continue

        section_content = extract_semantic_evidence_section(report_content, subbundle_number)
        if section_content is None:
            issues.append(f"{execution_report_path}: completed critical subbundle SB{subbundle_number} is missing semantic adequacy evidence")
            continue

        issues.extend(validate_semantic_evidence_block(execution_report_path, subbundle_number, section_content))

    return issues


def normalize_artifact_path_token(token: str) -> str:
    return token.strip().strip("`<>\"'").rstrip(".,;:)]}")


def is_absolute_artifact_path(path_value: str) -> bool:
    return WINDOWS_ABSOLUTE_PATTERN.match(path_value) is not None or POSIX_ABSOLUTE_PATTERN.match(path_value) is not None


def resolve_artifact_path(bundle_path: Path, repo_root: Path, path_value: str) -> Path:
    return resolve_reference_path(path_value, bundle_path, repo_root)


def extract_artifact_path_tokens(content: str) -> list[str]:
    tokens: list[str] = []
    seen: set[str] = set()

    for match in ARTIFACT_PATH_PATTERN.finditer(content):
        token = normalize_artifact_path_token(match.group(0))
        if re.search(r"\bSBxx\b", token, re.IGNORECASE):
            continue

        if token in seen:
            continue

        tokens.append(token)
        seen.add(token)

    return tokens


def is_transcript_path(path_value: str) -> bool:
    normalized = path_value.replace("\\", "/").lower()
    return "/transcripts/" in normalized


def extract_labeled_artifact_paths(content: str, labels: tuple[str, ...]) -> list[str]:
    paths: list[str] = []
    lower_labels = tuple(label.lower() for label in labels)

    for line in content.splitlines():
        if line.lstrip().startswith("|"):
            continue

        lowered = line.lower()
        if not any(label in lowered for label in lower_labels):
            continue

        paths.extend(extract_artifact_path_tokens(line))

    return paths


def has_explicit_failing_first_exemption(content: str) -> bool:
    for line in content.splitlines():
        lowered = line.lower()
        if "failing-first" not in lowered and "adversarial negative proof" not in lowered:
            continue

        if "n/a" not in lowered:
            continue

        if any(token in lowered for token in ("process", "non-production", "no behavior", "no production")):
            return True

    return False


def extract_test_names(content: str) -> list[str]:
    names: list[str] = []
    seen: set[str] = set()

    for match in TEST_NAME_PATTERN.finditer(content):
        value = normalize_markdown_value(match.group(1)).strip()
        if not value or value in seen:
            continue

        names.append(value)
        seen.add(value)

    return names


def semantic_invariant_contract_paths(bundle_path: Path, subbundle_number: str) -> list[Path]:
    proof_root = bundle_path / "proof" / f"SB{subbundle_number}"
    return [
        proof_root / "semantic-invariants.json",
        proof_root / "semantic-invariants.md",
    ]


def semantic_invariant_citations(subbundle_number: str) -> list[str]:
    return [
        f"proof/SB{subbundle_number}/semantic-invariants.json",
        f"proof/SB{subbundle_number}/semantic-invariants.md",
        f"bundle://proof/SB{subbundle_number}/semantic-invariants.json",
        f"bundle://proof/SB{subbundle_number}/semantic-invariants.md",
    ]


def extract_markdown_invariant_ids(content: str) -> list[str]:
    ids: list[str] = []
    seen: set[str] = set()
    for match in INVARIANT_ID_PATTERN.finditer(content):
        invariant_id = normalize_markdown_value(match.group(1)).strip()
        if not invariant_id or invariant_id in seen:
            continue

        ids.append(invariant_id)
        seen.add(invariant_id)

    return ids


def validate_markdown_semantic_invariants(path: Path, content: str) -> tuple[list[str], list[str]]:
    issues: list[str] = []
    lowered = content.lower()
    for label in SEMANTIC_INVARIANT_REQUIRED_LABELS:
        if label.lower() not in lowered:
            issues.append(f"{path}: semantic invariant contract is missing '{label}'")

    invariant_ids = extract_markdown_invariant_ids(content)
    if not invariant_ids:
        issues.append(f"{path}: semantic invariant contract must include at least one invariant id")

    issues.extend(validate_dream_meta_text_claims(path, extract_labeled_bullet_values(content)))
    if requires_production_behavior_matrix(content):
        issues.extend(validate_production_behavior_matrix(path, content))

    return issues, invariant_ids


def normalize_json_key(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.lower())


def validate_json_semantic_invariants(path: Path, content: str) -> tuple[list[str], list[str]]:
    issues: list[str] = []
    try:
        data = json.loads(content)
    except json.JSONDecodeError as exception:
        return [f"{path}: semantic invariant JSON is invalid: {exception}"], []

    raw_invariants = data.get("invariants") if isinstance(data, dict) else data
    if isinstance(raw_invariants, dict):
        invariants = [raw_invariants]
    elif isinstance(raw_invariants, list):
        invariants = raw_invariants
    else:
        return [f"{path}: semantic invariant JSON must contain an object, array, or 'invariants' array"], []

    ids: list[str] = []
    for index, invariant in enumerate(invariants):
        if not isinstance(invariant, dict):
            issues.append(f"{path}: invariant at index {index} must be an object")
            continue

        normalized_keys = {normalize_json_key(key): key for key in invariant.keys()}
        for label in SEMANTIC_INVARIANT_REQUIRED_LABELS:
            normalized_label = normalize_json_key(label)
            if normalized_label not in normalized_keys:
                issues.append(f"{path}: invariant at index {index} is missing '{label}'")

        invariant_id = invariant.get("Invariant ID") or invariant.get("invariantId") or invariant.get("id")
        if isinstance(invariant_id, str) and invariant_id.strip():
            ids.append(invariant_id.strip())

        normalized_values = {
            str(key).lower(): str(value)
            for key, value in invariant.items()
            if isinstance(value, (str, int, float, bool))
        }
        issues.extend(validate_dream_meta_text_claims(path, normalized_values))
        serialized_invariant = json.dumps(invariant, ensure_ascii=False)
        if requires_production_behavior_matrix(serialized_invariant):
            matrix = (
                invariant.get("Production behavior artifact matrix")
                or invariant.get("productionBehaviorArtifactMatrix")
                or invariant.get("production_behavior_artifact_matrix")
            )
            if not matrix:
                issues.append(
                    f"{path}: production behavior artifacts require a production behavior artifact matrix"
                )

    if not ids:
        issues.append(f"{path}: semantic invariant JSON must include at least one invariant id")

    return issues, ids


def validate_semantic_invariant_contract(contract_path: Path) -> tuple[list[str], list[str]]:
    content = contract_path.read_text(encoding="utf-8", errors="replace")
    if contract_path.suffix.lower() == ".json":
        return validate_json_semantic_invariants(contract_path, content)

    return validate_markdown_semantic_invariants(contract_path, content)


def validate_transcript(path: Path, expectation: str | None) -> tuple[list[str], str]:
    issues: list[str] = []
    content = path.read_text(encoding="utf-8", errors="replace")

    if COMMAND_FIELD_PATTERN.search(content) is None:
        issues.append(f"{path}: transcript must include 'Command:'")

    exit_codes = [int(match.group(1)) for match in EXIT_CODE_PATTERN.finditer(content)]
    if not exit_codes:
        issues.append(f"{path}: transcript must include an ExitCode or Exit code field")
    elif expectation == "failure" and all(exit_code == 0 for exit_code in exit_codes):
        issues.append(f"{path}: failing-first transcript must contain a non-zero exit code")
    elif expectation == "success" and not any(exit_code == 0 for exit_code in exit_codes):
        issues.append(f"{path}: passing transcript must contain exit code 0")

    return issues, content


def validate_completed_proof_manifests(bundle_path: Path, repo_root: Path, subbundle_directories: list[Path]) -> list[str]:
    issues: list[str] = []
    phase_plan_path = bundle_path / "plan" / "01-phase-plan.md"
    execution_report_path = bundle_path / "reviews" / "01-execution-report.md"

    if not phase_plan_path.is_file() or not execution_report_path.is_file():
        return issues

    critical_subbundle_numbers = extract_critical_subbundle_numbers(phase_plan_path)
    if not critical_subbundle_numbers:
        return issues

    report_content = execution_report_path.read_text(encoding="utf-8")
    normalized_report_content = report_content.replace("\\", "/")

    for subbundle_directory in subbundle_directories:
        subbundle_number = extract_subbundle_number(subbundle_directory)
        if subbundle_number is None or subbundle_number not in critical_subbundle_numbers:
            continue

        readme_path = subbundle_directory / "README.md"
        if not readme_path.is_file():
            continue

        readme_content = readme_path.read_text(encoding="utf-8")
        status = extract_first_status_value(readme_content)
        if status != "Completed":
            continue

        manifest_relative = f"proof/SB{subbundle_number}/manifest.md"
        manifest_path = bundle_path / manifest_relative
        manifest_citation = manifest_relative.replace("\\", "/")
        normalized_readme_content = readme_content.replace("\\", "/")

        if manifest_citation not in normalized_report_content and manifest_citation not in normalized_readme_content:
            issues.append(f"{execution_report_path}: completed critical subbundle SB{subbundle_number} must cite {manifest_relative}")

        if not manifest_path.is_file():
            issues.append(f"{manifest_path}: completed critical subbundle SB{subbundle_number} is missing proof manifest")
            continue

        manifest_content = manifest_path.read_text(encoding="utf-8")
        issues.extend(validate_no_machine_specific_artifact_paths(manifest_path, manifest_content))
        if SHA256_PATTERN.search(manifest_content) is None:
            issues.append(f"{manifest_path}: proof manifest must include at least one SHA-256 changed-file hash")

        if not has_portable_reference(manifest_content):
            issues.append(f"{manifest_path}: proof manifest must include at least one portable repo:// or bundle:// reference")

        manifest_ids: set[str] = set()
        invariant_contents: list[str] = []
        production_artifact_matrix_required = requires_production_behavior_matrix(manifest_content)
        invariant_contracts = [candidate for candidate in semantic_invariant_contract_paths(bundle_path, subbundle_number) if candidate.is_file()]
        if not invariant_contracts:
            issues.append(f"{manifest_path}: completed critical subbundle SB{subbundle_number} is missing semantic invariant contract")
        else:
            normalized_manifest_content = manifest_content.replace("\\", "/")
            contract_citations = semantic_invariant_citations(subbundle_number)
            if not any(citation in normalized_report_content for citation in contract_citations):
                issues.append(f"{execution_report_path}: completed critical subbundle SB{subbundle_number} must cite proof/SB{subbundle_number}/semantic-invariants.*")

            if not any(citation in normalized_manifest_content or citation in normalized_readme_content for citation in contract_citations):
                issues.append(f"{manifest_path}: proof manifest or subbundle README must cite proof/SB{subbundle_number}/semantic-invariants.*")

            for invariant_contract in invariant_contracts:
                invariant_content = invariant_contract.read_text(encoding="utf-8", errors="replace")
                invariant_contents.append(invariant_content)
                issues.extend(validate_no_machine_specific_artifact_paths(invariant_contract, invariant_content))
                production_artifact_matrix_required = (
                    production_artifact_matrix_required
                    or requires_production_behavior_matrix(invariant_content)
                )
                contract_issues, invariant_ids = validate_semantic_invariant_contract(invariant_contract)
                issues.extend(contract_issues)
                if not invariant_ids:
                    continue

                manifest_ids.update(invariant_ids)
                break

        if production_artifact_matrix_required:
            issues.extend(validate_production_behavior_matrix(manifest_path, manifest_content))

        semantic_evidence_content = extract_semantic_evidence_section(report_content, subbundle_number) or ""
        capability_claims = extract_capability_claims(
            "\n".join([semantic_evidence_content, manifest_content, *invariant_contents])
        )
        issues.extend(
            validate_proof_claim_to_code_matrix(
                manifest_path,
                manifest_content,
                capability_claims,
                bundle_path,
                repo_root,
            )
        )

        artifact_tokens = extract_artifact_path_tokens(manifest_content)
        for artifact_token in artifact_tokens:
            artifact_path = resolve_artifact_path(bundle_path, repo_root, artifact_token)
            if not artifact_path.exists():
                issues.append(f"{manifest_path}: referenced artifact path does not exist: {artifact_token}")

        transcript_tokens = [token for token in artifact_tokens if is_transcript_path(token)]
        if not transcript_tokens:
            issues.append(f"{manifest_path}: proof manifest must cite at least one command transcript path")

        transcript_contents: list[str] = []
        for transcript_token in transcript_tokens:
            transcript_path = resolve_artifact_path(bundle_path, repo_root, transcript_token)
            if not transcript_path.is_file():
                continue

            transcript_issues, transcript_content = validate_transcript(transcript_path, None)
            issues.extend(transcript_issues)
            transcript_contents.append(transcript_content)

        failing_tokens = [
            token for token in extract_labeled_artifact_paths(
                manifest_content,
                ("failing-first", "adversarial negative proof"),
            )
            if is_transcript_path(token)
        ]
        if not failing_tokens and not has_explicit_failing_first_exemption(manifest_content):
            issues.append(f"{manifest_path}: proof manifest must cite a failing-first transcript or an explicit process/non-production exemption")

        for failing_token in failing_tokens:
            failing_path = resolve_artifact_path(bundle_path, repo_root, failing_token)
            if not failing_path.is_file():
                continue

            transcript_issues, _ = validate_transcript(failing_path, "failure")
            issues.extend(transcript_issues)

        passing_tokens = [
            token for token in extract_labeled_artifact_paths(
                manifest_content,
                ("passing", "semantic positive proof"),
            )
            if is_transcript_path(token)
        ]
        if not passing_tokens:
            issues.append(f"{manifest_path}: proof manifest must cite a passing transcript")

        for passing_token in passing_tokens:
            passing_path = resolve_artifact_path(bundle_path, repo_root, passing_token)
            if not passing_path.is_file():
                continue

            transcript_issues, _ = validate_transcript(passing_path, "success")
            issues.extend(transcript_issues)

        anti_stub_tokens = [
            token for token in extract_labeled_artifact_paths(manifest_content, ("anti-stub",))
            if is_transcript_path(token)
        ]
        if not anti_stub_tokens:
            issues.append(f"{manifest_path}: proof manifest must cite an anti-stub audit transcript")

        combined_transcripts = "\n".join(transcript_contents)
        for invariant_id in manifest_ids:
            if invariant_id not in combined_transcripts:
                issues.append(f"{manifest_path}: invariant id is missing from transcript output: {invariant_id}")

        for test_name in extract_test_names(manifest_content):
            if test_name not in combined_transcripts:
                issues.append(f"{manifest_path}: cited test name is missing from transcript output: {test_name}")

    return issues


def validate_completed_raw_note_proof_depth(path: Path) -> list[str]:
    content = path.read_text(encoding="utf-8")
    raw_note_section = extract_markdown_section(content, "## Raw Note Closure")
    if raw_note_section is None:
        return []

    issues: list[str] = []
    for row in data_table_rows(raw_note_section):
        if len(row) < 3:
            continue

        status = normalize_markdown_value(row[1])
        proof = normalize_markdown_value(row[2])
        if status not in {"Solved", "Partially solved"}:
            continue

        if not is_meaningful_proof_value(proof):
            issues.append(f"{path}: raw note closure has weak proof: {' | '.join(row)}")
            continue

        if PROOF_TOKEN_PATTERN.search(proof) is None:
            issues.append(f"{path}: raw note closure proof must cite a command, test, file, gate row, or proof artifact: {' | '.join(row)}")

    return issues


def main() -> int:
    arguments = parse_arguments()
    bundle_path = Path(arguments.bundle_root or arguments.bundle_path).resolve()
    repo_root = Path(arguments.repo_root).resolve() if arguments.repo_root else discover_repo_root(bundle_path)

    issues: list[str] = []
    if not bundle_path.is_dir():
        print(f"Bundle path does not exist: {bundle_path}")
        return 1

    for missing_path in collect_missing_paths(bundle_path, arguments.profile):
        issues.append(f"Missing required path: {missing_path}")

    root_readme_path = bundle_path / "README.md"
    if root_readme_path.is_file():
        issues.extend(validate_root_readme(root_readme_path, arguments.stage))

    phase_plan_path = bundle_path / "plan" / "01-phase-plan.md"
    if phase_plan_path.is_file():
        issues.extend(validate_phase_plan(phase_plan_path))

    assumptions_and_risks_path = bundle_path / "analysis" / "02-assumptions-and-risks.md"
    if assumptions_and_risks_path.is_file():
        issues.extend(validate_assumptions_and_risks(assumptions_and_risks_path))

    subbundle_directories = sorted(directory for directory in (bundle_path / "subbundles").glob("*") if directory.is_dir())
    if not subbundle_directories:
        issues.append("No subbundle directories found under subbundles/")
    else:
        for subbundle_directory in subbundle_directories:
            subbundle_readme_path = subbundle_directory / "README.md"
            if not subbundle_readme_path.is_file():
                issues.append(f"Missing README.md in {subbundle_directory}")
                continue

            issues.extend(validate_subbundle_readme(subbundle_readme_path, arguments.stage, bundle_path, repo_root))

    execution_report_path = bundle_path / "reviews" / "01-execution-report.md"
    if execution_report_path.is_file():
        issues.extend(validate_execution_report(execution_report_path))

    if arguments.stage == "completed":
        if root_readme_path.is_file():
            issues.extend(validate_completed_root_readme(root_readme_path))

        issues.extend(validate_completed_subbundles(subbundle_directories))

        if execution_report_path.is_file():
            issues.extend(validate_completed_execution_report(execution_report_path))
            issues.extend(validate_completed_raw_note_proof_depth(execution_report_path))

        issues.extend(validate_completed_semantic_proof(bundle_path, subbundle_directories))
        issues.extend(validate_completed_proof_manifests(bundle_path, repo_root, subbundle_directories))

    if issues:
        print("Bundle validation failed:")
        for issue in issues:
            print(f"- {issue}")

        return 1

    print(f"Bundle is valid for stage '{arguments.stage}': {bundle_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
