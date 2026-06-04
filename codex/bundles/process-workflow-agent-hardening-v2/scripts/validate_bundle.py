#!/usr/bin/env python3
from pathlib import Path
import argparse
import json
import re
import sys

REQUIRED_ROOTS = [
    'README.md',
    'inputs',
    'analysis',
    'requirements',
    'architecture',
    'plan',
    'traceability',
    'shared-prompts',
    'subbundles',
    'reviews',
]

REQUIRED_SUBBUNDLE_SECTIONS = [
    '## Status',
    '## Objective',
    '## Covered Inputs',
    '## Prerequisites',
    '## Exact Source References',
    '## Deliverables',
    '## Dependency Impact',
    '## Validation Depth',
    '## Implementation Steps',
    '## Scope Exceptions',
    '## Do Not Do',
    '## Acceptance Checklist',
    '## Proof Required',
    '## Browser Validation Logging',
    '## Progression Gate',
    '## Suggested Agent Prompt',
]

PLAN_REQUIRED = [
    '## Subbundle Dependency Map',
    '## Critical Subbundles',
    '## Phase Gates',
    '```mermaid',
]

ANALYSIS_REQUIRED = [
    '## Critical Path Risks',
    '## Validation Risks',
    '## Reopen Triggers',
]


def fail(message: str) -> None:
    print(f'FAIL: {message}')
    sys.exit(1)


def require_path(root: Path, relative: str) -> None:
    if not (root / relative).exists():
        fail(f'Missing required path: {relative}')


def require_contains(path: Path, needles: list[str]) -> None:
    if not path.exists():
        fail(f'Missing required file: {path}')
    text = path.read_text(encoding='utf-8')
    missing = [needle for needle in needles if needle not in text]
    if missing:
        fail(f'{path} is missing required section(s): {missing}')


def read_json(path: Path):
    if not path.exists():
        raise ValueError(f'Missing JSON file: {path}')
    try:
        return json.loads(path.read_text(encoding='utf-8-sig'))
    except json.JSONDecodeError as ex:
        raise ValueError(f'Invalid JSON file {path}: {ex}') from ex


def read_text_if_exists(path: Path) -> str:
    if not path.exists():
        return ''
    return path.read_text(encoding='utf-8', errors='replace')


def value_by_key(data, *keys):
    if not isinstance(data, dict):
        return None

    normalized = {str(key).lower(): value for key, value in data.items()}
    for key in keys:
        value = normalized.get(key.lower())
        if value is not None:
            return value

    return None


def as_list(value):
    if isinstance(value, list):
        return value
    if value is None:
        return []
    return [value]


def contains_manual_transition_bypass(text: str) -> bool:
    if re.search(r'suppressAutomationDispatch\s*=\s*\$?true', text, re.IGNORECASE):
        return True
    if re.search(r'\bTransition-Step\b', text, re.IGNORECASE):
        return True
    return False


def contains_harness_source_generation(text: str) -> bool:
    patterns = [
        r'dotnet\s+new\s+blazor',
        r'New-Generated',
        r'Write-Generated',
        r'Generate-.*App',
        r'function\s+New-.*App',
    ]
    return any(re.search(pattern, text, re.IGNORECASE) for pattern in patterns)


def collect_process_e2e_failures(proof_root: Path, script_paths: list[Path]) -> list[str]:
    failures: list[str] = []
    manifest_path = proof_root / 'manifest.json'

    try:
        manifest = read_json(manifest_path)
    except ValueError as ex:
        return [str(ex)]

    schema = value_by_key(manifest, 'schema')
    if schema != 'candoitall.sb04.realProcessE2E.v1':
        failures.append(f'{manifest_path} has schema {schema!r}; expected candoitall.sb04.realProcessE2E.v1.')

    scenario_entries = as_list(value_by_key(manifest, 'scenarios'))
    scenario_count = value_by_key(manifest, 'scenarioCount')
    if scenario_count != 5:
        failures.append(f'{manifest_path} scenarioCount is {scenario_count!r}; expected 5.')
    if len(scenario_entries) != 5:
        failures.append(f'{manifest_path} contains {len(scenario_entries)} scenario entries; expected 5.')

    for script_path in script_paths:
        text = read_text_if_exists(script_path)
        if not text:
            failures.append(f'Missing process E2E proof script: {script_path}')
            continue
        if contains_manual_transition_bypass(text):
            failures.append(f'{script_path} contains manual transition or suppressAutomationDispatch bypass logic.')
        if contains_harness_source_generation(text):
            failures.append(f'{script_path} appears to generate application source inside the proof harness.')

    if not scenario_entries:
        failures.append(f'{manifest_path} has no scenarios.')
        return failures

    for entry in scenario_entries:
        scenario_key = value_by_key(entry, 'scenarioKey', 'ScenarioKey')
        run_id = value_by_key(entry, 'runId', 'RunId')
        proof_path_value = value_by_key(entry, 'proofPath', 'ProofPath')
        app_path = value_by_key(entry, 'appPath', 'AppPath')

        if not scenario_key:
            failures.append(f'{manifest_path} contains a scenario without scenarioKey.')
            continue
        if not run_id:
            failures.append(f'{scenario_key}: missing process run id in manifest scenario entry.')

        scenario_root = proof_root / 'scenarios' / str(scenario_key)
        if proof_path_value:
            proof_path = Path(str(proof_path_value))
            if proof_path.is_absolute():
                scenario_root = proof_path

        if app_path:
            failures.append(f'{scenario_key}: manifest includes AppPath, which indicates harness-owned app source instead of current-run generated source.')

        if not scenario_root.exists():
            failures.append(f'{scenario_key}: missing scenario proof folder {scenario_root}.')
            continue

        scenario_failures = collect_scenario_e2e_failures(scenario_root, str(scenario_key), str(run_id or ''))
        failures.extend(scenario_failures)

    harness_generation_transcripts = list(proof_root.glob('scenarios/*/command-transcripts/dotnet-new*.txt'))
    if harness_generation_transcripts:
        paths = ', '.join(str(path) for path in harness_generation_transcripts[:5])
        failures.append(f'Proof contains harness app-scaffold transcripts: {paths}.')

    return failures


def collect_scenario_e2e_failures(scenario_root: Path, scenario_key: str, run_id: str) -> list[str]:
    failures: list[str] = []

    process_detail_path = scenario_root / 'process-run-detail.json'
    execution_runs_path = scenario_root / 'agent-execution-runs.json'
    receipts_path = scenario_root / 'tool-receipts.json'
    usage_path = scenario_root / 'usage-summary.json'
    generated_root_path = scenario_root / 'generated-source-root.json'
    layout_path = scenario_root / 'generated-source-root-layout.json'
    browser_summary_path = scenario_root / 'browser' / 'browser-validation-summary.json'
    build_transcript_path = scenario_root / 'command-transcripts' / 'dotnet-build-generated-app.txt'

    for required in [
        process_detail_path,
        execution_runs_path,
        receipts_path,
        usage_path,
        generated_root_path,
        layout_path,
        browser_summary_path,
        build_transcript_path,
    ]:
        if not required.exists():
            failures.append(f'{scenario_key}: missing required proof file {required}.')

    execution_runs = []
    if execution_runs_path.exists():
        try:
            execution_payload = read_json(execution_runs_path)
            if isinstance(execution_payload, dict):
                execution_runs = as_list(value_by_key(execution_payload, 'executionRuns'))
                note = str(value_by_key(execution_payload, 'note') or '')
                if re.search(r'no candoitall .*provider execution runs|manual process transitions|automation dispatch suppressed', note, re.IGNORECASE):
                    failures.append(f'{scenario_key}: execution run proof explicitly says provider execution runs are absent or manually bypassed.')
            else:
                execution_runs = as_list(execution_payload)
        except ValueError as ex:
            failures.append(f'{scenario_key}: {ex}')

    if not execution_runs:
        failures.append(f'{scenario_key}: agent-execution-runs.json has no execution runs.')

    execution_run_ids: set[str] = set()
    requested_by_dispatch = False
    for item in execution_runs:
        run = value_by_key(item, 'run') if isinstance(item, dict) else None
        if run is None and isinstance(item, dict):
            run = item
        if not isinstance(run, dict):
            failures.append(f'{scenario_key}: execution run entry is missing a run object.')
            continue

        execution_run_id = value_by_key(run, 'id')
        process_run_id = value_by_key(run, 'processRunId')
        requested_by = value_by_key(run, 'requestedBy')
        source_kind = value_by_key(run, 'sourceKind')

        if execution_run_id:
            execution_run_ids.add(str(execution_run_id))
        else:
            failures.append(f'{scenario_key}: execution run entry is missing id.')

        if run_id and process_run_id != run_id:
            failures.append(f'{scenario_key}: execution run {execution_run_id} is bound to processRunId {process_run_id!r}, expected {run_id!r}.')

        if requested_by == 'process-automation-dispatch' and source_kind == 'process-step':
            requested_by_dispatch = True

    if execution_runs and not requested_by_dispatch:
        failures.append(f'{scenario_key}: execution runs are not bound to process-automation-dispatch process-step execution.')

    if receipts_path.exists():
        try:
            receipts = as_list(read_json(receipts_path))
            if not receipts:
                failures.append(f'{scenario_key}: tool-receipts.json is empty.')
            receipt_execution_ids = {
                str(value_by_key(receipt, 'executionRunId'))
                for receipt in receipts
                if isinstance(receipt, dict) and value_by_key(receipt, 'executionRunId')
            }
            if execution_run_ids and not (receipt_execution_ids & execution_run_ids):
                failures.append(f'{scenario_key}: tool receipts are not bound to recorded execution run ids.')
        except ValueError as ex:
            failures.append(f'{scenario_key}: {ex}')

    if usage_path.exists():
        try:
            usage = read_json(usage_path)
            observed = value_by_key(usage, 'canDoItAllProviderUsageObserved')
            observation_count = value_by_key(usage, 'observationCount', 'usageObservationCount')
            provider_response_ids = as_list(value_by_key(usage, 'providerResponseIds'))
            unavailable_reason = value_by_key(usage, 'incompleteUsageReason', 'actualCostSource')
            if observed is not True:
                failures.append(f'{scenario_key}: usage-summary.json does not confirm provider usage was observed. Reason: {unavailable_reason!r}.')
            if not isinstance(observation_count, (int, float)) or observation_count <= 0:
                failures.append(f'{scenario_key}: usage-summary.json has no positive observation count.')
            if not provider_response_ids:
                failures.append(f'{scenario_key}: usage-summary.json has no provider response ids.')
        except ValueError as ex:
            failures.append(f'{scenario_key}: {ex}')

    if generated_root_path.exists():
        try:
            generated_root = read_json(generated_root_path)
            root_value = str(value_by_key(generated_root, 'generatedSourceRoot', 'Root') or '')
            project_value = str(value_by_key(generated_root, 'generatedProjectFile', 'ProjectFile') or '')
            if run_id and run_id.lower() not in root_value.lower():
                failures.append(f'{scenario_key}: generated source root is not bound to current run id {run_id}.')
            if not root_value.lower().endswith(r'\generatedblazorapp'):
                failures.append(f'{scenario_key}: generated source root must end in GeneratedBlazorApp.')
            if not project_value.lower().endswith(r'\generatedblazorapp.csproj'):
                failures.append(f'{scenario_key}: generated project file must be GeneratedBlazorApp.csproj.')
        except ValueError as ex:
            failures.append(f'{scenario_key}: {ex}')

    if layout_path.exists():
        try:
            layout = read_json(layout_path)
            for field in ['misplacedProjectFiles', 'misplacedTestProjectFiles', 'disallowedSourceRootDirectories', 'disallowedProjectProperties']:
                values = as_list(value_by_key(layout, field))
                if values:
                    failures.append(f'{scenario_key}: generated source root layout has {field}: {values!r}.')
            source_root = str(value_by_key(layout, 'sourceRoot') or '')
            expected_root = str(value_by_key(layout, 'expectedSourceRoot') or '')
            project_relative_path = str(value_by_key(layout, 'projectRelativePath') or '')
            if source_root != expected_root:
                failures.append(f'{scenario_key}: sourceRoot does not exactly match expectedSourceRoot.')
            if project_relative_path != 'GeneratedBlazorApp.csproj':
                failures.append(f'{scenario_key}: runnable project is not directly under GeneratedBlazorApp.')
        except ValueError as ex:
            failures.append(f'{scenario_key}: {ex}')

    if browser_summary_path.exists():
        try:
            browser_rows = as_list(read_json(browser_summary_path))
            viewports = {str(value_by_key(row, 'Viewport', 'viewport')).lower() for row in browser_rows if isinstance(row, dict)}
            if viewports != {'desktop', 'mobile'}:
                failures.append(f'{scenario_key}: browser summary must contain exactly desktop and mobile rows.')
            for row in browser_rows:
                failed_response_count = value_by_key(row, 'FailedResponseCount', 'failedResponseCount') or 0
                failed_request_count = value_by_key(row, 'FailedRequestCount', 'failedRequestCount') or 0
                blocking_console_error_count = value_by_key(row, 'BlockingConsoleErrorCount', 'blockingConsoleErrorCount') or 0
                blocking_page_error_count = value_by_key(row, 'BlockingPageErrorCount', 'blockingPageErrorCount') or 0
                body_text_length = value_by_key(row, 'BodyTextLength', 'bodyTextLength') or 0
                interactive_control_count = value_by_key(row, 'InteractiveControlCount', 'interactiveControlCount') or 0
                if failed_response_count or failed_request_count or blocking_console_error_count or blocking_page_error_count:
                    failures.append(f'{scenario_key}: browser row contains blocking failures: {row!r}.')
                if body_text_length <= 0 or interactive_control_count <= 0:
                    failures.append(f'{scenario_key}: browser row does not prove rendered interactive UI: {row!r}.')
        except ValueError as ex:
            failures.append(f'{scenario_key}: {ex}')

    if build_transcript_path.exists():
        build_text = read_text_if_exists(build_transcript_path)
        if 'ExitCode: 0' not in build_text:
            failures.append(f'{scenario_key}: generated-app build transcript did not exit 0.')
        if not re.search(r'\b0\s+(warnings?|upozorn)', build_text, re.IGNORECASE):
            failures.append(f'{scenario_key}: generated-app build transcript does not prove zero warnings.')

    return failures


def validate_process_e2e_proof(proof_root: Path, script_paths: list[Path]) -> None:
    failures = collect_process_e2e_failures(proof_root, script_paths)
    if failures:
        print('FAIL: process E2E proof quality check failed:')
        for failure in failures:
            print(f'- {failure}')
        sys.exit(1)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', choices=['prepared', 'completed'], default='prepared')
    parser.add_argument('--root', default='.')
    parser.add_argument('--check-process-e2e-proof', action='store_true')
    parser.add_argument('--process-e2e-proof', default=None)
    parser.add_argument('--process-e2e-script', action='append', default=[])
    args = parser.parse_args()

    root = Path(args.root).resolve()

    if args.check_process_e2e_proof:
        proof_root = Path(args.process_e2e_proof or root).resolve()
        script_paths = [Path(path).resolve() for path in args.process_e2e_script]
        validate_process_e2e_proof(proof_root, script_paths)
        print(f'PASS: process E2E proof quality check succeeded for {proof_root}')
        return

    for relative in REQUIRED_ROOTS:
        require_path(root, relative)

    require_contains(root / 'plan' / '01-phase-plan.md', PLAN_REQUIRED)
    require_contains(root / 'analysis' / '02-assumptions-and-risks.md', ANALYSIS_REQUIRED)

    subbundle_roots = sorted((root / 'subbundles').glob('SB*'))
    if len(subbundle_roots) < 1:
        fail('No subbundles found.')

    for subbundle in subbundle_roots:
        readme = subbundle / 'README.md'
        require_contains(readme, REQUIRED_SUBBUNDLE_SECTIONS)

    if args.stage == 'completed':
        proof_root = root / 'proof'
        if not proof_root.exists():
            fail('Completed validation requires proof directory.')
        for subbundle in subbundle_roots:
            sb_id = subbundle.name.split('-', 1)[0]
            require_path(root, f'proof/{sb_id}/manifest.md')
            require_path(root, f'proof/{sb_id}/semantic-invariants.md')
        validate_process_e2e_proof(root / 'proof' / 'SB04', [root / 'scripts' / 'run_sb04_real_process_e2e.ps1'])

    print(f'PASS: bundle validation succeeded for stage {args.stage}')


if __name__ == '__main__':
    main()
