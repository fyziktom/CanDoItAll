#!/usr/bin/env python3
"""Validate campaign bookkeeping and optional evidence hashes, not execution authenticity."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from typing import Any

STATUSES = {'PASSED', 'FAILED', 'BLOCKED', 'REHEARSAL', 'NOT_RUN'}
MODES = {'static', 'deterministic-owner', 'production-ui', 'live-agent'}
EXECUTIONS = {'executed', 'not-run', 'rehearsal', 'live'}
HEX40 = re.compile(r'[0-9a-f]{40}')
HEX64 = re.compile(r'[0-9a-f]{64}')


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def integer(value: Any, message: str, minimum: int = 0) -> int:
    require(type(value) is int and value >= minimum, message)
    return value


def text(value: Any, message: str) -> str:
    require(isinstance(value, str) and bool(value.strip()), message)
    return value


def evidence(item: dict[str, Any], root: Path | None) -> str:
    kind = text(item.get('kind'), 'Evidence kind is required.')
    name = text(item.get('path'), 'Evidence path is required.')
    path = PurePosixPath(name)
    require(not path.is_absolute() and '..' not in path.parts and '\\' not in name
            and ':' not in name and name == path.as_posix(), f'Unsafe evidence path: {name}')
    digest = item.get('sha256', '')
    require(isinstance(digest, str) and HEX64.fullmatch(digest) is not None,
            f'Invalid evidence hash: {name}')
    if root is not None:
        target = root.joinpath(*path.parts)
        current = root
        for part in path.parts:
            current = current / part
            require(not current.is_symlink(), f'Symlink evidence is not accepted: {name}')
        require(target.resolve().is_relative_to(root), f'Evidence escapes root: {name}')
        require(target.is_file(), f'Missing evidence: {name}')
        require(hashlib.sha256(target.read_bytes()).hexdigest() == digest, f'Evidence hash mismatch: {name}')
    return kind


def validate(plan: dict[str, Any], results: dict[str, Any], root: Path | None = None,
             require_complete: bool = False) -> dict[str, Any]:
    require(plan.get('schema_version') == results.get('schema_version') == 1, 'Unsupported schema version.')
    require(plan.get('campaign_id') == results.get('campaign_id') and bool(plan.get('campaign_id')),
            'Campaign IDs differ or are missing.')
    root = root.resolve() if root is not None else None
    require(not require_complete or root is not None, 'Complete validation requires an evidence root.')
    for field in ('workspace_ui_complete', 'application_regression_ready'):
        require(type(results.get(field)) is bool, f'{field} must be a boolean.')
    expected: dict[str, dict[str, Any]] = {}
    for case in plan.get('cases', []):
        cid = text(case.get('id'), 'Case ID required.')
        require(cid not in expected, f'Duplicate plan case: {cid}')
        require(case.get('required_mode') in MODES, f'Unknown required mode: {cid}')
        for field in ('required', 'needs_ui', 'needs_owner'):
            require(type(case.get(field)) is bool, f'Invalid plan boolean {field}: {cid}')
        expected[cid] = case
    require(bool(expected), 'Plan has no cases.')
    actual: dict[str, dict[str, Any]] = {}
    for case in results.get('cases', []):
        cid = text(case.get('id'), 'Result case ID required.')
        require(cid not in actual, f'Duplicate result case: {cid}')
        actual[cid] = case
    require(set(actual) == set(expected), 'Result case inventory differs from sealed plan.')
    accepted = results.get('accepted_checkpoints')
    require(isinstance(accepted, list) and all(isinstance(x, str) and HEX64.fullmatch(x) for x in accepted),
            'Accepted checkpoints must be SHA-256 values.')
    require(len(accepted) == len(set(accepted)), 'Duplicate accepted checkpoint.')
    budget = plan.get('live_budget', {})
    per_execution = integer(budget.get('maximum_requests_per_execution'), 'Invalid per-execution budget.', 1)
    total_budget = integer(budget.get('maximum_requests_total'), 'Invalid total budget.', 1)
    statuses: dict[str, str] = {}
    attempt_ids: set[str] = set()
    total_requests = 0
    artifacts = 0
    for cid, row in actual.items():
        attempts = row.get('attempts')
        require(isinstance(attempts, list), f'Attempts must be a list: {cid}')
        statuses[cid] = 'NOT_RUN'
        for index, attempt in enumerate(attempts):
            require(isinstance(attempt, dict), f'Invalid attempt: {cid}')
            aid = text(attempt.get('attempt_id'), f'Attempt ID required: {cid}')
            require(aid not in attempt_ids, f'Duplicate attempt ID: {aid}')
            attempt_ids.add(aid)
            status = attempt.get('status')
            require(status in STATUSES, f'Unknown status: {cid}')
            mode = attempt.get('mode')
            require(mode in MODES, f'Unknown mode: {cid}')
            execution = attempt.get('execution')
            require(execution in EXECUTIONS, f'Unknown execution: {cid}')
            if status != 'PASSED':
                text(attempt.get('reason'), f'Non-passing attempt needs a reason: {cid}')
            if execution not in {'not-run'}:
                commit = attempt.get('source_commit', '')
                checkpoint = attempt.get('checkpoint_sha256', '')
                require(isinstance(commit, str) and HEX40.fullmatch(commit) is not None,
                        f'Actual source commit required: {cid}')
                require(isinstance(checkpoint, str) and HEX64.fullmatch(checkpoint) is not None,
                        f'Actual source checkpoint required: {cid}')
                text(attempt.get('command'), f'Command or replay procedure required: {cid}')
            proofs = attempt.get('evidence', [])
            require(isinstance(proofs, list), f'Evidence must be a list: {cid}')
            kinds = {evidence(item, root) for item in proofs}
            artifacts += len(proofs)
            count = integer(attempt.get('model_requests', 0), f'Invalid model request count: {cid}')
            total_requests += count
            if count > 0 or execution == 'live':
                require(mode == 'live-agent' and execution == 'live', f'Model calls require live execution: {cid}')
                text(attempt.get('provider'), f'Actual provider required: {cid}')
                text(attempt.get('model'), f'Actual model required: {cid}')
                executions = attempt.get('executions')
                require(isinstance(executions, list) and len(executions) > 0, f'Live execution counters required: {cid}')
                counts = []
                ids: set[str] = set()
                for run in executions:
                    rid = text(run.get('id'), f'Live run identity required: {cid}')
                    require(rid not in ids, f'Duplicate run within attempt: {cid}')
                    ids.add(rid)
                    consumed = integer(run.get('model_requests'), f'Invalid run usage: {cid}')
                    require(consumed <= per_execution, f'Per-execution live budget exceeded: {cid}')
                    counts.append(consumed)
                require(sum(counts) == count, f'Live counters do not reconcile: {cid}')
            if status == 'PASSED':
                spec = expected[cid]
                require(mode == spec['required_mode'], f'Passing mode does not satisfy plan: {cid}')
                require(bool(proofs), f'Passing attempt has no evidence: {cid}')
                if index == len(attempts) - 1:
                    require(attempt.get('checkpoint_sha256') in accepted,
                            f'Latest passing source checkpoint is not accepted: {cid}')
                checks = attempt.get('checks', {})
                require(isinstance(checks, dict), f'Checks must be an object: {cid}')
                if spec['needs_ui']:
                    require(checks.get('ui_driven') is True and 'ui' in kinds, f'Missing real UI evidence: {cid}')
                if spec['needs_owner']:
                    require(checks.get('owner_readback') is True and 'owner' in kinds,
                            f'Missing independent owner evidence: {cid}')
                if mode == 'live-agent':
                    require(execution == 'live' and count > 0 and 'provider' in kinds,
                            f'Passing live case lacks actual model execution: {cid}')
                else:
                    require(execution == 'executed', f'Passing non-live case was not executed: {cid}')
            if status == 'REHEARSAL':
                require(execution == 'rehearsal' and count == 0, f'Rehearsal must not claim model usage: {cid}')
            statuses[cid] = status
    require(total_requests <= total_budget, 'Total live request budget exceeded, including failed retries.')
    incomplete = [cid for cid, spec in expected.items() if spec['required'] and statuses[cid] != 'PASSED']
    workspace_incomplete = [cid for cid in incomplete if cid.startswith('WS-') or cid == 'BASE-01']
    if results['workspace_ui_complete']:
        require(not workspace_incomplete, 'Workspace declared complete with incomplete Workspace cases.')
    if results['application_regression_ready']:
        require(results['workspace_ui_complete'] and not incomplete,
                'Application readiness declared with incomplete proof.')
    if require_complete:
        require(not incomplete, f'Campaign incomplete: {", ".join(incomplete)}')
        require(results['workspace_ui_complete'] and results['application_regression_ready'],
                'Final completion decisions are not both true.')
    return dict(cases=len(expected), attempts=len(attempt_ids), evidence_references=artifacts,
                actual_live_requests=total_requests, incomplete=incomplete, statuses=statuses,
                bookkeeping_complete=not incomplete and results['workspace_ui_complete'] and results['application_regression_ready'])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--evidence-root', type=Path)
    parser.add_argument('--require-complete', action='store_true')
    args = parser.parse_args()
    try:
        summary = validate(json.loads(args.plan.read_text(encoding='utf-8')),
                           json.loads(args.results.read_text(encoding='utf-8')),
                           args.evidence_root, args.require_complete)
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        print(f'FAIL: {error}')
        return 1
    print('BOOKKEEPING VALID: ' + json.dumps(summary, sort_keys=True))
    print('This tool does not establish source authenticity, genuine live usage or product correctness.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
